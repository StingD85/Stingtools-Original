// ============================================================================
// StingBIM AI - Model Health Monitor
// Continuously monitors BIM model quality and health metrics
// Detects issues, tracks improvements, and provides recommendations
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Automation.Health
{
    /// <summary>
    /// Model Health Monitor
    /// Tracks and reports on BIM model quality metrics
    /// </summary>
    public class ModelHealthMonitor
    {
        private readonly QualityChecker _qualityChecker;
        private readonly PerformanceAnalyzer _performanceAnalyzer;
        private readonly ComplianceTracker _complianceTracker;
        private readonly TrendAnalyzer _trendAnalyzer;
        private readonly Dictionary<string, HealthThreshold> _thresholds;

        public ModelHealthMonitor()
        {
            _qualityChecker = new QualityChecker();
            _performanceAnalyzer = new PerformanceAnalyzer();
            _complianceTracker = new ComplianceTracker();
            _trendAnalyzer = new TrendAnalyzer();
            _thresholds = LoadHealthThresholds();
        }

        #region Health Assessment

        /// <summary>
        /// Run comprehensive model health assessment
        /// </summary>
        public async Task<ModelHealthReport> AssessHealthAsync(
            BIMModel model,
            HealthCheckOptions options = null)
        {
            options ??= HealthCheckOptions.Default;

            var report = new ModelHealthReport
            {
                ModelId = model.ModelId,
                ModelName = model.ModelName,
                AssessedAt = DateTime.UtcNow
            };

            // Run all health checks in parallel
            var tasks = new List<Task>
            {
                Task.Run(async () => report.DataQuality = await CheckDataQualityAsync(model, options)),
                Task.Run(async () => report.ModelStructure = await CheckModelStructureAsync(model, options)),
                Task.Run(async () => report.Performance = await CheckPerformanceAsync(model, options)),
                Task.Run(async () => report.Standards = await CheckStandardsComplianceAsync(model, options)),
                Task.Run(async () => report.Geometry = await CheckGeometryHealthAsync(model, options))
            };

            await Task.WhenAll(tasks);

            // Calculate overall health score
            report.OverallScore = CalculateOverallScore(report);
            report.HealthGrade = GetHealthGrade(report.OverallScore);

            // Generate prioritized issues list
            report.Issues = AggregateIssues(report);

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(report);

            // Compare with previous assessment if available
            if (options.PreviousReport != null)
            {
                report.Comparison = CompareWithPrevious(report, options.PreviousReport);
            }

            return report;
        }

        /// <summary>
        /// Quick health check for continuous monitoring
        /// </summary>
        public async Task<QuickHealthStatus> QuickCheckAsync(BIMModel model)
        {
            var status = new QuickHealthStatus
            {
                ModelId = model.ModelId,
                CheckedAt = DateTime.UtcNow
            };

            // Check critical metrics only
            status.WarningCount = await CountWarningsAsync(model);
            status.ErrorCount = await CountErrorsAsync(model);
            status.UnplacedElements = await CountUnplacedElementsAsync(model);
            status.DuplicateInstances = await CountDuplicatesAsync(model);

            // Quick performance check
            status.ElementCount = model.ElementCount;
            status.ViewCount = model.ViewCount;
            status.FamilyCount = model.FamilyCount;

            // Determine overall status
            status.Status = DetermineQuickStatus(status);

            return status;
        }

        #endregion

        #region Data Quality Checks

        private async Task<DataQualityMetrics> CheckDataQualityAsync(BIMModel model, HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metrics = new DataQualityMetrics();

            // Parameter completeness
            metrics.ParameterCompleteness = await CheckParameterCompletenessAsync(model, options);

            // Naming conventions
            metrics.NamingCompliance = await CheckNamingConventionsAsync(model, options);

            // Material assignments
            metrics.MaterialAssignment = await CheckMaterialAssignmentsAsync(model);

            // Phase/workset assignments
            metrics.PhaseAssignment = await CheckPhaseAssignmentsAsync(model);
            metrics.WorksetAssignment = await CheckWorksetAssignmentsAsync(model);

            // Classification compliance
            metrics.ClassificationCompliance = await CheckClassificationAsync(model, options);

            // Calculate overall data quality score
            metrics.Score = CalculateDataQualityScore(metrics);

            return metrics;
        }

        private async Task<CompletenessMetric> CheckParameterCompletenessAsync(
            BIMModel model,
            HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metric = new CompletenessMetric();
            var requiredParams = options.RequiredParameters ?? GetDefaultRequiredParameters();

            foreach (var element in model.Elements)
            {
                metric.TotalChecked++;

                var missingParams = requiredParams
                    .Where(p => p.AppliesTo(element.Category))
                    .Where(p => !element.HasParameterValue(p.Name))
                    .ToList();

                if (missingParams.Any())
                {
                    metric.IncompleteCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Parameter Completeness",
                        ElementId = element.ElementId,
                        Description = $"Missing parameters: {string.Join(", ", missingParams.Select(p => p.Name))}",
                        Recommendation = "Fill in required parameter values"
                    });
                }
                else
                {
                    metric.CompleteCount++;
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.CompleteCount / metric.TotalChecked * 100
                : 100;

            return metric;
        }

        private async Task<ComplianceMetric> CheckNamingConventionsAsync(
            BIMModel model,
            HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metric = new ComplianceMetric();
            var patterns = options.NamingPatterns ?? GetDefaultNamingPatterns();

            // Check family names
            foreach (var family in model.Families)
            {
                metric.TotalChecked++;

                var pattern = patterns.FirstOrDefault(p => p.AppliesTo == "Family");
                if (pattern != null && !pattern.Matches(family.Name))
                {
                    metric.NonCompliantCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Naming Convention",
                        ElementId = family.FamilyId,
                        Description = $"Family name '{family.Name}' doesn't match pattern",
                        Recommendation = $"Rename to match pattern: {pattern.Description}"
                    });
                }
                else
                {
                    metric.CompliantCount++;
                }
            }

            // Check view names
            foreach (var view in model.Views)
            {
                metric.TotalChecked++;

                var pattern = patterns.FirstOrDefault(p => p.AppliesTo == "View");
                if (pattern != null && !pattern.Matches(view.Name))
                {
                    metric.NonCompliantCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Info,
                        Category = "Naming Convention",
                        ElementId = view.ViewId,
                        Description = $"View name '{view.Name}' doesn't match pattern"
                    });
                }
                else
                {
                    metric.CompliantCount++;
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.CompliantCount / metric.TotalChecked * 100
                : 100;

            return metric;
        }

        private async Task<AssignmentMetric> CheckMaterialAssignmentsAsync(BIMModel model)
        {
            await Task.Delay(1);

            var metric = new AssignmentMetric();

            var categoriesRequiringMaterials = new HashSet<string>
            {
                "Walls", "Floors", "Ceilings", "Roofs", "Doors", "Windows",
                "Structural Columns", "Structural Beams"
            };

            foreach (var element in model.Elements.Where(e =>
                categoriesRequiringMaterials.Contains(e.Category)))
            {
                metric.TotalChecked++;

                if (string.IsNullOrEmpty(element.Material))
                {
                    metric.UnassignedCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Material Assignment",
                        ElementId = element.ElementId,
                        Description = $"{element.Category} element has no material assigned"
                    });
                }
                else
                {
                    metric.AssignedCount++;
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.AssignedCount / metric.TotalChecked * 100
                : 100;

            return metric;
        }

        private Task<AssignmentMetric> CheckPhaseAssignmentsAsync(BIMModel model)
            => Task.FromResult(new AssignmentMetric { Percentage = 100 });

        private Task<AssignmentMetric> CheckWorksetAssignmentsAsync(BIMModel model)
            => Task.FromResult(new AssignmentMetric { Percentage = 100 });

        private Task<ComplianceMetric> CheckClassificationAsync(BIMModel model, HealthCheckOptions options)
            => Task.FromResult(new ComplianceMetric { Percentage = 100 });

        #endregion

        #region Model Structure Checks

        private async Task<ModelStructureMetrics> CheckModelStructureAsync(
            BIMModel model,
            HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metrics = new ModelStructureMetrics();

            // Linked model health
            metrics.LinkedModels = await CheckLinkedModelsAsync(model);

            // Room/Space containment
            metrics.RoomContainment = await CheckRoomContainmentAsync(model);

            // Level usage
            metrics.LevelUsage = await CheckLevelUsageAsync(model);

            // Group health
            metrics.GroupHealth = await CheckGroupHealthAsync(model);

            // Workset organization
            metrics.WorksetOrganization = await CheckWorksetOrganizationAsync(model);

            metrics.Score = CalculateStructureScore(metrics);

            return metrics;
        }

        private Task<LinkedModelHealth> CheckLinkedModelsAsync(BIMModel model)
        {
            return Task.FromResult(new LinkedModelHealth
            {
                TotalLinks = model.LinkedModels?.Count ?? 0,
                LoadedLinks = model.LinkedModels?.Count(l => l.IsLoaded) ?? 0,
                UnloadedLinks = model.LinkedModels?.Count(l => !l.IsLoaded) ?? 0
            });
        }

        private async Task<ContainmentMetric> CheckRoomContainmentAsync(BIMModel model)
        {
            await Task.Delay(1);

            var metric = new ContainmentMetric();

            var roomBoundingElements = model.Elements.Where(e =>
                e.Category == "Doors" || e.Category == "Windows" ||
                e.Category == "Furniture" || e.Category == "Plumbing Fixtures");

            foreach (var element in roomBoundingElements)
            {
                metric.TotalChecked++;

                if (string.IsNullOrEmpty(element.Room))
                {
                    metric.UncontainedCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Room Containment",
                        ElementId = element.ElementId,
                        Description = $"{element.Category} not in any room"
                    });
                }
                else
                {
                    metric.ContainedCount++;
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.ContainedCount / metric.TotalChecked * 100
                : 100;

            return metric;
        }

        private Task<LevelUsageMetric> CheckLevelUsageAsync(BIMModel model)
        {
            return Task.FromResult(new LevelUsageMetric
            {
                TotalLevels = model.Levels?.Count ?? 0,
                UsedLevels = model.Levels?.Count(l => l.ElementCount > 0) ?? 0,
                UnusedLevels = model.Levels?.Count(l => l.ElementCount == 0) ?? 0
            });
        }

        private Task<GroupHealthMetric> CheckGroupHealthAsync(BIMModel model)
        {
            return Task.FromResult(new GroupHealthMetric
            {
                TotalGroups = model.Groups?.Count ?? 0,
                UnusedGroups = model.Groups?.Count(g => g.InstanceCount == 0) ?? 0
            });
        }

        private Task<WorksetMetric> CheckWorksetOrganizationAsync(BIMModel model)
        {
            return Task.FromResult(new WorksetMetric { Score = 100 });
        }

        #endregion

        #region Performance Checks

        private async Task<PerformanceMetrics> CheckPerformanceAsync(
            BIMModel model,
            HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metrics = new PerformanceMetrics
            {
                ElementCount = model.ElementCount,
                ViewCount = model.ViewCount,
                FamilyCount = model.FamilyCount,
                FileSize = model.FileSize
            };

            // Check against thresholds
            var threshold = _thresholds["Performance"];

            metrics.ElementCountStatus = model.ElementCount > threshold.MaxElements
                ? StatusLevel.Warning : StatusLevel.Good;

            metrics.ViewCountStatus = model.ViewCount > threshold.MaxViews
                ? StatusLevel.Warning : StatusLevel.Good;

            metrics.FileSizeStatus = model.FileSize > threshold.MaxFileSize
                ? StatusLevel.Warning : StatusLevel.Good;

            // Check for performance issues
            metrics.Issues = new List<HealthIssue>();

            // In-place families (performance impact)
            var inPlaceFamilies = model.Families.Where(f => f.IsInPlace).ToList();
            if (inPlaceFamilies.Count > threshold.MaxInPlaceFamilies)
            {
                metrics.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Performance",
                    Description = $"{inPlaceFamilies.Count} in-place families detected (limit: {threshold.MaxInPlaceFamilies})",
                    Recommendation = "Convert in-place families to loadable families"
                });
            }

            // Imported CAD (performance impact)
            var importedCad = model.ImportedCad?.Count ?? 0;
            if (importedCad > threshold.MaxImportedCad)
            {
                metrics.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Performance",
                    Description = $"{importedCad} imported CAD files detected",
                    Recommendation = "Link CAD files instead of importing, or convert to Revit geometry"
                });
            }

            // Unused families
            var unusedFamilies = model.Families.Where(f => f.InstanceCount == 0).ToList();
            if (unusedFamilies.Count > 50)
            {
                metrics.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = "Performance",
                    Description = $"{unusedFamilies.Count} unused families in project",
                    Recommendation = "Purge unused families to reduce file size"
                });
            }

            metrics.Score = CalculatePerformanceScore(metrics);

            return metrics;
        }

        #endregion

        #region Standards Compliance Checks

        private async Task<StandardsMetrics> CheckStandardsComplianceAsync(
            BIMModel model,
            HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metrics = new StandardsMetrics();

            // ISO 19650 compliance
            metrics.ISO19650Compliance = CheckISO19650Compliance(model);

            // BIM Execution Plan compliance
            metrics.BEPCompliance = options.BEPRequirements != null
                ? CheckBEPCompliance(model, options.BEPRequirements)
                : null;

            // LOD compliance
            metrics.LODCompliance = CheckLODCompliance(model, options.RequiredLOD);

            metrics.Score = CalculateStandardsScore(metrics);

            return metrics;
        }

        private ComplianceMetric CheckISO19650Compliance(BIMModel model)
        {
            var metric = new ComplianceMetric();

            // Check naming convention (ISO 19650-2)
            // Check information container structure
            // Check status and revision

            metric.Percentage = 85; // Placeholder

            return metric;
        }

        private ComplianceMetric CheckBEPCompliance(BIMModel model, BEPRequirements bep)
        {
            var metric = new ComplianceMetric();

            // Check against BEP requirements
            metric.Percentage = 90; // Placeholder

            return metric;
        }

        private LODMetric CheckLODCompliance(BIMModel model, int requiredLOD)
        {
            var metric = new LODMetric
            {
                RequiredLOD = requiredLOD
            };

            // Check geometric detail level
            // Check parameter data level

            metric.AchievedLOD = 300; // Placeholder
            metric.Compliant = metric.AchievedLOD >= metric.RequiredLOD;

            return metric;
        }

        #endregion

        #region Geometry Health Checks

        private async Task<GeometryMetrics> CheckGeometryHealthAsync(
            BIMModel model,
            HealthCheckOptions options)
        {
            await Task.Delay(1);

            var metrics = new GeometryMetrics();

            // Duplicate elements
            metrics.Duplicates = await CheckDuplicateElementsAsync(model);

            // Overlapping elements
            metrics.Overlaps = await CheckOverlappingElementsAsync(model);

            // Unconnected elements
            metrics.UnconnectedElements = await CheckUnconnectedElementsAsync(model);

            // Small/zero volume elements
            metrics.SmallElements = await CheckSmallElementsAsync(model);

            metrics.Score = CalculateGeometryScore(metrics);

            return metrics;
        }

        private async Task<DuplicateMetric> CheckDuplicateElementsAsync(BIMModel model)
        {
            await Task.Delay(1);

            var metric = new DuplicateMetric();

            // Group elements by location and type
            var groups = model.Elements
                .GroupBy(e => $"{e.TypeName}_{e.Location?.X:F0}_{e.Location?.Y:F0}_{e.Location?.Z:F0}")
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                var duplicates = group.Skip(1).ToList();
                metric.DuplicateCount += duplicates.Count;

                foreach (var dup in duplicates)
                {
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = "Geometry",
                        ElementId = dup.ElementId,
                        Description = $"Duplicate {dup.Category} at same location",
                        Recommendation = "Delete duplicate element"
                    });
                }
            }

            return metric;
        }

        private Task<OverlapMetric> CheckOverlappingElementsAsync(BIMModel model)
        {
            return Task.FromResult(new OverlapMetric { OverlapCount = 0 });
        }

        private Task<ConnectionMetric> CheckUnconnectedElementsAsync(BIMModel model)
        {
            return Task.FromResult(new ConnectionMetric { UnconnectedCount = 0 });
        }

        private Task<SmallElementMetric> CheckSmallElementsAsync(BIMModel model)
        {
            return Task.FromResult(new SmallElementMetric { SmallElementCount = 0 });
        }

        #endregion

        #region Quick Check Methods

        private Task<int> CountWarningsAsync(BIMModel model)
            => Task.FromResult(model.Warnings?.Count ?? 0);

        private Task<int> CountErrorsAsync(BIMModel model)
            => Task.FromResult(model.Errors?.Count ?? 0);

        private Task<int> CountUnplacedElementsAsync(BIMModel model)
            => Task.FromResult(model.Elements.Count(e => e.Location == null));

        private Task<int> CountDuplicatesAsync(BIMModel model)
            => Task.FromResult(0);

        private HealthStatus DetermineQuickStatus(QuickHealthStatus status)
        {
            if (status.ErrorCount > 0) return HealthStatus.Critical;
            if (status.WarningCount > 10 || status.DuplicateInstances > 50) return HealthStatus.Warning;
            if (status.WarningCount > 0) return HealthStatus.NeedsAttention;
            return HealthStatus.Healthy;
        }

        #endregion

        #region Score Calculations

        private double CalculateOverallScore(ModelHealthReport report)
        {
            var weights = new Dictionary<string, double>
            {
                { "DataQuality", 0.25 },
                { "ModelStructure", 0.20 },
                { "Performance", 0.20 },
                { "Standards", 0.20 },
                { "Geometry", 0.15 }
            };

            double score = 0;
            score += report.DataQuality.Score * weights["DataQuality"];
            score += report.ModelStructure.Score * weights["ModelStructure"];
            score += report.Performance.Score * weights["Performance"];
            score += report.Standards.Score * weights["Standards"];
            score += report.Geometry.Score * weights["Geometry"];

            return score;
        }

        private string GetHealthGrade(double score)
        {
            if (score >= 90) return "A";
            if (score >= 80) return "B";
            if (score >= 70) return "C";
            if (score >= 60) return "D";
            return "F";
        }

        private double CalculateDataQualityScore(DataQualityMetrics metrics)
        {
            var scores = new List<double>
            {
                metrics.ParameterCompleteness.Percentage,
                metrics.NamingCompliance.Percentage,
                metrics.MaterialAssignment.Percentage,
                metrics.PhaseAssignment.Percentage
            };

            return scores.Average();
        }

        private double CalculateStructureScore(ModelStructureMetrics metrics)
        {
            return (metrics.RoomContainment.Percentage +
                   metrics.LevelUsage.UsagePercentage +
                   metrics.GroupHealth.HealthPercentage) / 3;
        }

        private double CalculatePerformanceScore(PerformanceMetrics metrics)
        {
            double score = 100;

            if (metrics.ElementCountStatus == StatusLevel.Warning) score -= 15;
            if (metrics.ViewCountStatus == StatusLevel.Warning) score -= 10;
            if (metrics.FileSizeStatus == StatusLevel.Warning) score -= 10;
            score -= metrics.Issues.Count * 5;

            return Math.Max(0, score);
        }

        private double CalculateStandardsScore(StandardsMetrics metrics)
        {
            return metrics.ISO19650Compliance.Percentage;
        }

        private double CalculateGeometryScore(GeometryMetrics metrics)
        {
            double score = 100;
            score -= metrics.Duplicates.DuplicateCount * 2;
            score -= metrics.Overlaps.OverlapCount * 3;
            score -= metrics.SmallElements.SmallElementCount;

            return Math.Max(0, score);
        }

        #endregion

        #region Issue Aggregation and Recommendations

        private List<HealthIssue> AggregateIssues(ModelHealthReport report)
        {
            var allIssues = new List<HealthIssue>();

            allIssues.AddRange(report.DataQuality.ParameterCompleteness.Issues);
            allIssues.AddRange(report.DataQuality.NamingCompliance.Issues);
            allIssues.AddRange(report.DataQuality.MaterialAssignment.Issues);
            allIssues.AddRange(report.ModelStructure.RoomContainment.Issues);
            allIssues.AddRange(report.Performance.Issues);
            allIssues.AddRange(report.Geometry.Duplicates.Issues);

            return allIssues
                .OrderByDescending(i => i.Severity)
                .ThenBy(i => i.Category)
                .ToList();
        }

        private List<HealthRecommendation> GenerateRecommendations(ModelHealthReport report)
        {
            var recommendations = new List<HealthRecommendation>();

            // Data quality recommendations
            if (report.DataQuality.Score < 80)
            {
                recommendations.Add(new HealthRecommendation
                {
                    Priority = "High",
                    Category = "Data Quality",
                    Title = "Improve Parameter Completeness",
                    Description = "Several elements are missing required parameter values",
                    Impact = "Better data extraction and scheduling",
                    Effort = "Medium"
                });
            }

            // Performance recommendations
            if (report.Performance.Score < 70)
            {
                recommendations.Add(new HealthRecommendation
                {
                    Priority = "High",
                    Category = "Performance",
                    Title = "Optimize Model Performance",
                    Description = "Model shows performance concerns that may slow down work",
                    Impact = "Faster model operations and reduced crashes",
                    Effort = "High"
                });
            }

            // Geometry recommendations
            if (report.Geometry.Duplicates.DuplicateCount > 0)
            {
                recommendations.Add(new HealthRecommendation
                {
                    Priority = "Medium",
                    Category = "Geometry",
                    Title = "Remove Duplicate Elements",
                    Description = $"{report.Geometry.Duplicates.DuplicateCount} duplicate elements detected",
                    Impact = "Accurate quantities and cleaner model",
                    Effort = "Low"
                });
            }

            return recommendations;
        }

        private HealthComparison CompareWithPrevious(ModelHealthReport current, ModelHealthReport previous)
        {
            return new HealthComparison
            {
                ScoreChange = current.OverallScore - previous.OverallScore,
                IssueCountChange = current.Issues.Count - previous.Issues.Count,
                NewIssues = current.Issues.Count(i =>
                    !previous.Issues.Any(p => p.ElementId == i.ElementId && p.Category == i.Category)),
                ResolvedIssues = previous.Issues.Count(p =>
                    !current.Issues.Any(c => c.ElementId == p.ElementId && c.Category == p.Category)),
                Trend = current.OverallScore > previous.OverallScore ? "Improving" :
                       current.OverallScore < previous.OverallScore ? "Declining" : "Stable"
            };
        }

        #endregion

        #region Configuration

        private Dictionary<string, HealthThreshold> LoadHealthThresholds()
        {
            return new Dictionary<string, HealthThreshold>
            {
                { "Performance", new HealthThreshold
                    {
                        MaxElements = 500000,
                        MaxViews = 1000,
                        MaxFileSize = 500 * 1024 * 1024, // 500 MB
                        MaxInPlaceFamilies = 20,
                        MaxImportedCad = 10
                    }
                }
            };
        }

        private List<RequiredParameter> GetDefaultRequiredParameters()
        {
            return new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors", "Windows" } },
                new RequiredParameter { Name = "Fire Rating", Categories = new[] { "Doors", "Walls" } },
                new RequiredParameter { Name = "Comments", Categories = new[] { "All" } }
            };
        }

        private List<NamingPattern> GetDefaultNamingPatterns()
        {
            return new List<NamingPattern>
            {
                new NamingPattern { AppliesTo = "Family", Pattern = @"^[A-Z]{2,4}_.*", Description = "PREFIX_Name" },
                new NamingPattern { AppliesTo = "View", Pattern = @"^(Level|Section|Detail|3D).*", Description = "Type prefix" }
            };
        }

        #endregion
    }

    #region Data Models

    public class BIMModel
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public int ElementCount { get; set; }
        public int ViewCount { get; set; }
        public int FamilyCount { get; set; }
        public long FileSize { get; set; }
        public List<ModelElement> Elements { get; set; } = new List<ModelElement>();
        public List<ModelFamily> Families { get; set; } = new List<ModelFamily>();
        public List<ModelView> Views { get; set; } = new List<ModelView>();
        public List<ModelLevel> Levels { get; set; } = new List<ModelLevel>();
        public List<ModelGroup> Groups { get; set; } = new List<ModelGroup>();
        public List<LinkedModel> LinkedModels { get; set; } = new List<LinkedModel>();
        public List<ImportedCad> ImportedCad { get; set; } = new List<ImportedCad>();
        public List<ModelWarning> Warnings { get; set; } = new List<ModelWarning>();
        public List<ModelError> Errors { get; set; } = new List<ModelError>();
    }

    public class ModelElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string Material { get; set; }
        public string Room { get; set; }
        public Point3D Location { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        public bool HasParameterValue(string name)
        {
            return Parameters.ContainsKey(name) && Parameters[name] != null;
        }
    }

    public class ModelFamily
    {
        public string FamilyId { get; set; }
        public string Name { get; set; }
        public bool IsInPlace { get; set; }
        public int InstanceCount { get; set; }
    }

    public class ModelView
    {
        public string ViewId { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
    }

    public class ModelLevel
    {
        public string LevelId { get; set; }
        public string Name { get; set; }
        public int ElementCount { get; set; }
    }

    public class ModelGroup
    {
        public string GroupId { get; set; }
        public string Name { get; set; }
        public int InstanceCount { get; set; }
    }

    public class LinkedModel
    {
        public string LinkId { get; set; }
        public string Name { get; set; }
        public bool IsLoaded { get; set; }
    }

    public class ImportedCad { public string Name { get; set; } }
    public class ModelWarning { public string Message { get; set; } }
    public class ModelError { public string Message { get; set; } }

    public class HealthCheckOptions
    {
        public List<RequiredParameter> RequiredParameters { get; set; }
        public List<NamingPattern> NamingPatterns { get; set; }
        public BEPRequirements BEPRequirements { get; set; }
        public int RequiredLOD { get; set; } = 300;
        public ModelHealthReport PreviousReport { get; set; }

        public static HealthCheckOptions Default => new HealthCheckOptions();
    }

    public class RequiredParameter
    {
        public string Name { get; set; }
        public string[] Categories { get; set; }

        public bool AppliesTo(string category)
        {
            return Categories.Contains("All") || Categories.Contains(category);
        }
    }

    public class NamingPattern
    {
        public string AppliesTo { get; set; }
        public string Pattern { get; set; }
        public string Description { get; set; }

        public bool Matches(string name)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(name, Pattern);
        }
    }

    /// <summary>
    /// BIM Execution Plan (BEP) requirements definition.
    /// Defines LOD expectations per project phase, naming conventions,
    /// required parameter groups, model origin rules, and delivery milestones.
    /// </summary>
    public class BEPRequirements
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// LOD requirements mapped by project phase name (e.g., "Concept", "Design Development").
        /// Values range from 100 (LOD100) to 500 (LOD500).
        /// </summary>
        public Dictionary<string, int> LODRequirementsByPhase { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Regex pattern that all file names in the project must follow.
        /// Example: "^[A-Z]{2}-[A-Z]{3}-XX-[0-9]{2}-DR-[A-Z]-[0-9]{4}$" for ISO 19650.
        /// </summary>
        public string FileNamingConventionPattern { get; set; }

        /// <summary>
        /// List of parameter group names that must be present and populated on relevant elements.
        /// Examples: "Identity Data", "Phasing", "IFC Parameters".
        /// </summary>
        public List<string> RequiredParameterGroups { get; set; } = new List<string>();

        /// <summary>
        /// Defines the shared coordinate origin point the model must be based on.
        /// Null means no origin requirement.
        /// </summary>
        public ModelOriginRequirement ModelOrigin { get; set; }

        /// <summary>
        /// Delivery milestones with expected dates and minimum health scores.
        /// </summary>
        public List<DeliveryMilestone> DeliveryMilestones { get; set; } = new List<DeliveryMilestone>();

        /// <summary>
        /// Minimum overall health score required for BEP compliance (0-100).
        /// </summary>
        public double MinimumHealthScore { get; set; } = 70.0;

        /// <summary>
        /// Maximum number of critical issues allowed in the model.
        /// </summary>
        public int MaxCriticalIssues { get; set; } = 0;

        /// <summary>
        /// Maximum number of warning-level issues allowed in the model.
        /// </summary>
        public int MaxWarningIssues { get; set; } = 50;

        /// <summary>
        /// Validates whether a model health report meets the BEP requirements.
        /// Returns a list of validation issues found.
        /// </summary>
        public List<HealthIssue> Validate(ModelHealthReport report)
        {
            Logger.Info("Validating model '{0}' against BEP requirements", report.ModelName);
            var issues = new List<HealthIssue>();

            // Check overall health score
            if (report.OverallScore < MinimumHealthScore)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "BEP Compliance",
                    Description = $"Overall health score {report.OverallScore:F1} is below BEP minimum of {MinimumHealthScore:F1}",
                    Recommendation = "Address data quality, performance, and geometry issues to raise the health score"
                });
            }

            // Check critical issue count
            if (report.Issues != null)
            {
                int criticalCount = report.Issues.Count(i => i.Severity == IssueSeverity.Critical);
                if (criticalCount > MaxCriticalIssues)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = "BEP Compliance",
                        Description = $"Model has {criticalCount} critical issues; BEP allows maximum {MaxCriticalIssues}",
                        Recommendation = "Resolve all critical issues before delivery"
                    });
                }

                int warningCount = report.Issues.Count(i => i.Severity == IssueSeverity.Warning);
                if (warningCount > MaxWarningIssues)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "BEP Compliance",
                        Description = $"Model has {warningCount} warnings; BEP allows maximum {MaxWarningIssues}",
                        Recommendation = "Reduce warning count by addressing parameter completeness and naming issues"
                    });
                }
            }

            // Check LOD compliance against current phase
            if (report.Standards?.LODCompliance != null && LODRequirementsByPhase.Count > 0)
            {
                // Find the highest required LOD from all phases (most stringent)
                int highestRequired = LODRequirementsByPhase.Values.Max();
                if (report.Standards.LODCompliance.AchievedLOD < highestRequired)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = "BEP Compliance",
                        Description = $"Achieved LOD {report.Standards.LODCompliance.AchievedLOD} does not meet required LOD {highestRequired}",
                        Recommendation = "Increase model detail and parameter data to meet the required LOD level"
                    });
                }
            }

            // Check parameter completeness against required parameter groups
            if (report.DataQuality?.ParameterCompleteness != null && RequiredParameterGroups.Count > 0)
            {
                if (report.DataQuality.ParameterCompleteness.Percentage < 90.0)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "BEP Compliance",
                        Description = $"Parameter completeness is {report.DataQuality.ParameterCompleteness.Percentage:F1}%; " +
                                      $"BEP requires high completeness for groups: {string.Join(", ", RequiredParameterGroups)}",
                        Recommendation = "Populate all required parameter groups on relevant elements"
                    });
                }
            }

            // Check naming compliance against file naming convention
            if (report.DataQuality?.NamingCompliance != null)
            {
                if (report.DataQuality.NamingCompliance.Percentage < 80.0)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "BEP Compliance",
                        Description = $"Naming compliance is {report.DataQuality.NamingCompliance.Percentage:F1}%; BEP requires consistent naming",
                        Recommendation = !string.IsNullOrEmpty(FileNamingConventionPattern)
                            ? $"Apply naming convention pattern: {FileNamingConventionPattern}"
                            : "Apply consistent naming conventions to families and views"
                    });
                }
            }

            // Check delivery milestone compliance
            var now = DateTime.UtcNow;
            foreach (var milestone in DeliveryMilestones.Where(m => m.DueDate <= now && !m.IsCompleted))
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "BEP Compliance",
                    Description = $"Delivery milestone '{milestone.Name}' was due on {milestone.DueDate:yyyy-MM-dd} and is not completed",
                    Recommendation = $"Complete milestone deliverables and achieve minimum score of {milestone.MinimumScore:F0}"
                });
            }

            Logger.Info("BEP validation complete for '{0}': {1} issues found", report.ModelName, issues.Count);
            return issues;
        }

        /// <summary>
        /// Creates a standard BEP requirements set aligned with ISO 19650 information management.
        /// </summary>
        public static BEPRequirements ForISO19650()
        {
            Logger.Info("Creating ISO 19650-compliant BEP requirements");

            return new BEPRequirements
            {
                LODRequirementsByPhase = new Dictionary<string, int>
                {
                    { "Brief", 100 },
                    { "Concept", 200 },
                    { "Design Development", 300 },
                    { "Technical Design", 350 },
                    { "Construction", 400 },
                    { "As Built", 500 }
                },
                FileNamingConventionPattern = @"^[A-Z]{2,5}-[A-Z]{2,5}-[A-Z0-9]{2}-[A-Z0-9]{2,4}-[A-Z]{2}-[A-Z]-[0-9]{4,5}$",
                RequiredParameterGroups = new List<string>
                {
                    "Identity Data",
                    "Phasing",
                    "IFC Parameters",
                    "Classification",
                    "ISO 19650 Status",
                    "Revision Tracking"
                },
                ModelOrigin = new ModelOriginRequirement
                {
                    RequireSharedCoordinates = true,
                    RequireSurveyPoint = true,
                    MaxDistanceFromOriginMeters = 30000.0,
                    Description = "Model must use shared coordinates with survey point established per ISO 19650-2 Clause 5.3"
                },
                DeliveryMilestones = new List<DeliveryMilestone>
                {
                    new DeliveryMilestone
                    {
                        Name = "Information Model - Stage 2 (Concept)",
                        Phase = "Concept",
                        MinimumScore = 60.0,
                        RequiredLOD = 200,
                        Description = "Concept design information model delivery"
                    },
                    new DeliveryMilestone
                    {
                        Name = "Information Model - Stage 3 (Spatial Coordination)",
                        Phase = "Design Development",
                        MinimumScore = 75.0,
                        RequiredLOD = 300,
                        Description = "Spatially coordinated design information model"
                    },
                    new DeliveryMilestone
                    {
                        Name = "Information Model - Stage 4 (Technical Design)",
                        Phase = "Technical Design",
                        MinimumScore = 85.0,
                        RequiredLOD = 350,
                        Description = "Technical design information model for construction"
                    },
                    new DeliveryMilestone
                    {
                        Name = "Asset Information Model",
                        Phase = "As Built",
                        MinimumScore = 95.0,
                        RequiredLOD = 500,
                        Description = "As-built asset information model for handover"
                    }
                },
                MinimumHealthScore = 75.0,
                MaxCriticalIssues = 0,
                MaxWarningIssues = 25
            };
        }
    }

    /// <summary>
    /// Defines model origin coordinate requirements for BEP compliance.
    /// </summary>
    public class ModelOriginRequirement
    {
        /// <summary>Whether the model must use shared coordinate systems.</summary>
        public bool RequireSharedCoordinates { get; set; }

        /// <summary>Whether a survey point must be established.</summary>
        public bool RequireSurveyPoint { get; set; }

        /// <summary>Maximum allowed distance from the internal origin in meters.</summary>
        public double MaxDistanceFromOriginMeters { get; set; } = 30000.0;

        /// <summary>Human-readable description of the origin requirement.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Represents a project delivery milestone in the BEP.
    /// </summary>
    public class DeliveryMilestone
    {
        /// <summary>Milestone name (e.g., "Stage 3 - Spatial Coordination").</summary>
        public string Name { get; set; }

        /// <summary>Project phase this milestone belongs to.</summary>
        public string Phase { get; set; }

        /// <summary>Due date for this milestone delivery.</summary>
        public DateTime DueDate { get; set; }

        /// <summary>Minimum model health score required at this milestone.</summary>
        public double MinimumScore { get; set; }

        /// <summary>Required LOD level at this milestone.</summary>
        public int RequiredLOD { get; set; }

        /// <summary>Description of what must be delivered.</summary>
        public string Description { get; set; }

        /// <summary>Whether this milestone has been completed.</summary>
        public bool IsCompleted { get; set; }
    }

    public class HealthThreshold
    {
        public int MaxElements { get; set; }
        public int MaxViews { get; set; }
        public long MaxFileSize { get; set; }
        public int MaxInPlaceFamilies { get; set; }
        public int MaxImportedCad { get; set; }
    }

    public class ModelHealthReport
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public DateTime AssessedAt { get; set; }
        public double OverallScore { get; set; }
        public string HealthGrade { get; set; }
        public DataQualityMetrics DataQuality { get; set; }
        public ModelStructureMetrics ModelStructure { get; set; }
        public PerformanceMetrics Performance { get; set; }
        public StandardsMetrics Standards { get; set; }
        public GeometryMetrics Geometry { get; set; }
        public List<HealthIssue> Issues { get; set; }
        public List<HealthRecommendation> Recommendations { get; set; }
        public HealthComparison Comparison { get; set; }
    }

    public class QuickHealthStatus
    {
        public string ModelId { get; set; }
        public DateTime CheckedAt { get; set; }
        public HealthStatus Status { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public int UnplacedElements { get; set; }
        public int DuplicateInstances { get; set; }
        public int ElementCount { get; set; }
        public int ViewCount { get; set; }
        public int FamilyCount { get; set; }
    }

    public class DataQualityMetrics
    {
        public double Score { get; set; }
        public CompletenessMetric ParameterCompleteness { get; set; }
        public ComplianceMetric NamingCompliance { get; set; }
        public AssignmentMetric MaterialAssignment { get; set; }
        public AssignmentMetric PhaseAssignment { get; set; }
        public AssignmentMetric WorksetAssignment { get; set; }
        public ComplianceMetric ClassificationCompliance { get; set; }
    }

    public class ModelStructureMetrics
    {
        public double Score { get; set; }
        public LinkedModelHealth LinkedModels { get; set; }
        public ContainmentMetric RoomContainment { get; set; }
        public LevelUsageMetric LevelUsage { get; set; }
        public GroupHealthMetric GroupHealth { get; set; }
        public WorksetMetric WorksetOrganization { get; set; }
    }

    public class PerformanceMetrics
    {
        public double Score { get; set; }
        public int ElementCount { get; set; }
        public int ViewCount { get; set; }
        public int FamilyCount { get; set; }
        public long FileSize { get; set; }
        public StatusLevel ElementCountStatus { get; set; }
        public StatusLevel ViewCountStatus { get; set; }
        public StatusLevel FileSizeStatus { get; set; }
        public List<HealthIssue> Issues { get; set; }
    }

    public class StandardsMetrics
    {
        public double Score { get; set; }
        public ComplianceMetric ISO19650Compliance { get; set; }
        public ComplianceMetric BEPCompliance { get; set; }
        public LODMetric LODCompliance { get; set; }
    }

    public class GeometryMetrics
    {
        public double Score { get; set; }
        public DuplicateMetric Duplicates { get; set; }
        public OverlapMetric Overlaps { get; set; }
        public ConnectionMetric UnconnectedElements { get; set; }
        public SmallElementMetric SmallElements { get; set; }
    }

    public class CompletenessMetric
    {
        public int TotalChecked { get; set; }
        public int CompleteCount { get; set; }
        public int IncompleteCount { get; set; }
        public double Percentage { get; set; }
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();
    }

    public class ComplianceMetric
    {
        public int TotalChecked { get; set; }
        public int CompliantCount { get; set; }
        public int NonCompliantCount { get; set; }
        public double Percentage { get; set; }
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();
    }

    public class AssignmentMetric
    {
        public int TotalChecked { get; set; }
        public int AssignedCount { get; set; }
        public int UnassignedCount { get; set; }
        public double Percentage { get; set; }
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();
    }

    public class LinkedModelHealth
    {
        public int TotalLinks { get; set; }
        public int LoadedLinks { get; set; }
        public int UnloadedLinks { get; set; }
    }

    public class ContainmentMetric
    {
        public int TotalChecked { get; set; }
        public int ContainedCount { get; set; }
        public int UncontainedCount { get; set; }
        public double Percentage { get; set; }
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();
    }

    public class LevelUsageMetric
    {
        public int TotalLevels { get; set; }
        public int UsedLevels { get; set; }
        public int UnusedLevels { get; set; }
        public double UsagePercentage => TotalLevels > 0 ? (double)UsedLevels / TotalLevels * 100 : 100;
    }

    public class GroupHealthMetric
    {
        public int TotalGroups { get; set; }
        public int UnusedGroups { get; set; }
        public double HealthPercentage => TotalGroups > 0 ? (double)(TotalGroups - UnusedGroups) / TotalGroups * 100 : 100;
    }

    public class WorksetMetric { public double Score { get; set; } }
    public class LODMetric { public int RequiredLOD { get; set; } public int AchievedLOD { get; set; } public bool Compliant { get; set; } }
    public class DuplicateMetric { public int DuplicateCount { get; set; } public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>(); }
    public class OverlapMetric { public int OverlapCount { get; set; } }
    public class ConnectionMetric { public int UnconnectedCount { get; set; } }
    public class SmallElementMetric { public int SmallElementCount { get; set; } }

    public class HealthIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Category { get; set; }
        public string ElementId { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
    }

    public class HealthRecommendation
    {
        public string Priority { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public string Effort { get; set; }
    }

    public class HealthComparison
    {
        public double ScoreChange { get; set; }
        public int IssueCountChange { get; set; }
        public int NewIssues { get; set; }
        public int ResolvedIssues { get; set; }
        public string Trend { get; set; }
    }

    public enum HealthStatus { Healthy, NeedsAttention, Warning, Critical }
    public enum IssueSeverity { Info, Warning, Error, Critical }
    public enum StatusLevel { Good, Warning, Critical }

    /// <summary>
    /// Checks BIM model data quality across parameters, naming, and material assignments.
    /// Provides detailed metrics and per-element issue tracking.
    /// </summary>
    public class QualityChecker
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Checks how completely required parameters are filled in across all elements.
        /// Returns a CompletenessMetric with per-element issue details.
        /// </summary>
        /// <param name="elements">Elements to check.</param>
        /// <param name="required">Required parameters with category applicability.</param>
        /// <returns>Metric showing total checked, complete, incomplete, and percentage.</returns>
        public CompletenessMetric CheckParameterCompleteness(
            List<ModelElement> elements,
            List<RequiredParameter> required)
        {
            Logger.Debug("Checking parameter completeness for {0} elements against {1} required parameters",
                elements?.Count ?? 0, required?.Count ?? 0);

            var metric = new CompletenessMetric();

            if (elements == null || elements.Count == 0 || required == null || required.Count == 0)
            {
                metric.Percentage = 100.0;
                return metric;
            }

            foreach (var element in elements)
            {
                var applicableParams = required
                    .Where(p => p.AppliesTo(element.Category))
                    .ToList();

                if (applicableParams.Count == 0)
                    continue;

                metric.TotalChecked++;

                var missingParams = applicableParams
                    .Where(p => !element.HasParameterValue(p.Name))
                    .ToList();

                if (missingParams.Count > 0)
                {
                    metric.IncompleteCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = missingParams.Count > applicableParams.Count / 2
                            ? IssueSeverity.Error
                            : IssueSeverity.Warning,
                        Category = "Parameter Completeness",
                        ElementId = element.ElementId,
                        Description = $"{element.Category} element missing {missingParams.Count}/{applicableParams.Count} " +
                                      $"required parameters: {string.Join(", ", missingParams.Select(p => p.Name))}",
                        Recommendation = "Populate all required parameter values for this element"
                    });
                }
                else
                {
                    metric.CompleteCount++;
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.CompleteCount / metric.TotalChecked * 100.0
                : 100.0;

            Logger.Info("Parameter completeness: {0:F1}% ({1}/{2} elements complete)",
                metric.Percentage, metric.CompleteCount, metric.TotalChecked);

            return metric;
        }

        /// <summary>
        /// Checks whether element and family names comply with naming convention patterns.
        /// Evaluates each element against the pattern applicable to its type.
        /// </summary>
        /// <param name="elements">Elements to check (uses TypeName for matching).</param>
        /// <param name="patterns">Naming patterns with category applicability.</param>
        /// <returns>Metric showing total checked, compliant, non-compliant, and percentage.</returns>
        public ComplianceMetric CheckNamingCompliance(
            List<ModelElement> elements,
            List<NamingPattern> patterns)
        {
            Logger.Debug("Checking naming compliance for {0} elements against {1} patterns",
                elements?.Count ?? 0, patterns?.Count ?? 0);

            var metric = new ComplianceMetric();

            if (elements == null || elements.Count == 0 || patterns == null || patterns.Count == 0)
            {
                metric.Percentage = 100.0;
                return metric;
            }

            foreach (var element in elements)
            {
                var applicablePattern = patterns.FirstOrDefault(p =>
                    p.AppliesTo == element.Category || p.AppliesTo == "All");

                if (applicablePattern == null)
                    continue;

                metric.TotalChecked++;

                string nameToCheck = element.TypeName ?? "";
                if (applicablePattern.Matches(nameToCheck))
                {
                    metric.CompliantCount++;
                }
                else
                {
                    metric.NonCompliantCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Naming Convention",
                        ElementId = element.ElementId,
                        Description = $"{element.Category} type name '{nameToCheck}' does not match " +
                                      $"required pattern for {applicablePattern.AppliesTo}",
                        Recommendation = $"Rename to match pattern: {applicablePattern.Description}"
                    });
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.CompliantCount / metric.TotalChecked * 100.0
                : 100.0;

            Logger.Info("Naming compliance: {0:F1}% ({1}/{2} elements compliant)",
                metric.Percentage, metric.CompliantCount, metric.TotalChecked);

            return metric;
        }

        /// <summary>
        /// Checks whether elements that require materials have them assigned.
        /// Categories requiring materials: Walls, Floors, Ceilings, Roofs, Doors, Windows,
        /// Structural Columns, Structural Beams.
        /// </summary>
        /// <param name="elements">Elements to check.</param>
        /// <returns>Metric showing total checked, assigned, unassigned, and percentage.</returns>
        public AssignmentMetric CheckMaterialAssignment(List<ModelElement> elements)
        {
            Logger.Debug("Checking material assignment for {0} elements", elements?.Count ?? 0);

            var metric = new AssignmentMetric();

            if (elements == null || elements.Count == 0)
            {
                metric.Percentage = 100.0;
                return metric;
            }

            var categoriesRequiringMaterials = new HashSet<string>
            {
                "Walls", "Floors", "Ceilings", "Roofs", "Doors", "Windows",
                "Structural Columns", "Structural Beams"
            };

            foreach (var element in elements.Where(e => categoriesRequiringMaterials.Contains(e.Category)))
            {
                metric.TotalChecked++;

                if (string.IsNullOrWhiteSpace(element.Material))
                {
                    metric.UnassignedCount++;
                    metric.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Material Assignment",
                        ElementId = element.ElementId,
                        Description = $"{element.Category} element '{element.TypeName ?? element.ElementId}' has no material assigned",
                        Recommendation = "Assign an appropriate material from the project material library"
                    });
                }
                else
                {
                    metric.AssignedCount++;
                }
            }

            metric.Percentage = metric.TotalChecked > 0
                ? (double)metric.AssignedCount / metric.TotalChecked * 100.0
                : 100.0;

            Logger.Info("Material assignment: {0:F1}% ({1}/{2} elements assigned)",
                metric.Percentage, metric.AssignedCount, metric.TotalChecked);

            return metric;
        }
    }
    /// <summary>
    /// Analyzes BIM model performance characteristics by evaluating element count,
    /// view count, family count, and file size against configurable thresholds.
    /// Produces a scored PerformanceMetrics result with detailed issue reporting.
    /// </summary>
    public class PerformanceAnalyzer
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Analyzes model performance by comparing counts and sizes against thresholds.
        /// Returns a fully populated PerformanceMetrics with score and issues.
        /// </summary>
        /// <param name="elementCount">Total number of model elements.</param>
        /// <param name="viewCount">Total number of views in the model.</param>
        /// <param name="familyCount">Total number of loaded families.</param>
        /// <param name="fileSize">Model file size in bytes.</param>
        /// <param name="thresholds">Threshold configuration for performance limits.</param>
        /// <returns>PerformanceMetrics with evaluated status levels and score.</returns>
        public PerformanceMetrics AnalyzePerformance(
            int elementCount,
            int viewCount,
            int familyCount,
            long fileSize,
            HealthThreshold thresholds)
        {
            Logger.Info("Analyzing performance: {0} elements, {1} views, {2} families, {3:F1} MB",
                elementCount, viewCount, familyCount, fileSize / (1024.0 * 1024.0));

            if (thresholds == null)
            {
                Logger.Warn("No thresholds provided, using defaults");
                thresholds = GetDefaultThresholds();
            }

            var metrics = new PerformanceMetrics
            {
                ElementCount = elementCount,
                ViewCount = viewCount,
                FamilyCount = familyCount,
                FileSize = fileSize,
                Issues = new List<HealthIssue>()
            };

            // Evaluate each metric against thresholds
            metrics.ElementCountStatus = EvaluateElementCount(elementCount, thresholds, metrics.Issues);
            metrics.ViewCountStatus = EvaluateViewCount(viewCount, thresholds, metrics.Issues);
            metrics.FileSizeStatus = EvaluateFileSize(fileSize, thresholds, metrics.Issues);

            // Evaluate family count (uses element threshold as proxy if no specific one)
            EvaluateFamilyCount(familyCount, elementCount, metrics.Issues);

            // Calculate overall performance score
            metrics.Score = CalculateScore(metrics);

            Logger.Info("Performance analysis complete: score = {0:F1}, issues = {1}",
                metrics.Score, metrics.Issues.Count);

            return metrics;
        }

        /// <summary>
        /// Evaluates element count against the threshold and returns the appropriate status level.
        /// </summary>
        private StatusLevel EvaluateElementCount(int elementCount, HealthThreshold thresholds, List<HealthIssue> issues)
        {
            double ratio = (double)elementCount / thresholds.MaxElements;

            if (ratio > 1.0)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "Performance",
                    Description = $"Element count ({elementCount:N0}) exceeds maximum threshold ({thresholds.MaxElements:N0})",
                    Recommendation = "Split the model into discipline-specific linked models or remove unnecessary elements"
                });
                return StatusLevel.Critical;
            }

            if (ratio > 0.75)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Performance",
                    Description = $"Element count ({elementCount:N0}) is approaching the maximum threshold ({thresholds.MaxElements:N0})",
                    Recommendation = "Consider splitting the model or purging unused elements to prevent performance degradation"
                });
                return StatusLevel.Warning;
            }

            return StatusLevel.Good;
        }

        /// <summary>
        /// Evaluates view count against the threshold and returns the appropriate status level.
        /// </summary>
        private StatusLevel EvaluateViewCount(int viewCount, HealthThreshold thresholds, List<HealthIssue> issues)
        {
            double ratio = (double)viewCount / thresholds.MaxViews;

            if (ratio > 1.0)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "Performance",
                    Description = $"View count ({viewCount:N0}) exceeds maximum threshold ({thresholds.MaxViews:N0})",
                    Recommendation = "Delete unused views, close working views, and consolidate where possible"
                });
                return StatusLevel.Critical;
            }

            if (ratio > 0.75)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Performance",
                    Description = $"View count ({viewCount:N0}) is approaching the maximum threshold ({thresholds.MaxViews:N0})",
                    Recommendation = "Review and delete any unused or temporary views"
                });
                return StatusLevel.Warning;
            }

            return StatusLevel.Good;
        }

        /// <summary>
        /// Evaluates file size against the threshold and returns the appropriate status level.
        /// </summary>
        private StatusLevel EvaluateFileSize(long fileSize, HealthThreshold thresholds, List<HealthIssue> issues)
        {
            double ratio = (double)fileSize / thresholds.MaxFileSize;

            if (ratio > 1.0)
            {
                double fileSizeMB = fileSize / (1024.0 * 1024.0);
                double maxSizeMB = thresholds.MaxFileSize / (1024.0 * 1024.0);
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "Performance",
                    Description = $"File size ({fileSizeMB:F0} MB) exceeds maximum threshold ({maxSizeMB:F0} MB)",
                    Recommendation = "Purge unused families, audit the model, remove imported CAD, and consider splitting"
                });
                return StatusLevel.Critical;
            }

            if (ratio > 0.75)
            {
                double fileSizeMB = fileSize / (1024.0 * 1024.0);
                double maxSizeMB = thresholds.MaxFileSize / (1024.0 * 1024.0);
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Performance",
                    Description = $"File size ({fileSizeMB:F0} MB) is approaching the maximum threshold ({maxSizeMB:F0} MB)",
                    Recommendation = "Run audit and purge unused content to reduce file size"
                });
                return StatusLevel.Warning;
            }

            return StatusLevel.Good;
        }

        /// <summary>
        /// Evaluates family count relative to element count.
        /// A high family-to-element ratio can indicate excessive family loading.
        /// </summary>
        private void EvaluateFamilyCount(int familyCount, int elementCount, List<HealthIssue> issues)
        {
            // If there are more families than 1/3 of elements, there may be excessive unused families
            if (elementCount > 0 && familyCount > elementCount / 3 && familyCount > 200)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = "Performance",
                    Description = $"High family-to-element ratio: {familyCount} families for {elementCount} elements",
                    Recommendation = "Purge unused families to improve load times and reduce memory consumption"
                });
            }
        }

        /// <summary>
        /// Calculates the overall performance score (0-100) from the evaluated metrics.
        /// </summary>
        private double CalculateScore(PerformanceMetrics metrics)
        {
            double score = 100.0;

            // Deduct for element count issues
            if (metrics.ElementCountStatus == StatusLevel.Critical) score -= 25.0;
            else if (metrics.ElementCountStatus == StatusLevel.Warning) score -= 15.0;

            // Deduct for view count issues
            if (metrics.ViewCountStatus == StatusLevel.Critical) score -= 20.0;
            else if (metrics.ViewCountStatus == StatusLevel.Warning) score -= 10.0;

            // Deduct for file size issues
            if (metrics.FileSizeStatus == StatusLevel.Critical) score -= 25.0;
            else if (metrics.FileSizeStatus == StatusLevel.Warning) score -= 10.0;

            // Deduct for additional info-level issues
            int infoIssues = metrics.Issues.Count(i => i.Severity == IssueSeverity.Info);
            score -= infoIssues * 2.0;

            return Math.Max(0.0, Math.Min(100.0, score));
        }

        /// <summary>
        /// Returns default health thresholds when none are provided.
        /// </summary>
        private HealthThreshold GetDefaultThresholds()
        {
            return new HealthThreshold
            {
                MaxElements = 500000,
                MaxViews = 1000,
                MaxFileSize = 500L * 1024 * 1024, // 500 MB
                MaxInPlaceFamilies = 20,
                MaxImportedCad = 10
            };
        }
    }
    /// <summary>
    /// Tracks compliance of a BIM model against ISO 19650, BIM Execution Plan (BEP),
    /// and Level of Development (LOD) requirements. Produces scored compliance metrics
    /// with detailed issue tracking for remediation.
    /// </summary>
    public class ComplianceTracker
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// ISO 19650 parameter names that should be present on model elements for compliance.
        /// </summary>
        private static readonly string[] ISO19650RequiredFields = new[]
        {
            "Classification",
            "Status",
            "Revision",
            "Description",
            "Author"
        };

        /// <summary>
        /// Checks a model health report for ISO 19650 information management compliance.
        /// Evaluates naming conventions, parameter completeness, and standards metadata.
        /// </summary>
        /// <param name="report">The model health report to evaluate.</param>
        /// <returns>ComplianceMetric with overall compliance percentage and issues.</returns>
        public ComplianceMetric CheckISO19650Compliance(ModelHealthReport report)
        {
            Logger.Info("Checking ISO 19650 compliance for model '{0}'", report.ModelName);

            var metric = new ComplianceMetric();
            int checksPerformed = 0;
            int checksPassed = 0;

            // Check 1: Naming convention compliance (ISO 19650-2 Clause 11)
            checksPerformed++;
            if (report.DataQuality?.NamingCompliance != null &&
                report.DataQuality.NamingCompliance.Percentage >= 80.0)
            {
                checksPassed++;
            }
            else
            {
                metric.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "ISO 19650 Compliance",
                    Description = "Naming convention compliance below 80% required by ISO 19650-2 Clause 11",
                    Recommendation = "Apply the project naming convention (Originator-Volume-Level-Type-Role-Classification-Number) to all elements and views"
                });
                metric.NonCompliantCount++;
            }

            // Check 2: Parameter completeness (information requirements)
            checksPerformed++;
            if (report.DataQuality?.ParameterCompleteness != null &&
                report.DataQuality.ParameterCompleteness.Percentage >= 75.0)
            {
                checksPassed++;
            }
            else
            {
                metric.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "ISO 19650 Compliance",
                    Description = "Parameter completeness below 75% required for information exchange",
                    Recommendation = "Populate all required parameters as defined in the Exchange Information Requirements (EIR)"
                });
                metric.NonCompliantCount++;
            }

            // Check 3: Material assignments (asset data)
            checksPerformed++;
            if (report.DataQuality?.MaterialAssignment != null &&
                report.DataQuality.MaterialAssignment.Percentage >= 90.0)
            {
                checksPassed++;
            }
            else
            {
                metric.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "ISO 19650 Compliance",
                    Description = "Material assignment below 90% - asset information may be incomplete",
                    Recommendation = "Assign materials to all construction elements for accurate asset data"
                });
                metric.NonCompliantCount++;
            }

            // Check 4: Model structure - room containment (spatial coordination)
            checksPerformed++;
            if (report.ModelStructure?.RoomContainment != null &&
                report.ModelStructure.RoomContainment.Percentage >= 85.0)
            {
                checksPassed++;
            }
            else
            {
                metric.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "ISO 19650 Compliance",
                    Description = "Room containment below 85% - spatial data may be incomplete for facility management",
                    Recommendation = "Ensure all relevant elements are contained within rooms for spatial data exchange"
                });
                metric.NonCompliantCount++;
            }

            // Check 5: Geometry quality - no duplicates (data integrity)
            checksPerformed++;
            if (report.Geometry?.Duplicates != null &&
                report.Geometry.Duplicates.DuplicateCount == 0)
            {
                checksPassed++;
            }
            else
            {
                int dupCount = report.Geometry?.Duplicates?.DuplicateCount ?? 0;
                metric.Issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "ISO 19650 Compliance",
                    Description = $"{dupCount} duplicate elements compromise data integrity required by ISO 19650",
                    Recommendation = "Remove all duplicate elements to ensure accurate quantity take-offs and information exchange"
                });
                metric.NonCompliantCount++;
            }

            metric.TotalChecked = checksPerformed;
            metric.CompliantCount = checksPassed;
            metric.Percentage = checksPerformed > 0
                ? (double)checksPassed / checksPerformed * 100.0
                : 100.0;

            Logger.Info("ISO 19650 compliance: {0:F1}% ({1}/{2} checks passed)",
                metric.Percentage, checksPassed, checksPerformed);

            return metric;
        }

        /// <summary>
        /// Checks a model health report against specific BEP requirements.
        /// Delegates detailed validation to the BEPRequirements.Validate method
        /// and converts results into a ComplianceMetric.
        /// </summary>
        /// <param name="report">The model health report to evaluate.</param>
        /// <param name="requirements">The BEP requirements to check against.</param>
        /// <returns>ComplianceMetric with BEP compliance percentage and issues.</returns>
        public ComplianceMetric CheckBEPCompliance(ModelHealthReport report, BEPRequirements requirements)
        {
            Logger.Info("Checking BEP compliance for model '{0}'", report.ModelName);

            var metric = new ComplianceMetric();

            if (requirements == null)
            {
                Logger.Warn("No BEP requirements provided; returning full compliance");
                metric.Percentage = 100.0;
                return metric;
            }

            var validationIssues = requirements.Validate(report);

            // Count total BEP checks (base checks that BEPRequirements validates)
            int totalChecks = 5; // score, critical, warnings, LOD, parameters
            totalChecks += requirements.DeliveryMilestones.Count(m => m.DueDate <= DateTime.UtcNow);

            int failedChecks = validationIssues.Count;
            int passedChecks = Math.Max(0, totalChecks - failedChecks);

            metric.TotalChecked = totalChecks;
            metric.CompliantCount = passedChecks;
            metric.NonCompliantCount = failedChecks;
            metric.Issues.AddRange(validationIssues);

            metric.Percentage = totalChecks > 0
                ? (double)passedChecks / totalChecks * 100.0
                : 100.0;

            Logger.Info("BEP compliance: {0:F1}% ({1}/{2} checks passed)",
                metric.Percentage, passedChecks, totalChecks);

            return metric;
        }

        /// <summary>
        /// Checks whether the achieved Level of Development meets the required level.
        /// LOD levels: 100 (Concept), 200 (Approximate), 300 (Precise),
        /// 350 (Construction), 400 (Fabrication), 500 (As-Built).
        /// </summary>
        /// <param name="requiredLOD">The minimum required LOD level.</param>
        /// <param name="achievedLOD">The LOD level achieved in the model.</param>
        /// <returns>LODMetric indicating compliance status.</returns>
        public LODMetric CheckLODCompliance(int requiredLOD, int achievedLOD)
        {
            Logger.Debug("Checking LOD compliance: required={0}, achieved={1}", requiredLOD, achievedLOD);

            var metric = new LODMetric
            {
                RequiredLOD = requiredLOD,
                AchievedLOD = achievedLOD,
                Compliant = achievedLOD >= requiredLOD
            };

            if (!metric.Compliant)
            {
                Logger.Warn("LOD non-compliant: achieved LOD {0} is below required LOD {1}",
                    achievedLOD, requiredLOD);
            }
            else
            {
                Logger.Info("LOD compliant: achieved LOD {0} meets or exceeds required LOD {1}",
                    achievedLOD, requiredLOD);
            }

            return metric;
        }
    }
    /// <summary>
    /// Analyzes trends between successive model health reports.
    /// Computes score deltas, issue changes, category-level trends,
    /// and produces an overall health trajectory assessment.
    /// </summary>
    public class TrendAnalyzer
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Compares a current health report with a previous one to identify trends.
        /// Returns a HealthComparison with score change, issue deltas, and trend direction.
        /// </summary>
        /// <param name="current">The most recent health report.</param>
        /// <param name="previous">The earlier health report to compare against.</param>
        /// <returns>HealthComparison with trend analysis.</returns>
        public HealthComparison CompareReports(ModelHealthReport current, ModelHealthReport previous)
        {
            Logger.Info("Comparing health reports for model '{0}': current ({1:yyyy-MM-dd}) vs previous ({2:yyyy-MM-dd})",
                current.ModelName, current.AssessedAt, previous.AssessedAt);

            var comparison = new HealthComparison
            {
                ScoreChange = ComputeScoreChange(current, previous),
                IssueCountChange = ComputeIssueCountChange(current, previous),
                NewIssues = CountNewIssues(current, previous),
                ResolvedIssues = CountResolvedIssues(current, previous),
                Trend = DetermineTrend(current, previous)
            };

            Logger.Info("Trend analysis: score change = {0:+0.0;-0.0;0.0}, trend = {1}, " +
                         "new issues = {2}, resolved issues = {3}",
                comparison.ScoreChange, comparison.Trend,
                comparison.NewIssues, comparison.ResolvedIssues);

            return comparison;
        }

        /// <summary>
        /// Computes the difference in overall health scores between current and previous reports.
        /// </summary>
        private double ComputeScoreChange(ModelHealthReport current, ModelHealthReport previous)
        {
            return current.OverallScore - previous.OverallScore;
        }

        /// <summary>
        /// Computes the change in total issue count between current and previous reports.
        /// A positive value means more issues; negative means fewer issues.
        /// </summary>
        private int ComputeIssueCountChange(ModelHealthReport current, ModelHealthReport previous)
        {
            int currentCount = current.Issues?.Count ?? 0;
            int previousCount = previous.Issues?.Count ?? 0;
            return currentCount - previousCount;
        }

        /// <summary>
        /// Counts issues present in the current report that were not in the previous report.
        /// Matching is based on element ID and issue category.
        /// </summary>
        private int CountNewIssues(ModelHealthReport current, ModelHealthReport previous)
        {
            if (current.Issues == null || current.Issues.Count == 0)
                return 0;

            if (previous.Issues == null || previous.Issues.Count == 0)
                return current.Issues.Count;

            var previousIssueKeys = new HashSet<string>(
                previous.Issues.Select(i => BuildIssueKey(i)));

            return current.Issues.Count(i => !previousIssueKeys.Contains(BuildIssueKey(i)));
        }

        /// <summary>
        /// Counts issues from the previous report that no longer appear in the current report.
        /// These represent issues that have been fixed between assessments.
        /// </summary>
        private int CountResolvedIssues(ModelHealthReport current, ModelHealthReport previous)
        {
            if (previous.Issues == null || previous.Issues.Count == 0)
                return 0;

            if (current.Issues == null || current.Issues.Count == 0)
                return previous.Issues.Count;

            var currentIssueKeys = new HashSet<string>(
                current.Issues.Select(i => BuildIssueKey(i)));

            return previous.Issues.Count(p => !currentIssueKeys.Contains(BuildIssueKey(p)));
        }

        /// <summary>
        /// Builds a composite key for an issue based on its element ID and category.
        /// Used to match issues across reports for new/resolved detection.
        /// </summary>
        private string BuildIssueKey(HealthIssue issue)
        {
            return $"{issue.ElementId ?? "global"}|{issue.Category ?? "unknown"}";
        }

        /// <summary>
        /// Determines the overall health trend based on score change magnitude
        /// and issue resolution progress.
        /// </summary>
        private string DetermineTrend(ModelHealthReport current, ModelHealthReport previous)
        {
            double scoreChange = current.OverallScore - previous.OverallScore;
            int issueChange = (current.Issues?.Count ?? 0) - (previous.Issues?.Count ?? 0);

            // Strong improvement: score increased significantly and issues decreased
            if (scoreChange >= 5.0 && issueChange <= 0)
                return "Improving";

            // Moderate improvement: score increased
            if (scoreChange > 1.0)
                return "Improving";

            // Strong decline: score dropped significantly and issues increased
            if (scoreChange <= -5.0 && issueChange > 0)
                return "Declining";

            // Moderate decline: score decreased
            if (scoreChange < -1.0)
                return "Declining";

            // Within tolerance: stable
            return "Stable";
        }
    }

    #endregion
}
