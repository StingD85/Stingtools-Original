// StingBIM.AI.Agents.Framework.AgentCoordinator
// Coordinates multiple specialist agents for consensus
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (Coordinator)
// Phase 2: Enhanced with MessageBus, Conflict Resolution, and Collaborative Sessions

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Agents.Framework
{
    #region Message Bus System

    /// <summary>
    /// Publish-subscribe message bus for inter-agent communication.
    /// Enables agents to share information asynchronously.
    /// </summary>
    public class MessageBus
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, List<Subscription>> _subscriptions;
        private readonly ConcurrentQueue<AgentMessage> _messageHistory;
        private readonly int _maxHistorySize;
        private long _messageCounter;

        public MessageBus(int maxHistorySize = 1000)
        {
            _subscriptions = new ConcurrentDictionary<string, List<Subscription>>();
            _messageHistory = new ConcurrentQueue<AgentMessage>();
            _maxHistorySize = maxHistorySize;
            _messageCounter = 0;
        }

        /// <summary>
        /// Publishes a message to all subscribers of the topic.
        /// </summary>
        public async Task PublishAsync(AgentMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(message.Topic)) throw new ArgumentException("Message topic cannot be empty.", nameof(message));

            message.MessageId = Interlocked.Increment(ref _messageCounter);
            message.Timestamp = DateTime.UtcNow;

            // Store in history
            _messageHistory.Enqueue(message);
            while (_messageHistory.Count > _maxHistorySize)
            {
                _messageHistory.TryDequeue(out _);
            }

            Logger.Trace($"Message published: {message.Topic} from {message.SenderId}");

            // Deliver to subscribers
            if (_subscriptions.TryGetValue(message.Topic, out var subscribers))
            {
                var tasks = subscribers
                    .Where(s => s.ReceiverId != message.SenderId) // Don't send to self
                    .Select(s => DeliverSafeAsync(s, message, cancellationToken));
                await Task.WhenAll(tasks);
            }

            // Also deliver to wildcard subscribers
            if (_subscriptions.TryGetValue("*", out var wildcardSubs))
            {
                var tasks = wildcardSubs
                    .Where(s => s.ReceiverId != message.SenderId)
                    .Select(s => DeliverSafeAsync(s, message, cancellationToken));
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Subscribes an agent to a topic.
        /// </summary>
        public void Subscribe(string agentId, string topic, Func<AgentMessage, Task> handler)
        {
            if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));
            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Topic cannot be empty.", nameof(topic));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var subscription = new Subscription
            {
                ReceiverId = agentId,
                Topic = topic,
                Handler = handler
            };

            _subscriptions.AddOrUpdate(
                topic,
                new List<Subscription> { subscription },
                (_, list) => { list.Add(subscription); return list; });

            Logger.Debug($"Agent {agentId} subscribed to topic: {topic}");
        }

        /// <summary>
        /// Unsubscribes an agent from a topic.
        /// </summary>
        public void Unsubscribe(string agentId, string topic)
        {
            if (_subscriptions.TryGetValue(topic, out var subscribers))
            {
                subscribers.RemoveAll(s => s.ReceiverId == agentId);
            }
        }

        /// <summary>
        /// Gets recent messages matching a filter.
        /// </summary>
        public IEnumerable<AgentMessage> GetHistory(Func<AgentMessage, bool> filter = null, int limit = 100)
        {
            var messages = Enumerable.Reverse(_messageHistory.ToArray());
            if (filter != null)
            {
                messages = messages.Where(filter);
            }
            return messages.Take(limit);
        }

        /// <summary>
        /// Broadcasts a message to all agents on a topic.
        /// </summary>
        public async Task BroadcastAsync(string senderId, string topic, object payload,
            MessagePriority priority = MessagePriority.Normal, CancellationToken cancellationToken = default)
        {
            var message = new AgentMessage
            {
                SenderId = senderId,
                Topic = topic,
                Payload = payload,
                Priority = priority,
                MessageType = MessageType.Broadcast
            };
            await PublishAsync(message, cancellationToken);
        }

        /// <summary>
        /// Sends a direct message to a specific agent.
        /// </summary>
        public async Task SendDirectAsync(string senderId, string receiverId, string topic, object payload,
            CancellationToken cancellationToken = default)
        {
            var message = new AgentMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Topic = topic,
                Payload = payload,
                MessageType = MessageType.Direct
            };

            // Deliver directly to the receiver
            if (_subscriptions.TryGetValue(topic, out var subscribers))
            {
                var target = subscribers.FirstOrDefault(s => s.ReceiverId == receiverId);
                if (target != null)
                {
                    await DeliverSafeAsync(target, message, cancellationToken);
                }
            }
        }

        private async Task DeliverSafeAsync(Subscription subscription, AgentMessage message,
            CancellationToken cancellationToken)
        {
            try
            {
                await subscription.Handler(message);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to deliver message to {subscription.ReceiverId}");
            }
        }

        private class Subscription
        {
            public string ReceiverId { get; set; }
            public string Topic { get; set; }
            public Func<AgentMessage, Task> Handler { get; set; }
        }
    }

    /// <summary>
    /// Message passed between agents.
    /// </summary>
    public class AgentMessage
    {
        public long MessageId { get; set; }
        public string SenderId { get; set; }
        public string ReceiverId { get; set; } // Null for broadcast
        public string Topic { get; set; }
        public object Payload { get; set; }
        public DateTime Timestamp { get; set; }
        public MessageType MessageType { get; set; }
        public MessagePriority Priority { get; set; }
        public string CorrelationId { get; set; } // For request-response patterns

        public T GetPayload<T>() => Payload is T typed ? typed : default;
    }

    public enum MessageType
    {
        Broadcast,      // To all subscribers of topic
        Direct,         // To specific agent
        Request,        // Expecting a response
        Response        // Reply to a request
    }

    public enum MessagePriority
    {
        Low,
        Normal,
        High,
        Critical        // Safety-related, always delivered first
    }

    #endregion

    #region Conflict Resolution

    /// <summary>
    /// Resolves conflicts between agent opinions using weighted voting and priority rules.
    /// </summary>
    public class ConflictResolver
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, float> _specialtyWeights;
        private readonly Dictionary<string, HashSet<string>> _specialtyDomains;

        public ConflictResolver()
        {
            // Default weights by specialty
            _specialtyWeights = new Dictionary<string, float>
            {
                ["Safety"] = 2.0f,
                ["Structural"] = 1.8f,
                ["Fire"] = 1.8f,
                ["Accessibility"] = 1.6f,
                ["CodeCompliance"] = 1.5f,
                ["Mechanical"] = 1.3f,
                ["Electrical"] = 1.3f,
                ["Plumbing"] = 1.3f,
                ["Spatial"] = 1.2f,
                ["Acoustic"] = 1.0f,
                ["Aesthetic"] = 0.8f,
                ["Cost"] = 1.0f,
                ["Sustainability"] = 1.0f
            };

            // Which specialties have authority over which domains
            _specialtyDomains = new Dictionary<string, HashSet<string>>
            {
                ["Structural"] = new HashSet<string> { "LoadBearing", "Foundations", "Framing", "Shear" },
                ["Fire"] = new HashSet<string> { "FireRating", "Egress", "Compartmentation", "Detection" },
                ["Mechanical"] = new HashSet<string> { "HVAC", "Ventilation", "Thermal", "Ductwork" },
                ["Electrical"] = new HashSet<string> { "Power", "Lighting", "Communications", "Grounding" },
                ["Plumbing"] = new HashSet<string> { "Water", "Drainage", "Fixtures", "Gas" },
                ["Spatial"] = new HashSet<string> { "Layout", "Circulation", "Adjacency", "Zoning" },
                ["Accessibility"] = new HashSet<string> { "ADA", "WheelchairAccess", "Clearances", "Signage" }
            };
        }

        /// <summary>
        /// Resolves conflicts between opinions, returning the winning resolution.
        /// </summary>
        public ConflictResolution ResolveConflict(
            List<AgentOpinion> opinions,
            string conflictDomain,
            DesignProposal proposal)
        {
            if (opinions == null || !opinions.Any()) throw new ArgumentException("Opinions list cannot be null or empty.", nameof(opinions));

            Logger.Debug($"Resolving conflict in domain: {conflictDomain}");

            var resolution = new ConflictResolution
            {
                Domain = conflictDomain,
                InputOpinions = opinions
            };

            // Step 1: Check for safety overrides (always win)
            var safetyOverride = CheckSafetyOverride(opinions);
            if (safetyOverride != null)
            {
                resolution.Method = ResolutionMethod.SafetyOverride;
                resolution.WinningOpinion = safetyOverride;
                resolution.Confidence = 1.0f;
                resolution.Explanation = "Safety concerns override other considerations";
                return resolution;
            }

            // Step 2: Find domain expert
            var domainExpert = FindDomainExpert(opinions, conflictDomain);
            if (domainExpert != null && domainExpert.Score > 0.8f)
            {
                resolution.Method = ResolutionMethod.DomainExpertise;
                resolution.WinningOpinion = domainExpert;
                resolution.Confidence = domainExpert.Score;
                resolution.Explanation = $"Domain expert ({domainExpert.AgentId}) has high confidence";
                return resolution;
            }

            // Step 3: Weighted voting
            var votingResult = PerformWeightedVoting(opinions, conflictDomain);
            resolution.Method = ResolutionMethod.WeightedVoting;
            resolution.WinningOpinion = votingResult.Winner;
            resolution.VoteBreakdown = votingResult.Breakdown;
            resolution.Confidence = votingResult.Confidence;
            resolution.Explanation = $"Weighted voting: {votingResult.Winner?.AgentId} wins with {votingResult.Confidence:P0} confidence";

            // Step 4: Check if consensus is sufficient
            if (votingResult.Confidence < 0.5f)
            {
                resolution.RequiresHumanReview = true;
                resolution.Explanation += " (Low confidence - human review recommended)";
            }

            return resolution;
        }

        /// <summary>
        /// Resolves multiple conflicts in a design.
        /// </summary>
        public List<ConflictResolution> ResolveAllConflicts(
            List<AgentOpinion> opinions,
            DesignProposal proposal)
        {
            var resolutions = new List<ConflictResolution>();

            // Group issues by domain
            var issuesByDomain = opinions
                .SelectMany(o => o.Issues.Select(i => new { Opinion = o, Issue = i }))
                .GroupBy(x => x.Issue.Domain ?? "General");

            foreach (var domainGroup in issuesByDomain)
            {
                // Find conflicting opinions on this domain
                var relevantOpinions = opinions
                    .Where(o => o.Issues.Any(i => (i.Domain ?? "General") == domainGroup.Key))
                    .ToList();

                if (relevantOpinions.Count > 1)
                {
                    var resolution = ResolveConflict(relevantOpinions, domainGroup.Key, proposal);
                    resolutions.Add(resolution);
                }
            }

            return resolutions;
        }

        private AgentOpinion CheckSafetyOverride(List<AgentOpinion> opinions)
        {
            // Safety agents with critical issues always win
            return opinions
                .Where(o => o.HasCriticalIssues &&
                           (o.Specialty == "Safety" ||
                            o.Specialty == "Fire" ||
                            o.Specialty == "Structural"))
                .OrderByDescending(o => GetSpecialtyWeight(o.Specialty))
                .FirstOrDefault();
        }

        private AgentOpinion FindDomainExpert(List<AgentOpinion> opinions, string domain)
        {
            foreach (var kvp in _specialtyDomains)
            {
                if (kvp.Value.Contains(domain))
                {
                    return opinions.FirstOrDefault(o => o.Specialty == kvp.Key);
                }
            }
            return null;
        }

        private VotingResult PerformWeightedVoting(List<AgentOpinion> opinions, string domain)
        {
            var votes = new Dictionary<string, float>();
            var totalWeight = 0f;

            foreach (var opinion in opinions)
            {
                var weight = GetSpecialtyWeight(opinion.Specialty);

                // Boost weight if this specialty covers the domain
                if (_specialtyDomains.TryGetValue(opinion.Specialty, out var domains) &&
                    domains.Contains(domain))
                {
                    weight *= 1.5f;
                }

                // Weight also by confidence
                weight *= opinion.Score;

                totalWeight += weight;
                votes[opinion.AgentId] = weight;
            }

            var winner = opinions.OrderByDescending(o => votes[o.AgentId]).FirstOrDefault();
            var winnerWeight = winner != null ? votes[winner.AgentId] : 0f;

            return new VotingResult
            {
                Winner = winner,
                Confidence = totalWeight > 0 ? winnerWeight / totalWeight : 0f,
                Breakdown = votes
            };
        }

        private float GetSpecialtyWeight(string specialty)
        {
            return _specialtyWeights.TryGetValue(specialty, out var weight) ? weight : 1.0f;
        }

        private class VotingResult
        {
            public AgentOpinion Winner { get; set; }
            public float Confidence { get; set; }
            public Dictionary<string, float> Breakdown { get; set; }
        }
    }

    /// <summary>
    /// Result of conflict resolution.
    /// </summary>
    public class ConflictResolution
    {
        public string Domain { get; set; }
        public List<AgentOpinion> InputOpinions { get; set; }
        public AgentOpinion WinningOpinion { get; set; }
        public ResolutionMethod Method { get; set; }
        public float Confidence { get; set; }
        public string Explanation { get; set; }
        public bool RequiresHumanReview { get; set; }
        public Dictionary<string, float> VoteBreakdown { get; set; }
    }

    public enum ResolutionMethod
    {
        SafetyOverride,     // Safety concerns always win
        DomainExpertise,    // Domain expert has authority
        WeightedVoting,     // Weighted vote determines winner
        Consensus,          // All agents agree
        HumanDecision       // Escalated to human
    }

    #endregion

    #region Collaborative Sessions

    /// <summary>
    /// Manages a collaborative design session with iterative refinement.
    /// </summary>
    public class CollaborativeSession
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly AgentCoordinator _coordinator;
        private readonly MessageBus _messageBus;
        private readonly string _sessionId;
        private readonly List<DesignIteration> _iterations;
        private readonly ConcurrentDictionary<string, object> _sharedState;

        public string SessionId => _sessionId;
        public int IterationCount => _iterations.Count;
        public DesignProposal CurrentProposal { get; private set; }
        public SessionStatus Status { get; private set; }

        public CollaborativeSession(AgentCoordinator coordinator, MessageBus messageBus, DesignProposal initialProposal)
        {
            _coordinator = coordinator;
            _messageBus = messageBus;
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _iterations = new List<DesignIteration>();
            _sharedState = new ConcurrentDictionary<string, object>();
            CurrentProposal = initialProposal;
            Status = SessionStatus.Active;

            Logger.Info($"Created collaborative session: {_sessionId}");
        }

        /// <summary>
        /// Runs one iteration of collaborative refinement.
        /// </summary>
        public async Task<DesignIteration> IterateAsync(
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Session {_sessionId}: Starting iteration {_iterations.Count + 1}");

            var iteration = new DesignIteration
            {
                IterationNumber = _iterations.Count + 1,
                StartTime = DateTime.UtcNow,
                InputProposal = CurrentProposal
            };

            // Step 1: Get consensus on current proposal
            iteration.ConsensusResult = await _coordinator.GetConsensusAsync(
                CurrentProposal, context, cancellationToken);

            // Step 2: Collect improvement suggestions
            var designContext = new DesignContext
            {
                CurrentProposal = CurrentProposal,
                ConsensusResult = iteration.ConsensusResult,
                SessionState = new SessionState
                {
                    SessionId = Guid.NewGuid().ToString(),
                    Metadata = new Dictionary<string, object>(_sharedState)
                }
            };
            iteration.Suggestions = (await _coordinator.CollectSuggestionsAsync(
                designContext, cancellationToken)).ToList();

            // Step 3: Apply highest-priority improvements
            if (iteration.Suggestions.Any() && !iteration.ConsensusResult.IsApproved)
            {
                var topSuggestions = iteration.Suggestions
                    .OrderByDescending(s => s.Priority)
                    .ThenByDescending(s => s.Confidence * s.Impact)
                    .Take(3)
                    .ToList();

                iteration.AppliedSuggestions = ApplySuggestions(CurrentProposal, topSuggestions);
                iteration.OutputProposal = CurrentProposal; // Modified in place
            }
            else
            {
                iteration.OutputProposal = CurrentProposal;
            }

            iteration.EndTime = DateTime.UtcNow;
            _iterations.Add(iteration);

            // Broadcast iteration results
            await _messageBus.BroadcastAsync(
                "SessionCoordinator",
                "session.iteration.complete",
                iteration,
                MessagePriority.Normal,
                cancellationToken);

            // Check if we should stop
            if (iteration.ConsensusResult.IsApproved)
            {
                Status = SessionStatus.Converged;
                Logger.Info($"Session {_sessionId}: Converged after {_iterations.Count} iterations");
            }
            else if (_iterations.Count >= 10)
            {
                Status = SessionStatus.MaxIterations;
                Logger.Warn($"Session {_sessionId}: Max iterations reached without convergence");
            }

            return iteration;
        }

        /// <summary>
        /// Runs the session until convergence or max iterations.
        /// </summary>
        public async Task<SessionResult> RunToCompletionAsync(
            EvaluationContext context = null,
            int maxIterations = 10,
            CancellationToken cancellationToken = default)
        {
            while (Status == SessionStatus.Active && _iterations.Count < maxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await IterateAsync(context, cancellationToken);
            }

            return new SessionResult
            {
                SessionId = _sessionId,
                FinalProposal = CurrentProposal,
                IterationCount = _iterations.Count,
                FinalConsensus = _iterations.LastOrDefault()?.ConsensusResult,
                Iterations = _iterations,
                Status = Status
            };
        }

        /// <summary>
        /// Sets shared state accessible to all agents.
        /// </summary>
        public void SetSharedState(string key, object value)
        {
            _sharedState[key] = value;
        }

        /// <summary>
        /// Gets shared state.
        /// </summary>
        public T GetSharedState<T>(string key)
        {
            return _sharedState.TryGetValue(key, out var value) && value is T typed ? typed : default;
        }

        private List<AgentSuggestion> ApplySuggestions(DesignProposal proposal, List<AgentSuggestion> suggestions)
        {
            var applied = new List<AgentSuggestion>();

            foreach (var suggestion in suggestions)
            {
                try
                {
                    // Apply the suggestion's modifications to the proposal
                    if (suggestion.Modifications != null)
                    {
                        foreach (var mod in suggestion.Modifications)
                        {
                            proposal.ApplyModification(mod);
                        }
                        applied.Add(suggestion);
                        Logger.Debug($"Applied suggestion: {suggestion.Title}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to apply suggestion: {suggestion.Title}");
                }
            }

            return applied;
        }
    }

    /// <summary>
    /// One iteration of collaborative refinement.
    /// </summary>
    public class DesignIteration
    {
        public int IterationNumber { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DesignProposal InputProposal { get; set; }
        public DesignProposal OutputProposal { get; set; }
        public ConsensusResult ConsensusResult { get; set; }
        public List<AgentSuggestion> Suggestions { get; set; }
        public List<AgentSuggestion> AppliedSuggestions { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Result of a completed collaborative session.
    /// </summary>
    public class SessionResult
    {
        public string SessionId { get; set; }
        public DesignProposal FinalProposal { get; set; }
        public int IterationCount { get; set; }
        public ConsensusResult FinalConsensus { get; set; }
        public List<DesignIteration> Iterations { get; set; }
        public SessionStatus Status { get; set; }
    }

    public enum SessionStatus
    {
        Active,         // Session is running
        Converged,      // Agents reached consensus
        MaxIterations,  // Hit iteration limit
        Cancelled,      // User cancelled
        Error           // Session failed
    }

    #endregion

    /// <summary>
    /// Coordinates multiple specialist agents to reach consensus on design decisions.
    /// Implements the swarm intelligence coordination pattern.
    /// Phase 2: Enhanced with MessageBus, Conflict Resolution, and Collaborative Sessions.
    /// </summary>
    public class AgentCoordinator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<IDesignAgent> _agents;
        private readonly object _lock = new object();
        private readonly MessageBus _messageBus;
        private readonly ConflictResolver _conflictResolver;
        private readonly ConcurrentDictionary<string, CollaborativeSession> _activeSessions;

        // Coordination settings
        public int MaxConsensusRounds { get; set; } = 3;
        public float ConsensusThreshold { get; set; } = 0.7f;
        public float CriticalIssueWeight { get; set; } = 2.0f;
        public TimeSpan AgentTimeout { get; set; } = TimeSpan.FromSeconds(5);

        // Phase 2 properties
        public MessageBus MessageBus => _messageBus;
        public ConflictResolver ConflictResolver => _conflictResolver;
        public IReadOnlyDictionary<string, CollaborativeSession> ActiveSessions => _activeSessions;

        public AgentCoordinator()
        {
            _agents = new List<IDesignAgent>();
            _messageBus = new MessageBus();
            _conflictResolver = new ConflictResolver();
            _activeSessions = new ConcurrentDictionary<string, CollaborativeSession>();
        }

        /// <summary>
        /// Registers an agent with the coordinator.
        /// </summary>
        public void RegisterAgent(IDesignAgent agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));

            lock (_lock)
            {
                if (!_agents.Any(a => a.AgentId == agent.AgentId))
                {
                    _agents.Add(agent);
                    Logger.Info($"Registered agent: {agent.AgentId} ({agent.Specialty})");
                }
            }
        }

        /// <summary>
        /// Unregisters an agent.
        /// </summary>
        public void UnregisterAgent(string agentId)
        {
            lock (_lock)
            {
                _agents.RemoveAll(a => a.AgentId == agentId);
                Logger.Info($"Unregistered agent: {agentId}");
            }
        }

        /// <summary>
        /// Gets all registered agents.
        /// </summary>
        public IReadOnlyList<IDesignAgent> Agents
        {
            get { lock (_lock) { return _agents.ToList(); } }
        }

        /// <summary>
        /// Gets consensus from all agents on a design proposal.
        /// Implements multi-round deliberation for disagreements.
        /// </summary>
        public async Task<ConsensusResult> GetConsensusAsync(
            DesignProposal proposal,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            if (proposal == null) throw new ArgumentNullException(nameof(proposal));

            Logger.Info($"Starting consensus process for proposal: {proposal.ProposalId}");

            var activeAgents = _agents.Where(a => a.IsActive).ToList();
            if (activeAgents.Count == 0)
            {
                return new ConsensusResult
                {
                    Status = ConsensusStatus.NoAgents,
                    Message = "No active agents available for evaluation"
                };
            }

            // Round 1: Initial opinions
            var opinions = await CollectOpinionsAsync(activeAgents, proposal, context, cancellationToken);

            // Check for immediate consensus
            var consensusResult = EvaluateConsensus(opinions);
            if (consensusResult.Status == ConsensusStatus.Consensus)
            {
                Logger.Info("Consensus reached in round 1");
                return consensusResult;
            }

            // Rounds 2+: Share opinions and allow revisions
            for (int round = 2; round <= MaxConsensusRounds; round++)
            {
                Logger.Debug($"Consensus round {round}");

                // Share opinions among agents
                ShareOpinions(activeAgents, opinions);

                // Collect revised opinions
                opinions = await CollectOpinionsAsync(activeAgents, proposal, context, cancellationToken);

                consensusResult = EvaluateConsensus(opinions);
                if (consensusResult.Status == ConsensusStatus.Consensus)
                {
                    Logger.Info($"Consensus reached in round {round}");
                    return consensusResult;
                }
            }

            // No consensus - return majority decision with dissent noted
            Logger.Warn("Consensus not reached, using weighted majority");
            return BuildMajorityResult(opinions);
        }

        /// <summary>
        /// Validates an action against all agents.
        /// Returns combined validation result.
        /// </summary>
        public ValidationResult ValidateAction(DesignAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (string.IsNullOrWhiteSpace(action.ActionType)) throw new ArgumentException("ActionType cannot be empty.", nameof(action));

            var results = new List<ValidationResult>();

            foreach (var agent in _agents.Where(a => a.IsActive))
            {
                try
                {
                    var result = agent.ValidateAction(action);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Agent {agent.AgentId} failed to validate action");
                }
            }

            // Combine results - invalid if any agent says invalid
            var combined = new ValidationResult
            {
                IsValid = results.All(r => r.IsValid),
                Issues = results.SelectMany(r => r.Issues).ToList(),
                Warnings = results.SelectMany(r => r.Warnings).Distinct().ToList()
            };

            return combined;
        }

        /// <summary>
        /// Collects suggestions from all relevant agents.
        /// </summary>
        public async Task<IEnumerable<AgentSuggestion>> CollectSuggestionsAsync(
            DesignContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var suggestions = new List<AgentSuggestion>();
            var tasks = new List<Task<IEnumerable<AgentSuggestion>>>();

            foreach (var agent in _agents.Where(a => a.IsActive))
            {
                tasks.Add(SafeSuggestAsync(agent, context, cancellationToken));
            }

            var results = await Task.WhenAll(tasks);
            suggestions.AddRange(results.SelectMany(r => r));

            // Rank and deduplicate suggestions
            return RankSuggestions(suggestions);
        }

        private async Task<List<AgentOpinion>> CollectOpinionsAsync(
            List<IDesignAgent> agents,
            DesignProposal proposal,
            EvaluationContext context,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task<AgentOpinion>>();

            foreach (var agent in agents)
            {
                tasks.Add(SafeEvaluateAsync(agent, proposal, context, cancellationToken));
            }

            var opinions = await Task.WhenAll(tasks);
            return opinions.Where(o => o != null).ToList();
        }

        private async Task<AgentOpinion> SafeEvaluateAsync(
            IDesignAgent agent,
            DesignProposal proposal,
            EvaluationContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(AgentTimeout);

                return await agent.EvaluateAsync(proposal, context, cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Agent {agent.AgentId} failed to evaluate");
                return null;
            }
        }

        private async Task<IEnumerable<AgentSuggestion>> SafeSuggestAsync(
            IDesignAgent agent,
            DesignContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(AgentTimeout);

                return await agent.SuggestAsync(context, cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Agent {agent.AgentId} failed to suggest");
                return Enumerable.Empty<AgentSuggestion>();
            }
        }

        private void ShareOpinions(List<IDesignAgent> agents, List<AgentOpinion> opinions)
        {
            foreach (var agent in agents)
            {
                foreach (var opinion in opinions.Where(o => o.AgentId != agent.AgentId))
                {
                    agent.ReceiveFeedback(opinion);
                }
            }
        }

        private ConsensusResult EvaluateConsensus(List<AgentOpinion> opinions)
        {
            if (opinions.Count == 0)
            {
                return new ConsensusResult { Status = ConsensusStatus.NoAgents };
            }

            // Calculate weighted average score
            var totalWeight = 0f;
            var weightedScore = 0f;
            var allIssues = new List<DesignIssue>();
            var allStrengths = new List<string>();

            foreach (var opinion in opinions)
            {
                var agent = _agents.FirstOrDefault(a => a.AgentId == opinion.AgentId);
                var weight = agent?.ExpertiseLevel ?? 0.5f;

                // Increase weight for critical issues
                if (opinion.HasCriticalIssues)
                {
                    weight *= CriticalIssueWeight;
                }

                totalWeight += weight;
                weightedScore += opinion.Score * weight;
                allIssues.AddRange(opinion.Issues);
                allStrengths.AddRange(opinion.Strengths);
            }

            var averageScore = weightedScore / totalWeight;

            // Check if scores are close enough for consensus
            var scoreVariance = opinions.Select(o => Math.Pow(o.Score - averageScore, 2)).Average();
            var hasConsensus = scoreVariance < 0.05; // Low variance = consensus

            return new ConsensusResult
            {
                Status = hasConsensus ? ConsensusStatus.Consensus : ConsensusStatus.Disagreement,
                Score = averageScore,
                Opinions = opinions,
                Issues = allIssues.GroupBy(i => i.Code ?? i.Description).Select(g => g.First()).ToList(),
                Strengths = allStrengths.Distinct().ToList(),
                IsApproved = averageScore >= ConsensusThreshold && !opinions.Any(o => o.HasCriticalIssues),
                Message = hasConsensus ? "Agents reached consensus" : "Agents have differing opinions"
            };
        }

        private ConsensusResult BuildMajorityResult(List<AgentOpinion> opinions)
        {
            var consensusResult = EvaluateConsensus(opinions);
            consensusResult.Status = ConsensusStatus.Majority;
            consensusResult.Message = $"Majority decision ({opinions.Count(o => o.IsPositive)}/{opinions.Count} positive)";

            // Note dissenting opinions
            consensusResult.DissentingOpinions = opinions
                .Where(o => Math.Abs(o.Score - consensusResult.Score) > 0.2f)
                .ToList();

            return consensusResult;
        }

        private IEnumerable<AgentSuggestion> RankSuggestions(List<AgentSuggestion> suggestions)
        {
            // Score based on confidence and impact, deduplicate similar suggestions
            return suggestions
                .GroupBy(s => s.Title.ToLowerInvariant())
                .Select(g => g.OrderByDescending(s => s.Confidence * s.Impact).First())
                .OrderByDescending(s => s.Confidence * s.Impact)
                .Take(10);
        }

        #region Pareto Multi-Objective Negotiation (T2-9)

        /// <summary>
        /// Performs multi-objective negotiation between agents.
        /// Each agent proposes solutions scored across multiple objectives (e.g., cost, safety, sustainability).
        /// Returns the Pareto frontier: the set of non-dominated solutions representing optimal trade-offs.
        /// </summary>
        public async Task<ParetoNegotiationResult> NegotiateMultiObjectiveAsync(
            DesignProposal proposal,
            List<string> objectives,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            if (objectives == null || objectives.Count < 2)
                throw new ArgumentException("Multi-objective negotiation requires at least 2 objectives.", nameof(objectives));

            Logger.Info($"Starting Pareto negotiation with {objectives.Count} objectives: {string.Join(", ", objectives)}");

            var activeAgents = _agents.Where(a => a.IsActive).ToList();
            if (activeAgents.Count == 0)
            {
                return new ParetoNegotiationResult
                {
                    Objectives = objectives,
                    Status = NegotiationStatus.NoAgents
                };
            }

            // Collect proposals from all agents — each agent suggests modifications scored per objective
            var agentProposals = new List<ScoredProposal>();

            foreach (var agent in activeAgents)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(AgentTimeout);

                    var designContext = new DesignContext
                    {
                        CurrentProposal = proposal,
                        SessionState = new SessionState
                        {
                            SessionId = Guid.NewGuid().ToString(),
                            Metadata = new Dictionary<string, object> { ["objectives"] = objectives }
                        }
                    };

                    var suggestions = await agent.SuggestAsync(designContext, cts.Token);

                    foreach (var suggestion in suggestions)
                    {
                        var scores = new Dictionary<string, float>();
                        foreach (var objective in objectives)
                        {
                            scores[objective] = ScoreSuggestionForObjective(suggestion, objective, agent.Specialty);
                        }

                        agentProposals.Add(new ScoredProposal
                        {
                            AgentId = agent.AgentId,
                            Specialty = agent.Specialty,
                            Suggestion = suggestion,
                            ObjectiveScores = scores
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Agent {agent.AgentId} failed during negotiation");
                }
            }

            // Compute Pareto frontier
            var frontier = ComputeParetoFrontier(agentProposals, objectives);

            // Select the best compromise solution (closest to ideal point)
            var bestCompromise = SelectBestCompromise(frontier, objectives);

            var result = new ParetoNegotiationResult
            {
                Objectives = objectives,
                AllProposals = agentProposals,
                ParetoFrontier = frontier,
                BestCompromise = bestCompromise,
                Status = frontier.Count > 0 ? NegotiationStatus.FrontierFound : NegotiationStatus.NoFeasibleSolutions,
                DominatedCount = agentProposals.Count - frontier.Count
            };

            Logger.Info($"Pareto negotiation complete: {frontier.Count} frontier solutions from {agentProposals.Count} proposals");
            return result;
        }

        /// <summary>
        /// Computes the Pareto frontier from a set of scored proposals.
        /// A solution is on the frontier if no other solution dominates it
        /// (i.e., is at least as good in all objectives and strictly better in at least one).
        /// </summary>
        private List<ScoredProposal> ComputeParetoFrontier(List<ScoredProposal> proposals, List<string> objectives)
        {
            var frontier = new List<ScoredProposal>();

            foreach (var candidate in proposals)
            {
                bool isDominated = false;

                foreach (var other in proposals)
                {
                    if (ReferenceEquals(candidate, other)) continue;

                    if (Dominates(other, candidate, objectives))
                    {
                        isDominated = true;
                        break;
                    }
                }

                if (!isDominated)
                {
                    frontier.Add(candidate);
                }
            }

            return frontier;
        }

        /// <summary>
        /// Returns true if proposal A Pareto-dominates proposal B:
        /// A is at least as good in ALL objectives and strictly better in at least ONE.
        /// </summary>
        private bool Dominates(ScoredProposal a, ScoredProposal b, List<string> objectives)
        {
            bool atLeastAsBigInAll = true;
            bool strictlyBetterInOne = false;

            foreach (var obj in objectives)
            {
                var scoreA = a.ObjectiveScores.GetValueOrDefault(obj, 0f);
                var scoreB = b.ObjectiveScores.GetValueOrDefault(obj, 0f);

                if (scoreA < scoreB)
                {
                    atLeastAsBigInAll = false;
                    break;
                }

                if (scoreA > scoreB)
                {
                    strictlyBetterInOne = true;
                }
            }

            return atLeastAsBigInAll && strictlyBetterInOne;
        }

        /// <summary>
        /// Selects the best compromise from the Pareto frontier using the ideal point method.
        /// The ideal point has the maximum score in each objective across all frontier solutions.
        /// The best compromise is the frontier solution closest to this ideal point (min Euclidean distance).
        /// </summary>
        private ScoredProposal SelectBestCompromise(List<ScoredProposal> frontier, List<string> objectives)
        {
            if (frontier.Count == 0) return null;
            if (frontier.Count == 1) return frontier[0];

            // Compute ideal point: max score per objective across frontier
            var idealPoint = new Dictionary<string, float>();
            foreach (var obj in objectives)
            {
                idealPoint[obj] = frontier.Max(p => p.ObjectiveScores.GetValueOrDefault(obj, 0f));
            }

            // Find solution closest to ideal point (Euclidean distance)
            ScoredProposal best = null;
            float bestDistance = float.MaxValue;

            foreach (var solution in frontier)
            {
                float distance = 0f;
                foreach (var obj in objectives)
                {
                    var diff = idealPoint[obj] - solution.ObjectiveScores.GetValueOrDefault(obj, 0f);
                    distance += diff * diff;
                }
                distance = (float)Math.Sqrt(distance);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = solution;
                }
            }

            return best;
        }

        /// <summary>
        /// Scores a suggestion for a specific objective based on the agent's specialty alignment.
        /// </summary>
        private float ScoreSuggestionForObjective(AgentSuggestion suggestion, string objective, string agentSpecialty)
        {
            float baseScore = suggestion.Confidence * suggestion.Impact;

            // Objective-specialty alignment bonuses
            var alignments = new Dictionary<string, HashSet<string>>
            {
                ["Safety"] = new HashSet<string> { "Safety", "Fire", "Structural" },
                ["Cost"] = new HashSet<string> { "Cost", "Sustainability" },
                ["Sustainability"] = new HashSet<string> { "Sustainability", "Mechanical" },
                ["Compliance"] = new HashSet<string> { "CodeCompliance", "Safety", "Fire", "Accessibility" },
                ["Performance"] = new HashSet<string> { "Mechanical", "Electrical", "Plumbing" },
                ["Aesthetics"] = new HashSet<string> { "Aesthetic", "Spatial" },
                ["Accessibility"] = new HashSet<string> { "Accessibility", "Spatial" },
                ["Durability"] = new HashSet<string> { "Structural", "Safety" }
            };

            if (alignments.TryGetValue(objective, out var aligned) && aligned.Contains(agentSpecialty))
            {
                baseScore *= 1.3f; // 30% boost for aligned specialty
            }

            // Check suggestion title/description for objective keywords
            var desc = (suggestion.Title + " " + suggestion.Description).ToLowerInvariant();
            if (desc.Contains(objective.ToLowerInvariant()))
            {
                baseScore *= 1.2f; // 20% boost for keyword match
            }

            return Math.Min(1.0f, baseScore);
        }

        #endregion
    }

    /// <summary>
    /// Result of the consensus process.
    /// </summary>
    public class ConsensusResult
    {
        public ConsensusStatus Status { get; set; }
        public float Score { get; set; }
        public bool IsApproved { get; set; }
        public string Message { get; set; }
        public List<AgentOpinion> Opinions { get; set; } = new List<AgentOpinion>();
        public List<AgentOpinion> DissentingOpinions { get; set; } = new List<AgentOpinion>();
        public List<DesignIssue> Issues { get; set; } = new List<DesignIssue>();
        public List<string> Strengths { get; set; } = new List<string>();
    }

    public enum ConsensusStatus
    {
        Consensus,      // All agents agree
        Majority,       // Majority agrees, some dissent
        Disagreement,   // Significant disagreement
        NoAgents        // No agents available
    }

    #region Pareto Negotiation Types (T2-9)

    /// <summary>
    /// Result of multi-objective Pareto negotiation between agents.
    /// </summary>
    public class ParetoNegotiationResult
    {
        public List<string> Objectives { get; set; } = new List<string>();
        public List<ScoredProposal> AllProposals { get; set; } = new List<ScoredProposal>();
        public List<ScoredProposal> ParetoFrontier { get; set; } = new List<ScoredProposal>();
        public ScoredProposal BestCompromise { get; set; }
        public NegotiationStatus Status { get; set; }
        public int DominatedCount { get; set; }
    }

    /// <summary>
    /// A proposal scored across multiple objectives.
    /// </summary>
    public class ScoredProposal
    {
        public string AgentId { get; set; }
        public string Specialty { get; set; }
        public AgentSuggestion Suggestion { get; set; }
        public Dictionary<string, float> ObjectiveScores { get; set; } = new Dictionary<string, float>();

        /// <summary>
        /// Returns the average score across all objectives.
        /// </summary>
        public float AverageScore => ObjectiveScores.Count > 0 ? ObjectiveScores.Values.Average() : 0f;
    }

    public enum NegotiationStatus
    {
        FrontierFound,          // Pareto frontier computed successfully
        NoFeasibleSolutions,    // No valid proposals from agents
        NoAgents                // No agents available
    }

    #endregion
}
