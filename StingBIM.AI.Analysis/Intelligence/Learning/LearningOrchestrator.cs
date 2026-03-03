// StingBIM.AI.Intelligence.Learning.LearningOrchestrator
// Central orchestrator that wires all learning subsystems together:
// FeedbackCollector -> PatternLearner -> ModelUpdater -> Memory storage
// Implements confidence decay, session tracking, background processing, and learning metrics.
// Master Proposal Reference: Part 2.2 Strategy 6 - Intelligence Amplification Phase 1

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Core.Learning;
using StingBIM.AI.Core.Memory;
using StingBIM.AI.Knowledge.Graph;
using LearningPatternType = StingBIM.AI.Intelligence.Learning.PatternType;

namespace StingBIM.AI.Intelligence.Learning
{
    #region Learning Orchestrator

    /// <summary>
    /// The brain that wires all learning systems together. Singleton that owns and initializes
    /// FeedbackCollector, PatternLearner, ModelUpdater, all three Memory types, and KnowledgeGraph.
    /// Implements the full learning cycle: feedback -> pattern analysis -> model updates -> memory storage.
    /// </summary>
    public sealed class LearningOrchestrator : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<LearningOrchestrator> _instance =
            new Lazy<LearningOrchestrator>(() => new LearningOrchestrator());
        public static LearningOrchestrator Instance => _instance.Value;

        private readonly object _lockObject = new object();

        // ---- Core subsystems ----
        private FeedbackCollector _feedbackCollector;
        private PatternLearner _patternLearner;
        private ModelUpdater _modelUpdater;
        private WorkingMemory _workingMemory;
        private SemanticMemory _semanticMemory;
        private EpisodicMemory _episodicMemory;
        private KnowledgeGraph _knowledgeGraph;

        // ---- Background processing ----
        private readonly ConcurrentQueue<LearningWorkItem> _processingQueue;
        private Timer _processingTimer;
        private Timer _maintenanceTimer;
        private Timer _confidenceDecayTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isInitialized;
        private bool _isDisposed;

        // ---- Session tracking ----
        private readonly ConcurrentDictionary<string, ActiveSession> _activeSessions;
        private readonly ConcurrentDictionary<string, ProjectSession> _completedSessions;

        // ---- Metrics ----
        private readonly LearningMetrics _metrics;
        private readonly ConcurrentQueue<LearningCycleResult> _cycleHistory;
        private const int MaxCycleHistorySize = 500;

        // ---- Confidence decay ----
        private const double ConfidenceDecayHalfLifeDays = 90.0;
        private const double DecayLambda = 0.693147 / ConfidenceDecayHalfLifeDays; // ln(2) / half-life
        private const float MinimumConfidenceThreshold = 0.05f;
        private const int MaxPatternsBeforePrune = 10000;
        private const int MaxFactsBeforePrune = 50000;

        // ---- Feedback quality weights ----
        private static readonly Dictionary<UserReaction, double> FeedbackWeights =
            new Dictionary<UserReaction, double>
            {
                { UserReaction.Accepted, 1.0 },
                { UserReaction.Modified, 2.0 },
                { UserReaction.Undone, 3.0 },
                { UserReaction.Rated, 1.0 },
                { UserReaction.Confused, 1.5 },
                { UserReaction.Ignored, 0.5 }
            };

        // ---- Events ----
        /// <summary>
        /// Fires after each complete feedback -> pattern -> update -> memory cycle.
        /// </summary>
        public event EventHandler<LearningCycleCompletedEventArgs> LearningCycleCompleted;

        /// <summary>
        /// Fires when a new pattern is discovered.
        /// </summary>
        public event EventHandler<PatternDiscoveredEventArgs> PatternDiscovered;

        /// <summary>
        /// Fires when the knowledge graph is updated.
        /// </summary>
        public event EventHandler<KnowledgeGraphUpdatedEventArgs> KnowledgeGraphUpdated;

        /// <summary>
        /// Fires when maintenance completes.
        /// </summary>
        public event EventHandler<MaintenanceCompletedEventArgs> MaintenanceCompleted;

        // ---- Properties ----
        public bool IsInitialized => _isInitialized;
        public FeedbackCollector FeedbackCollector => _feedbackCollector;
        public PatternLearner PatternLearner => _patternLearner;
        public ModelUpdater ModelUpdater => _modelUpdater;
        public WorkingMemory WorkingMemory => _workingMemory;
        public SemanticMemory SemanticMemory => _semanticMemory;
        public EpisodicMemory EpisodicMemory => _episodicMemory;
        public KnowledgeGraph KnowledgeGraph => _knowledgeGraph;
        public int PendingWorkItems => _processingQueue.Count;

