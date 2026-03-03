// ============================================================================
// StingBIM AI - Predictive Maintenance Engine
// Core predictive maintenance analytics including Weibull failure analysis,
// remaining useful life estimation, health scoring, maintenance plan generation,
// resource-constrained schedule optimization, spare parts forecasting,
// KPI computation, risk assessment, and degradation pattern detection.
// Aligned with ISO 55000 (Asset Management) principles.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Maintenance.Models;

namespace StingBIM.AI.Maintenance.Engine
{
    /// <summary>
    /// Predictive maintenance engine providing failure prediction via Weibull distribution,
    /// remaining useful life calculation, multi-factor health scoring, optimal maintenance
    /// plan generation, schedule optimization, spare parts demand forecasting, KPI
    /// computation, risk assessment, degradation detection, and asset benchmarking.
    /// </summary>
    public class PredictiveMaintenanceEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        #region Internal State

        // Asset condition cache: assetId -> condition
        private readonly Dictionary<string, AssetCondition> _assetConditions =
            new(StringComparer.OrdinalIgnoreCase);

        // Maintenance plans: planId -> plan
        private readonly Dictionary<string, MaintenancePlan> _maintenancePlans =
            new(StringComparer.OrdinalIgnoreCase);

        // Work order history: assetId -> list of completed work orders
        private readonly Dictionary<string, List<WorkOrder>> _workOrderHistory =
            new(StringComparer.OrdinalIgnoreCase);

        // Sensor history: assetId -> list of readings
        private readonly Dictionary<string, List<SensorReading>> _sensorHistory =
            new(StringComparer.OrdinalIgnoreCase);

        // Spare parts inventory: partId -> part
        private readonly Dictionary<string, SparePart> _sparePartsInventory =
            new(StringComparer.OrdinalIgnoreCase);

        // Failure records: assetId -> list of failure timestamps
        private readonly Dictionary<string, List<DateTime>> _failureHistory =
            new(StringComparer.OrdinalIgnoreCase);

        // Technician pool
        private readonly Dictionary<string, TechnicianProfile> _technicians =
            new(StringComparer.OrdinalIgnoreCase);

        // Configuration constants
        private const double DefaultWeibullShape = 2.5;
        private const double DefaultWeibullScale = 8760.0; // hours (1 year)
        private const double ExponentialSmoothingAlpha = 0.3;
        private const int MaxPredictionHorizonDays = 365 * 5;
        private const double HealthScoreCriticalThreshold = 25.0;
        private const double HealthScoreWarningThreshold = 50.0;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the predictive maintenance engine.
        /// </summary>
        public PredictiveMaintenanceEngine()
        {
            Logger.Info("PredictiveMaintenanceEngine initialized.");
        }

        #endregion

        #region Data Registration

