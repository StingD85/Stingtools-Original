// StingBIM.AI.Intelligence.Reasoning.RuleLearner
// Self-improving inference rule system that learns new rules from repeated pattern observations
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Amplification (Deepen Reasoning)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Intelligence.Reasoning
{
    #region Rule Learning Engine

    /// <summary>
    /// Self-improving inference rule system that learns NEW inference rules from
    /// repeated pattern observations. When a pattern appears 5+ times, generates
    /// a candidate rule. Validates against building codes before activation.
    /// </summary>
    public class RuleLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly ConcurrentDictionary<string, LearnedRule> _learnedRules;
        private readonly ConcurrentDictionary<string, CandidateRule> _candidateRules;
        private readonly ConcurrentDictionary<string, PatternObservation> _patternObservations;
        private readonly ConcurrentDictionary<string, RuleVersion> _ruleVersionHistory;
        private readonly List<RuleTemplate> _ruleTemplates;
        private readonly List<BuildingCodeReference> _buildingCodeReferences;
        private readonly RuleLearnerConfiguration _configuration;

        private int _minPatternOccurrences;
        private float _activationConfidenceThreshold;
        private int _totalRulesLearned;
        private int _totalRulesActivated;
        private int _totalRulesRetired;
        private DateTime _lastLearningCycle;

        /// <summary>
        /// Initializes the rule learner with default configuration.
        /// </summary>
        public RuleLearner()
            : this(new RuleLearnerConfiguration())
        {
        }

        /// <summary>
        /// Initializes the rule learner with custom configuration.
        /// </summary>
        public RuleLearner(RuleLearnerConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _learnedRules = new ConcurrentDictionary<string, LearnedRule>(StringComparer.OrdinalIgnoreCase);
            _candidateRules = new ConcurrentDictionary<string, CandidateRule>(StringComparer.OrdinalIgnoreCase);
            _patternObservations = new ConcurrentDictionary<string, PatternObservation>(StringComparer.OrdinalIgnoreCase);
            _ruleVersionHistory = new ConcurrentDictionary<string, RuleVersion>(StringComparer.OrdinalIgnoreCase);
            _ruleTemplates = new List<RuleTemplate>();
            _buildingCodeReferences = new List<BuildingCodeReference>();

            _minPatternOccurrences = _configuration.MinPatternOccurrences;
            _activationConfidenceThreshold = _configuration.ActivationConfidenceThreshold;
            _totalRulesLearned = 0;
            _totalRulesActivated = 0;
            _totalRulesRetired = 0;
            _lastLearningCycle = DateTime.MinValue;

            InitializeRuleTemplates();
            InitializeBuildingCodeReferences();

            Logger.Info("RuleLearner initialized with {0} templates and {1} building code references",
                _ruleTemplates.Count, _buildingCodeReferences.Count);
        }

        #region Public Methods

        /// <summary>
        /// Learns new inference rules from repeated pattern observations.
        /// Induces candidate rules when a pattern appears MinPatternOccurrences+ times.
        /// </summary>
        public async Task<RuleLearningResult> LearnRulesFromPatternsAsync(
            IEnumerable<LearnedPatternInput> patterns,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (patterns == null) throw new ArgumentNullException(nameof(patterns));

            var result = new RuleLearningResult
            {
                StartTime = DateTime.UtcNow,
                NewCandidates = new List<CandidateRule>(),
                PromotedRules = new List<LearnedRule>(),
                RetiredRules = new List<LearnedRule>(),
                ValidationResults = new List<RuleValidationResult>()
            };

            var patternList = patterns.ToList();
            progress?.Report($"Processing {patternList.Count} patterns for rule induction...");
            Logger.Info("Starting rule learning cycle with {0} patterns", patternList.Count);

            try
            {
                // Step 1: Record pattern observations
                progress?.Report("Step 1/5: Recording pattern observations...");
                var observationCount = RecordPatternObservations(patternList);
                Logger.Debug("Recorded {0} pattern observations", observationCount);

                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Identify patterns that exceed the occurrence threshold
                progress?.Report("Step 2/5: Identifying frequent patterns...");
                var frequentPatterns = IdentifyFrequentPatterns();
                Logger.Debug("Found {0} frequent patterns above threshold", frequentPatterns.Count);

                cancellationToken.ThrowIfCancellationRequested();

                // Step 3: Induce candidate rules from frequent patterns
                progress?.Report("Step 3/5: Inducing candidate rules...");
                foreach (var pattern in frequentPatterns)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidates = InduceCandidateRules(pattern);
                    foreach (var candidate in candidates)
                    {
                        if (_candidateRules.TryAdd(candidate.RuleId, candidate))
                        {
                            result.NewCandidates.Add(candidate);
                            Logger.Debug("New candidate rule induced: {0} ({1})", candidate.RuleName, candidate.Category);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Validate candidate rules against building codes
                progress?.Report("Step 4/5: Validating against building codes...");
                foreach (var candidate in _candidateRules.Values.Where(c => c.Status == CandidateRuleStatus.PendingValidation))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var validationResult = await ValidateRuleAsync(candidate, cancellationToken);
                    result.ValidationResults.Add(validationResult);

                    if (validationResult.IsValid)
                    {
                        candidate.Status = CandidateRuleStatus.Validated;
                        candidate.ValidationConfidence = validationResult.Confidence;
                        Logger.Debug("Rule validated: {0} (confidence: {1:P0})", candidate.RuleName, validationResult.Confidence);
                    }
                    else
                    {
                        candidate.Status = CandidateRuleStatus.Rejected;
                        candidate.RejectionReason = validationResult.RejectionReason;
                        Logger.Debug("Rule rejected: {0} - {1}", candidate.RuleName, validationResult.RejectionReason);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Step 5: Promote validated candidates to active rules
                progress?.Report("Step 5/5: Promoting validated rules...");
                var promotedRules = PromoteValidatedCandidates();
                result.PromotedRules.AddRange(promotedRules);

                // Check for rules to retire
                var retiredRules = RetireUnderperformingRules();
                result.RetiredRules.AddRange(retiredRules);

                result.EndTime = DateTime.UtcNow;
                result.TotalPatternsProcessed = patternList.Count;
                result.TotalActiveRules = _learnedRules.Count(r => r.Value.Status == LearnedRuleStatus.Active);

                _lastLearningCycle = DateTime.UtcNow;

                Logger.Info("Rule learning cycle complete: {0} new candidates, {1} promoted, {2} retired, {3} total active",
                    result.NewCandidates.Count, result.PromotedRules.Count,
                    result.RetiredRules.Count, result.TotalActiveRules);

                progress?.Report($"Learning complete: {result.PromotedRules.Count} new rules activated, " +
                    $"{result.TotalActiveRules} total active rules");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Rule learning cycle was cancelled");
                result.WasCancelled = true;
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during rule learning cycle");
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Synchronous wrapper for learning rules from patterns.
        /// </summary>
        public RuleLearningResult LearnRulesFromPatterns(IEnumerable<LearnedPatternInput> patterns)
        {
            return LearnRulesFromPatternsAsync(patterns, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns all rules that are currently above the confidence threshold and active.
        /// </summary>
        public List<LearnedRule> GetActiveRules()
        {
            lock (_lockObject)
            {
                return _learnedRules.Values
                    .Where(r => r.Status == LearnedRuleStatus.Active &&
                                r.Confidence >= _activationConfidenceThreshold)
                    .OrderByDescending(r => r.Confidence)
                    .ThenByDescending(r => r.SuccessfulApplications)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns active rules filtered by building-science category.
        /// </summary>
        public List<LearnedRule> GetActiveRulesByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentNullException(nameof(category));

            return GetActiveRules()
                .Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Returns active rules filtered by domain.
        /// </summary>
        public List<LearnedRule> GetActiveRulesByDomain(RuleDomain domain)
        {
            return GetActiveRules()
                .Where(r => r.Domain == domain)
                .ToList();
        }

        /// <summary>
        /// Validates a single rule against known building code knowledge.
        /// </summary>
        public async Task<RuleValidationResult> ValidateRuleAsync(
            CandidateRule rule,
            CancellationToken cancellationToken = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            var result = new RuleValidationResult
            {
                RuleId = rule.RuleId,
                RuleName = rule.RuleName,
                ValidationTime = DateTime.UtcNow,
                Checks = new List<ValidationCheck>()
            };

            try
            {
                // Check 1: Does the rule contradict known building codes?
                var codeCheck = ValidateAgainstBuildingCodes(rule);
                result.Checks.Add(codeCheck);
                if (!codeCheck.Passed)
                {
                    result.IsValid = false;
                    result.RejectionReason = $"Contradicts building code: {codeCheck.Details}";
                    result.Confidence = 0f;
                    return result;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Check 2: Is the rule logically consistent?
                var logicCheck = ValidateLogicalConsistency(rule);
                result.Checks.Add(logicCheck);
                if (!logicCheck.Passed)
                {
                    result.IsValid = false;
                    result.RejectionReason = $"Logical inconsistency: {logicCheck.Details}";
                    result.Confidence = 0f;
                    return result;
                }

                // Check 3: Does the rule conflict with existing active rules?
                var conflictCheck = ValidateNoConflictWithExisting(rule);
                result.Checks.Add(conflictCheck);

                // Check 4: Is the rule specific enough to be useful?
                var specificityCheck = ValidateSpecificity(rule);
                result.Checks.Add(specificityCheck);

                // Check 5: Does the rule have sufficient supporting evidence?
                var evidenceCheck = ValidateEvidenceStrength(rule);
                result.Checks.Add(evidenceCheck);

                // Check 6: Domain-specific validation
                var domainCheck = ValidateDomainConstraints(rule);
                result.Checks.Add(domainCheck);

                // Calculate overall confidence from all checks
                var passedChecks = result.Checks.Where(c => c.Passed).ToList();
                result.Confidence = passedChecks.Any()
                    ? passedChecks.Average(c => c.Confidence)
                    : 0f;

                result.IsValid = result.Confidence >= 0.5f && codeCheck.Passed && logicCheck.Passed;

                if (!result.IsValid && string.IsNullOrEmpty(result.RejectionReason))
                {
                    var failedChecks = result.Checks.Where(c => !c.Passed).Select(c => c.CheckName);
                    result.RejectionReason = $"Failed checks: {string.Join(", ", failedChecks)}";
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error validating rule {0}", rule.RuleId);
                result.IsValid = false;
                result.RejectionReason = $"Validation error: {ex.Message}";
                result.Confidence = 0f;
            }

            return result;
        }

        /// <summary>
        /// Records a successful application of a learned rule, increasing its confidence.
        /// </summary>
        public void RecordRuleSuccess(string ruleId, string context = null)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return;

            lock (_lockObject)
            {
                if (_learnedRules.TryGetValue(ruleId, out var rule))
                {
                    rule.SuccessfulApplications++;
                    rule.TotalApplications++;
                    rule.LastApplied = DateTime.UtcNow;

                    // Increase confidence up to a max of 0.99
                    var successRate = (float)rule.SuccessfulApplications / rule.TotalApplications;
                    rule.Confidence = Math.Min(0.99f, rule.Confidence + (successRate * 0.02f));

                    Logger.Debug("Rule success recorded for {0}: confidence now {1:P1}", rule.RuleName, rule.Confidence);

                    // Record version if significant confidence change
                    RecordVersionIfSignificant(rule);
                }
            }
        }

        /// <summary>
        /// Records a failed application of a learned rule, decreasing its confidence.
        /// </summary>
        public void RecordRuleFailure(string ruleId, string failureReason = null)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return;

            lock (_lockObject)
            {
                if (_learnedRules.TryGetValue(ruleId, out var rule))
                {
                    rule.FailedApplications++;
                    rule.TotalApplications++;
                    rule.LastApplied = DateTime.UtcNow;
                    rule.LastFailureReason = failureReason;

                    // Decrease confidence
                    rule.Confidence = Math.Max(0.01f, rule.Confidence - 0.05f);

                    Logger.Debug("Rule failure recorded for {0}: confidence now {1:P1}", rule.RuleName, rule.Confidence);

                    // Check if rule should be deactivated
                    if (rule.Confidence < _configuration.DeactivationThreshold &&
                        rule.TotalApplications >= _configuration.MinApplicationsBeforeRetirement)
                    {
                        rule.Status = LearnedRuleStatus.Retired;
                        _totalRulesRetired++;
                        Logger.Warn("Rule {0} retired due to low confidence: {1:P1}", rule.RuleName, rule.Confidence);
                    }

                    RecordVersionIfSignificant(rule);
                }
            }
        }

        /// <summary>
        /// Gets the complete version history for a rule.
        /// </summary>
        public List<RuleVersion> GetRuleVersionHistory(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return new List<RuleVersion>();

            return _ruleVersionHistory.Values
                .Where(v => string.Equals(v.RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => v.VersionNumber)
                .ToList();
        }

        /// <summary>
        /// Gets a summary of the rule learning system's statistics.
        /// </summary>
        public RuleLearnerStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new RuleLearnerStatistics
                {
                    TotalRulesLearned = _totalRulesLearned,
                    TotalRulesActivated = _totalRulesActivated,
                    TotalRulesRetired = _totalRulesRetired,
                    CurrentActiveRules = _learnedRules.Count(r => r.Value.Status == LearnedRuleStatus.Active),
                    CurrentCandidateRules = _candidateRules.Count(r => r.Value.Status == CandidateRuleStatus.PendingValidation ||
                                                                       r.Value.Status == CandidateRuleStatus.Validated),
                    TotalPatternObservations = _patternObservations.Count,
                    AverageRuleConfidence = _learnedRules.Values
                        .Where(r => r.Status == LearnedRuleStatus.Active)
                        .Select(r => r.Confidence)
                        .DefaultIfEmpty(0f)
                        .Average(),
                    LastLearningCycle = _lastLearningCycle,
                    RulesByCategory = _learnedRules.Values
                        .Where(r => r.Status == LearnedRuleStatus.Active)
                        .GroupBy(r => r.Category)
                        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
                    RulesByDomain = _learnedRules.Values
                        .Where(r => r.Status == LearnedRuleStatus.Active)
                        .GroupBy(r => r.Domain)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count(), StringComparer.OrdinalIgnoreCase)
                };
            }
        }

        /// <summary>
        /// Persists all learned rules and state to disk.
        /// </summary>
        public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var state = new RuleLearnerState
            {
                LearnedRules = _learnedRules.Values.ToList(),
                CandidateRules = _candidateRules.Values.ToList(),
                PatternObservations = _patternObservations.Values.ToList(),
                RuleVersionHistory = _ruleVersionHistory.Values.ToList(),
                TotalRulesLearned = _totalRulesLearned,
                TotalRulesActivated = _totalRulesActivated,
                TotalRulesRetired = _totalRulesRetired,
                LastLearningCycle = _lastLearningCycle,
                SavedAt = DateTime.UtcNow
            };

            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await Task.Run(() => File.WriteAllText(filePath, json), cancellationToken);
            Logger.Info("RuleLearner state saved to {0} ({1} rules, {2} candidates)",
                filePath, _learnedRules.Count, _candidateRules.Count);
        }

        /// <summary>
        /// Loads previously saved rules and state from disk.
        /// </summary>
        public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
            {
                Logger.Warn("Rule learner state file not found: {0}", filePath);
                return;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(filePath), cancellationToken);
                var state = JsonConvert.DeserializeObject<RuleLearnerState>(json);

                if (state == null)
                {
                    Logger.Warn("Deserialized null state from {0}", filePath);
                    return;
                }

                lock (_lockObject)
                {
                    _learnedRules.Clear();
                    foreach (var rule in state.LearnedRules ?? Enumerable.Empty<LearnedRule>())
                    {
                        _learnedRules.TryAdd(rule.RuleId, rule);
                    }

                    _candidateRules.Clear();
                    foreach (var candidate in state.CandidateRules ?? Enumerable.Empty<CandidateRule>())
                    {
                        _candidateRules.TryAdd(candidate.RuleId, candidate);
                    }

                    _patternObservations.Clear();
                    foreach (var observation in state.PatternObservations ?? Enumerable.Empty<PatternObservation>())
                    {
                        _patternObservations.TryAdd(observation.PatternKey, observation);
                    }

                    _ruleVersionHistory.Clear();
                    foreach (var version in state.RuleVersionHistory ?? Enumerable.Empty<RuleVersion>())
                    {
                        var key = $"{version.RuleId}_v{version.VersionNumber}";
                        _ruleVersionHistory.TryAdd(key, version);
                    }

                    _totalRulesLearned = state.TotalRulesLearned;
                    _totalRulesActivated = state.TotalRulesActivated;
                    _totalRulesRetired = state.TotalRulesRetired;
                    _lastLearningCycle = state.LastLearningCycle;
                }

                Logger.Info("RuleLearner state loaded from {0}: {1} rules, {2} candidates",
                    filePath, _learnedRules.Count, _candidateRules.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading rule learner state from {0}", filePath);
            }
        }

        #endregion

        #region Private Methods - Pattern Processing

        private int RecordPatternObservations(List<LearnedPatternInput> patterns)
        {
            int count = 0;
            foreach (var pattern in patterns)
            {
                var key = GeneratePatternKey(pattern);
                var observation = _patternObservations.GetOrAdd(key, _ => new PatternObservation
                {
                    PatternKey = key,
                    PatternType = pattern.PatternType,
                    Description = pattern.Description,
                    Category = pattern.Category,
                    Domain = ClassifyDomain(pattern),
                    FirstObserved = DateTime.UtcNow,
                    Occurrences = 0,
                    ContextSnapshots = new List<Dictionary<string, object>>()
                });

                lock (_lockObject)
                {
                    observation.Occurrences++;
                    observation.LastObserved = DateTime.UtcNow;
                    observation.Confidence = Math.Min(1.0f,
                        observation.Confidence + (pattern.Confidence * 0.1f));

                    if (pattern.Context != null && observation.ContextSnapshots.Count < 20)
                    {
                        observation.ContextSnapshots.Add(
                            new Dictionary<string, object>(pattern.Context, StringComparer.OrdinalIgnoreCase));
                    }
                }

                count++;
            }
            return count;
        }

        private List<PatternObservation> IdentifyFrequentPatterns()
        {
            return _patternObservations.Values
                .Where(p => p.Occurrences >= _minPatternOccurrences &&
                           !p.HasInducedRule)
                .OrderByDescending(p => p.Occurrences)
                .ThenByDescending(p => p.Confidence)
                .ToList();
        }

        private List<CandidateRule> InduceCandidateRules(PatternObservation pattern)
        {
            var candidates = new List<CandidateRule>();

            // Match against rule templates
            foreach (var template in _ruleTemplates)
            {
                if (!IsTemplateApplicable(template, pattern))
                    continue;

                var candidate = InstantiateTemplate(template, pattern);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            // If no template matched, try generic induction
            if (candidates.Count == 0)
            {
                var genericCandidate = InduceGenericRule(pattern);
                if (genericCandidate != null)
                {
                    candidates.Add(genericCandidate);
                }
            }

            // Mark pattern as having induced rules
            if (candidates.Count > 0)
            {
                pattern.HasInducedRule = true;
            }

            return candidates;
        }

        private bool IsTemplateApplicable(RuleTemplate template, PatternObservation pattern)
        {
            // Check domain match
            if (template.ApplicableDomains != null &&
                template.ApplicableDomains.Count > 0 &&
                !template.ApplicableDomains.Contains(pattern.Domain))
            {
                return false;
            }

            // Check pattern type match
            if (!string.IsNullOrEmpty(template.RequiredPatternType) &&
                !string.Equals(template.RequiredPatternType, pattern.PatternType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check minimum confidence
            if (pattern.Confidence < template.MinPatternConfidence)
            {
                return false;
            }

            return true;
        }

        private CandidateRule InstantiateTemplate(RuleTemplate template, PatternObservation pattern)
        {
            try
            {
                var ruleId = $"LR_{template.TemplateId}_{Guid.NewGuid():N}".Substring(0, 32);

                // Extract condition and conclusion from pattern context
                var condition = ExtractCondition(pattern, template);
                var conclusion = ExtractConclusion(pattern, template);

                if (string.IsNullOrEmpty(condition) || string.IsNullOrEmpty(conclusion))
                    return null;

                return new CandidateRule
                {
                    RuleId = ruleId,
                    RuleName = $"{template.Name}: {pattern.Description}",
                    Description = $"Learned from {pattern.Occurrences} observations of pattern '{pattern.PatternKey}'",
                    Category = pattern.Category ?? template.DefaultCategory,
                    Domain = pattern.Domain,
                    TemplateId = template.TemplateId,
                    Condition = condition,
                    Conclusion = conclusion,
                    ConditionNodeType = template.ConditionNodeType,
                    ConditionProperty = template.ConditionProperty,
                    ConclusionRelationType = template.ConclusionRelationType,
                    ConclusionTarget = template.ConclusionTarget,
                    InitialConfidence = pattern.Confidence * template.ConfidenceMultiplier,
                    SupportingObservations = pattern.Occurrences,
                    CreatedAt = DateTime.UtcNow,
                    Status = CandidateRuleStatus.PendingValidation,
                    SourcePatternKey = pattern.PatternKey,
                    ContextEvidence = pattern.ContextSnapshots?.Take(5).ToList()
                        ?? new List<Dictionary<string, object>>()
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error instantiating template {0} for pattern {1}",
                    template.TemplateId, pattern.PatternKey);
                return null;
            }
        }

        private CandidateRule InduceGenericRule(PatternObservation pattern)
        {
            var ruleId = $"LR_GEN_{Guid.NewGuid():N}".Substring(0, 32);

            return new CandidateRule
            {
                RuleId = ruleId,
                RuleName = $"Observed Pattern: {pattern.Description}",
                Description = $"Generic rule induced from {pattern.Occurrences} pattern observations",
                Category = pattern.Category ?? "General",
                Domain = pattern.Domain,
                TemplateId = "GENERIC",
                Condition = $"PatternType={pattern.PatternType}",
                Conclusion = $"Apply pattern: {pattern.Description}",
                InitialConfidence = pattern.Confidence * 0.6f,
                SupportingObservations = pattern.Occurrences,
                CreatedAt = DateTime.UtcNow,
                Status = CandidateRuleStatus.PendingValidation,
                SourcePatternKey = pattern.PatternKey,
                ContextEvidence = pattern.ContextSnapshots?.Take(5).ToList()
                    ?? new List<Dictionary<string, object>>()
            };
        }

        private string ExtractCondition(PatternObservation pattern, RuleTemplate template)
        {
            // Build condition from template and pattern context
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(template.ConditionNodeType))
            {
                parts.Add($"NodeType={template.ConditionNodeType}");
            }

            if (!string.IsNullOrEmpty(template.ConditionProperty))
            {
                parts.Add($"HasProperty({template.ConditionProperty})");
            }

            // Add context-derived conditions
            if (pattern.ContextSnapshots != null && pattern.ContextSnapshots.Count > 0)
            {
                var commonKeys = pattern.ContextSnapshots
                    .SelectMany(c => c.Keys)
                    .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() >= pattern.ContextSnapshots.Count * 0.8)
                    .Select(g => g.Key)
                    .Take(3);

                foreach (var key in commonKeys)
                {
                    var commonValue = pattern.ContextSnapshots
                        .Where(c => c.ContainsKey(key))
                        .Select(c => c[key]?.ToString())
                        .GroupBy(v => v)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();

                    if (commonValue != null && commonValue.Count() >= pattern.ContextSnapshots.Count * 0.6)
                    {
                        parts.Add($"{key}={commonValue.Key}");
                    }
                }
            }

            return parts.Count > 0 ? string.Join(" AND ", parts) : pattern.PatternType;
        }

        private string ExtractConclusion(PatternObservation pattern, RuleTemplate template)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(template.ConclusionRelationType))
            {
                parts.Add($"Relationship={template.ConclusionRelationType}");
            }

            if (!string.IsNullOrEmpty(template.ConclusionTarget))
            {
                parts.Add($"Target={template.ConclusionTarget}");
            }

            if (parts.Count == 0)
            {
                parts.Add(pattern.Description);
            }

            return string.Join(", ", parts);
        }

        #endregion

        #region Private Methods - Validation

        private ValidationCheck ValidateAgainstBuildingCodes(CandidateRule rule)
        {
            var check = new ValidationCheck
            {
                CheckName = "BuildingCodeCompliance",
                Passed = true,
                Confidence = 1.0f
            };

            // Check if the rule contradicts any known building code reference
            foreach (var codeRef in _buildingCodeReferences)
            {
                if (!IsRuleRelevantToCode(rule, codeRef))
                    continue;

                if (DoesRuleContradictCode(rule, codeRef))
                {
                    check.Passed = false;
                    check.Confidence = 0f;
                    check.Details = $"Rule contradicts {codeRef.CodeName} section {codeRef.Section}: {codeRef.Requirement}";
                    Logger.Warn("Rule {0} contradicts {1}: {2}", rule.RuleName, codeRef.CodeName, codeRef.Requirement);
                    break;
                }

                // If rule aligns with a code, boost confidence
                if (DoesRuleAlignWithCode(rule, codeRef))
                {
                    check.Confidence = Math.Min(1.0f, check.Confidence + 0.1f);
                    check.Details = $"Aligns with {codeRef.CodeName} section {codeRef.Section}";
                }
            }

            return check;
        }

        private ValidationCheck ValidateLogicalConsistency(CandidateRule rule)
        {
            var check = new ValidationCheck
            {
                CheckName = "LogicalConsistency",
                Passed = true,
                Confidence = 0.8f
            };

            // Check for self-referencing rules
            if (string.Equals(rule.Condition, rule.Conclusion, StringComparison.OrdinalIgnoreCase))
            {
                check.Passed = false;
                check.Details = "Rule is self-referencing (condition equals conclusion)";
                check.Confidence = 0f;
                return check;
            }

            // Check for circular dependencies with existing rules
            var existingRules = _learnedRules.Values
                .Where(r => r.Status == LearnedRuleStatus.Active)
                .ToList();

            foreach (var existing in existingRules)
            {
                if (string.Equals(rule.Conclusion, existing.Condition, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Conclusion, rule.Condition, StringComparison.OrdinalIgnoreCase))
                {
                    check.Passed = false;
                    check.Details = $"Circular dependency detected with rule {existing.RuleId}";
                    check.Confidence = 0f;
                    return check;
                }
            }

            // Check for tautologies
            if (rule.Condition.Contains(rule.Conclusion) || rule.Conclusion.Contains(rule.Condition))
            {
                check.Confidence *= 0.7f;
                check.Details = "Possible tautological relationship detected";
            }

            return check;
        }

        private ValidationCheck ValidateNoConflictWithExisting(CandidateRule rule)
        {
            var check = new ValidationCheck
            {
                CheckName = "NoExistingConflict",
                Passed = true,
                Confidence = 0.9f
            };

            var existingRules = _learnedRules.Values
                .Where(r => r.Status == LearnedRuleStatus.Active)
                .ToList();

            foreach (var existing in existingRules)
            {
                // Same condition but different conclusion = conflict
                if (string.Equals(rule.Condition, existing.Condition, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(rule.Conclusion, existing.Conclusion, StringComparison.OrdinalIgnoreCase))
                {
                    // Not necessarily a hard conflict; might be an alternative
                    check.Confidence *= 0.7f;
                    check.Details = $"Alternative conclusion to existing rule {existing.RuleId}: " +
                        $"same condition '{rule.Condition}' but different outcome";
                }
            }

            if (check.Confidence < 0.5f)
            {
                check.Passed = false;
                check.Details = "Too many conflicts with existing rules";
            }

            return check;
        }

        private ValidationCheck ValidateSpecificity(CandidateRule rule)
        {
            var check = new ValidationCheck
            {
                CheckName = "Specificity",
                Passed = true,
                Confidence = 0.7f
            };

            // Very short conditions are likely too generic
            if (rule.Condition.Length < 10)
            {
                check.Confidence *= 0.5f;
                check.Details = "Condition may be too generic";
            }

            // Rules with specific node types and properties are more specific
            if (!string.IsNullOrEmpty(rule.ConditionNodeType) && !string.IsNullOrEmpty(rule.ConditionProperty))
            {
                check.Confidence = Math.Min(1.0f, check.Confidence + 0.2f);
            }

            check.Passed = check.Confidence >= 0.4f;
            return check;
        }

        private ValidationCheck ValidateEvidenceStrength(CandidateRule rule)
        {
            var check = new ValidationCheck
            {
                CheckName = "EvidenceStrength",
                Passed = true,
                Confidence = 0.6f
            };

            // More supporting observations = stronger evidence
            if (rule.SupportingObservations >= 20)
            {
                check.Confidence = 0.95f;
                check.Details = $"Strong evidence: {rule.SupportingObservations} observations";
            }
            else if (rule.SupportingObservations >= 10)
            {
                check.Confidence = 0.8f;
                check.Details = $"Good evidence: {rule.SupportingObservations} observations";
            }
            else if (rule.SupportingObservations >= _minPatternOccurrences)
            {
                check.Confidence = 0.6f;
                check.Details = $"Sufficient evidence: {rule.SupportingObservations} observations";
            }
            else
            {
                check.Passed = false;
                check.Confidence = 0.3f;
                check.Details = $"Insufficient evidence: only {rule.SupportingObservations} observations";
            }

            return check;
        }

        private ValidationCheck ValidateDomainConstraints(CandidateRule rule)
        {
            var check = new ValidationCheck
            {
                CheckName = "DomainConstraints",
                Passed = true,
                Confidence = 0.8f
            };

            switch (rule.Domain)
            {
                case RuleDomain.Structural:
                    // Structural rules must reference load, span, depth, or connection concepts
                    if (!ContainsStructuralConcept(rule))
                    {
                        check.Confidence *= 0.5f;
                        check.Details = "Structural rule lacks structural engineering concepts";
                    }
                    break;

                case RuleDomain.MEP:
                    // MEP rules should reference sizing, flow, velocity, or load concepts
                    if (!ContainsMEPConcept(rule))
                    {
                        check.Confidence *= 0.5f;
                        check.Details = "MEP rule lacks mechanical/electrical/plumbing concepts";
                    }
                    break;

                case RuleDomain.FireSafety:
                    // Fire safety rules should reference egress, rating, sprinkler, or compartment
                    if (!ContainsFireSafetyConcept(rule))
                    {
                        check.Confidence *= 0.5f;
                        check.Details = "Fire safety rule lacks fire/life safety concepts";
                    }
                    break;

                case RuleDomain.Accessibility:
                    // Accessibility rules should reference ADA, clearance, ramp, or width
                    if (!ContainsAccessibilityConcept(rule))
                    {
                        check.Confidence *= 0.5f;
                        check.Details = "Accessibility rule lacks ADA/accessibility concepts";
                    }
                    break;

                case RuleDomain.Spatial:
                    // Spatial rules should reference adjacency, circulation, or daylight
                    if (!ContainsSpatialConcept(rule))
                    {
                        check.Confidence *= 0.5f;
                        check.Details = "Spatial rule lacks spatial planning concepts";
                    }
                    break;

                case RuleDomain.Energy:
                    // Energy rules should reference insulation, HVAC, or lighting
                    if (!ContainsEnergyConcept(rule))
                    {
                        check.Confidence *= 0.5f;
                        check.Details = "Energy rule lacks energy performance concepts";
                    }
                    break;
            }

            check.Passed = check.Confidence >= 0.4f;
            return check;
        }

        private bool IsRuleRelevantToCode(CandidateRule rule, BuildingCodeReference codeRef)
        {
            return rule.Domain == codeRef.Domain ||
                   string.Equals(rule.Category, codeRef.Category, StringComparison.OrdinalIgnoreCase);
        }

        private bool DoesRuleContradictCode(CandidateRule rule, BuildingCodeReference codeRef)
        {
            // Check if the rule's conclusion contradicts the code requirement
            if (codeRef.ProhibitedConclusions != null)
            {
                return codeRef.ProhibitedConclusions
                    .Any(pc => rule.Conclusion.IndexOf(pc, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        private bool DoesRuleAlignWithCode(CandidateRule rule, BuildingCodeReference codeRef)
        {
            if (codeRef.AlignedConclusions != null)
            {
                return codeRef.AlignedConclusions
                    .Any(ac => rule.Conclusion.IndexOf(ac, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        private bool ContainsStructuralConcept(CandidateRule rule)
        {
            var concepts = new[] { "load", "span", "depth", "connection", "beam", "column", "foundation",
                "shear", "moment", "deflection", "reinforcement", "bearing", "brace", "footing" };
            var ruleText = $"{rule.Condition} {rule.Conclusion}".ToLowerInvariant();
            return concepts.Any(c => ruleText.Contains(c));
        }

        private bool ContainsMEPConcept(CandidateRule rule)
        {
            var concepts = new[] { "pipe", "duct", "velocity", "flow", "sizing", "electrical", "load",
                "hvac", "plumbing", "conduit", "cfm", "gpm", "ampere", "voltage", "pressure" };
            var ruleText = $"{rule.Condition} {rule.Conclusion}".ToLowerInvariant();
            return concepts.Any(c => ruleText.Contains(c));
        }

        private bool ContainsFireSafetyConcept(CandidateRule rule)
        {
            var concepts = new[] { "egress", "fire", "rating", "sprinkler", "compartment", "exit",
                "evacuation", "smoke", "alarm", "separation", "occupancy", "travel distance" };
            var ruleText = $"{rule.Condition} {rule.Conclusion}".ToLowerInvariant();
            return concepts.Any(c => ruleText.Contains(c));
        }

        private bool ContainsAccessibilityConcept(CandidateRule rule)
        {
            var concepts = new[] { "ada", "clearance", "ramp", "width", "accessibility", "wheelchair",
                "handrail", "slope", "threshold", "grab bar", "turning radius", "tactile" };
            var ruleText = $"{rule.Condition} {rule.Conclusion}".ToLowerInvariant();
            return concepts.Any(c => ruleText.Contains(c));
        }

        private bool ContainsSpatialConcept(CandidateRule rule)
        {
            var concepts = new[] { "adjacency", "circulation", "daylight", "proximity", "zoning",
                "orientation", "view", "access", "layout", "ratio", "proportion" };
            var ruleText = $"{rule.Condition} {rule.Conclusion}".ToLowerInvariant();
            return concepts.Any(c => ruleText.Contains(c));
        }

        private bool ContainsEnergyConcept(CandidateRule rule)
        {
            var concepts = new[] { "insulation", "r-value", "u-value", "hvac", "lighting", "energy",
                "climate", "thermal", "solar", "glazing", "efficiency", "lpd", "watt" };
            var ruleText = $"{rule.Condition} {rule.Conclusion}".ToLowerInvariant();
            return concepts.Any(c => ruleText.Contains(c));
        }

        #endregion

        #region Private Methods - Promotion and Retirement

        private List<LearnedRule> PromoteValidatedCandidates()
        {
            var promoted = new List<LearnedRule>();

            var validatedCandidates = _candidateRules.Values
                .Where(c => c.Status == CandidateRuleStatus.Validated &&
                           c.ValidationConfidence >= _activationConfidenceThreshold)
                .ToList();

            foreach (var candidate in validatedCandidates)
            {
                var learnedRule = new LearnedRule
                {
                    RuleId = candidate.RuleId,
                    RuleName = candidate.RuleName,
                    Description = candidate.Description,
                    Category = candidate.Category,
                    Domain = candidate.Domain,
                    Condition = candidate.Condition,
                    Conclusion = candidate.Conclusion,
                    ConditionNodeType = candidate.ConditionNodeType,
                    ConditionProperty = candidate.ConditionProperty,
                    ConclusionRelationType = candidate.ConclusionRelationType,
                    ConclusionTarget = candidate.ConclusionTarget,
                    Confidence = candidate.ValidationConfidence,
                    Status = LearnedRuleStatus.Active,
                    SourceTemplateId = candidate.TemplateId,
                    SourcePatternKey = candidate.SourcePatternKey,
                    SupportingObservations = candidate.SupportingObservations,
                    CreatedAt = candidate.CreatedAt,
                    ActivatedAt = DateTime.UtcNow,
                    Version = 1,
                    SuccessfulApplications = 0,
                    FailedApplications = 0,
                    TotalApplications = 0
                };

                if (_learnedRules.TryAdd(learnedRule.RuleId, learnedRule))
                {
                    candidate.Status = CandidateRuleStatus.Promoted;
                    promoted.Add(learnedRule);
                    _totalRulesLearned++;
                    _totalRulesActivated++;

                    // Record initial version
                    RecordVersion(learnedRule, "Initial activation");

                    Logger.Info("Rule promoted to active: {0} (confidence: {1:P1}, domain: {2})",
                        learnedRule.RuleName, learnedRule.Confidence, learnedRule.Domain);
                }
            }

            return promoted;
        }

        private List<LearnedRule> RetireUnderperformingRules()
        {
            var retired = new List<LearnedRule>();

            var candidates = _learnedRules.Values
                .Where(r => r.Status == LearnedRuleStatus.Active &&
                           r.TotalApplications >= _configuration.MinApplicationsBeforeRetirement &&
                           r.Confidence < _configuration.DeactivationThreshold)
                .ToList();

            foreach (var rule in candidates)
            {
                rule.Status = LearnedRuleStatus.Retired;
                rule.RetiredAt = DateTime.UtcNow;
                retired.Add(rule);
                _totalRulesRetired++;

                RecordVersion(rule, "Retired due to low confidence");

                Logger.Info("Rule retired: {0} (confidence: {1:P1}, applications: {2})",
                    rule.RuleName, rule.Confidence, rule.TotalApplications);
            }

            return retired;
        }

        #endregion

        #region Private Methods - Versioning

        private void RecordVersionIfSignificant(LearnedRule rule)
        {
            var history = GetRuleVersionHistory(rule.RuleId);
            var lastVersion = history.LastOrDefault();

            if (lastVersion == null ||
                Math.Abs(rule.Confidence - lastVersion.Confidence) >= 0.1f ||
                rule.TotalApplications - lastVersion.TotalApplications >= 10)
            {
                rule.Version++;
                RecordVersion(rule, "Confidence update");
            }
        }

        private void RecordVersion(LearnedRule rule, string changeDescription)
        {
            var version = new RuleVersion
            {
                RuleId = rule.RuleId,
                VersionNumber = rule.Version,
                Confidence = rule.Confidence,
                Status = rule.Status,
                TotalApplications = rule.TotalApplications,
                SuccessfulApplications = rule.SuccessfulApplications,
                FailedApplications = rule.FailedApplications,
                ChangeDescription = changeDescription,
                Timestamp = DateTime.UtcNow
            };

            var key = $"{version.RuleId}_v{version.VersionNumber}";
            _ruleVersionHistory.TryAdd(key, version);
        }

        #endregion

        #region Private Methods - Utilities

        private string GeneratePatternKey(LearnedPatternInput pattern)
        {
            return $"{pattern.PatternType}:{pattern.Key}:{pattern.Category}".ToLowerInvariant();
        }

        private RuleDomain ClassifyDomain(LearnedPatternInput pattern)
        {
            var text = $"{pattern.Description} {pattern.Category} {pattern.Key}".ToLowerInvariant();

            if (text.Contains("structur") || text.Contains("load") || text.Contains("beam") ||
                text.Contains("column") || text.Contains("foundation"))
                return RuleDomain.Structural;

            if (text.Contains("mep") || text.Contains("pipe") || text.Contains("duct") ||
                text.Contains("electr") || text.Contains("hvac") || text.Contains("plumb"))
                return RuleDomain.MEP;

            if (text.Contains("fire") || text.Contains("egress") || text.Contains("safety") ||
                text.Contains("sprinkler") || text.Contains("smoke"))
                return RuleDomain.FireSafety;

            if (text.Contains("ada") || text.Contains("accessib") || text.Contains("ramp") ||
                text.Contains("wheelchair") || text.Contains("clearance"))
                return RuleDomain.Accessibility;

            if (text.Contains("adjacen") || text.Contains("circulat") || text.Contains("daylight") ||
                text.Contains("spatial") || text.Contains("layout"))
                return RuleDomain.Spatial;

            if (text.Contains("energy") || text.Contains("insul") || text.Contains("thermal") ||
                text.Contains("solar") || text.Contains("climate"))
                return RuleDomain.Energy;

            return RuleDomain.General;
        }

        #endregion

        #region Initialization - Rule Templates

        private void InitializeRuleTemplates()
        {
            // Structural rule templates
            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "STRUCT_SPAN_DEPTH",
                Name = "Span-to-Depth Ratio",
                Description = "If [ElementType] has [Span], then [DepthRequirement]",
                ConditionNodeType = "StructuralElement",
                ConditionProperty = "Span",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "MinimumDepth",
                DefaultCategory = "Structural",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Structural },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.9f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "STRUCT_LOAD_PATH",
                Name = "Load Path Requirement",
                Description = "If [LoadBearingElement] supports [Load], then [ConnectionRequired]",
                ConditionNodeType = "LoadBearingElement",
                ConditionProperty = "AppliedLoad",
                ConclusionRelationType = "RequiresConnection",
                ConclusionTarget = "LoadPath",
                DefaultCategory = "Structural",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Structural },
                MinPatternConfidence = 0.6f,
                ConfidenceMultiplier = 0.85f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "STRUCT_CONNECTION",
                Name = "Connection Requirement",
                Description = "If [ElementA] meets [ElementB], then [ConnectionType] required",
                ConditionNodeType = "StructuralJoint",
                ConditionProperty = "JointType",
                ConclusionRelationType = "RequiresConnection",
                ConclusionTarget = "ConnectionDetail",
                DefaultCategory = "Structural",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Structural },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.9f
            });

            // MEP rule templates
            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "MEP_PIPE_SIZE",
                Name = "Pipe Sizing Rule",
                Description = "If [System] has [FlowRate], then [PipeSize] required",
                ConditionNodeType = "PipingSystem",
                ConditionProperty = "FlowRate",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "PipeDiameter",
                DefaultCategory = "MEP",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.MEP },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.85f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "MEP_DUCT_VELOCITY",
                Name = "Duct Velocity Limit",
                Description = "If [DuctType] serves [Space], then [MaxVelocity] applies",
                ConditionNodeType = "DuctSystem",
                ConditionProperty = "SpaceType",
                ConclusionRelationType = "LimitedBy",
                ConclusionTarget = "MaxVelocity",
                DefaultCategory = "MEP",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.MEP },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.9f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "MEP_ELEC_LOAD",
                Name = "Electrical Load Calculation",
                Description = "If [Space] has [Usage], then [ElectricalLoad] expected",
                ConditionNodeType = "ElectricalZone",
                ConditionProperty = "SpaceUsage",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "ConnectedLoad",
                DefaultCategory = "MEP",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.MEP },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.85f
            });

            // Fire safety rule templates
            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "FIRE_EGRESS",
                Name = "Egress Distance Rule",
                Description = "If [Occupancy] has [OccupantLoad], then [MaxTravelDistance]",
                ConditionNodeType = "OccupancyZone",
                ConditionProperty = "OccupantLoad",
                ConclusionRelationType = "LimitedBy",
                ConclusionTarget = "MaxTravelDistance",
                DefaultCategory = "FireSafety",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.FireSafety },
                MinPatternConfidence = 0.6f,
                ConfidenceMultiplier = 0.95f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "FIRE_RATING",
                Name = "Fire Rating Requirement",
                Description = "If [Assembly] separates [OccupancyA] from [OccupancyB], then [FireRating]",
                ConditionNodeType = "FireSeparation",
                ConditionProperty = "AdjacentOccupancies",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "FireResistanceRating",
                DefaultCategory = "FireSafety",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.FireSafety },
                MinPatternConfidence = 0.6f,
                ConfidenceMultiplier = 0.95f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "FIRE_SPRINKLER",
                Name = "Sprinkler Spacing Rule",
                Description = "If [HazardClassification] in [Space], then [SprinklerSpacing]",
                ConditionNodeType = "HazardZone",
                ConditionProperty = "HazardClass",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "MaxSprinklerSpacing",
                DefaultCategory = "FireSafety",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.FireSafety },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.9f
            });

            // Accessibility rule templates
            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "ADA_CLEARANCE",
                Name = "ADA Clearance Rule",
                Description = "If [Element] in [AccessibleRoute], then [MinClearance]",
                ConditionNodeType = "AccessibleElement",
                ConditionProperty = "ElementType",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "MinimumClearance",
                DefaultCategory = "Accessibility",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Accessibility },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.95f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "ADA_RAMP",
                Name = "Ramp Slope Limit",
                Description = "If [LevelChange] on [AccessibleRoute], then [MaxSlope]",
                ConditionNodeType = "LevelTransition",
                ConditionProperty = "HeightDifference",
                ConclusionRelationType = "LimitedBy",
                ConclusionTarget = "MaximumSlope",
                DefaultCategory = "Accessibility",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Accessibility },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.95f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "ADA_DOOR",
                Name = "Door Width Requirement",
                Description = "If [Door] on [AccessibleRoute], then [MinWidth]",
                ConditionNodeType = "AccessibleDoor",
                ConditionProperty = "RouteType",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "MinimumWidth",
                DefaultCategory = "Accessibility",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Accessibility },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.95f
            });

            // Spatial rule templates
            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "SPATIAL_ADJACENCY",
                Name = "Adjacency Preference",
                Description = "If [RoomTypeA] exists, then [RoomTypeB] should be adjacent",
                ConditionNodeType = "Room",
                ConditionProperty = "RoomType",
                ConclusionRelationType = "PreferAdjacent",
                ConclusionTarget = "AdjacentRoom",
                DefaultCategory = "Spatial",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Spatial },
                MinPatternConfidence = 0.4f,
                ConfidenceMultiplier = 0.8f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "SPATIAL_DAYLIGHT",
                Name = "Daylight Ratio Rule",
                Description = "If [RoomType] has [OccupancyType], then [MinDaylightRatio]",
                ConditionNodeType = "HabitableRoom",
                ConditionProperty = "OccupancyType",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "MinDaylightFactor",
                DefaultCategory = "Spatial",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Spatial },
                MinPatternConfidence = 0.4f,
                ConfidenceMultiplier = 0.85f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "SPATIAL_CIRCULATION",
                Name = "Circulation Requirement",
                Description = "If [Building] has [Floors], then [CirculationType] required",
                ConditionNodeType = "Building",
                ConditionProperty = "NumberOfFloors",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "VerticalCirculation",
                DefaultCategory = "Spatial",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Spatial },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.85f
            });

            // Energy rule templates
            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "ENERGY_INSULATION",
                Name = "Insulation Requirement by Climate Zone",
                Description = "If [ClimateZone] then [MinInsulationValue]",
                ConditionNodeType = "ThermalEnvelope",
                ConditionProperty = "ClimateZone",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "MinRValue",
                DefaultCategory = "Energy",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Energy },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.9f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "ENERGY_HVAC_SIZE",
                Name = "HVAC Sizing Rule",
                Description = "If [Space] has [ThermalLoad], then [HVACCapacity]",
                ConditionNodeType = "ThermalZone",
                ConditionProperty = "ThermalLoad",
                ConclusionRelationType = "Requires",
                ConclusionTarget = "HVACCapacity",
                DefaultCategory = "Energy",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Energy },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.85f
            });

            _ruleTemplates.Add(new RuleTemplate
            {
                TemplateId = "ENERGY_LPD",
                Name = "Lighting Power Density",
                Description = "If [SpaceType] then [MaxLPD]",
                ConditionNodeType = "LightingZone",
                ConditionProperty = "SpaceType",
                ConclusionRelationType = "LimitedBy",
                ConclusionTarget = "MaxLPD",
                DefaultCategory = "Energy",
                ApplicableDomains = new List<RuleDomain> { RuleDomain.Energy },
                MinPatternConfidence = 0.5f,
                ConfidenceMultiplier = 0.9f
            });

            Logger.Debug("Initialized {0} rule templates", _ruleTemplates.Count);
        }

        #endregion

        #region Initialization - Building Code References

        private void InitializeBuildingCodeReferences()
        {
            // Structural code references
            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ACI 318",
                Section = "9.3.1",
                Category = "Structural",
                Domain = RuleDomain.Structural,
                Requirement = "Minimum beam depth for non-prestressed members",
                ProhibitedConclusions = new List<string> { "NoDepthRequirement", "IgnoreDeflection" },
                AlignedConclusions = new List<string> { "MinimumDepth", "SpanDepthRatio", "DeflectionCheck" }
            });

            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ASCE 7",
                Section = "2.3",
                Category = "Structural",
                Domain = RuleDomain.Structural,
                Requirement = "Load combinations for strength design",
                ProhibitedConclusions = new List<string> { "IgnoreLoadCombination", "SingleLoadOnly" },
                AlignedConclusions = new List<string> { "LoadCombination", "FactoredLoad", "LoadPath" }
            });

            // MEP code references
            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ASHRAE 90.1",
                Section = "6.4",
                Category = "MEP",
                Domain = RuleDomain.MEP,
                Requirement = "HVAC equipment efficiency requirements",
                ProhibitedConclusions = new List<string> { "NoEfficiencyRequirement", "IgnoreASHRAE" },
                AlignedConclusions = new List<string> { "Efficiency", "COP", "SEER", "HVACCapacity" }
            });

            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ASHRAE 62.1",
                Section = "6.2",
                Category = "MEP",
                Domain = RuleDomain.MEP,
                Requirement = "Minimum ventilation rates for acceptable indoor air quality",
                ProhibitedConclusions = new List<string> { "NoVentilation", "IgnoreAirQuality" },
                AlignedConclusions = new List<string> { "VentilationRate", "CFM", "OutdoorAir", "AirChange" }
            });

            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "NEC 2023",
                Section = "220",
                Category = "MEP",
                Domain = RuleDomain.MEP,
                Requirement = "Branch circuit, feeder, and service load calculations",
                ProhibitedConclusions = new List<string> { "IgnoreLoadCalc", "NoCircuitProtection" },
                AlignedConclusions = new List<string> { "ConnectedLoad", "DemandFactor", "CircuitSize" }
            });

            // Fire safety code references
            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "IBC 2021",
                Section = "1017",
                Category = "FireSafety",
                Domain = RuleDomain.FireSafety,
                Requirement = "Exit access travel distance limitations",
                ProhibitedConclusions = new List<string> { "UnlimitedTravelDistance", "IgnoreEgress" },
                AlignedConclusions = new List<string> { "MaxTravelDistance", "EgressPath", "ExitAccess" }
            });

            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "NFPA 13",
                Section = "8.6",
                Category = "FireSafety",
                Domain = RuleDomain.FireSafety,
                Requirement = "Sprinkler spacing requirements for light hazard occupancies",
                ProhibitedConclusions = new List<string> { "NoSprinkler", "UnlimitedSpacing" },
                AlignedConclusions = new List<string> { "SprinklerSpacing", "Coverage", "MaxSprinklerSpacing" }
            });

            // Accessibility code references
            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ADA Standards",
                Section = "404.2.3",
                Category = "Accessibility",
                Domain = RuleDomain.Accessibility,
                Requirement = "Minimum clear width of doorways shall be 32 inches",
                ProhibitedConclusions = new List<string> { "NarrowDoor", "ReducedClearance" },
                AlignedConclusions = new List<string> { "MinimumWidth", "ClearWidth", "AccessibleDoor" }
            });

            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ADA Standards",
                Section = "405.2",
                Category = "Accessibility",
                Domain = RuleDomain.Accessibility,
                Requirement = "Running slope of ramp shall not be steeper than 1:12",
                ProhibitedConclusions = new List<string> { "SteepRamp", "IgnoreSlope" },
                AlignedConclusions = new List<string> { "MaximumSlope", "RampSlope", "1:12" }
            });

            // Energy code references
            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ASHRAE 90.1",
                Section = "5.5",
                Category = "Energy",
                Domain = RuleDomain.Energy,
                Requirement = "Opaque envelope insulation requirements by climate zone",
                ProhibitedConclusions = new List<string> { "NoInsulation", "IgnoreClimateZone" },
                AlignedConclusions = new List<string> { "MinRValue", "Insulation", "ClimateZone" }
            });

            _buildingCodeReferences.Add(new BuildingCodeReference
            {
                CodeName = "ASHRAE 90.1",
                Section = "9.6",
                Category = "Energy",
                Domain = RuleDomain.Energy,
                Requirement = "Lighting power density limits by space type",
                ProhibitedConclusions = new List<string> { "UnlimitedLighting", "IgnoreLPD" },
                AlignedConclusions = new List<string> { "MaxLPD", "LightingPower", "WattPerSqFt" }
            });

            Logger.Debug("Initialized {0} building code references", _buildingCodeReferences.Count);
        }

        #endregion
    }

    #endregion

    #region Rule Learning Types

    /// <summary>
    /// Input pattern from the learning system for rule induction.
    /// </summary>
    public class LearnedPatternInput
    {
        public string Key { get; set; }
        public string PatternType { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public float Confidence { get; set; }
        public int Occurrences { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    /// <summary>
    /// A pattern observation tracked by the rule learner.
    /// </summary>
    public class PatternObservation
    {
        public string PatternKey { get; set; }
        public string PatternType { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public RuleDomain Domain { get; set; }
        public int Occurrences { get; set; }
        public float Confidence { get; set; }
        public DateTime FirstObserved { get; set; }
        public DateTime LastObserved { get; set; }
        public bool HasInducedRule { get; set; }
        public List<Dictionary<string, object>> ContextSnapshots { get; set; }
    }

    /// <summary>
    /// A candidate rule awaiting validation before activation.
    /// </summary>
    public class CandidateRule
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public RuleDomain Domain { get; set; }
        public string TemplateId { get; set; }
        public string Condition { get; set; }
        public string Conclusion { get; set; }
        public string ConditionNodeType { get; set; }
        public string ConditionProperty { get; set; }
        public string ConclusionRelationType { get; set; }
        public string ConclusionTarget { get; set; }
        public float InitialConfidence { get; set; }
        public float ValidationConfidence { get; set; }
        public int SupportingObservations { get; set; }
        public DateTime CreatedAt { get; set; }
        public CandidateRuleStatus Status { get; set; }
        public string RejectionReason { get; set; }
        public string SourcePatternKey { get; set; }
        public List<Dictionary<string, object>> ContextEvidence { get; set; }
    }

    /// <summary>
    /// A validated and active learned rule.
    /// </summary>
    public class LearnedRule
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public RuleDomain Domain { get; set; }
        public string Condition { get; set; }
        public string Conclusion { get; set; }
        public string ConditionNodeType { get; set; }
        public string ConditionProperty { get; set; }
        public string ConclusionRelationType { get; set; }
        public string ConclusionTarget { get; set; }
        public float Confidence { get; set; }
        public LearnedRuleStatus Status { get; set; }
        public string SourceTemplateId { get; set; }
        public string SourcePatternKey { get; set; }
        public int SupportingObservations { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ActivatedAt { get; set; }
        public DateTime? RetiredAt { get; set; }
        public DateTime? LastApplied { get; set; }
        public int Version { get; set; }
        public int SuccessfulApplications { get; set; }
        public int FailedApplications { get; set; }
        public int TotalApplications { get; set; }
        public string LastFailureReason { get; set; }
    }

    /// <summary>
    /// A template for generating rules from observed patterns.
    /// Format: If [NodeType] has [Property], then [Relationship] to [Target].
    /// </summary>
    public class RuleTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ConditionNodeType { get; set; }
        public string ConditionProperty { get; set; }
        public string ConclusionRelationType { get; set; }
        public string ConclusionTarget { get; set; }
        public string DefaultCategory { get; set; }
        public string RequiredPatternType { get; set; }
        public List<RuleDomain> ApplicableDomains { get; set; }
        public float MinPatternConfidence { get; set; }
        public float ConfidenceMultiplier { get; set; }
    }

    /// <summary>
    /// A reference to a building code used for rule validation.
    /// </summary>
    public class BuildingCodeReference
    {
        public string CodeName { get; set; }
        public string Section { get; set; }
        public string Category { get; set; }
        public RuleDomain Domain { get; set; }
        public string Requirement { get; set; }
        public List<string> ProhibitedConclusions { get; set; }
        public List<string> AlignedConclusions { get; set; }
    }

    /// <summary>
    /// A snapshot of a rule at a point in time for version tracking.
    /// </summary>
    public class RuleVersion
    {
        public string RuleId { get; set; }
        public int VersionNumber { get; set; }
        public float Confidence { get; set; }
        public LearnedRuleStatus Status { get; set; }
        public int TotalApplications { get; set; }
        public int SuccessfulApplications { get; set; }
        public int FailedApplications { get; set; }
        public string ChangeDescription { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Result of a rule validation check.
    /// </summary>
    public class ValidationCheck
    {
        public string CheckName { get; set; }
        public bool Passed { get; set; }
        public float Confidence { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// Complete result of validating a candidate rule.
    /// </summary>
    public class RuleValidationResult
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public bool IsValid { get; set; }
        public float Confidence { get; set; }
        public string RejectionReason { get; set; }
        public DateTime ValidationTime { get; set; }
        public List<ValidationCheck> Checks { get; set; }
    }

    /// <summary>
    /// Result of a complete rule learning cycle.
    /// </summary>
    public class RuleLearningResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalPatternsProcessed { get; set; }
        public int TotalActiveRules { get; set; }
        public List<CandidateRule> NewCandidates { get; set; }
        public List<LearnedRule> PromotedRules { get; set; }
        public List<LearnedRule> RetiredRules { get; set; }
        public List<RuleValidationResult> ValidationResults { get; set; }
        public bool WasCancelled { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Statistics about the rule learning system.
    /// </summary>
    public class RuleLearnerStatistics
    {
        public int TotalRulesLearned { get; set; }
        public int TotalRulesActivated { get; set; }
        public int TotalRulesRetired { get; set; }
        public int CurrentActiveRules { get; set; }
        public int CurrentCandidateRules { get; set; }
        public int TotalPatternObservations { get; set; }
        public float AverageRuleConfidence { get; set; }
        public DateTime LastLearningCycle { get; set; }
        public Dictionary<string, int> RulesByCategory { get; set; }
        public Dictionary<string, int> RulesByDomain { get; set; }
    }

    /// <summary>
    /// Configuration for the rule learning engine.
    /// </summary>
    public class RuleLearnerConfiguration
    {
        public int MinPatternOccurrences { get; set; } = 5;
        public float ActivationConfidenceThreshold { get; set; } = 0.6f;
        public float DeactivationThreshold { get; set; } = 0.2f;
        public int MinApplicationsBeforeRetirement { get; set; } = 10;
        public int MaxCandidateRules { get; set; } = 500;
        public int MaxActiveRules { get; set; } = 200;
    }

    /// <summary>
    /// Persisted state of the rule learner.
    /// </summary>
    public class RuleLearnerState
    {
        public List<LearnedRule> LearnedRules { get; set; }
        public List<CandidateRule> CandidateRules { get; set; }
        public List<PatternObservation> PatternObservations { get; set; }
        public List<RuleVersion> RuleVersionHistory { get; set; }
        public int TotalRulesLearned { get; set; }
        public int TotalRulesActivated { get; set; }
        public int TotalRulesRetired { get; set; }
        public DateTime LastLearningCycle { get; set; }
        public DateTime SavedAt { get; set; }
    }

    /// <summary>
    /// Building-science rule domains.
    /// </summary>
    public enum RuleDomain
    {
        General,
        Structural,
        MEP,
        FireSafety,
        Accessibility,
        Spatial,
        Energy
    }

    /// <summary>
    /// Status of a candidate rule in the validation pipeline.
    /// </summary>
    public enum CandidateRuleStatus
    {
        PendingValidation,
        Validated,
        Rejected,
        Promoted
    }

    /// <summary>
    /// Status of a learned rule.
    /// </summary>
    public enum LearnedRuleStatus
    {
        Active,
        Suspended,
        Retired
    }

    #endregion
}
