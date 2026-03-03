using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Automation.FacilityManagement
{
    /// <summary>
    /// Comprehensive work order management for facility maintenance operations.
    /// Handles creation, assignment, tracking, and completion of work orders.
    /// </summary>
    public class WorkOrderManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly WorkOrderSettings _settings;
        private readonly WorkOrderRepository _repository;
        private readonly WorkflowEngine _workflowEngine;
        private readonly NotificationService _notificationService;
        private readonly CostTracker _costTracker;

        public WorkOrderManager(WorkOrderSettings settings = null)
        {
            _settings = settings ?? new WorkOrderSettings();
            _repository = new WorkOrderRepository();
            _workflowEngine = new WorkflowEngine(_settings);
            _notificationService = new NotificationService();
            _costTracker = new CostTracker();
        }

        #region Work Order Creation

        /// <summary>
        /// Create a new work order.
        /// </summary>
        public async Task<WorkOrderResult> CreateWorkOrderAsync(
            WorkOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Creating work order: {request.Title}");

            var workOrder = new WorkOrder
            {
                Id = GenerateWorkOrderId(request.Type),
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                Priority = request.Priority,
                Category = request.Category,
                AssetId = request.AssetId,
                Location = request.Location,
                RequestedBy = request.RequestedBy,
                RequestedDate = DateTime.UtcNow,
                DueDate = request.DueDate ?? CalculateDueDate(request.Priority),
                Status = WorkOrderStatus.Open,
                EstimatedHours = request.EstimatedHours,
                EstimatedCost = request.EstimatedCost,
                Tags = request.Tags ?? new List<string>()
            };

            // Auto-assign based on category/skills if enabled
            if (_settings.AutoAssign && string.IsNullOrEmpty(request.AssignedTo))
            {
                var assignee = await FindBestAssigneeAsync(workOrder, cancellationToken);
                if (assignee != null)
                {
                    workOrder.AssignedTo = assignee.Id;
                    workOrder.AssignedDate = DateTime.UtcNow;
                    workOrder.Status = WorkOrderStatus.Assigned;
                }
            }
            else if (!string.IsNullOrEmpty(request.AssignedTo))
            {
                workOrder.AssignedTo = request.AssignedTo;
                workOrder.AssignedDate = DateTime.UtcNow;
                workOrder.Status = WorkOrderStatus.Assigned;
            }

            // Link to parent work order if provided
            if (!string.IsNullOrEmpty(request.ParentWorkOrderId))
            {
                workOrder.ParentWorkOrderId = request.ParentWorkOrderId;
            }

            // Add attachments
            if (request.Attachments?.Any() == true)
            {
                workOrder.Attachments.AddRange(request.Attachments);
            }

            // Apply workflow
            await _workflowEngine.InitializeWorkflowAsync(workOrder, cancellationToken);

            await _repository.AddWorkOrderAsync(workOrder, cancellationToken);

            // Send notifications
            await _notificationService.NotifyWorkOrderCreatedAsync(workOrder, cancellationToken);

            return new WorkOrderResult
            {
                Success = true,
                WorkOrder = workOrder,
                WorkOrderId = workOrder.Id
            };
        }

        /// <summary>
        /// Create work order from maintenance schedule.
        /// </summary>
        public async Task<WorkOrderResult> CreatePreventiveMaintenanceOrderAsync(
            PreventiveMaintenanceTask pmTask,
            CancellationToken cancellationToken = default)
        {
            var request = new WorkOrderRequest
            {
                Title = $"PM: {pmTask.TaskName}",
                Description = pmTask.Description,
                Type = WorkOrderType.PreventiveMaintenance,
                Priority = WorkOrderPriority.Medium,
                Category = pmTask.Category,
                AssetId = pmTask.AssetId,
                Location = pmTask.Location,
                EstimatedHours = pmTask.EstimatedHours,
                DueDate = pmTask.ScheduledDate,
                Tags = new List<string> { "PM", pmTask.Frequency.ToString() }
            };

            var result = await CreateWorkOrderAsync(request, cancellationToken);

            if (result.Success)
            {
                result.WorkOrder.PreventiveMaintenanceId = pmTask.Id;
                result.WorkOrder.Checklist = pmTask.Checklist;
                await _repository.UpdateWorkOrderAsync(result.WorkOrder, cancellationToken);
            }

            return result;
        }

        /// <summary>
        /// Create emergency work order with high priority.
        /// </summary>
        public async Task<WorkOrderResult> CreateEmergencyOrderAsync(
            EmergencyRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Warn($"Creating EMERGENCY work order: {request.Title}");

            var woRequest = new WorkOrderRequest
            {
                Title = $"[EMERGENCY] {request.Title}",
                Description = request.Description,
                Type = WorkOrderType.Emergency,
                Priority = WorkOrderPriority.Emergency,
                Category = request.Category,
                AssetId = request.AssetId,
                Location = request.Location,
                RequestedBy = request.ReportedBy
            };

            var result = await CreateWorkOrderAsync(woRequest, cancellationToken);

            if (result.Success)
            {
                // Immediate notification to all on-call personnel
                await _notificationService.NotifyEmergencyAsync(result.WorkOrder, cancellationToken);
            }

            return result;
        }

        #endregion

        #region Work Order Management

        /// <summary>
        /// Assign work order to technician.
        /// </summary>
        public async Task<bool> AssignWorkOrderAsync(
            string workOrderId,
            string technicianId,
            string notes = null,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            workOrder.AssignedTo = technicianId;
            workOrder.AssignedDate = DateTime.UtcNow;
            workOrder.Status = WorkOrderStatus.Assigned;

            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Assigned",
                UserId = _settings.CurrentUserId,
                Details = $"Assigned to {technicianId}. {notes}"
            });

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            await _notificationService.NotifyAssignmentAsync(workOrder, technicianId, cancellationToken);

            return true;
        }

        /// <summary>
        /// Start work on a work order.
        /// </summary>
        public async Task<bool> StartWorkOrderAsync(
            string workOrderId,
            string notes = null,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            workOrder.Status = WorkOrderStatus.InProgress;
            workOrder.StartedDate = DateTime.UtcNow;

            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Started",
                UserId = _settings.CurrentUserId,
                Details = notes
            });

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        /// <summary>
        /// Put work order on hold.
        /// </summary>
        public async Task<bool> HoldWorkOrderAsync(
            string workOrderId,
            string reason,
            DateTime? expectedResumeDate = null,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            workOrder.PreviousStatus = workOrder.Status;
            workOrder.Status = WorkOrderStatus.OnHold;
            workOrder.HoldReason = reason;
            workOrder.ExpectedResumeDate = expectedResumeDate;

            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Put on Hold",
                UserId = _settings.CurrentUserId,
                Details = reason
            });

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        /// <summary>
        /// Resume work order from hold.
        /// </summary>
        public async Task<bool> ResumeWorkOrderAsync(
            string workOrderId,
            string notes = null,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null || workOrder.Status != WorkOrderStatus.OnHold) return false;

            workOrder.Status = workOrder.PreviousStatus ?? WorkOrderStatus.InProgress;
            workOrder.HoldReason = null;
            workOrder.ExpectedResumeDate = null;

            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Resumed",
                UserId = _settings.CurrentUserId,
                Details = notes
            });

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        /// <summary>
        /// Complete a work order.
        /// </summary>
        public async Task<WorkOrderCompletionResult> CompleteWorkOrderAsync(
            string workOrderId,
            WorkOrderCompletion completion,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null)
                return new WorkOrderCompletionResult { Success = false, Error = "Work order not found" };

            workOrder.Status = WorkOrderStatus.Completed;
            workOrder.CompletedDate = DateTime.UtcNow;
            workOrder.CompletedBy = completion.CompletedBy ?? _settings.CurrentUserId;
            workOrder.ActualHours = completion.ActualHours;
            workOrder.ActualCost = completion.ActualCost;
            workOrder.CompletionNotes = completion.Notes;
            workOrder.ResolutionCode = completion.ResolutionCode;

            // Add labor entries
            if (completion.LaborEntries?.Any() == true)
            {
                workOrder.LaborEntries.AddRange(completion.LaborEntries);
            }

            // Add parts used
            if (completion.PartsUsed?.Any() == true)
            {
                workOrder.PartsUsed.AddRange(completion.PartsUsed);
            }

            // Checklist completion
            if (completion.ChecklistResults?.Any() == true)
            {
                workOrder.ChecklistResults = completion.ChecklistResults;
            }

            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Completed",
                UserId = _settings.CurrentUserId,
                Details = completion.Notes
            });

            // Calculate final cost
            workOrder.ActualCost = _costTracker.CalculateTotalCost(workOrder);

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            await _notificationService.NotifyCompletionAsync(workOrder, cancellationToken);

            // Update asset if applicable
            if (!string.IsNullOrEmpty(workOrder.AssetId))
            {
                await UpdateAssetMaintenanceHistoryAsync(workOrder, cancellationToken);
            }

            return new WorkOrderCompletionResult
            {
                Success = true,
                WorkOrder = workOrder,
                TotalCost = workOrder.ActualCost ?? 0
            };
        }

        /// <summary>
        /// Cancel a work order.
        /// </summary>
        public async Task<bool> CancelWorkOrderAsync(
            string workOrderId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            workOrder.Status = WorkOrderStatus.Cancelled;
            workOrder.CancellationReason = reason;

            workOrder.History.Add(new WorkOrderHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Cancelled",
                UserId = _settings.CurrentUserId,
                Details = reason
            });

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        #endregion

        #region Work Order Queries

        /// <summary>
        /// Get work order by ID.
        /// </summary>
        public async Task<WorkOrder> GetWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default)
        {
            return await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
        }

        /// <summary>
        /// Search work orders with filters.
        /// </summary>
        public async Task<WorkOrderSearchResult> SearchWorkOrdersAsync(
            WorkOrderSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            return await _repository.SearchWorkOrdersAsync(criteria, cancellationToken);
        }

        /// <summary>
        /// Get work orders assigned to a technician.
        /// </summary>
        public async Task<List<WorkOrder>> GetTechnicianWorkOrdersAsync(
            string technicianId,
            bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var criteria = new WorkOrderSearchCriteria
            {
                AssignedTo = technicianId,
                ActiveOnly = activeOnly
            };

            var result = await _repository.SearchWorkOrdersAsync(criteria, cancellationToken);
            return result.WorkOrders;
        }

        /// <summary>
        /// Get work orders for an asset.
        /// </summary>
        public async Task<List<WorkOrder>> GetAssetWorkOrdersAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            var criteria = new WorkOrderSearchCriteria { AssetId = assetId };
            var result = await _repository.SearchWorkOrdersAsync(criteria, cancellationToken);
            return result.WorkOrders;
        }

        /// <summary>
        /// Get overdue work orders.
        /// </summary>
        public async Task<List<WorkOrder>> GetOverdueWorkOrdersAsync(CancellationToken cancellationToken = default)
        {
            var criteria = new WorkOrderSearchCriteria
            {
                ActiveOnly = true,
                OverdueOnly = true
            };

            var result = await _repository.SearchWorkOrdersAsync(criteria, cancellationToken);
            return result.WorkOrders;
        }

        /// <summary>
        /// Get work order dashboard summary.
        /// </summary>
        public async Task<WorkOrderDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
        {
            var allOrders = await _repository.GetAllWorkOrdersAsync(cancellationToken);
            var now = DateTime.UtcNow;

            return new WorkOrderDashboard
            {
                TotalOpen = allOrders.Count(w => w.Status == WorkOrderStatus.Open),
                TotalAssigned = allOrders.Count(w => w.Status == WorkOrderStatus.Assigned),
                TotalInProgress = allOrders.Count(w => w.Status == WorkOrderStatus.InProgress),
                TotalOnHold = allOrders.Count(w => w.Status == WorkOrderStatus.OnHold),
                TotalCompletedToday = allOrders.Count(w =>
                    w.Status == WorkOrderStatus.Completed && w.CompletedDate?.Date == now.Date),
                TotalCompletedThisWeek = allOrders.Count(w =>
                    w.Status == WorkOrderStatus.Completed &&
                    w.CompletedDate >= now.AddDays(-7)),
                TotalOverdue = allOrders.Count(w =>
                    IsActive(w.Status) && w.DueDate < now),
                ByPriority = allOrders.Where(w => IsActive(w.Status))
                    .GroupBy(w => w.Priority)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByCategory = allOrders.Where(w => IsActive(w.Status))
                    .GroupBy(w => w.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EmergencyCount = allOrders.Count(w =>
                    IsActive(w.Status) && w.Priority == WorkOrderPriority.Emergency)
            };
        }

        #endregion

        #region Time & Labor

        /// <summary>
        /// Log labor time for a work order.
        /// </summary>
        public async Task<bool> LogLaborAsync(
            string workOrderId,
            LaborEntry laborEntry,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            laborEntry.Id = Guid.NewGuid().ToString();
            laborEntry.EnteredDate = DateTime.UtcNow;
            workOrder.LaborEntries.Add(laborEntry);

            // Update actual hours
            workOrder.ActualHours = workOrder.LaborEntries.Sum(l => l.Hours);

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        /// <summary>
        /// Log parts/materials used.
        /// </summary>
        public async Task<bool> LogPartsUsedAsync(
            string workOrderId,
            PartUsage partUsage,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            partUsage.Id = Guid.NewGuid().ToString();
            partUsage.UsedDate = DateTime.UtcNow;
            workOrder.PartsUsed.Add(partUsage);

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        #endregion

        #region Comments & Attachments

        /// <summary>
        /// Add comment to work order.
        /// </summary>
        public async Task<bool> AddCommentAsync(
            string workOrderId,
            string comment,
            bool isInternal = false,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            workOrder.Comments.Add(new WorkOrderComment
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                UserId = _settings.CurrentUserId,
                Text = comment,
                IsInternal = isInternal
            });

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        /// <summary>
        /// Add attachment to work order.
        /// </summary>
        public async Task<bool> AddAttachmentAsync(
            string workOrderId,
            Attachment attachment,
            CancellationToken cancellationToken = default)
        {
            var workOrder = await _repository.GetWorkOrderAsync(workOrderId, cancellationToken);
            if (workOrder == null) return false;

            attachment.Id = Guid.NewGuid().ToString();
            attachment.UploadedDate = DateTime.UtcNow;
            attachment.UploadedBy = _settings.CurrentUserId;
            workOrder.Attachments.Add(attachment);

            await _repository.UpdateWorkOrderAsync(workOrder, cancellationToken);
            return true;
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate work order report.
        /// </summary>
        public async Task<WorkOrderReport> GenerateReportAsync(
            WorkOrderReportOptions options,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating work order report: {options.ReportType}");

            var criteria = new WorkOrderSearchCriteria
            {
                DateFrom = options.DateFrom,
                DateTo = options.DateTo,
                Category = options.Category
            };

            var searchResult = await _repository.SearchWorkOrdersAsync(criteria, cancellationToken);
            var orders = searchResult.WorkOrders;

            var report = new WorkOrderReport
            {
                GeneratedDate = DateTime.UtcNow,
                ReportType = options.ReportType,
                DateRange = $"{options.DateFrom:d} - {options.DateTo:d}"
            };

            report.Summary = new WorkOrderReportSummary
            {
                TotalWorkOrders = orders.Count,
                Completed = orders.Count(w => w.Status == WorkOrderStatus.Completed),
                Cancelled = orders.Count(w => w.Status == WorkOrderStatus.Cancelled),
                AverageCompletionDays = orders
                    .Where(w => w.CompletedDate.HasValue && w.Status == WorkOrderStatus.Completed)
                    .Select(w => (w.CompletedDate.Value - w.RequestedDate).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average(),
                TotalLaborHours = orders.Sum(w => w.ActualHours ?? 0),
                TotalCost = orders.Sum(w => w.ActualCost ?? 0),
                OnTimeCompletionRate = CalculateOnTimeRate(orders)
            };

            report.ByPriority = orders.GroupBy(w => w.Priority)
                .Select(g => new PrioritySummary
                {
                    Priority = g.Key,
                    Count = g.Count(),
                    AverageResponseTime = g.Where(w => w.StartedDate.HasValue)
                        .Select(w => (w.StartedDate.Value - w.RequestedDate).TotalHours)
                        .DefaultIfEmpty(0)
                        .Average()
                }).ToList();

            report.ByCategory = orders.GroupBy(w => w.Category)
                .Select(g => new CategorySummary
                {
                    Category = g.Key,
                    Count = g.Count(),
                    TotalCost = g.Sum(w => w.ActualCost ?? 0)
                }).ToList();

            return report;
        }

        /// <summary>
        /// Get technician performance metrics.
        /// </summary>
        public async Task<TechnicianMetrics> GetTechnicianMetricsAsync(
            string technicianId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var criteria = new WorkOrderSearchCriteria
            {
                AssignedTo = technicianId,
                DateFrom = fromDate,
                DateTo = toDate
            };

            var result = await _repository.SearchWorkOrdersAsync(criteria, cancellationToken);
            var orders = result.WorkOrders;

            return new TechnicianMetrics
            {
                TechnicianId = technicianId,
                Period = $"{fromDate:d} - {toDate:d}",
                TotalAssigned = orders.Count,
                Completed = orders.Count(w => w.Status == WorkOrderStatus.Completed),
                AverageCompletionTime = orders
                    .Where(w => w.CompletedDate.HasValue)
                    .Select(w => (w.CompletedDate.Value - (w.StartedDate ?? w.AssignedDate ?? w.RequestedDate)).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average(),
                TotalLaborHours = orders.Sum(w => w.ActualHours ?? 0),
                OnTimeCompletionRate = CalculateOnTimeRate(orders),
                FirstTimeFixRate = CalculateFirstTimeFixRate(orders)
            };
        }

        #endregion

        #region Private Methods

        private string GenerateWorkOrderId(WorkOrderType type)
        {
            var prefix = type switch
            {
                WorkOrderType.Corrective => "CM",
                WorkOrderType.PreventiveMaintenance => "PM",
                WorkOrderType.Emergency => "EM",
                WorkOrderType.Inspection => "IN",
                WorkOrderType.Project => "PR",
                _ => "WO"
            };

            return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        }

        private DateTime CalculateDueDate(WorkOrderPriority priority)
        {
            var hours = priority switch
            {
                WorkOrderPriority.Emergency => 4,
                WorkOrderPriority.High => 24,
                WorkOrderPriority.Medium => 72,
                WorkOrderPriority.Low => 168, // 1 week
                _ => 72
            };

            return DateTime.UtcNow.AddHours(hours);
        }

        private async Task<Technician> FindBestAssigneeAsync(WorkOrder workOrder, CancellationToken ct)
        {
            // Would integrate with technician/skills database
            return await Task.FromResult<Technician>(null);
        }

        private bool IsActive(WorkOrderStatus status)
        {
            return status == WorkOrderStatus.Open ||
                   status == WorkOrderStatus.Assigned ||
                   status == WorkOrderStatus.InProgress ||
                   status == WorkOrderStatus.OnHold;
        }

        private double CalculateOnTimeRate(List<WorkOrder> orders)
        {
            var completed = orders.Where(w => w.Status == WorkOrderStatus.Completed && w.CompletedDate.HasValue).ToList();
            if (!completed.Any()) return 100;

            var onTime = completed.Count(w => w.CompletedDate <= w.DueDate);
            return (onTime * 100.0) / completed.Count;
        }

        private double CalculateFirstTimeFixRate(List<WorkOrder> orders)
        {
            var completed = orders.Where(w => w.Status == WorkOrderStatus.Completed).ToList();
            if (!completed.Any()) return 100;

            // Work orders that didn't have follow-up work orders for same asset within 30 days
            var firstTimeFix = completed.Count(w =>
                !orders.Any(o => o.AssetId == w.AssetId &&
                                  o.Id != w.Id &&
                                  o.RequestedDate > w.CompletedDate &&
                                  o.RequestedDate <= w.CompletedDate.Value.AddDays(30)));

            return (firstTimeFix * 100.0) / completed.Count;
        }

        private async Task UpdateAssetMaintenanceHistoryAsync(WorkOrder workOrder, CancellationToken ct)
        {
            // Would integrate with AssetManagementEngine
            await Task.CompletedTask;
        }

        #endregion
    }

    #region Supporting Classes

    internal class WorkOrderRepository
    {
        private readonly List<WorkOrder> _workOrders = new();

        public Task AddWorkOrderAsync(WorkOrder wo, CancellationToken ct)
        {
            _workOrders.Add(wo);
            return Task.CompletedTask;
        }

        public Task UpdateWorkOrderAsync(WorkOrder wo, CancellationToken ct)
        {
            var index = _workOrders.FindIndex(w => w.Id == wo.Id);
            if (index >= 0) _workOrders[index] = wo;
            return Task.CompletedTask;
        }

        public Task<WorkOrder> GetWorkOrderAsync(string id, CancellationToken ct)
        {
            return Task.FromResult(_workOrders.FirstOrDefault(w => w.Id == id));
        }

        public Task<List<WorkOrder>> GetAllWorkOrdersAsync(CancellationToken ct)
        {
            return Task.FromResult(_workOrders.ToList());
        }

        public Task<WorkOrderSearchResult> SearchWorkOrdersAsync(WorkOrderSearchCriteria criteria, CancellationToken ct)
        {
            var query = _workOrders.AsQueryable();

            if (!string.IsNullOrEmpty(criteria.AssignedTo))
                query = query.Where(w => w.AssignedTo == criteria.AssignedTo);
            if (!string.IsNullOrEmpty(criteria.AssetId))
                query = query.Where(w => w.AssetId == criteria.AssetId);
            if (criteria.Status.HasValue)
                query = query.Where(w => w.Status == criteria.Status.Value);
            if (criteria.Priority.HasValue)
                query = query.Where(w => w.Priority == criteria.Priority.Value);
            if (!string.IsNullOrEmpty(criteria.Category))
                query = query.Where(w => w.Category == criteria.Category);
            if (criteria.DateFrom.HasValue)
                query = query.Where(w => w.RequestedDate >= criteria.DateFrom.Value);
            if (criteria.DateTo.HasValue)
                query = query.Where(w => w.RequestedDate <= criteria.DateTo.Value);
            if (criteria.ActiveOnly)
                query = query.Where(w => w.Status != WorkOrderStatus.Completed &&
                                          w.Status != WorkOrderStatus.Cancelled);
            if (criteria.OverdueOnly)
                query = query.Where(w => w.DueDate < DateTime.UtcNow);

            return Task.FromResult(new WorkOrderSearchResult { WorkOrders = query.ToList() });
        }
    }

    internal class WorkflowEngine
    {
        private readonly WorkOrderSettings _settings;
        public WorkflowEngine(WorkOrderSettings settings) => _settings = settings;

        public Task InitializeWorkflowAsync(WorkOrder wo, CancellationToken ct)
        {
            wo.WorkflowState = "Initial";
            return Task.CompletedTask;
        }
    }

    internal class NotificationService
    {
        public Task NotifyWorkOrderCreatedAsync(WorkOrder wo, CancellationToken ct) => Task.CompletedTask;
        public Task NotifyAssignmentAsync(WorkOrder wo, string techId, CancellationToken ct) => Task.CompletedTask;
        public Task NotifyCompletionAsync(WorkOrder wo, CancellationToken ct) => Task.CompletedTask;
        public Task NotifyEmergencyAsync(WorkOrder wo, CancellationToken ct) => Task.CompletedTask;
    }

    internal class CostTracker
    {
        public decimal CalculateTotalCost(WorkOrder wo)
        {
            var laborCost = wo.LaborEntries.Sum(l => { decimal hours = (decimal)l.Hours; return hours * l.HourlyRate; });
            var partsCost = wo.PartsUsed.Sum(p => (decimal)p.Quantity * p.UnitCost);
            return laborCost + partsCost;
        }
    }

    #endregion

    #region Data Models

    public class WorkOrderSettings
    {
        public string CurrentUserId { get; set; } = "System";
        public bool AutoAssign { get; set; } = true;
    }

    public class WorkOrder
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public WorkOrderType Type { get; set; }
        public WorkOrderPriority Priority { get; set; }
        public string Category { get; set; }
        public string AssetId { get; set; }
        public AssetLocation Location { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime DueDate { get; set; }
        public WorkOrderStatus Status { get; set; }
        public WorkOrderStatus? PreviousStatus { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string CompletedBy { get; set; }
        public double? EstimatedHours { get; set; }
        public double? ActualHours { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public string CompletionNotes { get; set; }
        public string ResolutionCode { get; set; }
        public string HoldReason { get; set; }
        public DateTime? ExpectedResumeDate { get; set; }
        public string CancellationReason { get; set; }
        public string ParentWorkOrderId { get; set; }
        public string PreventiveMaintenanceId { get; set; }
        public string WorkflowState { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ChecklistItem> Checklist { get; set; } = new();
        public List<ChecklistResult> ChecklistResults { get; set; } = new();
        public List<LaborEntry> LaborEntries { get; } = new();
        public List<PartUsage> PartsUsed { get; } = new();
        public List<WorkOrderComment> Comments { get; } = new();
        public List<Attachment> Attachments { get; } = new();
        public List<WorkOrderHistoryEntry> History { get; } = new();
    }

    public class WorkOrderRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public WorkOrderType Type { get; set; }
        public WorkOrderPriority Priority { get; set; }
        public string Category { get; set; }
        public string AssetId { get; set; }
        public AssetLocation Location { get; set; }
        public string RequestedBy { get; set; }
        public DateTime? DueDate { get; set; }
        public double? EstimatedHours { get; set; }
        public decimal? EstimatedCost { get; set; }
        public string AssignedTo { get; set; }
        public string ParentWorkOrderId { get; set; }
        public List<string> Tags { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class WorkOrderResult
    {
        public bool Success { get; set; }
        public WorkOrder WorkOrder { get; set; }
        public string WorkOrderId { get; set; }
        public string Error { get; set; }
    }

    public class WorkOrderCompletion
    {
        public string CompletedBy { get; set; }
        public double? ActualHours { get; set; }
        public decimal? ActualCost { get; set; }
        public string Notes { get; set; }
        public string ResolutionCode { get; set; }
        public List<LaborEntry> LaborEntries { get; set; }
        public List<PartUsage> PartsUsed { get; set; }
        public List<ChecklistResult> ChecklistResults { get; set; }
    }

    public class WorkOrderCompletionResult
    {
        public bool Success { get; set; }
        public WorkOrder WorkOrder { get; set; }
        public decimal TotalCost { get; set; }
        public string Error { get; set; }
    }

    public class EmergencyRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string AssetId { get; set; }
        public AssetLocation Location { get; set; }
        public string ReportedBy { get; set; }
    }

    public class PreventiveMaintenanceTask
    {
        public string Id { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string AssetId { get; set; }
        public AssetLocation Location { get; set; }
        public double EstimatedHours { get; set; }
        public DateTime ScheduledDate { get; set; }
        public PMFrequency Frequency { get; set; }
        public List<ChecklistItem> Checklist { get; set; }
    }

    public class ChecklistItem
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
    }

    public class ChecklistResult
    {
        public string ChecklistItemId { get; set; }
        public bool Completed { get; set; }
        public string Notes { get; set; }
    }

    public class LaborEntry
    {
        public string Id { get; set; }
        public string TechnicianId { get; set; }
        public DateTime WorkDate { get; set; }
        public double Hours { get; set; }
        public decimal HourlyRate { get; set; }
        public string Description { get; set; }
        public DateTime EnteredDate { get; set; }
    }

    public class PartUsage
    {
        public string Id { get; set; }
        public string PartNumber { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public DateTime UsedDate { get; set; }
    }

    public class WorkOrderComment
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string Text { get; set; }
        public bool IsInternal { get; set; }
    }

    public class Attachment
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; }
    }

    public class WorkOrderHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string UserId { get; set; }
        public string Details { get; set; }
    }

    public class WorkOrderSearchCriteria
    {
        public string AssignedTo { get; set; }
        public string AssetId { get; set; }
        public WorkOrderStatus? Status { get; set; }
        public WorkOrderPriority? Priority { get; set; }
        public string Category { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool ActiveOnly { get; set; }
        public bool OverdueOnly { get; set; }
    }

    public class WorkOrderSearchResult
    {
        public List<WorkOrder> WorkOrders { get; set; } = new();
        public int TotalCount => WorkOrders.Count;
    }

    public class WorkOrderDashboard
    {
        public int TotalOpen { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalInProgress { get; set; }
        public int TotalOnHold { get; set; }
        public int TotalCompletedToday { get; set; }
        public int TotalCompletedThisWeek { get; set; }
        public int TotalOverdue { get; set; }
        public int EmergencyCount { get; set; }
        public Dictionary<WorkOrderPriority, int> ByPriority { get; set; }
        public Dictionary<string, int> ByCategory { get; set; }
    }

    public class WorkOrderReport
    {
        public DateTime GeneratedDate { get; set; }
        public WorkOrderReportType ReportType { get; set; }
        public string DateRange { get; set; }
        public WorkOrderReportSummary Summary { get; set; }
        public List<PrioritySummary> ByPriority { get; set; }
        public List<CategorySummary> ByCategory { get; set; }
    }

    public class WorkOrderReportSummary
    {
        public int TotalWorkOrders { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public double AverageCompletionDays { get; set; }
        public double TotalLaborHours { get; set; }
        public decimal TotalCost { get; set; }
        public double OnTimeCompletionRate { get; set; }
    }

    public class PrioritySummary
    {
        public WorkOrderPriority Priority { get; set; }
        public int Count { get; set; }
        public double AverageResponseTime { get; set; }
    }

    public class CategorySummary
    {
        public string Category { get; set; }
        public int Count { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class WorkOrderReportOptions
    {
        public WorkOrderReportType ReportType { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string Category { get; set; }
    }

    public class TechnicianMetrics
    {
        public string TechnicianId { get; set; }
        public string Period { get; set; }
        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public double AverageCompletionTime { get; set; }
        public double TotalLaborHours { get; set; }
        public double OnTimeCompletionRate { get; set; }
        public double FirstTimeFixRate { get; set; }
    }

    public class Technician
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Skills { get; set; }
    }

    // Enums
    public enum WorkOrderType { Corrective, PreventiveMaintenance, Emergency, Inspection, Project, Request }
    public enum WorkOrderPriority { Low, Medium, High, Emergency }
    public enum WorkOrderStatus { Open, Assigned, InProgress, OnHold, Completed, Cancelled }
    public enum WorkOrderReportType { Summary, Detailed, Technician, Cost, Performance }
    public enum PMFrequency { Daily, Weekly, BiWeekly, Monthly, Quarterly, SemiAnnual, Annual }

    #endregion
}
