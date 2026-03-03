// ============================================================================
// StingBIM AI - Facility Management Anomaly Detection
// Detects unusual patterns in equipment behavior, costs, and operations
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Anomaly Models

    /// <summary>
    /// Detected anomaly
    /// </summary>
    public class Anomaly
    {
        public string AnomalyId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public AnomalyType Type { get; set; }
        public AnomalySeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Detection
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string DetectionMethod { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public double DeviationScore { get; set; } // Standard deviations from normal

        // Context
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;

        // Values
        public string MetricName { get; set; } = string.Empty;
        public double ObservedValue { get; set; }
        public double ExpectedValue { get; set; }
        public double LowerThreshold { get; set; }
        public double UpperThreshold { get; set; }
        public string Unit { get; set; } = string.Empty;

        // Impact
        public string ImpactDescription { get; set; } = string.Empty;
        public decimal EstimatedCostImpact { get; set; }
        public bool RequiresImmediateAction { get; set; }

        // Response
        public string RecommendedAction { get; set; } = string.Empty;
        public AnomalyStatus Status { get; set; } = AnomalyStatus.New;
        public string AssignedTo { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public DateTime? ResolvedAt { get; set; }
    }

    public enum AnomalyType
    {
        EquipmentPerformance,    // Equipment running outside normal parameters
        EnergyConsumption,       // Unusual energy usage
        MaintenanceCost,         // Cost anomalies
        WorkOrderVolume,         // Unusual number of work orders
        ResponseTime,            // SLA/response time anomalies
        SensorReading,           // Abnormal sensor values
        UtilityUsage,           // Water, gas, etc.
        EquipmentRuntime,       // Running hours anomaly
        FailureRate,            // Higher than expected failures
        InventoryLevel,         // Parts inventory anomalies
        ContractorPerformance,  // Contractor quality issues
        OccupantComplaint       // Spike in complaints
    }

    public enum AnomalySeverity
    {
        Critical,    // Immediate attention required
        High,        // Should be addressed within 24 hours
        Medium,      // Should be addressed within week
        Low,         // Monitor and address when convenient
        Info         // Informational, no action needed
    }

    public enum AnomalyStatus
    {
        New,            // Just detected
        Acknowledged,   // Seen by operator
        Investigating,  // Under investigation
        ActionTaken,    // Corrective action taken
        Resolved,       // Issue resolved
        FalsePositive,  // Marked as false positive
        Recurring       // Same anomaly occurred again
    }

    /// <summary>
    /// Threshold configuration for anomaly detection
    /// </summary>
    public class AnomalyThreshold
    {
        public string MetricName { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty; // "*" for all
        public double LowerWarning { get; set; }
        public double LowerCritical { get; set; }
        public double UpperWarning { get; set; }
        public double UpperCritical { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool UseStatistical { get; set; } // Use statistical thresholds
        public double StandardDeviations { get; set; } = 2.0; // For statistical
    }

    /// <summary>
    /// Statistical baseline for metric
    /// </summary>
    public class MetricBaseline
    {
        public string MetricName { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;

        public double Mean { get; set; }
        public double StandardDeviation { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Median { get; set; }

        public int SampleCount { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public DateTime LastUpdated { get; set; }

        // Seasonal adjustments
        public Dictionary<int, double> MonthlyAdjustment { get; set; } = new();
        public Dictionary<DayOfWeek, double> DayOfWeekAdjustment { get; set; } = new();
    }

    /// <summary>
    /// Real-time metric reading for anomaly detection
    /// </summary>
    public class MetricReading
    {
        public string MetricName { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // BMS, Sensor, Manual
    }

    #endregion

    #region Anomaly Detection Engine

    /// <summary>
    /// FM Anomaly Detection Engine
    /// Monitors operations and detects unusual patterns
    /// </summary>
    public class FMAnomalyDetection
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Thresholds and baselines
        private readonly Dictionary<string, AnomalyThreshold> _thresholds = new();
        private readonly Dictionary<string, MetricBaseline> _baselines = new();

        // Detected anomalies
        private readonly List<Anomaly> _activeAnomalies = new();
        private readonly List<Anomaly> _anomalyHistory = new();

        // Reading buffer for trend analysis
        private readonly Dictionary<string, Queue<MetricReading>> _readingBuffer = new();
        private const int BufferSize = 100;

        public FMAnomalyDetection()
        {
            InitializeThresholds();
            InitializeBaselines();
            Logger.Info("FM Anomaly Detection Engine initialized");
        }

        #region Initialization

        private void InitializeThresholds()
        {
            // Equipment performance thresholds
            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Chiller_Efficiency",
                AssetType = "Chiller",
                LowerWarning = 0.65,
                LowerCritical = 0.55,
                UpperWarning = 1.05, // Above nameplate
                UpperCritical = 1.10,
                Unit = "kW/ton"
            });

            AddThreshold(new AnomalyThreshold
            {
                MetricName = "AHU_SupplyAirTemp",
                AssetType = "AHU",
                LowerWarning = 10,
                LowerCritical = 8,
                UpperWarning = 16,
                UpperCritical = 18,
                Unit = "°C"
            });

            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Motor_Vibration",
                AssetType = "*",
                LowerWarning = 0,
                LowerCritical = 0,
                UpperWarning = 4.5,
                UpperCritical = 7.1,
                Unit = "mm/s"
            });

            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Motor_Temperature",
                AssetType = "*",
                LowerWarning = 20,
                LowerCritical = 10,
                UpperWarning = 75,
                UpperCritical = 85,
                Unit = "°C"
            });

            // Energy consumption thresholds (use statistical)
            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Daily_Energy_kWh",
                AssetType = "*",
                UseStatistical = true,
                StandardDeviations = 2.5,
                Unit = "kWh"
            });

            // Cost thresholds
            AddThreshold(new AnomalyThreshold
            {
                MetricName = "WorkOrder_Cost",
                AssetType = "*",
                UseStatistical = true,
                StandardDeviations = 3.0,
                Unit = "UGX"
            });

            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Monthly_Maintenance_Cost",
                AssetType = "*",
                UseStatistical = true,
                StandardDeviations = 2.0,
                Unit = "UGX"
            });

            // Work order volume
            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Daily_WorkOrders",
                AssetType = "*",
                UseStatistical = true,
                StandardDeviations = 2.5,
                Unit = "count"
            });

            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Emergency_WorkOrders_Weekly",
                AssetType = "*",
                LowerWarning = 0,
                LowerCritical = 0,
                UpperWarning = 5,
                UpperCritical = 10,
                Unit = "count"
            });

            // Runtime anomalies
            AddThreshold(new AnomalyThreshold
            {
                MetricName = "Daily_RunHours",
                AssetType = "Chiller",
                LowerWarning = 8, // Running too little
                LowerCritical = 4,
                UpperWarning = 20,
                UpperCritical = 23, // Running almost continuously
                Unit = "hours"
            });

            Logger.Info($"Initialized {_thresholds.Count} anomaly thresholds");
        }

        private void InitializeBaselines()
        {
            // Initialize typical baselines (in production, these would be learned from data)
            _baselines["Daily_Energy_kWh|Building"] = new MetricBaseline
            {
                MetricName = "Daily_Energy_kWh",
                AssetType = "Building",
                Mean = 2500,
                StandardDeviation = 350,
                Minimum = 1800,
                Maximum = 3500,
                SampleCount = 365,
                MonthlyAdjustment = new Dictionary<int, double>
                {
                    { 1, 0.95 }, { 2, 1.0 }, { 3, 1.1 }, { 4, 1.2 },
                    { 5, 1.15 }, { 6, 1.0 }, { 7, 0.9 }, { 8, 0.95 },
                    { 9, 1.0 }, { 10, 1.05 }, { 11, 1.0 }, { 12, 0.95 }
                }
            };

            _baselines["Monthly_Maintenance_Cost|*"] = new MetricBaseline
            {
                MetricName = "Monthly_Maintenance_Cost",
                AssetType = "*",
                Mean = 25000000, // UGX
                StandardDeviation = 5000000,
                Minimum = 15000000,
                Maximum = 45000000,
                SampleCount = 24
            };

            _baselines["Daily_WorkOrders|*"] = new MetricBaseline
            {
                MetricName = "Daily_WorkOrders",
                AssetType = "*",
                Mean = 8,
                StandardDeviation = 3,
                Minimum = 1,
                Maximum = 20,
                SampleCount = 365,
                DayOfWeekAdjustment = new Dictionary<DayOfWeek, double>
                {
                    { DayOfWeek.Monday, 1.3 },
                    { DayOfWeek.Tuesday, 1.1 },
                    { DayOfWeek.Wednesday, 1.0 },
                    { DayOfWeek.Thursday, 1.0 },
                    { DayOfWeek.Friday, 0.9 },
                    { DayOfWeek.Saturday, 0.5 },
                    { DayOfWeek.Sunday, 0.3 }
                }
            };

            Logger.Info($"Initialized {_baselines.Count} metric baselines");
        }

        private void AddThreshold(AnomalyThreshold threshold)
        {
            var key = $"{threshold.MetricName}|{threshold.AssetType}";
            _thresholds[key] = threshold;
        }

        #endregion

        #region Anomaly Detection

        /// <summary>
        /// Process a metric reading and check for anomalies
        /// </summary>
        public Anomaly CheckReading(MetricReading reading)
        {
            // Add to buffer
            AddToBuffer(reading);

            // Get threshold
            var threshold = GetThreshold(reading.MetricName, reading.AssetType);
            if (threshold == null)
            {
                Logger.Debug($"No threshold configured for {reading.MetricName}/{reading.AssetType}");
                return null;
            }

            Anomaly anomaly = null;

            if (threshold.UseStatistical)
            {
                anomaly = CheckStatisticalAnomaly(reading, threshold);
            }
            else
            {
                anomaly = CheckFixedThresholdAnomaly(reading, threshold);
            }

            if (anomaly != null)
            {
                // Check for trend-based anomaly
                var trendAnomaly = CheckTrendAnomaly(reading);
                if (trendAnomaly != null && trendAnomaly.ConfidenceScore > anomaly.ConfidenceScore)
                {
                    anomaly = trendAnomaly;
                }

                _activeAnomalies.Add(anomaly);
                Logger.Warn($"Anomaly detected: {anomaly.Title} - {anomaly.Description}");
            }

            return anomaly;
        }

        private Anomaly CheckFixedThresholdAnomaly(MetricReading reading, AnomalyThreshold threshold)
        {
            var severity = AnomalySeverity.Info;
            string description = null;

            if (reading.Value <= threshold.LowerCritical)
            {
                severity = AnomalySeverity.Critical;
                description = $"Value {reading.Value:F2} {threshold.Unit} is critically below threshold ({threshold.LowerCritical:F2})";
            }
            else if (reading.Value <= threshold.LowerWarning)
            {
                severity = AnomalySeverity.High;
                description = $"Value {reading.Value:F2} {threshold.Unit} is below warning threshold ({threshold.LowerWarning:F2})";
            }
            else if (reading.Value >= threshold.UpperCritical)
            {
                severity = AnomalySeverity.Critical;
                description = $"Value {reading.Value:F2} {threshold.Unit} is critically above threshold ({threshold.UpperCritical:F2})";
            }
            else if (reading.Value >= threshold.UpperWarning)
            {
                severity = AnomalySeverity.High;
                description = $"Value {reading.Value:F2} {threshold.Unit} is above warning threshold ({threshold.UpperWarning:F2})";
            }

            if (description == null)
                return null;

            return new Anomaly
            {
                Type = DetermineAnomalyType(reading.MetricName),
                Severity = severity,
                Title = $"{reading.MetricName} Threshold Exceeded",
                Description = description,
                DetectionMethod = "Fixed Threshold",
                ConfidenceScore = 0.95,
                AssetId = reading.AssetId,
                AssetType = reading.AssetType,
                MetricName = reading.MetricName,
                ObservedValue = reading.Value,
                ExpectedValue = (threshold.UpperWarning + threshold.LowerWarning) / 2,
                LowerThreshold = threshold.LowerWarning,
                UpperThreshold = threshold.UpperWarning,
                Unit = threshold.Unit,
                RequiresImmediateAction = severity == AnomalySeverity.Critical,
                RecommendedAction = GetRecommendedAction(reading.MetricName, reading.Value, threshold)
            };
        }

        private Anomaly CheckStatisticalAnomaly(MetricReading reading, AnomalyThreshold threshold)
        {
            var baselineKey = $"{reading.MetricName}|{reading.AssetType}";
            if (!_baselines.TryGetValue(baselineKey, out var baseline))
            {
                // Try wildcard
                baselineKey = $"{reading.MetricName}|*";
                if (!_baselines.TryGetValue(baselineKey, out baseline))
                    return null;
            }

            // Apply adjustments
            var adjustedMean = baseline.Mean;
            if (baseline.MonthlyAdjustment.TryGetValue(reading.Timestamp.Month, out var monthAdj))
                adjustedMean *= monthAdj;
            if (baseline.DayOfWeekAdjustment.TryGetValue(reading.Timestamp.DayOfWeek, out var dayAdj))
                adjustedMean *= dayAdj;

            // Calculate z-score
            var zScore = (reading.Value - adjustedMean) / baseline.StandardDeviation;
            var deviationScore = Math.Abs(zScore);

            if (deviationScore < threshold.StandardDeviations)
                return null;

            var severity = deviationScore switch
            {
                > 4.0 => AnomalySeverity.Critical,
                > 3.0 => AnomalySeverity.High,
                > 2.5 => AnomalySeverity.Medium,
                _ => AnomalySeverity.Low
            };

            var direction = zScore > 0 ? "above" : "below";

            return new Anomaly
            {
                Type = DetermineAnomalyType(reading.MetricName),
                Severity = severity,
                Title = $"{reading.MetricName} Statistical Anomaly",
                Description = $"Value {reading.Value:F2} is {deviationScore:F1} standard deviations {direction} expected ({adjustedMean:F2})",
                DetectionMethod = "Statistical (Z-Score)",
                ConfidenceScore = Math.Min(0.99, 0.5 + (deviationScore - 2) * 0.15),
                DeviationScore = deviationScore,
                AssetId = reading.AssetId,
                AssetType = reading.AssetType,
                MetricName = reading.MetricName,
                ObservedValue = reading.Value,
                ExpectedValue = adjustedMean,
                LowerThreshold = adjustedMean - threshold.StandardDeviations * baseline.StandardDeviation,
                UpperThreshold = adjustedMean + threshold.StandardDeviations * baseline.StandardDeviation,
                Unit = threshold.Unit,
                RequiresImmediateAction = severity == AnomalySeverity.Critical,
                RecommendedAction = GetStatisticalRecommendation(reading, zScore, baseline)
            };
        }

        private Anomaly CheckTrendAnomaly(MetricReading reading)
        {
            var bufferKey = $"{reading.MetricName}|{reading.AssetId}";
            if (!_readingBuffer.TryGetValue(bufferKey, out var buffer) || buffer.Count < 10)
                return null;

            var readings = buffer.ToList();
            var recentReadings = readings.TakeLast(10).ToList();

            // Check for sudden spike/drop (last reading vs previous 9)
            var previousAvg = recentReadings.Take(9).Average(r => r.Value);
            var previousStd = CalculateStdDev(recentReadings.Take(9).Select(r => r.Value));

            if (previousStd == 0) return null;

            var currentZScore = (reading.Value - previousAvg) / previousStd;

            if (Math.Abs(currentZScore) < 3)
                return null;

            // Check for persistent trend
            var trend = CalculateTrend(recentReadings.Select(r => r.Value).ToList());
            var isTrending = Math.Abs(trend) > 0.1; // More than 10% trend per reading

            var direction = currentZScore > 0 ? "spike" : "drop";

            return new Anomaly
            {
                Type = DetermineAnomalyType(reading.MetricName),
                Severity = isTrending ? AnomalySeverity.High : AnomalySeverity.Medium,
                Title = $"{reading.MetricName} Sudden {direction.ToUpper()}",
                Description = $"Sudden {direction} detected: {reading.Value:F2} vs recent average {previousAvg:F2}" +
                             (isTrending ? " with persistent trend" : ""),
                DetectionMethod = "Trend Analysis",
                ConfidenceScore = Math.Min(0.95, 0.5 + Math.Abs(currentZScore) * 0.1),
                DeviationScore = Math.Abs(currentZScore),
                AssetId = reading.AssetId,
                MetricName = reading.MetricName,
                ObservedValue = reading.Value,
                ExpectedValue = previousAvg,
                RecommendedAction = $"Investigate sudden {direction} in {reading.MetricName}"
            };
        }

        #endregion

        #region Batch Anomaly Detection

        /// <summary>
        /// Analyze a batch of readings for anomalies
        /// </summary>
        public List<Anomaly> AnalyzeBatch(List<MetricReading> readings)
        {
            var anomalies = new List<Anomaly>();

            foreach (var reading in readings)
            {
                var anomaly = CheckReading(reading);
                if (anomaly != null)
                    anomalies.Add(anomaly);
            }

            // Cross-asset analysis
            var crossAssetAnomalies = AnalyzeCrossAssetPatterns(readings);
            anomalies.AddRange(crossAssetAnomalies);

            return anomalies.OrderByDescending(a => a.Severity).ToList();
        }

        /// <summary>
        /// Detect anomalies that span multiple assets
        /// </summary>
        private List<Anomaly> AnalyzeCrossAssetPatterns(List<MetricReading> readings)
        {
            var anomalies = new List<Anomaly>();

            // Group readings by time window (5-minute windows)
            var windowedReadings = readings
                .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day,
                                          r.Timestamp.Hour, r.Timestamp.Minute / 5 * 5, 0))
                .ToList();

            foreach (var window in windowedReadings)
            {
                // Check for multiple assets showing same anomaly type
                var assetsByMetric = window.GroupBy(r => r.MetricName)
                    .Where(g => g.Select(r => r.AssetId).Distinct().Count() >= 3)
                    .ToList();

                foreach (var metricGroup in assetsByMetric)
                {
                    var values = metricGroup.Select(r => r.Value).ToList();
                    var mean = values.Average();
                    var std = CalculateStdDev(values);

                    // Check if all values are abnormally high or low
                    var allHigh = values.All(v => v > mean + std);
                    var allLow = values.All(v => v < mean - std);

                    if (allHigh || allLow)
                    {
                        var affectedAssets = metricGroup.Select(r => r.AssetId).Distinct().ToList();
                        var direction = allHigh ? "elevated" : "depressed";

                        anomalies.Add(new Anomaly
                        {
                            Type = AnomalyType.EquipmentPerformance,
                            Severity = AnomalySeverity.High,
                            Title = $"System-Wide {metricGroup.Key} Anomaly",
                            Description = $"Multiple assets showing {direction} {metricGroup.Key}: {string.Join(", ", affectedAssets)}",
                            DetectionMethod = "Cross-Asset Pattern",
                            ConfidenceScore = 0.85,
                            MetricName = metricGroup.Key,
                            ObservedValue = mean,
                            ImpactDescription = $"Affects {affectedAssets.Count} assets simultaneously",
                            RecommendedAction = "Investigate common cause (power supply, ambient conditions, etc.)"
                        });
                    }
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Analyze work order data for cost anomalies
        /// </summary>
        public List<Anomaly> AnalyzeWorkOrderCosts(List<WorkOrderCostData> workOrders)
        {
            var anomalies = new List<Anomaly>();

            if (workOrders.Count < 10)
                return anomalies;

            // Overall cost anomalies
            var costs = workOrders.Select(wo => (double)wo.TotalCost).ToList();
            var mean = costs.Average();
            var std = CalculateStdDev(costs);

            foreach (var wo in workOrders)
            {
                var zScore = ((double)wo.TotalCost - mean) / std;
                if (Math.Abs(zScore) > 3)
                {
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.MaintenanceCost,
                        Severity = zScore > 4 ? AnomalySeverity.High : AnomalySeverity.Medium,
                        Title = "Work Order Cost Anomaly",
                        Description = $"Work order {wo.WorkOrderId} cost ({wo.TotalCost:N0} UGX) is " +
                                     $"{Math.Abs(zScore):F1} standard deviations from average",
                        AssetId = wo.AssetId,
                        AssetName = wo.AssetName,
                        MetricName = "WorkOrder_Cost",
                        ObservedValue = (double)wo.TotalCost,
                        ExpectedValue = mean,
                        DeviationScore = Math.Abs(zScore),
                        Unit = "UGX",
                        EstimatedCostImpact = wo.TotalCost - (decimal)mean,
                        RecommendedAction = "Review work order for accuracy and cost justification"
                    });
                }
            }

            // Asset-specific cost trends
            var costsByAsset = workOrders.GroupBy(wo => wo.AssetId)
                .Where(g => g.Count() >= 5)
                .ToList();

            foreach (var assetGroup in costsByAsset)
            {
                var assetCosts = assetGroup.OrderBy(wo => wo.CompletedDate).Select(wo => (double)wo.TotalCost).ToList();
                var trend = CalculateTrend(assetCosts);

                if (trend > 0.2) // Costs increasing >20% per work order
                {
                    var lastWO = assetGroup.OrderByDescending(wo => wo.CompletedDate).First();
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.MaintenanceCost,
                        Severity = AnomalySeverity.Medium,
                        Title = "Rising Maintenance Costs",
                        Description = $"Maintenance costs for {lastWO.AssetName} are trending upward ({trend * 100:F0}% increase trend)",
                        AssetId = lastWO.AssetId,
                        AssetName = lastWO.AssetName,
                        MetricName = "Maintenance_Cost_Trend",
                        RecommendedAction = "Review asset condition, consider repair vs replace analysis"
                    });
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Analyze energy consumption for anomalies
        /// </summary>
        public List<Anomaly> AnalyzeEnergyConsumption(List<EnergyReading> readings)
        {
            var anomalies = new List<Anomaly>();

            if (readings.Count < 30)
                return anomalies;

            // Daily totals
            var dailyTotals = readings
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(r => r.kWh) })
                .OrderBy(d => d.Date)
                .ToList();

            var values = dailyTotals.Select(d => d.Total).ToList();
            var mean = values.Average();
            var std = CalculateStdDev(values);

            foreach (var day in dailyTotals)
            {
                var zScore = (day.Total - mean) / std;

                // Apply day-of-week adjustment
                var baseline = _baselines.GetValueOrDefault("Daily_Energy_kWh|Building");
                if (baseline?.DayOfWeekAdjustment.TryGetValue(day.Date.DayOfWeek, out var adj) == true)
                {
                    var adjustedMean = mean * adj;
                    zScore = (day.Total - adjustedMean) / std;
                }

                if (Math.Abs(zScore) > 2.5)
                {
                    var direction = zScore > 0 ? "high" : "low";
                    anomalies.Add(new Anomaly
                    {
                        Type = AnomalyType.EnergyConsumption,
                        Severity = Math.Abs(zScore) > 3.5 ? AnomalySeverity.High : AnomalySeverity.Medium,
                        Title = $"Abnormal Energy Consumption",
                        Description = $"Energy consumption on {day.Date:yyyy-MM-dd} ({day.Total:N0} kWh) was abnormally {direction}",
                        MetricName = "Daily_Energy_kWh",
                        ObservedValue = day.Total,
                        ExpectedValue = mean,
                        DeviationScore = Math.Abs(zScore),
                        Unit = "kWh",
                        EstimatedCostImpact = (decimal)(day.Total - mean) * 500, // UGX per kWh estimate
                        RecommendedAction = direction == "high" ?
                            "Check for equipment running unnecessarily or malfunctioning" :
                            "Verify meter readings, check for equipment outages"
                    });
                }
            }

            return anomalies;
        }

        #endregion

        #region Helper Methods

        private void AddToBuffer(MetricReading reading)
        {
            var key = $"{reading.MetricName}|{reading.AssetId}";
            if (!_readingBuffer.ContainsKey(key))
                _readingBuffer[key] = new Queue<MetricReading>();

            var buffer = _readingBuffer[key];
            buffer.Enqueue(reading);

            while (buffer.Count > BufferSize)
                buffer.Dequeue();
        }

        private AnomalyThreshold GetThreshold(string metricName, string assetType)
        {
            var key = $"{metricName}|{assetType}";
            if (_thresholds.TryGetValue(key, out var threshold))
                return threshold;

            // Try wildcard
            key = $"{metricName}|*";
            return _thresholds.GetValueOrDefault(key);
        }

        private AnomalyType DetermineAnomalyType(string metricName)
        {
            var lower = metricName.ToLowerInvariant();

            if (lower.Contains("energy") || lower.Contains("kwh") || lower.Contains("power"))
                return AnomalyType.EnergyConsumption;
            if (lower.Contains("cost"))
                return AnomalyType.MaintenanceCost;
            if (lower.Contains("workorder") || lower.Contains("work_order"))
                return AnomalyType.WorkOrderVolume;
            if (lower.Contains("temp") || lower.Contains("vibration") || lower.Contains("pressure"))
                return AnomalyType.SensorReading;
            if (lower.Contains("runtime") || lower.Contains("runhour"))
                return AnomalyType.EquipmentRuntime;

            return AnomalyType.EquipmentPerformance;
        }

        private string GetRecommendedAction(string metricName, double value, AnomalyThreshold threshold)
        {
            var lower = metricName.ToLowerInvariant();

            if (lower.Contains("vibration"))
                return value > threshold.UpperWarning ?
                    "Inspect bearings and alignment, check for loose components" :
                    "Verify sensor operation";

            if (lower.Contains("temp"))
                return value > threshold.UpperWarning ?
                    "Check cooling, verify airflow, inspect for blockages" :
                    "Verify heating system, check for freeze protection";

            if (lower.Contains("efficiency"))
                return "Schedule performance tune-up, check for fouling or degradation";

            return "Investigate root cause and take corrective action";
        }

        private string GetStatisticalRecommendation(MetricReading reading, double zScore, MetricBaseline baseline)
        {
            var direction = zScore > 0 ? "higher" : "lower";

            if (reading.MetricName.Contains("Cost"))
                return $"Review recent maintenance activities, costs are {direction} than typical";

            if (reading.MetricName.Contains("Energy"))
                return $"Energy usage is {direction} than expected - investigate equipment operation";

            if (reading.MetricName.Contains("WorkOrder"))
                return $"Work order volume is {direction} than normal - review staffing and prioritization";

            return $"Value is significantly {direction} than baseline - investigate cause";
        }

        private double CalculateStdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;

            var mean = list.Average();
            var sumSquares = list.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / (list.Count - 1));
        }

        private double CalculateTrend(List<double> values)
        {
            if (values.Count < 3) return 0;

            // Simple linear regression slope
            var n = values.Count;
            var sumX = (n - 1) * n / 2.0;
            var sumY = values.Sum();
            var sumXY = values.Select((y, x) => x * y).Sum();
            var sumXX = Enumerable.Range(0, n).Sum(x => x * x);

            var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            var mean = sumY / n;

            return mean != 0 ? slope / mean : 0;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get active anomalies
        /// </summary>
        public List<Anomaly> GetActiveAnomalies(AnomalySeverity? minSeverity = null)
        {
            var query = _activeAnomalies.Where(a => a.Status != AnomalyStatus.Resolved);

            if (minSeverity.HasValue)
                query = query.Where(a => a.Severity <= minSeverity.Value);

            return query.OrderBy(a => a.Severity).ThenByDescending(a => a.DetectedAt).ToList();
        }

        /// <summary>
        /// Get anomaly by ID
        /// </summary>
        public Anomaly GetAnomaly(string anomalyId)
        {
            return _activeAnomalies.FirstOrDefault(a => a.AnomalyId == anomalyId) ??
                   _anomalyHistory.FirstOrDefault(a => a.AnomalyId == anomalyId);
        }

        /// <summary>
        /// Update anomaly status
        /// </summary>
        public void UpdateAnomalyStatus(string anomalyId, AnomalyStatus newStatus, string notes = null)
        {
            var anomaly = GetAnomaly(anomalyId);
            if (anomaly == null) return;

            anomaly.Status = newStatus;
            if (!string.IsNullOrEmpty(notes))
                anomaly.Resolution = notes;

            if (newStatus == AnomalyStatus.Resolved || newStatus == AnomalyStatus.FalsePositive)
            {
                anomaly.ResolvedAt = DateTime.UtcNow;
                _activeAnomalies.Remove(anomaly);
                _anomalyHistory.Add(anomaly);
            }

            Logger.Info($"Anomaly {anomalyId} status updated to {newStatus}");
        }

        /// <summary>
        /// Get anomaly statistics
        /// </summary>
        public AnomalyStatistics GetStatistics(DateTime? since = null)
        {
            var cutoff = since ?? DateTime.UtcNow.AddDays(-30);

            var relevant = _activeAnomalies.Concat(_anomalyHistory)
                .Where(a => a.DetectedAt >= cutoff)
                .ToList();

            return new AnomalyStatistics
            {
                TotalDetected = relevant.Count,
                ActiveCount = _activeAnomalies.Count,
                ResolvedCount = _anomalyHistory.Count(a => a.DetectedAt >= cutoff),
                CriticalCount = relevant.Count(a => a.Severity == AnomalySeverity.Critical),
                HighCount = relevant.Count(a => a.Severity == AnomalySeverity.High),
                FalsePositiveRate = relevant.Count > 0 ?
                    (double)relevant.Count(a => a.Status == AnomalyStatus.FalsePositive) / relevant.Count : 0,
                AverageResolutionTimeHours = _anomalyHistory
                    .Where(a => a.ResolvedAt.HasValue && a.DetectedAt >= cutoff)
                    .Select(a => (a.ResolvedAt.Value - a.DetectedAt).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average(),
                ByType = relevant.GroupBy(a => a.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                TotalEstimatedImpact = relevant.Sum(a => a.EstimatedCostImpact)
            };
        }

        #endregion

        #endregion // Anomaly Detection Engine
    }

    #region Supporting Classes

    public class WorkOrderCostData
    {
        public string WorkOrderId { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public DateTime CompletedDate { get; set; }
        public decimal LaborCost { get; set; }
        public decimal PartsCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class EnergyReading
    {
        public DateTime Timestamp { get; set; }
        public string MeterId { get; set; } = string.Empty;
        public double kWh { get; set; }
    }

    public class AnomalyStatistics
    {
        public int TotalDetected { get; set; }
        public int ActiveCount { get; set; }
        public int ResolvedCount { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public double FalsePositiveRate { get; set; }
        public double AverageResolutionTimeHours { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public decimal TotalEstimatedImpact { get; set; }
    }

    #endregion
}
