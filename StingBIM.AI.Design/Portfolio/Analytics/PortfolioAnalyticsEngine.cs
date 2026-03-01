// StingBIM.AI.Portfolio - PortfolioAnalyticsEngine.cs
// Cross-Project Learning and Portfolio Analytics
// Phase 4: Enterprise AI Transformation - Enterprise Intelligence
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Portfolio.Analytics
{
    /// <summary>
    /// Enterprise-grade portfolio analytics engine providing cross-project learning,
    /// benchmarking, trend analysis, and predictive insights across building portfolios.
    /// </summary>
    public class PortfolioAnalyticsEngine
    {
        #region Fields

        private readonly Dictionary<string, ProjectRecord> _projects;
        private readonly Dictionary<string, PortfolioBenchmark> _benchmarks;
        private readonly PatternMiner _patternMiner;
        private readonly TrendAnalyzer _trendAnalyzer;
        private readonly PredictiveModeler _predictiveModeler;
        private readonly KnowledgeExtractor _knowledgeExtractor;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public PortfolioAnalyticsEngine()
        {
            _projects = new Dictionary<string, ProjectRecord>(StringComparer.OrdinalIgnoreCase);
            _benchmarks = new Dictionary<string, PortfolioBenchmark>(StringComparer.OrdinalIgnoreCase);
            _patternMiner = new PatternMiner();
            _trendAnalyzer = new TrendAnalyzer();
            _predictiveModeler = new PredictiveModeler();
            _knowledgeExtractor = new KnowledgeExtractor();

            InitializeDefaultBenchmarks();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultBenchmarks()
        {
            // Cost benchmarks by building type
            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "COST-OFFICE",
                Category = "Cost",
                BuildingType = "Office",
                Metric = "CostPerSqm",
                MedianValue = 1800,
                LowerQuartile = 1500,
                UpperQuartile = 2200,
                Unit = "USD/m²",
                Region = "East Africa"
            });

            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "COST-RESIDENTIAL",
                Category = "Cost",
                BuildingType = "Residential",
                Metric = "CostPerSqm",
                MedianValue = 1200,
                LowerQuartile = 900,
                UpperQuartile = 1600,
                Unit = "USD/m²",
                Region = "East Africa"
            });

            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "COST-HEALTHCARE",
                Category = "Cost",
                BuildingType = "Healthcare",
                Metric = "CostPerSqm",
                MedianValue = 2800,
                LowerQuartile = 2200,
                UpperQuartile = 3500,
                Unit = "USD/m²",
                Region = "East Africa"
            });

            // Schedule benchmarks
            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "SCHED-OFFICE",
                Category = "Schedule",
                BuildingType = "Office",
                Metric = "ConstructionDaysPerFloor",
                MedianValue = 45,
                LowerQuartile = 35,
                UpperQuartile = 60,
                Unit = "days/floor",
                Region = "Global"
            });

            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "SCHED-RESIDENTIAL",
                Category = "Schedule",
                BuildingType = "Residential",
                Metric = "ConstructionDaysPerFloor",
                MedianValue = 30,
                LowerQuartile = 25,
                UpperQuartile = 40,
                Unit = "days/floor",
                Region = "Global"
            });

            // Energy benchmarks
            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "ENERGY-OFFICE",
                Category = "Energy",
                BuildingType = "Office",
                Metric = "EUI",
                MedianValue = 150,
                LowerQuartile = 120,
                UpperQuartile = 200,
                Unit = "kWh/m²/year",
                Region = "Tropical"
            });

            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "ENERGY-RESIDENTIAL",
                Category = "Energy",
                BuildingType = "Residential",
                Metric = "EUI",
                MedianValue = 80,
                LowerQuartile = 60,
                UpperQuartile = 110,
                Unit = "kWh/m²/year",
                Region = "Tropical"
            });

            // Efficiency benchmarks
            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "EFF-OFFICE",
                Category = "Efficiency",
                BuildingType = "Office",
                Metric = "NetToGrossRatio",
                MedianValue = 0.82,
                LowerQuartile = 0.78,
                UpperQuartile = 0.87,
                Unit = "ratio",
                Region = "Global"
            });

            AddBenchmark(new PortfolioBenchmark
            {
                BenchmarkId = "EFF-HEALTHCARE",
                Category = "Efficiency",
                BuildingType = "Healthcare",
                Metric = "NetToGrossRatio",
                MedianValue = 0.65,
                LowerQuartile = 0.60,
                UpperQuartile = 0.72,
                Unit = "ratio",
                Region = "Global"
            });
        }

        #endregion

        #region Public Methods - Project Management

        public void AddProject(ProjectRecord project)
        {
            lock (_lockObject)
            {
                _projects[project.ProjectId] = project;
                UpdateBenchmarksFromProject(project);
            }
        }

        public void AddBenchmark(PortfolioBenchmark benchmark)
        {
            lock (_lockObject)
            {
                _benchmarks[benchmark.BenchmarkId] = benchmark;
            }
        }

        public ProjectRecord GetProject(string projectId)
        {
            lock (_lockObject)
            {
                return _projects.GetValueOrDefault(projectId);
            }
        }

        public IEnumerable<ProjectRecord> GetProjectsByType(string buildingType)
        {
            lock (_lockObject)
            {
                return _projects.Values
                    .Where(p => p.BuildingType.Equals(buildingType, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        public IEnumerable<ProjectRecord> GetProjectsByRegion(string region)
        {
            lock (_lockObject)
            {
                return _projects.Values
                    .Where(p => p.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        #endregion

        #region Public Methods - Benchmarking

        /// <summary>
        /// Compares a project against portfolio benchmarks
        /// </summary>
        public BenchmarkComparison BenchmarkProject(string projectId)
        {
            var project = _projects.GetValueOrDefault(projectId);
            if (project == null) return null;

            var comparison = new BenchmarkComparison
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                BuildingType = project.BuildingType,
                ComparisonDate = DateTime.Now
            };

            // Get applicable benchmarks
            var applicableBenchmarks = _benchmarks.Values
                .Where(b => b.BuildingType.Equals(project.BuildingType, StringComparison.OrdinalIgnoreCase) ||
                            b.BuildingType == "All")
                .ToList();

            foreach (var benchmark in applicableBenchmarks)
            {
                var projectValue = GetProjectMetricValue(project, benchmark.Metric);
                if (!projectValue.HasValue) continue;

                var result = new BenchmarkResult
                {
                    BenchmarkId = benchmark.BenchmarkId,
                    Metric = benchmark.Metric,
                    Category = benchmark.Category,
                    ProjectValue = projectValue.Value,
                    BenchmarkMedian = benchmark.MedianValue,
                    BenchmarkLower = benchmark.LowerQuartile,
                    BenchmarkUpper = benchmark.UpperQuartile,
                    Unit = benchmark.Unit
                };

                // Calculate percentile
                result.Percentile = CalculatePercentile(projectValue.Value, benchmark);
                result.Performance = GetPerformanceRating(result.Percentile, benchmark.Category);
                result.Variance = (projectValue.Value - benchmark.MedianValue) / benchmark.MedianValue * 100;

                comparison.Results.Add(result);
            }

            comparison.OverallScore = CalculateOverallBenchmarkScore(comparison.Results);
            comparison.Strengths = IdentifyStrengths(comparison.Results);
            comparison.Weaknesses = IdentifyWeaknesses(comparison.Results);

            return comparison;
        }

        /// <summary>
        /// Gets benchmark values for a specific metric and building type
        /// </summary>
        public PortfolioBenchmark GetBenchmark(string buildingType, string metric)
        {
            return _benchmarks.Values
                .FirstOrDefault(b => b.BuildingType.Equals(buildingType, StringComparison.OrdinalIgnoreCase) &&
                                     b.Metric.Equals(metric, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Generates custom benchmark from portfolio data
        /// </summary>
        public PortfolioBenchmark GenerateBenchmark(string buildingType, string metric, BenchmarkCriteria criteria = null)
        {
            var projects = GetProjectsByType(buildingType);
            if (criteria?.Region != null)
            {
                projects = projects.Where(p => p.Region.Equals(criteria.Region, StringComparison.OrdinalIgnoreCase));
            }
            if (criteria?.MinCompletionDate.HasValue == true)
            {
                projects = projects.Where(p => p.CompletionDate >= criteria.MinCompletionDate);
            }

            var values = projects
                .Select(p => GetProjectMetricValue(p, metric))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .OrderBy(v => v)
                .ToList();

            if (values.Count < 3) return null;

            return new PortfolioBenchmark
            {
                BenchmarkId = $"CUSTOM-{buildingType}-{metric}",
                Category = "Custom",
                BuildingType = buildingType,
                Metric = metric,
                MedianValue = Percentile(values, 50),
                LowerQuartile = Percentile(values, 25),
                UpperQuartile = Percentile(values, 75),
                MinValue = values.First(),
                MaxValue = values.Last(),
                SampleSize = values.Count,
                GeneratedAt = DateTime.Now
            };
        }

        #endregion

        #region Public Methods - Cross-Project Learning

        /// <summary>
        /// Mines patterns from successful projects
        /// </summary>
        public async Task<PatternMiningResult> MineSuccessPatternsAsync(
            PatternMiningOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new PatternMiningOptions();

            var successfulProjects = _projects.Values
                .Where(p => p.SuccessScore >= options.MinSuccessScore)
                .ToList();

            return await _patternMiner.MineAsync(successfulProjects, options, cancellationToken);
        }

        /// <summary>
        /// Extracts knowledge from project data
        /// </summary>
        public async Task<KnowledgeExtractionResult> ExtractKnowledgeAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            var project = _projects.GetValueOrDefault(projectId);
            if (project == null) return null;

            return await _knowledgeExtractor.ExtractAsync(project, cancellationToken);
        }

        /// <summary>
        /// Finds similar projects for reference
        /// </summary>
        public IEnumerable<SimilarProject> FindSimilarProjects(
            ProjectCharacteristics characteristics,
            int maxResults = 10)
        {
            var scoredProjects = _projects.Values
                .Select(p => new
                {
                    Project = p,
                    Similarity = CalculateSimilarity(p, characteristics)
                })
                .Where(s => s.Similarity > 0.5)
                .OrderByDescending(s => s.Similarity)
                .Take(maxResults)
                .ToList();

            return scoredProjects.Select(s => new SimilarProject
            {
                ProjectId = s.Project.ProjectId,
                ProjectName = s.Project.Name,
                BuildingType = s.Project.BuildingType,
                Similarity = s.Similarity,
                KeyMetrics = ExtractKeyMetrics(s.Project),
                LessonsLearned = s.Project.LessonsLearned
            });
        }

        /// <summary>
        /// Gets best practices from portfolio
        /// </summary>
        public BestPracticesReport GetBestPractices(string buildingType, string category = null)
        {
            var report = new BestPracticesReport
            {
                BuildingType = buildingType,
                GeneratedAt = DateTime.Now
            };

            var topProjects = GetProjectsByType(buildingType)
                .OrderByDescending(p => p.SuccessScore)
                .Take(10)
                .ToList();

            if (!topProjects.Any()) return report;

            // Extract common practices
            var commonPractices = ExtractCommonPractices(topProjects);
            report.Practices.AddRange(commonPractices);

            // Calculate metrics from top performers
            report.TargetMetrics = CalculateTargetMetrics(topProjects);

            // Add lessons learned
            report.LessonsLearned = topProjects
                .SelectMany(p => p.LessonsLearned ?? new List<string>())
                .GroupBy(l => l)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            return report;
        }

        #endregion

        #region Public Methods - Trend Analysis

        /// <summary>
        /// Analyzes trends across the portfolio
        /// </summary>
        public TrendAnalysisResult AnalyzeTrends(TrendAnalysisOptions options = null)
        {
            options = options ?? new TrendAnalysisOptions();

            return _trendAnalyzer.Analyze(_projects.Values.ToList(), options);
        }

        /// <summary>
        /// Gets cost trend over time
        /// </summary>
        public MetricTrend GetCostTrend(string buildingType, int years = 5)
        {
            var projects = GetProjectsByType(buildingType)
                .Where(p => p.CompletionDate >= DateTime.Now.AddYears(-years))
                .OrderBy(p => p.CompletionDate)
                .ToList();

            return CalculateMetricTrend(projects, "CostPerSqm", "Cost", years);
        }

        /// <summary>
        /// Gets schedule performance trend
        /// </summary>
        public MetricTrend GetScheduleTrend(string buildingType, int years = 5)
        {
            var projects = GetProjectsByType(buildingType)
                .Where(p => p.CompletionDate >= DateTime.Now.AddYears(-years))
                .OrderBy(p => p.CompletionDate)
                .ToList();

            return CalculateMetricTrend(projects, "ScheduleVariance", "Schedule", years);
        }

        /// <summary>
        /// Gets quality/success score trend
        /// </summary>
        public MetricTrend GetQualityTrend(int years = 5)
        {
            var projects = _projects.Values
                .Where(p => p.CompletionDate >= DateTime.Now.AddYears(-years))
                .OrderBy(p => p.CompletionDate)
                .ToList();

            return CalculateMetricTrend(projects, "SuccessScore", "Quality", years);
        }

        #endregion

        #region Public Methods - Predictive Analytics

        /// <summary>
        /// Predicts project cost based on historical data
        /// </summary>
        public CostPrediction PredictCost(ProjectCharacteristics characteristics)
        {
            return _predictiveModeler.PredictCost(characteristics, _projects.Values.ToList());
        }

        /// <summary>
        /// Predicts project duration
        /// </summary>
        public SchedulePrediction PredictSchedule(ProjectCharacteristics characteristics)
        {
            return _predictiveModeler.PredictSchedule(characteristics, _projects.Values.ToList());
        }

        /// <summary>
        /// Predicts project risk factors
        /// </summary>
        public RiskPrediction PredictRisks(ProjectCharacteristics characteristics)
        {
            return _predictiveModeler.PredictRisks(characteristics, _projects.Values.ToList());
        }

        /// <summary>
        /// Gets early warning indicators
        /// </summary>
        public List<EarlyWarning> GetEarlyWarnings(string projectId, ProjectProgress currentProgress)
        {
            var project = _projects.GetValueOrDefault(projectId);
            if (project == null) return new List<EarlyWarning>();

            var warnings = new List<EarlyWarning>();

            // Compare to similar completed projects at same stage
            var similarProjects = FindSimilarProjects(ExtractCharacteristics(project), 20);

            foreach (var similar in similarProjects.Where(s => s.Similarity > 0.7))
            {
                var historicalProject = _projects.GetValueOrDefault(similar.ProjectId);
                if (historicalProject == null) continue;

                // Check if current metrics indicate potential issues
                if (currentProgress.CostVariance > historicalProject.Metrics.GetValueOrDefault("EarlyCostVariance", 0) + 5)
                {
                    warnings.Add(new EarlyWarning
                    {
                        WarningType = WarningType.CostOverrun,
                        Severity = WarningSeverity.Medium,
                        Message = $"Cost variance ({currentProgress.CostVariance:F1}%) exceeds historical pattern",
                        BasedOnProjects = 1,
                        Recommendation = "Review cost control measures and contingency"
                    });
                }

                if (currentProgress.ScheduleVariance > historicalProject.Metrics.GetValueOrDefault("EarlyScheduleVariance", 0) + 5)
                {
                    warnings.Add(new EarlyWarning
                    {
                        WarningType = WarningType.ScheduleDelay,
                        Severity = WarningSeverity.Medium,
                        Message = $"Schedule variance ({currentProgress.ScheduleVariance:F1}%) exceeds historical pattern",
                        BasedOnProjects = 1,
                        Recommendation = "Review critical path and resource allocation"
                    });
                }
            }

            return warnings.Distinct().ToList();
        }

        #endregion

        #region Public Methods - Team & Vendor Analysis

        /// <summary>
        /// Analyzes team performance across projects
        /// </summary>
        public TeamPerformanceReport AnalyzeTeamPerformance(string teamId)
        {
            var teamProjects = _projects.Values
                .Where(p => p.TeamIds?.Contains(teamId) == true)
                .ToList();

            if (!teamProjects.Any()) return null;

            return new TeamPerformanceReport
            {
                TeamId = teamId,
                ProjectCount = teamProjects.Count,
                AverageSuccessScore = teamProjects.Average(p => p.SuccessScore),
                AverageCostVariance = teamProjects.Average(p => p.Metrics.GetValueOrDefault("CostVariance", 0)),
                AverageScheduleVariance = teamProjects.Average(p => p.Metrics.GetValueOrDefault("ScheduleVariance", 0)),
                BuildingTypeExperience = teamProjects.GroupBy(p => p.BuildingType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                StrengthAreas = IdentifyTeamStrengths(teamProjects),
                ImprovementAreas = IdentifyTeamWeaknesses(teamProjects)
            };
        }

        /// <summary>
        /// Analyzes vendor performance across projects
        /// </summary>
        public VendorPerformanceReport AnalyzeVendorPerformance(string vendorId)
        {
            var vendorProjects = _projects.Values
                .Where(p => p.VendorIds?.Contains(vendorId) == true)
                .ToList();

            if (!vendorProjects.Any()) return null;

            return new VendorPerformanceReport
            {
                VendorId = vendorId,
                ProjectCount = vendorProjects.Count,
                QualityRating = vendorProjects.Average(p => p.VendorRatings?.GetValueOrDefault(vendorId, 0) ?? 0),
                OnTimeDeliveryRate = vendorProjects.Count(p => p.Metrics.GetValueOrDefault("ScheduleVariance", 0) <= 0) /
                                     (double)vendorProjects.Count,
                DefectRate = vendorProjects.Average(p => p.Metrics.GetValueOrDefault("DefectRate", 0)),
                CostCompetitiveness = CalculateVendorCostCompetitiveness(vendorId, vendorProjects)
            };
        }

        #endregion

        #region Public Methods - Portfolio Dashboard

        /// <summary>
        /// Gets comprehensive portfolio dashboard
        /// </summary>
        public PortfolioDashboard GetDashboard()
        {
            var projects = _projects.Values.ToList();

            return new PortfolioDashboard
            {
                GeneratedAt = DateTime.Now,
                TotalProjects = projects.Count,
                TotalArea = projects.Sum(p => p.Metrics.GetValueOrDefault("GrossArea", 0)),
                TotalValue = projects.Sum(p => p.Metrics.GetValueOrDefault("TotalCost", 0)),
                AverageSuccessScore = projects.Any() ? projects.Average(p => p.SuccessScore) : 0,
                ProjectsByType = projects.GroupBy(p => p.BuildingType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ProjectsByRegion = projects.GroupBy(p => p.Region)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ProjectsByStatus = projects.GroupBy(p => p.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentProjects = projects.OrderByDescending(p => p.CompletionDate).Take(5).ToList(),
                TopPerformers = projects.OrderByDescending(p => p.SuccessScore).Take(5).ToList(),
                KPIs = CalculatePortfolioKPIs(projects)
            };
        }

        #endregion

        #region Private Methods - Calculations

        private double? GetProjectMetricValue(ProjectRecord project, string metric)
        {
            if (project.Metrics?.ContainsKey(metric) == true)
            {
                return project.Metrics[metric];
            }
            return null;
        }

        private double CalculatePercentile(double value, PortfolioBenchmark benchmark)
        {
            if (value <= benchmark.LowerQuartile) return 25 * value / benchmark.LowerQuartile;
            if (value <= benchmark.MedianValue)
                return 25 + 25 * (value - benchmark.LowerQuartile) / (benchmark.MedianValue - benchmark.LowerQuartile);
            if (value <= benchmark.UpperQuartile)
                return 50 + 25 * (value - benchmark.MedianValue) / (benchmark.UpperQuartile - benchmark.MedianValue);
            return 75 + 25 * Math.Min(1, (value - benchmark.UpperQuartile) / (benchmark.UpperQuartile - benchmark.MedianValue));
        }

        private PerformanceRating GetPerformanceRating(double percentile, string category)
        {
            // For cost/energy, lower is better
            if (category == "Cost" || category == "Energy" || category == "Schedule")
            {
                if (percentile < 25) return PerformanceRating.Excellent;
                if (percentile < 50) return PerformanceRating.Good;
                if (percentile < 75) return PerformanceRating.Average;
                return PerformanceRating.BelowAverage;
            }
            // For efficiency/quality, higher is better
            if (percentile >= 75) return PerformanceRating.Excellent;
            if (percentile >= 50) return PerformanceRating.Good;
            if (percentile >= 25) return PerformanceRating.Average;
            return PerformanceRating.BelowAverage;
        }

        private double CalculateOverallBenchmarkScore(List<BenchmarkResult> results)
        {
            if (!results.Any()) return 0;

            return results.Average(r =>
            {
                var score = r.Performance switch
                {
                    PerformanceRating.Excellent => 100,
                    PerformanceRating.Good => 75,
                    PerformanceRating.Average => 50,
                    PerformanceRating.BelowAverage => 25,
                    _ => 0
                };
                return score;
            });
        }

        private List<string> IdentifyStrengths(List<BenchmarkResult> results)
        {
            return results
                .Where(r => r.Performance == PerformanceRating.Excellent || r.Performance == PerformanceRating.Good)
                .Select(r => $"{r.Metric}: {r.Performance} (top {100 - r.Percentile:F0}%)")
                .ToList();
        }

        private List<string> IdentifyWeaknesses(List<BenchmarkResult> results)
        {
            return results
                .Where(r => r.Performance == PerformanceRating.BelowAverage)
                .Select(r => $"{r.Metric}: needs improvement ({r.Variance:+0.0;-0.0}% from median)")
                .ToList();
        }

        private double CalculateSimilarity(ProjectRecord project, ProjectCharacteristics characteristics)
        {
            var score = 0.0;
            var weights = 0.0;

            if (project.BuildingType.Equals(characteristics.BuildingType, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.3;
            }
            weights += 0.3;

            if (characteristics.GrossArea > 0)
            {
                var areaRatio = Math.Min(
                    project.Metrics.GetValueOrDefault("GrossArea", 0),
                    characteristics.GrossArea) /
                    Math.Max(project.Metrics.GetValueOrDefault("GrossArea", 1), characteristics.GrossArea);
                score += 0.25 * areaRatio;
            }
            weights += 0.25;

            if (characteristics.Floors > 0)
            {
                var floorRatio = Math.Min(project.Floors, characteristics.Floors) /
                    (double)Math.Max(project.Floors, characteristics.Floors);
                score += 0.15 * floorRatio;
            }
            weights += 0.15;

            if (project.Region.Equals(characteristics.Region, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.15;
            }
            weights += 0.15;

            if (project.ClimateZone.Equals(characteristics.ClimateZone, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.15;
            }
            weights += 0.15;

            return score / weights;
        }

        private Dictionary<string, double> ExtractKeyMetrics(ProjectRecord project)
        {
            var metrics = new Dictionary<string, double>();
            var keyMetricNames = new[] { "CostPerSqm", "EUI", "NetToGrossRatio", "SuccessScore" };

            foreach (var metricName in keyMetricNames)
            {
                if (project.Metrics?.ContainsKey(metricName) == true)
                {
                    metrics[metricName] = project.Metrics[metricName];
                }
            }

            return metrics;
        }

        private ProjectCharacteristics ExtractCharacteristics(ProjectRecord project)
        {
            return new ProjectCharacteristics
            {
                BuildingType = project.BuildingType,
                GrossArea = project.Metrics.GetValueOrDefault("GrossArea", 0),
                Floors = project.Floors,
                Region = project.Region,
                ClimateZone = project.ClimateZone
            };
        }

        private List<BestPractice> ExtractCommonPractices(List<ProjectRecord> topProjects)
        {
            var practices = new List<BestPractice>();

            // Check for common patterns
            var commonFeatures = topProjects
                .Where(p => p.Features != null)
                .SelectMany(p => p.Features)
                .GroupBy(f => f)
                .Where(g => g.Count() >= topProjects.Count / 2)
                .Select(g => g.Key);

            foreach (var feature in commonFeatures)
            {
                practices.Add(new BestPractice
                {
                    PracticeId = Guid.NewGuid().ToString(),
                    Name = feature,
                    Description = $"Common in {topProjects.Count(p => p.Features?.Contains(feature) == true)}/{topProjects.Count} top performers",
                    Category = "Feature",
                    Frequency = topProjects.Count(p => p.Features?.Contains(feature) == true) / (double)topProjects.Count
                });
            }

            return practices;
        }

        private Dictionary<string, double> CalculateTargetMetrics(List<ProjectRecord> topProjects)
        {
            var targets = new Dictionary<string, double>();
            var metricNames = new[] { "CostPerSqm", "EUI", "NetToGrossRatio", "ScheduleVariance", "CostVariance" };

            foreach (var metric in metricNames)
            {
                var values = topProjects
                    .Select(p => p.Metrics.GetValueOrDefault(metric, double.NaN))
                    .Where(v => !double.IsNaN(v))
                    .ToList();

                if (values.Any())
                {
                    targets[metric] = values.Average();
                }
            }

            return targets;
        }

        private MetricTrend CalculateMetricTrend(List<ProjectRecord> projects, string metric, string category, int years)
        {
            var trend = new MetricTrend
            {
                Metric = metric,
                Category = category,
                Period = $"Last {years} years"
            };

            var dataPoints = projects
                .Where(p => p.Metrics?.ContainsKey(metric) == true)
                .Select(p => new TrendDataPoint
                {
                    Date = p.CompletionDate,
                    Value = p.Metrics[metric]
                })
                .OrderBy(d => d.Date)
                .ToList();

            trend.DataPoints = dataPoints;

            if (dataPoints.Count >= 2)
            {
                // Simple linear trend
                var firstHalf = dataPoints.Take(dataPoints.Count / 2).Average(d => d.Value);
                var secondHalf = dataPoints.Skip(dataPoints.Count / 2).Average(d => d.Value);

                trend.TrendDirection = secondHalf > firstHalf * 1.05 ? TrendDirection.Increasing :
                                       secondHalf < firstHalf * 0.95 ? TrendDirection.Decreasing :
                                       TrendDirection.Stable;

                trend.AnnualChangeRate = (secondHalf - firstHalf) / firstHalf / (years / 2.0) * 100;
            }

            return trend;
        }

        private List<string> IdentifyTeamStrengths(List<ProjectRecord> projects)
        {
            var strengths = new List<string>();

            var avgSuccess = projects.Average(p => p.SuccessScore);
            if (avgSuccess > 80) strengths.Add("High project success rate");

            var onBudgetRate = projects.Count(p => p.Metrics.GetValueOrDefault("CostVariance", 0) <= 0) / (double)projects.Count;
            if (onBudgetRate > 0.7) strengths.Add("Strong budget management");

            return strengths;
        }

        private List<string> IdentifyTeamWeaknesses(List<ProjectRecord> projects)
        {
            var weaknesses = new List<string>();

            var avgScheduleVariance = projects.Average(p => p.Metrics.GetValueOrDefault("ScheduleVariance", 0));
            if (avgScheduleVariance > 10) weaknesses.Add("Schedule adherence needs improvement");

            return weaknesses;
        }

        private double CalculateVendorCostCompetitiveness(string vendorId, List<ProjectRecord> projects)
        {
            return 0.75; // Placeholder
        }

        private Dictionary<string, double> CalculatePortfolioKPIs(List<ProjectRecord> projects)
        {
            return new Dictionary<string, double>
            {
                ["AverageSuccessScore"] = projects.Any() ? projects.Average(p => p.SuccessScore) : 0,
                ["OnBudgetRate"] = projects.Any() ?
                    projects.Count(p => p.Metrics.GetValueOrDefault("CostVariance", 0) <= 0) / (double)projects.Count : 0,
                ["OnTimeRate"] = projects.Any() ?
                    projects.Count(p => p.Metrics.GetValueOrDefault("ScheduleVariance", 0) <= 0) / (double)projects.Count : 0,
                ["AverageEUI"] = projects.Any() ?
                    projects.Average(p => p.Metrics.GetValueOrDefault("EUI", 0)) : 0
            };
        }

        private void UpdateBenchmarksFromProject(ProjectRecord project)
        {
            // Update benchmarks with new project data
        }

        private static double Percentile(List<double> values, int percentile)
        {
            if (!values.Any()) return 0;
            var index = (int)Math.Ceiling(percentile / 100.0 * values.Count) - 1;
            return values[Math.Max(0, Math.Min(values.Count - 1, index))];
        }

        #endregion
    }

    #region Supporting Classes

    public class ProjectRecord
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string BuildingType { get; set; }
        public string Region { get; set; }
        public string ClimateZone { get; set; }
        public int Floors { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime CompletionDate { get; set; }
        public double SuccessScore { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
        public List<string> Features { get; set; } = new List<string>();
        public List<string> LessonsLearned { get; set; } = new List<string>();
        public List<string> TeamIds { get; set; } = new List<string>();
        public List<string> VendorIds { get; set; } = new List<string>();
        public Dictionary<string, double> VendorRatings { get; set; } = new Dictionary<string, double>();
    }

    public class PortfolioBenchmark
    {
        public string BenchmarkId { get; set; }
        public string Category { get; set; }
        public string BuildingType { get; set; }
        public string Metric { get; set; }
        public double MedianValue { get; set; }
        public double LowerQuartile { get; set; }
        public double UpperQuartile { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string Unit { get; set; }
        public string Region { get; set; }
        public int SampleSize { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class BenchmarkComparison
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public DateTime ComparisonDate { get; set; }
        public List<BenchmarkResult> Results { get; set; } = new List<BenchmarkResult>();
        public double OverallScore { get; set; }
        public List<string> Strengths { get; set; } = new List<string>();
        public List<string> Weaknesses { get; set; } = new List<string>();
    }

    public class BenchmarkResult
    {
        public string BenchmarkId { get; set; }
        public string Metric { get; set; }
        public string Category { get; set; }
        public double ProjectValue { get; set; }
        public double BenchmarkMedian { get; set; }
        public double BenchmarkLower { get; set; }
        public double BenchmarkUpper { get; set; }
        public double Percentile { get; set; }
        public double Variance { get; set; }
        public string Unit { get; set; }
        public PerformanceRating Performance { get; set; }
    }

    public class BenchmarkCriteria
    {
        public string Region { get; set; }
        public DateTime? MinCompletionDate { get; set; }
        public double? MinSuccessScore { get; set; }
    }

    public class ProjectCharacteristics
    {
        public string BuildingType { get; set; }
        public double GrossArea { get; set; }
        public int Floors { get; set; }
        public string Region { get; set; }
        public string ClimateZone { get; set; }
        public Dictionary<string, object> AdditionalFeatures { get; set; } = new Dictionary<string, object>();
    }

    public class SimilarProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public double Similarity { get; set; }
        public Dictionary<string, double> KeyMetrics { get; set; } = new Dictionary<string, double>();
        public List<string> LessonsLearned { get; set; } = new List<string>();
    }

    public class PatternMiningOptions
    {
        public double MinSuccessScore { get; set; } = 75;
        public int MinSupport { get; set; } = 3;
        public double MinConfidence { get; set; } = 0.7;
    }

    public class PatternMiningResult
    {
        public int ProjectsAnalyzed { get; set; }
        public List<DiscoveredPattern> Patterns { get; set; } = new List<DiscoveredPattern>();
    }

    public class DiscoveredPattern
    {
        public string PatternId { get; set; }
        public string Description { get; set; }
        public double Support { get; set; }
        public double Confidence { get; set; }
        public List<string> Conditions { get; set; } = new List<string>();
        public string Outcome { get; set; }
    }

    public class KnowledgeExtractionResult
    {
        public string ProjectId { get; set; }
        public List<ExtractedKnowledge> Knowledge { get; set; } = new List<ExtractedKnowledge>();
    }

    public class ExtractedKnowledge
    {
        public string KnowledgeId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public double Confidence { get; set; }
    }

    public class BestPracticesReport
    {
        public string BuildingType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<BestPractice> Practices { get; set; } = new List<BestPractice>();
        public Dictionary<string, double> TargetMetrics { get; set; } = new Dictionary<string, double>();
        public List<string> LessonsLearned { get; set; } = new List<string>();
    }

    public class BestPractice
    {
        public string PracticeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public double Frequency { get; set; }
    }

    public class TrendAnalysisOptions
    {
        public int YearsToAnalyze { get; set; } = 5;
        public List<string> MetricsToAnalyze { get; set; } = new List<string>();
    }

    public class TrendAnalysisResult
    {
        public List<MetricTrend> Trends { get; set; } = new List<MetricTrend>();
    }

    public class MetricTrend
    {
        public string Metric { get; set; }
        public string Category { get; set; }
        public string Period { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public double AnnualChangeRate { get; set; }
        public List<TrendDataPoint> DataPoints { get; set; } = new List<TrendDataPoint>();
    }

    public class TrendDataPoint
    {
        public DateTime Date { get; set; }
        public double Value { get; set; }
    }

    public class CostPrediction
    {
        public double PredictedCostPerSqm { get; set; }
        public double PredictedTotalCost { get; set; }
        public double ConfidenceInterval { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
        public List<string> KeyFactors { get; set; } = new List<string>();
    }

    public class SchedulePrediction
    {
        public int PredictedDurationDays { get; set; }
        public double ConfidenceInterval { get; set; }
        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
        public List<string> KeyFactors { get; set; } = new List<string>();
    }

    public class RiskPrediction
    {
        public double OverallRiskScore { get; set; }
        public List<PredictedRisk> Risks { get; set; } = new List<PredictedRisk>();
    }

    public class PredictedRisk
    {
        public string RiskCategory { get; set; }
        public double Probability { get; set; }
        public double Impact { get; set; }
        public string Mitigation { get; set; }
    }

    public class ProjectProgress
    {
        public double PercentComplete { get; set; }
        public double CostVariance { get; set; }
        public double ScheduleVariance { get; set; }
    }

    public class EarlyWarning
    {
        public WarningType WarningType { get; set; }
        public WarningSeverity Severity { get; set; }
        public string Message { get; set; }
        public int BasedOnProjects { get; set; }
        public string Recommendation { get; set; }
    }

    public class TeamPerformanceReport
    {
        public string TeamId { get; set; }
        public int ProjectCount { get; set; }
        public double AverageSuccessScore { get; set; }
        public double AverageCostVariance { get; set; }
        public double AverageScheduleVariance { get; set; }
        public Dictionary<string, int> BuildingTypeExperience { get; set; } = new Dictionary<string, int>();
        public List<string> StrengthAreas { get; set; } = new List<string>();
        public List<string> ImprovementAreas { get; set; } = new List<string>();
    }

    public class VendorPerformanceReport
    {
        public string VendorId { get; set; }
        public int ProjectCount { get; set; }
        public double QualityRating { get; set; }
        public double OnTimeDeliveryRate { get; set; }
        public double DefectRate { get; set; }
        public double CostCompetitiveness { get; set; }
    }

    public class PortfolioDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalProjects { get; set; }
        public double TotalArea { get; set; }
        public double TotalValue { get; set; }
        public double AverageSuccessScore { get; set; }
        public Dictionary<string, int> ProjectsByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ProjectsByRegion { get; set; } = new Dictionary<string, int>();
        public Dictionary<ProjectStatus, int> ProjectsByStatus { get; set; } = new Dictionary<ProjectStatus, int>();
        public List<ProjectRecord> RecentProjects { get; set; } = new List<ProjectRecord>();
        public List<ProjectRecord> TopPerformers { get; set; } = new List<ProjectRecord>();
        public Dictionary<string, double> KPIs { get; set; } = new Dictionary<string, double>();
    }

    public class PatternMiner
    {
        public async Task<PatternMiningResult> MineAsync(List<ProjectRecord> projects, PatternMiningOptions options, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new PatternMiningResult { ProjectsAnalyzed = projects.Count };
        }
    }

    public class TrendAnalyzer
    {
        public TrendAnalysisResult Analyze(List<ProjectRecord> projects, TrendAnalysisOptions options)
        {
            return new TrendAnalysisResult();
        }
    }

    public class PredictiveModeler
    {
        public CostPrediction PredictCost(ProjectCharacteristics characteristics, List<ProjectRecord> historicalData)
        {
            var similarProjects = historicalData
                .Where(p => p.BuildingType.Equals(characteristics.BuildingType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var avgCostPerSqm = similarProjects.Any() ?
                similarProjects.Average(p => p.Metrics.GetValueOrDefault("CostPerSqm", 1500)) : 1500;

            return new CostPrediction
            {
                PredictedCostPerSqm = avgCostPerSqm,
                PredictedTotalCost = avgCostPerSqm * characteristics.GrossArea,
                ConfidenceInterval = 0.15,
                LowerBound = avgCostPerSqm * characteristics.GrossArea * 0.85,
                UpperBound = avgCostPerSqm * characteristics.GrossArea * 1.15
            };
        }

        public SchedulePrediction PredictSchedule(ProjectCharacteristics characteristics, List<ProjectRecord> historicalData)
        {
            return new SchedulePrediction
            {
                PredictedDurationDays = (int)(characteristics.Floors * 45),
                ConfidenceInterval = 0.2,
                LowerBound = (int)(characteristics.Floors * 35),
                UpperBound = (int)(characteristics.Floors * 55)
            };
        }

        public RiskPrediction PredictRisks(ProjectCharacteristics characteristics, List<ProjectRecord> historicalData)
        {
            return new RiskPrediction
            {
                OverallRiskScore = 0.35,
                Risks = new List<PredictedRisk>
                {
                    new PredictedRisk { RiskCategory = "Cost Overrun", Probability = 0.3, Impact = 0.4 },
                    new PredictedRisk { RiskCategory = "Schedule Delay", Probability = 0.4, Impact = 0.3 }
                }
            };
        }
    }

    public class KnowledgeExtractor
    {
        public async Task<KnowledgeExtractionResult> ExtractAsync(ProjectRecord project, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new KnowledgeExtractionResult { ProjectId = project.ProjectId };
        }
    }

    public enum ProjectStatus
    {
        Planning,
        Design,
        Construction,
        Completed,
        OnHold,
        Cancelled
    }

    public enum PerformanceRating
    {
        Excellent,
        Good,
        Average,
        BelowAverage
    }

    public enum TrendDirection
    {
        Increasing,
        Stable,
        Decreasing
    }

    public enum WarningType
    {
        CostOverrun,
        ScheduleDelay,
        QualityIssue,
        ResourceConstraint
    }

    public enum WarningSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
