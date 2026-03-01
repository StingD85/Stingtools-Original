// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagDependencyTracker.cs - Parameter change tracking and dependency alert system
// Monitors which parameters each tag depends on and alerts when those parameters
// change, potentially making tags stale. Supports cascade detection, impact analysis,
// batch processing with configurable debounce, and comprehensive dependency reporting.
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Intelligence;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Data
{
    #region Enums

    /// <summary>Policy controlling when stale tags are automatically refreshed.</summary>
    public enum AutoRefreshPolicy
    {
        Manual,
        OnDetection,
        OnViewOpen,
        Periodic
    }

    #endregion

    #region Inner Types

    /// <summary>Records the dependency between a tag and the parameters its expression references.</summary>
    public class TagDependency
    {
        public string TagId { get; set; }
        public int ElementId { get; set; }
        public List<string> ParameterNames { get; set; } = new List<string>();
        public string ContentExpression { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    /// <summary>Represents a detected change to a parameter value on a model element.</summary>
    public class ParameterChangeEvent
    {
        public int ElementId { get; set; }
        public string ParameterName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>Information about a tag whose displayed content no longer matches parameter values.</summary>
    public class StaleTagInfo
    {
        public string TagId { get; set; }
        public List<string> StaleParameterNames { get; set; } = new List<string>();
        public string CurrentDisplayText { get; set; }
        public string ExpectedDisplayText { get; set; }
        public DateTime StaleSince { get; set; }
        public int ViewId { get; set; }
    }

    /// <summary>Result of refreshing stale tags.</summary>
    public class RefreshResult
    {
        public int TagsRefreshed { get; set; }
        public int TagsFailed { get; set; }
        public List<string> ParametersMissing { get; set; } = new List<string>();
        public List<string> RefreshedTagIds { get; set; } = new List<string>();
        public Dictionary<string, string> FailedTagDetails { get; set; } = new Dictionary<string, string>();
        public TimeSpan Duration { get; set; }
    }

    /// <summary>Predicted impact of a proposed parameter change on existing tags.</summary>
    public class ImpactReport
    {
        public int AffectedTagCount { get; set; }
        public int AffectedViewCount { get; set; }
        public Dictionary<string, (string OldText, string NewText)> ChangedContent { get; set; }
            = new Dictionary<string, (string, string)>();
        public List<string> AffectedTagIds { get; set; } = new List<string>();
        public string AnalyzedParameter { get; set; }
        public string ProposedNewValue { get; set; }
    }

    /// <summary>Cascade chain from a root parameter through intermediates to terminal tags.</summary>
    public class CascadeChain
    {
        public string RootParameter { get; set; }
        public List<string> IntermediateParameters { get; set; } = new List<string>();
        public List<string> TerminalTags { get; set; } = new List<string>();
        public int Depth { get; set; }
    }

    /// <summary>Result of processing a batch of parameter changes.</summary>
    public class ChangeProcessingResult
    {
        public int ChangesProcessed { get; set; }
        public int TagsAffected { get; set; }
        public List<string> NewlyStaleTagIds { get; set; } = new List<string>();
        public List<string> AlreadyStaleTagIds { get; set; } = new List<string>();
        public List<string> AutoRefreshedTagIds { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
    }

    /// <summary>Comprehensive report of all tag dependencies in the project.</summary>
    public class DependencyReport
    {
        public int TotalDependencies { get; set; }
        public int TotalTrackedTags { get; set; }
        public int TotalReferencedParameters { get; set; }
        public List<KeyValuePair<string, int>> TopParameters { get; set; }
            = new List<KeyValuePair<string, int>>();
        public Dictionary<string, List<string>> OrphanedDependencies { get; set; }
            = new Dictionary<string, List<string>>();
        public List<List<string>> CircularDependencies { get; set; } = new List<List<string>>();
        public int CurrentlyStaleCount { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    #endregion

    #region Dependency Graph

    /// <summary>
    /// Bidirectional index of tag-to-parameter dependencies. Supports O(1) lookup
    /// in both directions and cascade edge tracking for computed parameters.
    /// </summary>
    public class DependencyGraph
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _graphLock = new object();

        // Forward: tagId -> dependency record
        private readonly Dictionary<string, TagDependency> _tagDeps =
            new Dictionary<string, TagDependency>(StringComparer.OrdinalIgnoreCase);

        // Reverse: "elementId:paramName" -> tag IDs (element-scoped lookup)
        private readonly Dictionary<string, HashSet<string>> _scopedIndex =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // Reverse: paramName -> tag IDs (cross-element lookup for cascade analysis)
        private readonly Dictionary<string, HashSet<string>> _nameIndex =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // Cascade: sourceParam -> derived params (formula/computed dependencies)
        private readonly Dictionary<string, HashSet<string>> _cascadeEdges =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public int Count { get { lock (_graphLock) { return _tagDeps.Count; } } }
        public int DistinctParameterCount { get { lock (_graphLock) { return _nameIndex.Count; } } }

        public void AddDependency(TagDependency dep)
        {
            if (dep == null) throw new ArgumentNullException(nameof(dep));
            if (string.IsNullOrEmpty(dep.TagId)) return;

            lock (_graphLock)
            {
                RemoveInternal(dep.TagId);
                _tagDeps[dep.TagId] = dep;

                foreach (string param in dep.ParameterNames)
                {
                    string key = $"{dep.ElementId}:{param}";
                    if (!_scopedIndex.ContainsKey(key))
                        _scopedIndex[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _scopedIndex[key].Add(dep.TagId);

                    if (!_nameIndex.ContainsKey(param))
                        _nameIndex[param] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _nameIndex[param].Add(dep.TagId);
                }
            }
        }

        public void RemoveDependency(string tagId)
        {
            lock (_graphLock) { RemoveInternal(tagId); }
        }

        public HashSet<string> GetDependentTags(int elementId, string parameterName)
        {
            lock (_graphLock)
            {
                string key = $"{elementId}:{parameterName}";
                return _scopedIndex.TryGetValue(key, out var tags)
                    ? new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public HashSet<string> GetDependentTagsByParameterName(string parameterName)
        {
            lock (_graphLock)
            {
                return _nameIndex.TryGetValue(parameterName, out var tags)
                    ? new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public TagDependency GetDependenciesForTag(string tagId)
        {
            lock (_graphLock)
            {
                return _tagDeps.TryGetValue(tagId, out var dep) ? dep : null;
            }
        }

        public List<TagDependency> GetAllDependencies()
        {
            lock (_graphLock) { return _tagDeps.Values.ToList(); }
        }

        public Dictionary<string, int> GetParameterDependencyCounts()
        {
            lock (_graphLock)
            {
                return _nameIndex.ToDictionary(
                    kvp => kvp.Key, kvp => kvp.Value.Count, StringComparer.OrdinalIgnoreCase);
            }
        }

        public void AddCascadeEdge(string source, string derived)
        {
            lock (_graphLock)
            {
                if (!_cascadeEdges.ContainsKey(source))
                    _cascadeEdges[source] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _cascadeEdges[source].Add(derived);
            }
        }

        public void RemoveCascadeEdge(string source, string derived)
        {
            lock (_graphLock)
            {
                if (_cascadeEdges.TryGetValue(source, out var set))
                {
                    set.Remove(derived);
                    if (set.Count == 0) _cascadeEdges.Remove(source);
                }
            }
        }

        public HashSet<string> GetDerivedParameters(string source)
        {
            lock (_graphLock)
            {
                return _cascadeEdges.TryGetValue(source, out var derived)
                    ? new HashSet<string>(derived, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public Dictionary<string, HashSet<string>> GetAllCascadeEdges()
        {
            lock (_graphLock)
            {
                return _cascadeEdges.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        public void Clear()
        {
            lock (_graphLock)
            {
                _tagDeps.Clear();
                _scopedIndex.Clear();
                _nameIndex.Clear();
                _cascadeEdges.Clear();
            }
        }

        private void RemoveInternal(string tagId)
        {
            if (!_tagDeps.TryGetValue(tagId, out var existing)) return;

            foreach (string param in existing.ParameterNames)
            {
                string key = $"{existing.ElementId}:{param}";
                if (_scopedIndex.TryGetValue(key, out var s))
                {
                    s.Remove(tagId);
                    if (s.Count == 0) _scopedIndex.Remove(key);
                }
                if (_nameIndex.TryGetValue(param, out var n))
                {
                    n.Remove(tagId);
                    if (n.Count == 0) _nameIndex.Remove(param);
                }
            }
            _tagDeps.Remove(tagId);
        }
    }

    #endregion

    /// <summary>
    /// Monitors parameter changes and tracks their impact on tags. Maintains a
    /// dependency graph linking tags to the parameters their content expressions
    /// reference, detects staleness, supports batch change processing with
    /// configurable debounce, provides impact analysis, traces cascade effects
    /// through computed parameters, and generates dependency reports.
    /// </summary>
    public class TagDependencyTracker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly DependencyGraph _graph;
        private readonly TagRepository _repository;
        private readonly TagContentGenerator _contentGenerator;
        private readonly object _staleLock = new object();
        private readonly object _debounceLock = new object();
        private readonly object _accumulatorLock = new object();

        private readonly Dictionary<string, StaleTagInfo> _staleTags =
            new Dictionary<string, StaleTagInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ParameterChangeEvent> _changeAccumulator = new List<ParameterChangeEvent>();
        private readonly HashSet<string> _knownParameters =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Timer _debounceTimer;
        private bool _debounceActive;
        private int _debounceIntervalMs;
        private int _maxCascadeDepth;
        private AutoRefreshPolicy _refreshPolicy;

        #region Constructor

        /// <summary>
        /// Initializes the tracker with required collaborators and configuration.
        /// </summary>
        /// <param name="repository">Tag repository for accessing tag data.</param>
        /// <param name="contentGenerator">Content generator for re-evaluating expressions.</param>
        /// <param name="debounceIntervalMs">Debounce window in ms (default 2000).</param>
        /// <param name="maxCascadeDepth">Maximum cascade depth (default 3).</param>
        /// <param name="refreshPolicy">Auto-refresh policy (default Manual).</param>
        public TagDependencyTracker(
            TagRepository repository,
            TagContentGenerator contentGenerator,
            int debounceIntervalMs = 2000,
            int maxCascadeDepth = 3,
            AutoRefreshPolicy refreshPolicy = AutoRefreshPolicy.Manual)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _contentGenerator = contentGenerator ?? throw new ArgumentNullException(nameof(contentGenerator));
            _graph = new DependencyGraph();
            _debounceIntervalMs = debounceIntervalMs;
            _maxCascadeDepth = maxCascadeDepth;
            _refreshPolicy = refreshPolicy;

            Logger.Info("TagDependencyTracker initialized: debounce={0}ms, cascade={1}, policy={2}",
                _debounceIntervalMs, _maxCascadeDepth, _refreshPolicy);
        }

        #endregion

        #region Properties

        public int DebounceIntervalMs
        {
            get => _debounceIntervalMs;
            set => _debounceIntervalMs = Math.Max(100, value);
        }

        public int MaxCascadeDepth
        {
            get => _maxCascadeDepth;
            set => _maxCascadeDepth = Math.Max(1, Math.Min(10, value));
        }

        public AutoRefreshPolicy RefreshPolicy
        {
            get => _refreshPolicy;
            set => _refreshPolicy = value;
        }

        public DependencyGraph Graph => _graph;

        public int StaleTagCount
        {
            get { lock (_staleLock) { return _staleTags.Count; } }
        }

        #endregion

        #region Dependency Registration

        /// <summary>
        /// Registers parameter dependencies for a tag by parsing its content expression
        /// to discover which parameters it references.
        /// </summary>
        public void RegisterTagDependencies(TagInstance tag, string contentExpression)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (string.IsNullOrWhiteSpace(contentExpression)) return;

            var referencedParams = ExtractParameterReferences(contentExpression);
            if (referencedParams.Count == 0) return;

            _graph.AddDependency(new TagDependency
            {
                TagId = tag.TagId,
                ElementId = tag.HostElementId,
                ParameterNames = referencedParams,
                ContentExpression = contentExpression,
                RegisteredAt = DateTime.UtcNow
            });

            Logger.Debug("Registered tag {0}: [{1}] on element {2}",
                tag.TagId, string.Join(", ", referencedParams), tag.HostElementId);
        }

        /// <summary>
        /// Unregisters all dependency tracking and staleness records for a tag.
        /// </summary>
        public void UnregisterTag(string tagId)
        {
            if (string.IsNullOrEmpty(tagId)) return;
            _graph.RemoveDependency(tagId);
            lock (_staleLock) { _staleTags.Remove(tagId); }
        }

        /// <summary>
        /// Registers a cascade edge: when sourceParameter changes, derivedParameter is recomputed.
        /// </summary>
        public void RegisterCascadeEdge(string sourceParameter, string derivedParameter)
        {
            _graph.AddCascadeEdge(sourceParameter, derivedParameter);
        }

        /// <summary>Registers parameter names known to exist in the model (for orphan detection).</summary>
        public void RegisterKnownParameter(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName)) return;
            lock (_staleLock) { _knownParameters.Add(parameterName); }
        }

        /// <summary>Registers multiple known parameter names at once.</summary>
        public void RegisterKnownParameters(IEnumerable<string> parameterNames)
        {
            if (parameterNames == null) return;
            lock (_staleLock)
            {
                foreach (string name in parameterNames)
                    if (!string.IsNullOrEmpty(name)) _knownParameters.Add(name);
            }
        }

        #endregion

        #region Change Detection

        /// <summary>
        /// Processes a single parameter change. Finds all dependent tags (including
        /// cascade-affected ones) and marks them as stale.
        /// </summary>
        /// <returns>List of affected tag IDs.</returns>
        public List<string> ProcessParameterChange(ParameterChangeEvent change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));

            var affected = new List<string>();

            // Direct dependencies
            var directTags = _graph.GetDependentTags(change.ElementId, change.ParameterName);
            affected.AddRange(directTags);

            // Cascade dependencies
            var cascadeParams = ResolveCascadeParameters(change.ParameterName, _maxCascadeDepth);
            foreach (string cp in cascadeParams)
            {
                foreach (string tagId in _graph.GetDependentTags(change.ElementId, cp))
                    if (!affected.Contains(tagId)) affected.Add(tagId);
            }

            foreach (string tagId in affected)
                MarkTagStale(tagId, change.ParameterName);

            if (affected.Count > 0)
                Logger.Info("Parameter {0}.{1} changed: {2} tags affected",
                    change.ElementId, change.ParameterName, affected.Count);

            return affected;
        }

        /// <summary>
        /// Processes a batch of parameter changes, deduplicating affected tags.
        /// </summary>
        public ChangeProcessingResult ProcessBatchChanges(List<ParameterChangeEvent> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            var startTime = DateTime.UtcNow;
            var result = new ChangeProcessingResult();
            var allAffected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Snapshot current stale set
            HashSet<string> previouslyStale;
            lock (_staleLock)
            {
                previouslyStale = new HashSet<string>(_staleTags.Keys, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var change in changes)
            {
                foreach (string tagId in ProcessParameterChange(change))
                    allAffected.Add(tagId);
                result.ChangesProcessed++;
            }

            foreach (string tagId in allAffected)
            {
                if (previouslyStale.Contains(tagId))
                    result.AlreadyStaleTagIds.Add(tagId);
                else
                    result.NewlyStaleTagIds.Add(tagId);
            }

            result.TagsAffected = allAffected.Count;

            // Auto-refresh if policy demands it
            if (_refreshPolicy == AutoRefreshPolicy.OnDetection && result.NewlyStaleTagIds.Count > 0)
                result.AutoRefreshedTagIds.AddRange(RefreshSpecificTags(result.NewlyStaleTagIds));

            result.Duration = DateTime.UtcNow - startTime;
            Logger.Info("Batch: {0} changes, {1} affected, {2} new stale, {3} auto-refreshed",
                result.ChangesProcessed, result.TagsAffected,
                result.NewlyStaleTagIds.Count, result.AutoRefreshedTagIds.Count);

            return result;
        }

        /// <summary>
        /// Submits a change to the debounce accumulator. Changes are held for the
        /// configured debounce interval, then batch-processed.
        /// </summary>
        public void AccumulateChange(ParameterChangeEvent change)
        {
            if (change == null) return;

            lock (_accumulatorLock) { _changeAccumulator.Add(change); }

            lock (_debounceLock)
            {
                if (!_debounceActive)
                {
                    _debounceActive = true;
                    _debounceTimer?.Dispose();
                    _debounceTimer = new Timer(
                        FlushAccumulator, null, _debounceIntervalMs, Timeout.Infinite);
                }
                else
                {
                    _debounceTimer?.Change(_debounceIntervalMs, Timeout.Infinite);
                }
            }
        }

        /// <summary>Immediately flushes accumulated changes without waiting for the debounce timer.</summary>
        public ChangeProcessingResult FlushAccumulatedChanges()
        {
            List<ParameterChangeEvent> pending;
            lock (_accumulatorLock)
            {
                if (_changeAccumulator.Count == 0) return null;
                pending = new List<ParameterChangeEvent>(_changeAccumulator);
                _changeAccumulator.Clear();
            }

            lock (_debounceLock)
            {
                _debounceActive = false;
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            return ProcessBatchChanges(pending);
        }

        private void FlushAccumulator(object state)
        {
            try { FlushAccumulatedChanges(); }
            catch (Exception ex) { Logger.Error(ex, "Error flushing accumulated changes"); }
        }

        #endregion

        #region Staleness Management

        /// <summary>Gets all currently stale tags.</summary>
        public List<StaleTagInfo> GetStaleTags()
        {
            lock (_staleLock) { return _staleTags.Values.ToList(); }
        }

        /// <summary>Checks whether a specific tag is currently stale.</summary>
        public bool IsTagStale(string tagId)
        {
            lock (_staleLock) { return _staleTags.ContainsKey(tagId); }
        }

        /// <summary>
        /// Re-evaluates content expressions for all stale tags and updates their display text.
        /// </summary>
        public async Task<RefreshResult> RefreshStaleTagsAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new RefreshResult();

            List<StaleTagInfo> staleTags;
            lock (_staleLock) { staleTags = _staleTags.Values.ToList(); }

            if (staleTags.Count == 0)
            {
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }

            Logger.Info("Refreshing {0} stale tags", staleTags.Count);

            foreach (var staleInfo in staleTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await RefreshSingleTagAsync(staleInfo, result))
                    {
                        result.TagsRefreshed++;
                        result.RefreshedTagIds.Add(staleInfo.TagId);
                        lock (_staleLock) { _staleTags.Remove(staleInfo.TagId); }
                    }
                    else
                    {
                        result.TagsFailed++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to refresh tag {0}", staleInfo.TagId);
                    result.TagsFailed++;
                    result.FailedTagDetails[staleInfo.TagId] = ex.Message;
                }
            }

            result.Duration = DateTime.UtcNow - startTime;
            Logger.Info("Refresh: {0} ok, {1} failed, {2} missing params, {3:F1}ms",
                result.TagsRefreshed, result.TagsFailed,
                result.ParametersMissing.Count, result.Duration.TotalMilliseconds);

            return result;
        }

        /// <summary>Clears stale status for a specific tag without refreshing it.</summary>
        public void ClearStaleStatus(string tagId)
        {
            lock (_staleLock) { _staleTags.Remove(tagId); }
        }

        /// <summary>Clears all staleness records.</summary>
        public void ClearAllStaleStatuses()
        {
            lock (_staleLock) { _staleTags.Clear(); }
        }

        private void MarkTagStale(string tagId, string parameterName)
        {
            var tag = _repository.GetTag(tagId);
            if (tag == null) return;

            lock (_staleLock)
            {
                if (_staleTags.TryGetValue(tagId, out var existing))
                {
                    if (!existing.StaleParameterNames.Contains(parameterName))
                        existing.StaleParameterNames.Add(parameterName);
                }
                else
                {
                    _staleTags[tagId] = new StaleTagInfo
                    {
                        TagId = tagId,
                        StaleParameterNames = new List<string> { parameterName },
                        CurrentDisplayText = tag.DisplayText,
                        StaleSince = DateTime.UtcNow,
                        ViewId = tag.ViewId
                    };
                }
            }

            if (tag.State == TagState.Active)
            {
                tag.State = TagState.Stale;
                _repository.UpdateTag(tag);
            }
        }

        private async Task<bool> RefreshSingleTagAsync(StaleTagInfo staleInfo, RefreshResult result)
        {
            var tag = _repository.GetTag(staleInfo.TagId);
            if (tag == null)
            {
                result.FailedTagDetails[staleInfo.TagId] = "Tag no longer exists";
                return false;
            }

            var dep = _graph.GetDependenciesForTag(staleInfo.TagId);
            if (dep == null || string.IsNullOrEmpty(dep.ContentExpression))
            {
                result.FailedTagDetails[staleInfo.TagId] = "No dependency record";
                return false;
            }

            var context = BuildEvaluationContext(tag);
            var contentResult = _contentGenerator.Evaluate(dep.ContentExpression, context);

            if (!contentResult.Success)
            {
                result.FailedTagDetails[staleInfo.TagId] =
                    $"Evaluation failed: {string.Join("; ", contentResult.Messages)}";
                return false;
            }

            foreach (string missing in contentResult.MissingParameters)
                if (!result.ParametersMissing.Contains(missing))
                    result.ParametersMissing.Add(missing);

            tag.DisplayText = contentResult.Text ?? string.Empty;
            tag.State = TagState.Active;
            tag.LastModified = DateTime.UtcNow;
            _repository.UpdateTag(tag);
            return true;
        }

        private List<string> RefreshSpecificTags(List<string> tagIds)
        {
            var refreshed = new List<string>();
            foreach (string tagId in tagIds)
            {
                try
                {
                    var tag = _repository.GetTag(tagId);
                    if (tag == null) continue;

                    var dep = _graph.GetDependenciesForTag(tagId);
                    if (dep == null || string.IsNullOrEmpty(dep.ContentExpression)) continue;

                    var context = BuildEvaluationContext(tag);
                    var contentResult = _contentGenerator.Evaluate(dep.ContentExpression, context);

                    if (contentResult.Success)
                    {
                        tag.DisplayText = contentResult.Text ?? string.Empty;
                        tag.State = TagState.Active;
                        tag.LastModified = DateTime.UtcNow;
                        _repository.UpdateTag(tag);
                        lock (_staleLock) { _staleTags.Remove(tagId); }
                        refreshed.Add(tagId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Auto-refresh failed for tag {0}", tagId);
                }
            }
            return refreshed;
        }

        private ContentEvaluationContext BuildEvaluationContext(TagInstance tag)
        {
            var context = new ContentEvaluationContext
            {
                ElementId = tag.HostElementId.ToString()
            };
            // In full Revit integration this queries the API; here we use tag metadata.
            if (tag.Metadata != null)
                foreach (var kvp in tag.Metadata)
                    context.Parameters[kvp.Key] = kvp.Value;
            return context;
        }

        #endregion

        #region Impact Analysis

        /// <summary>
        /// Predicts the impact of a proposed parameter change without applying it.
        /// Returns which tags would be affected and what their new content would be.
        /// </summary>
        public ImpactReport PredictImpact(int elementId, string parameterName, string newValue)
        {
            var report = new ImpactReport
            {
                AnalyzedParameter = parameterName,
                ProposedNewValue = newValue
            };

            // Collect all affected tag IDs (direct + cascade)
            var allAffected = new HashSet<string>(
                _graph.GetDependentTags(elementId, parameterName), StringComparer.OrdinalIgnoreCase);

            foreach (string cp in ResolveCascadeParameters(parameterName, _maxCascadeDepth))
                foreach (string tagId in _graph.GetDependentTags(elementId, cp))
                    allAffected.Add(tagId);

            var viewIds = new HashSet<int>();

            foreach (string tagId in allAffected)
            {
                var tag = _repository.GetTag(tagId);
                if (tag == null) continue;

                var dep = _graph.GetDependenciesForTag(tagId);
                if (dep == null || string.IsNullOrEmpty(dep.ContentExpression)) continue;

                // Build context with proposed value substituted
                var context = BuildEvaluationContext(tag);
                context.Parameters[parameterName] = newValue;

                var contentResult = _contentGenerator.Evaluate(dep.ContentExpression, context);
                string predicted = contentResult.Success ? contentResult.Text : "[evaluation error]";
                string current = tag.DisplayText ?? string.Empty;

                if (!string.Equals(current, predicted, StringComparison.Ordinal))
                    report.ChangedContent[tagId] = (current, predicted);

                report.AffectedTagIds.Add(tagId);
                viewIds.Add(tag.ViewId);
            }

            report.AffectedTagCount = allAffected.Count;
            report.AffectedViewCount = viewIds.Count;
            return report;
        }

        /// <summary>Predicts the combined impact of multiple proposed parameter changes.</summary>
        public ImpactReport PredictBatchImpact(List<ParameterChangeEvent> proposedChanges)
        {
            if (proposedChanges == null || proposedChanges.Count == 0)
                return new ImpactReport();

            var aggregated = new ImpactReport
            {
                AnalyzedParameter = $"[batch of {proposedChanges.Count}]",
                ProposedNewValue = "[multiple]"
            };

            var viewIds = new HashSet<int>();
            var tagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var change in proposedChanges)
            {
                var single = PredictImpact(change.ElementId, change.ParameterName, change.NewValue);
                foreach (string id in single.AffectedTagIds) tagIds.Add(id);
                foreach (var kvp in single.ChangedContent)
                    if (!aggregated.ChangedContent.ContainsKey(kvp.Key))
                        aggregated.ChangedContent[kvp.Key] = kvp.Value;
                foreach (string id in single.AffectedTagIds)
                {
                    var tag = _repository.GetTag(id);
                    if (tag != null) viewIds.Add(tag.ViewId);
                }
            }

            aggregated.AffectedTagIds = tagIds.ToList();
            aggregated.AffectedTagCount = tagIds.Count;
            aggregated.AffectedViewCount = viewIds.Count;
            return aggregated;
        }

        #endregion

        #region Cascade Tracker

        /// <summary>
        /// Traces the full cascade chain from a root parameter through intermediate
        /// computed parameters to the terminal tags that are ultimately affected.
        /// </summary>
        public CascadeChain TraceCascade(string rootParameter)
        {
            var chain = new CascadeChain { RootParameter = rootParameter };
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootParameter };
            var frontier = new Queue<string>();
            frontier.Enqueue(rootParameter);
            int depth = 0;

            while (frontier.Count > 0 && depth < _maxCascadeDepth)
            {
                int levelSize = frontier.Count;
                depth++;
                for (int i = 0; i < levelSize; i++)
                {
                    foreach (string derived in _graph.GetDerivedParameters(frontier.Dequeue()))
                        if (visited.Add(derived))
                        {
                            chain.IntermediateParameters.Add(derived);
                            frontier.Enqueue(derived);
                        }
                }
            }

            chain.Depth = depth;

            // Find terminal tags across all chain parameters
            var allParams = new HashSet<string>(chain.IntermediateParameters, StringComparer.OrdinalIgnoreCase);
            allParams.Add(rootParameter);

            var terminals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in allParams)
                foreach (string tagId in _graph.GetDependentTagsByParameterName(p))
                    terminals.Add(tagId);

            chain.TerminalTags = terminals.ToList();
            return chain;
        }

        /// <summary>
        /// Resolves all parameters transitively derived from a source, up to maxDepth.
        /// </summary>
        private HashSet<string> ResolveCascadeParameters(string source, int maxDepth)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source };
            var frontier = new Queue<string>();
            frontier.Enqueue(source);
            int depth = 0;

            while (frontier.Count > 0 && depth < maxDepth)
            {
                int levelSize = frontier.Count;
                depth++;
                for (int i = 0; i < levelSize; i++)
                    foreach (string derived in _graph.GetDerivedParameters(frontier.Dequeue()))
                        if (visited.Add(derived))
                        {
                            result.Add(derived);
                            frontier.Enqueue(derived);
                        }
            }
            return result;
        }

        #endregion

        #region Dependency Report

        /// <summary>
        /// Generates a comprehensive report: top parameters, orphaned dependencies,
        /// circular dependency detection, and current staleness metrics.
        /// </summary>
        public DependencyReport GenerateDependencyReport()
        {
            var report = new DependencyReport { GeneratedAt = DateTime.UtcNow };
            var allDeps = _graph.GetAllDependencies();

            report.TotalDependencies = allDeps.Sum(d => d.ParameterNames.Count);
            report.TotalTrackedTags = allDeps.Count;

            var paramCounts = _graph.GetParameterDependencyCounts();
            report.TotalReferencedParameters = paramCounts.Count;
            report.TopParameters = paramCounts
                .OrderByDescending(kvp => kvp.Value).Take(20).ToList();

            // Orphaned dependencies
            HashSet<string> knownCopy;
            lock (_staleLock) { knownCopy = new HashSet<string>(_knownParameters, StringComparer.OrdinalIgnoreCase); }

            if (knownCopy.Count > 0)
            {
                foreach (var dep in allDeps)
                {
                    var orphaned = dep.ParameterNames.Where(p => !knownCopy.Contains(p)).ToList();
                    if (orphaned.Count > 0)
                        report.OrphanedDependencies[dep.TagId] = orphaned;
                }
            }

            // Circular dependency detection via DFS
            report.CircularDependencies = DetectCircularDependencies();

            lock (_staleLock) { report.CurrentlyStaleCount = _staleTags.Count; }

            Logger.Info("Report: {0} deps, {1} tags, {2} params, {3} orphaned, {4} circular, {5} stale",
                report.TotalDependencies, report.TotalTrackedTags,
                report.TotalReferencedParameters, report.OrphanedDependencies.Count,
                report.CircularDependencies.Count, report.CurrentlyStaleCount);

            return report;
        }

        private List<List<string>> DetectCircularDependencies()
        {
            var cycles = new List<List<string>>();
            var allEdges = _graph.GetAllCascadeEdges();
            if (allEdges.Count == 0) return cycles;

            var allNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in allEdges)
            {
                allNodes.Add(kvp.Key);
                foreach (string t in kvp.Value) allNodes.Add(t);
            }

            var globalVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string start in allNodes)
            {
                if (globalVisited.Contains(start)) continue;
                var path = new List<string>();
                var pathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var localVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CycleDfs(start, allEdges, path, pathSet, localVisited, cycles);
                foreach (string v in localVisited) globalVisited.Add(v);
            }
            return cycles;
        }

        private void CycleDfs(
            string node, Dictionary<string, HashSet<string>> edges,
            List<string> path, HashSet<string> pathSet,
            HashSet<string> visited, List<List<string>> cycles)
        {
            if (pathSet.Contains(node))
            {
                int start = path.IndexOf(node);
                if (start >= 0)
                {
                    var cycle = new List<string>();
                    for (int i = start; i < path.Count; i++) cycle.Add(path[i]);
                    cycle.Add(node);
                    cycles.Add(cycle);
                }
                return;
            }

            if (visited.Contains(node)) return;

            visited.Add(node);
            path.Add(node);
            pathSet.Add(node);

            if (edges.TryGetValue(node, out var neighbors))
                foreach (string n in neighbors)
                    CycleDfs(n, edges, path, pathSet, visited, cycles);

            path.RemoveAt(path.Count - 1);
            pathSet.Remove(node);
        }

        #endregion

        #region Parameter Extraction

        /// <summary>
        /// Extracts all parameter names from a content expression: simple refs ({Param}),
        /// formatted ({Param:F2}), arithmetic ({W * H}), and conditionals.
        /// </summary>
        private List<string> ExtractParameterReferences(string expression)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(expression)) return names.ToList();

            foreach (Match match in Regex.Matches(expression, @"\{([^{}]+)\}"))
            {
                string inner = match.Groups[1].Value.Trim();

                // Skip special tokens
                if (string.Equals(inner, "CLUSTER_COUNT", StringComparison.OrdinalIgnoreCase)) continue;
                if (inner.StartsWith("UNIQUE_ID:", StringComparison.OrdinalIgnoreCase)) continue;

                // Conditional: IF cond THEN branch ELSE branch
                var cm = Regex.Match(inner, @"^IF\s+(.+?)\s+THEN\s+(.+?)\s+ELSE\s+(.+)$",
                    RegexOptions.IgnoreCase);
                if (cm.Success)
                {
                    ExtractFromCondition(cm.Groups[1].Value.Trim(), names);
                    ExtractFromBranch(cm.Groups[2].Value.Trim(), names);
                    ExtractFromBranch(cm.Groups[3].Value.Trim(), names);
                    continue;
                }

                // Arithmetic: operand op operand
                var am = Regex.Match(inner, @"^(.+?)\s*[+\-*/]\s*(.+?)(?::[A-Za-z]\d+)?$");
                if (am.Success)
                {
                    string left = am.Groups[1].Value.Trim();
                    string right = am.Groups[2].Value.Trim();
                    if (!IsNumeric(left)) names.Add(CleanParam(left));
                    if (!IsNumeric(right)) names.Add(CleanParam(right));
                    continue;
                }

                // Simple reference: ParamName or ParamName:Format
                var sm = Regex.Match(inner, @"^([A-Za-z_][A-Za-z0-9_ ]*?)(?::[A-Za-z]\d+)?$");
                if (sm.Success)
                {
                    names.Add(sm.Groups[1].Value.Trim());
                }
            }

            return names.ToList();
        }

        private void ExtractFromCondition(string condition, HashSet<string> names)
        {
            var m = Regex.Match(condition, @"^(\S+)\s*(!=|==|>=|<=|>|<)\s*(.+)$");
            if (m.Success)
            {
                string param = m.Groups[1].Value.Trim();
                if (!IsNumeric(param) && !IsQuoted(param)
                    && !string.Equals(param, "null", StringComparison.OrdinalIgnoreCase))
                    names.Add(param);
                return;
            }

            if (!IsNumeric(condition) && !IsQuoted(condition)
                && !string.Equals(condition, "null", StringComparison.OrdinalIgnoreCase))
                names.Add(condition.Trim());
        }

        private void ExtractFromBranch(string branch, HashSet<string> names)
        {
            if (string.IsNullOrEmpty(branch) || IsQuoted(branch)) return;

            if (branch.Contains("+"))
            {
                foreach (string part in branch.Split('+'))
                {
                    string t = part.Trim();
                    if (!string.IsNullOrEmpty(t) && !IsQuoted(t) && !IsNumeric(t))
                        names.Add(t);
                }
                return;
            }

            if (!IsNumeric(branch))
                names.Add(branch.Trim());
        }

        private static string CleanParam(string name)
        {
            int idx = name.IndexOf(':');
            return (idx > 0 ? name.Substring(0, idx) : name).Trim();
        }

        private static bool IsNumeric(string v) =>
            double.TryParse(v, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _);

        private static bool IsQuoted(string v) =>
            v.Length >= 2 && v.StartsWith("\"") && v.EndsWith("\"");

        #endregion

        #region Disposal

        /// <summary>Disposes the debounce timer and flushes accumulated changes.</summary>
        public void Dispose()
        {
            try { FlushAccumulatedChanges(); }
            catch (Exception ex) { Logger.Warn(ex, "Error during final flush on dispose"); }

            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }

        #endregion
    }
}
