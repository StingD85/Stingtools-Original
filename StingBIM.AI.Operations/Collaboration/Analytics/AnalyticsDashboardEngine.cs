// ===================================================================
// StingBIM.AI.Collaboration - Analytics Dashboard Engine
// Comprehensive dashboard system for project KPIs, metrics tracking,
// trend analysis, forecasting, and multi-format reporting
// Supports real-time data aggregation and widget-based composition
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Collaboration.Analytics
{
    #region Enumerations

    /// <summary>
    /// Dashboard types for different analysis perspectives
    /// </summary>
    public enum DashboardType
    {
        ProjectOverview,
        SafetyMetrics,
        QualityMetrics,
        SchedulePerformance,
        CostAnalysis,
        TeamProductivity,
        IssueTracking,
        DocumentMetrics
    }

    /// <summary>
    /// Chart types for widget visualization
    /// </summary>
    public enum ChartType
    {
        Line,
        Bar,
        Pie,
        Gauge,
        Table,
        Heatmap,
        Area,
        Scatter,
        KPI,
        Sparkline
    }

    /// <summary>
    /// Time aggregation granularity
    /// </summary>
    public enum TimeGranularity
    {
        Hourly,
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        Yearly
    }

    /// <summary>
    /// Metric status based on threshold comparison
    /// </summary>
    public enum MetricStatus
    {
        Excellent,
        Good,
        Warning,
        Critical,
        Unknown
    }

    /// <summary>
    /// Trend direction indicator
    /// </summary>
    public enum TrendIndicator
    {
        StrongUp,
        Up,
        Stable,
        Down,
        StrongDown
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum ExportFormat
    {
        PDF,
        Excel,
        PowerPoint,
        CSV,
        JSON,
        HTML
    }

    /// <summary>
    /// Comparison type for analytics
    /// </summary>
    public enum ComparisonType
    {
        VsBaseline,
        VsPreviousPeriod,
        VsSimilarProjects,
        VsIndustryBenchmark,
        VsTarget
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Dashboard configuration and state
    /// </summary>
    public class Dashboard
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DashboardType Type { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime LastRefreshed { get; set; }
        public List<DashboardWidget> Widgets { get; set; } = new();
        public List<DashboardFilter> Filters { get; set; } = new();
        public DashboardLayout Layout { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public bool IsDefault { get; set; }
        public bool IsShared { get; set; }
        public List<string> SharedWith { get; set; } = new();
        public int RefreshIntervalSeconds { get; set; } = 300;
    }

    /// <summary>
    /// Dashboard widget configuration
    /// </summary>
    public class DashboardWidget
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ChartType ChartType { get; set; }
        public string MetricId { get; set; } = string.Empty;
        public List<string> MetricIds { get; set; } = new();
        public WidgetPosition Position { get; set; } = new();
        public WidgetSize Size { get; set; } = new();
        public TimeGranularity Granularity { get; set; } = TimeGranularity.Daily;
        public int DataPointsLimit { get; set; } = 30;
        public Dictionary<string, object> ChartOptions { get; set; } = new();
        public List<MetricThreshold> Thresholds { get; set; } = new();
        public bool ShowTrend { get; set; } = true;
        public bool ShowTarget { get; set; } = true;
        public string DataSource { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
    }

    /// <summary>
    /// Widget position in grid layout
    /// </summary>
    public class WidgetPosition
    {
        public int Row { get; set; }
        public int Column { get; set; }
    }

    /// <summary>
    /// Widget size in grid units
    /// </summary>
    public class WidgetSize
    {
        public int Width { get; set; } = 4;
        public int Height { get; set; } = 3;
    }

    /// <summary>
    /// Dashboard layout configuration
    /// </summary>
    public class DashboardLayout
    {
        public int Columns { get; set; } = 12;
        public int RowHeight { get; set; } = 100;
        public string Theme { get; set; } = "default";
        public bool CompactMode { get; set; }
        public bool AutoRefresh { get; set; } = true;
    }

    /// <summary>
    /// Dashboard filter definition
    /// </summary>
    public class DashboardFilter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public FilterType FilterType { get; set; }
        public object? DefaultValue { get; set; }
        public object? CurrentValue { get; set; }
        public List<FilterOption> Options { get; set; } = new();
        public DateTime? DateRangeStart { get; set; }
        public DateTime? DateRangeEnd { get; set; }
        public List<string> SelectedCategories { get; set; } = new();
    }

    /// <summary>
    /// Filter type enumeration
    /// </summary>
    public enum FilterType
    {
        DateRange,
        Category,
        MultiSelect,
        SingleSelect,
        Text,
        Numeric
    }

    /// <summary>
    /// Filter option for dropdowns
    /// </summary>
    public class FilterOption
    {
        public string Label { get; set; } = string.Empty;
        public object Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Metric definition with value and metadata
    /// </summary>
    public class Metric
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Value { get; set; }
        public double? PreviousValue { get; set; }
        public double? TargetValue { get; set; }
        public double? BaselineValue { get; set; }
        public TrendIndicator Trend { get; set; }
        public double TrendPercentage { get; set; }
        public MetricStatus Status { get; set; }
        public List<MetricThreshold> Thresholds { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public bool IsCalculated { get; set; }
        public string Formula { get; set; } = string.Empty;
    }

    /// <summary>
    /// Metric threshold for status determination
    /// </summary>
    public class MetricThreshold
    {
        public string Name { get; set; } = string.Empty;
        public MetricStatus Status { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    /// <summary>
    /// Time series data point
    /// </summary>
    public class TimeSeriesDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public double? LowerBound { get; set; }
        public double? UpperBound { get; set; }
        public bool IsForecasted { get; set; }
        public double? Confidence { get; set; }
        public Dictionary<string, double> Dimensions { get; set; } = new();
        public string? Label { get; set; }
    }

    /// <summary>
    /// Time series data collection
    /// </summary>
    public class TimeSeries
    {
        public string MetricId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public TimeGranularity Granularity { get; set; }
        public List<TimeSeriesDataPoint> DataPoints { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Average { get; set; }
        public double Sum { get; set; }
        public int Count { get; set; }
        public TrendAnalysisResult? TrendAnalysis { get; set; }
        public ForecastResult? Forecast { get; set; }
    }

    /// <summary>
    /// Trend analysis result
    /// </summary>
    public class TrendAnalysisResult
    {
        public TrendIndicator Direction { get; set; }
        public double Slope { get; set; }
        public double RSquared { get; set; }
        public double PercentageChange { get; set; }
        public List<ChangePoint> ChangePoints { get; set; } = new();
        public SeasonalityInfo? Seasonality { get; set; }
        public double Volatility { get; set; }
    }

    /// <summary>
    /// Change point detection result
    /// </summary>
    public class ChangePoint
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public double Magnitude { get; set; }
        public string? Cause { get; set; }
    }

    /// <summary>
    /// Seasonality information
    /// </summary>
    public class SeasonalityInfo
    {
        public string Period { get; set; } = string.Empty;
        public double Strength { get; set; }
        public Dictionary<int, double> SeasonalFactors { get; set; } = new();
    }

    /// <summary>
    /// Forecast result
    /// </summary>
    public class ForecastResult
    {
        public List<TimeSeriesDataPoint> Predictions { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double MAPE { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Comparative analysis result
    /// </summary>
    public class ComparativeAnalysis
    {
        public string MetricId { get; set; } = string.Empty;
        public ComparisonType ComparisonType { get; set; }
        public double CurrentValue { get; set; }
        public double ComparisonValue { get; set; }
        public double Difference { get; set; }
        public double PercentageDifference { get; set; }
        public MetricStatus RelativeStatus { get; set; }
        public string Interpretation { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Analytics report container
    /// </summary>
    public class AnalyticsReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DashboardType ReportType { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public List<ReportSection> Sections { get; set; } = new();
        public Dictionary<string, Metric> KPIs { get; set; } = new();
        public List<string> ExecutiveSummary { get; set; } = new();
        public List<string> KeyFindings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<ReportChart> Charts { get; set; } = new();
        public byte[]? ExportedContent { get; set; }
        public ExportFormat? ExportFormat { get; set; }
    }

    /// <summary>
    /// Report section
    /// </summary>
    public class ReportSection
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<Metric> Metrics { get; set; } = new();
        public List<ReportChart> Charts { get; set; } = new();
        public List<ReportTable> Tables { get; set; } = new();
    }

    /// <summary>
    /// Report chart configuration
    /// </summary>
    public class ReportChart
    {
        public string Title { get; set; } = string.Empty;
        public ChartType ChartType { get; set; }
        public TimeSeries Data { get; set; } = new();
        public Dictionary<string, object> Options { get; set; } = new();
    }

    /// <summary>
    /// Report table data
    /// </summary>
    public class ReportTable
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Headers { get; set; } = new();
        public List<List<object>> Rows { get; set; } = new();
    }

    #endregion

    #region KPI Models

    /// <summary>
    /// Schedule Performance Index metrics
    /// </summary>
    public class SchedulePerformanceKPIs
    {
        public double SPI { get; set; }
        public double ScheduleVariance { get; set; }
        public double ScheduleVariancePercent { get; set; }
        public double PlannedProgress { get; set; }
        public double ActualProgress { get; set; }
        public int ActivitiesOnTrack { get; set; }
        public int ActivitiesBehind { get; set; }
        public int ActivitiesAhead { get; set; }
        public int MilestonesMet { get; set; }
        public int MilestonesMissed { get; set; }
        public int DaysBehindSchedule { get; set; }
        public DateTime? ProjectedCompletion { get; set; }
        public DateTime PlannedCompletion { get; set; }
        public List<CriticalActivity> CriticalPathActivities { get; set; } = new();
    }

    /// <summary>
    /// Critical path activity
    /// </summary>
    public class CriticalActivity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime PlannedEnd { get; set; }
        public DateTime? ProjectedEnd { get; set; }
        public int VarianceDays { get; set; }
        public double PercentComplete { get; set; }
    }

    /// <summary>
    /// Cost Performance Index metrics
    /// </summary>
    public class CostPerformanceKPIs
    {
        public double CPI { get; set; }
        public decimal Budget { get; set; }
        public decimal ActualCost { get; set; }
        public decimal EarnedValue { get; set; }
        public decimal PlannedValue { get; set; }
        public decimal CostVariance { get; set; }
        public double CostVariancePercent { get; set; }
        public decimal EstimateAtCompletion { get; set; }
        public decimal EstimateToComplete { get; set; }
        public decimal VarianceAtCompletion { get; set; }
        public decimal Contingency { get; set; }
        public decimal ContingencyUsed { get; set; }
        public double ContingencyUsedPercent { get; set; }
        public Dictionary<string, decimal> CostByCategory { get; set; } = new();
    }

    /// <summary>
    /// Safety metrics
    /// </summary>
    public class SafetyKPIs
    {
        public double TRIR { get; set; }
        public double DART { get; set; }
        public double LostTimeIncidentRate { get; set; }
        public int TotalIncidents { get; set; }
        public int RecordableIncidents { get; set; }
        public int FirstAidCases { get; set; }
        public int NearMisses { get; set; }
        public int SafetyObservations { get; set; }
        public double SafetyObservationRate { get; set; }
        public int DaysWithoutIncident { get; set; }
        public double ToolboxTalkAttendance { get; set; }
        public double SafetyTrainingCompliance { get; set; }
        public double PPECompliance { get; set; }
        public int OpenSafetyActions { get; set; }
        public int ClosedSafetyActions { get; set; }
        public double SafetyActionClosureRate { get; set; }
        public Dictionary<string, int> IncidentsByType { get; set; } = new();
        public Dictionary<string, int> IncidentsByTrade { get; set; } = new();
    }

    /// <summary>
    /// Quality metrics
    /// </summary>
    public class QualityKPIs
    {
        public double DefectRate { get; set; }
        public double InspectionPassRate { get; set; }
        public double FirstTimePassRate { get; set; }
        public int TotalInspections { get; set; }
        public int PassedInspections { get; set; }
        public int FailedInspections { get; set; }
        public int Defects { get; set; }
        public int DefectsResolved { get; set; }
        public double DefectResolutionRate { get; set; }
        public double AverageDefectResolutionTime { get; set; }
        public int NonConformanceReports { get; set; }
        public int OpenNCRs { get; set; }
        public double ReworkCost { get; set; }
        public double ReworkAsPercentOfCost { get; set; }
        public double CustomerSatisfactionScore { get; set; }
        public Dictionary<string, int> DefectsByType { get; set; } = new();
        public Dictionary<string, int> DefectsByTrade { get; set; } = new();
    }

    /// <summary>
    /// Team productivity metrics
    /// </summary>
    public class ProductivityKPIs
    {
        public int TasksCompleted { get; set; }
        public int TasksCreated { get; set; }
        public double TaskCompletionRate { get; set; }
        public double AverageTaskDuration { get; set; }
        public double OnTimeTaskCompletion { get; set; }
        public int RFIsSubmitted { get; set; }
        public int RFIsResolved { get; set; }
        public double RFIResponseTime { get; set; }
        public int SubmittalsReviewed { get; set; }
        public double SubmittalTurnaroundTime { get; set; }
        public int ChangeOrdersProcessed { get; set; }
        public double ChangeOrderCycleTime { get; set; }
        public double CollaborationScore { get; set; }
        public double ModelUpdateFrequency { get; set; }
        public int DocumentsUploaded { get; set; }
        public int CommentsResolved { get; set; }
        public Dictionary<string, double> ProductivityByTeam { get; set; } = new();
    }

    /// <summary>
    /// Issue tracking metrics
    /// </summary>
    public class IssueTrackingKPIs
    {
        public int TotalIssues { get; set; }
        public int OpenIssues { get; set; }
        public int ClosedIssues { get; set; }
        public int OverdueIssues { get; set; }
        public double IssueResolutionRate { get; set; }
        public double AverageResolutionTime { get; set; }
        public int CriticalIssues { get; set; }
        public int HighPriorityIssues { get; set; }
        public double AgeOfOldestIssue { get; set; }
        public int ClashesDetected { get; set; }
        public int ClashesResolved { get; set; }
        public double ClashResolutionRate { get; set; }
        public Dictionary<string, int> IssuesByCategory { get; set; } = new();
        public Dictionary<string, int> IssuesByPriority { get; set; } = new();
        public Dictionary<string, int> IssuesByAssignee { get; set; } = new();
    }

    /// <summary>
    /// Document metrics
    /// </summary>
    public class DocumentKPIs
    {
        public int TotalDocuments { get; set; }
        public int DocumentsThisPeriod { get; set; }
        public int PendingApprovals { get; set; }
        public double AverageApprovalTime { get; set; }
        public int DrawingsIssued { get; set; }
        public int DrawingRevisions { get; set; }
        public double RevisionRate { get; set; }
        public int SpecificationsApproved { get; set; }
        public int SubmittalsApproved { get; set; }
        public double SubmittalApprovalRate { get; set; }
        public int TransmittalsCount { get; set; }
        public long TotalStorageUsed { get; set; }
        public Dictionary<string, int> DocumentsByType { get; set; } = new();
        public Dictionary<string, int> DocumentsByDiscipline { get; set; } = new();
    }

    #endregion

    #region Analytics Dashboard Engine

    /// <summary>
    /// Comprehensive analytics dashboard engine for project KPIs, metrics tracking,
    /// trend analysis, forecasting, and reporting with real-time data aggregation.
    /// </summary>
    public class AnalyticsDashboardEngine : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Dashboard storage
        private readonly ConcurrentDictionary<string, Dashboard> _dashboards = new();
        private readonly ConcurrentDictionary<string, Metric> _metricCache = new();
        private readonly ConcurrentDictionary<string, TimeSeries> _timeSeriesCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
        private readonly ConcurrentDictionary<string, List<TimeSeriesDataPoint>> _rawData = new();

        // Project context
        private readonly string _projectId;
        private readonly string _projectName;

        // Cache settings
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _forecastHorizon = TimeSpan.FromDays(30);

        // Background refresh
        private CancellationTokenSource? _refreshCts;
        private Task? _refreshTask;
        private bool _isDisposed;

        /// <summary>
        /// Initialize the Analytics Dashboard Engine
        /// </summary>
        public AnalyticsDashboardEngine(string projectId, string projectName)
        {
            _projectId = projectId;
            _projectName = projectName;
            InitializeDefaultDashboards();
            Logger.Info($"AnalyticsDashboardEngine initialized for project: {projectName}");
        }

        #region Dashboard Management

        /// <summary>
        /// Get a dashboard by ID
        /// </summary>
        public async Task<Dashboard?> GetDashboardAsync(
            string dashboardId,
            CancellationToken ct = default)
        {
            if (_dashboards.TryGetValue(dashboardId, out var dashboard))
            {
                // Check if refresh needed
                if (DateTime.UtcNow - dashboard.LastRefreshed > TimeSpan.FromSeconds(dashboard.RefreshIntervalSeconds))
                {
                    await RefreshDashboardAsync(dashboardId, ct);
                }
                return dashboard;
            }
            return null;
        }

        /// <summary>
        /// Get dashboard by type
        /// </summary>
        public async Task<Dashboard?> GetDashboardByTypeAsync(
            DashboardType type,
            CancellationToken ct = default)
        {
            var dashboard = _dashboards.Values.FirstOrDefault(d => d.Type == type && d.ProjectId == _projectId);
            if (dashboard != null)
            {
                return await GetDashboardAsync(dashboard.Id, ct);
            }
            return null;
        }

        /// <summary>
        /// Get all dashboards for the project
        /// </summary>
        public List<Dashboard> GetAllDashboards()
        {
            return _dashboards.Values
                .Where(d => d.ProjectId == _projectId)
                .OrderBy(d => d.Type)
                .ToList();
        }

        /// <summary>
        /// Create a new dashboard
        /// </summary>
        public async Task<Dashboard> CreateDashboardAsync(
            string name,
            DashboardType type,
            string createdBy,
            List<DashboardWidget>? widgets = null,
            CancellationToken ct = default)
        {
            var dashboard = new Dashboard
            {
                Name = name,
                Type = type,
                ProjectId = _projectId,
                CreatedBy = createdBy,
                Widgets = widgets ?? GetDefaultWidgetsForType(type),
                Filters = GetDefaultFiltersForType(type)
            };

            _dashboards[dashboard.Id] = dashboard;
            await RefreshDashboardAsync(dashboard.Id, ct);

            Logger.Info($"Created dashboard: {name} ({type}) for project {_projectId}");
            return dashboard;
        }

        /// <summary>
        /// Update an existing dashboard
        /// </summary>
        public async Task<Dashboard?> UpdateDashboardAsync(
            string dashboardId,
            Action<Dashboard> updateAction,
            CancellationToken ct = default)
        {
            if (_dashboards.TryGetValue(dashboardId, out var dashboard))
            {
                updateAction(dashboard);
                dashboard.LastModified = DateTime.UtcNow;
                await RefreshDashboardAsync(dashboardId, ct);
                Logger.Info($"Updated dashboard: {dashboard.Name}");
                return dashboard;
            }
            return null;
        }

        /// <summary>
        /// Delete a dashboard
        /// </summary>
        public bool DeleteDashboard(string dashboardId)
        {
            if (_dashboards.TryRemove(dashboardId, out var dashboard))
            {
                Logger.Info($"Deleted dashboard: {dashboard.Name}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Refresh dashboard data
        /// </summary>
        public async Task RefreshDashboardAsync(
            string dashboardId,
            CancellationToken ct = default)
        {
            if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
                return;

            foreach (var widget in dashboard.Widgets)
            {
                try
                {
                    await RefreshWidgetDataAsync(widget, dashboard.Filters, ct);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error refreshing widget {widget.Id}");
                }
            }

            dashboard.LastRefreshed = DateTime.UtcNow;
        }

        private async Task RefreshWidgetDataAsync(
            DashboardWidget widget,
            List<DashboardFilter> filters,
            CancellationToken ct)
        {
            // Determine date range from filters
            var dateFilter = filters.FirstOrDefault(f => f.FilterType == FilterType.DateRange);
            var startDate = dateFilter?.DateRangeStart ?? DateTime.UtcNow.AddDays(-30);
            var endDate = dateFilter?.DateRangeEnd ?? DateTime.UtcNow;

            // Refresh time series if needed
            foreach (var metricId in widget.MetricIds.Concat(new[] { widget.MetricId }).Where(m => !string.IsNullOrEmpty(m)))
            {
                await GetTimeSeriesAsync(metricId, startDate, endDate, widget.Granularity, ct);
            }
        }

        #endregion

        #region Metric Operations

        /// <summary>
        /// Get a single metric by ID
        /// </summary>
        public async Task<Metric?> GetMetricAsync(
            string metricId,
            CancellationToken ct = default)
        {
            // Check cache
            if (_metricCache.TryGetValue(metricId, out var cachedMetric))
            {
                if (_cacheTimestamps.TryGetValue($"metric_{metricId}", out var timestamp))
                {
                    if (DateTime.UtcNow - timestamp < _cacheDuration)
                    {
                        return cachedMetric;
                    }
                }
            }

            // Calculate metric
            var metric = await CalculateMetricAsync(metricId, ct);
            if (metric != null)
            {
                _metricCache[metricId] = metric;
                _cacheTimestamps[$"metric_{metricId}"] = DateTime.UtcNow;
            }

            return metric;
        }

        /// <summary>
        /// Get multiple metrics
        /// </summary>
        public async Task<Dictionary<string, Metric>> GetMetricsAsync(
            IEnumerable<string> metricIds,
            CancellationToken ct = default)
        {
            var results = new Dictionary<string, Metric>();

            var tasks = metricIds.Select(async id =>
            {
                var metric = await GetMetricAsync(id, ct);
                return (id, metric);
            });

            var completed = await Task.WhenAll(tasks);

            foreach (var (id, metric) in completed)
            {
                if (metric != null)
                {
                    results[id] = metric;
                }
            }

            return results;
        }

        /// <summary>
        /// Get time series data for a metric
        /// </summary>
        public async Task<TimeSeries> GetTimeSeriesAsync(
            string metricId,
            DateTime startDate,
            DateTime endDate,
            TimeGranularity granularity = TimeGranularity.Daily,
            CancellationToken ct = default)
        {
            var cacheKey = $"{metricId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{granularity}";

            // Check cache
            if (_timeSeriesCache.TryGetValue(cacheKey, out var cached))
            {
                if (_cacheTimestamps.TryGetValue($"ts_{cacheKey}", out var timestamp))
                {
                    if (DateTime.UtcNow - timestamp < _cacheDuration)
                    {
                        return cached;
                    }
                }
            }

            // Build time series
            var timeSeries = await BuildTimeSeriesAsync(metricId, startDate, endDate, granularity, ct);

            _timeSeriesCache[cacheKey] = timeSeries;
            _cacheTimestamps[$"ts_{cacheKey}"] = DateTime.UtcNow;

            return timeSeries;
        }

        private async Task<Metric?> CalculateMetricAsync(string metricId, CancellationToken ct)
        {
            return metricId.ToLower() switch
            {
                "spi" => await CalculateSPIMetricAsync(ct),
                "cpi" => await CalculateCPIMetricAsync(ct),
                "trir" => await CalculateTRIRMetricAsync(ct),
                "defect_rate" => await CalculateDefectRateMetricAsync(ct),
                "task_completion_rate" => await CalculateTaskCompletionRateAsync(ct),
                "rfi_response_time" => await CalculateRFIResponseTimeAsync(ct),
                "clash_resolution_rate" => await CalculateClashResolutionRateAsync(ct),
                "progress" => await CalculateProgressMetricAsync(ct),
                _ => await CalculateGenericMetricAsync(metricId, ct)
            };
        }

        private async Task<Metric> CalculateSPIMetricAsync(CancellationToken ct)
        {
            // Simulate calculation - would query actual schedule data
            var plannedProgress = 0.45;
            var actualProgress = 0.42;
            var spi = actualProgress / plannedProgress;

            return new Metric
            {
                Id = "spi",
                Name = "Schedule Performance Index",
                Category = "Schedule",
                Unit = "",
                Value = Math.Round(spi, 2),
                TargetValue = 1.0,
                PreviousValue = 0.95,
                Trend = spi > 0.95 ? TrendIndicator.Stable : TrendIndicator.Down,
                TrendPercentage = ((spi - 0.95) / 0.95) * 100,
                Status = spi switch
                {
                    >= 1.0 => MetricStatus.Excellent,
                    >= 0.95 => MetricStatus.Good,
                    >= 0.85 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Thresholds = new List<MetricThreshold>
                {
                    new() { Name = "Excellent", Status = MetricStatus.Excellent, MinValue = 1.0, MaxValue = double.MaxValue, Color = "#28a745" },
                    new() { Name = "Good", Status = MetricStatus.Good, MinValue = 0.95, MaxValue = 1.0, Color = "#17a2b8" },
                    new() { Name = "Warning", Status = MetricStatus.Warning, MinValue = 0.85, MaxValue = 0.95, Color = "#ffc107" },
                    new() { Name = "Critical", Status = MetricStatus.Critical, MinValue = 0, MaxValue = 0.85, Color = "#dc3545" }
                },
                Description = "Ratio of earned value to planned value - measures schedule efficiency"
            };
        }

        private async Task<Metric> CalculateCPIMetricAsync(CancellationToken ct)
        {
            var earnedValue = 2150000m;
            var actualCost = 2250000m;
            var cpi = (double)(earnedValue / actualCost);

            return new Metric
            {
                Id = "cpi",
                Name = "Cost Performance Index",
                Category = "Cost",
                Unit = "",
                Value = Math.Round(cpi, 2),
                TargetValue = 1.0,
                PreviousValue = 0.97,
                Trend = cpi >= 0.97 ? TrendIndicator.Stable : TrendIndicator.Down,
                TrendPercentage = ((cpi - 0.97) / 0.97) * 100,
                Status = cpi switch
                {
                    >= 1.0 => MetricStatus.Excellent,
                    >= 0.95 => MetricStatus.Good,
                    >= 0.85 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Description = "Ratio of earned value to actual cost - measures cost efficiency"
            };
        }

        private async Task<Metric> CalculateTRIRMetricAsync(CancellationToken ct)
        {
            var recordableIncidents = 2;
            var hoursWorked = 125000.0;
            var trir = (recordableIncidents * 200000) / hoursWorked;

            return new Metric
            {
                Id = "trir",
                Name = "Total Recordable Incident Rate",
                Category = "Safety",
                Unit = "per 200,000 hrs",
                Value = Math.Round(trir, 2),
                TargetValue = 2.0,
                BaselineValue = 3.5,
                PreviousValue = 3.8,
                Trend = trir < 3.8 ? TrendIndicator.Down : TrendIndicator.Up,
                TrendPercentage = ((trir - 3.8) / 3.8) * 100,
                Status = trir switch
                {
                    <= 2.0 => MetricStatus.Excellent,
                    <= 3.0 => MetricStatus.Good,
                    <= 4.0 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Description = "Number of recordable injuries per 200,000 hours worked"
            };
        }

        private async Task<Metric> CalculateDefectRateMetricAsync(CancellationToken ct)
        {
            var defects = 15;
            var inspections = 180;
            var rate = (double)defects / inspections * 100;

            return new Metric
            {
                Id = "defect_rate",
                Name = "Defect Rate",
                Category = "Quality",
                Unit = "%",
                Value = Math.Round(rate, 1),
                TargetValue = 5.0,
                PreviousValue = 9.5,
                Trend = rate < 9.5 ? TrendIndicator.Down : TrendIndicator.Up,
                Status = rate switch
                {
                    <= 5.0 => MetricStatus.Excellent,
                    <= 8.0 => MetricStatus.Good,
                    <= 12.0 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Description = "Percentage of inspections that identified defects"
            };
        }

        private async Task<Metric> CalculateTaskCompletionRateAsync(CancellationToken ct)
        {
            var completed = 145;
            var total = 160;
            var rate = (double)completed / total * 100;

            return new Metric
            {
                Id = "task_completion_rate",
                Name = "Task Completion Rate",
                Category = "Productivity",
                Unit = "%",
                Value = Math.Round(rate, 1),
                TargetValue = 95.0,
                PreviousValue = 88.0,
                Trend = rate > 88.0 ? TrendIndicator.Up : TrendIndicator.Down,
                Status = rate switch
                {
                    >= 95.0 => MetricStatus.Excellent,
                    >= 85.0 => MetricStatus.Good,
                    >= 75.0 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Description = "Percentage of scheduled tasks completed on time"
            };
        }

        private async Task<Metric> CalculateRFIResponseTimeAsync(CancellationToken ct)
        {
            var avgDays = 4.2;

            return new Metric
            {
                Id = "rfi_response_time",
                Name = "RFI Response Time",
                Category = "Productivity",
                Unit = "days",
                Value = avgDays,
                TargetValue = 3.0,
                PreviousValue = 5.1,
                Trend = avgDays < 5.1 ? TrendIndicator.Down : TrendIndicator.Up,
                Status = avgDays switch
                {
                    <= 3.0 => MetricStatus.Excellent,
                    <= 5.0 => MetricStatus.Good,
                    <= 7.0 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Description = "Average days to respond to RFIs"
            };
        }

        private async Task<Metric> CalculateClashResolutionRateAsync(CancellationToken ct)
        {
            var resolved = 89;
            var total = 105;
            var rate = (double)resolved / total * 100;

            return new Metric
            {
                Id = "clash_resolution_rate",
                Name = "Clash Resolution Rate",
                Category = "Issues",
                Unit = "%",
                Value = Math.Round(rate, 1),
                TargetValue = 90.0,
                PreviousValue = 82.0,
                Trend = rate > 82.0 ? TrendIndicator.Up : TrendIndicator.Down,
                Status = rate switch
                {
                    >= 90.0 => MetricStatus.Excellent,
                    >= 80.0 => MetricStatus.Good,
                    >= 70.0 => MetricStatus.Warning,
                    _ => MetricStatus.Critical
                },
                Description = "Percentage of detected clashes that have been resolved"
            };
        }

        private async Task<Metric> CalculateProgressMetricAsync(CancellationToken ct)
        {
            var progress = 42.5;

            return new Metric
            {
                Id = "progress",
                Name = "Overall Progress",
                Category = "Schedule",
                Unit = "%",
                Value = progress,
                TargetValue = 45.0,
                PreviousValue = 38.0,
                Trend = TrendIndicator.Up,
                TrendPercentage = 4.5,
                Status = MetricStatus.Good,
                Description = "Overall project completion percentage"
            };
        }

        private async Task<Metric?> CalculateGenericMetricAsync(string metricId, CancellationToken ct)
        {
            Logger.Debug($"Generic metric calculation requested for: {metricId}");
            return null;
        }

        private async Task<TimeSeries> BuildTimeSeriesAsync(
            string metricId,
            DateTime startDate,
            DateTime endDate,
            TimeGranularity granularity,
            CancellationToken ct)
        {
            var timeSeries = new TimeSeries
            {
                MetricId = metricId,
                Name = GetMetricName(metricId),
                Granularity = granularity,
                StartDate = startDate,
                EndDate = endDate
            };

            // Generate data points based on granularity
            var dataPoints = new List<TimeSeriesDataPoint>();
            var current = startDate;
            var random = new Random(metricId.GetHashCode());

            double baseValue = metricId switch
            {
                "spi" => 0.95,
                "cpi" => 0.96,
                "trir" => 3.2,
                "defect_rate" => 8.5,
                "progress" => 30.0,
                _ => 50.0
            };

            while (current <= endDate)
            {
                var variance = (random.NextDouble() - 0.5) * 0.1 * baseValue;
                var trendAdjustment = (current - startDate).TotalDays / (endDate - startDate).TotalDays * 0.05 * baseValue;

                var value = baseValue + variance + trendAdjustment;

                dataPoints.Add(new TimeSeriesDataPoint
                {
                    Timestamp = current,
                    Value = Math.Round(value, 2)
                });

                current = granularity switch
                {
                    TimeGranularity.Hourly => current.AddHours(1),
                    TimeGranularity.Daily => current.AddDays(1),
                    TimeGranularity.Weekly => current.AddDays(7),
                    TimeGranularity.Monthly => current.AddMonths(1),
                    TimeGranularity.Quarterly => current.AddMonths(3),
                    TimeGranularity.Yearly => current.AddYears(1),
                    _ => current.AddDays(1)
                };
            }

            timeSeries.DataPoints = dataPoints;

            // Calculate statistics
            if (dataPoints.Any())
            {
                timeSeries.Min = dataPoints.Min(d => d.Value);
                timeSeries.Max = dataPoints.Max(d => d.Value);
                timeSeries.Average = dataPoints.Average(d => d.Value);
                timeSeries.Sum = dataPoints.Sum(d => d.Value);
                timeSeries.Count = dataPoints.Count;

                // Perform trend analysis
                timeSeries.TrendAnalysis = AnalyzeTrend(dataPoints);

                // Generate forecast
                timeSeries.Forecast = await GenerateForecastAsync(dataPoints, granularity, ct);
            }

            return timeSeries;
        }

        private string GetMetricName(string metricId)
        {
            return metricId.ToLower() switch
            {
                "spi" => "Schedule Performance Index",
                "cpi" => "Cost Performance Index",
                "trir" => "Total Recordable Incident Rate",
                "defect_rate" => "Defect Rate",
                "task_completion_rate" => "Task Completion Rate",
                "rfi_response_time" => "RFI Response Time",
                "clash_resolution_rate" => "Clash Resolution Rate",
                "progress" => "Overall Progress",
                _ => metricId
            };
        }

        #endregion

        #region KPI Calculations

        /// <summary>
        /// Calculate all KPIs and refresh cache
        /// </summary>
        public async Task<Dictionary<string, Metric>> CalculateKPIsAsync(
            DashboardType dashboardType,
            CancellationToken ct = default)
        {
            return dashboardType switch
            {
                DashboardType.SchedulePerformance => await CalculateScheduleKPIsAsync(ct),
                DashboardType.CostAnalysis => await CalculateCostKPIsAsync(ct),
                DashboardType.SafetyMetrics => await CalculateSafetyKPIsAsync(ct),
                DashboardType.QualityMetrics => await CalculateQualityKPIsAsync(ct),
                DashboardType.TeamProductivity => await CalculateProductivityKPIsAsync(ct),
                DashboardType.IssueTracking => await CalculateIssueKPIsAsync(ct),
                DashboardType.DocumentMetrics => await CalculateDocumentKPIsAsync(ct),
                DashboardType.ProjectOverview => await CalculateOverviewKPIsAsync(ct),
                _ => new Dictionary<string, Metric>()
            };
        }

        private async Task<Dictionary<string, Metric>> CalculateScheduleKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["spi"] = await CalculateSPIMetricAsync(ct);
            kpis["progress"] = await CalculateProgressMetricAsync(ct);

            kpis["schedule_variance"] = new Metric
            {
                Id = "schedule_variance",
                Name = "Schedule Variance",
                Category = "Schedule",
                Unit = "days",
                Value = -5,
                Status = MetricStatus.Warning,
                Description = "Days ahead/behind planned schedule"
            };

            kpis["milestones_on_track"] = new Metric
            {
                Id = "milestones_on_track",
                Name = "Milestones On Track",
                Category = "Schedule",
                Unit = "%",
                Value = 85,
                TargetValue = 100,
                Status = MetricStatus.Good,
                Description = "Percentage of milestones meeting planned dates"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateCostKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["cpi"] = await CalculateCPIMetricAsync(ct);

            kpis["cost_variance"] = new Metric
            {
                Id = "cost_variance",
                Name = "Cost Variance",
                Category = "Cost",
                Unit = "USD",
                Value = -100000,
                Status = MetricStatus.Warning,
                Description = "Earned value minus actual cost"
            };

            kpis["eac"] = new Metric
            {
                Id = "eac",
                Name = "Estimate at Completion",
                Category = "Cost",
                Unit = "USD",
                Value = 5250000,
                TargetValue = 5000000,
                Status = MetricStatus.Warning,
                Description = "Projected total cost at project completion"
            };

            kpis["contingency_used"] = new Metric
            {
                Id = "contingency_used",
                Name = "Contingency Used",
                Category = "Cost",
                Unit = "%",
                Value = 35,
                TargetValue = 50,
                Status = MetricStatus.Good,
                Description = "Percentage of contingency budget consumed"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateSafetyKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["trir"] = await CalculateTRIRMetricAsync(ct);

            kpis["days_without_incident"] = new Metric
            {
                Id = "days_without_incident",
                Name = "Days Without Incident",
                Category = "Safety",
                Unit = "days",
                Value = 45,
                Status = MetricStatus.Excellent,
                Trend = TrendIndicator.Up,
                Description = "Consecutive days without recordable incident"
            };

            kpis["near_miss_rate"] = new Metric
            {
                Id = "near_miss_rate",
                Name = "Near Miss Reporting Rate",
                Category = "Safety",
                Unit = "per week",
                Value = 8.5,
                TargetValue = 10.0,
                Status = MetricStatus.Good,
                Description = "Near miss reports submitted per week"
            };

            kpis["safety_training_compliance"] = new Metric
            {
                Id = "safety_training_compliance",
                Name = "Safety Training Compliance",
                Category = "Safety",
                Unit = "%",
                Value = 96,
                TargetValue = 100,
                Status = MetricStatus.Excellent,
                Description = "Workers current on required safety training"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateQualityKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["defect_rate"] = await CalculateDefectRateMetricAsync(ct);

            kpis["inspection_pass_rate"] = new Metric
            {
                Id = "inspection_pass_rate",
                Name = "First-Time Inspection Pass Rate",
                Category = "Quality",
                Unit = "%",
                Value = 91.7,
                TargetValue = 95.0,
                Status = MetricStatus.Good,
                Description = "Inspections passing on first attempt"
            };

            kpis["ncr_count"] = new Metric
            {
                Id = "ncr_count",
                Name = "Open NCRs",
                Category = "Quality",
                Unit = "",
                Value = 3,
                Status = MetricStatus.Good,
                Description = "Non-conformance reports requiring resolution"
            };

            kpis["rework_cost"] = new Metric
            {
                Id = "rework_cost",
                Name = "Rework as % of Cost",
                Category = "Quality",
                Unit = "%",
                Value = 1.8,
                TargetValue = 2.0,
                Status = MetricStatus.Good,
                Description = "Rework expenditure as percentage of total cost"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateProductivityKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["task_completion_rate"] = await CalculateTaskCompletionRateAsync(ct);
            kpis["rfi_response_time"] = await CalculateRFIResponseTimeAsync(ct);

            kpis["submittal_turnaround"] = new Metric
            {
                Id = "submittal_turnaround",
                Name = "Submittal Turnaround Time",
                Category = "Productivity",
                Unit = "days",
                Value = 7.2,
                TargetValue = 7.0,
                Status = MetricStatus.Good,
                Description = "Average days to process submittals"
            };

            kpis["collaboration_score"] = new Metric
            {
                Id = "collaboration_score",
                Name = "Team Collaboration Score",
                Category = "Productivity",
                Unit = "",
                Value = 78,
                TargetValue = 80,
                Status = MetricStatus.Good,
                Description = "Overall team collaboration effectiveness"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateIssueKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["clash_resolution_rate"] = await CalculateClashResolutionRateAsync(ct);

            kpis["open_issues"] = new Metric
            {
                Id = "open_issues",
                Name = "Open Issues",
                Category = "Issues",
                Unit = "",
                Value = 47,
                Status = MetricStatus.Good,
                Description = "Total unresolved issues"
            };

            kpis["overdue_issues"] = new Metric
            {
                Id = "overdue_issues",
                Name = "Overdue Issues",
                Category = "Issues",
                Unit = "",
                Value = 8,
                Status = MetricStatus.Warning,
                Description = "Issues past their due date"
            };

            kpis["avg_resolution_time"] = new Metric
            {
                Id = "avg_resolution_time",
                Name = "Avg Issue Resolution Time",
                Category = "Issues",
                Unit = "days",
                Value = 3.5,
                TargetValue = 3.0,
                Status = MetricStatus.Good,
                Description = "Average days to resolve issues"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateDocumentKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["pending_approvals"] = new Metric
            {
                Id = "pending_approvals",
                Name = "Pending Approvals",
                Category = "Documents",
                Unit = "",
                Value = 12,
                Status = MetricStatus.Good,
                Description = "Documents awaiting approval"
            };

            kpis["drawing_revision_rate"] = new Metric
            {
                Id = "drawing_revision_rate",
                Name = "Drawing Revision Rate",
                Category = "Documents",
                Unit = "per drawing",
                Value = 2.3,
                TargetValue = 2.0,
                Status = MetricStatus.Good,
                Description = "Average revisions per drawing"
            };

            kpis["documents_this_week"] = new Metric
            {
                Id = "documents_this_week",
                Name = "Documents This Week",
                Category = "Documents",
                Unit = "",
                Value = 45,
                Status = MetricStatus.Excellent,
                Description = "Documents uploaded this week"
            };

            return kpis;
        }

        private async Task<Dictionary<string, Metric>> CalculateOverviewKPIsAsync(CancellationToken ct)
        {
            var kpis = new Dictionary<string, Metric>();

            kpis["spi"] = await CalculateSPIMetricAsync(ct);
            kpis["cpi"] = await CalculateCPIMetricAsync(ct);
            kpis["progress"] = await CalculateProgressMetricAsync(ct);
            kpis["trir"] = await CalculateTRIRMetricAsync(ct);
            kpis["defect_rate"] = await CalculateDefectRateMetricAsync(ct);
            kpis["task_completion_rate"] = await CalculateTaskCompletionRateAsync(ct);

            return kpis;
        }

        /// <summary>
        /// Refresh the KPI cache
        /// </summary>
        public async Task RefreshCacheAsync(CancellationToken ct = default)
        {
            Logger.Info("Refreshing KPI cache...");

            foreach (DashboardType type in Enum.GetValues(typeof(DashboardType)))
            {
                var kpis = await CalculateKPIsAsync(type, ct);
                foreach (var (id, metric) in kpis)
                {
                    _metricCache[id] = metric;
                    _cacheTimestamps[$"metric_{id}"] = DateTime.UtcNow;
                }
            }

            Logger.Info($"KPI cache refreshed with {_metricCache.Count} metrics");
        }

        #endregion

        #region Trend Analysis and Forecasting

        /// <summary>
        /// Get trend analysis for metrics
        /// </summary>
        public async Task<Dictionary<string, TrendAnalysisResult>> GetTrendsAsync(
            IEnumerable<string> metricIds,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct = default)
        {
            var results = new Dictionary<string, TrendAnalysisResult>();

            foreach (var metricId in metricIds)
            {
                var timeSeries = await GetTimeSeriesAsync(metricId, startDate, endDate, TimeGranularity.Daily, ct);
                if (timeSeries.TrendAnalysis != null)
                {
                    results[metricId] = timeSeries.TrendAnalysis;
                }
            }

            return results;
        }

        /// <summary>
        /// Get forecasts for metrics
        /// </summary>
        public async Task<Dictionary<string, ForecastResult>> GetForecastsAsync(
            IEnumerable<string> metricIds,
            int forecastPeriods = 14,
            CancellationToken ct = default)
        {
            var results = new Dictionary<string, ForecastResult>();

            foreach (var metricId in metricIds)
            {
                var timeSeries = await GetTimeSeriesAsync(
                    metricId,
                    DateTime.UtcNow.AddDays(-90),
                    DateTime.UtcNow,
                    TimeGranularity.Daily,
                    ct);

                if (timeSeries.Forecast != null)
                {
                    results[metricId] = timeSeries.Forecast;
                }
            }

            return results;
        }

        private TrendAnalysisResult AnalyzeTrend(List<TimeSeriesDataPoint> dataPoints)
        {
            if (dataPoints.Count < 3)
            {
                return new TrendAnalysisResult { Direction = TrendIndicator.Stable };
            }

            var result = new TrendAnalysisResult();

            // Linear regression
            var n = dataPoints.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += dataPoints[i].Value;
                sumXY += i * dataPoints[i].Value;
                sumX2 += i * i;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var meanY = sumY / n;

            // R-squared
            var ssTotal = 0.0;
            var ssResidual = 0.0;
            var intercept = (sumY - slope * sumX) / n;

            for (int i = 0; i < n; i++)
            {
                var predicted = slope * i + intercept;
                ssTotal += Math.Pow(dataPoints[i].Value - meanY, 2);
                ssResidual += Math.Pow(dataPoints[i].Value - predicted, 2);
            }

            result.Slope = slope;
            result.RSquared = ssTotal > 0 ? Math.Max(0, 1 - (ssResidual / ssTotal)) : 0;

            // Calculate percentage change
            var firstValue = dataPoints.First().Value;
            var lastValue = dataPoints.Last().Value;
            result.PercentageChange = firstValue != 0 ? ((lastValue - firstValue) / firstValue) * 100 : 0;

            // Determine direction
            var normalizedSlope = meanY != 0 ? (slope / meanY) * 100 : slope;
            result.Direction = normalizedSlope switch
            {
                > 5 => TrendIndicator.StrongUp,
                > 1 => TrendIndicator.Up,
                < -5 => TrendIndicator.StrongDown,
                < -1 => TrendIndicator.Down,
                _ => TrendIndicator.Stable
            };

            // Calculate volatility (coefficient of variation)
            var stdDev = Math.Sqrt(dataPoints.Sum(d => Math.Pow(d.Value - meanY, 2)) / n);
            result.Volatility = meanY != 0 ? (stdDev / Math.Abs(meanY)) * 100 : 0;

            // Detect change points
            result.ChangePoints = DetectChangePoints(dataPoints);

            return result;
        }

        private List<ChangePoint> DetectChangePoints(List<TimeSeriesDataPoint> dataPoints)
        {
            var changePoints = new List<ChangePoint>();
            if (dataPoints.Count < 5) return changePoints;

            var windowSize = Math.Max(2, dataPoints.Count / 5);

            for (int i = windowSize; i < dataPoints.Count - windowSize; i++)
            {
                var beforeAvg = dataPoints.Skip(i - windowSize).Take(windowSize).Average(d => d.Value);
                var afterAvg = dataPoints.Skip(i).Take(windowSize).Average(d => d.Value);

                var change = beforeAvg != 0 ? (afterAvg - beforeAvg) / beforeAvg : 0;

                if (Math.Abs(change) > 0.15) // 15% threshold
                {
                    changePoints.Add(new ChangePoint
                    {
                        Date = dataPoints[i].Timestamp,
                        Type = change > 0 ? "Increase" : "Decrease",
                        Magnitude = Math.Abs(change) * 100
                    });
                }
            }

            return changePoints;
        }

        private async Task<ForecastResult> GenerateForecastAsync(
            List<TimeSeriesDataPoint> historicalData,
            TimeGranularity granularity,
            CancellationToken ct)
        {
            var forecast = new ForecastResult
            {
                Model = "ExponentialSmoothing",
                Confidence = 0.8,
                GeneratedAt = DateTime.UtcNow
            };

            if (historicalData.Count < 5)
            {
                return forecast;
            }

            // Simple exponential smoothing
            var alpha = 0.3;
            var values = historicalData.Select(d => d.Value).ToList();
            var smoothed = values[0];

            foreach (var value in values.Skip(1))
            {
                smoothed = alpha * value + (1 - alpha) * smoothed;
            }

            // Calculate trend
            var trend = values.Count > 1 ? (values.Last() - values.First()) / values.Count : 0;

            // Generate forecast points
            var lastDate = historicalData.Last().Timestamp;
            var forecastPeriods = 14;

            for (int i = 1; i <= forecastPeriods; i++)
            {
                var forecastDate = granularity switch
                {
                    TimeGranularity.Hourly => lastDate.AddHours(i),
                    TimeGranularity.Daily => lastDate.AddDays(i),
                    TimeGranularity.Weekly => lastDate.AddDays(i * 7),
                    TimeGranularity.Monthly => lastDate.AddMonths(i),
                    _ => lastDate.AddDays(i)
                };

                var forecastValue = smoothed + trend * i;
                var uncertainty = 0.1 * forecastValue * Math.Sqrt(i);

                forecast.Predictions.Add(new TimeSeriesDataPoint
                {
                    Timestamp = forecastDate,
                    Value = Math.Round(forecastValue, 2),
                    LowerBound = Math.Round(forecastValue - uncertainty, 2),
                    UpperBound = Math.Round(forecastValue + uncertainty, 2),
                    IsForecasted = true,
                    Confidence = Math.Max(0.5, 0.95 - i * 0.02)
                });
            }

            // Calculate MAPE (Mean Absolute Percentage Error) on hold-out set
            if (values.Count > 10)
            {
                var holdout = values.Skip(values.Count - 5).ToList();
                var errors = new List<double>();
                var testSmoothed = values.Take(values.Count - 5).Last();

                for (int i = 0; i < holdout.Count; i++)
                {
                    var predicted = testSmoothed + trend * (i + 1);
                    var error = Math.Abs((holdout[i] - predicted) / holdout[i]);
                    errors.Add(error);
                    testSmoothed = alpha * holdout[i] + (1 - alpha) * testSmoothed;
                }

                forecast.MAPE = errors.Average() * 100;
            }

            return forecast;
        }

        #endregion

        #region Comparative Analytics

        /// <summary>
        /// Get comparative analysis for a metric
        /// </summary>
        public async Task<ComparativeAnalysis> GetComparativeAnalysisAsync(
            string metricId,
            ComparisonType comparisonType,
            CancellationToken ct = default)
        {
            var metric = await GetMetricAsync(metricId, ct);
            if (metric == null)
            {
                return new ComparativeAnalysis
                {
                    MetricId = metricId,
                    ComparisonType = comparisonType
                };
            }

            var comparisonValue = comparisonType switch
            {
                ComparisonType.VsBaseline => metric.BaselineValue ?? metric.Value,
                ComparisonType.VsPreviousPeriod => metric.PreviousValue ?? metric.Value,
                ComparisonType.VsTarget => metric.TargetValue ?? metric.Value,
                ComparisonType.VsIndustryBenchmark => GetIndustryBenchmark(metricId),
                ComparisonType.VsSimilarProjects => await GetSimilarProjectAverageAsync(metricId, ct),
                _ => metric.Value
            };

            var difference = metric.Value - comparisonValue;
            var percentDiff = comparisonValue != 0 ? (difference / comparisonValue) * 100 : 0;

            var analysis = new ComparativeAnalysis
            {
                MetricId = metricId,
                ComparisonType = comparisonType,
                CurrentValue = metric.Value,
                ComparisonValue = comparisonValue,
                Difference = difference,
                PercentageDifference = percentDiff
            };

            // Determine relative status and interpretation
            var isHigherBetter = IsHigherBetter(metricId);
            var isPositiveChange = difference > 0;
            var isImproving = isHigherBetter ? isPositiveChange : !isPositiveChange;

            analysis.RelativeStatus = Math.Abs(percentDiff) switch
            {
                < 2 => MetricStatus.Good,
                < 5 when isImproving => MetricStatus.Excellent,
                < 5 => MetricStatus.Warning,
                _ when isImproving => MetricStatus.Excellent,
                _ => MetricStatus.Critical
            };

            analysis.Interpretation = GenerateInterpretation(metric, analysis, isImproving);
            analysis.Recommendations = GenerateRecommendations(metric, analysis, isImproving);

            return analysis;
        }

        private double GetIndustryBenchmark(string metricId)
        {
            return metricId.ToLower() switch
            {
                "spi" => 0.95,
                "cpi" => 0.95,
                "trir" => 3.5,
                "defect_rate" => 8.0,
                "task_completion_rate" => 90.0,
                "rfi_response_time" => 5.0,
                _ => 0
            };
        }

        private async Task<double> GetSimilarProjectAverageAsync(string metricId, CancellationToken ct)
        {
            // Would query similar projects - returning mock data
            return metricId.ToLower() switch
            {
                "spi" => 0.94,
                "cpi" => 0.96,
                "trir" => 3.2,
                "defect_rate" => 7.5,
                "task_completion_rate" => 88.0,
                _ => 0
            };
        }

        private bool IsHigherBetter(string metricId)
        {
            var lowerIsBetter = new[] { "trir", "defect_rate", "rfi_response_time", "avg_resolution_time" };
            return !lowerIsBetter.Contains(metricId.ToLower());
        }

        private string GenerateInterpretation(Metric metric, ComparativeAnalysis analysis, bool isImproving)
        {
            var direction = analysis.Difference > 0 ? "higher" : "lower";
            var performance = isImproving ? "outperforming" : "underperforming";

            return $"{metric.Name} is {Math.Abs(analysis.PercentageDifference):F1}% {direction} than " +
                   $"{analysis.ComparisonType.ToString().Replace("Vs", "")}. " +
                   $"The project is {performance} on this metric.";
        }

        private List<string> GenerateRecommendations(Metric metric, ComparativeAnalysis analysis, bool isImproving)
        {
            var recommendations = new List<string>();

            if (!isImproving && Math.Abs(analysis.PercentageDifference) > 5)
            {
                recommendations.Add($"Review root causes for {metric.Name} performance gap");
                recommendations.Add($"Develop action plan to close the {Math.Abs(analysis.PercentageDifference):F0}% gap");

                if (metric.Category == "Schedule")
                {
                    recommendations.Add("Consider schedule compression techniques or additional resources");
                }
                else if (metric.Category == "Cost")
                {
                    recommendations.Add("Identify cost optimization opportunities");
                }
                else if (metric.Category == "Safety")
                {
                    recommendations.Add("Reinforce safety protocols and increase monitoring");
                }
            }
            else if (isImproving)
            {
                recommendations.Add($"Document best practices contributing to {metric.Name} success");
                recommendations.Add("Share learnings with other projects");
            }

            return recommendations;
        }

        #endregion

        #region Report Generation

        /// <summary>
        /// Generate an analytics report
        /// </summary>
        public async Task<AnalyticsReport> GenerateReportAsync(
            DashboardType reportType,
            DateTime startDate,
            DateTime endDate,
            string generatedBy,
            CancellationToken ct = default)
        {
            var report = new AnalyticsReport
            {
                Title = $"{reportType} Report - {_projectName}",
                Description = $"Analytics report for period {startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy}",
                ReportType = reportType,
                ProjectId = _projectId,
                ProjectName = _projectName,
                PeriodStart = startDate,
                PeriodEnd = endDate,
                GeneratedBy = generatedBy
            };

            // Get KPIs
            report.KPIs = await CalculateKPIsAsync(reportType, ct);

            // Generate executive summary
            report.ExecutiveSummary = GenerateExecutiveSummary(report.KPIs.Values.ToList());

            // Generate key findings
            report.KeyFindings = GenerateKeyFindings(report.KPIs.Values.ToList());

            // Generate recommendations
            report.Recommendations = GenerateReportRecommendations(report.KPIs.Values.ToList());

            // Build sections
            report.Sections = await BuildReportSectionsAsync(reportType, startDate, endDate, ct);

            Logger.Info($"Generated {reportType} report for {_projectName}");
            return report;
        }

        /// <summary>
        /// Export dashboard to specified format
        /// </summary>
        public async Task<byte[]> ExportDashboardAsync(
            string dashboardId,
            ExportFormat format,
            CancellationToken ct = default)
        {
            var dashboard = await GetDashboardAsync(dashboardId, ct);
            if (dashboard == null)
            {
                throw new ArgumentException($"Dashboard not found: {dashboardId}");
            }

            return format switch
            {
                ExportFormat.PDF => await ExportToPdfAsync(dashboard, ct),
                ExportFormat.Excel => await ExportToExcelAsync(dashboard, ct),
                ExportFormat.PowerPoint => await ExportToPowerPointAsync(dashboard, ct),
                ExportFormat.CSV => await ExportToCsvAsync(dashboard, ct),
                ExportFormat.JSON => await ExportToJsonAsync(dashboard, ct),
                ExportFormat.HTML => await ExportToHtmlAsync(dashboard, ct),
                _ => throw new NotSupportedException($"Export format not supported: {format}")
            };
        }

        private List<string> GenerateExecutiveSummary(List<Metric> kpis)
        {
            var summary = new List<string>();

            var criticalMetrics = kpis.Where(k => k.Status == MetricStatus.Critical).ToList();
            var warningMetrics = kpis.Where(k => k.Status == MetricStatus.Warning).ToList();
            var excellentMetrics = kpis.Where(k => k.Status == MetricStatus.Excellent).ToList();

            if (criticalMetrics.Any())
            {
                summary.Add($"ATTENTION REQUIRED: {criticalMetrics.Count} metric(s) at critical status requiring immediate action.");
            }

            if (warningMetrics.Any())
            {
                summary.Add($"{warningMetrics.Count} metric(s) showing warning signs and trending toward threshold violations.");
            }

            if (excellentMetrics.Any())
            {
                summary.Add($"{excellentMetrics.Count} metric(s) performing at excellent levels, exceeding targets.");
            }

            var overallHealth = criticalMetrics.Count switch
            {
                0 when warningMetrics.Count == 0 => "Project is performing well across all measured KPIs.",
                0 => "Project performance is acceptable with some areas needing attention.",
                _ => "Project performance requires immediate management attention."
            };

            summary.Add(overallHealth);

            return summary;
        }

        private List<string> GenerateKeyFindings(List<Metric> kpis)
        {
            var findings = new List<string>();

            foreach (var metric in kpis.OrderByDescending(k => Math.Abs(k.TrendPercentage)))
            {
                if (Math.Abs(metric.TrendPercentage) > 5)
                {
                    var direction = metric.TrendPercentage > 0 ? "increased" : "decreased";
                    findings.Add($"{metric.Name} has {direction} by {Math.Abs(metric.TrendPercentage):F1}% from previous period.");
                }

                if (metric.Status == MetricStatus.Critical)
                {
                    findings.Add($"{metric.Name} is at critical level ({metric.Value} {metric.Unit}) - below acceptable threshold.");
                }
            }

            return findings.Take(5).ToList();
        }

        private List<string> GenerateReportRecommendations(List<Metric> kpis)
        {
            var recommendations = new List<string>();

            foreach (var metric in kpis.Where(k => k.Status == MetricStatus.Critical || k.Status == MetricStatus.Warning))
            {
                recommendations.Add($"Address {metric.Name}: Current value {metric.Value} {metric.Unit} vs target {metric.TargetValue} {metric.Unit}.");
            }

            var improvingMetrics = kpis.Where(k => k.Trend == TrendIndicator.Up || k.Trend == TrendIndicator.StrongUp).ToList();
            if (improvingMetrics.Any())
            {
                recommendations.Add($"Continue current strategies for {string.Join(", ", improvingMetrics.Take(3).Select(m => m.Name))} - showing positive trends.");
            }

            return recommendations;
        }

        private async Task<List<ReportSection>> BuildReportSectionsAsync(
            DashboardType reportType,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct)
        {
            var sections = new List<ReportSection>();

            // Overview section
            var kpis = await CalculateKPIsAsync(reportType, ct);
            sections.Add(new ReportSection
            {
                Title = "Key Performance Indicators",
                Content = "Summary of all tracked KPIs for the reporting period.",
                Metrics = kpis.Values.ToList()
            });

            // Trend section
            var trendSection = new ReportSection
            {
                Title = "Trend Analysis",
                Content = "Historical trends and patterns identified during the reporting period."
            };

            foreach (var kpi in kpis.Take(4))
            {
                var ts = await GetTimeSeriesAsync(kpi.Key, startDate, endDate, TimeGranularity.Daily, ct);
                trendSection.Charts.Add(new ReportChart
                {
                    Title = $"{kpi.Value.Name} Trend",
                    ChartType = ChartType.Line,
                    Data = ts
                });
            }
            sections.Add(trendSection);

            return sections;
        }

        private async Task<byte[]> ExportToPdfAsync(Dashboard dashboard, CancellationToken ct)
        {
            // Would use a PDF library like iTextSharp or PdfSharp
            var content = new StringBuilder();
            content.AppendLine($"Dashboard Report: {dashboard.Name}");
            content.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            content.AppendLine($"Project: {_projectName}");
            content.AppendLine();

            foreach (var widget in dashboard.Widgets)
            {
                content.AppendLine($"--- {widget.Title} ---");
                if (_metricCache.TryGetValue(widget.MetricId, out var metric))
                {
                    content.AppendLine($"  Value: {metric.Value} {metric.Unit}");
                    content.AppendLine($"  Status: {metric.Status}");
                    content.AppendLine($"  Trend: {metric.Trend}");
                }
                content.AppendLine();
            }

            Logger.Info($"Exported dashboard {dashboard.Id} to PDF");
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        private async Task<byte[]> ExportToExcelAsync(Dashboard dashboard, CancellationToken ct)
        {
            // Would use EPPlus or similar library
            var csv = new StringBuilder();
            csv.AppendLine("Metric,Value,Unit,Status,Trend,Target,Previous");

            foreach (var widget in dashboard.Widgets)
            {
                if (_metricCache.TryGetValue(widget.MetricId, out var metric))
                {
                    csv.AppendLine($"{metric.Name},{metric.Value},{metric.Unit},{metric.Status},{metric.Trend},{metric.TargetValue},{metric.PreviousValue}");
                }
            }

            Logger.Info($"Exported dashboard {dashboard.Id} to Excel format");
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private async Task<byte[]> ExportToPowerPointAsync(Dashboard dashboard, CancellationToken ct)
        {
            // Would use OpenXML SDK or similar
            var content = new StringBuilder();
            content.AppendLine($"<Presentation>");
            content.AppendLine($"  <Title>{dashboard.Name}</Title>");
            content.AppendLine($"  <Project>{_projectName}</Project>");

            foreach (var widget in dashboard.Widgets)
            {
                content.AppendLine($"  <Slide>");
                content.AppendLine($"    <Title>{widget.Title}</Title>");
                content.AppendLine($"    <ChartType>{widget.ChartType}</ChartType>");
                content.AppendLine($"  </Slide>");
            }

            content.AppendLine($"</Presentation>");

            Logger.Info($"Exported dashboard {dashboard.Id} to PowerPoint format");
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        private async Task<byte[]> ExportToCsvAsync(Dashboard dashboard, CancellationToken ct)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Widget,Metric,Value,Unit,Status,Timestamp");

            foreach (var widget in dashboard.Widgets)
            {
                foreach (var metricId in widget.MetricIds.Concat(new[] { widget.MetricId }).Where(m => !string.IsNullOrEmpty(m)))
                {
                    if (_metricCache.TryGetValue(metricId, out var metric))
                    {
                        csv.AppendLine($"\"{widget.Title}\",\"{metric.Name}\",{metric.Value},\"{metric.Unit}\",{metric.Status},{metric.Timestamp:o}");
                    }
                }
            }

            Logger.Info($"Exported dashboard {dashboard.Id} to CSV");
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private async Task<byte[]> ExportToJsonAsync(Dashboard dashboard, CancellationToken ct)
        {
            var export = new
            {
                dashboard.Id,
                dashboard.Name,
                dashboard.Type,
                Project = _projectName,
                ExportedAt = DateTime.UtcNow,
                Widgets = dashboard.Widgets.Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.ChartType,
                    Metrics = w.MetricIds.Concat(new[] { w.MetricId })
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Select(m => _metricCache.TryGetValue(m, out var metric) ? metric : null)
                        .Where(m => m != null)
                }).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            Logger.Info($"Exported dashboard {dashboard.Id} to JSON");
            return Encoding.UTF8.GetBytes(json);
        }

        private async Task<byte[]> ExportToHtmlAsync(Dashboard dashboard, CancellationToken ct)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine($"<title>{dashboard.Name}</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine(".widget { border: 1px solid #ddd; padding: 15px; margin: 10px 0; border-radius: 8px; }");
            html.AppendLine(".metric { margin: 5px 0; }");
            html.AppendLine(".excellent { color: #28a745; }");
            html.AppendLine(".good { color: #17a2b8; }");
            html.AppendLine(".warning { color: #ffc107; }");
            html.AppendLine(".critical { color: #dc3545; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            html.AppendLine($"<h1>{dashboard.Name}</h1>");
            html.AppendLine($"<p>Project: {_projectName} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>");

            foreach (var widget in dashboard.Widgets)
            {
                html.AppendLine($"<div class=\"widget\">");
                html.AppendLine($"<h3>{widget.Title}</h3>");

                foreach (var metricId in widget.MetricIds.Concat(new[] { widget.MetricId }).Where(m => !string.IsNullOrEmpty(m)))
                {
                    if (_metricCache.TryGetValue(metricId, out var metric))
                    {
                        var statusClass = metric.Status.ToString().ToLower();
                        html.AppendLine($"<div class=\"metric {statusClass}\">");
                        html.AppendLine($"<strong>{metric.Name}:</strong> {metric.Value} {metric.Unit}");
                        html.AppendLine($" (Status: {metric.Status}, Trend: {metric.Trend})");
                        html.AppendLine($"</div>");
                    }
                }

                html.AppendLine($"</div>");
            }

            html.AppendLine("</body></html>");

            Logger.Info($"Exported dashboard {dashboard.Id} to HTML");
            return Encoding.UTF8.GetBytes(html.ToString());
        }

        #endregion

        #region Dashboard Initialization

        private void InitializeDefaultDashboards()
        {
            foreach (DashboardType type in Enum.GetValues(typeof(DashboardType)))
            {
                var dashboard = new Dashboard
                {
                    Name = $"{type} Dashboard",
                    Type = type,
                    ProjectId = _projectId,
                    CreatedBy = "System",
                    IsDefault = true,
                    Widgets = GetDefaultWidgetsForType(type),
                    Filters = GetDefaultFiltersForType(type)
                };

                _dashboards[dashboard.Id] = dashboard;
            }

            Logger.Info($"Initialized {_dashboards.Count} default dashboards");
        }

        private List<DashboardWidget> GetDefaultWidgetsForType(DashboardType type)
        {
            return type switch
            {
                DashboardType.ProjectOverview => new List<DashboardWidget>
                {
                    CreateWidget("SPI Gauge", ChartType.Gauge, "spi", 0, 0, 3, 2),
                    CreateWidget("CPI Gauge", ChartType.Gauge, "cpi", 3, 0, 3, 2),
                    CreateWidget("Progress", ChartType.Gauge, "progress", 6, 0, 3, 2),
                    CreateWidget("Safety Score", ChartType.Gauge, "trir", 9, 0, 3, 2),
                    CreateWidget("Schedule Trend", ChartType.Line, "spi", 0, 2, 6, 3),
                    CreateWidget("Cost Trend", ChartType.Line, "cpi", 6, 2, 6, 3),
                    CreateWidget("Key Metrics", ChartType.Table, "", 0, 5, 12, 3, new[] { "spi", "cpi", "progress", "trir", "defect_rate" })
                },
                DashboardType.SchedulePerformance => new List<DashboardWidget>
                {
                    CreateWidget("SPI", ChartType.Gauge, "spi", 0, 0, 4, 2),
                    CreateWidget("Progress", ChartType.Gauge, "progress", 4, 0, 4, 2),
                    CreateWidget("Schedule Variance", ChartType.KPI, "schedule_variance", 8, 0, 4, 2),
                    CreateWidget("SPI Trend", ChartType.Line, "spi", 0, 2, 8, 4),
                    CreateWidget("Activities Status", ChartType.Pie, "", 8, 2, 4, 4)
                },
                DashboardType.CostAnalysis => new List<DashboardWidget>
                {
                    CreateWidget("CPI", ChartType.Gauge, "cpi", 0, 0, 4, 2),
                    CreateWidget("Cost Variance", ChartType.KPI, "cost_variance", 4, 0, 4, 2),
                    CreateWidget("EAC", ChartType.KPI, "eac", 8, 0, 4, 2),
                    CreateWidget("Cost Trend", ChartType.Line, "cpi", 0, 2, 8, 4),
                    CreateWidget("Cost by Category", ChartType.Pie, "", 8, 2, 4, 4)
                },
                DashboardType.SafetyMetrics => new List<DashboardWidget>
                {
                    CreateWidget("TRIR", ChartType.Gauge, "trir", 0, 0, 4, 2),
                    CreateWidget("Days Without Incident", ChartType.KPI, "days_without_incident", 4, 0, 4, 2),
                    CreateWidget("Training Compliance", ChartType.Gauge, "safety_training_compliance", 8, 0, 4, 2),
                    CreateWidget("Safety Trend", ChartType.Line, "trir", 0, 2, 8, 4),
                    CreateWidget("Incidents by Type", ChartType.Bar, "", 8, 2, 4, 4)
                },
                DashboardType.QualityMetrics => new List<DashboardWidget>
                {
                    CreateWidget("Defect Rate", ChartType.Gauge, "defect_rate", 0, 0, 4, 2),
                    CreateWidget("Inspection Pass Rate", ChartType.Gauge, "inspection_pass_rate", 4, 0, 4, 2),
                    CreateWidget("Open NCRs", ChartType.KPI, "ncr_count", 8, 0, 4, 2),
                    CreateWidget("Quality Trend", ChartType.Line, "defect_rate", 0, 2, 8, 4),
                    CreateWidget("Defects by Type", ChartType.Pie, "", 8, 2, 4, 4)
                },
                DashboardType.TeamProductivity => new List<DashboardWidget>
                {
                    CreateWidget("Task Completion", ChartType.Gauge, "task_completion_rate", 0, 0, 4, 2),
                    CreateWidget("RFI Response Time", ChartType.KPI, "rfi_response_time", 4, 0, 4, 2),
                    CreateWidget("Collaboration Score", ChartType.Gauge, "collaboration_score", 8, 0, 4, 2),
                    CreateWidget("Productivity Trend", ChartType.Line, "task_completion_rate", 0, 2, 8, 4),
                    CreateWidget("Team Performance", ChartType.Bar, "", 8, 2, 4, 4)
                },
                DashboardType.IssueTracking => new List<DashboardWidget>
                {
                    CreateWidget("Open Issues", ChartType.KPI, "open_issues", 0, 0, 3, 2),
                    CreateWidget("Overdue Issues", ChartType.KPI, "overdue_issues", 3, 0, 3, 2),
                    CreateWidget("Resolution Rate", ChartType.Gauge, "clash_resolution_rate", 6, 0, 3, 2),
                    CreateWidget("Avg Resolution Time", ChartType.KPI, "avg_resolution_time", 9, 0, 3, 2),
                    CreateWidget("Issues Trend", ChartType.Area, "open_issues", 0, 2, 8, 4),
                    CreateWidget("Issues by Category", ChartType.Pie, "", 8, 2, 4, 4)
                },
                DashboardType.DocumentMetrics => new List<DashboardWidget>
                {
                    CreateWidget("Pending Approvals", ChartType.KPI, "pending_approvals", 0, 0, 4, 2),
                    CreateWidget("Documents This Week", ChartType.KPI, "documents_this_week", 4, 0, 4, 2),
                    CreateWidget("Revision Rate", ChartType.KPI, "drawing_revision_rate", 8, 0, 4, 2),
                    CreateWidget("Document Activity", ChartType.Line, "documents_this_week", 0, 2, 8, 4),
                    CreateWidget("Documents by Type", ChartType.Pie, "", 8, 2, 4, 4)
                },
                _ => new List<DashboardWidget>()
            };
        }

        private DashboardWidget CreateWidget(
            string title,
            ChartType chartType,
            string metricId,
            int col,
            int row,
            int width,
            int height,
            string[]? additionalMetrics = null)
        {
            return new DashboardWidget
            {
                Title = title,
                ChartType = chartType,
                MetricId = metricId,
                MetricIds = additionalMetrics?.ToList() ?? new List<string>(),
                Position = new WidgetPosition { Column = col, Row = row },
                Size = new WidgetSize { Width = width, Height = height }
            };
        }

        private List<DashboardFilter> GetDefaultFiltersForType(DashboardType type)
        {
            return new List<DashboardFilter>
            {
                new DashboardFilter
                {
                    Name = "Date Range",
                    Field = "timestamp",
                    FilterType = FilterType.DateRange,
                    DateRangeStart = DateTime.UtcNow.AddDays(-30),
                    DateRangeEnd = DateTime.UtcNow
                },
                new DashboardFilter
                {
                    Name = "Time Granularity",
                    Field = "granularity",
                    FilterType = FilterType.SingleSelect,
                    Options = new List<FilterOption>
                    {
                        new() { Label = "Hourly", Value = "hourly" },
                        new() { Label = "Daily", Value = "daily" },
                        new() { Label = "Weekly", Value = "weekly" },
                        new() { Label = "Monthly", Value = "monthly" }
                    },
                    DefaultValue = "daily"
                }
            };
        }

        #endregion

        #region Background Refresh

        /// <summary>
        /// Start background refresh task
        /// </summary>
        public void StartBackgroundRefresh(TimeSpan interval)
        {
            StopBackgroundRefresh();

            _refreshCts = new CancellationTokenSource();
            _refreshTask = Task.Run(async () =>
            {
                while (!_refreshCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await RefreshCacheAsync(_refreshCts.Token);
                        await Task.Delay(interval, _refreshCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error in background refresh");
                    }
                }
            }, _refreshCts.Token);

            Logger.Info($"Started background refresh with {interval.TotalMinutes} minute interval");
        }

        /// <summary>
        /// Stop background refresh task
        /// </summary>
        public void StopBackgroundRefresh()
        {
            if (_refreshCts != null)
            {
                _refreshCts.Cancel();
                _refreshCts.Dispose();
                _refreshCts = null;
            }

            _refreshTask = null;
            Logger.Info("Stopped background refresh");
        }

        #endregion

        #region Data Recording

        /// <summary>
        /// Record a raw data point for future analysis
        /// </summary>
        public void RecordDataPoint(string metricId, double value, Dictionary<string, double>? dimensions = null)
        {
            var dataPoints = _rawData.GetOrAdd(metricId, _ => new List<TimeSeriesDataPoint>());

            lock (dataPoints)
            {
                dataPoints.Add(new TimeSeriesDataPoint
                {
                    Timestamp = DateTime.UtcNow,
                    Value = value,
                    Dimensions = dimensions ?? new Dictionary<string, double>()
                });

                // Keep last 10,000 points per metric
                while (dataPoints.Count > 10000)
                {
                    dataPoints.RemoveAt(0);
                }
            }

            // Invalidate cache for this metric
            _cacheTimestamps.TryRemove($"metric_{metricId}", out _);
        }

        /// <summary>
        /// Record multiple data points in batch
        /// </summary>
        public void RecordDataPoints(string metricId, IEnumerable<(DateTime timestamp, double value)> points)
        {
            var dataPoints = _rawData.GetOrAdd(metricId, _ => new List<TimeSeriesDataPoint>());

            lock (dataPoints)
            {
                foreach (var (timestamp, value) in points)
                {
                    dataPoints.Add(new TimeSeriesDataPoint
                    {
                        Timestamp = timestamp,
                        Value = value
                    });
                }

                // Sort and deduplicate
                var sorted = dataPoints.OrderBy(d => d.Timestamp).ToList();
                dataPoints.Clear();
                dataPoints.AddRange(sorted.TakeLast(10000));
            }

            _cacheTimestamps.TryRemove($"metric_{metricId}", out _);
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            StopBackgroundRefresh();

            _dashboards.Clear();
            _metricCache.Clear();
            _timeSeriesCache.Clear();
            _cacheTimestamps.Clear();
            _rawData.Clear();

            Logger.Info("AnalyticsDashboardEngine disposed");

            await ValueTask.CompletedTask;
        }

        #endregion
    }

    #endregion
}
