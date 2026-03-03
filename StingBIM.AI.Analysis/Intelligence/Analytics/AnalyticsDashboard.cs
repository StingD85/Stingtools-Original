// ============================================================================
// StingBIM AI - Analytics Dashboard
// Project health metrics, KPIs, predictive analytics, and portfolio overview
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.Analytics
{
    /// <summary>
    /// Comprehensive Analytics Dashboard providing project health metrics,
    /// KPIs, trend analysis, and portfolio-level insights.
    /// </summary>
    public sealed class AnalyticsDashboard
    {
        private static readonly Lazy<AnalyticsDashboard> _instance =
            new Lazy<AnalyticsDashboard>(() => new AnalyticsDashboard());
        public static AnalyticsDashboard Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, ProjectMetrics> _projectMetrics = new();
        private readonly List<MetricSnapshot> _metricHistory = new();
        private readonly Dictionary<string, KPIDefinition> _kpiDefinitions = new();
        private readonly Dictionary<string, Alert> _alerts = new();

        public event EventHandler<AnalyticsEventArgs> ThresholdExceeded;
        public event EventHandler<AnalyticsEventArgs> TrendDetected;

        private AnalyticsDashboard()
        {
            InitializeKPIDefinitions();
        }

        #region Initialization

        private void InitializeKPIDefinitions()
        {
            // Schedule KPIs
            _kpiDefinitions["SPI"] = new KPIDefinition
            {
                KPIId = "SPI",
                Name = "Schedule Performance Index",
                Category = KPICategory.Schedule,
                Description = "Earned Value / Planned Value",
                Unit = "ratio",
                TargetMin = 0.95,
                TargetMax = 1.05,
                WarningThreshold = 0.90,
                CriticalThreshold = 0.80,
                TrendDirection = TrendPreference.Higher
            };

            _kpiDefinitions["SCHEDULE_VARIANCE"] = new KPIDefinition
            {
                KPIId = "SCHEDULE_VARIANCE",
                Name = "Schedule Variance",
                Category = KPICategory.Schedule,
                Description = "Days ahead/behind schedule",
                Unit = "days",
                TargetMin = -5,
                TargetMax = 5,
                WarningThreshold = -10,
                CriticalThreshold = -20,
                TrendDirection = TrendPreference.Higher
            };

            // Cost KPIs
            _kpiDefinitions["CPI"] = new KPIDefinition
            {
                KPIId = "CPI",
                Name = "Cost Performance Index",
                Category = KPICategory.Cost,
                Description = "Earned Value / Actual Cost",
                Unit = "ratio",
                TargetMin = 0.95,
                TargetMax = 1.10,
                WarningThreshold = 0.90,
                CriticalThreshold = 0.80,
                TrendDirection = TrendPreference.Higher
            };

            _kpiDefinitions["COST_VARIANCE"] = new KPIDefinition
            {
                KPIId = "COST_VARIANCE",
                Name = "Cost Variance %",
                Category = KPICategory.Cost,
                Description = "Actual vs Budget variance percentage",
                Unit = "%",
                TargetMin = -5,
                TargetMax = 5,
                WarningThreshold = 10,
                CriticalThreshold = 15,
                TrendDirection = TrendPreference.Lower
            };

            // Quality KPIs
            _kpiDefinitions["CLASH_RATE"] = new KPIDefinition
            {
                KPIId = "CLASH_RATE",
                Name = "Clash Resolution Rate",
                Category = KPICategory.Quality,
                Description = "Clashes resolved / Clashes identified",
                Unit = "%",
                TargetMin = 80,
                TargetMax = 100,
                WarningThreshold = 70,
                CriticalThreshold = 50,
                TrendDirection = TrendPreference.Higher
            };

            _kpiDefinitions["RFI_RESPONSE"] = new KPIDefinition
            {
                KPIId = "RFI_RESPONSE",
                Name = "RFI Response Time",
                Category = KPICategory.Quality,
                Description = "Average days to respond to RFIs",
                Unit = "days",
                TargetMin = 0,
                TargetMax = 5,
                WarningThreshold = 7,
                CriticalThreshold = 14,
                TrendDirection = TrendPreference.Lower
            };

            _kpiDefinitions["MODEL_HEALTH"] = new KPIDefinition
            {
                KPIId = "MODEL_HEALTH",
                Name = "Model Health Score",
                Category = KPICategory.Quality,
                Description = "Overall BIM model quality score",
                Unit = "%",
                TargetMin = 85,
                TargetMax = 100,
                WarningThreshold = 75,
                CriticalThreshold = 60,
                TrendDirection = TrendPreference.Higher
            };

            // Safety KPIs
            _kpiDefinitions["SAFETY_INCIDENTS"] = new KPIDefinition
            {
                KPIId = "SAFETY_INCIDENTS",
                Name = "Safety Incident Rate",
                Category = KPICategory.Safety,
                Description = "Recordable incidents per 200,000 hours",
                Unit = "rate",
                TargetMin = 0,
                TargetMax = 2,
                WarningThreshold = 3,
                CriticalThreshold = 5,
                TrendDirection = TrendPreference.Lower
            };

            // Coordination KPIs
            _kpiDefinitions["COORDINATION_SCORE"] = new KPIDefinition
            {
                KPIId = "COORDINATION_SCORE",
                Name = "Coordination Score",
                Category = KPICategory.Coordination,
                Description = "Multi-discipline coordination effectiveness",
                Unit = "%",
                TargetMin = 80,
                TargetMax = 100,
                WarningThreshold = 70,
                CriticalThreshold = 50,
                TrendDirection = TrendPreference.Higher
            };

            // Change Management KPIs
            _kpiDefinitions["CHANGE_RATE"] = new KPIDefinition
            {
                KPIId = "CHANGE_RATE",
                Name = "Change Order Rate",
                Category = KPICategory.ChangeManagement,
                Description = "Change orders as % of contract value",
                Unit = "%",
                TargetMin = 0,
                TargetMax = 5,
                WarningThreshold = 8,
                CriticalThreshold = 12,
                TrendDirection = TrendPreference.Lower
            };
        }

        #endregion

        #region Metrics Collection

        /// <summary>
        /// Update project metrics
        /// </summary>
        public void UpdateProjectMetrics(string projectId, MetricsUpdate update)
        {
            lock (_lock)
            {
                if (!_projectMetrics.TryGetValue(projectId, out var metrics))
                {
                    metrics = new ProjectMetrics
                    {
                        ProjectId = projectId,
                        ProjectName = update.ProjectName,
                        StartDate = update.ProjectStartDate ?? DateTime.UtcNow,
                        KPIValues = new Dictionary<string, double>()
                    };
                    _projectMetrics[projectId] = metrics;
                }

                // Update values
                metrics.LastUpdated = DateTime.UtcNow;

                if (update.PlannedValue.HasValue) metrics.PlannedValue = update.PlannedValue.Value;
                if (update.EarnedValue.HasValue) metrics.EarnedValue = update.EarnedValue.Value;
                if (update.ActualCost.HasValue) metrics.ActualCost = update.ActualCost.Value;
                if (update.Budget.HasValue) metrics.Budget = update.Budget.Value;
                if (update.PercentComplete.HasValue) metrics.PercentComplete = update.PercentComplete.Value;
                if (update.ScheduledComplete.HasValue) metrics.ScheduledComplete = update.ScheduledComplete.Value;

                if (update.TotalClashes.HasValue) metrics.TotalClashes = update.TotalClashes.Value;
                if (update.ResolvedClashes.HasValue) metrics.ResolvedClashes = update.ResolvedClashes.Value;
                if (update.OpenRFIs.HasValue) metrics.OpenRFIs = update.OpenRFIs.Value;
                if (update.ClosedRFIs.HasValue) metrics.ClosedRFIs = update.ClosedRFIs.Value;
                if (update.OpenSubmittals.HasValue) metrics.OpenSubmittals = update.OpenSubmittals.Value;
                if (update.ModelHealthScore.HasValue) metrics.ModelHealthScore = update.ModelHealthScore.Value;
                if (update.ChangeOrderCount.HasValue) metrics.ChangeOrderCount = update.ChangeOrderCount.Value;
                if (update.ChangeOrderValue.HasValue) metrics.ChangeOrderValue = update.ChangeOrderValue.Value;
                if (update.SafetyIncidents.HasValue) metrics.SafetyIncidents = update.SafetyIncidents.Value;
                if (update.ManHoursWorked.HasValue) metrics.ManHoursWorked = update.ManHoursWorked.Value;

                // Calculate KPIs
                CalculateKPIs(metrics);

                // Take snapshot
                TakeMetricSnapshot(projectId, metrics);

                // Check thresholds
                CheckThresholds(projectId, metrics);
            }
        }

        private void CalculateKPIs(ProjectMetrics metrics)
        {
            // SPI (Schedule Performance Index)
            if (metrics.PlannedValue > 0)
            {
                metrics.KPIValues["SPI"] = (double)(metrics.EarnedValue / metrics.PlannedValue);
            }

            // CPI (Cost Performance Index)
            if (metrics.ActualCost > 0)
            {
                metrics.KPIValues["CPI"] = (double)(metrics.EarnedValue / metrics.ActualCost);
            }

            // Schedule Variance (in days)
            if (metrics.ScheduledComplete > 0 && metrics.PercentComplete > 0)
            {
                var expectedProgress = (DateTime.UtcNow - metrics.StartDate).TotalDays /
                    (metrics.PlannedEndDate - metrics.StartDate).TotalDays * 100;
                metrics.KPIValues["SCHEDULE_VARIANCE"] = (metrics.PercentComplete - expectedProgress) / 100 *
                    (metrics.PlannedEndDate - metrics.StartDate).TotalDays;
            }

            // Cost Variance %
            if (metrics.Budget > 0)
            {
                metrics.KPIValues["COST_VARIANCE"] = (double)((metrics.ActualCost - metrics.Budget) / metrics.Budget * 100);
            }

            // Clash Resolution Rate
            if (metrics.TotalClashes > 0)
            {
                metrics.KPIValues["CLASH_RATE"] = (double)metrics.ResolvedClashes / metrics.TotalClashes * 100;
            }

            // Model Health Score
            metrics.KPIValues["MODEL_HEALTH"] = metrics.ModelHealthScore;

            // Change Order Rate
            if (metrics.Budget > 0)
            {
                metrics.KPIValues["CHANGE_RATE"] = (double)metrics.ChangeOrderValue / (double)metrics.Budget * 100;
            }

            // Safety Incident Rate (per 200,000 hours)
            if (metrics.ManHoursWorked > 0)
            {
                metrics.KPIValues["SAFETY_INCIDENTS"] = (double)metrics.SafetyIncidents / metrics.ManHoursWorked * 200000;
            }

            // Coordination Score (composite)
            var coordScore = 100.0;
            if (metrics.TotalClashes > 100) coordScore -= 20;
            else if (metrics.TotalClashes > 50) coordScore -= 10;

            if (metrics.OpenRFIs > 20) coordScore -= 15;
            else if (metrics.OpenRFIs > 10) coordScore -= 5;

            metrics.KPIValues["COORDINATION_SCORE"] = Math.Max(0, coordScore);
        }

        private void TakeMetricSnapshot(string projectId, ProjectMetrics metrics)
        {
            _metricHistory.Add(new MetricSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                Timestamp = DateTime.UtcNow,
                KPIValues = new Dictionary<string, double>(metrics.KPIValues),
                PercentComplete = metrics.PercentComplete,
                EarnedValue = metrics.EarnedValue,
                ActualCost = metrics.ActualCost
            });

            // Keep last 365 days of history
            var cutoff = DateTime.UtcNow.AddDays(-365);
            _metricHistory.RemoveAll(s => s.Timestamp < cutoff);
        }

        private void CheckThresholds(string projectId, ProjectMetrics metrics)
        {
            foreach (var kpi in _kpiDefinitions.Values)
            {
                if (!metrics.KPIValues.TryGetValue(kpi.KPIId, out var value))
                    continue;

                var alertKey = $"{projectId}_{kpi.KPIId}";
                AlertSeverity? severity = null;

                if (kpi.TrendDirection == TrendPreference.Higher)
                {
                    if (value < kpi.CriticalThreshold) severity = AlertSeverity.Critical;
                    else if (value < kpi.WarningThreshold) severity = AlertSeverity.Warning;
                }
                else
                {
                    if (value > kpi.CriticalThreshold) severity = AlertSeverity.Critical;
                    else if (value > kpi.WarningThreshold) severity = AlertSeverity.Warning;
                }

                if (severity.HasValue)
                {
                    if (!_alerts.ContainsKey(alertKey))
                    {
                        var alert = new Alert
                        {
                            AlertId = alertKey,
                            ProjectId = projectId,
                            KPIId = kpi.KPIId,
                            Severity = severity.Value,
                            Message = $"{kpi.Name} is {(kpi.TrendDirection == TrendPreference.Higher ? "below" : "above")} threshold: {value:F2}",
                            Value = value,
                            Threshold = severity == AlertSeverity.Critical ? kpi.CriticalThreshold : kpi.WarningThreshold,
                            CreatedAt = DateTime.UtcNow
                        };

                        _alerts[alertKey] = alert;

                        ThresholdExceeded?.Invoke(this, new AnalyticsEventArgs
                        {
                            Type = AnalyticsEventType.ThresholdExceeded,
                            ProjectId = projectId,
                            Message = alert.Message
                        });
                    }
                }
                else
                {
                    _alerts.Remove(alertKey);
                }
            }
        }

        #endregion

        #region Dashboard Generation

        /// <summary>
        /// Generate project dashboard
        /// </summary>
        public ProjectDashboard GetProjectDashboard(string projectId)
        {
            lock (_lock)
            {
                if (!_projectMetrics.TryGetValue(projectId, out var metrics))
                    return null;

                var history = _metricHistory
                    .Where(s => s.ProjectId == projectId)
                    .OrderBy(s => s.Timestamp)
                    .ToList();

                return new ProjectDashboard
                {
                    GeneratedAt = DateTime.UtcNow,
                    ProjectId = projectId,
                    ProjectName = metrics.ProjectName,

                    // Overall Health
                    OverallHealth = CalculateOverallHealth(metrics),

                    // Schedule Section
                    ScheduleMetrics = new ScheduleMetrics
                    {
                        PercentComplete = metrics.PercentComplete,
                        ScheduledComplete = metrics.ScheduledComplete,
                        SPI = metrics.KPIValues.GetValueOrDefault("SPI", 0),
                        ScheduleVarianceDays = metrics.KPIValues.GetValueOrDefault("SCHEDULE_VARIANCE", 0),
                        PlannedEndDate = metrics.PlannedEndDate,
                        ForecastEndDate = ForecastEndDate(metrics),
                        Status = GetScheduleStatus(metrics)
                    },

                    // Cost Section
                    CostMetrics = new CostMetrics
                    {
                        Budget = metrics.Budget,
                        ActualCost = metrics.ActualCost,
                        EarnedValue = metrics.EarnedValue,
                        CPI = metrics.KPIValues.GetValueOrDefault("CPI", 0),
                        CostVariancePercent = metrics.KPIValues.GetValueOrDefault("COST_VARIANCE", 0),
                        EstimateAtCompletion = CalculateEAC(metrics),
                        VarianceAtCompletion = CalculateVAC(metrics),
                        Status = GetCostStatus(metrics)
                    },

                    // Quality Section
                    QualityMetrics = new QualityMetrics
                    {
                        ModelHealthScore = metrics.ModelHealthScore,
                        ClashResolutionRate = metrics.KPIValues.GetValueOrDefault("CLASH_RATE", 0),
                        TotalClashes = metrics.TotalClashes,
                        ResolvedClashes = metrics.ResolvedClashes,
                        OpenRFIs = metrics.OpenRFIs,
                        OpenSubmittals = metrics.OpenSubmittals,
                        Status = GetQualityStatus(metrics)
                    },

                    // Coordination Section
                    CoordinationMetrics = new CoordinationMetrics
                    {
                        CoordinationScore = metrics.KPIValues.GetValueOrDefault("COORDINATION_SCORE", 0),
                        ChangeOrderRate = metrics.KPIValues.GetValueOrDefault("CHANGE_RATE", 0),
                        ChangeOrderCount = metrics.ChangeOrderCount,
                        ChangeOrderValue = metrics.ChangeOrderValue,
                        Status = GetCoordinationStatus(metrics)
                    },

                    // Safety Section
                    SafetyMetrics = new SafetyMetrics
                    {
                        IncidentRate = metrics.KPIValues.GetValueOrDefault("SAFETY_INCIDENTS", 0),
                        TotalIncidents = metrics.SafetyIncidents,
                        ManHoursWorked = metrics.ManHoursWorked,
                        DaysSinceLastIncident = CalculateDaysSinceIncident(metrics),
                        Status = GetSafetyStatus(metrics)
                    },

                    // KPI Summary
                    KPISummary = metrics.KPIValues.Select(kvp => new KPISummaryItem
                    {
                        KPIId = kvp.Key,
                        Name = _kpiDefinitions.TryGetValue(kvp.Key, out var def) ? def.Name : kvp.Key,
                        Value = kvp.Value,
                        Unit = def?.Unit ?? "",
                        Status = GetKPIStatus(kvp.Key, kvp.Value)
                    }).ToList(),

                    // Trends
                    Trends = CalculateTrends(history),

                    // Active Alerts
                    ActiveAlerts = _alerts.Values
                        .Where(a => a.ProjectId == projectId)
                        .OrderByDescending(a => a.Severity)
                        .ToList()
                };
            }
        }

        private HealthStatus CalculateOverallHealth(ProjectMetrics metrics)
        {
            var scores = new List<double>();

            if (metrics.KPIValues.TryGetValue("SPI", out var spi))
                scores.Add(spi >= 0.95 ? 100 : spi >= 0.90 ? 75 : spi >= 0.80 ? 50 : 25);

            if (metrics.KPIValues.TryGetValue("CPI", out var cpi))
                scores.Add(cpi >= 0.95 ? 100 : cpi >= 0.90 ? 75 : cpi >= 0.80 ? 50 : 25);

            scores.Add(metrics.ModelHealthScore);

            if (metrics.KPIValues.TryGetValue("COORDINATION_SCORE", out var coord))
                scores.Add(coord);

            var avgScore = scores.Any() ? scores.Average() : 50;

            return new HealthStatus
            {
                Score = avgScore,
                Status = avgScore >= 80 ? StatusLevel.Good :
                    avgScore >= 60 ? StatusLevel.Warning : StatusLevel.Critical,
                Summary = avgScore >= 80 ? "Project is performing well" :
                    avgScore >= 60 ? "Some areas need attention" : "Critical issues require immediate action"
            };
        }

        private DateTime ForecastEndDate(ProjectMetrics metrics)
        {
            var spi = metrics.KPIValues.GetValueOrDefault("SPI", 1.0);
            if (spi <= 0) spi = 0.5;

            var remainingDuration = (metrics.PlannedEndDate - DateTime.UtcNow).TotalDays;
            var adjustedDuration = remainingDuration / spi;

            return DateTime.UtcNow.AddDays(adjustedDuration);
        }

        private decimal CalculateEAC(ProjectMetrics metrics)
        {
            var cpi = metrics.KPIValues.GetValueOrDefault("CPI", 1.0);
            if (cpi <= 0) cpi = 0.5;
            return metrics.Budget / (decimal)cpi;
        }

        private decimal CalculateVAC(ProjectMetrics metrics)
        {
            return metrics.Budget - CalculateEAC(metrics);
        }

        private int CalculateDaysSinceIncident(ProjectMetrics metrics)
        {
            // Would need last incident date tracked
            return metrics.SafetyIncidents == 0 ? 999 : 30;
        }

        private StatusLevel GetScheduleStatus(ProjectMetrics metrics)
        {
            var spi = metrics.KPIValues.GetValueOrDefault("SPI", 1.0);
            return spi >= 0.95 ? StatusLevel.Good :
                spi >= 0.85 ? StatusLevel.Warning : StatusLevel.Critical;
        }

        private StatusLevel GetCostStatus(ProjectMetrics metrics)
        {
            var cpi = metrics.KPIValues.GetValueOrDefault("CPI", 1.0);
            return cpi >= 0.95 ? StatusLevel.Good :
                cpi >= 0.85 ? StatusLevel.Warning : StatusLevel.Critical;
        }

        private StatusLevel GetQualityStatus(ProjectMetrics metrics)
        {
            return metrics.ModelHealthScore >= 85 ? StatusLevel.Good :
                metrics.ModelHealthScore >= 70 ? StatusLevel.Warning : StatusLevel.Critical;
        }

        private StatusLevel GetCoordinationStatus(ProjectMetrics metrics)
        {
            var score = metrics.KPIValues.GetValueOrDefault("COORDINATION_SCORE", 100);
            return score >= 80 ? StatusLevel.Good :
                score >= 60 ? StatusLevel.Warning : StatusLevel.Critical;
        }

        private StatusLevel GetSafetyStatus(ProjectMetrics metrics)
        {
            var rate = metrics.KPIValues.GetValueOrDefault("SAFETY_INCIDENTS", 0);
            return rate <= 2 ? StatusLevel.Good :
                rate <= 4 ? StatusLevel.Warning : StatusLevel.Critical;
        }

        private StatusLevel GetKPIStatus(string kpiId, double value)
        {
            if (!_kpiDefinitions.TryGetValue(kpiId, out var def))
                return StatusLevel.Good;

            if (def.TrendDirection == TrendPreference.Higher)
            {
                if (value >= def.TargetMin) return StatusLevel.Good;
                if (value >= def.WarningThreshold) return StatusLevel.Warning;
                return StatusLevel.Critical;
            }
            else
            {
                if (value <= def.TargetMax) return StatusLevel.Good;
                if (value <= def.WarningThreshold) return StatusLevel.Warning;
                return StatusLevel.Critical;
            }
        }

        private List<TrendAnalysis> CalculateTrends(List<MetricSnapshot> history)
        {
            var trends = new List<TrendAnalysis>();

            if (history.Count < 2)
                return trends;

            var recent = history.TakeLast(7).ToList();
            var older = history.Take(history.Count - 7).TakeLast(7).ToList();

            foreach (var kpiId in _kpiDefinitions.Keys)
            {
                var recentAvg = recent.Average(s => s.KPIValues.GetValueOrDefault(kpiId, 0));
                var olderAvg = older.Any() ? older.Average(s => s.KPIValues.GetValueOrDefault(kpiId, 0)) : recentAvg;

                var change = olderAvg != 0 ? (recentAvg - olderAvg) / Math.Abs(olderAvg) * 100 : 0;

                trends.Add(new TrendAnalysis
                {
                    KPIId = kpiId,
                    Name = _kpiDefinitions[kpiId].Name,
                    CurrentValue = recentAvg,
                    PreviousValue = olderAvg,
                    ChangePercent = change,
                    Direction = change > 5 ? TrendDirection.Improving :
                        change < -5 ? TrendDirection.Declining : TrendDirection.Stable
                });
            }

            return trends;
        }

        #endregion

        #region Portfolio View

        /// <summary>
        /// Get portfolio overview across all projects
        /// </summary>
        public PortfolioOverview GetPortfolioOverview()
        {
            lock (_lock)
            {
                var projects = _projectMetrics.Values.ToList();

                return new PortfolioOverview
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalProjects = projects.Count,
                    TotalBudget = projects.Sum(p => p.Budget),
                    TotalActualCost = projects.Sum(p => p.ActualCost),
                    AveragePercentComplete = projects.Any() ? projects.Average(p => p.PercentComplete) : 0,

                    ProjectsByHealth = new Dictionary<StatusLevel, int>
                    {
                        { StatusLevel.Good, projects.Count(p => CalculateOverallHealth(p).Status == StatusLevel.Good) },
                        { StatusLevel.Warning, projects.Count(p => CalculateOverallHealth(p).Status == StatusLevel.Warning) },
                        { StatusLevel.Critical, projects.Count(p => CalculateOverallHealth(p).Status == StatusLevel.Critical) }
                    },

                    ProjectSummaries = projects.Select(p => new ProjectSummary
                    {
                        ProjectId = p.ProjectId,
                        ProjectName = p.ProjectName,
                        PercentComplete = p.PercentComplete,
                        Budget = p.Budget,
                        ActualCost = p.ActualCost,
                        SPI = p.KPIValues.GetValueOrDefault("SPI", 0),
                        CPI = p.KPIValues.GetValueOrDefault("CPI", 0),
                        HealthScore = CalculateOverallHealth(p).Score,
                        HealthStatus = CalculateOverallHealth(p).Status,
                        ActiveAlerts = _alerts.Values.Count(a => a.ProjectId == p.ProjectId)
                    }).OrderByDescending(p => p.HealthScore).ToList(),

                    TotalActiveAlerts = _alerts.Count,
                    CriticalAlerts = _alerts.Values.Count(a => a.Severity == AlertSeverity.Critical),

                    PortfolioKPIs = new PortfolioKPIs
                    {
                        AverageSPI = projects.Any() ? projects.Average(p => p.KPIValues.GetValueOrDefault("SPI", 1)) : 1,
                        AverageCPI = projects.Any() ? projects.Average(p => p.KPIValues.GetValueOrDefault("CPI", 1)) : 1,
                        AverageModelHealth = projects.Any() ? projects.Average(p => p.ModelHealthScore) : 0,
                        TotalClashes = projects.Sum(p => p.TotalClashes),
                        TotalChangeOrders = projects.Sum(p => p.ChangeOrderCount)
                    }
                };
            }
        }

        #endregion

        #region Reports

        /// <summary>
        /// Generate executive report
        /// </summary>
        public ExecutiveReport GenerateExecutiveReport(string projectId, DateTime fromDate, DateTime toDate)
        {
            lock (_lock)
            {
                if (!_projectMetrics.TryGetValue(projectId, out var metrics))
                    return null;

                var historyInPeriod = _metricHistory
                    .Where(s => s.ProjectId == projectId && s.Timestamp >= fromDate && s.Timestamp <= toDate)
                    .OrderBy(s => s.Timestamp)
                    .ToList();

                return new ExecutiveReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    ProjectId = projectId,
                    ProjectName = metrics.ProjectName,
                    ReportPeriod = new DateRange { Start = fromDate, End = toDate },

                    ExecutiveSummary = GenerateExecutiveSummary(metrics, historyInPeriod),

                    ScheduleSection = new ReportSection
                    {
                        Title = "Schedule Performance",
                        Summary = $"SPI: {metrics.KPIValues.GetValueOrDefault("SPI", 0):F2}",
                        Status = GetScheduleStatus(metrics),
                        KeyPoints = new List<string>
                        {
                            $"Project is {metrics.PercentComplete:F1}% complete",
                            $"Scheduled to be {metrics.ScheduledComplete:F1}% complete",
                            $"Forecast completion: {ForecastEndDate(metrics):d}"
                        }
                    },

                    CostSection = new ReportSection
                    {
                        Title = "Cost Performance",
                        Summary = $"CPI: {metrics.KPIValues.GetValueOrDefault("CPI", 0):F2}",
                        Status = GetCostStatus(metrics),
                        KeyPoints = new List<string>
                        {
                            $"Budget: {metrics.Budget:C0}",
                            $"Actual Cost: {metrics.ActualCost:C0}",
                            $"Estimate at Completion: {CalculateEAC(metrics):C0}"
                        }
                    },

                    QualitySection = new ReportSection
                    {
                        Title = "Quality & Coordination",
                        Summary = $"Model Health: {metrics.ModelHealthScore:F0}%",
                        Status = GetQualityStatus(metrics),
                        KeyPoints = new List<string>
                        {
                            $"Clash Resolution Rate: {metrics.KPIValues.GetValueOrDefault("CLASH_RATE", 0):F0}%",
                            $"Open RFIs: {metrics.OpenRFIs}",
                            $"Open Submittals: {metrics.OpenSubmittals}"
                        }
                    },

                    Recommendations = GenerateRecommendations(metrics),

                    KPIHistory = historyInPeriod.Select(s => new KPIHistoryPoint
                    {
                        Date = s.Timestamp,
                        Values = s.KPIValues
                    }).ToList()
                };
            }
        }

        private string GenerateExecutiveSummary(ProjectMetrics metrics, List<MetricSnapshot> history)
        {
            var health = CalculateOverallHealth(metrics);
            var trend = history.Count >= 2 ?
                (history.Last().KPIValues.GetValueOrDefault("SPI", 1) >
                 history.First().KPIValues.GetValueOrDefault("SPI", 1) ? "improving" : "declining") :
                "stable";

            return $"Project {metrics.ProjectName} is currently at {metrics.PercentComplete:F1}% complete " +
                   $"with an overall health score of {health.Score:F0}%. " +
                   $"Performance trend over the reporting period is {trend}. " +
                   $"Key attention areas: {(health.Status != StatusLevel.Good ? "Cost and schedule require monitoring. " : "All metrics within acceptable range.")}";
        }

        private List<string> GenerateRecommendations(ProjectMetrics metrics)
        {
            var recommendations = new List<string>();

            if (metrics.KPIValues.GetValueOrDefault("SPI", 1) < 0.9)
                recommendations.Add("Schedule recovery plan needed - consider fast-tracking critical path activities");

            if (metrics.KPIValues.GetValueOrDefault("CPI", 1) < 0.9)
                recommendations.Add("Cost control measures required - review change order process and value engineering opportunities");

            if (metrics.ModelHealthScore < 75)
                recommendations.Add("Model quality improvement needed - schedule model audit and address critical warnings");

            if (metrics.TotalClashes - metrics.ResolvedClashes > 50)
                recommendations.Add("Prioritize clash resolution - schedule focused coordination sessions");

            if (recommendations.Count == 0)
                recommendations.Add("Continue current performance monitoring and maintain quality standards");

            return recommendations;
        }

        #endregion
    }

    #region Data Models

    public class ProjectMetrics
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime LastUpdated { get; set; }

        // Earned Value
        public decimal PlannedValue { get; set; }
        public decimal EarnedValue { get; set; }
        public decimal ActualCost { get; set; }
        public decimal Budget { get; set; }

        // Progress
        public double PercentComplete { get; set; }
        public double ScheduledComplete { get; set; }

        // Quality
        public int TotalClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public int OpenRFIs { get; set; }
        public int ClosedRFIs { get; set; }
        public int OpenSubmittals { get; set; }
        public double ModelHealthScore { get; set; }

        // Change Management
        public int ChangeOrderCount { get; set; }
        public decimal ChangeOrderValue { get; set; }

        // Safety
        public int SafetyIncidents { get; set; }
        public double ManHoursWorked { get; set; }

        // KPI Values
        public Dictionary<string, double> KPIValues { get; set; }
    }

    public class MetricsUpdate
    {
        public string ProjectName { get; set; }
        public DateTime? ProjectStartDate { get; set; }
        public decimal? PlannedValue { get; set; }
        public decimal? EarnedValue { get; set; }
        public decimal? ActualCost { get; set; }
        public decimal? Budget { get; set; }
        public double? PercentComplete { get; set; }
        public double? ScheduledComplete { get; set; }
        public int? TotalClashes { get; set; }
        public int? ResolvedClashes { get; set; }
        public int? OpenRFIs { get; set; }
        public int? ClosedRFIs { get; set; }
        public int? OpenSubmittals { get; set; }
        public double? ModelHealthScore { get; set; }
        public int? ChangeOrderCount { get; set; }
        public decimal? ChangeOrderValue { get; set; }
        public int? SafetyIncidents { get; set; }
        public double? ManHoursWorked { get; set; }
    }

    public class MetricSnapshot
    {
        public string SnapshotId { get; set; }
        public string ProjectId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> KPIValues { get; set; }
        public double PercentComplete { get; set; }
        public decimal EarnedValue { get; set; }
        public decimal ActualCost { get; set; }
    }

    public class KPIDefinition
    {
        public string KPIId { get; set; }
        public string Name { get; set; }
        public KPICategory Category { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public double TargetMin { get; set; }
        public double TargetMax { get; set; }
        public double WarningThreshold { get; set; }
        public double CriticalThreshold { get; set; }
        public TrendPreference TrendDirection { get; set; }
    }

    public class Alert
    {
        public string AlertId { get; set; }
        public string ProjectId { get; set; }
        public string KPIId { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProjectDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public HealthStatus OverallHealth { get; set; }
        public ScheduleMetrics ScheduleMetrics { get; set; }
        public CostMetrics CostMetrics { get; set; }
        public QualityMetrics QualityMetrics { get; set; }
        public CoordinationMetrics CoordinationMetrics { get; set; }
        public SafetyMetrics SafetyMetrics { get; set; }
        public List<KPISummaryItem> KPISummary { get; set; }
        public List<TrendAnalysis> Trends { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
    }

    public class HealthStatus
    {
        public double Score { get; set; }
        public StatusLevel Status { get; set; }
        public string Summary { get; set; }
    }

    public class ScheduleMetrics
    {
        public double PercentComplete { get; set; }
        public double ScheduledComplete { get; set; }
        public double SPI { get; set; }
        public double ScheduleVarianceDays { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime ForecastEndDate { get; set; }
        public StatusLevel Status { get; set; }
    }

    public class CostMetrics
    {
        public decimal Budget { get; set; }
        public decimal ActualCost { get; set; }
        public decimal EarnedValue { get; set; }
        public double CPI { get; set; }
        public double CostVariancePercent { get; set; }
        public decimal EstimateAtCompletion { get; set; }
        public decimal VarianceAtCompletion { get; set; }
        public StatusLevel Status { get; set; }
    }

    public class QualityMetrics
    {
        public double ModelHealthScore { get; set; }
        public double ClashResolutionRate { get; set; }
        public int TotalClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public int OpenRFIs { get; set; }
        public int OpenSubmittals { get; set; }
        public StatusLevel Status { get; set; }
    }

    public class CoordinationMetrics
    {
        public double CoordinationScore { get; set; }
        public double ChangeOrderRate { get; set; }
        public int ChangeOrderCount { get; set; }
        public decimal ChangeOrderValue { get; set; }
        public StatusLevel Status { get; set; }
    }

    public class SafetyMetrics
    {
        public double IncidentRate { get; set; }
        public int TotalIncidents { get; set; }
        public double ManHoursWorked { get; set; }
        public int DaysSinceLastIncident { get; set; }
        public StatusLevel Status { get; set; }
    }

    public class KPISummaryItem
    {
        public string KPIId { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public StatusLevel Status { get; set; }
    }

    public class TrendAnalysis
    {
        public string KPIId { get; set; }
        public string Name { get; set; }
        public double CurrentValue { get; set; }
        public double PreviousValue { get; set; }
        public double ChangePercent { get; set; }
        public TrendDirection Direction { get; set; }
    }

    public class PortfolioOverview
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalProjects { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalActualCost { get; set; }
        public double AveragePercentComplete { get; set; }
        public Dictionary<StatusLevel, int> ProjectsByHealth { get; set; }
        public List<ProjectSummary> ProjectSummaries { get; set; }
        public int TotalActiveAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public PortfolioKPIs PortfolioKPIs { get; set; }
    }

    public class ProjectSummary
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public double PercentComplete { get; set; }
        public decimal Budget { get; set; }
        public decimal ActualCost { get; set; }
        public double SPI { get; set; }
        public double CPI { get; set; }
        public double HealthScore { get; set; }
        public StatusLevel HealthStatus { get; set; }
        public int ActiveAlerts { get; set; }
    }

    public class PortfolioKPIs
    {
        public double AverageSPI { get; set; }
        public double AverageCPI { get; set; }
        public double AverageModelHealth { get; set; }
        public int TotalClashes { get; set; }
        public int TotalChangeOrders { get; set; }
    }

    public class ExecutiveReport
    {
        public DateTime GeneratedAt { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateRange ReportPeriod { get; set; }
        public string ExecutiveSummary { get; set; }
        public ReportSection ScheduleSection { get; set; }
        public ReportSection CostSection { get; set; }
        public ReportSection QualitySection { get; set; }
        public List<string> Recommendations { get; set; }
        public List<KPIHistoryPoint> KPIHistory { get; set; }
    }

    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class ReportSection
    {
        public string Title { get; set; }
        public string Summary { get; set; }
        public StatusLevel Status { get; set; }
        public List<string> KeyPoints { get; set; }
    }

    public class KPIHistoryPoint
    {
        public DateTime Date { get; set; }
        public Dictionary<string, double> Values { get; set; }
    }

    public class AnalyticsEventArgs : EventArgs
    {
        public AnalyticsEventType Type { get; set; }
        public string ProjectId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum KPICategory { Schedule, Cost, Quality, Safety, Coordination, ChangeManagement }
    public enum TrendPreference { Higher, Lower }
    public enum AlertSeverity { Info, Warning, Critical }
    public enum StatusLevel { Good, Warning, Critical }
    public enum TrendDirection { Improving, Stable, Declining }
    public enum AnalyticsEventType { ThresholdExceeded, TrendDetected, ReportGenerated }

    #endregion
}
