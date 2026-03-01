// ============================================================================
// StingBIM AI - FM Dashboard and KPI Reporting
// Consolidated facility management metrics and analytics
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.WorkOrders;
using StingBIM.AI.FacilityManagement.Helpdesk;
using StingBIM.AI.FacilityManagement.SpaceManagement;

namespace StingBIM.AI.FacilityManagement.Reporting
{
    /// <summary>
    /// Consolidated FM Dashboard - all KPIs in one place
    /// </summary>
    public class FMDashboard
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly AssetRegistry _assetRegistry;
        private readonly WorkOrderManager _workOrderManager;
        private readonly ServiceRequestPortal _helpdesk;
        private readonly SpaceRegistry _spaceRegistry;

        public FMDashboard(
            AssetRegistry assetRegistry,
            WorkOrderManager workOrderManager,
            ServiceRequestPortal helpdesk,
            SpaceRegistry? spaceRegistry = null)
        {
            _assetRegistry = assetRegistry;
            _workOrderManager = workOrderManager;
            _helpdesk = helpdesk;
            _spaceRegistry = spaceRegistry ?? new SpaceRegistry();
        }

        /// <summary>
        /// Generate comprehensive FM dashboard
        /// </summary>
        public FMDashboardData GetDashboard(DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var dashboard = new FMDashboardData
            {
                GeneratedAt = DateTime.UtcNow,
                ReportPeriod = new ReportPeriod
                {
                    StartDate = fromDate.Value,
                    EndDate = toDate.Value
                },

                // Asset Metrics
                AssetMetrics = GetAssetMetrics(),

                // Work Order Metrics
                WorkOrderMetrics = GetWorkOrderMetrics(fromDate, toDate),

                // Helpdesk Metrics
                HelpdeskMetrics = GetHelpdeskMetrics(fromDate, toDate),

                // Space Metrics
                SpaceMetrics = GetSpaceMetrics(),

                // Key Performance Indicators
                KPIs = CalculateKPIs(fromDate, toDate),

                // Alerts and Issues
                Alerts = GetActiveAlerts(),

                // Trends
                Trends = CalculateTrends(fromDate, toDate)
            };

            return dashboard;
        }

        #region Metrics Gathering

        private AssetMetricsSection GetAssetMetrics()
        {
            var stats = _assetRegistry.GetStatistics();
            var criticalAssets = _assetRegistry.GetCriticalAssets();
            var attentionRequired = _assetRegistry.GetAssetsRequiringAttention();
            var nearEOL = _assetRegistry.GetAssetsNearEndOfLife(2);

            return new AssetMetricsSection
            {
                TotalAssets = stats.TotalAssets,
                TotalReplacementValue = stats.TotalReplacementValue,
                AverageAge = stats.AverageAge,

                ByStatus = stats.ByStatus.ToDictionary(
                    k => k.Key.ToString(),
                    v => v.Value),
                ByCondition = stats.ByCondition.ToDictionary(
                    k => k.Key.ToString(),
                    v => v.Value),
                ByCriticality = stats.ByCriticality.ToDictionary(
                    k => k.Key.ToString(),
                    v => v.Value),
                BySystem = stats.BySystem,

                CriticalAssetCount = criticalAssets.Count,
                AssetsRequiringAttention = attentionRequired.Count,
                AssetsNearEndOfLife = nearEOL.Count,
                AssetsUnderWarranty = stats.AssetsUnderWarranty,
                MaintenanceDue = stats.MaintenanceDue,
                LinkedToBIM = stats.LinkedToBIM,

                TopAssetsAtRisk = attentionRequired.Take(5).Select(a => new AssetRiskItem
                {
                    AssetId = a.AssetId,
                    AssetName = a.Name,
                    System = a.System,
                    Condition = a.Condition.ToString(),
                    RemainingLife = a.RemainingLifeYears,
                    RiskReason = GetRiskReason(a)
                }).ToList()
            };
        }