        private LearningOrchestrator()
        {
            _processingQueue = new ConcurrentQueue<LearningWorkItem>();
            _activeSessions = new ConcurrentDictionary<string, ActiveSession>(StringComparer.OrdinalIgnoreCase);
            _completedSessions = new ConcurrentDictionary<string, ProjectSession>(StringComparer.OrdinalIgnoreCase);
            _metrics = new LearningMetrics();
            _cycleHistory = new ConcurrentQueue<LearningCycleResult>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        #region Initialization

        /// <summary>
        /// Initializes the learning orchestrator with all subsystems.
        /// Must be called before any learning operations.
        /// </summary>
        public async Task InitializeAsync(
            LearningOrchestratorConfig config = null,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (_isInitialized)
            {
                Logger.Warn("LearningOrchestrator already initialized, skipping");
                return;
            }

            lock (_lockObject)
            {
                if (_isInitialized) return;
            }

            config = config ?? new LearningOrchestratorConfig();
            Logger.Info("Initializing LearningOrchestrator...");
            progress?.Report("Initializing learning subsystems...");

            try
            {
                // Initialize core subsystems
                progress?.Report("Creating FeedbackCollector...");
                _feedbackCollector = new FeedbackCollector();

                progress?.Report("Creating PatternLearner...");
                _patternLearner = new PatternLearner();

                progress?.Report("Creating ModelUpdater...");
                _modelUpdater = new ModelUpdater();

                // Initialize memory systems
                progress?.Report("Initializing WorkingMemory...");
                _workingMemory = new WorkingMemory();

                progress?.Report("Initializing SemanticMemory...");
                _semanticMemory = new SemanticMemory();
                await _semanticMemory.LoadAsync();

                progress?.Report("Initializing EpisodicMemory...");
                _episodicMemory = new EpisodicMemory();

                // Initialize knowledge graph
                progress?.Report("Initializing KnowledgeGraph...");
                _knowledgeGraph = new KnowledgeGraph();

                // Wire event handlers
                WireFeedbackPipeline();
                WireModelUpdatePipeline();

                // Start background processing
                StartBackgroundProcessing(config);

                lock (_lockObject)
                {
                    _isInitialized = true;
                }

                _metrics.InitializedAt = DateTime.UtcNow;
                Logger.Info("LearningOrchestrator initialized successfully");
                progress?.Report("Learning orchestrator ready.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize LearningOrchestrator");
                throw;
            }
        }

        /// <summary>
        /// Wires the feedback pipeline: FeedbackReceived -> analyze patterns -> propose model updates -> store in memory.
        /// </summary>
        private void WireFeedbackPipeline()
        {
            _feedbackCollector.FeedbackReceived += OnFeedbackReceived;
            Logger.Debug("Feedback pipeline wired: FeedbackCollector -> PatternAnalysis -> ModelUpdates -> Memory");
        }

        /// <summary>
        /// Wires the model update pipeline: when updates are ready, apply them and update knowledge graph.
        /// </summary>
        private void WireModelUpdatePipeline()
        {
            _modelUpdater.UpdateReady += OnModelUpdateReady;
            Logger.Debug("Model update pipeline wired: ModelUpdater -> KnowledgeGraph");
        }

        /// <summary>
        /// Starts background timers for processing queue, maintenance, and confidence decay.
        /// </summary>
        private void StartBackgroundProcessing(LearningOrchestratorConfig config)
        {
            // Processing queue timer: runs every 500ms to drain the work queue
            _processingTimer = new Timer(
                ProcessQueueCallback,
                null,
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(config.ProcessingIntervalMs));

            // Maintenance timer: runs every hour to prune and optimize
            _maintenanceTimer = new Timer(
                MaintenanceCallback,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(config.MaintenanceIntervalMs));

            // Confidence decay timer: runs every 6 hours
            _confidenceDecayTimer = new Timer(
                ConfidenceDecayCallback,
                null,
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMilliseconds(config.ConfidenceDecayIntervalMs));

            Logger.Info($"Background processing started: queue={config.ProcessingIntervalMs}ms, " +
                        $"maintenance={config.MaintenanceIntervalMs}ms, decay={config.ConfidenceDecayIntervalMs}ms");
        }

        #endregion

        #region Feedback Processing Pipeline

        /// <summary>
        /// Handles incoming feedback from the FeedbackCollector.
        /// Applies quality weighting and enqueues for pattern analysis.
        /// </summary>
        private void OnFeedbackReceived(object sender, FeedbackEntry feedback)
        {
            try
            {
                Logger.Debug($"Feedback received: Action={feedback.Action}, Reaction={feedback.Reaction}");

                // Apply quality weight based on reaction type
                double weight = GetFeedbackWeight(feedback);

                // Create weighted work item
                var workItem = new LearningWorkItem
                {
                    Type = LearningWorkItemType.FeedbackProcessing,
                    FeedbackEntry = feedback,
                    QualityWeight = weight,
                    EnqueuedAt = DateTime.UtcNow,
                    Priority = CalculateFeedbackPriority(feedback)
                };

                _processingQueue.Enqueue(workItem);
                Interlocked.Increment(ref _metrics._totalFeedbackReceived);

                // Store as episodic memory immediately for real-time recall
                StoreAsEpisode(feedback);

                // Update active session if applicable
                UpdateActiveSession(feedback);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error handling feedback: {feedback?.Id}");
            }
        }

        /// <summary>
        /// Computes quality weight for a feedback entry.
        /// Modifications weight 2x, Undos weight 3x, Ratings by score.
        /// </summary>
        private double GetFeedbackWeight(FeedbackEntry feedback)
        {
            double baseWeight = FeedbackWeights.TryGetValue(feedback.Reaction, out var w) ? w : 1.0;

            // For rated feedback, scale by the rating value
            if (feedback.Reaction == UserReaction.Rated && feedback.Rating.HasValue)
            {
                // Rating 1-5 maps to weight 0.5-2.5
                baseWeight = 0.5 + (feedback.Rating.Value / 5.0) * 2.0;
            }

            // Boost weight if the user provided a comment (explicit feedback is more valuable)
            if (!string.IsNullOrWhiteSpace(feedback.Comment))
            {
                baseWeight *= 1.25;
            }

            // Context-based weight adjustments
            if (feedback.Context != null)
            {
                // Repeated corrections on same action type get increasing weight
                if (feedback.Context.TryGetValue("CorrectionCount", out var countObj) &&
                    countObj is int correctionCount && correctionCount > 1)
                {
                    baseWeight *= 1.0 + Math.Min(correctionCount * 0.1, 0.5);
                }
            }

            return baseWeight;
        }

        /// <summary>
        /// Calculates processing priority for a feedback item.
        /// Higher priority items are processed first.
        /// </summary>
        private int CalculateFeedbackPriority(FeedbackEntry feedback)
        {
            int priority = 0;

            // Undos and modifications are high priority - user actively correcting the system
            if (feedback.Reaction == UserReaction.Undone) priority += 30;
            else if (feedback.Reaction == UserReaction.Modified) priority += 20;
            else if (feedback.Reaction == UserReaction.Confused) priority += 15;
            else if (feedback.Reaction == UserReaction.Rated && feedback.Rating.HasValue)
            {
                // Low ratings are higher priority to learn from
                priority += Math.Max(0, 6 - feedback.Rating.Value) * 5;
            }

            // Comments indicate strong user engagement
            if (!string.IsNullOrWhiteSpace(feedback.Comment)) priority += 5;

            return priority;
        }

        /// <summary>
        /// Stores feedback as an episodic memory for future recall.
        /// </summary>
        private void StoreAsEpisode(FeedbackEntry feedback)
        {
            try
            {
                var episode = new Episode
                {
                    Id = $"EP-{feedback.Id}",
                    UserId = feedback.Context?.TryGetValue("UserId", out var uid) == true ? uid?.ToString() : "unknown",
                    ProjectId = feedback.Context?.TryGetValue("ProjectId", out var pid) == true ? pid?.ToString() : "unknown",
                    Action = feedback.Action,
                    Context = $"Reaction: {feedback.Reaction}",
                    Outcome = MapReactionToOutcome(feedback.Reaction),
                    UserCorrection = feedback.ModifiedAction,
                    Parameters = feedback.Context ?? new Dictionary<string, object>(),
                    Importance = (float)GetFeedbackWeight(feedback) / 3.0f
                };

                _episodicMemory.RecordEpisode(episode);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to store feedback as episode");
            }
        }

        /// <summary>
        /// Maps a user reaction to an episode outcome.
        /// </summary>
        private EpisodeOutcome MapReactionToOutcome(UserReaction reaction)
        {
            switch (reaction)
            {
                case UserReaction.Accepted: return EpisodeOutcome.Accepted;
                case UserReaction.Modified: return EpisodeOutcome.Corrected;
                case UserReaction.Undone: return EpisodeOutcome.Undone;
                case UserReaction.Rated: return EpisodeOutcome.Accepted;
                case UserReaction.Confused: return EpisodeOutcome.Failed;
                case UserReaction.Ignored: return EpisodeOutcome.Abandoned;
                default: return EpisodeOutcome.Abandoned;
            }
        }

        /// <summary>
        /// Handles ready model updates by applying them to the knowledge graph and semantic memory.
        /// </summary>
        private void OnModelUpdateReady(object sender, ModelUpdate update)
        {
            try
            {
                Logger.Info($"Model update ready: {update?.GetType().Name}");

                var workItem = new LearningWorkItem
                {
                    Type = LearningWorkItemType.ModelUpdateApplication,
                    ModelUpdate = update,
                    EnqueuedAt = DateTime.UtcNow,
                    Priority = 50 // High priority for model updates
                };

                _processingQueue.Enqueue(workItem);
                Interlocked.Increment(ref _metrics._totalModelUpdatesProposed);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling model update");
            }
        }

        #endregion

        #region Background Processing Queue

        /// <summary>
        /// Timer callback that drains the processing queue.
        /// </summary>
        private async void ProcessQueueCallback(object state)
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            try
            {
                int processedCount = 0;
                const int maxBatchSize = 20;
                var feedbackBatch = new List<FeedbackEntry>();
                var patternBatch = new List<DesignPattern>();

                // Drain up to maxBatchSize items from the queue
                while (processedCount < maxBatchSize &&
                       _processingQueue.TryDequeue(out var workItem))
                {
                    try
                    {
                        switch (workItem.Type)
                        {
                            case LearningWorkItemType.FeedbackProcessing:
                                feedbackBatch.Add(workItem.FeedbackEntry);
                                break;

                            case LearningWorkItemType.PatternAnalysis:
                                await ProcessPatternAnalysisAsync(workItem);
                                break;

                            case LearningWorkItemType.ModelUpdateApplication:
                                await ApplyModelUpdateAsync(workItem);
                                break;

                            case LearningWorkItemType.SessionAnalysis:
                                await ProcessSessionAnalysisAsync(workItem);
                                break;

                            case LearningWorkItemType.KnowledgeGraphUpdate:
                                ProcessKnowledgeGraphUpdate(workItem);
                                break;
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Error processing work item: {workItem.Type}");
                    }
                }

                // Batch process feedback items
                if (feedbackBatch.Count > 0)
                {
                    await ProcessFeedbackBatchAsync(feedbackBatch);
                }

                if (processedCount > 0)
                {
                    Interlocked.Add(ref _metrics._totalItemsProcessed, processedCount);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in processing queue callback");
            }
        }

        /// <summary>
        /// Batch processes a collection of feedback entries through the full learning cycle.
        /// </summary>
        private async Task ProcessFeedbackBatchAsync(List<FeedbackEntry> feedbackBatch)
        {
            var cycleStart = DateTime.UtcNow;

            try
            {
                Logger.Debug($"Processing feedback batch of {feedbackBatch.Count} items");

                // Step 1: Find active sessions for these feedback items and analyze patterns
                var allPatterns = new List<DesignPattern>();
                foreach (var feedback in feedbackBatch)
                {
                    var sessionId = feedback.Context?.TryGetValue("SessionId", out var sid) == true
                        ? sid?.ToString() : null;

                    if (!string.IsNullOrEmpty(sessionId) &&
                        _activeSessions.TryGetValue(sessionId, out var activeSession))
                    {
                        var sessionPatterns = _patternLearner.AnalyzeSession(activeSession.Session);
                        if (sessionPatterns != null)
                        {
                            allPatterns.AddRange(sessionPatterns);
                        }
                    }
                }

                // Step 2: Also include all currently known patterns for model update analysis
                var existingPatterns = _patternLearner.GetAllPatterns();
                if (existingPatterns != null)
                {
                    allPatterns.AddRange(existingPatterns);
                }

                // Deduplicate patterns by key
                var uniquePatterns = allPatterns
                    .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(p => p.Confidence).First())
                    .ToList();

                // Step 3: Propose model updates from patterns and feedback
                // Convert to LearnedPattern for Core ModelUpdater compatibility
                var corePatterns = uniquePatterns.Select(p => new StingBIM.AI.Core.Learning.LearnedPattern
                {
                    Key = p.Key,
                    PatternType = MapToCoreLearningPatternType(p.PatternType),
                    Description = p.Description,
                    Confidence = p.Confidence,
                    Occurrences = p.Occurrences,
                    FirstSeen = p.FirstSeen,
                    LastSeen = p.LastSeen
                });
                var modelUpdates = await _modelUpdater.AnalyzeAndProposeUpdatesAsync(
                    corePatterns, feedbackBatch);

                int updatesApplied = 0;
                if (modelUpdates != null)
                {
                    foreach (var update in modelUpdates)
                    {
                        await ApplyModelUpdateDirectAsync(update);
                        updatesApplied++;
                    }
                }

                // Step 4: Store new patterns as semantic facts
                int factsStored = 0;
                foreach (var pattern in uniquePatterns.Where(p => p.Confidence >= 0.3f))
                {
                    StorePatternAsFact(pattern);
                    factsStored++;
                }

                // Step 5: Predict next likely actions from patterns
                foreach (var feedback in feedbackBatch)
                {
                    var prediction = _patternLearner.PredictNextAction(feedback.Action);
                    if (!string.IsNullOrEmpty(prediction))
                    {
                        _workingMemory.SetCurrentCommand(prediction);
                    }
                }

                // Record cycle result
                var cycleResult = new LearningCycleResult
                {
                    CycleId = Guid.NewGuid().ToString("N"),
                    StartedAt = cycleStart,
                    CompletedAt = DateTime.UtcNow,
                    FeedbackProcessed = feedbackBatch.Count,
                    PatternsDiscovered = uniquePatterns.Count,
                    ModelUpdatesApplied = updatesApplied,
                    FactsStored = factsStored
                };

                EnqueueCycleResult(cycleResult);
                Interlocked.Increment(ref _metrics._totalCyclesCompleted);

                // Fire event
                LearningCycleCompleted?.Invoke(this, new LearningCycleCompletedEventArgs
                {
                    CycleResult = cycleResult
                });

                Logger.Info($"Learning cycle complete: {feedbackBatch.Count} feedback -> " +
                            $"{uniquePatterns.Count} patterns -> {updatesApplied} updates -> {factsStored} facts " +
                            $"({(DateTime.UtcNow - cycleStart).TotalMilliseconds:F0}ms)");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in feedback batch processing");
            }
        }

        /// <summary>
        /// Processes a pattern analysis work item.
        /// </summary>
        private async Task ProcessPatternAnalysisAsync(LearningWorkItem workItem)
        {
            if (workItem.Session == null) return;

            var patterns = _patternLearner.AnalyzeSession(workItem.Session);
            if (patterns == null) return;

            var patternList = patterns.ToList();
            foreach (var pattern in patternList)
            {
                PatternDiscovered?.Invoke(this, new PatternDiscoveredEventArgs
                {
                    Pattern = pattern
                });
            }

            Interlocked.Add(ref _metrics._totalPatternsDiscovered, patternList.Count);
        }

        /// <summary>
        /// Applies a model update work item.
        /// </summary>
        private async Task ApplyModelUpdateAsync(LearningWorkItem workItem)
        {
            if (workItem.ModelUpdate == null) return;
            await ApplyModelUpdateDirectAsync(workItem.ModelUpdate);
        }

        /// <summary>
        /// Directly applies a model update to the knowledge graph and semantic memory.
        /// </summary>
        private async Task ApplyModelUpdateDirectAsync(ModelUpdate update)
        {
            try
            {
                // Model updates can represent new knowledge, corrections, or reinforcements.
                // We store the update metadata as a knowledge node and semantic fact.

                var nodeId = $"UPDATE-{Guid.NewGuid():N}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = $"ModelUpdate_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                    NodeType = "ModelUpdate",
                    Description = $"Model update applied at {DateTime.UtcNow:O}",
                    Properties = new Dictionary<string, object>
                    {
                        ["UpdateType"] = update.GetType().Name,
                        ["AppliedAt"] = DateTime.UtcNow.ToString("O")
                    }
                };

                _knowledgeGraph.AddNode(node);

                Interlocked.Increment(ref _metrics._totalModelUpdatesApplied);

                KnowledgeGraphUpdated?.Invoke(this, new KnowledgeGraphUpdatedEventArgs
                {
                    NodesAdded = 1,
                    EdgesAdded = 0,
                    Source = "ModelUpdate"
                });

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to apply model update");
            }
        }

