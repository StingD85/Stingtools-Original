// StingBIM.AI.Core.Learning.ModelUpdater
// Updates and improves AI models based on learning
// Master Proposal Reference: Part 2.2 Strategy 6 - Recursive Self-Improvement

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Core.Learning
{
    /// <summary>
    /// Updates AI models and rules based on accumulated learning.
    /// Implements Loop 3 (Lifetime) of the Triple Learning Loop.
    /// Enables recursive self-improvement (Strategy 6).
    /// </summary>
    public class ModelUpdater
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _updatesPath;
        private readonly object _lock = new object();
        private List<ModelUpdate> _pendingUpdates;
        private PerformanceMetrics _currentMetrics;
        private readonly Dictionary<string, Func<ModelUpdate, bool>> _updateConsumers;
        private readonly Dictionary<string, object> _learnedDefaults;
        private readonly Dictionary<string, ConfidenceAdjustment> _confidenceAdjustments;
        private readonly List<LearnedValidationRule> _learnedValidationRules;

        public ModelUpdater()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "model_updates.json"))
        {
        }

        public ModelUpdater(string updatesPath)
        {
            _updatesPath = updatesPath;
            _pendingUpdates = new List<ModelUpdate>();
            _currentMetrics = new PerformanceMetrics();
            _updateConsumers = new Dictionary<string, Func<ModelUpdate, bool>>(StringComparer.OrdinalIgnoreCase);
            _learnedDefaults = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _confidenceAdjustments = new Dictionary<string, ConfidenceAdjustment>(StringComparer.OrdinalIgnoreCase);
            _learnedValidationRules = new List<LearnedValidationRule>();
        }

        /// <summary>
        /// Event fired when a model update is ready to be applied.
        /// </summary>
        public event EventHandler<ModelUpdate> UpdateReady;

        /// <summary>
        /// Analyzes accumulated patterns and proposes model updates.
        /// </summary>
        public async Task<IEnumerable<ModelUpdate>> AnalyzeAndProposeUpdatesAsync(
            IEnumerable<LearnedPattern> patterns,
            IEnumerable<FeedbackEntry> feedback)
        {
            return await Task.Run(() =>
            {
                var updates = new List<ModelUpdate>();

                // Analyze correction patterns to propose rule updates
                var correctionPatterns = patterns.Where(p => p.PatternType == PatternType.Correction).ToList();
                if (correctionPatterns.Any())
                {
                    updates.AddRange(ProposeRuleUpdatesFromCorrections(correctionPatterns));
                }

                // Analyze preference patterns to propose default updates
                var preferencePatterns = patterns.Where(p => p.PatternType == PatternType.Preference).ToList();
                if (preferencePatterns.Any())
                {
                    updates.AddRange(ProposeDefaultUpdatesFromPreferences(preferencePatterns));
                }

                // Analyze feedback to propose confidence adjustments
                var feedbackList = feedback.ToList();
                if (feedbackList.Any())
                {
                    updates.AddRange(ProposeConfidenceAdjustments(feedbackList));
                }

                lock (_lock)
                {
                    _pendingUpdates.AddRange(updates);
                }

                Logger.Info($"Proposed {updates.Count} model updates");
                return updates;
            });
        }

        /// <summary>
        /// Proposes rule updates based on correction patterns.
        /// </summary>
        private IEnumerable<ModelUpdate> ProposeRuleUpdatesFromCorrections(IEnumerable<LearnedPattern> corrections)
        {
            var updates = new List<ModelUpdate>();

            foreach (var correction in corrections)
            {
                if (correction.Occurrences >= 3 && correction.Confidence >= 0.7f)
                {
                    var problematicAction = correction.Context.GetValueOrDefault("ProblematicAction")?.ToString();

                    updates.Add(new ModelUpdate
                    {
                        Id = Guid.NewGuid().ToString(),
                        UpdateType = UpdateType.ValidationRule,
                        TargetComponent = "ValidationEngine",
                        Description = $"Add warning for {problematicAction} based on {correction.Occurrences} corrections",
                        Priority = UpdatePriority.Medium,
                        ProposedChange = new Dictionary<string, object>
                        {
                            { "Action", problematicAction },
                            { "WarningMessage", $"This action is often modified. Consider reviewing parameters." },
                            { "ConfidenceThreshold", 0.5f }
                        },
                        ExpectedImpact = $"Reduce undo rate for {problematicAction} by ~30%",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            return updates;
        }

        /// <summary>
        /// Proposes default value updates based on preference patterns.
        /// </summary>
        private IEnumerable<ModelUpdate> ProposeDefaultUpdatesFromPreferences(IEnumerable<LearnedPattern> preferences)
        {
            var updates = new List<ModelUpdate>();

            foreach (var preference in preferences)
            {
                if (preference.Occurrences >= 5 && preference.Confidence >= 0.8f)
                {
                    var actionType = preference.Context.GetValueOrDefault("ActionType")?.ToString();
                    var parameter = preference.Context.GetValueOrDefault("Parameter")?.ToString();
                    var preferredValue = preference.Context.GetValueOrDefault("PreferredValue");

                    updates.Add(new ModelUpdate
                    {
                        Id = Guid.NewGuid().ToString(),
                        UpdateType = UpdateType.DefaultValue,
                        TargetComponent = "ParameterDefaults",
                        Description = $"Update default {parameter} for {actionType} to {preferredValue}",
                        Priority = UpdatePriority.Low,
                        ProposedChange = new Dictionary<string, object>
                        {
                            { "Action", actionType },
                            { "Parameter", parameter },
                            { "NewDefault", preferredValue },
                            { "Confidence", preference.Confidence }
                        },
                        ExpectedImpact = $"Reduce parameter modifications for {actionType} by ~50%",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            return updates;
        }

        /// <summary>
        /// Proposes confidence adjustments based on feedback.
        /// </summary>
        private IEnumerable<ModelUpdate> ProposeConfidenceAdjustments(IEnumerable<FeedbackEntry> feedback)
        {
            var updates = new List<ModelUpdate>();

            // Group feedback by action
            var feedbackByAction = feedback.GroupBy(f => f.Action).ToList();

            foreach (var group in feedbackByAction)
            {
                var action = group.Key;
                var entries = group.ToList();

                var acceptRate = entries.Count(e => e.Reaction == UserReaction.Accepted) / (float)entries.Count;
                var undoRate = entries.Count(e => e.Reaction == UserReaction.Undone) / (float)entries.Count;

                // If acceptance rate is high, increase confidence
                if (acceptRate >= 0.9f && entries.Count >= 10)
                {
                    updates.Add(new ModelUpdate
                    {
                        Id = Guid.NewGuid().ToString(),
                        UpdateType = UpdateType.ConfidenceBoost,
                        TargetComponent = "IntentClassifier",
                        Description = $"Increase confidence for {action} (acceptance rate: {acceptRate:P0})",
                        Priority = UpdatePriority.Low,
                        ProposedChange = new Dictionary<string, object>
                        {
                            { "Action", action },
                            { "ConfidenceBoost", 0.1f },
                            { "Reason", $"High acceptance rate ({entries.Count} samples)" }
                        },
                        ExpectedImpact = "Faster response times for this action",
                        CreatedAt = DateTime.Now
                    });
                }

                // If undo rate is high, decrease confidence or add warning
                if (undoRate >= 0.3f && entries.Count >= 5)
                {
                    updates.Add(new ModelUpdate
                    {
                        Id = Guid.NewGuid().ToString(),
                        UpdateType = UpdateType.ConfidenceReduction,
                        TargetComponent = "IntentClassifier",
                        Description = $"Add confirmation for {action} (undo rate: {undoRate:P0})",
                        Priority = UpdatePriority.High,
                        ProposedChange = new Dictionary<string, object>
                        {
                            { "Action", action },
                            { "RequireConfirmation", true },
                            { "Reason", $"High undo rate ({entries.Count} samples)" }
                        },
                        ExpectedImpact = "Reduce accidental actions and improve user satisfaction",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            return updates;
        }

        /// <summary>
        /// Registers an update consumer for a specific target component.
        /// When updates targeting that component are applied, the consumer callback is invoked.
        /// </summary>
        public void RegisterUpdateConsumer(string targetComponent, Func<ModelUpdate, bool> consumer)
        {
            lock (_lock)
            {
                _updateConsumers[targetComponent] = consumer;
            }
            Logger.Info($"Registered update consumer for: {targetComponent}");
        }

        /// <summary>
        /// Applies a model update by dispatching to the registered consumer.
        /// Returns true if the update was successfully consumed by the target component.
        /// </summary>
        public async Task<bool> ApplyUpdateAsync(string updateId)
        {
            ModelUpdate update;
            lock (_lock)
            {
                update = _pendingUpdates.FirstOrDefault(u => u.Id == updateId);
                if (update == null)
                {
                    Logger.Warn($"Update {updateId} not found");
                    return false;
                }
            }

            Logger.Info($"Applying update: {update.Description}");

            // Try to dispatch to registered consumer
            bool consumed = false;
            Func<ModelUpdate, bool> consumer = null;

            lock (_lock)
            {
                _updateConsumers.TryGetValue(update.TargetComponent, out consumer);
            }

            if (consumer != null)
            {
                try
                {
                    consumed = consumer(update);
                    if (consumed)
                    {
                        Logger.Info($"Update consumed by {update.TargetComponent}");
                    }
                    else
                    {
                        Logger.Warn($"Update rejected by {update.TargetComponent}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Update consumer for {update.TargetComponent} threw exception");
                }
            }

            // Apply to learned defaults store (built-in consumer for DefaultValue updates)
            if (update.UpdateType == UpdateType.DefaultValue)
            {
                ApplyDefaultValueUpdate(update);
                consumed = true;
            }

            // Apply to confidence adjustments store (built-in consumer for confidence updates)
            if (update.UpdateType == UpdateType.ConfidenceBoost || update.UpdateType == UpdateType.ConfidenceReduction)
            {
                ApplyConfidenceUpdate(update);
                consumed = true;
            }

            // Apply to validation rules store (built-in consumer for ValidationRule updates)
            if (update.UpdateType == UpdateType.ValidationRule)
            {
                ApplyValidationRuleUpdate(update);
                consumed = true;
            }

            update.AppliedAt = DateTime.Now;
            update.Status = consumed ? UpdateStatus.Applied : UpdateStatus.Proposed;

            // Fire event for additional listeners
            UpdateReady?.Invoke(this, update);

            // Persist the update
            await PersistUpdateAsync(update);
            return consumed;
        }

        /// <summary>
        /// Gets all learned default values that have been applied.
        /// Consumers should query this to get user-preferred defaults.
        /// </summary>
        public IReadOnlyDictionary<string, object> GetLearnedDefaults()
        {
            lock (_lock)
            {
                return new Dictionary<string, object>(_learnedDefaults);
            }
        }

        /// <summary>
        /// Gets confidence adjustments that have been applied.
        /// Returns action → (boost amount, requires confirmation).
        /// </summary>
        public IReadOnlyDictionary<string, ConfidenceAdjustment> GetConfidenceAdjustments()
        {
            lock (_lock)
            {
                return new Dictionary<string, ConfidenceAdjustment>(_confidenceAdjustments);
            }
        }

        /// <summary>
        /// Gets learned validation rules that have been applied.
        /// </summary>
        public IReadOnlyList<LearnedValidationRule> GetLearnedValidationRules()
        {
            lock (_lock)
            {
                return _learnedValidationRules.ToList();
            }
        }

        private void ApplyDefaultValueUpdate(ModelUpdate update)
        {
            var action = update.ProposedChange.GetValueOrDefault("Action")?.ToString() ?? "";
            var parameter = update.ProposedChange.GetValueOrDefault("Parameter")?.ToString() ?? "";
            var newDefault = update.ProposedChange.GetValueOrDefault("NewDefault");

            if (!string.IsNullOrEmpty(action) && !string.IsNullOrEmpty(parameter) && newDefault != null)
            {
                var key = $"{action}.{parameter}";
                lock (_lock)
                {
                    _learnedDefaults[key] = newDefault;
                }
                Logger.Info($"Applied learned default: {key} = {newDefault}");
            }
        }

        private void ApplyConfidenceUpdate(ModelUpdate update)
        {
            var action = update.ProposedChange.GetValueOrDefault("Action")?.ToString() ?? "";
            if (string.IsNullOrEmpty(action)) return;

            lock (_lock)
            {
                if (!_confidenceAdjustments.ContainsKey(action))
                {
                    _confidenceAdjustments[action] = new ConfidenceAdjustment();
                }

                if (update.UpdateType == UpdateType.ConfidenceBoost)
                {
                    var boost = update.ProposedChange.GetValueOrDefault("ConfidenceBoost") is float b ? b : 0.1f;
                    _confidenceAdjustments[action].BoostAmount += boost;
                }
                else
                {
                    _confidenceAdjustments[action].RequiresConfirmation =
                        update.ProposedChange.GetValueOrDefault("RequireConfirmation") is bool rc && rc;
                }
            }
            Logger.Info($"Applied confidence adjustment for: {action}");
        }

        private void ApplyValidationRuleUpdate(ModelUpdate update)
        {
            var action = update.ProposedChange.GetValueOrDefault("Action")?.ToString() ?? "";
            var warning = update.ProposedChange.GetValueOrDefault("WarningMessage")?.ToString() ?? "";

            if (!string.IsNullOrEmpty(action))
            {
                lock (_lock)
                {
                    _learnedValidationRules.Add(new LearnedValidationRule
                    {
                        Action = action,
                        WarningMessage = warning,
                        CreatedAt = DateTime.Now
                    });
                }
                Logger.Info($"Applied validation rule for: {action}");
            }
        }

        /// <summary>
        /// Updates performance metrics.
        /// </summary>
        public void UpdateMetrics(PerformanceMetrics metrics)
        {
            lock (_lock)
            {
                _currentMetrics = metrics;
            }
        }

        /// <summary>
        /// Gets pending updates.
        /// </summary>
        public IEnumerable<ModelUpdate> GetPendingUpdates()
        {
            lock (_lock)
            {
                return _pendingUpdates.Where(u => u.Status == UpdateStatus.Proposed).ToList();
            }
        }

        private async Task PersistUpdateAsync(ModelUpdate update)
        {
            try
            {
                var directory = Path.GetDirectoryName(_updatesPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var appliedUpdates = new List<ModelUpdate>();
                if (File.Exists(_updatesPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(_updatesPath));
                    appliedUpdates = JsonConvert.DeserializeObject<List<ModelUpdate>>(json) ?? new List<ModelUpdate>();
                }

                appliedUpdates.Add(update);
                var outputJson = JsonConvert.SerializeObject(appliedUpdates, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(_updatesPath, outputJson));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to persist model update");
            }
        }
    }

    /// <summary>
    /// Represents a proposed or applied model update.
    /// </summary>
    public class ModelUpdate
    {
        public string Id { get; set; }
        public UpdateType UpdateType { get; set; }
        public string TargetComponent { get; set; }
        public string Description { get; set; }
        public UpdatePriority Priority { get; set; }
        public Dictionary<string, object> ProposedChange { get; set; } = new Dictionary<string, object>();
        public string ExpectedImpact { get; set; }
        public UpdateStatus Status { get; set; } = UpdateStatus.Proposed;
        public DateTime CreatedAt { get; set; }
        public DateTime? AppliedAt { get; set; }
    }

    public enum UpdateType
    {
        ValidationRule,
        DefaultValue,
        ConfidenceBoost,
        ConfidenceReduction,
        NewCapability,
        BehaviorAdjustment
    }

    public enum UpdatePriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum UpdateStatus
    {
        Proposed,
        Approved,
        Applied,
        Rejected,
        Reverted
    }

    /// <summary>
    /// Tracks AI performance metrics for self-improvement analysis.
    /// </summary>
    public class PerformanceMetrics
    {
        public float CommandSuccessRate { get; set; }
        public float AverageResponseTimeMs { get; set; }
        public float UndoRate { get; set; }
        public float ClarificationRate { get; set; }
        public int TotalCommands { get; set; }
        public DateTime MeasuredAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents a confidence adjustment for an action based on user feedback patterns.
    /// </summary>
    public class ConfidenceAdjustment
    {
        public float BoostAmount { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    /// <summary>
    /// Represents a validation rule learned from user correction patterns.
    /// </summary>
    public class LearnedValidationRule
    {
        public string Action { get; set; }
        public string WarningMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