        private WorkOrderMetricsSection GetWorkOrderMetrics(DateTime? fromDate, DateTime? toDate)
        {
            var stats = _workOrderManager.GetStatistics(fromDate, toDate);
            var openWOs = _workOrderManager.GetOpenWorkOrders();
            var overdueWOs = _workOrderManager.GetOverdueWorkOrders();

            return new WorkOrderMetricsSection
            {
                TotalWorkOrders = stats.TotalWorkOrders,
                OpenWorkOrders = stats.OpenWorkOrders,
                CompletedWorkOrders = stats.CompletedWorkOrders,
                OverdueWorkOrders = stats.OverdueWorkOrders,

                ByType = stats.ByType.ToDictionary(k => k.Key.ToString(), v => v.Value),
                ByPriority = stats.ByPriority.ToDictionary(k => k.Key.ToString(), v => v.Value),
                ByStatus = stats.ByStatus.ToDictionary(k => k.Key.ToString(), v => v.Value),
                ByCategory = stats.ByCategory,

                TotalEstimatedCost = stats.TotalEstimatedCost,
                TotalActualCost = stats.TotalActualCost,
                CostVariance = stats.CostVariance,
                CostVariancePercent = stats.TotalEstimatedCost > 0
                    ? (stats.CostVariance / stats.TotalEstimatedCost * 100)
                    : 0,

                AverageCompletionTime = stats.AverageCompletionTime,
                PlannedMaintenancePercent = stats.PlannedMaintenancePercent,
                FirstTimeFixRate = stats.FirstTimeFixRate,

                PredictiveWorkOrders = stats.PredictiveWorkOrders,
                PreventiveWorkOrders = stats.PreventiveWorkOrders,
                ReactiveWorkOrders = stats.ReactiveWorkOrders,

                OverdueWorkOrderList = overdueWOs.Take(10).Select(wo => new WorkOrderSummaryItem
                {
                    WorkOrderId = wo.WorkOrderId,
                    Subject = wo.Subject,
                    Priority = wo.Priority.ToString(),
                    Status = wo.Status.ToString(),
                    DaysOverdue = wo.TargetCompletionDate.HasValue
                        ? (int)(DateTime.UtcNow - wo.TargetCompletionDate.Value).TotalDays
                        : 0,
                    AssetName = wo.AssetName,
                    AssignedTo = wo.AssignedTo
                }).ToList()
            };
        }

        private HelpdeskMetricsSection GetHelpdeskMetrics(DateTime? fromDate, DateTime? toDate)
        {
            var stats = _helpdesk.GetStatistics(fromDate, toDate);
            var slaBreached = _helpdesk.GetSLABreachedRequests();

            return new HelpdeskMetricsSection
            {
                TotalRequests = stats.TotalRequests,
                OpenRequests = stats.OpenRequests,
                ClosedRequests = stats.ClosedRequests,

                ByCategory = stats.ByCategory.ToDictionary(k => k.Key.ToString(), v => v.Value),
                ByPriority = stats.ByPriority.ToDictionary(k => k.Key.ToString(), v => v.Value),
                ByStatus = stats.ByStatus.ToDictionary(k => k.Key.ToString(), v => v.Value),

                AverageResponseTime = stats.AverageResponseTime,
                AverageResolutionTime = stats.AverageResolutionTime,
                SLACompliancePercent = stats.SLACompliancePercent,
                AverageSatisfaction = stats.AverageSatisfaction,

                SLABreachedCount = slaBreached.Count
            };
        }

        private SpaceMetricsSection GetSpaceMetrics()
        {
            var utilization = _spaceRegistry.GetUtilizationReport();

            return new SpaceMetricsSection
            {
                TotalSpaces = utilization.TotalSpaces,
                TotalArea = utilization.TotalArea,
                TotalCapacity = utilization.TotalCapacity,
                CurrentOccupancy = utilization.CurrentOccupancy,
                OccupancyRate = utilization.OccupancyRate,
                VacantSpaces = utilization.VacantSpaces,
                UnderRenovation = utilization.UnderRenovation,

                ByFloor = utilization.ByFloor.ToDictionary(
                    k => k.Key,
                    v => new FloorOccupancy
                    {
                        FloorId = v.Value.FloorId,
                        SpaceCount = v.Value.SpaceCount,
                        Area = v.Value.TotalArea,
                        Capacity = v.Value.Capacity,
                        Occupancy = v.Value.Occupancy,
                        OccupancyRate = v.Value.OccupancyRate
                    }),
                AreaByType = utilization.ByType
            };
        }

