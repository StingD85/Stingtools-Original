// ScheduleParameterIntegrator.cs
// StingBIM AI - Integration of Schedule Generation with Parameter Auto-Population
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Parameters.AutoPopulation;
using StingBIM.AI.Parameters.Intelligence;

namespace StingBIM.AI.Parameters.Integration
{
    /// <summary>
    /// Integrates schedule generation with parameter auto-population, providing
    /// intelligent field selection, parameter mapping, and value population for schedules.
    /// </summary>
    public class ScheduleParameterIntegrator
    {
        #region Private Fields

        private readonly ParameterAutoPopulator _autoPopulator;
        private readonly ParameterIntelligenceEngine _intelligence;
        private readonly Dictionary<string, ScheduleTemplate> _scheduleTemplates;
        private readonly Dictionary<string, List<ScheduleFieldMapping>> _fieldMappings;
        private readonly Dictionary<string, CategoryScheduleConfig> _categoryConfigs;
        private readonly object _lockObject = new object();
        private bool _isInitialized;

        #endregion

        #region Constructor

        public ScheduleParameterIntegrator()
        {
            _autoPopulator = new ParameterAutoPopulator();
            _intelligence = new ParameterIntelligenceEngine();
            _scheduleTemplates = new Dictionary<string, ScheduleTemplate>(StringComparer.OrdinalIgnoreCase);
            _fieldMappings = new Dictionary<string, List<ScheduleFieldMapping>>(StringComparer.OrdinalIgnoreCase);
            _categoryConfigs = new Dictionary<string, CategoryScheduleConfig>(StringComparer.OrdinalIgnoreCase);

            InitializeDefaultConfigurations();
        }

        #endregion

        #region Public Methods - Initialization

        /// <summary>
        /// Initialize the integrator with parameter and schedule data.
        /// </summary>
        public async Task InitializeAsync(
            string parametersPath,
            string mappingsPath,
            string templatesPath = null,
            CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            // Initialize auto-populator
            await _autoPopulator.InitializeAsync(parametersPath, mappingsPath, cancellationToken);

            // Load schedule templates if provided
            if (!string.IsNullOrEmpty(templatesPath) && Directory.Exists(templatesPath))
            {
                LoadScheduleTemplates(templatesPath);
            }

            _isInitialized = true;
        }

        #endregion

        #region Public Methods - Schedule Configuration

        /// <summary>
        /// Get recommended schedule fields for a category with auto-populate capability info.
        /// </summary>
        public ScheduleFieldRecommendation GetRecommendedFields(
            string category,
            SchedulePurpose purpose = SchedulePurpose.General,
            ParameterContext context = null)
        {
            var recommendation = new ScheduleFieldRecommendation
            {
                Category = category,
                Purpose = purpose
            };

            // Get category configuration
            if (_categoryConfigs.TryGetValue(category, out var config))
            {
                // Add required fields
                foreach (var field in config.RequiredFields)
                {
                    var fieldInfo = CreateFieldInfo(field, category, true);
                    recommendation.RequiredFields.Add(fieldInfo);
                }

                // Add recommended fields based on purpose
                var purposeFields = GetFieldsForPurpose(config, purpose);
                foreach (var field in purposeFields)
                {
                    if (!recommendation.RequiredFields.Any(f => f.ParameterId == field))
                    {
                        var fieldInfo = CreateFieldInfo(field, category, false);
                        recommendation.RecommendedFields.Add(fieldInfo);
                    }
                }
            }

            // Add intelligence-based suggestions
            if (context != null)
            {
                var intelligentSuggestions = _intelligence.GetRecommendedParameters(category, context);
                foreach (var suggestion in intelligentSuggestions)
                {
                    if (!recommendation.RequiredFields.Any(f => f.ParameterId == suggestion.ParameterId) &&
                        !recommendation.RecommendedFields.Any(f => f.ParameterId == suggestion.ParameterId))
                    {
                        var fieldInfo = CreateFieldInfo(suggestion.ParameterId, category, false);
                        fieldInfo.RecommendationReason = suggestion.Reason;
                        recommendation.OptionalFields.Add(fieldInfo);
                    }
                }
            }

            // Calculate auto-populate coverage
            var allFields = recommendation.RequiredFields
                .Concat(recommendation.RecommendedFields)
                .Concat(recommendation.OptionalFields)
                .ToList();

            recommendation.AutoPopulateCoverage = allFields.Count > 0
                ? (double)allFields.Count(f => f.CanAutoPopulate) / allFields.Count * 100
                : 0;

            return recommendation;
        }

