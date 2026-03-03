// ============================================================================
// StingBIM AI - Work Order Engine
// Complete work order lifecycle management including creation, skill-based
// assignment with load balancing, status transitions with validation,
// SLA compliance tracking, backlog reporting, labor utilization metrics,
// warranty tracking, and overdue escalation.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Maintenance.Models;

namespace StingBIM.AI.Maintenance.Engine
{
    /// <summary>
    /// Work order lifecycle management engine providing creation, assignment,
    /// status tracking, SLA monitoring, backlog reporting, labor utilization,
    /// and escalation for maintenance work orders.
    /// </summary>
    public class WorkOrderEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        #region Internal State

        // Active and historical work orders: orderId -> work order
        private readonly Dictionary<string, WorkOrder> _workOrders =
            new(StringComparer.OrdinalIgnoreCase);

        // Technician pool: techId -> profile
        private readonly Dictionary<string, TechnicianProfile> _technicians =
            new(StringComparer.OrdinalIgnoreCase);

        // SLA response times by priority (hours)
        private readonly Dictionary<WorkOrderPriority, double> _slaResponseHours =
            new()
            {
                { WorkOrderPriority.Emergency, 1.0 },
                { WorkOrderPriority.Urgent, 4.0 },
                { WorkOrderPriority.High, 24.0 },
                { WorkOrderPriority.Medium, 168.0 },  // 7 days
                { WorkOrderPriority.Low, 720.0 }       // 30 days
            };

        // SLA completion times by priority (hours)
        private readonly Dictionary<WorkOrderPriority, double> _slaCompletionHours =
            new()
            {
                { WorkOrderPriority.Emergency, 4.0 },
                { WorkOrderPriority.Urgent, 24.0 },
                { WorkOrderPriority.High, 72.0 },
                { WorkOrderPriority.Medium, 336.0 },   // 14 days
                { WorkOrderPriority.Low, 1440.0 }      // 60 days
            };

        // Valid status transitions
        private static readonly Dictionary<WorkOrderStatus, HashSet<WorkOrderStatus>> _validTransitions =
            new()
            {
                { WorkOrderStatus.Open, new HashSet<WorkOrderStatus> { WorkOrderStatus.Assigned, WorkOrderStatus.Cancelled } },
                { WorkOrderStatus.Assigned, new HashSet<WorkOrderStatus> { WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Cancelled } },
                { WorkOrderStatus.InProgress, new HashSet<WorkOrderStatus> { WorkOrderStatus.OnHold, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled } },
                { WorkOrderStatus.OnHold, new HashSet<WorkOrderStatus> { WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled } },
                { WorkOrderStatus.Completed, new HashSet<WorkOrderStatus> { WorkOrderStatus.Closed, WorkOrderStatus.InProgress } },
                { WorkOrderStatus.Cancelled, new HashSet<WorkOrderStatus>() },
                { WorkOrderStatus.Closed, new HashSet<WorkOrderStatus>() }
            };

        // Auto-increment counter for work order IDs
        private int _nextOrderNumber = 1000;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the work order engine.
        /// </summary>
        public WorkOrderEngine()
        {
            Logger.Info("WorkOrderEngine initialized.");
        }

        #endregion

        #region Technician Management

        /// <summary>
        /// Registers or updates a technician in the workforce pool.
        /// </summary>
        public void RegisterTechnician(TechnicianProfile technician)
        {
            if (technician == null) throw new ArgumentNullException(nameof(technician));

            lock (_lockObject)
            {
                _technicians[technician.TechnicianId] = technician;
            }

            Logger.Debug("Registered technician '{TechId}' ({Name}), skills: [{Skills}]",
                technician.TechnicianId, technician.Name,
                string.Join(", ", technician.Skills));
        }

        /// <summary>
        /// Returns all registered technician profiles.
        /// </summary>
        public List<TechnicianProfile> GetTechnicians()
        {
            lock (_lockObject)
            {
                return _technicians.Values.ToList();
            }
        }

        #endregion

        #region Work Order Creation

        /// <summary>
        /// Creates a new work order with automatic ID generation, SLA target calculation,
        /// and optional auto-assignment to an available technician with matching skills.
        /// </summary>
        /// <param name="assetId">The asset requiring maintenance.</param>
        /// <param name="type">Maintenance type classification.</param>
        /// <param name="description">Description of the work to be performed.</param>
        /// <param name="priority">Work order priority level.</param>
        /// <param name="autoAssign">If true, automatically assign to best-fit technician.</param>
        /// <param name="requiredSkills">Skills required for the work (used for auto-assignment).</param>
        /// <returns>The created work order with generated ID and calculated SLA targets.</returns>
        public WorkOrder CreateWorkOrder(
            string assetId,
            MaintenanceType type,
            string description,
            WorkOrderPriority priority = WorkOrderPriority.Medium,
            bool autoAssign = true,
            List<string> requiredSkills = null)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Description is required.", nameof(description));