        private List<KPIResult> CalculateKPIs(DateTime? fromDate, DateTime? toDate)
        {
            var kpis = new List<KPIResult>();
            var woStats = _workOrderManager.GetStatistics(fromDate, toDate);
            var hdStats = _helpdesk.GetStatistics(fromDate, toDate);
            var assetStats = _assetRegistry.GetStatistics();

            // Work Order Completion Rate
            var woTotal = woStats.TotalWorkOrders > 0 ? woStats.TotalWorkOrders : 1;
            var completionRate = woStats.CompletedWorkOrders * 100.0 / woTotal;
            kpis.Add(new KPIResult
            {
                Name = "Work Order Completion Rate",
                Value = completionRate,
                Target = 95,
                Unit = "%",
                Status = completionRate >= 95 ? KPIStatus.Good :
                        completionRate >= 85 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Stable
            });

            // Planned Maintenance Percentage
            kpis.Add(new KPIResult
            {
                Name = "Planned Maintenance %",
                Value = woStats.PlannedMaintenancePercent,
                Target = 80,
                Unit = "%",
                Status = woStats.PlannedMaintenancePercent >= 80 ? KPIStatus.Good :
                        woStats.PlannedMaintenancePercent >= 60 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Up
            });

            // First Time Fix Rate
            kpis.Add(new KPIResult
            {
                Name = "First Time Fix Rate",
                Value = woStats.FirstTimeFixRate,
                Target = 85,
                Unit = "%",
                Status = woStats.FirstTimeFixRate >= 85 ? KPIStatus.Good :
                        woStats.FirstTimeFixRate >= 70 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Stable
            });

            // SLA Compliance
            kpis.Add(new KPIResult
            {
                Name = "SLA Compliance",
                Value = hdStats.SLACompliancePercent,
                Target = 90,
                Unit = "%",
                Status = hdStats.SLACompliancePercent >= 90 ? KPIStatus.Good :
                        hdStats.SLACompliancePercent >= 80 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Stable
            });

            // Mean Time to Repair (MTTR)
            kpis.Add(new KPIResult
            {
                Name = "Mean Time to Repair",
                Value = woStats.AverageCompletionTime,
                Target = 4,
                Unit = "hours",
                Status = woStats.AverageCompletionTime <= 4 ? KPIStatus.Good :
                        woStats.AverageCompletionTime <= 8 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Down
            });

            // Asset Availability
            var operational = assetStats.ByStatus.TryGetValue(AssetStatus.Operational, out var opCount) ? opCount : 0;
            var availability = assetStats.TotalAssets > 0
                ? operational * 100.0 / assetStats.TotalAssets
                : 100;
            kpis.Add(new KPIResult
            {
                Name = "Asset Availability",
                Value = availability,
                Target = 98,
                Unit = "%",
                Status = availability >= 98 ? KPIStatus.Good :
                        availability >= 95 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Stable
            });

            // Customer Satisfaction
            kpis.Add(new KPIResult
            {
                Name = "Customer Satisfaction",
                Value = hdStats.AverageSatisfaction,
                Target = 4,
                Unit = "/5",
                Status = hdStats.AverageSatisfaction >= 4 ? KPIStatus.Good :
                        hdStats.AverageSatisfaction >= 3 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = TrendDirection.Up
            });

            // Cost Variance
            var costVar = woStats.TotalEstimatedCost > 0
                ? Math.Abs(woStats.CostVariance) / woStats.TotalEstimatedCost * 100
                : 0;
            kpis.Add(new KPIResult
            {
                Name = "Cost Variance",
                Value = (double)costVar,
                Target = 10,
                Unit = "%",
                Status = costVar <= 10 ? KPIStatus.Good :
                        costVar <= 20 ? KPIStatus.Warning : KPIStatus.Critical,
                Trend = woStats.CostVariance > 0 ? TrendDirection.Up : TrendDirection.Down
            });

            return kpis;
        }

