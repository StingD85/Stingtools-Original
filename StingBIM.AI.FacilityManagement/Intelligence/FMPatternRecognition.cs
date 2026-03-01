// ============================================================================
// StingBIM AI - Facility Management Pattern Recognition
// Identifies maintenance patterns, seasonal trends, and recurring issues
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Pattern Models

    /// <summary>
    /// Recognized maintenance pattern
    /// </summary>
    public class MaintenancePattern
    {
        public string PatternId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public string PatternName { get; set; } = string.Empty;
        public PatternType Type { get; set; }
        public string Description { get; set; } = string.Empty;

        // Pattern Details
        public double Confidence { get; set; }
        public int OccurrenceCount { get; set; }
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }

        // Affected Scope
        public List<string> AffectedAssets { get; set; } = new();
        public List<string> AffectedSystems { get; set; } = new();
        public List<string> AffectedLocations { get; set; } = new();

        // Timing
        public PatternTiming Timing { get; set; }
        public string TimingDescription { get; set; } = string.Empty;

        // Impact
        public string ImpactDescription { get; set; } = string.Empty;
        public decimal TotalCostImpact { get; set; }
        public double TotalDowntimeHours { get; set; }

        // Actions
        public string RecommendedAction { get; set; } = string.Empty;
        public bool IsActionable { get; set; }
        public string PreventionStrategy { get; set; } = string.Empty;
    }

    public enum PatternType
    {
        Seasonal,           // Occurs in specific seasons
        Cyclic,            // Repeats at regular intervals
        Cascading,         // One failure leads to others
        Environmental,     // Related to environmental conditions
        Usage,             // Related to usage patterns
        AgeRelated,        // Related to equipment age
        Maintenance,       // Related to maintenance practices
        Vendor,            // Related to specific vendor/manufacturer
        Location,          // Specific to certain locations
        TimeOfDay,         // Occurs at specific times
        EventDriven        // Triggered by specific events
    }

    /// <summary>
    /// Pattern timing characteristics
    /// </summary>
    public class PatternTiming
    {
        public bool IsSeasonal { get; set; }
        public List<int> ActiveMonths { get; set; } = new(); // 1-12
        public List<DayOfWeek> ActiveDays { get; set; } = new();
        public int? RecurrenceIntervalDays { get; set; }
        public TimeSpan? ActiveStartTime { get; set; }
        public TimeSpan? ActiveEndTime { get; set; }
    }

    /// <summary>
    /// Seasonal trend analysis
    /// </summary>
    public class SeasonalTrend
    {
        public string TrendId { get; set; } = string.Empty;
        public string TrendName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // HVAC, Electrical, etc.

        // Monthly data
        public Dictionary<int, MonthlyTrendData> MonthlyData { get; set; } = new();

        // Analysis
        public int PeakMonth { get; set; }
        public int LowMonth { get; set; }
        public double SeasonalityIndex { get; set; } // How seasonal (0-1)
        public string TrendDescription { get; set; } = string.Empty;

        // Planning insights
        public List<string> PlanningRecommendations { get; set; } = new();
    }

    public class MonthlyTrendData
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public int WorkOrderCount { get; set; }
        public decimal AverageCost { get; set; }
        public double AverageResponseTime { get; set; }
        public double IndexVsAnnualAverage { get; set; } // 1.0 = average, >1 = above
    }

    /// <summary>
    /// Failure correlation
    /// </summary>
    public class FailureCorrelation
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string PrimaryFailure { get; set; } = string.Empty;
        public string PrimaryAssetType { get; set; } = string.Empty;
        public string SecondaryFailure { get; set; } = string.Empty;
        public string SecondaryAssetType { get; set; } = string.Empty;

        public double CorrelationStrength { get; set; } // 0-1
        public int CoOccurrences { get; set; }
        public TimeSpan TypicalTimeGap { get; set; }
        public string RelationshipType { get; set; } = string.Empty; // Causal, Coincidental, System-linked

        public string Description { get; set; } = string.Empty;
        public string PreventionStrategy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Work order trend analysis
    /// </summary>
    public class WorkOrderTrend
    {
        public string TrendPeriod { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Volume trends
        public int TotalWorkOrders { get; set; }
        public int PreventiveCount { get; set; }
        public int CorrectiveCount { get; set; }
        public int EmergencyCount { get; set; }
        public double PreventiveRatio { get; set; }

        // Cost trends
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerWorkOrder { get; set; }
        public decimal LaborCost { get; set; }
        public decimal PartsCost { get; set; }

        // Performance trends
        public double AverageCompletionTime { get; set; }
        public double SLAComplianceRate { get; set; }
        public double FirstTimeFixRate { get; set; }
        public int BacklogCount { get; set; }

        // Comparison
        public double VolumeChangePercent { get; set; } // vs previous period
        public double CostChangePercent { get; set; }
        public string TrendDirection { get; set; } = string.Empty; // Improving, Stable, Declining
    }

    /// <summary>
    /// Recurring issue identification
    /// </summary>
    public class RecurringIssue
    {
        public string IssueId { get; set; } = string.Empty;
        public string IssueName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Frequency
        public int TotalOccurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public double AverageIntervalDays { get; set; }

        // Affected assets
        public List<string> AffectedAssets { get; set; } = new();
        public string CommonLocation { get; set; } = string.Empty;

        // Impact
        public decimal TotalCost { get; set; }
        public double TotalDowntimeHours { get; set; }
        public int AffectedOccupantCount { get; set; }

        // Root cause analysis
        public List<string> PotentialRootCauses { get; set; } = new();
        public string IdentifiedRootCause { get; set; } = string.Empty;
        public bool RootCauseAddressed { get; set; }

        // Resolution
        public string RecommendedPermanentFix { get; set; } = string.Empty;
        public decimal EstimatedFixCost { get; set; }
        public decimal ProjectedAnnualSavings { get; set; }
        public double ROIMonths { get; set; } // Payback period
    }

    #endregion

    #region Pattern Recognition Engine

    /// <summary>
    /// FM Pattern Recognition Engine
    /// Identifies patterns and trends in maintenance data
    /// </summary>
    public class FMPatternRecognition
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Pattern storage
        private readonly List<MaintenancePattern> _identifiedPatterns = new();
        private readonly List<SeasonalTrend> _seasonalTrends = new();
        private readonly List<FailureCorrelation> _correlations = new();
        private readonly List<RecurringIssue> _recurringIssues = new();

        // Historical data (in production, would be from database)
        private readonly List<WorkOrderHistoryRecord> _workOrderHistory = new();

        public FMPatternRecognition()
        {
            InitializeKnownPatterns();
            InitializeSeasonalTrends();
            Logger.Info("FM Pattern Recognition Engine initialized");
        }

        #region Initialization

        private void InitializeKnownPatterns()
        {
            // Pre-populate with common FM patterns
            _identifiedPatterns.AddRange(new[]
            {
                new MaintenancePattern
                {
                    PatternId = "PAT-001",
                    PatternName = "HVAC Peak Season Failures",
                    Type = PatternType.Seasonal,
                    Description = "Increased HVAC system failures during peak cooling season (March-September in East Africa)",
                    Confidence = 0.85,
                    OccurrenceCount = 45,
                    AffectedSystems = new() { "HVAC" },
                    Timing = new PatternTiming
                    {
                        IsSeasonal = true,
                        ActiveMonths = new() { 3, 4, 5, 6, 7, 8, 9 }
                    },
                    TimingDescription = "March through September annually",
                    ImpactDescription = "30% increase in HVAC work orders during peak season",
                    RecommendedAction = "Schedule preventive maintenance in February before peak season",
                    IsActionable = true,
                    PreventionStrategy = "Pre-season inspection and maintenance program"
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-002",
                    PatternName = "Monday Morning Elevator Issues",
                    Type = PatternType.TimeOfDay,
                    Description = "Higher frequency of elevator issues on Monday mornings due to weekend idle period",
                    Confidence = 0.72,
                    OccurrenceCount = 23,
                    AffectedSystems = new() { "Vertical Transport" },
                    Timing = new PatternTiming
                    {
                        IsSeasonal = false,
                        ActiveDays = new() { DayOfWeek.Monday },
                        ActiveStartTime = TimeSpan.FromHours(7),
                        ActiveEndTime = TimeSpan.FromHours(10)
                    },
                    TimingDescription = "Monday mornings 7AM-10AM",
                    ImpactDescription = "Elevator availability issues during peak arrival time",
                    RecommendedAction = "Implement Sunday evening pre-checks or Monday early morning technician presence",
                    IsActionable = true
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-003",
                    PatternName = "Power Quality Issues During Grid Fluctuations",
                    Type = PatternType.Environmental,
                    Description = "Electrical equipment failures correlated with grid power fluctuations",
                    Confidence = 0.78,
                    OccurrenceCount = 18,
                    AffectedSystems = new() { "Electrical" },
                    ImpactDescription = "UPS cycling, sensitive equipment damage",
                    RecommendedAction = "Install power quality monitoring, enhance UPS capacity",
                    IsActionable = true,
                    PreventionStrategy = "Power conditioning equipment, surge protection"
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-004",
                    PatternName = "Chiller Tube Fouling Cycle",
                    Type = PatternType.Cyclic,
                    Description = "Chiller performance degradation on approximately 6-month cycles due to tube fouling",
                    Confidence = 0.88,
                    OccurrenceCount = 12,
                    AffectedSystems = new() { "HVAC" },
                    Timing = new PatternTiming
                    {
                        IsSeasonal = false,
                        RecurrenceIntervalDays = 180
                    },
                    TimingDescription = "Every 6 months",
                    ImpactDescription = "10-15% reduction in chiller efficiency",
                    RecommendedAction = "Implement 6-month tube cleaning schedule",
                    IsActionable = true,
                    PreventionStrategy = "Enhanced water treatment, regular eddy current testing"
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-005",
                    PatternName = "Cascading HVAC Failures",
                    Type = PatternType.Cascading,
                    Description = "Chiller failure leads to AHU overloading and subsequent failures",
                    Confidence = 0.82,
                    OccurrenceCount = 8,
                    AffectedSystems = new() { "HVAC" },
                    ImpactDescription = "Single equipment failure escalates to system-wide issues",
                    RecommendedAction = "Implement load shedding controls, improve system redundancy",
                    IsActionable = true,
                    PreventionStrategy = "N+1 redundancy, automatic load management"
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-006",
                    PatternName = "Filter Clogging in Dusty Areas",
                    Type = PatternType.Location,
                    Description = "Air filters in ground floor and loading dock areas clog faster",
                    Confidence = 0.91,
                    OccurrenceCount = 52,
                    AffectedSystems = new() { "HVAC" },
                    AffectedLocations = new() { "Ground Floor", "Loading Dock", "Parking Level" },
                    ImpactDescription = "2x faster filter replacement needed in affected areas",
                    RecommendedAction = "Increase filter replacement frequency for affected areas",
                    IsActionable = true,
                    PreventionStrategy = "Pre-filters, improved sealing, more frequent checks"
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-007",
                    PatternName = "Generator Fuel Quality Issues",
                    Type = PatternType.Vendor,
                    Description = "Generator starting issues correlated with specific fuel supplier",
                    Confidence = 0.75,
                    OccurrenceCount = 6,
                    AffectedSystems = new() { "Electrical" },
                    ImpactDescription = "Unreliable emergency power backup",
                    RecommendedAction = "Review fuel supplier, implement fuel testing",
                    IsActionable = true,
                    PreventionStrategy = "Regular fuel quality testing, supplier qualification"
                },
                new MaintenancePattern
                {
                    PatternId = "PAT-008",
                    PatternName = "End-of-Life Equipment Cluster",
                    Type = PatternType.AgeRelated,
                    Description = "Multiple equipment items from 2010 installation approaching end of life simultaneously",
                    Confidence = 0.95,
                    OccurrenceCount = 15,
                    AffectedAssets = new() { "AHU-001", "AHU-002", "FCU-101", "FCU-102", "FCU-103" },
                    ImpactDescription = "Potential for multiple simultaneous failures in 2025-2026",
                    RecommendedAction = "Develop staged replacement plan to avoid simultaneous failures",
                    IsActionable = true,
                    PreventionStrategy = "Capital planning, phased replacement program"
                }
            });

            Logger.Info($"Initialized {_identifiedPatterns.Count} known patterns");
        }

        private void InitializeSeasonalTrends()
        {
            // HVAC Seasonal Trend (East Africa context)
            var hvacTrend = new SeasonalTrend
            {
                TrendId = "ST-001",
                TrendName = "HVAC Work Order Seasonality",
                Category = "HVAC",
                PeakMonth = 4, // April - start of heavy rains
                LowMonth = 7,  // July - cooler dry season
                SeasonalityIndex = 0.65, // Moderate seasonality
                TrendDescription = "HVAC work orders peak during hot humid months, lowest during cool dry season"
            };

            // Populate monthly data
            var hvacMonthlyIndex = new Dictionary<int, double>
            {
                { 1, 1.1 }, { 2, 1.2 }, { 3, 1.4 }, { 4, 1.5 }, // Hot season building
                { 5, 1.3 }, { 6, 1.0 }, { 7, 0.7 }, { 8, 0.8 }, // Cooler months
                { 9, 0.9 }, { 10, 1.0 }, { 11, 1.1 }, { 12, 1.0 } // Building again
            };

            var monthNames = new[] { "", "January", "February", "March", "April", "May", "June",
                                    "July", "August", "September", "October", "November", "December" };

            foreach (var (month, index) in hvacMonthlyIndex)
            {
                hvacTrend.MonthlyData[month] = new MonthlyTrendData
                {
                    Month = month,
                    MonthName = monthNames[month],
                    IndexVsAnnualAverage = index,
                    WorkOrderCount = (int)(50 * index), // Base 50 WOs/month
                    AverageCost = 2500000m * (decimal)index
                };
            }

            hvacTrend.PlanningRecommendations = new()
            {
                "Schedule major HVAC maintenance in June-July during low-demand period",
                "Increase HVAC technician availability February-May",
                "Stock additional HVAC parts before March peak",
                "Consider temporary contractor support for March-May period"
            };

            _seasonalTrends.Add(hvacTrend);

            // Electrical Trend
            _seasonalTrends.Add(new SeasonalTrend
            {
                TrendId = "ST-002",
                TrendName = "Electrical Work Order Seasonality",
                Category = "Electrical",
                PeakMonth = 4, // Rainy season - more lightning, humidity issues
                LowMonth = 8,
                SeasonalityIndex = 0.35, // Lower seasonality
                TrendDescription = "Electrical issues increase during rainy season due to lightning and humidity",
                PlanningRecommendations = new()
                {
                    "Inspect lightning protection before rainy season",
                    "Verify surge protection equipment",
                    "Check outdoor electrical enclosure seals"
                }
            });

            Logger.Info($"Initialized {_seasonalTrends.Count} seasonal trends");
        }

        #endregion

        #region Pattern Detection

        /// <summary>
        /// Analyze work order history to identify patterns
        /// </summary>
        public PatternAnalysisResult AnalyzePatterns(List<WorkOrderHistoryRecord> workOrders = null)
        {
            var records = workOrders ?? _workOrderHistory;

            if (!records.Any())
            {
                Logger.Info("No work order history available, returning known patterns");
                return new PatternAnalysisResult
                {
                    AnalysisDate = DateTime.UtcNow,
                    RecordsAnalyzed = 0,
                    IdentifiedPatterns = _identifiedPatterns,
                    SeasonalTrends = _seasonalTrends,
                    RecurringIssues = _recurringIssues
                };
            }

            var result = new PatternAnalysisResult
            {
                AnalysisDate = DateTime.UtcNow,
                RecordsAnalyzed = records.Count
            };

            // Analyze temporal patterns
            var temporalPatterns = AnalyzeTemporalPatterns(records);
            result.IdentifiedPatterns.AddRange(temporalPatterns);

            // Analyze failure correlations
            result.FailureCorrelations = AnalyzeFailureCorrelations(records);

            // Identify recurring issues
            result.RecurringIssues = IdentifyRecurringIssues(records);

            // Analyze work order trends
            result.WorkOrderTrends = AnalyzeWorkOrderTrends(records);

            // Include known patterns
            result.IdentifiedPatterns.AddRange(_identifiedPatterns);
            result.SeasonalTrends = _seasonalTrends;

            Logger.Info($"Pattern analysis complete: {result.IdentifiedPatterns.Count} patterns, " +
                       $"{result.RecurringIssues.Count} recurring issues");

            return result;
        }

        private List<MaintenancePattern> AnalyzeTemporalPatterns(List<WorkOrderHistoryRecord> records)
        {
            var patterns = new List<MaintenancePattern>();

            // Group by day of week
            var dayOfWeekGroups = records.GroupBy(r => r.CreatedDate.DayOfWeek)
                .Select(g => new { Day = g.Key, Count = g.Count(), Avg = records.Count / 7.0 })
                .Where(g => g.Count > g.Avg * 1.3) // 30% above average
                .ToList();

            foreach (var dayGroup in dayOfWeekGroups)
            {
                patterns.Add(new MaintenancePattern
                {
                    PatternName = $"Elevated Issues on {dayGroup.Day}s",
                    Type = PatternType.TimeOfDay,
                    Description = $"{dayGroup.Count / records.Count * 100:F0}% more work orders on {dayGroup.Day}s",
                    Confidence = 0.7,
                    OccurrenceCount = dayGroup.Count,
                    Timing = new PatternTiming
                    {
                        ActiveDays = new() { dayGroup.Day }
                    },
                    IsActionable = true,
                    RecommendedAction = $"Increase staffing on {dayGroup.Day}s"
                });
            }

            // Group by hour of day for emergency work orders
            var emergencyRecords = records.Where(r => r.WorkOrderType == "Emergency").ToList();
            if (emergencyRecords.Count > 10)
            {
                var hourGroups = emergencyRecords.GroupBy(r => r.CreatedDate.Hour)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .ToList();

                if (hourGroups.Any())
                {
                    var peakHours = string.Join(", ", hourGroups.Select(g => $"{g.Key}:00"));
                    patterns.Add(new MaintenancePattern
                    {
                        PatternName = "Emergency Peak Hours",
                        Type = PatternType.TimeOfDay,
                        Description = $"Emergency work orders peak at: {peakHours}",
                        Confidence = 0.75,
                        OccurrenceCount = hourGroups.Sum(g => g.Count()),
                        IsActionable = true,
                        RecommendedAction = "Ensure coverage during peak emergency hours"
                    });
                }
            }

            return patterns;
        }

        private List<FailureCorrelation> AnalyzeFailureCorrelations(List<WorkOrderHistoryRecord> records)
        {
            var correlations = new List<FailureCorrelation>();

            // Find work orders that occurred close together (within 24 hours)
            var orderedRecords = records.OrderBy(r => r.CreatedDate).ToList();

            var coOccurrences = new Dictionary<string, int>();

            for (int i = 0; i < orderedRecords.Count - 1; i++)
            {
                for (int j = i + 1; j < orderedRecords.Count && j < i + 10; j++)
                {
                    var timeDiff = (orderedRecords[j].CreatedDate - orderedRecords[i].CreatedDate).TotalHours;
                    if (timeDiff > 24) break;

                    var key = $"{orderedRecords[i].AssetType}|{orderedRecords[j].AssetType}";
                    if (!coOccurrences.ContainsKey(key))
                        coOccurrences[key] = 0;
                    coOccurrences[key]++;
                }
            }

            // Identify significant correlations
            foreach (var (key, count) in coOccurrences.Where(kv => kv.Value >= 3))
            {
                var parts = key.Split('|');
                correlations.Add(new FailureCorrelation
                {
                    CorrelationId = $"CORR-{correlations.Count + 1:D3}",
                    PrimaryAssetType = parts[0],
                    SecondaryAssetType = parts[1],
                    CoOccurrences = count,
                    CorrelationStrength = Math.Min(1.0, count / 10.0),
                    RelationshipType = parts[0] == parts[1] ? "Same-type cluster" : "Cross-system",
                    Description = $"{parts[0]} and {parts[1]} failures occur within 24 hours {count} times"
                });
            }

            return correlations.OrderByDescending(c => c.CorrelationStrength).Take(10).ToList();
        }

        private List<RecurringIssue> IdentifyRecurringIssues(List<WorkOrderHistoryRecord> records)
        {
            var issues = new List<RecurringIssue>();

            // Group by asset and problem description
            var assetIssueGroups = records
                .GroupBy(r => new { r.AssetId, Problem = NormalizeProblem(r.Description) })
                .Where(g => g.Count() >= 3)
                .ToList();

            foreach (var group in assetIssueGroups)
            {
                var orderedOccurrences = group.OrderBy(r => r.CreatedDate).ToList();
                var intervals = new List<double>();

                for (int i = 1; i < orderedOccurrences.Count; i++)
                {
                    intervals.Add((orderedOccurrences[i].CreatedDate -
                                  orderedOccurrences[i - 1].CreatedDate).TotalDays);
                }

                var issue = new RecurringIssue
                {
                    IssueId = $"RI-{issues.Count + 1:D3}",
                    IssueName = $"Recurring: {group.Key.Problem}",
                    Category = group.First().AssetType,
                    Description = group.Key.Problem,
                    TotalOccurrences = group.Count(),
                    FirstOccurrence = orderedOccurrences.First().CreatedDate,
                    LastOccurrence = orderedOccurrences.Last().CreatedDate,
                    AverageIntervalDays = intervals.Any() ? intervals.Average() : 0,
                    AffectedAssets = new() { group.Key.AssetId },
                    TotalCost = group.Sum(r => r.TotalCost),
                    TotalDowntimeHours = group.Sum(r => r.DowntimeHours),
                    PotentialRootCauses = DetermineRootCauses(group.Key.Problem),
                    RecommendedPermanentFix = DetermineFixRecommendation(group.Key.Problem)
                };

                // Calculate ROI
                if (issue.TotalCost > 0)
                {
                    var annualCost = issue.TotalCost / (decimal)((issue.LastOccurrence - issue.FirstOccurrence).TotalDays / 365.0 + 0.1);
                    issue.ProjectedAnnualSavings = annualCost * 0.8m;
                    issue.EstimatedFixCost = annualCost * 2; // Assume fix costs 2x annual maintenance
                    issue.ROIMonths = issue.EstimatedFixCost > 0 ?
                        (double)(issue.EstimatedFixCost / (issue.ProjectedAnnualSavings / 12)) : 0;
                }

                issues.Add(issue);
            }

            return issues.OrderByDescending(i => i.TotalOccurrences).Take(20).ToList();
        }

        private string NormalizeProblem(string description)
        {
            if (string.IsNullOrEmpty(description))
                return "Unknown issue";

            // Simplify to common categories
            var lower = description.ToLowerInvariant();

            if (lower.Contains("leak")) return "Water/Refrigerant Leak";
            if (lower.Contains("noise") || lower.Contains("vibrat")) return "Noise/Vibration";
            if (lower.Contains("not cool") || lower.Contains("hot")) return "Insufficient Cooling";
            if (lower.Contains("not work") || lower.Contains("fail")) return "Equipment Failure";
            if (lower.Contains("trip") || lower.Contains("breaker")) return "Electrical Trip";
            if (lower.Contains("clog") || lower.Contains("block")) return "Blockage/Clogged";
            if (lower.Contains("light")) return "Lighting Issue";
            if (lower.Contains("door")) return "Door Malfunction";
            if (lower.Contains("smell") || lower.Contains("odor")) return "Odor/Air Quality";

            return description.Length > 30 ? description[..30] + "..." : description;
        }

        private List<string> DetermineRootCauses(string problem)
        {
            var causes = new List<string>();

            switch (problem)
            {
                case "Water/Refrigerant Leak":
                    causes.AddRange(new[] { "Pipe corrosion", "Joint failure", "Vibration damage", "Age deterioration" });
                    break;
                case "Noise/Vibration":
                    causes.AddRange(new[] { "Bearing wear", "Imbalance", "Loose mounting", "Belt wear" });
                    break;
                case "Insufficient Cooling":
                    causes.AddRange(new[] { "Low refrigerant", "Dirty coils", "Compressor issue", "Airflow restriction" });
                    break;
                case "Electrical Trip":
                    causes.AddRange(new[] { "Overload", "Short circuit", "Ground fault", "Equipment failure" });
                    break;
                default:
                    causes.Add("Requires investigation");
                    break;
            }

            return causes;
        }

        private string DetermineFixRecommendation(string problem)
        {
            return problem switch
            {
                "Water/Refrigerant Leak" => "Replace aging piping section, improve vibration isolation",
                "Noise/Vibration" => "Replace bearings, balance rotating components, check alignment",
                "Insufficient Cooling" => "Implement regular coil cleaning, check refrigerant charge",
                "Electrical Trip" => "Load analysis and circuit redistribution, upgrade protection",
                "Door Malfunction" => "Replace door operator, adjust sensors",
                _ => "Conduct root cause analysis and implement corrective action"
            };
        }

        private List<WorkOrderTrend> AnalyzeWorkOrderTrends(List<WorkOrderHistoryRecord> records)
        {
            var trends = new List<WorkOrderTrend>();

            // Group by month
            var monthlyGroups = records
                .GroupBy(r => new { r.CreatedDate.Year, r.CreatedDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToList();

            WorkOrderTrend previousTrend = null;

            foreach (var group in monthlyGroups)
            {
                var trend = new WorkOrderTrend
                {
                    TrendPeriod = $"{group.Key.Year}-{group.Key.Month:D2}",
                    PeriodStart = new DateTime(group.Key.Year, group.Key.Month, 1),
                    PeriodEnd = new DateTime(group.Key.Year, group.Key.Month, 1).AddMonths(1).AddDays(-1),
                    TotalWorkOrders = group.Count(),
                    PreventiveCount = group.Count(r => r.WorkOrderType == "Preventive"),
                    CorrectiveCount = group.Count(r => r.WorkOrderType == "Corrective"),
                    EmergencyCount = group.Count(r => r.WorkOrderType == "Emergency"),
                    TotalCost = group.Sum(r => r.TotalCost),
                    LaborCost = group.Sum(r => r.LaborCost),
                    PartsCost = group.Sum(r => r.PartsCost)
                };

                trend.PreventiveRatio = trend.TotalWorkOrders > 0 ?
                    (double)trend.PreventiveCount / trend.TotalWorkOrders : 0;
                trend.AverageCostPerWorkOrder = trend.TotalWorkOrders > 0 ?
                    trend.TotalCost / trend.TotalWorkOrders : 0;

                if (previousTrend != null)
                {
                    trend.VolumeChangePercent = previousTrend.TotalWorkOrders > 0 ?
                        ((double)trend.TotalWorkOrders / previousTrend.TotalWorkOrders - 1) * 100 : 0;
                    trend.CostChangePercent = previousTrend.TotalCost > 0 ?
                        (double)((trend.TotalCost / previousTrend.TotalCost) - 1) * 100 : 0;

                    trend.TrendDirection = trend.PreventiveRatio > previousTrend.PreventiveRatio ?
                        "Improving" : trend.PreventiveRatio < previousTrend.PreventiveRatio ?
                        "Declining" : "Stable";
                }

                trends.Add(trend);
                previousTrend = trend;
            }

            return trends;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get patterns by type
        /// </summary>
        public List<MaintenancePattern> GetPatternsByType(PatternType type)
        {
            return _identifiedPatterns.Where(p => p.Type == type).ToList();
        }

        /// <summary>
        /// Get actionable patterns
        /// </summary>
        public List<MaintenancePattern> GetActionablePatterns()
        {
            return _identifiedPatterns
                .Where(p => p.IsActionable && p.Confidence > 0.6)
                .OrderByDescending(p => p.Confidence)
                .ToList();
        }

        /// <summary>
        /// Get seasonal trend for system
        /// </summary>
        public SeasonalTrend GetSeasonalTrend(string category)
        {
            return _seasonalTrends.FirstOrDefault(t =>
                t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get current month's expected activity level
        /// </summary>
        public double GetCurrentMonthActivityIndex(string category)
        {
            var trend = GetSeasonalTrend(category);
            if (trend == null) return 1.0;

            var currentMonth = DateTime.Now.Month;
            return trend.MonthlyData.TryGetValue(currentMonth, out var data)
                ? data.IndexVsAnnualAverage : 1.0;
        }

        /// <summary>
        /// Get high-impact recurring issues
        /// </summary>
        public List<RecurringIssue> GetHighImpactRecurringIssues(int topN = 10)
        {
            return _recurringIssues
                .OrderByDescending(i => i.TotalCost + (decimal)i.TotalDowntimeHours * 100000)
                .Take(topN)
                .ToList();
        }

        #endregion

        #endregion // Pattern Recognition Engine
    }

    #region Supporting Classes

    /// <summary>
    /// Work order history record for analysis
    /// </summary>
    public class WorkOrderHistoryRecord
    {
        public string WorkOrderId { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string WorkOrderType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal LaborCost { get; set; }
        public decimal PartsCost { get; set; }
        public decimal TotalCost { get; set; }
        public double DowntimeHours { get; set; }
        public bool WasEmergency { get; set; }
        public string FailureCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pattern analysis result
    /// </summary>
    public class PatternAnalysisResult
    {
        public DateTime AnalysisDate { get; set; }
        public int RecordsAnalyzed { get; set; }
        public List<MaintenancePattern> IdentifiedPatterns { get; set; } = new();
        public List<SeasonalTrend> SeasonalTrends { get; set; } = new();
        public List<FailureCorrelation> FailureCorrelations { get; set; } = new();
        public List<RecurringIssue> RecurringIssues { get; set; } = new();
        public List<WorkOrderTrend> WorkOrderTrends { get; set; } = new();

        public PatternAnalysisSummary GetSummary()
        {
            return new PatternAnalysisSummary
            {
                TotalPatterns = IdentifiedPatterns.Count,
                ActionablePatterns = IdentifiedPatterns.Count(p => p.IsActionable),
                HighConfidencePatterns = IdentifiedPatterns.Count(p => p.Confidence > 0.8),
                RecurringIssueCount = RecurringIssues.Count,
                TotalRecurringIssueCost = RecurringIssues.Sum(i => i.TotalCost),
                TopIssue = RecurringIssues.OrderByDescending(i => i.TotalCost).FirstOrDefault()?.IssueName
            };
        }
    }

    public class PatternAnalysisSummary
    {
        public int TotalPatterns { get; set; }
        public int ActionablePatterns { get; set; }
        public int HighConfidencePatterns { get; set; }
        public int RecurringIssueCount { get; set; }
        public decimal TotalRecurringIssueCost { get; set; }
        public string TopIssue { get; set; } = string.Empty;
    }

    #endregion
}
