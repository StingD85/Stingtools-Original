// -----------------------------------------------------------------------
// StingBIM.AI.Collaboration - Workflow Automation Engine
// Advanced workflow orchestration with visual designer support
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Collaboration.Workflow
{
    #region Enums

    public enum WorkflowType
    {
        Approval,
        Review,
        Submittal,
        RFI,
        ChangeOrder,
        Inspection,
        PunchList,
        Closeout,
        Onboarding,
        Custom
    }

    public enum WorkflowStatus
    {
        Draft,
        Active,
        Paused,
        Completed,
        Cancelled,
        Failed
    }

    public enum StepType
    {
        Task,
        Approval,
        Notification,
        Condition,
        Parallel,
        Loop,
        Delay,
        Integration,
        SubWorkflow
    }

    public enum StepStatus
    {
        Pending,
        InProgress,
        Completed,
        Skipped,
        Failed,
        Rejected,
        Cancelled
    }

    public enum TriggerType
    {
        Manual,
        Scheduled,
        EventBased,
        Conditional,
        Webhook
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected,
        ChangesRequested,
        Delegated,
        Escalated
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        Contains,
        StartsWith,
        EndsWith,
        IsNull,
        IsNotNull,
        In,
        NotIn,
        Matches
    }

    #endregion

    #region Data Models

    public class WorkflowDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public WorkflowType Type { get; set; }
        public string Version { get; set; } = "1.0";
        public bool IsTemplate { get; set; }
        public bool IsActive { get; set; } = true;
        public List<WorkflowStep> Steps { get; set; } = new();
        public List<WorkflowTransition> Transitions { get; set; } = new();
        public List<WorkflowTrigger> Triggers { get; set; } = new();
        public List<WorkflowVariable> Variables { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; set; }
        public TimeSpan? DefaultSLA { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class WorkflowStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public StepType Type { get; set; }
        public int Order { get; set; }
        public List<string> AssigneeRoles { get; set; } = new();
        public List<string> AssigneeUsers { get; set; } = new();
        public TimeSpan? DueOffset { get; set; }
        public TimeSpan? SLA { get; set; }
        public List<WorkflowAction> EntryActions { get; set; } = new();
        public List<WorkflowAction> ExitActions { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<FormField> FormFields { get; set; } = new();
        public bool IsRequired { get; set; } = true;
        public bool AllowSkip { get; set; }
        public bool AllowReassign { get; set; } = true;
        public int? RequiredApprovals { get; set; }
        public bool RequireAllApprovers { get; set; }

        // Visual designer properties
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    public class WorkflowTransition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromStepId { get; set; }
        public string ToStepId { get; set; }
        public string Name { get; set; }
        public List<TransitionCondition> Conditions { get; set; } = new();
        public List<WorkflowAction> Actions { get; set; } = new();
        public int Priority { get; set; }
        public bool IsDefault { get; set; }
    }

    public class TransitionCondition
    {
        public string Field { get; set; }
        public ConditionOperator Operator { get; set; }
        public object Value { get; set; }
        public string Expression { get; set; }
        public bool IsAnd { get; set; } = true;
    }

    public class WorkflowTrigger
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public TriggerType Type { get; set; }
        public string EventType { get; set; }
        public string Schedule { get; set; } // Cron expression
        public List<TransitionCondition> Conditions { get; set; } = new();
        public Dictionary<string, string> ParameterMappings { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class WorkflowVariable
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public object DefaultValue { get; set; }
        public bool IsRequired { get; set; }
        public string Description { get; set; }
    }

    public class WorkflowAction
    {
        public string Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool ContinueOnError { get; set; }
        public int RetryCount { get; set; }
    }

    public class FormField
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public List<string> Options { get; set; } = new();
        public string ValidationRule { get; set; }
    }

    public class WorkflowInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DefinitionId { get; set; }
        public string DefinitionVersion { get; set; }
        public WorkflowStatus Status { get; set; } = WorkflowStatus.Active;
        public string CurrentStepId { get; set; }
        public List<string> ActiveStepIds { get; set; } = new();
        public Dictionary<string, object> Variables { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
        public List<StepInstance> StepHistory { get; set; } = new();
        public string InitiatedBy { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public string ParentInstanceId { get; set; }
        public List<string> ChildInstanceIds { get; set; } = new();
        public int Priority { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class StepInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string StepId { get; set; }
        public string StepName { get; set; }
        public StepStatus Status { get; set; }
        public string AssignedTo { get; set; }
        public List<string> Participants { get; set; } = new();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public Dictionary<string, object> Input { get; set; } = new();
        public Dictionary<string, object> Output { get; set; } = new();
        public List<StepComment> Comments { get; set; } = new();
        public List<ApprovalRecord> Approvals { get; set; } = new();
        public string CompletedBy { get; set; }
        public string TransitionUsed { get; set; }
    }

    public class StepComment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Attachments { get; set; } = new();
        public bool IsInternal { get; set; }
    }

    public class ApprovalRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ApproverId { get; set; }
        public string ApproverName { get; set; }
        public ApprovalStatus Status { get; set; }
        public string Comments { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string DelegatedTo { get; set; }
        public string EscalatedTo { get; set; }
    }

    public class ApprovalRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string InstanceId { get; set; }
        public string StepInstanceId { get; set; }
        public string StepName { get; set; }
        public string RequesterId { get; set; }
        public string RequesterName { get; set; }
        public List<string> Approvers { get; set; } = new();
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        public int RequiredCount { get; set; } = 1;
        public int CurrentCount { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public List<ApprovalRecord> Records { get; set; } = new();
    }

    public class TaskAssignment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string InstanceId { get; set; }
        public string StepInstanceId { get; set; }
        public string StepName { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        public int Priority { get; set; }
        public StepStatus Status { get; set; } = StepStatus.Pending;
        public string Title { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> FormData { get; set; } = new();
        public List<FormField> FormFields { get; set; } = new();
    }

    public class WorkflowEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string InstanceId { get; set; }
        public string EventType { get; set; }
        public string StepId { get; set; }
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Data { get; set; } = new();
        public string Description { get; set; }
    }

    public class SLAViolation
    {
        public string InstanceId { get; set; }
        public string StepInstanceId { get; set; }
        public string StepName { get; set; }
        public DateTime DueDate { get; set; }
        public TimeSpan Overdue { get; set; }
        public string AssignedTo { get; set; }
        public bool EscalationSent { get; set; }
        public int EscalationLevel { get; set; }
    }

    #endregion

    #region Workflow Automation Engine

    /// <summary>
    /// Comprehensive workflow automation engine with visual designer support,
    /// conditional routing, SLA monitoring, and template management.
    /// </summary>
    public sealed class WorkflowAutomationEngine : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, WorkflowDefinition> _definitions = new();
        private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();
        private readonly ConcurrentDictionary<string, ApprovalRequest> _approvalRequests = new();
        private readonly ConcurrentDictionary<string, TaskAssignment> _taskAssignments = new();
        private readonly ConcurrentDictionary<string, List<WorkflowEvent>> _eventLog = new();
        private readonly ConcurrentDictionary<string, WorkflowDefinition> _templates = new();

        private readonly Channel<WorkflowEvent> _eventChannel;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly CancellationTokenSource _cts;
        private readonly Task _eventProcessor;
        private readonly Task _slaMonitor;

        private bool _disposed;

        public WorkflowAutomationEngine()
        {
            _eventChannel = Channel.CreateUnbounded<WorkflowEvent>();
            _executionSemaphore = new SemaphoreSlim(50);
            _cts = new CancellationTokenSource();

            InitializeBuiltInTemplates();

            _eventProcessor = ProcessEventsAsync(_cts.Token);
            _slaMonitor = MonitorSLAAsync(_cts.Token);

            Logger.Info("WorkflowAutomationEngine initialized with built-in templates");
        }

        #region Workflow Definition Management

        public async Task<WorkflowDefinition> CreateWorkflowAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(definition.Name))
                throw new ArgumentException("Workflow name is required");

            definition.CreatedAt = DateTime.UtcNow;

            if (_definitions.TryAdd(definition.Id, definition))
            {
                await RaiseEventAsync(new WorkflowEvent
                {
                    EventType = "WorkflowCreated",
                    Data = new Dictionary<string, object>
                    {
                        ["DefinitionId"] = definition.Id,
                        ["Name"] = definition.Name
                    }
                });

                Logger.Info($"Workflow definition created: {definition.Name} ({definition.Id})");
                return definition;
            }

            throw new InvalidOperationException("Failed to create workflow definition");
        }

        public async Task<WorkflowDefinition> UpdateWorkflowAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
        {
            if (!_definitions.ContainsKey(definition.Id))
                throw new KeyNotFoundException($"Workflow definition not found: {definition.Id}");

            definition.ModifiedAt = DateTime.UtcNow;
            definition.Version = IncrementVersion(definition.Version);

            _definitions[definition.Id] = definition;

            await RaiseEventAsync(new WorkflowEvent
            {
                EventType = "WorkflowUpdated",
                Data = new Dictionary<string, object>
                {
                    ["DefinitionId"] = definition.Id,
                    ["Version"] = definition.Version
                }
            });

            Logger.Info($"Workflow definition updated: {definition.Name} v{definition.Version}");
            return definition;
        }

        public async Task DeleteWorkflowAsync(
            string definitionId,
            CancellationToken cancellationToken = default)
        {
            if (_definitions.TryRemove(definitionId, out var definition))
            {
                await RaiseEventAsync(new WorkflowEvent
                {
                    EventType = "WorkflowDeleted",
                    Data = new Dictionary<string, object>
                    {
                        ["DefinitionId"] = definitionId,
                        ["Name"] = definition.Name
                    }
                });

                Logger.Info($"Workflow definition deleted: {definition.Name}");
            }
        }

        public WorkflowDefinition GetWorkflow(string definitionId)
        {
            return _definitions.TryGetValue(definitionId, out var definition) ? definition : null;
        }

        public List<WorkflowDefinition> GetAllWorkflows()
        {
            return _definitions.Values.ToList();
        }

        public List<WorkflowDefinition> GetWorkflowsByType(WorkflowType type)
        {
            return _definitions.Values.Where(w => w.Type == type).ToList();
        }

        #endregion

        #region Workflow Instance Management

        public async Task<WorkflowInstance> StartWorkflowInstanceAsync(
            string definitionId,
            string initiatedBy,
            Dictionary<string, object> variables = null,
            Dictionary<string, object> context = null,
            CancellationToken cancellationToken = default)
        {
            if (!_definitions.TryGetValue(definitionId, out var definition))
                throw new KeyNotFoundException($"Workflow definition not found: {definitionId}");

            if (!definition.IsActive)
                throw new InvalidOperationException("Workflow definition is not active");

            var instance = new WorkflowInstance
            {
                DefinitionId = definitionId,
                DefinitionVersion = definition.Version,
                InitiatedBy = initiatedBy,
                Variables = variables ?? new Dictionary<string, object>(),
                Context = context ?? new Dictionary<string, object>()
            };

            // Set default variable values
            foreach (var variable in definition.Variables)
            {
                if (!instance.Variables.ContainsKey(variable.Name) && variable.DefaultValue != null)
                {
                    instance.Variables[variable.Name] = variable.DefaultValue;
                }
            }

            // Set due date based on SLA
            if (definition.DefaultSLA.HasValue)
            {
                instance.DueDate = DateTime.UtcNow.Add(definition.DefaultSLA.Value);
            }

            // Find the start step
            var startStep = definition.Steps.OrderBy(s => s.Order).FirstOrDefault();
            if (startStep != null)
            {
                instance.CurrentStepId = startStep.Id;
                instance.ActiveStepIds.Add(startStep.Id);

                // Execute entry to first step
                await ExecuteStepEntryAsync(instance, startStep, definition, cancellationToken);
            }

            _instances.TryAdd(instance.Id, instance);

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = instance.Id,
                EventType = "WorkflowStarted",
                UserId = initiatedBy,
                Data = new Dictionary<string, object>
                {
                    ["DefinitionId"] = definitionId,
                    ["InitiatedBy"] = initiatedBy
                }
            });

            Logger.Info($"Workflow instance started: {instance.Id} (Definition: {definition.Name})");
            return instance;
        }

        public async Task<WorkflowInstance> AdvanceWorkflowAsync(
            string instanceId,
            string transitionId = null,
            Dictionary<string, object> output = null,
            string completedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(instanceId, out var instance))
                throw new KeyNotFoundException($"Workflow instance not found: {instanceId}");

            if (!_definitions.TryGetValue(instance.DefinitionId, out var definition))
                throw new InvalidOperationException("Workflow definition not found");

            if (instance.Status != WorkflowStatus.Active)
                throw new InvalidOperationException($"Workflow is not active: {instance.Status}");

            var currentStep = definition.Steps.FirstOrDefault(s => s.Id == instance.CurrentStepId);
            if (currentStep == null)
                throw new InvalidOperationException("Current step not found");

            // Find applicable transition
            var transition = string.IsNullOrEmpty(transitionId)
                ? await FindApplicableTransitionAsync(instance, currentStep, definition, output)
                : definition.Transitions.FirstOrDefault(t => t.Id == transitionId);

            if (transition == null)
                throw new InvalidOperationException("No valid transition found");

            // Complete current step
            await CompleteStepAsync(instance, currentStep, output, completedBy, transition.Id, cancellationToken);

            // Execute transition actions
            await ExecuteActionsAsync(transition.Actions, instance, cancellationToken);

            // Move to next step
            var nextStep = definition.Steps.FirstOrDefault(s => s.Id == transition.ToStepId);
            if (nextStep != null)
            {
                instance.CurrentStepId = nextStep.Id;
                instance.ActiveStepIds = new List<string> { nextStep.Id };

                await ExecuteStepEntryAsync(instance, nextStep, definition, cancellationToken);
            }
            else
            {
                // No next step - workflow complete
                instance.Status = WorkflowStatus.Completed;
                instance.CompletedAt = DateTime.UtcNow;
                instance.ActiveStepIds.Clear();
            }

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = instanceId,
                EventType = "WorkflowAdvanced",
                StepId = currentStep.Id,
                UserId = completedBy,
                Data = new Dictionary<string, object>
                {
                    ["FromStep"] = currentStep.Name,
                    ["ToStep"] = nextStep?.Name ?? "Complete",
                    ["Transition"] = transition.Name
                }
            });

            return instance;
        }

        public WorkflowInstance GetInstance(string instanceId)
        {
            return _instances.TryGetValue(instanceId, out var instance) ? instance : null;
        }

        public List<WorkflowInstance> GetActiveInstances()
        {
            return _instances.Values
                .Where(i => i.Status == WorkflowStatus.Active)
                .ToList();
        }

        public List<WorkflowInstance> GetInstancesByDefinition(string definitionId)
        {
            return _instances.Values
                .Where(i => i.DefinitionId == definitionId)
                .ToList();
        }

        public List<WorkflowEvent> GetWorkflowHistory(string instanceId)
        {
            return _eventLog.TryGetValue(instanceId, out var events)
                ? events.OrderBy(e => e.Timestamp).ToList()
                : new List<WorkflowEvent>();
        }

        #endregion

        #region Approval Management

        public async Task<ApprovalRecord> ApproveStepAsync(
            string instanceId,
            string stepInstanceId,
            string approverId,
            string comments = null,
            CancellationToken cancellationToken = default)
        {
            return await ProcessApprovalAsync(
                instanceId, stepInstanceId, approverId,
                ApprovalStatus.Approved, comments, cancellationToken);
        }

        public async Task<ApprovalRecord> RejectStepAsync(
            string instanceId,
            string stepInstanceId,
            string approverId,
            string comments = null,
            CancellationToken cancellationToken = default)
        {
            return await ProcessApprovalAsync(
                instanceId, stepInstanceId, approverId,
                ApprovalStatus.Rejected, comments, cancellationToken);
        }

        public async Task<ApprovalRecord> RequestChangesAsync(
            string instanceId,
            string stepInstanceId,
            string approverId,
            string comments,
            CancellationToken cancellationToken = default)
        {
            return await ProcessApprovalAsync(
                instanceId, stepInstanceId, approverId,
                ApprovalStatus.ChangesRequested, comments, cancellationToken);
        }

        private async Task<ApprovalRecord> ProcessApprovalAsync(
            string instanceId,
            string stepInstanceId,
            string approverId,
            ApprovalStatus status,
            string comments,
            CancellationToken cancellationToken)
        {
            if (!_instances.TryGetValue(instanceId, out var instance))
                throw new KeyNotFoundException($"Workflow instance not found: {instanceId}");

            var stepInstance = instance.StepHistory.FirstOrDefault(s => s.Id == stepInstanceId);
            if (stepInstance == null)
                throw new KeyNotFoundException($"Step instance not found: {stepInstanceId}");

            var record = new ApprovalRecord
            {
                ApproverId = approverId,
                Status = status,
                Comments = comments,
                RequestedAt = stepInstance.StartedAt,
                RespondedAt = DateTime.UtcNow
            };

            stepInstance.Approvals.Add(record);

            // Check if approval requirements are met
            var approvedCount = stepInstance.Approvals.Count(a => a.Status == ApprovalStatus.Approved);

            // Find the step definition to check required approvals
            if (_definitions.TryGetValue(instance.DefinitionId, out var definition))
            {
                var stepDef = definition.Steps.FirstOrDefault(s => s.Id == stepInstance.StepId);
                if (stepDef != null)
                {
                    var requiredApprovals = stepDef.RequiredApprovals ?? 1;

                    if (status == ApprovalStatus.Rejected)
                    {
                        stepInstance.Status = StepStatus.Rejected;
                    }
                    else if (approvedCount >= requiredApprovals)
                    {
                        stepInstance.Status = StepStatus.Completed;
                        stepInstance.CompletedAt = DateTime.UtcNow;

                        // Auto-advance workflow if step is complete
                        await AdvanceWorkflowAsync(instanceId, completedBy: approverId, cancellationToken: cancellationToken);
                    }
                }
            }

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = instanceId,
                EventType = $"Approval{status}",
                StepId = stepInstance.StepId,
                UserId = approverId,
                Data = new Dictionary<string, object>
                {
                    ["StepName"] = stepInstance.StepName,
                    ["Status"] = status.ToString(),
                    ["Comments"] = comments
                }
            });

            return record;
        }

        public List<ApprovalRequest> GetPendingApprovals(string userId)
        {
            return _approvalRequests.Values
                .Where(r => r.Status == ApprovalStatus.Pending && r.Approvers.Contains(userId))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        #endregion

        #region Task Management

        public async Task<TaskAssignment> AssignTaskAsync(
            string instanceId,
            string stepInstanceId,
            string assignedTo,
            string assignedBy,
            CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(instanceId, out var instance))
                throw new KeyNotFoundException($"Workflow instance not found: {instanceId}");

            var stepInstance = instance.StepHistory.FirstOrDefault(s => s.Id == stepInstanceId);
            if (stepInstance == null)
                throw new KeyNotFoundException($"Step instance not found: {stepInstanceId}");

            var assignment = new TaskAssignment
            {
                InstanceId = instanceId,
                StepInstanceId = stepInstanceId,
                StepName = stepInstance.StepName,
                AssignedTo = assignedTo,
                AssignedBy = assignedBy,
                DueDate = stepInstance.DueDate,
                Title = $"Task: {stepInstance.StepName}",
                Description = $"Please complete the assigned task for workflow step: {stepInstance.StepName}"
            };

            stepInstance.AssignedTo = assignedTo;
            _taskAssignments.TryAdd(assignment.Id, assignment);

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = instanceId,
                EventType = "TaskAssigned",
                StepId = stepInstance.StepId,
                UserId = assignedBy,
                Data = new Dictionary<string, object>
                {
                    ["AssignedTo"] = assignedTo,
                    ["StepName"] = stepInstance.StepName
                }
            });

            return assignment;
        }

        public async Task<TaskAssignment> ReassignTaskAsync(
            string taskId,
            string newAssignee,
            string reassignedBy,
            string reason = null,
            CancellationToken cancellationToken = default)
        {
            if (!_taskAssignments.TryGetValue(taskId, out var assignment))
                throw new KeyNotFoundException($"Task assignment not found: {taskId}");

            var previousAssignee = assignment.AssignedTo;
            assignment.AssignedTo = newAssignee;
            assignment.AssignedBy = reassignedBy;
            assignment.AssignedAt = DateTime.UtcNow;

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = assignment.InstanceId,
                EventType = "TaskReassigned",
                StepId = assignment.StepInstanceId,
                UserId = reassignedBy,
                Data = new Dictionary<string, object>
                {
                    ["PreviousAssignee"] = previousAssignee,
                    ["NewAssignee"] = newAssignee,
                    ["Reason"] = reason
                }
            });

            return assignment;
        }

        public async Task CompleteTaskAsync(
            string taskId,
            string completedBy,
            Dictionary<string, object> formData = null,
            CancellationToken cancellationToken = default)
        {
            if (!_taskAssignments.TryGetValue(taskId, out var assignment))
                throw new KeyNotFoundException($"Task assignment not found: {taskId}");

            assignment.Status = StepStatus.Completed;
            assignment.FormData = formData ?? new Dictionary<string, object>();

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = assignment.InstanceId,
                EventType = "TaskCompleted",
                StepId = assignment.StepInstanceId,
                UserId = completedBy,
                Data = new Dictionary<string, object>
                {
                    ["StepName"] = assignment.StepName,
                    ["FormData"] = formData
                }
            });

            // Advance the workflow
            await AdvanceWorkflowAsync(
                assignment.InstanceId,
                output: formData,
                completedBy: completedBy,
                cancellationToken: cancellationToken);
        }

        public List<TaskAssignment> GetMyTasks(string userId)
        {
            return _taskAssignments.Values
                .Where(t => t.AssignedTo == userId && t.Status == StepStatus.Pending)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.Priority)
                .ToList();
        }

        public List<TaskAssignment> GetOverdueTasks()
        {
            var now = DateTime.UtcNow;
            return _taskAssignments.Values
                .Where(t => t.Status == StepStatus.Pending && t.DueDate.HasValue && t.DueDate < now)
                .OrderBy(t => t.DueDate)
                .ToList();
        }

        #endregion

        #region Condition Evaluation

        public async Task<WorkflowTransition> FindApplicableTransitionAsync(
            WorkflowInstance instance,
            WorkflowStep currentStep,
            WorkflowDefinition definition,
            Dictionary<string, object> output)
        {
            var transitions = definition.Transitions
                .Where(t => t.FromStepId == currentStep.Id)
                .OrderBy(t => t.Priority)
                .ToList();

            foreach (var transition in transitions)
            {
                if (transition.Conditions.Count == 0 || transition.IsDefault)
                {
                    if (transition.IsDefault)
                        continue; // Check non-default transitions first
                    return transition;
                }

                var allConditionsMet = await EvaluateConditionsAsync(
                    transition.Conditions, instance.Variables, output);

                if (allConditionsMet)
                    return transition;
            }

            // Fall back to default transition
            return transitions.FirstOrDefault(t => t.IsDefault);
        }

        public async Task<bool> EvaluateConditionsAsync(
            List<TransitionCondition> conditions,
            Dictionary<string, object> variables,
            Dictionary<string, object> output)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            var allValues = new Dictionary<string, object>(variables);
            if (output != null)
            {
                foreach (var kv in output)
                    allValues[kv.Key] = kv.Value;
            }

            bool? result = null;

            foreach (var condition in conditions)
            {
                var conditionResult = EvaluateCondition(condition, allValues);

                if (!result.HasValue)
                {
                    result = conditionResult;
                }
                else if (condition.IsAnd)
                {
                    result = result.Value && conditionResult;
                }
                else
                {
                    result = result.Value || conditionResult;
                }
            }

            return result ?? true;
        }

        private bool EvaluateCondition(TransitionCondition condition, Dictionary<string, object> values)
        {
            if (!string.IsNullOrEmpty(condition.Expression))
            {
                return EvaluateExpression(condition.Expression, values);
            }

            if (!values.TryGetValue(condition.Field, out var fieldValue))
                fieldValue = null;

            return condition.Operator switch
            {
                ConditionOperator.Equals => Equals(fieldValue, condition.Value),
                ConditionOperator.NotEquals => !Equals(fieldValue, condition.Value),
                ConditionOperator.GreaterThan => Compare(fieldValue, condition.Value) > 0,
                ConditionOperator.LessThan => Compare(fieldValue, condition.Value) < 0,
                ConditionOperator.Contains => fieldValue?.ToString()?.Contains(condition.Value?.ToString() ?? "") ?? false,
                ConditionOperator.StartsWith => fieldValue?.ToString()?.StartsWith(condition.Value?.ToString() ?? "") ?? false,
                ConditionOperator.EndsWith => fieldValue?.ToString()?.EndsWith(condition.Value?.ToString() ?? "") ?? false,
                ConditionOperator.IsNull => fieldValue == null,
                ConditionOperator.IsNotNull => fieldValue != null,
                ConditionOperator.In => IsIn(fieldValue, condition.Value),
                ConditionOperator.NotIn => !IsIn(fieldValue, condition.Value),
                _ => false
            };
        }

        private bool EvaluateExpression(string expression, Dictionary<string, object> values)
        {
            // Simple expression evaluation for common patterns
            // In production, use a proper expression parser

            try
            {
                // Replace variable references with values
                foreach (var kv in values)
                {
                    expression = expression.Replace($"${{{kv.Key}}}", kv.Value?.ToString() ?? "null");
                    expression = expression.Replace($"@{kv.Key}", kv.Value?.ToString() ?? "null");
                }

                // Basic boolean evaluation
                if (expression.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (expression.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private int Compare(object a, object b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            if (a is IComparable comparableA && b is IComparable)
            {
                try
                {
                    return comparableA.CompareTo(Convert.ChangeType(b, a.GetType()));
                }
                catch
                {
                    return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
                }
            }

            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        private bool IsIn(object value, object collection)
        {
            if (collection is IEnumerable<object> enumerable)
            {
                return enumerable.Any(item => Equals(item, value));
            }

            if (collection is string str)
            {
                var items = str.Split(',').Select(s => s.Trim());
                return items.Contains(value?.ToString());
            }

            return false;
        }

        #endregion

        #region Action Execution

        public async Task ExecuteActionsAsync(
            List<WorkflowAction> actions,
            WorkflowInstance instance,
            CancellationToken cancellationToken = default)
        {
            if (actions == null || actions.Count == 0)
                return;

            foreach (var action in actions)
            {
                try
                {
                    await ExecuteActionAsync(action, instance, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error executing action {action.Type}");

                    if (!action.ContinueOnError)
                        throw;
                }
            }
        }

        private async Task ExecuteActionAsync(
            WorkflowAction action,
            WorkflowInstance instance,
            CancellationToken cancellationToken)
        {
            switch (action.Type.ToLowerInvariant())
            {
                case "setVariable":
                    if (action.Parameters.TryGetValue("name", out var nameObj) &&
                        action.Parameters.TryGetValue("value", out var valueObj))
                    {
                        instance.Variables[nameObj.ToString()] = valueObj;
                    }
                    break;

                case "sendNotification":
                    // Integration point for notification system
                    Logger.Info($"Sending notification: {action.Parameters.GetValueOrDefault("message")}");
                    break;

                case "callWebhook":
                    if (action.Parameters.TryGetValue("url", out var urlObj))
                    {
                        // Integration point for webhook calls
                        Logger.Info($"Calling webhook: {urlObj}");
                    }
                    break;

                case "delay":
                    if (action.Parameters.TryGetValue("duration", out var durationObj) &&
                        TimeSpan.TryParse(durationObj.ToString(), out var duration))
                    {
                        await Task.Delay(duration, cancellationToken);
                    }
                    break;

                case "startSubWorkflow":
                    if (action.Parameters.TryGetValue("definitionId", out var defIdObj))
                    {
                        var subInstance = await StartWorkflowInstanceAsync(
                            defIdObj.ToString(),
                            instance.InitiatedBy,
                            instance.Variables,
                            cancellationToken: cancellationToken);

                        subInstance.ParentInstanceId = instance.Id;
                        instance.ChildInstanceIds.Add(subInstance.Id);
                    }
                    break;

                default:
                    Logger.Warn($"Unknown action type: {action.Type}");
                    break;
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Step Execution Helpers

        private async Task ExecuteStepEntryAsync(
            WorkflowInstance instance,
            WorkflowStep step,
            WorkflowDefinition definition,
            CancellationToken cancellationToken)
        {
            var stepInstance = new StepInstance
            {
                StepId = step.Id,
                StepName = step.Name,
                Status = StepStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };

            // Calculate due date
            if (step.DueOffset.HasValue)
            {
                stepInstance.DueDate = DateTime.UtcNow.Add(step.DueOffset.Value);
            }
            else if (step.SLA.HasValue)
            {
                stepInstance.DueDate = DateTime.UtcNow.Add(step.SLA.Value);
            }

            instance.StepHistory.Add(stepInstance);

            // Execute entry actions
            await ExecuteActionsAsync(step.EntryActions, instance, cancellationToken);

            // Auto-assign based on roles
            if (step.AssigneeRoles.Count > 0 || step.AssigneeUsers.Count > 0)
            {
                var assignee = step.AssigneeUsers.FirstOrDefault() ??
                               await ResolveRoleAssigneeAsync(step.AssigneeRoles.FirstOrDefault());

                if (!string.IsNullOrEmpty(assignee))
                {
                    await AssignTaskAsync(instance.Id, stepInstance.Id, assignee, "system", cancellationToken);
                }
            }

            // Create approval request if needed
            if (step.Type == StepType.Approval)
            {
                var approvalRequest = new ApprovalRequest
                {
                    InstanceId = instance.Id,
                    StepInstanceId = stepInstance.Id,
                    StepName = step.Name,
                    RequesterId = instance.InitiatedBy,
                    Approvers = step.AssigneeUsers.ToList(),
                    Title = $"Approval Required: {step.Name}",
                    Description = step.Description,
                    DueDate = stepInstance.DueDate,
                    RequiredCount = step.RequiredApprovals ?? 1
                };

                _approvalRequests.TryAdd(approvalRequest.Id, approvalRequest);
            }

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = instance.Id,
                EventType = "StepEntered",
                StepId = step.Id,
                Data = new Dictionary<string, object>
                {
                    ["StepName"] = step.Name,
                    ["StepType"] = step.Type.ToString()
                }
            });
        }

        private async Task CompleteStepAsync(
            WorkflowInstance instance,
            WorkflowStep step,
            Dictionary<string, object> output,
            string completedBy,
            string transitionId,
            CancellationToken cancellationToken)
        {
            var stepInstance = instance.StepHistory.LastOrDefault(s => s.StepId == step.Id);
            if (stepInstance != null)
            {
                stepInstance.Status = StepStatus.Completed;
                stepInstance.CompletedAt = DateTime.UtcNow;
                stepInstance.CompletedBy = completedBy;
                stepInstance.TransitionUsed = transitionId;
                stepInstance.Output = output ?? new Dictionary<string, object>();
            }

            // Execute exit actions
            await ExecuteActionsAsync(step.ExitActions, instance, cancellationToken);

            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = instance.Id,
                EventType = "StepCompleted",
                StepId = step.Id,
                UserId = completedBy,
                Data = new Dictionary<string, object>
                {
                    ["StepName"] = step.Name,
                    ["Output"] = output
                }
            });
        }

        private async Task<string> ResolveRoleAssigneeAsync(string role)
        {
            // In production, integrate with RBAC system
            await Task.CompletedTask;
            return null;
        }

        #endregion

        #region Template Management

        private void InitializeBuiltInTemplates()
        {
            // Submittal Approval Workflow
            var submittalWorkflow = new WorkflowDefinition
            {
                Id = "template-submittal-approval",
                Name = "Submittal Approval Workflow",
                Description = "Standard workflow for reviewing and approving submittals",
                Type = WorkflowType.Submittal,
                IsTemplate = true,
                DefaultSLA = TimeSpan.FromDays(5),
                Steps = new List<WorkflowStep>
                {
                    new() { Id = "submit", Name = "Submit Document", Type = StepType.Task, Order = 1, PositionX = 100, PositionY = 100 },
                    new() { Id = "review", Name = "Technical Review", Type = StepType.Approval, Order = 2, AssigneeRoles = new List<string> { "ProjectManager" }, PositionX = 300, PositionY = 100 },
                    new() { Id = "approve", Name = "Final Approval", Type = StepType.Approval, Order = 3, AssigneeRoles = new List<string> { "Architect" }, PositionX = 500, PositionY = 100 },
                    new() { Id = "notify", Name = "Notify Stakeholders", Type = StepType.Notification, Order = 4, PositionX = 700, PositionY = 100 }
                },
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromStepId = "submit", ToStepId = "review", Name = "Submit for Review", IsDefault = true },
                    new() { FromStepId = "review", ToStepId = "approve", Name = "Approve", Conditions = new List<TransitionCondition> { new() { Field = "approved", Operator = ConditionOperator.Equals, Value = true } } },
                    new() { FromStepId = "review", ToStepId = "submit", Name = "Request Revisions", Conditions = new List<TransitionCondition> { new() { Field = "approved", Operator = ConditionOperator.Equals, Value = false } } },
                    new() { FromStepId = "approve", ToStepId = "notify", Name = "Complete", IsDefault = true }
                }
            };
            _templates.TryAdd(submittalWorkflow.Id, submittalWorkflow);

            // RFI Response Workflow
            var rfiWorkflow = new WorkflowDefinition
            {
                Id = "template-rfi-response",
                Name = "RFI Response Workflow",
                Description = "Workflow for processing and responding to RFIs",
                Type = WorkflowType.RFI,
                IsTemplate = true,
                DefaultSLA = TimeSpan.FromDays(3),
                Steps = new List<WorkflowStep>
                {
                    new() { Id = "log", Name = "Log RFI", Type = StepType.Task, Order = 1, PositionX = 100, PositionY = 100 },
                    new() { Id = "route", Name = "Route to Discipline", Type = StepType.Condition, Order = 2, PositionX = 250, PositionY = 100 },
                    new() { Id = "respond", Name = "Prepare Response", Type = StepType.Task, Order = 3, PositionX = 400, PositionY = 100 },
                    new() { Id = "review", Name = "Review Response", Type = StepType.Approval, Order = 4, PositionX = 550, PositionY = 100 },
                    new() { Id = "issue", Name = "Issue Response", Type = StepType.Task, Order = 5, PositionX = 700, PositionY = 100 }
                },
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromStepId = "log", ToStepId = "route", Name = "Route", IsDefault = true },
                    new() { FromStepId = "route", ToStepId = "respond", Name = "Assign", IsDefault = true },
                    new() { FromStepId = "respond", ToStepId = "review", Name = "Submit", IsDefault = true },
                    new() { FromStepId = "review", ToStepId = "issue", Name = "Approve" },
                    new() { FromStepId = "review", ToStepId = "respond", Name = "Revise" }
                }
            };
            _templates.TryAdd(rfiWorkflow.Id, rfiWorkflow);

            // Change Order Approval Workflow
            var changeOrderWorkflow = new WorkflowDefinition
            {
                Id = "template-change-order",
                Name = "Change Order Approval Workflow",
                Description = "Multi-level approval workflow for change orders",
                Type = WorkflowType.ChangeOrder,
                IsTemplate = true,
                DefaultSLA = TimeSpan.FromDays(7),
                Steps = new List<WorkflowStep>
                {
                    new() { Id = "initiate", Name = "Initiate Change Order", Type = StepType.Task, Order = 1, PositionX = 100, PositionY = 100 },
                    new() { Id = "estimate", Name = "Cost Estimation", Type = StepType.Task, Order = 2, AssigneeRoles = new List<string> { "Estimator" }, PositionX = 250, PositionY = 100 },
                    new() { Id = "pm-review", Name = "PM Review", Type = StepType.Approval, Order = 3, AssigneeRoles = new List<string> { "ProjectManager" }, PositionX = 400, PositionY = 100 },
                    new() { Id = "owner-approval", Name = "Owner Approval", Type = StepType.Approval, Order = 4, AssigneeRoles = new List<string> { "Owner" }, PositionX = 550, PositionY = 100 },
                    new() { Id = "execute", Name = "Execute Change", Type = StepType.Task, Order = 5, PositionX = 700, PositionY = 100 }
                },
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromStepId = "initiate", ToStepId = "estimate", IsDefault = true },
                    new() { FromStepId = "estimate", ToStepId = "pm-review", IsDefault = true },
                    new() { FromStepId = "pm-review", ToStepId = "owner-approval", Name = "Approve" },
                    new() { FromStepId = "pm-review", ToStepId = "initiate", Name = "Reject" },
                    new() { FromStepId = "owner-approval", ToStepId = "execute", Name = "Approve" },
                    new() { FromStepId = "owner-approval", ToStepId = "initiate", Name = "Reject" }
                }
            };
            _templates.TryAdd(changeOrderWorkflow.Id, changeOrderWorkflow);

            // Document Review Workflow
            var documentReviewWorkflow = new WorkflowDefinition
            {
                Id = "template-document-review",
                Name = "Document Review Workflow",
                Description = "Collaborative document review with multiple reviewers",
                Type = WorkflowType.Review,
                IsTemplate = true,
                DefaultSLA = TimeSpan.FromDays(5),
                Steps = new List<WorkflowStep>
                {
                    new() { Id = "upload", Name = "Upload Document", Type = StepType.Task, Order = 1, PositionX = 100, PositionY = 100 },
                    new() { Id = "parallel-review", Name = "Parallel Review", Type = StepType.Parallel, Order = 2, PositionX = 300, PositionY = 100 },
                    new() { Id = "consolidate", Name = "Consolidate Comments", Type = StepType.Task, Order = 3, PositionX = 500, PositionY = 100 },
                    new() { Id = "finalize", Name = "Finalize Document", Type = StepType.Task, Order = 4, PositionX = 700, PositionY = 100 }
                },
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromStepId = "upload", ToStepId = "parallel-review", IsDefault = true },
                    new() { FromStepId = "parallel-review", ToStepId = "consolidate", IsDefault = true },
                    new() { FromStepId = "consolidate", ToStepId = "finalize", IsDefault = true }
                }
            };
            _templates.TryAdd(documentReviewWorkflow.Id, documentReviewWorkflow);
        }

        public List<WorkflowDefinition> GetTemplates()
        {
            return _templates.Values.ToList();
        }

        public WorkflowDefinition GetTemplate(string templateId)
        {
            return _templates.TryGetValue(templateId, out var template) ? template : null;
        }

        public async Task<WorkflowDefinition> CreateFromTemplateAsync(
            string templateId,
            string name,
            string createdBy,
            CancellationToken cancellationToken = default)
        {
            if (!_templates.TryGetValue(templateId, out var template))
                throw new KeyNotFoundException($"Template not found: {templateId}");

            var definition = new WorkflowDefinition
            {
                Name = name,
                Description = template.Description,
                Type = template.Type,
                CreatedBy = createdBy,
                DefaultSLA = template.DefaultSLA,
                Steps = template.Steps.Select(s => new WorkflowStep
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = s.Name,
                    Description = s.Description,
                    Type = s.Type,
                    Order = s.Order,
                    AssigneeRoles = new List<string>(s.AssigneeRoles),
                    AssigneeUsers = new List<string>(s.AssigneeUsers),
                    DueOffset = s.DueOffset,
                    SLA = s.SLA,
                    PositionX = s.PositionX,
                    PositionY = s.PositionY
                }).ToList()
            };

            // Remap step IDs in transitions
            var stepIdMap = new Dictionary<string, string>();
            for (int i = 0; i < template.Steps.Count; i++)
            {
                stepIdMap[template.Steps[i].Id] = definition.Steps[i].Id;
            }

            definition.Transitions = template.Transitions.Select(t => new WorkflowTransition
            {
                FromStepId = stepIdMap.GetValueOrDefault(t.FromStepId, t.FromStepId),
                ToStepId = stepIdMap.GetValueOrDefault(t.ToStepId, t.ToStepId),
                Name = t.Name,
                IsDefault = t.IsDefault,
                Priority = t.Priority,
                Conditions = t.Conditions.ToList()
            }).ToList();

            return await CreateWorkflowAsync(definition, cancellationToken);
        }

        #endregion

        #region SLA Monitoring

        private async Task MonitorSLAAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                    var violations = await CheckSLAViolationsAsync();

                    foreach (var violation in violations)
                    {
                        await HandleSLAViolationAsync(violation);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in SLA monitoring");
                }
            }
        }

        private async Task<List<SLAViolation>> CheckSLAViolationsAsync()
        {
            var violations = new List<SLAViolation>();
            var now = DateTime.UtcNow;

            foreach (var instance in _instances.Values.Where(i => i.Status == WorkflowStatus.Active))
            {
                foreach (var step in instance.StepHistory.Where(s => s.Status == StepStatus.InProgress))
                {
                    if (step.DueDate.HasValue && step.DueDate < now)
                    {
                        violations.Add(new SLAViolation
                        {
                            InstanceId = instance.Id,
                            StepInstanceId = step.Id,
                            StepName = step.StepName,
                            DueDate = step.DueDate.Value,
                            Overdue = now - step.DueDate.Value,
                            AssignedTo = step.AssignedTo
                        });
                    }
                }
            }

            await Task.CompletedTask;
            return violations;
        }

        private async Task HandleSLAViolationAsync(SLAViolation violation)
        {
            await RaiseEventAsync(new WorkflowEvent
            {
                InstanceId = violation.InstanceId,
                EventType = "SLAViolation",
                StepId = violation.StepInstanceId,
                Data = new Dictionary<string, object>
                {
                    ["StepName"] = violation.StepName,
                    ["DueDate"] = violation.DueDate,
                    ["Overdue"] = violation.Overdue.TotalHours,
                    ["AssignedTo"] = violation.AssignedTo
                }
            });

            Logger.Warn($"SLA violation: {violation.StepName} is {violation.Overdue.TotalHours:F1} hours overdue");
        }

        #endregion

        #region Event Processing

        private async Task RaiseEventAsync(WorkflowEvent workflowEvent)
        {
            if (!string.IsNullOrEmpty(workflowEvent.InstanceId))
            {
                var events = _eventLog.GetOrAdd(workflowEvent.InstanceId, _ => new List<WorkflowEvent>());
                lock (events)
                {
                    events.Add(workflowEvent);
                }
            }

            await _eventChannel.Writer.WriteAsync(workflowEvent);
        }

        private async Task ProcessEventsAsync(CancellationToken cancellationToken)
        {
            await foreach (var workflowEvent in _eventChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    Logger.Debug($"Workflow event: {workflowEvent.EventType} for instance {workflowEvent.InstanceId}");

                    // Event handlers can be added here
                    // Integration point for external systems
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error processing workflow event: {workflowEvent.EventType}");
                }
            }
        }

        #endregion

        #region Utility Methods

        private string IncrementVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "1.0";

            var parts = version.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                return $"{parts[0]}.{minor + 1}";
            }

            return version + ".1";
        }

        public WorkflowStatistics GetStatistics()
        {
            var instances = _instances.Values.ToList();

            return new WorkflowStatistics
            {
                TotalDefinitions = _definitions.Count,
                ActiveDefinitions = _definitions.Values.Count(d => d.IsActive),
                TotalInstances = instances.Count,
                ActiveInstances = instances.Count(i => i.Status == WorkflowStatus.Active),
                CompletedInstances = instances.Count(i => i.Status == WorkflowStatus.Completed),
                PendingApprovals = _approvalRequests.Values.Count(r => r.Status == ApprovalStatus.Pending),
                PendingTasks = _taskAssignments.Values.Count(t => t.Status == StepStatus.Pending),
                OverdueTasks = GetOverdueTasks().Count
            };
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _eventChannel.Writer.Complete();

            try
            {
                await Task.WhenAll(_eventProcessor, _slaMonitor);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }

            _executionSemaphore.Dispose();
            _cts.Dispose();

            Logger.Info("WorkflowAutomationEngine disposed");
        }

        #endregion
    }

    #endregion

    #region Statistics

    public class WorkflowStatistics
    {
        public int TotalDefinitions { get; set; }
        public int ActiveDefinitions { get; set; }
        public int TotalInstances { get; set; }
        public int ActiveInstances { get; set; }
        public int CompletedInstances { get; set; }
        public int PendingApprovals { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
    }

    #endregion
}
