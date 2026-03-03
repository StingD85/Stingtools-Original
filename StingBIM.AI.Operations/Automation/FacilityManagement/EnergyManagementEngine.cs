// =========================================================================
// StingBIM.AI.Automation - Energy Management Engine
// Comprehensive energy monitoring, analysis, and optimization for facilities
// =========================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Automation.FacilityManagement
{
    /// <summary>
    /// Manages energy consumption monitoring, analysis, optimization, and reporting.
    /// Integrates with BIM for energy modeling and supports sustainability goals.
    /// </summary>
    public class EnergyManagementEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly EnergyDataCollector _dataCollector;
        private readonly ConsumptionAnalyzer _consumptionAnalyzer;
        private readonly LoadProfiler _loadProfiler;
        private readonly CostCalculator _costCalculator;
        private readonly OptimizationEngine _optimizationEngine;
        private readonly CarbonCalculator _carbonCalculator;
        private readonly BenchmarkEngine _benchmarkEngine;
        private readonly AlertManager _alertManager;

        private readonly Dictionary<string, EnergyMeter> _meters;
        private readonly Dictionary<string, EnergyTariff> _tariffs;
        private readonly Dictionary<string, CarbonFactor> _carbonFactors;

        public EnergyManagementEngine()
        {
            _dataCollector = new EnergyDataCollector();
            _consumptionAnalyzer = new ConsumptionAnalyzer();
            _loadProfiler = new LoadProfiler();
            _costCalculator = new CostCalculator();
            _optimizationEngine = new OptimizationEngine();
            _carbonCalculator = new CarbonCalculator();
            _benchmarkEngine = new BenchmarkEngine();
            _alertManager = new AlertManager();

            _meters = new Dictionary<string, EnergyMeter>();
            _tariffs = InitializeTariffs();
            _carbonFactors = InitializeCarbonFactors();

            Logger.Info("EnergyManagementEngine initialized successfully");
        }

        #region Energy Monitoring

        /// <summary>
        /// Registers an energy meter for monitoring.
        /// </summary>
        public async Task<MeterRegistrationResult> RegisterMeterAsync(
            MeterConfiguration config,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Registering meter: {config.MeterId}");

            var result = new MeterRegistrationResult
            {
                MeterId = config.MeterId,
                RegistrationTime = DateTime.UtcNow
            };

            try
            {
                var meter = new EnergyMeter
                {
                    Id = config.MeterId,
                    Name = config.Name,
                    Type = config.MeterType,
                    Location = config.Location,
                    Unit = config.Unit,
                    Resolution = config.Resolution,
                    LinkedSpaceId = config.LinkedSpaceId,
                    LinkedSystemId = config.LinkedSystemId,
                    TariffId = config.TariffId,
                    IsActive = true,
                    RegisteredDate = DateTime.UtcNow
                };

                _meters[meter.Id] = meter;
                await _dataCollector.InitializeMeterAsync(meter, cancellationToken);

                result.Success = true;
                Logger.Info($"Meter {config.MeterId} registered successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error registering meter: {config.MeterId}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Records energy consumption data from a meter.
        /// </summary>
        public async Task<ConsumptionRecordResult> RecordConsumptionAsync(
            string meterId,
            double value,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            var result = new ConsumptionRecordResult
            {
                MeterId = meterId,
                Timestamp = timestamp
            };

            try
            {
                if (!_meters.TryGetValue(meterId, out var meter))
                {
                    throw new ArgumentException($"Meter not found: {meterId}");
                }

                var reading = new MeterReading
                {
                    Id = Guid.NewGuid().ToString(),
                    MeterId = meterId,
                    Value = value,
                    Unit = meter.Unit,
                    Timestamp = timestamp,
                    ReadingType = ReadingType.Actual
                };

                await _dataCollector.StoreReadingAsync(reading, cancellationToken);

                // Check for alerts
                await _alertManager.CheckThresholdsAsync(meter, reading, cancellationToken);

                // Update running totals
                await UpdateRunningTotalsAsync(meter, reading, cancellationToken);

                result.Success = true;
                result.RecordedValue = value;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error recording consumption for meter: {meterId}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Gets real-time consumption data.
        /// </summary>
        public async Task<RealTimeConsumptionData> GetRealTimeConsumptionAsync(
            string meterId = null,
            CancellationToken cancellationToken = default)
        {
            var data = new RealTimeConsumptionData
            {
                Timestamp = DateTime.UtcNow
            };

            var metersToQuery = meterId != null
                ? _meters.Values.Where(m => m.Id == meterId)
                : _meters.Values.Where(m => m.IsActive);

            foreach (var meter in metersToQuery)
            {
                var latestReading = await _dataCollector.GetLatestReadingAsync(meter.Id, cancellationToken);
                if (latestReading != null)
                {
                    data.Readings.Add(new RealTimeReading
                    {
                        MeterId = meter.Id,
                        MeterName = meter.Name,
                        CurrentValue = latestReading.Value,
                        Unit = latestReading.Unit,
                        Timestamp = latestReading.Timestamp,
                        TrendDirection = CalculateTrend(meter.Id)
                    });
                }
            }

            // Calculate totals
            data.TotalElectricity = data.Readings
                .Where(r => r.Unit == "kWh" || r.Unit == "kW")
                .Sum(r => r.CurrentValue);

            data.TotalGas = data.Readings
                .Where(r => r.Unit == "m³" || r.Unit == "therms")
                .Sum(r => r.CurrentValue);

            data.TotalWater = data.Readings
                .Where(r => r.Unit == "liters" || r.Unit == "gallons")
                .Sum(r => r.CurrentValue);

            return data;
        }

        #endregion

        #region Consumption Analysis

        /// <summary>
        /// Analyzes energy consumption patterns.
        /// </summary>
        public async Task<ConsumptionAnalysisResult> AnalyzeConsumptionAsync(
            ConsumptionAnalysisRequest request,
            IProgress<AnalysisProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Analyzing consumption for period: {request.StartDate} to {request.EndDate}");

            var result = new ConsumptionAnalysisResult
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                AnalysisTime = DateTime.UtcNow
            };

            try
            {
                // Collect data
                progress?.Report(new AnalysisProgress { Stage = "Collecting Data", Percentage = 10 });
                var readings = await _dataCollector.GetReadingsAsync(
                    request.MeterIds, request.StartDate, request.EndDate, cancellationToken);

                // Calculate totals
                progress?.Report(new AnalysisProgress { Stage = "Calculating Totals", Percentage = 25 });
                result.TotalConsumption = CalculateTotalConsumption(readings);
                result.ConsumptionByMeter = CalculateConsumptionByMeter(readings);
                result.ConsumptionByType = CalculateConsumptionByType(readings);

                // Analyze patterns
                progress?.Report(new AnalysisProgress { Stage = "Analyzing Patterns", Percentage = 45 });
                result.DailyPattern = await _consumptionAnalyzer.AnalyzeDailyPatternAsync(readings, cancellationToken);
                result.WeeklyPattern = await _consumptionAnalyzer.AnalyzeWeeklyPatternAsync(readings, cancellationToken);

                // Identify peaks
                progress?.Report(new AnalysisProgress { Stage = "Identifying Peaks", Percentage = 60 });
                result.PeakDemand = await _loadProfiler.IdentifyPeaksAsync(readings, cancellationToken);
                result.BaseLoad = await _loadProfiler.CalculateBaseLoadAsync(readings, cancellationToken);

                // Calculate costs
                progress?.Report(new AnalysisProgress { Stage = "Calculating Costs", Percentage = 75 });
                result.TotalCost = await _costCalculator.CalculateTotalCostAsync(readings, _tariffs, cancellationToken);
                result.CostBreakdown = await _costCalculator.CalculateCostBreakdownAsync(readings, _tariffs, cancellationToken);

                // Calculate carbon
                progress?.Report(new AnalysisProgress { Stage = "Carbon Analysis", Percentage = 90 });
                result.CarbonEmissions = await _carbonCalculator.CalculateEmissionsAsync(readings, _carbonFactors, cancellationToken);

                result.Success = true;
                progress?.Report(new AnalysisProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info($"Consumption analysis completed. Total: {result.TotalConsumption.Value} {result.TotalConsumption.Unit}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing consumption");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates load profile for the facility.
        /// </summary>
        public async Task<LoadProfileResult> GenerateLoadProfileAsync(
            LoadProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating load profile for: {request.Period}");

            var result = new LoadProfileResult
            {
                Period = request.Period,
                GeneratedTime = DateTime.UtcNow
            };

            try
            {
                var readings = await _dataCollector.GetReadingsAsync(
                    request.MeterIds, request.StartDate, request.EndDate, cancellationToken);

                // Generate hourly profile
                result.HourlyProfile = GenerateHourlyProfile(readings);

                // Generate daily profile
                result.DailyProfile = GenerateDailyProfile(readings);

                // Calculate statistics
                result.PeakDemand = result.HourlyProfile.Max(p => p.Value);
                result.MinDemand = result.HourlyProfile.Min(p => p.Value);
                result.AverageDemand = result.HourlyProfile.Average(p => p.Value);
                result.LoadFactor = result.AverageDemand / result.PeakDemand;

                // Identify patterns
                result.PeakHours = IdentifyPeakHours(result.HourlyProfile);
                result.OffPeakHours = IdentifyOffPeakHours(result.HourlyProfile);

                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating load profile");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Compares consumption against benchmarks.
        /// </summary>
        public async Task<BenchmarkResult> BenchmarkConsumptionAsync(
            BenchmarkRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Benchmarking consumption for building type: {request.BuildingType}");

            var result = new BenchmarkResult
            {
                BuildingType = request.BuildingType,
                AnalysisDate = DateTime.UtcNow
            };

            try
            {
                // Get consumption data
                var totalConsumption = await GetTotalConsumptionAsync(
                    request.StartDate, request.EndDate, cancellationToken);

                // Calculate EUI (Energy Use Intensity)
                result.EUI = totalConsumption / request.GrossFloorArea;
                result.EUIUnit = "kWh/m²/year";

                // Get benchmark values
                var benchmarks = await _benchmarkEngine.GetBenchmarksAsync(
                    request.BuildingType, request.Climate, request.Location, cancellationToken);

                result.MedianBenchmark = benchmarks.Median;
                result.Top25Benchmark = benchmarks.Top25;
                result.TargetBenchmark = benchmarks.Target;

                // Calculate percentile
                result.Percentile = await _benchmarkEngine.CalculatePercentileAsync(
                    result.EUI, benchmarks, cancellationToken);

                // Calculate potential savings
                if (result.EUI > benchmarks.Median)
                {
                    result.PotentialSavings = (result.EUI - benchmarks.Median) * request.GrossFloorArea;
                    result.SavingsPercentage = ((result.EUI - benchmarks.Median) / result.EUI) * 100;
                }

                // Assign rating
                result.Rating = AssignEnergyRating(result.Percentile);

                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error benchmarking consumption");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Energy Optimization

        /// <summary>
        /// Identifies energy saving opportunities.
        /// </summary>
        public async Task<SavingsOpportunityResult> IdentifySavingsOpportunitiesAsync(
            SavingsAnalysisRequest request,
            IProgress<AnalysisProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Identifying energy saving opportunities");

            var result = new SavingsOpportunityResult
            {
                AnalysisDate = DateTime.UtcNow
            };

            try
            {
                // Analyze consumption patterns
                progress?.Report(new AnalysisProgress { Stage = "Analyzing Patterns", Percentage = 20 });
                var patterns = await _consumptionAnalyzer.AnalyzePatternsAsync(
                    request.StartDate, request.EndDate, cancellationToken);

                // Check for scheduling opportunities
                progress?.Report(new AnalysisProgress { Stage = "Schedule Analysis", Percentage = 35 });
                var scheduleOps = await IdentifyScheduleOpportunitiesAsync(patterns, cancellationToken);
                result.Opportunities.AddRange(scheduleOps);

                // Check for equipment efficiency
                progress?.Report(new AnalysisProgress { Stage = "Equipment Analysis", Percentage = 50 });
                var equipmentOps = await IdentifyEquipmentOpportunitiesAsync(patterns, cancellationToken);
                result.Opportunities.AddRange(equipmentOps);

                // Check for load shifting opportunities
                progress?.Report(new AnalysisProgress { Stage = "Load Shifting", Percentage = 65 });
                var loadShiftOps = await IdentifyLoadShiftingOpportunitiesAsync(patterns, _tariffs, cancellationToken);
                result.Opportunities.AddRange(loadShiftOps);

                // Check for waste elimination
                progress?.Report(new AnalysisProgress { Stage = "Waste Analysis", Percentage = 80 });
                var wasteOps = await IdentifyWasteEliminationOpportunitiesAsync(patterns, cancellationToken);
                result.Opportunities.AddRange(wasteOps);

                // Calculate totals
                progress?.Report(new AnalysisProgress { Stage = "Calculating Totals", Percentage = 95 });
                result.TotalPotentialSavings = result.Opportunities.Sum(o => o.AnnualSavings);
                result.TotalImplementationCost = result.Opportunities.Sum(o => o.ImplementationCost);
                result.AveragePaybackPeriod = result.TotalImplementationCost / result.TotalPotentialSavings;

                // Prioritize opportunities
                result.Opportunities = result.Opportunities
                    .OrderByDescending(o => o.ROI)
                    .ThenBy(o => o.PaybackPeriod)
                    .ToList();

                result.Success = true;
                progress?.Report(new AnalysisProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info($"Identified {result.Opportunities.Count} saving opportunities, " +
                           $"total potential: ${result.TotalPotentialSavings:N2}/year");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error identifying savings opportunities");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates optimized operation schedule.
        /// </summary>
        public async Task<OptimizedScheduleResult> OptimizeOperationScheduleAsync(
            ScheduleOptimizationRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Optimizing operation schedule");

            var result = new OptimizedScheduleResult
            {
                OptimizationDate = DateTime.UtcNow
            };

            try
            {
                // Get current schedule and consumption
                var currentConsumption = await GetConsumptionByScheduleAsync(
                    request.CurrentSchedule, cancellationToken);

                // Optimize HVAC schedules
                result.OptimizedHVACSchedule = await _optimizationEngine.OptimizeHVACScheduleAsync(
                    request.CurrentSchedule.HVAC,
                    request.OccupancyPattern,
                    request.ComfortRequirements,
                    cancellationToken);

                // Optimize lighting schedules
                result.OptimizedLightingSchedule = await _optimizationEngine.OptimizeLightingScheduleAsync(
                    request.CurrentSchedule.Lighting,
                    request.OccupancyPattern,
                    request.DaylightAvailability,
                    cancellationToken);

                // Calculate projected savings
                var projectedConsumption = await CalculateProjectedConsumptionAsync(
                    result.OptimizedHVACSchedule,
                    result.OptimizedLightingSchedule,
                    cancellationToken);

                result.ProjectedSavings = currentConsumption - projectedConsumption;
                result.SavingsPercentage = (result.ProjectedSavings / currentConsumption) * 100;

                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error optimizing schedule");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Recommends demand response actions.
        /// </summary>
        public async Task<DemandResponseRecommendation> GetDemandResponseRecommendationAsync(
            DemandResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Getting demand response recommendations for target: {request.ReductionTarget} kW");

            var recommendation = new DemandResponseRecommendation
            {
                RequestTime = DateTime.UtcNow,
                ReductionTarget = request.ReductionTarget
            };

            try
            {
                // Get current loads
                var currentLoads = await GetCurrentLoadsAsync(cancellationToken);

                // Identify sheddable loads
                var sheddableLoads = await IdentifySheddableLoadsAsync(
                    currentLoads, request.Constraints, cancellationToken);

                // Prioritize by impact and comfort
                var prioritized = sheddableLoads
                    .OrderByDescending(l => l.ReductionPotential / l.ComfortImpact)
                    .ToList();

                // Build recommendation
                double accumulated = 0;
                foreach (var load in prioritized)
                {
                    if (accumulated >= request.ReductionTarget) break;

                    recommendation.Actions.Add(new DemandResponseAction
                    {
                        SystemId = load.SystemId,
                        SystemName = load.SystemName,
                        ActionType = load.RecommendedAction,
                        ReductionAmount = load.ReductionPotential,
                        ComfortImpact = load.ComfortImpact,
                        Duration = request.Duration
                    });

                    accumulated += load.ReductionPotential;
                }

                recommendation.AchievableReduction = accumulated;
                recommendation.ConfidenceLevel = accumulated >= request.ReductionTarget ? 1.0 : accumulated / request.ReductionTarget;
                recommendation.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting demand response recommendations");
                recommendation.Success = false;
                recommendation.ErrorMessage = ex.Message;
            }

            return recommendation;
        }

        #endregion

        #region Carbon and Sustainability

        /// <summary>
        /// Calculates carbon footprint.
        /// </summary>
        public async Task<CarbonFootprintResult> CalculateCarbonFootprintAsync(
            CarbonCalculationRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Calculating carbon footprint for period: {request.StartDate} to {request.EndDate}");

            var result = new CarbonFootprintResult
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CalculationDate = DateTime.UtcNow
            };

            try
            {
                // Get consumption data
                var readings = await _dataCollector.GetReadingsAsync(
                    null, request.StartDate, request.EndDate, cancellationToken);

                // Calculate Scope 1 emissions (direct)
                result.Scope1Emissions = await _carbonCalculator.CalculateScope1Async(
                    readings.Where(r => IsDirectFuel(r.Unit)), _carbonFactors, cancellationToken);

                // Calculate Scope 2 emissions (indirect - electricity)
                result.Scope2Emissions = await _carbonCalculator.CalculateScope2Async(
                    readings.Where(r => r.Unit == "kWh"), _carbonFactors, request.GridRegion, cancellationToken);

                // Calculate total
                result.TotalEmissions = result.Scope1Emissions + result.Scope2Emissions;

                // Calculate intensity
                if (request.GrossFloorArea > 0)
                {
                    result.CarbonIntensity = result.TotalEmissions / request.GrossFloorArea;
                    result.IntensityUnit = "kgCO2e/m²";
                }

                // Compare to baseline
                if (request.BaselineYear.HasValue)
                {
                    var baseline = await GetBaselineEmissionsAsync(request.BaselineYear.Value, cancellationToken);
                    result.ChangeFromBaseline = ((result.TotalEmissions - baseline) / baseline) * 100;
                }

                // Get breakdown
                result.EmissionsBySource = await _carbonCalculator.GetBreakdownBySourceAsync(
                    readings, _carbonFactors, cancellationToken);

                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error calculating carbon footprint");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates sustainability report.
        /// </summary>
        public async Task<SustainabilityReport> GenerateSustainabilityReportAsync(
            SustainabilityReportRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating sustainability report for: {request.ReportPeriod}");

            var report = new SustainabilityReport
            {
                ReportPeriod = request.ReportPeriod,
                GeneratedDate = DateTime.UtcNow
            };

            try
            {
                // Energy metrics
                report.EnergyMetrics = await CalculateEnergyMetricsAsync(request, cancellationToken);

                // Carbon metrics
                report.CarbonMetrics = await CalculateCarbonMetricsAsync(request, cancellationToken);

                // Water metrics
                report.WaterMetrics = await CalculateWaterMetricsAsync(request, cancellationToken);

                // Progress against targets
                report.TargetProgress = await CalculateTargetProgressAsync(
                    request.Targets, report.EnergyMetrics, report.CarbonMetrics, cancellationToken);

                // Recommendations
                report.Recommendations = await GenerateSustainabilityRecommendationsAsync(
                    report.EnergyMetrics, report.CarbonMetrics, request.Targets, cancellationToken);

                report.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating sustainability report");
                report.Success = false;
                report.ErrorMessage = ex.Message;
            }

            return report;
        }

        #endregion

        #region Alerts and Notifications

        /// <summary>
        /// Configures consumption alerts.
        /// </summary>
        public async Task<AlertConfigResult> ConfigureAlertAsync(
            AlertConfiguration config,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Configuring alert: {config.AlertName}");

            var result = new AlertConfigResult
            {
                AlertId = config.AlertId ?? Guid.NewGuid().ToString(),
                ConfigTime = DateTime.UtcNow
            };

            try
            {
                var alert = new EnergyAlert
                {
                    Id = result.AlertId,
                    Name = config.AlertName,
                    MeterId = config.MeterId,
                    ThresholdType = config.ThresholdType,
                    ThresholdValue = config.ThresholdValue,
                    ComparisonPeriod = config.ComparisonPeriod,
                    NotificationChannels = config.NotificationChannels,
                    IsActive = true
                };

                await _alertManager.RegisterAlertAsync(alert, cancellationToken);

                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error configuring alert");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Gets active alerts.
        /// </summary>
        public async Task<List<ActiveAlert>> GetActiveAlertsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _alertManager.GetActiveAlertsAsync(cancellationToken);
        }

        #endregion

        #region Helper Methods

        private Dictionary<string, EnergyTariff> InitializeTariffs()
        {
            return new Dictionary<string, EnergyTariff>
            {
                ["STANDARD"] = new EnergyTariff
                {
                    Id = "STANDARD",
                    Name = "Standard Rate",
                    RatePerUnit = 0.12,
                    Unit = "kWh",
                    Currency = "USD"
                },
                ["TOU_PEAK"] = new EnergyTariff
                {
                    Id = "TOU_PEAK",
                    Name = "Time-of-Use Peak",
                    RatePerUnit = 0.18,
                    Unit = "kWh",
                    Currency = "USD",
                    PeakHours = new[] { 14, 15, 16, 17, 18, 19 }
                },
                ["TOU_OFFPEAK"] = new EnergyTariff
                {
                    Id = "TOU_OFFPEAK",
                    Name = "Time-of-Use Off-Peak",
                    RatePerUnit = 0.08,
                    Unit = "kWh",
                    Currency = "USD"
                },
                ["GAS"] = new EnergyTariff
                {
                    Id = "GAS",
                    Name = "Natural Gas",
                    RatePerUnit = 0.80,
                    Unit = "therm",
                    Currency = "USD"
                }
            };
        }

        private Dictionary<string, CarbonFactor> InitializeCarbonFactors()
        {
            return new Dictionary<string, CarbonFactor>
            {
                ["GRID_US_AVG"] = new CarbonFactor { Id = "GRID_US_AVG", Factor = 0.42, Unit = "kgCO2e/kWh" },
                ["GRID_UK"] = new CarbonFactor { Id = "GRID_UK", Factor = 0.23, Unit = "kgCO2e/kWh" },
                ["GRID_EU"] = new CarbonFactor { Id = "GRID_EU", Factor = 0.30, Unit = "kgCO2e/kWh" },
                ["NATURAL_GAS"] = new CarbonFactor { Id = "NATURAL_GAS", Factor = 2.0, Unit = "kgCO2e/m³" },
                ["DIESEL"] = new CarbonFactor { Id = "DIESEL", Factor = 2.68, Unit = "kgCO2e/liter" }
            };
        }

        private async Task UpdateRunningTotalsAsync(EnergyMeter meter, MeterReading reading, CancellationToken ct)
        {
            // Update daily, monthly, yearly totals
        }

        private TrendDirection CalculateTrend(string meterId)
        {
            return TrendDirection.Stable;
        }

        private ConsumptionValue CalculateTotalConsumption(List<MeterReading> readings)
        {
            return new ConsumptionValue
            {
                Value = readings.Sum(r => r.Value),
                Unit = "kWh"
            };
        }

        private Dictionary<string, ConsumptionValue> CalculateConsumptionByMeter(List<MeterReading> readings)
        {
            return readings.GroupBy(r => r.MeterId)
                .ToDictionary(
                    g => g.Key,
                    g => new ConsumptionValue { Value = g.Sum(r => r.Value), Unit = g.First().Unit }
                );
        }

        private Dictionary<string, ConsumptionValue> CalculateConsumptionByType(List<MeterReading> readings)
        {
            return readings.GroupBy(r => r.Unit)
                .ToDictionary(
                    g => g.Key,
                    g => new ConsumptionValue { Value = g.Sum(r => r.Value), Unit = g.Key }
                );
        }

        private List<ProfilePoint> GenerateHourlyProfile(List<MeterReading> readings)
        {
            return readings
                .GroupBy(r => r.Timestamp.Hour)
                .Select(g => new ProfilePoint { Hour = g.Key, Value = g.Average(r => r.Value) })
                .OrderBy(p => p.Hour)
                .ToList();
        }

        private List<DailyProfile> GenerateDailyProfile(List<MeterReading> readings)
        {
            return readings
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new DailyProfile { Date = g.Key, TotalConsumption = g.Sum(r => r.Value) })
                .OrderBy(p => p.Date)
                .ToList();
        }

        private List<int> IdentifyPeakHours(List<ProfilePoint> profile)
        {
            var avg = profile.Average(p => p.Value);
            return profile.Where(p => p.Value > avg * 1.2).Select(p => p.Hour).ToList();
        }

        private List<int> IdentifyOffPeakHours(List<ProfilePoint> profile)
        {
            var avg = profile.Average(p => p.Value);
            return profile.Where(p => p.Value < avg * 0.8).Select(p => p.Hour).ToList();
        }

        private async Task<double> GetTotalConsumptionAsync(DateTime start, DateTime end, CancellationToken ct)
        {
            var readings = await _dataCollector.GetReadingsAsync(null, start, end, ct);
            return readings.Sum(r => r.Value);
        }

        private EnergyRating AssignEnergyRating(double percentile)
        {
            if (percentile <= 25) return EnergyRating.A;
            if (percentile <= 50) return EnergyRating.B;
            if (percentile <= 75) return EnergyRating.C;
            if (percentile <= 90) return EnergyRating.D;
            return EnergyRating.E;
        }

        private async Task<List<SavingsOpportunity>> IdentifyScheduleOpportunitiesAsync(
            ConsumptionPatterns patterns, CancellationToken ct)
        {
            var opportunities = new List<SavingsOpportunity>();
            // Check for after-hours consumption
            if (patterns.AfterHoursConsumption > patterns.TotalConsumption * 0.2)
            {
                opportunities.Add(new SavingsOpportunity
                {
                    Category = "Scheduling",
                    Description = "Reduce after-hours energy consumption",
                    AnnualSavings = patterns.AfterHoursConsumption * 0.5 * 0.12 * 365,
                    ImplementationCost = 0,
                    PaybackPeriod = 0,
                    ROI = double.MaxValue,
                    Priority = Priority.High
                });
            }
            return opportunities;
        }

        private async Task<List<SavingsOpportunity>> IdentifyEquipmentOpportunitiesAsync(
            ConsumptionPatterns patterns, CancellationToken ct)
        {
            var opportunities = new List<SavingsOpportunity>();
            // Check for inefficient equipment
            return opportunities;
        }

        private async Task<List<SavingsOpportunity>> IdentifyLoadShiftingOpportunitiesAsync(
            ConsumptionPatterns patterns, Dictionary<string, EnergyTariff> tariffs, CancellationToken ct)
        {
            var opportunities = new List<SavingsOpportunity>();
            // Check for load shifting opportunities
            return opportunities;
        }

        private async Task<List<SavingsOpportunity>> IdentifyWasteEliminationOpportunitiesAsync(
            ConsumptionPatterns patterns, CancellationToken ct)
        {
            var opportunities = new List<SavingsOpportunity>();
            // Check for energy waste
            return opportunities;
        }

        private async Task<double> GetConsumptionByScheduleAsync(OperationSchedule schedule, CancellationToken ct)
        {
            return 1000; // Placeholder
        }

        private async Task<double> CalculateProjectedConsumptionAsync(
            HVACSchedule hvac, LightingSchedule lighting, CancellationToken ct)
        {
            return 800; // Placeholder
        }

        private async Task<List<CurrentLoad>> GetCurrentLoadsAsync(CancellationToken ct)
        {
            return new List<CurrentLoad>();
        }

        private async Task<List<SheddableLoad>> IdentifySheddableLoadsAsync(
            List<CurrentLoad> loads, DemandConstraints constraints, CancellationToken ct)
        {
            return new List<SheddableLoad>();
        }

        private bool IsDirectFuel(string unit)
        {
            return unit == "m³" || unit == "therms" || unit == "liters" || unit == "gallons";
        }

        private async Task<double> GetBaselineEmissionsAsync(int year, CancellationToken ct)
        {
            return 1000; // Placeholder
        }

        private async Task<EnergyMetrics> CalculateEnergyMetricsAsync(
            SustainabilityReportRequest request, CancellationToken ct)
        {
            return new EnergyMetrics();
        }

        private async Task<CarbonMetrics> CalculateCarbonMetricsAsync(
            SustainabilityReportRequest request, CancellationToken ct)
        {
            return new CarbonMetrics();
        }

        private async Task<WaterMetrics> CalculateWaterMetricsAsync(
            SustainabilityReportRequest request, CancellationToken ct)
        {
            return new WaterMetrics();
        }

        private async Task<TargetProgress> CalculateTargetProgressAsync(
            SustainabilityTargets targets, EnergyMetrics energy, CarbonMetrics carbon, CancellationToken ct)
        {
            return new TargetProgress();
        }

        private async Task<List<SustainabilityRecommendation>> GenerateSustainabilityRecommendationsAsync(
            EnergyMetrics energy, CarbonMetrics carbon, SustainabilityTargets targets, CancellationToken ct)
        {
            return new List<SustainabilityRecommendation>();
        }

        #endregion
    }

    #region Supporting Classes

    internal class EnergyDataCollector
    {
        public async Task InitializeMeterAsync(EnergyMeter meter, CancellationToken ct) { }
        public async Task StoreReadingAsync(MeterReading reading, CancellationToken ct) { }
        public async Task<MeterReading> GetLatestReadingAsync(string meterId, CancellationToken ct)
            => new MeterReading { MeterId = meterId, Value = 100, Unit = "kWh", Timestamp = DateTime.UtcNow };
        public async Task<List<MeterReading>> GetReadingsAsync(IEnumerable<string> meterIds, DateTime start, DateTime end, CancellationToken ct)
            => new List<MeterReading>();
    }

    internal class ConsumptionAnalyzer
    {
        public async Task<DailyPattern> AnalyzeDailyPatternAsync(List<MeterReading> readings, CancellationToken ct)
            => new DailyPattern();
        public async Task<WeeklyPattern> AnalyzeWeeklyPatternAsync(List<MeterReading> readings, CancellationToken ct)
            => new WeeklyPattern();
        public async Task<ConsumptionPatterns> AnalyzePatternsAsync(DateTime start, DateTime end, CancellationToken ct)
            => new ConsumptionPatterns();
    }

    internal class LoadProfiler
    {
        public async Task<PeakDemandInfo> IdentifyPeaksAsync(List<MeterReading> readings, CancellationToken ct)
            => new PeakDemandInfo();
        public async Task<double> CalculateBaseLoadAsync(List<MeterReading> readings, CancellationToken ct) => 50;
    }

    internal class CostCalculator
    {
        public async Task<decimal> CalculateTotalCostAsync(List<MeterReading> readings, Dictionary<string, EnergyTariff> tariffs, CancellationToken ct)
            => readings.Sum(r => (decimal)r.Value * 0.12m);
        public async Task<CostBreakdown> CalculateCostBreakdownAsync(List<MeterReading> readings, Dictionary<string, EnergyTariff> tariffs, CancellationToken ct)
            => new CostBreakdown();
    }

    internal class OptimizationEngine
    {
        public async Task<HVACSchedule> OptimizeHVACScheduleAsync(HVACSchedule current, OccupancyPattern occupancy, ComfortRequirements comfort, CancellationToken ct)
            => new HVACSchedule();
        public async Task<LightingSchedule> OptimizeLightingScheduleAsync(LightingSchedule current, OccupancyPattern occupancy, DaylightAvailability daylight, CancellationToken ct)
            => new LightingSchedule();
    }

    internal class CarbonCalculator
    {
        public async Task<CarbonEmissions> CalculateEmissionsAsync(List<MeterReading> readings, Dictionary<string, CarbonFactor> factors, CancellationToken ct)
            => new CarbonEmissions();
        public async Task<double> CalculateScope1Async(IEnumerable<MeterReading> readings, Dictionary<string, CarbonFactor> factors, CancellationToken ct)
            => 100;
        public async Task<double> CalculateScope2Async(IEnumerable<MeterReading> readings, Dictionary<string, CarbonFactor> factors, string gridRegion, CancellationToken ct)
            => 200;
        public async Task<Dictionary<string, double>> GetBreakdownBySourceAsync(List<MeterReading> readings, Dictionary<string, CarbonFactor> factors, CancellationToken ct)
            => new Dictionary<string, double>();
    }

    internal class BenchmarkEngine
    {
        public async Task<BenchmarkValues> GetBenchmarksAsync(string buildingType, string climate, string location, CancellationToken ct)
            => new BenchmarkValues { Median = 150, Top25 = 100, Target = 80 };
        public async Task<double> CalculatePercentileAsync(double eui, BenchmarkValues benchmarks, CancellationToken ct)
            => 50;
    }

    internal class AlertManager
    {
        public async Task CheckThresholdsAsync(EnergyMeter meter, MeterReading reading, CancellationToken ct) { }
        public async Task RegisterAlertAsync(EnergyAlert alert, CancellationToken ct) { }
        public async Task<List<ActiveAlert>> GetActiveAlertsAsync(CancellationToken ct)
            => new List<ActiveAlert>();
    }

    #endregion

    #region Data Models

    public class EnergyMeter
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public MeterType Type { get; set; }
        public string Location { get; set; }
        public string Unit { get; set; }
        public TimeSpan Resolution { get; set; }
        public string LinkedSpaceId { get; set; }
        public string LinkedSystemId { get; set; }
        public string TariffId { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegisteredDate { get; set; }
    }

    public class MeterConfiguration
    {
        public string MeterId { get; set; }
        public string Name { get; set; }
        public MeterType MeterType { get; set; }
        public string Location { get; set; }
        public string Unit { get; set; }
        public TimeSpan Resolution { get; set; }
        public string LinkedSpaceId { get; set; }
        public string LinkedSystemId { get; set; }
        public string TariffId { get; set; }
    }

    public class MeterReading
    {
        public string Id { get; set; }
        public string MeterId { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
        public ReadingType ReadingType { get; set; }
    }

    public class EnergyTariff
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double RatePerUnit { get; set; }
        public string Unit { get; set; }
        public string Currency { get; set; }
        public int[] PeakHours { get; set; }
    }

    public class CarbonFactor
    {
        public string Id { get; set; }
        public double Factor { get; set; }
        public string Unit { get; set; }
    }

    public class MeterRegistrationResult
    {
        public string MeterId { get; set; }
        public DateTime RegistrationTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ConsumptionRecordResult
    {
        public string MeterId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double RecordedValue { get; set; }
    }

    public class RealTimeConsumptionData
    {
        public DateTime Timestamp { get; set; }
        public List<RealTimeReading> Readings { get; set; } = new List<RealTimeReading>();
        public double TotalElectricity { get; set; }
        public double TotalGas { get; set; }
        public double TotalWater { get; set; }
    }

    public class RealTimeReading
    {
        public string MeterId { get; set; }
        public string MeterName { get; set; }
        public double CurrentValue { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
        public TrendDirection TrendDirection { get; set; }
    }

    public class ConsumptionAnalysisRequest
    {
        public List<string> MeterIds { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ConsumptionAnalysisResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime AnalysisTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ConsumptionValue TotalConsumption { get; set; }
        public Dictionary<string, ConsumptionValue> ConsumptionByMeter { get; set; }
        public Dictionary<string, ConsumptionValue> ConsumptionByType { get; set; }
        public DailyPattern DailyPattern { get; set; }
        public WeeklyPattern WeeklyPattern { get; set; }
        public PeakDemandInfo PeakDemand { get; set; }
        public double BaseLoad { get; set; }
        public decimal TotalCost { get; set; }
        public CostBreakdown CostBreakdown { get; set; }
        public CarbonEmissions CarbonEmissions { get; set; }
    }

    public class ConsumptionValue { public double Value { get; set; } public string Unit { get; set; } }
    public class DailyPattern { public List<double> HourlyValues { get; set; } }
    public class WeeklyPattern { public List<double> DailyValues { get; set; } }
    public class PeakDemandInfo { public double PeakValue { get; set; } public DateTime PeakTime { get; set; } }
    public class CostBreakdown { public Dictionary<string, decimal> ByCategory { get; set; } }
    public class CarbonEmissions { public double TotalCO2e { get; set; } }

    public class AnalysisProgress { public string Stage { get; set; } public int Percentage { get; set; } }

    public class LoadProfileRequest
    {
        public List<string> MeterIds { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Period { get; set; }
    }

    public class LoadProfileResult
    {
        public string Period { get; set; }
        public DateTime GeneratedTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<ProfilePoint> HourlyProfile { get; set; }
        public List<DailyProfile> DailyProfile { get; set; }
        public double PeakDemand { get; set; }
        public double MinDemand { get; set; }
        public double AverageDemand { get; set; }
        public double LoadFactor { get; set; }
        public List<int> PeakHours { get; set; }
        public List<int> OffPeakHours { get; set; }
    }

    public class ProfilePoint { public int Hour { get; set; } public double Value { get; set; } }
    public class DailyProfile { public DateTime Date { get; set; } public double TotalConsumption { get; set; } }

    public class BenchmarkRequest
    {
        public string BuildingType { get; set; }
        public string Climate { get; set; }
        public string Location { get; set; }
        public double GrossFloorArea { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class BenchmarkResult
    {
        public string BuildingType { get; set; }
        public DateTime AnalysisDate { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double EUI { get; set; }
        public string EUIUnit { get; set; }
        public double MedianBenchmark { get; set; }
        public double Top25Benchmark { get; set; }
        public double TargetBenchmark { get; set; }
        public double Percentile { get; set; }
        public double PotentialSavings { get; set; }
        public double SavingsPercentage { get; set; }
        public EnergyRating Rating { get; set; }
    }

    public class BenchmarkValues { public double Median { get; set; } public double Top25 { get; set; } public double Target { get; set; } }

    public class SavingsAnalysisRequest { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }

    public class SavingsOpportunityResult
    {
        public DateTime AnalysisDate { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<SavingsOpportunity> Opportunities { get; set; } = new List<SavingsOpportunity>();
        public double TotalPotentialSavings { get; set; }
        public double TotalImplementationCost { get; set; }
        public double AveragePaybackPeriod { get; set; }
    }

    public class SavingsOpportunity
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public double AnnualSavings { get; set; }
        public double ImplementationCost { get; set; }
        public double PaybackPeriod { get; set; }
        public double ROI { get; set; }
        public Priority Priority { get; set; }
    }

    public class ConsumptionPatterns { public double TotalConsumption { get; set; } public double AfterHoursConsumption { get; set; } }

    public class ScheduleOptimizationRequest
    {
        public OperationSchedule CurrentSchedule { get; set; }
        public OccupancyPattern OccupancyPattern { get; set; }
        public ComfortRequirements ComfortRequirements { get; set; }
        public DaylightAvailability DaylightAvailability { get; set; }
    }

    public class OptimizedScheduleResult
    {
        public DateTime OptimizationDate { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public HVACSchedule OptimizedHVACSchedule { get; set; }
        public LightingSchedule OptimizedLightingSchedule { get; set; }
        public double ProjectedSavings { get; set; }
        public double SavingsPercentage { get; set; }
    }

    public class OperationSchedule { public HVACSchedule HVAC { get; set; } public LightingSchedule Lighting { get; set; } }
    public class HVACSchedule { public Dictionary<int, double> HourlySetpoints { get; set; } }
    public class LightingSchedule { public Dictionary<int, double> HourlyLevels { get; set; } }
    public class OccupancyPattern { public Dictionary<int, double> HourlyOccupancy { get; set; } }
    public class ComfortRequirements { public double MinTemp { get; set; } public double MaxTemp { get; set; } }
    public class DaylightAvailability { public Dictionary<int, double> HourlyDaylight { get; set; } }

    public class DemandResponseRequest
    {
        public double ReductionTarget { get; set; }
        public TimeSpan Duration { get; set; }
        public DemandConstraints Constraints { get; set; }
    }

    public class DemandResponseRecommendation
    {
        public DateTime RequestTime { get; set; }
        public double ReductionTarget { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<DemandResponseAction> Actions { get; set; } = new List<DemandResponseAction>();
        public double AchievableReduction { get; set; }
        public double ConfidenceLevel { get; set; }
    }

    public class DemandResponseAction
    {
        public string SystemId { get; set; }
        public string SystemName { get; set; }
        public string ActionType { get; set; }
        public double ReductionAmount { get; set; }
        public double ComfortImpact { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class DemandConstraints { public List<string> ExcludedSystems { get; set; } public double MaxComfortImpact { get; set; } }
    public class CurrentLoad { public string SystemId { get; set; } public double Load { get; set; } }
    public class SheddableLoad { public string SystemId { get; set; } public string SystemName { get; set; } public double ReductionPotential { get; set; } public double ComfortImpact { get; set; } public string RecommendedAction { get; set; } }

    public class CarbonCalculationRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GridRegion { get; set; }
        public double GrossFloorArea { get; set; }
        public int? BaselineYear { get; set; }
    }

    public class CarbonFootprintResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CalculationDate { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double Scope1Emissions { get; set; }
        public double Scope2Emissions { get; set; }
        public double TotalEmissions { get; set; }
        public double CarbonIntensity { get; set; }
        public string IntensityUnit { get; set; }
        public double ChangeFromBaseline { get; set; }
        public Dictionary<string, double> EmissionsBySource { get; set; }
    }

    public class SustainabilityReportRequest
    {
        public string ReportPeriod { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SustainabilityTargets Targets { get; set; }
    }

    public class SustainabilityReport
    {
        public string ReportPeriod { get; set; }
        public DateTime GeneratedDate { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public EnergyMetrics EnergyMetrics { get; set; }
        public CarbonMetrics CarbonMetrics { get; set; }
        public WaterMetrics WaterMetrics { get; set; }
        public TargetProgress TargetProgress { get; set; }
        public List<SustainabilityRecommendation> Recommendations { get; set; }
    }

    public class SustainabilityTargets { public double EnergyReductionTarget { get; set; } public double CarbonReductionTarget { get; set; } }
    public class EnergyMetrics { public double TotalConsumption { get; set; } public double EUI { get; set; } }
    public class CarbonMetrics { public double TotalEmissions { get; set; } public double Intensity { get; set; } }
    public class WaterMetrics { public double TotalConsumption { get; set; } public double Intensity { get; set; } }
    public class TargetProgress { public double EnergyProgress { get; set; } public double CarbonProgress { get; set; } }
    public class SustainabilityRecommendation { public string Category { get; set; } public string Recommendation { get; set; } public double PotentialImpact { get; set; } }

    public class AlertConfiguration
    {
        public string AlertId { get; set; }
        public string AlertName { get; set; }
        public string MeterId { get; set; }
        public ThresholdType ThresholdType { get; set; }
        public double ThresholdValue { get; set; }
        public string ComparisonPeriod { get; set; }
        public List<string> NotificationChannels { get; set; }
    }

    public class AlertConfigResult
    {
        public string AlertId { get; set; }
        public DateTime ConfigTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class EnergyAlert
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MeterId { get; set; }
        public ThresholdType ThresholdType { get; set; }
        public double ThresholdValue { get; set; }
        public string ComparisonPeriod { get; set; }
        public List<string> NotificationChannels { get; set; }
        public bool IsActive { get; set; }
    }

    public class ActiveAlert
    {
        public string AlertId { get; set; }
        public string AlertName { get; set; }
        public DateTime TriggeredTime { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    public enum MeterType { Electricity, Gas, Water, Steam, ChilledWater, HotWater }
    public enum ReadingType { Actual, Estimated, Manual }
    public enum TrendDirection { Rising, Falling, Stable }
    public enum EnergyRating { A, B, C, D, E, F, G }
    public enum Priority { Low, Medium, High, Critical }
    public enum ThresholdType { Absolute, PercentageAboveAverage, PercentageAboveBaseline }
    public enum AlertSeverity { Info, Warning, Critical }

    #endregion
}
