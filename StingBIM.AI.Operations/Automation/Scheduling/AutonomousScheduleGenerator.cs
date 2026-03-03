// ============================================================================
// StingBIM AI - Autonomous Schedule Generation
// Automatically generates and optimizes Revit schedules based on model analysis
// Intelligent template selection and field configuration
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Automation.Scheduling
{
    /// <summary>
    /// Autonomous Schedule Generator
    /// Analyzes the model and automatically creates appropriate schedules
    /// </summary>
    public class AutonomousScheduleGenerator
    {
        private readonly ModelAnalyzer _modelAnalyzer;
        private readonly ScheduleTemplateSelector _templateSelector;
        private readonly FieldOptimizer _fieldOptimizer;
        private readonly ScheduleOrganizer _organizer;
        private readonly Dictionary<string, ScheduleTemplate> _templates;

        public AutonomousScheduleGenerator()
        {
            _modelAnalyzer = new ModelAnalyzer();
            _templateSelector = new ScheduleTemplateSelector();
            _fieldOptimizer = new FieldOptimizer();
            _organizer = new ScheduleOrganizer();
            _templates = LoadScheduleTemplates();
        }

        /// <summary>
        /// Analyze model and generate all recommended schedules
        /// </summary>
        public async Task<ScheduleGenerationResult> GenerateSchedulesAsync(
            ModelContext model,
            ScheduleGenerationOptions options = null)
        {
            options ??= ScheduleGenerationOptions.Default;

            var result = new ScheduleGenerationResult
            {
                StartTime = DateTime.UtcNow,
                ModelId = model.ModelId
            };

            try
            {
                // Step 1: Analyze model content
                var modelAnalysis = await _modelAnalyzer.AnalyzeAsync(model);
                result.ModelAnalysis = modelAnalysis;

                // Step 2: Determine project type and discipline focus
                var projectProfile = DetermineProjectProfile(modelAnalysis);
                result.ProjectProfile = projectProfile;

                // Step 3: Select appropriate schedule templates
                var selectedTemplates = _templateSelector.SelectTemplates(
                    projectProfile, modelAnalysis, options);

                // Step 4: Generate each schedule
                foreach (var template in selectedTemplates)
                {
                    var scheduleResult = await GenerateScheduleAsync(
                        model, template, modelAnalysis, options);
                    result.GeneratedSchedules.Add(scheduleResult);
                }

                // Step 5: Organize schedules into logical groups
                result.ScheduleOrganization = _organizer.OrganizeSchedules(
                    result.GeneratedSchedules);

                // Step 6: Generate schedule index/summary
                result.ScheduleIndex = GenerateScheduleIndex(result.GeneratedSchedules);

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Generate a single schedule with optimized configuration
        /// </summary>
        public async Task<GeneratedSchedule> GenerateScheduleAsync(
            ModelContext model,
            ScheduleTemplate template,
            ModelAnalysis analysis,
            ScheduleGenerationOptions options)
        {
            var schedule = new GeneratedSchedule
            {
                TemplateId = template.TemplateId,
                TemplateName = template.Name,
                Category = template.Category,
                Discipline = template.Discipline
            };

            // Optimize fields based on actual model content
            schedule.Fields = _fieldOptimizer.OptimizeFields(
                template.DefaultFields,
                analysis,
                template.Category);

            // Determine appropriate filters
            schedule.Filters = DetermineFilters(template, analysis, options);

            // Configure sorting and grouping
            schedule.SortingConfig = DetermineSorting(template, analysis);
            schedule.GroupingConfig = DetermineGrouping(template, analysis, options);

            // Configure formatting
            schedule.Formatting = DetermineFormatting(template, analysis);

            // Calculate schedule
            schedule.RowCount = await CalculateScheduleRowCountAsync(model, schedule);

            // Generate schedule name
            schedule.GeneratedName = GenerateScheduleName(template, analysis, options);

            // Validate schedule configuration
            schedule.ValidationResult = ValidateScheduleConfig(schedule, analysis);

            return schedule;
        }

        /// <summary>
        /// Generate schedules for a specific discipline
        /// </summary>
        public async Task<List<GeneratedSchedule>> GenerateForDisciplineAsync(
            ModelContext model,
            Discipline discipline,
            ScheduleGenerationOptions options = null)
        {
            options ??= ScheduleGenerationOptions.Default;
            options.DisciplineFilter = discipline;

            var result = await GenerateSchedulesAsync(model, options);
            return result.GeneratedSchedules;
        }

        /// <summary>
        /// Suggest additional schedules based on model content
        /// </summary>
        public List<ScheduleSuggestion> SuggestAdditionalSchedules(
            ModelAnalysis analysis,
            List<string> existingScheduleTypes)
        {
            var suggestions = new List<ScheduleSuggestion>();

            // Check for missing standard schedules
            var standardSchedules = GetStandardSchedulesForProject(analysis.ProjectType);
            foreach (var standard in standardSchedules)
            {
                if (!existingScheduleTypes.Contains(standard.Type))
                {
                    suggestions.Add(new ScheduleSuggestion
                    {
                        ScheduleType = standard.Type,
                        Priority = standard.Priority,
                        Reason = $"Standard {analysis.ProjectType} project schedule",
                        RecommendedTemplate = standard.TemplateId
                    });
                }
            }

            // Suggest based on element quantities
            foreach (var category in analysis.ElementCounts)
            {
                if (category.Value > 10 && !existingScheduleTypes.Any(s =>
                    s.Contains(category.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    suggestions.Add(new ScheduleSuggestion
                    {
                        ScheduleType = $"{category.Key} Schedule",
                        Priority = category.Value > 100 ? "High" : "Medium",
                        Reason = $"{category.Value} {category.Key} elements in model",
                        RecommendedTemplate = FindTemplateForCategory(category.Key)
                    });
                }
            }

            // Suggest material takeoff if materials assigned
            if (analysis.HasMaterialAssignments && !existingScheduleTypes.Contains("Material Takeoff"))
            {
                suggestions.Add(new ScheduleSuggestion
                {
                    ScheduleType = "Material Takeoff",
                    Priority = "High",
                    Reason = "Model has material assignments for quantity extraction",
                    RecommendedTemplate = "MAT_TAKEOFF_01"
                });
            }

            // Suggest room-based schedules if rooms exist
            if (analysis.RoomCount > 0)
            {
                if (!existingScheduleTypes.Contains("Room Schedule"))
                {
                    suggestions.Add(new ScheduleSuggestion
                    {
                        ScheduleType = "Room Schedule",
                        Priority = "High",
                        Reason = $"{analysis.RoomCount} rooms defined in model",
                        RecommendedTemplate = "ARCH_ROOM_01"
                    });
                }

                if (!existingScheduleTypes.Contains("Room Finish Schedule"))
                {
                    suggestions.Add(new ScheduleSuggestion
                    {
                        ScheduleType = "Room Finish Schedule",
                        Priority = "Medium",
                        Reason = "Room finish documentation",
                        RecommendedTemplate = "ARCH_FINISH_01"
                    });
                }
            }

            return suggestions.OrderByDescending(s =>
                s.Priority == "High" ? 3 : s.Priority == "Medium" ? 2 : 1).ToList();
        }

        #region Private Methods

        private ProjectProfile DetermineProjectProfile(ModelAnalysis analysis)
        {
            var profile = new ProjectProfile
            {
                ProjectType = analysis.ProjectType,
                PrimaryDiscipline = DeterminePrimaryDiscipline(analysis),
                SecondaryDisciplines = DetermineSecondaryDisciplines(analysis),
                Scale = DetermineProjectScale(analysis),
                Phase = analysis.CurrentPhase
            };

            // Determine focus areas
            if (analysis.ElementCounts.GetValueOrDefault("Rooms", 0) > 50)
                profile.FocusAreas.Add("Space Management");

            if (analysis.HasDetailedMEP)
                profile.FocusAreas.Add("MEP Coordination");

            if (analysis.HasStructuralElements)
                profile.FocusAreas.Add("Structural Documentation");

            if (analysis.ElementCounts.GetValueOrDefault("Furniture", 0) > 20)
                profile.FocusAreas.Add("FF&E");

            return profile;
        }

        private Discipline DeterminePrimaryDiscipline(ModelAnalysis analysis)
        {
            var disciplineScores = new Dictionary<Discipline, int>
            {
                { Discipline.Architectural, 0 },
                { Discipline.Structural, 0 },
                { Discipline.Mechanical, 0 },
                { Discipline.Electrical, 0 },
                { Discipline.Plumbing, 0 }
            };

            // Score based on element counts
            disciplineScores[Discipline.Architectural] +=
                analysis.ElementCounts.GetValueOrDefault("Walls", 0) +
                analysis.ElementCounts.GetValueOrDefault("Doors", 0) +
                analysis.ElementCounts.GetValueOrDefault("Windows", 0) +
                analysis.ElementCounts.GetValueOrDefault("Rooms", 0);

            disciplineScores[Discipline.Structural] +=
                analysis.ElementCounts.GetValueOrDefault("Structural Columns", 0) * 2 +
                analysis.ElementCounts.GetValueOrDefault("Structural Beams", 0) * 2 +
                analysis.ElementCounts.GetValueOrDefault("Structural Foundations", 0) * 2;

            disciplineScores[Discipline.Mechanical] +=
                analysis.ElementCounts.GetValueOrDefault("Ducts", 0) +
                analysis.ElementCounts.GetValueOrDefault("Mechanical Equipment", 0) * 2;

            disciplineScores[Discipline.Electrical] +=
                analysis.ElementCounts.GetValueOrDefault("Electrical Fixtures", 0) +
                analysis.ElementCounts.GetValueOrDefault("Cable Trays", 0);

            disciplineScores[Discipline.Plumbing] +=
                analysis.ElementCounts.GetValueOrDefault("Pipes", 0) +
                analysis.ElementCounts.GetValueOrDefault("Plumbing Fixtures", 0) * 2;

            return disciplineScores.OrderByDescending(d => d.Value).First().Key;
        }

        private List<Discipline> DetermineSecondaryDisciplines(ModelAnalysis analysis)
        {
            var disciplines = new List<Discipline>();

            if (analysis.HasStructuralElements)
                disciplines.Add(Discipline.Structural);
            if (analysis.HasDetailedMEP)
            {
                if (analysis.ElementCounts.GetValueOrDefault("Ducts", 0) > 0)
                    disciplines.Add(Discipline.Mechanical);
                if (analysis.ElementCounts.GetValueOrDefault("Electrical Fixtures", 0) > 0)
                    disciplines.Add(Discipline.Electrical);
                if (analysis.ElementCounts.GetValueOrDefault("Pipes", 0) > 0)
                    disciplines.Add(Discipline.Plumbing);
            }

            return disciplines.Distinct().ToList();
        }

        private ProjectScale DetermineProjectScale(ModelAnalysis analysis)
        {
            var totalElements = analysis.ElementCounts.Values.Sum();

            if (totalElements > 10000) return ProjectScale.Large;
            if (totalElements > 2000) return ProjectScale.Medium;
            return ProjectScale.Small;
        }

        private List<ScheduleFilter> DetermineFilters(
            ScheduleTemplate template,
            ModelAnalysis analysis,
            ScheduleGenerationOptions options)
        {
            var filters = new List<ScheduleFilter>();

            // Phase filter if multiple phases
            if (analysis.PhaseCount > 1 && options.FilterByPhase)
            {
                filters.Add(new ScheduleFilter
                {
                    FieldName = "Phase",
                    FilterType = FilterType.Equals,
                    Value = analysis.CurrentPhase
                });
            }

            // Level filter for large projects
            if (options.SeparateByLevel && analysis.LevelCount > 5)
            {
                // Will generate separate schedules per level
            }

            // Design option filter
            if (analysis.HasDesignOptions && options.FilterByDesignOption)
            {
                filters.Add(new ScheduleFilter
                {
                    FieldName = "Design Option",
                    FilterType = FilterType.Equals,
                    Value = "Main Model"
                });
            }

            // Category-specific filters
            filters.AddRange(template.DefaultFilters ?? new List<ScheduleFilter>());

            return filters;
        }

        private SortingConfiguration DetermineSorting(
            ScheduleTemplate template,
            ModelAnalysis analysis)
        {
            var config = new SortingConfiguration();

            // Use template default if available
            if (template.DefaultSorting != null)
            {
                config = template.DefaultSorting;
            }
            else
            {
                // Intelligent sorting based on category
                config.PrimarySort = template.Category switch
                {
                    "Doors" => new SortField { FieldName = "Mark", Ascending = true },
                    "Windows" => new SortField { FieldName = "Mark", Ascending = true },
                    "Rooms" => new SortField { FieldName = "Number", Ascending = true },
                    "Walls" => new SortField { FieldName = "Type", Ascending = true },
                    "Equipment" => new SortField { FieldName = "System", Ascending = true },
                    _ => new SortField { FieldName = "Family and Type", Ascending = true }
                };

                // Secondary sort by level for multi-story
                if (analysis.LevelCount > 1)
                {
                    config.SecondarySort = new SortField { FieldName = "Level", Ascending = true };
                }
            }

            return config;
        }

        private GroupingConfiguration DetermineGrouping(
            ScheduleTemplate template,
            ModelAnalysis analysis,
            ScheduleGenerationOptions options)
        {
            var config = new GroupingConfiguration();

            if (!options.EnableGrouping) return config;

            // Template default grouping
            if (template.DefaultGrouping != null)
            {
                return template.DefaultGrouping;
            }

            // Intelligent grouping
            if (analysis.LevelCount > 1)
            {
                config.GroupByFields.Add(new GroupField
                {
                    FieldName = "Level",
                    ShowHeader = true,
                    ShowFooter = true,
                    ShowCount = true
                });
            }

            // Category-specific grouping
            switch (template.Category)
            {
                case "Doors":
                case "Windows":
                    config.GroupByFields.Add(new GroupField
                    {
                        FieldName = "Type",
                        ShowHeader = true,
                        ShowFooter = true,
                        ShowCount = true
                    });
                    break;

                case "Mechanical Equipment":
                case "Electrical Equipment":
                    config.GroupByFields.Add(new GroupField
                    {
                        FieldName = "System",
                        ShowHeader = true,
                        ShowFooter = false
                    });
                    break;

                case "Rooms":
                    config.GroupByFields.Add(new GroupField
                    {
                        FieldName = "Department",
                        ShowHeader = true,
                        ShowFooter = true,
                        ShowCount = true
                    });
                    break;
            }

            return config;
        }

        private FormattingConfiguration DetermineFormatting(
            ScheduleTemplate template,
            ModelAnalysis analysis)
        {
            return new FormattingConfiguration
            {
                ShowTitle = true,
                ShowHeaders = true,
                ShowGridLines = true,
                AlternateRowShading = analysis.ElementCounts.Values.Sum() > 50,
                TitleFormat = new TextFormat
                {
                    Bold = true,
                    Size = 10,
                    FontName = "Arial"
                },
                HeaderFormat = new TextFormat
                {
                    Bold = true,
                    Size = 8,
                    FontName = "Arial"
                },
                BodyFormat = new TextFormat
                {
                    Bold = false,
                    Size = 8,
                    FontName = "Arial"
                }
            };
        }

        private async Task<int> CalculateScheduleRowCountAsync(
            ModelContext model,
            GeneratedSchedule schedule)
        {
            // In real implementation, this would query the Revit model
            // For now, return estimated count based on filters
            await Task.Delay(10); // Simulate async operation
            return 0; // Placeholder
        }

        private string GenerateScheduleName(
            ScheduleTemplate template,
            ModelAnalysis analysis,
            ScheduleGenerationOptions options)
        {
            var baseName = template.Name;

            if (options.IncludePhaseInName && !string.IsNullOrEmpty(analysis.CurrentPhase))
            {
                baseName += $" - {analysis.CurrentPhase}";
            }

            if (options.IncludeDateInName)
            {
                baseName += $" ({DateTime.Now:yyyy-MM-dd})";
            }

            return baseName;
        }

        private ScheduleValidationResult ValidateScheduleConfig(
            GeneratedSchedule schedule,
            ModelAnalysis analysis)
        {
            var result = new ScheduleValidationResult { IsValid = true };

            // Check if category exists in model
            if (!analysis.ElementCounts.ContainsKey(schedule.Category) ||
                analysis.ElementCounts[schedule.Category] == 0)
            {
                result.Warnings.Add($"No {schedule.Category} elements found in model");
            }

            // Check field availability
            foreach (var field in schedule.Fields)
            {
                if (!IsFieldAvailable(field, schedule.Category, analysis))
                {
                    result.Warnings.Add($"Field '{field.FieldName}' may not be available");
                }
            }

            return result;
        }

        private bool IsFieldAvailable(ScheduleField field, string category, ModelAnalysis analysis)
        {
            // Check if field is a standard parameter or exists in model
            var standardFields = new HashSet<string>
            {
                "Family and Type", "Type", "Level", "Phase", "Mark", "Comments",
                "Count", "Area", "Volume", "Length", "Width", "Height"
            };

            return standardFields.Contains(field.FieldName) ||
                   analysis.AvailableParameters.Contains(field.FieldName);
        }

        private ScheduleIndex GenerateScheduleIndex(List<GeneratedSchedule> schedules)
        {
            var index = new ScheduleIndex
            {
                GeneratedAt = DateTime.UtcNow,
                TotalSchedules = schedules.Count
            };

            // Group by discipline
            index.ByDiscipline = schedules
                .GroupBy(s => s.Discipline)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Group by category
            index.ByCategory = schedules
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            return index;
        }

        private List<StandardSchedule> GetStandardSchedulesForProject(string projectType)
        {
            var schedules = new List<StandardSchedule>
            {
                // Universal schedules
                new StandardSchedule { Type = "Door Schedule", Priority = "High", TemplateId = "ARCH_DOOR_01" },
                new StandardSchedule { Type = "Window Schedule", Priority = "High", TemplateId = "ARCH_WINDOW_01" },
                new StandardSchedule { Type = "Room Schedule", Priority = "High", TemplateId = "ARCH_ROOM_01" },
                new StandardSchedule { Type = "Wall Schedule", Priority = "Medium", TemplateId = "ARCH_WALL_01" }
            };

            // Project-type specific
            switch (projectType?.ToLower())
            {
                case "residential":
                    schedules.Add(new StandardSchedule { Type = "Appliance Schedule", Priority = "Medium", TemplateId = "MEP_APPLIANCE_01" });
                    break;

                case "commercial":
                case "office":
                    schedules.Add(new StandardSchedule { Type = "Furniture Schedule", Priority = "High", TemplateId = "ARCH_FURN_01" });
                    schedules.Add(new StandardSchedule { Type = "Equipment Schedule", Priority = "High", TemplateId = "MEP_EQUIP_01" });
                    break;

                case "healthcare":
                    schedules.Add(new StandardSchedule { Type = "Medical Equipment Schedule", Priority = "High", TemplateId = "MED_EQUIP_01" });
                    schedules.Add(new StandardSchedule { Type = "Room Data Sheet", Priority = "High", TemplateId = "ARCH_RDS_01" });
                    break;

                case "educational":
                    schedules.Add(new StandardSchedule { Type = "Furniture Schedule", Priority = "High", TemplateId = "ARCH_FURN_01" });
                    schedules.Add(new StandardSchedule { Type = "AV Equipment Schedule", Priority = "Medium", TemplateId = "ELEC_AV_01" });
                    break;
            }

            return schedules;
        }

        private string FindTemplateForCategory(string category)
        {
            var templateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Walls", "ARCH_WALL_01" },
                { "Doors", "ARCH_DOOR_01" },
                { "Windows", "ARCH_WINDOW_01" },
                { "Rooms", "ARCH_ROOM_01" },
                { "Floors", "ARCH_FLOOR_01" },
                { "Ceilings", "ARCH_CEIL_01" },
                { "Furniture", "ARCH_FURN_01" },
                { "Ducts", "MECH_DUCT_01" },
                { "Pipes", "PLUMB_PIPE_01" },
                { "Electrical Fixtures", "ELEC_FIX_01" },
                { "Plumbing Fixtures", "PLUMB_FIX_01" },
                { "Mechanical Equipment", "MECH_EQUIP_01" }
            };

            return templateMap.GetValueOrDefault(category, "GENERIC_01");
        }

        private Dictionary<string, ScheduleTemplate> LoadScheduleTemplates()
        {
            // Load from CSV files in data/schedules/
            return new Dictionary<string, ScheduleTemplate>
            {
                { "ARCH_DOOR_01", new ScheduleTemplate
                    {
                        TemplateId = "ARCH_DOOR_01",
                        Name = "Door Schedule",
                        Category = "Doors",
                        Discipline = Discipline.Architectural,
                        DefaultFields = new List<ScheduleField>
                        {
                            new ScheduleField { FieldName = "Mark", Width = 50 },
                            new ScheduleField { FieldName = "Level", Width = 80 },
                            new ScheduleField { FieldName = "Width", Width = 60 },
                            new ScheduleField { FieldName = "Height", Width = 60 },
                            new ScheduleField { FieldName = "Family and Type", Width = 150 },
                            new ScheduleField { FieldName = "Fire Rating", Width = 80 },
                            new ScheduleField { FieldName = "Comments", Width = 100 }
                        }
                    }
                },
                { "ARCH_WINDOW_01", new ScheduleTemplate
                    {
                        TemplateId = "ARCH_WINDOW_01",
                        Name = "Window Schedule",
                        Category = "Windows",
                        Discipline = Discipline.Architectural,
                        DefaultFields = new List<ScheduleField>
                        {
                            new ScheduleField { FieldName = "Mark", Width = 50 },
                            new ScheduleField { FieldName = "Level", Width = 80 },
                            new ScheduleField { FieldName = "Width", Width = 60 },
                            new ScheduleField { FieldName = "Height", Width = 60 },
                            new ScheduleField { FieldName = "Sill Height", Width = 70 },
                            new ScheduleField { FieldName = "Family and Type", Width = 150 },
                            new ScheduleField { FieldName = "Glazing", Width = 80 }
                        }
                    }
                },
                { "ARCH_ROOM_01", new ScheduleTemplate
                    {
                        TemplateId = "ARCH_ROOM_01",
                        Name = "Room Schedule",
                        Category = "Rooms",
                        Discipline = Discipline.Architectural,
                        DefaultFields = new List<ScheduleField>
                        {
                            new ScheduleField { FieldName = "Number", Width = 60 },
                            new ScheduleField { FieldName = "Name", Width = 120 },
                            new ScheduleField { FieldName = "Level", Width = 80 },
                            new ScheduleField { FieldName = "Area", Width = 70 },
                            new ScheduleField { FieldName = "Department", Width = 100 },
                            new ScheduleField { FieldName = "Occupancy", Width = 70 }
                        }
                    }
                }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class ModelAnalyzer
    {
        public async Task<ModelAnalysis> AnalyzeAsync(ModelContext model)
        {
            await Task.Delay(10); // Simulate async analysis

            return new ModelAnalysis
            {
                ModelId = model.ModelId,
                AnalyzedAt = DateTime.UtcNow,
                ProjectType = model.ProjectType ?? "Commercial",
                ElementCounts = new Dictionary<string, int>(),
                LevelCount = 1,
                PhaseCount = 1,
                RoomCount = 0,
                HasMaterialAssignments = false,
                HasStructuralElements = false,
                HasDetailedMEP = false,
                HasDesignOptions = false,
                AvailableParameters = new HashSet<string>()
            };
        }
    }

    public class ScheduleTemplateSelector
    {
        public List<ScheduleTemplate> SelectTemplates(
            ProjectProfile profile,
            ModelAnalysis analysis,
            ScheduleGenerationOptions options)
        {
            var templates = new List<ScheduleTemplate>();

            // Select based on discipline and project type
            // Implementation would query template database

            return templates;
        }
    }

    public class FieldOptimizer
    {
        public List<ScheduleField> OptimizeFields(
            List<ScheduleField> defaultFields,
            ModelAnalysis analysis,
            string category)
        {
            var optimized = new List<ScheduleField>(defaultFields);

            // Remove fields that don't exist in model
            optimized.RemoveAll(f => !analysis.AvailableParameters.Contains(f.FieldName) &&
                !IsBuiltInField(f.FieldName));

            // Add commonly used fields that exist
            var commonFields = GetCommonFieldsForCategory(category);
            foreach (var field in commonFields)
            {
                if (analysis.AvailableParameters.Contains(field) &&
                    !optimized.Any(f => f.FieldName == field))
                {
                    optimized.Add(new ScheduleField { FieldName = field, Width = 80 });
                }
            }

            return optimized;
        }

        private bool IsBuiltInField(string fieldName)
        {
            var builtIn = new HashSet<string>
            {
                "Family and Type", "Type", "Level", "Phase", "Mark",
                "Comments", "Count", "Area", "Volume", "Length"
            };
            return builtIn.Contains(fieldName);
        }

        private List<string> GetCommonFieldsForCategory(string category)
        {
            return category switch
            {
                "Doors" => new List<string> { "Fire Rating", "Hardware Set", "Finish" },
                "Windows" => new List<string> { "Glazing", "Frame Material", "U-Value" },
                "Rooms" => new List<string> { "Department", "Occupancy", "Finish Floor" },
                _ => new List<string>()
            };
        }
    }

    public class ScheduleOrganizer
    {
        public ScheduleOrganization OrganizeSchedules(List<GeneratedSchedule> schedules)
        {
            return new ScheduleOrganization
            {
                ArchitecturalSchedules = schedules.Where(s => s.Discipline == Discipline.Architectural).ToList(),
                StructuralSchedules = schedules.Where(s => s.Discipline == Discipline.Structural).ToList(),
                MechanicalSchedules = schedules.Where(s => s.Discipline == Discipline.Mechanical).ToList(),
                ElectricalSchedules = schedules.Where(s => s.Discipline == Discipline.Electrical).ToList(),
                PlumbingSchedules = schedules.Where(s => s.Discipline == Discipline.Plumbing).ToList()
            };
        }
    }

    #endregion

    #region Data Models

    public class ModelContext
    {
        public string ModelId { get; set; }
        public string ModelPath { get; set; }
        public string ProjectType { get; set; }
    }

    public class ModelAnalysis
    {
        public string ModelId { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public string ProjectType { get; set; }
        public Dictionary<string, int> ElementCounts { get; set; }
        public int LevelCount { get; set; }
        public int PhaseCount { get; set; }
        public string CurrentPhase { get; set; }
        public int RoomCount { get; set; }
        public bool HasMaterialAssignments { get; set; }
        public bool HasStructuralElements { get; set; }
        public bool HasDetailedMEP { get; set; }
        public bool HasDesignOptions { get; set; }
        public HashSet<string> AvailableParameters { get; set; }
    }

    public class ScheduleGenerationOptions
    {
        public Discipline? DisciplineFilter { get; set; }
        public bool FilterByPhase { get; set; } = true;
        public bool FilterByDesignOption { get; set; } = true;
        public bool SeparateByLevel { get; set; } = false;
        public bool EnableGrouping { get; set; } = true;
        public bool IncludePhaseInName { get; set; } = false;
        public bool IncludeDateInName { get; set; } = false;

        public static ScheduleGenerationOptions Default => new ScheduleGenerationOptions();
    }

    public class ScheduleGenerationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string ModelId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ModelAnalysis ModelAnalysis { get; set; }
        public ProjectProfile ProjectProfile { get; set; }
        public List<GeneratedSchedule> GeneratedSchedules { get; set; } = new List<GeneratedSchedule>();
        public ScheduleOrganization ScheduleOrganization { get; set; }
        public ScheduleIndex ScheduleIndex { get; set; }
    }

    public class ProjectProfile
    {
        public string ProjectType { get; set; }
        public Discipline PrimaryDiscipline { get; set; }
        public List<Discipline> SecondaryDisciplines { get; set; } = new List<Discipline>();
        public ProjectScale Scale { get; set; }
        public string Phase { get; set; }
        public List<string> FocusAreas { get; set; } = new List<string>();
    }

    public class ScheduleTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public Discipline Discipline { get; set; }
        public List<ScheduleField> DefaultFields { get; set; }
        public List<ScheduleFilter> DefaultFilters { get; set; }
        public SortingConfiguration DefaultSorting { get; set; }
        public GroupingConfiguration DefaultGrouping { get; set; }
    }

    public class GeneratedSchedule
    {
        public string TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string GeneratedName { get; set; }
        public string Category { get; set; }
        public Discipline Discipline { get; set; }
        public List<ScheduleField> Fields { get; set; }
        public List<ScheduleFilter> Filters { get; set; }
        public SortingConfiguration SortingConfig { get; set; }
        public GroupingConfiguration GroupingConfig { get; set; }
        public FormattingConfiguration Formatting { get; set; }
        public int RowCount { get; set; }
        public ScheduleValidationResult ValidationResult { get; set; }
    }

    public class ScheduleField
    {
        public string FieldName { get; set; }
        public int Width { get; set; }
        public string Format { get; set; }
        public bool Hidden { get; set; }
        public bool CalculatedTotal { get; set; }
    }

    public class ScheduleFilter
    {
        public string FieldName { get; set; }
        public FilterType FilterType { get; set; }
        public string Value { get; set; }
    }

    public enum FilterType
    {
        Equals,
        NotEquals,
        Contains,
        GreaterThan,
        LessThan,
        BeginsWith,
        EndsWith
    }

    public class SortingConfiguration
    {
        public SortField PrimarySort { get; set; }
        public SortField SecondarySort { get; set; }
        public SortField TertiarySort { get; set; }
    }

    public class SortField
    {
        public string FieldName { get; set; }
        public bool Ascending { get; set; }
    }

    public class GroupingConfiguration
    {
        public List<GroupField> GroupByFields { get; set; } = new List<GroupField>();
    }

    public class GroupField
    {
        public string FieldName { get; set; }
        public bool ShowHeader { get; set; }
        public bool ShowFooter { get; set; }
        public bool ShowCount { get; set; }
    }

    public class FormattingConfiguration
    {
        public bool ShowTitle { get; set; }
        public bool ShowHeaders { get; set; }
        public bool ShowGridLines { get; set; }
        public bool AlternateRowShading { get; set; }
        public TextFormat TitleFormat { get; set; }
        public TextFormat HeaderFormat { get; set; }
        public TextFormat BodyFormat { get; set; }
    }

    public class TextFormat
    {
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public int Size { get; set; }
        public string FontName { get; set; }
    }

    public class ScheduleValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class ScheduleOrganization
    {
        public List<GeneratedSchedule> ArchitecturalSchedules { get; set; }
        public List<GeneratedSchedule> StructuralSchedules { get; set; }
        public List<GeneratedSchedule> MechanicalSchedules { get; set; }
        public List<GeneratedSchedule> ElectricalSchedules { get; set; }
        public List<GeneratedSchedule> PlumbingSchedules { get; set; }
    }

    public class ScheduleIndex
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalSchedules { get; set; }
        public Dictionary<Discipline, List<GeneratedSchedule>> ByDiscipline { get; set; }
        public Dictionary<string, List<GeneratedSchedule>> ByCategory { get; set; }
    }

    public class ScheduleSuggestion
    {
        public string ScheduleType { get; set; }
        public string Priority { get; set; }
        public string Reason { get; set; }
        public string RecommendedTemplate { get; set; }
    }

    public class StandardSchedule
    {
        public string Type { get; set; }
        public string Priority { get; set; }
        public string TemplateId { get; set; }
    }

    public enum Discipline
    {
        Architectural,
        Structural,
        Mechanical,
        Electrical,
        Plumbing,
        FireProtection,
        Civil
    }

    public enum ProjectScale
    {
        Small,
        Medium,
        Large,
        Mega
    }

    #endregion
}