        /// <summary>
        /// Processes a session analysis work item after a session ends.
        /// </summary>
        private async Task ProcessSessionAnalysisAsync(LearningWorkItem workItem)
        {
            if (workItem.Session == null) return;

            try
            {
                Logger.Info($"Analyzing completed session: {workItem.Session.SessionId}");

                // Analyze patterns from the full session
                var patterns = _patternLearner.AnalyzeSession(workItem.Session);
                var patternList = patterns?.ToList() ?? new List<DesignPattern>();

                // Store session as episodic memory
                var episode = new Episode
                {
                    Id = $"SESSION-{workItem.Session.SessionId}",
                    UserId = workItem.Session.UserId,
                    ProjectId = workItem.Session.ProjectId,
                    Action = "SessionCompleted",
                    Context = $"Actions: {workItem.Session.Actions?.Count ?? 0}, " +
                              $"Duration: {(workItem.Session.EndTime - workItem.Session.StartTime).TotalMinutes:F1}min",
                    Outcome = EpisodeOutcome.Accepted,
                    Parameters = new Dictionary<string, object>
                    {
                        ["ActionCount"] = workItem.Session.Actions?.Count ?? 0,
                        ["PatternsFound"] = patternList.Count,
                        ["Duration"] = (workItem.Session.EndTime - workItem.Session.StartTime).TotalMinutes
                    },
                    Importance = 0.6f
                };

                _episodicMemory.RecordEpisode(episode);

                // Store high-confidence patterns as semantic facts
                foreach (var pattern in patternList.Where(p => p.Confidence >= 0.4f))
                {
                    StorePatternAsFact(pattern);
                }

                Interlocked.Increment(ref _metrics._totalSessionsAnalyzed);
                Logger.Info($"Session analysis complete: {patternList.Count} patterns found");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error analyzing session: {workItem.Session?.SessionId}");
            }
        }

