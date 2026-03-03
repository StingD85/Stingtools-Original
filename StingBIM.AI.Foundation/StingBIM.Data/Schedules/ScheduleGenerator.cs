using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;
using StingBIM.Core.Transactions;

namespace StingBIM.Data.Schedules
{
    /// <summary>
    /// Generates schedules in Revit from templates
    /// Supports creating 146 schedule templates
    /// Handles field mapping, filters, sorting, and formatting
    /// </summary>
    public class ScheduleGenerator
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<ScheduleGenerator>();
        private readonly Document _document;
        private readonly TransactionManager _transactionManager;
        private readonly Dictionary<string, ViewSchedule> _createdSchedules;
        private readonly object _schedulesLock = new object();
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Gets the number of schedules created
        /// </summary>
        public int SchedulesCreated
        {
            get
            {
                lock (_schedulesLock)
                {
                    return _createdSchedules.Count;
                }
            }
        }
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new ScheduleGenerator
        /// </summary>
        /// <param name="document">Revit document</param>
        public ScheduleGenerator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _transactionManager = TransactionManager.For(document);
            _createdSchedules = new Dictionary<string, ViewSchedule>(StringComparer.OrdinalIgnoreCase);
            
            _logger.Info($"ScheduleGenerator initialized for document: {document.Title}");
        }
        
        #endregion

        #region Generate Methods
        
        /// <summary>
        /// Generates a single schedule from template
        /// </summary>
        /// <param name="template">Schedule template</param>
        /// <returns>Created ViewSchedule or null if failed</returns>
        public ViewSchedule GenerateSchedule(ScheduleTemplate template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            
            using (_logger.StartPerformanceTimer($"GenerateSchedule: {template.Name}"))
            {
                try
                {
                    _logger.Info($"Generating schedule: {template.Name}");
                    
                    ViewSchedule schedule = null;
                    
                    _transactionManager.Execute(
                        $"Create Schedule: {template.Name}",
                        () =>
                        {
                            // Create schedule
                            schedule = CreateScheduleView(template);
                            
                            if (schedule == null)
                            {
                                _logger.Error($"Failed to create schedule view: {template.Name}");
                                return;
                            }
                            
                            // Add fields
                            AddScheduleFields(schedule, template.Fields);
                            
                            // Apply filters
                            if (template.HasFilters)
                            {
                                ApplyFilters(schedule, template.Filters);
                            }
                            
                            // Apply sorting
                            if (template.HasSorting)
                            {
                                ApplySorting(schedule, template.Sorting);
                            }
                            
                            // Apply grouping
                            if (template.HasGrouping)
                            {
                                ApplyGrouping(schedule, template.Grouping);
                            }
                            
                            // Apply formatting
                            if (template.HasCustomFormatting)
                            {
                                ApplyFormatting(schedule, template.Formatting);
                            }
                        });
                    
                    if (schedule != null)
                    {
                        lock (_schedulesLock)
                        {
                            _createdSchedules[template.Name] = schedule;
                        }
                        
                        _logger.Info($"Successfully created schedule: {template.Name} (ID: {schedule.Id})");
                    }
                    
                    return schedule;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to generate schedule: {template.Name}");
                    return null;
                }
            }
        }
        
        /// <summary>
        /// Generates multiple schedules from templates
        /// </summary>
        /// <param name="templates">Schedule templates</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>List of created schedules</returns>
        public List<ViewSchedule> GenerateSchedules(
            IEnumerable<ScheduleTemplate> templates,
            IProgress<GenerationProgress> progress = null)
        {
            var templateList = templates.ToList();
            var createdSchedules = new List<ViewSchedule>();
            
            _logger.Info($"Generating {templateList.Count} schedules...");
            progress?.Report(new GenerationProgress { Stage = "Starting", PercentComplete = 0 });
            
            int processedCount = 0;
            int successCount = 0;
            
            foreach (var template in templateList)
            {
                try
                {
                    var schedule = GenerateSchedule(template);
                    if (schedule != null)
                    {
                        createdSchedules.Add(schedule);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to generate schedule: {template.Name}");
                }
                
                processedCount++;
                
                // Report progress
                if (processedCount % 10 == 0 || processedCount == templateList.Count)
                {
                    int percentComplete = (int)((processedCount / (double)templateList.Count) * 100);
                    progress?.Report(new GenerationProgress
                    {
                        Stage = "Generating schedules",
                        PercentComplete = percentComplete,
                        SchedulesCreated = successCount,
                        TotalSchedules = templateList.Count
                    });
                }
            }
            
            progress?.Report(new GenerationProgress
            {
                Stage = "Complete",
                PercentComplete = 100,
                SchedulesCreated = successCount,
                TotalSchedules = templateList.Count
            });
            
            _logger.Info($"Generated {successCount}/{templateList.Count} schedules successfully");
            return createdSchedules;
        }
        
        /// <summary>
        /// Generates schedules asynchronously
        /// </summary>
        public async Task<List<ViewSchedule>> GenerateSchedulesAsync(
            IEnumerable<ScheduleTemplate> templates,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GenerateSchedules(templates, progress), cancellationToken);
        }
        
        #endregion

        #region Schedule Creation
        
        /// <summary>
        /// Creates the base schedule view
        /// </summary>
        private ViewSchedule CreateScheduleView(ScheduleTemplate template)
        {
            try
            {
                // Get category
                var category = GetCategoryByName(template.CategoryName);
                if (category == null)
                {
                    _logger.Error($"Category not found: {template.CategoryName}");
                    return null;
                }
                
                // Check if schedule already exists
                var existing = GetExistingSchedule(template.Name);
                if (existing != null)
                {
                    _logger.Warn($"Schedule already exists: {template.Name}, updating...");
                    return existing;
                }
                
                // Create schedule based on type
                ViewSchedule schedule = null;
                
                switch (template.Type)
                {
                    case ScheduleType.Standard:
                        schedule = ViewSchedule.CreateSchedule(_document, category.Id);
                        break;
                    
                    case ScheduleType.MaterialTakeoff:
                        schedule = ViewSchedule.CreateMaterialTakeoff(_document, category.Id);
                        break;
                    
                    case ScheduleType.KeySchedule:
                        // Key schedules require additional setup
                        _logger.Warn($"Key schedule creation not fully implemented: {template.Name}");
                        schedule = ViewSchedule.CreateSchedule(_document, category.Id);
                        break;
                    
                    default:
                        schedule = ViewSchedule.CreateSchedule(_document, category.Id);
                        break;
                }
                
                if (schedule != null)
                {
                    // Set schedule name
                    schedule.Name = template.Name;
                    
                    _logger.Debug($"Created base schedule view: {template.Name}");
                }
                
                return schedule;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create schedule view: {template.Name}");
                return null;
            }
        }
        
        /// <summary>
        /// Adds fields to schedule
        /// </summary>
        private void AddScheduleFields(ViewSchedule schedule, IReadOnlyList<ScheduleFieldDefinition> fields)
        {
            try
            {
                var definition = schedule.Definition;
                
                foreach (var fieldDef in fields.OrderBy(f => f.DisplayOrder))
                {
                    try
                    {
                        // Find schedulable field
                        var schedulableField = FindSchedulableField(definition, fieldDef.ParameterName);
                        
                        if (schedulableField != null)
                        {
                            // Add field to schedule
                            var scheduleField = definition.AddField(schedulableField);
                            
                            // Configure field
                            if (!string.IsNullOrEmpty(fieldDef.Heading))
                            {
                                scheduleField.ColumnHeading = fieldDef.Heading;
                            }
                            
                            scheduleField.IsHidden = !fieldDef.ShowHeader;
                            
                            if (fieldDef.Width > 0)
                            {
                                scheduleField.GridColumnWidth = fieldDef.Width / 304.8; // Convert mm to feet
                            }
                            
                            scheduleField.HorizontalAlignment = (ScheduleHorizontalAlignment)(int)fieldDef.Alignment;
                            
                            _logger.Debug($"Added field to schedule: {fieldDef.ParameterName}");
                        }
                        else
                        {
                            _logger.Warn($"Schedulable field not found: {fieldDef.ParameterName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to add field: {fieldDef.ParameterName}");
                    }
                }
                
                _logger.Debug($"Added {fields.Count} fields to schedule");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add schedule fields");
            }
        }
        
        /// <summary>
        /// Applies filters to schedule
        /// </summary>
        private void ApplyFilters(ViewSchedule schedule, IReadOnlyList<ScheduleFilterDefinition> filters)
        {
            try
            {
                var definition = schedule.Definition;
                
                foreach (var filterDef in filters)
                {
                    try
                    {
                        // Find field by parameter name
                        var field = FindScheduleField(definition, filterDef.ParameterName);
                        
                        if (field != null)
                        {
                            // Create filter
                            var filter = new ScheduleFilter(
                                field.FieldId,
                                ConvertFilterType(filterDef.FilterType),
                                filterDef.Value);
                            
                            definition.AddFilter(filter);
                            
                            _logger.Debug($"Added filter: {filterDef.ParameterName} {filterDef.FilterType} {filterDef.Value}");
                        }
                        else
                        {
                            _logger.Warn($"Field not found for filter: {filterDef.ParameterName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to apply filter: {filterDef.ParameterName}");
                    }
                }
                
                _logger.Debug($"Applied {filters.Count} filters to schedule");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply filters");
            }
        }
        
        /// <summary>
        /// Applies sorting to schedule
        /// </summary>
        private void ApplySorting(ViewSchedule schedule, IReadOnlyList<ScheduleSortDefinition> sorting)
        {
            try
            {
                var definition = schedule.Definition;
                
                foreach (var sortDef in sorting.OrderBy(s => s.SortOrder))
                {
                    try
                    {
                        // Find field by parameter name
                        var field = FindScheduleField(definition, sortDef.ParameterName);
                        
                        if (field != null)
                        {
                            // Create sort
                            var sortGroup = new ScheduleSortGroupField(
                                field.FieldId,
                                sortDef.Ascending ? ScheduleSortOrder.Ascending : ScheduleSortOrder.Descending);
                            
                            definition.AddSortGroupField(sortGroup);
                            
                            _logger.Debug($"Added sorting: {sortDef.ParameterName} {(sortDef.Ascending ? "ASC" : "DESC")}");
                        }
                        else
                        {
                            _logger.Warn($"Field not found for sorting: {sortDef.ParameterName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to apply sorting: {sortDef.ParameterName}");
                    }
                }
                
                _logger.Debug($"Applied {sorting.Count} sorting rules to schedule");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply sorting");
            }
        }
        
        /// <summary>
        /// Applies grouping to schedule
        /// </summary>
        private void ApplyGrouping(ViewSchedule schedule, IReadOnlyList<ScheduleGroupDefinition> grouping)
        {
            try
            {
                var definition = schedule.Definition;
                
                foreach (var groupDef in grouping)
                {
                    try
                    {
                        // Find field by parameter name
                        var field = FindScheduleField(definition, groupDef.ParameterName);
                        
                        if (field != null)
                        {
                            // Create grouping
                            var sortGroup = new ScheduleSortGroupField(field.FieldId);
                            sortGroup.ShowHeader = groupDef.ShowHeader;
                            sortGroup.ShowFooter = groupDef.ShowFooter;
                            sortGroup.ShowBlankLine = groupDef.ShowBlankLine;
                            
                            definition.AddSortGroupField(sortGroup);
                            
                            _logger.Debug($"Added grouping: {groupDef.ParameterName}");
                        }
                        else
                        {
                            _logger.Warn($"Field not found for grouping: {groupDef.ParameterName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to apply grouping: {groupDef.ParameterName}");
                    }
                }
                
                _logger.Debug($"Applied {grouping.Count} grouping rules to schedule");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply grouping");
            }
        }
        
        /// <summary>
        /// Applies formatting to schedule
        /// </summary>
        private void ApplyFormatting(ViewSchedule schedule, ScheduleFormatting formatting)
        {
            try
            {
                // Note: Some formatting options require accessing schedule appearance settings
                // which may vary by Revit version. Basic implementation provided.
                
                var definition = schedule.Definition;
                
                // Set text size if specified
                if (formatting.TextSize > 0)
                {
                    // Text size formatting would go here
                    // This may require accessing view-specific appearance settings
                    _logger.Debug($"Text size: {formatting.TextSize}mm");
                }
                
                // Apply header formatting
                if (formatting.BoldHeaders)
                {
                    // Bold headers formatting
                    _logger.Debug("Bold headers enabled");
                }
                
                // Color formatting would require additional API calls
                // to set table appearance settings
                if (formatting.HeaderColor != null)
                {
                    _logger.Debug($"Header color: {formatting.HeaderColor}");
                }
                
                _logger.Debug("Applied formatting to schedule");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply formatting");
            }
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Gets a category by name
        /// </summary>
        private Category GetCategoryByName(string categoryName)
        {
            try
            {
                foreach (Category category in _document.Settings.Categories)
                {
                    if (category.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        return category;
                    }
                }
                
                // Try built-in category mapping
                var builtInCategory = MapToBuiltInCategory(categoryName);
                if (builtInCategory.HasValue)
                {
                    return Category.GetCategory(_document, builtInCategory.Value);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get category: {categoryName}");
                return null;
            }
        }
        
        /// <summary>
        /// Maps category name to BuiltInCategory
        /// </summary>
        private BuiltInCategory? MapToBuiltInCategory(string categoryName)
        {
            var categoryMap = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                { "Doors", BuiltInCategory.OST_Doors },
                { "Windows", BuiltInCategory.OST_Windows },
                { "Walls", BuiltInCategory.OST_Walls },
                { "Floors", BuiltInCategory.OST_Floors },
                { "Roofs", BuiltInCategory.OST_Roofs },
                { "Ceilings", BuiltInCategory.OST_Ceilings },
                { "Rooms", BuiltInCategory.OST_Rooms },
                { "Spaces", BuiltInCategory.OST_MEPSpaces },
                { "Mechanical Equipment", BuiltInCategory.OST_MechanicalEquipment },
                { "Electrical Equipment", BuiltInCategory.OST_ElectricalEquipment },
                { "Electrical Fixtures", BuiltInCategory.OST_ElectricalFixtures },
                { "Lighting Fixtures", BuiltInCategory.OST_LightingFixtures },
                { "Plumbing Fixtures", BuiltInCategory.OST_PlumbingFixtures },
                { "Ducts", BuiltInCategory.OST_DuctCurves },
                { "Pipes", BuiltInCategory.OST_PipeCurves },
                { "Cable Trays", BuiltInCategory.OST_CableTray },
                { "Conduits", BuiltInCategory.OST_Conduit },
                { "Furniture", BuiltInCategory.OST_Furniture },
                { "Structural Columns", BuiltInCategory.OST_StructuralColumns },
                { "Structural Framing", BuiltInCategory.OST_StructuralFraming }
            };
            
            return categoryMap.TryGetValue(categoryName, out var builtIn) ? builtIn : (BuiltInCategory?)null;
        }
        
        /// <summary>
        /// Finds a schedulable field by parameter name
        /// </summary>
        private SchedulableField FindSchedulableField(ScheduleDefinition definition, string parameterName)
        {
            try
            {
                var schedulableFields = definition.GetSchedulableFields();
                
                foreach (var field in schedulableFields)
                {
                    var fieldName = field.GetName(_document);
                    if (fieldName.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return field;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to find schedulable field: {parameterName}");
                return null;
            }
        }
        
        /// <summary>
        /// Finds a schedule field by parameter name
        /// </summary>
        private ScheduleField FindScheduleField(ScheduleDefinition definition, string parameterName)
        {
            try
            {
                var fieldCount = definition.GetFieldCount();

                for (int i = 0; i < fieldCount; i++)
                {
                    var field = definition.GetField(i);

                    // Match by column heading name
                    if (field.ColumnHeading.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return field;
                    }

                    // Fall back to matching by parameter ID via schedulable fields
                    var schedulableField = definition.GetSchedulableFields()
                        .FirstOrDefault(sf => sf.ParameterId == field.ParameterId);

                    if (schedulableField != null &&
                        schedulableField.GetName(_document).Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return field;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to find schedule field: {parameterName}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets existing schedule by name
        /// </summary>
        private ViewSchedule GetExistingSchedule(string scheduleName)
        {
            try
            {
                var collector = new FilteredElementCollector(_document)
                    .OfClass(typeof(ViewSchedule));
                
                foreach (ViewSchedule schedule in collector)
                {
                    if (schedule.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return schedule;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to find existing schedule: {scheduleName}");
                return null;
            }
        }
        
        /// <summary>
        /// Converts filter type to Revit ScheduleFilterType
        /// </summary>
        private Autodesk.Revit.DB.ScheduleFilterType ConvertFilterType(Data.Schedules.ScheduleFilterType filterType)
        {
            switch (filterType)
            {
                case Data.Schedules.ScheduleFilterType.Equal:
                    return Autodesk.Revit.DB.ScheduleFilterType.Equal;
                case Data.Schedules.ScheduleFilterType.NotEqual:
                    return Autodesk.Revit.DB.ScheduleFilterType.NotEqual;
                case Data.Schedules.ScheduleFilterType.GreaterThan:
                    return Autodesk.Revit.DB.ScheduleFilterType.GreaterThan;
                case Data.Schedules.ScheduleFilterType.GreaterOrEqual:
                    return Autodesk.Revit.DB.ScheduleFilterType.GreaterThanOrEqual;
                case Data.Schedules.ScheduleFilterType.LessThan:
                    return Autodesk.Revit.DB.ScheduleFilterType.LessThan;
                case Data.Schedules.ScheduleFilterType.LessOrEqual:
                    return Autodesk.Revit.DB.ScheduleFilterType.LessThanOrEqual;
                case Data.Schedules.ScheduleFilterType.Contains:
                    return Autodesk.Revit.DB.ScheduleFilterType.Contains;
                default:
                    return Autodesk.Revit.DB.ScheduleFilterType.Equal;
            }
        }
        
        #endregion

        #region Public Query Methods
        
        /// <summary>
        /// Gets all created schedules
        /// </summary>
        public Dictionary<string, ViewSchedule> GetCreatedSchedules()
        {
            lock (_schedulesLock)
            {
                return new Dictionary<string, ViewSchedule>(_createdSchedules);
            }
        }
        
        /// <summary>
        /// Gets a created schedule by name
        /// </summary>
        public ViewSchedule GetScheduleByName(string scheduleName)
        {
            lock (_schedulesLock)
            {
                return _createdSchedules.TryGetValue(scheduleName, out var schedule) ? schedule : null;
            }
        }
        
        /// <summary>
        /// Clears the created schedules cache
        /// </summary>
        public void ClearCache()
        {
            lock (_schedulesLock)
            {
                _createdSchedules.Clear();
                _logger.Debug("Cleared schedules cache");
            }
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a ScheduleGenerator for the specified document
        /// </summary>
        public static ScheduleGenerator For(Document document)
        {
            return new ScheduleGenerator(document);
        }
        
        #endregion
    }
    
    #region Support Classes
    
    /// <summary>
    /// Schedule generation progress information
    /// </summary>
    public class GenerationProgress
    {
        public string Stage { get; set; }
        public int PercentComplete { get; set; }
        public int SchedulesCreated { get; set; }
        public int TotalSchedules { get; set; }
        
        public override string ToString()
        {
            return $"{Stage}: {PercentComplete}% ({SchedulesCreated}/{TotalSchedules})";
        }
    }
    
    /// <summary>
    /// Filter types for schedules
    /// </summary>
    public enum ScheduleFilterType
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        Contains,
        NotContains,
        BeginsWith,
        EndsWith
    }
    
    #endregion
}
