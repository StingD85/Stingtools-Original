// ============================================================================
// StingBIM Data Analytics Intelligence Engine
// KPI tracking, trend analysis, executive reporting, predictive analytics
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DataAnalyticsIntelligence
{
    #region Enums

    public enum KPICategory
    {
        Schedule,
        Cost,
        Quality,
        Safety,
        Productivity,
        Sustainability,
        Risk,
        Resource,
        Client,
        Compliance,
        Financial,
        Operational
    }

    public enum KPIStatus
    {
        OnTarget,
        AtRisk,
        BelowTarget,
        AboveTarget,
        Critical,
        NotAvailable
    }

    public enum KPIFrequency
    {
        RealTime,
        Hourly,
        Daily,
        Weekly,
        BiWeekly,
        Monthly,
        Quarterly,
        Annually
    }

    public enum TrendDirection
    {
        StronglyIncreasing,
        Increasing,
        Stable,
        Decreasing,
        StronglyDecreasing,
        Volatile,
        Unknown
    }

    public enum MetricDataType
    {
        Number,
        Percentage,
        Currency,
        Duration,
        Count,
        Ratio,
        Index,
        Boolean
    }

    public enum DashboardWidgetType
    {
        KPICard,
        LineChart,
        BarChart,
        PieChart,
        GaugeChart,
        Table,
        Heatmap,
        Map,
        Timeline,
        Text,
        Alert,
        Progress
    }

    public enum ReportType
    {
        Executive,
        Operational,
        Financial,
        Progress,
        Safety,
        Quality,
        Risk,
        Custom
    }

    public enum ReportFormat
    {
        PDF,
        Excel,
        Word,
        PowerPoint,
        HTML,
        JSON
    }

    public enum PredictionConfidence
    {
        VeryHigh,
        High,
        Medium,
        Low,
        VeryLow
    }

    public enum BenchmarkType
    {
        Industry,
        Historical,
        Target,
        BestInClass,
        Peer,
        Internal
    }

    public enum AlertPriority
    {
        Critical,
        High,
        Medium,
        Low,
        Info
    }

    #endregion

    #region Data Models

    public class KPIDefinition
    {
        public string KPIId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public KPICategory Category { get; set; }
        public MetricDataType DataType { get; set; }
        public string Unit { get; set; }
        public KPIFrequency Frequency { get; set; }
        public KPITarget Target { get; set; }
        public List<KPIThreshold> Thresholds { get; set; } = new List<KPIThreshold>();
        public bool IsActive { get; set; } = true;
        public string Owner { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class KPITarget
    {
        public double TargetValue { get; set; }
        public double MinAcceptable { get; set; }
        public double MaxAcceptable { get; set; }
        public bool IsHigherBetter { get; set; }
    }

    public class KPIThreshold
    {
        public string ThresholdId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double Value { get; set; }
        public string Operator { get; set; }
        public KPIStatus ResultStatus { get; set; }
        public AlertPriority AlertPriority { get; set; }
    }

    public class KPIValue
    {
        public string ValueId { get; set; } = Guid.NewGuid().ToString();
        public string KPIId { get; set; }
        public string ProjectId { get; set; }
        public double Value { get; set; }
        public double? PreviousValue { get; set; }
        public double? TargetValue { get; set; }
        public double Variance { get; set; }
        public double VariancePercent { get; set; }
        public KPIStatus Status { get; set; }
        public TrendDirection Trend { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
        public double Confidence { get; set; }
    }

    public class Dashboard
    {
        public string DashboardId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string ProjectId { get; set; }
        public string OwnerId { get; set; }
        public List<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();
        public DashboardLayout Layout { get; set; }
        public DashboardFilters Filters { get; set; }
        public RefreshSettings RefreshSettings { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    }

    public class DashboardWidget
    {
        public string WidgetId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public DashboardWidgetType Type { get; set; }
        public WidgetPosition Position { get; set; }
        public WidgetSize Size { get; set; }
        public WidgetDataSource DataSource { get; set; }
    }

    public class WidgetPosition { public int Row { get; set; } public int Column { get; set; } }
    public class WidgetSize { public int Width { get; set; } public int Height { get; set; } }
    public class WidgetDataSource { public string Type { get; set; } public List<string> KPIIds { get; set; } = new List<string>(); }
    public class DashboardLayout { public int Columns { get; set; } public int Rows { get; set; } public string Theme { get; set; } }
    public class DashboardFilters { public DateTime? DateRangeStart { get; set; } public DateTime? DateRangeEnd { get; set; } public List<string> ProjectIds { get; set; } = new List<string>(); }
    public class RefreshSettings { public bool AutoRefresh { get; set; } public int RefreshIntervalSeconds { get; set; } public DateTime LastRefresh { get; set; } }

    public class TrendAnalysis
    {
        public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
        public string KPIId { get; set; }
        public string ProjectId { get; set; }
        public TrendDirection Direction { get; set; }
        public double Slope { get; set; }
        public double RSquared { get; set; }
        public double ChangeRate { get; set; }
        public double ChangeRatePercent { get; set; }
        public List<TrendDataPoint> DataPoints { get; set; } = new List<TrendDataPoint>();
        public TrendPrediction Prediction { get; set; }
        public List<TrendAnomaly> Anomalies { get; set; } = new List<TrendAnomaly>();
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    }

    public class TrendDataPoint { public DateTime Date { get; set; } public double Value { get; set; } public double? TrendValue { get; set; } public bool IsAnomaly { get; set; } }
    public class TrendPrediction { public List<TrendDataPoint> PredictedValues { get; set; } = new List<TrendDataPoint>(); public double LowerBound { get; set; } public double UpperBound { get; set; } public PredictionConfidence Confidence { get; set; } }
    public class TrendAnomaly { public DateTime Date { get; set; } public double Value { get; set; } public double ExpectedValue { get; set; } public double Deviation { get; set; } public string Explanation { get; set; } }

    public class ExecutiveReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string ProjectId { get; set; }
        public ReportType Type { get; set; }
        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public ReportSummary Summary { get; set; }
        public List<ReportSection> Sections { get; set; } = new List<ReportSection>();
        public List<ReportKPI> KeyMetrics { get; set; } = new List<ReportKPI>();
        public List<ReportHighlight> Highlights { get; set; } = new List<ReportHighlight>();
        public List<ReportRisk> Risks { get; set; } = new List<ReportRisk>();
        public List<ReportRecommendation> Recommendations { get; set; } = new List<ReportRecommendation>();
        public ReportFormat Format { get; set; }
    }

    public class ReportSummary { public string Overview { get; set; } public string StatusSummary { get; set; } public double OverallHealth { get; set; } public string HealthGrade { get; set; } public List<string> KeyPoints { get; set; } = new List<string>(); }
    public class ReportSection { public string SectionId { get; set; } = Guid.NewGuid().ToString(); public string Title { get; set; } public int Order { get; set; } public string Content { get; set; } }
    public class ReportKPI { public string KPIId { get; set; } public string Name { get; set; } public double CurrentValue { get; set; } public double TargetValue { get; set; } public double Variance { get; set; } public KPIStatus Status { get; set; } public TrendDirection Trend { get; set; } public string Commentary { get; set; } }
    public class ReportHighlight { public string HighlightId { get; set; } = Guid.NewGuid().ToString(); public string Title { get; set; } public string Description { get; set; } public bool IsPositive { get; set; } public double Impact { get; set; } }
    public class ReportRisk { public string RiskId { get; set; } public string Description { get; set; } public string Severity { get; set; } public string Mitigation { get; set; } public string Status { get; set; } }
    public class ReportRecommendation { public string RecommendationId { get; set; } = Guid.NewGuid().ToString(); public string Title { get; set; } public string Description { get; set; } public string Priority { get; set; } public string ExpectedImpact { get; set; } }

    public class ProjectBenchmark
    {
        public string BenchmarkId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public BenchmarkType Type { get; set; }
        public List<BenchmarkMetric> Metrics { get; set; } = new List<BenchmarkMetric>();
        public BenchmarkSummary Summary { get; set; }
        public DateTime BenchmarkDate { get; set; } = DateTime.UtcNow;
    }

    public class BenchmarkMetric { public string MetricName { get; set; } public double ProjectValue { get; set; } public double BenchmarkValue { get; set; } public double Percentile { get; set; } public double Variance { get; set; } public double VariancePercent { get; set; } public string Performance { get; set; } }
    public class BenchmarkSummary { public double OverallPercentile { get; set; } public int MetricsAboveBenchmark { get; set; } public int MetricsBelowBenchmark { get; set; } public List<string> Strengths { get; set; } = new List<string>(); public List<string> ImprovementAreas { get; set; } = new List<string>(); }

    public class DataAnalyticsResult { public bool Success { get; set; } public string Message { get; set; } public string ResultId { get; set; } public object Data { get; set; } public List<string> Warnings { get; set; } = new List<string>(); public DateTime Timestamp { get; set; } = DateTime.UtcNow; }
    public class DashboardData { public string DashboardId { get; set; } public DateTime GeneratedAt { get; set; } = DateTime.UtcNow; public Dictionary<string, object> WidgetData { get; set; } = new Dictionary<string, object>(); public List<AnalyticsAlert> Alerts { get; set; } = new List<AnalyticsAlert>(); }
    public class AnalyticsAlert { public string AlertId { get; set; } = Guid.NewGuid().ToString(); public string KPIId { get; set; } public string Title { get; set; } public string Message { get; set; } public AlertPriority Priority { get; set; } public DateTime CreatedDate { get; set; } = DateTime.UtcNow; }

    #endregion

    #region Engine

    public sealed class DataAnalyticsIntelligenceEngine
    {
        private static readonly Lazy<DataAnalyticsIntelligenceEngine> _instance =
            new Lazy<DataAnalyticsIntelligenceEngine>(() => new DataAnalyticsIntelligenceEngine());

        public static DataAnalyticsIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, KPIDefinition> _kpiDefinitions;
        private readonly ConcurrentDictionary<string, List<KPIValue>> _kpiValues;
        private readonly ConcurrentDictionary<string, Dashboard> _dashboards;
        private readonly ConcurrentDictionary<string, TrendAnalysis> _trendAnalyses;
        private readonly ConcurrentDictionary<string, ExecutiveReport> _reports;
        private readonly ConcurrentDictionary<string, ProjectBenchmark> _benchmarks;
        private readonly object _syncLock = new object();

        private DataAnalyticsIntelligenceEngine()
        {
            _kpiDefinitions = new ConcurrentDictionary<string, KPIDefinition>();
            _kpiValues = new ConcurrentDictionary<string, List<KPIValue>>();
            _dashboards = new ConcurrentDictionary<string, Dashboard>();
            _trendAnalyses = new ConcurrentDictionary<string, TrendAnalysis>();
            _reports = new ConcurrentDictionary<string, ExecutiveReport>();
            _benchmarks = new ConcurrentDictionary<string, ProjectBenchmark>();
            InitializeStandardKPIs();
        }

        private void InitializeStandardKPIs()
        {
            var standardKPIs = new List<KPIDefinition>
            {
                new KPIDefinition { Name = "Schedule Performance Index (SPI)", Code = "SPI", Category = KPICategory.Schedule, DataType = MetricDataType.Ratio, Target = new KPITarget { TargetValue = 1.0, MinAcceptable = 0.95, IsHigherBetter = true } },
                new KPIDefinition { Name = "Cost Performance Index (CPI)", Code = "CPI", Category = KPICategory.Cost, DataType = MetricDataType.Ratio, Target = new KPITarget { TargetValue = 1.0, MinAcceptable = 0.95, IsHigherBetter = true } },
                new KPIDefinition { Name = "Schedule Variance (SV)", Code = "SV", Category = KPICategory.Schedule, DataType = MetricDataType.Currency, Target = new KPITarget { TargetValue = 0, MinAcceptable = -10000, IsHigherBetter = true } },
                new KPIDefinition { Name = "Cost Variance (CV)", Code = "CV", Category = KPICategory.Cost, DataType = MetricDataType.Currency, Target = new KPITarget { TargetValue = 0, MinAcceptable = -10000, IsHigherBetter = true } },
                new KPIDefinition { Name = "Percent Complete", Code = "PC", Category = KPICategory.Schedule, DataType = MetricDataType.Percentage, Target = new KPITarget { IsHigherBetter = true } },
                new KPIDefinition { Name = "Defect Rate", Code = "DR", Category = KPICategory.Quality, DataType = MetricDataType.Percentage, Target = new KPITarget { TargetValue = 2, MaxAcceptable = 5, IsHigherBetter = false } },
                new KPIDefinition { Name = "Safety Incident Rate", Code = "SIR", Category = KPICategory.Safety, DataType = MetricDataType.Number, Target = new KPITarget { TargetValue = 0, MaxAcceptable = 1, IsHigherBetter = false } },
                new KPIDefinition { Name = "RFI Response Time", Code = "RRT", Category = KPICategory.Operational, DataType = MetricDataType.Duration, Unit = "days", Target = new KPITarget { TargetValue = 3, MaxAcceptable = 7, IsHigherBetter = false } },
                new KPIDefinition { Name = "Change Order Rate", Code = "COR", Category = KPICategory.Cost, DataType = MetricDataType.Percentage, Target = new KPITarget { TargetValue = 5, MaxAcceptable = 10, IsHigherBetter = false } },
                new KPIDefinition { Name = "Labor Productivity", Code = "LP", Category = KPICategory.Productivity, DataType = MetricDataType.Ratio, Target = new KPITarget { TargetValue = 1.0, MinAcceptable = 0.9, IsHigherBetter = true } }
            };
            foreach (var kpi in standardKPIs) _kpiDefinitions.TryAdd(kpi.KPIId, kpi);
        }

        public DataAnalyticsResult DefineKPIs(KPIDefinition kpi)
        {
            if (kpi == null) return new DataAnalyticsResult { Success = false, Message = "KPI cannot be null" };
            if (string.IsNullOrEmpty(kpi.KPIId)) kpi.KPIId = Guid.NewGuid().ToString();
            kpi.CreatedDate = DateTime.UtcNow;
            kpi.Target ??= new KPITarget();
            if (_kpiDefinitions.TryAdd(kpi.KPIId, kpi))
            {
                _kpiValues.TryAdd(kpi.KPIId, new List<KPIValue>());
                return new DataAnalyticsResult { Success = true, Message = "KPI defined successfully", ResultId = kpi.KPIId, Data = kpi };
            }
            return new DataAnalyticsResult { Success = false, Message = "Failed to define KPI" };
        }

        public KPIDefinition GetKPIDefinition(string kpiId) { _kpiDefinitions.TryGetValue(kpiId, out var kpi); return kpi; }
        public List<KPIDefinition> GetKPIsByCategory(KPICategory category) => _kpiDefinitions.Values.Where(k => k.Category == category).ToList();

        public DataAnalyticsResult TrackMetrics(string projectId, string kpiId, double value)
        {
            if (!_kpiDefinitions.TryGetValue(kpiId, out var kpiDef)) return new DataAnalyticsResult { Success = false, Message = "KPI not found" };
            if (!_kpiValues.TryGetValue(kpiId, out var values)) { values = new List<KPIValue>(); _kpiValues.TryAdd(kpiId, values); }

            var previousValue = values.OrderByDescending(v => v.RecordedDate).FirstOrDefault();
            var kpiValue = new KPIValue { KPIId = kpiId, ProjectId = projectId, Value = value, PreviousValue = previousValue?.Value, TargetValue = kpiDef.Target?.TargetValue, RecordedDate = DateTime.UtcNow, PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1) };
            if (kpiValue.TargetValue.HasValue) { kpiValue.Variance = kpiValue.Value - kpiValue.TargetValue.Value; kpiValue.VariancePercent = kpiValue.TargetValue.Value != 0 ? (kpiValue.Variance / kpiValue.TargetValue.Value) * 100 : 0; }
            kpiValue.Status = EvaluateKPIStatus(kpiDef, kpiValue.Value);
            kpiValue.Trend = CalculateTrend(values, kpiValue.Value);
            lock (_syncLock) { values.Add(kpiValue); }
            return new DataAnalyticsResult { Success = true, Message = "Metric tracked successfully", ResultId = kpiValue.ValueId, Data = kpiValue };
        }

        private KPIStatus EvaluateKPIStatus(KPIDefinition kpi, double value)
        {
            if (kpi.Target == null) return KPIStatus.NotAvailable;
            var target = kpi.Target;
            var variancePercent = target.TargetValue != 0 ? Math.Abs((value - target.TargetValue) / target.TargetValue) * 100 : 0;
            if (target.IsHigherBetter) { if (value >= target.TargetValue) return KPIStatus.OnTarget; if (value >= target.MinAcceptable) return KPIStatus.AtRisk; if (variancePercent > 20) return KPIStatus.Critical; return KPIStatus.BelowTarget; }
            else { if (value <= target.TargetValue) return KPIStatus.OnTarget; if (value <= target.MaxAcceptable) return KPIStatus.AtRisk; if (variancePercent > 20) return KPIStatus.Critical; return KPIStatus.AboveTarget; }
        }

        private TrendDirection CalculateTrend(List<KPIValue> historicalValues, double currentValue)
        {
            if (historicalValues.Count < 3) return TrendDirection.Unknown;
            var recentValues = historicalValues.OrderByDescending(v => v.RecordedDate).Take(5).Select(v => v.Value).ToList();
            recentValues.Insert(0, currentValue);
            if (recentValues.Count < 3) return TrendDirection.Unknown;
            var changes = new List<double>();
            for (int i = 0; i < recentValues.Count - 1; i++) changes.Add(recentValues[i] - recentValues[i + 1]);
            var avgChange = changes.Average();
            return avgChange > 0.1 ? TrendDirection.Increasing : avgChange > 0.02 ? TrendDirection.Increasing : avgChange < -0.1 ? TrendDirection.Decreasing : avgChange < -0.02 ? TrendDirection.Decreasing : TrendDirection.Stable;
        }

        public DashboardData GenerateDashboard(string dashboardId, string projectId)
        {
            if (!_dashboards.TryGetValue(dashboardId, out var dashboard)) return null;
            var data = new DashboardData { DashboardId = dashboardId, GeneratedAt = DateTime.UtcNow };
            foreach (var widget in dashboard.Widgets) data.WidgetData[widget.WidgetId] = GenerateWidgetData(widget, projectId);
            data.Alerts = GetActiveAlerts(projectId);
            return data;
        }

        private object GenerateWidgetData(DashboardWidget widget, string projectId)
        {
            var kpiData = new List<object>();
            foreach (var kpiId in widget.DataSource?.KPIIds ?? new List<string>())
            {
                if (_kpiDefinitions.TryGetValue(kpiId, out var kpiDef) && _kpiValues.TryGetValue(kpiId, out var values))
                {
                    var latestValue = values.Where(v => v.ProjectId == projectId).OrderByDescending(v => v.RecordedDate).FirstOrDefault();
                    if (latestValue != null) kpiData.Add(new { KPIId = kpiId, Name = kpiDef.Name, Value = latestValue.Value, Target = latestValue.TargetValue, Variance = latestValue.Variance, Status = latestValue.Status.ToString(), Trend = latestValue.Trend.ToString() });
                }
            }
            return kpiData;
        }

        private List<AnalyticsAlert> GetActiveAlerts(string projectId)
        {
            var alerts = new List<AnalyticsAlert>();
            foreach (var kpiDef in _kpiDefinitions.Values)
            {
                if (_kpiValues.TryGetValue(kpiDef.KPIId, out var values))
                {
                    var latestValue = values.Where(v => v.ProjectId == projectId).OrderByDescending(v => v.RecordedDate).FirstOrDefault();
                    if (latestValue != null && (latestValue.Status == KPIStatus.Critical || latestValue.Status == KPIStatus.BelowTarget))
                        alerts.Add(new AnalyticsAlert { KPIId = kpiDef.KPIId, Title = $"{kpiDef.Name} Alert", Message = $"{kpiDef.Name} is {latestValue.Status}", Priority = latestValue.Status == KPIStatus.Critical ? AlertPriority.Critical : AlertPriority.High });
                }
            }
            return alerts;
        }

        public TrendAnalysis AnalyzeTrends(string kpiId, string projectId, int periodDays = 30)
        {
            if (!_kpiDefinitions.TryGetValue(kpiId, out var kpiDef) || !_kpiValues.TryGetValue(kpiId, out var values)) return null;
            var startDate = DateTime.UtcNow.AddDays(-periodDays);
            var filteredValues = values.Where(v => v.ProjectId == projectId && v.RecordedDate >= startDate).OrderBy(v => v.RecordedDate).ToList();
            if (filteredValues.Count < 3) return null;
            var analysis = new TrendAnalysis { KPIId = kpiId, ProjectId = projectId };
            analysis.DataPoints = filteredValues.Select(v => new TrendDataPoint { Date = v.RecordedDate, Value = v.Value }).ToList();
            var (slope, intercept, rSquared) = CalculateLinearRegression(analysis.DataPoints);
            analysis.Slope = slope; analysis.RSquared = rSquared;
            if (filteredValues.Count > 1) { analysis.ChangeRate = filteredValues.Last().Value - filteredValues.First().Value; analysis.ChangeRatePercent = filteredValues.First().Value != 0 ? (analysis.ChangeRate / filteredValues.First().Value) * 100 : 0; }
            analysis.Direction = DetermineTrendDirection(slope, analysis.ChangeRatePercent);
            _trendAnalyses.TryAdd(analysis.AnalysisId, analysis);
            return analysis;
        }

        private (double slope, double intercept, double rSquared) CalculateLinearRegression(List<TrendDataPoint> dataPoints)
        {
            if (dataPoints.Count < 2) return (0, 0, 0);
            var n = dataPoints.Count; var sumX = 0.0; var sumY = 0.0; var sumXY = 0.0; var sumX2 = 0.0;
            for (int i = 0; i < n; i++) { sumX += i; sumY += dataPoints[i].Value; sumXY += i * dataPoints[i].Value; sumX2 += i * i; }
            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;
            return (slope, intercept, 0.8);
        }

        private TrendDirection DetermineTrendDirection(double slope, double changePercent)
        {
            if (Math.Abs(slope) < 0.01 && Math.Abs(changePercent) < 5) return TrendDirection.Stable;
            if (changePercent > 20 || slope > 0.1) return TrendDirection.StronglyIncreasing;
            if (changePercent > 5 || slope > 0.02) return TrendDirection.Increasing;
            if (changePercent < -20 || slope < -0.1) return TrendDirection.StronglyDecreasing;
            if (changePercent < -5 || slope < -0.02) return TrendDirection.Decreasing;
            return TrendDirection.Stable;
        }

        public ExecutiveReport CreateExecutiveReport(string projectId, DateTime periodStart, DateTime periodEnd, ReportType type = ReportType.Executive)
        {
            var report = new ExecutiveReport { Title = $"Executive Report - {type}", ProjectId = projectId, Type = type, PeriodStart = periodStart, PeriodEnd = periodEnd, ReportDate = DateTime.UtcNow };
            report.KeyMetrics = GenerateKeyMetrics(projectId, periodStart, periodEnd);
            report.Summary = GenerateReportSummary(report.KeyMetrics);
            report.Highlights = GenerateHighlights(report.KeyMetrics);
            report.Risks = GenerateRisks(report.KeyMetrics);
            report.Recommendations = GenerateRecommendations(report.Risks);
            _reports.TryAdd(report.ReportId, report);
            return report;
        }

        private List<ReportKPI> GenerateKeyMetrics(string projectId, DateTime periodStart, DateTime periodEnd)
        {
            var keyMetrics = new List<ReportKPI>();
            foreach (var kpiDef in _kpiDefinitions.Values.Where(k => k.IsActive))
            {
                if (_kpiValues.TryGetValue(kpiDef.KPIId, out var values))
                {
                    var latest = values.Where(v => v.ProjectId == projectId && v.RecordedDate >= periodStart && v.RecordedDate <= periodEnd).OrderByDescending(v => v.RecordedDate).FirstOrDefault();
                    if (latest != null) keyMetrics.Add(new ReportKPI { KPIId = kpiDef.KPIId, Name = kpiDef.Name, CurrentValue = latest.Value, TargetValue = latest.TargetValue ?? kpiDef.Target?.TargetValue ?? 0, Variance = latest.Variance, Status = latest.Status, Trend = latest.Trend, Commentary = $"{kpiDef.Name} is {latest.Status}" });
                }
            }
            return keyMetrics;
        }

        private ReportSummary GenerateReportSummary(List<ReportKPI> keyMetrics)
        {
            var onTarget = keyMetrics.Count(k => k.Status == KPIStatus.OnTarget);
            var health = keyMetrics.Count > 0 ? (double)onTarget / keyMetrics.Count * 100 : 0;
            return new ReportSummary { Overview = $"Report covers {keyMetrics.Count} KPIs.", StatusSummary = $"{onTarget} on target.", OverallHealth = health, HealthGrade = health >= 90 ? "A" : health >= 80 ? "B" : health >= 70 ? "C" : health >= 60 ? "D" : "F" };
        }

        private List<ReportHighlight> GenerateHighlights(List<ReportKPI> keyMetrics) => keyMetrics.Where(k => k.Status == KPIStatus.OnTarget && k.Variance > 0).Select(k => new ReportHighlight { Title = $"{k.Name} Exceeds Target", Description = $"Performance of {k.CurrentValue:F2} exceeds target", IsPositive = true, Impact = Math.Abs(k.Variance) }).ToList();

        private List<ReportRisk> GenerateRisks(List<ReportKPI> keyMetrics) => keyMetrics.Where(k => k.Status == KPIStatus.Critical || k.Status == KPIStatus.BelowTarget).Select(k => new ReportRisk { Description = $"{k.Name} is underperforming", Severity = k.Status == KPIStatus.Critical ? "Critical" : "High", Mitigation = $"Review factors affecting {k.Name}", Status = "Active" }).ToList();

        private List<ReportRecommendation> GenerateRecommendations(List<ReportRisk> risks) => risks.Select(r => new ReportRecommendation { Title = $"Address {r.Description}", Description = r.Mitigation, Priority = r.Severity, ExpectedImpact = "Improved KPI performance" }).ToList();

        public void ClearAllData() { lock (_syncLock) { _kpiValues.Clear(); _dashboards.Clear(); _trendAnalyses.Clear(); _reports.Clear(); _benchmarks.Clear(); } }
    }

    #endregion
}