        private List<AlertItem> GetActiveAlerts()
        {
            var alerts = new List<AlertItem>();

            // Critical assets
            var criticalAssets = _assetRegistry.GetAssetsRequiringAttention()
                .Where(a => a.Condition == AssetCondition.Critical || a.Status == AssetStatus.Failed);
            foreach (var asset in criticalAssets.Take(5))
            {
                alerts.Add(new AlertItem
                {
                    AlertType = AlertType.Critical,
                    Category = "Asset",
                    Title = $"Critical Asset: {asset.Name}",
                    Description = $"Asset in {asset.Condition} condition, Status: {asset.Status}",
                    Timestamp = DateTime.UtcNow,
                    EntityId = asset.AssetId
                });
            }

            // Overdue work orders
            var overdueWOs = _workOrderManager.GetOverdueWorkOrders();
            foreach (var wo in overdueWOs.Where(w => w.Priority == WorkOrderPriority.Critical).Take(5))
            {
                alerts.Add(new AlertItem
                {
                    AlertType = AlertType.Warning,
                    Category = "Work Order",
                    Title = $"Overdue: {wo.Subject}",
                    Description = $"Priority: {wo.Priority}, Target: {wo.TargetCompletionDate:g}",
                    Timestamp = DateTime.UtcNow,
                    EntityId = wo.WorkOrderId
                });
            }

            // SLA breaches
            var slaBreached = _helpdesk.GetSLABreachedRequests();
            foreach (var req in slaBreached.Take(5))
            {
                alerts.Add(new AlertItem
                {
                    AlertType = AlertType.Warning,
                    Category = "SLA",
                    Title = $"SLA Breached: {req.Subject}",
                    Description = $"Priority: {req.Priority}, Target was: {req.SLAResolutionTarget:g}",
                    Timestamp = DateTime.UtcNow,
                    EntityId = req.RequestId
                });
            }

            // Assets near end of life
            var eolAssets = _assetRegistry.GetAssetsNearEndOfLife(1);
            foreach (var asset in eolAssets.Take(3))
            {
                alerts.Add(new AlertItem
                {
                    AlertType = AlertType.Info,
                    Category = "Lifecycle",
                    Title = $"End of Life Approaching: {asset.Name}",
                    Description = $"Remaining life: {asset.RemainingLifeYears:F1} years, Replacement cost: {asset.ReplacementCost:N0}",
                    Timestamp = DateTime.UtcNow,
                    EntityId = asset.AssetId
                });
            }

            return alerts.OrderByDescending(a => a.AlertType).ToList();
        }

        private TrendData CalculateTrends(DateTime? fromDate, DateTime? toDate)
        {
            // In production, this would compare to previous period
            return new TrendData
            {
                WorkOrderTrend = TrendDirection.Stable,
                CostTrend = TrendDirection.Down,
                SatisfactionTrend = TrendDirection.Up,
                AssetConditionTrend = TrendDirection.Stable
            };
        }

        private string GetRiskReason(Asset asset)
        {
            var reasons = new List<string>();

            if (asset.Condition == AssetCondition.Critical)
                reasons.Add("Critical condition");
            else if (asset.Condition == AssetCondition.Poor)
                reasons.Add("Poor condition");

            if (asset.IsNearEndOfLife)
                reasons.Add($"Near EOL ({asset.RemainingLifeYears:F1} years)");

            if (asset.IsMaintenanceDue)
                reasons.Add("Maintenance overdue");

            if (asset.Status == AssetStatus.Degraded)
                reasons.Add("Degraded performance");

            return reasons.Any() ? string.Join("; ", reasons) : "Requires review";
        }

