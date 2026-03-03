// ============================================================================
// StingBIM AI - Work Order Management System (CMMS)
// Integrated with Predictive Maintenance Scheduler
// Automatically generates work orders from AI predictions and service requests
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.Helpdesk;

namespace StingBIM.AI.FacilityManagement.WorkOrders
{
    #region Work Order Models

    /// <summary>
    /// Work Order - Core maintenance task record
    /// </summary>
    public class WorkOrder
    {
        // Identification
        public string WorkOrderId { get; set; } = string.Empty;
        public string ParentWorkOrderId { get; set; } = string.Empty;
        public string ServiceRequestId { get; set; } = string.Empty;

        // Classification
        public WorkOrderType Type { get; set; }
        public WorkOrderPriority Priority { get; set; }
        public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;
        public string Category { get; set; } = string.Empty;

        // Asset Information
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public Guid? RevitElementGuid { get; set; }

        // Location
        public string LocationId { get; set; } = string.Empty;
        public string FloorId { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public string LocationDescription { get; set; } = string.Empty;

        // Description
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Procedure { get; set; } = string.Empty;
        public string SafetyNotes { get; set; } = string.Empty;

        // Scheduling
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ScheduledDate { get; set; }
        public DateTime? TargetCompletionDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualCompletionDate { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public TimeSpan? ActualDuration { get; set; }

        // Assignment
        public string CreatedBy { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string AssignedTeam { get; set; } = string.Empty;
        public List<string> RequiredSkills { get; set; } = new();

        // Resources
        public List<WorkOrderPart> PartsRequired { get; set; } = new();
        public List<WorkOrderPart> PartsUsed { get; set; } = new();
        public List<WorkOrderLabor> LaborEntries { get; set; } = new();
        public List<string> ToolsRequired { get; set; } = new();

        // Costs
        public decimal EstimatedLaborCost { get; set; }
        public decimal EstimatedPartsCost { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public decimal ActualLaborCost { get; set; }
        public decimal ActualPartsCost { get; set; }
        public decimal ActualTotalCost { get; set; }
        public string CostCode { get; set; } = string.Empty;

        // Completion
        public string CompletionNotes { get; set; } = string.Empty;
        public string FailureCode { get; set; } = string.Empty;
        public string FailureCause { get; set; } = string.Empty;
        public string ActionTaken { get; set; } = string.Empty;
        public AssetCondition? ConditionAfter { get; set; }
        public bool RequiresFollowUp { get; set; }
        public string FollowUpNotes { get; set; } = string.Empty;

        // Attachments
        public List<WorkOrderAttachment> Attachments { get; set; } = new();

        // Predictive Source (if generated from AI)
        public string PredictionSource { get; set; } = string.Empty;
        public double? FailureProbability { get; set; }
        public string PredictedFailureMode { get; set; } = string.Empty;

        // Audit Trail
        public List<WorkOrderHistoryEntry> History { get; set; } = new();

        // Calculated Properties
        public bool IsOverdue => TargetCompletionDate.HasValue &&
                                  DateTime.UtcNow > TargetCompletionDate &&
                                  Status != WorkOrderStatus.Completed &&
                                  Status != WorkOrderStatus.Closed;
        public decimal CostVariance => ActualTotalCost - EstimatedTotalCost;
        public double DurationVariance => ActualDuration.HasValue
            ? (ActualDuration.Value - EstimatedDuration).TotalHours
            : 0;
    }

    public class WorkOrderPart
    {
        public string PartId { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public int QuantityRequired { get; set; }
        public int QuantityUsed { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => QuantityUsed * UnitCost;
        public string WarehouseLocation { get; set; } = string.Empty;
        public bool IsInStock { get; set; }
    }

    public class WorkOrderLabor
    {
        public string EntryId { get; set; } = string.Empty;
        public string TechnicianId { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public decimal HourlyRate { get; set; }
        public decimal TotalCost => (decimal)Duration.TotalHours * HourlyRate;
        public string WorkPerformed { get; set; } = string.Empty;
        public bool IsOvertime { get; set; }
    }

    public class WorkOrderAttachment
    {
        public string AttachmentId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public AttachmentType Type { get; set; }
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class WorkOrderHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public WorkOrderStatus? OldStatus { get; set; }
        public WorkOrderStatus? NewStatus { get; set; }
        public string? FieldChanged { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public enum WorkOrderType
    {
        Preventive,      // Scheduled maintenance
        Predictive,      // AI/condition-based
        Corrective,      // Reactive repair
        Emergency,       // Urgent/safety
        Inspection,      // Routine checks
        Project,         // Capital works
        ServiceRequest   // From helpdesk
    }

    public enum WorkOrderPriority
    {
        Critical,    // Immediate (0-1 hour)
        Emergency,   // Alias for Critical
        High,        // Urgent (1-4 hours)
        Urgent,      // Alias for High
        Medium,      // Standard (1-2 days)
        Low,         // Scheduled (1-2 weeks)
        Planned      // Next maintenance window
    }

    public enum WorkOrderStatus
    {
        Draft,
        Pending,
        Approved,
        Scheduled,
        Assigned,
        InProgress,
        OnHold,
        PendingParts,
        PendingApproval,
        Completed,
        Closed,
        Cancelled
    }

    public enum AttachmentType
    {
        Photo,
        Document,
        Video,
        Drawing,
        Report
    }

    #endregion

    #region Work Order Manager

    /// <summary>
    /// Central work order management system
    /// Integrates with predictive maintenance and helpdesk
    /// </summary>
    public class WorkOrderManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, WorkOrder> _workOrders;
        private readonly Dictionary<string, List<WorkOrder>> _workOrdersByAsset;
        private readonly AssetRegistry _assetRegistry;
        private readonly TechnicianDispatcher _dispatcher;
        private readonly object _lock = new();
        private int _workOrderCounter = 0;

        public event EventHandler<WorkOrder>? WorkOrderCreated;
        public event EventHandler<WorkOrder>? WorkOrderCompleted;
        public event EventHandler<WorkOrder>? WorkOrderOverdue;

        public WorkOrderManager(AssetRegistry assetRegistry, TechnicianDispatcher? dispatcher = null)
        {
            _assetRegistry = assetRegistry;
            _dispatcher = dispatcher ?? new TechnicianDispatcher();
            _workOrders = new Dictionary<string, WorkOrder>(StringComparer.OrdinalIgnoreCase);
            _workOrdersByAsset = new Dictionary<string, List<WorkOrder>>(StringComparer.OrdinalIgnoreCase);
        }

        #region Work Order Creation

        /// <summary>
        /// Create work order from predictive maintenance alert
        /// </summary>
        public WorkOrder CreateFromPrediction(
            string assetId,
            double failureProbability,
            string predictedFailureMode,
            string recommendedAction,
            DateTime? scheduledDate = null)
        {
            var asset = _assetRegistry.GetById(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset not found: {assetId}");

            var priority = DeterminePriorityFromProbability(failureProbability);
            var workOrderId = GenerateWorkOrderId("PdM");

            var workOrder = new WorkOrder
            {
                WorkOrderId = workOrderId,
                Type = WorkOrderType.Predictive,
                Priority = priority,
                Status = WorkOrderStatus.Pending,
                Category = asset.System,

                AssetId = assetId,
                AssetName = asset.Name,
                AssetType = asset.AssetType,
                RevitElementGuid = asset.RevitElementGuid,

                LocationId = asset.LocationId,
                FloorId = asset.FloorId,
                RoomNumber = asset.RoomNumber,

                Subject = $"Predictive Maintenance - {asset.Name}",
                Description = $"AI-predicted potential failure: {predictedFailureMode}\n" +
                             $"Failure probability: {failureProbability:P0}\n" +
                             $"Recommended action: {recommendedAction}",

                ScheduledDate = scheduledDate ?? DateTime.UtcNow.AddDays(GetDaysUntilFailure(failureProbability)),
                EstimatedDuration = TimeSpan.FromHours(2),

                PredictionSource = "PredictiveMaintenanceScheduler",
                FailureProbability = failureProbability,
                PredictedFailureMode = predictedFailureMode,

                CreatedBy = "AI System"
            };

            // Set required skills based on asset type
            workOrder.RequiredSkills = GetRequiredSkills(asset.System, asset.AssetType);

            RegisterWorkOrder(workOrder);

            Logger.Info($"Created predictive work order {workOrderId} for {asset.Name} - Failure probability: {failureProbability:P0}");

            return workOrder;
        }

        /// <summary>
        /// Create work order from service request
        /// </summary>
        public WorkOrder CreateFromServiceRequest(ServiceRequest request)
        {
            var workOrderId = GenerateWorkOrderId("SR");
            var asset = !string.IsNullOrEmpty(request.InferredAssetId)
                ? _assetRegistry.GetById(request.InferredAssetId)
                : null;

            var workOrder = new WorkOrder
            {
                WorkOrderId = workOrderId,
                ServiceRequestId = request.RequestId,
                Type = WorkOrderType.ServiceRequest,
                Priority = MapRequestPriority(request.Priority),
                Status = WorkOrderStatus.Pending,
                Category = request.Category.ToString(),

                AssetId = request.InferredAssetId ?? string.Empty,
                AssetName = asset?.Name ?? request.InferredAssetType ?? "Unknown",
                AssetType = request.InferredAssetType ?? string.Empty,
                RevitElementGuid = asset?.RevitElementGuid,

                LocationId = request.LocationId,
                FloorId = request.FloorId,
                RoomNumber = request.RoomNumber,

                Subject = request.Subject,
                Description = request.Description,

                ScheduledDate = DateTime.UtcNow,
                TargetCompletionDate = request.SLAResolutionTarget,
                EstimatedDuration = TimeSpan.FromHours(2),

                CreatedBy = "Helpdesk System"
            };

            workOrder.RequiredSkills = GetRequiredSkills(request.Category.ToString(), request.InferredAssetType ?? "");

            RegisterWorkOrder(workOrder);

            Logger.Info($"Created work order {workOrderId} from service request {request.RequestId}");

            return workOrder;
        }

        /// <summary>
        /// Create scheduled preventive maintenance work order
        /// </summary>
        public WorkOrder CreatePreventiveMaintenance(
            string assetId,
            string procedure,
            DateTime scheduledDate,
            TimeSpan estimatedDuration,
            List<WorkOrderPart>? partsRequired = null)
        {
            var asset = _assetRegistry.GetById(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset not found: {assetId}");

            var workOrderId = GenerateWorkOrderId("PM");

            var workOrder = new WorkOrder
            {
                WorkOrderId = workOrderId,
                Type = WorkOrderType.Preventive,
                Priority = WorkOrderPriority.Planned,
                Status = WorkOrderStatus.Scheduled,
                Category = asset.System,

                AssetId = assetId,
                AssetName = asset.Name,
                AssetType = asset.AssetType,
                RevitElementGuid = asset.RevitElementGuid,

                LocationId = asset.LocationId,
                FloorId = asset.FloorId,
                RoomNumber = asset.RoomNumber,

                Subject = $"Scheduled PM - {asset.Name}",
                Description = $"Scheduled preventive maintenance for {asset.AssetType}",
                Procedure = procedure,

                ScheduledDate = scheduledDate,
                EstimatedDuration = estimatedDuration,
                PartsRequired = partsRequired ?? new List<WorkOrderPart>(),

                CreatedBy = "PM Scheduler"
            };

            workOrder.RequiredSkills = GetRequiredSkills(asset.System, asset.AssetType);
            workOrder.EstimatedPartsCost = partsRequired?.Sum(p => p.QuantityRequired * p.UnitCost) ?? 0;
            workOrder.EstimatedLaborCost = (decimal)estimatedDuration.TotalHours * 75000; // UGX rate
            workOrder.EstimatedTotalCost = workOrder.EstimatedPartsCost + workOrder.EstimatedLaborCost;

            RegisterWorkOrder(workOrder);

            return workOrder;
        }

        /// <summary>
        /// Create emergency work order
        /// </summary>
        public WorkOrder CreateEmergencyWorkOrder(
            string assetId,
            string description,
            string reportedBy)
        {
            var asset = _assetRegistry.GetById(assetId);
            var workOrderId = GenerateWorkOrderId("EM");

            var workOrder = new WorkOrder
            {
                WorkOrderId = workOrderId,
                Type = WorkOrderType.Emergency,
                Priority = WorkOrderPriority.Critical,
                Status = WorkOrderStatus.Assigned,
                Category = asset?.System ?? "Emergency",

                AssetId = assetId ?? string.Empty,
                AssetName = asset?.Name ?? "Unknown",
                AssetType = asset?.AssetType ?? string.Empty,
                RevitElementGuid = asset?.RevitElementGuid,

                LocationId = asset?.LocationId ?? string.Empty,
                FloorId = asset?.FloorId ?? string.Empty,

                Subject = "EMERGENCY - " + (asset?.Name ?? description.Substring(0, Math.Min(50, description.Length))),
                Description = description,

                ScheduledDate = DateTime.UtcNow,
                TargetCompletionDate = DateTime.UtcNow.AddHours(1),
                EstimatedDuration = TimeSpan.FromHours(2),

                CreatedBy = reportedBy
            };

            RegisterWorkOrder(workOrder);

            // Auto-dispatch to available technician
            _dispatcher.DispatchEmergency(workOrder);

            Logger.Warn($"EMERGENCY work order created: {workOrderId}");

            return workOrder;
        }

        #endregion

        #region Work Order Operations

        /// <summary>
        /// Assign work order to technician
        /// </summary>
        public bool AssignWorkOrder(string workOrderId, string technicianId, string assignedBy)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                var oldStatus = workOrder.Status;
                workOrder.AssignedTo = technicianId;
                workOrder.Status = WorkOrderStatus.Assigned;

                AddHistory(workOrder, "Assigned", assignedBy,
                    $"Assigned to {technicianId}", oldStatus, WorkOrderStatus.Assigned);

                return true;
            }
        }

        /// <summary>
        /// Start work on work order
        /// </summary>
        public bool StartWorkOrder(string workOrderId, string technicianId)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                var oldStatus = workOrder.Status;
                workOrder.Status = WorkOrderStatus.InProgress;
                workOrder.ActualStartDate = DateTime.UtcNow;

                AddHistory(workOrder, "Started", technicianId,
                    "Work started", oldStatus, WorkOrderStatus.InProgress);

                return true;
            }
        }

        /// <summary>
        /// Add labor entry
        /// </summary>
        public bool AddLaborEntry(string workOrderId, WorkOrderLabor laborEntry)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                laborEntry.EntryId = $"LAB-{workOrder.LaborEntries.Count + 1:D3}";
                workOrder.LaborEntries.Add(laborEntry);
                workOrder.ActualLaborCost += laborEntry.TotalCost;
                workOrder.ActualTotalCost = workOrder.ActualLaborCost + workOrder.ActualPartsCost;

                return true;
            }
        }

        /// <summary>
        /// Add part usage
        /// </summary>
        public bool AddPartUsage(string workOrderId, WorkOrderPart part)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                workOrder.PartsUsed.Add(part);
                workOrder.ActualPartsCost += part.TotalCost;
                workOrder.ActualTotalCost = workOrder.ActualLaborCost + workOrder.ActualPartsCost;

                return true;
            }
        }

        /// <summary>
        /// Complete work order
        /// </summary>
        public bool CompleteWorkOrder(
            string workOrderId,
            string completedBy,
            string completionNotes,
            string actionTaken,
            AssetCondition? conditionAfter = null)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                var oldStatus = workOrder.Status;
                workOrder.Status = WorkOrderStatus.Completed;
                workOrder.ActualCompletionDate = DateTime.UtcNow;
                workOrder.ActualDuration = workOrder.ActualStartDate.HasValue
                    ? DateTime.UtcNow - workOrder.ActualStartDate.Value
                    : workOrder.EstimatedDuration;
                workOrder.CompletionNotes = completionNotes;
                workOrder.ActionTaken = actionTaken;
                workOrder.ConditionAfter = conditionAfter;

                AddHistory(workOrder, "Completed", completedBy,
                    completionNotes, oldStatus, WorkOrderStatus.Completed);

                // Update asset maintenance date
                if (!string.IsNullOrEmpty(workOrder.AssetId))
                {
                    var asset = _assetRegistry.GetById(workOrder.AssetId);
                    if (asset != null)
                    {
                        asset.LastMaintenanceDate = DateTime.UtcNow;
                        asset.NextMaintenanceDate = DateTime.UtcNow.AddDays(asset.MaintenanceIntervalDays);
                        if (conditionAfter.HasValue)
                            asset.Condition = conditionAfter.Value;
                    }
                }

                WorkOrderCompleted?.Invoke(this, workOrder);

                Logger.Info($"Work order {workOrderId} completed by {completedBy}");

                return true;
            }
        }

