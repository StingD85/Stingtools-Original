// ============================================================================
// StingBIM AI - Facility Management Benchmarking & Performance Analytics
// KPI benchmarking, contractor performance, and operational excellence metrics
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Benchmarking Models

    /// <summary>
    /// FM Key Performance Indicator
    /// </summary>
    public class FMKpi
    {
        public string KpiId { get; set; } = string.Empty;
        public string KpiName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Current performance
        public double CurrentValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime MeasurementDate { get; set; }

        // Targets and benchmarks
        public double Target { get; set; }
        public double IndustryBenchmark { get; set; }
        public double BestInClass { get; set; }
        public double PreviousPeriodValue { get; set; }

        // Performance assessment
        public double PerformanceVsTarget { get; set; } // %
        public double PerformanceVsBenchmark { get; set; }
        public string PerformanceRating { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty; // Improving, Stable, Declining

        // Context
        public bool HigherIsBetter { get; set; }
        public string CalculationMethod { get; set; } = string.Empty;
        public string DataSource { get; set; } = string.Empty;
    }

    /// <summary>
    /// Benchmark comparison
    /// </summary>
    public class BenchmarkComparison
    {
        public string BuildingId { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double YourValue { get; set; }
        public double PeerMedian { get; set; }
        public double PeerPercentile25 { get; set; }
        public double PeerPercentile75 { get; set; }
        public double BestInClass { get; set; }
        public int YourPercentile { get; set; }
        public string PerformanceCategory { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contractor/vendor performance
    /// </summary>
    public class ContractorPerformance
    {
        public string ContractorId { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string ContractorType { get; set; } = string.Empty; // Specialist, General, OEM
        public List<string> ServiceCategories { get; set; } = new();

        // Contract info
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime ContractStart { get; set; }
        public DateTime ContractEnd { get; set; }
        public decimal AnnualContractValue { get; set; }

        // Performance metrics
        public int TotalWorkOrders { get; set; }
        public int CompletedOnTime { get; set; }
        public double OnTimeCompletionRate { get; set; }
        public int FirstTimeFixCount { get; set; }
        public double FirstTimeFixRate { get; set; }
        public double AverageResponseTimeHours { get; set; }
        public double AverageCompletionTimeHours { get; set; }
        public int CallbackCount { get; set; }
        public double CallbackRate { get; set; }

        // Quality metrics
        public double QualityScore { get; set; } // 0-100
        public int SafetyIncidents { get; set; }
        public double ComplianceRate { get; set; }
        public double DocumentationScore { get; set; }

        // Cost metrics
        public decimal TotalBilledAmount { get; set; }
        public decimal AverageCostPerWorkOrder { get; set; }
        public double CostVariance { get; set; } // vs estimates

        // Customer satisfaction
        public double SatisfactionScore { get; set; } // 1-5
        public int ComplaintsReceived { get; set; }

        // Overall
        public double OverallPerformanceScore { get; set; }
        public string PerformanceRating { get; set; } = string.Empty; // Excellent, Good, Acceptable, Poor
        public bool RecommendForRenewal { get; set; }
    }

    /// <summary>
    /// Operational excellence scorecard
    /// </summary>
    public class OperationalScorecard
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Category scores (0-100)
        public double MaintenanceScore { get; set; }
        public double EnergyScore { get; set; }
        public double ComfortScore { get; set; }
        public double SafetyScore { get; set; }
        public double CostScore { get; set; }
        public double SustainabilityScore { get; set; }

        // Overall
        public double OverallScore { get; set; }
        public string OverallRating { get; set; } = string.Empty;
        public int Ranking { get; set; } // Within portfolio

        // KPIs
        public List<FMKpi> KeyMetrics { get; set; } = new();

        // Trends
        public double ScoreChangePrevPeriod { get; set; }
        public string TrendDirection { get; set; } = string.Empty;

        // Improvement areas
        public List<string> StrengthAreas { get; set; } = new();
        public List<string> ImprovementAreas { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    #endregion

    #region Benchmarking Engine

    /// <summary>
    /// FM Benchmarking and Performance Engine
    /// Provides KPI tracking, benchmarking, and contractor performance analytics
    /// </summary>
    public class FMBenchmarking
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // KPI definitions and targets
        private readonly Dictionary<string, FMKpiDefinition> _kpiDefinitions = new();

        // Industry benchmarks (East Africa specific)
        private readonly Dictionary<string, IndustryBenchmark> _benchmarks = new();

        // Contractor data
        private readonly Dictionary<string, ContractorPerformance> _contractors = new();

        public FMBenchmarking()
        {
            InitializeKPIDefinitions();
            InitializeBenchmarks();
            InitializeSampleContractors();
            Logger.Info("FM Benchmarking Engine initialized");
        }

        #region Initialization

        private void InitializeKPIDefinitions()
        {
            _kpiDefinitions["MTTR"] = new FMKpiDefinition
            {
                KpiId = "MTTR",
                KpiName = "Mean Time To Repair",
                Category = "Maintenance",
                Unit = "hours",
                HigherIsBetter = false,
                Target = 4,
                WarningThreshold = 6,
                CriticalThreshold = 8,
                CalculationMethod = "Average time from work order creation to completion"
            };

            _kpiDefinitions["MTBF"] = new FMKpiDefinition
            {
                KpiId = "MTBF",
                KpiName = "Mean Time Between Failures",
                Category = "Reliability",
                Unit = "hours",
                HigherIsBetter = true,
                Target = 2000,
                CalculationMethod = "Average operating time between failures for critical equipment"
            };

            _kpiDefinitions["PMCompliance"] = new FMKpiDefinition
            {
                KpiId = "PMCompliance",
                KpiName = "Preventive Maintenance Compliance",
                Category = "Maintenance",
                Unit = "%",
                HigherIsBetter = true,
                Target = 95,
                WarningThreshold = 85,
                CriticalThreshold = 75,
                CalculationMethod = "PM work orders completed on schedule / Total scheduled PM"
            };

            _kpiDefinitions["WOCompletionRate"] = new FMKpiDefinition
            {
                KpiId = "WOCompletionRate",
                KpiName = "Work Order Completion Rate",
                Category = "Maintenance",
                Unit = "%",
                HigherIsBetter = true,
                Target = 98,
                CalculationMethod = "Completed work orders / Total work orders"
            };

            _kpiDefinitions["FirstTimeFix"] = new FMKpiDefinition
            {
                KpiId = "FirstTimeFix",
                KpiName = "First Time Fix Rate",
                Category = "Quality",
                Unit = "%",
                HigherIsBetter = true,
                Target = 85,
                CalculationMethod = "Work orders resolved on first visit / Total work orders"
            };

            _kpiDefinitions["SLACompliance"] = new FMKpiDefinition
            {
                KpiId = "SLACompliance",
                KpiName = "SLA Compliance Rate",
                Category = "Service",
                Unit = "%",
                HigherIsBetter = true,
                Target = 95,
                CalculationMethod = "Work orders meeting SLA / Total work orders"
            };

            _kpiDefinitions["ReactiveRatio"] = new FMKpiDefinition
            {
                KpiId = "ReactiveRatio",
                KpiName = "Reactive Maintenance Ratio",
                Category = "Maintenance",
                Unit = "%",
                HigherIsBetter = false,
                Target = 20,
                WarningThreshold = 30,
                CriticalThreshold = 40,
                CalculationMethod = "Reactive work orders / Total maintenance work orders"
            };

            _kpiDefinitions["EUI"] = new FMKpiDefinition
            {
                KpiId = "EUI",
                KpiName = "Energy Use Intensity",
                Category = "Energy",
                Unit = "kWh/m²/year",
                HigherIsBetter = false,
                Target = 150,
                CalculationMethod = "Total energy consumption / Gross floor area"
            };

            _kpiDefinitions["CostPerM2"] = new FMKpiDefinition
            {
                KpiId = "CostPerM2",
                KpiName = "FM Cost per Square Meter",
                Category = "Cost",
                Unit = "UGX/m²/year",
                HigherIsBetter = false,
                Target = 50000,
                CalculationMethod = "Total FM operating cost / Gross floor area"
            };

            _kpiDefinitions["OccupantSatisfaction"] = new FMKpiDefinition
            {
                KpiId = "OccupantSatisfaction",
                KpiName = "Occupant Satisfaction Score",
                Category = "Service",
                Unit = "score (1-5)",
                HigherIsBetter = true,
                Target = 4.2,
                CalculationMethod = "Average satisfaction rating from occupant surveys"
            };

            _kpiDefinitions["EquipmentUptime"] = new FMKpiDefinition
            {
                KpiId = "EquipmentUptime",
                KpiName = "Critical Equipment Uptime",
                Category = "Reliability",
                Unit = "%",
                HigherIsBetter = true,
                Target = 99.5,
                CalculationMethod = "(Total time - Downtime) / Total time"
            };
        }

        private void InitializeBenchmarks()
        {
            // Office building benchmarks (East Africa context)
            _benchmarks["Office-MTTR"] = new IndustryBenchmark
            {
                MetricName = "MTTR",
                BuildingType = "Office",
                Region = "East Africa",
                Percentile25 = 3,
                Median = 5,
                Percentile75 = 8,
                BestInClass = 2
            };

            _benchmarks["Office-PMCompliance"] = new IndustryBenchmark
            {
                MetricName = "PMCompliance",
                BuildingType = "Office",
                Percentile25 = 80,
                Median = 88,
                Percentile75 = 95,
                BestInClass = 98
            };

            _benchmarks["Office-EUI"] = new IndustryBenchmark
            {
                MetricName = "EUI",
                BuildingType = "Office",
                Region = "East Africa",
                Percentile25 = 220,
                Median = 180,
                Percentile75 = 140,
                BestInClass = 100
            };

            _benchmarks["Office-CostPerM2"] = new IndustryBenchmark
            {
                MetricName = "CostPerM2",
                BuildingType = "Office",
                Region = "East Africa",
                Percentile25 = 70000,
                Median = 55000,
                Percentile75 = 40000,
                BestInClass = 30000
            };

            _benchmarks["Office-ReactiveRatio"] = new IndustryBenchmark
            {
                MetricName = "ReactiveRatio",
                BuildingType = "Office",
                Percentile25 = 35,
                Median = 25,
                Percentile75 = 18,
                BestInClass = 10
            };
        }

        private void InitializeSampleContractors()
        {
            _contractors["CONT-001"] = new ContractorPerformance
            {
                ContractorId = "CONT-001",
                ContractorName = "Apex Elevator Services",
                ContractorType = "Specialist",
                ServiceCategories = new() { "Elevators", "Escalators" },
                ContractStart = new DateTime(2024, 1, 1),
                ContractEnd = new DateTime(2026, 12, 31),
                AnnualContractValue = 24000000,
                TotalWorkOrders = 48,
                CompletedOnTime = 44,
                OnTimeCompletionRate = 91.7,
                FirstTimeFixCount = 42,
                FirstTimeFixRate = 87.5,
                AverageResponseTimeHours = 2.5,
                AverageCompletionTimeHours = 3.8,
                CallbackCount = 3,
                CallbackRate = 6.25,
                QualityScore = 88,
                SafetyIncidents = 0,
                ComplianceRate = 100,
                SatisfactionScore = 4.3,
                OverallPerformanceScore = 89,
                PerformanceRating = "Good",
                RecommendForRenewal = true
            };

            _contractors["CONT-002"] = new ContractorPerformance
            {
                ContractorId = "CONT-002",
                ContractorName = "CoolTech HVAC Specialists",
                ContractorType = "Specialist",
                ServiceCategories = new() { "Chillers", "AHU", "HVAC Controls" },
                ContractStart = new DateTime(2023, 7, 1),
                ContractEnd = new DateTime(2025, 6, 30),
                AnnualContractValue = 48000000,
                TotalWorkOrders = 156,
                CompletedOnTime = 138,
                OnTimeCompletionRate = 88.5,
                FirstTimeFixCount = 125,
                FirstTimeFixRate = 80.1,
                AverageResponseTimeHours = 3.2,
                AverageCompletionTimeHours = 5.5,
                CallbackCount = 18,
                CallbackRate = 11.5,
                QualityScore = 78,
                SafetyIncidents = 1,
                ComplianceRate = 95,
                SatisfactionScore = 3.8,
                OverallPerformanceScore = 76,
                PerformanceRating = "Acceptable",
                RecommendForRenewal = true
            };

            _contractors["CONT-003"] = new ContractorPerformance
            {
                ContractorId = "CONT-003",
                ContractorName = "FireSafe Systems Ltd",
                ContractorType = "Specialist",
                ServiceCategories = new() { "Fire Alarm", "Fire Suppression", "Extinguishers" },
                ContractStart = new DateTime(2024, 4, 1),
                ContractEnd = new DateTime(2027, 3, 31),
                AnnualContractValue = 18000000,
                TotalWorkOrders = 24,
                CompletedOnTime = 24,
                OnTimeCompletionRate = 100,
                FirstTimeFixCount = 23,
                FirstTimeFixRate = 95.8,
                AverageResponseTimeHours = 1.5,
                AverageCompletionTimeHours = 2.2,
                CallbackCount = 0,
                CallbackRate = 0,
                QualityScore = 95,
                SafetyIncidents = 0,
                ComplianceRate = 100,
                SatisfactionScore = 4.7,
                OverallPerformanceScore = 96,
                PerformanceRating = "Excellent",
                RecommendForRenewal = true
            };
        }

        #endregion

        #region KPI Calculation

        /// <summary>
        /// Calculate KPIs for building
        /// </summary>
        public List<FMKpi> CalculateKPIs(string buildingId, FMOperationalData data)
        {
            var kpis = new List<FMKpi>();

            foreach (var (kpiId, definition) in _kpiDefinitions)
            {
                var kpi = new FMKpi
                {
                    KpiId = kpiId,
                    KpiName = definition.KpiName,
                    Category = definition.Category,
                    Unit = definition.Unit,
                    Target = definition.Target,
                    HigherIsBetter = definition.HigherIsBetter,
                    CalculationMethod = definition.CalculationMethod,
                    MeasurementDate = DateTime.UtcNow
                };

                // Calculate current value based on KPI
                kpi.CurrentValue = kpiId switch
                {
                    "MTTR" => data.AverageMTTR,
                    "MTBF" => data.AverageMTBF,
                    "PMCompliance" => data.PMCompletedOnTime > 0 ? data.PMCompletedOnTime / (double)data.PMScheduled * 100 : 0,
                    "WOCompletionRate" => data.TotalWorkOrders > 0 ? data.CompletedWorkOrders / (double)data.TotalWorkOrders * 100 : 0,
                    "FirstTimeFix" => data.CompletedWorkOrders > 0 ? data.FirstTimeFixCount / (double)data.CompletedWorkOrders * 100 : 0,
                    "SLACompliance" => data.TotalWorkOrders > 0 ? data.WithinSLA / (double)data.TotalWorkOrders * 100 : 0,
                    "ReactiveRatio" => data.TotalWorkOrders > 0 ? data.ReactiveWorkOrders / (double)data.TotalWorkOrders * 100 : 0,
                    "EUI" => data.TotalEnergyKWh / data.GrossFloorArea,
                    "CostPerM2" => (double)(data.TotalFMCost / (decimal)data.GrossFloorArea),
                    "OccupantSatisfaction" => data.AverageSatisfactionScore,
                    "EquipmentUptime" => data.CriticalEquipmentUptime,
                    _ => 0
                };

                // Get benchmark
                var benchmarkKey = $"Office-{kpiId}";
                if (_benchmarks.TryGetValue(benchmarkKey, out var benchmark))
                {
                    kpi.IndustryBenchmark = benchmark.Median;
                    kpi.BestInClass = benchmark.BestInClass;
                }

                // Calculate performance vs target
                if (kpi.HigherIsBetter)
                {
                    kpi.PerformanceVsTarget = kpi.Target > 0 ? (kpi.CurrentValue / kpi.Target - 1) * 100 : 0;
                    kpi.PerformanceVsBenchmark = kpi.IndustryBenchmark > 0 ? (kpi.CurrentValue / kpi.IndustryBenchmark - 1) * 100 : 0;
                }
                else
                {
                    kpi.PerformanceVsTarget = kpi.Target > 0 ? (1 - kpi.CurrentValue / kpi.Target) * 100 : 0;
                    kpi.PerformanceVsBenchmark = kpi.IndustryBenchmark > 0 ? (1 - kpi.CurrentValue / kpi.IndustryBenchmark) * 100 : 0;
                }

                // Determine rating
                kpi.PerformanceRating = DeterminePerformanceRating(kpi, definition);

                kpis.Add(kpi);
            }

            return kpis;
        }

        private string DeterminePerformanceRating(FMKpi kpi, FMKpiDefinition definition)
        {
            bool meetingTarget;
            if (kpi.HigherIsBetter)
                meetingTarget = kpi.CurrentValue >= kpi.Target;
            else
                meetingTarget = kpi.CurrentValue <= kpi.Target;

            if (meetingTarget && kpi.PerformanceVsTarget >= 10)
                return "Excellent";
            if (meetingTarget)
                return "Good";
            if (Math.Abs(kpi.PerformanceVsTarget) <= 10)
                return "Acceptable";
            return "Needs Improvement";
        }

        /// <summary>
        /// Get benchmark comparison
        /// </summary>
        public BenchmarkComparison GetBenchmarkComparison(string metricName, double yourValue, string buildingType = "Office")
        {
            var benchmarkKey = $"{buildingType}-{metricName}";
            if (!_benchmarks.TryGetValue(benchmarkKey, out var benchmark))
                return null;

            var comparison = new BenchmarkComparison
            {
                MetricName = metricName,
                YourValue = yourValue,
                PeerMedian = benchmark.Median,
                PeerPercentile25 = benchmark.Percentile25,
                PeerPercentile75 = benchmark.Percentile75,
                BestInClass = benchmark.BestInClass
            };

            // Calculate percentile (simplified linear interpolation)
            var definition = _kpiDefinitions.GetValueOrDefault(metricName);
            bool higherIsBetter = definition?.HigherIsBetter ?? true;

            if (higherIsBetter)
            {
                if (yourValue >= benchmark.BestInClass)
                    comparison.YourPercentile = 95;
                else if (yourValue >= benchmark.Percentile75)
                    comparison.YourPercentile = 75 + (int)((yourValue - benchmark.Percentile75) / (benchmark.BestInClass - benchmark.Percentile75) * 20);
                else if (yourValue >= benchmark.Median)
                    comparison.YourPercentile = 50 + (int)((yourValue - benchmark.Median) / (benchmark.Percentile75 - benchmark.Median) * 25);
                else if (yourValue >= benchmark.Percentile25)
                    comparison.YourPercentile = 25 + (int)((yourValue - benchmark.Percentile25) / (benchmark.Median - benchmark.Percentile25) * 25);
                else
                    comparison.YourPercentile = (int)(yourValue / benchmark.Percentile25 * 25);
            }
            else
            {
                // Lower is better - reverse logic
                if (yourValue <= benchmark.BestInClass)
                    comparison.YourPercentile = 95;
                else if (yourValue <= benchmark.Percentile75)
                    comparison.YourPercentile = 75;
                else if (yourValue <= benchmark.Median)
                    comparison.YourPercentile = 50;
                else if (yourValue <= benchmark.Percentile25)
                    comparison.YourPercentile = 25;
                else
                    comparison.YourPercentile = 10;
            }

            comparison.PerformanceCategory = comparison.YourPercentile >= 75 ? "Top Quartile" :
                                            comparison.YourPercentile >= 50 ? "Above Median" :
                                            comparison.YourPercentile >= 25 ? "Below Median" : "Bottom Quartile";

            return comparison;
        }

        #endregion

        #region Contractor Performance

        /// <summary>
        /// Evaluate contractor performance
        /// </summary>
        public ContractorPerformance EvaluateContractor(string contractorId, ContractorWorkData workData)
        {
            if (!_contractors.TryGetValue(contractorId, out var contractor))
            {
                contractor = new ContractorPerformance { ContractorId = contractorId };
                _contractors[contractorId] = contractor;
            }

            // Update metrics
            contractor.TotalWorkOrders = workData.TotalWorkOrders;
            contractor.CompletedOnTime = workData.CompletedOnTime;
            contractor.OnTimeCompletionRate = workData.TotalWorkOrders > 0 ?
                workData.CompletedOnTime / (double)workData.TotalWorkOrders * 100 : 0;

            contractor.FirstTimeFixCount = workData.FirstTimeFixed;
            contractor.FirstTimeFixRate = workData.TotalWorkOrders > 0 ?
                workData.FirstTimeFixed / (double)workData.TotalWorkOrders * 100 : 0;

            contractor.AverageResponseTimeHours = workData.AverageResponseHours;
            contractor.AverageCompletionTimeHours = workData.AverageCompletionHours;

            contractor.CallbackCount = workData.Callbacks;
            contractor.CallbackRate = workData.TotalWorkOrders > 0 ?
                workData.Callbacks / (double)workData.TotalWorkOrders * 100 : 0;

            contractor.TotalBilledAmount = workData.TotalBilled;
            contractor.AverageCostPerWorkOrder = workData.TotalWorkOrders > 0 ?
                workData.TotalBilled / workData.TotalWorkOrders : 0;

            // Calculate overall score
            contractor.OverallPerformanceScore = CalculateContractorScore(contractor);
            contractor.PerformanceRating = contractor.OverallPerformanceScore >= 90 ? "Excellent" :
                                          contractor.OverallPerformanceScore >= 80 ? "Good" :
                                          contractor.OverallPerformanceScore >= 65 ? "Acceptable" : "Poor";

            contractor.RecommendForRenewal = contractor.OverallPerformanceScore >= 70 && contractor.SafetyIncidents == 0;

            return contractor;
        }

        private double CalculateContractorScore(ContractorPerformance contractor)
        {
            double score = 0;

            // On-time completion (25%)
            score += Math.Min(25, contractor.OnTimeCompletionRate * 0.25);

            // First time fix (20%)
            score += Math.Min(20, contractor.FirstTimeFixRate * 0.20);

            // Response time (15%) - assume 4 hours is target
            var responseScore = contractor.AverageResponseTimeHours <= 2 ? 15 :
                               contractor.AverageResponseTimeHours <= 4 ? 12 :
                               contractor.AverageResponseTimeHours <= 8 ? 8 : 4;
            score += responseScore;

            // Callback rate (15%) - lower is better
            var callbackScore = contractor.CallbackRate <= 5 ? 15 :
                               contractor.CallbackRate <= 10 ? 12 :
                               contractor.CallbackRate <= 15 ? 8 : 4;
            score += callbackScore;

            // Quality score (15%)
            score += contractor.QualityScore * 0.15;

            // Safety (10%)
            score += contractor.SafetyIncidents == 0 ? 10 : contractor.SafetyIncidents == 1 ? 5 : 0;

            return Math.Min(100, score);
        }

        /// <summary>
        /// Get all contractors
        /// </summary>
        public List<ContractorPerformance> GetAllContractors()
        {
            return _contractors.Values.OrderByDescending(c => c.OverallPerformanceScore).ToList();
        }

        /// <summary>
        /// Get contractor scorecard
        /// </summary>
        public ContractorScorecard GetContractorScorecard(string contractorId)
        {
            if (!_contractors.TryGetValue(contractorId, out var contractor))
                return null;

            return new ContractorScorecard
            {
                Contractor = contractor,
                StrengthAreas = GetContractorStrengths(contractor),
                ImprovementAreas = GetContractorImprovements(contractor),
                Recommendations = GetContractorRecommendations(contractor),
                BenchmarkComparisons = new List<BenchmarkComparison>
                {
                    new BenchmarkComparison
                    {
                        MetricName = "On-Time Completion",
                        YourValue = contractor.OnTimeCompletionRate,
                        PeerMedian = 85,
                        BestInClass = 98,
                        PerformanceCategory = contractor.OnTimeCompletionRate >= 90 ? "Good" : "Needs Improvement"
                    },
                    new BenchmarkComparison
                    {
                        MetricName = "First Time Fix Rate",
                        YourValue = contractor.FirstTimeFixRate,
                        PeerMedian = 80,
                        BestInClass = 95,
                        PerformanceCategory = contractor.FirstTimeFixRate >= 85 ? "Good" : "Needs Improvement"
                    }
                }
            };
        }

        private List<string> GetContractorStrengths(ContractorPerformance contractor)
        {
            var strengths = new List<string>();
            if (contractor.OnTimeCompletionRate >= 95) strengths.Add("Excellent on-time performance");
            if (contractor.FirstTimeFixRate >= 90) strengths.Add("High first-time fix rate");
            if (contractor.SafetyIncidents == 0) strengths.Add("Perfect safety record");
            if (contractor.SatisfactionScore >= 4.5) strengths.Add("High customer satisfaction");
            if (contractor.CallbackRate <= 5) strengths.Add("Low callback rate");
            return strengths;
        }

        private List<string> GetContractorImprovements(ContractorPerformance contractor)
        {
            var improvements = new List<string>();
            if (contractor.OnTimeCompletionRate < 85) improvements.Add("On-time completion rate below target");
            if (contractor.FirstTimeFixRate < 80) improvements.Add("First time fix rate needs improvement");
            if (contractor.AverageResponseTimeHours > 4) improvements.Add("Response time exceeds SLA");
            if (contractor.CallbackRate > 10) improvements.Add("High callback rate indicating quality issues");
            if (contractor.DocumentationScore < 80) improvements.Add("Documentation completeness needs improvement");
            return improvements;
        }

        private List<string> GetContractorRecommendations(ContractorPerformance contractor)
        {
            var recommendations = new List<string>();

            if (contractor.OverallPerformanceScore >= 90)
                recommendations.Add("Consider for expanded scope of work");
            else if (contractor.OverallPerformanceScore >= 80)
                recommendations.Add("Maintain current service level agreement");
            else if (contractor.OverallPerformanceScore >= 65)
            {
                recommendations.Add("Implement performance improvement plan");
                recommendations.Add("Schedule monthly performance reviews");
            }
            else
            {
                recommendations.Add("Issue formal performance warning");
                recommendations.Add("Evaluate alternative service providers");
            }

            if (contractor.CallbackRate > 10)
                recommendations.Add("Require quality audit of completed work");

            return recommendations;
        }

        #endregion

        #region Operational Scorecard

        /// <summary>
        /// Generate operational scorecard
        /// </summary>
        public OperationalScorecard GenerateScorecard(string buildingId, FMOperationalData data)
        {
            var kpis = CalculateKPIs(buildingId, data);

            var scorecard = new OperationalScorecard
            {
                BuildingId = buildingId,
                BuildingName = data.BuildingName,
                PeriodStart = data.PeriodStart,
                PeriodEnd = data.PeriodEnd,
                KeyMetrics = kpis
            };

            // Calculate category scores
            scorecard.MaintenanceScore = CalculateCategoryScore(kpis, "Maintenance");
            scorecard.EnergyScore = CalculateCategoryScore(kpis, "Energy");
            scorecard.ComfortScore = data.AverageSatisfactionScore * 20; // Convert 1-5 to 0-100
            scorecard.SafetyScore = data.SafetyIncidents == 0 ? 100 : Math.Max(0, 100 - data.SafetyIncidents * 20);
            scorecard.CostScore = CalculateCategoryScore(kpis, "Cost");
            scorecard.SustainabilityScore = 75; // Placeholder

            // Overall score (weighted average)
            scorecard.OverallScore =
                scorecard.MaintenanceScore * 0.25 +
                scorecard.EnergyScore * 0.20 +
                scorecard.ComfortScore * 0.15 +
                scorecard.SafetyScore * 0.15 +
                scorecard.CostScore * 0.15 +
                scorecard.SustainabilityScore * 0.10;

            scorecard.OverallRating = scorecard.OverallScore >= 90 ? "Excellent" :
                                     scorecard.OverallScore >= 80 ? "Good" :
                                     scorecard.OverallScore >= 70 ? "Acceptable" :
                                     scorecard.OverallScore >= 60 ? "Needs Improvement" : "Poor";

            // Identify strengths and improvement areas
            var categoryScores = new Dictionary<string, double>
            {
                ["Maintenance"] = scorecard.MaintenanceScore,
                ["Energy"] = scorecard.EnergyScore,
                ["Comfort"] = scorecard.ComfortScore,
                ["Safety"] = scorecard.SafetyScore,
                ["Cost"] = scorecard.CostScore,
                ["Sustainability"] = scorecard.SustainabilityScore
            };

            scorecard.StrengthAreas = categoryScores.Where(c => c.Value >= 85).Select(c => c.Key).ToList();
            scorecard.ImprovementAreas = categoryScores.Where(c => c.Value < 70).Select(c => c.Key).ToList();

            // Generate recommendations
            scorecard.Recommendations = GenerateScorecardRecommendations(scorecard, kpis);

            return scorecard;
        }

        private double CalculateCategoryScore(List<FMKpi> kpis, string category)
        {
            var categoryKpis = kpis.Where(k => k.Category == category).ToList();
            if (!categoryKpis.Any()) return 75; // Default

            var score = 0.0;
            foreach (var kpi in categoryKpis)
            {
                var kpiScore = kpi.PerformanceRating switch
                {
                    "Excellent" => 95,
                    "Good" => 85,
                    "Acceptable" => 70,
                    _ => 50
                };
                score += kpiScore;
            }

            return score / categoryKpis.Count;
        }

        private List<string> GenerateScorecardRecommendations(OperationalScorecard scorecard, List<FMKpi> kpis)
        {
            var recommendations = new List<string>();

            if (scorecard.MaintenanceScore < 75)
                recommendations.Add("Focus on improving preventive maintenance compliance");

            if (scorecard.EnergyScore < 75)
                recommendations.Add("Implement energy optimization initiatives");

            var reactiveKpi = kpis.FirstOrDefault(k => k.KpiId == "ReactiveRatio");
            if (reactiveKpi != null && reactiveKpi.CurrentValue > 30)
                recommendations.Add("Shift from reactive to preventive maintenance strategy");

            var mttrKpi = kpis.FirstOrDefault(k => k.KpiId == "MTTR");
            if (mttrKpi != null && mttrKpi.CurrentValue > 6)
                recommendations.Add("Improve work order response and completion times");

            if (scorecard.ComfortScore < 70)
                recommendations.Add("Address occupant comfort issues through IEQ improvements");

            return recommendations;
        }

        #endregion

        #endregion // Benchmarking Engine
    }

    #region Supporting Classes

    public class FMKpiDefinition
    {
        public string KpiId { get; set; } = string.Empty;
        public string KpiName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public bool HigherIsBetter { get; set; }
        public double Target { get; set; }
        public double WarningThreshold { get; set; }
        public double CriticalThreshold { get; set; }
        public string CalculationMethod { get; set; } = string.Empty;
    }

    public class IndustryBenchmark
    {
        public string MetricName { get; set; } = string.Empty;
        public string BuildingType { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public double Percentile25 { get; set; }
        public double Median { get; set; }
        public double Percentile75 { get; set; }
        public double BestInClass { get; set; }
    }

    public class FMOperationalData
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double GrossFloorArea { get; set; }

        // Work order metrics
        public int TotalWorkOrders { get; set; }
        public int CompletedWorkOrders { get; set; }
        public int ReactiveWorkOrders { get; set; }
        public int PMScheduled { get; set; }
        public int PMCompletedOnTime { get; set; }
        public int FirstTimeFixCount { get; set; }
        public int WithinSLA { get; set; }

        // Time metrics
        public double AverageMTTR { get; set; }
        public double AverageMTBF { get; set; }
        public double CriticalEquipmentUptime { get; set; }

        // Energy
        public double TotalEnergyKWh { get; set; }

        // Cost
        public decimal TotalFMCost { get; set; }

        // Satisfaction
        public double AverageSatisfactionScore { get; set; }

        // Safety
        public int SafetyIncidents { get; set; }
    }

    public class ContractorWorkData
    {
        public int TotalWorkOrders { get; set; }
        public int CompletedOnTime { get; set; }
        public int FirstTimeFixed { get; set; }
        public double AverageResponseHours { get; set; }
        public double AverageCompletionHours { get; set; }
        public int Callbacks { get; set; }
        public decimal TotalBilled { get; set; }
    }

    public class ContractorScorecard
    {
        public ContractorPerformance Contractor { get; set; }
        public List<string> StrengthAreas { get; set; } = new();
        public List<string> ImprovementAreas { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<BenchmarkComparison> BenchmarkComparisons { get; set; } = new();
    }

    #endregion
}
