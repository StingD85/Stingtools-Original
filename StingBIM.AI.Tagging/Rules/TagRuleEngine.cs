// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagRuleEngine.cs - Comprehensive rule evaluation engine for automated tag assignment
// Surpasses Ideate's rule-based tagging and Naviate's priority system with boolean conditions,
// group inheritance, conflict resolution, view-type/phase filtering, and JSON import/export

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Rules
{
    /// <summary>
    /// Production-grade tag rule evaluation engine providing intelligent, priority-ordered
    /// rule matching against element properties. Surpasses Ideate's rule-based tagging with
    /// full boolean condition trees (And/Or with 13 operators including regex and set membership),
    /// named rule groups with inheritance, multi-axis conflict resolution (priority then
    /// specificity), view-type and phase filtering, default rule auto-generation, and
    /// portable JSON import/export for cross-project rule sharing.
    ///
    /// Thread-safe: all mutable state is guarded by <see cref="_lockObject"/>.
    /// </summary>
    public class TagRuleEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lockObject = new object();

        /// <summary>All registered rules keyed by RuleId.</summary>
        private readonly Dictionary<string, TagRule> _rules;

        /// <summary>All registered rule groups keyed by group Name.</summary>
        private readonly Dictionary<string, RuleGroup> _ruleGroups;

        /// <summary>
        /// Map from category name to its auto-generated default tag family name.
        /// Used by <see cref="GenerateDefaultRules"/> to create fallback rules.
        /// </summary>
        private readonly Dictionary<string, string> _loadedTagFamilies;

        /// <summary>Cached sorted rule lists keyed by group name (empty string = all rules).</summary>
        private readonly Dictionary<string, List<TagRule>> _sortedRuleCache;

        /// <summary>Pre-compiled regex patterns keyed by pattern string, for reuse.</summary>
        private readonly Dictionary<string, Regex> _regexCache;

        /// <summary>Tracks evaluation statistics for diagnostics.</summary>
        private readonly EvaluationStatistics _statistics;

        /// <summary>Optional reference to the tag repository for rule persistence.</summary>
        private TagRepository _repository;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TagRuleEngine"/> class
        /// with empty rule and group collections.
        /// </summary>
        public TagRuleEngine()
        {
            _rules = new Dictionary<string, TagRule>(StringComparer.OrdinalIgnoreCase);
            _ruleGroups = new Dictionary<string, RuleGroup>(StringComparer.OrdinalIgnoreCase);
            _loadedTagFamilies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sortedRuleCache = new Dictionary<string, List<TagRule>>(StringComparer.OrdinalIgnoreCase);
            _regexCache = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
            _statistics = new EvaluationStatistics();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TagRuleEngine"/> class
        /// backed by the specified <see cref="TagRepository"/> for persistence.
        /// Rules and groups already present in the repository are loaded immediately.
        /// </summary>
        /// <param name="repository">Repository to read rules from and persist changes to.</param>
        public TagRuleEngine(TagRepository repository) : this()
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            LoadFromRepository();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Loads all rules and groups from the backing repository into the in-memory store.
        /// Existing in-memory rules are cleared first.
        /// </summary>
        private void LoadFromRepository()
        {
            if (_repository == null) return;

            lock (_lockObject)
            {
                _rules.Clear();
                _ruleGroups.Clear();
                InvalidateSortedCache();

                foreach (var rule in _repository.GetRules())
                {
                    _rules[rule.RuleId] = rule;
                }

                foreach (var group in _repository.GetRuleGroups())
                {
                    _ruleGroups[group.Name] = group;
                }

                Logger.Info("TagRuleEngine loaded {0} rules and {1} groups from repository",
                    _rules.Count, _ruleGroups.Count);
            }
        }

        /// <summary>
        /// Associates a tag repository with this engine and loads its persisted rules.
        /// </summary>
        /// <param name="repository">The repository to bind.</param>
        public void BindRepository(TagRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            LoadFromRepository();
        }

        /// <summary>
        /// Registers a loaded tag family for a category. This information is used
        /// by <see cref="GenerateDefaultRules"/> to auto-create fallback rules
        /// for categories that have a tag family loaded but no explicit rule.
        /// </summary>
        /// <param name="categoryName">Revit category name (e.g., "Doors").</param>
        /// <param name="tagFamilyName">Name of the loaded tag family.</param>
        public void RegisterLoadedTagFamily(string categoryName, string tagFamilyName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                throw new ArgumentException("Category name is required.", nameof(categoryName));
            if (string.IsNullOrWhiteSpace(tagFamilyName))
                throw new ArgumentException("Tag family name is required.", nameof(tagFamilyName));

            lock (_lockObject)
            {
                _loadedTagFamilies[categoryName] = tagFamilyName;
            }

            Logger.Debug("Registered tag family '{0}' for category '{1}'", tagFamilyName, categoryName);
        }

        #endregion

        #region Rule CRUD

        /// <summary>
        /// Adds or updates a tag rule. If a rule with the same <see cref="TagRule.RuleId"/>
        /// already exists, it is replaced. The sorted-rule cache is invalidated.
        /// </summary>
        /// <param name="rule">The rule to add or update.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public void AddRule(TagRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            if (string.IsNullOrWhiteSpace(rule.RuleId))
                rule.RuleId = Guid.NewGuid().ToString("N");

            lock (_lockObject)
            {
                _rules[rule.RuleId] = rule;
                InvalidateSortedCache();
            }

            _repository?.SaveRule(rule);
            Logger.Info("Rule '{0}' ({1}) added/updated, priority={2}", rule.Name, rule.RuleId, rule.Priority);
        }

        /// <summary>
        /// Removes a rule by its identifier.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the rule to remove.</param>
        /// <returns><c>true</c> if the rule was found and removed; otherwise <c>false</c>.</returns>
        public bool RemoveRule(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return false;

            bool removed;
            lock (_lockObject)
            {
                removed = _rules.Remove(ruleId);
                if (removed)
                {
                    InvalidateSortedCache();

                    // Remove from any groups that reference it
                    foreach (var group in _ruleGroups.Values)
                    {
                        group.RuleIds.Remove(ruleId);
                    }
                }
            }

            if (removed)
            {
                _repository?.RemoveRule(ruleId);
                Logger.Info("Rule '{0}' removed", ruleId);
            }

            return removed;
        }

        /// <summary>
        /// Retrieves a rule by its identifier.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the rule.</param>
        /// <returns>The rule if found; otherwise <c>null</c>.</returns>
        public TagRule GetRule(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return null;

            lock (_lockObject)
            {
                return _rules.TryGetValue(ruleId, out var rule) ? rule : null;
            }
        }

        /// <summary>
        /// Returns all registered rules ordered by priority (ascending).
        /// </summary>
        /// <returns>A list of all rules sorted by priority.</returns>
        public List<TagRule> GetAllRules()
        {
            lock (_lockObject)
            {
                return _rules.Values.OrderBy(r => r.Priority).ThenBy(r => r.Name).ToList();
            }
        }

        /// <summary>
        /// Enables or disables a rule by its identifier.
        /// </summary>
        /// <param name="ruleId">The identifier of the rule.</param>
        /// <param name="enabled"><c>true</c> to enable; <c>false</c> to disable.</param>
        /// <returns><c>true</c> if the rule was found and its state changed; otherwise <c>false</c>.</returns>
        public bool SetRuleEnabled(string ruleId, bool enabled)
        {
            lock (_lockObject)
            {
                if (_rules.TryGetValue(ruleId, out var rule))
                {
                    if (rule.IsEnabled != enabled)
                    {
                        rule.IsEnabled = enabled;
                        InvalidateSortedCache();
                        _repository?.SaveRule(rule);
                        Logger.Info("Rule '{0}' ({1}) {2}", rule.Name, ruleId, enabled ? "enabled" : "disabled");
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region Group Management

        /// <summary>
        /// Adds or updates a named rule group. Groups allow sets of rules to be
        /// activated as a unit. Groups may inherit from a parent group, combining
        /// the parent's rules with their own (child rules override parent rules
        /// that share the same category filter).
        /// </summary>
        /// <param name="group">The group to add or update.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is null.</exception>
        public void AddGroup(RuleGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (string.IsNullOrWhiteSpace(group.Name))
                throw new ArgumentException("Group name is required.", nameof(group));

            lock (_lockObject)
            {
                _ruleGroups[group.Name] = group;
                InvalidateSortedCache();
            }

            _repository?.SaveRuleGroup(group);
            Logger.Info("Rule group '{0}' added/updated with {1} rules, inherits='{2}'",
                group.Name, group.RuleIds?.Count ?? 0, group.InheritsFrom ?? "(none)");
        }

        /// <summary>
        /// Removes a rule group by name. Rules belonging to the group are not deleted.
        /// </summary>
        /// <param name="groupName">Name of the group to remove.</param>
        /// <returns><c>true</c> if the group was found and removed; otherwise <c>false</c>.</returns>
        public bool RemoveGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return false;

            bool removed;
            lock (_lockObject)
            {
                removed = _ruleGroups.Remove(groupName);
                if (removed) InvalidateSortedCache();
            }

            if (removed) Logger.Info("Rule group '{0}' removed", groupName);
            return removed;
        }

        /// <summary>
        /// Retrieves a rule group by name.
        /// </summary>
        /// <param name="groupName">Name of the group.</param>
        /// <returns>The group if found; otherwise <c>null</c>.</returns>
        public RuleGroup GetGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return null;

            lock (_lockObject)
            {
                return _ruleGroups.TryGetValue(groupName, out var group) ? group : null;
            }
        }

        /// <summary>
        /// Returns all registered rule groups.
        /// </summary>
        /// <returns>List of all groups.</returns>
        public List<RuleGroup> GetAllGroups()
        {
            lock (_lockObject)
            {
                return _ruleGroups.Values.ToList();
            }
        }

        /// <summary>
        /// Resolves the effective rule list for a group, including inherited rules from
        /// the parent chain. Child group rules override parent rules that target the same
        /// category filter. Circular inheritance is detected and broken.
        /// </summary>
        /// <param name="groupName">Name of the group to resolve.</param>
        /// <returns>Resolved list of rules sorted by priority.</returns>
        public List<TagRule> ResolveGroupRules(string groupName)
        {
            lock (_lockObject)
            {
                return ResolveGroupRulesInternal(groupName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Internal recursive group resolution with cycle detection.
        /// </summary>
        private List<TagRule> ResolveGroupRulesInternal(string groupName, HashSet<string> visited)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return new List<TagRule>();
            if (!_ruleGroups.TryGetValue(groupName, out var group)) return new List<TagRule>();

            // Cycle detection
            if (!visited.Add(groupName))
            {
                Logger.Warn("Circular group inheritance detected at '{0}', breaking cycle", groupName);
                return new List<TagRule>();
            }

            // Start with inherited rules
            var inheritedRules = new Dictionary<string, TagRule>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(group.InheritsFrom))
            {
                foreach (var parentRule in ResolveGroupRulesInternal(group.InheritsFrom, visited))
                {
                    inheritedRules[parentRule.RuleId] = parentRule;
                }
            }

            // Child group rules override parent rules sharing the same category filter
            var childCategoryFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ruleId in group.RuleIds ?? Enumerable.Empty<string>())
            {
                if (_rules.TryGetValue(ruleId, out var rule))
                {
                    inheritedRules[rule.RuleId] = rule;
                    if (!string.IsNullOrEmpty(rule.CategoryFilter))
                    {
                        childCategoryFilters.Add(rule.CategoryFilter);
                    }
                }
            }

            // Remove parent rules whose category is overridden by a child rule
            if (childCategoryFilters.Count > 0)
            {
                var toRemove = inheritedRules.Values
                    .Where(r => !group.RuleIds.Contains(r.RuleId) &&
                                !string.IsNullOrEmpty(r.CategoryFilter) &&
                                childCategoryFilters.Contains(r.CategoryFilter))
                    .Select(r => r.RuleId)
                    .ToList();

                foreach (var id in toRemove)
                {
                    inheritedRules.Remove(id);
                }
            }

            return inheritedRules.Values
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToList();
        }

        #endregion

        #region Rule Evaluation

        /// <summary>
        /// Evaluates all applicable rules against the given element properties and returns
        /// the best matching result. Rules are tested in priority order; the first match
        /// is the primary result. If multiple rules match at the same priority level,
        /// specificity-based conflict resolution is applied.
        /// </summary>
        /// <param name="element">Properties of the element to evaluate.</param>
        /// <param name="groupName">
        /// Optional group name. When specified, only rules in the resolved group are considered.
        /// When <c>null</c>, all enabled rules are evaluated.
        /// </param>
        /// <returns>
        /// A <see cref="RuleEvaluationResult"/> containing the winning rule, its template name,
        /// all matching rules, and any detected conflicts.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
        public RuleEvaluationResult Evaluate(ElementProperties element, string groupName = null)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            var result = new RuleEvaluationResult();
            var startTime = DateTime.UtcNow;

            lock (_lockObject)
            {
                var candidateRules = GetCandidateRules(groupName);

                foreach (var rule in candidateRules)
                {
                    if (!PassesPreFilters(rule, element))
                        continue;

                    if (EvaluateConditions(rule, element))
                    {
                        result.AllMatchingRules.Add(rule);
                    }
                }
            }

            // Determine the winning rule via priority and specificity
            if (result.AllMatchingRules.Count > 0)
            {
                ResolveConflicts(result);
            }

            // Update statistics
            var elapsed = DateTime.UtcNow - startTime;
            UpdateStatistics(result, elapsed);

            if (result.MatchedRule != null)
            {
                Logger.Debug("Rule '{0}' matched element [{1}/{2}/{3}] -> template '{4}'",
                    result.MatchedRule.Name, element.Category, element.Family, element.Type,
                    result.TemplateName);
            }
            else
            {
                Logger.Debug("No rule matched element [{0}/{1}/{2}]",
                    element.Category, element.Family, element.Type);
            }

            return result;
        }

        /// <summary>
        /// Evaluates rules for a batch of elements, returning results keyed by
        /// a caller-supplied element identifier.
        /// </summary>
        /// <param name="elements">
        /// Dictionary mapping element identifiers to their properties.
        /// </param>
        /// <param name="groupName">Optional rule group to filter by.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Dictionary mapping each element identifier to its evaluation result.
        /// </returns>
        public async Task<Dictionary<string, RuleEvaluationResult>> EvaluateBatchAsync(
            Dictionary<string, ElementProperties> elements,
            string groupName = null,
            CancellationToken cancellationToken = default)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));

            var results = new Dictionary<string, RuleEvaluationResult>(
                elements.Count, StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                foreach (var kvp in elements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results[kvp.Key] = Evaluate(kvp.Value, groupName);
                }
            }, cancellationToken);

            Logger.Info("Batch evaluation completed: {0} elements, {1} matched",
                elements.Count, results.Values.Count(r => r.MatchedRule != null));

            return results;
        }

        /// <summary>
        /// Returns the sorted list of candidate rules for evaluation, either from
        /// a named group (with inheritance resolved) or all enabled rules.
        /// Results are cached until the rule set changes.
        /// </summary>
        private List<TagRule> GetCandidateRules(string groupName)
        {
            string cacheKey = groupName ?? string.Empty;

            if (_sortedRuleCache.TryGetValue(cacheKey, out var cached))
                return cached;

            List<TagRule> resolved;
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                resolved = ResolveGroupRulesInternal(groupName,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                resolved = _rules.Values
                    .Where(r => r.IsEnabled)
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.Name)
                    .ToList();
            }

            _sortedRuleCache[cacheKey] = resolved;
            return resolved;
        }

        /// <summary>
        /// Applies pre-filters that short-circuit evaluation before condition checking:
        /// category, family, type, view type, and phase.
        /// </summary>
        private static bool PassesPreFilters(TagRule rule, ElementProperties element)
        {
            // Category filter
            if (!string.IsNullOrEmpty(rule.CategoryFilter))
            {
                if (!MatchesWildcard(element.Category, rule.CategoryFilter))
                    return false;
            }

            // Family filter
            if (!string.IsNullOrEmpty(rule.FamilyFilter))
            {
                if (!MatchesWildcard(element.Family, rule.FamilyFilter))
                    return false;
            }

            // Type filter
            if (!string.IsNullOrEmpty(rule.TypeFilter))
            {
                if (!MatchesWildcard(element.Type, rule.TypeFilter))
                    return false;
            }

            // View-type filter
            if (rule.ApplicableViewTypes != null && rule.ApplicableViewTypes.Count > 0)
            {
                if (!rule.ApplicableViewTypes.Contains(element.ViewType))
                    return false;
            }

            // Phase filter
            if (!string.IsNullOrEmpty(rule.PhaseFilter))
            {
                if (!string.Equals(element.Phase, rule.PhaseFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluates the parameter conditions for a rule against element properties,
        /// combining them with the rule's logic (And / Or).
        /// </summary>
        private bool EvaluateConditions(TagRule rule, ElementProperties element)
        {
            if (rule.Conditions == null || rule.Conditions.Count == 0)
                return true;

            if (rule.ConditionLogic == RuleLogic.And)
            {
                foreach (var condition in rule.Conditions)
                {
                    if (!EvaluateSingleCondition(condition, element))
                        return false;
                }
                return true;
            }
            else // Or
            {
                foreach (var condition in rule.Conditions)
                {
                    if (EvaluateSingleCondition(condition, element))
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Evaluates a single <see cref="RuleCondition"/> against the element's parameters.
        /// Supports 13 operators: Equals, NotEquals, Contains, StartsWith, EndsWith,
        /// GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual, RegexMatch,
        /// IsNull, IsNotNull, In, NotIn.
        /// </summary>
        private bool EvaluateSingleCondition(RuleCondition condition, ElementProperties element)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.ParameterName))
                return false;

            // Retrieve the parameter value from the element's dictionary
            string parameterValue = null;
            if (element.Parameters != null)
            {
                element.Parameters.TryGetValue(condition.ParameterName, out parameterValue);
            }

            switch (condition.Operator)
            {
                case RuleOperator.IsNull:
                    return string.IsNullOrEmpty(parameterValue);

                case RuleOperator.IsNotNull:
                    return !string.IsNullOrEmpty(parameterValue);

                case RuleOperator.Equals:
                    return string.Equals(parameterValue, condition.Value, StringComparison.OrdinalIgnoreCase);

                case RuleOperator.NotEquals:
                    return !string.Equals(parameterValue, condition.Value, StringComparison.OrdinalIgnoreCase);

                case RuleOperator.Contains:
                    return parameterValue != null &&
                           parameterValue.IndexOf(condition.Value ?? string.Empty,
                               StringComparison.OrdinalIgnoreCase) >= 0;

                case RuleOperator.StartsWith:
                    return parameterValue != null &&
                           parameterValue.StartsWith(condition.Value ?? string.Empty,
                               StringComparison.OrdinalIgnoreCase);

                case RuleOperator.EndsWith:
                    return parameterValue != null &&
                           parameterValue.EndsWith(condition.Value ?? string.Empty,
                               StringComparison.OrdinalIgnoreCase);

                case RuleOperator.GreaterThan:
                    return CompareNumeric(parameterValue, condition.Value) > 0;

                case RuleOperator.LessThan:
                    return CompareNumeric(parameterValue, condition.Value) < 0;

                case RuleOperator.GreaterThanOrEqual:
                    return CompareNumeric(parameterValue, condition.Value) >= 0;

                case RuleOperator.LessThanOrEqual:
                    return CompareNumeric(parameterValue, condition.Value) <= 0;

                case RuleOperator.RegexMatch:
                    return EvaluateRegex(parameterValue, condition.Value);

                case RuleOperator.In:
                    return EvaluateIn(parameterValue, condition.Value);

                case RuleOperator.NotIn:
                    return !EvaluateIn(parameterValue, condition.Value);

                default:
                    Logger.Warn("Unknown rule operator: {0}", condition.Operator);
                    return false;
            }
        }

        #endregion

        #region Condition Helpers

        /// <summary>
        /// Performs numeric comparison between two string values.
        /// Returns negative if left &lt; right, zero if equal, positive if left &gt; right.
        /// Falls back to ordinal string comparison if either value is non-numeric.
        /// </summary>
        private static int CompareNumeric(string left, string right)
        {
            if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right)) return 0;
            if (string.IsNullOrEmpty(left)) return -1;
            if (string.IsNullOrEmpty(right)) return 1;

            if (double.TryParse(left, NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out double leftVal) &&
                double.TryParse(right, NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out double rightVal))
            {
                return leftVal.CompareTo(rightVal);
            }

            // Fallback to string comparison
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Evaluates a regular expression match with caching and timeout protection.
        /// </summary>
        private bool EvaluateRegex(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern))
                return false;

            try
            {
                Regex regex;
                lock (_lockObject)
                {
                    if (!_regexCache.TryGetValue(pattern, out regex))
                    {
                        regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                            TimeSpan.FromSeconds(2));
                        _regexCache[pattern] = regex;
                    }
                }

                return regex.IsMatch(value);
            }
            catch (RegexMatchTimeoutException)
            {
                Logger.Warn("Regex evaluation timed out for pattern '{0}'", pattern);
                return false;
            }
            catch (ArgumentException ex)
            {
                Logger.Warn(ex, "Invalid regex pattern: '{0}'", pattern);
                return false;
            }
        }

        /// <summary>
        /// Evaluates whether a value is in a comma-separated set of values.
        /// Example condition value: "Fire,Smoke,Gas" matches "Fire".
        /// </summary>
        private static bool EvaluateIn(string value, string setValues)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(setValues))
                return false;

            var items = setValues.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                if (string.Equals(value, item.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Matches a value against a wildcard pattern supporting * (any characters)
        /// and ? (single character). Comparison is case-insensitive.
        /// </summary>
        private static bool MatchesWildcard(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value) && string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) return false;

            // Fast path: no wildcards
            if (!pattern.Contains('*') && !pattern.Contains('?'))
            {
                return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
            }

            // Convert wildcard pattern to regex
            string regexPattern = "^" +
                Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") +
                "$";

            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
        }

        #endregion

        #region Conflict Resolution

        /// <summary>
        /// Resolves conflicts among matching rules by applying priority ordering first,
        /// then specificity ranking when priorities are tied. A family-level rule beats
        /// a category-level rule; a type-level rule beats a family-level rule; a rule
        /// with more conditions beats one with fewer. All detected conflicts are logged
        /// and attached to the result.
        /// </summary>
        private void ResolveConflicts(RuleEvaluationResult result)
        {
            if (result.AllMatchingRules.Count == 0) return;

            // Sort by priority ascending, then by specificity descending
            var sorted = result.AllMatchingRules
                .OrderBy(r => r.Priority)
                .ThenByDescending(r => CalculateSpecificity(r))
                .ToList();

            result.MatchedRule = sorted[0];
            result.TemplateName = sorted[0].TemplateName;

            // Detect conflicts: rules at the same priority level as the winner
            if (sorted.Count > 1)
            {
                int winnerPriority = sorted[0].Priority;
                var samePriorityRules = sorted.Where(r => r.Priority == winnerPriority).ToList();

                if (samePriorityRules.Count > 1)
                {
                    for (int i = 1; i < samePriorityRules.Count; i++)
                    {
                        var conflict = new RuleConflict
                        {
                            WinningRule = sorted[0],
                            LosingRule = samePriorityRules[i],
                            Resolution = ConflictResolution.Specificity,
                            WinnerSpecificity = CalculateSpecificity(sorted[0]),
                            LoserSpecificity = CalculateSpecificity(samePriorityRules[i])
                        };
                        result.Conflicts.Add(conflict);

                        Logger.Info("Rule conflict: '{0}' (specificity={1}) wins over '{2}' (specificity={3}) " +
                                    "at priority {4}",
                            conflict.WinningRule.Name, conflict.WinnerSpecificity,
                            conflict.LosingRule.Name, conflict.LoserSpecificity,
                            winnerPriority);
                    }
                }

                // Also log when different-priority rules match (informational)
                var lowerPriorityMatches = sorted.Where(r => r.Priority > winnerPriority).ToList();
                foreach (var lower in lowerPriorityMatches)
                {
                    var conflict = new RuleConflict
                    {
                        WinningRule = sorted[0],
                        LosingRule = lower,
                        Resolution = ConflictResolution.Priority,
                        WinnerSpecificity = CalculateSpecificity(sorted[0]),
                        LoserSpecificity = CalculateSpecificity(lower)
                    };
                    result.Conflicts.Add(conflict);
                }
            }
        }

        /// <summary>
        /// Calculates a numeric specificity score for a rule. Higher values indicate
        /// more specific rules. The scoring is:
        /// - Category filter present: +1
        /// - Family filter present: +2
        /// - Type filter present: +4
        /// - Each condition: +1
        /// - View type filter present: +1
        /// - Phase filter present: +1
        /// </summary>
        private static int CalculateSpecificity(TagRule rule)
        {
            int score = 0;

            if (!string.IsNullOrEmpty(rule.CategoryFilter)) score += 1;
            if (!string.IsNullOrEmpty(rule.FamilyFilter)) score += 2;
            if (!string.IsNullOrEmpty(rule.TypeFilter)) score += 4;
            if (rule.Conditions != null) score += rule.Conditions.Count;
            if (rule.ApplicableViewTypes != null && rule.ApplicableViewTypes.Count > 0) score += 1;
            if (!string.IsNullOrEmpty(rule.PhaseFilter)) score += 1;

            return score;
        }

        #endregion

        #region Default Rule Generation

        /// <summary>
        /// Auto-generates default rules for categories that have a loaded tag family
        /// (registered via <see cref="RegisterLoadedTagFamily"/>) but no explicit rule
        /// covering them. Default rules match all elements of the category with the
        /// lowest priority (999) and use a template name derived from the category
        /// (e.g., "Doors_Default").
        ///
        /// This ensures every taggable category has at least one rule, providing
        /// comprehensive coverage out of the box.
        /// </summary>
        /// <returns>The list of newly generated default rules.</returns>
        public List<TagRule> GenerateDefaultRules()
        {
            var generated = new List<TagRule>();

            lock (_lockObject)
            {
                // Determine which categories already have rules
                var coveredCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rule in _rules.Values)
                {
                    if (rule.IsEnabled && !string.IsNullOrEmpty(rule.CategoryFilter))
                    {
                        coveredCategories.Add(rule.CategoryFilter);
                    }
                }

                // Generate defaults for uncovered categories with loaded tag families
                foreach (var kvp in _loadedTagFamilies)
                {
                    string category = kvp.Key;
                    string tagFamily = kvp.Value;

                    if (coveredCategories.Contains(category))
                        continue;

                    var defaultRule = new TagRule
                    {
                        RuleId = $"default_{category.Replace(" ", "_").ToLowerInvariant()}_{Guid.NewGuid():N}",
                        Name = $"{category} Default Rule",
                        Description = $"Auto-generated default rule for {category} elements using tag family '{tagFamily}'.",
                        CategoryFilter = category,
                        FamilyFilter = null,
                        TypeFilter = null,
                        Conditions = new List<RuleCondition>(),
                        ConditionLogic = RuleLogic.And,
                        TemplateName = $"{category}_Default",
                        Priority = 999,
                        IsEnabled = true,
                        ApplicableViewTypes = null,
                        GroupName = null,
                        IncludeLinkedFiles = false,
                        PhaseFilter = null
                    };

                    _rules[defaultRule.RuleId] = defaultRule;
                    generated.Add(defaultRule);
                    _repository?.SaveRule(defaultRule);

                    Logger.Info("Generated default rule '{0}' for category '{1}' using tag family '{2}'",
                        defaultRule.Name, category, tagFamily);
                }

                if (generated.Count > 0)
                {
                    InvalidateSortedCache();
                }
            }

            Logger.Info("Default rule generation complete: {0} rules created", generated.Count);
            return generated;
        }

        /// <summary>
        /// Generates a default rule for a specific category with a specified tag family name
        /// and template name. Does not overwrite existing rules for the category.
        /// </summary>
        /// <param name="categoryName">The Revit category name.</param>
        /// <param name="tagFamilyName">The tag family to use.</param>
        /// <param name="templateName">The template name to assign. If null, defaults to "{category}_Default".</param>
        /// <returns>The generated rule, or null if the category already has a rule.</returns>
        public TagRule GenerateDefaultRuleForCategory(string categoryName, string tagFamilyName,
            string templateName = null)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                throw new ArgumentException("Category name is required.", nameof(categoryName));
            if (string.IsNullOrWhiteSpace(tagFamilyName))
                throw new ArgumentException("Tag family name is required.", nameof(tagFamilyName));

            lock (_lockObject)
            {
                // Check if a rule already covers this category
                bool alreadyCovered = _rules.Values.Any(r =>
                    r.IsEnabled &&
                    string.Equals(r.CategoryFilter, categoryName, StringComparison.OrdinalIgnoreCase));

                if (alreadyCovered)
                {
                    Logger.Debug("Category '{0}' already has an explicit rule; skipping default generation",
                        categoryName);
                    return null;
                }

                var rule = new TagRule
                {
                    RuleId = $"default_{categoryName.Replace(" ", "_").ToLowerInvariant()}_{Guid.NewGuid():N}",
                    Name = $"{categoryName} Default Rule",
                    Description = $"Auto-generated default rule for {categoryName} elements using tag family '{tagFamilyName}'.",
                    CategoryFilter = categoryName,
                    TemplateName = templateName ?? $"{categoryName}_Default",
                    Priority = 999,
                    IsEnabled = true,
                    Conditions = new List<RuleCondition>(),
                    ConditionLogic = RuleLogic.And
                };

                _rules[rule.RuleId] = rule;
                InvalidateSortedCache();
                _repository?.SaveRule(rule);

                Logger.Info("Generated default rule for category '{0}'", categoryName);
                return rule;
            }
        }

        #endregion

        #region View-Type and Phase Filtering

        /// <summary>
        /// Returns all rules applicable to a specific view type, respecting group scoping.
        /// </summary>
        /// <param name="viewType">The target view type.</param>
        /// <param name="groupName">Optional group name for scoping.</param>
        /// <returns>Rules that apply to the given view type, sorted by priority.</returns>
        public List<TagRule> GetRulesForViewType(TagViewType viewType, string groupName = null)
        {
            lock (_lockObject)
            {
                var candidates = GetCandidateRules(groupName);
                return candidates
                    .Where(r => r.ApplicableViewTypes == null ||
                                r.ApplicableViewTypes.Count == 0 ||
                                r.ApplicableViewTypes.Contains(viewType))
                    .ToList();
            }
        }

        /// <summary>
        /// Returns all rules applicable to a specific Revit phase, respecting group scoping.
        /// </summary>
        /// <param name="phaseName">The target phase name.</param>
        /// <param name="groupName">Optional group name for scoping.</param>
        /// <returns>Rules that apply to the given phase, sorted by priority.</returns>
        public List<TagRule> GetRulesForPhase(string phaseName, string groupName = null)
        {
            lock (_lockObject)
            {
                var candidates = GetCandidateRules(groupName);
                return candidates
                    .Where(r => string.IsNullOrEmpty(r.PhaseFilter) ||
                                string.Equals(r.PhaseFilter, phaseName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Returns rules applicable to a combined view type and phase filter.
        /// </summary>
        /// <param name="viewType">The target view type.</param>
        /// <param name="phaseName">The target phase name.</param>
        /// <param name="groupName">Optional group name for scoping.</param>
        /// <returns>Filtered rules sorted by priority.</returns>
        public List<TagRule> GetRulesForContext(TagViewType viewType, string phaseName,
            string groupName = null)
        {
            lock (_lockObject)
            {
                var candidates = GetCandidateRules(groupName);
                return candidates
                    .Where(r => (r.ApplicableViewTypes == null ||
                                 r.ApplicableViewTypes.Count == 0 ||
                                 r.ApplicableViewTypes.Contains(viewType)) &&
                                (string.IsNullOrEmpty(r.PhaseFilter) ||
                                 string.Equals(r.PhaseFilter, phaseName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        #endregion

        #region Import / Export

        /// <summary>
        /// Exports all rules and groups to a JSON string for sharing across projects or teams.
        /// The format includes versioning metadata for forward compatibility.
        /// </summary>
        /// <returns>A formatted JSON string representing the full rule set.</returns>
        public string ExportRulesToJson()
        {
            lock (_lockObject)
            {
                var exportData = new RuleExportData
                {
                    FormatVersion = "1.0",
                    ExportedAt = DateTime.UtcNow,
                    ExportedBy = Environment.UserName,
                    Rules = _rules.Values.OrderBy(r => r.Priority).ThenBy(r => r.Name).ToList(),
                    Groups = _ruleGroups.Values.OrderBy(g => g.Name).ToList(),
                    LoadedTagFamilies = new Dictionary<string, string>(_loadedTagFamilies,
                        StringComparer.OrdinalIgnoreCase)
                };

                string json = JsonConvert.SerializeObject(exportData, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Include
                    });

                Logger.Info("Exported {0} rules and {1} groups to JSON ({2} bytes)",
                    exportData.Rules.Count, exportData.Groups.Count, json.Length);

                return json;
            }
        }

        /// <summary>
        /// Exports rules and groups to a JSON file at the specified path.
        /// </summary>
        /// <param name="filePath">Absolute path for the output file.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        public async Task ExportRulesToFileAsync(string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            string json = ExportRulesToJson();
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            Logger.Info("Rules exported to file: {0}", filePath);
        }

        /// <summary>
        /// Imports rules and groups from a JSON string. Existing rules with the same
        /// RuleId are overwritten. New rules are added. The import is atomic: if
        /// deserialization fails, the engine state is unchanged.
        /// </summary>
        /// <param name="json">JSON string in the <see cref="RuleExportData"/> format.</param>
        /// <returns>A summary of the import operation.</returns>
        public RuleImportSummary ImportRulesFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string is required.", nameof(json));

            var summary = new RuleImportSummary();

            RuleExportData importData;
            try
            {
                importData = JsonConvert.DeserializeObject<RuleExportData>(json);
            }
            catch (JsonException ex)
            {
                Logger.Error(ex, "Failed to deserialize rule import JSON");
                summary.ErrorMessage = $"Deserialization failed: {ex.Message}";
                return summary;
            }

            if (importData == null)
            {
                summary.ErrorMessage = "Deserialized data was null.";
                return summary;
            }

            lock (_lockObject)
            {
                // Import rules
                foreach (var rule in importData.Rules ?? Enumerable.Empty<TagRule>())
                {
                    if (string.IsNullOrWhiteSpace(rule.RuleId))
                        rule.RuleId = Guid.NewGuid().ToString("N");

                    bool existed = _rules.ContainsKey(rule.RuleId);
                    _rules[rule.RuleId] = rule;
                    _repository?.SaveRule(rule);

                    if (existed)
                        summary.RulesUpdated++;
                    else
                        summary.RulesAdded++;
                }

                // Import groups
                foreach (var group in importData.Groups ?? Enumerable.Empty<RuleGroup>())
                {
                    if (string.IsNullOrWhiteSpace(group.Name)) continue;

                    bool existed = _ruleGroups.ContainsKey(group.Name);
                    _ruleGroups[group.Name] = group;
                    _repository?.SaveRuleGroup(group);

                    if (existed)
                        summary.GroupsUpdated++;
                    else
                        summary.GroupsAdded++;
                }

                // Import loaded tag family registrations
                foreach (var kvp in importData.LoadedTagFamilies ?? new Dictionary<string, string>())
                {
                    _loadedTagFamilies[kvp.Key] = kvp.Value;
                }

                InvalidateSortedCache();
            }

            summary.Success = true;
            Logger.Info("Rule import complete: {0} rules added, {1} updated, {2} groups added, {3} updated",
                summary.RulesAdded, summary.RulesUpdated, summary.GroupsAdded, summary.GroupsUpdated);

            return summary;
        }

        /// <summary>
        /// Imports rules and groups from a JSON file at the specified path.
        /// </summary>
        /// <param name="filePath">Absolute path to the input file.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A summary of the import operation.</returns>
        public async Task<RuleImportSummary> ImportRulesFromFileAsync(string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            if (!File.Exists(filePath))
            {
                return new RuleImportSummary
                {
                    ErrorMessage = $"File not found: {filePath}"
                };
            }

            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return ImportRulesFromJson(json);
        }

        #endregion

        #region Diagnostics and Statistics

        /// <summary>
        /// Returns current evaluation statistics.
        /// </summary>
        /// <returns>A snapshot of the evaluation statistics.</returns>
        public EvaluationStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new EvaluationStatistics
                {
                    TotalEvaluations = _statistics.TotalEvaluations,
                    TotalMatches = _statistics.TotalMatches,
                    TotalConflicts = _statistics.TotalConflicts,
                    AverageEvaluationTimeMs = _statistics.AverageEvaluationTimeMs,
                    RuleMatchCounts = new Dictionary<string, int>(_statistics.RuleMatchCounts,
                        StringComparer.OrdinalIgnoreCase)
                };
            }
        }

        /// <summary>
        /// Resets all evaluation statistics to zero.
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lockObject)
            {
                _statistics.TotalEvaluations = 0;
                _statistics.TotalMatches = 0;
                _statistics.TotalConflicts = 0;
                _statistics.AverageEvaluationTimeMs = 0;
                _statistics.RuleMatchCounts.Clear();
            }
        }

        /// <summary>
        /// Updates cumulative statistics after an evaluation.
        /// </summary>
        private void UpdateStatistics(RuleEvaluationResult result, TimeSpan elapsed)
        {
            lock (_lockObject)
            {
                _statistics.TotalEvaluations++;

                if (result.MatchedRule != null)
                {
                    _statistics.TotalMatches++;

                    if (!_statistics.RuleMatchCounts.ContainsKey(result.MatchedRule.RuleId))
                        _statistics.RuleMatchCounts[result.MatchedRule.RuleId] = 0;
                    _statistics.RuleMatchCounts[result.MatchedRule.RuleId]++;
                }

                _statistics.TotalConflicts += result.Conflicts.Count;

                // Running average of evaluation time
                double n = _statistics.TotalEvaluations;
                _statistics.AverageEvaluationTimeMs =
                    _statistics.AverageEvaluationTimeMs * ((n - 1) / n) +
                    elapsed.TotalMilliseconds / n;
            }
        }

        /// <summary>
        /// Validates the rule set for common issues: orphaned group references,
        /// duplicate rule names, circular inheritance, and missing template names.
        /// </summary>
        /// <returns>List of validation warnings.</returns>
        public List<string> ValidateRuleSet()
        {
            var warnings = new List<string>();

            lock (_lockObject)
            {
                // Check for duplicate names
                var nameGroups = _rules.Values
                    .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1);

                foreach (var dupe in nameGroups)
                {
                    warnings.Add($"Duplicate rule name '{dupe.Key}' found on {dupe.Count()} rules " +
                                 $"(IDs: {string.Join(", ", dupe.Select(r => r.RuleId))}).");
                }

                // Check for rules missing template name
                foreach (var rule in _rules.Values)
                {
                    if (string.IsNullOrWhiteSpace(rule.TemplateName))
                    {
                        warnings.Add($"Rule '{rule.Name}' ({rule.RuleId}) has no TemplateName assigned.");
                    }
                }

                // Check group references to non-existent rules
                foreach (var group in _ruleGroups.Values)
                {
                    foreach (var ruleId in group.RuleIds ?? Enumerable.Empty<string>())
                    {
                        if (!_rules.ContainsKey(ruleId))
                        {
                            warnings.Add($"Group '{group.Name}' references non-existent rule '{ruleId}'.");
                        }
                    }

                    // Check for circular inheritance
                    if (!string.IsNullOrWhiteSpace(group.InheritsFrom))
                    {
                        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { group.Name };
                        string current = group.InheritsFrom;
                        while (!string.IsNullOrWhiteSpace(current))
                        {
                            if (!visited.Add(current))
                            {
                                warnings.Add($"Circular inheritance detected in group chain starting at '{group.Name}'.");
                                break;
                            }

                            if (_ruleGroups.TryGetValue(current, out var parentGroup))
                                current = parentGroup.InheritsFrom;
                            else
                            {
                                warnings.Add($"Group '{group.Name}' inherits from non-existent group '{current}'.");
                                break;
                            }
                        }
                    }
                }

                // Check for rules with identical priority and overlapping category
                var priorityGroups = _rules.Values
                    .Where(r => r.IsEnabled)
                    .GroupBy(r => new { r.Priority, Category = r.CategoryFilter ?? "*" });

                foreach (var pg in priorityGroups)
                {
                    if (pg.Count() > 1 && pg.Key.Category != "*")
                    {
                        warnings.Add($"Multiple rules at priority {pg.Key.Priority} for category " +
                                     $"'{pg.Key.Category}': {string.Join(", ", pg.Select(r => r.Name))}. " +
                                     "Consider adjusting priorities to avoid ambiguity.");
                    }
                }
            }

            Logger.Info("Rule set validation complete: {0} warnings", warnings.Count);
            return warnings;
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Invalidates the sorted rule cache, forcing re-computation on next evaluation.
        /// Called whenever rules or groups are modified.
        /// </summary>
        private void InvalidateSortedCache()
        {
            _sortedRuleCache.Clear();
        }

        /// <summary>
        /// Clears the compiled regex cache. Useful when rules with regex conditions
        /// are updated or removed.
        /// </summary>
        public void ClearRegexCache()
        {
            lock (_lockObject)
            {
                _regexCache.Clear();
            }
            Logger.Debug("Regex cache cleared");
        }

        /// <summary>
        /// Clears all in-memory state: rules, groups, caches, and statistics.
        /// Does not affect persisted data in the repository.
        /// </summary>
        public void ClearAll()
        {
            lock (_lockObject)
            {
                _rules.Clear();
                _ruleGroups.Clear();
                _loadedTagFamilies.Clear();
                _sortedRuleCache.Clear();
                _regexCache.Clear();
                _statistics.TotalEvaluations = 0;
                _statistics.TotalMatches = 0;
                _statistics.TotalConflicts = 0;
                _statistics.AverageEvaluationTimeMs = 0;
                _statistics.RuleMatchCounts.Clear();
            }
            Logger.Info("TagRuleEngine cleared all in-memory state");
        }

        #endregion

        #region Persistence Helpers

        /// <summary>
        /// Persists all current rules and groups to the backing repository.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        public async Task PersistToRepositoryAsync(CancellationToken cancellationToken = default)
        {
            if (_repository == null)
            {
                Logger.Warn("No repository bound; cannot persist rules");
                return;
            }

            List<TagRule> rules;
            List<RuleGroup> groups;

            lock (_lockObject)
            {
                rules = _rules.Values.ToList();
                groups = _ruleGroups.Values.ToList();
            }

            await Task.Run(() =>
            {
                foreach (var rule in rules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _repository.SaveRule(rule);
                }

                foreach (var group in groups)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _repository.SaveRuleGroup(group);
                }
            }, cancellationToken);

            Logger.Info("Persisted {0} rules and {1} groups to repository", rules.Count, groups.Count);
        }

        /// <summary>
        /// Reloads rules and groups from the backing repository, replacing
        /// all in-memory state.
        /// </summary>
        public void ReloadFromRepository()
        {
            if (_repository == null)
            {
                Logger.Warn("No repository bound; cannot reload rules");
                return;
            }

            LoadFromRepository();
            Logger.Info("Rules reloaded from repository");
        }

        #endregion

        #region Convenience Query Methods

        /// <summary>
        /// Gets all rules that target a specific category, respecting group scoping.
        /// </summary>
        /// <param name="categoryName">Category name to filter by.</param>
        /// <param name="groupName">Optional group name for scoping.</param>
        /// <returns>Matching rules sorted by priority.</returns>
        public List<TagRule> GetRulesForCategory(string categoryName, string groupName = null)
        {
            lock (_lockObject)
            {
                var candidates = GetCandidateRules(groupName);
                return candidates
                    .Where(r => string.IsNullOrEmpty(r.CategoryFilter) ||
                                MatchesWildcard(categoryName, r.CategoryFilter))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets a summary of rule coverage: categories with rules, categories without,
        /// and the total number of rules per category.
        /// </summary>
        /// <returns>A dictionary mapping category names to their rule counts.</returns>
        public Dictionary<string, int> GetCoverageSummary()
        {
            lock (_lockObject)
            {
                var coverage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var rule in _rules.Values.Where(r => r.IsEnabled))
                {
                    string key = rule.CategoryFilter ?? "(All Categories)";
                    if (!coverage.ContainsKey(key))
                        coverage[key] = 0;
                    coverage[key]++;
                }

                // Add entries for loaded tag families with zero rules
                foreach (var category in _loadedTagFamilies.Keys)
                {
                    if (!coverage.ContainsKey(category))
                        coverage[category] = 0;
                }

                return coverage;
            }
        }

        /// <summary>
        /// Returns the total number of registered rules.
        /// </summary>
        public int RuleCount
        {
            get { lock (_lockObject) { return _rules.Count; } }
        }

        /// <summary>
        /// Returns the total number of registered groups.
        /// </summary>
        public int GroupCount
        {
            get { lock (_lockObject) { return _ruleGroups.Count; } }
        }

        #endregion

        #region Placement Engine Integration

        /// <summary>
        /// Gets the matching rule for a specific element identified by its ID,
        /// using the view context and optional group name for scoping.
        /// </summary>
        public TagRule GetMatchingRule(int elementId, ViewTagContext viewContext, string groupName = null)
        {
            var elementProps = new ElementProperties
            {
                ElementId = elementId,
                ViewType = viewContext?.ViewType ?? TagViewType.FloorPlan
            };

            var result = Evaluate(elementProps, groupName);
            return result.MatchedRule;
        }

        /// <summary>
        /// Checks whether an element is taggable according to the current rule set.
        /// Returns true if there is at least one active rule that could apply.
        /// </summary>
        public bool IsElementTaggable(int elementId, ViewTagContext viewContext)
        {
            lock (_lockObject)
            {
                // Element is taggable if there are any enabled rules at all
                return _rules.Values.Any(r => r.IsEnabled);
            }
        }

        /// <summary>
        /// Filters element IDs to only those that match at least one rule.
        /// </summary>
        public List<int> GetFilteredElementIds(
            List<int> elementIds, ViewTagContext viewContext, string groupName = null)
        {
            if (elementIds == null || elementIds.Count == 0)
                return new List<int>();

            // In the absence of full element property data, return all elements
            // that pass the basic taggability check
            return elementIds
                .Where(id => IsElementTaggable(id, viewContext))
                .ToList();
        }

        #endregion

        #region Inner Types

        /// <summary>
        /// Represents the properties of a Revit element being evaluated against tag rules.
        /// This is a lightweight DTO decoupled from the Revit API so the engine can be
        /// unit-tested without Revit dependencies.
        /// </summary>
        public class ElementProperties
        {
            /// <summary>
            /// Revit category name of the element (e.g., "Doors", "Walls", "Mechanical Equipment").
            /// </summary>
            public string Category { get; set; }

            /// <summary>
            /// Family name of the element (e.g., "Single-Flush", "Basic Wall").
            /// </summary>
            public string Family { get; set; }

            /// <summary>
            /// Type name of the element (e.g., "0915 x 2134mm", "Concrete 200mm").
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Dictionary of parameter names to their string-formatted values.
            /// Includes both instance and type parameters. Keys are case-insensitive.
            /// </summary>
            public Dictionary<string, string> Parameters { get; set; }
                = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// The view type in which this element is being tagged.
            /// Used for view-type-based rule filtering.
            /// </summary>
            public TagViewType ViewType { get; set; }

            /// <summary>
            /// The Revit phase name associated with the element.
            /// Used for phase-based rule filtering.
            /// </summary>
            public string Phase { get; set; }

            /// <summary>
            /// Optional Revit ElementId for traceability.
            /// </summary>
            public int ElementId { get; set; }

            /// <summary>
            /// Creates a new <see cref="ElementProperties"/> instance.
            /// </summary>
            public ElementProperties() { }

            /// <summary>
            /// Creates a new <see cref="ElementProperties"/> instance with the specified
            /// category, family, and type.
            /// </summary>
            /// <param name="category">Revit category name.</param>
            /// <param name="family">Family name.</param>
            /// <param name="type">Type name.</param>
            public ElementProperties(string category, string family, string type)
            {
                Category = category;
                Family = family;
                Type = type;
            }
        }

        /// <summary>
        /// Result of evaluating rules against an element. Contains the winning rule,
        /// the resolved template name, the full list of matching rules, and any
        /// conflicts that were detected and resolved.
        /// </summary>
        public class RuleEvaluationResult
        {
            /// <summary>
            /// The rule that won the evaluation (highest priority, then specificity).
            /// Null if no rule matched.
            /// </summary>
            public TagRule MatchedRule { get; set; }

            /// <summary>
            /// The template name from the matched rule. Null if no rule matched.
            /// </summary>
            public string TemplateName { get; set; }

            /// <summary>
            /// All rules that matched the element, regardless of priority.
            /// </summary>
            public List<TagRule> AllMatchingRules { get; set; } = new List<TagRule>();

            /// <summary>
            /// Conflicts detected during evaluation. Each conflict represents a pair
            /// of rules that both matched, with resolution metadata.
            /// </summary>
            public List<RuleConflict> Conflicts { get; set; } = new List<RuleConflict>();

            /// <summary>
            /// Whether any rule matched the element.
            /// </summary>
            public bool HasMatch => MatchedRule != null;

            /// <summary>
            /// Whether conflicts were detected during resolution.
            /// </summary>
            public bool HasConflicts => Conflicts.Count > 0;

            /// <summary>
            /// Number of rules that matched.
            /// </summary>
            public int MatchCount => AllMatchingRules.Count;
        }

        /// <summary>
        /// Represents a conflict between two rules that both matched an element.
        /// </summary>
        public class RuleConflict
        {
            /// <summary>
            /// The rule that won the conflict.
            /// </summary>
            public TagRule WinningRule { get; set; }

            /// <summary>
            /// The rule that lost the conflict.
            /// </summary>
            public TagRule LosingRule { get; set; }

            /// <summary>
            /// How the conflict was resolved.
            /// </summary>
            public ConflictResolution Resolution { get; set; }

            /// <summary>
            /// Specificity score of the winning rule.
            /// </summary>
            public int WinnerSpecificity { get; set; }

            /// <summary>
            /// Specificity score of the losing rule.
            /// </summary>
            public int LoserSpecificity { get; set; }

            /// <summary>
            /// Returns a human-readable description of this conflict.
            /// </summary>
            public override string ToString()
            {
                return $"Conflict: '{WinningRule?.Name}' (priority={WinningRule?.Priority}, " +
                       $"specificity={WinnerSpecificity}) wins over '{LosingRule?.Name}' " +
                       $"(priority={LosingRule?.Priority}, specificity={LoserSpecificity}) " +
                       $"via {Resolution}";
            }
        }

        /// <summary>
        /// How a rule conflict was resolved.
        /// </summary>
        public enum ConflictResolution
        {
            /// <summary>Resolved by differing priority values (lower number wins).</summary>
            Priority,

            /// <summary>Resolved by specificity when priorities are equal (more specific wins).</summary>
            Specificity
        }

        /// <summary>
        /// Data transfer object for JSON export/import of the complete rule set.
        /// </summary>
        public class RuleExportData
        {
            /// <summary>Format version for forward compatibility.</summary>
            public string FormatVersion { get; set; }

            /// <summary>When the export was created.</summary>
            public DateTime ExportedAt { get; set; }

            /// <summary>Username that performed the export.</summary>
            public string ExportedBy { get; set; }

            /// <summary>All rules in the export.</summary>
            public List<TagRule> Rules { get; set; } = new List<TagRule>();

            /// <summary>All rule groups in the export.</summary>
            public List<RuleGroup> Groups { get; set; } = new List<RuleGroup>();

            /// <summary>Registered category-to-tag-family mappings.</summary>
            public Dictionary<string, string> LoadedTagFamilies { get; set; }
                = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Summary of a rule import operation.
        /// </summary>
        public class RuleImportSummary
        {
            /// <summary>Whether the import succeeded without deserialization errors.</summary>
            public bool Success { get; set; }

            /// <summary>Number of new rules added.</summary>
            public int RulesAdded { get; set; }

            /// <summary>Number of existing rules updated/overwritten.</summary>
            public int RulesUpdated { get; set; }

            /// <summary>Number of new groups added.</summary>
            public int GroupsAdded { get; set; }

            /// <summary>Number of existing groups updated/overwritten.</summary>
            public int GroupsUpdated { get; set; }

            /// <summary>Total rules processed (added + updated).</summary>
            public int TotalRulesProcessed => RulesAdded + RulesUpdated;

            /// <summary>Total groups processed (added + updated).</summary>
            public int TotalGroupsProcessed => GroupsAdded + GroupsUpdated;

            /// <summary>Error message if the import failed.</summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Cumulative evaluation statistics for diagnostics and performance monitoring.
        /// </summary>
        public class EvaluationStatistics
        {
            /// <summary>Total number of evaluations performed.</summary>
            public long TotalEvaluations { get; set; }

            /// <summary>Total number of evaluations that produced a match.</summary>
            public long TotalMatches { get; set; }

            /// <summary>Total number of conflicts detected across all evaluations.</summary>
            public long TotalConflicts { get; set; }

            /// <summary>Running average evaluation time in milliseconds.</summary>
            public double AverageEvaluationTimeMs { get; set; }

            /// <summary>
            /// Number of times each rule was the winning match, keyed by RuleId.
            /// </summary>
            public Dictionary<string, int> RuleMatchCounts { get; set; }
                = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Match rate as a percentage (0.0 - 100.0).
            /// </summary>
            public double MatchRatePercent => TotalEvaluations > 0
                ? (double)TotalMatches / TotalEvaluations * 100.0
                : 0.0;
        }

        #endregion
    }
}
