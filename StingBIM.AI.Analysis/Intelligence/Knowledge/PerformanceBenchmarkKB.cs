// StingBIM.AI.Intelligence.Knowledge.PerformanceBenchmarkKB
// Building performance baselines and benchmarking knowledge base.
// Stores benchmarks for energy, water, IAQ, thermal comfort, lighting,
// acoustics, cost, schedule, and carbon across building types and climate zones.
// Master Proposal Reference: Part 2.3 - Phase 3 Active Intelligence

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Intelligence.Knowledge
{
    #region Performance Benchmark Knowledge Base

    /// <summary>
    /// Knowledge base of building performance baselines and benchmarks.
    /// Covers energy, water, indoor air quality, thermal comfort, lighting,
    /// acoustics, cost, schedule, and carbon metrics. Supports comparison,
    /// rating, trend analysis, peer comparison, and target setting.
    /// </summary>
    public class PerformanceBenchmarkKB
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Benchmark storage by metric type
        private readonly ConcurrentDictionary<string, BenchmarkEntry> _benchmarks;

        // Indexes for fast lookup
        private readonly ConcurrentDictionary<string, HashSet<string>> _metricTypeIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _buildingTypeIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _climateZoneIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _regionIndex;

        // Project performance tracking
        private readonly ConcurrentDictionary<string, ProjectPerformanceRecord> _projectRecords;

        // Configuration
        private readonly BenchmarkConfiguration _configuration;

        // Known metric types
        private static readonly Dictionary<string, MetricTypeInfo> MetricTypes =
            new Dictionary<string, MetricTypeInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["EUI"] = new MetricTypeInfo
                {
                    MetricType = "EUI", Category = BenchmarkCategory.Energy,
                    DisplayName = "Energy Use Intensity", Unit = "kBtu/ft\u00B2/yr",
                    LowerIsBetter = true, Description = "Total site energy per unit floor area"
                },
                ["EUI_Metric"] = new MetricTypeInfo
                {
                    MetricType = "EUI_Metric", Category = BenchmarkCategory.Energy,
                    DisplayName = "Energy Use Intensity (Metric)", Unit = "kWh/m\u00B2/yr",
                    LowerIsBetter = true, Description = "Total site energy per unit floor area (metric)"
                },
                ["WaterUse"] = new MetricTypeInfo
                {
                    MetricType = "WaterUse", Category = BenchmarkCategory.Water,
                    DisplayName = "Water Use", Unit = "gallons/person/day",
                    LowerIsBetter = true, Description = "Potable water consumption per occupant"
                },
                ["WaterUse_Metric"] = new MetricTypeInfo
                {
                    MetricType = "WaterUse_Metric", Category = BenchmarkCategory.Water,
                    DisplayName = "Water Use (Metric)", Unit = "liters/person/day",
                    LowerIsBetter = true, Description = "Potable water consumption per occupant (metric)"
                },
                ["VentilationRate"] = new MetricTypeInfo
                {
                    MetricType = "VentilationRate", Category = BenchmarkCategory.IndoorAirQuality,
                    DisplayName = "Ventilation Rate", Unit = "cfm/person",
                    LowerIsBetter = false, Description = "Outdoor air ventilation per person"
                },
                ["CO2Level"] = new MetricTypeInfo
                {
                    MetricType = "CO2Level", Category = BenchmarkCategory.IndoorAirQuality,
                    DisplayName = "CO\u2082 Concentration", Unit = "ppm",
                    LowerIsBetter = true, Description = "Indoor CO\u2082 concentration"
                },
                ["VOCLevel"] = new MetricTypeInfo
                {
                    MetricType = "VOCLevel", Category = BenchmarkCategory.IndoorAirQuality,
                    DisplayName = "VOC Level", Unit = "\u00B5g/m\u00B3",
                    LowerIsBetter = true, Description = "Total volatile organic compounds"
                },
                ["PMV"] = new MetricTypeInfo
                {
                    MetricType = "PMV", Category = BenchmarkCategory.ThermalComfort,
                    DisplayName = "Predicted Mean Vote", Unit = "PMV scale",
                    LowerIsBetter = false, Description = "ASHRAE 55 thermal comfort index (-3 to +3)"
                },
                ["PPD"] = new MetricTypeInfo
                {
                    MetricType = "PPD", Category = BenchmarkCategory.ThermalComfort,
                    DisplayName = "Predicted Percentage Dissatisfied", Unit = "%",
                    LowerIsBetter = true, Description = "Percentage of occupants thermally uncomfortable"
                },
                ["LPD"] = new MetricTypeInfo
                {
                    MetricType = "LPD", Category = BenchmarkCategory.Lighting,
                    DisplayName = "Lighting Power Density", Unit = "W/ft\u00B2",
                    LowerIsBetter = true, Description = "Installed lighting power per unit floor area"
                },
                ["DaylightAutonomy"] = new MetricTypeInfo
                {
                    MetricType = "DaylightAutonomy", Category = BenchmarkCategory.Lighting,
                    DisplayName = "Daylight Autonomy", Unit = "%",
                    LowerIsBetter = false, Description = "Percentage of occupied hours with adequate daylight"
                },
                ["NCRating"] = new MetricTypeInfo
                {
                    MetricType = "NCRating", Category = BenchmarkCategory.Acoustic,
                    DisplayName = "Noise Criteria Rating", Unit = "NC",
                    LowerIsBetter = true, Description = "Background noise level rating"
                },
                ["STC"] = new MetricTypeInfo
                {
                    MetricType = "STC", Category = BenchmarkCategory.Acoustic,
                    DisplayName = "Sound Transmission Class", Unit = "STC",
                    LowerIsBetter = false, Description = "Airborne sound insulation rating"
                },
                ["CostPerSqFt"] = new MetricTypeInfo
                {
                    MetricType = "CostPerSqFt", Category = BenchmarkCategory.Cost,
                    DisplayName = "Cost per Square Foot", Unit = "$/ft\u00B2",
                    LowerIsBetter = true, Description = "Total construction cost per unit floor area"
                },
                ["CostPerSqM"] = new MetricTypeInfo
                {
                    MetricType = "CostPerSqM", Category = BenchmarkCategory.Cost,
                    DisplayName = "Cost per Square Meter", Unit = "$/m\u00B2",
                    LowerIsBetter = true, Description = "Total construction cost per unit floor area (metric)"
                },
                ["CostPerSqM_UGX"] = new MetricTypeInfo
                {
                    MetricType = "CostPerSqM_UGX", Category = BenchmarkCategory.Cost,
                    DisplayName = "Cost per Square Meter (UGX)", Unit = "UGX/m\u00B2",
                    LowerIsBetter = true, Description = "Construction cost per unit floor area (Uganda Shillings)"
                },
                ["MonthsPerFloor"] = new MetricTypeInfo
                {
                    MetricType = "MonthsPerFloor", Category = BenchmarkCategory.Schedule,
                    DisplayName = "Months per Floor", Unit = "months",
                    LowerIsBetter = true, Description = "Construction duration per floor"
                },
                ["EmbodiedCarbon"] = new MetricTypeInfo
                {
                    MetricType = "EmbodiedCarbon", Category = BenchmarkCategory.Carbon,
                    DisplayName = "Embodied Carbon", Unit = "kgCO\u2082e/m\u00B2",
                    LowerIsBetter = true, Description = "Upfront embodied carbon per unit floor area"
                },
                ["OperationalCarbon"] = new MetricTypeInfo
                {
                    MetricType = "OperationalCarbon", Category = BenchmarkCategory.Carbon,
                    DisplayName = "Operational Carbon", Unit = "kgCO\u2082e/m\u00B2/yr",
                    LowerIsBetter = true, Description = "Annual operational carbon per unit floor area"
                }
            };

        public int BenchmarkCount => _benchmarks.Count;

        public PerformanceBenchmarkKB()
            : this(new BenchmarkConfiguration())
        {
        }

        public PerformanceBenchmarkKB(BenchmarkConfiguration configuration)
        {
            _configuration = configuration ?? new BenchmarkConfiguration();
            _benchmarks = new ConcurrentDictionary<string, BenchmarkEntry>(StringComparer.OrdinalIgnoreCase);
            _metricTypeIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _buildingTypeIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _climateZoneIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _regionIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _projectRecords = new ConcurrentDictionary<string, ProjectPerformanceRecord>(StringComparer.OrdinalIgnoreCase);

            InitializeDefaultBenchmarks();

            Logger.Info("PerformanceBenchmarkKB initialized with {0} default benchmarks", _benchmarks.Count);
        }

        #region Benchmark Retrieval

        /// <summary>
        /// Gets the benchmark value for a specific metric, building type, and climate zone.
        /// Returns the most specific match available, falling back to broader categories.
        /// </summary>
        public BenchmarkResult GetBenchmark(
            string metricType,
            string buildingType,
            string climateZone = null)
        {
            Logger.Debug("Getting benchmark for metric={0}, building={1}, climate={2}",
                metricType, buildingType, climateZone);

            // Try exact match first
            var exactKey = BuildKey(metricType, buildingType, climateZone);
            if (_benchmarks.TryGetValue(exactKey, out var exactMatch))
            {
                return BuildResult(exactMatch, "Exact match");
            }

            // Try without climate zone
            var noClimateKey = BuildKey(metricType, buildingType, null);
            if (_benchmarks.TryGetValue(noClimateKey, out var noClimateMatch))
            {
                return BuildResult(noClimateMatch, "Building type match (no climate-specific data)");
            }

            // Try generic building type
            var genericKey = BuildKey(metricType, "Generic", climateZone);
            if (_benchmarks.TryGetValue(genericKey, out var genericMatch))
            {
                return BuildResult(genericMatch, "Generic building type for this climate zone");
            }

            // Try fully generic
            var fullyGenericKey = BuildKey(metricType, "Generic", null);
            if (_benchmarks.TryGetValue(fullyGenericKey, out var fullyGenericMatch))
            {
                return BuildResult(fullyGenericMatch, "Generic baseline");
            }

            Logger.Debug("No benchmark found for {0}/{1}/{2}", metricType, buildingType, climateZone);
            return new BenchmarkResult
            {
                Found = false,
                MetricType = metricType,
                BuildingType = buildingType,
                ClimateZone = climateZone,
                MatchQuality = "No matching benchmark found"
            };
        }

        /// <summary>
        /// Gets all benchmarks for a specific metric type.
        /// </summary>
        public List<BenchmarkEntry> GetBenchmarksByMetric(string metricType)
        {
            if (_metricTypeIndex.TryGetValue(metricType, out var keys))
            {
                return keys
                    .Select(k => _benchmarks.TryGetValue(k, out var b) ? b : null)
                    .Where(b => b != null)
                    .ToList();
            }
            return new List<BenchmarkEntry>();
        }

        /// <summary>
        /// Gets all benchmarks for a specific building type.
        /// </summary>
        public List<BenchmarkEntry> GetBenchmarksByBuildingType(string buildingType)
        {
            if (_buildingTypeIndex.TryGetValue(buildingType, out var keys))
            {
                return keys
                    .Select(k => _benchmarks.TryGetValue(k, out var b) ? b : null)
                    .Where(b => b != null)
                    .ToList();
            }
            return new List<BenchmarkEntry>();
        }

        #endregion

        #region Comparison Engine

        /// <summary>
        /// Compares project performance metrics against benchmarks.
        /// Returns a detailed comparison report with ratings per metric.
        /// </summary>
        public async Task<ComparisonReport> CompareToBaselineAsync(
            ProjectMetrics projectMetrics,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (projectMetrics == null)
                throw new ArgumentNullException(nameof(projectMetrics));

            Logger.Info("Comparing project '{0}' to benchmarks", projectMetrics.ProjectName);
            progress?.Report("Comparing project metrics to performance baselines...");

            var report = new ComparisonReport
            {
                ProjectName = projectMetrics.ProjectName,
                BuildingType = projectMetrics.BuildingType,
                ClimateZone = projectMetrics.ClimateZone,
                Region = projectMetrics.Region,
                GeneratedAt = DateTime.UtcNow,
                MetricComparisons = new List<MetricComparison>()
            };

            await Task.Run(() =>
            {
                foreach (var metric in projectMetrics.Metrics)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var benchmark = GetBenchmark(metric.Key,
                        projectMetrics.BuildingType, projectMetrics.ClimateZone);

                    if (benchmark.Found)
                    {
                        var comparison = CompareMetric(metric.Key, metric.Value, benchmark);
                        report.MetricComparisons.Add(comparison);
                    }
                    else
                    {
                        report.MetricComparisons.Add(new MetricComparison
                        {
                            MetricType = metric.Key,
                            ProjectValue = metric.Value,
                            BenchmarkValue = 0,
                            Rating = PerformanceRating.NotRated,
                            Explanation = "No benchmark data available for comparison"
                        });
                    }
                }

                // Calculate overall score
                var ratedMetrics = report.MetricComparisons
                    .Where(m => m.Rating != PerformanceRating.NotRated)
                    .ToList();

                if (ratedMetrics.Any())
                {
                    report.OverallScore = ratedMetrics.Average(m => RatingToScore(m.Rating));
                    report.OverallRating = ScoreToRating(report.OverallScore);
                }
                else
                {
                    report.OverallRating = PerformanceRating.NotRated;
                }

                // Generate category summaries
                report.CategorySummaries = GenerateCategorySummaries(report.MetricComparisons);

            }, cancellationToken);

            // Track project performance
            TrackProjectPerformance(projectMetrics, report);

            progress?.Report($"Comparison complete: overall rating is {report.OverallRating}");
            Logger.Info("Project comparison complete: {0} metrics compared, overall: {1}",
                report.MetricComparisons.Count, report.OverallRating);

            return report;
        }

        /// <summary>
        /// Gets the overall performance rating for a project.
        /// </summary>
        public async Task<PerformanceRatingResult> GetPerformanceRatingAsync(
            ProjectMetrics projectMetrics,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            var comparison = await CompareToBaselineAsync(projectMetrics, cancellationToken, progress);

            var result = new PerformanceRatingResult
            {
                ProjectName = projectMetrics.ProjectName,
                OverallRating = comparison.OverallRating,
                OverallScore = comparison.OverallScore,
                CategoryRatings = new Dictionary<string, PerformanceRating>(StringComparer.OrdinalIgnoreCase),
                Strengths = new List<string>(),
                Weaknesses = new List<string>(),
                Recommendations = new List<string>()
            };

            // Category ratings
            foreach (var category in comparison.CategorySummaries)
            {
                result.CategoryRatings[category.Key] = category.Value.Rating;
            }

            // Identify strengths
            var excellentMetrics = comparison.MetricComparisons
                .Where(m => m.Rating == PerformanceRating.Excellent)
                .ToList();
            foreach (var metric in excellentMetrics)
            {
                result.Strengths.Add($"{GetMetricDisplayName(metric.MetricType)}: " +
                                     $"Excellent ({metric.ProjectValue:F1} vs benchmark {metric.BenchmarkValue:F1})");
            }

            // Identify weaknesses
            var poorMetrics = comparison.MetricComparisons
                .Where(m => m.Rating == PerformanceRating.BelowAverage ||
                            m.Rating == PerformanceRating.Poor)
                .ToList();
            foreach (var metric in poorMetrics)
            {
                result.Weaknesses.Add($"{GetMetricDisplayName(metric.MetricType)}: " +
                                      $"{metric.Rating} ({metric.ProjectValue:F1} vs benchmark {metric.BenchmarkValue:F1})");
            }

            // Generate recommendations
            foreach (var metric in poorMetrics)
            {
                var recommendations = GenerateRecommendations(metric, projectMetrics);
                result.Recommendations.AddRange(recommendations);
            }

            return result;
        }

        #endregion

        #region Target Setting

        /// <summary>
        /// Suggests realistic performance targets based on building type, climate,
        /// and optional certification goals (LEED, WELL, etc.).
        /// </summary>
        public async Task<TargetRecommendations> SetProjectTargetsAsync(
            string buildingType,
            string certificationGoal = null,
            string climateZone = null,
            string region = null,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info("Setting targets for {0} (cert: {1}, climate: {2})",
                buildingType, certificationGoal ?? "none", climateZone ?? "any");
            progress?.Report("Calculating performance targets...");

            var targets = new TargetRecommendations
            {
                BuildingType = buildingType,
                CertificationGoal = certificationGoal,
                ClimateZone = climateZone,
                Region = region,
                GeneratedAt = DateTime.UtcNow,
                Targets = new List<PerformanceTarget>()
            };

            await Task.Run(() =>
            {
                foreach (var metricInfo in MetricTypes.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var benchmark = GetBenchmark(metricInfo.MetricType, buildingType, climateZone);
                    if (!benchmark.Found) continue;

                    var target = new PerformanceTarget
                    {
                        MetricType = metricInfo.MetricType,
                        Category = metricInfo.Category,
                        DisplayName = metricInfo.DisplayName,
                        Unit = metricInfo.Unit,
                        BaselineValue = benchmark.BaselineValue,
                        LowerIsBetter = metricInfo.LowerIsBetter
                    };

                    // Set targets based on certification level
                    if (string.IsNullOrEmpty(certificationGoal) ||
                        string.Equals(certificationGoal, "None", StringComparison.OrdinalIgnoreCase))
                    {
                        // Meet code minimum
                        target.MinimumTarget = benchmark.BaselineValue;
                        target.RecommendedTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.9f
                            : benchmark.BaselineValue * 1.1f;
                        target.StretchTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.75f
                            : benchmark.BaselineValue * 1.25f;
                        target.TargetRationale = "Based on code baseline with improvement margin";
                    }
                    else if (string.Equals(certificationGoal, "LEED Silver",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        target.MinimumTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.85f
                            : benchmark.BaselineValue * 1.15f;
                        target.RecommendedTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.75f
                            : benchmark.BaselineValue * 1.25f;
                        target.StretchTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.65f
                            : benchmark.BaselineValue * 1.35f;
                        target.TargetRationale = "Based on LEED Silver prerequisites and credits";
                    }
                    else if (string.Equals(certificationGoal, "LEED Gold",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        target.MinimumTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.75f
                            : benchmark.BaselineValue * 1.25f;
                        target.RecommendedTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.65f
                            : benchmark.BaselineValue * 1.35f;
                        target.StretchTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.5f
                            : benchmark.BaselineValue * 1.5f;
                        target.TargetRationale = "Based on LEED Gold credit requirements";
                    }
                    else if (string.Equals(certificationGoal, "LEED Platinum",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        target.MinimumTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.6f
                            : benchmark.BaselineValue * 1.4f;
                        target.RecommendedTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.5f
                            : benchmark.BaselineValue * 1.5f;
                        target.StretchTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.35f
                            : benchmark.BaselineValue * 1.65f;
                        target.TargetRationale = "Based on LEED Platinum credit requirements";
                    }
                    else if (string.Equals(certificationGoal, "NetZero",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (metricInfo.Category == BenchmarkCategory.Energy ||
                            metricInfo.Category == BenchmarkCategory.Carbon)
                        {
                            target.MinimumTarget = metricInfo.LowerIsBetter
                                ? benchmark.BaselineValue * 0.4f
                                : benchmark.BaselineValue * 1.6f;
                            target.RecommendedTarget = metricInfo.LowerIsBetter
                                ? benchmark.BaselineValue * 0.25f
                                : benchmark.BaselineValue * 1.75f;
                            target.StretchTarget = 0;
                            target.TargetRationale = "Net-zero target: minimize then offset";
                        }
                        else
                        {
                            target.MinimumTarget = metricInfo.LowerIsBetter
                                ? benchmark.BaselineValue * 0.7f
                                : benchmark.BaselineValue * 1.3f;
                            target.RecommendedTarget = metricInfo.LowerIsBetter
                                ? benchmark.BaselineValue * 0.6f
                                : benchmark.BaselineValue * 1.4f;
                            target.StretchTarget = metricInfo.LowerIsBetter
                                ? benchmark.BaselineValue * 0.5f
                                : benchmark.BaselineValue * 1.5f;
                            target.TargetRationale = "High-performance target for net-zero building";
                        }
                    }
                    else
                    {
                        // Default improvement targets
                        target.MinimumTarget = benchmark.BaselineValue;
                        target.RecommendedTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.85f
                            : benchmark.BaselineValue * 1.15f;
                        target.StretchTarget = metricInfo.LowerIsBetter
                            ? benchmark.BaselineValue * 0.7f
                            : benchmark.BaselineValue * 1.3f;
                        target.TargetRationale = $"Based on {certificationGoal} goals";
                    }

                    targets.Targets.Add(target);
                }
            }, cancellationToken);

            progress?.Report($"Generated {targets.Targets.Count} performance targets for {buildingType}");
            return targets;
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Loads benchmark data from CSV files in the data directory.
        /// Reads SUSTAINABILITY_BENCHMARKS.csv, ENERGY_EMISSION_FACTORS.csv,
        /// and cost CSV files.
        /// </summary>
        public async Task<int> LoadBenchmarksFromCsvAsync(
            string csvDirectory,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info("Loading benchmarks from CSV directory: {0}", csvDirectory);
            progress?.Report("Loading performance benchmarks from CSV files...");

            int totalLoaded = 0;

            // Load sustainability benchmarks
            var sustainFile = FindCsvFile(csvDirectory, "SUSTAINABILITY_BENCHMARKS");
            if (sustainFile != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading sustainability benchmarks...");
                totalLoaded += await LoadSustainabilityBenchmarksAsync(sustainFile, cancellationToken);
            }

            // Load energy emission factors
            var energyFile = FindCsvFile(csvDirectory, "ENERGY_EMISSION_FACTORS");
            if (energyFile != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading energy emission factors...");
                totalLoaded += await LoadEnergyBenchmarksAsync(energyFile, cancellationToken);
            }

            // Load cost data
            var costMaterialsFile = FindCsvFile(csvDirectory, "COST_MATERIALS_REGIONAL");
            if (costMaterialsFile != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading regional cost benchmarks...");
                totalLoaded += await LoadCostBenchmarksAsync(costMaterialsFile, cancellationToken);
            }

            var costLaborFile = FindCsvFile(csvDirectory, "COST_LABOR_RATES");
            if (costLaborFile != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading labor cost benchmarks...");
                totalLoaded += await LoadLaborCostBenchmarksAsync(costLaborFile, cancellationToken);
            }

            // Search recursively in subdirectories
            var aiDir = Path.Combine(csvDirectory, "ai");
            if (Directory.Exists(aiDir))
            {
                var subFiles = Directory.GetFiles(aiDir, "*.csv", SearchOption.AllDirectories);
                foreach (var file in subFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
                    if (fileName.Contains("BENCHMARK") || fileName.Contains("PERFORMANCE") ||
                        fileName.Contains("ENERGY") || fileName.Contains("SUSTAINABILITY"))
                    {
                        progress?.Report($"Loading {Path.GetFileName(file)}...");
                        totalLoaded += await LoadGenericBenchmarkCsvAsync(file, cancellationToken);
                    }
                }
            }

            progress?.Report($"Loaded {totalLoaded} benchmarks from CSV files (total: {_benchmarks.Count})");
            Logger.Info("Loaded {0} benchmarks from CSV files", totalLoaded);
            return totalLoaded;
        }

        /// <summary>
        /// Adds a single benchmark entry.
        /// </summary>
        public void AddBenchmark(BenchmarkEntry entry)
        {
            if (entry == null) return;

            var key = BuildKey(entry.MetricType, entry.BuildingType, entry.ClimateZone);
            entry.BenchmarkKey = key;
            _benchmarks[key] = entry;
            IndexBenchmark(key, entry);
        }

        #endregion

        #region Trend and Peer Analysis

        /// <summary>
        /// Tracks project performance over its lifecycle for trend analysis.
        /// </summary>
        public void RecordProjectSnapshot(string projectId, ProjectMetrics metrics)
        {
            var record = _projectRecords.GetOrAdd(projectId, id => new ProjectPerformanceRecord
            {
                ProjectId = id,
                BuildingType = metrics.BuildingType,
                ClimateZone = metrics.ClimateZone,
                Region = metrics.Region,
                Snapshots = new List<PerformanceSnapshot>()
            });

            lock (_lockObject)
            {
                record.Snapshots.Add(new PerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    Phase = metrics.Phase ?? "Unknown",
                    Metrics = new Dictionary<string, float>(metrics.Metrics, StringComparer.OrdinalIgnoreCase)
                });

                // Keep bounded
                while (record.Snapshots.Count > 100)
                    record.Snapshots.RemoveAt(0);
            }
        }

        /// <summary>
        /// Gets performance trends for a project over time.
        /// </summary>
        public ProjectTrendReport GetProjectTrends(string projectId)
        {
            if (!_projectRecords.TryGetValue(projectId, out var record) ||
                record.Snapshots.Count < 2)
            {
                return new ProjectTrendReport
                {
                    ProjectId = projectId,
                    HasSufficientData = false
                };
            }

            var report = new ProjectTrendReport
            {
                ProjectId = projectId,
                HasSufficientData = true,
                SnapshotCount = record.Snapshots.Count,
                FirstSnapshot = record.Snapshots.First().Timestamp,
                LastSnapshot = record.Snapshots.Last().Timestamp,
                MetricTrends = new Dictionary<string, MetricTrend>(StringComparer.OrdinalIgnoreCase)
            };

            // Analyze each metric's trend
            var allMetrics = record.Snapshots
                .SelectMany(s => s.Metrics.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var metric in allMetrics)
            {
                var values = record.Snapshots
                    .Where(s => s.Metrics.ContainsKey(metric))
                    .Select(s => new { s.Timestamp, Value = s.Metrics[metric] })
                    .OrderBy(x => x.Timestamp)
                    .ToList();

                if (values.Count < 2) continue;

                var first = values.First().Value;
                var last = values.Last().Value;
                var change = last - first;
                var pctChange = first != 0 ? change / Math.Abs(first) : 0f;

                var isImproving = MetricTypes.TryGetValue(metric, out var info)
                    ? (info.LowerIsBetter ? change < 0 : change > 0)
                    : false;

                report.MetricTrends[metric] = new MetricTrend
                {
                    MetricType = metric,
                    StartValue = first,
                    EndValue = last,
                    AbsoluteChange = change,
                    PercentageChange = pctChange,
                    IsImproving = isImproving,
                    DataPoints = values.Count,
                    TrendDirection = change > 0.01f ? TrendDirection.Increasing :
                                    change < -0.01f ? TrendDirection.Decreasing :
                                    TrendDirection.Stable
                };
            }

            return report;
        }

        /// <summary>
        /// Compares a project against peers of the same building type and climate.
        /// </summary>
        public PeerComparisonReport CompareToPeers(string projectId)
        {
            if (!_projectRecords.TryGetValue(projectId, out var project))
            {
                return new PeerComparisonReport { ProjectId = projectId, HasPeers = false };
            }

            var peers = _projectRecords.Values
                .Where(p => p.ProjectId != projectId &&
                            string.Equals(p.BuildingType, project.BuildingType,
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!peers.Any())
            {
                return new PeerComparisonReport { ProjectId = projectId, HasPeers = false };
            }

            var report = new PeerComparisonReport
            {
                ProjectId = projectId,
                HasPeers = true,
                PeerCount = peers.Count,
                MetricRankings = new Dictionary<string, PeerRanking>(StringComparer.OrdinalIgnoreCase)
            };

            var projectLatest = project.Snapshots.LastOrDefault();
            if (projectLatest == null) return report;

            foreach (var metric in projectLatest.Metrics)
            {
                var peerValues = peers
                    .Select(p => p.Snapshots.LastOrDefault()?.Metrics?.TryGetValue(metric.Key, out var v) == true ? v : (float?)null)
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToList();

                if (!peerValues.Any()) continue;

                var allValues = peerValues.Concat(new[] { metric.Value }).OrderBy(v => v).ToList();
                var rank = allValues.IndexOf(metric.Value) + 1;

                // If lower is better, rank 1 is best
                bool lowerIsBetter = MetricTypes.TryGetValue(metric.Key, out var info) && info.LowerIsBetter;
                if (!lowerIsBetter)
                    rank = allValues.Count - rank + 1;

                report.MetricRankings[metric.Key] = new PeerRanking
                {
                    MetricType = metric.Key,
                    ProjectValue = metric.Value,
                    PeerAverage = peerValues.Average(),
                    PeerMedian = peerValues.OrderBy(v => v).ElementAt(peerValues.Count / 2),
                    PeerMin = peerValues.Min(),
                    PeerMax = peerValues.Max(),
                    Rank = rank,
                    TotalPeers = peerValues.Count + 1,
                    Percentile = (1.0f - (float)rank / (peerValues.Count + 1)) * 100f
                };
            }

            return report;
        }

        /// <summary>
        /// Gets statistics about the benchmark database.
        /// </summary>
        public BenchmarkStatistics GetStatistics()
        {
            return new BenchmarkStatistics
            {
                TotalBenchmarks = _benchmarks.Count,
                BenchmarksByCategory = _benchmarks.Values
                    .GroupBy(b => b.Category)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                BenchmarksByBuildingType = _buildingTypeIndex
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Count),
                MetricTypesAvailable = _metricTypeIndex.Keys.ToList(),
                ClimateZonesCovered = _climateZoneIndex.Keys.ToList(),
                RegionsCovered = _regionIndex.Keys.ToList(),
                ProjectsTracked = _projectRecords.Count
            };
        }

        #endregion

        #region Private Comparison Helpers

        private MetricComparison CompareMetric(string metricType, float projectValue,
            BenchmarkResult benchmark)
        {
            var info = MetricTypes.TryGetValue(metricType, out var mi) ? mi : null;
            bool lowerIsBetter = info?.LowerIsBetter ?? true;

            float baselineValue = benchmark.BaselineValue;
            float percentDifference = baselineValue != 0
                ? (projectValue - baselineValue) / Math.Abs(baselineValue)
                : 0f;

            // Rate performance
            PerformanceRating rating;
            if (lowerIsBetter)
            {
                rating = percentDifference <= -0.3f ? PerformanceRating.Excellent :
                         percentDifference <= -0.15f ? PerformanceRating.Good :
                         percentDifference <= 0.05f ? PerformanceRating.Average :
                         percentDifference <= 0.2f ? PerformanceRating.BelowAverage :
                         PerformanceRating.Poor;
            }
            else
            {
                rating = percentDifference >= 0.3f ? PerformanceRating.Excellent :
                         percentDifference >= 0.15f ? PerformanceRating.Good :
                         percentDifference >= -0.05f ? PerformanceRating.Average :
                         percentDifference >= -0.2f ? PerformanceRating.BelowAverage :
                         PerformanceRating.Poor;
            }

            return new MetricComparison
            {
                MetricType = metricType,
                DisplayName = info?.DisplayName ?? metricType,
                Unit = info?.Unit ?? "",
                Category = info?.Category ?? BenchmarkCategory.Energy,
                ProjectValue = projectValue,
                BenchmarkValue = baselineValue,
                PercentDifference = percentDifference,
                Rating = rating,
                LowerIsBetter = lowerIsBetter,
                Explanation = GenerateComparisonExplanation(metricType, projectValue,
                    baselineValue, percentDifference, rating, lowerIsBetter),
                BenchmarkSource = benchmark.Source
            };
        }

        private string GenerateComparisonExplanation(string metricType, float projectValue,
            float baselineValue, float percentDiff, PerformanceRating rating, bool lowerIsBetter)
        {
            var direction = percentDiff >= 0 ? "higher" : "lower";
            var betterOrWorse = (lowerIsBetter && percentDiff < 0) || (!lowerIsBetter && percentDiff > 0)
                ? "better" : "worse";

            return $"{GetMetricDisplayName(metricType)}: {projectValue:F1} vs baseline {baselineValue:F1} " +
                   $"({Math.Abs(percentDiff):P0} {direction}, {betterOrWorse} than benchmark). " +
                   $"Rating: {rating}";
        }

        private Dictionary<string, CategorySummary> GenerateCategorySummaries(
            List<MetricComparison> comparisons)
        {
            var summaries = new Dictionary<string, CategorySummary>(StringComparer.OrdinalIgnoreCase);

            var byCategory = comparisons
                .Where(c => c.Rating != PerformanceRating.NotRated)
                .GroupBy(c => c.Category)
                .ToList();

            foreach (var group in byCategory)
            {
                var avgScore = group.Average(c => RatingToScore(c.Rating));
                summaries[group.Key.ToString()] = new CategorySummary
                {
                    Category = group.Key,
                    MetricCount = group.Count(),
                    AverageScore = avgScore,
                    Rating = ScoreToRating(avgScore),
                    BestMetric = group.OrderByDescending(c => RatingToScore(c.Rating)).First().MetricType,
                    WorstMetric = group.OrderBy(c => RatingToScore(c.Rating)).First().MetricType
                };
            }

            return summaries;
        }

        private List<string> GenerateRecommendations(MetricComparison metric,
            ProjectMetrics projectMetrics)
        {
            var recommendations = new List<string>();

            switch (metric.Category)
            {
                case BenchmarkCategory.Energy:
                    recommendations.Add($"Improve {metric.DisplayName}: consider higher-efficiency " +
                                        $"HVAC equipment, improved envelope insulation, or LED lighting upgrades");
                    break;
                case BenchmarkCategory.Water:
                    recommendations.Add($"Reduce {metric.DisplayName}: install low-flow fixtures, " +
                                        $"rainwater harvesting, or greywater recycling");
                    break;
                case BenchmarkCategory.IndoorAirQuality:
                    recommendations.Add($"Improve {metric.DisplayName}: increase ventilation rates, " +
                                        $"add air filtration, or use low-VOC materials");
                    break;
                case BenchmarkCategory.Lighting:
                    recommendations.Add($"Optimize {metric.DisplayName}: redesign lighting layout, " +
                                        $"increase daylight harvesting, or add lighting controls");
                    break;
                case BenchmarkCategory.Acoustic:
                    recommendations.Add($"Address {metric.DisplayName}: add acoustic insulation, " +
                                        $"improve wall STC ratings, or add sound masking");
                    break;
                case BenchmarkCategory.Cost:
                    recommendations.Add($"Review {metric.DisplayName}: consider value engineering, " +
                                        $"alternative materials, or phased construction approach");
                    break;
                case BenchmarkCategory.Carbon:
                    recommendations.Add($"Reduce {metric.DisplayName}: specify low-carbon materials, " +
                                        $"optimize structural systems, or increase renewable energy");
                    break;
                default:
                    recommendations.Add($"Improve {metric.DisplayName} to meet baseline performance");
                    break;
            }

            return recommendations;
        }

        private float RatingToScore(PerformanceRating rating)
        {
            return rating switch
            {
                PerformanceRating.Excellent => 5.0f,
                PerformanceRating.Good => 4.0f,
                PerformanceRating.Average => 3.0f,
                PerformanceRating.BelowAverage => 2.0f,
                PerformanceRating.Poor => 1.0f,
                _ => 0f
            };
        }

        private PerformanceRating ScoreToRating(float score)
        {
            return score >= 4.5f ? PerformanceRating.Excellent :
                   score >= 3.5f ? PerformanceRating.Good :
                   score >= 2.5f ? PerformanceRating.Average :
                   score >= 1.5f ? PerformanceRating.BelowAverage :
                   PerformanceRating.Poor;
        }

        private string GetMetricDisplayName(string metricType)
        {
            return MetricTypes.TryGetValue(metricType, out var info)
                ? info.DisplayName : metricType;
        }

        #endregion

        #region Indexing and Key Building

        private string BuildKey(string metricType, string buildingType, string climateZone)
        {
            var parts = new List<string> { metricType ?? "Unknown", buildingType ?? "Generic" };
            if (!string.IsNullOrEmpty(climateZone))
                parts.Add(climateZone);
            return string.Join("|", parts);
        }

        private void IndexBenchmark(string key, BenchmarkEntry entry)
        {
            AddToIndex(_metricTypeIndex, entry.MetricType, key);
            AddToIndex(_buildingTypeIndex, entry.BuildingType ?? "Generic", key);

            if (!string.IsNullOrEmpty(entry.ClimateZone))
                AddToIndex(_climateZoneIndex, entry.ClimateZone, key);

            if (!string.IsNullOrEmpty(entry.Region))
                AddToIndex(_regionIndex, entry.Region, key);
        }

        private void AddToIndex(ConcurrentDictionary<string, HashSet<string>> index,
            string indexKey, string benchmarkKey)
        {
            index.AddOrUpdate(indexKey,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { benchmarkKey },
                (_, existing) =>
                {
                    lock (existing) { existing.Add(benchmarkKey); }
                    return existing;
                });
        }

        private BenchmarkResult BuildResult(BenchmarkEntry entry, string matchQuality)
        {
            return new BenchmarkResult
            {
                Found = true,
                MetricType = entry.MetricType,
                BuildingType = entry.BuildingType,
                ClimateZone = entry.ClimateZone,
                BaselineValue = entry.BaselineValue,
                GoodValue = entry.GoodValue,
                ExcellentValue = entry.ExcellentValue,
                Unit = entry.Unit,
                Source = entry.Source,
                MatchQuality = matchQuality,
                LowerIsBetter = entry.LowerIsBetter,
                Notes = entry.Notes
            };
        }

        private void TrackProjectPerformance(ProjectMetrics metrics, ComparisonReport report)
        {
            if (string.IsNullOrEmpty(metrics.ProjectId)) return;
            RecordProjectSnapshot(metrics.ProjectId, metrics);
        }

        private string FindCsvFile(string directory, string fileNamePart)
        {
            if (!Directory.Exists(directory)) return null;

            var files = Directory.GetFiles(directory, "*.csv")
                .Where(f => Path.GetFileNameWithoutExtension(f)
                    .ToUpperInvariant()
                    .Contains(fileNamePart.ToUpperInvariant()))
                .ToList();

            return files.FirstOrDefault();
        }

        #endregion

        #region CSV Loading Helpers

        private async Task<int> LoadSustainabilityBenchmarksAsync(
            string filePath, CancellationToken cancellationToken)
        {
            int loaded = 0;
            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return 0;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = BuildHeaderIndex(headers);

                for (int i = 1; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[i]);
                        var entry = MapSustainabilityCsvRow(fields, headerIndex);
                        if (entry != null)
                        {
                            AddBenchmark(entry);
                            loaded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Trace("Skipping sustainability CSV row {0}: {1}", i, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load sustainability benchmarks from {0}", filePath);
            }
            return loaded;
        }

        private async Task<int> LoadEnergyBenchmarksAsync(
            string filePath, CancellationToken cancellationToken)
        {
            int loaded = 0;
            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return 0;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = BuildHeaderIndex(headers);

                for (int i = 1; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[i]);
                        var entry = MapEnergyCsvRow(fields, headerIndex);
                        if (entry != null)
                        {
                            AddBenchmark(entry);
                            loaded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Trace("Skipping energy CSV row {0}: {1}", i, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load energy benchmarks from {0}", filePath);
            }
            return loaded;
        }

        private async Task<int> LoadCostBenchmarksAsync(
            string filePath, CancellationToken cancellationToken)
        {
            int loaded = 0;
            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return 0;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = BuildHeaderIndex(headers);

                for (int i = 1; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[i]);
                        var entry = MapCostCsvRow(fields, headerIndex);
                        if (entry != null)
                        {
                            AddBenchmark(entry);
                            loaded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Trace("Skipping cost CSV row {0}: {1}", i, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load cost benchmarks from {0}", filePath);
            }
            return loaded;
        }

        private async Task<int> LoadLaborCostBenchmarksAsync(
            string filePath, CancellationToken cancellationToken)
        {
            // Similar to cost benchmarks but for labor rates
            return await LoadCostBenchmarksAsync(filePath, cancellationToken);
        }

        private async Task<int> LoadGenericBenchmarkCsvAsync(
            string filePath, CancellationToken cancellationToken)
        {
            int loaded = 0;
            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return 0;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = BuildHeaderIndex(headers);

                for (int i = 1; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[i]);
                        var entry = MapGenericCsvRow(fields, headerIndex);
                        if (entry != null)
                        {
                            AddBenchmark(entry);
                            loaded++;
                        }
                    }
                    catch
                    {
                        // Skip malformed rows silently
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load generic benchmark CSV: {0}", filePath);
            }
            return loaded;
        }

        private BenchmarkEntry MapSustainabilityCsvRow(string[] fields,
            Dictionary<string, int> headers)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var metric = GetField("Metric") ?? GetField("MetricType") ?? GetField("Type");
            var value = GetField("Value") ?? GetField("Baseline") ?? GetField("Target");
            if (string.IsNullOrEmpty(metric) || string.IsNullOrEmpty(value)) return null;
            if (!float.TryParse(value, out var floatValue)) return null;

            return new BenchmarkEntry
            {
                MetricType = NormalizeMetricType(metric),
                BuildingType = GetField("BuildingType") ?? "Generic",
                ClimateZone = GetField("ClimateZone") ?? GetField("Climate"),
                BaselineValue = floatValue,
                GoodValue = float.TryParse(GetField("Good"), out var gv) ? gv : floatValue * 0.85f,
                ExcellentValue = float.TryParse(GetField("Excellent"), out var ev) ? ev : floatValue * 0.7f,
                Unit = GetField("Unit") ?? "",
                Source = GetField("Source") ?? "CSV",
                Category = ClassifyMetricCategory(metric),
                LowerIsBetter = ClassifyLowerIsBetter(metric),
                Notes = GetField("Notes")
            };
        }

        private BenchmarkEntry MapEnergyCsvRow(string[] fields,
            Dictionary<string, int> headers)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var factor = GetField("Factor") ?? GetField("EmissionFactor") ?? GetField("Value");
            if (string.IsNullOrEmpty(factor)) return null;
            if (!float.TryParse(factor, out var floatValue)) return null;

            return new BenchmarkEntry
            {
                MetricType = GetField("MetricType") ?? "OperationalCarbon",
                BuildingType = GetField("BuildingType") ?? "Generic",
                ClimateZone = GetField("ClimateZone"),
                Region = GetField("Region") ?? GetField("Country"),
                BaselineValue = floatValue,
                Unit = GetField("Unit") ?? "kgCO2e",
                Source = GetField("Source") ?? "Energy CSV",
                Category = BenchmarkCategory.Carbon,
                LowerIsBetter = true,
                Notes = GetField("Notes")
            };
        }

        private BenchmarkEntry MapCostCsvRow(string[] fields,
            Dictionary<string, int> headers)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var costStr = GetField("Cost") ?? GetField("Rate") ?? GetField("UnitCost") ??
                          GetField("Price") ?? GetField("Value");
            if (string.IsNullOrEmpty(costStr)) return null;

            // Remove currency symbols and parse
            costStr = costStr.Replace(",", "").Replace("$", "").Replace("UGX", "").Trim();
            if (!float.TryParse(costStr, out var costValue)) return null;

            var region = GetField("Region") ?? GetField("Country") ?? "Generic";
            var currency = GetField("Currency") ?? (region.Contains("Uganda") ? "UGX" : "USD");
            var metricType = currency == "UGX" ? "CostPerSqM_UGX" : "CostPerSqM";

            return new BenchmarkEntry
            {
                MetricType = metricType,
                BuildingType = GetField("BuildingType") ?? GetField("Category") ?? "Generic",
                Region = region,
                BaselineValue = costValue,
                Unit = $"{currency}/m\u00B2",
                Source = "Cost CSV",
                Category = BenchmarkCategory.Cost,
                LowerIsBetter = true,
                Notes = GetField("Description") ?? GetField("Notes")
            };
        }

        private BenchmarkEntry MapGenericCsvRow(string[] fields,
            Dictionary<string, int> headers)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var metric = GetField("Metric") ?? GetField("MetricType") ?? GetField("Parameter");
            var value = GetField("Value") ?? GetField("Baseline");
            if (string.IsNullOrEmpty(metric) || string.IsNullOrEmpty(value)) return null;
            if (!float.TryParse(value, out var floatValue)) return null;

            return new BenchmarkEntry
            {
                MetricType = NormalizeMetricType(metric),
                BuildingType = GetField("BuildingType") ?? "Generic",
                ClimateZone = GetField("ClimateZone"),
                Region = GetField("Region"),
                BaselineValue = floatValue,
                Unit = GetField("Unit") ?? "",
                Source = "CSV",
                Category = ClassifyMetricCategory(metric),
                LowerIsBetter = ClassifyLowerIsBetter(metric)
            };
        }

        private Dictionary<string, int> BuildHeaderIndex(string[] headers)
        {
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                index[headers[i].Trim()] = i;
            return index;
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { inQuotes = !inQuotes; }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else { current.Append(c); }
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        private string NormalizeMetricType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown";
            var lower = raw.ToLowerInvariant();

            if (lower.Contains("eui") || lower.Contains("energy use intensity")) return "EUI";
            if (lower.Contains("water use") || lower.Contains("water consumption")) return "WaterUse";
            if (lower.Contains("ventilation")) return "VentilationRate";
            if (lower.Contains("co2")) return "CO2Level";
            if (lower.Contains("voc")) return "VOCLevel";
            if (lower.Contains("pmv")) return "PMV";
            if (lower.Contains("ppd")) return "PPD";
            if (lower.Contains("lpd") || lower.Contains("lighting power")) return "LPD";
            if (lower.Contains("daylight")) return "DaylightAutonomy";
            if (lower.Contains("nc rating") || lower.Contains("noise criteria")) return "NCRating";
            if (lower.Contains("stc") || lower.Contains("sound transmission")) return "STC";
            if (lower.Contains("cost") && lower.Contains("sqft")) return "CostPerSqFt";
            if (lower.Contains("cost") && lower.Contains("sqm")) return "CostPerSqM";
            if (lower.Contains("embodied carbon")) return "EmbodiedCarbon";
            if (lower.Contains("operational carbon")) return "OperationalCarbon";

            return raw;
        }

        private BenchmarkCategory ClassifyMetricCategory(string metric)
        {
            if (string.IsNullOrEmpty(metric)) return BenchmarkCategory.Energy;
            var lower = metric.ToLowerInvariant();

            if (lower.Contains("energy") || lower.Contains("eui")) return BenchmarkCategory.Energy;
            if (lower.Contains("water")) return BenchmarkCategory.Water;
            if (lower.Contains("air") || lower.Contains("ventil") || lower.Contains("co2") ||
                lower.Contains("voc")) return BenchmarkCategory.IndoorAirQuality;
            if (lower.Contains("thermal") || lower.Contains("pmv") || lower.Contains("ppd"))
                return BenchmarkCategory.ThermalComfort;
            if (lower.Contains("light") || lower.Contains("lpd") || lower.Contains("daylight"))
                return BenchmarkCategory.Lighting;
            if (lower.Contains("acoustic") || lower.Contains("noise") || lower.Contains("stc") ||
                lower.Contains("sound")) return BenchmarkCategory.Acoustic;
            if (lower.Contains("cost") || lower.Contains("price")) return BenchmarkCategory.Cost;
            if (lower.Contains("schedule") || lower.Contains("duration") || lower.Contains("month"))
                return BenchmarkCategory.Schedule;
            if (lower.Contains("carbon") || lower.Contains("emission"))
                return BenchmarkCategory.Carbon;

            return BenchmarkCategory.Energy;
        }

        private bool ClassifyLowerIsBetter(string metric)
        {
            if (string.IsNullOrEmpty(metric)) return true;
            var lower = metric.ToLowerInvariant();

            // Metrics where higher is better
            if (lower.Contains("daylight") || lower.Contains("ventilation") ||
                lower.Contains("stc") || lower.Contains("autonomy"))
                return false;

            // Most building metrics: lower is better (less energy, less cost, etc.)
            return true;
        }

        #endregion

        #region Default Benchmarks

        private void InitializeDefaultBenchmarks()
        {
            // Energy benchmarks by building type (ASHRAE 90.1 baseline EUI in kBtu/ft2/yr)
            AddDefaultEnergyBenchmarks();

            // Water benchmarks
            AddDefaultWaterBenchmarks();

            // IAQ benchmarks (ASHRAE 62.1)
            AddDefaultIAQBenchmarks();

            // Lighting benchmarks (ASHRAE 90.1 LPD)
            AddDefaultLightingBenchmarks();

            // Acoustic benchmarks
            AddDefaultAcousticBenchmarks();

            // Cost benchmarks (multiple regions)
            AddDefaultCostBenchmarks();

            // Carbon benchmarks
            AddDefaultCarbonBenchmarks();

            // Schedule benchmarks
            AddDefaultScheduleBenchmarks();
        }

        private void AddDefaultEnergyBenchmarks()
        {
            var euiByType = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 85.1f, ["Retail"] = 78.2f, ["School"] = 58.5f,
                ["Hospital"] = 167.2f, ["Hotel"] = 93.3f, ["Residential"] = 47.5f,
                ["Warehouse"] = 26.8f, ["Restaurant"] = 179.3f, ["Healthcare"] = 140.0f,
                ["Laboratory"] = 195.0f, ["DataCenter"] = 250.0f, ["Generic"] = 75.0f
            };

            foreach (var kvp in euiByType)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "EUI",
                    BuildingType = kvp.Key,
                    BaselineValue = kvp.Value,
                    GoodValue = kvp.Value * 0.75f,
                    ExcellentValue = kvp.Value * 0.5f,
                    Unit = "kBtu/ft\u00B2/yr",
                    Source = "ASHRAE 90.1 Baseline",
                    Category = BenchmarkCategory.Energy,
                    LowerIsBetter = true,
                    Notes = "ASHRAE 90.1-2019 baseline"
                });
            }
        }

        private void AddDefaultWaterBenchmarks()
        {
            var waterByType = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 15.0f, ["Retail"] = 10.0f, ["School"] = 12.0f,
                ["Hospital"] = 80.0f, ["Hotel"] = 60.0f, ["Residential"] = 50.0f,
                ["Restaurant"] = 25.0f, ["Generic"] = 20.0f
            };

            foreach (var kvp in waterByType)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "WaterUse",
                    BuildingType = kvp.Key,
                    BaselineValue = kvp.Value,
                    GoodValue = kvp.Value * 0.8f,
                    ExcellentValue = kvp.Value * 0.6f,
                    Unit = "gallons/person/day",
                    Source = "WaterSense / EPA",
                    Category = BenchmarkCategory.Water,
                    LowerIsBetter = true
                });
            }
        }

        private void AddDefaultIAQBenchmarks()
        {
            AddBenchmark(new BenchmarkEntry
            {
                MetricType = "VentilationRate", BuildingType = "Office",
                BaselineValue = 17.0f, GoodValue = 20.0f, ExcellentValue = 25.0f,
                Unit = "cfm/person", Source = "ASHRAE 62.1",
                Category = BenchmarkCategory.IndoorAirQuality, LowerIsBetter = false
            });

            AddBenchmark(new BenchmarkEntry
            {
                MetricType = "CO2Level", BuildingType = "Generic",
                BaselineValue = 1000.0f, GoodValue = 800.0f, ExcellentValue = 600.0f,
                Unit = "ppm", Source = "ASHRAE 62.1",
                Category = BenchmarkCategory.IndoorAirQuality, LowerIsBetter = true
            });

            AddBenchmark(new BenchmarkEntry
            {
                MetricType = "VOCLevel", BuildingType = "Generic",
                BaselineValue = 500.0f, GoodValue = 300.0f, ExcellentValue = 100.0f,
                Unit = "\u00B5g/m\u00B3", Source = "EPA / WELL",
                Category = BenchmarkCategory.IndoorAirQuality, LowerIsBetter = true
            });
        }

        private void AddDefaultLightingBenchmarks()
        {
            var lpdByType = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 0.82f, ["Retail"] = 1.06f, ["School"] = 0.87f,
                ["Hospital"] = 0.96f, ["Hotel"] = 0.75f, ["Warehouse"] = 0.66f,
                ["Laboratory"] = 1.11f, ["Generic"] = 0.85f
            };

            foreach (var kvp in lpdByType)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "LPD",
                    BuildingType = kvp.Key,
                    BaselineValue = kvp.Value,
                    GoodValue = kvp.Value * 0.75f,
                    ExcellentValue = kvp.Value * 0.5f,
                    Unit = "W/ft\u00B2",
                    Source = "ASHRAE 90.1-2019",
                    Category = BenchmarkCategory.Lighting,
                    LowerIsBetter = true
                });
            }
        }

        private void AddDefaultAcousticBenchmarks()
        {
            var ncBySpace = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 40.0f, ["ConferenceRoom"] = 30.0f, ["Hospital"] = 35.0f,
                ["School"] = 35.0f, ["Library"] = 30.0f, ["Retail"] = 45.0f,
                ["Hotel"] = 35.0f, ["Generic"] = 40.0f
            };

            foreach (var kvp in ncBySpace)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "NCRating",
                    BuildingType = kvp.Key,
                    BaselineValue = kvp.Value,
                    GoodValue = kvp.Value - 5.0f,
                    ExcellentValue = kvp.Value - 10.0f,
                    Unit = "NC",
                    Source = "ASHRAE Handbook",
                    Category = BenchmarkCategory.Acoustic,
                    LowerIsBetter = true
                });
            }
        }

        private void AddDefaultCostBenchmarks()
        {
            // US costs ($/ft2)
            var usCosts = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 250.0f, ["Retail"] = 180.0f, ["School"] = 220.0f,
                ["Hospital"] = 450.0f, ["Hotel"] = 280.0f, ["Residential"] = 200.0f,
                ["Warehouse"] = 100.0f, ["Generic"] = 200.0f
            };
            foreach (var kvp in usCosts)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "CostPerSqFt", BuildingType = kvp.Key, Region = "US",
                    BaselineValue = kvp.Value, Unit = "$/ft\u00B2", Source = "RSMeans",
                    Category = BenchmarkCategory.Cost, LowerIsBetter = true
                });
            }

            // Uganda costs (UGX/m2)
            var ugCosts = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 3500000.0f, ["Retail"] = 2800000.0f, ["School"] = 2500000.0f,
                ["Hospital"] = 5500000.0f, ["Residential"] = 2000000.0f, ["Generic"] = 3000000.0f
            };
            foreach (var kvp in ugCosts)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "CostPerSqM_UGX", BuildingType = kvp.Key, Region = "Uganda",
                    BaselineValue = kvp.Value, Unit = "UGX/m\u00B2", Source = "Uganda Market Data",
                    Category = BenchmarkCategory.Cost, LowerIsBetter = true
                });
            }

            // Kenya costs (USD/m2)
            var keCosts = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 800.0f, ["Retail"] = 600.0f, ["School"] = 500.0f,
                ["Hospital"] = 1200.0f, ["Residential"] = 450.0f, ["Generic"] = 650.0f
            };
            foreach (var kvp in keCosts)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "CostPerSqM", BuildingType = kvp.Key, Region = "Kenya",
                    BaselineValue = kvp.Value, Unit = "$/m\u00B2", Source = "Kenya Market Data",
                    Category = BenchmarkCategory.Cost, LowerIsBetter = true
                });
            }
        }

        private void AddDefaultCarbonBenchmarks()
        {
            var embodiedByType = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 500.0f, ["Retail"] = 400.0f, ["School"] = 450.0f,
                ["Hospital"] = 700.0f, ["Residential"] = 350.0f, ["Generic"] = 450.0f
            };
            foreach (var kvp in embodiedByType)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "EmbodiedCarbon", BuildingType = kvp.Key,
                    BaselineValue = kvp.Value, GoodValue = kvp.Value * 0.7f,
                    ExcellentValue = kvp.Value * 0.5f,
                    Unit = "kgCO\u2082e/m\u00B2", Source = "Industry Average",
                    Category = BenchmarkCategory.Carbon, LowerIsBetter = true
                });
            }
        }

        private void AddDefaultScheduleBenchmarks()
        {
            var monthsPerFloor = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["SteelFrame"] = 0.75f, ["ConcreteFrame"] = 1.0f,
                ["TimberFrame"] = 0.6f, ["MasonryBearing"] = 1.2f,
                ["Generic"] = 1.0f
            };
            foreach (var kvp in monthsPerFloor)
            {
                AddBenchmark(new BenchmarkEntry
                {
                    MetricType = "MonthsPerFloor", BuildingType = kvp.Key,
                    BaselineValue = kvp.Value,
                    Unit = "months", Source = "Industry Average",
                    Category = BenchmarkCategory.Schedule, LowerIsBetter = true
                });
            }
        }

        #endregion
    }

    #endregion

    #region Configuration

    public class BenchmarkConfiguration
    {
        public float DefaultGoodMultiplier { get; set; } = 0.85f;
        public float DefaultExcellentMultiplier { get; set; } = 0.7f;
    }

    #endregion

    #region Benchmark Types

    /// <summary>
    /// A single benchmark data entry.
    /// </summary>
    public class BenchmarkEntry
    {
        public string BenchmarkKey { get; set; }
        public string MetricType { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public string Region { get; set; }
        public BenchmarkCategory Category { get; set; }
        public float BaselineValue { get; set; }
        public float GoodValue { get; set; }
        public float ExcellentValue { get; set; }
        public string Unit { get; set; }
        public string Source { get; set; }
        public bool LowerIsBetter { get; set; } = true;
        public string Notes { get; set; }
    }

    public class MetricTypeInfo
    {
        public string MetricType { get; set; }
        public BenchmarkCategory Category { get; set; }
        public string DisplayName { get; set; }
        public string Unit { get; set; }
        public bool LowerIsBetter { get; set; }
        public string Description { get; set; }
    }

    public enum BenchmarkCategory
    {
        Energy,
        Water,
        IndoorAirQuality,
        ThermalComfort,
        Lighting,
        Acoustic,
        Cost,
        Schedule,
        Carbon
    }

    #endregion

    #region Result Types

    public class BenchmarkResult
    {
        public bool Found { get; set; }
        public string MetricType { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public float BaselineValue { get; set; }
        public float GoodValue { get; set; }
        public float ExcellentValue { get; set; }
        public string Unit { get; set; }
        public string Source { get; set; }
        public string MatchQuality { get; set; }
        public bool LowerIsBetter { get; set; }
        public string Notes { get; set; }
    }

    public enum PerformanceRating
    {
        Excellent,
        Good,
        Average,
        BelowAverage,
        Poor,
        NotRated
    }

    #endregion

    #region Comparison Types

    public class ProjectMetrics
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public string Region { get; set; }
        public string Phase { get; set; }
        public Dictionary<string, float> Metrics { get; set; }
            = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    public class ComparisonReport
    {
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public string Region { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<MetricComparison> MetricComparisons { get; set; }
        public PerformanceRating OverallRating { get; set; }
        public float OverallScore { get; set; }
        public Dictionary<string, CategorySummary> CategorySummaries { get; set; }
    }

    public class MetricComparison
    {
        public string MetricType { get; set; }
        public string DisplayName { get; set; }
        public string Unit { get; set; }
        public BenchmarkCategory Category { get; set; }
        public float ProjectValue { get; set; }
        public float BenchmarkValue { get; set; }
        public float PercentDifference { get; set; }
        public PerformanceRating Rating { get; set; }
        public bool LowerIsBetter { get; set; }
        public string Explanation { get; set; }
        public string BenchmarkSource { get; set; }
    }

    public class CategorySummary
    {
        public BenchmarkCategory Category { get; set; }
        public int MetricCount { get; set; }
        public float AverageScore { get; set; }
        public PerformanceRating Rating { get; set; }
        public string BestMetric { get; set; }
        public string WorstMetric { get; set; }
    }

    public class PerformanceRatingResult
    {
        public string ProjectName { get; set; }
        public PerformanceRating OverallRating { get; set; }
        public float OverallScore { get; set; }
        public Dictionary<string, PerformanceRating> CategoryRatings { get; set; }
        public List<string> Strengths { get; set; }
        public List<string> Weaknesses { get; set; }
        public List<string> Recommendations { get; set; }
    }

    #endregion

    #region Target Types

    public class TargetRecommendations
    {
        public string BuildingType { get; set; }
        public string CertificationGoal { get; set; }
        public string ClimateZone { get; set; }
        public string Region { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<PerformanceTarget> Targets { get; set; } = new List<PerformanceTarget>();
    }

    public class PerformanceTarget
    {
        public string MetricType { get; set; }
        public BenchmarkCategory Category { get; set; }
        public string DisplayName { get; set; }
        public string Unit { get; set; }
        public float BaselineValue { get; set; }
        public float MinimumTarget { get; set; }
        public float RecommendedTarget { get; set; }
        public float StretchTarget { get; set; }
        public bool LowerIsBetter { get; set; }
        public string TargetRationale { get; set; }
    }

    #endregion

    #region Trend and Peer Types

    public class ProjectPerformanceRecord
    {
        public string ProjectId { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public string Region { get; set; }
        public List<PerformanceSnapshot> Snapshots { get; set; } = new List<PerformanceSnapshot>();
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string Phase { get; set; }
        public Dictionary<string, float> Metrics { get; set; }
    }

    public class ProjectTrendReport
    {
        public string ProjectId { get; set; }
        public bool HasSufficientData { get; set; }
        public int SnapshotCount { get; set; }
        public DateTime FirstSnapshot { get; set; }
        public DateTime LastSnapshot { get; set; }
        public Dictionary<string, MetricTrend> MetricTrends { get; set; }
    }

    public class MetricTrend
    {
        public string MetricType { get; set; }
        public float StartValue { get; set; }
        public float EndValue { get; set; }
        public float AbsoluteChange { get; set; }
        public float PercentageChange { get; set; }
        public bool IsImproving { get; set; }
        public int DataPoints { get; set; }
        public TrendDirection TrendDirection { get; set; }
    }

    public enum TrendDirection
    {
        Increasing,
        Decreasing,
        Stable
    }

    public class PeerComparisonReport
    {
        public string ProjectId { get; set; }
        public bool HasPeers { get; set; }
        public int PeerCount { get; set; }
        public Dictionary<string, PeerRanking> MetricRankings { get; set; }
    }

    public class PeerRanking
    {
        public string MetricType { get; set; }
        public float ProjectValue { get; set; }
        public float PeerAverage { get; set; }
        public float PeerMedian { get; set; }
        public float PeerMin { get; set; }
        public float PeerMax { get; set; }
        public int Rank { get; set; }
        public int TotalPeers { get; set; }
        public float Percentile { get; set; }
    }

    #endregion

    #region Statistics

    public class BenchmarkStatistics
    {
        public int TotalBenchmarks { get; set; }
        public Dictionary<string, int> BenchmarksByCategory { get; set; }
        public Dictionary<string, int> BenchmarksByBuildingType { get; set; }
        public List<string> MetricTypesAvailable { get; set; }
        public List<string> ClimateZonesCovered { get; set; }
        public List<string> RegionsCovered { get; set; }
        public int ProjectsTracked { get; set; }
    }

    #endregion
}