            Logger.Info("Creating work order for asset '{AssetId}', type={Type}, priority={Priority}",
                assetId, type, priority);

            string orderId;
            lock (_lockObject)
            {
                orderId = $"WO-{_nextOrderNumber:D6}";
                _nextOrderNumber++;
            }

            // Calculate SLA target dates
            DateTime createdDate = DateTime.UtcNow;
            double responseHours = _slaResponseHours.GetValueOrDefault(priority, 168.0);
            double completionHours = _slaCompletionHours.GetValueOrDefault(priority, 336.0);

            var workOrder = new WorkOrder
            {
                OrderId = orderId,
                AssetId = assetId,
                Type = type,
                Description = description,
                Priority = priority,
                Status = WorkOrderStatus.Open,
                CreatedDate = createdDate,
                TargetCompletionDate = createdDate.AddHours(completionHours),
                EstimatedCost = EstimateWorkOrderCost(type, priority),
                StatusHistory = new List<StatusChange>
                {
                    new StatusChange
                    {
                        FromStatus = WorkOrderStatus.Open,
                        ToStatus = WorkOrderStatus.Open,
                        Timestamp = createdDate,
                        ChangedBy = "System",
                        Reason = "Work order created"
                    }
                }
            };

            // Check warranty coverage
            workOrder.IsWarrantyCovered = CheckWarrantyCoverage(assetId);
            if (workOrder.IsWarrantyCovered)
            {
                workOrder.WarrantyReference = $"WARRANTY-{assetId}";
                workOrder.Notes.Add("Asset is under active warranty. Contact manufacturer for service.");
            }

            // Auto-assign if requested
            if (autoAssign)
            {
                var bestTechnician = FindBestTechnician(requiredSkills ?? new List<string>(), priority);
                if (bestTechnician != null)
                {
                    workOrder.AssignedTo = bestTechnician.TechnicianId;
                    workOrder.Status = WorkOrderStatus.Assigned;
                    workOrder.ScheduledDate = CalculateScheduledDate(bestTechnician, priority);

                    workOrder.StatusHistory.Add(new StatusChange
                    {
                        FromStatus = WorkOrderStatus.Open,
                        ToStatus = WorkOrderStatus.Assigned,
                        Timestamp = DateTime.UtcNow,
                        ChangedBy = "System",
                        Reason = $"Auto-assigned to {bestTechnician.Name} (skill match + load balance)"
                    });

                    // Update technician workload
                    lock (_lockObject)
                    {
                        bestTechnician.ActiveWorkOrders++;
                    }

                    Logger.Info("Auto-assigned WO '{OrderId}' to technician '{TechId}'",
                        orderId, bestTechnician.TechnicianId);
                }
                else
                {
                    Logger.Warn("No available technician for WO '{OrderId}'; left unassigned.", orderId);
                }
            }

            // Store the work order
            lock (_lockObject)
            {
                _workOrders[orderId] = workOrder;
            }

            Logger.Info("Created work order '{OrderId}' for asset '{AssetId}', " +
                        "status={Status}, target completion={TargetDate:yyyy-MM-dd}",
                orderId, assetId, workOrder.Status, workOrder.TargetCompletionDate);