        /// <summary>
        /// Processes a knowledge graph update work item.
        /// </summary>
        private void ProcessKnowledgeGraphUpdate(LearningWorkItem workItem)
        {
            if (workItem.KnowledgeNode != null)
            {
                _knowledgeGraph.AddNode(workItem.KnowledgeNode);
            }

            if (workItem.KnowledgeEdge != null)
            {
                _knowledgeGraph.AddEdge(workItem.KnowledgeEdge);
            }
        }

        /// <summary>
        /// Stores a learned pattern as a semantic fact for text-based retrieval.
        /// </summary>
        private void StorePatternAsFact(DesignPattern pattern)
        {
            try
            {
                var fact = new SemanticFact
                {
                    Id = $"PATTERN-{pattern.Key}",
                    Subject = pattern.Key,
                    Predicate = "hasPattern",
                    Object = pattern.Description ?? pattern.Key,
                    Description = $"Learned pattern ({pattern.PatternType}): {pattern.Description}",
                    Category = "DesignPattern",
                    Source = "LearningOrchestrator",
                    Confidence = pattern.Confidence,
                    Metadata = new Dictionary<string, object>
                    {
                        ["PatternType"] = pattern.PatternType.ToString(),
                        ["Occurrences"] = pattern.Occurrences,
                        ["FirstSeen"] = pattern.FirstSeen.ToString("O"),
                        ["LastSeen"] = pattern.LastSeen.ToString("O")
                    }
                };

                if (!string.IsNullOrEmpty(pattern.Context))
                {
                    fact.Metadata["Context"] = pattern.Context;
                }

                if (pattern.Parameters != null)
                {
                    foreach (var ctx in pattern.Parameters)
                    {
                        fact.Metadata[$"ctx_{ctx.Key}"] = ctx.Value;
                    }
                }

                _semanticMemory.StoreFact(fact);
                Interlocked.Increment(ref _metrics._totalFactsStored);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to store pattern as fact: {pattern?.Key}");
            }
        }

        /// <summary>
        /// Enqueues a cycle result to the history, trimming old entries.
        /// </summary>
        private void EnqueueCycleResult(LearningCycleResult result)
        {
            _cycleHistory.Enqueue(result);
            while (_cycleHistory.Count > MaxCycleHistorySize)
            {
                _cycleHistory.TryDequeue(out _);
            }
        }

        #endregion

        #region Session Tracking

        /// <summary>
        /// Starts tracking a new user session. Auto-creates a ProjectSession from user actions.
        /// </summary>
        public string StartSession(string userId, string projectId)
        {
            EnsureInitialized();

            var sessionId = $"SES-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 32);

            var session = new ProjectSession
            {
                SessionId = sessionId,
                UserId = userId,
                ProjectId = projectId,
                StartTime = DateTime.UtcNow,
                Actions = new List<UserAction>()
            };

            var activeSession = new ActiveSession
            {
                Session = session,
                LastActivityAt = DateTime.UtcNow
            };

            _activeSessions[sessionId] = activeSession;
            Interlocked.Increment(ref _metrics._totalSessionsStarted);

            Logger.Info($"Session started: {sessionId} for user={userId}, project={projectId}");
            return sessionId;
        }

        /// <summary>
        /// Records a user action within an active session.
        /// </summary>
        public void RecordAction(string sessionId, UserAction action)
        {
            EnsureInitialized();

            if (!_activeSessions.TryGetValue(sessionId, out var activeSession))
            {
                Logger.Warn($"Unknown session: {sessionId}");
                return;
            }

            lock (activeSession.LockObject)
            {
                activeSession.Session.Actions.Add(action);
                activeSession.LastActivityAt = DateTime.UtcNow;
            }

            Interlocked.Increment(ref _metrics._totalActionsRecorded);

            // Update working memory with current action
            _workingMemory.SetCurrentCommand(action.ActionType);

            Logger.Debug($"Action recorded in session {sessionId}: {action.ActionType}");
        }

