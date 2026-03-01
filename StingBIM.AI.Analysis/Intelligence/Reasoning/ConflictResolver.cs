// StingBIM.AI.Intelligence.Reasoning.ConflictResolver
// Detects and resolves contradictions in knowledge from multiple sources
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Amplification (Deepen Reasoning)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Intelligence.Reasoning
{
    #region Conflict Resolver Engine

    /// <summary>
    /// Detects and resolves contradictions in the knowledge base.
    /// Scans knowledge graph for contradictory edges/facts, classifies conflicts
    /// by type, and applies resolution strategies from priority-based to escalation.
    /// Maintains a complete conflict audit log.
    /// </summary>
    public class ConflictResolver
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly ConcurrentDictionary<string, KnowledgeConflict> _activeConflicts;
        private readonly ConcurrentDictionary<string, KnowledgeConflict> _resolvedConflicts;
        private readonly ConcurrentDictionary<string, ConflictResolutionRecord> _resolutionLog;
        private readonly List<AuthorityLevel> _authorityHierarchy;
        private readonly List<ConflictDetectionRule> _detectionRules;
        private readonly ConflictResolverConfiguration _configuration;

        private int _totalConflictsDetected;
        private int _totalConflictsResolved;
        private int _totalConflictsEscalated;
        private DateTime _lastScanTime;

        /// <summary>
        /// Initializes the conflict resolver with default configuration.
        /// </summary>
        public ConflictResolver()
            : this(new ConflictResolverConfiguration())
        {
        }

        /// <summary>
        /// Initializes the conflict resolver with custom configuration.
        /// </summary>
        public ConflictResolver(ConflictResolverConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _activeConflicts = new ConcurrentDictionary<string, KnowledgeConflict>(StringComparer.OrdinalIgnoreCase);
            _resolvedConflicts = new ConcurrentDictionary<string, KnowledgeConflict>(StringComparer.OrdinalIgnoreCase);
            _resolutionLog = new ConcurrentDictionary<string, ConflictResolutionRecord>(StringComparer.OrdinalIgnoreCase);
            _authorityHierarchy = new List<AuthorityLevel>();
            _detectionRules = new List<ConflictDetectionRule>();

            _totalConflictsDetected = 0;
            _totalConflictsResolved = 0;
            _totalConflictsEscalated = 0;
            _lastScanTime = DateTime.MinValue;

            InitializeAuthorityHierarchy();
            InitializeDetectionRules();

            Logger.Info("ConflictResolver initialized with {0} authority levels and {1} detection rules",
                _authorityHierarchy.Count, _detectionRules.Count);
        }

        #region Public Methods - Detection

        /// <summary>
        /// Scans the full knowledge base for conflicts. Accepts knowledge facts and rules
        /// to compare against each other, returning all detected conflicts.
        /// </summary>
        public async Task<ConflictScanResult> DetectConflictsAsync(
            IEnumerable<KnowledgeFact> facts,
            IEnumerable<KnowledgeRuleEntry> rules = null,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));

            var result = new ConflictScanResult
            {
                ScanStartTime = DateTime.UtcNow,
                NewConflicts = new List<KnowledgeConflict>(),
                ResolvedConflicts = new List<KnowledgeConflict>(),
                EscalatedConflicts = new List<KnowledgeConflict>()
            };

            var factList = facts.ToList();
            var ruleList = rules?.ToList() ?? new List<KnowledgeRuleEntry>();

            progress?.Report($"Scanning {factList.Count} facts and {ruleList.Count} rules for conflicts...");
            Logger.Info("Starting conflict scan: {0} facts, {1} rules", factList.Count, ruleList.Count);

            try
            {
                // Phase 1: Detect fact vs fact conflicts
                progress?.Report("Phase 1/5: Checking for fact contradictions...");
                var factConflicts = DetectFactVsFactConflicts(factList);
                result.NewConflicts.AddRange(factConflicts);

                cancellationToken.ThrowIfCancellationRequested();

                // Phase 2: Detect code vs code conflicts
                progress?.Report("Phase 2/5: Checking for building code conflicts...");
                var codeConflicts = DetectCodeVsCodeConflicts(factList);
                result.NewConflicts.AddRange(codeConflicts);

                cancellationToken.ThrowIfCancellationRequested();

                // Phase 3: Detect code vs design conflicts
                progress?.Report("Phase 3/5: Checking design vs code compliance...");
                var codeDesignConflicts = DetectCodeVsDesignConflicts(factList);
                result.NewConflicts.AddRange(codeDesignConflicts);

                cancellationToken.ThrowIfCancellationRequested();

                // Phase 4: Detect rule vs rule conflicts
                progress?.Report("Phase 4/5: Checking for rule contradictions...");
                var ruleConflicts = DetectRuleVsRuleConflicts(ruleList);
                result.NewConflicts.AddRange(ruleConflicts);

                cancellationToken.ThrowIfCancellationRequested();

                // Phase 5: Detect user vs system conflicts
                progress?.Report("Phase 5/5: Checking user preferences vs system requirements...");
                var userConflicts = DetectUserVsSystemConflicts(factList);
                result.NewConflicts.AddRange(userConflicts);

                // Register all detected conflicts
                foreach (var conflict in result.NewConflicts)
                {
                    if (_activeConflicts.TryAdd(conflict.ConflictId, conflict))
                    {
                        _totalConflictsDetected++;
                    }
                }

                // Auto-resolve where possible
                if (_configuration.EnableAutoResolution)
                {
                    progress?.Report("Attempting auto-resolution of detected conflicts...");
                    foreach (var conflict in result.NewConflicts.Where(c => c.Status == ConflictStatus.Detected))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var autoResolved = TryAutoResolve(conflict);
                        if (autoResolved)
                        {
                            result.ResolvedConflicts.Add(conflict);
                        }
                        else if (conflict.Severity >= ConflictSeverity.High)
                        {
                            conflict.Status = ConflictStatus.Escalated;
                            result.EscalatedConflicts.Add(conflict);
                            _totalConflictsEscalated++;
                        }
                    }
                }

                result.ScanEndTime = DateTime.UtcNow;
                result.TotalFactsScanned = factList.Count;
                result.TotalRulesScanned = ruleList.Count;
                result.TotalConflictsDetected = result.NewConflicts.Count;
                result.TotalAutoResolved = result.ResolvedConflicts.Count;
                result.TotalEscalated = result.EscalatedConflicts.Count;
                result.TotalActiveConflicts = _activeConflicts.Count;

                _lastScanTime = DateTime.UtcNow;

                Logger.Info("Conflict scan complete: {0} detected, {1} auto-resolved, {2} escalated, {3} active",
                    result.TotalConflictsDetected, result.TotalAutoResolved,
                    result.TotalEscalated, result.TotalActiveConflicts);

                progress?.Report($"Scan complete: {result.TotalConflictsDetected} conflicts found, " +
                    $"{result.TotalAutoResolved} auto-resolved");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Conflict scan was cancelled");
                result.WasCancelled = true;
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during conflict scan");
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Synchronous wrapper for DetectConflicts.
        /// </summary>
        public ConflictScanResult DetectConflicts(IEnumerable<KnowledgeFact> facts,
            IEnumerable<KnowledgeRuleEntry> rules = null)
        {
            return DetectConflictsAsync(facts, rules).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Checks a single new fact against existing knowledge for conflicts.
        /// </summary>
        public List<KnowledgeConflict> CheckForConflicts(KnowledgeFact newFact,
            IEnumerable<KnowledgeFact> existingFacts)
        {
            if (newFact == null) throw new ArgumentNullException(nameof(newFact));
            var conflicts = new List<KnowledgeConflict>();

            foreach (var existing in existingFacts ?? Enumerable.Empty<KnowledgeFact>())
            {
                if (AreFactsContradictory(newFact, existing))
                {
                    var conflict = CreateConflict(newFact, existing);
                    conflicts.Add(conflict);
                    _activeConflicts.TryAdd(conflict.ConflictId, conflict);
                    _totalConflictsDetected++;
                }
            }

            return conflicts;
        }

        #endregion

        #region Public Methods - Resolution

        /// <summary>
        /// Resolves a conflict using the specified strategy.
        /// </summary>
        public async Task<ConflictResolutionRecord> ResolveConflictAsync(
            string conflictId,
            ResolutionStrategy strategy,
            string justification = null,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (string.IsNullOrWhiteSpace(conflictId))
                throw new ArgumentNullException(nameof(conflictId));

            if (!_activeConflicts.TryGetValue(conflictId, out var conflict))
            {
                Logger.Warn("Conflict not found: {0}", conflictId);
                return new ConflictResolutionRecord
                {
                    ConflictId = conflictId,
                    Success = false,
                    Reason = $"Conflict {conflictId} not found in active conflicts"
                };
            }

            progress?.Report($"Resolving conflict {conflictId} using {strategy} strategy...");

            var record = new ConflictResolutionRecord
            {
                ConflictId = conflictId,
                Strategy = strategy,
                ResolvedAt = DateTime.UtcNow,
                ConflictType = conflict.ConflictType,
                Severity = conflict.Severity
            };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (strategy)
                {
                    case ResolutionStrategy.PriorityBased:
                        record = ResolvePriorityBased(conflict, record);
                        break;

                    case ResolutionStrategy.RecencyBased:
                        record = ResolveRecencyBased(conflict, record);
                        break;

                    case ResolutionStrategy.ConfidenceBased:
                        record = ResolveConfidenceBased(conflict, record);
                        break;

                    case ResolutionStrategy.Negotiation:
                        record = ResolveNegotiation(conflict, record);
                        break;

                    case ResolutionStrategy.Escalation:
                        record = ResolveEscalation(conflict, record);
                        break;

                    default:
                        record.Success = false;
                        record.Reason = $"Unknown resolution strategy: {strategy}";
                        break;
                }

                if (!string.IsNullOrEmpty(justification))
                {
                    record.Justification = justification;
                }

                // Move conflict from active to resolved
                if (record.Success)
                {
                    conflict.Status = ConflictStatus.Resolved;
                    conflict.ResolvedAt = DateTime.UtcNow;
                    conflict.ResolutionStrategy = strategy;
                    conflict.ResolutionDetails = record.Reason;

                    _activeConflicts.TryRemove(conflictId, out _);
                    _resolvedConflicts.TryAdd(conflictId, conflict);
                    _totalConflictsResolved++;

                    Logger.Info("Conflict resolved: {0} using {1} strategy - {2}",
                        conflictId, strategy, record.Reason);
                }
                else if (strategy == ResolutionStrategy.Escalation)
                {
                    conflict.Status = ConflictStatus.Escalated;
                    _totalConflictsEscalated++;

                    Logger.Info("Conflict escalated: {0} - {1}", conflictId, record.Reason);
                }

                // Record in resolution log
                _resolutionLog.TryAdd($"{conflictId}_{DateTime.UtcNow.Ticks}", record);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error resolving conflict {0}", conflictId);
                record.Success = false;
                record.Reason = $"Resolution error: {ex.Message}";
            }

            return record;
        }

        /// <summary>
        /// Synchronous wrapper for ResolveConflict.
        /// </summary>
        public ConflictResolutionRecord ResolveConflict(string conflictId,
            ResolutionStrategy strategy, string justification = null)
        {
            return ResolveConflictAsync(conflictId, strategy, justification).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attempts to automatically resolve a conflict using the best available strategy.
        /// </summary>
        public bool TryAutoResolve(KnowledgeConflict conflict)
        {
            if (conflict == null) return false;

            // Try priority-based first (most reliable)
            var priorityResult = ResolvePriorityBased(conflict, new ConflictResolutionRecord
            {
                ConflictId = conflict.ConflictId,
                Strategy = ResolutionStrategy.PriorityBased,
                ResolvedAt = DateTime.UtcNow,
                ConflictType = conflict.ConflictType,
                Severity = conflict.Severity
            });

            if (priorityResult.Success)
            {
                conflict.Status = ConflictStatus.Resolved;
                conflict.ResolvedAt = DateTime.UtcNow;
                conflict.ResolutionStrategy = ResolutionStrategy.PriorityBased;
                conflict.ResolutionDetails = priorityResult.Reason;

                _activeConflicts.TryRemove(conflict.ConflictId, out _);
                _resolvedConflicts.TryAdd(conflict.ConflictId, conflict);
                _resolutionLog.TryAdd($"{conflict.ConflictId}_{DateTime.UtcNow.Ticks}", priorityResult);
                _totalConflictsResolved++;

                Logger.Debug("Auto-resolved conflict {0} via priority", conflict.ConflictId);
                return true;
            }

            // Try confidence-based
            var confidenceResult = ResolveConfidenceBased(conflict, new ConflictResolutionRecord
            {
                ConflictId = conflict.ConflictId,
                Strategy = ResolutionStrategy.ConfidenceBased,
                ResolvedAt = DateTime.UtcNow,
                ConflictType = conflict.ConflictType,
                Severity = conflict.Severity
            });

            if (confidenceResult.Success)
            {
                conflict.Status = ConflictStatus.Resolved;
                conflict.ResolvedAt = DateTime.UtcNow;
                conflict.ResolutionStrategy = ResolutionStrategy.ConfidenceBased;
                conflict.ResolutionDetails = confidenceResult.Reason;

                _activeConflicts.TryRemove(conflict.ConflictId, out _);
                _resolvedConflicts.TryAdd(conflict.ConflictId, conflict);
                _resolutionLog.TryAdd($"{conflict.ConflictId}_{DateTime.UtcNow.Ticks}", confidenceResult);
                _totalConflictsResolved++;

                Logger.Debug("Auto-resolved conflict {0} via confidence", conflict.ConflictId);
                return true;
            }

            // Try negotiation for cost vs performance
            if (conflict.ConflictType == ConflictType.CostVsPerformance)
            {
                var negotiationResult = ResolveNegotiation(conflict, new ConflictResolutionRecord
                {
                    ConflictId = conflict.ConflictId,
                    Strategy = ResolutionStrategy.Negotiation,
                    ResolvedAt = DateTime.UtcNow,
                    ConflictType = conflict.ConflictType,
                    Severity = conflict.Severity
                });

                if (negotiationResult.Success)
                {
                    conflict.Status = ConflictStatus.Resolved;
                    conflict.ResolvedAt = DateTime.UtcNow;
                    conflict.ResolutionStrategy = ResolutionStrategy.Negotiation;
                    conflict.ResolutionDetails = negotiationResult.Reason;

                    _activeConflicts.TryRemove(conflict.ConflictId, out _);
                    _resolvedConflicts.TryAdd(conflict.ConflictId, conflict);
                    _resolutionLog.TryAdd($"{conflict.ConflictId}_{DateTime.UtcNow.Ticks}", negotiationResult);
                    _totalConflictsResolved++;

                    Logger.Debug("Auto-resolved conflict {0} via negotiation", conflict.ConflictId);
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Public Methods - Reporting

        /// <summary>
        /// Gets a summary report of all active conflicts.
        /// </summary>
        public ConflictReport GetConflictReport()
        {
            lock (_lockObject)
            {
                var activeList = _activeConflicts.Values.ToList();
                var resolvedList = _resolvedConflicts.Values.ToList();

                return new ConflictReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalConflictsDetected = _totalConflictsDetected,
                    TotalConflictsResolved = _totalConflictsResolved,
                    TotalConflictsEscalated = _totalConflictsEscalated,
                    ActiveConflictCount = activeList.Count,
                    ResolvedConflictCount = resolvedList.Count,
                    LastScanTime = _lastScanTime,

                    ActiveConflicts = activeList
                        .OrderByDescending(c => c.Severity)
                        .ThenByDescending(c => c.DetectedAt)
                        .ToList(),

                    ConflictsByType = activeList
                        .GroupBy(c => c.ConflictType)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count(), StringComparer.OrdinalIgnoreCase),

                    ConflictsBySeverity = activeList
                        .GroupBy(c => c.Severity)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count(), StringComparer.OrdinalIgnoreCase),

                    RecentResolutions = resolvedList
                        .OrderByDescending(c => c.ResolvedAt)
                        .Take(20)
                        .Select(c => new ResolutionSummary
                        {
                            ConflictId = c.ConflictId,
                            ConflictType = c.ConflictType,
                            Strategy = c.ResolutionStrategy,
                            ResolvedAt = c.ResolvedAt ?? DateTime.MinValue,
                            Details = c.ResolutionDetails
                        })
                        .ToList(),

                    CriticalConflicts = activeList
                        .Where(c => c.Severity >= ConflictSeverity.High)
                        .OrderByDescending(c => c.Severity)
                        .ToList(),

                    Summary = GenerateReportSummary(activeList, resolvedList)
                };
            }
        }

        /// <summary>
        /// Gets a specific conflict by its ID.
        /// </summary>
        public KnowledgeConflict GetConflict(string conflictId)
        {
            if (string.IsNullOrWhiteSpace(conflictId)) return null;

            if (_activeConflicts.TryGetValue(conflictId, out var active))
                return active;

            if (_resolvedConflicts.TryGetValue(conflictId, out var resolved))
                return resolved;

            return null;
        }

        /// <summary>
        /// Gets all active conflicts of a specific type.
        /// </summary>
        public List<KnowledgeConflict> GetConflictsByType(ConflictType type)
        {
            return _activeConflicts.Values
                .Where(c => c.ConflictType == type)
                .OrderByDescending(c => c.Severity)
                .ToList();
        }

        /// <summary>
        /// Gets the resolution history for a conflict.
        /// </summary>
        public List<ConflictResolutionRecord> GetResolutionHistory(string conflictId)
        {
            if (string.IsNullOrWhiteSpace(conflictId))
                return new List<ConflictResolutionRecord>();

            return _resolutionLog.Values
                .Where(r => string.Equals(r.ConflictId, conflictId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.ResolvedAt)
                .ToList();
        }

        /// <summary>
        /// Gets conflict resolver statistics.
        /// </summary>
        public ConflictResolverStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new ConflictResolverStatistics
                {
                    TotalDetected = _totalConflictsDetected,
                    TotalResolved = _totalConflictsResolved,
                    TotalEscalated = _totalConflictsEscalated,
                    CurrentActive = _activeConflicts.Count,
                    ResolutionRate = _totalConflictsDetected > 0
                        ? (float)_totalConflictsResolved / _totalConflictsDetected
                        : 0f,
                    LastScanTime = _lastScanTime,
                    MostCommonType = _activeConflicts.Values
                        .GroupBy(c => c.ConflictType)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key.ToString() ?? "None",
                    AverageSeverity = _activeConflicts.Values.Any()
                        ? _activeConflicts.Values.Average(c => (float)c.Severity)
                        : 0f
                };
            }
        }

        #endregion

        #region Private Methods - Conflict Detection

        private List<KnowledgeConflict> DetectFactVsFactConflicts(List<KnowledgeFact> facts)
        {
            var conflicts = new List<KnowledgeConflict>();

            // Group facts by subject to find contradictions about the same thing
            var factsBySubject = facts
                .Where(f => !string.IsNullOrEmpty(f.Subject))
                .GroupBy(f => f.Subject, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in factsBySubject)
            {
                var groupFacts = group.ToList();
                for (int i = 0; i < groupFacts.Count; i++)
                {
                    for (int j = i + 1; j < groupFacts.Count; j++)
                    {
                        if (AreFactsContradictory(groupFacts[i], groupFacts[j]))
                        {
                            conflicts.Add(CreateConflict(groupFacts[i], groupFacts[j]));
                        }
                    }
                }
            }

            Logger.Debug("Detected {0} fact vs fact conflicts", conflicts.Count);
            return conflicts;
        }

        private List<KnowledgeConflict> DetectCodeVsCodeConflicts(List<KnowledgeFact> facts)
        {
            var conflicts = new List<KnowledgeConflict>();

            var codeFacts = facts
                .Where(f => string.Equals(f.SourceType, "BuildingCode", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(f.SourceType, "Standard", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Group by topic area
            var byTopic = codeFacts
                .Where(f => !string.IsNullOrEmpty(f.Topic))
                .GroupBy(f => f.Topic, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var topicGroup in byTopic)
            {
                var topicFacts = topicGroup.ToList();
                for (int i = 0; i < topicFacts.Count; i++)
                {
                    for (int j = i + 1; j < topicFacts.Count; j++)
                    {
                        if (DoCodeFactsConflict(topicFacts[i], topicFacts[j]))
                        {
                            var conflict = CreateConflict(topicFacts[i], topicFacts[j]);
                            conflict.ConflictType = ConflictType.CodeVsCode;
                            conflict.Severity = ConflictSeverity.High;
                            conflicts.Add(conflict);
                        }
                    }
                }
            }

            Logger.Debug("Detected {0} code vs code conflicts", conflicts.Count);
            return conflicts;
        }

        private List<KnowledgeConflict> DetectCodeVsDesignConflicts(List<KnowledgeFact> facts)
        {
            var conflicts = new List<KnowledgeConflict>();

            var codeFacts = facts
                .Where(f => string.Equals(f.SourceType, "BuildingCode", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var designFacts = facts
                .Where(f => string.Equals(f.SourceType, "DesignIntent", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(f.SourceType, "DesignDecision", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var designFact in designFacts)
            {
                foreach (var codeFact in codeFacts)
                {
                    if (DoesDesignViolateCode(designFact, codeFact))
                    {
                        var conflict = new KnowledgeConflict
                        {
                            ConflictId = $"CVD_{Guid.NewGuid():N}".Substring(0, 24),
                            ConflictType = ConflictType.CodeVsDesign,
                            Severity = ConflictSeverity.High,
                            Status = ConflictStatus.Detected,
                            DetectedAt = DateTime.UtcNow,
                            FactA = new ConflictFact
                            {
                                FactId = designFact.FactId,
                                Source = designFact.Source,
                                SourceType = designFact.SourceType,
                                Subject = designFact.Subject,
                                Predicate = designFact.Predicate,
                                Value = designFact.Value,
                                Confidence = designFact.Confidence
                            },
                            FactB = new ConflictFact
                            {
                                FactId = codeFact.FactId,
                                Source = codeFact.Source,
                                SourceType = codeFact.SourceType,
                                Subject = codeFact.Subject,
                                Predicate = codeFact.Predicate,
                                Value = codeFact.Value,
                                Confidence = codeFact.Confidence
                            },
                            Description = $"Design intent '{designFact.Value}' for {designFact.Subject} " +
                                $"conflicts with code requirement '{codeFact.Value}' from {codeFact.Source}",
                            Impact = "Design may not achieve code compliance"
                        };

                        conflicts.Add(conflict);
                    }
                }
            }

            Logger.Debug("Detected {0} code vs design conflicts", conflicts.Count);
            return conflicts;
        }

        private List<KnowledgeConflict> DetectRuleVsRuleConflicts(List<KnowledgeRuleEntry> rules)
        {
            var conflicts = new List<KnowledgeConflict>();

            // Find rules with same condition but different conclusions
            var rulesByCondition = rules
                .Where(r => !string.IsNullOrEmpty(r.Condition))
                .GroupBy(r => r.Condition, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in rulesByCondition)
            {
                var groupRules = group.ToList();
                for (int i = 0; i < groupRules.Count; i++)
                {
                    for (int j = i + 1; j < groupRules.Count; j++)
                    {
                        if (!string.Equals(groupRules[i].Conclusion, groupRules[j].Conclusion,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            var conflict = new KnowledgeConflict
                            {
                                ConflictId = $"RVR_{Guid.NewGuid():N}".Substring(0, 24),
                                ConflictType = ConflictType.RuleVsRule,
                                Severity = ConflictSeverity.Medium,
                                Status = ConflictStatus.Detected,
                                DetectedAt = DateTime.UtcNow,
                                FactA = new ConflictFact
                                {
                                    FactId = groupRules[i].RuleId,
                                    Source = groupRules[i].Source,
                                    SourceType = "InferenceRule",
                                    Subject = groupRules[i].Condition,
                                    Predicate = "concludes",
                                    Value = groupRules[i].Conclusion,
                                    Confidence = groupRules[i].Confidence
                                },
                                FactB = new ConflictFact
                                {
                                    FactId = groupRules[j].RuleId,
                                    Source = groupRules[j].Source,
                                    SourceType = "InferenceRule",
                                    Subject = groupRules[j].Condition,
                                    Predicate = "concludes",
                                    Value = groupRules[j].Conclusion,
                                    Confidence = groupRules[j].Confidence
                                },
                                Description = $"Rules '{groupRules[i].RuleName}' and '{groupRules[j].RuleName}' " +
                                    $"have the same condition but produce different conclusions",
                                Impact = "Ambiguous inference results"
                            };

                            conflicts.Add(conflict);
                        }
                    }
                }
            }

            Logger.Debug("Detected {0} rule vs rule conflicts", conflicts.Count);
            return conflicts;
        }

        private List<KnowledgeConflict> DetectUserVsSystemConflicts(List<KnowledgeFact> facts)
        {
            var conflicts = new List<KnowledgeConflict>();

            var userPreferences = facts
                .Where(f => string.Equals(f.SourceType, "UserPreference", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var systemRecommendations = facts
                .Where(f => string.Equals(f.SourceType, "BestPractice", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(f.SourceType, "SystemRecommendation", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var pref in userPreferences)
            {
                foreach (var rec in systemRecommendations)
                {
                    if (AreFactsContradictory(pref, rec))
                    {
                        var conflict = CreateConflict(pref, rec);
                        conflict.ConflictType = ConflictType.UserVsSystem;
                        conflict.Severity = ConflictSeverity.Low;
                        conflict.Description = $"User preference '{pref.Value}' for {pref.Subject} " +
                            $"contradicts system recommendation '{rec.Value}'";
                        conflicts.Add(conflict);
                    }
                }
            }

            Logger.Debug("Detected {0} user vs system conflicts", conflicts.Count);
            return conflicts;
        }

        private bool AreFactsContradictory(KnowledgeFact factA, KnowledgeFact factB)
        {
            // Same subject and predicate but different values
            if (string.Equals(factA.Subject, factB.Subject, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(factA.Predicate, factB.Predicate, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(factA.Value, factB.Value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check for semantic contradiction
            if (string.Equals(factA.Subject, factB.Subject, StringComparison.OrdinalIgnoreCase))
            {
                return AreValuesContradictory(factA.Value, factB.Value, factA.Predicate);
            }

            return false;
        }

        private bool AreValuesContradictory(string valueA, string valueB, string predicate)
        {
            if (string.IsNullOrEmpty(valueA) || string.IsNullOrEmpty(valueB))
                return false;

            var a = valueA.ToLowerInvariant();
            var b = valueB.ToLowerInvariant();

            // Numeric contradictions (one says min 100, other says max 80)
            if (predicate != null)
            {
                var pred = predicate.ToLowerInvariant();
                if ((pred.Contains("minimum") || pred.Contains("at least")) &&
                    (b.Contains("maximum") || b.Contains("at most")))
                {
                    if (TryExtractNumber(a, out var minVal) && TryExtractNumber(b, out var maxVal))
                    {
                        if (minVal > maxVal) return true;
                    }
                }
            }

            // Direct semantic oppositions
            var oppositions = new[]
            {
                ("required", "prohibited"), ("allowed", "not allowed"),
                ("true", "false"), ("yes", "no"),
                ("compliant", "non-compliant"), ("pass", "fail")
            };

            foreach (var (pos, neg) in oppositions)
            {
                if ((a.Contains(pos) && b.Contains(neg)) || (a.Contains(neg) && b.Contains(pos)))
                    return true;
            }

            return false;
        }

        private bool DoCodeFactsConflict(KnowledgeFact codeA, KnowledgeFact codeB)
        {
            // Same topic, different requirements from different codes
            if (!string.Equals(codeA.Source, codeB.Source, StringComparison.OrdinalIgnoreCase))
            {
                return AreFactsContradictory(codeA, codeB);
            }
            return false;
        }

        private bool DoesDesignViolateCode(KnowledgeFact designFact, KnowledgeFact codeFact)
        {
            // Check if they address the same subject
            if (!string.Equals(designFact.Subject, codeFact.Subject, StringComparison.OrdinalIgnoreCase))
                return false;

            // Check if design value conflicts with code requirement
            return AreValuesContradictory(designFact.Value, codeFact.Value, codeFact.Predicate);
        }

        private bool TryExtractNumber(string text, out double number)
        {
            number = 0;
            if (string.IsNullOrEmpty(text)) return false;

            // Extract first number from text
            var numStr = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            return double.TryParse(numStr, out number);
        }

        private KnowledgeConflict CreateConflict(KnowledgeFact factA, KnowledgeFact factB)
        {
            var conflict = new KnowledgeConflict
            {
                ConflictId = $"CON_{Guid.NewGuid():N}".Substring(0, 24),
                ConflictType = ClassifyConflictType(factA, factB),
                Severity = AssessConflictSeverity(factA, factB),
                Status = ConflictStatus.Detected,
                DetectedAt = DateTime.UtcNow,
                FactA = new ConflictFact
                {
                    FactId = factA.FactId,
                    Source = factA.Source,
                    SourceType = factA.SourceType,
                    Subject = factA.Subject,
                    Predicate = factA.Predicate,
                    Value = factA.Value,
                    Confidence = factA.Confidence
                },
                FactB = new ConflictFact
                {
                    FactId = factB.FactId,
                    Source = factB.Source,
                    SourceType = factB.SourceType,
                    Subject = factB.Subject,
                    Predicate = factB.Predicate,
                    Value = factB.Value,
                    Confidence = factB.Confidence
                },
                Description = $"Conflicting claims about '{factA.Subject}': " +
                    $"'{factA.Source}' says '{factA.Value}' while '{factB.Source}' says '{factB.Value}'",
                Impact = EstimateConflictImpact(factA, factB)
            };

            return conflict;
        }

        private ConflictType ClassifyConflictType(KnowledgeFact factA, KnowledgeFact factB)
        {
            var typeA = factA.SourceType?.ToLowerInvariant() ?? "";
            var typeB = factB.SourceType?.ToLowerInvariant() ?? "";

            if (typeA.Contains("code") && typeB.Contains("code"))
                return ConflictType.CodeVsCode;
            if ((typeA.Contains("code") && typeB.Contains("design")) ||
                (typeA.Contains("design") && typeB.Contains("code")))
                return ConflictType.CodeVsDesign;
            if ((typeA.Contains("user") && !typeB.Contains("user")) ||
                (!typeA.Contains("user") && typeB.Contains("user")))
                return ConflictType.UserVsSystem;
            if (typeA.Contains("cost") || typeB.Contains("cost"))
                return ConflictType.CostVsPerformance;

            return ConflictType.FactVsFact;
        }

        private ConflictSeverity AssessConflictSeverity(KnowledgeFact factA, KnowledgeFact factB)
        {
            var authorityA = GetFactAuthority(factA.SourceType);
            var authorityB = GetFactAuthority(factB.SourceType);

            // Two high-authority sources conflicting = critical
            if (authorityA >= 6 && authorityB >= 6)
                return ConflictSeverity.Critical;

            // One high-authority source = high
            if (authorityA >= 5 || authorityB >= 5)
                return ConflictSeverity.High;

            // Both medium = medium
            if (authorityA >= 3 && authorityB >= 3)
                return ConflictSeverity.Medium;

            return ConflictSeverity.Low;
        }

        private string EstimateConflictImpact(KnowledgeFact factA, KnowledgeFact factB)
        {
            var topic = factA.Topic ?? factA.Subject ?? "unknown area";

            if (factA.SourceType?.Contains("Code") == true || factB.SourceType?.Contains("Code") == true)
                return $"May affect code compliance for {topic}";

            if (factA.SourceType?.Contains("Safety") == true || factB.SourceType?.Contains("Safety") == true)
                return $"May impact safety requirements for {topic}";

            return $"Uncertainty in {topic} requirements";
        }

        #endregion

        #region Private Methods - Resolution Strategies

        private ConflictResolutionRecord ResolvePriorityBased(KnowledgeConflict conflict,
            ConflictResolutionRecord record)
        {
            var authorityA = GetFactAuthority(conflict.FactA.SourceType);
            var authorityB = GetFactAuthority(conflict.FactB.SourceType);

            if (authorityA != authorityB)
            {
                var winner = authorityA > authorityB ? conflict.FactA : conflict.FactB;
                var loser = authorityA > authorityB ? conflict.FactB : conflict.FactA;

                record.Success = true;
                record.WinningFactId = winner.FactId;
                record.WinningSource = winner.Source;
                record.Reason = $"'{winner.Source}' ({winner.SourceType}, authority {Math.Max(authorityA, authorityB)}) " +
                    $"takes precedence over '{loser.Source}' ({loser.SourceType}, authority {Math.Min(authorityA, authorityB)}) " +
                    $"in the authority hierarchy: International Code > National Code > Local Code > " +
                    $"Industry Standard > Best Practice > User Preference > Learned Pattern";
            }
            else
            {
                record.Success = false;
                record.Reason = $"Both sources have equal authority level ({authorityA}). " +
                    $"Priority-based resolution cannot determine a winner.";
            }

            return record;
        }

        private ConflictResolutionRecord ResolveRecencyBased(KnowledgeConflict conflict,
            ConflictResolutionRecord record)
        {
            var dateA = conflict.FactA.LastUpdated ?? DateTime.MinValue;
            var dateB = conflict.FactB.LastUpdated ?? DateTime.MinValue;

            if (dateA != dateB)
            {
                var winner = dateA > dateB ? conflict.FactA : conflict.FactB;
                var winnerDate = dateA > dateB ? dateA : dateB;

                record.Success = true;
                record.WinningFactId = winner.FactId;
                record.WinningSource = winner.Source;
                record.Reason = $"More recent information from '{winner.Source}' " +
                    $"(updated {winnerDate:yyyy-MM-dd}) preferred over older data.";
            }
            else
            {
                record.Success = false;
                record.Reason = "Both facts have the same timestamp. Recency-based resolution cannot determine a winner.";
            }

            return record;
        }

        private ConflictResolutionRecord ResolveConfidenceBased(KnowledgeConflict conflict,
            ConflictResolutionRecord record)
        {
            var confidenceDiff = Math.Abs(conflict.FactA.Confidence - conflict.FactB.Confidence);

            if (confidenceDiff >= _configuration.MinConfidenceDifferenceForResolution)
            {
                var winner = conflict.FactA.Confidence > conflict.FactB.Confidence
                    ? conflict.FactA : conflict.FactB;
                var loser = conflict.FactA.Confidence > conflict.FactB.Confidence
                    ? conflict.FactB : conflict.FactA;

                record.Success = true;
                record.WinningFactId = winner.FactId;
                record.WinningSource = winner.Source;
                record.Reason = $"Higher confidence source '{winner.Source}' ({winner.Confidence:P0}) " +
                    $"preferred over '{loser.Source}' ({loser.Confidence:P0}). " +
                    $"Difference of {confidenceDiff:P0} exceeds threshold.";
            }
            else
            {
                record.Success = false;
                record.Reason = $"Confidence difference ({confidenceDiff:P0}) below threshold " +
                    $"({_configuration.MinConfidenceDifferenceForResolution:P0}). " +
                    $"Cannot reliably determine which source is more accurate.";
            }

            return record;
        }

        private ConflictResolutionRecord ResolveNegotiation(KnowledgeConflict conflict,
            ConflictResolutionRecord record)
        {
            // Negotiation: find a middle ground
            if (conflict.ConflictType == ConflictType.CostVsPerformance)
            {
                record.Success = true;
                record.NegotiatedValue = "Balanced approach recommended";
                record.Reason = $"Cost-performance trade-off between '{conflict.FactA.Value}' and " +
                    $"'{conflict.FactB.Value}'. Recommend: meet minimum performance requirement " +
                    $"while optimizing for cost. Consider value engineering alternatives.";
                return record;
            }

            // Try numeric interpolation
            if (TryExtractNumber(conflict.FactA.Value, out var numA) &&
                TryExtractNumber(conflict.FactB.Value, out var numB))
            {
                var midpoint = (numA + numB) / 2.0;
                record.Success = true;
                record.NegotiatedValue = midpoint.ToString("F2");
                record.Reason = $"Negotiated compromise value of {midpoint:F2} between " +
                    $"'{conflict.FactA.Value}' ({conflict.FactA.Source}) and " +
                    $"'{conflict.FactB.Value}' ({conflict.FactB.Source}).";
                return record;
            }

            record.Success = false;
            record.Reason = "Cannot negotiate between non-numeric or incompatible values. Manual review needed.";
            return record;
        }

        private ConflictResolutionRecord ResolveEscalation(KnowledgeConflict conflict,
            ConflictResolutionRecord record)
        {
            record.Success = false;
            record.RequiresHumanDecision = true;
            record.Reason = $"Conflict escalated for human review. " +
                $"Source A: '{conflict.FactA.Source}' claims '{conflict.FactA.Value}'. " +
                $"Source B: '{conflict.FactB.Source}' claims '{conflict.FactB.Value}'. " +
                $"Severity: {conflict.Severity}. Automated resolution was not possible.";

            return record;
        }

        private int GetFactAuthority(string sourceType)
        {
            if (string.IsNullOrEmpty(sourceType)) return 0;

            var level = _authorityHierarchy
                .FirstOrDefault(a => string.Equals(a.SourceType, sourceType, StringComparison.OrdinalIgnoreCase));

            return level?.Authority ?? 0;
        }

        #endregion

        #region Private Methods - Reporting

        private string GenerateReportSummary(List<KnowledgeConflict> active, List<KnowledgeConflict> resolved)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Conflict Resolution Report - {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Active Conflicts: {active.Count}");
            sb.AppendLine($"Resolved Conflicts: {resolved.Count}");
            sb.AppendLine($"Total Detected: {_totalConflictsDetected}");
            sb.AppendLine();

            var critical = active.Count(c => c.Severity == ConflictSeverity.Critical);
            var high = active.Count(c => c.Severity == ConflictSeverity.High);

            if (critical > 0)
            {
                sb.AppendLine($"CRITICAL: {critical} critical conflict(s) require immediate attention!");
            }
            if (high > 0)
            {
                sb.AppendLine($"WARNING: {high} high-severity conflict(s) detected.");
            }
            if (critical == 0 && high == 0)
            {
                sb.AppendLine("No critical or high-severity conflicts active.");
            }

            return sb.ToString();
        }

        #endregion

        #region Initialization

        private void InitializeAuthorityHierarchy()
        {
            // Authority hierarchy: International Code > National Code > Local Code >
            // Industry Standard > Best Practice > User Preference > Learned Pattern

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "InternationalCode",
                Authority = 7,
                Description = "International building codes and standards (ISO, IBC)"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "NationalCode",
                Authority = 6,
                Description = "National building codes (ACI 318, ASCE 7, NEC)"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "BuildingCode",
                Authority = 6,
                Description = "Building codes (generic classification)"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "Standard",
                Authority = 6,
                Description = "Published standards (ASHRAE, NFPA)"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "LocalCode",
                Authority = 5,
                Description = "Local amendments and regional codes"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "IndustryStandard",
                Authority = 4,
                Description = "Industry guidelines and standards of practice"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "BestPractice",
                Authority = 3,
                Description = "Established best practices and rules of thumb"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "SystemRecommendation",
                Authority = 3,
                Description = "StingBIM system-generated recommendations"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "CsvData",
                Authority = 3,
                Description = "Data from CSV reference files"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "DesignIntent",
                Authority = 2,
                Description = "Design intent from the project team"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "DesignDecision",
                Authority = 2,
                Description = "Explicit design decisions made by the user"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "UserPreference",
                Authority = 2,
                Description = "User preferences and custom settings"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "LearnedPattern",
                Authority = 1,
                Description = "Patterns learned from observation"
            });

            _authorityHierarchy.Add(new AuthorityLevel
            {
                SourceType = "InferenceRule",
                Authority = 1,
                Description = "Machine-learned inference rules"
            });

            Logger.Debug("Initialized {0} authority levels", _authorityHierarchy.Count);
        }

        private void InitializeDetectionRules()
        {
            // Numeric threshold contradictions
            _detectionRules.Add(new ConflictDetectionRule
            {
                RuleId = "NUM_MINMAX",
                Description = "Minimum value exceeds maximum value",
                AppliesTo = "NumericRequirements",
                DetectionLogic = "If MinValue > MaxValue for same subject"
            });

            // Boolean contradictions
            _detectionRules.Add(new ConflictDetectionRule
            {
                RuleId = "BOOL_OPPOSITE",
                Description = "Opposite boolean requirements",
                AppliesTo = "BooleanRequirements",
                DetectionLogic = "If required=true and required=false for same subject"
            });

            // Code section conflicts
            _detectionRules.Add(new ConflictDetectionRule
            {
                RuleId = "CODE_OVERLAP",
                Description = "Overlapping code requirements with different values",
                AppliesTo = "BuildingCodes",
                DetectionLogic = "Same requirement topic, different codes, different values"
            });

            // Temporal conflicts
            _detectionRules.Add(new ConflictDetectionRule
            {
                RuleId = "TEMPORAL_VERSION",
                Description = "Superseded code version still referenced",
                AppliesTo = "BuildingCodes",
                DetectionLogic = "Newer version of code exists with different requirement"
            });

            Logger.Debug("Initialized {0} detection rules", _detectionRules.Count);
        }

        #endregion
    }

    #endregion

    #region Conflict Types

    /// <summary>
    /// Types of knowledge conflicts.
    /// </summary>
    public enum ConflictType
    {
        /// <summary>Two building codes disagree (e.g., local vs international).</summary>
        CodeVsCode,
        /// <summary>Design intent violates a code requirement.</summary>
        CodeVsDesign,
        /// <summary>User preference contradicts a best practice.</summary>
        UserVsSystem,
        /// <summary>Two learned facts contradict each other.</summary>
        FactVsFact,
        /// <summary>Two inference rules produce contradictory results.</summary>
        RuleVsRule,
        /// <summary>Budget constraint vs performance requirement.</summary>
        CostVsPerformance
    }

    /// <summary>
    /// Severity levels for conflicts.
    /// </summary>
    public enum ConflictSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Current status of a conflict.
    /// </summary>
    public enum ConflictStatus
    {
        Detected,
        UnderReview,
        Resolved,
        Escalated,
        Dismissed
    }

    /// <summary>
    /// Available resolution strategies.
    /// </summary>
    public enum ResolutionStrategy
    {
        /// <summary>Higher-authority source wins.</summary>
        PriorityBased,
        /// <summary>More recent information preferred.</summary>
        RecencyBased,
        /// <summary>Higher confidence fact wins.</summary>
        ConfidenceBased,
        /// <summary>Find middle ground satisfying both constraints.</summary>
        Negotiation,
        /// <summary>Flag for human decision when no auto-resolution possible.</summary>
        Escalation
    }

    /// <summary>
    /// A detected conflict between knowledge sources.
    /// </summary>
    public class KnowledgeConflict
    {
        public string ConflictId { get; set; }
        public ConflictType ConflictType { get; set; }
        public ConflictSeverity Severity { get; set; }
        public ConflictStatus Status { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public ConflictFact FactA { get; set; }
        public ConflictFact FactB { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public ResolutionStrategy? ResolutionStrategy { get; set; }
        public string ResolutionDetails { get; set; }
    }

    /// <summary>
    /// One side of a conflict.
    /// </summary>
    public class ConflictFact
    {
        public string FactId { get; set; }
        public string Source { get; set; }
        public string SourceType { get; set; }
        public string Subject { get; set; }
        public string Predicate { get; set; }
        public string Value { get; set; }
        public float Confidence { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    /// <summary>
    /// A knowledge fact for conflict detection.
    /// </summary>
    public class KnowledgeFact
    {
        public string FactId { get; set; }
        public string Source { get; set; }
        public string SourceType { get; set; }
        public string Subject { get; set; }
        public string Predicate { get; set; }
        public string Value { get; set; }
        public string Topic { get; set; }
        public float Confidence { get; set; }
        public DateTime? LastUpdated { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// A knowledge rule entry for conflict detection.
    /// </summary>
    public class KnowledgeRuleEntry
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Source { get; set; }
        public string Condition { get; set; }
        public string Conclusion { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Record of a conflict resolution attempt.
    /// </summary>
    public class ConflictResolutionRecord
    {
        public string ConflictId { get; set; }
        public ResolutionStrategy Strategy { get; set; }
        public bool Success { get; set; }
        public string WinningFactId { get; set; }
        public string WinningSource { get; set; }
        public string NegotiatedValue { get; set; }
        public string Reason { get; set; }
        public string Justification { get; set; }
        public bool RequiresHumanDecision { get; set; }
        public ConflictType ConflictType { get; set; }
        public ConflictSeverity Severity { get; set; }
        public DateTime ResolvedAt { get; set; }
    }

    /// <summary>
    /// Result of a conflict scan operation.
    /// </summary>
    public class ConflictScanResult
    {
        public DateTime ScanStartTime { get; set; }
        public DateTime ScanEndTime { get; set; }
        public int TotalFactsScanned { get; set; }
        public int TotalRulesScanned { get; set; }
        public int TotalConflictsDetected { get; set; }
        public int TotalAutoResolved { get; set; }
        public int TotalEscalated { get; set; }
        public int TotalActiveConflicts { get; set; }
        public List<KnowledgeConflict> NewConflicts { get; set; }
        public List<KnowledgeConflict> ResolvedConflicts { get; set; }
        public List<KnowledgeConflict> EscalatedConflicts { get; set; }
        public bool WasCancelled { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Full conflict report.
    /// </summary>
    public class ConflictReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalConflictsDetected { get; set; }
        public int TotalConflictsResolved { get; set; }
        public int TotalConflictsEscalated { get; set; }
        public int ActiveConflictCount { get; set; }
        public int ResolvedConflictCount { get; set; }
        public DateTime LastScanTime { get; set; }
        public List<KnowledgeConflict> ActiveConflicts { get; set; }
        public List<KnowledgeConflict> CriticalConflicts { get; set; }
        public Dictionary<string, int> ConflictsByType { get; set; }
        public Dictionary<string, int> ConflictsBySeverity { get; set; }
        public List<ResolutionSummary> RecentResolutions { get; set; }
        public string Summary { get; set; }
    }

    /// <summary>
    /// Summary of a resolution for reporting.
    /// </summary>
    public class ResolutionSummary
    {
        public string ConflictId { get; set; }
        public ConflictType ConflictType { get; set; }
        public ResolutionStrategy? Strategy { get; set; }
        public DateTime ResolvedAt { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// An authority level in the hierarchy.
    /// </summary>
    public class AuthorityLevel
    {
        public string SourceType { get; set; }
        public int Authority { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// A rule for detecting specific types of conflicts.
    /// </summary>
    public class ConflictDetectionRule
    {
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string AppliesTo { get; set; }
        public string DetectionLogic { get; set; }
    }

    /// <summary>
    /// Statistics about conflict resolution.
    /// </summary>
    public class ConflictResolverStatistics
    {
        public int TotalDetected { get; set; }
        public int TotalResolved { get; set; }
        public int TotalEscalated { get; set; }
        public int CurrentActive { get; set; }
        public float ResolutionRate { get; set; }
        public DateTime LastScanTime { get; set; }
        public string MostCommonType { get; set; }
        public float AverageSeverity { get; set; }
    }

    /// <summary>
    /// Configuration for the conflict resolver.
    /// </summary>
    public class ConflictResolverConfiguration
    {
        public bool EnableAutoResolution { get; set; } = true;
        public float MinConfidenceDifferenceForResolution { get; set; } = 0.15f;
        public int MaxActiveConflicts { get; set; } = 1000;
        public bool EscalateHighSeverity { get; set; } = true;
    }

    #endregion
}