        /// <summary>
        /// Close work order
        /// </summary>
        public bool CloseWorkOrder(string workOrderId, string closedBy, string? notes = null)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                if (workOrder.Status != WorkOrderStatus.Completed)
                    return false;

                var oldStatus = workOrder.Status;
                workOrder.Status = WorkOrderStatus.Closed;

                AddHistory(workOrder, "Closed", closedBy, notes, oldStatus, WorkOrderStatus.Closed);

                return true;
            }
        }

        /// <summary>
        /// Put work order on hold
        /// </summary>
        public bool PutOnHold(string workOrderId, string heldBy, string reason)
        {
            lock (_lock)
            {
                if (!_workOrders.TryGetValue(workOrderId, out var workOrder))
                    return false;

                var oldStatus = workOrder.Status;
                workOrder.Status = WorkOrderStatus.OnHold;

                AddHistory(workOrder, "On Hold", heldBy, reason, oldStatus, WorkOrderStatus.OnHold);

                return true;
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Get work order by ID
        /// </summary>
        public WorkOrder? GetById(string workOrderId)
        {
            lock (_lock)
            {
                return _workOrders.TryGetValue(workOrderId, out var wo) ? wo : null;
            }
        }

        /// <summary>
        /// Get work orders for asset
        /// </summary>
        public IReadOnlyList<WorkOrder> GetByAsset(string assetId)
        {
            lock (_lock)
            {
                return _workOrdersByAsset.TryGetValue(assetId, out var orders)
                    ? orders.ToList()
                    : new List<WorkOrder>();
            }
        }

        /// <summary>
        /// Get open work orders
        /// </summary>
        public IReadOnlyList<WorkOrder> GetOpenWorkOrders()
        {
            lock (_lock)
            {
                return _workOrders.Values
                    .Where(wo => wo.Status != WorkOrderStatus.Closed &&
                                wo.Status != WorkOrderStatus.Cancelled &&
                                wo.Status != WorkOrderStatus.Completed)
                    .OrderByDescending(wo => wo.Priority)
                    .ThenBy(wo => wo.ScheduledDate)
                    .ToList();
            }
        }

        /// <summary>
        /// Get overdue work orders
        /// </summary>
        public IReadOnlyList<WorkOrder> GetOverdueWorkOrders()
        {
            lock (_lock)
            {
                return _workOrders.Values
                    .Where(wo => wo.IsOverdue)
                    .OrderBy(wo => wo.TargetCompletionDate)
                    .ToList();
            }
        }

        /// <summary>
        /// Get work orders by technician
        /// </summary>
        public IReadOnlyList<WorkOrder> GetByTechnician(string technicianId)
        {
            lock (_lock)
            {
                return _workOrders.Values
                    .Where(wo => wo.AssignedTo == technicianId &&
                                wo.Status != WorkOrderStatus.Closed &&
                                wo.Status != WorkOrderStatus.Cancelled)
                    .OrderByDescending(wo => wo.Priority)
                    .ThenBy(wo => wo.ScheduledDate)
                    .ToList();
            }
        }

        /// <summary>
        /// Get work order statistics
        /// </summary>
        public WorkOrderStatistics GetStatistics(DateTime? fromDate = null, DateTime? toDate = null)
        {
            lock (_lock)
            {
                var orders = _workOrders.Values.AsEnumerable();

                if (fromDate.HasValue)
                    orders = orders.Where(wo => wo.CreatedDate >= fromDate.Value);
                if (toDate.HasValue)
                    orders = orders.Where(wo => wo.CreatedDate <= toDate.Value);

                var list = orders.ToList();
                var completed = list.Where(wo => wo.Status == WorkOrderStatus.Completed ||
                                                 wo.Status == WorkOrderStatus.Closed).ToList();

                return new WorkOrderStatistics
                {
                    TotalWorkOrders = list.Count,
                    OpenWorkOrders = list.Count(wo => wo.Status != WorkOrderStatus.Completed &&
                                                      wo.Status != WorkOrderStatus.Closed &&
                                                      wo.Status != WorkOrderStatus.Cancelled),
                    CompletedWorkOrders = completed.Count,
                    OverdueWorkOrders = list.Count(wo => wo.IsOverdue),

                    ByType = list.GroupBy(wo => wo.Type).ToDictionary(g => g.Key, g => g.Count()),
                    ByPriority = list.GroupBy(wo => wo.Priority).ToDictionary(g => g.Key, g => g.Count()),
                    ByStatus = list.GroupBy(wo => wo.Status).ToDictionary(g => g.Key, g => g.Count()),
                    ByCategory = list.GroupBy(wo => wo.Category).ToDictionary(g => g.Key, g => g.Count()),

                    TotalEstimatedCost = list.Sum(wo => wo.EstimatedTotalCost),
                    TotalActualCost = completed.Sum(wo => wo.ActualTotalCost),
                    CostVariance = completed.Sum(wo => wo.CostVariance),

                    AverageCompletionTime = completed.Any(wo => wo.ActualDuration.HasValue)
                        ? completed.Where(wo => wo.ActualDuration.HasValue)
                            .Average(wo => wo.ActualDuration!.Value.TotalHours)
                        : 0,

                    PlannedMaintenancePercent = list.Count > 0
                        ? list.Count(wo => wo.Type == WorkOrderType.Preventive ||
                                          wo.Type == WorkOrderType.Predictive) * 100.0 / list.Count
                        : 0,

                    FirstTimeFixRate = completed.Count > 0
                        ? completed.Count(wo => !wo.RequiresFollowUp) * 100.0 / completed.Count
                        : 100,

                    PredictiveWorkOrders = list.Count(wo => wo.Type == WorkOrderType.Predictive),
                    PreventiveWorkOrders = list.Count(wo => wo.Type == WorkOrderType.Preventive),
                    ReactiveWorkOrders = list.Count(wo => wo.Type == WorkOrderType.Corrective ||
                                                         wo.Type == WorkOrderType.Emergency ||
                                                         wo.Type == WorkOrderType.ServiceRequest)
                };
            }
        }

        #endregion

        #region Private Helpers

        private void RegisterWorkOrder(WorkOrder workOrder)
        {
            lock (_lock)
            {
                _workOrders[workOrder.WorkOrderId] = workOrder;

                if (!string.IsNullOrEmpty(workOrder.AssetId))
                {
                    if (!_workOrdersByAsset.ContainsKey(workOrder.AssetId))
                        _workOrdersByAsset[workOrder.AssetId] = new List<WorkOrder>();
                    _workOrdersByAsset[workOrder.AssetId].Add(workOrder);
                }
            }

            AddHistory(workOrder, "Created", workOrder.CreatedBy,
                $"Work order created: {workOrder.Subject}", null, workOrder.Status);

            WorkOrderCreated?.Invoke(this, workOrder);
        }

        private string GenerateWorkOrderId(string prefix)
        {
            var counter = Interlocked.Increment(ref _workOrderCounter);
            return $"WO-{prefix}-{DateTime.Now:yyyyMMdd}-{counter:D4}";
        }

        private void AddHistory(WorkOrder workOrder, string action, string performedBy,
            string? notes, WorkOrderStatus? oldStatus, WorkOrderStatus? newStatus)
        {
            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                PerformedBy = performedBy,
                Notes = notes,
                OldStatus = oldStatus,
                NewStatus = newStatus
            });
        }

        private WorkOrderPriority DeterminePriorityFromProbability(double failureProbability)
        {
            return failureProbability switch
            {
                > 0.8 => WorkOrderPriority.High,
                > 0.6 => WorkOrderPriority.Medium,
                > 0.4 => WorkOrderPriority.Low,
                _ => WorkOrderPriority.Planned
            };
        }

        private int GetDaysUntilFailure(double probability)
        {
            // Higher probability = sooner scheduling
            return probability switch
            {
                > 0.8 => 3,
                > 0.6 => 7,
                > 0.4 => 14,
                _ => 30
            };
        }

        private WorkOrderPriority MapRequestPriority(RequestPriority priority)
        {
            return priority switch
            {
                RequestPriority.Critical => WorkOrderPriority.Critical,
                RequestPriority.High => WorkOrderPriority.High,
                RequestPriority.Medium => WorkOrderPriority.Medium,
                _ => WorkOrderPriority.Low
            };
        }

        private List<string> GetRequiredSkills(string system, string assetType)
        {
            var skills = new List<string>();

            var systemSkills = system?.ToUpper() switch
            {
                "HVAC" => new[] { "HVAC Technician" },
                "ELECTRICAL" => new[] { "Electrician" },
                "PLUMBING" => new[] { "Plumber" },
                "FIRE" or "FIRE PROTECTION" => new[] { "Fire Technician" },
                "LIFT" or "VERTICAL TRANSPORTATION" => new[] { "Lift Engineer" },
                "BMS" or "BUILDING MANAGEMENT" => new[] { "BMS Technician" },
                "SECURITY" => new[] { "Security Systems" },
                _ => new[] { "General Maintenance" }
            };

            skills.AddRange(systemSkills);

            return skills;
        }

        #endregion
    }

    /// <summary>
    /// Work order statistics summary
    /// </summary>
    public class WorkOrderStatistics
    {
        public int TotalWorkOrders { get; set; }
        public int OpenWorkOrders { get; set; }
        public int CompletedWorkOrders { get; set; }
        public int OverdueWorkOrders { get; set; }

        public Dictionary<WorkOrderType, int> ByType { get; set; } = new();
        public Dictionary<WorkOrderPriority, int> ByPriority { get; set; } = new();
        public Dictionary<WorkOrderStatus, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByCategory { get; set; } = new();

        public decimal TotalEstimatedCost { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal CostVariance { get; set; }

        public double AverageCompletionTime { get; set; }
        public double PlannedMaintenancePercent { get; set; }
        public double FirstTimeFixRate { get; set; }

        public int PredictiveWorkOrders { get; set; }
        public int PreventiveWorkOrders { get; set; }
        public int ReactiveWorkOrders { get; set; }
    }

    #endregion

    #region Technician Dispatcher

    /// <summary>
    /// Automatic technician assignment and dispatching
    /// </summary>
    public class TechnicianDispatcher
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, Technician> _technicians;

        public TechnicianDispatcher()
        {
            _technicians = new Dictionary<string, Technician>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load technicians from CSV
        /// </summary>
        public async Task LoadFromCsvAsync(string csvPath)
        {
            if (!File.Exists(csvPath)) return;

            var lines = await File.ReadAllLinesAsync(csvPath);
            foreach (var line in lines.Skip(1))
            {
                var values = line.Split(',');
                if (values.Length < 5) continue;

                var tech = new Technician
                {
                    TechnicianId = values[0],
                    Name = values[1],
                    EmployeeType = values[2],
                    PrimarySkill = values[4]
                };

                _technicians[tech.TechnicianId] = tech;
            }
        }

        /// <summary>
        /// Find best available technician for work order
        /// </summary>
        public string? FindBestTechnician(WorkOrder workOrder)
        {
            var available = _technicians.Values
                .Where(t => t.IsAvailable && t.EmployeeType == "Internal")
                .Where(t => workOrder.RequiredSkills.Count == 0 ||
                           workOrder.RequiredSkills.Any(s => t.PrimarySkill.Contains(s, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(t => t.CurrentWorkload)
                .FirstOrDefault();

            return available?.TechnicianId;
        }

        /// <summary>
        /// Dispatch emergency work order
        /// </summary>
        public void DispatchEmergency(WorkOrder workOrder)
        {
            var techId = FindBestTechnician(workOrder);
            if (techId != null)
            {
                workOrder.AssignedTo = techId;
                Logger.Info($"Emergency {workOrder.WorkOrderId} dispatched to {techId}");
            }
            else
            {
                Logger.Warn($"No technician available for emergency {workOrder.WorkOrderId}");
            }
        }
    }

    public class Technician
    {
        public string TechnicianId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string EmployeeType { get; set; } = string.Empty;
        public string PrimarySkill { get; set; } = string.Empty;
        public List<string> Skills { get; set; } = new();
        public bool IsAvailable { get; set; } = true;
        public int CurrentWorkload { get; set; }
        public string Shift { get; set; } = "Day";
    }

    #endregion
}