        /// <summary>
        /// Create an optimized schedule configuration with auto-populated fields.
        /// </summary>
        public ScheduleConfiguration CreateScheduleConfiguration(
            string scheduleType,
            string category,
            SchedulePurpose purpose = SchedulePurpose.General,
            ParameterContext context = null)
        {
            var config = new ScheduleConfiguration
            {
                ScheduleType = scheduleType,
                Category = category,
                Purpose = purpose,
                CreatedAt = DateTime.Now
            };

            // Get field recommendations
            var recommendations = GetRecommendedFields(category, purpose, context);

            // Add fields in order: Required -> Recommended -> Optional
            int sortOrder = 1;

            foreach (var field in recommendations.RequiredFields)
            {
                config.Fields.Add(CreateScheduleField(field, sortOrder++, true));
            }

            foreach (var field in recommendations.RecommendedFields)
            {
                config.Fields.Add(CreateScheduleField(field, sortOrder++, false));
            }

            // Configure sorting (typically by mark/number first)
            var markField = config.Fields.FirstOrDefault(f =>
                f.ParameterName.Contains("Mark") || f.ParameterName.Contains("Number"));
            if (markField != null)
            {
                config.SortFields.Add(new ScheduleSortField
                {
                    FieldName = markField.ParameterName,
                    Ascending = true
                });
            }

            // Configure grouping based on purpose
            ConfigureGrouping(config, purpose);

            // Configure filtering based on context
            ConfigureFiltering(config, context);

            // Set formatting options
            config.Formatting = GetDefaultFormatting(purpose);

            return config;
        }

        /// <summary>
        /// Get available schedule templates for a category.
        /// </summary>
        public List<ScheduleTemplateInfo> GetAvailableTemplates(string category)
        {
            var templates = new List<ScheduleTemplateInfo>();

            foreach (var template in _scheduleTemplates.Values
                .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                           t.Category.Equals("All", StringComparison.OrdinalIgnoreCase)))
            {
                templates.Add(new ScheduleTemplateInfo
                {
                    TemplateId = template.TemplateId,
                    Name = template.Name,
                    Category = template.Category,
                    Purpose = template.Purpose,
                    FieldCount = template.Fields.Count,
                    AutoPopulateFields = template.Fields.Count(f => f.CanAutoPopulate),
                    Description = template.Description
                });
            }

            return templates.OrderBy(t => t.Name).ToList();
        }

        #endregion

        #region Public Methods - Schedule Population

        /// <summary>
        /// Populate schedule data with auto-populated parameter values.
        /// </summary>
        public async Task<SchedulePopulationResult> PopulateScheduleDataAsync(
            ScheduleConfiguration config,
            IEnumerable<ElementData> elements,
            IProgress<SchedulePopulationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new SchedulePopulationResult
            {
                ScheduleType = config.ScheduleType,
                StartTime = DateTime.Now
            };

            var elementList = elements.ToList();
            var processed = 0;

            foreach (var element in elementList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = new ScheduleRow
                {
                    ElementId = element.ElementId
                };

                // Auto-populate parameters for this element
                var populationResult = _autoPopulator.PopulateElement(element);

                // Build row values from fields
                foreach (var field in config.Fields)
                {
                    object value = null;

                    // Try auto-populated values first
                    if (populationResult.PopulatedParameters.TryGetValue(field.ParameterId, out var populated))
                    {
                        value = populated.Value;
                        row.AutoPopulatedFields.Add(field.ParameterId);
                    }
                    // Try native parameters
                    else if (element.NativeParameters.TryGetValue(field.RevitParameterName, out var native))
                    {
                        value = native;
                    }
                    // Try type parameters
                    else if (element.TypeParameters.TryGetValue(field.RevitParameterName, out var typeValue))
                    {
                        value = typeValue;
                    }

                    row.Values[field.ParameterId] = value;
                }

                result.Rows.Add(row);
                processed++;

                progress?.Report(new SchedulePopulationProgress
                {
                    Current = processed,
                    Total = elementList.Count,
                    CurrentElement = element.ElementId,
                    PercentComplete = (double)processed / elementList.Count * 100
                });
            }

            result.EndTime = DateTime.Now;
            result.TotalRows = result.Rows.Count;
            result.TotalFields = config.Fields.Count;
            result.AutoPopulatedFieldCount = result.Rows.SelectMany(r => r.AutoPopulatedFields).Distinct().Count();

            return result;
        }