        /// <summary>
        /// Ends a session, triggering full session analysis.
        /// </summary>
        public void EndSession(string sessionId)
        {
            EnsureInitialized();

            if (!_activeSessions.TryRemove(sessionId, out var activeSession))
            {
                Logger.Warn($"Cannot end unknown session: {sessionId}");
                return;
            }

            activeSession.Session.EndTime = DateTime.UtcNow;
            _completedSessions[sessionId] = activeSession.Session;

            // Enqueue for session analysis
            var workItem = new LearningWorkItem
            {
                Type = LearningWorkItemType.SessionAnalysis,
                Session = activeSession.Session,
                EnqueuedAt = DateTime.UtcNow,
                Priority = 40
            };

            _processingQueue.Enqueue(workItem);

            Logger.Info($"Session ended: {sessionId}, " +
                        $"Actions: {activeSession.Session.Actions?.Count ?? 0}, " +
                        $"Duration: {(DateTime.UtcNow - activeSession.Session.StartTime).TotalMinutes:F1}min");
        }

        /// <summary>
        /// Updates the active session associated with a feedback entry.
        /// </summary>
        private void UpdateActiveSession(FeedbackEntry feedback)
        {
            if (feedback.Context == null) return;

            if (feedback.Context.TryGetValue("SessionId", out var sidObj) &&
                sidObj is string sid &&
                _activeSessions.TryGetValue(sid, out var activeSession))
            {
                var action = new UserAction
                {
                    ActionId = feedback.ActionId,
                    ActionType = feedback.Action,
                    Timestamp = feedback.Timestamp,
                    Parameters = feedback.Context,
                    WasUndone = feedback.Reaction == UserReaction.Undone,
                    WasModified = feedback.Reaction == UserReaction.Modified
                };

                lock (activeSession.LockObject)
                {
                    activeSession.Session.Actions.Add(action);
                    activeSession.LastActivityAt = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Gets the number of currently active sessions.
        /// </summary>
        public int ActiveSessionCount => _activeSessions.Count;

        /// <summary>
        /// Gets all active session IDs.
        /// </summary>
        public IReadOnlyList<string> GetActiveSessionIds()
        {
            return _activeSessions.Keys.ToList().AsReadOnly();
        }

        #endregion

        #region Confidence Decay

        /// <summary>
        /// Timer callback for confidence decay processing.
        /// Applies exponential decay with 90-day half-life to all facts and patterns.
        /// </summary>
        private void ConfidenceDecayCallback(object state)
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            try
            {
                Logger.Debug("Running confidence decay cycle...");
                var now = DateTime.UtcNow;
                int decayedFacts = 0;
                int decayedPatterns = 0;

                // Decay semantic facts
                var allFacts = _semanticMemory.Search("*", int.MaxValue);
                if (allFacts != null)
                {
                    foreach (var fact in allFacts)
                    {
                        double daysSinceUpdate = (now - fact.LastUpdated).TotalDays;
                        if (daysSinceUpdate > 1.0)
                        {
                            float decayedConfidence = ApplyExponentialDecay(fact.Confidence, daysSinceUpdate);
                            if (Math.Abs(decayedConfidence - fact.Confidence) > 0.001f)
                            {
                                // Reinforce with the decayed value (negative reinforcement)
                                float delta = decayedConfidence - fact.Confidence;
                                _semanticMemory.ReinforceFact(fact.Id, delta);
                                decayedFacts++;
                            }
                        }
                    }
                }

                // Decay learned patterns
                var allPatterns = _patternLearner.GetAllPatterns();
                if (allPatterns != null)
                {
                    foreach (var pattern in allPatterns)
                    {
                        double daysSinceSeen = (now - pattern.LastSeen).TotalDays;
                        if (daysSinceSeen > 1.0)
                        {
                            pattern.Confidence = ApplyExponentialDecay(pattern.Confidence, daysSinceSeen);
                            decayedPatterns++;
                        }
                    }
                }

                if (decayedFacts > 0 || decayedPatterns > 0)
                {
                    Logger.Info($"Confidence decay applied: {decayedFacts} facts, {decayedPatterns} patterns");
                }

                Interlocked.Increment(ref _metrics._totalDecayCycles);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error in confidence decay cycle");
            }
        }

        /// <summary>
        /// Applies exponential decay formula: C(t) = C0 * e^(-lambda * t)
        /// Half-life of 90 days.
        /// </summary>
        private float ApplyExponentialDecay(float currentConfidence, double daysSinceLastUpdate)
        {
            double decayed = currentConfidence * Math.Exp(-DecayLambda * daysSinceLastUpdate);
            return Math.Max(MinimumConfidenceThreshold, (float)decayed);
        }

        #endregion

        #region Periodic Maintenance

        /// <summary>
        /// Timer callback for periodic maintenance: prune low-confidence facts,
        /// merge duplicate patterns, expire stale sessions, and save state.
        /// </summary>
        private async void MaintenanceCallback(object state)
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            try
            {
                Logger.Info("Running periodic maintenance...");
                var maintenanceStart = DateTime.UtcNow;

                int prunedFacts = PruneLowConfidenceFacts();
                int mergedPatterns = MergeDuplicatePatterns();
                int expiredSessions = ExpireStaleSessions();

                // Save persistent state
                await SaveStateAsync();

                var maintenanceResult = new MaintenanceResult
                {
                    PrunedFacts = prunedFacts,
                    MergedPatterns = mergedPatterns,
                    ExpiredSessions = expiredSessions,
                    Duration = DateTime.UtcNow - maintenanceStart,
                    KnowledgeGraphNodes = _knowledgeGraph.NodeCount,
                    KnowledgeGraphEdges = _knowledgeGraph.EdgeCount,
                    SemanticFactCount = _semanticMemory.Count
                };

                Interlocked.Increment(ref _metrics._totalMaintenanceCycles);

                MaintenanceCompleted?.Invoke(this, new MaintenanceCompletedEventArgs
                {
                    Result = maintenanceResult
                });

                Logger.Info($"Maintenance complete: pruned={prunedFacts} facts, " +
                            $"merged={mergedPatterns} patterns, expired={expiredSessions} sessions " +
                            $"({maintenanceResult.Duration.TotalMilliseconds:F0}ms)");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in maintenance callback");
            }
        }

        /// <summary>
        /// Prunes semantic facts whose confidence has decayed below threshold.
        /// </summary>
        private int PruneLowConfidenceFacts()
        {
            int pruned = 0;

            try
            {
                var facts = _semanticMemory.Search("*", int.MaxValue);
                if (facts == null) return 0;

                var factList = facts.ToList();
                if (factList.Count <= MaxFactsBeforePrune) return 0;

                // Sort by confidence ascending and prune the bottom 10%
                var toPrune = factList
                    .OrderBy(f => f.Confidence)
                    .ThenBy(f => f.ReinforcementCount)
                    .Take(factList.Count / 10)
                    .Where(f => f.Confidence < MinimumConfidenceThreshold * 2)
                    .ToList();

                // We can't directly remove from SemanticMemory, but we can reinforce to zero
                foreach (var fact in toPrune)
                {
                    _semanticMemory.ReinforceFact(fact.Id, -fact.Confidence);
                    pruned++;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error pruning low-confidence facts");
            }

            return pruned;
        }

        /// <summary>
        /// Merges duplicate or near-duplicate patterns.
        /// </summary>
        private int MergeDuplicatePatterns()
        {
            int merged = 0;

            try
            {
                var patterns = _patternLearner.GetAllPatterns()?.ToList();
                if (patterns == null || patterns.Count <= 1) return 0;

                // Group patterns by type and find near-duplicates
                var groups = patterns
                    .GroupBy(p => p.PatternType)
                    .ToList();

                foreach (var group in groups)
                {
                    var patternList = group.ToList();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var pattern in patternList)
                    {
                        // Simple duplicate detection by description similarity
                        var normalizedKey = NormalizePatternKey(pattern);
                        if (seen.Contains(normalizedKey))
                        {
                            // This is a duplicate; reinforce the original by boosting confidence
                            var original = patternList
                                .FirstOrDefault(p => NormalizePatternKey(p) == normalizedKey &&
                                                     p.Key != pattern.Key);

                            if (original != null && original.Confidence < 1.0f)
                            {
                                original.Confidence = Math.Min(1.0f,
                                    original.Confidence + pattern.Confidence * 0.5f);
                                original.Occurrences += pattern.Occurrences;
                                merged++;
                            }
                        }
                        else
                        {
                            seen.Add(normalizedKey);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error merging duplicate patterns");
            }

            return merged;
        }

        /// <summary>
        /// Normalizes a pattern key for deduplication.
        /// </summary>
        private string NormalizePatternKey(DesignPattern pattern)
        {
            var desc = (pattern.Description ?? pattern.Key ?? "").ToLowerInvariant().Trim();
            return $"{pattern.PatternType}:{desc}";
        }

        /// <summary>
        /// Maps a local PatternType to the Core.Learning PatternType for ModelUpdater compatibility.
        /// </summary>
        private static StingBIM.AI.Core.Learning.PatternType MapToCoreLearningPatternType(PatternType localType)
        {
            switch (localType)
            {
                case PatternType.Sequential:
                case PatternType.Sequence:
                    return StingBIM.AI.Core.Learning.PatternType.Sequential;
                case PatternType.Preference:
                    return StingBIM.AI.Core.Learning.PatternType.Preference;
                case PatternType.Correction:
                    return StingBIM.AI.Core.Learning.PatternType.Correction;
                case PatternType.Temporal:
                    return StingBIM.AI.Core.Learning.PatternType.Temporal;
                case PatternType.Contextual:
                case PatternType.CoOccurrence:
                case PatternType.Parameter:
                case PatternType.Spatial:
                    return StingBIM.AI.Core.Learning.PatternType.Contextual;
                default:
                    return StingBIM.AI.Core.Learning.PatternType.Sequential;
            }
        }

        /// <summary>
        /// Expires sessions that have been inactive for too long.
        /// </summary>
        private int ExpireStaleSessions()
        {
            int expired = 0;
            var staleThreshold = TimeSpan.FromHours(2);
            var now = DateTime.UtcNow;

            var staleSessionIds = _activeSessions
                .Where(kvp => (now - kvp.Value.LastActivityAt) > staleThreshold)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in staleSessionIds)
            {
                try
                {
                    EndSession(sessionId);
                    expired++;
                    Logger.Debug($"Expired stale session: {sessionId}");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Error expiring session: {sessionId}");
                }
            }

            return expired;
        }

        /// <summary>
        /// Saves all persistent state to disk.
        /// </summary>
        private async Task SaveStateAsync()
        {
            try
            {
                await _semanticMemory.SaveAsync();
                Logger.Debug("Persistent state saved");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error saving persistent state");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Submits feedback directly (alternative to using FeedbackCollector).
        /// </summary>
        public void SubmitFeedback(FeedbackEntry feedback)
        {
            EnsureInitialized();

            if (feedback == null) throw new ArgumentNullException(nameof(feedback));

            if (string.IsNullOrEmpty(feedback.Id))
            {
                feedback.Id = Guid.NewGuid().ToString("N");
            }

            if (feedback.Timestamp == default)
            {
                feedback.Timestamp = DateTime.UtcNow;
            }

            OnFeedbackReceived(this, feedback);
        }

        /// <summary>
        /// Enqueues a knowledge graph node for background addition.
        /// </summary>
        public void EnqueueKnowledgeNode(KnowledgeNode node)
        {
            EnsureInitialized();

            _processingQueue.Enqueue(new LearningWorkItem
            {
                Type = LearningWorkItemType.KnowledgeGraphUpdate,
                KnowledgeNode = node,
                EnqueuedAt = DateTime.UtcNow,
                Priority = 10
            });
        }

        /// <summary>
        /// Enqueues a knowledge graph edge for background addition.
        /// </summary>
        public void EnqueueKnowledgeEdge(KnowledgeEdge edge)
        {
            EnsureInitialized();

            _processingQueue.Enqueue(new LearningWorkItem
            {
                Type = LearningWorkItemType.KnowledgeGraphUpdate,
                KnowledgeEdge = edge,
                EnqueuedAt = DateTime.UtcNow,
                Priority = 10
            });
        }

        /// <summary>
        /// Queries episodic memory for the success rate of a specific action type.
        /// </summary>
        public float GetActionSuccessRate(string actionType)
        {
            EnsureInitialized();
            return _episodicMemory.GetActionSuccessRate(actionType);
        }

        /// <summary>
        /// Searches semantic memory for facts related to a query.
        /// </summary>
        public IEnumerable<SemanticFact> SearchKnowledge(string query, int maxResults = 10)
        {
            EnsureInitialized();
            return _semanticMemory.Search(query, maxResults);
        }

        /// <summary>
        /// Finds similar past episodes for context-aware suggestions.
        /// </summary>
        public IEnumerable<Episode> FindSimilarEpisodes(string context, string projectId, int maxResults = 5)
        {
            EnsureInitialized();
            return _episodicMemory.FindSimilarEpisodes(context, projectId, maxResults);
        }

        /// <summary>
        /// Gets repeated action patterns within a time window.
        /// </summary>
        public IEnumerable<ActionPattern> GetRepeatedPatterns(TimeSpan window, int minOccurrences = 3)
        {
            EnsureInitialized();
            return _episodicMemory.GetRepeatedPatterns(window, minOccurrences);
        }

        /// <summary>
        /// Predicts the next likely action based on learned patterns.
        /// </summary>
        public string PredictNextAction(string currentAction)
        {
            EnsureInitialized();
            return _patternLearner.PredictNextAction(currentAction);
        }

        /// <summary>
        /// Forces an immediate processing of all queued items.
        /// </summary>
        public async Task FlushProcessingQueueAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            Logger.Debug($"Flushing processing queue ({_processingQueue.Count} items)...");

            while (!_processingQueue.IsEmpty && !cancellationToken.IsCancellationRequested)
            {
                ProcessQueueCallback(null);
                await Task.Delay(10, cancellationToken);
            }

            Logger.Debug("Processing queue flushed");
        }

        /// <summary>
        /// Forces an immediate maintenance cycle.
        /// </summary>
        public async Task RunMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            Logger.Info("Running on-demand maintenance...");
            MaintenanceCallback(null);
            await Task.CompletedTask;
        }

        #endregion

        #region Learning Metrics & Status

        /// <summary>
        /// Returns comprehensive intelligence stats: total facts, patterns, updates, velocity, etc.
        /// </summary>
        public LearningStatus GetLearningStatus()
        {
            EnsureInitialized();

            var recentCycles = _cycleHistory.ToArray();
            var last24h = recentCycles
                .Where(c => c.CompletedAt > DateTime.UtcNow.AddHours(-24))
                .ToList();

            var allPatterns = _patternLearner.GetAllPatterns()?.ToList() ?? new List<DesignPattern>();

            return new LearningStatus
            {
                IsInitialized = _isInitialized,
                InitializedAt = _metrics.InitializedAt,
                UptimeMinutes = _metrics.InitializedAt != default
                    ? (DateTime.UtcNow - _metrics.InitializedAt).TotalMinutes : 0,

                // Knowledge counts
                KnowledgeGraphNodes = _knowledgeGraph.NodeCount,
                KnowledgeGraphEdges = _knowledgeGraph.EdgeCount,
                SemanticFactCount = _semanticMemory.Count,
                DesignPatternCount = allPatterns.Count,

                // Pattern breakdown
                SequentialPatterns = allPatterns.Count(p => p.PatternType == LearningPatternType.Sequential),
                PreferencePatterns = allPatterns.Count(p => p.PatternType == LearningPatternType.Preference),
                CorrectionPatterns = allPatterns.Count(p => p.PatternType == LearningPatternType.Correction),
                TemporalPatterns = allPatterns.Count(p => p.PatternType == LearningPatternType.Temporal),
                ContextualPatterns = allPatterns.Count(p => p.PatternType == LearningPatternType.Contextual),

                // Average confidence
                AveragePatternConfidence = allPatterns.Count > 0
                    ? allPatterns.Average(p => p.Confidence) : 0f,

                // Processing stats
                TotalFeedbackReceived = _metrics._totalFeedbackReceived,
                TotalCyclesCompleted = _metrics._totalCyclesCompleted,
                TotalPatternsDiscovered = _metrics._totalPatternsDiscovered,
                TotalModelUpdatesApplied = _metrics._totalModelUpdatesApplied,
                TotalFactsStored = _metrics._totalFactsStored,
                TotalSessionsAnalyzed = _metrics._totalSessionsAnalyzed,
                TotalActionsRecorded = _metrics._totalActionsRecorded,
                TotalMaintenanceCycles = _metrics._totalMaintenanceCycles,
                TotalDecayCycles = _metrics._totalDecayCycles,

                // Queue status
                PendingWorkItems = _processingQueue.Count,
                ActiveSessions = _activeSessions.Count,

                // Learning velocity (last 24h)
                FeedbackLast24h = last24h.Sum(c => c.FeedbackProcessed),
                PatternsLast24h = last24h.Sum(c => c.PatternsDiscovered),
                UpdatesLast24h = last24h.Sum(c => c.ModelUpdatesApplied),
                CyclesLast24h = last24h.Count,

                // Learning velocity rate
                LearningVelocity = last24h.Count > 0
                    ? (double)last24h.Sum(c => c.PatternsDiscovered) / last24h.Count : 0.0,

                // Top patterns by confidence
                TopPatterns = allPatterns
                    .OrderByDescending(p => p.Confidence)
                    .Take(10)
                    .Select(p => new PatternSummary
                    {
                        Key = p.Key,
                        Type = p.PatternType.ToString(),
                        Description = p.Description,
                        Confidence = p.Confidence,
                        Occurrences = p.Occurrences
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Gets learning metrics as a formatted string for display.
        /// </summary>
        public string GetLearningStatusSummary()
        {
            var status = GetLearningStatus();
            return $"=== StingBIM Learning Intelligence Status ===\n" +
                   $"Uptime: {status.UptimeMinutes:F1} minutes\n" +
                   $"Knowledge Graph: {status.KnowledgeGraphNodes:N0} nodes, {status.KnowledgeGraphEdges:N0} edges\n" +
                   $"Semantic Facts: {status.SemanticFactCount:N0}\n" +
                   $"Learned Patterns: {status.DesignPatternCount:N0} (avg confidence: {status.AveragePatternConfidence:P1})\n" +
                   $"  Sequential: {status.SequentialPatterns}, Preference: {status.PreferencePatterns}, " +
                   $"Correction: {status.CorrectionPatterns}, Temporal: {status.TemporalPatterns}, " +
                   $"Contextual: {status.ContextualPatterns}\n" +
                   $"Feedback Processed: {status.TotalFeedbackReceived:N0}\n" +
                   $"Learning Cycles: {status.TotalCyclesCompleted:N0}\n" +
                   $"Model Updates Applied: {status.TotalModelUpdatesApplied:N0}\n" +
                   $"Sessions Analyzed: {status.TotalSessionsAnalyzed:N0}\n" +
                   $"Active Sessions: {status.ActiveSessions}\n" +
                   $"Pending Work Items: {status.PendingWorkItems}\n" +
                   $"Last 24h: {status.FeedbackLast24h} feedback, {status.PatternsLast24h} patterns, " +
                   $"{status.UpdatesLast24h} updates, velocity={status.LearningVelocity:F2}";
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Shuts down the orchestrator gracefully, saving all state.
        /// </summary>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("Shutting down LearningOrchestrator...");

            _cancellationTokenSource.Cancel();

            // Dispose timers
            _processingTimer?.Dispose();
            _maintenanceTimer?.Dispose();
            _confidenceDecayTimer?.Dispose();

            // End all active sessions
            var activeIds = _activeSessions.Keys.ToList();
            foreach (var sessionId in activeIds)
            {
                try { EndSession(sessionId); } catch { /* best effort */ }
            }

            // Flush remaining queue items
            try
            {
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                flushCts.CancelAfter(TimeSpan.FromSeconds(10));
                // Process remaining items directly
                while (_processingQueue.TryDequeue(out _)) { }
            }
            catch { /* best effort */ }

            // Save state
            await SaveStateAsync();

            lock (_lockObject)
            {
                _isInitialized = false;
            }

            Logger.Info("LearningOrchestrator shut down");
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException(
                    "LearningOrchestrator is not initialized. Call InitializeAsync() first.");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cancellationTokenSource?.Cancel();
            _processingTimer?.Dispose();
            _maintenanceTimer?.Dispose();
            _confidenceDecayTimer?.Dispose();
            _cancellationTokenSource?.Dispose();

            _feedbackCollector.FeedbackReceived -= OnFeedbackReceived;
            _modelUpdater.UpdateReady -= OnModelUpdateReady;
        }

        #endregion
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Configuration for the LearningOrchestrator.
    /// </summary>
    public class LearningOrchestratorConfig
    {
        /// <summary>Processing queue drain interval in milliseconds.</summary>
        public int ProcessingIntervalMs { get; set; } = 500;

        /// <summary>Maintenance cycle interval in milliseconds (default: 1 hour).</summary>
        public int MaintenanceIntervalMs { get; set; } = 3_600_000;

        /// <summary>Confidence decay check interval in milliseconds (default: 6 hours).</summary>
        public int ConfidenceDecayIntervalMs { get; set; } = 21_600_000;

        /// <summary>Directory for persisting learning state.</summary>
        public string PersistenceDirectory { get; set; }

        /// <summary>Maximum number of completed sessions to keep in memory.</summary>
        public int MaxCompletedSessions { get; set; } = 200;
    }

    /// <summary>
    /// Work item for the background processing queue.
    /// </summary>
    internal class LearningWorkItem
    {
        public LearningWorkItemType Type { get; set; }
        public FeedbackEntry FeedbackEntry { get; set; }
        public ModelUpdate ModelUpdate { get; set; }
        public ProjectSession Session { get; set; }
        public KnowledgeNode KnowledgeNode { get; set; }
        public KnowledgeEdge KnowledgeEdge { get; set; }
        public double QualityWeight { get; set; } = 1.0;
        public int Priority { get; set; }
        public DateTime EnqueuedAt { get; set; }
    }

    /// <summary>
    /// Types of work items for the background processing queue.
    /// </summary>
    internal enum LearningWorkItemType
    {
        FeedbackProcessing,
        PatternAnalysis,
        ModelUpdateApplication,
        SessionAnalysis,
        KnowledgeGraphUpdate
    }

    /// <summary>
    /// Tracks an active user session.
    /// </summary>
    internal class ActiveSession
    {
        public ProjectSession Session { get; set; }
        public DateTime LastActivityAt { get; set; }
        public readonly object LockObject = new object();
    }

    // EpisodeOutcome is defined in StingBIM.AI.Core.Memory (EpisodicMemory.cs)
    // with values: Accepted, Corrected, Undone, Failed, Abandoned

    /// <summary>
    /// Result of a single learning cycle.
    /// </summary>
    public class LearningCycleResult
    {
        public string CycleId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public int FeedbackProcessed { get; set; }
        public int PatternsDiscovered { get; set; }
        public int ModelUpdatesApplied { get; set; }
        public int FactsStored { get; set; }
        public TimeSpan Duration => CompletedAt - StartedAt;
    }

    /// <summary>
    /// Comprehensive learning status snapshot.
    /// </summary>
    public class LearningStatus
    {
        // State
        public bool IsInitialized { get; set; }
        public DateTime InitializedAt { get; set; }
        public double UptimeMinutes { get; set; }

        // Knowledge counts
        public int KnowledgeGraphNodes { get; set; }
        public int KnowledgeGraphEdges { get; set; }
        public int SemanticFactCount { get; set; }
        public int DesignPatternCount { get; set; }

        // Pattern breakdown
        public int SequentialPatterns { get; set; }
        public int PreferencePatterns { get; set; }
        public int CorrectionPatterns { get; set; }
        public int TemporalPatterns { get; set; }
        public int ContextualPatterns { get; set; }
        public float AveragePatternConfidence { get; set; }

        // Processing totals
        public long TotalFeedbackReceived { get; set; }
        public long TotalCyclesCompleted { get; set; }
        public long TotalPatternsDiscovered { get; set; }
        public long TotalModelUpdatesApplied { get; set; }
        public long TotalFactsStored { get; set; }
        public long TotalSessionsAnalyzed { get; set; }
        public long TotalActionsRecorded { get; set; }
        public long TotalMaintenanceCycles { get; set; }
        public long TotalDecayCycles { get; set; }

        // Queue status
        public int PendingWorkItems { get; set; }
        public int ActiveSessions { get; set; }

        // Last 24h metrics
        public int FeedbackLast24h { get; set; }
        public int PatternsLast24h { get; set; }
        public int UpdatesLast24h { get; set; }
        public int CyclesLast24h { get; set; }
        public double LearningVelocity { get; set; }

        // Top patterns
        public List<PatternSummary> TopPatterns { get; set; } = new List<PatternSummary>();
    }

    /// <summary>
    /// Summary of a pattern for status reporting.
    /// </summary>
    public class PatternSummary
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public int Occurrences { get; set; }
    }

    /// <summary>
    /// Internal metrics tracker with volatile counters.
    /// </summary>
    internal class LearningMetrics
    {
        public DateTime InitializedAt;
        public long _totalFeedbackReceived;
        public long _totalCyclesCompleted;
        public long _totalPatternsDiscovered;
        public long _totalModelUpdatesProposed;
        public long _totalModelUpdatesApplied;
        public long _totalFactsStored;
        public long _totalSessionsStarted;
        public long _totalSessionsAnalyzed;
        public long _totalActionsRecorded;
        public long _totalItemsProcessed;
        public long _totalMaintenanceCycles;
        public long _totalDecayCycles;
    }

    /// <summary>
    /// Result of a maintenance cycle.
    /// </summary>
    public class MaintenanceResult
    {
        public int PrunedFacts { get; set; }
        public int MergedPatterns { get; set; }
        public int ExpiredSessions { get; set; }
        public TimeSpan Duration { get; set; }
        public int KnowledgeGraphNodes { get; set; }
        public int KnowledgeGraphEdges { get; set; }
        public int SemanticFactCount { get; set; }
    }

    #region Event Args

    public class LearningCycleCompletedEventArgs : EventArgs
    {
        public LearningCycleResult CycleResult { get; set; }
    }

    public class PatternDiscoveredEventArgs : EventArgs
    {
        public DesignPattern Pattern { get; set; }
    }

    public class KnowledgeGraphUpdatedEventArgs : EventArgs
    {
        public int NodesAdded { get; set; }
        public int EdgesAdded { get; set; }
        public string Source { get; set; }
    }

    public class MaintenanceCompletedEventArgs : EventArgs
    {
        public MaintenanceResult Result { get; set; }
    }

    #endregion

    #endregion
}