            return workOrder;
        }

        #endregion

        #region Work Order Assignment

        /// <summary>
        /// Assigns a work order to a specific technician with skill validation and
        /// workload balancing. Rejects assignment if the technician is at capacity.
        /// </summary>
        /// <param name="orderId">Work order identifier.</param>
        /// <param name="technicianId">Technician to assign.</param>
        /// <returns>True if assignment succeeded; false if rejected.</returns>
        public bool AssignWorkOrder(string orderId, string technicianId)
        {
            if (string.IsNullOrEmpty(orderId))
                throw new ArgumentException("Order ID is required.", nameof(orderId));
            if (string.IsNullOrEmpty(technicianId))
                throw new ArgumentException("Technician ID is required.", nameof(technicianId));

            Logger.Info("Assigning WO '{OrderId}' to technician '{TechId}'", orderId, technicianId);

            lock (_lockObject)
            {
                if (!_workOrders.TryGetValue(orderId, out var workOrder))
                {
                    Logger.Error("Work order '{OrderId}' not found.", orderId);
                    return false;
                }

                if (!_technicians.TryGetValue(technicianId, out var technician))
                {
                    Logger.Error("Technician '{TechId}' not found.", technicianId);
                    return false;
                }

                // Validate status allows assignment
                if (workOrder.Status != WorkOrderStatus.Open &&
                    workOrder.Status != WorkOrderStatus.Assigned)
                {
                    Logger.Warn("Cannot assign WO '{OrderId}' in status {Status}.",
                        orderId, workOrder.Status);
                    return false;
                }

                // Check technician capacity
                if (!technician.IsAvailable ||
                    technician.ActiveWorkOrders >= technician.MaxConcurrentWorkOrders)
                {
                    Logger.Warn("Technician '{TechId}' is at capacity ({Active}/{Max}).",
                        technicianId, technician.ActiveWorkOrders,
                        technician.MaxConcurrentWorkOrders);
                    return false;
                }

                // Unassign previous technician if reassigning
                if (!string.IsNullOrEmpty(workOrder.AssignedTo) &&
                    _technicians.TryGetValue(workOrder.AssignedTo, out var previousTech))
                {
                    previousTech.ActiveWorkOrders = Math.Max(0, previousTech.ActiveWorkOrders - 1);
                }

                // Perform assignment
                var previousStatus = workOrder.Status;
                workOrder.AssignedTo = technicianId;
                workOrder.Status = WorkOrderStatus.Assigned;
                workOrder.ScheduledDate = CalculateScheduledDate(technician, workOrder.Priority);

                workOrder.StatusHistory.Add(new StatusChange
                {
                    FromStatus = previousStatus,
                    ToStatus = WorkOrderStatus.Assigned,
                    Timestamp = DateTime.UtcNow,
                    ChangedBy = "System",
                    Reason = $"Assigned to {technician.Name}"
                });

                technician.ActiveWorkOrders++;

                Logger.Info("Assigned WO '{OrderId}' to '{TechName}', scheduled for {Date:yyyy-MM-dd}",
                    orderId, technician.Name, workOrder.ScheduledDate);
            }

            return true;
        }

        #endregion

        #region Status Management

        /// <summary>
        /// Updates the status of a work order with validation of allowed transitions.
        /// Records the status change in the audit trail.
        /// </summary>
        /// <param name="orderId">Work order identifier.</param>
        /// <param name="newStatus">Target status.</param>
        /// <param name="notes">Reason or notes for the status change.</param>
        /// <param name="changedBy">User or system performing the change.</param>
        /// <returns>True if the transition was valid and applied; false otherwise.</returns>
        public bool UpdateWorkOrderStatus(
            string orderId,
            WorkOrderStatus newStatus,
            string notes = "",
            string changedBy = "System")
        {
            if (string.IsNullOrEmpty(orderId))
                throw new ArgumentException("Order ID is required.", nameof(orderId));

            Logger.Info("Updating WO '{OrderId}' status to {NewStatus}", orderId, newStatus);

            lock (_lockObject)
            {
                if (!_workOrders.TryGetValue(orderId, out var workOrder))
                {
                    Logger.Error("Work order '{OrderId}' not found.", orderId);
                    return false;
                }

                // Validate transition
                if (!_validTransitions.TryGetValue(workOrder.Status, out var allowedTargets) ||
                    !allowedTargets.Contains(newStatus))
                {
                    Logger.Warn("Invalid status transition for WO '{OrderId}': {From} -> {To}",
                        orderId, workOrder.Status, newStatus);
                    return false;
                }

                var previousStatus = workOrder.Status;
                workOrder.Status = newStatus;

                // Handle status-specific side effects
                switch (newStatus)
                {
                    case WorkOrderStatus.InProgress:
                        workOrder.StartedDate ??= DateTime.UtcNow;
                        break;

                    case WorkOrderStatus.OnHold:
                        workOrder.Notes.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Placed on hold: {notes}");
                        break;

                    case WorkOrderStatus.Cancelled:
                        // Release technician workload
                        ReleaseTechnicianWorkload(workOrder.AssignedTo);
                        workOrder.Notes.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Cancelled: {notes}");
                        break;
                }

                workOrder.StatusHistory.Add(new StatusChange
                {
                    FromStatus = previousStatus,
                    ToStatus = newStatus,
                    Timestamp = DateTime.UtcNow,
                    ChangedBy = changedBy,
                    Reason = notes
                });

                Logger.Info("WO '{OrderId}' status: {From} -> {To} by {By}",
                    orderId, previousStatus, newStatus, changedBy);
            }

            return true;
        }

        /// <summary>
        /// Completes a work order with actual labor hours, parts cost, and completion notes.
        /// Updates technician workload and records failure mode if identified.
        /// </summary>
        /// <param name="orderId">Work order identifier.</param>
        /// <param name="laborHours">Actual labor hours expended.</param>
        /// <param name="partsCost">Actual parts cost in USD.</param>
        /// <param name="notes">Completion notes and findings.</param>
        /// <param name="failureMode">Failure mode identified during work, if applicable.</param>
        /// <param name="partsUsed">List of spare parts consumed.</param>
        /// <returns>The completed work order.</returns>
        public WorkOrder CompleteWorkOrder(
            string orderId,
            double laborHours,
            decimal partsCost,
            string notes = "",
            FailureMode? failureMode = null,
            List<SparePartUsage> partsUsed = null)
        {
            if (string.IsNullOrEmpty(orderId))
                throw new ArgumentException("Order ID is required.", nameof(orderId));

            Logger.Info("Completing WO '{OrderId}' with {Hours:F1}h labor, {Cost:C} parts",
                orderId, laborHours, partsCost);

            lock (_lockObject)
            {
                if (!_workOrders.TryGetValue(orderId, out var workOrder))
                {
                    throw new InvalidOperationException($"Work order '{orderId}' not found.");
                }

                if (workOrder.Status != WorkOrderStatus.InProgress &&
                    workOrder.Status != WorkOrderStatus.Assigned)
                {
                    throw new InvalidOperationException(
                        $"Work order '{orderId}' cannot be completed from status {workOrder.Status}.");
                }

                var previousStatus = workOrder.Status;
                DateTime completedDate = DateTime.UtcNow;

                // Record actuals
                workOrder.Status = WorkOrderStatus.Completed;
                workOrder.CompletedDate = completedDate;
                workOrder.StartedDate ??= completedDate;
                workOrder.LaborHours = laborHours;
                workOrder.PartsCost = partsCost;

                // Calculate labor cost (default rate $75/hour)
                workOrder.LaborCost = (decimal)laborHours * 75.0m;
                workOrder.TotalCost = workOrder.LaborCost + partsCost;

                // Record failure mode if identified
                if (failureMode.HasValue)
                {
                    workOrder.IdentifiedFailureMode = failureMode;
                }

                // Record parts used
                if (partsUsed != null)
                {
                    workOrder.PartsUsed.AddRange(partsUsed);
                }

                // Add completion notes
                if (!string.IsNullOrEmpty(notes))
                {
                    workOrder.Notes.Add($"[{completedDate:yyyy-MM-dd HH:mm}] Completion: {notes}");
                }

                workOrder.StatusHistory.Add(new StatusChange
                {
                    FromStatus = previousStatus,
                    ToStatus = WorkOrderStatus.Completed,
                    Timestamp = completedDate,
                    ChangedBy = workOrder.AssignedTo,
                    Reason = $"Completed: {laborHours:F1}h labor, {partsCost:C} parts. {notes}"
                });

                // Release technician workload
                ReleaseTechnicianWorkload(workOrder.AssignedTo);

                // Check SLA compliance
                bool slaCompliant = workOrder.TargetCompletionDate.HasValue &&
                                    completedDate <= workOrder.TargetCompletionDate.Value;

                Logger.Info("Completed WO '{OrderId}': total cost={Cost:C}, " +
                            "duration={Duration:F1}h, SLA={SLA}",
                    orderId, workOrder.TotalCost, laborHours,
                    slaCompliant ? "MET" : "MISSED");

                return workOrder;
            }
        }

        #endregion

        #region Escalation

        /// <summary>
        /// Identifies and escalates work orders that have exceeded their SLA target
        /// completion date by the specified number of days. Increases priority and
        /// logs escalation events.
        /// </summary>
        /// <param name="maxOverdueDays">Maximum days overdue before escalation triggers.</param>
        /// <returns>List of escalated work orders.</returns>
        public List<WorkOrder> EscalateOverdue(int maxOverdueDays = 3)
        {
            Logger.Info("Checking for overdue work orders (threshold: {Days} days)...", maxOverdueDays);

            var escalated = new List<WorkOrder>();
            DateTime now = DateTime.UtcNow;

            lock (_lockObject)
            {
                var overdueOrders = _workOrders.Values
                    .Where(wo =>
                        wo.Status != WorkOrderStatus.Completed &&
                        wo.Status != WorkOrderStatus.Cancelled &&
                        wo.Status != WorkOrderStatus.Closed &&
                        wo.TargetCompletionDate.HasValue &&
                        (now - wo.TargetCompletionDate.Value).TotalDays >= maxOverdueDays)
                    .ToList();

                foreach (var workOrder in overdueOrders)
                {
                    double daysOverdue = (now - workOrder.TargetCompletionDate.Value).TotalDays;

                    // Escalate priority (but not above Emergency)
                    var previousPriority = workOrder.Priority;
                    if (workOrder.Priority > WorkOrderPriority.Emergency)
                    {
                        workOrder.Priority = (WorkOrderPriority)Math.Max(0,
                            (int)workOrder.Priority - 1);
                    }

                    // Extend target date and mark escalation
                    double extensionHours = _slaCompletionHours.GetValueOrDefault(
                        workOrder.Priority, 168.0);
                    workOrder.TargetCompletionDate = now.AddHours(extensionHours);

                    workOrder.Notes.Add(
                        $"[{now:yyyy-MM-dd HH:mm}] ESCALATED: {daysOverdue:F0} days overdue. " +
                        $"Priority escalated from {previousPriority} to {workOrder.Priority}.");

                    workOrder.StatusHistory.Add(new StatusChange
                    {
                        FromStatus = workOrder.Status,
                        ToStatus = workOrder.Status,
                        Timestamp = now,
                        ChangedBy = "System",
                        Reason = $"Escalation: {daysOverdue:F0} days overdue, " +
                                 $"priority {previousPriority} -> {workOrder.Priority}"
                    });

                    escalated.Add(workOrder);

                    Logger.Warn("Escalated WO '{OrderId}': {Days:F0} days overdue, " +
                                "priority {From} -> {To}",
                        workOrder.OrderId, daysOverdue, previousPriority, workOrder.Priority);
                }
            }

            Logger.Info("Escalation check complete: {Count} work orders escalated.", escalated.Count);
            return escalated;
        }

        #endregion

        #region SLA Compliance

        /// <summary>
        /// Calculates SLA compliance metrics for completed work orders within a date range,
        /// broken down by priority level.
        /// </summary>
        /// <param name="periodStart">Start of the analysis period.</param>
        /// <param name="periodEnd">End of the analysis period.</param>
        /// <returns>Dictionary of priority -> (total, compliant, percentage) tuples.</returns>
        public Dictionary<WorkOrderPriority, (int Total, int Compliant, double Percent)>
            CalculateSLACompliance(DateTime periodStart, DateTime periodEnd)
        {
            Logger.Info("Calculating SLA compliance for {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                periodStart, periodEnd);

            var results = new Dictionary<WorkOrderPriority, (int, int, double)>();

            lock (_lockObject)
            {
                var completedInPeriod = _workOrders.Values
                    .Where(wo =>
                        (wo.Status == WorkOrderStatus.Completed ||
                         wo.Status == WorkOrderStatus.Closed) &&
                        wo.CompletedDate.HasValue &&
                        wo.CompletedDate.Value >= periodStart &&
                        wo.CompletedDate.Value <= periodEnd)
                    .ToList();

                foreach (WorkOrderPriority priority in Enum.GetValues<WorkOrderPriority>())
                {
                    var priorityOrders = completedInPeriod
                        .Where(wo => wo.Priority == priority)
                        .ToList();

                    int total = priorityOrders.Count;
                    int compliant = priorityOrders.Count(wo =>
                        wo.TargetCompletionDate.HasValue &&
                        wo.CompletedDate.HasValue &&
                        wo.CompletedDate.Value <= wo.TargetCompletionDate.Value);

                    double percent = total > 0 ? (double)compliant / total * 100.0 : 100.0;
                    results[priority] = (total, compliant, percent);
                }
            }

            foreach (var kvp in results.Where(r => r.Value.Item1 > 0))
            {
                Logger.Info("SLA {Priority}: {Compliant}/{Total} ({Percent:F1}%)",
                    kvp.Key, kvp.Value.Item2, kvp.Value.Item1, kvp.Value.Item3);
            }

            return results;
        }

        #endregion

        #region Backlog Reporting

        /// <summary>
        /// Generates a backlog aging report showing open work orders categorized by
        /// age (0-7 days, 8-14 days, 15-30 days, 31-60 days, 60+ days) and priority.
        /// </summary>
        /// <returns>
        /// Dictionary of aging bucket -> list of (priority, count, totalEstimatedHours) entries.
        /// </returns>
        public Dictionary<string, List<(WorkOrderPriority Priority, int Count, double EstimatedHours)>>
            GenerateBacklogReport()
        {
            Logger.Info("Generating backlog aging report...");

            var agingBuckets = new[] { "0-7 days", "8-14 days", "15-30 days", "31-60 days", "60+ days" };
            var report = agingBuckets.ToDictionary(
                b => b,
                _ => new List<(WorkOrderPriority Priority, int Count, double EstimatedHours)>(),
                StringComparer.OrdinalIgnoreCase);

            DateTime now = DateTime.UtcNow;

            lock (_lockObject)
            {
                var openOrders = _workOrders.Values
                    .Where(wo =>
                        wo.Status == WorkOrderStatus.Open ||
                        wo.Status == WorkOrderStatus.Assigned ||
                        wo.Status == WorkOrderStatus.InProgress ||
                        wo.Status == WorkOrderStatus.OnHold)
                    .ToList();

                foreach (var bucket in agingBuckets)
                {
                    var (minDays, maxDays) = ParseAgingBucket(bucket);

                    var bucketOrders = openOrders
                        .Where(wo =>
                        {
                            double ageDays = (now - wo.CreatedDate).TotalDays;
                            return ageDays >= minDays && ageDays < maxDays;
                        })
                        .ToList();

                    foreach (WorkOrderPriority priority in Enum.GetValues<WorkOrderPriority>())
                    {
                        var priorityOrders = bucketOrders
                            .Where(wo => wo.Priority == priority)
                            .ToList();

                        if (priorityOrders.Count > 0)
                        {
                            double estimatedHours = priorityOrders.Sum(wo =>
                                wo.LaborHours > 0 ? wo.LaborHours : EstimateDefaultHours(wo.Priority));

                            report[bucket].Add((priority, priorityOrders.Count, estimatedHours));
                        }
                    }
                }
            }

            // Log summary
            int totalOpen = report.Values.SelectMany(v => v).Sum(e => e.Item2);
            double totalHours = report.Values.SelectMany(v => v).Sum(e => e.Item3);
            Logger.Info("Backlog report: {Total} open work orders, {Hours:F0} estimated labor hours",
                totalOpen, totalHours);

            return report;
        }

        #endregion

        #region Labor Utilization

        /// <summary>
        /// Tracks labor utilization for a technician over a date range, computing
        /// productive hours, utilization percentage, work orders completed, and
        /// average completion time.
        /// </summary>
        /// <param name="technicianId">Technician identifier.</param>
        /// <param name="periodStart">Start of the analysis period.</param>
        /// <param name="periodEnd">End of the analysis period.</param>
        /// <returns>Dictionary of utilization metrics.</returns>
        public Dictionary<string, double> TrackLaborUtilization(
            string technicianId,
            DateTime periodStart,
            DateTime periodEnd)
        {
            if (string.IsNullOrEmpty(technicianId))
                throw new ArgumentException("Technician ID is required.", nameof(technicianId));

            Logger.Info("Tracking utilization for technician '{TechId}' from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                technicianId, periodStart, periodEnd);

            var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            lock (_lockObject)
            {
                if (!_technicians.TryGetValue(technicianId, out var technician))
                {
                    Logger.Warn("Technician '{TechId}' not found.", technicianId);
                    return metrics;
                }

                // Get all work orders completed by this technician in the period
                var completedOrders = _workOrders.Values
                    .Where(wo =>
                        wo.AssignedTo != null &&
                        wo.AssignedTo.Equals(technicianId, StringComparison.OrdinalIgnoreCase) &&
                        wo.Status == WorkOrderStatus.Completed &&
                        wo.CompletedDate.HasValue &&
                        wo.CompletedDate.Value >= periodStart &&
                        wo.CompletedDate.Value <= periodEnd)
                    .ToList();

                // Calculate available hours (8 hours/day, excluding weekends)
                int workDays = CountWorkDays(periodStart, periodEnd);
                double availableHours = workDays * 8.0;

                // Productive hours
                double productiveHours = completedOrders.Sum(wo => wo.LaborHours);

                // Utilization percentage
                double utilization = availableHours > 0
                    ? (productiveHours / availableHours) * 100.0
                    : 0;

                // Average completion time per work order
                double avgCompletionHours = completedOrders.Count > 0
                    ? completedOrders
                        .Where(wo => wo.StartedDate.HasValue)
                        .Select(wo => (wo.CompletedDate.Value - wo.StartedDate.Value).TotalHours)
                        .DefaultIfEmpty(0)
                        .Average()
                    : 0;

                // First-time fix rate (completed without re-opening)
                int firstTimeFix = completedOrders.Count(wo =>
                    wo.StatusHistory.Count(sc => sc.ToStatus == WorkOrderStatus.InProgress) <= 1);
                double firstTimeFixRate = completedOrders.Count > 0
                    ? (double)firstTimeFix / completedOrders.Count * 100.0
                    : 100.0;

                // Active work orders
                int activeCount = _workOrders.Values.Count(wo =>
                    wo.AssignedTo != null &&
                    wo.AssignedTo.Equals(technicianId, StringComparison.OrdinalIgnoreCase) &&
                    wo.Status != WorkOrderStatus.Completed &&
                    wo.Status != WorkOrderStatus.Cancelled &&
                    wo.Status != WorkOrderStatus.Closed);

                metrics["AvailableHours"] = availableHours;
                metrics["ProductiveHours"] = productiveHours;
                metrics["UtilizationPercent"] = Math.Min(utilization, 100.0);
                metrics["WorkOrdersCompleted"] = completedOrders.Count;
                metrics["ActiveWorkOrders"] = activeCount;
                metrics["AvgCompletionHours"] = avgCompletionHours;
                metrics["FirstTimeFixRatePercent"] = firstTimeFixRate;
                metrics["RevenueGenerated"] = (double)completedOrders.Sum(wo => wo.TotalCost);

                // Update the technician profile
                technician.UtilizationPercent = utilization;
                technician.ActiveWorkOrders = activeCount;
                technician.AverageCompletionRatePerDay = availableHours > 0
                    ? completedOrders.Count / (double)workDays
                    : 0;
            }

            Logger.Info("Technician '{TechId}' utilization: {Util:F1}%, " +
                        "{Completed} WOs completed, {Productive:F0}h productive",
                technicianId,
                metrics.GetValueOrDefault("UtilizationPercent"),
                metrics.GetValueOrDefault("WorkOrdersCompleted"),
                metrics.GetValueOrDefault("ProductiveHours"));

            return metrics;
        }

        #endregion

        #region Repeat Failure Tracking

        /// <summary>
        /// Identifies assets with repeat failures by analyzing work order history
        /// for corrective maintenance patterns. Returns assets sorted by failure frequency.
        /// </summary>
        /// <param name="lookbackDays">Number of days to look back for repeat failures.</param>
        /// <param name="minRepeatCount">Minimum number of corrective WOs to flag as repeat.</param>
        /// <returns>List of (assetId, failureCount, totalCost, primaryFailureMode) tuples.</returns>
        public List<(string AssetId, int FailureCount, decimal TotalCost, FailureMode PrimaryMode)>
            IdentifyRepeatFailures(int lookbackDays = 365, int minRepeatCount = 3)
        {
            Logger.Info("Identifying repeat failures (lookback={Days} days, minCount={Min})...",
                lookbackDays, minRepeatCount);

            DateTime cutoffDate = DateTime.UtcNow.AddDays(-lookbackDays);

            List<(string, int, decimal, FailureMode)> results;

            lock (_lockObject)
            {
                var correctiveOrders = _workOrders.Values
                    .Where(wo =>
                        wo.Type == MaintenanceType.Corrective &&
                        wo.CreatedDate >= cutoffDate)
                    .GroupBy(wo => wo.AssetId, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() >= minRepeatCount)
                    .Select(g =>
                    {
                        var orders = g.ToList();
                        int count = orders.Count;
                        decimal totalCost = orders.Sum(wo => wo.TotalCost);

                        // Find most common failure mode
                        var primaryMode = orders
                            .Where(wo => wo.IdentifiedFailureMode.HasValue)
                            .GroupBy(wo => wo.IdentifiedFailureMode.Value)
                            .OrderByDescending(fg => fg.Count())
                            .Select(fg => fg.Key)
                            .FirstOrDefault();

                        return (g.Key, count, totalCost, primaryMode);
                    })
                    .OrderByDescending(r => r.count)
                    .ToList();

                results = results = new List<(string, int, decimal, FailureMode)>(correctiveOrders);
            }

            Logger.Info("Found {Count} assets with repeat failures.", results.Count);
            return results;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Retrieves a work order by its identifier.
        /// </summary>
        public WorkOrder GetWorkOrder(string orderId)
        {
            lock (_lockObject)
            {
                return _workOrders.GetValueOrDefault(orderId);
            }
        }

        /// <summary>
        /// Retrieves all work orders for a specific asset.
        /// </summary>
        public List<WorkOrder> GetWorkOrdersByAsset(string assetId)
        {
            lock (_lockObject)
            {
                return _workOrders.Values
                    .Where(wo => wo.AssetId.Equals(assetId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(wo => wo.CreatedDate)
                    .ToList();
            }
        }

        /// <summary>
        /// Retrieves all work orders assigned to a specific technician.
        /// </summary>
        public List<WorkOrder> GetWorkOrdersByTechnician(string technicianId)
        {
            lock (_lockObject)
            {
                return _workOrders.Values
                    .Where(wo =>
                        wo.AssignedTo != null &&
                        wo.AssignedTo.Equals(technicianId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(wo => wo.CreatedDate)
                    .ToList();
            }
        }

        /// <summary>
        /// Retrieves all open work orders sorted by priority then age.
        /// </summary>
        public List<WorkOrder> GetOpenWorkOrders()
        {
            lock (_lockObject)
            {
                return _workOrders.Values
                    .Where(wo =>
                        wo.Status != WorkOrderStatus.Completed &&
                        wo.Status != WorkOrderStatus.Cancelled &&
                        wo.Status != WorkOrderStatus.Closed)
                    .OrderBy(wo => (int)wo.Priority)
                    .ThenBy(wo => wo.CreatedDate)
                    .ToList();
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Finds the best available technician based on skill matching, current workload,
        /// and completion rate. Uses a composite scoring approach.
        /// </summary>
        private TechnicianProfile FindBestTechnician(
            List<string> requiredSkills,
            WorkOrderPriority priority)
        {
            lock (_lockObject)
            {
                if (_technicians.Count == 0) return null;

                return _technicians.Values
                    .Where(t => t.IsAvailable &&
                                t.ActiveWorkOrders < t.MaxConcurrentWorkOrders)
                    .Select(t =>
                    {
                        // Skill match score (0-50 points)
                        double skillScore = 0;
                        if (requiredSkills.Count > 0)
                        {
                            int matched = requiredSkills.Count(rs =>
                                t.Skills.Any(ts =>
                                    ts.Equals(rs, StringComparison.OrdinalIgnoreCase)));
                            skillScore = (double)matched / requiredSkills.Count * 50.0;
                        }
                        else
                        {
                            skillScore = 25.0; // Neutral if no skills specified
                        }

                        // Load balance score (0-30 points): fewer active WOs = higher score
                        double loadScore = t.MaxConcurrentWorkOrders > 0
                            ? (1.0 - (double)t.ActiveWorkOrders / t.MaxConcurrentWorkOrders) * 30.0
                            : 30.0;

                        // Productivity score (0-20 points)
                        double productivityScore = Math.Min(20.0, t.AverageCompletionRatePerDay * 10.0);

                        return new { Technician = t, Score = skillScore + loadScore + productivityScore };
                    })
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Technician)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Calculates the optimal scheduled date for a work order based on technician
        /// workload and priority response times.
        /// </summary>
        private DateTime CalculateScheduledDate(TechnicianProfile technician, WorkOrderPriority priority)
        {
            double responseHours = _slaResponseHours.GetValueOrDefault(priority, 168.0);
            DateTime targetStart = DateTime.UtcNow.AddHours(responseHours * 0.5);

            // Delay based on current workload
            int currentLoad = technician.ActiveWorkOrders;
            if (currentLoad > 2)
            {
                targetStart = targetStart.AddDays(currentLoad - 2);
            }

            // Skip weekends
            while (targetStart.DayOfWeek == DayOfWeek.Saturday ||
                   targetStart.DayOfWeek == DayOfWeek.Sunday)
            {
                targetStart = targetStart.AddDays(1);
            }

            return targetStart;
        }

        private decimal EstimateWorkOrderCost(MaintenanceType type, WorkOrderPriority priority)
        {
            decimal baseCost = type switch
            {
                MaintenanceType.Preventive => 300m,
                MaintenanceType.Predictive => 500m,
                MaintenanceType.Corrective => 1500m,
                MaintenanceType.ConditionBased => 400m,
                MaintenanceType.CapitalRenewal => 10000m,
                MaintenanceType.Statutory => 350m,
                _ => 500m
            };

            // Emergency/urgent premium
            decimal priorityMultiplier = priority switch
            {
                WorkOrderPriority.Emergency => 2.0m,
                WorkOrderPriority.Urgent => 1.5m,
                _ => 1.0m
            };

            return baseCost * priorityMultiplier;
        }

        private bool CheckWarrantyCoverage(string assetId)
        {
            // Simplified warranty check: assets less than 2 years old are under warranty
            lock (_lockObject)
            {
                // No warranty data structure yet; return false by default
                // In a full implementation, this would check a warranty database
                return false;
            }
        }

        private void ReleaseTechnicianWorkload(string technicianId)
        {
            if (string.IsNullOrEmpty(technicianId)) return;

            lock (_lockObject)
            {
                if (_technicians.TryGetValue(technicianId, out var tech))
                {
                    tech.ActiveWorkOrders = Math.Max(0, tech.ActiveWorkOrders - 1);
                }
            }
        }

        private static (int Min, int Max) ParseAgingBucket(string bucket)
        {
            return bucket switch
            {
                "0-7 days" => (0, 8),
                "8-14 days" => (8, 15),
                "15-30 days" => (15, 31),
                "31-60 days" => (31, 61),
                "60+ days" => (61, int.MaxValue),
                _ => (0, int.MaxValue)
            };
        }

        private static double EstimateDefaultHours(WorkOrderPriority priority)
        {
            return priority switch
            {
                WorkOrderPriority.Emergency => 4.0,
                WorkOrderPriority.Urgent => 4.0,
                WorkOrderPriority.High => 3.0,
                WorkOrderPriority.Medium => 2.0,
                WorkOrderPriority.Low => 1.5,
                _ => 2.0
            };
        }

        private static int CountWorkDays(DateTime start, DateTime end)
        {
            int totalDays = (int)(end - start).TotalDays;
            int workDays = 0;
            for (int i = 0; i < totalDays; i++)
            {
                var day = start.AddDays(i).DayOfWeek;
                if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
                    workDays++;
            }
            return Math.Max(1, workDays);
        }

        #endregion
    }
}
