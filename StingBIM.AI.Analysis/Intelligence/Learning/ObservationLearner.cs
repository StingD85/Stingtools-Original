// StingBIM.AI.Intelligence.Learning.ObservationLearner
// Silent background learner that watches every user action in Revit,
// extracts patterns from element placement, parameter modifications,
// workflows, timing, and errors without user awareness.
// Master Proposal Reference: Part 2.3 - Phase 3 Active Intelligence

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Intelligence.Learning
{
    #region Observation Learner

    /// <summary>
    /// Silent background learner that passively observes all Revit API actions
    /// and user behavior. Extracts element placement patterns, parameter modification
    /// patterns, spatial patterns, workflow sequences, timing data, error patterns,
    /// expert identification, and implicit preferences. All data is anonymized.
    /// </summary>
    public class ObservationLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Observation queues and storage
        private readonly ConcurrentQueue<ObservationRecord> _pendingObservations;
        private readonly ConcurrentDictionary<string, ElementPlacementPattern> _placementPatterns;
        private readonly ConcurrentDictionary<string, ParameterModificationPattern> _parameterPatterns;
        private readonly ConcurrentDictionary<string, SpatialPlacementPattern> _spatialPatterns;
        private readonly ConcurrentDictionary<string, WorkflowSequencePattern> _workflowPatterns;
        private readonly ConcurrentDictionary<string, TimePattern> _timePatterns;
        private readonly ConcurrentDictionary<string, ErrorPattern> _errorPatterns;
        private readonly ConcurrentDictionary<string, UserProfile> _userProfiles;
        private readonly ConcurrentDictionary<string, ImplicitPreference> _implicitPreferences;
        private readonly ConcurrentDictionary<string, AnomalyRecord> _anomalies;

        // Workflow tracking per user
        private readonly ConcurrentDictionary<string, UserWorkflowTracker> _activeWorkflows;

        // Configuration and background processing
        private readonly ObservationLearnerConfiguration _configuration;
        private Timer _batchProcessingTimer;
        private CancellationTokenSource _processingCts;
        private Task _backgroundTask;
        private long _totalObservations;
        private long _processedObservations;
        private DateTime _startTime;

        public ObservationLearner()
            : this(new ObservationLearnerConfiguration())
        {
        }

        public ObservationLearner(ObservationLearnerConfiguration configuration)
        {
            _configuration = configuration ?? new ObservationLearnerConfiguration();
            _pendingObservations = new ConcurrentQueue<ObservationRecord>();
            _placementPatterns = new ConcurrentDictionary<string, ElementPlacementPattern>(StringComparer.OrdinalIgnoreCase);
            _parameterPatterns = new ConcurrentDictionary<string, ParameterModificationPattern>(StringComparer.OrdinalIgnoreCase);
            _spatialPatterns = new ConcurrentDictionary<string, SpatialPlacementPattern>(StringComparer.OrdinalIgnoreCase);
            _workflowPatterns = new ConcurrentDictionary<string, WorkflowSequencePattern>(StringComparer.OrdinalIgnoreCase);
            _timePatterns = new ConcurrentDictionary<string, TimePattern>(StringComparer.OrdinalIgnoreCase);
            _errorPatterns = new ConcurrentDictionary<string, ErrorPattern>(StringComparer.OrdinalIgnoreCase);
            _userProfiles = new ConcurrentDictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);
            _implicitPreferences = new ConcurrentDictionary<string, ImplicitPreference>(StringComparer.OrdinalIgnoreCase);
            _anomalies = new ConcurrentDictionary<string, AnomalyRecord>(StringComparer.OrdinalIgnoreCase);
            _activeWorkflows = new ConcurrentDictionary<string, UserWorkflowTracker>(StringComparer.OrdinalIgnoreCase);

            _totalObservations = 0;
            _processedObservations = 0;
            _startTime = DateTime.UtcNow;

            Logger.Info("ObservationLearner initialized");
        }

        #region Lifecycle

        /// <summary>
        /// Starts the background observation processing loop.
        /// </summary>
        public void Start()
        {
            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
            {
                Logger.Warn("ObservationLearner already running");
                return;
            }

            _processingCts = new CancellationTokenSource();

            // Timer-based batch processing
            _batchProcessingTimer = new Timer(
                _ => ProcessBatch(),
                null,
                TimeSpan.FromMilliseconds(_configuration.BatchProcessingIntervalMs),
                TimeSpan.FromMilliseconds(_configuration.BatchProcessingIntervalMs));

            // Background consolidation task
            _backgroundTask = Task.Run(async () =>
            {
                Logger.Info("ObservationLearner background processing started");
                while (!_processingCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromMinutes(_configuration.ConsolidationIntervalMinutes),
                            _processingCts.Token);
                        ConsolidatePatterns();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error in background consolidation");
                    }
                }
                Logger.Info("ObservationLearner background processing stopped");
            }, _processingCts.Token);

            Logger.Info("ObservationLearner started with batch interval {0}ms",
                _configuration.BatchProcessingIntervalMs);
        }

        /// <summary>
        /// Stops the background processing and saves state.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("Stopping ObservationLearner...");

            _batchProcessingTimer?.Dispose();
            _batchProcessingTimer = null;

            _processingCts?.Cancel();
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Process remaining observations
            ProcessBatch();

            // Save state
            await SaveStateAsync(cancellationToken);

            Logger.Info("ObservationLearner stopped. Total observations: {0}, processed: {1}",
                _totalObservations, _processedObservations);
        }

        #endregion

        #region Observation Recording

        /// <summary>
        /// Records a generic user action observation.
        /// This is the primary entry point for all observation types.
        /// </summary>
        public void ObserveAction(
            string userId,
            string actionType,
            Dictionary<string, object> parameters,
            Dictionary<string, object> context)
        {
            var record = new ObservationRecord
            {
                ObservationId = GenerateObservationId(),
                AnonymizedUserId = AnonymizeUserId(userId),
                ActionType = actionType,
                Parameters = parameters ?? new Dictionary<string, object>(),
                Context = context ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow,
                ObservationType = ClassifyObservationType(actionType)
            };

            _pendingObservations.Enqueue(record);
            Interlocked.Increment(ref _totalObservations);

            // Update workflow tracker
            UpdateWorkflowTracker(record);

            // Update time patterns
            RecordTimingData(record);

            // Check for anomalies
            CheckForAnomalies(record);

            Logger.Trace("Observed action '{0}' from user (anonymized)", actionType);
        }

        /// <summary>
        /// Records an element placement observation with spatial data.
        /// </summary>
        public void ObserveElementPlacement(
            string elementType,
            PlacementLocation location,
            Dictionary<string, object> parameters)
        {
            var record = new ObservationRecord
            {
                ObservationId = GenerateObservationId(),
                ActionType = "ElementPlacement",
                ObservationType = ObservationType.ElementPlacement,
                Parameters = parameters ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow,
                Context = new Dictionary<string, object>
                {
                    ["ElementType"] = elementType,
                    ["X"] = location.X,
                    ["Y"] = location.Y,
                    ["Z"] = location.Z,
                    ["Level"] = location.Level ?? "",
                    ["RoomType"] = location.RoomType ?? "",
                    ["NearbyElements"] = location.NearbyElementTypes ?? new List<string>()
                }
            };

            _pendingObservations.Enqueue(record);
            Interlocked.Increment(ref _totalObservations);

            // Immediate spatial pattern tracking
            TrackSpatialPlacement(elementType, location, parameters);

            Logger.Trace("Observed placement of '{0}' at ({1:F1}, {2:F1}, {3:F1})",
                elementType, location.X, location.Y, location.Z);
        }

        /// <summary>
        /// Records a parameter modification observation.
        /// </summary>
        public void ObserveParameterModification(
            string elementType,
            string parameterName,
            object oldValue,
            object newValue,
            Dictionary<string, object> context)
        {
            var record = new ObservationRecord
            {
                ObservationId = GenerateObservationId(),
                ActionType = "ParameterModification",
                ObservationType = ObservationType.ParameterModification,
                Parameters = new Dictionary<string, object>
                {
                    ["ElementType"] = elementType,
                    ["ParameterName"] = parameterName,
                    ["OldValue"] = oldValue,
                    ["NewValue"] = newValue
                },
                Context = context ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow
            };

            _pendingObservations.Enqueue(record);
            Interlocked.Increment(ref _totalObservations);

            // Immediate parameter pattern tracking
            TrackParameterModification(elementType, parameterName, oldValue, newValue);

            Logger.Trace("Observed parameter change '{0}.{1}': {2} -> {3}",
                elementType, parameterName, oldValue, newValue);
        }

        /// <summary>
        /// Records an error or failure observation.
        /// </summary>
        public void ObserveError(
            string userId,
            string actionType,
            string errorMessage,
            Dictionary<string, object> context)
        {
            var anonymizedId = AnonymizeUserId(userId);
            var key = $"{actionType}_{errorMessage?.GetHashCode():X8}";

            _errorPatterns.AddOrUpdate(key,
                new ErrorPattern
                {
                    PatternId = key,
                    ActionType = actionType,
                    ErrorMessage = errorMessage ?? "Unknown error",
                    OccurrenceCount = 1,
                    FirstSeen = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    AffectedUsers = new HashSet<string> { anonymizedId },
                    PrecedingActions = GetRecentActionsForUser(anonymizedId, 3),
                    Context = context ?? new Dictionary<string, object>()
                },
                (_, existing) =>
                {
                    existing.OccurrenceCount++;
                    existing.LastSeen = DateTime.UtcNow;
                    existing.AffectedUsers.Add(anonymizedId);
                    return existing;
                });

            Logger.Debug("Observed error in '{0}': {1} (count: {2})",
                actionType, TruncateForLog(errorMessage, 80),
                _errorPatterns.TryGetValue(key, out var ep) ? ep.OccurrenceCount : 1);
        }

        #endregion

        #region Pattern Retrieval

        /// <summary>
        /// Gets implicit preferences - what users accept without modification.
        /// Parameters that are never changed indicate satisfaction with defaults.
        /// </summary>
        public List<ImplicitPreference> GetImplicitPreferences(
            string actionType = null,
            CancellationToken cancellationToken = default)
        {
            var query = _implicitPreferences.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(actionType))
            {
                query = query.Where(p =>
                    string.Equals(p.ActionType, actionType, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .Where(p => p.AcceptanceRate >= _configuration.ImplicitPreferenceThreshold)
                .OrderByDescending(p => p.AcceptanceRate)
                .ThenByDescending(p => p.ObservationCount)
                .ToList();
        }

        /// <summary>
        /// Gets patterns from experienced/expert users, weighted higher for confidence.
        /// Expert identification is based on speed, accuracy, and consistency.
        /// </summary>
        public List<ExpertPattern> GetExpertPatterns(
            CancellationToken cancellationToken = default)
        {
            var expertPatterns = new List<ExpertPattern>();

            // Identify expert users
            var experts = _userProfiles.Values
                .Where(u => u.ExpertiseScore >= _configuration.ExpertThreshold)
                .OrderByDescending(u => u.ExpertiseScore)
                .ToList();

            if (!experts.Any())
            {
                Logger.Debug("No expert users identified yet");
                return expertPatterns;
            }

            Logger.Debug("Identified {0} expert users", experts.Count);

            // Collect patterns from experts
            foreach (var expert in experts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Expert placement patterns
                var expertPlacements = _placementPatterns.Values
                    .Where(p => p.ContributingUsers.Contains(expert.AnonymizedUserId))
                    .ToList();

                foreach (var placement in expertPlacements)
                {
                    expertPatterns.Add(new ExpertPattern
                    {
                        PatternId = $"EXP_PLC_{placement.PatternKey}",
                        PatternType = ExpertPatternType.ElementPlacement,
                        Description = $"Expert placement pattern: {placement.ElementType} in " +
                                      $"{placement.ContextType} context",
                        Confidence = placement.Confidence * expert.ExpertiseScore,
                        ObservationCount = placement.ObservationCount,
                        ExpertCount = 1,
                        Details = new Dictionary<string, object>
                        {
                            ["ElementType"] = placement.ElementType,
                            ["ContextType"] = placement.ContextType,
                            ["CommonParameters"] = placement.CommonParameterValues,
                            ["CoPlacedElements"] = placement.CoPlacedElementTypes
                        }
                    });
                }

                // Expert parameter patterns
                var expertParams = _parameterPatterns.Values
                    .Where(p => p.ContributingUsers.Contains(expert.AnonymizedUserId))
                    .ToList();

                foreach (var param in expertParams)
                {
                    expertPatterns.Add(new ExpertPattern
                    {
                        PatternId = $"EXP_PRM_{param.PatternKey}",
                        PatternType = ExpertPatternType.ParameterValue,
                        Description = $"Expert parameter pattern: {param.ElementType}.{param.ParameterName}",
                        Confidence = param.Confidence * expert.ExpertiseScore,
                        ObservationCount = param.ModificationCount,
                        ExpertCount = 1,
                        Details = new Dictionary<string, object>
                        {
                            ["ElementType"] = param.ElementType,
                            ["ParameterName"] = param.ParameterName,
                            ["PreferredValue"] = param.MostCommonValue,
                            ["ValueDistribution"] = param.ValueDistribution
                        }
                    });
                }

                // Expert workflow patterns
                var expertWorkflows = _workflowPatterns.Values
                    .Where(w => w.ContributingUsers.Contains(expert.AnonymizedUserId))
                    .ToList();

                foreach (var workflow in expertWorkflows)
                {
                    expertPatterns.Add(new ExpertPattern
                    {
                        PatternId = $"EXP_WF_{workflow.PatternKey}",
                        PatternType = ExpertPatternType.Workflow,
                        Description = $"Expert workflow: {string.Join(" -> ", workflow.ActionSequence.Take(4))}",
                        Confidence = workflow.Confidence * expert.ExpertiseScore,
                        ObservationCount = workflow.ObservationCount,
                        ExpertCount = 1,
                        Details = new Dictionary<string, object>
                        {
                            ["Sequence"] = workflow.ActionSequence,
                            ["AverageDurationMinutes"] = workflow.AverageDurationMinutes,
                            ["SuccessRate"] = workflow.SuccessRate
                        }
                    });
                }
            }

            // Merge patterns from multiple experts
            var merged = MergeExpertPatterns(expertPatterns);

            return merged
                .OrderByDescending(p => p.Confidence)
                .ThenByDescending(p => p.ExpertCount)
                .ToList();
        }

        /// <summary>
        /// Gets common workflow templates - sequences of operations that users
        /// repeatedly perform.
        /// </summary>
        public List<WorkflowTemplate> GetWorkflowTemplates(
            int maxTemplates = 10,
            CancellationToken cancellationToken = default)
        {
            var templates = _workflowPatterns.Values
                .Where(w => w.ObservationCount >= _configuration.MinWorkflowObservations &&
                            w.Confidence >= _configuration.MinWorkflowConfidence)
                .OrderByDescending(w => w.ObservationCount * w.Confidence)
                .Take(maxTemplates)
                .Select(w => new WorkflowTemplate
                {
                    TemplateId = w.PatternKey,
                    Name = GenerateWorkflowName(w),
                    Description = GenerateWorkflowDescription(w),
                    ActionSequence = w.ActionSequence.ToList(),
                    AverageDurationMinutes = w.AverageDurationMinutes,
                    SuccessRate = w.SuccessRate,
                    ObservationCount = w.ObservationCount,
                    Confidence = w.Confidence,
                    UserCount = w.ContributingUsers.Count,
                    TypicalContext = w.TypicalContext
                })
                .ToList();

            Logger.Debug("Returning {0} workflow templates", templates.Count);
            return templates;
        }

        /// <summary>
        /// Gets element placement patterns - which elements are placed together,
        /// in what order, and in what context.
        /// </summary>
        public List<ElementPlacementPattern> GetPlacementPatterns(
            string elementType = null,
            string contextType = null,
            int maxPatterns = 20)
        {
            var query = _placementPatterns.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(elementType))
            {
                query = query.Where(p =>
                    string.Equals(p.ElementType, elementType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(contextType))
            {
                query = query.Where(p =>
                    string.Equals(p.ContextType, contextType, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(p => p.ObservationCount * p.Confidence)
                .Take(maxPatterns)
                .ToList();
        }

        /// <summary>
        /// Gets parameter modification patterns - what values users set and how
        /// they differ from defaults.
        /// </summary>
        public List<ParameterModificationPattern> GetParameterPatterns(
            string elementType = null,
            string parameterName = null,
            int maxPatterns = 20)
        {
            var query = _parameterPatterns.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(elementType))
            {
                query = query.Where(p =>
                    string.Equals(p.ElementType, elementType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(parameterName))
            {
                query = query.Where(p =>
                    string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(p => p.ModificationCount)
                .Take(maxPatterns)
                .ToList();
        }

        /// <summary>
        /// Gets error patterns - common failure sequences and their frequencies.
        /// </summary>
        public List<ErrorPattern> GetErrorPatterns(int maxPatterns = 20)
        {
            return _errorPatterns.Values
                .OrderByDescending(e => e.OccurrenceCount)
                .Take(maxPatterns)
                .ToList();
        }

        /// <summary>
        /// Gets detected anomalies - unusual behavior that may indicate new patterns.
        /// </summary>
        public List<AnomalyRecord> GetDetectedAnomalies(int maxRecords = 20)
        {
            return _anomalies.Values
                .OrderByDescending(a => a.AnomalyScore)
                .Take(maxRecords)
                .ToList();
        }

        /// <summary>
        /// Gets current observation statistics.
        /// </summary>
        public ObservationStatistics GetStatistics()
        {
            return new ObservationStatistics
            {
                TotalObservations = Interlocked.Read(ref _totalObservations),
                ProcessedObservations = Interlocked.Read(ref _processedObservations),
                PendingObservations = _pendingObservations.Count,
                PlacementPatterns = _placementPatterns.Count,
                ParameterPatterns = _parameterPatterns.Count,
                SpatialPatterns = _spatialPatterns.Count,
                WorkflowPatterns = _workflowPatterns.Count,
                TimePatterns = _timePatterns.Count,
                ErrorPatterns = _errorPatterns.Count,
                ImplicitPreferences = _implicitPreferences.Count,
                AnomaliesDetected = _anomalies.Count,
                TrackedUsers = _userProfiles.Count,
                ExpertUsers = _userProfiles.Values.Count(u => u.ExpertiseScore >= _configuration.ExpertThreshold),
                UptimeMinutes = (DateTime.UtcNow - _startTime).TotalMinutes,
                StartedAt = _startTime
            };
        }

        #endregion

        #region Batch Processing

        private void ProcessBatch()
        {
            var batch = new List<ObservationRecord>();
            int count = 0;

            while (count < _configuration.MaxBatchSize && _pendingObservations.TryDequeue(out var record))
            {
                batch.Add(record);
                count++;
            }

            if (batch.Count == 0) return;

            try
            {
                foreach (var record in batch)
                {
                    ProcessSingleObservation(record);
                    Interlocked.Increment(ref _processedObservations);
                }

                Logger.Trace("Processed batch of {0} observations", batch.Count);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error processing observation batch of {0}", batch.Count);
            }
        }

        private void ProcessSingleObservation(ObservationRecord record)
        {
            switch (record.ObservationType)
            {
                case ObservationType.ElementPlacement:
                    ProcessElementPlacement(record);
                    break;

                case ObservationType.ParameterModification:
                    ProcessParameterModification(record);
                    break;

                case ObservationType.ElementDeletion:
                    ProcessElementDeletion(record);
                    break;

                case ObservationType.ViewChange:
                    ProcessViewChange(record);
                    break;

                case ObservationType.Selection:
                    ProcessSelection(record);
                    break;

                case ObservationType.Command:
                    ProcessCommand(record);
                    break;

                case ObservationType.Undo:
                    ProcessUndo(record);
                    break;

                default:
                    ProcessGenericAction(record);
                    break;
            }

            // Update user profile
            UpdateUserProfile(record);
        }

        private void ProcessElementPlacement(ObservationRecord record)
        {
            if (!record.Context.TryGetValue("ElementType", out var elementTypeObj))
                return;

            var elementType = elementTypeObj?.ToString() ?? "Unknown";
            var contextType = record.Context.TryGetValue("RoomType", out var rt) ? rt?.ToString() : "General";
            var key = $"{elementType}_{contextType}";

            // Update placement pattern
            _placementPatterns.AddOrUpdate(key,
                new ElementPlacementPattern
                {
                    PatternKey = key,
                    ElementType = elementType,
                    ContextType = contextType,
                    ObservationCount = 1,
                    Confidence = 0.5f,
                    CommonParameterValues = ExtractCommonParameters(record.Parameters),
                    CoPlacedElementTypes = ExtractNearbyElements(record.Context),
                    ContributingUsers = new HashSet<string> { record.AnonymizedUserId ?? "anonymous" },
                    FirstObserved = DateTime.UtcNow,
                    LastObserved = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.ObservationCount++;
                    existing.LastObserved = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(record.AnonymizedUserId))
                        existing.ContributingUsers.Add(record.AnonymizedUserId);
                    MergeCommonParameters(existing.CommonParameterValues, record.Parameters);
                    MergeNearbyElements(existing.CoPlacedElementTypes, record.Context);
                    existing.Confidence = CalculatePatternConfidence(existing.ObservationCount,
                        existing.ContributingUsers.Count);
                    return existing;
                });
        }

        private void ProcessParameterModification(ObservationRecord record)
        {
            if (!record.Parameters.TryGetValue("ElementType", out var etObj) ||
                !record.Parameters.TryGetValue("ParameterName", out var pnObj))
                return;

            var elementType = etObj?.ToString() ?? "Unknown";
            var parameterName = pnObj?.ToString() ?? "Unknown";
            var newValue = record.Parameters.TryGetValue("NewValue", out var nv) ? nv : null;
            var key = $"{elementType}_{parameterName}";

            _parameterPatterns.AddOrUpdate(key,
                new ParameterModificationPattern
                {
                    PatternKey = key,
                    ElementType = elementType,
                    ParameterName = parameterName,
                    ModificationCount = 1,
                    Confidence = 0.5f,
                    ValueDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        [newValue?.ToString() ?? "null"] = 1
                    },
                    MostCommonValue = newValue?.ToString(),
                    ContributingUsers = new HashSet<string> { record.AnonymizedUserId ?? "anonymous" },
                    FirstObserved = DateTime.UtcNow,
                    LastObserved = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.ModificationCount++;
                    existing.LastObserved = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(record.AnonymizedUserId))
                        existing.ContributingUsers.Add(record.AnonymizedUserId);

                    var valStr = newValue?.ToString() ?? "null";
                    if (!existing.ValueDistribution.ContainsKey(valStr))
                        existing.ValueDistribution[valStr] = 0;
                    existing.ValueDistribution[valStr]++;

                    existing.MostCommonValue = existing.ValueDistribution
                        .OrderByDescending(kv => kv.Value)
                        .First().Key;

                    existing.Confidence = CalculatePatternConfidence(existing.ModificationCount,
                        existing.ContributingUsers.Count);
                    return existing;
                });

            // Track implicit preferences (parameters NOT modified = acceptance)
            TrackImplicitPreference(elementType, parameterName, record);
        }

        private void ProcessElementDeletion(ObservationRecord record)
        {
            // Deletions can indicate mistakes or dissatisfaction
            var elementType = record.Parameters.TryGetValue("ElementType", out var et) ? et?.ToString() : "Unknown";
            var key = $"DEL_{elementType}";

            // If deletion follows a placement, this might be an error pattern
            if (record.AnonymizedUserId != null &&
                _activeWorkflows.TryGetValue(record.AnonymizedUserId, out var tracker))
            {
                var lastAction = tracker.GetLastAction();
                if (lastAction != null &&
                    lastAction.ActionType == "ElementPlacement" &&
                    (DateTime.UtcNow - lastAction.Timestamp).TotalSeconds < 30)
                {
                    // Quick delete after placement = likely mistake
                    var errorKey = $"QUICK_DEL_{elementType}";
                    _errorPatterns.AddOrUpdate(errorKey,
                        new ErrorPattern
                        {
                            PatternId = errorKey,
                            ActionType = "QuickDeleteAfterPlacement",
                            ErrorMessage = $"Element '{elementType}' deleted within 30s of placement",
                            OccurrenceCount = 1,
                            FirstSeen = DateTime.UtcNow,
                            LastSeen = DateTime.UtcNow,
                            AffectedUsers = new HashSet<string> { record.AnonymizedUserId },
                            PrecedingActions = new List<string> { "ElementPlacement" },
                            Context = record.Context
                        },
                        (_, existing) =>
                        {
                            existing.OccurrenceCount++;
                            existing.LastSeen = DateTime.UtcNow;
                            existing.AffectedUsers.Add(record.AnonymizedUserId);
                            return existing;
                        });
                }
            }
        }

        private void ProcessViewChange(ObservationRecord record)
        {
            // View changes help understand user navigation patterns
            // Tracked via workflow patterns
        }

        private void ProcessSelection(ObservationRecord record)
        {
            // Selections reveal what users inspect, even without modifying
        }

        private void ProcessCommand(ObservationRecord record)
        {
            // Revit commands reveal tool usage patterns
        }

        private void ProcessUndo(ObservationRecord record)
        {
            // Undo operations indicate mistakes or exploratory behavior
            if (record.AnonymizedUserId != null &&
                _activeWorkflows.TryGetValue(record.AnonymizedUserId, out var tracker))
            {
                tracker.RecordUndo();

                // Multiple undos in sequence indicate confusion
                if (tracker.RecentUndoCount > 3)
                {
                    var anomaly = new AnomalyRecord
                    {
                        AnomalyId = $"UNDO_{record.AnonymizedUserId}_{DateTime.UtcNow.Ticks}",
                        AnomalyType = AnomalyType.RepeatedUndo,
                        Description = $"User performed {tracker.RecentUndoCount} undos in rapid succession",
                        AnomalyScore = Math.Min(1.0f, tracker.RecentUndoCount / 10.0f),
                        DetectedAt = DateTime.UtcNow,
                        Context = record.Context
                    };

                    _anomalies.TryAdd(anomaly.AnomalyId, anomaly);
                }
            }
        }

        private void ProcessGenericAction(ObservationRecord record)
        {
            // Any action contributes to workflow pattern tracking
        }

        #endregion

        #region Pattern Tracking Helpers

        private void TrackSpatialPlacement(string elementType, PlacementLocation location,
            Dictionary<string, object> parameters)
        {
            if (location.NearbyElementTypes == null || !location.NearbyElementTypes.Any())
                return;

            foreach (var nearbyType in location.NearbyElementTypes)
            {
                var key = $"SPAT_{elementType}_{nearbyType}_{location.RoomType ?? "General"}";

                _spatialPatterns.AddOrUpdate(key,
                    new SpatialPlacementPattern
                    {
                        PatternKey = key,
                        PrimaryElementType = elementType,
                        RelatedElementType = nearbyType,
                        ContextType = location.RoomType ?? "General",
                        ObservationCount = 1,
                        AverageDistance = 0,
                        Distances = new List<double>(),
                        FirstObserved = DateTime.UtcNow,
                        LastObserved = DateTime.UtcNow
                    },
                    (_, existing) =>
                    {
                        existing.ObservationCount++;
                        existing.LastObserved = DateTime.UtcNow;
                        return existing;
                    });
            }
        }

        private void TrackParameterModification(string elementType, string parameterName,
            object oldValue, object newValue)
        {
            // If the old value is the default and the user changed it, this is an explicit preference
            // If the old value is NOT the default and the user left it, this contributes to implicit pref
        }

        private void TrackImplicitPreference(string elementType, string parameterName,
            ObservationRecord record)
        {
            // When we see a parameter modification, all OTHER parameters that were NOT modified
            // on that element represent implicit acceptance of their current values.
            // We track this over time to identify satisfied defaults.

            var key = $"IMPL_{elementType}_{parameterName}";

            _implicitPreferences.AddOrUpdate(key,
                new ImplicitPreference
                {
                    PreferenceKey = key,
                    ActionType = elementType,
                    ParameterName = parameterName,
                    AcceptedValue = record.Parameters.TryGetValue("NewValue", out var nv)
                        ? nv?.ToString() : null,
                    ObservationCount = 1,
                    AcceptanceCount = 0, // This particular param was modified, not accepted
                    AcceptanceRate = 0f,
                    FirstObserved = DateTime.UtcNow,
                    LastObserved = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.ObservationCount++;
                    existing.LastObserved = DateTime.UtcNow;
                    existing.AcceptanceRate = (float)existing.AcceptanceCount / existing.ObservationCount;
                    return existing;
                });
        }

        private void UpdateWorkflowTracker(ObservationRecord record)
        {
            var userId = record.AnonymizedUserId ?? "anonymous";

            var tracker = _activeWorkflows.GetOrAdd(userId, id => new UserWorkflowTracker
            {
                UserId = id,
                StartTime = DateTime.UtcNow
            });

            tracker.AddAction(new WorkflowAction
            {
                ActionType = record.ActionType,
                Timestamp = record.Timestamp,
                Parameters = record.Parameters
            });

            // Check if a workflow pattern has emerged
            var sequence = tracker.GetCurrentSequence(_configuration.WorkflowWindowMinutes);
            if (sequence.Count >= _configuration.MinWorkflowLength)
            {
                var seqKey = string.Join("|", sequence.Select(a => a.ActionType));
                var key = $"WF_{seqKey.GetHashCode():X8}";

                _workflowPatterns.AddOrUpdate(key,
                    new WorkflowSequencePattern
                    {
                        PatternKey = key,
                        ActionSequence = sequence.Select(a => a.ActionType).ToList(),
                        ObservationCount = 1,
                        Confidence = 0.3f,
                        AverageDurationMinutes = (float)(sequence.Last().Timestamp -
                            sequence.First().Timestamp).TotalMinutes,
                        SuccessRate = 1.0f,
                        ContributingUsers = new HashSet<string> { userId },
                        TypicalContext = record.Context,
                        FirstObserved = DateTime.UtcNow,
                        LastObserved = DateTime.UtcNow
                    },
                    (_, existing) =>
                    {
                        existing.ObservationCount++;
                        existing.LastObserved = DateTime.UtcNow;
                        existing.ContributingUsers.Add(userId);
                        existing.Confidence = CalculatePatternConfidence(
                            existing.ObservationCount, existing.ContributingUsers.Count);
                        return existing;
                    });
            }
        }

        private void RecordTimingData(ObservationRecord record)
        {
            var hourOfDay = record.Timestamp.Hour;
            var dayOfWeek = record.Timestamp.DayOfWeek.ToString();
            var key = $"TIME_{dayOfWeek}_{hourOfDay}";

            _timePatterns.AddOrUpdate(key,
                new TimePattern
                {
                    PatternKey = key,
                    DayOfWeek = dayOfWeek,
                    HourOfDay = hourOfDay,
                    ActionCount = 1,
                    ActionTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        [record.ActionType] = 1
                    },
                    FirstObserved = DateTime.UtcNow,
                    LastObserved = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.ActionCount++;
                    existing.LastObserved = DateTime.UtcNow;
                    if (!existing.ActionTypes.ContainsKey(record.ActionType))
                        existing.ActionTypes[record.ActionType] = 0;
                    existing.ActionTypes[record.ActionType]++;
                    return existing;
                });
        }

        private void CheckForAnomalies(ObservationRecord record)
        {
            // Check for unusual action types
            if (record.AnonymizedUserId != null &&
                _userProfiles.TryGetValue(record.AnonymizedUserId, out var profile))
            {
                // Check if this action type is unusual for this user
                if (profile.ActionFrequency.Any())
                {
                    var totalActions = profile.ActionFrequency.Values.Sum();
                    var thisActionCount = profile.ActionFrequency.TryGetValue(record.ActionType, out var ac) ? ac : 0;
                    var actionRatio = (float)thisActionCount / totalActions;

                    // If this action is very rare for this user, flag as anomaly
                    if (totalActions > 50 && actionRatio < _configuration.AnomalyThreshold)
                    {
                        var anomaly = new AnomalyRecord
                        {
                            AnomalyId = $"ACT_{record.ObservationId}",
                            AnomalyType = AnomalyType.UnusualAction,
                            Description = $"Unusual action '{record.ActionType}' " +
                                          $"(only {actionRatio:P1} of user's actions)",
                            AnomalyScore = 1.0f - actionRatio,
                            DetectedAt = DateTime.UtcNow,
                            Context = record.Context
                        };

                        _anomalies.TryAdd(anomaly.AnomalyId, anomaly);
                    }
                }

                // Check for unusual timing
                var currentHour = record.Timestamp.Hour;
                if (profile.ActiveHours.Any() && !profile.ActiveHours.Contains(currentHour))
                {
                    var anomaly = new AnomalyRecord
                    {
                        AnomalyId = $"TIME_{record.ObservationId}",
                        AnomalyType = AnomalyType.UnusualTiming,
                        Description = $"Action at unusual hour ({currentHour}:00). " +
                                      $"Normal hours: {string.Join(", ", profile.ActiveHours)}",
                        AnomalyScore = 0.5f,
                        DetectedAt = DateTime.UtcNow,
                        Context = record.Context
                    };

                    _anomalies.TryAdd(anomaly.AnomalyId, anomaly);
                }
            }

            // Prune old anomalies
            PruneAnomalies();
        }

        private void UpdateUserProfile(ObservationRecord record)
        {
            var userId = record.AnonymizedUserId ?? "anonymous";

            _userProfiles.AddOrUpdate(userId,
                new UserProfile
                {
                    AnonymizedUserId = userId,
                    TotalActions = 1,
                    ActionFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        [record.ActionType] = 1
                    },
                    SessionCount = 1,
                    ActiveHours = new HashSet<int> { record.Timestamp.Hour },
                    FirstSeen = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    UndoRate = 0f,
                    ErrorRate = 0f,
                    AverageActionSpeedMs = 0,
                    ExpertiseScore = 0.5f
                },
                (_, existing) =>
                {
                    existing.TotalActions++;
                    existing.LastSeen = DateTime.UtcNow;
                    existing.ActiveHours.Add(record.Timestamp.Hour);

                    if (!existing.ActionFrequency.ContainsKey(record.ActionType))
                        existing.ActionFrequency[record.ActionType] = 0;
                    existing.ActionFrequency[record.ActionType]++;

                    // Recompute expertise score
                    existing.ExpertiseScore = CalculateExpertiseScore(existing);

                    return existing;
                });
        }

        #endregion

        #region Pattern Consolidation

        private void ConsolidatePatterns()
        {
            lock (_lockObject)
            {
                Logger.Debug("Consolidating patterns...");

                // Merge similar placement patterns
                ConsolidatePlacementPatterns();

                // Merge similar workflow patterns
                ConsolidateWorkflowPatterns();

                // Recompute implicit preferences
                RecomputeImplicitPreferences();

                // Prune low-quality patterns
                PruneLowQualityPatterns();

                // Prune old workflow trackers
                PruneInactiveWorkflowTrackers();

                Logger.Debug("Consolidation complete. Patterns: placement={0}, param={1}, workflow={2}",
                    _placementPatterns.Count, _parameterPatterns.Count, _workflowPatterns.Count);
            }
        }

        private void ConsolidatePlacementPatterns()
        {
            // Remove patterns with very low observation counts after enough time
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var toRemove = _placementPatterns.Values
                .Where(p => p.ObservationCount < 2 && p.FirstObserved < cutoff)
                .Select(p => p.PatternKey)
                .ToList();

            foreach (var key in toRemove)
            {
                _placementPatterns.TryRemove(key, out _);
            }
        }

        private void ConsolidateWorkflowPatterns()
        {
            // Merge workflow patterns that are subsequences of longer patterns
            var patterns = _workflowPatterns.Values
                .OrderByDescending(w => w.ActionSequence.Count)
                .ToList();

            var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < patterns.Count; i++)
            {
                for (int j = i + 1; j < patterns.Count; j++)
                {
                    if (IsSubsequence(patterns[j].ActionSequence, patterns[i].ActionSequence))
                    {
                        // Shorter pattern is subsequence of longer; absorb its observations
                        patterns[i].ObservationCount += patterns[j].ObservationCount / 2;
                        toRemove.Add(patterns[j].PatternKey);
                    }
                }
            }

            foreach (var key in toRemove)
            {
                _workflowPatterns.TryRemove(key, out _);
            }
        }

        private void RecomputeImplicitPreferences()
        {
            // For each element type, find parameters that are rarely modified
            var modifiedParams = _parameterPatterns.Values
                .GroupBy(p => p.ElementType)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.ParameterName).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // Parameters NOT in the modified set are implicitly accepted
            foreach (var pref in _implicitPreferences.Values)
            {
                if (modifiedParams.TryGetValue(pref.ActionType, out var modified))
                {
                    if (!modified.Contains(pref.ParameterName))
                    {
                        // This parameter is never modified => high acceptance
                        pref.AcceptanceRate = 1.0f;
                    }
                }
            }
        }

        private void PruneLowQualityPatterns()
        {
            int maxPatterns = _configuration.MaxPatternsPerType;

            // Prune placement patterns
            PruneDictionaryToCapacity(_placementPatterns, maxPatterns,
                p => p.ObservationCount * p.Confidence);

            // Prune parameter patterns
            PruneDictionaryToCapacity(_parameterPatterns, maxPatterns,
                p => p.ModificationCount * p.Confidence);

            // Prune workflow patterns
            PruneDictionaryToCapacity(_workflowPatterns, maxPatterns,
                p => p.ObservationCount * p.Confidence);

            // Prune anomalies
            PruneAnomalies();
        }

        private void PruneDictionaryToCapacity<T>(
            ConcurrentDictionary<string, T> dict,
            int maxCapacity,
            Func<T, float> scoreFunc)
        {
            if (dict.Count <= maxCapacity) return;

            var toRemove = dict
                .OrderBy(kv => scoreFunc(kv.Value))
                .Take(dict.Count - maxCapacity)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                dict.TryRemove(key, out _);
            }
        }

        private void PruneAnomalies()
        {
            if (_anomalies.Count <= _configuration.MaxAnomalies) return;

            var toRemove = _anomalies.Values
                .OrderBy(a => a.AnomalyScore)
                .ThenBy(a => a.DetectedAt)
                .Take(_anomalies.Count - _configuration.MaxAnomalies)
                .Select(a => a.AnomalyId)
                .ToList();

            foreach (var key in toRemove)
            {
                _anomalies.TryRemove(key, out _);
            }
        }

        private void PruneInactiveWorkflowTrackers()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-_configuration.WorkflowWindowMinutes * 2);
            var inactive = _activeWorkflows
                .Where(kv => kv.Value.LastActionTime < cutoff)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in inactive)
            {
                _activeWorkflows.TryRemove(key, out _);
            }
        }

        #endregion

        #region Expert Pattern Merging

        private List<ExpertPattern> MergeExpertPatterns(List<ExpertPattern> patterns)
        {
            var merged = new Dictionary<string, ExpertPattern>(StringComparer.OrdinalIgnoreCase);

            foreach (var pattern in patterns)
            {
                // Use a normalized key for merging
                var mergeKey = $"{pattern.PatternType}_{pattern.PatternId}";

                if (merged.TryGetValue(mergeKey, out var existing))
                {
                    existing.ExpertCount++;
                    existing.Confidence = Math.Max(existing.Confidence, pattern.Confidence);
                    existing.ObservationCount += pattern.ObservationCount;
                }
                else
                {
                    merged[mergeKey] = pattern;
                }
            }

            return merged.Values.ToList();
        }

        #endregion

        #region Helper Methods

        private string GenerateObservationId()
        {
            return $"OBS_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        private string AnonymizeUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return "anonymous";
            // Simple anonymization: hash the user ID
            return $"U{userId.GetHashCode():X8}";
        }

        private ObservationType ClassifyObservationType(string actionType)
        {
            if (string.IsNullOrEmpty(actionType)) return ObservationType.Generic;

            var normalized = actionType.ToUpperInvariant();

            if (normalized.Contains("PLACE") || normalized.Contains("CREATE") || normalized.Contains("INSERT"))
                return ObservationType.ElementPlacement;
            if (normalized.Contains("MODIFY") || normalized.Contains("PARAMETER") || normalized.Contains("SET"))
                return ObservationType.ParameterModification;
            if (normalized.Contains("DELETE") || normalized.Contains("REMOVE"))
                return ObservationType.ElementDeletion;
            if (normalized.Contains("VIEW") || normalized.Contains("ZOOM") || normalized.Contains("PAN"))
                return ObservationType.ViewChange;
            if (normalized.Contains("SELECT") || normalized.Contains("PICK"))
                return ObservationType.Selection;
            if (normalized.Contains("UNDO"))
                return ObservationType.Undo;
            if (normalized.Contains("REDO"))
                return ObservationType.Redo;

            return ObservationType.Command;
        }

        private float CalculatePatternConfidence(int observationCount, int userCount)
        {
            // More observations and more users = higher confidence
            float obsScore = Math.Min(1.0f, observationCount / 20.0f);
            float userScore = Math.Min(1.0f, userCount / 5.0f);
            return obsScore * 0.6f + userScore * 0.4f;
        }

        private float CalculateExpertiseScore(UserProfile profile)
        {
            if (profile.TotalActions < 10) return 0.5f;

            // Speed factor: more actions per session = faster
            float speedFactor = Math.Min(1.0f, profile.TotalActions /
                (float)(profile.SessionCount * 50));

            // Accuracy factor: fewer undos and errors = more accurate
            float accuracyFactor = 1.0f - Math.Min(1.0f, profile.UndoRate + profile.ErrorRate);

            // Diversity factor: using more action types = more experienced
            float diversityFactor = Math.Min(1.0f, profile.ActionFrequency.Count / 15.0f);

            // Consistency factor: regular usage = more experienced
            float consistencyFactor = Math.Min(1.0f, profile.SessionCount / 20.0f);

            return speedFactor * 0.25f + accuracyFactor * 0.35f +
                   diversityFactor * 0.2f + consistencyFactor * 0.2f;
        }

        private Dictionary<string, object> ExtractCommonParameters(Dictionary<string, object> parameters)
        {
            var common = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (parameters == null) return common;

            foreach (var kvp in parameters)
            {
                if (kvp.Key != "ElementType" && kvp.Key != "ParameterName")
                {
                    common[kvp.Key] = kvp.Value;
                }
            }
            return common;
        }

        private List<string> ExtractNearbyElements(Dictionary<string, object> context)
        {
            if (context != null && context.TryGetValue("NearbyElements", out var nearby) &&
                nearby is List<string> nearbyList)
            {
                return nearbyList;
            }
            return new List<string>();
        }

        private void MergeCommonParameters(Dictionary<string, object> existing,
            Dictionary<string, object> newParams)
        {
            if (newParams == null) return;
            foreach (var kvp in newParams)
            {
                if (kvp.Key != "ElementType" && kvp.Key != "ParameterName")
                {
                    existing[kvp.Key] = kvp.Value;
                }
            }
        }

        private void MergeNearbyElements(List<string> existing, Dictionary<string, object> context)
        {
            var nearby = ExtractNearbyElements(context);
            foreach (var element in nearby)
            {
                if (!existing.Contains(element))
                    existing.Add(element);
            }
        }

        private List<string> GetRecentActionsForUser(string userId, int count)
        {
            if (_activeWorkflows.TryGetValue(userId, out var tracker))
            {
                return tracker.GetRecentActionTypes(count);
            }
            return new List<string>();
        }

        private bool IsSubsequence(List<string> shorter, List<string> longer)
        {
            if (shorter.Count >= longer.Count) return false;
            var shortStr = string.Join("|", shorter);
            var longStr = string.Join("|", longer);
            return longStr.Contains(shortStr);
        }

        private string GenerateWorkflowName(WorkflowSequencePattern pattern)
        {
            if (pattern.ActionSequence.Count <= 3)
                return string.Join(" -> ", pattern.ActionSequence);

            return $"{pattern.ActionSequence.First()} -> ... -> {pattern.ActionSequence.Last()} " +
                   $"({pattern.ActionSequence.Count} steps)";
        }

        private string GenerateWorkflowDescription(WorkflowSequencePattern pattern)
        {
            return $"Observed {pattern.ObservationCount} times from {pattern.ContributingUsers.Count} users. " +
                   $"Average duration: {pattern.AverageDurationMinutes:F1} min. " +
                   $"Success rate: {pattern.SuccessRate:P0}.";
        }

        private string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private async Task SaveStateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var storagePath = _configuration.StoragePath;
                if (string.IsNullOrEmpty(storagePath))
                {
                    storagePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "StingBIM", "observation_learner_state.json");
                }

                var dir = Path.GetDirectoryName(storagePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var state = new ObservationLearnerState
                {
                    SavedAt = DateTime.UtcNow,
                    TotalObservations = Interlocked.Read(ref _totalObservations),
                    PlacementPatternCount = _placementPatterns.Count,
                    ParameterPatternCount = _parameterPatterns.Count,
                    WorkflowPatternCount = _workflowPatterns.Count,
                    UserProfileCount = _userProfiles.Count
                };

                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(storagePath, json), cancellationToken);

                Logger.Info("Saved ObservationLearner state to {0}", storagePath);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save ObservationLearner state");
            }
        }

        #endregion
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configuration for the ObservationLearner.
    /// </summary>
    public class ObservationLearnerConfiguration
    {
        public int BatchProcessingIntervalMs { get; set; } = 500;
        public int MaxBatchSize { get; set; } = 50;
        public int ConsolidationIntervalMinutes { get; set; } = 30;
        public int MaxPatternsPerType { get; set; } = 1000;
        public int MaxAnomalies { get; set; } = 200;
        public float ExpertThreshold { get; set; } = 0.75f;
        public float ImplicitPreferenceThreshold { get; set; } = 0.8f;
        public float AnomalyThreshold { get; set; } = 0.02f;
        public int MinWorkflowLength { get; set; } = 3;
        public int MinWorkflowObservations { get; set; } = 3;
        public float MinWorkflowConfidence { get; set; } = 0.4f;
        public int WorkflowWindowMinutes { get; set; } = 10;
        public string StoragePath { get; set; }
    }

    #endregion

    #region Observation Types

    /// <summary>
    /// A single observation record, privacy-safe and anonymized.
    /// </summary>
    public class ObservationRecord
    {
        public string ObservationId { get; set; }
        public string AnonymizedUserId { get; set; }
        public string ActionType { get; set; }
        public ObservationType ObservationType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; }
    }

    public enum ObservationType
    {
        ElementPlacement,
        ParameterModification,
        ElementDeletion,
        ViewChange,
        Selection,
        Command,
        Undo,
        Redo,
        Generic
    }

    /// <summary>
    /// Location data for an element placement.
    /// </summary>
    public class PlacementLocation
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Level { get; set; }
        public string RoomType { get; set; }
        public List<string> NearbyElementTypes { get; set; }
    }

    #endregion

    #region Pattern Types

    /// <summary>
    /// Pattern of how elements are placed together.
    /// </summary>
    public class ElementPlacementPattern
    {
        public string PatternKey { get; set; }
        public string ElementType { get; set; }
        public string ContextType { get; set; }
        public int ObservationCount { get; set; }
        public float Confidence { get; set; }
        public Dictionary<string, object> CommonParameterValues { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public List<string> CoPlacedElementTypes { get; set; } = new List<string>();
        public HashSet<string> ContributingUsers { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
    }

    /// <summary>
    /// Pattern of how parameters are modified.
    /// </summary>
    public class ParameterModificationPattern
    {
        public string PatternKey { get; set; }
        public string ElementType { get; set; }
        public string ParameterName { get; set; }
        public int ModificationCount { get; set; }
        public float Confidence { get; set; }
        public Dictionary<string, int> ValueDistribution { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public string MostCommonValue { get; set; }
        public HashSet<string> ContributingUsers { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
    }

    /// <summary>
    /// Pattern of spatial relationships between placed elements.
    /// </summary>
    public class SpatialPlacementPattern
    {
        public string PatternKey { get; set; }
        public string PrimaryElementType { get; set; }
        public string RelatedElementType { get; set; }
        public string ContextType { get; set; }
        public int ObservationCount { get; set; }
        public double AverageDistance { get; set; }
        public List<double> Distances { get; set; } = new List<double>();
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
    }

    /// <summary>
    /// A common workflow sequence pattern.
    /// </summary>
    public class WorkflowSequencePattern
    {
        public string PatternKey { get; set; }
        public List<string> ActionSequence { get; set; } = new List<string>();
        public int ObservationCount { get; set; }
        public float Confidence { get; set; }
        public float AverageDurationMinutes { get; set; }
        public float SuccessRate { get; set; }
        public HashSet<string> ContributingUsers { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, object> TypicalContext { get; set; } = new Dictionary<string, object>();
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
    }

    /// <summary>
    /// Time-based activity patterns.
    /// </summary>
    public class TimePattern
    {
        public string PatternKey { get; set; }
        public string DayOfWeek { get; set; }
        public int HourOfDay { get; set; }
        public int ActionCount { get; set; }
        public Dictionary<string, int> ActionTypes { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
    }

    /// <summary>
    /// Pattern of errors and failures.
    /// </summary>
    public class ErrorPattern
    {
        public string PatternId { get; set; }
        public string ActionType { get; set; }
        public string ErrorMessage { get; set; }
        public int OccurrenceCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public HashSet<string> AffectedUsers { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<string> PrecedingActions { get; set; } = new List<string>();
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// An implicit preference inferred from non-action (acceptance of defaults).
    /// </summary>
    public class ImplicitPreference
    {
        public string PreferenceKey { get; set; }
        public string ActionType { get; set; }
        public string ParameterName { get; set; }
        public string AcceptedValue { get; set; }
        public int ObservationCount { get; set; }
        public int AcceptanceCount { get; set; }
        public float AcceptanceRate { get; set; }
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
    }

    #endregion

    #region Expert Types

    /// <summary>
    /// A pattern identified from expert user behavior.
    /// </summary>
    public class ExpertPattern
    {
        public string PatternId { get; set; }
        public ExpertPatternType PatternType { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public int ObservationCount { get; set; }
        public int ExpertCount { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    public enum ExpertPatternType
    {
        ElementPlacement,
        ParameterValue,
        Workflow,
        SpatialArrangement,
        ToolUsage
    }

    /// <summary>
    /// Anonymized user profile for expertise scoring.
    /// </summary>
    public class UserProfile
    {
        public string AnonymizedUserId { get; set; }
        public int TotalActions { get; set; }
        public Dictionary<string, int> ActionFrequency { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int SessionCount { get; set; }
        public HashSet<int> ActiveHours { get; set; } = new HashSet<int>();
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public float UndoRate { get; set; }
        public float ErrorRate { get; set; }
        public double AverageActionSpeedMs { get; set; }
        public float ExpertiseScore { get; set; }
    }

    #endregion

    #region Workflow Tracking

    /// <summary>
    /// Tracks the current workflow for a specific user session.
    /// </summary>
    public class UserWorkflowTracker
    {
        public string UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastActionTime { get; set; }
        public int RecentUndoCount { get; set; }

        private readonly List<WorkflowAction> _actions = new List<WorkflowAction>();
        private readonly object _lock = new object();
        private DateTime _lastUndoTime = DateTime.MinValue;

        public void AddAction(WorkflowAction action)
        {
            lock (_lock)
            {
                _actions.Add(action);
                LastActionTime = action.Timestamp;

                // Reset undo counter if non-undo action
                if (action.ActionType != "Undo")
                {
                    RecentUndoCount = 0;
                }

                // Bound the list
                while (_actions.Count > 200)
                {
                    _actions.RemoveAt(0);
                }
            }
        }

        public void RecordUndo()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastUndoTime).TotalSeconds < 10)
                {
                    RecentUndoCount++;
                }
                else
                {
                    RecentUndoCount = 1;
                }
                _lastUndoTime = now;
            }
        }

        public WorkflowAction GetLastAction()
        {
            lock (_lock)
            {
                return _actions.LastOrDefault();
            }
        }

        public List<string> GetRecentActionTypes(int count)
        {
            lock (_lock)
            {
                return _actions
                    .TakeLast(count)
                    .Select(a => a.ActionType)
                    .ToList();
            }
        }

        public List<WorkflowAction> GetCurrentSequence(int windowMinutes)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
                return _actions
                    .Where(a => a.Timestamp >= cutoff)
                    .ToList();
            }
        }
    }

    /// <summary>
    /// A single action in a workflow sequence.
    /// </summary>
    public class WorkflowAction
    {
        public string ActionType { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// A workflow template derived from observed patterns.
    /// </summary>
    public class WorkflowTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> ActionSequence { get; set; }
        public float AverageDurationMinutes { get; set; }
        public float SuccessRate { get; set; }
        public int ObservationCount { get; set; }
        public float Confidence { get; set; }
        public int UserCount { get; set; }
        public Dictionary<string, object> TypicalContext { get; set; }
    }

    #endregion

    #region Anomaly Types

    /// <summary>
    /// A detected anomaly in user behavior.
    /// </summary>
    public class AnomalyRecord
    {
        public string AnomalyId { get; set; }
        public AnomalyType AnomalyType { get; set; }
        public string Description { get; set; }
        public float AnomalyScore { get; set; }
        public DateTime DetectedAt { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public enum AnomalyType
    {
        UnusualAction,
        UnusualTiming,
        UnusualParameterValue,
        RepeatedUndo,
        RapidActions,
        UnexpectedSequence
    }

    #endregion

    #region Statistics and State

    /// <summary>
    /// Statistics about the ObservationLearner.
    /// </summary>
    public class ObservationStatistics
    {
        public long TotalObservations { get; set; }
        public long ProcessedObservations { get; set; }
        public int PendingObservations { get; set; }
        public int PlacementPatterns { get; set; }
        public int ParameterPatterns { get; set; }
        public int SpatialPatterns { get; set; }
        public int WorkflowPatterns { get; set; }
        public int TimePatterns { get; set; }
        public int ErrorPatterns { get; set; }
        public int ImplicitPreferences { get; set; }
        public int AnomaliesDetected { get; set; }
        public int TrackedUsers { get; set; }
        public int ExpertUsers { get; set; }
        public double UptimeMinutes { get; set; }
        public DateTime StartedAt { get; set; }
    }

    /// <summary>
    /// Serializable state for persistence.
    /// </summary>
    public class ObservationLearnerState
    {
        public DateTime SavedAt { get; set; }
        public long TotalObservations { get; set; }
        public int PlacementPatternCount { get; set; }
        public int ParameterPatternCount { get; set; }
        public int WorkflowPatternCount { get; set; }
        public int UserProfileCount { get; set; }
    }

    #endregion
}