        #endregion
    }

    #region Dashboard Data Models

    public class FMDashboardData
    {
        public DateTime GeneratedAt { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = new();
        public AssetMetricsSection AssetMetrics { get; set; } = new();
        public WorkOrderMetricsSection WorkOrderMetrics { get; set; } = new();
        public HelpdeskMetricsSection HelpdeskMetrics { get; set; } = new();
        public SpaceMetricsSection SpaceMetrics { get; set; } = new();
        public List<KPIResult> KPIs { get; set; } = new();
        public List<AlertItem> Alerts { get; set; } = new();
        public TrendData Trends { get; set; } = new();
    }

    public class ReportPeriod
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class AssetMetricsSection
    {
        public int TotalAssets { get; set; }
        public decimal TotalReplacementValue { get; set; }
        public double AverageAge { get; set; }
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByCondition { get; set; } = new();
        public Dictionary<string, int> ByCriticality { get; set; } = new();
        public Dictionary<string, int> BySystem { get; set; } = new();
        public int CriticalAssetCount { get; set; }
        public int AssetsRequiringAttention { get; set; }
        public int AssetsNearEndOfLife { get; set; }
        public int AssetsUnderWarranty { get; set; }
        public int MaintenanceDue { get; set; }
        public int LinkedToBIM { get; set; }
        public List<AssetRiskItem> TopAssetsAtRisk { get; set; } = new();
    }

    public class AssetRiskItem
    {
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public double RemainingLife { get; set; }
        public string RiskReason { get; set; } = string.Empty;
    }

    public class WorkOrderMetricsSection
    {
        public int TotalWorkOrders { get; set; }
        public int OpenWorkOrders { get; set; }
        public int CompletedWorkOrders { get; set; }
        public int OverdueWorkOrders { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByPriority { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public decimal TotalEstimatedCost { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal CostVariance { get; set; }
        public decimal CostVariancePercent { get; set; }
        public double AverageCompletionTime { get; set; }
        public double PlannedMaintenancePercent { get; set; }
        public double FirstTimeFixRate { get; set; }
        public int PredictiveWorkOrders { get; set; }
        public int PreventiveWorkOrders { get; set; }
        public int ReactiveWorkOrders { get; set; }
        public List<WorkOrderSummaryItem> OverdueWorkOrderList { get; set; } = new();
    }

    public class WorkOrderSummaryItem
    {
        public string WorkOrderId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int DaysOverdue { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
    }

    public class HelpdeskMetricsSection
    {
        public int TotalRequests { get; set; }
        public int OpenRequests { get; set; }
        public int ClosedRequests { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, int> ByPriority { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public double AverageResponseTime { get; set; }
        public double AverageResolutionTime { get; set; }
        public double SLACompliancePercent { get; set; }
        public double AverageSatisfaction { get; set; }
        public int SLABreachedCount { get; set; }
    }

    public class SpaceMetricsSection
    {
        public int TotalSpaces { get; set; }
        public double TotalArea { get; set; }
        public int TotalCapacity { get; set; }
        public int CurrentOccupancy { get; set; }
        public double OccupancyRate { get; set; }
        public int VacantSpaces { get; set; }
        public int UnderRenovation { get; set; }
        public Dictionary<string, FloorOccupancy> ByFloor { get; set; } = new();
        public Dictionary<string, double> AreaByType { get; set; } = new();
    }

    public class FloorOccupancy
    {
        public string FloorId { get; set; } = string.Empty;
        public int SpaceCount { get; set; }
        public double Area { get; set; }
        public int Capacity { get; set; }
        public int Occupancy { get; set; }
        public double OccupancyRate { get; set; }
    }

    public class KPIResult
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Target { get; set; }
        public string Unit { get; set; } = string.Empty;
        public KPIStatus Status { get; set; }
        public TrendDirection Trend { get; set; }
    }

    public enum KPIStatus { Good, Warning, Critical }
    public enum TrendDirection { Up, Down, Stable }

    public class AlertItem
    {
        public AlertType AlertType { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string EntityId { get; set; } = string.Empty;
    }

    public enum AlertType { Critical, Warning, Info }

    public class TrendData
    {
        public TrendDirection WorkOrderTrend { get; set; }
        public TrendDirection CostTrend { get; set; }
        public TrendDirection SatisfactionTrend { get; set; }
        public TrendDirection AssetConditionTrend { get; set; }
    }

    #endregion
}
