// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagAnalyticsEngine.cs - Comprehensive analytics and insights engine for tagging operations
// Provides deep analysis of tagging quality, productivity, patterns, and recommendations
//
// Analytics Capabilities:
//   1. Coverage Analytics  - Per-category, per-view, per-sheet, per-discipline tagging coverage
//   2. Quality Trends      - Quality score tracking over time with direction detection
//   3. Productivity Metrics - Tags per session, correction rates, time saved estimates
//   4. Pattern Discovery   - Template usage, category scoring, re-tag instability detection
//   5. Smart Recommendations - Actionable suggestions prioritized by impact
//   6. Dashboard Data      - Structured composite data for UI rendering

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Enums

    /// <summary>
    /// Classification of recommendation types by the area they address.
    /// </summary>
    public enum RecommendationType
    {
        /// <summary>Addresses gaps in tagging coverage.</summary>
        Coverage,
        /// <summary>Addresses declining or low quality metrics.</summary>
        Quality,
        /// <summary>Addresses workflow efficiency and automation tuning.</summary>
        Productivity,
        /// <summary>Addresses template, rule, or setting adjustments.</summary>
        Configuration
    }

    /// <summary>
    /// Direction of a quality trend metric over time.
    /// </summary>
    public enum TrendDirection
    {
        Improving,
        Stable,
        Declining
    }

    /// <summary>
    /// Health status thresholds for color coding in dashboards.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>Score >= 80.</summary>
        Green,
        /// <summary>Score 50-79.</summary>
        Yellow,
        /// <summary>Score below 50.</summary>
        Red
    }

    #endregion

    #region Report DTOs

    /// <summary>
    /// Complete analytics report combining all analysis dimensions.
    /// </summary>
    public class TagAnalyticsReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan AnalysisDuration { get; set; }
        public CoverageReport Coverage { get; set; }
        public List<QualityTrend> QualityTrends { get; set; } = new List<QualityTrend>();
        public ProductivityReport Productivity { get; set; }
        public List<PatternInsight> Patterns { get; set; } = new List<PatternInsight>();
        public List<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
        public DashboardData Dashboard { get; set; }
        public int TotalTags { get; set; }
        public int TotalViewsWithTags { get; set; }
        public int TotalCategoriesWithTags { get; set; }
    }

    /// <summary>
    /// Tagging coverage analysis across categories, views, sheets, and disciplines.
    /// </summary>
    public class CoverageReport
    {
        /// <summary>Per-category coverage: category name -> percentage tagged (0-100).</summary>
        public Dictionary<string, double> CategoryScores { get; set; } = new Dictionary<string, double>();
        /// <summary>Per-view coverage: view ID -> completion percentage (0-100).</summary>
        public Dictionary<int, double> ViewScores { get; set; } = new Dictionary<int, double>();
        /// <summary>Per-sheet coverage: sheet ID -> average view coverage (0-100).</summary>
        public Dictionary<int, double> SheetScores { get; set; } = new Dictionary<int, double>();
        /// <summary>Per-discipline coverage: discipline -> composite score (0-100).</summary>
        public Dictionary<string, double> DisciplineScores { get; set; } = new Dictionary<string, double>();
        /// <summary>Overall project tagging coverage (0-100).</summary>
        public double OverallScore { get; set; }
        /// <summary>Trend direction based on recent session coverage changes.</summary>
        public TrendDirection Trend { get; set; }
        /// <summary>Categories with zero tags but configured templates.</summary>
        public List<string> UntaggedCategories { get; set; } = new List<string>();
        public int TotalTaggableElements { get; set; }
        public int TotalTaggedElements { get; set; }
    }

    /// <summary>
    /// Tracks a single quality metric over time with trend direction and forecast.
    /// </summary>
    public class QualityTrend
    {
        public string MetricName { get; set; }
        public List<TimestampedValue> Values { get; set; } = new List<TimestampedValue>();
        public TrendDirection Direction { get; set; }
        public double Forecast { get; set; }
        public bool IsSignificant { get; set; }
        public string CategoryName { get; set; }
    }

    /// <summary>A value at a specific point in time.</summary>
    public class TimestampedValue
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public TimestampedValue() { }
        public TimestampedValue(DateTime timestamp, double value)
        { Timestamp = timestamp; Value = value; }
    }

    /// <summary>
    /// Productivity metrics for tagging operations.
    /// </summary>
    public class ProductivityReport
    {
        public double TagsPerSession { get; set; }
        public double TagsPerHour { get; set; }
        /// <summary>Average placement quality score (0-100).</summary>
        public double AveragePlacementScore { get; set; }
        /// <summary>Rate of manual corrections (0-1). Lower = better automation.</summary>
        public double CorrectionRate { get; set; }
        /// <summary>Estimated time saved vs manual tagging (~30s/tag), in minutes.</summary>
        public double EstimatedTimeSavedMinutes { get; set; }
        public int TotalSessions { get; set; }
        public int TotalTagsPlaced { get; set; }
        public int TotalCorrections { get; set; }
        public TrendDirection PlacementScoreTrend { get; set; }
        public TrendDirection CorrectionRateTrend { get; set; }
    }

    /// <summary>
    /// A discovered pattern or insight from tagging data analysis.
    /// </summary>
    public class PatternInsight
    {
        public string Description { get; set; }
        public double Metric { get; set; }
        /// <summary>Statistical significance (0-1).</summary>
        public double Significance { get; set; }
        public string Recommendation { get; set; }
        /// <summary>Category: "TemplateUsage", "Instability", "Correlation", etc.</summary>
        public string PatternCategory { get; set; }
    }

    /// <summary>
    /// An actionable recommendation prioritized by estimated impact.
    /// </summary>
    public class Recommendation
    {
        public RecommendationType Type { get; set; }
        /// <summary>Priority score (higher = more impactful). Computed as gap * count.</summary>
        public double Priority { get; set; }
        public string Description { get; set; }
        public string SuggestedAction { get; set; }
        public string EstimatedImpact { get; set; }
        public string Scope { get; set; }
    }

    /// <summary>
    /// Structured data for rendering a tagging analytics dashboard.
    /// </summary>
    public class DashboardData
    {
        public ProjectHealthScore HealthScore { get; set; }
        public List<CategoryHealth> CategoryHealthGrid { get; set; } = new List<CategoryHealth>();
        public List<ActivityEntry> RecentActivity { get; set; } = new List<ActivityEntry>();
        public List<Recommendation> TopRecommendations { get; set; } = new List<Recommendation>();
        public Dictionary<string, string> QuickStats { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Composite project health score with component breakdown.
    /// </summary>
    public class ProjectHealthScore
    {
        public double OverallScore { get; set; }
        /// <summary>Coverage component (0-100), weight 40%.</summary>
        public double CoverageScore { get; set; }
        /// <summary>Quality component (0-100), weight 35%.</summary>
        public double QualityScore { get; set; }
        /// <summary>Consistency component (0-100), weight 25%.</summary>
        public double ConsistencyScore { get; set; }
        public HealthStatus Status { get; set; }
    }

    /// <summary>
    /// Health data for a single Revit category in the dashboard grid.
    /// </summary>
    public class CategoryHealth
    {
        public string CategoryName { get; set; }
        public int TaggedCount { get; set; }
        public int TotalCount { get; set; }
        public double CoveragePercent { get; set; }
        public double AverageQuality { get; set; }
        public HealthStatus Status { get; set; }
    }

    /// <summary>
    /// A single entry in the recent activity timeline.
    /// </summary>
    public class ActivityEntry
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public int TagCount { get; set; }
        public double QualityScore { get; set; }
        public string SessionId { get; set; }
    }

    #endregion

    /// <summary>
    /// Comprehensive analytics and insights engine for tagging operations. Provides deep
    /// analysis of tagging quality, productivity, patterns, and prioritized recommendations.
    ///
    /// <para>
    /// Aggregates data from <see cref="TagRepository"/> (tags, sessions, corrections, patterns)
    /// and <see cref="TagConfiguration"/> (thresholds, expected formats) to produce actionable
    /// insights. All computations use real statistical methods: linear regression for trends,
    /// weighted scoring for recommendations, and composite health scoring for dashboards.
    /// </para>
    ///
    /// <para>Thread-safe: all mutable state is protected by dedicated lock objects.</para>
    /// </summary>
    public sealed class TagAnalyticsEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly TagRepository _repository;
        private readonly TagConfiguration _configuration;
        private readonly object _snapshotsLock = new object();
        private readonly List<QualitySnapshot> _qualitySnapshots;

        // Discipline-to-category mapping for coverage aggregation
        private static readonly Dictionary<string, List<string>> DisciplineCategories =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Architectural", new List<string>
                    { "Doors", "Windows", "Walls", "Rooms", "Floors", "Ceilings", "Roofs",
                      "Stairs", "Railings", "Curtain Panels", "Curtain Wall Mullions",
                      "Generic Models", "Furniture", "Casework", "Columns" } },
                { "Structural", new List<string>
                    { "Structural Columns", "Structural Framing", "Structural Foundations",
                      "Structural Connections", "Structural Rebar", "Structural Stiffeners" } },
                { "MEP", new List<string>
                    { "Mechanical Equipment", "Air Terminals", "Ducts", "Duct Fittings",
                      "Duct Accessories", "Pipes", "Pipe Fittings", "Pipe Accessories",
                      "Plumbing Fixtures", "Sprinklers", "Electrical Equipment",
                      "Electrical Fixtures", "Lighting Fixtures", "Communication Devices",
                      "Data Devices", "Cable Trays", "Conduits" } },
                { "Fire Protection", new List<string>
                    { "Sprinklers", "Fire Alarm Devices", "Fire Protection" } }
            };

        private const double ManualSecondsPerTag = 30.0;
        private const double CoverageWeight = 0.40;
        private const double QualityWeight = 0.35;
        private const double ConsistencyWeight = 0.25;
        private const double GreenThreshold = 80.0;
        private const double YellowThreshold = 50.0;

        #region Constructor

        /// <summary>
        /// Initializes a new <see cref="TagAnalyticsEngine"/>.
        /// </summary>
        public TagAnalyticsEngine(TagRepository repository, TagConfiguration configuration)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _qualitySnapshots = new List<QualitySnapshot>();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Generates a comprehensive analytics report covering coverage, quality trends,
        /// productivity, patterns, and recommendations.
        /// </summary>
        public async Task<TagAnalyticsReport> GenerateFullReportAsync(
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.Info("Generating full analytics report");

            var allTags = _repository.GetAllTags();
            var report = new TagAnalyticsReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalTags = allTags.Count
            };

            cancellationToken.ThrowIfCancellationRequested();

            // Run independent analyses in parallel
            var coverageTask = Task.Run(() => GetCoverageReport(), cancellationToken);
            var productivityTask = Task.Run(() => GetProductivityMetrics(), cancellationToken);
            var patternsTask = Task.Run(() => DiscoverPatterns(), cancellationToken);
            var trendsTask = Task.Run(() => GetQualityTrends(30), cancellationToken);

            await Task.WhenAll(coverageTask, productivityTask, patternsTask, trendsTask)
                .ConfigureAwait(false);

            report.Coverage = coverageTask.Result;
            report.Productivity = productivityTask.Result;
            report.Patterns = patternsTask.Result;
            report.QualityTrends = trendsTask.Result;

            cancellationToken.ThrowIfCancellationRequested();
            report.Recommendations = GetRecommendations(10);
            report.Dashboard = GetDashboardData();

            var viewIds = new HashSet<int>(allTags.Select(t => t.ViewId));
            var categories = new HashSet<string>(
                allTags.Where(t => !string.IsNullOrEmpty(t.CategoryName))
                       .Select(t => t.CategoryName), StringComparer.OrdinalIgnoreCase);
            report.TotalViewsWithTags = viewIds.Count;
            report.TotalCategoriesWithTags = categories.Count;

            stopwatch.Stop();
            report.AnalysisDuration = stopwatch.Elapsed;

            Logger.Info("Analytics report: {0}ms, {1} tags, {2} views, {3} recommendations",
                stopwatch.ElapsedMilliseconds, allTags.Count,
                report.TotalViewsWithTags, report.Recommendations.Count);

            return report;
        }

        /// <summary>
        /// Computes tagging coverage across categories, views, sheets, and disciplines.
        /// </summary>
        /// <param name="viewIds">Optional filter: only these views. Null = all views.</param>
        public CoverageReport GetCoverageReport(List<int> viewIds = null)
        {
            var report = new CoverageReport();
            var allTags = _repository.GetAllTags();
            var tags = viewIds != null
                ? allTags.Where(t => viewIds.Contains(t.ViewId)).ToList()
                : allTags;

            if (tags.Count == 0) return report;

            // Per-category coverage: count unique host elements tagged per category
            var tagsByCategory = tags
                .Where(t => !string.IsNullOrEmpty(t.CategoryName) &&
                            t.State != TagState.MarkedForDeletion &&
                            t.State != TagState.Orphaned)
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase);

            int totalTagged = 0, totalTaggable = 0;

            foreach (var group in tagsByCategory)
            {
                int uniqueHostsTagged = group.Select(t => t.HostElementId).Distinct().Count();
                int estimatedTotal = EstimateTotalElementsForCategory(group.Key, allTags);
                double coverage = estimatedTotal > 0
                    ? Math.Min(100.0, (double)uniqueHostsTagged / estimatedTotal * 100.0)
                    : 100.0;
                report.CategoryScores[group.Key] = Math.Round(coverage, 1);
                totalTagged += uniqueHostsTagged;
                totalTaggable += estimatedTotal;
            }

            // Identify categories with templates but zero tags
            foreach (var template in _repository.GetTemplates())
            {
                if (!string.IsNullOrEmpty(template.CategoryName) &&
                    !report.CategoryScores.ContainsKey(template.CategoryName))
                {
                    report.CategoryScores[template.CategoryName] = 0.0;
                    report.UntaggedCategories.Add(template.CategoryName);
                }
            }

            report.TotalTaggedElements = totalTagged;
            report.TotalTaggableElements = Math.Max(totalTaggable, totalTagged);

            // Per-view coverage: ratio of active to total tags as completeness proxy
            var tagsByView = tags
                .Where(t => t.State != TagState.MarkedForDeletion && t.State != TagState.Orphaned)
                .GroupBy(t => t.ViewId);

            foreach (var viewGroup in tagsByView)
            {
                int activeCount = viewGroup.Count(t => t.State == TagState.Active);
                double viewScore = viewGroup.Count() > 0
                    ? (double)activeCount / viewGroup.Count() * 100.0 : 0.0;
                report.ViewScores[viewGroup.Key] = Math.Round(viewScore, 1);
            }

            // Per-sheet coverage: group views by sheet metadata, average their scores
            var viewsWithSheets = tags
                .Where(t => t.Metadata != null && t.Metadata.ContainsKey("SheetId"))
                .Select(t => new { t.ViewId, SheetId = Convert.ToInt32(t.Metadata["SheetId"]) })
                .Where(x => x.SheetId > 0)
                .Distinct()
                .GroupBy(x => x.SheetId);

            foreach (var sheetGroup in viewsWithSheets)
            {
                var scores = sheetGroup
                    .Where(sv => report.ViewScores.ContainsKey(sv.ViewId))
                    .Select(sv => report.ViewScores[sv.ViewId]).ToList();
                report.SheetScores[sheetGroup.Key] = scores.Count > 0
                    ? Math.Round(scores.Average(), 1) : 0.0;
            }

            // Per-discipline coverage: aggregate category scores by discipline mapping
            foreach (var kvp in DisciplineCategories)
            {
                var matchingScores = kvp.Value
                    .Where(cat => report.CategoryScores.ContainsKey(cat))
                    .Select(cat => report.CategoryScores[cat]).ToList();
                report.DisciplineScores[kvp.Key] = matchingScores.Count > 0
                    ? Math.Round(matchingScores.Average(), 1) : 0.0;
            }

            // Overall: average of all category scores
            var allScores = report.CategoryScores.Values.ToList();
            report.OverallScore = allScores.Count > 0
                ? Math.Round(allScores.Average(), 1) : 0.0;
            report.Trend = ComputeCoverageTrend();

            Logger.Debug("Coverage: overall {0}%, {1} categories, {2} views",
                report.OverallScore, report.CategoryScores.Count, report.ViewScores.Count);
            return report;
        }

        /// <summary>
        /// Analyzes quality score trends over the specified time period.
        /// </summary>
        /// <param name="daysBack">Number of days to look back for trend data.</param>
        public List<QualityTrend> GetQualityTrends(int daysBack = 30)
        {
            var trends = new List<QualityTrend>();
            DateTime cutoff = DateTime.UtcNow.AddDays(-daysBack);

            var recentSessions = _repository.GetSessionHistory(100)
                .Where(s => s.CompletedAt.HasValue && s.CompletedAt.Value >= cutoff)
                .OrderBy(s => s.CompletedAt.Value)
                .ToList();

            if (recentSessions.Count < 2) return trends;

            // Overall quality trend from session scores
            var overallValues = recentSessions
                .Select(s => new TimestampedValue(s.CompletedAt.Value, s.QualityScore)).ToList();
            trends.Add(BuildTrend("Overall Quality Score", overallValues, null));

            // Per-category and per-issue-type trends from recorded snapshots
            List<QualitySnapshot> snapshots;
            lock (_snapshotsLock)
            {
                snapshots = _qualitySnapshots
                    .Where(s => s.Timestamp >= cutoff)
                    .OrderBy(s => s.Timestamp).ToList();
            }

            if (snapshots.Count >= 2)
            {
                var categories = snapshots
                    .SelectMany(s => s.CategoryScores.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (string category in categories)
                {
                    var catValues = snapshots
                        .Where(s => s.CategoryScores.ContainsKey(category))
                        .Select(s => new TimestampedValue(s.Timestamp, s.CategoryScores[category]))
                        .ToList();
                    if (catValues.Count >= 2)
                        trends.Add(BuildTrend($"{category} Quality", catValues, category));
                }

                // Issue-type rate trends (orphan, clash, etc.)
                foreach (string issueType in new[] { "Orphan", "Clash", "Duplicate", "Blank", "Stale" })
                {
                    var issueValues = snapshots
                        .Where(s => s.IssueRates.ContainsKey(issueType))
                        .Select(s => new TimestampedValue(s.Timestamp, s.IssueRates[issueType]))
                        .ToList();
                    if (issueValues.Count >= 2)
                    {
                        var trend = BuildTrend($"{issueType} Rate", issueValues, null);
                        // For issue rates, declining is actually improving
                        if (trend.Direction == TrendDirection.Declining)
                            trend.Direction = TrendDirection.Improving;
                        else if (trend.Direction == TrendDirection.Improving)
                            trend.Direction = TrendDirection.Declining;
                        trends.Add(trend);
                    }
                }
            }

            // Tags-per-session productivity trend
            var tpsValues = recentSessions
                .Where(s => s.TagsPlaced > 0)
                .Select(s => new TimestampedValue(s.CompletedAt.Value, s.TagsPlaced)).ToList();
            if (tpsValues.Count >= 2)
                trends.Add(BuildTrend("Tags Per Session", tpsValues, null));

            Logger.Debug("Quality trends: {0} from {1} sessions, {2} snapshots",
                trends.Count, recentSessions.Count, snapshots.Count);
            return trends;
        }

        /// <summary>
        /// Computes productivity metrics from session history and correction data.
        /// </summary>
        public ProductivityReport GetProductivityMetrics()
        {
            var report = new ProductivityReport();
            var corrections = _repository.GetCorrections();
            var allTags = _repository.GetAllTags();

            var completedSessions = _repository.GetSessionHistory(100)
                .Where(s => s.CompletedAt.HasValue && s.TagsPlaced > 0)
                .OrderBy(s => s.CompletedAt.Value).ToList();

            report.TotalSessions = completedSessions.Count;
            report.TotalCorrections = corrections.Count;
            if (completedSessions.Count == 0) return report;

            // Tags per session and per hour
            report.TotalTagsPlaced = completedSessions.Sum(s => s.TagsPlaced);
            report.TagsPerSession = (double)report.TotalTagsPlaced / completedSessions.Count;

            double totalHours = completedSessions
                .Select(s => (s.CompletedAt.Value - s.StartedAt).TotalHours)
                .Where(h => h > 0 && h < 24) // filter outliers
                .Sum();
            report.TagsPerHour = totalHours > 0 ? report.TotalTagsPlaced / totalHours : 0;

            // Average placement score from active tags
            var activeTags = allTags.Where(t => t.State == TagState.Active).ToList();
            report.AveragePlacementScore = activeTags.Count > 0
                ? Math.Round(activeTags.Average(t => t.PlacementScore) * 100.0, 1) : 0;

            // Correction rate
            report.CorrectionRate = report.TotalTagsPlaced > 0
                ? Math.Round((double)corrections.Count / report.TotalTagsPlaced, 3) : 0;

            // Time saved: (total tags * 30s manual) - actual session time
            double manualMinutes = report.TotalTagsPlaced * ManualSecondsPerTag / 60.0;
            report.EstimatedTimeSavedMinutes = Math.Max(0,
                Math.Round(manualMinutes - totalHours * 60.0, 1));

            // Score and correction rate trends (need >= 3 sessions)
            if (completedSessions.Count >= 3)
            {
                var scoreValues = completedSessions.Select(s => s.QualityScore).ToList();
                var scoreTrend = ComputeLinearTrend(scoreValues);
                report.PlacementScoreTrend = scoreTrend.slope > 0.5
                    ? TrendDirection.Improving
                    : scoreTrend.slope < -0.5 ? TrendDirection.Declining : TrendDirection.Stable;

                // Per-session correction rate trend
                var sessionDates = completedSessions.Select(s => s.CompletedAt.Value).ToList();
                var corrRates = new List<double>();
                for (int i = 0; i < sessionDates.Count; i++)
                {
                    DateTime start = i > 0 ? sessionDates[i - 1] : sessionDates[i].AddHours(-1);
                    int windowCorr = corrections.Count(c =>
                        c.CorrectedAt >= start && c.CorrectedAt <= sessionDates[i]);
                    int windowTags = completedSessions[i].TagsPlaced;
                    corrRates.Add(windowTags > 0 ? (double)windowCorr / windowTags : 0);
                }
                var corrTrend = ComputeLinearTrend(corrRates);
                report.CorrectionRateTrend = corrTrend.slope < -0.01
                    ? TrendDirection.Improving
                    : corrTrend.slope > 0.01 ? TrendDirection.Declining : TrendDirection.Stable;
            }

            Logger.Debug("Productivity: {0:F1} tags/session, {1:F0} tags/hour, " +
                "{2}% avg score, {3:P1} correction rate",
                report.TagsPerSession, report.TagsPerHour,
                report.AveragePlacementScore, report.CorrectionRate);
            return report;
        }

        /// <summary>
        /// Discovers tagging patterns: template usage, category scoring,
        /// re-tag instability, rule group effectiveness, and user preferences.
        /// </summary>
        public List<PatternInsight> DiscoverPatterns()
        {
            var insights = new List<PatternInsight>();
            var allTags = _repository.GetAllTags();
            var corrections = _repository.GetCorrections();
            var operations = _repository.GetRecentOperations(500);
            if (allTags.Count == 0) return insights;

            // Pattern 1: Most commonly used tag templates with quality correlation
            var templateUsage = allTags
                .Where(t => !string.IsNullOrEmpty(t.CreatedByTemplate))
                .GroupBy(t => t.CreatedByTemplate, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()).Take(5);

            foreach (var group in templateUsage)
            {
                double avgScore = group.Average(t => t.PlacementScore);
                insights.Add(new PatternInsight
                {
                    Description = $"Template '{group.Key}' used {group.Count()} times " +
                        $"with average placement score {avgScore:F2}.",
                    Metric = group.Count(),
                    Significance = Math.Min(1.0, group.Count() / (double)allTags.Count),
                    Recommendation = avgScore < 0.6
                        ? $"Consider revising template '{group.Key}' - low average quality."
                        : $"Template '{group.Key}' is performing well.",
                    PatternCategory = "TemplateUsage"
                });
            }

            // Pattern 2: Categories with highest/lowest quality scores
            var categoryScores = allTags
                .Where(t => !string.IsNullOrEmpty(t.CategoryName) && t.State == TagState.Active)
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Cat = g.Key, Avg = g.Average(t => t.PlacementScore), N = g.Count() })
                .Where(x => x.N >= 3).OrderBy(x => x.Avg).ToList();

            if (categoryScores.Count > 0)
            {
                var worst = categoryScores.First();
                insights.Add(new PatternInsight
                {
                    Description = $"'{worst.Cat}' has the lowest average placement score " +
                        $"({worst.Avg:F2}) across {worst.N} tags.",
                    Metric = worst.Avg, Significance = 0.8,
                    Recommendation = $"Review tag templates for '{worst.Cat}' or adjust " +
                        "placement rules for better positioning.",
                    PatternCategory = "CategoryQuality"
                });
                if (categoryScores.Count > 1)
                {
                    var best = categoryScores.Last();
                    insights.Add(new PatternInsight
                    {
                        Description = $"'{best.Cat}' has the highest average placement score " +
                            $"({best.Avg:F2}) across {best.N} tags.",
                        Metric = best.Avg, Significance = 0.6,
                        Recommendation = $"Use '{best.Cat}' template settings as a reference " +
                            "for underperforming categories.",
                        PatternCategory = "CategoryQuality"
                    });
                }
            }

            // Pattern 3: Elements frequently re-tagged (instability indicator)
            var reTagged = operations
                .Where(o => o.Type == TagOperationType.Delete || o.Type == TagOperationType.Create)
                .Where(o => o.PreviousState != null || o.NewState != null)
                .Select(o => o.PreviousState?.HostElementId ?? o.NewState?.HostElementId ?? 0)
                .Where(id => id > 0).GroupBy(id => id)
                .Where(g => g.Count() >= 3).OrderByDescending(g => g.Count()).Take(5);

            foreach (var group in reTagged)
            {
                string catName = allTags.FirstOrDefault(
                    t => t.HostElementId == group.Key)?.CategoryName ?? "Unknown";
                insights.Add(new PatternInsight
                {
                    Description = $"Element {group.Key} ({catName}) re-tagged {group.Count()} " +
                        "times - potential instability.",
                    Metric = group.Count(),
                    Significance = Math.Min(1.0, group.Count() / 10.0),
                    Recommendation = $"Investigate placement issues for element {group.Key}. " +
                        "Consider pinning the tag or adjusting the template.",
                    PatternCategory = "Instability"
                });
            }

            // Pattern 4: Rule group effectiveness correlation
            var ruleScores = allTags
                .Where(t => !string.IsNullOrEmpty(t.CreatedByRule) && t.State == TagState.Active)
                .GroupBy(t => t.CreatedByRule, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 5)
                .Select(g => new { Rule = g.Key, Avg = g.Average(t => t.PlacementScore) })
                .OrderByDescending(x => x.Avg).ToList();

            if (ruleScores.Count >= 2)
            {
                var best = ruleScores.First();
                var worst = ruleScores.Last();
                insights.Add(new PatternInsight
                {
                    Description = $"Rule '{best.Rule}' produces best scores ({best.Avg:F2} avg) " +
                        $"vs '{worst.Rule}' ({worst.Avg:F2} avg).",
                    Metric = best.Avg - worst.Avg,
                    Significance = Math.Min(1.0, (best.Avg - worst.Avg) * 2.0),
                    Recommendation = $"Refine rule '{worst.Rule}' using settings from " +
                        $"'{best.Rule}' as a baseline.",
                    PatternCategory = "Correlation"
                });
            }

            // Pattern 5: User correction direction preferences
            if (corrections.Count >= 5)
            {
                var dirCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in corrections)
                {
                    if (c.OriginalPlacement?.Position != null &&
                        c.CorrectedPlacement?.Position != null)
                    {
                        double dx = c.CorrectedPlacement.Position.X - c.OriginalPlacement.Position.X;
                        double dy = c.CorrectedPlacement.Position.Y - c.OriginalPlacement.Position.Y;
                        string dir = GetPrimaryDirection(dx, dy);
                        dirCounts.TryGetValue(dir, out int cnt);
                        dirCounts[dir] = cnt + 1;
                    }
                }
                if (dirCounts.Count > 0)
                {
                    var dominant = dirCounts.OrderByDescending(kv => kv.Value).First();
                    double pct = (double)dominant.Value / corrections.Count * 100.0;
                    if (pct >= 40.0)
                    {
                        insights.Add(new PatternInsight
                        {
                            Description = $"User corrections predominantly move tags to the " +
                                $"{dominant.Key} ({pct:F0}% of {corrections.Count} corrections).",
                            Metric = pct,
                            Significance = Math.Min(1.0, pct / 100.0),
                            Recommendation = $"Update default tag offset to favor " +
                                $"{dominant.Key}-side placement to reduce corrections.",
                            PatternCategory = "UserPreference"
                        });
                    }
                }
            }

            Logger.Debug("Pattern discovery: {0} insights", insights.Count);
            return insights;
        }

        /// <summary>
        /// Generates prioritized, actionable recommendations based on coverage gaps,
        /// quality issues, productivity problems, and configuration opportunities.
        /// </summary>
        /// <param name="maxCount">Maximum recommendations to return.</param>
        public List<Recommendation> GetRecommendations(int maxCount = 10)
        {
            var recs = new List<Recommendation>();
            var allTags = _repository.GetAllTags();
            var corrections = _repository.GetCorrections();
            var templates = _repository.GetTemplates();

            if (allTags.Count == 0)
            {
                recs.Add(new Recommendation
                {
                    Type = RecommendationType.Coverage, Priority = 100.0,
                    Description = "No tags exist in the project.",
                    SuggestedAction = "Run 'Tag All' to begin automated tagging.",
                    EstimatedImpact = "Full project tagging coverage.", Scope = "Project"
                });
                return recs;
            }

            // Category stats for coverage and quality analysis
            var catStats = allTags
                .Where(t => !string.IsNullOrEmpty(t.CategoryName) &&
                            t.State != TagState.MarkedForDeletion)
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new
                {
                    Hosts = g.Select(t => t.HostElementId).Distinct().Count(),
                    Count = g.Count(),
                    Avg = g.Average(t => t.PlacementScore)
                }, StringComparer.OrdinalIgnoreCase);

            // Coverage: categories with templates but zero tags
            foreach (var tmpl in templates)
            {
                if (string.IsNullOrEmpty(tmpl.CategoryName)) continue;
                if (!catStats.ContainsKey(tmpl.CategoryName))
                {
                    int est = EstimateTotalElementsForCategory(tmpl.CategoryName, allTags);
                    recs.Add(new Recommendation
                    {
                        Type = RecommendationType.Coverage, Priority = est,
                        Description = $"'{tmpl.CategoryName}' has a template but zero tags.",
                        SuggestedAction = $"Run 'Tag All {tmpl.CategoryName}' using '{tmpl.Name}'.",
                        EstimatedImpact = est > 0 ? $"Would tag ~{est} elements."
                            : "Would tag all elements in this category.",
                        Scope = tmpl.CategoryName
                    });
                }
            }

            // Quality: low-scoring categories
            foreach (var kvp in catStats)
            {
                if (kvp.Value.Avg < 0.6 && kvp.Value.Count >= 3)
                {
                    double gap = 0.8 - kvp.Value.Avg;
                    recs.Add(new Recommendation
                    {
                        Type = RecommendationType.Quality,
                        Priority = gap * kvp.Value.Count,
                        Description = $"{kvp.Key} tag quality is low ({kvp.Value.Avg:F2} avg " +
                            $"across {kvp.Value.Count} tags).",
                        SuggestedAction = $"Review template and rules for '{kvp.Key}'. " +
                            "Consider cluster detection or adjusted clearance.",
                        EstimatedImpact = $"Could improve {kvp.Value.Count} tags.",
                        Scope = kvp.Key
                    });
                }
            }

            // Productivity: high correction rate
            if (corrections.Count > 0)
            {
                double corrRate = (double)corrections.Count / allTags.Count;
                if (corrRate > 0.15)
                {
                    var topCat = corrections
                        .Where(c => !string.IsNullOrEmpty(c.CategoryName))
                        .GroupBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count()).FirstOrDefault();

                    recs.Add(new Recommendation
                    {
                        Type = RecommendationType.Productivity,
                        Priority = corrRate * allTags.Count,
                        Description = $"Correction rate is {corrRate:P1} ({corrections.Count}/{allTags.Count})." +
                            (topCat != null ? $" Highest: {topCat.Key} ({topCat.Count()})." : ""),
                        SuggestedAction = topCat != null
                            ? $"Update template for '{topCat.Key}' to match learned preferences."
                            : "Review placement templates to reduce adjustments.",
                        EstimatedImpact = $"Could reduce ~{(int)(corrections.Count * 0.5)} corrections.",
                        Scope = topCat?.Key ?? "Project"
                    });
                }
            }

            // Quality: orphan and stale cleanup
            int orphans = allTags.Count(t => t.State == TagState.Orphaned);
            int stale = allTags.Count(t => t.State == TagState.Stale);
            if (orphans + stale > 0)
            {
                recs.Add(new Recommendation
                {
                    Type = RecommendationType.Quality,
                    Priority = orphans * 5.0 + stale * 2.0,
                    Description = $"Project has {orphans} orphaned and {stale} stale tags.",
                    SuggestedAction = "Run quality analysis with auto-fix to clean up.",
                    EstimatedImpact = $"Would resolve {orphans + stale} issues.",
                    Scope = "Project"
                });
            }

            // Configuration: learning disabled with corrections available
            if (!_configuration.Settings.LearningEnabled && corrections.Count >= 10)
            {
                recs.Add(new Recommendation
                {
                    Type = RecommendationType.Configuration,
                    Priority = corrections.Count * 0.5,
                    Description = $"Learning disabled but {corrections.Count} corrections recorded.",
                    SuggestedAction = "Enable learning engine for adaptive placements.",
                    EstimatedImpact = "Placements would adapt to user preferences.",
                    Scope = "Settings"
                });
            }

            // Configuration: cluster detection disabled with dense categories
            if (!_configuration.Settings.DefaultEnableClusterDetection)
            {
                bool hasDense = allTags
                    .Where(t => !string.IsNullOrEmpty(t.CategoryName))
                    .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                    .Any(g => g.Count() >= 10);

                if (hasDense)
                {
                    recs.Add(new Recommendation
                    {
                        Type = RecommendationType.Configuration, Priority = 15.0,
                        Description = "Cluster detection disabled but dense categories exist.",
                        SuggestedAction = "Enable cluster detection for improved layout.",
                        EstimatedImpact = "Reduces clashes in repeated element areas.",
                        Scope = "Settings"
                    });
                }
            }

            return recs.OrderByDescending(r => r.Priority).Take(maxCount).ToList();
        }

        /// <summary>
        /// Generates structured dashboard data: health score, category grid,
        /// activity timeline, top recommendations, and quick stats.
        /// </summary>
        public DashboardData GetDashboardData()
        {
            var dashboard = new DashboardData();
            var allTags = _repository.GetAllTags();
            var sessions = _repository.GetSessionHistory(20);

            // Project Health Score (composite: coverage 40%, quality 35%, consistency 25%)
            var coverage = GetCoverageReport();
            double quality = ComputeAverageQualityFromTags(allTags);
            double consistency = ComputeConsistencyScore(allTags);
            double composite = Math.Round(Math.Max(0, Math.Min(100,
                coverage.OverallScore * CoverageWeight +
                quality * QualityWeight +
                consistency * ConsistencyWeight)), 1);

            dashboard.HealthScore = new ProjectHealthScore
            {
                OverallScore = composite,
                CoverageScore = Math.Round(coverage.OverallScore, 1),
                QualityScore = Math.Round(quality, 1),
                ConsistencyScore = Math.Round(consistency, 1),
                Status = GetHealthStatus(composite)
            };

            // Category Health Grid
            var activeTags = allTags
                .Where(t => t.State == TagState.Active && !string.IsNullOrEmpty(t.CategoryName))
                .ToList();

            foreach (var group in activeTags
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()))
            {
                int hosts = group.Select(t => t.HostElementId).Distinct().Count();
                int total = EstimateTotalElementsForCategory(group.Key, allTags);
                double covPct = total > 0
                    ? Math.Min(100.0, (double)hosts / total * 100.0) : 100.0;
                double avgQ = group.Average(t => t.PlacementScore) * 100.0;
                double health = covPct * 0.5 + avgQ * 0.5;

                dashboard.CategoryHealthGrid.Add(new CategoryHealth
                {
                    CategoryName = group.Key,
                    TaggedCount = hosts, TotalCount = total,
                    CoveragePercent = Math.Round(covPct, 1),
                    AverageQuality = Math.Round(avgQ, 1),
                    Status = GetHealthStatus(health)
                });
            }

            // Recent Activity Timeline (last 20 sessions)
            foreach (var s in sessions.Take(20))
            {
                dashboard.RecentActivity.Add(new ActivityEntry
                {
                    Timestamp = s.CompletedAt ?? s.StartedAt,
                    Description = s.Description ?? "Tagging session",
                    TagCount = s.TagsPlaced,
                    QualityScore = s.QualityScore,
                    SessionId = s.SessionId
                });
            }

            dashboard.TopRecommendations = GetRecommendations(5);

            // Quick Stats
            dashboard.QuickStats["Total Tags"] = allTags.Count.ToString("N0");
            dashboard.QuickStats["Active Tags"] = activeTags.Count.ToString("N0");
            dashboard.QuickStats["Coverage"] = $"{coverage.OverallScore:F1}%";
            dashboard.QuickStats["Health Score"] = $"{composite:F0}/100";
            dashboard.QuickStats["Categories"] = dashboard.CategoryHealthGrid.Count.ToString();
            dashboard.QuickStats["Sessions"] = sessions.Count.ToString();

            int orphans = allTags.Count(t => t.State == TagState.Orphaned);
            if (orphans > 0) dashboard.QuickStats["Orphaned Tags"] = orphans.ToString();
            int pending = allTags.Count(t => t.State == TagState.PendingReview);
            if (pending > 0) dashboard.QuickStats["Pending Review"] = pending.ToString();

            return dashboard;
        }

        /// <summary>
        /// Computes the project health score (0-100) as a weighted composite of
        /// coverage (40%), quality (35%), and consistency (25%).
        /// </summary>
        public double GetProjectHealthScore()
        {
            var allTags = _repository.GetAllTags();
            if (allTags.Count == 0) return 0.0;

            var coverage = GetCoverageReport();
            double quality = ComputeAverageQualityFromTags(allTags);
            double consistency = ComputeConsistencyScore(allTags);

            return Math.Round(Math.Max(0, Math.Min(100,
                coverage.OverallScore * CoverageWeight +
                quality * QualityWeight +
                consistency * ConsistencyWeight)), 1);
        }

        /// <summary>
        /// Records a quality analysis snapshot for trend tracking. Call after each
        /// TagQualityAnalyzer run to accumulate historical data points.
        /// </summary>
        public void RecordQualitySnapshot(TagQualityReport report)
        {
            if (report == null) return;

            var snapshot = new QualitySnapshot
            {
                Timestamp = report.GeneratedAt,
                OverallScore = report.QualityScore,
                TotalTags = report.TotalTagsAnalyzed,
                TotalIssues = report.Issues.Count
            };

            // Per-category scores from view scores grouped by tags in those views
            var allTags = _repository.GetAllTags();
            var catGroups = allTags
                .Where(t => !string.IsNullOrEmpty(t.CategoryName) &&
                            report.ViewScores.ContainsKey(t.ViewId))
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in catGroups)
            {
                double avgViewScore = group
                    .Select(t => t.ViewId).Distinct()
                    .Where(v => report.ViewScores.ContainsKey(v))
                    .Select(v => report.ViewScores[v])
                    .DefaultIfEmpty(0).Average();
                snapshot.CategoryScores[group.Key] = avgViewScore;
            }

            // Issue rates normalized by total tags
            if (report.TotalTagsAnalyzed > 0)
            {
                foreach (var kvp in report.IssueCountsByType)
                    snapshot.IssueRates[kvp.Key.ToString()] =
                        (double)kvp.Value / report.TotalTagsAnalyzed * 100.0;
            }

            lock (_snapshotsLock)
            {
                _qualitySnapshots.Add(snapshot);
                DateTime retention = DateTime.UtcNow.AddDays(-365);
                _qualitySnapshots.RemoveAll(s => s.Timestamp < retention);
            }

            Logger.Debug("Snapshot recorded: score {0}, {1} tags, {2} issues",
                snapshot.OverallScore, snapshot.TotalTags, snapshot.TotalIssues);
        }

        #endregion

        #region Coverage Internals

        /// <summary>
        /// Estimates total taggable elements for a category from metadata or heuristic.
        /// </summary>
        private int EstimateTotalElementsForCategory(string categoryName, List<TagInstance> allTags)
        {
            var catTags = allTags
                .Where(t => string.Equals(t.CategoryName, categoryName,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            if (catTags.Count == 0) return 0;

            int uniqueHosts = catTags.Select(t => t.HostElementId).Distinct().Count();

            // Check metadata for a total count hint from a prior model scan
            var withTotal = catTags.FirstOrDefault(
                t => t.Metadata != null && t.Metadata.ContainsKey("CategoryTotalElements"));
            if (withTotal != null &&
                int.TryParse(withTotal.Metadata["CategoryTotalElements"].ToString(),
                    out int total) && total > 0)
                return Math.Max(total, uniqueHosts);

            // Heuristic: assume ~20% more elements exist untagged
            return Math.Max(uniqueHosts, (int)(uniqueHosts * 1.2));
        }

        /// <summary>
        /// Determines coverage trend by comparing recent vs. older session scores.
        /// </summary>
        private TrendDirection ComputeCoverageTrend()
        {
            var completed = _repository.GetSessionHistory(20)
                .Where(s => s.CompletedAt.HasValue && s.TagsPlaced > 0)
                .OrderBy(s => s.CompletedAt.Value).ToList();

            if (completed.Count < 4) return TrendDirection.Stable;

            int half = completed.Count / 2;
            double olderAvg = completed.Take(half).Average(s => s.QualityScore);
            double newerAvg = completed.Skip(half).Average(s => s.QualityScore);
            double delta = newerAvg - olderAvg;

            if (delta > 2.0) return TrendDirection.Improving;
            if (delta < -2.0) return TrendDirection.Declining;
            return TrendDirection.Stable;
        }

        #endregion

        #region Trend Internals

        /// <summary>
        /// Builds a quality trend from timestamped values using linear regression.
        /// </summary>
        private QualityTrend BuildTrend(string name, List<TimestampedValue> values, string category)
        {
            var trend = new QualityTrend
            {
                MetricName = name, Values = values, CategoryName = category
            };

            if (values.Count < 2)
            {
                trend.Direction = TrendDirection.Stable;
                trend.Forecast = values.Count > 0 ? values.Last().Value : 0;
                return trend;
            }

            var yVals = values.Select(v => v.Value).ToList();
            var (slope, intercept, rSq) = ComputeLinearTrend(yVals);
            trend.IsSignificant = rSq >= 0.5;

            if (trend.IsSignificant)
            {
                double mean = yVals.Average();
                double normSlope = mean > 0 ? slope / mean : slope;
                trend.Direction = normSlope > 0.02 ? TrendDirection.Improving
                    : normSlope < -0.02 ? TrendDirection.Declining : TrendDirection.Stable;
            }
            else
            {
                trend.Direction = TrendDirection.Stable;
            }

            trend.Forecast = Math.Round(intercept + slope * values.Count, 2);
            return trend;
        }

        /// <summary>
        /// Simple linear regression on y-values indexed 0..n-1.
        /// Returns (slope, intercept, rSquared).
        /// </summary>
        private (double slope, double intercept, double rSquared) ComputeLinearTrend(
            List<double> yValues)
        {
            int n = yValues.Count;
            if (n < 2) return (0, yValues.FirstOrDefault(), 0);

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += yValues[i];
                sumXY += i * yValues[i];
                sumX2 += i * (double)i;
            }

            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-10) return (0, sumY / n, 0);

            double slope = (n * sumXY - sumX * sumY) / denom;
            double intercept = (sumY - slope * sumX) / n;

            double meanY = sumY / n;
            double ssTot = 0, ssRes = 0;
            for (int i = 0; i < n; i++)
            {
                double pred = intercept + slope * i;
                ssTot += (yValues[i] - meanY) * (yValues[i] - meanY);
                ssRes += (yValues[i] - pred) * (yValues[i] - pred);
            }

            double rSq = ssTot > 1e-10 ? Math.Max(0, 1.0 - ssRes / ssTot) : 0;
            return (slope, intercept, rSq);
        }

        #endregion

        #region Dashboard Internals

        /// <summary>
        /// Average quality from active tag placement scores, scaled to 0-100.
        /// </summary>
        private double ComputeAverageQualityFromTags(List<TagInstance> allTags)
        {
            var active = allTags.Where(t => t.State == TagState.Active).ToList();
            if (active.Count == 0) return 0;
            return Math.Max(0, Math.Min(100, active.Average(t => t.PlacementScore) * 100.0));
        }

        /// <summary>
        /// Consistency score (0-100): how uniform placement quality is across categories.
        /// Low standard deviation of per-category averages = high consistency.
        /// </summary>
        private double ComputeConsistencyScore(List<TagInstance> allTags)
        {
            var catAvgs = allTags
                .Where(t => t.State == TagState.Active && !string.IsNullOrEmpty(t.CategoryName))
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Average(t => t.PlacementScore))
                .ToList();

            if (catAvgs.Count < 2) return 100.0;

            double mean = catAvgs.Average();
            double stdDev = Math.Sqrt(catAvgs.Sum(v => (v - mean) * (v - mean)) / catAvgs.Count);
            // Map: 0 stdDev = 100, 0.5 stdDev = 0
            return Math.Round(Math.Max(0, 100.0 * (1.0 - stdDev / 0.5)), 1);
        }

        private HealthStatus GetHealthStatus(double score)
        {
            if (score >= GreenThreshold) return HealthStatus.Green;
            if (score >= YellowThreshold) return HealthStatus.Yellow;
            return HealthStatus.Red;
        }

        #endregion

        #region Pattern Discovery Internals

        /// <summary>
        /// Determines the primary cardinal direction of a displacement vector.
        /// </summary>
        private string GetPrimaryDirection(double dx, double dy)
        {
            if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) return "Center";
            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx > 0 ? "Right" : "Left";
            return dy > 0 ? "Up" : "Down";
        }

        #endregion

        #region Inner Types

        /// <summary>
        /// Internal snapshot of quality metrics at a point in time for trend analysis.
        /// </summary>
        private class QualitySnapshot
        {
            public DateTime Timestamp { get; set; }
            public double OverallScore { get; set; }
            public int TotalTags { get; set; }
            public int TotalIssues { get; set; }
            public Dictionary<string, double> CategoryScores { get; set; } =
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, double> IssueRates { get; set; } =
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