        /// <summary>
        /// Preview schedule with sample data and auto-population.
        /// </summary>
        public SchedulePreview GeneratePreview(
            ScheduleConfiguration config,
            IEnumerable<ElementData> sampleElements,
            int maxRows = 10)
        {
            var preview = new SchedulePreview
            {
                ScheduleType = config.ScheduleType,
                Category = config.Category
            };

            // Add column headers
            foreach (var field in config.Fields)
            {
                preview.Columns.Add(new ScheduleColumnPreview
                {
                    ParameterId = field.ParameterId,
                    DisplayName = field.DisplayName ?? field.ParameterName,
                    CanAutoPopulate = field.CanAutoPopulate,
                    DataType = field.DataType
                });
            }

            // Generate sample rows
            var samples = sampleElements.Take(maxRows).ToList();
            foreach (var element in samples)
            {
                var populationResult = _autoPopulator.PopulateElement(element);
                var populationPreview = _autoPopulator.GetPopulationPreview(element);

                var row = new Dictionary<string, ScheduleCellPreview>();

                foreach (var field in config.Fields)
                {
                    var cell = new ScheduleCellPreview
                    {
                        ParameterId = field.ParameterId
                    };

                    // Get value
                    if (populationResult.PopulatedParameters.TryGetValue(field.ParameterId, out var populated))
                    {
                        cell.Value = populated.Value;
                        cell.Source = populated.Source;
                        cell.IsAutoPopulated = true;
                    }
                    else if (element.NativeParameters.TryGetValue(field.RevitParameterName, out var native))
                    {
                        cell.Value = native;
                        cell.Source = "Native";
                        cell.IsAutoPopulated = false;
                    }

                    row[field.ParameterId] = cell;
                }

                preview.SampleRows.Add(row);
            }

            // Calculate coverage statistics
            preview.AutoPopulateCoverage = CalculateCoverage(preview);

            return preview;
        }

        /// <summary>
        /// Validate schedule configuration and identify potential issues.
        /// </summary>
        public ScheduleValidationResult ValidateConfiguration(
            ScheduleConfiguration config,
            ElementData sampleElement = null)
        {
            var result = new ScheduleValidationResult
            {
                IsValid = true
            };

            // Check for required fields
            if (_categoryConfigs.TryGetValue(config.Category, out var categoryConfig))
            {
                foreach (var requiredField in categoryConfig.RequiredFields)
                {
                    if (!config.Fields.Any(f => f.ParameterId == requiredField))
                    {
                        result.Warnings.Add(new ScheduleValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = $"Recommended field '{requiredField}' is not included",
                            Suggestion = "Consider adding this field for completeness"
                        });
                    }
                }
            }