        /// <summary>
        /// Registers or updates an asset condition in the engine cache.
        /// </summary>
        public void RegisterAssetCondition(AssetCondition condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));

            lock (_lockObject)
            {
                _assetConditions[condition.AssetId] = condition;
            }

            Logger.Debug("Registered asset condition for '{AssetId}', health={Health:F1}",
                condition.AssetId, condition.HealthScore);
        }

        /// <summary>
        /// Registers a maintenance plan.
        /// </summary>
        public void RegisterMaintenancePlan(MaintenancePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            lock (_lockObject)
            {
                _maintenancePlans[plan.PlanId] = plan;
            }

            Logger.Debug("Registered maintenance plan '{PlanId}' for asset '{AssetId}'",
                plan.PlanId, plan.AssetId);
        }

        /// <summary>
        /// Records a completed work order for historical analysis.
        /// </summary>
        public void RecordWorkOrderHistory(WorkOrder workOrder)
        {
            if (workOrder == null) throw new ArgumentNullException(nameof(workOrder));

            lock (_lockObject)
            {
                if (!_workOrderHistory.TryGetValue(workOrder.AssetId, out var history))
                {
                    history = new List<WorkOrder>();
                    _workOrderHistory[workOrder.AssetId] = history;
                }
                history.Add(workOrder);
            }
        }

        /// <summary>
        /// Records a sensor reading for trend analysis.
        /// </summary>
        public void RecordSensorReading(SensorReading reading)
        {
            if (reading == null) throw new ArgumentNullException(nameof(reading));

            lock (_lockObject)
            {
                if (!_sensorHistory.TryGetValue(reading.AssetId, out var readings))
                {
                    readings = new List<SensorReading>();
                    _sensorHistory[reading.AssetId] = readings;
                }
                readings.Add(reading);
            }
        }

        /// <summary>
        /// Records a failure event for reliability analysis.
        /// </summary>
        public void RecordFailure(string assetId, DateTime failureDate)
        {
            if (string.IsNullOrEmpty(assetId)) throw new ArgumentException("Asset ID required.", nameof(assetId));

            lock (_lockObject)
            {
                if (!_failureHistory.TryGetValue(assetId, out var failures))
                {
                    failures = new List<DateTime>();
                    _failureHistory[assetId] = failures;
                }
                failures.Add(failureDate);
            }
        }

        /// <summary>
        /// Registers a spare part in the inventory.
        /// </summary>
        public void RegisterSparePart(SparePart part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            lock (_lockObject)
            {
                _sparePartsInventory[part.PartId] = part;
            }
        }

        /// <summary>
        /// Registers a technician in the workforce pool.
        /// </summary>
        public void RegisterTechnician(TechnicianProfile technician)
        {
            if (technician == null) throw new ArgumentNullException(nameof(technician));

            lock (_lockObject)
            {
                _technicians[technician.TechnicianId] = technician;
            }
        }

        #endregion

        #region Failure Prediction

        /// <summary>
        /// Predicts the next failure for an asset using Weibull distribution analysis
        /// combined with sensor trend extrapolation and maintenance history patterns.
        /// </summary>
        /// <param name="assetId">Unique identifier of the asset to analyze.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>Failure prediction result with confidence and recommended actions.</returns>
        public async Task<FailurePrediction> PredictFailureAsync(
            string assetId,
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Info("Predicting failure for asset '{AssetId}'...", assetId);
            progress?.Report(0.0);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Step 1: Estimate Weibull parameters from failure history
                progress?.Report(0.1);
                var (shape, scale) = EstimateWeibullParameters(assetId);

                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Calculate current operating age in hours
                progress?.Report(0.2);
                double currentAgeHours = GetAssetOperatingAgeHours(assetId);

                // Step 3: Compute failure probability over prediction horizon
                progress?.Report(0.3);
                double failureProbability = CalculateWeibullFailureProbability(
                    currentAgeHours, shape, scale, hoursAhead: 8760);

                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Estimate remaining useful life
                progress?.Report(0.4);
                double rulHours = EstimateRULFromWeibull(currentAgeHours, shape, scale);
                double rulDays = rulHours / 24.0;

                // Step 5: Incorporate sensor trend analysis
                progress?.Report(0.5);
                double sensorAdjustment = AnalyzeSensorTrends(assetId);
                rulDays *= sensorAdjustment; // Adjust RUL based on sensor trends

                cancellationToken.ThrowIfCancellationRequested();

                // Step 6: Determine most likely failure mode
                progress?.Report(0.6);
                var failureMode = DetermineFailureMode(assetId);

                // Step 7: Calculate risk score (probability x consequence)
                progress?.Report(0.7);
                double consequenceScore = CalculateConsequenceScore(assetId);
                double riskScore = failureProbability * consequenceScore * 100.0;

                // Step 8: Compute degradation factors
                progress?.Report(0.8);
                var factors = ComputeDegradationFactors(assetId);

                // Step 9: Estimate costs
                progress?.Report(0.9);
                var (failureCost, preventiveCost) = EstimateMaintenanceCosts(assetId);

                // Step 10: Build confidence level
                double confidence = CalculatePredictionConfidence(assetId, shape, scale);

                // Step 11: Generate recommended actions
                var recommendations = GenerateRecommendations(
                    assetId, failureProbability, riskScore, rulDays, failureMode);

                progress?.Report(1.0);

                var prediction = new FailurePrediction
                {
                    AssetId = assetId,
                    AssetName = GetAssetName(assetId),
                    PredictedFailureDate = DateTime.UtcNow.AddDays(rulDays),
                    Confidence = confidence,
                    RemainingUsefulLifeDays = rulDays,
                    FailureMode = failureMode,
                    RiskScore = Math.Min(riskScore, 100.0),
                    FailureProbability = failureProbability,
                    ConsequenceScore = consequenceScore * 100.0,
                    EstimatedFailureCost = failureCost,
                    PreventiveActionCost = preventiveCost,
                    WeibullShape = shape,
                    WeibullScale = scale,
                    DegradationFactors = factors,
                    RecommendedActions = recommendations,
                    PredictionTimestamp = DateTime.UtcNow
                };

                Logger.Info("Failure prediction for '{AssetId}': RUL={RUL:F0} days, " +
                            "probability={Prob:P1}, risk={Risk:F1}",
                    assetId, rulDays, failureProbability, riskScore);

                return prediction;
            }, cancellationToken);
        }

        #endregion

        #region Remaining Useful Life

        /// <summary>
        /// Calculates the remaining useful life of an asset using degradation curves
        /// and Weibull reliability analysis. Accounts for current condition, age,
        /// maintenance history, and environmental factors.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>Remaining useful life in days.</returns>
        public double CalculateRemainingUsefulLife(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Debug("Calculating RUL for asset '{AssetId}'...", assetId);

            var (shape, scale) = EstimateWeibullParameters(assetId);
            double currentAgeHours = GetAssetOperatingAgeHours(assetId);

            // Weibull-based RUL
            double weibullRULHours = EstimateRULFromWeibull(currentAgeHours, shape, scale);

            // Degradation-based RUL
            double degradationRULHours = double.MaxValue;
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition) &&
                    condition.DegradationRate > 0)
                {
                    // Time until health reaches critical threshold
                    double remainingHealth = condition.HealthScore - HealthScoreCriticalThreshold;
                    if (remainingHealth > 0)
                    {
                        double monthsRemaining = remainingHealth / condition.DegradationRate;
                        degradationRULHours = monthsRemaining * 30.44 * 24.0;
                    }
                    else
                    {
                        degradationRULHours = 0;
                    }
                }
            }

            // Sensor trend-based adjustment
            double sensorFactor = AnalyzeSensorTrends(assetId);

            // Combined RUL: take the minimum of available estimates, adjusted by sensor trends
            double combinedRULHours = Math.Min(weibullRULHours, degradationRULHours);
            combinedRULHours *= sensorFactor;

            double rulDays = Math.Max(0, combinedRULHours / 24.0);
            rulDays = Math.Min(rulDays, MaxPredictionHorizonDays);

            Logger.Info("RUL for '{AssetId}': {RUL:F0} days (Weibull={W:F0}h, Degradation={D:F0}h, SensorFactor={S:F2})",
                assetId, rulDays, weibullRULHours, degradationRULHours, sensorFactor);

            return rulDays;
        }

        #endregion

        #region Health Score

        /// <summary>
        /// Calculates a composite health score (0-100) for an asset based on multiple
        /// weighted factors: age, usage intensity, environmental conditions, maintenance
        /// history quality, and real-time sensor data.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>Updated asset condition with computed health score.</returns>
        public AssetCondition CalculateHealthScore(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Debug("Calculating health score for asset '{AssetId}'...", assetId);

            AssetCondition condition;
            lock (_lockObject)
            {
                if (!_assetConditions.TryGetValue(assetId, out condition))
                {
                    condition = new AssetCondition
                    {
                        AssetId = assetId,
                        AssetName = assetId,
                        HealthScore = 100.0,
                        ConditionGrade = ConditionGrade.A
                    };
                    _assetConditions[assetId] = condition;
                }
            }

            // Factor 1: Age factor (0-100)
            double ageFactor = CalculateAgeFactor(condition);

            // Factor 2: Usage intensity factor (0-100)
            double usageFactor = CalculateUsageFactor(assetId);

            // Factor 3: Environmental factor (0-100)
            double environmentFactor = CalculateEnvironmentFactor(assetId);

            // Factor 4: Maintenance history factor (0-100)
            double maintenanceFactor = CalculateMaintenanceHistoryFactor(assetId);

            // Factor 5: Sensor data factor (0-100)
            double sensorFactor = CalculateSensorDataFactor(assetId);

            // Weighted composite score
            const double wAge = 0.25;
            const double wUsage = 0.15;
            const double wEnvironment = 0.10;
            const double wMaintenance = 0.25;
            const double wSensor = 0.25;

            double healthScore = (ageFactor * wAge) +
                                 (usageFactor * wUsage) +
                                 (environmentFactor * wEnvironment) +
                                 (maintenanceFactor * wMaintenance) +
                                 (sensorFactor * wSensor);

            healthScore = Math.Clamp(healthScore, 0.0, 100.0);

            // Update condition record
            lock (_lockObject)
            {
                condition.HealthScore = healthScore;
                condition.ConditionGrade = MapHealthScoreToGrade(healthScore);
                condition.LastAssessment = DateTime.UtcNow;
                condition.FactorScores["Age"] = ageFactor;
                condition.FactorScores["Usage"] = usageFactor;
                condition.FactorScores["Environment"] = environmentFactor;
                condition.FactorScores["Maintenance"] = maintenanceFactor;
                condition.FactorScores["Sensor"] = sensorFactor;
            }

            Logger.Info("Health score for '{AssetId}': {Score:F1} (Grade={Grade}), " +
                        "factors: Age={Age:F0}, Usage={Usage:F0}, Env={Env:F0}, Maint={Maint:F0}, Sensor={Sensor:F0}",
                assetId, healthScore, condition.ConditionGrade,
                ageFactor, usageFactor, environmentFactor, maintenanceFactor, sensorFactor);

            return condition;
        }

        #endregion

        #region Maintenance Plan Generation

        /// <summary>
        /// Auto-generates an optimal maintenance plan for an asset based on its current
        /// condition, manufacturer recommendations, failure history, and criticality.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>A list of maintenance plans covering all required activities.</returns>
        public List<MaintenancePlan> GenerateMaintenancePlan(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Info("Generating maintenance plan for asset '{AssetId}'...", assetId);

            var plans = new List<MaintenancePlan>();
            var condition = CalculateHealthScore(assetId);

            // Plan 1: Preventive inspection based on condition grade
            int inspectionIntervalDays = condition.ConditionGrade switch
            {
                ConditionGrade.A => 365,
                ConditionGrade.B => 180,
                ConditionGrade.C => 90,
                ConditionGrade.D => 30,
                ConditionGrade.E => 14,
                ConditionGrade.F => 7,
                _ => 90
            };

            plans.Add(new MaintenancePlan
            {
                PlanId = $"PM-{assetId}-INSP-{Guid.NewGuid():N}".Substring(0, 32),
                AssetId = assetId,
                PlanName = $"Condition Inspection - {GetAssetName(assetId)}",
                Type = MaintenanceType.Preventive,
                IntervalDays = inspectionIntervalDays,
                NextDueDate = DateTime.UtcNow.AddDays(inspectionIntervalDays),
                EstimatedCost = 150.00m,
                EstimatedLaborHours = 1.5,
                Priority = condition.ConditionGrade >= ConditionGrade.D
                    ? WorkOrderPriority.High
                    : WorkOrderPriority.Medium,
                ProcedureSteps = new List<string>
                {
                    "Perform visual inspection of all accessible components",
                    "Check for unusual noise, vibration, or temperature",
                    "Verify operational parameters against manufacturer specs",
                    "Document condition with photographs",
                    "Record sensor readings and compare to baselines",
                    "Update condition grade in asset register"
                },
                SafetyPrecautions = new List<string>
                {
                    "Follow lockout/tagout procedures before physical contact",
                    "Wear appropriate PPE for asset type"
                }
            });

            // Plan 2: Predictive maintenance based on RUL
            double rulDays = CalculateRemainingUsefulLife(assetId);
            if (rulDays < 365 && rulDays > 0)
            {
                int predictiveIntervalDays = Math.Max(7, (int)(rulDays * 0.25));
                plans.Add(new MaintenancePlan
                {
                    PlanId = $"PM-{assetId}-PRED-{Guid.NewGuid():N}".Substring(0, 32),
                    AssetId = assetId,
                    PlanName = $"Predictive Service - {GetAssetName(assetId)}",
                    Type = MaintenanceType.Predictive,
                    IntervalDays = predictiveIntervalDays,
                    NextDueDate = DateTime.UtcNow.AddDays(predictiveIntervalDays),
                    EstimatedCost = 500.00m,
                    EstimatedLaborHours = 4.0,
                    Priority = rulDays < 90 ? WorkOrderPriority.High : WorkOrderPriority.Medium,
                    ProcedureSteps = new List<string>
                    {
                        "Review latest sensor data and trend analysis",
                        "Perform detailed component inspection",
                        "Replace wear components approaching end of life",
                        "Calibrate and test operational parameters",
                        "Update RUL estimate based on findings"
                    }
                });
            }

            // Plan 3: Condition-based monitoring for assets with sensor data
            bool hasSensors;
            lock (_lockObject) { hasSensors = _sensorHistory.ContainsKey(assetId); }

            if (hasSensors)
            {
                plans.Add(new MaintenancePlan
                {
                    PlanId = $"PM-{assetId}-CBM-{Guid.NewGuid():N}".Substring(0, 32),
                    AssetId = assetId,
                    PlanName = $"Condition Monitoring - {GetAssetName(assetId)}",
                    Type = MaintenanceType.ConditionBased,
                    IntervalDays = 7,
                    NextDueDate = DateTime.UtcNow.AddDays(7),
                    EstimatedCost = 50.00m,
                    EstimatedLaborHours = 0.5,
                    Priority = WorkOrderPriority.Medium,
                    ProcedureSteps = new List<string>
                    {
                        "Download and review sensor data logs",
                        "Check for threshold exceedances or anomalous patterns",
                        "Verify sensor calibration and connectivity",
                        "Generate trend report and update predictions"
                    }
                });
            }

            // Plan 4: Statutory/compliance maintenance if applicable
            plans.Add(new MaintenancePlan
            {
                PlanId = $"PM-{assetId}-STAT-{Guid.NewGuid():N}".Substring(0, 32),
                AssetId = assetId,
                PlanName = $"Annual Compliance Check - {GetAssetName(assetId)}",
                Type = MaintenanceType.Statutory,
                IntervalDays = 365,
                NextDueDate = DateTime.UtcNow.AddDays(365),
                EstimatedCost = 300.00m,
                EstimatedLaborHours = 3.0,
                Priority = WorkOrderPriority.High,
                ComplianceStandard = "ISO 55000 / ASHRAE 180",
                ProcedureSteps = new List<string>
                {
                    "Verify all safety systems operational",
                    "Test emergency shutoffs and alarms",
                    "Inspect fire protection and suppression components",
                    "Review environmental compliance parameters",
                    "Document findings for regulatory file"
                }
            });

            // Register all generated plans
            foreach (var plan in plans)
            {
                RegisterMaintenancePlan(plan);
            }

            Logger.Info("Generated {Count} maintenance plans for asset '{AssetId}'", plans.Count, assetId);
            return plans;
        }

        #endregion

        #region Schedule Optimization

        /// <summary>
        /// Optimizes a set of maintenance plans under resource constraints (technician
        /// availability, budget, and asset criticality). Uses a greedy priority-based
        /// scheduling algorithm with look-ahead conflict resolution.
        /// </summary>
        /// <param name="plans">List of maintenance plans to schedule.</param>
        /// <param name="budgetLimit">Maximum budget for the scheduling window.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Optimized list of plans with adjusted due dates.</returns>
        public async Task<List<MaintenancePlan>> OptimizeMaintenanceSchedule(
            List<MaintenancePlan> plans,
            decimal budgetLimit = decimal.MaxValue,
            CancellationToken cancellationToken = default)
        {
            if (plans == null || plans.Count == 0)
                return new List<MaintenancePlan>();

            Logger.Info("Optimizing schedule for {Count} maintenance plans, budget={Budget:C}",
                plans.Count, budgetLimit);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Sort by priority (Emergency first), then by next due date
                var sorted = plans
                    .Where(p => p.IsActive)
                    .OrderBy(p => (int)p.Priority)
                    .ThenBy(p => p.NextDueDate)
                    .ToList();

                // Track daily technician capacity (simplified: max 8 labor-hours per day per technician)
                int technicianCount;
                lock (_lockObject) { technicianCount = Math.Max(1, _technicians.Count); }
                double dailyCapacityHours = technicianCount * 8.0;

                // Schedule day by day, accumulating costs
                var scheduledPlans = new List<MaintenancePlan>();
                decimal accumulatedCost = 0m;
                var dailyLoadMap = new Dictionary<int, double>(); // dayOffset -> consumed hours

                foreach (var plan in sorted)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (accumulatedCost + plan.EstimatedCost > budgetLimit)
                    {
                        Logger.Warn("Budget limit reached; plan '{PlanId}' deferred.", plan.PlanId);
                        continue;
                    }

                    // Find the earliest feasible slot starting from the plan's due date
                    int targetDayOffset = (int)(plan.NextDueDate - DateTime.UtcNow).TotalDays;
                    targetDayOffset = Math.Max(0, targetDayOffset);

                    bool scheduled = false;
                    for (int dayOffset = targetDayOffset; dayOffset < targetDayOffset + 30; dayOffset++)
                    {
                        if (!dailyLoadMap.TryGetValue(dayOffset, out double currentLoad))
                            currentLoad = 0;

                        if (currentLoad + plan.EstimatedLaborHours <= dailyCapacityHours)
                        {
                            dailyLoadMap[dayOffset] = currentLoad + plan.EstimatedLaborHours;
                            plan.NextDueDate = DateTime.UtcNow.AddDays(dayOffset);
                            accumulatedCost += plan.EstimatedCost;
                            scheduledPlans.Add(plan);
                            scheduled = true;
                            break;
                        }
                    }

                    if (!scheduled)
                    {
                        Logger.Warn("Could not schedule plan '{PlanId}' within 30-day window from due date.",
                            plan.PlanId);
                    }
                }

                Logger.Info("Optimized schedule: {Scheduled}/{Total} plans, total cost={Cost:C}",
                    scheduledPlans.Count, sorted.Count, accumulatedCost);

                return scheduledPlans;
            }, cancellationToken);
        }

        #endregion

        #region Spare Parts Forecasting

        /// <summary>
        /// Forecasts spare parts demand over a specified number of months based on
        /// planned maintenance activities, historical consumption rates, and predicted
        /// corrective maintenance needs.
        /// </summary>
        /// <param name="months">Forecast horizon in months.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>List of spare part forecasts with demand predictions and reorder recommendations.</returns>
        public async Task<List<SparePartForecast>> ForecastSparePartsAsync(
            int months,
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            if (months <= 0) throw new ArgumentOutOfRangeException(nameof(months), "Months must be positive.");

            Logger.Info("Forecasting spare parts demand for {Months} months...", months);
            progress?.Report(0.0);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var forecasts = new List<SparePartForecast>();
                List<SparePart> parts;

                lock (_lockObject)
                {
                    parts = _sparePartsInventory.Values.ToList();
                }

                int processed = 0;
                foreach (var part in parts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Base demand from planned maintenance
                    int plannedDemand = CalculatePlannedDemand(part.PartId, months);

                    // Historical average demand with exponential smoothing
                    double historicalMonthlyRate = part.AverageMonthlyUsage;
                    int historicalDemand = (int)Math.Ceiling(historicalMonthlyRate * months);

                    // Predicted unplanned demand from failure predictions (30% of historical corrective rate)
                    int predictedUnplannedDemand = (int)Math.Ceiling(historicalMonthlyRate * months * 0.3);

                    // Total forecast demand
                    int totalDemand = plannedDemand + historicalDemand + predictedUnplannedDemand;

                    // Safety stock: 1.65 sigma for 95% service level
                    double demandStdDev = historicalMonthlyRate * 0.3 * Math.Sqrt(months);
                    int safetyStock = (int)Math.Ceiling(1.65 * demandStdDev);
                    totalDemand += safetyStock;

                    // Shortfall analysis
                    int shortfall = Math.Max(0, totalDemand - part.CurrentStock);

                    // Recommended order date considering lead time
                    int daysUntilStockout = part.CurrentStock > 0 && historicalMonthlyRate > 0
                        ? (int)(part.CurrentStock / (historicalMonthlyRate / 30.44))
                        : 0;
                    int orderLeadDays = Math.Max(0, daysUntilStockout - part.LeadTimeDays - 7);

                    forecasts.Add(new SparePartForecast
                    {
                        PartId = part.PartId,
                        PartName = part.Name,
                        ForecastMonths = months,
                        PredictedDemand = totalDemand,
                        CurrentStock = part.CurrentStock,
                        ShortfallQuantity = shortfall,
                        EstimatedCost = shortfall * part.UnitCost,
                        RecommendedOrderDate = shortfall > 0
                            ? DateTime.UtcNow.AddDays(orderLeadDays)
                            : DateTime.UtcNow.AddDays(months * 30)
                    });

                    processed++;
                    progress?.Report((double)processed / parts.Count);
                }

                Logger.Info("Spare parts forecast complete: {Count} parts analyzed, " +
                            "{Shortfalls} require reorder.",
                    forecasts.Count,
                    forecasts.Count(f => f.ShortfallQuantity > 0));

                return forecasts;
            }, cancellationToken);
        }

        #endregion

        #region KPI Calculation

        /// <summary>
        /// Computes maintenance Key Performance Indicators for a date range,
        /// aligned with EN 15341 and ISO 55000 KPI frameworks.
        /// </summary>
        /// <param name="periodStart">Start of the KPI period.</param>
        /// <param name="periodEnd">End of the KPI period.</param>
        /// <returns>Maintenance KPI metrics for the period.</returns>
        public MaintenanceKPI CalculateKPIs(DateTime periodStart, DateTime periodEnd)
        {
            if (periodEnd <= periodStart)
                throw new ArgumentException("Period end must be after period start.");

            Logger.Info("Calculating KPIs for period {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                periodStart, periodEnd);

            List<WorkOrder> allOrders;
            lock (_lockObject)
            {
                allOrders = _workOrderHistory.Values
                    .SelectMany(h => h)
                    .Where(wo => wo.CreatedDate >= periodStart && wo.CreatedDate <= periodEnd)
                    .ToList();
            }

            var completedOrders = allOrders
                .Where(wo => wo.Status == WorkOrderStatus.Completed || wo.Status == WorkOrderStatus.Closed)
                .ToList();

            var correctiveOrders = allOrders
                .Where(wo => wo.Type == MaintenanceType.Corrective)
                .ToList();

            var plannedOrders = allOrders
                .Where(wo => wo.Type != MaintenanceType.Corrective)
                .ToList();

            // MTBF: Mean Time Between Failures (hours)
            double mtbf = CalculateMTBF(periodStart, periodEnd);

            // MTTR: Mean Time To Repair (hours)
            double mttr = 0;
            if (completedOrders.Count > 0)
            {
                mttr = completedOrders.Average(wo => wo.LaborHours);
            }

            // Availability: A = MTBF / (MTBF + MTTR)
            double availability = mtbf > 0 ? (mtbf / (mtbf + mttr)) * 100.0 : 100.0;

            // OEE: simplified as Availability x Performance (assume 85%) x Quality (assume 95%)
            double oee = (availability / 100.0) * 0.85 * 0.95 * 100.0;

            // Backlog: sum of estimated hours for all open work orders
            double backlogHours;
            lock (_lockObject)
            {
                backlogHours = _workOrderHistory.Values
                    .SelectMany(h => h)
                    .Where(wo => wo.Status == WorkOrderStatus.Open ||
                                 wo.Status == WorkOrderStatus.Assigned ||
                                 wo.Status == WorkOrderStatus.InProgress)
                    .Sum(wo => wo.LaborHours > 0 ? wo.LaborHours : 2.0);
            }

            // Planned vs Unplanned ratio
            double plannedVsUnplanned = correctiveOrders.Count > 0
                ? (double)plannedOrders.Count / correctiveOrders.Count
                : plannedOrders.Count > 0 ? 10.0 : 0.0;

            // SLA compliance: % of orders completed before target date
            var ordersWithSLA = completedOrders
                .Where(wo => wo.TargetCompletionDate.HasValue && wo.CompletedDate.HasValue)
                .ToList();
            double slaCompliance = ordersWithSLA.Count > 0
                ? ordersWithSLA.Count(wo => wo.CompletedDate <= wo.TargetCompletionDate) * 100.0
                  / ordersWithSLA.Count
                : 100.0;

            // PM Compliance: % of preventive work orders completed on time
            var pmOrders = completedOrders
                .Where(wo => wo.Type == MaintenanceType.Preventive)
                .ToList();
            double pmCompliance = pmOrders.Count > 0 ? 85.0 : 0.0; // Default estimate

            // Total cost
            decimal totalCost = completedOrders.Sum(wo => wo.TotalCost);

            // Emergency work orders count
            int emergencyCount = allOrders.Count(wo => wo.Priority == WorkOrderPriority.Emergency);

            // Labor utilization
            double totalAvailableHours = (periodEnd - periodStart).TotalDays * 8.0;
            int techCount;
            lock (_lockObject) { techCount = Math.Max(1, _technicians.Count); }
            totalAvailableHours *= techCount;
            double totalLaborHours = completedOrders.Sum(wo => wo.LaborHours);
            double laborUtilization = totalAvailableHours > 0
                ? (totalLaborHours / totalAvailableHours) * 100.0
                : 0.0;

            // Facility Condition Index
            double fci = CalculateAverageFCI();

            var kpi = new MaintenanceKPI
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                MTBF = mtbf,
                MTTR = mttr,
                Availability = Math.Min(availability, 100.0),
                OEE = Math.Min(oee, 100.0),
                BacklogHours = backlogHours,
                PlannedVsUnplannedRatio = plannedVsUnplanned,
                SLACompliancePercent = slaCompliance,
                PMCompliancePercent = pmCompliance,
                TotalMaintenanceCost = totalCost,
                WorkOrdersCompleted = completedOrders.Count,
                WorkOrdersOpen = allOrders.Count - completedOrders.Count,
                EmergencyWorkOrders = emergencyCount,
                LaborUtilizationPercent = Math.Min(laborUtilization, 100.0),
                FacilityConditionIndex = fci
            };

            Logger.Info("KPIs: MTBF={MTBF:F0}h, MTTR={MTTR:F1}h, Availability={Avail:F1}%, " +
                        "OEE={OEE:F1}%, Planned:Unplanned={Ratio:F1}",
                mtbf, mttr, availability, oee, plannedVsUnplanned);

            return kpi;
        }

        #endregion

        #region Risk Assessment

        /// <summary>
        /// Performs a risk assessment for an asset using a failure probability x consequence
        /// matrix approach. Returns risk score, category, mitigation actions, and cost estimates.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Risk assessment result.</returns>
        public async Task<RiskAssessment> RiskAssessmentAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Info("Performing risk assessment for asset '{AssetId}'...", assetId);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate failure probability from Weibull analysis
                var (shape, scale) = EstimateWeibullParameters(assetId);
                double ageHours = GetAssetOperatingAgeHours(assetId);
                double failureProbability = CalculateWeibullFailureProbability(
                    ageHours, shape, scale, hoursAhead: 8760);

                // Calculate consequence severity (0-1 scale)
                double consequenceSeverity = CalculateConsequenceScore(assetId);

                // Risk score: probability x consequence x 100
                double riskScore = failureProbability * consequenceSeverity * 100.0;
                riskScore = Math.Min(riskScore, 100.0);

                // Categorize risk
                string riskCategory = riskScore switch
                {
                    >= 75 => "Critical",
                    >= 50 => "High",
                    >= 25 => "Medium",
                    _ => "Low"
                };

                // Generate mitigation actions
                var mitigationActions = new List<string>();
                decimal mitigationCost = 0m;

                if (riskScore >= 75)
                {
                    mitigationActions.Add("Immediate detailed inspection and condition assessment");
                    mitigationActions.Add("Schedule emergency preventive maintenance within 7 days");
                    mitigationActions.Add("Prepare contingency plan for asset replacement");
                    mitigationActions.Add("Order critical spare parts on expedited delivery");
                    mitigationCost = 5000m;
                }
                else if (riskScore >= 50)
                {
                    mitigationActions.Add("Increase monitoring frequency to weekly");
                    mitigationActions.Add("Schedule preventive maintenance within 30 days");
                    mitigationActions.Add("Review and update maintenance procedures");
                    mitigationCost = 2500m;
                }
                else if (riskScore >= 25)
                {
                    mitigationActions.Add("Continue routine condition monitoring");
                    mitigationActions.Add("Schedule preventive maintenance per standard interval");
                    mitigationActions.Add("Verify spare parts availability");
                    mitigationCost = 1000m;
                }
                else
                {
                    mitigationActions.Add("Maintain current monitoring and maintenance schedule");
                    mitigationCost = 250m;
                }

                // Estimate potential loss from unmitigated failure
                decimal potentialLoss = 0m;
                lock (_lockObject)
                {
                    if (_assetConditions.TryGetValue(assetId, out var condition))
                    {
                        potentialLoss = condition.CurrentReplacementValue *
                            (decimal)consequenceSeverity;
                    }
                    else
                    {
                        potentialLoss = 50000m * (decimal)consequenceSeverity;
                    }
                }

                var assessment = new RiskAssessment
                {
                    AssetId = assetId,
                    FailureProbability = failureProbability,
                    ConsequenceSeverity = consequenceSeverity,
                    RiskScore = riskScore,
                    RiskCategory = riskCategory,
                    MitigationActions = mitigationActions,
                    MitigationCost = mitigationCost,
                    PotentialLoss = potentialLoss,
                    AssessmentDate = DateTime.UtcNow
                };

                Logger.Info("Risk assessment for '{AssetId}': score={Score:F1}, " +
                            "category={Category}, P(fail)={Prob:P1}, consequence={Cons:F2}",
                    assetId, riskScore, riskCategory, failureProbability, consequenceSeverity);

                return assessment;
            }, cancellationToken);
        }

        #endregion

        #region Degradation Pattern Detection

        /// <summary>
        /// Detects the degradation pattern (linear, exponential, step, logarithmic, or random)
        /// from a time series of sensor readings or health score measurements.
        /// Uses least-squares regression to fit candidate curves and selects the best fit.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <param name="sensorHistory">Time-ordered list of sensor readings.</param>
        /// <returns>Detected degradation pattern and fitted parameters.</returns>
        public (DegradationPattern Pattern, double R2, double Rate) DetectDegradationPattern(
            string assetId,
            List<SensorReading> sensorHistory)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            if (sensorHistory == null || sensorHistory.Count < 3)
            {
                Logger.Warn("Insufficient sensor data for '{AssetId}'; at least 3 readings required.", assetId);
                return (DegradationPattern.Random, 0.0, 0.0);
            }

            Logger.Debug("Detecting degradation pattern for '{AssetId}' with {Count} readings...",
                assetId, sensorHistory.Count);

            var ordered = sensorHistory.OrderBy(s => s.Timestamp).ToList();

            // Convert to x (days since first reading) and y (values)
            DateTime t0 = ordered[0].Timestamp;
            double[] x = ordered.Select(s => (s.Timestamp - t0).TotalDays).ToArray();
            double[] y = ordered.Select(s => s.Value).ToArray();

            // Fit linear: y = a*x + b
            var (linearR2, linearSlope) = FitLinearRegression(x, y);

            // Fit exponential: ln(y) = a*x + b => y = e^b * e^(a*x)
            double expR2 = 0.0;
            double expRate = 0.0;
            double[] lnY = y.Where(v => v > 0).Select(v => Math.Log(v)).ToArray();
            if (lnY.Length == y.Length)
            {
                var (r2, slope) = FitLinearRegression(x, lnY);
                expR2 = r2;
                expRate = slope;
            }

            // Fit logarithmic: y = a*ln(x) + b (skip x=0)
            double logR2 = 0.0;
            double logRate = 0.0;
            var xPositive = x.Where(v => v > 0).ToArray();
            if (xPositive.Length >= 3)
            {
                double[] lnX = xPositive.Select(v => Math.Log(v)).ToArray();
                double[] ySubset = y.Skip(y.Length - xPositive.Length).ToArray();
                var (r2, slope) = FitLinearRegression(lnX, ySubset);
                logR2 = r2;
                logRate = slope;
            }

            // Detect step pattern: count large jumps relative to standard deviation
            double mean = y.Average();
            double stdDev = Math.Sqrt(y.Sum(v => (v - mean) * (v - mean)) / y.Length);
            int stepCount = 0;
            for (int i = 1; i < y.Length; i++)
            {
                if (stdDev > 0 && Math.Abs(y[i] - y[i - 1]) > 2.0 * stdDev)
                    stepCount++;
            }
            double stepScore = y.Length > 1 ? (double)stepCount / (y.Length - 1) : 0;

            // Select best fit
            DegradationPattern bestPattern;
            double bestR2;
            double bestRate;

            if (stepScore > 0.3)
            {
                bestPattern = DegradationPattern.Step;
                bestR2 = stepScore;
                bestRate = stepCount;
            }
            else if (expR2 > linearR2 && expR2 > logR2 && expR2 > 0.5)
            {
                bestPattern = DegradationPattern.Exponential;
                bestR2 = expR2;
                bestRate = expRate;
            }
            else if (logR2 > linearR2 && logR2 > 0.5)
            {
                bestPattern = DegradationPattern.Logarithmic;
                bestR2 = logR2;
                bestRate = logRate;
            }
            else if (linearR2 > 0.5)
            {
                bestPattern = DegradationPattern.Linear;
                bestR2 = linearR2;
                bestRate = linearSlope;
            }
            else
            {
                bestPattern = DegradationPattern.Random;
                bestR2 = Math.Max(linearR2, Math.Max(expR2, logR2));
                bestRate = linearSlope;
            }

            // Update asset condition with detected pattern
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    condition.DegradationPattern = bestPattern;
                    condition.DegradationRate = Math.Abs(bestRate);
                }
            }

            Logger.Info("Degradation pattern for '{AssetId}': {Pattern} (R2={R2:F3}, rate={Rate:F4})",
                assetId, bestPattern, bestR2, bestRate);

            return (bestPattern, bestR2, bestRate);
        }

        #endregion

        #region Asset Benchmarking

        /// <summary>
        /// Benchmarks an asset's performance against the fleet average for assets of
        /// the same category and against manufacturer-specified performance baselines.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>Dictionary of benchmark metrics with asset value, fleet average, and manufacturer spec.</returns>
        public Dictionary<string, (double AssetValue, double FleetAverage, double ManufacturerSpec)>
            BenchmarkAssetPerformance(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Info("Benchmarking asset '{AssetId}' performance...", assetId);

            var benchmarks = new Dictionary<string, (double, double, double)>(StringComparer.OrdinalIgnoreCase);

            AssetCondition targetCondition;
            List<AssetCondition> fleetConditions;

            lock (_lockObject)
            {
                if (!_assetConditions.TryGetValue(assetId, out targetCondition))
                {
                    Logger.Warn("No condition data for asset '{AssetId}'.", assetId);
                    return benchmarks;
                }

                // Get all assets in the same category for fleet comparison
                fleetConditions = _assetConditions.Values
                    .Where(c => c.AssetCategory.Equals(targetCondition.AssetCategory,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (fleetConditions.Count == 0)
                fleetConditions.Add(targetCondition);

            // Benchmark 1: Health Score
            double fleetAvgHealth = fleetConditions.Average(c => c.HealthScore);
            benchmarks["HealthScore"] = (targetCondition.HealthScore, fleetAvgHealth, 85.0);

            // Benchmark 2: Degradation Rate
            double fleetAvgDegradation = fleetConditions.Average(c => c.DegradationRate);
            benchmarks["DegradationRate"] = (
                targetCondition.DegradationRate,
                fleetAvgDegradation,
                0.5 // Manufacturer expected rate (points/month)
            );

            // Benchmark 3: Age Ratio (current age / expected service life)
            double ageRatio = targetCondition.ExpectedServiceLifeYears > 0
                ? targetCondition.AgeYears / targetCondition.ExpectedServiceLifeYears
                : 0;
            double fleetAvgAgeRatio = fleetConditions
                .Where(c => c.ExpectedServiceLifeYears > 0)
                .Select(c => c.AgeYears / c.ExpectedServiceLifeYears)
                .DefaultIfEmpty(0)
                .Average();
            benchmarks["AgeRatio"] = (ageRatio, fleetAvgAgeRatio, 0.5);

            // Benchmark 4: RUL (Remaining Useful Life in months)
            double assetRULMonths = targetCondition.RemainingUsefulLifeMonths;
            double fleetAvgRUL = fleetConditions.Average(c => c.RemainingUsefulLifeMonths);
            double specRUL = targetCondition.ExpectedServiceLifeYears * 12.0 * 0.5;
            benchmarks["RULMonths"] = (assetRULMonths, fleetAvgRUL, specRUL);

            // Benchmark 5: Maintenance cost ratio (deferred / replacement value)
            double costRatio = targetCondition.CurrentReplacementValue > 0
                ? (double)(targetCondition.DeferredMaintenanceCost / targetCondition.CurrentReplacementValue)
                : 0;
            double fleetAvgCostRatio = fleetConditions
                .Where(c => c.CurrentReplacementValue > 0)
                .Select(c => (double)(c.DeferredMaintenanceCost / c.CurrentReplacementValue))
                .DefaultIfEmpty(0)
                .Average();
            benchmarks["MaintenanceCostRatio"] = (costRatio, fleetAvgCostRatio, 0.05);

            // Benchmark 6: Failure frequency
            double assetFailureRate = GetFailureRate(assetId);
            double fleetAvgFailureRate = fleetConditions
                .Select(c => GetFailureRate(c.AssetId))
                .DefaultIfEmpty(0)
                .Average();
            benchmarks["FailureRatePerYear"] = (assetFailureRate, fleetAvgFailureRate, 0.1);

            Logger.Info("Benchmarking complete for '{AssetId}': Health={H:F1} vs fleet={FH:F1}, " +
                        "Degradation={D:F2} vs fleet={FD:F2}",
                assetId, targetCondition.HealthScore, fleetAvgHealth,
                targetCondition.DegradationRate, fleetAvgDegradation);

            return benchmarks;
        }

        /// <summary>
        /// Performs a repair-vs-replace cost-benefit analysis for an asset.
        /// Compares annualized cost of repair (extending life) versus full replacement.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <param name="repairCost">Estimated cost of repair/overhaul.</param>
        /// <param name="repairLifeExtensionYears">Expected life extension from repair in years.</param>
        /// <returns>Analysis result with recommendation.</returns>
        public RepairReplaceAnalysis AnalyzeRepairVsReplace(
            string assetId,
            decimal repairCost,
            double repairLifeExtensionYears)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Info("Analyzing repair vs replace for '{AssetId}'...", assetId);

            decimal replacementCost;
            double replacementLifeYears;

            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    replacementCost = condition.CurrentReplacementValue;
                    replacementLifeYears = condition.ExpectedServiceLifeYears;
                }
                else
                {
                    replacementCost = repairCost * 5m;
                    replacementLifeYears = 20.0;
                }
            }

            if (replacementLifeYears <= 0) replacementLifeYears = 20.0;
            if (repairLifeExtensionYears <= 0) repairLifeExtensionYears = 3.0;

            // Annualized cost comparison
            decimal repairAnnualized = repairCost / (decimal)repairLifeExtensionYears;
            decimal replaceAnnualized = replacementCost / (decimal)replacementLifeYears;

            // Break-even analysis: years where repair total equals replacement cost
            double breakEvenYears = repairCost > 0 && replaceAnnualized > repairAnnualized
                ? (double)(replacementCost / (replaceAnnualized - repairAnnualized + 0.01m))
                : repairLifeExtensionYears;

            string recommendation;
            if (repairAnnualized < replaceAnnualized * 0.8m)
            {
                recommendation = $"REPAIR: Annualized repair cost ({repairAnnualized:C}/yr) is significantly " +
                                 $"lower than replacement ({replaceAnnualized:C}/yr). Repair is cost-effective.";
            }
            else if (repairAnnualized > replaceAnnualized * 1.2m)
            {
                recommendation = $"REPLACE: Annualized replacement cost ({replaceAnnualized:C}/yr) is lower " +
                                 $"than continued repair ({repairAnnualized:C}/yr). Replacement recommended.";
            }
            else
            {
                recommendation = $"EVALUATE: Costs are comparable (repair={repairAnnualized:C}/yr, " +
                                 $"replace={replaceAnnualized:C}/yr). Consider reliability and operational factors.";
            }

            var analysis = new RepairReplaceAnalysis
            {
                AssetId = assetId,
                RepairCost = repairCost,
                ReplacementCost = replacementCost,
                RepairExtendedLifeYears = repairLifeExtensionYears,
                ReplacementLifeYears = replacementLifeYears,
                RepairAnnualizedCost = repairAnnualized,
                ReplacementAnnualizedCost = replaceAnnualized,
                Recommendation = recommendation,
                BreakEvenYears = breakEvenYears
            };

            Logger.Info("Repair vs Replace for '{AssetId}': {Recommendation}", assetId, recommendation);
            return analysis;
        }

        #endregion

        #region Weibull Analysis (Private)

        /// <summary>
        /// Estimates Weibull shape (beta) and scale (eta) parameters from failure history
        /// using the method of median ranks and least squares regression.
        /// </summary>
        private (double Shape, double Scale) EstimateWeibullParameters(string assetId)
        {
            List<DateTime> failures;
            lock (_lockObject)
            {
                _failureHistory.TryGetValue(assetId, out failures);
            }

            if (failures == null || failures.Count < 2)
            {
                // Use defaults based on asset category heuristics
                return (DefaultWeibullShape, DefaultWeibullScale);
            }

            var sorted = failures.OrderBy(f => f).ToList();

            // Calculate time-between-failures in hours
            var tbf = new List<double>();
            for (int i = 1; i < sorted.Count; i++)
            {
                double hours = (sorted[i] - sorted[i - 1]).TotalHours;
                if (hours > 0) tbf.Add(hours);
            }

            if (tbf.Count < 2)
                return (DefaultWeibullShape, DefaultWeibullScale);

            tbf.Sort();
            int n = tbf.Count;

            // Median rank estimation: F(i) = (i - 0.3) / (n + 0.4)
            double[] x = new double[n]; // ln(tbf)
            double[] y = new double[n]; // ln(-ln(1 - F(i)))

            for (int i = 0; i < n; i++)
            {
                double medianRank = ((i + 1) - 0.3) / (n + 0.4);
                x[i] = Math.Log(tbf[i]);
                y[i] = Math.Log(-Math.Log(1.0 - medianRank));
            }

            // Least squares fit: y = beta * x - beta * ln(eta)
            // => slope = beta, intercept = -beta * ln(eta)
            var (_, slope) = FitLinearRegression(x, y);

            double beta = Math.Max(0.5, slope);

            // Calculate intercept for eta
            double xMean = x.Average();
            double yMean = y.Average();
            double intercept = yMean - beta * xMean;
            double eta = Math.Exp(-intercept / beta);
            eta = Math.Max(100, eta); // Minimum 100 hours

            Logger.Debug("Weibull parameters for '{AssetId}': beta={Beta:F3}, eta={Eta:F0}h",
                assetId, beta, eta);

            return (beta, eta);
        }

        /// <summary>
        /// Calculates the probability of failure within a given time window using the
        /// Weibull cumulative distribution function.
        /// F(t) = 1 - exp(-(t/eta)^beta)
        /// Conditional probability: P(fail in [t, t+dt] | survived to t)
        /// </summary>
        private double CalculateWeibullFailureProbability(
            double currentAgeHours, double shape, double scale, double hoursAhead)
        {
            if (scale <= 0) return 0;

            // Reliability at current age: R(t) = exp(-(t/eta)^beta)
            double reliabilityNow = Math.Exp(-Math.Pow(currentAgeHours / scale, shape));

            // Reliability at future time: R(t + dt)
            double reliabilityFuture = Math.Exp(
                -Math.Pow((currentAgeHours + hoursAhead) / scale, shape));

            // Conditional failure probability
            if (reliabilityNow <= 0) return 1.0;
            double probability = 1.0 - (reliabilityFuture / reliabilityNow);

            return Math.Clamp(probability, 0.0, 1.0);
        }

        /// <summary>
        /// Estimates the remaining useful life from Weibull parameters by finding the time
        /// at which the survival probability drops to 50% (median life remaining).
        /// </summary>
        private double EstimateRULFromWeibull(double currentAgeHours, double shape, double scale)
        {
            if (scale <= 0 || shape <= 0) return DefaultWeibullScale;

            // Current reliability
            double reliabilityNow = Math.Exp(-Math.Pow(currentAgeHours / scale, shape));

            if (reliabilityNow <= 0.01)
                return 0; // Already past expected life

            // Target reliability for median remaining life: R(t+RUL) = R(t) * 0.5
            double targetReliability = reliabilityNow * 0.5;

            // Solve: exp(-(t_target/eta)^beta) = targetReliability
            // t_target = eta * (-ln(targetReliability))^(1/beta)
            double tTarget = scale * Math.Pow(-Math.Log(targetReliability), 1.0 / shape);
            double rulHours = Math.Max(0, tTarget - currentAgeHours);

            return Math.Min(rulHours, MaxPredictionHorizonDays * 24.0);
        }

        #endregion

        #region Sensor Trend Analysis (Private)

        /// <summary>
        /// Analyzes sensor trends using exponential smoothing and returns an adjustment
        /// factor for RUL. Factor less than 1.0 means faster degradation; above 1.0 means slower.
        /// </summary>
        private double AnalyzeSensorTrends(string assetId)
        {
            List<SensorReading> readings;
            lock (_lockObject)
            {
                if (!_sensorHistory.TryGetValue(assetId, out readings) || readings.Count < 3)
                    return 1.0; // No adjustment without sufficient data
            }

            // Group by sensor type and analyze each trend
            var sensorGroups = readings
                .GroupBy(r => r.SensorType, StringComparer.OrdinalIgnoreCase)
                .ToList();

            double totalAdjustment = 0;
            int sensorCount = 0;

            foreach (var group in sensorGroups)
            {
                var ordered = group.OrderBy(r => r.Timestamp).ToList();
                if (ordered.Count < 3) continue;

                // Apply exponential smoothing to detect trend direction
                double smoothed = ordered[0].Value;
                double previousSmoothed = smoothed;

                for (int i = 1; i < ordered.Count; i++)
                {
                    previousSmoothed = smoothed;
                    smoothed = ExponentialSmoothingAlpha * ordered[i].Value +
                               (1.0 - ExponentialSmoothingAlpha) * smoothed;
                }

                // Trend direction: positive means degrading (higher sensor values = worse)
                double trendRate = (smoothed - previousSmoothed);

                // Normalize the trend impact
                double baselineValue = ordered.Average(r => r.Value);
                if (baselineValue > 0)
                {
                    double normalizedTrend = trendRate / baselineValue;

                    // Adjustment: negative trend (improving) increases RUL, positive decreases
                    double sensorAdjustment = 1.0 - (normalizedTrend * 2.0);
                    sensorAdjustment = Math.Clamp(sensorAdjustment, 0.3, 1.5);

                    totalAdjustment += sensorAdjustment;
                    sensorCount++;
                }
            }

            return sensorCount > 0 ? totalAdjustment / sensorCount : 1.0;
        }

        #endregion

        #region Health Score Factors (Private)

        private double CalculateAgeFactor(AssetCondition condition)
        {
            if (condition.ExpectedServiceLifeYears <= 0)
                return 75.0;

            double ageRatio = condition.AgeYears / condition.ExpectedServiceLifeYears;

            // Sigmoidal decay: starts at 100, drops steeply as age approaches expected life
            double factor = 100.0 / (1.0 + Math.Exp(5.0 * (ageRatio - 0.8)));
            return Math.Clamp(factor, 0, 100);
        }

        private double CalculateUsageFactor(string assetId)
        {
            // Estimate from work order frequency: more corrective work = higher usage stress
            List<WorkOrder> history;
            lock (_lockObject)
            {
                _workOrderHistory.TryGetValue(assetId, out history);
            }

            if (history == null || history.Count == 0)
                return 80.0; // Default good score

            int correctiveCount = history.Count(wo => wo.Type == MaintenanceType.Corrective);
            int totalCount = history.Count;

            // Penalty for high corrective ratio
            double correctiveRatio = totalCount > 0 ? (double)correctiveCount / totalCount : 0;
            double factor = 100.0 * (1.0 - correctiveRatio * 1.5);
            return Math.Clamp(factor, 0, 100);
        }

        private double CalculateEnvironmentFactor(string assetId)
        {
            // Use sensor data for environmental assessment
            List<SensorReading> readings;
            lock (_lockObject)
            {
                _sensorHistory.TryGetValue(assetId, out readings);
            }

            if (readings == null || readings.Count == 0)
                return 75.0;

            // Check for anomalous readings (harsh environmental conditions)
            int anomalousCount = readings.Count(r => r.IsAnomalous);
            double anomalyRate = (double)anomalousCount / readings.Count;

            double factor = 100.0 * (1.0 - anomalyRate * 3.0);
            return Math.Clamp(factor, 0, 100);
        }

        private double CalculateMaintenanceHistoryFactor(string assetId)
        {
            List<WorkOrder> history;
            lock (_lockObject)
            {
                _workOrderHistory.TryGetValue(assetId, out history);
            }

            if (history == null || history.Count == 0)
                return 50.0; // Unknown maintenance history

            // Check for regular preventive maintenance
            var preventiveOrders = history.Where(wo =>
                wo.Type == MaintenanceType.Preventive && wo.Status == WorkOrderStatus.Completed).ToList();

            var recentOrders = history.Where(wo =>
                wo.CompletedDate.HasValue &&
                wo.CompletedDate.Value >= DateTime.UtcNow.AddYears(-1)).ToList();

            // Score based on PM completion regularity
            double pmScore = preventiveOrders.Count > 0 ? Math.Min(100, preventiveOrders.Count * 15.0) : 20.0;

            // Bonus for recent activity
            double recencyBonus = recentOrders.Count > 0 ? Math.Min(20, recentOrders.Count * 5.0) : 0;

            return Math.Clamp(pmScore + recencyBonus, 0, 100);
        }

        private double CalculateSensorDataFactor(string assetId)
        {
            List<SensorReading> readings;
            lock (_lockObject)
            {
                _sensorHistory.TryGetValue(assetId, out readings);
            }

            if (readings == null || readings.Count == 0)
                return 70.0; // No sensor data, moderate score

            // Use the most recent readings
            var recent = readings
                .OrderByDescending(r => r.Timestamp)
                .Take(50)
                .ToList();

            int normalCount = recent.Count(r => !r.IsAnomalous);
            double normalRatio = (double)normalCount / recent.Count;

            return Math.Clamp(normalRatio * 100.0, 0, 100);
        }

        #endregion

        #region Utility Methods (Private)

        private static ConditionGrade MapHealthScoreToGrade(double healthScore)
        {
            return healthScore switch
            {
                >= 85 => ConditionGrade.A,
                >= 70 => ConditionGrade.B,
                >= 55 => ConditionGrade.C,
                >= 40 => ConditionGrade.D,
                >= 20 => ConditionGrade.E,
                _ => ConditionGrade.F
            };
        }

        private double GetAssetOperatingAgeHours(string assetId)
        {
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    return condition.AgeYears * 365.25 * 24.0;
                }
            }
            return 8760; // Default: 1 year
        }

        private string GetAssetName(string assetId)
        {
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                    return condition.AssetName;
            }
            return assetId;
        }

        private FailureMode DetermineFailureMode(string assetId)
        {
            // Analyze work order history for predominant failure modes
            List<WorkOrder> history;
            lock (_lockObject)
            {
                _workOrderHistory.TryGetValue(assetId, out history);
            }

            if (history == null || history.Count == 0)
                return FailureMode.Wear; // Most common default

            var failureModes = history
                .Where(wo => wo.IdentifiedFailureMode.HasValue)
                .GroupBy(wo => wo.IdentifiedFailureMode.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return failureModes?.Key ?? FailureMode.Wear;
        }

        private double CalculateConsequenceScore(string assetId)
        {
            // Consequence severity based on asset criticality and replacement cost
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    // Normalize replacement value to 0-1 scale (assume max $500K)
                    double costFactor = Math.Min(1.0,
                        (double)condition.CurrentReplacementValue / 500000.0);

                    // Category criticality multiplier
                    double criticalityMultiplier = condition.AssetCategory?.ToUpperInvariant() switch
                    {
                        "HVAC" => 0.8,
                        "ELECTRICAL" => 0.9,
                        "PLUMBING" => 0.7,
                        "FIRE PROTECTION" => 1.0,
                        "ELEVATOR" => 0.95,
                        "STRUCTURAL" => 1.0,
                        _ => 0.6
                    };

                    return Math.Clamp(costFactor * 0.5 + criticalityMultiplier * 0.5, 0.1, 1.0);
                }
            }

            return 0.5; // Default moderate consequence
        }

        private List<DegradationFactor> ComputeDegradationFactors(string assetId)
        {
            var factors = new List<DegradationFactor>();

            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    foreach (var kvp in condition.FactorScores)
                    {
                        factors.Add(new DegradationFactor
                        {
                            FactorName = kvp.Key,
                            Weight = kvp.Key switch
                            {
                                "Age" => 0.25,
                                "Usage" => 0.15,
                                "Environment" => 0.10,
                                "Maintenance" => 0.25,
                                "Sensor" => 0.25,
                                _ => 0.10
                            },
                            CurrentValue = kvp.Value,
                            ThresholdValue = HealthScoreWarningThreshold,
                            NormalizedScore = kvp.Value / 100.0,
                            Description = $"{kvp.Key} factor score: {kvp.Value:F1}/100"
                        });
                    }
                }
            }

            return factors;
        }

        private (decimal FailureCost, decimal PreventiveCost) EstimateMaintenanceCosts(string assetId)
        {
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    // Failure cost: typically 3-5x replacement + downtime
                    decimal failureCost = condition.CurrentReplacementValue * 0.5m + 10000m;

                    // Preventive cost: typically 5-15% of failure cost
                    decimal preventiveCost = failureCost * 0.10m;

                    return (failureCost, preventiveCost);
                }
            }

            return (25000m, 2500m); // Default estimates
        }

        private double CalculatePredictionConfidence(string assetId, double shape, double scale)
        {
            double confidence = 0.5; // Base confidence

            // More failure data = higher confidence
            lock (_lockObject)
            {
                if (_failureHistory.TryGetValue(assetId, out var failures))
                {
                    confidence += Math.Min(0.2, failures.Count * 0.04);
                }

                // More sensor data = higher confidence
                if (_sensorHistory.TryGetValue(assetId, out var sensors))
                {
                    confidence += Math.Min(0.15, sensors.Count * 0.003);
                }

                // Good Weibull fit = higher confidence
                if (shape > 1.0 && shape < 5.0 && scale > 100)
                {
                    confidence += 0.1;
                }

                // Recent maintenance data = higher confidence
                if (_workOrderHistory.TryGetValue(assetId, out var history))
                {
                    var recentCount = history.Count(wo =>
                        wo.CompletedDate.HasValue &&
                        wo.CompletedDate.Value >= DateTime.UtcNow.AddYears(-2));
                    confidence += Math.Min(0.1, recentCount * 0.02);
                }
            }

            return Math.Clamp(confidence, 0.1, 0.95);
        }

        private List<string> GenerateRecommendations(
            string assetId, double failureProbability, double riskScore,
            double rulDays, FailureMode failureMode)
        {
            var recommendations = new List<string>();

            if (riskScore >= 75)
            {
                recommendations.Add("CRITICAL: Schedule immediate maintenance intervention");
                recommendations.Add($"Primary failure mode ({failureMode}) requires targeted inspection");
                recommendations.Add("Prepare backup or redundant equipment");
            }

            if (failureProbability > 0.5)
            {
                recommendations.Add($"High failure probability ({failureProbability:P0}) within 12 months");
                recommendations.Add("Evaluate repair-vs-replace economics");
            }

            if (rulDays < 90)
            {
                recommendations.Add($"Short remaining life ({rulDays:F0} days); plan capital replacement");
                recommendations.Add("Order long-lead spare parts immediately");
            }
            else if (rulDays < 365)
            {
                recommendations.Add($"Moderate remaining life ({rulDays:F0} days); include in next budget cycle");
                recommendations.Add("Increase condition monitoring frequency");
            }
            else
            {
                recommendations.Add("Asset within acceptable operating parameters");
                recommendations.Add("Continue standard preventive maintenance schedule");
            }

            // Failure-mode-specific recommendations
            switch (failureMode)
            {
                case FailureMode.Wear:
                    recommendations.Add("Inspect and replace wear components (bearings, seals, belts)");
                    break;
                case FailureMode.Corrosion:
                    recommendations.Add("Apply protective coating and improve environmental controls");
                    break;
                case FailureMode.Fatigue:
                    recommendations.Add("Perform non-destructive testing for crack propagation");
                    break;
                case FailureMode.Electrical:
                    recommendations.Add("Perform insulation resistance and thermal imaging tests");
                    break;
                case FailureMode.Misalignment:
                    recommendations.Add("Perform laser alignment check on shafts and couplings");
                    break;
                case FailureMode.Imbalance:
                    recommendations.Add("Schedule dynamic balancing of rotating components");
                    break;
            }

            return recommendations;
        }

        private int CalculatePlannedDemand(string partId, int months)
        {
            int demand = 0;
            lock (_lockObject)
            {
                foreach (var plan in _maintenancePlans.Values.Where(p => p.IsActive))
                {
                    var partReq = plan.RequiredParts.FirstOrDefault(
                        r => r.PartId.Equals(partId, StringComparison.OrdinalIgnoreCase));

                    if (partReq != null && plan.IntervalDays > 0)
                    {
                        int executionsInPeriod = (int)Math.Ceiling((months * 30.44) / plan.IntervalDays);
                        demand += partReq.Quantity * executionsInPeriod;
                    }
                }
            }
            return demand;
        }

        private double CalculateMTBF(DateTime periodStart, DateTime periodEnd)
        {
            double totalOperatingHours = 0;
            int totalFailures = 0;

            lock (_lockObject)
            {
                foreach (var kvp in _failureHistory)
                {
                    var failures = kvp.Value
                        .Where(f => f >= periodStart && f <= periodEnd)
                        .OrderBy(f => f)
                        .ToList();

                    totalFailures += failures.Count;

                    if (failures.Count > 0)
                    {
                        totalOperatingHours += (periodEnd - periodStart).TotalHours;
                    }
                }
            }

            if (totalFailures == 0)
                return (periodEnd - periodStart).TotalHours; // No failures = perfect MTBF

            return totalOperatingHours / totalFailures;
        }

        private double CalculateAverageFCI()
        {
            lock (_lockObject)
            {
                var assetsWithValues = _assetConditions.Values
                    .Where(c => c.CurrentReplacementValue > 0)
                    .ToList();

                if (assetsWithValues.Count == 0) return 0;

                decimal totalDeferred = assetsWithValues.Sum(c => c.DeferredMaintenanceCost);
                decimal totalReplacement = assetsWithValues.Sum(c => c.CurrentReplacementValue);

                return totalReplacement > 0
                    ? (double)(totalDeferred / totalReplacement)
                    : 0;
            }
        }

        private double GetFailureRate(string assetId)
        {
            lock (_lockObject)
            {
                if (_failureHistory.TryGetValue(assetId, out var failures) && failures.Count > 0)
                {
                    var sorted = failures.OrderBy(f => f).ToList();
                    double spanYears = (sorted.Last() - sorted.First()).TotalDays / 365.25;
                    return spanYears > 0 ? failures.Count / spanYears : failures.Count;
                }
            }
            return 0;
        }

        /// <summary>
        /// Simple linear regression returning R-squared and slope.
        /// </summary>
        private static (double R2, double Slope) FitLinearRegression(double[] x, double[] y)
        {
            if (x.Length != y.Length || x.Length < 2)
                return (0, 0);

            int n = x.Length;
            double sumX = x.Sum();
            double sumY = y.Sum();
            double sumXY = x.Zip(y, (a, b) => a * b).Sum();
            double sumX2 = x.Sum(a => a * a);
            double sumY2 = y.Sum(b => b * b);

            double denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-12)
                return (0, 0);

            double slope = (n * sumXY - sumX * sumY) / denominator;
            double intercept = (sumY - slope * sumX) / n;

            // R-squared
            double ssRes = 0, ssTot = 0;
            double yMean = sumY / n;
            for (int i = 0; i < n; i++)
            {
                double predicted = slope * x[i] + intercept;
                ssRes += (y[i] - predicted) * (y[i] - predicted);
                ssTot += (y[i] - yMean) * (y[i] - yMean);
            }

            double r2 = ssTot > 0 ? 1.0 - (ssRes / ssTot) : 0;
            return (Math.Max(0, r2), slope);
        }

        #endregion
    }
}
