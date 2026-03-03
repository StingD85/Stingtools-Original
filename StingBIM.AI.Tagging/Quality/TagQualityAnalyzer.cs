// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagQualityAnalyzer.cs - Comprehensive annotation quality assurance engine
// Surpasses Ideate Review's 8 issue types with deeper analysis and auto-fix capabilities
//
// Quality Check Types:
//   1. Clash       - Tag-to-tag and tag-to-annotation overlap detection
//   2. Orphan      - Tags whose host element no longer exists
//   3. Duplicate   - Same element tagged multiple times in one view
//   4. Blank       - Tags with empty, null, or "?" display text
//   5. Hidden      - Tags in Active state but not visible
//   6. Unexpected  - Tag text doesn't match expected format pattern
//   7. Misaligned  - Tags breaking alignment with neighbors
//   8. Stale       - Tag text doesn't match current parameter values

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Quality
{
    #region Dependency Interfaces

    /// <summary>
    /// Collision detection abstraction. Implemented by CollisionResolver in the Intelligence layer.
    /// </summary>
    public interface ICollisionResolver
    {
        /// <summary>Detects all collisions for a tag against other annotation bounds.</summary>
        List<TagCollision> DetectCollisions(
            TagInstance tag, List<TagBounds2D> otherBounds,
            List<string> otherIds, double clearance);

        /// <summary>Computes overlap percentage between two bounds relative to the smaller area.</summary>
        double ComputeOverlapPercentage(TagBounds2D a, TagBounds2D b);
    }

    /// <summary>
    /// Revit API bridge abstraction for element queries and tag modifications.
    /// </summary>
    public interface ITagCreator
    {
        /// <summary>Checks whether a Revit element still exists in the model.</summary>
        bool IsElementValid(int elementId);

        /// <summary>Checks whether a tag is visible in its view (filters, crop, overrides).</summary>
        bool IsTagVisible(int tagElementId, int viewId);

        /// <summary>Gets a parameter value for an element. Returns null if not found.</summary>
        string GetParameterValue(int elementId, string parameterName);

        /// <summary>Deletes a tag from the Revit model. Returns true on success.</summary>
        bool DeleteTag(int tagElementId);

        /// <summary>Updates tag display text in the Revit model. Returns true on success.</summary>
        bool UpdateTagText(int tagElementId, string newText);
    }

    /// <summary>
    /// Content expression evaluator abstraction for tag text generation.
    /// </summary>
    public interface IContentExpressionEvaluator
    {
        /// <summary>Evaluates a content expression for a host element. Returns null on failure.</summary>
        string Evaluate(string contentExpression, int hostElementId);
    }

    #endregion

    #region Options and Progress

    /// <summary>
    /// Controls which quality checks run and their sensitivity thresholds.
    /// </summary>
    public class QualityCheckOptions
    {
        /// <summary>Check for tag-to-tag and tag-to-annotation overlaps.</summary>
        public bool CheckClashes { get; set; } = true;

        /// <summary>Check for tags whose host element no longer exists.</summary>
        public bool CheckOrphans { get; set; } = true;

        /// <summary>Check for duplicate tags on the same element in a view.</summary>
        public bool CheckDuplicates { get; set; } = true;

        /// <summary>Check for tags with blank display text.</summary>
        public bool CheckBlanks { get; set; } = true;

        /// <summary>Check for tags that are active but not visible.</summary>
        public bool CheckHidden { get; set; } = true;

        /// <summary>Check for tags with unexpected text formats.</summary>
        public bool CheckUnexpectedValues { get; set; } = true;

        /// <summary>Check for tags that break alignment with neighbors.</summary>
        public bool CheckMisalignment { get; set; } = true;

        /// <summary>Check for tags whose text is stale relative to parameters.</summary>
        public bool CheckStale { get; set; } = true;

        /// <summary>
        /// Clash overlap sensitivity (0-100). 0 = most sensitive, 100 = least sensitive.
        /// Overlaps below this percentage are filtered out. Default 50.
        /// </summary>
        public int ClashSensitivity { get; set; } = 50;

        /// <summary>Alignment tolerance in model units. Default ~2mm.</summary>
        public double AlignmentTolerance { get; set; } = 0.002;

        /// <summary>Max issues per check type per view. 0 = unlimited.</summary>
        public int MaxIssuesPerCheckPerView { get; set; } = 0;

        /// <summary>Include dismissed issues in report (marked with IsDismissed = true).</summary>
        public bool IncludeDismissed { get; set; } = false;

        /// <summary>Default options with all checks enabled.</summary>
        public static QualityCheckOptions Default => new QualityCheckOptions();

        /// <summary>Critical-only preset: orphans, blanks, and stale checks.</summary>
        public static QualityCheckOptions CriticalOnly => new QualityCheckOptions
        {
            CheckClashes = false, CheckDuplicates = false, CheckHidden = false,
            CheckUnexpectedValues = false, CheckMisalignment = false
        };
    }

    /// <summary>
    /// Progress information during quality analysis.
    /// </summary>
    public class QualityAnalysisProgress
    {
        public int CurrentView { get; set; }
        public int TotalViews { get; set; }
        public string CurrentCheckName { get; set; }
        public int IssuesFoundSoFar { get; set; }
        public double PercentComplete => TotalViews > 0
            ? (double)CurrentView / TotalViews * 100.0 : 0.0;
    }

    #endregion

    /// <summary>
    /// Comprehensive annotation quality assurance engine. Performs 8 quality check types
    /// across managed tags, computes quality scores, and provides auto-fix capabilities.
    /// Surpasses Ideate Review with configurable sensitivity, severity grading, automatic fixes,
    /// alignment rail analysis, stale text detection, and composite quality scoring.
    /// Thread-safe: all mutable state is protected by dedicated lock objects.
    /// </summary>
    public sealed class TagQualityAnalyzer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly TagRepository _repository;
        private readonly TagConfiguration _configuration;
        private readonly ICollisionResolver _collisionResolver;
        private readonly ITagCreator _tagCreator;
        private readonly IContentExpressionEvaluator _expressionEvaluator;

        private readonly object _dismissedLock = new object();
        private readonly object _issuesCacheLock = new object();

        private readonly HashSet<string> _dismissedIssueIds;
        private readonly Dictionary<string, TagQualityIssue> _issuesCache;
        private readonly Dictionary<string, List<TagQualityIssue>> _issuesByTag;

        private const double CriticalPenalty = 5.0;
        private const double WarningPenalty = 2.0;
        private const double InfoPenalty = 0.5;

        private static readonly HashSet<string> BlankPatterns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "", "?", "??", "???", "-", "--", "N/A", "n/a", "TBD", "tbd", "null" };

        #region Constructor

        /// <summary>
        /// Initializes a new <see cref="TagQualityAnalyzer"/> with required dependencies.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
        public TagQualityAnalyzer(
            TagRepository repository,
            TagConfiguration configuration,
            ICollisionResolver collisionResolver,
            ITagCreator tagCreator,
            IContentExpressionEvaluator expressionEvaluator)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
            _tagCreator = tagCreator ?? throw new ArgumentNullException(nameof(tagCreator));
            _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));

            _dismissedIssueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _issuesCache = new Dictionary<string, TagQualityIssue>(StringComparer.OrdinalIgnoreCase);
            _issuesByTag = new Dictionary<string, List<TagQualityIssue>>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Performs comprehensive quality analysis across the specified views.
        /// Executes all enabled checks, computes per-view and aggregate scores,
        /// and returns a full report with auto-fix annotations.
        /// </summary>
        /// <param name="viewIds">Revit view element IDs to analyze.</param>
        /// <param name="options">Check options (null for defaults).</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Complete quality report with issues and scores.</returns>
        public async Task<TagQualityReport> AnalyzeAsync(
            List<int> viewIds,
            QualityCheckOptions options = null,
            IProgress<QualityAnalysisProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (viewIds == null) throw new ArgumentNullException(nameof(viewIds));
            if (viewIds.Count == 0)
                throw new ArgumentException("At least one view ID is required.", nameof(viewIds));

            options = options ?? QualityCheckOptions.Default;
            var stopwatch = Stopwatch.StartNew();
            Logger.Info("Starting quality analysis across {0} view(s)", viewIds.Count);

            var allIssues = new List<TagQualityIssue>();
            int totalTagsAnalyzed = 0;
            var viewScores = new Dictionary<int, double>();

            for (int i = 0; i < viewIds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int viewId = viewIds[i];

                progress?.Report(new QualityAnalysisProgress
                {
                    CurrentView = i + 1, TotalViews = viewIds.Count,
                    CurrentCheckName = $"Analyzing view {viewId}",
                    IssuesFoundSoFar = allIssues.Count
                });

                var viewIssues = await AnalyzeViewAsync(viewId, options, cancellationToken)
                    .ConfigureAwait(false);
                totalTagsAnalyzed += _repository.GetTagsByView(viewId).Count;

                // Apply dismissed filter
                if (!options.IncludeDismissed)
                {
                    lock (_dismissedLock)
                    {
                        viewIssues.RemoveAll(issue => _dismissedIssueIds.Contains(issue.IssueId));
                    }
                }
                else
                {
                    lock (_dismissedLock)
                    {
                        foreach (var issue in viewIssues)
                            issue.IsDismissed = _dismissedIssueIds.Contains(issue.IssueId);
                    }
                }

                allIssues.AddRange(viewIssues);
                viewScores[viewId] = ComputeQualityScore(viewIssues);
            }

            stopwatch.Stop();

            var issueCountsByType = new Dictionary<QualityIssueType, int>();
            foreach (QualityIssueType issueType in Enum.GetValues(typeof(QualityIssueType)))
            {
                int count = allIssues.Count(i => i.IssueType == issueType);
                if (count > 0) issueCountsByType[issueType] = count;
            }

            var issueCountsBySeverity = new Dictionary<IssueSeverity, int>();
            foreach (IssueSeverity severity in Enum.GetValues(typeof(IssueSeverity)))
            {
                int count = allIssues.Count(i => i.Severity == severity);
                if (count > 0) issueCountsBySeverity[severity] = count;
            }

            var report = new TagQualityReport
            {
                Issues = allIssues,
                QualityScore = ComputeQualityScore(allIssues),
                TotalTagsAnalyzed = totalTagsAnalyzed,
                TotalViewsAnalyzed = viewIds.Count,
                IssueCountsByType = issueCountsByType,
                IssueCountsBySeverity = issueCountsBySeverity,
                ViewScores = viewScores,
                AutoFixableCount = allIssues.Count(i => i.IsAutoFixable && !i.IsDismissed),
                GeneratedAt = DateTime.UtcNow,
                AnalysisDuration = stopwatch.Elapsed
            };

            CacheIssues(allIssues);

            Logger.Info(
                "Quality analysis complete: {0} issues ({1}C/{2}W/{3}I) across {4} tags " +
                "in {5} views. Score: {6:F1}. Duration: {7}ms",
                allIssues.Count,
                issueCountsBySeverity.GetValueOrDefault(IssueSeverity.Critical),
                issueCountsBySeverity.GetValueOrDefault(IssueSeverity.Warning),
                issueCountsBySeverity.GetValueOrDefault(IssueSeverity.Info),
                totalTagsAnalyzed, viewIds.Count, report.QualityScore,
                stopwatch.ElapsedMilliseconds);

            progress?.Report(new QualityAnalysisProgress
            {
                CurrentView = viewIds.Count, TotalViews = viewIds.Count,
                CurrentCheckName = "Analysis complete", IssuesFoundSoFar = allIssues.Count
            });

            return report;
        }

        /// <summary>
        /// Performs quality analysis on a single view, returning the raw list of issues.
        /// </summary>
        public async Task<List<TagQualityIssue>> AnalyzeViewAsync(
            int viewId,
            QualityCheckOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? QualityCheckOptions.Default;
            var issues = new List<TagQualityIssue>();
            List<TagInstance> tags = _repository.GetTagsByView(viewId);

            if (tags.Count == 0)
            {
                Logger.Debug("No tags in view {0}, skipping", viewId);
                return issues;
            }

            Logger.Debug("Analyzing {0} tags in view {1}", tags.Count, viewId);

            // Dispatch to each enabled check type
            var checks = new (bool enabled, Func<List<TagQualityIssue>> check)[]
            {
                (options.CheckClashes,           () => DetectClashes(tags, viewId, options)),
                (options.CheckOrphans,           () => DetectOrphans(tags, viewId)),
                (options.CheckDuplicates,        () => DetectDuplicates(tags, viewId)),
                (options.CheckBlanks,            () => DetectBlanks(tags, viewId)),
                (options.CheckHidden,            () => DetectHidden(tags, viewId)),
                (options.CheckUnexpectedValues,  () => DetectUnexpectedValues(tags, viewId)),
                (options.CheckMisalignment,      () => DetectMisalignment(tags, viewId, options)),
                (options.CheckStale,             () => DetectStale(tags, viewId))
            };

            foreach (var (enabled, check) in checks)
            {
                if (!enabled) continue;
                cancellationToken.ThrowIfCancellationRequested();
                var result = await Task.Run(check, cancellationToken).ConfigureAwait(false);
                AppendIssues(issues, result, options.MaxIssuesPerCheckPerView);
            }

            Logger.Debug("View {0}: {1} issues across {2} tags", viewId, issues.Count, tags.Count);
            return issues;
        }

        /// <summary>
        /// Fixes specific issues by ID. Only auto-fixable issues are attempted.
        /// </summary>
        public async Task<AutoFixResult> FixIssuesAsync(
            List<string> issueIds, CancellationToken cancellationToken = default)
        {
            if (issueIds == null) throw new ArgumentNullException(nameof(issueIds));
            Logger.Info("Attempting to fix {0} issue(s)", issueIds.Count);

            var result = new AutoFixResult();

            foreach (string issueId in issueIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TagQualityIssue issue;
                lock (_issuesCacheLock) { _issuesCache.TryGetValue(issueId, out issue); }

                if (issue == null || !issue.IsAutoFixable)
                {
                    if (issue == null)
                        Logger.Warn("Issue {0} not found in cache", issueId);
                    result.FailedIssueIds.Add(issueId);
                    continue;
                }

                bool fixed_ = await Task.Run(() => ApplyAutoFix(issue, result), cancellationToken)
                    .ConfigureAwait(false);

                if (fixed_)
                {
                    result.FixedIssueIds.Add(issueId);
                    lock (_issuesCacheLock) { _issuesCache.Remove(issueId); }
                }
                else
                {
                    result.FailedIssueIds.Add(issueId);
                }
            }

            Logger.Info("Fix complete: {0} fixed, {1} failed",
                result.FixedIssueIds.Count, result.FailedIssueIds.Count);
            return result;
        }

        /// <summary>
        /// Fixes all auto-fixable, non-dismissed issues from a quality report.
        /// </summary>
        public async Task<AutoFixResult> FixAllAutoFixableAsync(
            TagQualityReport report, CancellationToken cancellationToken = default)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var ids = report.Issues
                .Where(i => i.IsAutoFixable && !i.IsDismissed)
                .Select(i => i.IssueId).ToList();

            Logger.Info("Fixing all {0} auto-fixable issues", ids.Count);
            return ids.Count == 0
                ? new AutoFixResult()
                : await FixIssuesAsync(ids, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Dismisses an issue so it no longer appears in subsequent reports.</summary>
        public void DismissIssue(string issueId)
        {
            if (string.IsNullOrEmpty(issueId)) return;
            lock (_dismissedLock) { _dismissedIssueIds.Add(issueId); }
            lock (_issuesCacheLock)
            {
                if (_issuesCache.TryGetValue(issueId, out var issue))
                    issue.IsDismissed = true;
            }
            Logger.Debug("Issue {0} dismissed", issueId);
        }

        /// <summary>Returns all currently dismissed issue IDs.</summary>
        public List<string> GetDismissedIssues()
        {
            lock (_dismissedLock) { return _dismissedIssueIds.ToList(); }
        }

        /// <summary>Clears all dismissed issues so they reappear on next analysis.</summary>
        public void ClearDismissedIssues()
        {
            lock (_dismissedLock) { _dismissedIssueIds.Clear(); }
            Logger.Debug("All dismissed issues cleared");
        }

        /// <summary>Undoes a previous dismissal.</summary>
        public void UndismissIssue(string issueId)
        {
            if (string.IsNullOrEmpty(issueId)) return;
            lock (_dismissedLock) { _dismissedIssueIds.Remove(issueId); }
            lock (_issuesCacheLock)
            {
                if (_issuesCache.TryGetValue(issueId, out var issue))
                    issue.IsDismissed = false;
            }
            Logger.Debug("Issue {0} un-dismissed", issueId);
        }

        #endregion

        #region Check 1: Clash Detection

        /// <summary>
        /// Detects tag-to-tag and tag-to-annotation bounding box overlaps using the
        /// CollisionResolver. Severity: Warning if overlap &lt;25%, Critical if &gt;=25%.
        /// Configurable sensitivity (0-100%) filters out minor overlaps.
        /// </summary>
        private List<TagQualityIssue> DetectClashes(
            List<TagInstance> tags, int viewId, QualityCheckOptions options)
        {
            var issues = new List<TagQualityIssue>();
            var tagsWithBounds = tags.Where(t => t.Bounds != null && t.Bounds.Area > 0).ToList();
            if (tagsWithBounds.Count < 2) return issues;

            double thresholdPercent = options.ClashSensitivity;
            var reportedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            double clearance = _configuration.Settings.CollisionClearance;

            for (int i = 0; i < tagsWithBounds.Count; i++)
            {
                TagInstance tagA = tagsWithBounds[i];

                var otherBounds = new List<TagBounds2D>();
                var otherIds = new List<string>();
                for (int j = 0; j < tagsWithBounds.Count; j++)
                {
                    if (j != i)
                    {
                        otherBounds.Add(tagsWithBounds[j].Bounds);
                        otherIds.Add(tagsWithBounds[j].TagId);
                    }
                }

                List<TagCollision> collisions;
                try
                {
                    collisions = _collisionResolver.DetectCollisions(
                        tagA, otherBounds, otherIds, clearance);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Collision detection failed for tag {0}", tagA.TagId);
                    continue;
                }

                foreach (var collision in collisions)
                {
                    string pairKey = CreatePairKey(tagA.TagId, collision.ConflictId);
                    if (reportedPairs.Contains(pairKey)) continue;
                    reportedPairs.Add(pairKey);

                    if (collision.OverlapPercentage < thresholdPercent) continue;

                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("CLH", tagA.TagId, collision.ConflictId),
                        IssueType = QualityIssueType.Clash,
                        Severity = collision.OverlapPercentage >= 25.0
                            ? IssueSeverity.Critical : IssueSeverity.Warning,
                        AffectedTagId = tagA.TagId,
                        AffectedElementId = tagA.RevitElementId,
                        ViewId = viewId,
                        Description = $"Tag '{tagA.DisplayText ?? "(blank)"}' overlaps " +
                            $"'{collision.ConflictType ?? "tag"}' {collision.ConflictId} " +
                            $"by {collision.OverlapPercentage:F1}%",
                        IsAutoFixable = false,
                        SuggestedFix = "Reposition one of the overlapping tags.",
                        Location = tagA.Bounds?.Center ?? new Point2D(),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Clash detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Check 2: Orphan Detection

        /// <summary>
        /// Detects tags whose HostElementId no longer exists in the model via
        /// ITagCreator.IsElementValid. Severity: Critical. Auto-fix: delete.
        /// </summary>
        private List<TagQualityIssue> DetectOrphans(List<TagInstance> tags, int viewId)
        {
            var issues = new List<TagQualityIssue>();

            foreach (var tag in tags)
            {
                if (tag.State == TagState.Orphaned || tag.State == TagState.MarkedForDeletion)
                    continue;

                bool isValid;
                try { isValid = _tagCreator.IsElementValid(tag.HostElementId); }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Validation failed for host {0}", tag.HostElementId);
                    isValid = true; // assume valid on error
                }

                if (!isValid)
                {
                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("ORP", tag.TagId),
                        IssueType = QualityIssueType.Orphan,
                        Severity = IssueSeverity.Critical,
                        AffectedTagId = tag.TagId,
                        AffectedElementId = tag.RevitElementId,
                        ViewId = viewId,
                        Description = $"Tag '{tag.DisplayText ?? "(blank)"}' references host " +
                            $"{tag.HostElementId} which no longer exists.",
                        IsAutoFixable = true,
                        SuggestedFix = "Delete the orphaned tag.",
                        Location = GetTagLocation(tag),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Orphan detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Check 3: Duplicate Detection

        /// <summary>
        /// Detects same HostElementId tagged multiple times in a view. Groups by host,
        /// keeps highest PlacementScore, flags the rest. Severity: Warning. Auto-fix: delete.
        /// </summary>
        private List<TagQualityIssue> DetectDuplicates(List<TagInstance> tags, int viewId)
        {
            var issues = new List<TagQualityIssue>();

            var grouped = tags
                .Where(t => t.State != TagState.MarkedForDeletion && t.State != TagState.Orphaned)
                .GroupBy(t => t.HostElementId)
                .Where(g => g.Count() > 1);

            foreach (var group in grouped)
            {
                var ordered = group.OrderByDescending(t => t.PlacementScore).ToList();
                TagInstance keeper = ordered[0];

                for (int i = 1; i < ordered.Count; i++)
                {
                    TagInstance dup = ordered[i];
                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("DUP", dup.TagId),
                        IssueType = QualityIssueType.Duplicate,
                        Severity = IssueSeverity.Warning,
                        AffectedTagId = dup.TagId,
                        AffectedElementId = dup.RevitElementId,
                        ViewId = viewId,
                        Description = $"Element {group.Key} tagged {ordered.Count} times. " +
                            $"'{dup.DisplayText ?? "(blank)"}' (score:{dup.PlacementScore:F2}) " +
                            $"duplicates '{keeper.DisplayText ?? "(blank)"}' (score:{keeper.PlacementScore:F2}).",
                        IsAutoFixable = true,
                        SuggestedFix = $"Delete duplicate. Keep tag '{keeper.TagId}' (best score).",
                        Location = GetTagLocation(dup),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Duplicate detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Check 4: Blank Detection

        /// <summary>
        /// Detects tags with empty, null, whitespace, or placeholder display text
        /// (?, ??, ???, -, --, N/A, TBD, null). Severity: Warning.
        /// Auto-fix: re-evaluate content expression if present.
        /// </summary>
        private List<TagQualityIssue> DetectBlanks(List<TagInstance> tags, int viewId)
        {
            var issues = new List<TagQualityIssue>();

            foreach (var tag in tags)
            {
                if (tag.State == TagState.MarkedForDeletion || tag.State == TagState.Orphaned)
                    continue;

                string trimmed = tag.DisplayText?.Trim();
                bool isBlank = string.IsNullOrWhiteSpace(trimmed) || BlankPatterns.Contains(trimmed ?? "");

                if (isBlank)
                {
                    bool canFix = !string.IsNullOrEmpty(tag.ContentExpression);
                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("BLK", tag.TagId),
                        IssueType = QualityIssueType.Blank,
                        Severity = IssueSeverity.Warning,
                        AffectedTagId = tag.TagId,
                        AffectedElementId = tag.RevitElementId,
                        ViewId = viewId,
                        Description = $"Tag on {tag.CategoryName ?? "element"} {tag.HostElementId} " +
                            $"displays blank: '{tag.DisplayText ?? "(null)"}'.",
                        IsAutoFixable = canFix,
                        SuggestedFix = canFix
                            ? $"Re-evaluate expression '{tag.ContentExpression}'."
                            : "Manually set tag text or assign a content expression.",
                        Location = GetTagLocation(tag),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Blank detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Check 5: Hidden Detection

        /// <summary>
        /// Detects tags in Active state that are not visible (view filter, crop, override).
        /// Uses ITagCreator.IsTagVisible. Severity: Info.
        /// </summary>
        private List<TagQualityIssue> DetectHidden(List<TagInstance> tags, int viewId)
        {
            var issues = new List<TagQualityIssue>();

            foreach (var tag in tags)
            {
                if (tag.State != TagState.Active || tag.RevitElementId <= 0)
                    continue;

                bool isVisible;
                try { isVisible = _tagCreator.IsTagVisible(tag.RevitElementId, viewId); }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Visibility check failed for tag {0}", tag.TagId);
                    continue;
                }

                if (!isVisible)
                {
                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("HID", tag.TagId),
                        IssueType = QualityIssueType.Hidden,
                        Severity = IssueSeverity.Info,
                        AffectedTagId = tag.TagId,
                        AffectedElementId = tag.RevitElementId,
                        ViewId = viewId,
                        Description = $"Tag '{tag.DisplayText ?? "(blank)"}' on element " +
                            $"{tag.HostElementId} is Active but not visible. May be hidden " +
                            $"by view filter, crop region, or graphic override.",
                        IsAutoFixable = false,
                        SuggestedFix = "Check view filters/crop/overrides, or update tag state to Hidden.",
                        Location = GetTagLocation(tag),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Hidden detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Check 6: Unexpected Value Detection

        /// <summary>
        /// Checks tag DisplayText against expected format regex from
        /// TagConfiguration.Settings.ExpectedFormats keyed by category. Severity: Warning.
        /// </summary>
        private List<TagQualityIssue> DetectUnexpectedValues(List<TagInstance> tags, int viewId)
        {
            var issues = new List<TagQualityIssue>();
            var formats = _configuration.Settings.ExpectedFormats;
            if (formats == null || formats.Count == 0) return issues;

            foreach (var tag in tags)
            {
                if (tag.State == TagState.MarkedForDeletion || tag.State == TagState.Orphaned)
                    continue;
                if (string.IsNullOrEmpty(tag.CategoryName)) continue;
                if (!formats.TryGetValue(tag.CategoryName, out string pattern)) continue;
                if (string.IsNullOrEmpty(pattern)) continue;

                string text = tag.DisplayText?.Trim();
                if (string.IsNullOrEmpty(text)) continue; // blank check handles this

                bool matches;
                try
                {
                    matches = Regex.IsMatch(text, pattern, RegexOptions.None,
                        TimeSpan.FromMilliseconds(100));
                }
                catch (RegexMatchTimeoutException)
                {
                    Logger.Warn("Regex timeout: pattern '{0}' on '{1}'", pattern, text);
                    continue;
                }
                catch (ArgumentException ex)
                {
                    Logger.Warn(ex, "Invalid regex '{0}' for category '{1}'",
                        pattern, tag.CategoryName);
                    continue;
                }

                if (!matches)
                {
                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("UNX", tag.TagId),
                        IssueType = QualityIssueType.UnexpectedValue,
                        Severity = IssueSeverity.Warning,
                        AffectedTagId = tag.TagId,
                        AffectedElementId = tag.RevitElementId,
                        ViewId = viewId,
                        Description = $"Tag text '{text}' on {tag.CategoryName} {tag.HostElementId} " +
                            $"does not match expected pattern '{pattern}'.",
                        IsAutoFixable = false,
                        SuggestedFix = $"Verify text matches {tag.CategoryName} format: {pattern}",
                        Location = GetTagLocation(tag),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Unexpected value detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Check 7: Misalignment Detection

        /// <summary>
        /// Detects tags breaking horizontal/vertical alignment with same-category neighbors.
        /// Groups by category, detects alignment rails (coordinate clusters), flags tags
        /// that deviate from nearest rail beyond tolerance. Severity: Info.
        /// </summary>
        private List<TagQualityIssue> DetectMisalignment(
            List<TagInstance> tags, int viewId, QualityCheckOptions options)
        {
            var issues = new List<TagQualityIssue>();
            double tolerance = options.AlignmentTolerance;

            var byCategory = tags
                .Where(t => t.State != TagState.MarkedForDeletion &&
                            t.State != TagState.Orphaned &&
                            t.Bounds != null && !string.IsNullOrEmpty(t.CategoryName))
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 3);

            foreach (var group in byCategory)
            {
                var catTags = group.ToList();
                var hRails = DetectAlignmentRails(
                    catTags.Select(t => t.Bounds.Center.Y).ToList(), tolerance);
                var vRails = DetectAlignmentRails(
                    catTags.Select(t => t.Bounds.Center.X).ToList(), tolerance);

                foreach (var tag in catTags)
                {
                    double cy = tag.Bounds.Center.Y;
                    double cx = tag.Bounds.Center.X;

                    // Check horizontal alignment
                    if (!IsOnAnyRail(cy, hRails, tolerance) && hRails.Count > 0)
                    {
                        double rail = FindNearestRailValue(cy, hRails);
                        double dev = Math.Abs(cy - rail);
                        double maxDev = GetMaxDeviationThreshold(catTags, true);
                        if (dev > tolerance && dev < maxDev)
                        {
                            issues.Add(CreateMisalignmentIssue(
                                tag, viewId, "horizontal", rail, dev, cy));
                        }
                    }

                    // Check vertical alignment (skip if already reported for this tag)
                    if (!IsOnAnyRail(cx, vRails, tolerance) && vRails.Count > 0)
                    {
                        double rail = FindNearestRailValue(cx, vRails);
                        double dev = Math.Abs(cx - rail);
                        double maxDev = GetMaxDeviationThreshold(catTags, false);
                        bool alreadyReported = issues.Any(
                            i => i.AffectedTagId == tag.TagId &&
                                 i.IssueType == QualityIssueType.Misaligned);

                        if (dev > tolerance && dev < maxDev && !alreadyReported)
                        {
                            issues.Add(CreateMisalignmentIssue(
                                tag, viewId, "vertical", rail, dev, cx));
                        }
                    }
                }
            }

            Logger.Debug("Misalignment detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        /// <summary>
        /// Detects alignment rails from coordinate values. A rail is a value shared
        /// by at least 2 tags within the tolerance. Returns rail center values.
        /// </summary>
        private List<double> DetectAlignmentRails(List<double> values, double tolerance)
        {
            var rails = new List<double>();
            if (values.Count < 2) return rails;

            var sorted = values.OrderBy(v => v).ToList();
            var used = new bool[sorted.Count];

            for (int i = 0; i < sorted.Count; i++)
            {
                if (used[i]) continue;
                var members = new List<double> { sorted[i] };
                used[i] = true;

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (used[j]) continue;
                    if (Math.Abs(sorted[j] - members.Average()) <= tolerance)
                    {
                        members.Add(sorted[j]);
                        used[j] = true;
                    }
                }

                if (members.Count >= 2)
                    rails.Add(members.Average());
            }
            return rails;
        }

        private bool IsOnAnyRail(double value, List<double> rails, double tolerance)
            => rails.Any(r => Math.Abs(value - r) <= tolerance);

        private double FindNearestRailValue(double value, List<double> rails)
        {
            double nearest = rails[0];
            double minDist = Math.Abs(value - rails[0]);
            for (int i = 1; i < rails.Count; i++)
            {
                double d = Math.Abs(value - rails[i]);
                if (d < minDist) { minDist = d; nearest = rails[i]; }
            }
            return nearest;
        }

        /// <summary>
        /// Max deviation threshold: tags beyond this are in a different row/column,
        /// not misaligned. Uses half the average spacing between tag centers.
        /// </summary>
        private double GetMaxDeviationThreshold(List<TagInstance> tags, bool horizontal)
        {
            var centers = tags
                .Select(t => horizontal ? t.Bounds.Center.Y : t.Bounds.Center.X)
                .OrderBy(v => v).ToList();
            if (centers.Count < 2) return double.MaxValue;
            double avgSpacing = (centers.Last() - centers.First()) / (centers.Count - 1);
            return Math.Max(avgSpacing * 0.5, 0.01);
        }

        private TagQualityIssue CreateMisalignmentIssue(
            TagInstance tag, int viewId, string dir,
            double railValue, double deviation, double actual)
        {
            return new TagQualityIssue
            {
                IssueId = GenerateIssueId("MIS", tag.TagId, dir),
                IssueType = QualityIssueType.Misaligned,
                Severity = IssueSeverity.Info,
                AffectedTagId = tag.TagId,
                AffectedElementId = tag.RevitElementId,
                ViewId = viewId,
                Description = $"Tag '{tag.DisplayText ?? "(blank)"}' on {tag.CategoryName ?? "element"} " +
                    $"{tag.HostElementId} is {dir}ly misaligned by {deviation:F4} from " +
                    $"rail at {railValue:F4} (actual: {actual:F4}).",
                IsAutoFixable = false,
                SuggestedFix = $"Align {dir}ly with neighboring {tag.CategoryName ?? "element"} tags.",
                Location = tag.Bounds.Center,
                DetectedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Check 8: Stale Detection

        /// <summary>
        /// Detects tags whose DisplayText no longer matches what the content expression
        /// would produce from current parameter values. Uses IContentExpressionEvaluator.
        /// Severity: Warning. Auto-fix: refresh text.
        /// </summary>
        private List<TagQualityIssue> DetectStale(List<TagInstance> tags, int viewId)
        {
            var issues = new List<TagQualityIssue>();

            foreach (var tag in tags)
            {
                if (tag.State == TagState.MarkedForDeletion || tag.State == TagState.Orphaned)
                    continue;
                if (string.IsNullOrEmpty(tag.ContentExpression)) continue;
                if (string.IsNullOrEmpty(tag.DisplayText)) continue;

                string expected;
                try { expected = _expressionEvaluator.Evaluate(tag.ContentExpression, tag.HostElementId); }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Expression eval failed for tag {0}", tag.TagId);
                    continue;
                }
                if (expected == null) continue;

                string current = tag.DisplayText.Trim();
                string exp = expected.Trim();

                if (!string.Equals(current, exp, StringComparison.Ordinal))
                {
                    issues.Add(new TagQualityIssue
                    {
                        IssueId = GenerateIssueId("STL", tag.TagId),
                        IssueType = QualityIssueType.Stale,
                        Severity = IssueSeverity.Warning,
                        AffectedTagId = tag.TagId,
                        AffectedElementId = tag.RevitElementId,
                        ViewId = viewId,
                        Description = $"Tag on {tag.CategoryName ?? "element"} {tag.HostElementId} " +
                            $"shows '{current}' but parameters produce '{exp}'.",
                        IsAutoFixable = true,
                        SuggestedFix = $"Refresh text from '{current}' to '{exp}'.",
                        Location = GetTagLocation(tag),
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            Logger.Debug("Stale detection view {0}: {1} issues", viewId, issues.Count);
            return issues;
        }

        #endregion

        #region Auto-Fix Engine

        /// <summary>
        /// Dispatches auto-fix to the appropriate handler based on issue type.
        /// </summary>
        private bool ApplyAutoFix(TagQualityIssue issue, AutoFixResult result)
        {
            try
            {
                switch (issue.IssueType)
                {
                    case QualityIssueType.Orphan:
                    case QualityIssueType.Duplicate:
                        return FixByDeletion(issue, result);

                    case QualityIssueType.Blank:
                    case QualityIssueType.Stale:
                        return FixByTextRefresh(issue, result);

                    default:
                        Logger.Debug("No auto-fix for issue type {0}", issue.IssueType);
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Auto-fix failed for {0} ({1})", issue.IssueId, issue.IssueType);
                return false;
            }
        }

        /// <summary>
        /// Fixes orphan and duplicate issues by deleting the tag from Revit and the repository.
        /// </summary>
        private bool FixByDeletion(TagQualityIssue issue, AutoFixResult result)
        {
            TagInstance tag = _repository.GetTag(issue.AffectedTagId);
            if (tag == null)
            {
                Logger.Warn("Cannot fix: tag {0} not found", issue.AffectedTagId);
                return false;
            }

            if (tag.RevitElementId > 0)
            {
                if (!_tagCreator.DeleteTag(tag.RevitElementId))
                {
                    Logger.Warn("Failed to delete tag element {0}", tag.RevitElementId);
                    return false;
                }
            }

            _repository.RemoveTag(tag.TagId);
            _repository.RecordOperation(new TagOperation
            {
                Type = TagOperationType.Delete,
                TagId = tag.TagId,
                PreviousState = tag,
                Source = TagCreationSource.AutomatedPlacement
            });

            result.TagsDeleted++;
            Logger.Info("Auto-fixed {0}: deleted tag {1} (host {2}, view {3})",
                issue.IssueType, tag.TagId, tag.HostElementId, tag.ViewId);
            return true;
        }

        /// <summary>
        /// Fixes blank and stale issues by re-evaluating the content expression
        /// and updating the tag text in both Revit and the repository.
        /// </summary>
        private bool FixByTextRefresh(TagQualityIssue issue, AutoFixResult result)
        {
            TagInstance tag = _repository.GetTag(issue.AffectedTagId);
            if (tag == null)
            {
                Logger.Warn("Cannot fix: tag {0} not found", issue.AffectedTagId);
                return false;
            }

            if (string.IsNullOrEmpty(tag.ContentExpression))
            {
                Logger.Warn("Cannot fix: tag {0} has no content expression", tag.TagId);
                return false;
            }

            string newText;
            try { newText = _expressionEvaluator.Evaluate(tag.ContentExpression, tag.HostElementId); }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Expression eval failed for tag {0}", tag.TagId);
                return false;
            }

            // For blank fixes, ensure the new text is actually non-blank
            if (issue.IssueType == QualityIssueType.Blank)
            {
                if (string.IsNullOrWhiteSpace(newText) || BlankPatterns.Contains(newText?.Trim() ?? ""))
                {
                    Logger.Warn("Re-evaluation still produces blank for tag {0}: '{1}'",
                        tag.TagId, newText);
                    return false;
                }
            }

            if (newText == null)
            {
                Logger.Warn("Expression returned null for tag {0}", tag.TagId);
                return false;
            }

            if (tag.RevitElementId > 0)
            {
                if (!_tagCreator.UpdateTagText(tag.RevitElementId, newText))
                {
                    Logger.Warn("Failed to update tag element {0} text", tag.RevitElementId);
                    return false;
                }
            }

            TagInstance prev = CloneTagInstance(tag);
            string oldText = tag.DisplayText;
            tag.DisplayText = newText;
            tag.State = TagState.Active;
            tag.LastModified = DateTime.UtcNow;
            _repository.UpdateTag(tag);

            _repository.RecordOperation(new TagOperation
            {
                Type = TagOperationType.ReText,
                TagId = tag.TagId,
                PreviousState = prev,
                NewState = tag,
                Source = TagCreationSource.AutomatedPlacement
            });

            result.TagsRefreshed++;
            Logger.Info("Auto-fixed {0}: tag {1} text '{2}' -> '{3}'",
                issue.IssueType, tag.TagId, oldText, newText);
            return true;
        }

        #endregion

        #region Quality Score Calculation

        /// <summary>
        /// Score = 100 - (critical * 5) - (warning * 2) - (info * 0.5), clamped [0, 100].
        /// Dismissed issues are excluded.
        /// </summary>
        private double ComputeQualityScore(List<TagQualityIssue> issues)
        {
            if (issues == null || issues.Count == 0) return 100.0;

            var active = issues.Where(i => !i.IsDismissed).ToList();
            if (active.Count == 0) return 100.0;

            double score = 100.0
                - active.Count(i => i.Severity == IssueSeverity.Critical) * CriticalPenalty
                - active.Count(i => i.Severity == IssueSeverity.Warning) * WarningPenalty
                - active.Count(i => i.Severity == IssueSeverity.Info) * InfoPenalty;

            return Math.Max(0.0, Math.Min(100.0, score));
        }

        #endregion

        #region Helper Methods

        /// <summary>Gets the best available location for a tag (placement, bounds center, or origin).</summary>
        private Point2D GetTagLocation(TagInstance tag)
            => tag.Placement?.Position ?? tag.Bounds?.Center ?? new Point2D();

        /// <summary>
        /// Generates a deterministic issue ID from prefix and components.
        /// Uses a stable hash to ensure the same input always produces the same ID.
        /// </summary>
        private string GenerateIssueId(string prefix, params string[] components)
        {
            string combined = prefix + ":" + string.Join("|", components);
            int hash = GetStableHashCode(combined);
            return $"{prefix}-{Math.Abs(hash):X8}";
        }

        /// <summary>Stable string hash consistent across process runs (djb2 variant).</summary>
        private int GetStableHashCode(string str)
        {
            unchecked
            {
                int h1 = 5381, h2 = h1;
                for (int i = 0; i < str.Length; i += 2)
                {
                    h1 = ((h1 << 5) + h1) ^ str[i];
                    if (i + 1 < str.Length)
                        h2 = ((h2 << 5) + h2) ^ str[i + 1];
                }
                return h1 + (h2 * 1566083941);
            }
        }

        /// <summary>Canonical pair key: A|B and B|A produce the same key.</summary>
        private string CreatePairKey(string id1, string id2)
            => string.Compare(id1, id2, StringComparison.OrdinalIgnoreCase) <= 0
                ? $"{id1}|{id2}" : $"{id2}|{id1}";

        /// <summary>Appends issues respecting the per-check maximum, prioritizing by severity.</summary>
        private void AppendIssues(
            List<TagQualityIssue> target, List<TagQualityIssue> source, int maxPerCheck)
        {
            if (source == null || source.Count == 0) return;
            if (maxPerCheck > 0 && source.Count > maxPerCheck)
                target.AddRange(source.OrderByDescending(i => i.Severity).Take(maxPerCheck));
            else
                target.AddRange(source);
        }

        /// <summary>Caches issues for subsequent fix operations. Replaces previous cache.</summary>
        private void CacheIssues(List<TagQualityIssue> issues)
        {
            lock (_issuesCacheLock)
            {
                _issuesCache.Clear();
                _issuesByTag.Clear();
                foreach (var issue in issues)
                {
                    _issuesCache[issue.IssueId] = issue;
                    if (!string.IsNullOrEmpty(issue.AffectedTagId))
                    {
                        if (!_issuesByTag.TryGetValue(issue.AffectedTagId, out var list))
                        {
                            list = new List<TagQualityIssue>();
                            _issuesByTag[issue.AffectedTagId] = list;
                        }
                        list.Add(issue);
                    }
                }
            }
        }

        /// <summary>Creates a shallow clone of a TagInstance for operation history.</summary>
        private TagInstance CloneTagInstance(TagInstance src)
        {
            return new TagInstance
            {
                TagId = src.TagId, RevitElementId = src.RevitElementId,
                HostElementId = src.HostElementId, ViewId = src.ViewId,
                CategoryName = src.CategoryName, FamilyName = src.FamilyName,
                TypeName = src.TypeName, TagFamilyName = src.TagFamilyName,
                TagTypeName = src.TagTypeName, Placement = src.Placement,
                DisplayText = src.DisplayText, ContentExpression = src.ContentExpression,
                State = src.State, Bounds = src.Bounds,
                CreatedByRule = src.CreatedByRule, CreatedByTemplate = src.CreatedByTemplate,
                CreationSource = src.CreationSource, PlacementScore = src.PlacementScore,
                LastModified = src.LastModified, UserAdjusted = src.UserAdjusted,
                Metadata = src.Metadata != null
                    ? new Dictionary<string, object>(src.Metadata)
                    : new Dictionary<string, object>()
            };
        }

        #endregion
    }
}
