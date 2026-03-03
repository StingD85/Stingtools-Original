using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;
using StingBIM.Data.Parameters;

namespace StingBIM.Data.Schedules
{
    /// <summary>
    /// Maps parameters to schedule fields
    /// Handles type conversion and field configuration
    /// Provides intelligent field mapping based on parameter types
    /// </summary>
    public class FieldMapper
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<FieldMapper>();
        private readonly Document _document;
        private readonly IParameterRepository _parameterRepository;
        private readonly Dictionary<string, SchedulableField> _fieldCache;
        private readonly object _cacheLock = new object();
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new FieldMapper
        /// </summary>
        /// <param name="document">Revit document</param>
        /// <param name="parameterRepository">Parameter repository for lookups</param>
        public FieldMapper(Document document, IParameterRepository parameterRepository = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _parameterRepository = parameterRepository;
            _fieldCache = new Dictionary<string, SchedulableField>(StringComparer.OrdinalIgnoreCase);
            
            _logger.Info($"FieldMapper initialized for document: {document.Title}");
        }
        
        #endregion

        #region Field Mapping Methods
        
        /// <summary>
        /// Maps a parameter name to a schedulable field
        /// </summary>
        /// <param name="parameterName">Parameter name to map</param>
        /// <param name="schedule">Schedule definition</param>
        /// <returns>SchedulableField or null if not found</returns>
        public SchedulableField MapParameterToField(string parameterName, ScheduleDefinition schedule)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                _logger.Warn("Parameter name is null or empty");
                return null;
            }
            
            // Check cache first
            string cacheKey = $"{schedule.CategoryId.Value}_{parameterName}";
            lock (_cacheLock)
            {
                if (_fieldCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }
            }
            
            try
            {
                // Get all schedulable fields
                var schedulableFields = schedule.GetSchedulableFields();
                
                // Try exact match first
                foreach (var field in schedulableFields)
                {
                    var fieldName = field.GetName(_document);
                    if (fieldName.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        lock (_cacheLock)
                        {
                            _fieldCache[cacheKey] = field;
                        }
                        return field;
                    }
                }
                
                // Try partial match
                foreach (var field in schedulableFields)
                {
                    var fieldName = field.GetName(_document);
                    if (fieldName.Contains(parameterName, StringComparison.OrdinalIgnoreCase) ||
                        parameterName.Contains(fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Debug($"Found partial match: {fieldName} for {parameterName}");
                        lock (_cacheLock)
                        {
                            _fieldCache[cacheKey] = field;
                        }
                        return field;
                    }
                }
                
                _logger.Warn($"No schedulable field found for parameter: {parameterName}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to map parameter: {parameterName}");
                return null;
            }
        }
        
        /// <summary>
        /// Maps multiple parameters to fields
        /// </summary>
        /// <param name="parameterNames">List of parameter names</param>
        /// <param name="schedule">Schedule definition</param>
        /// <returns>Dictionary of parameter names to schedulable fields</returns>
        public Dictionary<string, SchedulableField> MapParametersToFields(
            IEnumerable<string> parameterNames,
            ScheduleDefinition schedule)
        {
            var mappings = new Dictionary<string, SchedulableField>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var paramName in parameterNames)
            {
                var field = MapParameterToField(paramName, schedule);
                if (field != null)
                {
                    mappings[paramName] = field;
                }
            }
            
            _logger.Debug($"Mapped {mappings.Count}/{parameterNames.Count()} parameters to fields");
            return mappings;
        }
        
        /// <summary>
        /// Adds a field to a schedule with configuration
        /// </summary>
        /// <param name="schedule">Schedule definition</param>
        /// <param name="fieldDef">Field definition</param>
        /// <returns>ScheduleField or null if failed</returns>
        public ScheduleField AddFieldToSchedule(
            ScheduleDefinition schedule,
            ScheduleFieldDefinition fieldDef)
        {
            if (fieldDef == null)
            {
                throw new ArgumentNullException(nameof(fieldDef));
            }
            
            try
            {
                // Map parameter to schedulable field
                var schedulableField = MapParameterToField(fieldDef.ParameterName, schedule);
                
                if (schedulableField == null)
                {
                    _logger.Warn($"Cannot add field - schedulable field not found: {fieldDef.ParameterName}");
                    return null;
                }
                
                // Add field to schedule
                var scheduleField = schedule.AddField(schedulableField);
                
                // Configure field
                ConfigureField(scheduleField, fieldDef);
                
                _logger.Debug($"Added field to schedule: {fieldDef.ParameterName}");
                return scheduleField;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to add field: {fieldDef.ParameterName}");
                return null;
            }
        }
        
        /// <summary>
        /// Configures a schedule field with settings from definition
        /// </summary>
        /// <param name="field">Schedule field to configure</param>
        /// <param name="fieldDef">Field definition with configuration</param>
        public void ConfigureField(ScheduleField field, ScheduleFieldDefinition fieldDef)
        {
            if (field == null || fieldDef == null)
                return;
            
            try
            {
                // Set heading
                if (!string.IsNullOrWhiteSpace(fieldDef.Heading))
                {
                    field.ColumnHeading = fieldDef.Heading;
                }
                
                // Set alignment (convert custom enum to Revit 2025 ScheduleHorizontalAlignment)
                field.HorizontalAlignment = (ScheduleHorizontalAlignment)(int)MapAlignment(fieldDef.Alignment);

                // Set width (convert from mm to feet)
                if (fieldDef.Width > 0)
                {
                    field.GridColumnWidth = fieldDef.Width / 304.8; // mm to feet
                }
                
                // Handle calculated fields
                if (fieldDef.IsCalculatedValue && !string.IsNullOrWhiteSpace(fieldDef.Formula))
                {
                    // Note: Formula fields require ScheduleField.IsCalculatedValue = true
                    // and setting the formula through appropriate API
                    _logger.Debug($"Calculated field: {fieldDef.ParameterName}, formula: {fieldDef.Formula}");
                }
                
                _logger.Debug($"Configured field: {field.ColumnHeading}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to configure field: {fieldDef.ParameterName}");
            }
        }
        
        #endregion

        #region Field Discovery
        
        /// <summary>
        /// Gets all available fields for a category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>List of field information</returns>
        public List<FieldInfo> GetAvailableFields(ElementId categoryId)
        {
            var fields = new List<FieldInfo>();
            
            try
            {
                // Create temporary schedule to get available fields
                using (var trans = new Transaction(_document, "Get Fields"))
                {
                    trans.Start();
                    
                    var tempSchedule = ViewSchedule.CreateSchedule(_document, categoryId);
                    var schedulableFields = tempSchedule.Definition.GetSchedulableFields();
                    
                    foreach (var field in schedulableFields)
                    {
                        var fieldInfo = new FieldInfo
                        {
                            ParameterId = field.ParameterId,
                            Name = field.GetName(_document),
                            FieldType = field.FieldType
                        };
                        
                        // Get parameter definition if available
                        if (_parameterRepository != null)
                        {
                            var paramDef = _parameterRepository.GetByName(fieldInfo.Name);
                            if (paramDef != null)
                            {
                                fieldInfo.DataType = paramDef.DataType;
                                fieldInfo.Description = paramDef.Description;
                            }
                        }
                        
                        fields.Add(fieldInfo);
                    }
                    
                    trans.RollBack();
                }
                
                _logger.Debug($"Found {fields.Count} available fields for category {categoryId}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get available fields for category: {categoryId}");
            }
            
            return fields;
        }
        
        /// <summary>
        /// Gets fields filtered by data type
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="dataType">Data type to filter by</param>
        /// <returns>List of matching fields</returns>
        public List<FieldInfo> GetFieldsByDataType(ElementId categoryId, string dataType)
        {
            var allFields = GetAvailableFields(categoryId);
            
            return allFields.Where(f => 
                string.Equals(f.DataType, dataType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        
        /// <summary>
        /// Suggests fields based on schedule purpose
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="schedulePurpose">Purpose keyword (e.g., "material", "dimension", "location")</param>
        /// <returns>List of suggested fields</returns>
        public List<FieldInfo> SuggestFields(ElementId categoryId, string schedulePurpose)
        {
            var allFields = GetAvailableFields(categoryId);
            
            if (string.IsNullOrWhiteSpace(schedulePurpose))
                return allFields;
            
            var purpose = schedulePurpose.ToLowerInvariant();
            
            return allFields.Where(f =>
                f.Name.ToLowerInvariant().Contains(purpose) ||
                (f.Description != null && f.Description.ToLowerInvariant().Contains(purpose)))
                .ToList();
        }
        
        #endregion

        #region Type Conversion
        
        /// <summary>
        /// Maps HorizontalAlignmentStyle (pass-through for API compatibility)
        /// </summary>
        private HorizontalAlignmentStyle MapAlignment(HorizontalAlignmentStyle alignment)
        {
            return alignment;
        }
        
        /// <summary>
        /// Determines optimal alignment based on data type
        /// </summary>
        /// <param name="dataType">Parameter data type</param>
        /// <returns>Recommended alignment</returns>
        public HorizontalAlignmentStyle GetRecommendedAlignment(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                return HorizontalAlignmentStyle.Left;
            
            switch (dataType.ToUpperInvariant())
            {
                case "NUMBER":
                case "INTEGER":
                case "LENGTH":
                case "AREA":
                case "VOLUME":
                case "ANGLE":
                case "CURRENCY":
                case "ELECTRICAL_CURRENT":
                case "ELECTRICAL_POTENTIAL":
                case "ELECTRICAL_POWER":
                    return HorizontalAlignmentStyle.Right;
                
                case "YESNO":
                    return HorizontalAlignmentStyle.Center;
                
                case "TEXT":
                case "URL":
                default:
                    return HorizontalAlignmentStyle.Left;
            }
        }
        
        /// <summary>
        /// Gets recommended column width based on data type (in mm)
        /// </summary>
        /// <param name="dataType">Parameter data type</param>
        /// <returns>Recommended width in mm</returns>
        public int GetRecommendedWidth(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                return 100;
            
            switch (dataType.ToUpperInvariant())
            {
                case "YESNO":
                    return 30;
                
                case "INTEGER":
                    return 50;
                
                case "NUMBER":
                case "LENGTH":
                case "AREA":
                case "VOLUME":
                case "ANGLE":
                case "CURRENCY":
                    return 70;
                
                case "ELECTRICAL_CURRENT":
                case "ELECTRICAL_POTENTIAL":
                case "ELECTRICAL_POWER":
                    return 60;
                
                case "TEXT":
                    return 150;
                
                case "URL":
                    return 200;
                
                default:
                    return 100;
            }
        }
        
        #endregion

        #region Cache Management
        
        /// <summary>
        /// Clears the field mapping cache
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _fieldCache.Clear();
                _logger.Debug("Field mapping cache cleared");
            }
        }
        
        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Number of cached mappings</returns>
        public int GetCacheSize()
        {
            lock (_cacheLock)
            {
                return _fieldCache.Count;
            }
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a FieldMapper for the specified document
        /// </summary>
        public static FieldMapper For(Document document)
        {
            return new FieldMapper(document);
        }
        
        /// <summary>
        /// Creates a FieldMapper with parameter repository
        /// </summary>
        public static FieldMapper For(Document document, IParameterRepository parameterRepository)
        {
            return new FieldMapper(document, parameterRepository);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Information about a schedulable field
    /// </summary>
    public class FieldInfo
    {
        /// <summary>
        /// Gets or sets the parameter ID
        /// </summary>
        public ElementId ParameterId { get; set; }
        
        /// <summary>
        /// Gets or sets the field ID
        /// </summary>
        public ScheduleFieldId FieldId { get; set; }
        
        /// <summary>
        /// Gets or sets the field name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the field type
        /// </summary>
        public ScheduleFieldType FieldType { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter data type
        /// </summary>
        public string DataType { get; set; }
        
        /// <summary>
        /// Gets or sets the field description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets whether this is a built-in field
        /// </summary>
        public bool IsBuiltIn => ParameterId.Value < 0;
        
        /// <summary>
        /// Gets whether this is a shared parameter
        /// </summary>
        public bool IsShared => !IsBuiltIn && ParameterId != ElementId.InvalidElementId;
        
        public override string ToString()
        {
            return $"{Name} ({DataType}) - {FieldType}";
        }
    }
    
    /// <summary>
    /// Field mapping result
    /// </summary>
    public class FieldMappingResult
    {
        public bool Success { get; set; }
        public string ParameterName { get; set; }
        public SchedulableField SchedulableField { get; set; }
        public ScheduleField ScheduleField { get; set; }
        public string ErrorMessage { get; set; }
        
        public static FieldMappingResult Successful(
            string parameterName,
            SchedulableField schedulableField,
            ScheduleField scheduleField)
        {
            return new FieldMappingResult
            {
                Success = true,
                ParameterName = parameterName,
                SchedulableField = schedulableField,
                ScheduleField = scheduleField
            };
        }
        
        public static FieldMappingResult Failed(string parameterName, string errorMessage)
        {
            return new FieldMappingResult
            {
                Success = false,
                ParameterName = parameterName,
                ErrorMessage = errorMessage
            };
        }
        
        public override string ToString()
        {
            return Success 
                ? $"Success: {ParameterName}" 
                : $"Failed: {ParameterName} - {ErrorMessage}";
        }
    }
}