            // Check for duplicate fields
            var duplicates = config.Fields
                .GroupBy(f => f.ParameterId)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var duplicate in duplicates)
            {
                result.Errors.Add(new ScheduleValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"Duplicate field: {duplicate.Key}",
                    Suggestion = "Remove duplicate field entries"
                });
                result.IsValid = false;
            }

            // Check auto-populate availability
            foreach (var field in config.Fields.Where(f => f.CanAutoPopulate))
            {
                if (sampleElement != null)
                {
                    var preview = _autoPopulator.GetPopulationPreview(sampleElement);
                    var paramPreview = preview.Parameters
                        .FirstOrDefault(p => p.SharedParameterId == field.ParameterId);

                    if (paramPreview != null && !paramPreview.SourceAvailable)
                    {
                        result.Warnings.Add(new ScheduleValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = $"Source not available for auto-populate: {field.ParameterName}",
                            Suggestion = "Verify the source parameter exists in the model"
                        });
                    }
                }
            }

            // Check sort field validity
            foreach (var sortField in config.SortFields)
            {
                if (!config.Fields.Any(f => f.ParameterName == sortField.FieldName ||
                                            f.ParameterId == sortField.FieldName))
                {
                    result.Warnings.Add(new ScheduleValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Sort field not in schedule: {sortField.FieldName}",
                        Suggestion = "Add the field to the schedule or remove from sorting"
                    });
                }
            }

            return result;
        }

        #endregion

        #region Public Methods - Bulk Operations

        /// <summary>
        /// Generate multiple schedules for a project based on model content.
        /// </summary>
        public async Task<List<ScheduleConfiguration>> GenerateProjectSchedulesAsync(
            ProjectAnalysis projectAnalysis,
            ScheduleGenerationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new ScheduleGenerationOptions();
            var schedules = new List<ScheduleConfiguration>();

            foreach (var category in projectAnalysis.CategoriesWithElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Determine purposes based on options
                var purposes = new List<SchedulePurpose> { SchedulePurpose.General };

                if (options.IncludeQuantitySchedules)
                    purposes.Add(SchedulePurpose.Quantities);

                if (options.IncludeEquipmentSchedules &&
                    IsMEPCategory(category))
                    purposes.Add(SchedulePurpose.Equipment);

                foreach (var purpose in purposes)
                {
                    var config = CreateScheduleConfiguration(
                        $"{category}_{purpose}",
                        category,
                        purpose,
                        projectAnalysis.Context);

                    schedules.Add(config);
                }
            }

            // Add cross-category schedules if requested
            if (options.IncludeSummarySchedules)
            {
                schedules.AddRange(GenerateSummarySchedules(projectAnalysis));
            }

            return schedules;
        }

        /// <summary>
        /// Batch populate parameters for all elements before schedule generation.
        /// </summary>
        public async Task<BatchPopulationSummary> PrepopulateParametersAsync(
            IEnumerable<ElementData> elements,
            IProgress<PopulationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var batchResult = await _autoPopulator.PopulateBatchAsync(elements, progress, cancellationToken);

            return new BatchPopulationSummary
            {
                TotalElements = batchResult.TotalElements,
                SuccessfulElements = batchResult.SuccessCount,
                FailedElements = batchResult.ErrorCount,
                TotalParametersPopulated = batchResult.Results.Sum(r => r.PopulatedParameters.Count),
                Duration = batchResult.Duration,
                CategoryBreakdown = batchResult.Results
                    .GroupBy(r => r.Category)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        #endregion

        #region Private Methods - Initialization

        private void InitializeDefaultConfigurations()
        {
            // Rooms configuration
            _categoryConfigs["Rooms"] = new CategoryScheduleConfig
            {
                Category = "Rooms",
                RequiredFields = new List<string> { "SP001", "SP002", "SP003", "SP006", "SP007" },
                QuantityFields = new List<string> { "SP003", "SP004", "SP005" },
                EquipmentFields = new List<string>(),
                FinishFields = new List<string> { "SP011", "SP012", "SP013", "SP014" },
                DefaultSortField = "SP002",
                DefaultGroupField = "SP007"
            };

            // Doors configuration
            _categoryConfigs["Doors"] = new CategoryScheduleConfig
            {
                Category = "Doors",
                RequiredFields = new List<string> { "SP021", "SP022", "SP023", "SP024", "SP025", "SP026" },
                QuantityFields = new List<string> { "SP022", "SP023" },
                EquipmentFields = new List<string> { "SP027", "SP028", "SP031" },
                FinishFields = new List<string> { "SP029", "SP030" },
                DefaultSortField = "SP021",
                DefaultGroupField = "SP024"
            };

            // Windows configuration
            _categoryConfigs["Windows"] = new CategoryScheduleConfig
            {
                Category = "Windows",
                RequiredFields = new List<string> { "SP036", "SP037", "SP038", "SP039", "SP040" },
                QuantityFields = new List<string> { "SP037", "SP038", "SP049" },
                EquipmentFields = new List<string> { "SP044", "SP045", "SP046" },
                FinishFields = new List<string> { "SP042", "SP043" },
                DefaultSortField = "SP036",
                DefaultGroupField = "SP040"
            };

            // Walls configuration
            _categoryConfigs["Walls"] = new CategoryScheduleConfig
            {
                Category = "Walls",
                RequiredFields = new List<string> { "SP052", "SP055", "SP057", "SP063" },
                QuantityFields = new List<string> { "SP053", "SP055", "SP056" },
                EquipmentFields = new List<string> { "SP059", "SP060", "SP061" },
                FinishFields = new List<string> { "SP065" },
                DefaultSortField = "SP052",
                DefaultGroupField = "SP063"
            };

            // Ducts configuration
            _categoryConfigs["Ducts"] = new CategoryScheduleConfig
            {
                Category = "Ducts",
                RequiredFields = new List<string> { "SP123", "SP124", "SP125", "SP126", "SP128" },
                QuantityFields = new List<string> { "SP128", "SP129" },
                EquipmentFields = new List<string> { "SP130", "SP131", "SP132" },
                FinishFields = new List<string> { "SP133", "SP134", "SP136" },
                DefaultSortField = "SP124",
                DefaultGroupField = "SP135"
            };

            // Pipes configuration
            _categoryConfigs["Pipes"] = new CategoryScheduleConfig
            {
                Category = "Pipes",
                RequiredFields = new List<string> { "SP111", "SP112", "SP113", "SP114" },
                QuantityFields = new List<string> { "SP113", "SP114" },
                EquipmentFields = new List<string> { "SP118", "SP119", "SP120" },
                FinishFields = new List<string> { "SP115", "SP116", "SP117" },
                DefaultSortField = "SP112",
                DefaultGroupField = "SP121"
            };

            // Lighting configuration
            _categoryConfigs["LightingFixtures"] = new CategoryScheduleConfig
            {
                Category = "LightingFixtures",
                RequiredFields = new List<string> { "SP215", "SP216", "SP217", "SP222", "SP223" },
                QuantityFields = new List<string> { "SP217" },
                EquipmentFields = new List<string> { "SP218", "SP219", "SP220", "SP221" },
                FinishFields = new List<string> { "SP228" },
                DefaultSortField = "SP216",
                DefaultGroupField = "SP222"
            };

            // Structural columns configuration
            _categoryConfigs["StructuralColumns"] = new CategoryScheduleConfig
            {
                Category = "StructuralColumns",
                RequiredFields = new List<string> { "SP261", "SP262", "SP265", "SP266", "SP267" },
                QuantityFields = new List<string> { "SP267", "SP269", "SP270" },
                EquipmentFields = new List<string> { "SP264" },
                FinishFields = new List<string>(),
                DefaultSortField = "SP261",
                DefaultGroupField = "SP265"
            };

            // Structural framing configuration
            _categoryConfigs["StructuralFraming"] = new CategoryScheduleConfig
            {
                Category = "StructuralFraming",
                RequiredFields = new List<string> { "SP271", "SP272", "SP275", "SP276" },
                QuantityFields = new List<string> { "SP276", "SP277", "SP278" },
                EquipmentFields = new List<string> { "SP274" },
                FinishFields = new List<string>(),
                DefaultSortField = "SP272",
                DefaultGroupField = "SP275"
            };
        }

        private void LoadScheduleTemplates(string templatesPath)
        {
            // Load schedule templates from CSV files in the templates directory
            var csvFiles = Directory.GetFiles(templatesPath, "*.csv");

            foreach (var file in csvFiles)
            {
                try
                {
                    var template = LoadTemplateFromCsv(file);
                    if (template != null)
                    {
                        _scheduleTemplates[template.TemplateId] = template;
                    }
                }
                catch
                {
                    // Skip invalid template files
                }
            }
        }

        private ScheduleTemplate LoadTemplateFromCsv(string filePath)
        {
            // Simplified template loading - in production would parse full CSV structure
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var parts = fileName.Split('_');

            return new ScheduleTemplate
            {
                TemplateId = fileName,
                Name = fileName.Replace("_", " "),
                Category = parts.Length > 0 ? parts[0] : "General",
                Description = $"Schedule template loaded from {fileName}"
            };
        }

        #endregion

        #region Private Methods - Helpers

        private ScheduleFieldInfo CreateFieldInfo(string parameterId, string category, bool isRequired)
        {
            var suggestions = _autoPopulator.GetScheduleParameterSuggestions(category);
            var suggestion = suggestions.FirstOrDefault(s => s.ParameterId == parameterId);

            return new ScheduleFieldInfo
            {
                ParameterId = parameterId,
                ParameterName = suggestion?.ParameterName ?? parameterId,
                Description = suggestion?.Description,
                DataType = suggestion?.DataType ?? "Text",
                Group = suggestion?.Group,
                IsRequired = isRequired,
                CanAutoPopulate = suggestion?.CanAutoPopulate ?? false,
                AutoPopulateSource = suggestion?.AutoPopulateSource
            };
        }

        private ScheduleField CreateScheduleField(ScheduleFieldInfo fieldInfo, int sortOrder, bool isVisible)
        {
            return new ScheduleField
            {
                ParameterId = fieldInfo.ParameterId,
                ParameterName = fieldInfo.ParameterName,
                DisplayName = fieldInfo.ParameterName,
                RevitParameterName = fieldInfo.AutoPopulateSource?.Split(':').LastOrDefault()?.Trim() ?? fieldInfo.ParameterName,
                DataType = fieldInfo.DataType,
                CanAutoPopulate = fieldInfo.CanAutoPopulate,
                IsVisible = isVisible,
                SortOrder = sortOrder,
                ColumnWidth = CalculateColumnWidth(fieldInfo.DataType)
            };
        }

        private List<string> GetFieldsForPurpose(CategoryScheduleConfig config, SchedulePurpose purpose)
        {
            switch (purpose)
            {
                case SchedulePurpose.Quantities:
                    return config.QuantityFields;
                case SchedulePurpose.Equipment:
                    return config.EquipmentFields;
                case SchedulePurpose.Finishes:
                    return config.FinishFields;
                default:
                    return config.QuantityFields.Concat(config.EquipmentFields).Distinct().ToList();
            }
        }

        private void ConfigureGrouping(ScheduleConfiguration config, SchedulePurpose purpose)
        {
            if (_categoryConfigs.TryGetValue(config.Category, out var categoryConfig) &&
                !string.IsNullOrEmpty(categoryConfig.DefaultGroupField))
            {
                config.GroupFields.Add(new ScheduleGroupField
                {
                    FieldName = categoryConfig.DefaultGroupField,
                    ShowHeader = true,
                    ShowFooter = purpose == SchedulePurpose.Quantities,
                    ShowCount = true
                });
            }
        }

        private void ConfigureFiltering(ScheduleConfiguration config, ParameterContext context)
        {
            if (context == null) return;

            // Add level filter if context specifies
            // Add phase filter
            // Add design option filter
            // These would be implemented based on context parameters
        }

        private ScheduleFormatting GetDefaultFormatting(SchedulePurpose purpose)
        {
            return new ScheduleFormatting
            {
                ShowTitle = true,
                ShowHeaders = true,
                ShowGridLines = true,
                UseAlternatingRowColors = purpose != SchedulePurpose.Equipment,
                TitleFontSize = 12,
                HeaderFontSize = 10,
                BodyFontSize = 9,
                HeaderBackgroundColor = "#E0E0E0"
            };
        }

        private int CalculateColumnWidth(string dataType)
        {
            switch (dataType?.ToLowerInvariant())
            {
                case "text":
                    return 120;
                case "integer":
                case "number":
                    return 80;
                case "area":
                case "volume":
                case "length":
                    return 90;
                case "yesno":
                    return 60;
                default:
                    return 100;
            }
        }

        private double CalculateCoverage(SchedulePreview preview)
        {
            if (preview.SampleRows.Count == 0 || preview.Columns.Count == 0)
                return 0;

            var totalCells = preview.SampleRows.Count * preview.Columns.Count;
            var autoPopulatedCells = preview.SampleRows
                .SelectMany(r => r.Values.Where(c => c.IsAutoPopulated))
                .Count();

            return (double)autoPopulatedCells / totalCells * 100;
        }

        private bool IsMEPCategory(string category)
        {
            var mepCategories = new[]
            {
                "Ducts", "Pipes", "CableTrays", "Conduits",
                "MechanicalEquipment", "ElectricalEquipment", "PlumbingFixtures",
                "LightingFixtures", "AirTerminals", "Sprinklers"
            };

            return mepCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
        }

        private List<ScheduleConfiguration> GenerateSummarySchedules(ProjectAnalysis analysis)
        {
            var summaries = new List<ScheduleConfiguration>();

            // Area summary schedule
            if (analysis.CategoriesWithElements.Contains("Rooms"))
            {
                summaries.Add(CreateScheduleConfiguration(
                    "Area_Summary",
                    "Rooms",
                    SchedulePurpose.Quantities,
                    analysis.Context));
            }

            // MEP equipment summary
            var mepCategories = analysis.CategoriesWithElements
                .Where(IsMEPCategory)
                .ToList();

            if (mepCategories.Any())
            {
                // Would create cross-category equipment summary
            }

            return summaries;
        }

        #endregion
    }

    #region Supporting Types

    public class ScheduleFieldRecommendation
    {
        public string Category { get; set; }
        public SchedulePurpose Purpose { get; set; }
        public List<ScheduleFieldInfo> RequiredFields { get; set; } = new List<ScheduleFieldInfo>();
        public List<ScheduleFieldInfo> RecommendedFields { get; set; } = new List<ScheduleFieldInfo>();
        public List<ScheduleFieldInfo> OptionalFields { get; set; } = new List<ScheduleFieldInfo>();
        public double AutoPopulateCoverage { get; set; }
    }

    public class ScheduleFieldInfo
    {
        public string ParameterId { get; set; }
        public string ParameterName { get; set; }
        public string Description { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public bool IsRequired { get; set; }
        public bool CanAutoPopulate { get; set; }
        public string AutoPopulateSource { get; set; }
        public string RecommendationReason { get; set; }
    }

    public class ScheduleConfiguration
    {
        public string ScheduleType { get; set; }
        public string Category { get; set; }
        public SchedulePurpose Purpose { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ScheduleField> Fields { get; set; } = new List<ScheduleField>();
        public List<ScheduleSortField> SortFields { get; set; } = new List<ScheduleSortField>();
        public List<ScheduleGroupField> GroupFields { get; set; } = new List<ScheduleGroupField>();
        public List<ScheduleFilter> Filters { get; set; } = new List<ScheduleFilter>();
        public ScheduleFormatting Formatting { get; set; } = new ScheduleFormatting();
    }

    public class ScheduleField
    {
        public string ParameterId { get; set; }
        public string ParameterName { get; set; }
        public string DisplayName { get; set; }
        public string RevitParameterName { get; set; }
        public string DataType { get; set; }
        public bool CanAutoPopulate { get; set; }
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
        public int ColumnWidth { get; set; }
    }

    public class ScheduleSortField
    {
        public string FieldName { get; set; }
        public bool Ascending { get; set; }
    }

    public class ScheduleGroupField
    {
        public string FieldName { get; set; }
        public bool ShowHeader { get; set; }
        public bool ShowFooter { get; set; }
        public bool ShowCount { get; set; }
    }

    public class ScheduleFilter
    {
        public string FieldName { get; set; }
        public FilterOperator Operator { get; set; }
        public object Value { get; set; }
    }

    public enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        GreaterThan,
        LessThan,
        IsNotEmpty
    }

    public class ScheduleFormatting
    {
        public bool ShowTitle { get; set; }
        public bool ShowHeaders { get; set; }
        public bool ShowGridLines { get; set; }
        public bool UseAlternatingRowColors { get; set; }
        public int TitleFontSize { get; set; }
        public int HeaderFontSize { get; set; }
        public int BodyFontSize { get; set; }
        public string HeaderBackgroundColor { get; set; }
    }

    public enum SchedulePurpose
    {
        General,
        Quantities,
        Equipment,
        Finishes,
        Coordination,
        Compliance
    }

    public class ScheduleTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public SchedulePurpose Purpose { get; set; }
        public string Description { get; set; }
        public List<ScheduleField> Fields { get; set; } = new List<ScheduleField>();
    }

    public class ScheduleTemplateInfo
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public SchedulePurpose Purpose { get; set; }
        public int FieldCount { get; set; }
        public int AutoPopulateFields { get; set; }
        public string Description { get; set; }
    }

    public class SchedulePopulationResult
    {
        public string ScheduleType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalRows { get; set; }
        public int TotalFields { get; set; }
        public int AutoPopulatedFieldCount { get; set; }
        public List<ScheduleRow> Rows { get; set; } = new List<ScheduleRow>();
    }

    public class ScheduleRow
    {
        public string ElementId { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
        public List<string> AutoPopulatedFields { get; set; } = new List<string>();
    }

    public class SchedulePopulationProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentElement { get; set; }
        public double PercentComplete { get; set; }
    }

    public class SchedulePreview
    {
        public string ScheduleType { get; set; }
        public string Category { get; set; }
        public List<ScheduleColumnPreview> Columns { get; set; } = new List<ScheduleColumnPreview>();
        public List<Dictionary<string, ScheduleCellPreview>> SampleRows { get; set; } = new List<Dictionary<string, ScheduleCellPreview>>();
        public double AutoPopulateCoverage { get; set; }
    }

    public class ScheduleColumnPreview
    {
        public string ParameterId { get; set; }
        public string DisplayName { get; set; }
        public bool CanAutoPopulate { get; set; }
        public string DataType { get; set; }
    }

    public class ScheduleCellPreview
    {
        public string ParameterId { get; set; }
        public object Value { get; set; }
        public string Source { get; set; }
        public bool IsAutoPopulated { get; set; }
    }

    public class ScheduleValidationResult
    {
        public bool IsValid { get; set; }
        public List<ScheduleValidationIssue> Errors { get; set; } = new List<ScheduleValidationIssue>();
        public List<ScheduleValidationIssue> Warnings { get; set; } = new List<ScheduleValidationIssue>();
    }

    public class ScheduleValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Suggestion { get; set; }
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public class CategoryScheduleConfig
    {
        public string Category { get; set; }
        public List<string> RequiredFields { get; set; } = new List<string>();
        public List<string> QuantityFields { get; set; } = new List<string>();
        public List<string> EquipmentFields { get; set; } = new List<string>();
        public List<string> FinishFields { get; set; } = new List<string>();
        public string DefaultSortField { get; set; }
        public string DefaultGroupField { get; set; }
    }

    public class ProjectAnalysis
    {
        public List<string> CategoriesWithElements { get; set; } = new List<string>();
        public Dictionary<string, int> ElementCounts { get; set; } = new Dictionary<string, int>();
        public ParameterContext Context { get; set; }
    }

    public class ScheduleGenerationOptions
    {
        public bool IncludeQuantitySchedules { get; set; } = true;
        public bool IncludeEquipmentSchedules { get; set; } = true;
        public bool IncludeSummarySchedules { get; set; } = false;
        public bool AutoPopulateBeforeGeneration { get; set; } = true;
    }

    public class BatchPopulationSummary
    {
        public int TotalElements { get; set; }
        public int SuccessfulElements { get; set; }
        public int FailedElements { get; set; }
        public int TotalParametersPopulated { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, int> CategoryBreakdown { get; set; } = new Dictionary<string, int>();
    }

    #endregion
}
