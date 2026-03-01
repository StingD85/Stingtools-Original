// ===================================================================
// StingBIM.AI.Collaboration - Predictive Analytics Intelligence Layer
// Provides forecasting for schedules, costs, risks, and resource needs
// Uses time-series analysis, regression, and ML-based predictions
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Prediction Models

    /// <summary>
    /// Time series data point
    /// </summary>
    public class TimeSeriesPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public Dictionary<string, double> Features { get; set; } = new();
    }

    /// <summary>
    /// Prediction result
    /// </summary>
    public class PredictionResult
    {
        public string MetricName { get; set; } = string.Empty;
        public double PredictedValue { get; set; }
        public double Confidence { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
        public DateTime PredictedFor { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string Model { get; set; } = string.Empty;
        public Dictionary<string, double> FeatureImportance { get; set; } = new();
        public List<string> Factors { get; set; } = new();
    }

    /// <summary>
    /// Schedule prediction
    /// </summary>
    public class SchedulePrediction
    {
        public string ActivityId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime PredictedStart { get; set; }
        public DateTime PredictedEnd { get; set; }
        public int PredictedDelayDays { get; set; }
        public double DelayProbability { get; set; }
        public List<string> DelayRiskFactors { get; set; } = new();
        public List<ScheduleMitigation> Mitigations { get; set; } = new();
    }

    /// <summary>
    /// Schedule mitigation suggestion
    /// </summary>
    public class ScheduleMitigation
    {
        public string Action { get; set; } = string.Empty;
        public int RecoveryDays { get; set; }
        public decimal AdditionalCost { get; set; }
        public double SuccessProbability { get; set; }
    }

    /// <summary>
    /// Cost prediction
    /// </summary>
    public class CostPrediction
    {
        public string Category { get; set; } = string.Empty;
        public decimal BudgetedCost { get; set; }
        public decimal CurrentSpent { get; set; }
        public decimal PredictedFinal { get; set; }
        public decimal PredictedVariance { get; set; }
        public double VarianceProbability { get; set; }
        public double Confidence { get; set; }
        public List<CostDriver> CostDrivers { get; set; } = new();
        public List<CostSavingOpportunity> SavingOpportunities { get; set; } = new();
    }

    /// <summary>
    /// Cost driver analysis
    /// </summary>
    public class CostDriver
    {
        public string Name { get; set; } = string.Empty;
        public decimal Impact { get; set; }
        public double Probability { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsControllable { get; set; }
    }

    /// <summary>
    /// Cost saving opportunity
    /// </summary>
    public class CostSavingOpportunity
    {
        public string Description { get; set; } = string.Empty;
        public decimal PotentialSaving { get; set; }
        public double Feasibility { get; set; }
        public string Implementation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risk prediction
    /// </summary>
    public class RiskPrediction
    {
        public string RiskId { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Probability { get; set; }
        public double Impact { get; set; }
        public double RiskScore => Probability * Impact;
        public DateTime PredictedOccurrence { get; set; }
        public List<string> EarlyWarningSignals { get; set; } = new();
        public List<RiskMitigation> Mitigations { get; set; } = new();
        public List<string> RelatedRisks { get; set; } = new();
    }

    /// <summary>
    /// Risk mitigation option
    /// </summary>
    public class RiskMitigation
    {
        public string Action { get; set; } = string.Empty;
        public double Effectiveness { get; set; }
        public decimal Cost { get; set; }
        public int ImplementationDays { get; set; }
    }

    /// <summary>
    /// Resource demand prediction
    /// </summary>
    public class ResourcePrediction
    {
        public string ResourceType { get; set; } = string.Empty;
        public DateTime Period { get; set; }
        public int CurrentCapacity { get; set; }
        public int PredictedDemand { get; set; }
        public int Gap => PredictedDemand - CurrentCapacity;
        public double Confidence { get; set; }
        public List<ResourceRecommendation> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Resource recommendation
    /// </summary>
    public class ResourceRecommendation
    {
        public string Action { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime NeededBy { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    /// <summary>
    /// Issue resolution prediction
    /// </summary>
    public class IssueResolutionPrediction
    {
        public string IssueId { get; set; } = string.Empty;
        public int PredictedResolutionDays { get; set; }
        public DateTime PredictedResolutionDate { get; set; }
        public double Confidence { get; set; }
        public List<ResolutionFactor> Factors { get; set; } = new();
        public string? RecommendedAssignee { get; set; }
        public string? SimilarResolvedIssue { get; set; }
    }

    /// <summary>
    /// Resolution factor
    /// </summary>
    public class ResolutionFactor
    {
        public string Factor { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string Impact { get; set; } = string.Empty;
    }

    /// <summary>
    /// Trend analysis result
    /// </summary>
    public class TrendAnalysis
    {
        public string Metric { get; set; } = string.Empty;
        public TrendDirection Direction { get; set; }
        public double Slope { get; set; }
        public double Acceleration { get; set; }
        public double RSquared { get; set; }
        public List<TrendChangePoint> ChangePoints { get; set; } = new();
        public SeasonalPattern? Seasonality { get; set; }
    }

    /// <summary>
    /// Trend direction
    /// </summary>
    public enum TrendDirection
    {
        StrongUp,
        Up,
        Stable,
        Down,
        StrongDown
    }

    /// <summary>
    /// Trend change point
    /// </summary>
    public class TrendChangePoint
    {
        public DateTime Date { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public double Magnitude { get; set; }
        public string? PossibleCause { get; set; }
    }

    /// <summary>
    /// Seasonal pattern
    /// </summary>
    public class SeasonalPattern
    {
        public string Period { get; set; } = string.Empty; // daily, weekly, monthly
        public double Strength { get; set; }
        public Dictionary<string, double> SeasonalFactors { get; set; } = new();
    }

    /// <summary>
    /// What-if scenario
    /// </summary>
    public class WhatIfScenario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Assumptions { get; set; } = new();
        public Dictionary<string, double> Outcomes { get; set; } = new();
        public double Probability { get; set; }
        public decimal CostImpact { get; set; }
        public int ScheduleImpactDays { get; set; }
    }

    #endregion

    #region Predictive Analytics Layer

    /// <summary>
    /// Predictive analytics intelligence layer
    /// </summary>
    public class PredictiveAnalyticsLayer : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, List<TimeSeriesPoint>> _historicalData = new();
        private readonly ConcurrentDictionary<string, PredictionResult> _predictionCache = new();
        private readonly ConcurrentDictionary<string, TrendAnalysis> _trendCache = new();

        public PredictiveAnalyticsLayer(ILogger? logger = null)
        {
            _logger = logger;
            _logger?.LogInformation("PredictiveAnalyticsLayer initialized");
        }

        #region Schedule Predictions

        /// <summary>
        /// Predict schedule outcomes
        /// </summary>
        public async Task<List<SchedulePrediction>> PredictScheduleAsync(
            string projectId,
            List<ScheduleActivity> activities,
            CancellationToken ct = default)
        {
            var predictions = new List<SchedulePrediction>();

            foreach (var activity in activities)
            {
                var prediction = await PredictActivityCompletionAsync(projectId, activity, ct);
                predictions.Add(prediction);
            }

            // Identify critical path impacts
            var criticalDelays = predictions.Where(p => p.DelayProbability > 0.7).ToList();
            if (criticalDelays.Any())
            {
                _logger?.LogWarning("Project {ProjectId} has {Count} activities with high delay risk",
                    projectId, criticalDelays.Count);
            }

            return predictions.OrderByDescending(p => p.DelayProbability).ToList();
        }

        private async Task<SchedulePrediction> PredictActivityCompletionAsync(
            string projectId,
            ScheduleActivity activity,
            CancellationToken ct)
        {
            var prediction = new SchedulePrediction
            {
                ActivityId = activity.Id,
                ActivityName = activity.Name,
                PlannedStart = activity.PlannedStart,
                PlannedEnd = activity.PlannedEnd
            };

            // Analyze historical completion data for similar activities
            var historicalFactor = await GetHistoricalCompletionFactorAsync(activity.Type, ct);

            // Calculate delay probability based on factors
            var delayFactors = new Dictionary<string, double>();

            // Resource availability
            if (activity.ResourceUtilization > 0.9)
            {
                delayFactors["high_resource_utilization"] = 0.3;
                prediction.DelayRiskFactors.Add("High resource utilization (>90%)");
            }

            // Predecessor delays
            if (activity.HasDelayedPredecessors)
            {
                delayFactors["predecessor_delay"] = 0.4;
                prediction.DelayRiskFactors.Add("Predecessor activities delayed");
            }

            // Weather sensitivity
            if (activity.IsWeatherSensitive)
            {
                delayFactors["weather_risk"] = 0.15;
                prediction.DelayRiskFactors.Add("Weather-sensitive activity");
            }

            // Complexity
            if (activity.Complexity > 0.7)
            {
                delayFactors["high_complexity"] = 0.2;
                prediction.DelayRiskFactors.Add("High complexity activity");
            }

            // Calculate overall delay probability
            prediction.DelayProbability = Math.Min(1.0,
                delayFactors.Values.Sum() * historicalFactor);

            // Predict dates
            var plannedDuration = (activity.PlannedEnd - activity.PlannedStart).TotalDays;
            var predictedDuration = plannedDuration * (1 + prediction.DelayProbability * 0.3);
            var delayDays = (int)(predictedDuration - plannedDuration);

            prediction.PredictedStart = activity.PlannedStart.AddDays(
                activity.HasDelayedPredecessors ? 3 : 0);
            prediction.PredictedEnd = prediction.PredictedStart.AddDays(predictedDuration);
            prediction.PredictedDelayDays = delayDays;

            // Generate mitigations if delay predicted
            if (prediction.DelayProbability > 0.5)
            {
                prediction.Mitigations = GenerateScheduleMitigations(prediction, activity);
            }

            return prediction;
        }

        private async Task<double> GetHistoricalCompletionFactorAsync(string activityType, CancellationToken ct)
        {
            // Would analyze historical data
            await Task.Delay(1, ct);
            return activityType switch
            {
                "excavation" => 1.15, // Historically 15% longer
                "concrete" => 1.1,
                "steel" => 1.05,
                "mep" => 1.2,
                "finishes" => 1.1,
                _ => 1.0
            };
        }

        private List<ScheduleMitigation> GenerateScheduleMitigations(
            SchedulePrediction prediction,
            ScheduleActivity activity)
        {
            var mitigations = new List<ScheduleMitigation>();

            if (prediction.DelayRiskFactors.Contains("High resource utilization (>90%)"))
            {
                mitigations.Add(new ScheduleMitigation
                {
                    Action = "Add additional crew",
                    RecoveryDays = Math.Min(prediction.PredictedDelayDays, 5),
                    AdditionalCost = 15000m,
                    SuccessProbability = 0.75
                });
            }

            if (prediction.PredictedDelayDays > 3)
            {
                mitigations.Add(new ScheduleMitigation
                {
                    Action = "Implement overtime schedule",
                    RecoveryDays = Math.Min(prediction.PredictedDelayDays / 2, 7),
                    AdditionalCost = 8000m,
                    SuccessProbability = 0.8
                });

                mitigations.Add(new ScheduleMitigation
                {
                    Action = "Parallel sequencing of follow-on activities",
                    RecoveryDays = Math.Min(prediction.PredictedDelayDays, 4),
                    AdditionalCost = 5000m,
                    SuccessProbability = 0.65
                });
            }

            return mitigations;
        }

        #endregion

        #region Cost Predictions

        /// <summary>
        /// Predict cost outcomes
        /// </summary>
        public async Task<CostPrediction> PredictCostAsync(
            string projectId,
            string category,
            decimal budget,
            decimal spentToDate,
            double percentComplete,
            CancellationToken ct = default)
        {
            var prediction = new CostPrediction
            {
                Category = category,
                BudgetedCost = budget,
                CurrentSpent = spentToDate
            };

            // Calculate earned value metrics
            var plannedValue = budget * (decimal)percentComplete;
            var costPerformanceIndex = spentToDate > 0
                ? plannedValue / spentToDate
                : 1.0m;

            // Predict final cost using EAC formulas
            decimal predictedFinal;

            if (costPerformanceIndex < 0.9m) // Poor performance
            {
                // EAC = AC + (BAC - EV) / CPI
                predictedFinal = spentToDate + (budget - plannedValue) / costPerformanceIndex;
                prediction.Confidence = 0.7;
            }
            else if (costPerformanceIndex > 1.1m) // Very good performance
            {
                predictedFinal = spentToDate + (budget - plannedValue);
                prediction.Confidence = 0.85;
            }
            else // Normal performance
            {
                predictedFinal = budget / costPerformanceIndex;
                prediction.Confidence = 0.8;
            }

            prediction.PredictedFinal = predictedFinal;
            prediction.PredictedVariance = predictedFinal - budget;
            prediction.VarianceProbability = Math.Abs((double)prediction.PredictedVariance / (double)budget);

            // Identify cost drivers
            prediction.CostDrivers = await IdentifyCostDriversAsync(projectId, category, ct);

            // Find saving opportunities
            if (prediction.PredictedVariance > 0)
            {
                prediction.SavingOpportunities = await FindSavingOpportunitiesAsync(
                    projectId, category, prediction.PredictedVariance, ct);
            }

            _logger?.LogDebug("Cost prediction for {Category}: Budget={Budget:C}, Predicted={Predicted:C}, Variance={Variance:P}",
                category, budget, predictedFinal, prediction.VarianceProbability);

            return prediction;
        }

        private async Task<List<CostDriver>> IdentifyCostDriversAsync(
            string projectId,
            string category,
            CancellationToken ct)
        {
            // Would analyze actual cost data
            return new List<CostDriver>
            {
                new()
                {
                    Name = "Material price increases",
                    Impact = 25000m,
                    Probability = 0.8,
                    Category = "External",
                    IsControllable = false
                },
                new()
                {
                    Name = "Overtime labor",
                    Impact = 15000m,
                    Probability = 0.6,
                    Category = "Labor",
                    IsControllable = true
                },
                new()
                {
                    Name = "Design changes",
                    Impact = 35000m,
                    Probability = 0.5,
                    Category = "Scope",
                    IsControllable = true
                }
            };
        }

        private async Task<List<CostSavingOpportunity>> FindSavingOpportunitiesAsync(
            string projectId,
            string category,
            decimal variance,
            CancellationToken ct)
        {
            return new List<CostSavingOpportunity>
            {
                new()
                {
                    Description = "Value engineering on finishes",
                    PotentialSaving = variance * 0.2m,
                    Feasibility = 0.75,
                    Implementation = "Review specification alternatives"
                },
                new()
                {
                    Description = "Bulk purchasing for remaining materials",
                    PotentialSaving = variance * 0.1m,
                    Feasibility = 0.85,
                    Implementation = "Consolidate material orders"
                },
                new()
                {
                    Description = "Optimize crew scheduling",
                    PotentialSaving = variance * 0.15m,
                    Feasibility = 0.7,
                    Implementation = "Implement lean construction practices"
                }
            };
        }

        #endregion

        #region Risk Predictions

        /// <summary>
        /// Predict project risks
        /// </summary>
        public async Task<List<RiskPrediction>> PredictRisksAsync(
            string projectId,
            CancellationToken ct = default)
        {
            var risks = new List<RiskPrediction>();

            // Analyze various risk categories
            risks.AddRange(await PredictScheduleRisksAsync(projectId, ct));
            risks.AddRange(await PredictCostRisksAsync(projectId, ct));
            risks.AddRange(await PredictQualityRisksAsync(projectId, ct));
            risks.AddRange(await PredictSafetyRisksAsync(projectId, ct));
            risks.AddRange(await PredictCoordinationRisksAsync(projectId, ct));

            // Sort by risk score
            return risks.OrderByDescending(r => r.RiskScore).ToList();
        }

        private async Task<List<RiskPrediction>> PredictScheduleRisksAsync(
            string projectId,
            CancellationToken ct)
        {
            return new List<RiskPrediction>
            {
                new()
                {
                    Category = "Schedule",
                    Description = "MEP coordination delays critical path",
                    Probability = 0.6,
                    Impact = 0.8,
                    PredictedOccurrence = DateTime.UtcNow.AddDays(21),
                    EarlyWarningSignals = new List<string>
                    {
                        "RFI response time increasing",
                        "Clash count not decreasing",
                        "Coordination meetings overrunning"
                    },
                    Mitigations = new List<RiskMitigation>
                    {
                        new() { Action = "Add BIM coordinator", Effectiveness = 0.7, Cost = 15000m, ImplementationDays = 5 },
                        new() { Action = "Increase coordination meeting frequency", Effectiveness = 0.5, Cost = 2000m, ImplementationDays = 1 }
                    }
                }
            };
        }

        private async Task<List<RiskPrediction>> PredictCostRisksAsync(
            string projectId,
            CancellationToken ct)
        {
            return new List<RiskPrediction>
            {
                new()
                {
                    Category = "Cost",
                    Description = "Steel price volatility may exceed contingency",
                    Probability = 0.4,
                    Impact = 0.7,
                    PredictedOccurrence = DateTime.UtcNow.AddDays(45),
                    EarlyWarningSignals = new List<string>
                    {
                        "Commodity index trending up",
                        "Supplier quotes expiring soon"
                    },
                    Mitigations = new List<RiskMitigation>
                    {
                        new() { Action = "Lock in steel prices now", Effectiveness = 0.9, Cost = 5000m, ImplementationDays = 3 }
                    }
                }
            };
        }

        private async Task<List<RiskPrediction>> PredictQualityRisksAsync(
            string projectId,
            CancellationToken ct)
        {
            return new List<RiskPrediction>
            {
                new()
                {
                    Category = "Quality",
                    Description = "Concrete placement in cold weather",
                    Probability = 0.5,
                    Impact = 0.6,
                    PredictedOccurrence = DateTime.UtcNow.AddDays(14),
                    EarlyWarningSignals = new List<string>
                    {
                        "Weather forecast showing cold snap",
                        "Concrete pours scheduled for affected period"
                    }
                }
            };
        }

        private async Task<List<RiskPrediction>> PredictSafetyRisksAsync(
            string projectId,
            CancellationToken ct)
        {
            return new List<RiskPrediction>
            {
                new()
                {
                    Category = "Safety",
                    Description = "Elevated work increasing as building rises",
                    Probability = 0.3,
                    Impact = 0.9,
                    PredictedOccurrence = DateTime.UtcNow.AddDays(7),
                    EarlyWarningSignals = new List<string>
                    {
                        "Fall protection inspections pending",
                        "New workers onboarding"
                    }
                }
            };
        }

        private async Task<List<RiskPrediction>> PredictCoordinationRisksAsync(
            string projectId,
            CancellationToken ct)
        {
            return new List<RiskPrediction>
            {
                new()
                {
                    Category = "Coordination",
                    Description = "Multiple trades working in same area",
                    Probability = 0.7,
                    Impact = 0.5,
                    PredictedOccurrence = DateTime.UtcNow.AddDays(10),
                    EarlyWarningSignals = new List<string>
                    {
                        "Schedule showing overlapping activities",
                        "Space constraints identified"
                    }
                }
            };
        }

        #endregion

        #region Resource Predictions

        /// <summary>
        /// Predict resource demands
        /// </summary>
        public async Task<List<ResourcePrediction>> PredictResourceDemandsAsync(
            string projectId,
            DateTime forecastStart,
            DateTime forecastEnd,
            CancellationToken ct = default)
        {
            var predictions = new List<ResourcePrediction>();
            var resourceTypes = new[] { "Electricians", "Plumbers", "Carpenters", "Laborers", "Equipment Operators" };

            foreach (var resourceType in resourceTypes)
            {
                var weekly = await PredictResourceDemandAsync(projectId, resourceType, forecastStart, forecastEnd, ct);
                predictions.AddRange(weekly);
            }

            return predictions.OrderBy(p => p.Period).ThenBy(p => p.ResourceType).ToList();
        }

        private async Task<List<ResourcePrediction>> PredictResourceDemandAsync(
            string projectId,
            string resourceType,
            DateTime start,
            DateTime end,
            CancellationToken ct)
        {
            var predictions = new List<ResourcePrediction>();
            var currentDate = start;

            while (currentDate < end)
            {
                var weekEnd = currentDate.AddDays(7);

                // Would analyze schedule activities and resource requirements
                var baseDemand = resourceType switch
                {
                    "Electricians" => 8,
                    "Plumbers" => 6,
                    "Carpenters" => 12,
                    "Laborers" => 20,
                    _ => 5
                };

                // Apply phase factor
                var phaseFactor = 1.0 + (currentDate - start).TotalDays / 100 * 0.5;
                var predictedDemand = (int)(baseDemand * phaseFactor);

                var currentCapacity = (int)(baseDemand * 0.9); // 90% of base

                var prediction = new ResourcePrediction
                {
                    ResourceType = resourceType,
                    Period = currentDate,
                    CurrentCapacity = currentCapacity,
                    PredictedDemand = predictedDemand,
                    Confidence = 0.8
                };

                if (prediction.Gap > 0)
                {
                    prediction.Recommendations.Add(new ResourceRecommendation
                    {
                        Action = $"Hire additional {resourceType}",
                        Quantity = prediction.Gap,
                        NeededBy = currentDate.AddDays(-7),
                        EstimatedCost = prediction.Gap * 500m
                    });
                }

                predictions.Add(prediction);
                currentDate = weekEnd;
            }

            return predictions;
        }

        #endregion

        #region Issue Resolution Predictions

        /// <summary>
        /// Predict issue resolution time
        /// </summary>
        public async Task<IssueResolutionPrediction> PredictIssueResolutionAsync(
            string issueId,
            string issueType,
            string priority,
            string? assignedTo,
            int commentCount,
            int attachmentCount,
            CancellationToken ct = default)
        {
            var prediction = new IssueResolutionPrediction
            {
                IssueId = issueId
            };

            // Base resolution time by type
            var baseDays = issueType.ToLower() switch
            {
                "clash" => 3,
                "rfi" => 5,
                "design" => 7,
                "construction" => 4,
                _ => 5
            };

            // Priority factor
            var priorityFactor = priority.ToLower() switch
            {
                "critical" => 0.5,
                "high" => 0.75,
                "normal" => 1.0,
                "low" => 1.5,
                _ => 1.0
            };

            // Engagement factor (more activity = faster resolution)
            var engagementFactor = 1.0 - Math.Min(0.3, (commentCount + attachmentCount) * 0.02);

            // Assignment factor
            var assignmentFactor = string.IsNullOrEmpty(assignedTo) ? 1.3 : 1.0;

            prediction.PredictedResolutionDays = (int)(baseDays * priorityFactor * engagementFactor * assignmentFactor);
            prediction.PredictedResolutionDate = DateTime.UtcNow.AddDays(prediction.PredictedResolutionDays);
            prediction.Confidence = 0.75;

            // Add factors
            prediction.Factors = new List<ResolutionFactor>
            {
                new() { Factor = "Issue Type", Weight = 0.3, Impact = $"Base: {baseDays} days" },
                new() { Factor = "Priority", Weight = 0.25, Impact = $"Factor: {priorityFactor:F2}" },
                new() { Factor = "Engagement", Weight = 0.2, Impact = $"Factor: {engagementFactor:F2}" }
            };

            if (string.IsNullOrEmpty(assignedTo))
            {
                prediction.Factors.Add(new ResolutionFactor
                {
                    Factor = "Assignment",
                    Weight = 0.15,
                    Impact = "Unassigned - adds delay"
                });

                prediction.RecommendedAssignee = await SuggestAssigneeAsync(issueType, ct);
            }

            return prediction;
        }

        private async Task<string?> SuggestAssigneeAsync(string issueType, CancellationToken ct)
        {
            // Would analyze team workload and expertise
            return issueType.ToLower() switch
            {
                "clash" => "bim_coordinator",
                "rfi" => "project_manager",
                "design" => "lead_designer",
                _ => null
            };
        }

        #endregion

        #region Trend Analysis

        /// <summary>
        /// Analyze trend for a metric
        /// </summary>
        public async Task<TrendAnalysis> AnalyzeTrendAsync(
            string metricName,
            List<TimeSeriesPoint> data,
            CancellationToken ct = default)
        {
            if (data.Count < 3)
            {
                return new TrendAnalysis { Metric = metricName, Direction = TrendDirection.Stable };
            }

            var analysis = new TrendAnalysis { Metric = metricName };

            // Sort by time
            var sorted = data.OrderBy(d => d.Timestamp).ToList();

            // Calculate linear regression
            var n = sorted.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;
            var sumY2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += sorted[i].Value;
                sumXY += i * sorted[i].Value;
                sumX2 += i * i;
                sumY2 += sorted[i].Value * sorted[i].Value;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            // Calculate R-squared
            var meanY = sumY / n;
            var ssTotal = 0.0;
            var ssResidual = 0.0;

            for (int i = 0; i < n; i++)
            {
                var predicted = slope * i + intercept;
                ssTotal += Math.Pow(sorted[i].Value - meanY, 2);
                ssResidual += Math.Pow(sorted[i].Value - predicted, 2);
            }

            analysis.Slope = slope;
            analysis.RSquared = ssTotal > 0 ? 1 - (ssResidual / ssTotal) : 0;

            // Determine direction
            var normalizedSlope = slope / meanY * 100; // Percentage change per period
            analysis.Direction = normalizedSlope switch
            {
                > 5 => TrendDirection.StrongUp,
                > 1 => TrendDirection.Up,
                < -5 => TrendDirection.StrongDown,
                < -1 => TrendDirection.Down,
                _ => TrendDirection.Stable
            };

            // Detect change points (simplified)
            analysis.ChangePoints = DetectChangePoints(sorted);

            // Cache result
            _trendCache[metricName] = analysis;

            _logger?.LogDebug("Trend analysis for {Metric}: {Direction}, slope={Slope:F3}, R²={R2:F3}",
                metricName, analysis.Direction, analysis.Slope, analysis.RSquared);

            return analysis;
        }

        private List<TrendChangePoint> DetectChangePoints(List<TimeSeriesPoint> data)
        {
            var changePoints = new List<TrendChangePoint>();

            if (data.Count < 5) return changePoints;

            // Simple change detection: compare moving averages
            var windowSize = Math.Max(2, data.Count / 5);

            for (int i = windowSize; i < data.Count - windowSize; i++)
            {
                var beforeAvg = data.Skip(i - windowSize).Take(windowSize).Average(d => d.Value);
                var afterAvg = data.Skip(i).Take(windowSize).Average(d => d.Value);

                var change = (afterAvg - beforeAvg) / beforeAvg;

                if (Math.Abs(change) > 0.2) // 20% change threshold
                {
                    changePoints.Add(new TrendChangePoint
                    {
                        Date = data[i].Timestamp,
                        ChangeType = change > 0 ? "Increase" : "Decrease",
                        Magnitude = Math.Abs(change)
                    });
                }
            }

            return changePoints;
        }

        #endregion

        #region What-If Scenarios

        /// <summary>
        /// Evaluate what-if scenario
        /// </summary>
        public async Task<WhatIfScenario> EvaluateScenarioAsync(
            string projectId,
            WhatIfScenario scenario,
            CancellationToken ct = default)
        {
            // Evaluate each assumption's impact
            foreach (var assumption in scenario.Assumptions)
            {
                var impact = await EvaluateAssumptionImpactAsync(projectId, assumption.Key, assumption.Value, ct);
                scenario.Outcomes[assumption.Key + "_impact"] = impact;
            }

            // Calculate aggregate impacts
            if (scenario.Assumptions.ContainsKey("schedule_delay_days"))
            {
                var days = Convert.ToInt32(scenario.Assumptions["schedule_delay_days"]);
                scenario.ScheduleImpactDays = days;
                scenario.CostImpact = days * 5000m; // $5k per day of delay
            }

            if (scenario.Assumptions.ContainsKey("resource_reduction_percent"))
            {
                var reduction = Convert.ToDouble(scenario.Assumptions["resource_reduction_percent"]);
                scenario.ScheduleImpactDays += (int)(reduction * 0.5);
                scenario.CostImpact -= (decimal)(reduction * 1000);
            }

            scenario.Probability = 0.5; // Would be calculated based on historical data

            return scenario;
        }

        private async Task<double> EvaluateAssumptionImpactAsync(
            string projectId,
            string assumptionType,
            object value,
            CancellationToken ct)
        {
            // Would run simulation based on assumption
            return assumptionType switch
            {
                "schedule_delay_days" => Convert.ToDouble(value) * 0.05,
                "budget_change_percent" => Convert.ToDouble(value) * 0.8,
                "resource_reduction_percent" => Convert.ToDouble(value) * 0.6,
                _ => 0
            };
        }

        #endregion

        #region Data Management

        /// <summary>
        /// Record historical data point
        /// </summary>
        public void RecordDataPoint(string metricName, double value, Dictionary<string, double>? features = null)
        {
            var data = _historicalData.GetOrAdd(metricName, _ => new List<TimeSeriesPoint>());

            lock (data)
            {
                data.Add(new TimeSeriesPoint
                {
                    Timestamp = DateTime.UtcNow,
                    Value = value,
                    Features = features ?? new Dictionary<string, double>()
                });

                // Keep last 1000 points
                while (data.Count > 1000)
                {
                    data.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Get historical data
        /// </summary>
        public List<TimeSeriesPoint> GetHistoricalData(string metricName, DateTime? since = null)
        {
            if (!_historicalData.TryGetValue(metricName, out var data))
                return new List<TimeSeriesPoint>();

            lock (data)
            {
                if (since.HasValue)
                    return data.Where(d => d.Timestamp >= since.Value).ToList();
                return data.ToList();
            }
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _logger?.LogInformation("PredictiveAnalyticsLayer disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Support Models

    /// <summary>
    /// Schedule activity for prediction
    /// </summary>
    public class ScheduleActivity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public double ResourceUtilization { get; set; }
        public bool HasDelayedPredecessors { get; set; }
        public bool IsWeatherSensitive { get; set; }
        public double Complexity { get; set; }
    }

    #endregion
}
