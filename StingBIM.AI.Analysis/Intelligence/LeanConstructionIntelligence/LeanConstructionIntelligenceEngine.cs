// LeanConstructionIntelligenceEngine.cs
// StingBIM v7 - Lean Construction Intelligence (Comprehensive)
// Last Planner System, constraint logs, PPC tracking, value stream mapping, pull planning

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.LeanConstructionIntelligence
{
    #region Enums

    public enum PlanningLevel { MasterSchedule, PhaseSchedule, LookaheadPlan, WeeklyWorkPlan, DailyPlan }
    public enum LeanTaskStatus { NotStarted, Planned, Ready, InProgress, Complete, Incomplete, Blocked, Deferred }
    public enum ConstraintType { Material, Labor, Equipment, Information, Predecessor, Space, Permit, Weather, Safety, Design, RFI, Submittal }
    public enum ConstraintStatus { Identified, InProgress, Resolved, Escalated, Deferred }
    public enum ConstraintPriority { Critical, High, Medium, Low }
    public enum VarianceReason { Material, Labor, Equipment, Prerequisite, RFI, Design, Weather, Safety, Quality, Coordination, Scope, Other }
    public enum ValueStreamCategory { ValueAdding, NonValueNecessary, Waste, Waiting, Transportation, Motion, Inventory, Overproduction }
    public enum WasteType { Defects, Overproduction, Waiting, NonUtilizedTalent, Transportation, Inventory, Motion, ExtraProcessing }
    public enum TeamRole { GeneralContractor, Subcontractor, Owner, Architect, Engineer, Superintendent, PM, Foreman }

    #endregion

    #region Data Models

    public class LeanProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Phase> Phases { get; set; } = new();
        public List<WeeklyWorkPlan> WeeklyPlans { get; set; } = new();
        public List<Constraint> Constraints { get; set; } = new();
        public List<PPCRecord> PPCHistory { get; set; } = new();
        public ValueStreamMap ValueStream { get; set; }
        public List<PullPlanningSession> PullPlanningSessions { get; set; } = new();
        public LeanMetrics Metrics { get; set; }
        public List<TeamMember> Team { get; set; } = new();
    }

    public class Phase
    {
        public string PhaseId { get; set; }
        public string PhaseName { get; set; }
        public int PhaseNumber { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public List<Milestone> Milestones { get; set; } = new();
        public List<WorkPackage> WorkPackages { get; set; } = new();
        public double CompletionPercentage { get; set; }
    }

    public class Milestone
    {
        public string MilestoneId { get; set; }
        public string MilestoneName { get; set; }
        public DateTime TargetDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public string Owner { get; set; }
        public LeanTaskStatus Status { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public bool IsCriticalPath { get; set; }
    }

    public class WorkPackage
    {
        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public string PhaseId { get; set; }
        public string ResponsibleParty { get; set; }
        public List<LeanTask> Tasks { get; set; } = new();
        public double PlannedDuration { get; set; }
        public double ActualDuration { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class LeanTask
    {
        public string TaskId { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public string WorkPackageId { get; set; }
        public string AssignedTo { get; set; }
        public string Location { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public double PlannedDuration { get; set; }
        public double ActualDuration { get; set; }
        public LeanTaskStatus Status { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
        public bool IsReady { get; set; }
        public bool CanBeDone { get; set; }
        public bool WillBeDone { get; set; }
        public VarianceReason? VarianceReason { get; set; }
        public string VarianceNotes { get; set; }
    }

    public class WeeklyWorkPlan
    {
        public string PlanId { get; set; } = Guid.NewGuid().ToString();
        public int WeekNumber { get; set; }
        public int Year { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; }
        public List<PlannedTask> PlannedTasks { get; set; } = new();
        public List<string> Attendees { get; set; } = new();
        public PPCRecord WeeklyPPC { get; set; }
        public List<string> LookaheadConstraints { get; set; } = new();
        public string MeetingNotes { get; set; }
        public List<Commitment> Commitments { get; set; } = new();
    }

    public class PlannedTask
    {
        public string TaskId { get; set; }
        public string TaskName { get; set; }
        public string AssignedTo { get; set; }
        public string Location { get; set; }
        public DateTime PlannedDate { get; set; }
        public double PlannedDuration { get; set; }
        public bool WasComplete { get; set; }
        public VarianceReason? VarianceReason { get; set; }
        public string VarianceDescription { get; set; }
        public string CommittedBy { get; set; }
        public DateTime CommittedDate { get; set; }
    }

    public class Commitment
    {
        public string CommitmentId { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; }
        public string CommittedBy { get; set; }
        public string CommittedTo { get; set; }
        public DateTime DueDate { get; set; }
        public LeanTaskStatus Status { get; set; }
        public string Notes { get; set; }
    }

    public class Constraint
    {
        public string ConstraintId { get; set; } = Guid.NewGuid().ToString();
        public ConstraintType Type { get; set; }
        public string Description { get; set; }
        public string AffectedTask { get; set; }
        public string AffectedLocation { get; set; }
        public DateTime IdentifiedDate { get; set; } = DateTime.UtcNow;
        public DateTime NeededByDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string IdentifiedBy { get; set; }
        public string AssignedTo { get; set; }
        public ConstraintStatus Status { get; set; } = ConstraintStatus.Identified;
        public ConstraintPriority Priority { get; set; }
        public string ResolutionPlan { get; set; }
        public string ResolutionNotes { get; set; }
        public int DaysOutstanding { get; set; }
        public double ImpactDays { get; set; }
        public double ImpactCost { get; set; }
    }

    public class PPCRecord
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public int WeekNumber { get; set; }
        public int Year { get; set; }
        public DateTime WeekEndDate { get; set; }
        public int TasksPlanned { get; set; }
        public int TasksCompleted { get; set; }
        public double PPC { get; set; }
        public double RollingAverage4Week { get; set; }
        public double RollingAverage8Week { get; set; }
        public Dictionary<VarianceReason, int> VarianceBreakdown { get; set; } = new();
        public Dictionary<string, double> PPCByTrade { get; set; } = new();
        public Dictionary<string, double> PPCByLocation { get; set; } = new();
        public List<string> TopVarianceReasons { get; set; } = new();
        public List<string> ImprovementActions { get; set; } = new();
        public double TMR { get; set; }
        public string Notes { get; set; }
    }

    public class ValueStreamMap
    {
        public string MapId { get; set; } = Guid.NewGuid().ToString();
        public string ProcessName { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public List<ProcessStep> CurrentState { get; set; } = new();
        public List<ProcessStep> FutureState { get; set; } = new();
        public ValueStreamMetrics CurrentMetrics { get; set; }
        public ValueStreamMetrics FutureMetrics { get; set; }
        public List<ImprovementOpportunity> Improvements { get; set; } = new();
        public List<WasteIdentification> Wastes { get; set; } = new();
    }

    public class ProcessStep
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public int Sequence { get; set; }
        public string ResponsibleParty { get; set; }
        public double CycleTime { get; set; }
        public double WaitTime { get; set; }
        public double ProcessTime { get; set; }
        public double LeadTime { get; set; }
        public ValueStreamCategory Category { get; set; }
        public double ValueAddedRatio { get; set; }
        public int WIPQuantity { get; set; }
        public int BatchSize { get; set; }
        public double QualityRate { get; set; } = 100;
        public List<string> Issues { get; set; } = new();
    }

    public class ValueStreamMetrics
    {
        public double TotalLeadTime { get; set; }
        public double TotalProcessTime { get; set; }
        public double TotalWaitTime { get; set; }
        public double ValueAddedTime { get; set; }
        public double NonValueAddedTime { get; set; }
        public double ProcessEfficiency { get; set; }
        public double FirstPassYield { get; set; }
        public int TotalWIP { get; set; }
        public double TaktTime { get; set; }
    }

    public class ImprovementOpportunity
    {
        public string OpportunityId { get; set; } = Guid.NewGuid().ToString();
        public string StepId { get; set; }
        public string Description { get; set; }
        public WasteType WasteType { get; set; }
        public double PotentialTimeSavings { get; set; }
        public double PotentialCostSavings { get; set; }
        public string Priority { get; set; }
        public string Owner { get; set; }
        public DateTime TargetDate { get; set; }
        public string Status { get; set; } = "Identified";
    }

    public class WasteIdentification
    {
        public string WasteId { get; set; } = Guid.NewGuid().ToString();
        public WasteType Type { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public double EstimatedImpact { get; set; }
        public string RootCause { get; set; }
        public string Countermeasure { get; set; }
    }

    public class PullPlanningSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string PhaseName { get; set; }
        public DateTime SessionDate { get; set; } = DateTime.UtcNow;
        public string Facilitator { get; set; }
        public List<string> Participants { get; set; } = new();
        public Milestone TargetMilestone { get; set; }
        public List<PullPlanningTask> Tasks { get; set; } = new();
        public List<string> IdentifiedConstraints { get; set; } = new();
        public double TotalDuration { get; set; }
        public double CriticalPathDuration { get; set; }
        public List<HandoffPoint> Handoffs { get; set; } = new();
        public List<ActionItem> ActionItems { get; set; } = new();
    }

    public class PullPlanningTask
    {
        public string TaskId { get; set; }
        public string TaskName { get; set; }
        public string ResponsibleParty { get; set; }
        public int Duration { get; set; }
        public List<string> Predecessors { get; set; } = new();
        public List<string> Successors { get; set; } = new();
        public int EarlyStart { get; set; }
        public int EarlyFinish { get; set; }
        public int LateStart { get; set; }
        public int LateFinish { get; set; }
        public int Float { get; set; }
        public bool IsCriticalPath { get; set; }
        public string StickyNoteColor { get; set; }
        public string Location { get; set; }
        public List<string> ResourcesNeeded { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
    }

    public class HandoffPoint
    {
        public string HandoffId { get; set; } = Guid.NewGuid().ToString();
        public string FromParty { get; set; }
        public string ToParty { get; set; }
        public string Deliverable { get; set; }
        public DateTime PlannedDate { get; set; }
        public List<string> Conditions { get; set; } = new();
        public bool IsConfirmed { get; set; }
    }

    public class ActionItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Open";
        public string Notes { get; set; }
    }

    public class LeanMetrics
    {
        public double CurrentPPC { get; set; }
        public double AveragePPC { get; set; }
        public double PPCTrend { get; set; }
        public int OpenConstraints { get; set; }
        public int ResolvedConstraintsThisWeek { get; set; }
        public double AverageConstraintAge { get; set; }
        public double TMR { get; set; }
        public double MakeReady { get; set; }
        public double PlanReliability { get; set; }
        public int ActiveLookaheadWeeks { get; set; } = 6;
        public double WorkableBacklog { get; set; }
        public int CommitmentsMade { get; set; }
        public int CommitmentsKept { get; set; }
        public double CommitmentReliability { get; set; }
        public Dictionary<string, double> PPCByTrade { get; set; } = new();
        public List<string> TopConstraintTypes { get; set; } = new();
        public List<string> TopVarianceReasons { get; set; } = new();
    }

    public class TeamMember
    {
        public string MemberId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Company { get; set; }
        public TeamRole Role { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public List<string> ResponsibleAreas { get; set; } = new();
        public double IndividualPPC { get; set; }
    }

    public class TaskCompletion
    {
        public string TaskId { get; set; }
        public bool WasComplete { get; set; }
        public VarianceReason? VarianceReason { get; set; }
        public string VarianceDescription { get; set; }
    }

    #endregion

    #region Engine

    public sealed class LeanConstructionIntelligenceEngine
    {
        private static readonly Lazy<LeanConstructionIntelligenceEngine> _instance =
            new(() => new LeanConstructionIntelligenceEngine());
        public static LeanConstructionIntelligenceEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly Dictionary<string, LeanProject> _projects = new();
        private readonly Random _random = new();

        private LeanConstructionIntelligenceEngine() { }

        public WeeklyWorkPlan CreateWeeklyPlan(LeanProject project, int weekNumber, int year, List<PlannedTask> tasks, List<string> attendees)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            DateTime weekStart = GetWeekStartDate(weekNumber, year);
            var plan = new WeeklyWorkPlan
            {
                WeekNumber = weekNumber,
                Year = year,
                WeekStartDate = weekStart,
                WeekEndDate = weekStart.AddDays(6),
                PlannedTasks = tasks ?? new List<PlannedTask>(),
                Attendees = attendees ?? new List<string>(),
                LookaheadConstraints = IdentifyLookaheadConstraints(project, weekStart, 6)
            };

            foreach (var task in plan.PlannedTasks)
            {
                task.CommittedDate = DateTime.UtcNow;
                var projectTask = FindTask(project, task.TaskId);
                if (projectTask != null) { projectTask.WillBeDone = true; projectTask.Status = LeanTaskStatus.Planned; }
            }

            lock (_lock) { project.WeeklyPlans ??= new List<WeeklyWorkPlan>(); project.WeeklyPlans.Add(plan); _projects[project.ProjectId] = project; }
            return plan;
        }

        public Constraint LogConstraint(LeanProject project, ConstraintType type, string description, string affectedTask, DateTime neededBy, string identifiedBy, ConstraintPriority priority = ConstraintPriority.Medium)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var constraint = new Constraint
            {
                Type = type,
                Description = description,
                AffectedTask = affectedTask,
                NeededByDate = neededBy,
                IdentifiedBy = identifiedBy,
                Priority = priority
            };

            lock (_lock)
            {
                project.Constraints ??= new List<Constraint>();
                project.Constraints.Add(constraint);
                var task = FindTask(project, affectedTask);
                if (task != null) { task.Constraints ??= new List<string>(); task.Constraints.Add(constraint.ConstraintId); task.IsReady = false; }
                _projects[project.ProjectId] = project;
            }
            return constraint;
        }

        public PPCRecord TrackPPC(LeanProject project, int weekNumber, int year, List<TaskCompletion> completions)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var plan = project.WeeklyPlans?.FirstOrDefault(p => p.WeekNumber == weekNumber && p.Year == year)
                ?? throw new InvalidOperationException($"No weekly plan for week {weekNumber}/{year}");

            int tasksPlanned = plan.PlannedTasks?.Count ?? 0;
            int tasksCompleted = 0;
            var variances = new Dictionary<VarianceReason, int>();
            var tradeCounts = new Dictionary<string, (int planned, int completed)>();

            foreach (var planned in plan.PlannedTasks ?? new List<PlannedTask>())
            {
                var completion = completions?.FirstOrDefault(c => c.TaskId == planned.TaskId);
                if (completion != null)
                {
                    planned.WasComplete = completion.WasComplete;
                    planned.VarianceReason = completion.VarianceReason;
                    planned.VarianceDescription = completion.VarianceDescription;

                    if (completion.WasComplete) tasksCompleted++;
                    else if (completion.VarianceReason.HasValue)
                    {
                        if (!variances.ContainsKey(completion.VarianceReason.Value)) variances[completion.VarianceReason.Value] = 0;
                        variances[completion.VarianceReason.Value]++;
                    }

                    string trade = planned.AssignedTo ?? "Unknown";
                    if (!tradeCounts.ContainsKey(trade)) tradeCounts[trade] = (0, 0);
                    var current = tradeCounts[trade];
                    tradeCounts[trade] = (current.planned + 1, current.completed + (completion.WasComplete ? 1 : 0));
                }
            }

            var ppcByTrade = new Dictionary<string, double>();
            foreach (var kvp in tradeCounts) ppcByTrade[kvp.Key] = kvp.Value.planned > 0 ? kvp.Value.completed * 100.0 / kvp.Value.planned : 0;

            double ppc = tasksPlanned > 0 ? tasksCompleted * 100.0 / tasksPlanned : 0;

            var record = new PPCRecord
            {
                WeekNumber = weekNumber,
                Year = year,
                WeekEndDate = plan.WeekEndDate,
                TasksPlanned = tasksPlanned,
                TasksCompleted = tasksCompleted,
                PPC = ppc,
                VarianceBreakdown = variances,
                PPCByTrade = ppcByTrade,
                TopVarianceReasons = variances.OrderByDescending(v => v.Value).Take(3).Select(v => v.Key.ToString()).ToList(),
                TMR = CalculateTMR(project),
                ImprovementActions = GenerateImprovementActions(variances)
            };

            record.RollingAverage4Week = CalculateRollingAverage(project, 4, record.PPC);
            record.RollingAverage8Week = CalculateRollingAverage(project, 8, record.PPC);

            lock (_lock)
            {
                plan.WeeklyPPC = record;
                project.PPCHistory ??= new List<PPCRecord>();
                project.PPCHistory.Add(record);
                UpdateProjectMetrics(project);
                _projects[project.ProjectId] = project;
            }
            return record;
        }

        public ValueStreamMap MapValueStream(LeanProject project, string processName, List<ProcessStep> steps)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var currentMetrics = CalculateValueStreamMetrics(steps);
            var wastes = IdentifyWastes(steps);
            var improvements = GenerateImprovements(steps, wastes);
            var futureSteps = OptimizeProcessSteps(steps, improvements);
            var futureMetrics = CalculateValueStreamMetrics(futureSteps);

            var map = new ValueStreamMap
            {
                ProcessName = processName,
                CurrentState = steps,
                FutureState = futureSteps,
                CurrentMetrics = currentMetrics,
                FutureMetrics = futureMetrics,
                Improvements = improvements,
                Wastes = wastes
            };

            lock (_lock) { project.ValueStream = map; _projects[project.ProjectId] = project; }
            return map;
        }

        public PullPlanningSession FacilitatePullPlanning(LeanProject project, string phaseName, Milestone targetMilestone, List<TeamMember> participants, List<PullPlanningTask> tasks, string facilitator)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            CalculateCriticalPath(tasks);
            var handoffs = IdentifyHandoffs(tasks, participants);
            var constraints = IdentifyPullPlanningConstraints(tasks);

            double criticalDuration = tasks?.Where(t => t.IsCriticalPath).Sum(t => t.Duration) ?? 0;
            double totalDuration = tasks?.Any() == true ? tasks.Max(t => t.LateFinish) : 0;

            var session = new PullPlanningSession
            {
                PhaseName = phaseName,
                Facilitator = facilitator,
                Participants = participants?.Select(p => p.Name).ToList() ?? new List<string>(),
                TargetMilestone = targetMilestone,
                Tasks = tasks ?? new List<PullPlanningTask>(),
                IdentifiedConstraints = constraints,
                TotalDuration = totalDuration,
                CriticalPathDuration = criticalDuration,
                Handoffs = handoffs,
                ActionItems = constraints.Select(c => new ActionItem
                {
                    Description = $"Resolve: {c}",
                    DueDate = DateTime.Now.AddDays(7)
                }).ToList()
            };

            lock (_lock)
            {
                project.PullPlanningSessions ??= new List<PullPlanningSession>();
                project.PullPlanningSessions.Add(session);
                _projects[project.ProjectId] = project;
            }
            return session;
        }

        public Constraint ResolveConstraint(LeanProject project, string constraintId, string resolutionNotes)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            lock (_lock)
            {
                var constraint = project.Constraints?.FirstOrDefault(c => c.ConstraintId == constraintId)
                    ?? throw new InvalidOperationException($"Constraint {constraintId} not found");

                constraint.Status = ConstraintStatus.Resolved;
                constraint.ResolvedDate = DateTime.UtcNow;
                constraint.ResolutionNotes = resolutionNotes;

                var task = FindTask(project, constraint.AffectedTask);
                if (task != null)
                {
                    task.Constraints?.Remove(constraintId);
                    if (task.Constraints?.Count == 0) task.IsReady = true;
                }

                UpdateProjectMetrics(project);
                _projects[project.ProjectId] = project;
                return constraint;
            }
        }

        public LeanMetrics GetProjectMetrics(LeanProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            UpdateProjectMetrics(project);
            return project.Metrics;
        }

        #region Private Methods

        private DateTime GetWeekStartDate(int week, int year)
        {
            DateTime jan1 = new(year, 1, 1);
            int offset = DayOfWeek.Monday - jan1.DayOfWeek;
            return jan1.AddDays(offset + (week - 1) * 7);
        }

        private LeanTask FindTask(LeanProject project, string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return null;
            foreach (var phase in project.Phases ?? new List<Phase>())
                foreach (var pkg in phase.WorkPackages ?? new List<WorkPackage>())
                {
                    var task = pkg.Tasks?.FirstOrDefault(t => t.TaskId == taskId);
                    if (task != null) return task;
                }
            return null;
        }

        private List<string> IdentifyLookaheadConstraints(LeanProject project, DateTime weekStart, int weeksAhead)
        {
            var constraints = new List<string>();
            DateTime end = weekStart.AddDays(weeksAhead * 7);

            var activeConstraints = project.Constraints?.Where(c => c.Status != ConstraintStatus.Resolved && c.NeededByDate <= end);
            if (activeConstraints != null)
                foreach (var c in activeConstraints)
                    constraints.Add($"{c.Type}: {c.Description}");

            return constraints;
        }

        private double CalculateTMR(LeanProject project)
        {
            int total = 0, ready = 0;
            foreach (var phase in project.Phases ?? new List<Phase>())
                foreach (var pkg in phase.WorkPackages ?? new List<WorkPackage>())
                {
                    var tasks = pkg.Tasks?.Where(t => t.Status == LeanTaskStatus.NotStarted || t.Status == LeanTaskStatus.Planned);
                    if (tasks != null) { total += tasks.Count(); ready += tasks.Count(t => t.IsReady); }
                }
            return total > 0 ? ready * 100.0 / total : 0;
        }

        private double CalculateRollingAverage(LeanProject project, int weeks, double currentPPC)
        {
            var recent = project.PPCHistory?.OrderByDescending(p => p.Year).ThenByDescending(p => p.WeekNumber).Take(weeks - 1).Select(p => p.PPC).ToList() ?? new List<double>();
            recent.Insert(0, currentPPC);
            return recent.Average();
        }

        private List<string> GenerateImprovementActions(Dictionary<VarianceReason, int> variances)
        {
            var actions = new List<string>();
            foreach (var v in variances.OrderByDescending(x => x.Value).Take(3))
            {
                string action = v.Key switch
                {
                    VarianceReason.Material => "Improve material ordering",
                    VarianceReason.Labor => "Review labor planning",
                    VarianceReason.Prerequisite => "Enhance constraint analysis",
                    VarianceReason.RFI => "Expedite RFI process",
                    VarianceReason.Design => "Improve change management",
                    VarianceReason.Weather => "Develop weather contingency",
                    VarianceReason.Coordination => "Increase coordination meetings",
                    _ => $"Address {v.Key} issues"
                };
                actions.Add(action);
            }
            return actions;
        }

        private void UpdateProjectMetrics(LeanProject project)
        {
            var latest = project.PPCHistory?.OrderByDescending(p => p.Year).ThenByDescending(p => p.WeekNumber).FirstOrDefault();
            int open = project.Constraints?.Count(c => c.Status != ConstraintStatus.Resolved) ?? 0;
            int resolved = project.Constraints?.Count(c => c.ResolvedDate.HasValue && c.ResolvedDate.Value >= DateTime.Now.AddDays(-7)) ?? 0;
            double avgAge = project.Constraints?.Where(c => c.Status != ConstraintStatus.Resolved).Select(c => (DateTime.Now - c.IdentifiedDate).TotalDays).DefaultIfEmpty(0).Average() ?? 0;

            project.Metrics = new LeanMetrics
            {
                CurrentPPC = latest?.PPC ?? 0,
                AveragePPC = project.PPCHistory?.Average(p => p.PPC) ?? 0,
                PPCTrend = CalculatePPCTrend(project),
                OpenConstraints = open,
                ResolvedConstraintsThisWeek = resolved,
                AverageConstraintAge = avgAge,
                TMR = CalculateTMR(project),
                MakeReady = CalculateTMR(project),
                PlanReliability = latest?.RollingAverage4Week ?? 0,
                WorkableBacklog = CalculateWorkableBacklog(project),
                CommitmentsMade = project.WeeklyPlans?.Sum(p => p.Commitments?.Count ?? 0) ?? 0,
                CommitmentsKept = project.WeeklyPlans?.Sum(p => p.Commitments?.Count(c => c.Status == LeanTaskStatus.Complete) ?? 0) ?? 0,
                TopConstraintTypes = project.Constraints?.Where(c => c.Status != ConstraintStatus.Resolved).GroupBy(c => c.Type).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key.ToString()).ToList() ?? new List<string>()
            };

            if (project.Metrics.CommitmentsMade > 0)
                project.Metrics.CommitmentReliability = project.Metrics.CommitmentsKept * 100.0 / project.Metrics.CommitmentsMade;
        }

        private double CalculatePPCTrend(LeanProject project)
        {
            var recent = project.PPCHistory?.OrderByDescending(p => p.Year).ThenByDescending(p => p.WeekNumber).Take(4).Select(p => p.PPC).ToList();
            if (recent == null || recent.Count < 2) return 0;
            double firstHalf = recent.Skip(recent.Count / 2).Average();
            double secondHalf = recent.Take(recent.Count / 2).Average();
            return secondHalf - firstHalf;
        }

        private double CalculateWorkableBacklog(LeanProject project)
        {
            int ready = 0;
            foreach (var phase in project.Phases ?? new List<Phase>())
                foreach (var pkg in phase.WorkPackages ?? new List<WorkPackage>())
                    ready += pkg.Tasks?.Count(t => t.IsReady && t.Status == LeanTaskStatus.NotStarted) ?? 0;
            return ready;
        }

        private ValueStreamMetrics CalculateValueStreamMetrics(List<ProcessStep> steps)
        {
            double leadTime = steps?.Sum(s => s.LeadTime) ?? 0;
            double processTime = steps?.Sum(s => s.ProcessTime) ?? 0;
            double waitTime = steps?.Sum(s => s.WaitTime) ?? 0;
            double valueAdded = steps?.Where(s => s.Category == ValueStreamCategory.ValueAdding).Sum(s => s.ProcessTime) ?? 0;

            return new ValueStreamMetrics
            {
                TotalLeadTime = leadTime,
                TotalProcessTime = processTime,
                TotalWaitTime = waitTime,
                ValueAddedTime = valueAdded,
                NonValueAddedTime = processTime - valueAdded + waitTime,
                ProcessEfficiency = leadTime > 0 ? valueAdded / leadTime * 100 : 0,
                FirstPassYield = steps?.Any() == true ? steps.Average(s => s.QualityRate) : 100,
                TotalWIP = steps?.Sum(s => s.WIPQuantity) ?? 0,
                TaktTime = steps?.Any() == true ? steps.Average(s => s.CycleTime) : 0
            };
        }

        private List<WasteIdentification> IdentifyWastes(List<ProcessStep> steps)
        {
            var wastes = new List<WasteIdentification>();
            foreach (var step in steps ?? new List<ProcessStep>())
            {
                if (step.WaitTime > step.ProcessTime)
                    wastes.Add(new WasteIdentification { Type = WasteType.Waiting, Description = $"Excessive waiting at {step.StepName}", Location = step.StepName, EstimatedImpact = step.WaitTime });
                if (step.WIPQuantity > step.BatchSize * 2)
                    wastes.Add(new WasteIdentification { Type = WasteType.Inventory, Description = $"Excess WIP at {step.StepName}", Location = step.StepName, EstimatedImpact = step.WIPQuantity - step.BatchSize });
                if (step.QualityRate < 95)
                    wastes.Add(new WasteIdentification { Type = WasteType.Defects, Description = $"Quality issues at {step.StepName}", Location = step.StepName, EstimatedImpact = 100 - step.QualityRate });
            }
            return wastes;
        }

        private List<ImprovementOpportunity> GenerateImprovements(List<ProcessStep> steps, List<WasteIdentification> wastes)
        {
            var improvements = wastes.Select(w => new ImprovementOpportunity
            {
                StepId = steps?.FirstOrDefault(s => s.StepName == w.Location)?.StepId,
                Description = $"Eliminate {w.Type}: {w.Description}",
                WasteType = w.Type,
                PotentialTimeSavings = w.EstimatedImpact,
                Priority = w.EstimatedImpact > 10 ? "High" : "Medium"
            }).ToList();

            var bottlenecks = steps?.OrderByDescending(s => s.CycleTime).Take(2);
            if (bottlenecks != null)
                foreach (var b in bottlenecks)
                    improvements.Add(new ImprovementOpportunity
                    {
                        StepId = b.StepId,
                        Description = $"Reduce cycle time at bottleneck: {b.StepName}",
                        WasteType = WasteType.Waiting,
                        PotentialTimeSavings = b.CycleTime * 0.2,
                        Priority = "High"
                    });

            return improvements;
        }

        private List<ProcessStep> OptimizeProcessSteps(List<ProcessStep> current, List<ImprovementOpportunity> improvements)
        {
            return current?.Select(s =>
            {
                var optimized = new ProcessStep
                {
                    StepId = s.StepId,
                    StepName = s.StepName,
                    Sequence = s.Sequence,
                    ResponsibleParty = s.ResponsibleParty,
                    CycleTime = s.CycleTime,
                    WaitTime = s.WaitTime,
                    ProcessTime = s.ProcessTime,
                    LeadTime = s.LeadTime,
                    Category = s.Category,
                    WIPQuantity = s.WIPQuantity,
                    BatchSize = s.BatchSize,
                    QualityRate = s.QualityRate
                };

                foreach (var imp in improvements.Where(i => i.StepId == s.StepId))
                {
                    if (imp.WasteType == WasteType.Waiting) { optimized.WaitTime *= 0.5; optimized.CycleTime *= 0.8; }
                    if (imp.WasteType == WasteType.Inventory) optimized.WIPQuantity = optimized.BatchSize;
                    if (imp.WasteType == WasteType.Defects) optimized.QualityRate = Math.Min(99, optimized.QualityRate + 3);
                }

                optimized.LeadTime = optimized.ProcessTime + optimized.WaitTime;
                optimized.ValueAddedRatio = optimized.LeadTime > 0 ? optimized.ProcessTime / optimized.LeadTime : 1;
                return optimized;
            }).ToList() ?? new List<ProcessStep>();
        }

        private void CalculateCriticalPath(List<PullPlanningTask> tasks)
        {
            if (tasks == null || !tasks.Any()) return;
            var dict = tasks.ToDictionary(t => t.TaskId);

            foreach (var task in tasks.OrderBy(t => t.Predecessors?.Count ?? 0))
            {
                int earliest = 0;
                foreach (var pred in task.Predecessors ?? new List<string>())
                    if (dict.TryGetValue(pred, out var p)) earliest = Math.Max(earliest, p.EarlyFinish);
                task.EarlyStart = earliest;
                task.EarlyFinish = task.EarlyStart + task.Duration;
            }

            int projectEnd = tasks.Max(t => t.EarlyFinish);

            foreach (var task in tasks.OrderByDescending(t => t.EarlyFinish))
            {
                var successors = tasks.Where(t => t.Predecessors?.Contains(task.TaskId) == true);
                task.LateFinish = successors.Any() ? successors.Min(s => s.LateStart) : projectEnd;
                task.LateStart = task.LateFinish - task.Duration;
                task.Float = task.LateStart - task.EarlyStart;
                task.IsCriticalPath = task.Float == 0;
            }
        }

        private List<HandoffPoint> IdentifyHandoffs(List<PullPlanningTask> tasks, List<TeamMember> participants)
        {
            var handoffs = new List<HandoffPoint>();
            if (tasks == null) return handoffs;

            foreach (var task in tasks)
            {
                var successors = tasks.Where(t => t.Predecessors?.Contains(task.TaskId) == true);
                foreach (var succ in successors)
                    if (task.ResponsibleParty != succ.ResponsibleParty)
                        handoffs.Add(new HandoffPoint
                        {
                            FromParty = task.ResponsibleParty,
                            ToParty = succ.ResponsibleParty,
                            Deliverable = $"Completion of {task.TaskName}",
                            Conditions = new List<string> { "Work complete", "Quality verified" }
                        });
            }
            return handoffs;
        }

        private List<string> IdentifyPullPlanningConstraints(List<PullPlanningTask> tasks)
        {
            var constraints = new List<string>();
            if (tasks == null) return constraints;

            foreach (var task in tasks)
                foreach (var c in task.Constraints ?? new List<string>())
                    constraints.Add($"{task.TaskName}: {c}");

            int criticalCount = tasks.Count(t => t.IsCriticalPath);
            if (criticalCount > tasks.Count * 0.5)
                constraints.Add("High percentage on critical path - limited flexibility");

            return constraints;
        }

        #endregion
    }

    #endregion
}
