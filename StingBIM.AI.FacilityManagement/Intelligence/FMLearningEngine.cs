// ============================================================================
// StingBIM AI - Facility Management Learning Engine
// Adaptive learning system for continuous improvement of FM operations
// Learns from outcomes, feedback, and operational data
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Learning Models

    /// <summary>
    /// Learning observation - input for the learning engine
    /// </summary>
    public class LearningObservation
    {
        public string ObservationId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public ObservationType Type { get; set; }
        public DateTime ObservedAt { get; set; } = DateTime.UtcNow;

        // Context
        public string EntityId { get; set; } = string.Empty; // Asset, WorkOrder, etc.
        public string EntityType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();

        // Prediction vs Outcome
        public string PredictionId { get; set; } = string.Empty;
        public double? PredictedValue { get; set; }
        public double? ActualValue { get; set; }
        public bool PredictionCorrect { get; set; }

        // Feedback
        public FeedbackType? Feedback { get; set; }
        public string FeedbackNotes { get; set; } = string.Empty;
        public string FeedbackBy { get; set; } = string.Empty;

        // Impact
        public double? ConfidenceAdjustment { get; set; }
        public string LessonLearned { get; set; } = string.Empty;
    }

    public enum ObservationType
    {
        PredictionOutcome,      // Prediction was validated against actual
        UserFeedback,           // User provided explicit feedback
        RecommendationOutcome,  // Recommendation was accepted/rejected/implemented
        MaintenanceOutcome,     // Maintenance task completion result
        FailureEvent,           // Equipment failure occurred
        CostVariance,           // Actual vs estimated cost
        TimeVariance,           // Actual vs estimated time
        PerformanceChange,      // Equipment performance changed
        AnomalyResolution       // How anomaly was resolved
    }

    public enum FeedbackType
    {
        Positive,       // Prediction/recommendation was helpful
        Negative,       // Prediction/recommendation was wrong
        Neutral,        // No strong opinion
        FalsePositive,  // Alert was incorrect
        FalseNegative,  // Missed something important
        Adjusted        // User corrected the prediction
    }

    /// <summary>
    /// Learned parameter adjustment
    /// </summary>
    public class ParameterAdjustment
    {
        public string AdjustmentId { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty; // AssetType, System, Global
        public string ScopeValue { get; set; } = string.Empty;

        public double OriginalValue { get; set; }
        public double AdjustedValue { get; set; }
        public double AdjustmentFactor { get; set; }

        public int SupportingObservations { get; set; }
        public double Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model performance metrics
    /// </summary>
    public class ModelPerformanceMetrics
    {
        public string ModelName { get; set; } = string.Empty;
        public DateTime EvaluationDate { get; set; }
        public int TotalPredictions { get; set; }
        public int CorrectPredictions { get; set; }

        // Accuracy metrics
        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }

        // Error metrics
        public double MeanAbsoluteError { get; set; }
        public double MeanSquaredError { get; set; }
        public double RootMeanSquaredError { get; set; }

        // Calibration
        public double CalibrationError { get; set; }
        public Dictionary<string, double> ConfidenceBucketAccuracy { get; set; } = new();

        // Trend
        public double AccuracyTrend { get; set; } // Improving/Declining
        public string PerformanceStatus { get; set; } = string.Empty;
    }

    /// <summary>
    /// Learned knowledge entry
    /// </summary>
    public class LearnedKnowledge
    {
        public string KnowledgeId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public DateTime LearnedAt { get; set; }
        public int SupportingObservations { get; set; }
        public double Confidence { get; set; }

        public List<string> ApplicableTo { get; set; } = new(); // Asset types, systems
        public Dictionary<string, object> Data { get; set; } = new();

        public bool IsValidated { get; set; }
        public string ValidatedBy { get; set; } = string.Empty;
    }

    #endregion

    #region FM Learning Engine

    /// <summary>
    /// FM Learning Engine
    /// Learns from operational data to improve predictions and recommendations
    /// </summary>
    public class FMLearningEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Observation storage
        private readonly List<LearningObservation> _observations = new();
        private const int MaxObservations = 10000;

        // Learned adjustments
        private readonly Dictionary<string, ParameterAdjustment> _parameterAdjustments = new();
        private readonly List<LearnedKnowledge> _learnedKnowledge = new();

        // Model performance tracking
        private readonly Dictionary<string, List<ModelPerformanceMetrics>> _modelPerformance = new();

        // Learning parameters
        private double _learningRate = 0.1;
        private int _minObservationsForAdjustment = 5;
        private double _significanceThreshold = 0.05;

        public FMLearningEngine()
        {
            InitializeDefaultAdjustments();
            Logger.Info("FM Learning Engine initialized");
        }

        #region Initialization

        private void InitializeDefaultAdjustments()
        {
            // Default adjustments based on typical FM patterns
            AddParameterAdjustment(new ParameterAdjustment
            {
                ParameterName = "FailureRateMultiplier",
                Scope = "AssetType",
                ScopeValue = "AHU",
                OriginalValue = 1.0,
                AdjustedValue = 1.0,
                AdjustmentFactor = 1.0,
                Confidence = 0.5,
                Reason = "Default baseline"
            });

            AddParameterAdjustment(new ParameterAdjustment
            {
                ParameterName = "MaintenanceDurationMultiplier",
                Scope = "Global",
                ScopeValue = "*",
                OriginalValue = 1.0,
                AdjustedValue = 1.15, // Typically underestimated by 15%
                AdjustmentFactor = 1.15,
                SupportingObservations = 100,
                Confidence = 0.8,
                Reason = "Historical pattern: maintenance duration typically 15% longer than estimated"
            });

            AddParameterAdjustment(new ParameterAdjustment
            {
                ParameterName = "CostEstimateMultiplier",
                Scope = "Global",
                ScopeValue = "*",
                OriginalValue = 1.0,
                AdjustedValue = 1.20, // Typically underestimated by 20%
                AdjustmentFactor = 1.20,
                SupportingObservations = 100,
                Confidence = 0.85,
                Reason = "Historical pattern: costs typically 20% higher than initial estimates"
            });
        }

        private void AddParameterAdjustment(ParameterAdjustment adjustment)
        {
            var key = $"{adjustment.ParameterName}|{adjustment.Scope}|{adjustment.ScopeValue}";
            adjustment.AdjustmentId = key;
            adjustment.LastUpdated = DateTime.UtcNow;
            _parameterAdjustments[key] = adjustment;
        }

        #endregion

        #region Observation Recording

        /// <summary>
        /// Record a learning observation
        /// </summary>
        public void RecordObservation(LearningObservation observation)
        {
            _observations.Add(observation);

            // Maintain maximum size
            while (_observations.Count > MaxObservations)
                _observations.RemoveAt(0);

            // Process observation
            ProcessObservation(observation);

            Logger.Debug($"Recorded observation: {observation.Type} for {observation.EntityType} {observation.EntityId}");
        }

        /// <summary>
        /// Record prediction outcome
        /// </summary>
        public void RecordPredictionOutcome(
            string predictionId,
            string entityId,
            string entityType,
            double predictedValue,
            double actualValue,
            Dictionary<string, object> context = null)
        {
            var observation = new LearningObservation
            {
                Type = ObservationType.PredictionOutcome,
                EntityId = entityId,
                EntityType = entityType,
                PredictionId = predictionId,
                PredictedValue = predictedValue,
                ActualValue = actualValue,
                PredictionCorrect = Math.Abs(predictedValue - actualValue) < (actualValue * 0.2), // Within 20%
                Context = context ?? new Dictionary<string, object>()
            };

            RecordObservation(observation);
        }

        /// <summary>
        /// Record user feedback
        /// </summary>
        public void RecordFeedback(
            string entityId,
            string entityType,
            FeedbackType feedback,
            string notes = null,
            string feedbackBy = null)
        {
            var observation = new LearningObservation
            {
                Type = ObservationType.UserFeedback,
                EntityId = entityId,
                EntityType = entityType,
                Feedback = feedback,
                FeedbackNotes = notes ?? string.Empty,
                FeedbackBy = feedbackBy ?? "Unknown"
            };

            RecordObservation(observation);
        }

        /// <summary>
        /// Record maintenance outcome
        /// </summary>
        public void RecordMaintenanceOutcome(
            string workOrderId,
            string assetId,
            string assetType,
            double estimatedHours,
            double actualHours,
            decimal estimatedCost,
            decimal actualCost,
            bool successful)
        {
            var observation = new LearningObservation
            {
                Type = ObservationType.MaintenanceOutcome,
                EntityId = workOrderId,
                EntityType = "WorkOrder",
                Category = assetType,
                Context = new Dictionary<string, object>
                {
                    ["AssetId"] = assetId,
                    ["AssetType"] = assetType,
                    ["EstimatedHours"] = estimatedHours,
                    ["ActualHours"] = actualHours,
                    ["EstimatedCost"] = estimatedCost,
                    ["ActualCost"] = actualCost,
                    ["Successful"] = successful
                }
            };

            RecordObservation(observation);
        }

        /// <summary>
        /// Record failure event
        /// </summary>
        public void RecordFailureEvent(
            string assetId,
            string assetType,
            string failureMode,
            bool wasPredicted,
            double? predictedProbability = null)
        {
            var observation = new LearningObservation
            {
                Type = ObservationType.FailureEvent,
                EntityId = assetId,
                EntityType = assetType,
                Category = failureMode,
                PredictionCorrect = wasPredicted,
                PredictedValue = predictedProbability,
                Context = new Dictionary<string, object>
                {
                    ["FailureMode"] = failureMode,
                    ["WasPredicted"] = wasPredicted
                }
            };

            if (!wasPredicted)
            {
                observation.LessonLearned = $"Missed failure prediction for {assetType}: {failureMode}";
            }

            RecordObservation(observation);
        }

        #endregion

        #region Observation Processing

        private void ProcessObservation(LearningObservation observation)
        {
            switch (observation.Type)
            {
                case ObservationType.PredictionOutcome:
                    ProcessPredictionOutcome(observation);
                    break;
                case ObservationType.MaintenanceOutcome:
                    ProcessMaintenanceOutcome(observation);
                    break;
                case ObservationType.FailureEvent:
                    ProcessFailureEvent(observation);
                    break;
                case ObservationType.UserFeedback:
                    ProcessUserFeedback(observation);
                    break;
            }

            // Periodically check if adjustments are needed
            if (_observations.Count % 50 == 0)
            {
                UpdateParameterAdjustments();
            }
        }

        private void ProcessPredictionOutcome(LearningObservation observation)
        {
            if (!observation.PredictedValue.HasValue || !observation.ActualValue.HasValue)
                return;

            var error = observation.ActualValue.Value - observation.PredictedValue.Value;
            var errorPercent = observation.PredictedValue.Value != 0 ?
                error / observation.PredictedValue.Value : 0;

            // Track model performance
            TrackModelPerformance("FailurePrediction", observation.PredictionCorrect, Math.Abs(error));

            // If systematic bias detected, adjust parameters
            if (Math.Abs(errorPercent) > 0.1)
            {
                observation.ConfidenceAdjustment = -Math.Abs(errorPercent) * 0.1;
                observation.LessonLearned = $"Prediction error of {errorPercent:P0} - consider adjustment";
            }
        }

        private void ProcessMaintenanceOutcome(LearningObservation observation)
        {
            if (!observation.Context.ContainsKey("ActualHours") ||
                !observation.Context.ContainsKey("EstimatedHours"))
                return;

            var actualHours = Convert.ToDouble(observation.Context["ActualHours"]);
            var estimatedHours = Convert.ToDouble(observation.Context["EstimatedHours"]);
            var assetType = observation.Category;

            if (estimatedHours > 0)
            {
                var ratio = actualHours / estimatedHours;

                // Update duration multiplier for this asset type
                UpdateDurationMultiplier(assetType, ratio);
            }

            // Process cost variance
            if (observation.Context.ContainsKey("ActualCost") &&
                observation.Context.ContainsKey("EstimatedCost"))
            {
                var actualCost = Convert.ToDecimal(observation.Context["ActualCost"]);
                var estimatedCost = Convert.ToDecimal(observation.Context["EstimatedCost"]);

                if (estimatedCost > 0)
                {
                    var costRatio = (double)(actualCost / estimatedCost);
                    UpdateCostMultiplier(assetType, costRatio);
                }
            }
        }

        private void ProcessFailureEvent(LearningObservation observation)
        {
            var wasPredicted = observation.PredictionCorrect;
            var assetType = observation.EntityType;
            var failureMode = observation.Category;

            if (!wasPredicted)
            {
                // Missed failure - increase failure rate estimate
                var key = $"FailureRateMultiplier|AssetType|{assetType}";
                if (_parameterAdjustments.TryGetValue(key, out var adjustment))
                {
                    var newValue = adjustment.AdjustedValue * (1 + _learningRate);
                    adjustment.AdjustedValue = Math.Min(3.0, newValue); // Cap at 3x
                    adjustment.AdjustmentFactor = adjustment.AdjustedValue / adjustment.OriginalValue;
                    adjustment.SupportingObservations++;
                    adjustment.LastUpdated = DateTime.UtcNow;
                    adjustment.Reason = $"Increased due to missed failure prediction ({failureMode})";

                    Logger.Info($"Increased failure rate multiplier for {assetType} to {adjustment.AdjustedValue:F2}");
                }

                // Learn about this failure mode
                LearnFailureMode(assetType, failureMode);
            }
        }

        private void ProcessUserFeedback(LearningObservation observation)
        {
            switch (observation.Feedback)
            {
                case FeedbackType.FalsePositive:
                    // Reduce sensitivity
                    AdjustSensitivity(observation.EntityType, -0.1);
                    break;
                case FeedbackType.FalseNegative:
                    // Increase sensitivity
                    AdjustSensitivity(observation.EntityType, 0.1);
                    break;
                case FeedbackType.Negative:
                    // Learn from the error
                    if (!string.IsNullOrEmpty(observation.FeedbackNotes))
                    {
                        _learnedKnowledge.Add(new LearnedKnowledge
                        {
                            Category = "UserCorrection",
                            Subject = observation.EntityType,
                            Description = observation.FeedbackNotes,
                            LearnedAt = DateTime.UtcNow,
                            Confidence = 0.9,
                            ApplicableTo = new() { observation.EntityType }
                        });
                    }
                    break;
            }
        }

        #endregion

        #region Parameter Adjustment

        private void UpdateDurationMultiplier(string assetType, double ratio)
        {
            var key = $"MaintenanceDurationMultiplier|AssetType|{assetType}";

            if (!_parameterAdjustments.TryGetValue(key, out var adjustment))
            {
                adjustment = new ParameterAdjustment
                {
                    ParameterName = "MaintenanceDurationMultiplier",
                    Scope = "AssetType",
                    ScopeValue = assetType,
                    OriginalValue = 1.0,
                    AdjustedValue = 1.0
                };
                AddParameterAdjustment(adjustment);
            }

            // Exponential moving average
            adjustment.AdjustedValue = adjustment.AdjustedValue * (1 - _learningRate) + ratio * _learningRate;
            adjustment.AdjustmentFactor = adjustment.AdjustedValue / adjustment.OriginalValue;
            adjustment.SupportingObservations++;
            adjustment.LastUpdated = DateTime.UtcNow;
            adjustment.Confidence = Math.Min(0.95, 0.5 + adjustment.SupportingObservations * 0.01);
        }

        private void UpdateCostMultiplier(string assetType, double ratio)
        {
            var key = $"CostEstimateMultiplier|AssetType|{assetType}";

            if (!_parameterAdjustments.TryGetValue(key, out var adjustment))
            {
                adjustment = new ParameterAdjustment
                {
                    ParameterName = "CostEstimateMultiplier",
                    Scope = "AssetType",
                    ScopeValue = assetType,
                    OriginalValue = 1.0,
                    AdjustedValue = 1.0
                };
                AddParameterAdjustment(adjustment);
            }

            adjustment.AdjustedValue = adjustment.AdjustedValue * (1 - _learningRate) + ratio * _learningRate;
            adjustment.AdjustmentFactor = adjustment.AdjustedValue / adjustment.OriginalValue;
            adjustment.SupportingObservations++;
            adjustment.LastUpdated = DateTime.UtcNow;
            adjustment.Confidence = Math.Min(0.95, 0.5 + adjustment.SupportingObservations * 0.01);
        }

        private void AdjustSensitivity(string entityType, double delta)
        {
            var key = $"SensitivityMultiplier|EntityType|{entityType}";

            if (!_parameterAdjustments.TryGetValue(key, out var adjustment))
            {
                adjustment = new ParameterAdjustment
                {
                    ParameterName = "SensitivityMultiplier",
                    Scope = "EntityType",
                    ScopeValue = entityType,
                    OriginalValue = 1.0,
                    AdjustedValue = 1.0
                };
                AddParameterAdjustment(adjustment);
            }

            adjustment.AdjustedValue = Math.Max(0.5, Math.Min(2.0, adjustment.AdjustedValue + delta));
            adjustment.AdjustmentFactor = adjustment.AdjustedValue / adjustment.OriginalValue;
            adjustment.SupportingObservations++;
            adjustment.LastUpdated = DateTime.UtcNow;
        }

        private void UpdateParameterAdjustments()
        {
            // Analyze recent observations for systematic patterns
            var recentObservations = _observations
                .Where(o => o.ObservedAt > DateTime.UtcNow.AddDays(-30))
                .ToList();

            if (recentObservations.Count < _minObservationsForAdjustment)
                return;

            // Analyze prediction accuracy by category
            var predictionOutcomes = recentObservations
                .Where(o => o.Type == ObservationType.PredictionOutcome && o.PredictedValue.HasValue && o.ActualValue.HasValue)
                .GroupBy(o => o.EntityType)
                .ToList();

            foreach (var group in predictionOutcomes)
            {
                var errors = group.Select(o => o.ActualValue.Value - o.PredictedValue.Value).ToList();
                var meanError = errors.Average();

                // If there's a systematic bias
                if (Math.Abs(meanError) > _significanceThreshold && group.Count() >= _minObservationsForAdjustment)
                {
                    var avgPredicted = group.Average(o => o.PredictedValue.Value);
                    if (avgPredicted > 0)
                    {
                        var biasRatio = 1 + (meanError / avgPredicted);
                        Logger.Info($"Detected systematic bias for {group.Key}: {biasRatio:F2}x adjustment recommended");
                    }
                }
            }
        }

        private void LearnFailureMode(string assetType, string failureMode)
        {
            var existingKnowledge = _learnedKnowledge
                .FirstOrDefault(k => k.Category == "FailureMode" &&
                                    k.Subject == $"{assetType}:{failureMode}");

            if (existingKnowledge != null)
            {
                existingKnowledge.SupportingObservations++;
                existingKnowledge.Confidence = Math.Min(0.95, existingKnowledge.Confidence + 0.05);
            }
            else
            {
                _learnedKnowledge.Add(new LearnedKnowledge
                {
                    KnowledgeId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    Category = "FailureMode",
                    Subject = $"{assetType}:{failureMode}",
                    Description = $"Observed failure mode {failureMode} for {assetType}",
                    LearnedAt = DateTime.UtcNow,
                    SupportingObservations = 1,
                    Confidence = 0.6,
                    ApplicableTo = new() { assetType }
                });
            }
        }

        #endregion

        #region Model Performance Tracking

        private void TrackModelPerformance(string modelName, bool correct, double absoluteError)
        {
            if (!_modelPerformance.ContainsKey(modelName))
                _modelPerformance[modelName] = new List<ModelPerformanceMetrics>();

            var history = _modelPerformance[modelName];

            // Get or create today's metrics
            var today = DateTime.UtcNow.Date;
            var todayMetrics = history.FirstOrDefault(m => m.EvaluationDate.Date == today);

            if (todayMetrics == null)
            {
                todayMetrics = new ModelPerformanceMetrics
                {
                    ModelName = modelName,
                    EvaluationDate = today
                };
                history.Add(todayMetrics);
            }

            todayMetrics.TotalPredictions++;
            if (correct)
                todayMetrics.CorrectPredictions++;

            todayMetrics.Accuracy = (double)todayMetrics.CorrectPredictions / todayMetrics.TotalPredictions;
            todayMetrics.MeanAbsoluteError =
                (todayMetrics.MeanAbsoluteError * (todayMetrics.TotalPredictions - 1) + absoluteError) /
                todayMetrics.TotalPredictions;

            // Keep only last 90 days
            while (history.Count > 90)
                history.RemoveAt(0);
        }

        /// <summary>
        /// Get model performance metrics
        /// </summary>
        public ModelPerformanceMetrics GetModelPerformance(string modelName, int days = 30)
        {
            if (!_modelPerformance.TryGetValue(modelName, out var history))
                return null;

            var cutoff = DateTime.UtcNow.AddDays(-days);
            var relevantMetrics = history.Where(m => m.EvaluationDate >= cutoff).ToList();

            if (!relevantMetrics.Any())
                return null;

            var aggregated = new ModelPerformanceMetrics
            {
                ModelName = modelName,
                EvaluationDate = DateTime.UtcNow,
                TotalPredictions = relevantMetrics.Sum(m => m.TotalPredictions),
                CorrectPredictions = relevantMetrics.Sum(m => m.CorrectPredictions)
            };

            aggregated.Accuracy = aggregated.TotalPredictions > 0 ?
                (double)aggregated.CorrectPredictions / aggregated.TotalPredictions : 0;

            aggregated.MeanAbsoluteError = relevantMetrics.Average(m => m.MeanAbsoluteError);

            // Calculate trend
            if (relevantMetrics.Count >= 7)
            {
                var firstHalf = relevantMetrics.Take(relevantMetrics.Count / 2).Average(m => m.Accuracy);
                var secondHalf = relevantMetrics.Skip(relevantMetrics.Count / 2).Average(m => m.Accuracy);
                aggregated.AccuracyTrend = secondHalf - firstHalf;
                aggregated.PerformanceStatus = aggregated.AccuracyTrend > 0.02 ? "Improving" :
                                               aggregated.AccuracyTrend < -0.02 ? "Declining" : "Stable";
            }

            return aggregated;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get parameter adjustment
        /// </summary>
        public double GetAdjustmentFactor(string parameterName, string scope, string scopeValue)
        {
            // Try specific first
            var key = $"{parameterName}|{scope}|{scopeValue}";
            if (_parameterAdjustments.TryGetValue(key, out var adjustment) && adjustment.Confidence > 0.6)
                return adjustment.AdjustmentFactor;

            // Try global
            key = $"{parameterName}|Global|*";
            if (_parameterAdjustments.TryGetValue(key, out adjustment) && adjustment.Confidence > 0.6)
                return adjustment.AdjustmentFactor;

            return 1.0; // No adjustment
        }

        /// <summary>
        /// Get all parameter adjustments
        /// </summary>
        public List<ParameterAdjustment> GetAllAdjustments()
        {
            return _parameterAdjustments.Values
                .OrderByDescending(a => a.Confidence)
                .ToList();
        }

        /// <summary>
        /// Get learned knowledge
        /// </summary>
        public List<LearnedKnowledge> GetLearnedKnowledge(string category = null)
        {
            var query = _learnedKnowledge.AsEnumerable();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(k => k.Category == category);

            return query.OrderByDescending(k => k.Confidence).ToList();
        }

        /// <summary>
        /// Get learning statistics
        /// </summary>
        public LearningStatistics GetStatistics()
        {
            var recentObs = _observations
                .Where(o => o.ObservedAt > DateTime.UtcNow.AddDays(-30))
                .ToList();

            return new LearningStatistics
            {
                TotalObservations = _observations.Count,
                RecentObservations = recentObs.Count,
                TotalAdjustments = _parameterAdjustments.Count,
                HighConfidenceAdjustments = _parameterAdjustments.Values.Count(a => a.Confidence > 0.8),
                LearnedKnowledgeItems = _learnedKnowledge.Count,
                ObservationsByType = recentObs
                    .GroupBy(o => o.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                FeedbackSummary = new FeedbackSummary
                {
                    PositiveCount = recentObs.Count(o => o.Feedback == FeedbackType.Positive),
                    NegativeCount = recentObs.Count(o => o.Feedback == FeedbackType.Negative),
                    FalsePositiveCount = recentObs.Count(o => o.Feedback == FeedbackType.FalsePositive),
                    FalseNegativeCount = recentObs.Count(o => o.Feedback == FeedbackType.FalseNegative)
                },
                PredictionAccuracy = recentObs
                    .Where(o => o.Type == ObservationType.PredictionOutcome)
                    .Select(o => o.PredictionCorrect)
                    .DefaultIfEmpty(false)
                    .Average(correct => correct ? 1.0 : 0.0)
            };
        }

        /// <summary>
        /// Export learning data for analysis
        /// </summary>
        public LearningExport ExportLearningData()
        {
            return new LearningExport
            {
                ExportDate = DateTime.UtcNow,
                Observations = _observations.TakeLast(1000).ToList(),
                ParameterAdjustments = _parameterAdjustments.Values.ToList(),
                LearnedKnowledge = _learnedKnowledge,
                ModelPerformance = _modelPerformance
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => GetModelPerformance(kvp.Key)
                    )
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate learned knowledge
        /// </summary>
        public void ValidateKnowledge(string knowledgeId, string validatedBy)
        {
            var knowledge = _learnedKnowledge.FirstOrDefault(k => k.KnowledgeId == knowledgeId);
            if (knowledge != null)
            {
                knowledge.IsValidated = true;
                knowledge.ValidatedBy = validatedBy;
                knowledge.Confidence = Math.Min(0.98, knowledge.Confidence + 0.1);

                Logger.Info($"Knowledge {knowledgeId} validated by {validatedBy}");
            }
        }

        /// <summary>
        /// Reset learning for a specific parameter
        /// </summary>
        public void ResetParameter(string parameterName, string scope, string scopeValue)
        {
            var key = $"{parameterName}|{scope}|{scopeValue}";
            if (_parameterAdjustments.TryGetValue(key, out var adjustment))
            {
                adjustment.AdjustedValue = adjustment.OriginalValue;
                adjustment.AdjustmentFactor = 1.0;
                adjustment.SupportingObservations = 0;
                adjustment.Confidence = 0.5;
                adjustment.LastUpdated = DateTime.UtcNow;
                adjustment.Reason = "Reset by user";

                Logger.Info($"Reset parameter adjustment: {key}");
            }
        }

        #endregion

        #endregion // FM Learning Engine
    }

    #region Supporting Classes

    public class LearningStatistics
    {
        public int TotalObservations { get; set; }
        public int RecentObservations { get; set; }
        public int TotalAdjustments { get; set; }
        public int HighConfidenceAdjustments { get; set; }
        public int LearnedKnowledgeItems { get; set; }
        public Dictionary<string, int> ObservationsByType { get; set; } = new();
        public FeedbackSummary FeedbackSummary { get; set; }
        public double PredictionAccuracy { get; set; }
    }

    public class FeedbackSummary
    {
        public int PositiveCount { get; set; }
        public int NegativeCount { get; set; }
        public int FalsePositiveCount { get; set; }
        public int FalseNegativeCount { get; set; }
    }

    public class LearningExport
    {
        public DateTime ExportDate { get; set; }
        public List<LearningObservation> Observations { get; set; } = new();
        public List<ParameterAdjustment> ParameterAdjustments { get; set; } = new();
        public List<LearnedKnowledge> LearnedKnowledge { get; set; } = new();
        public Dictionary<string, ModelPerformanceMetrics> ModelPerformance { get; set; } = new();
    }

    #endregion
}
