// ============================================================================
// StingBIM AI - Facility Management Predictive Analytics
// AI-powered failure prediction, demand forecasting, and budget projection
// Integrates with existing Predictive Maintenance Scheduler
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.Knowledge;
using StingBIM.AI.FacilityManagement.WorkOrders;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Prediction Models

    /// <summary>
    /// Equipment failure prediction result
    /// </summary>
    public class FailurePrediction
    {
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string EquipmentType { get; set; } = string.Empty;
        public Guid? RevitElementGuid { get; set; }

        // Prediction Results
        public double FailureProbability { get; set; }
        public string PredictedFailureMode { get; set; } = string.Empty;
        public DateTime PredictedFailureDate { get; set; }
        public int DaysUntilPredictedFailure { get; set; }
        public PredictionConfidence Confidence { get; set; }

        // Risk Assessment
        public string RiskLevel { get; set; } = string.Empty; // Critical, High, Medium, Low
        public double RiskScore { get; set; }
        public string ImpactDescription { get; set; } = string.Empty;
        public decimal EstimatedFailureCost { get; set; }
        public decimal PreventiveMaintenanceCost { get; set; }
        public decimal CostAvoidancePotential { get; set; }

        // Contributing Factors
        public List<PredictionFactor> ContributingFactors { get; set; } = new();

        // Recommendations
        public string RecommendedAction { get; set; } = string.Empty;
        public WorkOrderPriority RecommendedPriority { get; set; }
        public DateTime RecommendedActionDate { get; set; }

        // Metadata
        public DateTime PredictionDate { get; set; } = DateTime.UtcNow;
        public string ModelVersion { get; set; } = "1.0";
    }

    public enum PredictionConfidence
    {
        VeryHigh,  // >90% confidence
        High,      // 75-90%
        Medium,    // 50-75%
        Low,       // 25-50%
        VeryLow    // <25%
    }

    /// <summary>
    /// Factor contributing to failure prediction
    /// </summary>
    public class PredictionFactor
    {
        public string FactorName { get; set; } = string.Empty;
        public string FactorType { get; set; } = string.Empty; // Age, Usage, Environment, History
        public double Weight { get; set; } // 0-1
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public string Impact { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maintenance demand forecast
    /// </summary>
    public class MaintenanceDemandForecast
    {
        public DateTime ForecastPeriodStart { get; set; }
        public DateTime ForecastPeriodEnd { get; set; }
        public string PeriodLabel { get; set; } = string.Empty; // e.g., "Q1 2026"

        // Work Order Projections
        public int PredictedPreventiveWorkOrders { get; set; }
        public int PredictedCorrectiveWorkOrders { get; set; }
        public int PredictedEmergencyWorkOrders { get; set; }
        public int TotalPredictedWorkOrders { get; set; }

        // Labor Demand
        public Dictionary<string, double> LaborHoursBySkill { get; set; } = new();
        public double TotalLaborHours { get; set; }
        public int TechnicianDaysRequired { get; set; }

        // Resource Demand
        public List<PartsDemandForecast> PredictedPartsDemand { get; set; } = new();
        public List<ContractorDemandForecast> PredictedContractorDemand { get; set; } = new();

        // Cost Projections
        public decimal ProjectedLaborCost { get; set; }
        public decimal ProjectedPartsCost { get; set; }
        public decimal ProjectedContractorCost { get; set; }
        public decimal TotalProjectedCost { get; set; }

        // Confidence
        public PredictionConfidence Confidence { get; set; }
        public string ForecastNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parts demand forecast
    /// </summary>
    public class PartsDemandForecast
    {
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public int ProjectedQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public int CurrentInventory { get; set; }
        public int ReorderQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public bool RequiresReorder { get; set; }
    }

    /// <summary>
    /// Contractor demand forecast
    /// </summary>
    public class ContractorDemandForecast
    {
        public string ContractorType { get; set; } = string.Empty; // Elevator, Fire, HVAC specialist
        public int ProjectedVisits { get; set; }
        public double ProjectedHours { get; set; }
        public decimal ProjectedCost { get; set; }
        public List<string> PlannedActivities { get; set; } = new();
    }

    /// <summary>
    /// Budget projection for FM operations
    /// </summary>
    public class FMBudgetProjection
    {
        public int Year { get; set; }
        public string Scenario { get; set; } = "Baseline"; // Baseline, Optimistic, Pessimistic

        // Monthly Breakdown
        public List<MonthlyBudgetProjection> MonthlyProjections { get; set; } = new();

        // Category Breakdown
        public Dictionary<string, decimal> BudgetByCategory { get; set; } = new();
        public Dictionary<string, decimal> BudgetBySystem { get; set; } = new();
        public Dictionary<string, decimal> BudgetByWorkOrderType { get; set; } = new();

        // Totals
        public decimal TotalLaborBudget { get; set; }
        public decimal TotalPartsBudget { get; set; }
        public decimal TotalContractorBudget { get; set; }
        public decimal TotalUtilitiesBudget { get; set; }
        public decimal TotalBudget { get; set; }

        // Capital Planning
        public List<CapitalExpenseForecast> CapitalExpenses { get; set; } = new();
        public decimal TotalCapitalBudget { get; set; }

        // Comparison
        public decimal PreviousYearActual { get; set; }
        public decimal YearOverYearChange { get; set; }
        public double YearOverYearChangePercent { get; set; }

        // Confidence and Risk
        public PredictionConfidence Confidence { get; set; }
        public decimal BudgetVarianceRange { get; set; } // +/- amount
        public List<BudgetRisk> IdentifiedRisks { get; set; } = new();
    }

    public class MonthlyBudgetProjection
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal LaborCost { get; set; }
        public decimal PartsCost { get; set; }
        public decimal ContractorCost { get; set; }
        public decimal UtilitiesCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class CapitalExpenseForecast
    {
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string ExpenseType { get; set; } = string.Empty; // Replacement, Major Overhaul, Upgrade
        public DateTime ProjectedDate { get; set; }
        public decimal EstimatedCost { get; set; }
        public string Justification { get; set; } = string.Empty;
        public PredictionConfidence Confidence { get; set; }
    }

    public class BudgetRisk
    {
        public string RiskDescription { get; set; } = string.Empty;
        public string RiskCategory { get; set; } = string.Empty;
        public double Probability { get; set; }
        public decimal PotentialImpact { get; set; }
        public string Mitigation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Equipment health score
    /// </summary>
    public class EquipmentHealthScore
    {
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string EquipmentType { get; set; } = string.Empty;

        // Health Score (0-100)
        public double OverallHealthScore { get; set; }
        public string HealthStatus { get; set; } = string.Empty; // Excellent, Good, Fair, Poor, Critical

        // Component Scores
        public Dictionary<string, double> ComponentScores { get; set; } = new();

        // Trend
        public string HealthTrend { get; set; } = string.Empty; // Improving, Stable, Declining
        public double TrendRate { get; set; } // Change per month

        // Factors
        public double AgeScore { get; set; }
        public double MaintenanceScore { get; set; }
        public double PerformanceScore { get; set; }
        public double ReliabilityScore { get; set; }

        // Remaining Useful Life
        public int EstimatedRemainingLifeMonths { get; set; }
        public double RemainingLifeConfidence { get; set; }

        public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region FM Predictive Analytics Engine

    /// <summary>
    /// Facility Management Predictive Analytics Engine
    /// Provides AI-powered predictions for maintenance planning
    /// </summary>
    public class FMPredictiveAnalytics
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly FMKnowledgeBase _knowledgeBase;
        private readonly AssetRegistry _assetRegistry;

        // Historical data for predictions (in production, would come from database)
        private readonly Dictionary<string, List<MaintenanceEvent>> _maintenanceHistory = new();
        private readonly Dictionary<string, List<SensorReading>> _sensorData = new();

        public FMPredictiveAnalytics(FMKnowledgeBase knowledgeBase, AssetRegistry assetRegistry = null)
        {
            _knowledgeBase = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
            _assetRegistry = assetRegistry;

            Logger.Info("FM Predictive Analytics Engine initialized");
        }

        #region Failure Prediction

        /// <summary>
        /// Predict failures for all assets
        /// </summary>
        public List<FailurePrediction> PredictFailures(int forecastDays = 90)
        {
            var predictions = new List<FailurePrediction>();

            if (_assetRegistry == null)
            {
                Logger.Warn("No asset registry connected, using simulated predictions");
                return GenerateSimulatedPredictions(forecastDays);
            }

            var assets = _assetRegistry.GetAllAssets();
            foreach (var asset in assets)
            {
                var prediction = PredictAssetFailure(asset, forecastDays);
                if (prediction != null && prediction.FailureProbability > 0.1) // Only include if >10% probability
                {
                    predictions.Add(prediction);
                }
            }

            return predictions.OrderByDescending(p => p.RiskScore).ToList();
        }

        /// <summary>
        /// Predict failure for specific asset
        /// </summary>
        public FailurePrediction PredictAssetFailure(Asset asset, int forecastDays = 90)
        {
            var knowledge = _knowledgeBase.GetEquipmentKnowledge(asset.AssetType);
            var failureModes = _knowledgeBase.GetFailureModes(asset.AssetType);

            if (knowledge == null)
            {
                Logger.Debug($"No knowledge available for asset type: {asset.AssetType}");
                return null;
            }

            // Calculate age factor
            var ageFactor = CalculateAgeFactor(asset, knowledge);

            // Calculate maintenance factor (based on compliance with PM schedule)
            var maintenanceFactor = CalculateMaintenanceFactor(asset, knowledge);

            // Calculate usage factor (if run hours available)
            var usageFactor = CalculateUsageFactor(asset, knowledge);

            // Calculate environmental factor
            var environmentFactor = 1.0; // Baseline, would be adjusted based on sensor data

            // Determine most likely failure mode
            var likelyFailureMode = DetermineLikelyFailureMode(failureModes, ageFactor, maintenanceFactor);

            // Calculate combined failure probability
            var baseFailureRate = likelyFailureMode?.OccurrenceProbability ?? 0.05;
            var failureProbability = CalculateFailureProbability(
                baseFailureRate,
                ageFactor,
                maintenanceFactor,
                usageFactor,
                environmentFactor,
                forecastDays
            );

            // Calculate risk score
            var riskScore = CalculateRiskScore(failureProbability, likelyFailureMode, asset);

            // Estimate costs
            var failureCost = likelyFailureMode?.TypicalRepairCost ?? knowledge.TypicalMaintenanceCostPerYear * 0.5m;
            var preventiveCost = failureCost * 0.3m; // PM typically 30% of repair cost

            var prediction = new FailurePrediction
            {
                AssetId = asset.AssetId,
                AssetName = asset.AssetName,
                EquipmentType = asset.AssetType,
                RevitElementGuid = asset.RevitElementGuid,
                FailureProbability = failureProbability,
                PredictedFailureMode = likelyFailureMode?.Name ?? "General wear",
                PredictedFailureDate = DateTime.UtcNow.AddDays(forecastDays * (1 - failureProbability)),
                DaysUntilPredictedFailure = (int)(forecastDays * (1 - failureProbability)),
                Confidence = DetermineConfidence(failureProbability, ageFactor),
                RiskLevel = DetermineRiskLevel(riskScore),
                RiskScore = riskScore,
                ImpactDescription = likelyFailureMode?.ImpactDescription ?? "Potential service disruption",
                EstimatedFailureCost = failureCost,
                PreventiveMaintenanceCost = preventiveCost,
                CostAvoidancePotential = failureCost - preventiveCost,
                ContributingFactors = new List<PredictionFactor>
                {
                    new PredictionFactor
                    {
                        FactorName = "Age",
                        FactorType = "Age",
                        Weight = 0.3,
                        CurrentValue = ageFactor,
                        ThresholdValue = 1.5,
                        Impact = ageFactor > 1.0 ? "Accelerating degradation" : "Within expected range"
                    },
                    new PredictionFactor
                    {
                        FactorName = "Maintenance Compliance",
                        FactorType = "History",
                        Weight = 0.25,
                        CurrentValue = maintenanceFactor,
                        ThresholdValue = 1.2,
                        Impact = maintenanceFactor > 1.0 ? "Maintenance gaps identified" : "Well maintained"
                    },
                    new PredictionFactor
                    {
                        FactorName = "Usage Intensity",
                        FactorType = "Usage",
                        Weight = 0.25,
                        CurrentValue = usageFactor,
                        ThresholdValue = 1.3,
                        Impact = usageFactor > 1.0 ? "High utilization" : "Normal utilization"
                    }
                },
                RecommendedAction = DetermineRecommendedAction(failureProbability, likelyFailureMode),
                RecommendedPriority = DetermineWorkOrderPriority(riskScore),
                RecommendedActionDate = DateTime.UtcNow.AddDays(Math.Max(1, (forecastDays * (1 - failureProbability)) - 7))
            };

            return prediction;
        }

        private double CalculateAgeFactor(Asset asset, EquipmentKnowledge knowledge)
        {
            if (asset.InstallationDate == default)
                return 1.0;

            var ageYears = (DateTime.UtcNow - asset.InstallationDate).TotalDays / 365.25;
            var expectedLife = knowledge.TypicalLifespanYears;

            // Weibull-like aging factor
            var ageRatio = ageYears / expectedLife;

            if (ageRatio < 0.3) return 0.5;      // Infant mortality phase passed
            if (ageRatio < 0.7) return 1.0;      // Normal operation phase
            if (ageRatio < 1.0) return 1.5;      // Wear-out phase beginning
            if (ageRatio < 1.2) return 2.0;      // Beyond expected life
            return 3.0;                           // Significantly over expected life
        }

        private double CalculateMaintenanceFactor(Asset asset, EquipmentKnowledge knowledge)
        {
            // In production, this would analyze actual maintenance history
            // For now, use a baseline with some variation

            if (!asset.LastMaintenanceDate.HasValue)
                return 1.5; // No maintenance recorded

            var daysSinceMaintenance = (DateTime.UtcNow - asset.LastMaintenanceDate.Value).TotalDays;

            // Assume monthly PM for most equipment
            var expectedInterval = 30.0;

            if (daysSinceMaintenance < expectedInterval) return 0.8;
            if (daysSinceMaintenance < expectedInterval * 1.5) return 1.0;
            if (daysSinceMaintenance < expectedInterval * 2) return 1.2;
            if (daysSinceMaintenance < expectedInterval * 3) return 1.5;
            return 2.0;
        }

        private double CalculateUsageFactor(Asset asset, EquipmentKnowledge knowledge)
        {
            // Would be calculated from run hours, cycles, etc.
            // Using baseline for demonstration
            return 1.0;
        }

        private FailureMode DetermineLikelyFailureMode(
            List<FailureMode> failureModes,
            double ageFactor,
            double maintenanceFactor)
        {
            if (!failureModes.Any())
                return null;

            // Weight failure modes by their probability adjusted for current conditions
            var weighted = failureModes
                .Select(fm => new
                {
                    Mode = fm,
                    AdjustedProbability = fm.OccurrenceProbability * ageFactor *
                        (maintenanceFactor > 1.0 && fm.CommonCauses.Contains("Lack of maintenance") ? maintenanceFactor : 1.0)
                })
                .OrderByDescending(x => x.AdjustedProbability)
                .FirstOrDefault();

            return weighted?.Mode;
        }

        private double CalculateFailureProbability(
            double baseRate,
            double ageFactor,
            double maintenanceFactor,
            double usageFactor,
            double environmentFactor,
            int forecastDays)
        {
            // Exponential failure probability over time
            var dailyRate = baseRate / 365.0;
            var adjustedRate = dailyRate * ageFactor * maintenanceFactor * usageFactor * environmentFactor;

            // Probability of at least one failure in forecast period
            var probability = 1 - Math.Exp(-adjustedRate * forecastDays);

            return Math.Min(0.99, Math.Max(0, probability));
        }

        private double CalculateRiskScore(double failureProbability, FailureMode failureMode, Asset asset)
        {
            // Risk = Probability × Impact
            var severityMultiplier = failureMode?.Severity switch
            {
                FailureSeverity.Catastrophic => 10,
                FailureSeverity.Critical => 8,
                FailureSeverity.Major => 6,
                FailureSeverity.Minor => 4,
                FailureSeverity.Negligible => 2,
                _ => 5
            };

            var criticalityMultiplier = asset.Criticality switch
            {
                AssetCriticality.Critical => 2.0,
                AssetCriticality.High => 1.5,
                AssetCriticality.Medium => 1.0,
                AssetCriticality.Low => 0.5,
                _ => 1.0
            };

            return failureProbability * severityMultiplier * criticalityMultiplier * 10;
        }

        private string DetermineRiskLevel(double riskScore)
        {
            if (riskScore >= 80) return "Critical";
            if (riskScore >= 60) return "High";
            if (riskScore >= 40) return "Medium";
            if (riskScore >= 20) return "Low";
            return "Minimal";
        }

        private PredictionConfidence DetermineConfidence(double probability, double ageFactor)
        {
            // Higher confidence when probability is clear (very high or very low)
            // and when we have good age data
            var certainty = Math.Abs(probability - 0.5) * 2; // 0 at 50%, 1 at 0% or 100%

            if (certainty > 0.8) return PredictionConfidence.VeryHigh;
            if (certainty > 0.6) return PredictionConfidence.High;
            if (certainty > 0.4) return PredictionConfidence.Medium;
            if (certainty > 0.2) return PredictionConfidence.Low;
            return PredictionConfidence.VeryLow;
        }

        private string DetermineRecommendedAction(double probability, FailureMode failureMode)
        {
            if (probability > 0.8)
                return $"Immediate inspection and {failureMode?.RecommendedAction ?? "preventive maintenance"} required";
            if (probability > 0.6)
                return $"Schedule {failureMode?.RecommendedAction ?? "maintenance"} within 1 week";
            if (probability > 0.4)
                return $"Plan {failureMode?.RecommendedAction ?? "preventive maintenance"} within 2 weeks";
            if (probability > 0.2)
                return "Include in next scheduled maintenance cycle";
            return "Continue normal monitoring";
        }

        private WorkOrderPriority DetermineWorkOrderPriority(double riskScore)
        {
            if (riskScore >= 80) return WorkOrderPriority.Emergency;
            if (riskScore >= 60) return WorkOrderPriority.Urgent;
            if (riskScore >= 40) return WorkOrderPriority.High;
            if (riskScore >= 20) return WorkOrderPriority.Medium;
            return WorkOrderPriority.Low;
        }

        private List<FailurePrediction> GenerateSimulatedPredictions(int forecastDays)
        {
            // Generate sample predictions when no real asset data available
            return new List<FailurePrediction>
            {
                new FailurePrediction
                {
                    AssetId = "AHU-001",
                    AssetName = "Main AHU Level 1",
                    EquipmentType = "AHU",
                    FailureProbability = 0.65,
                    PredictedFailureMode = "Belt Failure",
                    DaysUntilPredictedFailure = 12,
                    Confidence = PredictionConfidence.High,
                    RiskLevel = "High",
                    RiskScore = 68,
                    EstimatedFailureCost = 2500000,
                    PreventiveMaintenanceCost = 500000,
                    RecommendedAction = "Replace fan belts and inspect pulleys",
                    RecommendedPriority = WorkOrderPriority.High
                },
                new FailurePrediction
                {
                    AssetId = "CHI-001",
                    AssetName = "Chiller 1",
                    EquipmentType = "Chiller",
                    FailureProbability = 0.35,
                    PredictedFailureMode = "Tube Fouling",
                    DaysUntilPredictedFailure = 45,
                    Confidence = PredictionConfidence.Medium,
                    RiskLevel = "Medium",
                    RiskScore = 42,
                    EstimatedFailureCost = 8000000,
                    PreventiveMaintenanceCost = 2000000,
                    RecommendedAction = "Schedule tube cleaning during low-load period",
                    RecommendedPriority = WorkOrderPriority.Medium
                }
            };
        }

        #endregion

        #region Demand Forecasting

        /// <summary>
        /// Forecast maintenance demand for upcoming period
        /// </summary>
        public MaintenanceDemandForecast ForecastDemand(DateTime startDate, int months = 3)
        {
            var endDate = startDate.AddMonths(months);

            var forecast = new MaintenanceDemandForecast
            {
                ForecastPeriodStart = startDate,
                ForecastPeriodEnd = endDate,
                PeriodLabel = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}"
            };

            // Estimate work orders by type
            var workingDays = CountWorkingDays(startDate, endDate);
            var assetCount = _assetRegistry?.GetAllAssets().Count() ?? 100;

            // PM work orders: Based on maintenance schedules
            forecast.PredictedPreventiveWorkOrders = (int)(assetCount * 0.8 * months / 3); // 80% of assets have quarterly PM

            // Corrective work orders: Historical average + prediction adjustment
            var failurePredictions = PredictFailures(months * 30);
            var highRiskCount = failurePredictions.Count(p => p.FailureProbability > 0.5);
            forecast.PredictedCorrectiveWorkOrders = (int)(assetCount * 0.15 * months / 3) + highRiskCount;

            // Emergency work orders: Small percentage
            forecast.PredictedEmergencyWorkOrders = (int)(assetCount * 0.02 * months / 3);

            forecast.TotalPredictedWorkOrders =
                forecast.PredictedPreventiveWorkOrders +
                forecast.PredictedCorrectiveWorkOrders +
                forecast.PredictedEmergencyWorkOrders;

            // Labor hours by skill
            forecast.LaborHoursBySkill = new Dictionary<string, double>
            {
                ["HVAC Technician"] = forecast.TotalPredictedWorkOrders * 0.35 * 2.5,
                ["Electrical"] = forecast.TotalPredictedWorkOrders * 0.25 * 2.0,
                ["Plumbing"] = forecast.TotalPredictedWorkOrders * 0.15 * 1.5,
                ["General Maintenance"] = forecast.TotalPredictedWorkOrders * 0.25 * 1.5
            };

            forecast.TotalLaborHours = forecast.LaborHoursBySkill.Values.Sum();
            forecast.TechnicianDaysRequired = (int)Math.Ceiling(forecast.TotalLaborHours / 8);

            // Cost projections (UGX)
            forecast.ProjectedLaborCost = (decimal)forecast.TotalLaborHours * 25000; // UGX per hour
            forecast.ProjectedPartsCost = forecast.TotalPredictedWorkOrders * 150000m;
            forecast.ProjectedContractorCost = forecast.PredictedPreventiveWorkOrders * 50000m * 0.2m; // 20% outsourced
            forecast.TotalProjectedCost = forecast.ProjectedLaborCost + forecast.ProjectedPartsCost + forecast.ProjectedContractorCost;

            // Parts demand forecast
            forecast.PredictedPartsDemand = new List<PartsDemandForecast>
            {
                new PartsDemandForecast
                {
                    PartNumber = "FLT-001",
                    PartName = "Air Filter 20x20x2",
                    ProjectedQuantity = (int)(assetCount * 0.3 * months),
                    UnitCost = 25000,
                    CurrentInventory = 50,
                    LeadTimeDays = 7
                },
                new PartsDemandForecast
                {
                    PartNumber = "BLT-001",
                    PartName = "V-Belt A68",
                    ProjectedQuantity = (int)(assetCount * 0.05 * months),
                    UnitCost = 45000,
                    CurrentInventory = 10,
                    LeadTimeDays = 14
                }
            };

            foreach (var part in forecast.PredictedPartsDemand)
            {
                part.TotalCost = part.ProjectedQuantity * part.UnitCost;
                part.RequiresReorder = part.ProjectedQuantity > part.CurrentInventory * 0.8;
                if (part.RequiresReorder)
                    part.ReorderQuantity = part.ProjectedQuantity - part.CurrentInventory + 10; // Safety stock
            }

            // Contractor demand
            forecast.PredictedContractorDemand = new List<ContractorDemandForecast>
            {
                new ContractorDemandForecast
                {
                    ContractorType = "Elevator Specialist",
                    ProjectedVisits = months,
                    ProjectedHours = months * 4,
                    ProjectedCost = months * 2000000,
                    PlannedActivities = new() { "Monthly service", "Safety inspection" }
                },
                new ContractorDemandForecast
                {
                    ContractorType = "Fire System Specialist",
                    ProjectedVisits = months / 3 + 1,
                    ProjectedHours = (months / 3 + 1) * 8,
                    ProjectedCost = (months / 3 + 1) * 1500000,
                    PlannedActivities = new() { "Quarterly inspection", "Pump test" }
                }
            };

            forecast.Confidence = PredictionConfidence.Medium;

            return forecast;
        }

        private int CountWorkingDays(DateTime start, DateTime end)
        {
            int count = 0;
            for (var date = start; date < end; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    count++;
            }
            return count;
        }

        #endregion

        #region Budget Projection

        /// <summary>
        /// Project FM budget for a year
        /// </summary>
        public FMBudgetProjection ProjectBudget(int year, string scenario = "Baseline")
        {
            var projection = new FMBudgetProjection
            {
                Year = year,
                Scenario = scenario
            };

            var scenarioMultiplier = scenario switch
            {
                "Optimistic" => 0.9,
                "Pessimistic" => 1.15,
                _ => 1.0
            };

            // Generate monthly projections
            var months = new[] { "January", "February", "March", "April", "May", "June",
                                "July", "August", "September", "October", "November", "December" };

            decimal totalLaborCost = 0, totalPartsCost = 0, totalContractorCost = 0, totalUtilitiesCost = 0;

            for (int m = 1; m <= 12; m++)
            {
                // Seasonal adjustment (higher in hot months for HVAC)
                var seasonalFactor = m >= 3 && m <= 9 ? 1.2 : 0.9; // Mar-Sep higher in East Africa

                var monthly = new MonthlyBudgetProjection
                {
                    Month = m,
                    MonthName = months[m - 1],
                    LaborCost = (decimal)(15000000 * scenarioMultiplier * seasonalFactor), // Base UGX 15M
                    PartsCost = (decimal)(5000000 * scenarioMultiplier * seasonalFactor),
                    ContractorCost = (decimal)(3000000 * scenarioMultiplier),
                    UtilitiesCost = (decimal)(25000000 * seasonalFactor) // Electricity primarily
                };

                monthly.TotalCost = monthly.LaborCost + monthly.PartsCost +
                                   monthly.ContractorCost + monthly.UtilitiesCost;

                // Add notes for significant months
                if (m == 1) monthly.Notes = "Annual contracts renewal";
                if (m == 6) monthly.Notes = "Mid-year PM intensive";
                if (m == 12) monthly.Notes = "Year-end compliance inspections";

                projection.MonthlyProjections.Add(monthly);

                totalLaborCost += monthly.LaborCost;
                totalPartsCost += monthly.PartsCost;
                totalContractorCost += monthly.ContractorCost;
                totalUtilitiesCost += monthly.UtilitiesCost;
            }

            projection.TotalLaborBudget = totalLaborCost;
            projection.TotalPartsBudget = totalPartsCost;
            projection.TotalContractorBudget = totalContractorCost;
            projection.TotalUtilitiesBudget = totalUtilitiesCost;
            projection.TotalBudget = totalLaborCost + totalPartsCost + totalContractorCost + totalUtilitiesCost;

            // Budget by category
            projection.BudgetByCategory = new Dictionary<string, decimal>
            {
                ["Labor"] = totalLaborCost,
                ["Parts & Materials"] = totalPartsCost,
                ["Contracted Services"] = totalContractorCost,
                ["Utilities"] = totalUtilitiesCost
            };

            // Budget by system
            projection.BudgetBySystem = new Dictionary<string, decimal>
            {
                ["HVAC"] = projection.TotalBudget * 0.40m,
                ["Electrical"] = projection.TotalBudget * 0.20m,
                ["Plumbing"] = projection.TotalBudget * 0.10m,
                ["Fire Protection"] = projection.TotalBudget * 0.08m,
                ["Vertical Transport"] = projection.TotalBudget * 0.12m,
                ["Building Fabric"] = projection.TotalBudget * 0.10m
            };

            // Capital expenses forecast
            var failures = PredictFailures(365);
            projection.CapitalExpenses = failures
                .Where(f => f.FailureProbability > 0.7)
                .Select(f => new CapitalExpenseForecast
                {
                    AssetId = f.AssetId,
                    AssetName = f.AssetName,
                    ExpenseType = "Replacement",
                    ProjectedDate = DateTime.UtcNow.AddMonths(6),
                    EstimatedCost = f.EstimatedFailureCost * 5, // Replacement >> repair
                    Justification = $"High failure probability ({f.FailureProbability:P0})",
                    Confidence = f.Confidence
                })
                .ToList();

            projection.TotalCapitalBudget = projection.CapitalExpenses.Sum(ce => ce.EstimatedCost);

            // Comparison with previous year (simulated)
            projection.PreviousYearActual = projection.TotalBudget * 0.95m;
            projection.YearOverYearChange = projection.TotalBudget - projection.PreviousYearActual;
            projection.YearOverYearChangePercent = (double)(projection.YearOverYearChange / projection.PreviousYearActual) * 100;

            // Risks
            projection.IdentifiedRisks = new List<BudgetRisk>
            {
                new BudgetRisk
                {
                    RiskDescription = "Major equipment failure requiring emergency replacement",
                    RiskCategory = "Equipment",
                    Probability = 0.15,
                    PotentialImpact = projection.TotalBudget * 0.2m,
                    Mitigation = "Maintain contingency fund, implement predictive maintenance"
                },
                new BudgetRisk
                {
                    RiskDescription = "Utility rate increase",
                    RiskCategory = "External",
                    Probability = 0.30,
                    PotentialImpact = totalUtilitiesCost * 0.15m,
                    Mitigation = "Energy efficiency improvements, demand management"
                },
                new BudgetRisk
                {
                    RiskDescription = "Parts supply chain disruption",
                    RiskCategory = "Supply Chain",
                    Probability = 0.10,
                    PotentialImpact = totalPartsCost * 0.25m,
                    Mitigation = "Maintain critical spare parts inventory, identify alternative suppliers"
                }
            };

            projection.Confidence = PredictionConfidence.Medium;
            projection.BudgetVarianceRange = projection.TotalBudget * 0.10m; // +/- 10%

            return projection;
        }

        #endregion

        #region Equipment Health Scoring

        /// <summary>
        /// Calculate health score for asset
        /// </summary>
        public EquipmentHealthScore CalculateHealthScore(Asset asset)
        {
            var knowledge = _knowledgeBase.GetEquipmentKnowledge(asset.AssetType);
            var score = new EquipmentHealthScore
            {
                AssetId = asset.AssetId,
                AssetName = asset.AssetName,
                EquipmentType = asset.AssetType
            };

            // Age score (0-100, 100 = brand new)
            if (asset.InstallationDate != default && knowledge != null)
            {
                var ageYears = (DateTime.UtcNow - asset.InstallationDate).TotalDays / 365.25;
                var lifeRatio = ageYears / knowledge.TypicalLifespanYears;
                score.AgeScore = Math.Max(0, 100 * (1 - lifeRatio));
                score.EstimatedRemainingLifeMonths = Math.Max(0,
                    (int)((knowledge.TypicalLifespanYears - ageYears) * 12));
            }
            else
            {
                score.AgeScore = 70; // Default if unknown
                score.EstimatedRemainingLifeMonths = 60;
            }

            // Maintenance score (0-100, 100 = perfectly maintained)
            if (asset.LastMaintenanceDate.HasValue)
            {
                var daysSinceMaintenance = (DateTime.UtcNow - asset.LastMaintenanceDate.Value).TotalDays;
                score.MaintenanceScore = daysSinceMaintenance < 30 ? 100 :
                                        daysSinceMaintenance < 60 ? 80 :
                                        daysSinceMaintenance < 90 ? 60 :
                                        daysSinceMaintenance < 180 ? 40 : 20;
            }
            else
            {
                score.MaintenanceScore = 50;
            }

            // Performance score (would come from sensors/BMS)
            score.PerformanceScore = 85; // Default good performance

            // Reliability score (based on failure history)
            score.ReliabilityScore = 80; // Default

            // Calculate overall health score
            score.OverallHealthScore =
                score.AgeScore * 0.25 +
                score.MaintenanceScore * 0.30 +
                score.PerformanceScore * 0.25 +
                score.ReliabilityScore * 0.20;

            // Determine status
            score.HealthStatus = score.OverallHealthScore >= 90 ? "Excellent" :
                                score.OverallHealthScore >= 75 ? "Good" :
                                score.OverallHealthScore >= 60 ? "Fair" :
                                score.OverallHealthScore >= 40 ? "Poor" : "Critical";

            // Trend (would be calculated from historical scores)
            score.HealthTrend = "Stable";
            score.TrendRate = -0.5; // Slight decline typical for aging equipment

            score.RemainingLifeConfidence = 0.7;

            return score;
        }

        /// <summary>
        /// Calculate health scores for all assets
        /// </summary>
        public List<EquipmentHealthScore> CalculateAllHealthScores()
        {
            if (_assetRegistry == null)
                return new List<EquipmentHealthScore>();

            return _assetRegistry.GetAllAssets()
                .Select(CalculateHealthScore)
                .OrderBy(s => s.OverallHealthScore)
                .ToList();
        }

        #endregion

        #endregion // FM Predictive Analytics Engine
    }

    #region Supporting Classes

    /// <summary>
    /// Historical maintenance event for analytics
    /// </summary>
    public class MaintenanceEvent
    {
        public string AssetId { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string EventType { get; set; } = string.Empty; // PM, CM, Emergency
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public double LaborHours { get; set; }
        public bool WasFailure { get; set; }
        public string FailureMode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sensor reading for condition monitoring
    /// </summary>
    public class SensorReading
    {
        public string AssetId { get; set; } = string.Empty;
        public string SensorId { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    #endregion
}
