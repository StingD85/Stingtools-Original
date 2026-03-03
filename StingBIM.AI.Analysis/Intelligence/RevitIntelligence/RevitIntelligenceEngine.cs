// ============================================================================
// StingBIM AI - Revit Intelligence Engine
// Comprehensive Revit-specific BIM operations and model optimization
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RevitIntelligence
{
    /// <summary>
    /// Revit Intelligence Engine providing advanced model analysis,
    /// family management, performance optimization, and Revit-specific workflows.
    /// </summary>
    public sealed class RevitIntelligenceEngine
    {
        private static readonly Lazy<RevitIntelligenceEngine> _instance =
            new Lazy<RevitIntelligenceEngine>(() => new RevitIntelligenceEngine());
        public static RevitIntelligenceEngine Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, ModelHealthReport> _healthReports = new();
        private readonly Dictionary<string, FamilyAuditReport> _familyReports = new();
        private readonly Dictionary<string, ViewTemplateLibrary> _viewTemplates = new();
        private readonly List<ModelingRule> _modelingRules = new();
        private readonly List<PerformanceRecommendation> _performanceRules = new();
        private readonly Dictionary<string, WorksetStrategy> _worksetStrategies = new();

        public event EventHandler<ModelAlertEventArgs> ModelAlert;
        public event EventHandler<OptimizationEventArgs> OptimizationSuggested;

        private RevitIntelligenceEngine()
        {
            InitializeModelingRules();
            InitializePerformanceRules();
            InitializeWorksetStrategies();
        }

        #region Initialization

        private void InitializeModelingRules()
        {
            _modelingRules.AddRange(new[]
            {
                new ModelingRule
                {
                    RuleId = "MR001",
                    Name = "Wall Attachment",
                    Category = "Walls",
                    Description = "Walls should be attached to floors/roofs above",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = true
                },
                new ModelingRule
                {
                    RuleId = "MR002",
                    Name = "Room Bounding",
                    Category = "Rooms",
                    Description = "All rooms must have valid bounding elements",
                    Severity = RuleSeverity.Error,
                    AutoFixable = false
                },
                new ModelingRule
                {
                    RuleId = "MR003",
                    Name = "Level Association",
                    Category = "General",
                    Description = "Elements should be associated with correct levels",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = true
                },
                new ModelingRule
                {
                    RuleId = "MR004",
                    Name = "Workset Assignment",
                    Category = "Worksets",
                    Description = "All elements should be assigned to appropriate worksets",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = true
                },
                new ModelingRule
                {
                    RuleId = "MR005",
                    Name = "Parameter Population",
                    Category = "Data",
                    Description = "Required parameters must be populated",
                    Severity = RuleSeverity.Error,
                    AutoFixable = false
                },
                new ModelingRule
                {
                    RuleId = "MR006",
                    Name = "Family Naming",
                    Category = "Families",
                    Description = "Families should follow naming convention",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = false
                },
                new ModelingRule
                {
                    RuleId = "MR007",
                    Name = "View Template Usage",
                    Category = "Views",
                    Description = "Project views should use view templates",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = true
                },
                new ModelingRule
                {
                    RuleId = "MR008",
                    Name = "Group Usage",
                    Category = "Groups",
                    Description = "Avoid excessive use of groups",
                    Severity = RuleSeverity.Info,
                    AutoFixable = false
                },
                new ModelingRule
                {
                    RuleId = "MR009",
                    Name = "Design Option Cleanup",
                    Category = "Design Options",
                    Description = "Remove unused design options",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = true
                },
                new ModelingRule
                {
                    RuleId = "MR010",
                    Name = "Phasing Consistency",
                    Category = "Phasing",
                    Description = "Elements should have consistent phase assignments",
                    Severity = RuleSeverity.Warning,
                    AutoFixable = false
                }
            });
        }

        private void InitializePerformanceRules()
        {
            _performanceRules.AddRange(new[]
            {
                new PerformanceRecommendation
                {
                    Id = "PERF001",
                    Title = "Excessive DWG Imports",
                    Description = "Model contains many DWG imports which impact performance",
                    Threshold = 50,
                    Unit = "DWG files",
                    Recommendation = "Link DWGs instead of importing, or convert to native Revit elements",
                    Impact = PerformanceImpact.High
                },
                new PerformanceRecommendation
                {
                    Id = "PERF002",
                    Title = "Large Image Files",
                    Description = "Raster images larger than recommended",
                    Threshold = 5,
                    Unit = "MB per image",
                    Recommendation = "Compress images or reduce resolution",
                    Impact = PerformanceImpact.Medium
                },
                new PerformanceRecommendation
                {
                    Id = "PERF003",
                    Title = "Excessive Warnings",
                    Description = "Model has many warnings affecting stability",
                    Threshold = 100,
                    Unit = "warnings",
                    Recommendation = "Resolve warnings systematically",
                    Impact = PerformanceImpact.High
                },
                new PerformanceRecommendation
                {
                    Id = "PERF004",
                    Title = "Unused Families",
                    Description = "Model contains families that are not placed",
                    Threshold = 50,
                    Unit = "unused families",
                    Recommendation = "Purge unused families",
                    Impact = PerformanceImpact.Medium
                },
                new PerformanceRecommendation
                {
                    Id = "PERF005",
                    Title = "In-Place Families",
                    Description = "Excessive use of in-place families",
                    Threshold = 20,
                    Unit = "in-place families",
                    Recommendation = "Convert to loadable families",
                    Impact = PerformanceImpact.High
                },
                new PerformanceRecommendation
                {
                    Id = "PERF006",
                    Title = "Array Groups",
                    Description = "Large array groups impact performance",
                    Threshold = 100,
                    Unit = "members per array",
                    Recommendation = "Ungroup or use component arrays",
                    Impact = PerformanceImpact.Medium
                },
                new PerformanceRecommendation
                {
                    Id = "PERF007",
                    Title = "Complex Filled Regions",
                    Description = "Overly complex filled regions in views",
                    Threshold = 500,
                    Unit = "vertices",
                    Recommendation = "Simplify geometry or use masking regions",
                    Impact = PerformanceImpact.Low
                },
                new PerformanceRecommendation
                {
                    Id = "PERF008",
                    Title = "Model Size",
                    Description = "Model file size exceeds recommended limits",
                    Threshold = 500,
                    Unit = "MB",
                    Recommendation = "Consider splitting into linked models",
                    Impact = PerformanceImpact.Critical
                }
            });
        }

        private void InitializeWorksetStrategies()
        {
            _worksetStrategies["Standard"] = new WorksetStrategy
            {
                StrategyId = "STD",
                Name = "Standard Project",
                Description = "Standard workset organization for typical projects",
                Worksets = new List<WorksetDefinition>
                {
                    new WorksetDefinition { Name = "00_Links", Purpose = "External references and links", DefaultVisibility = false },
                    new WorksetDefinition { Name = "01_Shared Levels and Grids", Purpose = "Datums shared across disciplines", DefaultVisibility = true },
                    new WorksetDefinition { Name = "02_Core and Shell", Purpose = "Building core and exterior shell", DefaultVisibility = true },
                    new WorksetDefinition { Name = "03_Interior", Purpose = "Interior partitions and finishes", DefaultVisibility = true },
                    new WorksetDefinition { Name = "04_Furniture", Purpose = "Furniture and loose equipment", DefaultVisibility = false },
                    new WorksetDefinition { Name = "05_Site", Purpose = "Site elements", DefaultVisibility = true },
                    new WorksetDefinition { Name = "99_Working", Purpose = "Temporary working elements", DefaultVisibility = false }
                }
            };

            _worksetStrategies["LargeProject"] = new WorksetStrategy
            {
                StrategyId = "LRG",
                Name = "Large Project",
                Description = "Detailed workset organization for large/complex projects",
                Worksets = new List<WorksetDefinition>
                {
                    new WorksetDefinition { Name = "00_Links_Architecture", Purpose = "Architectural links" },
                    new WorksetDefinition { Name = "00_Links_Structure", Purpose = "Structural links" },
                    new WorksetDefinition { Name = "00_Links_MEP", Purpose = "MEP links" },
                    new WorksetDefinition { Name = "00_Links_Civil", Purpose = "Civil/site links" },
                    new WorksetDefinition { Name = "00_Links_CAD", Purpose = "CAD file links" },
                    new WorksetDefinition { Name = "01_Grids", Purpose = "Grid lines" },
                    new WorksetDefinition { Name = "01_Levels", Purpose = "Level datums" },
                    new WorksetDefinition { Name = "02_Exterior_Walls", Purpose = "Exterior walls and curtain walls" },
                    new WorksetDefinition { Name = "02_Roof", Purpose = "Roof elements" },
                    new WorksetDefinition { Name = "03_Interior_Walls", Purpose = "Interior partitions" },
                    new WorksetDefinition { Name = "03_Floors", Purpose = "Floor slabs" },
                    new WorksetDefinition { Name = "03_Ceilings", Purpose = "Ceiling elements" },
                    new WorksetDefinition { Name = "04_Doors", Purpose = "Doors" },
                    new WorksetDefinition { Name = "04_Windows", Purpose = "Windows" },
                    new WorksetDefinition { Name = "05_Stairs_Ramps", Purpose = "Vertical circulation" },
                    new WorksetDefinition { Name = "06_Furniture", Purpose = "Furniture systems" },
                    new WorksetDefinition { Name = "06_Equipment", Purpose = "Equipment" },
                    new WorksetDefinition { Name = "07_Site", Purpose = "Site elements" },
                    new WorksetDefinition { Name = "99_Working", Purpose = "Temporary elements" }
                }
            };

            _worksetStrategies["MEP"] = new WorksetStrategy
            {
                StrategyId = "MEP",
                Name = "MEP Model",
                Description = "Workset organization for MEP models",
                Worksets = new List<WorksetDefinition>
                {
                    new WorksetDefinition { Name = "00_Links", Purpose = "Architecture and structure links" },
                    new WorksetDefinition { Name = "01_Shared", Purpose = "Shared datums and spaces" },
                    new WorksetDefinition { Name = "M_Ductwork", Purpose = "HVAC ductwork" },
                    new WorksetDefinition { Name = "M_Piping", Purpose = "Mechanical piping" },
                    new WorksetDefinition { Name = "M_Equipment", Purpose = "Mechanical equipment" },
                    new WorksetDefinition { Name = "E_Power", Purpose = "Electrical power" },
                    new WorksetDefinition { Name = "E_Lighting", Purpose = "Lighting systems" },
                    new WorksetDefinition { Name = "E_LowVoltage", Purpose = "Low voltage systems" },
                    new WorksetDefinition { Name = "P_Piping", Purpose = "Plumbing piping" },
                    new WorksetDefinition { Name = "P_Fixtures", Purpose = "Plumbing fixtures" },
                    new WorksetDefinition { Name = "FP_Systems", Purpose = "Fire protection" },
                    new WorksetDefinition { Name = "99_Working", Purpose = "Temporary elements" }
                }
            };
        }

        #endregion

        #region Model Health Analysis

        /// <summary>
        /// Perform comprehensive model health analysis
        /// </summary>
        public async Task<ModelHealthReport> AnalyzeModelHealthAsync(ModelHealthRequest request)
        {
            var report = new ModelHealthReport
            {
                ReportId = Guid.NewGuid().ToString(),
                ModelPath = request.ModelPath,
                ModelName = request.ModelName,
                AnalysisDate = DateTime.UtcNow,
                Metrics = new ModelMetrics(),
                Issues = new List<ModelIssue>(),
                Recommendations = new List<string>(),
                OverallScore = 100
            };

            // Analyze model metrics
            report.Metrics = AnalyzeMetrics(request.ModelData);

            // Check against modeling rules
            foreach (var rule in _modelingRules)
            {
                var violations = CheckRule(rule, request.ModelData);
                foreach (var violation in violations)
                {
                    report.Issues.Add(violation);
                    report.OverallScore -= GetScoreDeduction(violation.Severity);
                }
            }

            // Check performance
            foreach (var perfRule in _performanceRules)
            {
                var issue = CheckPerformance(perfRule, request.ModelData);
                if (issue != null)
                {
                    report.Issues.Add(issue);
                    report.OverallScore -= GetScoreDeduction(issue.Severity);
                    report.Recommendations.Add(perfRule.Recommendation);
                }
            }

            // Ensure score doesn't go below 0
            report.OverallScore = Math.Max(0, report.OverallScore);

            // Categorize health
            report.HealthStatus = report.OverallScore >= 90 ? HealthStatus.Excellent :
                                  report.OverallScore >= 75 ? HealthStatus.Good :
                                  report.OverallScore >= 50 ? HealthStatus.Fair :
                                  HealthStatus.Poor;

            lock (_lock)
            {
                _healthReports[report.ReportId] = report;
            }

            // Trigger alerts for critical issues
            if (report.Issues.Any(i => i.Severity == RuleSeverity.Critical))
            {
                ModelAlert?.Invoke(this, new ModelAlertEventArgs
                {
                    AlertType = AlertType.Critical,
                    Message = $"Model health critical: {report.Issues.Count(i => i.Severity == RuleSeverity.Critical)} critical issues found",
                    ModelName = request.ModelName
                });
            }

            return report;
        }

        private ModelMetrics AnalyzeMetrics(ModelData data)
        {
            return new ModelMetrics
            {
                FileSize = data?.FileSize ?? 0,
                ElementCount = data?.ElementCount ?? 0,
                FamilyCount = data?.FamilyCount ?? 0,
                ViewCount = data?.ViewCount ?? 0,
                SheetCount = data?.SheetCount ?? 0,
                WarningCount = data?.WarningCount ?? 0,
                LinkCount = data?.LinkCount ?? 0,
                GroupCount = data?.GroupCount ?? 0,
                RoomCount = data?.RoomCount ?? 0,
                LevelCount = data?.LevelCount ?? 0,
                WorksetCount = data?.WorksetCount ?? 0,
                DesignOptionCount = data?.DesignOptionCount ?? 0
            };
        }

        private List<ModelIssue> CheckRule(ModelingRule rule, ModelData data)
        {
            var issues = new List<ModelIssue>();

            // Simulate rule checking based on model data
            switch (rule.RuleId)
            {
                case "MR003": // Level Association
                    if (data?.UnassociatedElements > 0)
                    {
                        issues.Add(new ModelIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            RuleId = rule.RuleId,
                            Category = rule.Category,
                            Description = $"{data.UnassociatedElements} elements not properly associated with levels",
                            Severity = rule.Severity,
                            AffectedElementCount = data.UnassociatedElements
                        });
                    }
                    break;

                case "MR005": // Parameter Population
                    if (data?.MissingParameterCount > 0)
                    {
                        issues.Add(new ModelIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            RuleId = rule.RuleId,
                            Category = rule.Category,
                            Description = $"{data.MissingParameterCount} elements with missing required parameters",
                            Severity = rule.Severity,
                            AffectedElementCount = data.MissingParameterCount
                        });
                    }
                    break;

                case "MR007": // View Template Usage
                    if (data?.ViewsWithoutTemplate > 0)
                    {
                        issues.Add(new ModelIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            RuleId = rule.RuleId,
                            Category = rule.Category,
                            Description = $"{data.ViewsWithoutTemplate} views not using view templates",
                            Severity = rule.Severity,
                            AffectedElementCount = data.ViewsWithoutTemplate
                        });
                    }
                    break;
            }

            return issues;
        }

        private ModelIssue CheckPerformance(PerformanceRecommendation rule, ModelData data)
        {
            double value = rule.Id switch
            {
                "PERF001" => data?.DwgImportCount ?? 0,
                "PERF003" => data?.WarningCount ?? 0,
                "PERF004" => data?.UnusedFamilyCount ?? 0,
                "PERF005" => data?.InPlaceFamilyCount ?? 0,
                "PERF008" => data?.FileSize ?? 0,
                _ => 0
            };

            if (value > rule.Threshold)
            {
                return new ModelIssue
                {
                    IssueId = Guid.NewGuid().ToString(),
                    RuleId = rule.Id,
                    Category = "Performance",
                    Description = $"{rule.Title}: {value} {rule.Unit} (threshold: {rule.Threshold})",
                    Severity = rule.Impact == PerformanceImpact.Critical ? RuleSeverity.Critical :
                              rule.Impact == PerformanceImpact.High ? RuleSeverity.Error :
                              RuleSeverity.Warning
                };
            }

            return null;
        }

        private int GetScoreDeduction(RuleSeverity severity)
        {
            return severity switch
            {
                RuleSeverity.Critical => 20,
                RuleSeverity.Error => 10,
                RuleSeverity.Warning => 5,
                RuleSeverity.Info => 1,
                _ => 0
            };
        }

        #endregion

        #region Family Management

        /// <summary>
        /// Audit families in a model
        /// </summary>
        public FamilyAuditReport AuditFamilies(FamilyAuditRequest request)
        {
            var report = new FamilyAuditReport
            {
                ReportId = Guid.NewGuid().ToString(),
                ModelName = request.ModelName,
                AuditDate = DateTime.UtcNow,
                FamilyAnalyses = new List<FamilyAnalysis>(),
                Summary = new FamilyAuditSummary()
            };

            // Analyze each family
            foreach (var family in request.Families ?? new List<FamilyInfo>())
            {
                var analysis = AnalyzeFamily(family);
                report.FamilyAnalyses.Add(analysis);
            }

            // Generate summary
            report.Summary = new FamilyAuditSummary
            {
                TotalFamilies = report.FamilyAnalyses.Count,
                PlacedFamilies = report.FamilyAnalyses.Count(f => f.InstanceCount > 0),
                UnusedFamilies = report.FamilyAnalyses.Count(f => f.InstanceCount == 0),
                InPlaceFamilies = report.FamilyAnalyses.Count(f => f.IsInPlace),
                OversizedFamilies = report.FamilyAnalyses.Count(f => f.FileSize > 2),
                NamingViolations = report.FamilyAnalyses.Count(f => !f.FollowsNamingConvention),
                FamiliesByCategory = report.FamilyAnalyses
                    .GroupBy(f => f.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TotalFileSize = report.FamilyAnalyses.Sum(f => f.FileSize),
                RecommendedActions = GenerateFamilyRecommendations(report.FamilyAnalyses)
            };

            lock (_lock)
            {
                _familyReports[report.ReportId] = report;
            }

            return report;
        }

        private FamilyAnalysis AnalyzeFamily(FamilyInfo family)
        {
            var analysis = new FamilyAnalysis
            {
                FamilyId = family.FamilyId,
                FamilyName = family.FamilyName,
                Category = family.Category,
                InstanceCount = family.InstanceCount,
                TypeCount = family.TypeCount,
                FileSize = family.FileSize,
                IsInPlace = family.IsInPlace,
                HasNestedFamilies = family.NestedFamilyCount > 0,
                NestedFamilyCount = family.NestedFamilyCount,
                ParameterCount = family.ParameterCount,
                FollowsNamingConvention = CheckFamilyNaming(family.FamilyName),
                Issues = new List<string>(),
                Recommendations = new List<string>()
            };

            // Check for issues
            if (analysis.InstanceCount == 0)
            {
                analysis.Issues.Add("Family is loaded but not placed in model");
                analysis.Recommendations.Add("Consider purging if not needed");
            }

            if (analysis.FileSize > 2) // MB
            {
                analysis.Issues.Add($"Family file size ({analysis.FileSize:F1} MB) exceeds recommended 2 MB");
                analysis.Recommendations.Add("Optimize family geometry and nested content");
            }

            if (analysis.IsInPlace)
            {
                analysis.Issues.Add("In-place family impacts model performance");
                analysis.Recommendations.Add("Convert to loadable family if possible");
            }

            if (analysis.NestedFamilyCount > 10)
            {
                analysis.Issues.Add($"Excessive nested families ({analysis.NestedFamilyCount})");
                analysis.Recommendations.Add("Review and simplify nested family structure");
            }

            if (!analysis.FollowsNamingConvention)
            {
                analysis.Issues.Add("Family name does not follow naming convention");
                analysis.Recommendations.Add("Rename to follow project standards");
            }

            analysis.HealthScore = 100 - (analysis.Issues.Count * 15);
            analysis.HealthScore = Math.Max(0, analysis.HealthScore);

            return analysis;
        }

        private bool CheckFamilyNaming(string familyName)
        {
            // Check common naming patterns
            // Expected: Category_Type_Description or similar
            if (string.IsNullOrEmpty(familyName))
                return false;

            // Check for common issues
            if (familyName.Contains(" ") && !familyName.Contains("_"))
                return false;

            if (familyName.StartsWith("Family") || familyName.StartsWith("Copy"))
                return false;

            return true;
        }

        private List<string> GenerateFamilyRecommendations(List<FamilyAnalysis> analyses)
        {
            var recommendations = new List<string>();

            var unusedCount = analyses.Count(f => f.InstanceCount == 0);
            if (unusedCount > 10)
                recommendations.Add($"Purge {unusedCount} unused families to reduce file size");

            var inPlaceCount = analyses.Count(f => f.IsInPlace);
            if (inPlaceCount > 5)
                recommendations.Add($"Convert {inPlaceCount} in-place families to loadable families");

            var oversizedCount = analyses.Count(f => f.FileSize > 2);
            if (oversizedCount > 0)
                recommendations.Add($"Optimize {oversizedCount} oversized families");

            var namingIssues = analyses.Count(f => !f.FollowsNamingConvention);
            if (namingIssues > 0)
                recommendations.Add($"Review naming convention for {namingIssues} families");

            return recommendations;
        }

        /// <summary>
        /// Get family optimization suggestions
        /// </summary>
        public List<FamilyOptimization> GetFamilyOptimizations(string familyId, FamilyInfo family)
        {
            var optimizations = new List<FamilyOptimization>();

            if (family.FileSize > 1)
            {
                optimizations.Add(new FamilyOptimization
                {
                    Type = OptimizationType.ReduceFileSize,
                    Description = "Reduce family file size",
                    Steps = new List<string>
                    {
                        "Remove unused reference planes",
                        "Simplify complex geometry",
                        "Reduce nested family count",
                        "Use symbolic lines instead of model lines where appropriate",
                        "Optimize imported geometry"
                    },
                    ExpectedImprovement = "30-50% file size reduction"
                });
            }

            if (family.NestedFamilyCount > 5)
            {
                optimizations.Add(new FamilyOptimization
                {
                    Type = OptimizationType.SimplifyStructure,
                    Description = "Simplify nested family structure",
                    Steps = new List<string>
                    {
                        "Combine similar nested families",
                        "Use shared nested families",
                        "Remove unused nested families",
                        "Consider using family types instead of nested families"
                    },
                    ExpectedImprovement = "Improved load time and stability"
                });
            }

            if (family.ParameterCount > 50)
            {
                optimizations.Add(new FamilyOptimization
                {
                    Type = OptimizationType.OptimizeParameters,
                    Description = "Optimize parameter count",
                    Steps = new List<string>
                    {
                        "Remove unused parameters",
                        "Consolidate similar parameters",
                        "Use type parameters instead of instance where appropriate",
                        "Review formula complexity"
                    },
                    ExpectedImprovement = "Faster family editing and placement"
                });
            }

            return optimizations;
        }

        #endregion

        #region View Management

        /// <summary>
        /// Create a view template library for a project
        /// </summary>
        public ViewTemplateLibrary CreateViewTemplateLibrary(ViewTemplateLibraryRequest request)
        {
            var library = new ViewTemplateLibrary
            {
                LibraryId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                CreatedDate = DateTime.UtcNow,
                Templates = new List<ViewTemplateDefinition>()
            };

            // Add standard architectural templates
            library.Templates.AddRange(new[]
            {
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-AR-PLAN",
                    Name = "AR_Floor Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Architecture",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    VisibilitySettings = new Dictionary<string, bool>
                    {
                        { "Walls", true },
                        { "Doors", true },
                        { "Windows", true },
                        { "Furniture", true },
                        { "MEP", false },
                        { "Structure", true }
                    },
                    Purpose = "Standard architectural floor plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-AR-RCP",
                    Name = "AR_Reflected Ceiling Plan",
                    ViewType = "Ceiling Plan",
                    Discipline = "Architecture",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    VisibilitySettings = new Dictionary<string, bool>
                    {
                        { "Ceilings", true },
                        { "Lighting", true },
                        { "HVAC Diffusers", true },
                        { "Sprinklers", true },
                        { "Walls", true }
                    },
                    Purpose = "Reflected ceiling plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-AR-ELEV",
                    Name = "AR_Elevation",
                    ViewType = "Elevation",
                    Discipline = "Architecture",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    Purpose = "Building elevations"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-AR-SECT",
                    Name = "AR_Section",
                    ViewType = "Section",
                    Discipline = "Architecture",
                    Scale = "1:50",
                    DetailLevel = "Fine",
                    Purpose = "Building sections"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-AR-DET",
                    Name = "AR_Detail",
                    ViewType = "Detail",
                    Discipline = "Architecture",
                    Scale = "1:10",
                    DetailLevel = "Fine",
                    Purpose = "Architectural details"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-AR-3D",
                    Name = "AR_3D View",
                    ViewType = "3D",
                    Discipline = "Architecture",
                    DetailLevel = "Medium",
                    Purpose = "3D visualization"
                }
            });

            // Add structural templates
            library.Templates.AddRange(new[]
            {
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-ST-PLAN",
                    Name = "ST_Structural Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Structure",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    VisibilitySettings = new Dictionary<string, bool>
                    {
                        { "Structural Framing", true },
                        { "Structural Columns", true },
                        { "Structural Foundations", true },
                        { "Architecture", false }
                    },
                    Purpose = "Structural framing plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-ST-FDN",
                    Name = "ST_Foundation Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Structure",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    Purpose = "Foundation plans"
                }
            });

            // Add MEP templates
            library.Templates.AddRange(new[]
            {
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-ME-HVAC",
                    Name = "ME_HVAC Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Mechanical",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    VisibilitySettings = new Dictionary<string, bool>
                    {
                        { "Ductwork", true },
                        { "Mechanical Equipment", true },
                        { "Architecture", true },
                        { "Electrical", false },
                        { "Plumbing", false }
                    },
                    Purpose = "HVAC ductwork plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-EL-PWR",
                    Name = "EL_Power Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Electrical",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    Purpose = "Electrical power plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-EL-LTG",
                    Name = "EL_Lighting Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Electrical",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    Purpose = "Lighting plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-PL-PLAN",
                    Name = "PL_Plumbing Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Plumbing",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    Purpose = "Plumbing plans"
                },
                new ViewTemplateDefinition
                {
                    TemplateId = "VT-FP-PLAN",
                    Name = "FP_Fire Protection Plan",
                    ViewType = "Floor Plan",
                    Discipline = "Fire Protection",
                    Scale = "1:100",
                    DetailLevel = "Medium",
                    Purpose = "Fire protection plans"
                }
            });

            lock (_lock)
            {
                _viewTemplates[library.LibraryId] = library;
            }

            return library;
        }

        /// <summary>
        /// Analyze view organization
        /// </summary>
        public ViewOrganizationAnalysis AnalyzeViews(ViewAnalysisRequest request)
        {
            var analysis = new ViewOrganizationAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                AnalysisDate = DateTime.UtcNow,
                TotalViews = request.Views?.Count ?? 0,
                ViewsByType = new Dictionary<string, int>(),
                ViewsWithTemplates = 0,
                ViewsWithoutTemplates = 0,
                UnusedViews = 0,
                Issues = new List<ViewIssue>(),
                Recommendations = new List<string>()
            };

            foreach (var view in request.Views ?? new List<ViewInfo>())
            {
                // Count by type
                if (!analysis.ViewsByType.ContainsKey(view.ViewType))
                    analysis.ViewsByType[view.ViewType] = 0;
                analysis.ViewsByType[view.ViewType]++;

                // Check template usage
                if (view.HasTemplate)
                    analysis.ViewsWithTemplates++;
                else
                {
                    analysis.ViewsWithoutTemplates++;
                    if (view.IsOnSheet)
                    {
                        analysis.Issues.Add(new ViewIssue
                        {
                            ViewName = view.ViewName,
                            Issue = "View on sheet without template",
                            Severity = "Warning"
                        });
                    }
                }

                // Check for unused views
                if (!view.IsOnSheet && !view.ViewName.StartsWith("Working"))
                {
                    analysis.UnusedViews++;
                }
            }

            // Generate recommendations
            if (analysis.ViewsWithoutTemplates > analysis.TotalViews * 0.3)
            {
                analysis.Recommendations.Add("More than 30% of views lack templates - consider standardizing with view templates");
            }

            if (analysis.UnusedViews > 50)
            {
                analysis.Recommendations.Add($"{analysis.UnusedViews} views not placed on sheets - review and clean up");
            }

            return analysis;
        }

        #endregion

        #region Workset Management

        /// <summary>
        /// Get recommended workset strategy
        /// </summary>
        public WorksetStrategy GetWorksetStrategy(string strategyType)
        {
            if (_worksetStrategies.TryGetValue(strategyType, out var strategy))
                return strategy;

            return _worksetStrategies["Standard"];
        }

        /// <summary>
        /// Analyze workset usage
        /// </summary>
        public WorksetAnalysis AnalyzeWorksets(WorksetAnalysisRequest request)
        {
            var analysis = new WorksetAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                AnalysisDate = DateTime.UtcNow,
                WorksetStatuses = new List<WorksetStatus>(),
                Issues = new List<WorksetIssue>(),
                Recommendations = new List<string>()
            };

            foreach (var workset in request.Worksets ?? new List<WorksetInfo>())
            {
                var status = new WorksetStatus
                {
                    WorksetName = workset.Name,
                    ElementCount = workset.ElementCount,
                    Owner = workset.Owner,
                    IsEditable = workset.IsEditable,
                    FollowsNaming = workset.Name.Contains("_") || workset.Name.StartsWith("0")
                };

                analysis.WorksetStatuses.Add(status);

                // Check for issues
                if (workset.ElementCount == 0)
                {
                    analysis.Issues.Add(new WorksetIssue
                    {
                        WorksetName = workset.Name,
                        Issue = "Empty workset",
                        Severity = "Info"
                    });
                }

                if (workset.ElementCount > 10000)
                {
                    analysis.Issues.Add(new WorksetIssue
                    {
                        WorksetName = workset.Name,
                        Issue = "Workset contains many elements - consider splitting",
                        Severity = "Warning"
                    });
                }

                if (!status.FollowsNaming)
                {
                    analysis.Issues.Add(new WorksetIssue
                    {
                        WorksetName = workset.Name,
                        Issue = "Workset name doesn't follow convention",
                        Severity = "Warning"
                    });
                }
            }

            // Overall recommendations
            if (request.Worksets?.Count < 5)
            {
                analysis.Recommendations.Add("Consider creating more worksets for better model organization");
            }

            if (request.Worksets?.Count > 30)
            {
                analysis.Recommendations.Add("Many worksets present - review if all are necessary");
            }

            return analysis;
        }

        #endregion

        #region Performance Optimization

        /// <summary>
        /// Get performance optimization recommendations
        /// </summary>
        public List<PerformanceOptimization> GetPerformanceOptimizations(ModelData modelData)
        {
            var optimizations = new List<PerformanceOptimization>();

            // File size optimization
            if (modelData.FileSize > 300)
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Category = "File Size",
                    Priority = modelData.FileSize > 500 ? OptimizationPriority.Critical : OptimizationPriority.High,
                    Issue = $"Model file size ({modelData.FileSize} MB) is large",
                    Recommendations = new List<string>
                    {
                        "Audit and compact the model",
                        "Purge unused families and types",
                        "Review and remove unused views",
                        "Consider splitting into linked models",
                        "Optimize family content"
                    },
                    ExpectedImprovement = "20-40% file size reduction"
                });
            }

            // Warning cleanup
            if (modelData.WarningCount > 100)
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Category = "Warnings",
                    Priority = modelData.WarningCount > 500 ? OptimizationPriority.Critical : OptimizationPriority.High,
                    Issue = $"{modelData.WarningCount} warnings in model",
                    Recommendations = new List<string>
                    {
                        "Export warning report",
                        "Prioritize room bounding warnings",
                        "Fix duplicate instance warnings",
                        "Resolve constraint warnings",
                        "Address join warnings systematically"
                    },
                    ExpectedImprovement = "Improved model stability and performance"
                });
            }

            // DWG imports
            if (modelData.DwgImportCount > 20)
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Category = "DWG Files",
                    Priority = OptimizationPriority.Medium,
                    Issue = $"{modelData.DwgImportCount} DWG files imported",
                    Recommendations = new List<string>
                    {
                        "Link DWGs instead of importing",
                        "Convert critical DWG content to Revit elements",
                        "Remove unused DWG imports",
                        "Reduce DWG detail level where possible"
                    },
                    ExpectedImprovement = "Faster file open and save times"
                });
            }

            // Groups
            if (modelData.GroupCount > 50)
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Category = "Groups",
                    Priority = OptimizationPriority.Medium,
                    Issue = $"{modelData.GroupCount} groups in model",
                    Recommendations = new List<string>
                    {
                        "Review necessity of each group",
                        "Ungroup where not needed for repetition",
                        "Consider using assemblies instead",
                        "Avoid nested groups"
                    },
                    ExpectedImprovement = "Better model editability"
                });
            }

            // In-place families
            if (modelData.InPlaceFamilyCount > 10)
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Category = "In-Place Families",
                    Priority = OptimizationPriority.High,
                    Issue = $"{modelData.InPlaceFamilyCount} in-place families",
                    Recommendations = new List<string>
                    {
                        "Convert to loadable families",
                        "Use model-in-place only for unique conditions",
                        "Review each in-place family for necessity"
                    },
                    ExpectedImprovement = "Significant performance improvement"
                });
            }

            return optimizations;
        }

        /// <summary>
        /// Generate model cleanup script recommendations
        /// </summary>
        public ModelCleanupPlan GenerateCleanupPlan(ModelData modelData)
        {
            return new ModelCleanupPlan
            {
                PlanId = Guid.NewGuid().ToString(),
                GeneratedDate = DateTime.UtcNow,
                Steps = new List<CleanupStep>
                {
                    new CleanupStep
                    {
                        Order = 1,
                        Name = "Create Backup",
                        Description = "Create a backup copy of the model before cleanup",
                        IsCritical = true
                    },
                    new CleanupStep
                    {
                        Order = 2,
                        Name = "Review Warnings",
                        Description = "Export and review warning report",
                        IsCritical = false
                    },
                    new CleanupStep
                    {
                        Order = 3,
                        Name = "Resolve Critical Warnings",
                        Description = "Fix room bounding, duplicate instances, and constraint warnings",
                        IsCritical = true
                    },
                    new CleanupStep
                    {
                        Order = 4,
                        Name = "Purge Unused",
                        Description = "Run purge unused multiple times",
                        IsCritical = false
                    },
                    new CleanupStep
                    {
                        Order = 5,
                        Name = "Audit Model",
                        Description = "Open with audit option enabled",
                        IsCritical = false
                    },
                    new CleanupStep
                    {
                        Order = 6,
                        Name = "Compact Central",
                        Description = "Save and compact the central model",
                        IsCritical = false
                    },
                    new CleanupStep
                    {
                        Order = 7,
                        Name = "Review Views",
                        Description = "Delete unused working views",
                        IsCritical = false
                    },
                    new CleanupStep
                    {
                        Order = 8,
                        Name = "Check Links",
                        Description = "Verify all linked models are current",
                        IsCritical = false
                    }
                },
                EstimatedDuration = CalculateCleanupDuration(modelData),
                Warnings = new List<string>
                {
                    "Ensure all users are synced before cleanup",
                    "Schedule cleanup during low-activity periods",
                    "Verify backup before proceeding"
                }
            };
        }

        private TimeSpan CalculateCleanupDuration(ModelData data)
        {
            // Estimate based on model complexity
            var minutes = 30; // Base time
            minutes += (int)(data.FileSize / 100) * 10; // Add time for large files
            minutes += (int)(data.WarningCount / 100) * 5; // Add time for warnings
            return TimeSpan.FromMinutes(minutes);
        }

        #endregion

        #region Queries

        public List<ModelingRule> GetModelingRules() => _modelingRules.ToList();

        public List<PerformanceRecommendation> GetPerformanceRules() => _performanceRules.ToList();

        public ModelHealthReport GetHealthReport(string reportId)
        {
            lock (_lock)
            {
                return _healthReports.TryGetValue(reportId, out var report) ? report : null;
            }
        }

        public FamilyAuditReport GetFamilyReport(string reportId)
        {
            lock (_lock)
            {
                return _familyReports.TryGetValue(reportId, out var report) ? report : null;
            }
        }

        #endregion
    }

    #region Data Models

    public class ModelHealthRequest
    {
        public string ModelPath { get; set; }
        public string ModelName { get; set; }
        public ModelData ModelData { get; set; }
    }

    public class ModelData
    {
        public double FileSize { get; set; }
        public int ElementCount { get; set; }
        public int FamilyCount { get; set; }
        public int ViewCount { get; set; }
        public int SheetCount { get; set; }
        public int WarningCount { get; set; }
        public int LinkCount { get; set; }
        public int GroupCount { get; set; }
        public int RoomCount { get; set; }
        public int LevelCount { get; set; }
        public int WorksetCount { get; set; }
        public int DesignOptionCount { get; set; }
        public int DwgImportCount { get; set; }
        public int InPlaceFamilyCount { get; set; }
        public int UnusedFamilyCount { get; set; }
        public int UnassociatedElements { get; set; }
        public int MissingParameterCount { get; set; }
        public int ViewsWithoutTemplate { get; set; }
    }

    public class ModelHealthReport
    {
        public string ReportId { get; set; }
        public string ModelPath { get; set; }
        public string ModelName { get; set; }
        public DateTime AnalysisDate { get; set; }
        public ModelMetrics Metrics { get; set; }
        public List<ModelIssue> Issues { get; set; }
        public List<string> Recommendations { get; set; }
        public int OverallScore { get; set; }
        public HealthStatus HealthStatus { get; set; }
    }

    public class ModelMetrics
    {
        public double FileSize { get; set; }
        public int ElementCount { get; set; }
        public int FamilyCount { get; set; }
        public int ViewCount { get; set; }
        public int SheetCount { get; set; }
        public int WarningCount { get; set; }
        public int LinkCount { get; set; }
        public int GroupCount { get; set; }
        public int RoomCount { get; set; }
        public int LevelCount { get; set; }
        public int WorksetCount { get; set; }
        public int DesignOptionCount { get; set; }
    }

    public class ModelIssue
    {
        public string IssueId { get; set; }
        public string RuleId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public RuleSeverity Severity { get; set; }
        public int AffectedElementCount { get; set; }
    }

    public class ModelingRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public RuleSeverity Severity { get; set; }
        public bool AutoFixable { get; set; }
    }

    public class PerformanceRecommendation
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Threshold { get; set; }
        public string Unit { get; set; }
        public string Recommendation { get; set; }
        public PerformanceImpact Impact { get; set; }
    }

    public class FamilyAuditRequest
    {
        public string ModelName { get; set; }
        public List<FamilyInfo> Families { get; set; }
    }

    public class FamilyInfo
    {
        public string FamilyId { get; set; }
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public int InstanceCount { get; set; }
        public int TypeCount { get; set; }
        public double FileSize { get; set; }
        public bool IsInPlace { get; set; }
        public int NestedFamilyCount { get; set; }
        public int ParameterCount { get; set; }
    }

    public class FamilyAuditReport
    {
        public string ReportId { get; set; }
        public string ModelName { get; set; }
        public DateTime AuditDate { get; set; }
        public List<FamilyAnalysis> FamilyAnalyses { get; set; }
        public FamilyAuditSummary Summary { get; set; }
    }

    public class FamilyAnalysis
    {
        public string FamilyId { get; set; }
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public int InstanceCount { get; set; }
        public int TypeCount { get; set; }
        public double FileSize { get; set; }
        public bool IsInPlace { get; set; }
        public bool HasNestedFamilies { get; set; }
        public int NestedFamilyCount { get; set; }
        public int ParameterCount { get; set; }
        public bool FollowsNamingConvention { get; set; }
        public int HealthScore { get; set; }
        public List<string> Issues { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class FamilyAuditSummary
    {
        public int TotalFamilies { get; set; }
        public int PlacedFamilies { get; set; }
        public int UnusedFamilies { get; set; }
        public int InPlaceFamilies { get; set; }
        public int OversizedFamilies { get; set; }
        public int NamingViolations { get; set; }
        public Dictionary<string, int> FamiliesByCategory { get; set; }
        public double TotalFileSize { get; set; }
        public List<string> RecommendedActions { get; set; }
    }

    public class FamilyOptimization
    {
        public OptimizationType Type { get; set; }
        public string Description { get; set; }
        public List<string> Steps { get; set; }
        public string ExpectedImprovement { get; set; }
    }

    public class ViewTemplateLibrary
    {
        public string LibraryId { get; set; }
        public string ProjectId { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ViewTemplateDefinition> Templates { get; set; }
    }

    public class ViewTemplateLibraryRequest
    {
        public string ProjectId { get; set; }
    }

    public class ViewTemplateDefinition
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
        public string Discipline { get; set; }
        public string Scale { get; set; }
        public string DetailLevel { get; set; }
        public Dictionary<string, bool> VisibilitySettings { get; set; }
        public string Purpose { get; set; }
    }

    public class ViewAnalysisRequest
    {
        public List<ViewInfo> Views { get; set; }
    }

    public class ViewInfo
    {
        public string ViewId { get; set; }
        public string ViewName { get; set; }
        public string ViewType { get; set; }
        public bool HasTemplate { get; set; }
        public bool IsOnSheet { get; set; }
    }

    public class ViewOrganizationAnalysis
    {
        public string AnalysisId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int TotalViews { get; set; }
        public Dictionary<string, int> ViewsByType { get; set; }
        public int ViewsWithTemplates { get; set; }
        public int ViewsWithoutTemplates { get; set; }
        public int UnusedViews { get; set; }
        public List<ViewIssue> Issues { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ViewIssue
    {
        public string ViewName { get; set; }
        public string Issue { get; set; }
        public string Severity { get; set; }
    }

    public class WorksetStrategy
    {
        public string StrategyId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<WorksetDefinition> Worksets { get; set; }
    }

    public class WorksetDefinition
    {
        public string Name { get; set; }
        public string Purpose { get; set; }
        public bool DefaultVisibility { get; set; } = true;
    }

    public class WorksetAnalysisRequest
    {
        public List<WorksetInfo> Worksets { get; set; }
    }

    public class WorksetInfo
    {
        public string Name { get; set; }
        public int ElementCount { get; set; }
        public string Owner { get; set; }
        public bool IsEditable { get; set; }
    }

    public class WorksetAnalysis
    {
        public string AnalysisId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<WorksetStatus> WorksetStatuses { get; set; }
        public List<WorksetIssue> Issues { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class WorksetStatus
    {
        public string WorksetName { get; set; }
        public int ElementCount { get; set; }
        public string Owner { get; set; }
        public bool IsEditable { get; set; }
        public bool FollowsNaming { get; set; }
    }

    public class WorksetIssue
    {
        public string WorksetName { get; set; }
        public string Issue { get; set; }
        public string Severity { get; set; }
    }

    public class PerformanceOptimization
    {
        public string Category { get; set; }
        public OptimizationPriority Priority { get; set; }
        public string Issue { get; set; }
        public List<string> Recommendations { get; set; }
        public string ExpectedImprovement { get; set; }
    }

    public class ModelCleanupPlan
    {
        public string PlanId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public List<CleanupStep> Steps { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class CleanupStep
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsCritical { get; set; }
    }

    public class ModelAlertEventArgs : EventArgs
    {
        public AlertType AlertType { get; set; }
        public string Message { get; set; }
        public string ModelName { get; set; }
    }

    public class OptimizationEventArgs : EventArgs
    {
        public string Category { get; set; }
        public string Recommendation { get; set; }
    }

    #endregion

    #region Enums

    public enum RuleSeverity { Info, Warning, Error, Critical }
    public enum PerformanceImpact { Low, Medium, High, Critical }
    public enum HealthStatus { Excellent, Good, Fair, Poor }
    public enum OptimizationType { ReduceFileSize, SimplifyStructure, OptimizeParameters, Other }
    public enum OptimizationPriority { Low, Medium, High, Critical }
    public enum AlertType { Info, Warning, Error, Critical }

    #endregion
}
