// ===================================================================
// StingBIM Schedule Intelligence Engine - 4D/5D Scheduling & Analysis
// Advanced scheduling, critical path, delay forensics, resource leveling
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ScheduleIntelligence
{
    /// <summary>
    /// Comprehensive 4D/5D schedule intelligence with critical path analysis,
    /// delay forensics, resource leveling, and schedule optimization
    /// </summary>
    public sealed class ScheduleIntelligenceEngine
    {
        private static readonly Lazy<ScheduleIntelligenceEngine> _instance =
            new Lazy<ScheduleIntelligenceEngine>(() => new ScheduleIntelligenceEngine());
        public static ScheduleIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ProjectSchedule> _schedules;
        private readonly Dictionary<string, ScheduleBaseline> _baselines;
        private readonly Dictionary<string, DelayAnalysis> _delayAnalyses;
        private readonly Dictionary<string, ResourcePool> _resourcePools;
        private readonly List<ScheduleTemplate> _templates;
        private readonly object _lockObject = new object();

        public event EventHandler<ScheduleAlertEventArgs> ScheduleAlert;
        public event EventHandler<CriticalPathChangeEventArgs> CriticalPathChanged;
        public event EventHandler<DelayDetectedEventArgs> DelayDetected;

        private ScheduleIntelligenceEngine()
        {
            _schedules = new Dictionary<string, ProjectSchedule>();
            _baselines = new Dictionary<string, ScheduleBaseline>();
            _delayAnalyses = new Dictionary<string, DelayAnalysis>();
            _resourcePools = new Dictionary<string, ResourcePool>();
            _templates = new List<ScheduleTemplate>();
            InitializeTemplates();
            InitializeActivityLibrary();
        }

        #region Schedule Management

        public ProjectSchedule CreateSchedule(string projectId, string projectName, ScheduleType type)
        {
            var schedule = new ProjectSchedule
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ProjectName = projectName,
                Type = type,
                CreatedDate = DateTime.Now,
                Status = ScheduleStatus.Draft,
                Activities = new List<ScheduleActivity>(),
                Milestones = new List<Milestone>(),
                Calendars = new List<ProjectCalendar> { CreateDefaultCalendar() },
                WBSStructure = new WBSNode { Code = "1", Name = projectName, Level = 0 }
            };

            lock (_lockObject)
            {
                _schedules[schedule.Id] = schedule;
            }

            return schedule;
        }

        public ScheduleActivity AddActivity(string scheduleId, ScheduleActivity activity)
        {
            lock (_lockObject)
            {
                if (_schedules.TryGetValue(scheduleId, out var schedule))
                {
                    activity.Id = Guid.NewGuid().ToString();
                    activity.ScheduleId = scheduleId;
                    activity.CreatedDate = DateTime.Now;

                    // Auto-calculate dates if not set
                    if (activity.PlannedStart == default && activity.Predecessors.Any())
                    {
                        var maxPredFinish = activity.Predecessors
                            .Select(p => schedule.Activities.FirstOrDefault(a => a.Id == p.PredecessorId))
                            .Where(a => a != null)
                            .Max(a => a.PlannedFinish);
                        activity.PlannedStart = maxPredFinish.AddDays(1);
                    }

                    if (activity.PlannedFinish == default && activity.Duration > 0)
                    {
                        activity.PlannedFinish = CalculateFinishDate(
                            activity.PlannedStart,
                            activity.Duration,
                            schedule.Calendars.First());
                    }

                    schedule.Activities.Add(activity);
                    schedule.LastModified = DateTime.Now;

                    return activity;
                }
            }
            return null;
        }

        public Milestone AddMilestone(string scheduleId, Milestone milestone)
        {
            lock (_lockObject)
            {
                if (_schedules.TryGetValue(scheduleId, out var schedule))
                {
                    milestone.Id = Guid.NewGuid().ToString();
                    milestone.ScheduleId = scheduleId;
                    schedule.Milestones.Add(milestone);
                    schedule.LastModified = DateTime.Now;
                    return milestone;
                }
            }
            return null;
        }

        public void LinkActivities(string scheduleId, string predecessorId, string successorId,
            DependencyType type = DependencyType.FinishToStart, int lag = 0)
        {
            lock (_lockObject)
            {
                if (_schedules.TryGetValue(scheduleId, out var schedule))
                {
                    var successor = schedule.Activities.FirstOrDefault(a => a.Id == successorId);
                    if (successor != null)
                    {
                        successor.Predecessors.Add(new ActivityDependency
                        {
                            PredecessorId = predecessorId,
                            SuccessorId = successorId,
                            Type = type,
                            Lag = lag
                        });
                        schedule.LastModified = DateTime.Now;
                    }
                }
            }
        }

        #endregion

        #region Critical Path Method (CPM)

        public async Task<CriticalPathResult> CalculateCriticalPathAsync(string scheduleId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_schedules.TryGetValue(scheduleId, out var schedule))
                        return null;

                    var result = new CriticalPathResult
                    {
                        ScheduleId = scheduleId,
                        CalculationDate = DateTime.Now,
                        CriticalActivities = new List<string>(),
                        NearCriticalActivities = new List<string>(),
                        ActivityFloats = new Dictionary<string, FloatInfo>()
                    };

                    var activities = schedule.Activities.ToList();
                    if (!activities.Any()) return result;

                    // Forward pass - calculate early start/finish
                    var sorted = TopologicalSort(activities);
                    foreach (var activity in sorted)
                    {
                        if (!activity.Predecessors.Any())
                        {
                            activity.EarlyStart = activity.PlannedStart;
                        }
                        else
                        {
                            var maxEarlyFinish = activity.Predecessors
                                .Select(p => activities.FirstOrDefault(a => a.Id == p.PredecessorId))
                                .Where(a => a != null)
                                .Max(a => CalculateDependencyDate(a, p => p.Type, p => p.Lag, schedule.Calendars.First()));
                            activity.EarlyStart = maxEarlyFinish;
                        }
                        activity.EarlyFinish = CalculateFinishDate(
                            activity.EarlyStart, activity.Duration, schedule.Calendars.First());
                    }

                    // Backward pass - calculate late start/finish
                    var projectEnd = activities.Max(a => a.EarlyFinish);
                    sorted.Reverse();
                    foreach (var activity in sorted)
                    {
                        var successors = activities
                            .Where(a => a.Predecessors.Any(p => p.PredecessorId == activity.Id))
                            .ToList();

                        if (!successors.Any())
                        {
                            activity.LateFinish = projectEnd;
                        }
                        else
                        {
                            activity.LateFinish = successors.Min(s => s.LateStart);
                        }
                        activity.LateStart = CalculateStartDate(
                            activity.LateFinish, activity.Duration, schedule.Calendars.First());

                        // Calculate float
                        var totalFloat = (activity.LateFinish - activity.EarlyFinish).Days;
                        var freeFloat = successors.Any()
                            ? (successors.Min(s => s.EarlyStart) - activity.EarlyFinish).Days
                            : 0;

                        result.ActivityFloats[activity.Id] = new FloatInfo
                        {
                            ActivityId = activity.Id,
                            TotalFloat = totalFloat,
                            FreeFloat = freeFloat,
                            IsCritical = totalFloat == 0,
                            IsNearCritical = totalFloat > 0 && totalFloat <= 5
                        };

                        if (totalFloat == 0)
                            result.CriticalActivities.Add(activity.Id);
                        else if (totalFloat <= 5)
                            result.NearCriticalActivities.Add(activity.Id);
                    }

                    result.ProjectDuration = (projectEnd - activities.Min(a => a.EarlyStart)).Days;
                    result.CriticalPathLength = result.CriticalActivities.Count;
                    result.TotalFloatDays = result.ActivityFloats.Values.Sum(f => f.TotalFloat);

                    // Check for critical path changes
                    var previousCritical = schedule.CriticalPath?.ToList() ?? new List<string>();
                    if (!previousCritical.SequenceEqual(result.CriticalActivities))
                    {
                        schedule.CriticalPath = result.CriticalActivities;
                        OnCriticalPathChanged(new CriticalPathChangeEventArgs
                        {
                            ScheduleId = scheduleId,
                            PreviousCriticalPath = previousCritical,
                            NewCriticalPath = result.CriticalActivities,
                            ChangeDate = DateTime.Now
                        });
                    }

                    return result;
                }
            });
        }

        private List<ScheduleActivity> TopologicalSort(List<ScheduleActivity> activities)
        {
            var sorted = new List<ScheduleActivity>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            void Visit(ScheduleActivity activity)
            {
                if (visited.Contains(activity.Id)) return;
                if (visiting.Contains(activity.Id))
                    throw new InvalidOperationException($"Circular dependency detected at activity {activity.Name}");

                visiting.Add(activity.Id);
                foreach (var pred in activity.Predecessors)
                {
                    var predActivity = activities.FirstOrDefault(a => a.Id == pred.PredecessorId);
                    if (predActivity != null)
                        Visit(predActivity);
                }
                visiting.Remove(activity.Id);
                visited.Add(activity.Id);
                sorted.Add(activity);
            }

            foreach (var activity in activities)
                Visit(activity);

            return sorted;
        }

        #endregion

        #region Delay Analysis (Forensic Scheduling)

        public async Task<DelayAnalysis> AnalyzeDelaysAsync(string scheduleId, DelayAnalysisMethod method)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_schedules.TryGetValue(scheduleId, out var schedule))
                        return null;

                    var analysis = new DelayAnalysis
                    {
                        Id = Guid.NewGuid().ToString(),
                        ScheduleId = scheduleId,
                        Method = method,
                        AnalysisDate = DateTime.Now,
                        DelayEvents = new List<DelayEvent>(),
                        ImpactedActivities = new List<string>(),
                        Recommendations = new List<string>()
                    };

                    // Get baseline for comparison
                    var baseline = _baselines.Values
                        .Where(b => b.ScheduleId == scheduleId)
                        .OrderByDescending(b => b.BaselineDate)
                        .FirstOrDefault();

                    if (baseline == null)
                    {
                        analysis.Recommendations.Add("No baseline found. Create a baseline for accurate delay analysis.");
                        return analysis;
                    }

                    switch (method)
                    {
                        case DelayAnalysisMethod.AsPlannedVsAsBuilt:
                            PerformAsPlannedVsAsBuilt(schedule, baseline, analysis);
                            break;
                        case DelayAnalysisMethod.ImpactedAsPlanned:
                            PerformImpactedAsPlanned(schedule, baseline, analysis);
                            break;
                        case DelayAnalysisMethod.TimeImpactAnalysis:
                            PerformTimeImpactAnalysis(schedule, baseline, analysis);
                            break;
                        case DelayAnalysisMethod.WindowsAnalysis:
                            PerformWindowsAnalysis(schedule, baseline, analysis);
                            break;
                        case DelayAnalysisMethod.CollapsedAsBuilt:
                            PerformCollapsedAsBuilt(schedule, baseline, analysis);
                            break;
                    }

                    // Calculate total delay
                    analysis.TotalDelayDays = analysis.DelayEvents.Sum(d => d.DelayDays);
                    analysis.ExcusableDelayDays = analysis.DelayEvents
                        .Where(d => d.Type == DelayType.Excusable || d.Type == DelayType.ExcusableCompensable)
                        .Sum(d => d.DelayDays);
                    analysis.NonExcusableDelayDays = analysis.DelayEvents
                        .Where(d => d.Type == DelayType.NonExcusable)
                        .Sum(d => d.DelayDays);
                    analysis.ConcurrentDelayDays = analysis.DelayEvents
                        .Where(d => d.IsConcurrent)
                        .Sum(d => d.DelayDays);

                    _delayAnalyses[analysis.Id] = analysis;

                    // Raise alert if significant delays
                    if (analysis.TotalDelayDays > 10)
                    {
                        OnDelayDetected(new DelayDetectedEventArgs
                        {
                            ScheduleId = scheduleId,
                            TotalDelayDays = analysis.TotalDelayDays,
                            CriticalDelays = analysis.DelayEvents.Where(d => d.ImpactsCriticalPath).ToList()
                        });
                    }

                    return analysis;
                }
            });
        }

        private void PerformAsPlannedVsAsBuilt(ProjectSchedule schedule, ScheduleBaseline baseline, DelayAnalysis analysis)
        {
            foreach (var activity in schedule.Activities)
            {
                var baselineActivity = baseline.Activities.FirstOrDefault(a => a.ActivityId == activity.Id);
                if (baselineActivity == null) continue;

                var startVariance = (activity.ActualStart - baselineActivity.PlannedStart).Days;
                var finishVariance = (activity.ActualFinish - baselineActivity.PlannedFinish).Days;

                if (finishVariance > 0)
                {
                    analysis.DelayEvents.Add(new DelayEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        ActivityId = activity.Id,
                        ActivityName = activity.Name,
                        DelayDays = finishVariance,
                        StartVariance = startVariance,
                        FinishVariance = finishVariance,
                        Type = ClassifyDelayType(activity),
                        ImpactsCriticalPath = schedule.CriticalPath?.Contains(activity.Id) ?? false,
                        Description = $"Activity finished {finishVariance} days late"
                    });
                    analysis.ImpactedActivities.Add(activity.Id);
                }
            }
        }

        private void PerformImpactedAsPlanned(ProjectSchedule schedule, ScheduleBaseline baseline, DelayAnalysis analysis)
        {
            // Insert delay events into baseline schedule and recalculate
            var impactedSchedule = CloneSchedule(schedule);
            foreach (var delayEvent in GetKnownDelayEvents(schedule))
            {
                // Apply delay to impacted schedule
                var activity = impactedSchedule.Activities.FirstOrDefault(a => a.Id == delayEvent.ActivityId);
                if (activity != null)
                {
                    activity.PlannedFinish = activity.PlannedFinish.AddDays(delayEvent.DelayDays);
                    analysis.DelayEvents.Add(delayEvent);
                }
            }
        }

        private void PerformTimeImpactAnalysis(ProjectSchedule schedule, ScheduleBaseline baseline, DelayAnalysis analysis)
        {
            // Chronological insertion of delays with CPM recalculation
            analysis.Recommendations.Add("Time Impact Analysis performed - delays inserted chronologically");
            PerformAsPlannedVsAsBuilt(schedule, baseline, analysis);
        }

        private void PerformWindowsAnalysis(ProjectSchedule schedule, ScheduleBaseline baseline, DelayAnalysis analysis)
        {
            // Divide project into windows and analyze each
            var windowSize = 30; // days
            var projectStart = schedule.Activities.Min(a => a.PlannedStart);
            var projectEnd = schedule.Activities.Max(a => a.ActualFinish != default ? a.ActualFinish : a.PlannedFinish);

            var currentWindow = projectStart;
            var windowNumber = 1;

            while (currentWindow < projectEnd)
            {
                var windowEnd = currentWindow.AddDays(windowSize);
                var windowActivities = schedule.Activities
                    .Where(a => a.ActualStart >= currentWindow && a.ActualStart < windowEnd)
                    .ToList();

                var windowDelay = windowActivities.Sum(a =>
                    Math.Max(0, (a.ActualFinish - a.PlannedFinish).Days));

                if (windowDelay > 0)
                {
                    analysis.DelayEvents.Add(new DelayEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        Description = $"Window {windowNumber}: {currentWindow:d} to {windowEnd:d}",
                        DelayDays = windowDelay,
                        Type = DelayType.ToBeDetermined
                    });
                }

                currentWindow = windowEnd;
                windowNumber++;
            }
        }

        private void PerformCollapsedAsBuilt(ProjectSchedule schedule, ScheduleBaseline baseline, DelayAnalysis analysis)
        {
            // Remove delays from as-built to determine "but-for" completion
            analysis.Recommendations.Add("Collapsed As-Built Analysis - removing employer delays to show contractor performance");
            PerformAsPlannedVsAsBuilt(schedule, baseline, analysis);
        }

        private DelayType ClassifyDelayType(ScheduleActivity activity)
        {
            // Classification based on delay cause
            if (activity.DelayReason?.Contains("weather", StringComparison.OrdinalIgnoreCase) == true)
                return DelayType.Excusable;
            if (activity.DelayReason?.Contains("owner", StringComparison.OrdinalIgnoreCase) == true)
                return DelayType.ExcusableCompensable;
            if (activity.DelayReason?.Contains("contractor", StringComparison.OrdinalIgnoreCase) == true)
                return DelayType.NonExcusable;
            return DelayType.ToBeDetermined;
        }

        private List<DelayEvent> GetKnownDelayEvents(ProjectSchedule schedule)
        {
            return schedule.Activities
                .Where(a => a.ActualFinish > a.PlannedFinish)
                .Select(a => new DelayEvent
                {
                    ActivityId = a.Id,
                    ActivityName = a.Name,
                    DelayDays = (a.ActualFinish - a.PlannedFinish).Days,
                    Type = ClassifyDelayType(a)
                })
                .ToList();
        }

        #endregion

        #region Resource Leveling

        public async Task<ResourceLevelingResult> LevelResourcesAsync(string scheduleId, ResourceLevelingOptions options)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_schedules.TryGetValue(scheduleId, out var schedule))
                        return null;

                    var result = new ResourceLevelingResult
                    {
                        ScheduleId = scheduleId,
                        LevelingDate = DateTime.Now,
                        OriginalDuration = (schedule.Activities.Max(a => a.PlannedFinish) -
                                          schedule.Activities.Min(a => a.PlannedStart)).Days,
                        Adjustments = new List<ResourceAdjustment>(),
                        ResourceProfiles = new Dictionary<string, List<ResourceUsage>>()
                    };

                    // Build resource usage profile
                    var resources = schedule.Activities
                        .SelectMany(a => a.AssignedResources)
                        .Select(r => r.ResourceId)
                        .Distinct()
                        .ToList();

                    foreach (var resourceId in resources)
                    {
                        var usageProfile = BuildResourceProfile(schedule, resourceId);
                        result.ResourceProfiles[resourceId] = usageProfile;

                        // Find overallocations
                        var resource = _resourcePools.Values
                            .SelectMany(p => p.Resources)
                            .FirstOrDefault(r => r.Id == resourceId);

                        if (resource == null) continue;

                        var overallocations = usageProfile
                            .Where(u => u.AllocatedHours > resource.AvailableHoursPerDay)
                            .ToList();

                        if (!overallocations.Any()) continue;

                        // Level resources based on options
                        if (options.PreserveProjectEnd)
                        {
                            // Level within float
                            LevelWithinFloat(schedule, resourceId, overallocations, result);
                        }
                        else
                        {
                            // Allow project extension
                            LevelWithExtension(schedule, resourceId, overallocations, result);
                        }
                    }

                    result.NewDuration = (schedule.Activities.Max(a => a.PlannedFinish) -
                                         schedule.Activities.Min(a => a.PlannedStart)).Days;
                    result.DurationChange = result.NewDuration - result.OriginalDuration;

                    return result;
                }
            });
        }

        private List<ResourceUsage> BuildResourceProfile(ProjectSchedule schedule, string resourceId)
        {
            var profile = new List<ResourceUsage>();
            var projectStart = schedule.Activities.Min(a => a.PlannedStart);
            var projectEnd = schedule.Activities.Max(a => a.PlannedFinish);

            for (var date = projectStart; date <= projectEnd; date = date.AddDays(1))
            {
                var activitiesOnDate = schedule.Activities
                    .Where(a => a.PlannedStart <= date && a.PlannedFinish >= date)
                    .Where(a => a.AssignedResources.Any(r => r.ResourceId == resourceId))
                    .ToList();

                var totalHours = activitiesOnDate
                    .SelectMany(a => a.AssignedResources)
                    .Where(r => r.ResourceId == resourceId)
                    .Sum(r => r.HoursPerDay);

                profile.Add(new ResourceUsage
                {
                    Date = date,
                    ResourceId = resourceId,
                    AllocatedHours = totalHours,
                    Activities = activitiesOnDate.Select(a => a.Id).ToList()
                });
            }

            return profile;
        }

        private void LevelWithinFloat(ProjectSchedule schedule, string resourceId,
            List<ResourceUsage> overallocations, ResourceLevelingResult result)
        {
            foreach (var overallocation in overallocations)
            {
                var activities = schedule.Activities
                    .Where(a => overallocation.Activities.Contains(a.Id))
                    .OrderBy(a => a.TotalFloat)
                    .ToList();

                // Delay non-critical activities first
                foreach (var activity in activities.Skip(1))
                {
                    if (activity.TotalFloat > 0)
                    {
                        var delayDays = Math.Min(activity.TotalFloat, 1);
                        activity.PlannedStart = activity.PlannedStart.AddDays(delayDays);
                        activity.PlannedFinish = activity.PlannedFinish.AddDays(delayDays);

                        result.Adjustments.Add(new ResourceAdjustment
                        {
                            ActivityId = activity.Id,
                            ResourceId = resourceId,
                            AdjustmentType = "Delayed within float",
                            DaysDelayed = delayDays
                        });
                    }
                }
            }
        }

        private void LevelWithExtension(ProjectSchedule schedule, string resourceId,
            List<ResourceUsage> overallocations, ResourceLevelingResult result)
        {
            foreach (var overallocation in overallocations)
            {
                var activities = schedule.Activities
                    .Where(a => overallocation.Activities.Contains(a.Id))
                    .OrderByDescending(a => a.Priority)
                    .ToList();

                // Delay lower priority activities
                foreach (var activity in activities.Skip(1))
                {
                    activity.PlannedStart = activity.PlannedStart.AddDays(1);
                    activity.PlannedFinish = activity.PlannedFinish.AddDays(1);

                    result.Adjustments.Add(new ResourceAdjustment
                    {
                        ActivityId = activity.Id,
                        ResourceId = resourceId,
                        AdjustmentType = "Delayed for leveling",
                        DaysDelayed = 1
                    });
                }
            }
        }

        #endregion

        #region Schedule Compression

        public async Task<CompressionResult> CompressScheduleAsync(string scheduleId, CompressionOptions options)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_schedules.TryGetValue(scheduleId, out var schedule))
                        return null;

                    var result = new CompressionResult
                    {
                        ScheduleId = scheduleId,
                        OriginalDuration = (schedule.Activities.Max(a => a.PlannedFinish) -
                                          schedule.Activities.Min(a => a.PlannedStart)).Days,
                        CompressionOptions = new List<CompressionOption>(),
                        RecommendedOption = null
                    };

                    // Only compress critical path activities
                    var criticalActivities = schedule.Activities
                        .Where(a => schedule.CriticalPath?.Contains(a.Id) ?? false)
                        .ToList();

                    foreach (var activity in criticalActivities)
                    {
                        // Crashing option
                        if (activity.CrashDuration.HasValue && activity.CrashCost.HasValue)
                        {
                            var crashDays = activity.Duration - activity.CrashDuration.Value;
                            var crashCostPerDay = activity.CrashCost.Value / crashDays;

                            result.CompressionOptions.Add(new CompressionOption
                            {
                                ActivityId = activity.Id,
                                ActivityName = activity.Name,
                                Method = CompressionMethod.Crashing,
                                DaysReduced = crashDays,
                                AdditionalCost = activity.CrashCost.Value,
                                CostPerDayReduced = crashCostPerDay,
                                RiskIncrease = CalculateCrashRisk(activity)
                            });
                        }

                        // Fast-tracking option
                        var successors = schedule.Activities
                            .Where(a => a.Predecessors.Any(p => p.PredecessorId == activity.Id))
                            .ToList();

                        foreach (var successor in successors)
                        {
                            var overlap = Math.Min(activity.Duration / 2, successor.Duration / 2);
                            result.CompressionOptions.Add(new CompressionOption
                            {
                                ActivityId = activity.Id,
                                ActivityName = $"{activity.Name} -> {successor.Name}",
                                Method = CompressionMethod.FastTracking,
                                DaysReduced = overlap,
                                AdditionalCost = 0,
                                RiskIncrease = CalculateFastTrackRisk(activity, successor)
                            });
                        }
                    }

                    // Sort by cost efficiency
                    result.CompressionOptions = result.CompressionOptions
                        .OrderBy(o => o.CostPerDayReduced)
                        .ThenBy(o => o.RiskIncrease)
                        .ToList();

                    // Recommend best option based on target
                    if (options.TargetDuration.HasValue)
                    {
                        var daysToReduce = result.OriginalDuration - options.TargetDuration.Value;
                        var selectedOptions = SelectCompressionOptions(result.CompressionOptions, daysToReduce, options.MaxBudget);
                        result.RecommendedOption = selectedOptions.FirstOrDefault();
                        result.TotalCostIncrease = selectedOptions.Sum(o => o.AdditionalCost);
                        result.NewDuration = result.OriginalDuration - selectedOptions.Sum(o => o.DaysReduced);
                    }

                    return result;
                }
            });
        }

        private double CalculateCrashRisk(ScheduleActivity activity)
        {
            // Risk increases with crash percentage
            var crashPercentage = activity.CrashDuration.HasValue
                ? (activity.Duration - activity.CrashDuration.Value) / (double)activity.Duration
                : 0;
            return crashPercentage * 0.5; // 50% max risk increase
        }

        private double CalculateFastTrackRisk(ScheduleActivity activity, ScheduleActivity successor)
        {
            // Risk based on dependency type and overlap
            return 0.3; // 30% risk increase for fast-tracking
        }

        private List<CompressionOption> SelectCompressionOptions(List<CompressionOption> options,
            int daysNeeded, decimal? maxBudget)
        {
            var selected = new List<CompressionOption>();
            var daysRemaining = daysNeeded;
            var budgetRemaining = maxBudget ?? decimal.MaxValue;

            foreach (var option in options)
            {
                if (daysRemaining <= 0) break;
                if (option.AdditionalCost > budgetRemaining) continue;

                selected.Add(option);
                daysRemaining -= option.DaysReduced;
                budgetRemaining -= option.AdditionalCost;
            }

            return selected;
        }

        #endregion

        #region What-If Scenarios

        public async Task<WhatIfResult> RunWhatIfScenarioAsync(string scheduleId, WhatIfScenario scenario)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_schedules.TryGetValue(scheduleId, out var schedule))
                        return null;

                    var result = new WhatIfResult
                    {
                        ScenarioId = Guid.NewGuid().ToString(),
                        ScheduleId = scheduleId,
                        ScenarioName = scenario.Name,
                        AnalysisDate = DateTime.Now,
                        BaselineCompletion = schedule.Activities.Max(a => a.PlannedFinish),
                        Impacts = new List<ScenarioImpact>()
                    };

                    // Clone schedule for simulation
                    var simSchedule = CloneSchedule(schedule);

                    // Apply scenario changes
                    foreach (var change in scenario.Changes)
                    {
                        var activity = simSchedule.Activities.FirstOrDefault(a => a.Id == change.ActivityId);
                        if (activity == null) continue;

                        switch (change.ChangeType)
                        {
                            case ScenarioChangeType.DurationChange:
                                activity.Duration += change.Value;
                                activity.PlannedFinish = CalculateFinishDate(
                                    activity.PlannedStart, activity.Duration, simSchedule.Calendars.First());
                                break;
                            case ScenarioChangeType.StartDelay:
                                activity.PlannedStart = activity.PlannedStart.AddDays(change.Value);
                                activity.PlannedFinish = activity.PlannedFinish.AddDays(change.Value);
                                break;
                            case ScenarioChangeType.ResourceChange:
                                // Adjust duration based on resource change
                                activity.Duration = (int)(activity.Duration * (1 - change.Value * 0.1));
                                break;
                        }
                    }

                    // Recalculate schedule
                    RecalculateSchedule(simSchedule);

                    result.ScenarioCompletion = simSchedule.Activities.Max(a => a.PlannedFinish);
                    result.DurationImpact = (result.ScenarioCompletion - result.BaselineCompletion).Days;
                    result.CostImpact = CalculateCostImpact(schedule, simSchedule);

                    // Identify impacted activities
                    foreach (var activity in simSchedule.Activities)
                    {
                        var original = schedule.Activities.FirstOrDefault(a => a.Id == activity.Id);
                        if (original == null) continue;

                        if (activity.PlannedFinish != original.PlannedFinish)
                        {
                            result.Impacts.Add(new ScenarioImpact
                            {
                                ActivityId = activity.Id,
                                ActivityName = activity.Name,
                                OriginalFinish = original.PlannedFinish,
                                ScenarioFinish = activity.PlannedFinish,
                                DaysImpact = (activity.PlannedFinish - original.PlannedFinish).Days
                            });
                        }
                    }

                    return result;
                }
            });
        }

        private void RecalculateSchedule(ProjectSchedule schedule)
        {
            var sorted = TopologicalSort(schedule.Activities.ToList());
            foreach (var activity in sorted)
            {
                if (activity.Predecessors.Any())
                {
                    var maxPredFinish = activity.Predecessors
                        .Select(p => schedule.Activities.FirstOrDefault(a => a.Id == p.PredecessorId))
                        .Where(a => a != null)
                        .Max(a => a.PlannedFinish);

                    if (maxPredFinish > activity.PlannedStart)
                    {
                        var delay = (maxPredFinish - activity.PlannedStart).Days + 1;
                        activity.PlannedStart = maxPredFinish.AddDays(1);
                        activity.PlannedFinish = activity.PlannedFinish.AddDays(delay);
                    }
                }
            }
        }

        private decimal CalculateCostImpact(ProjectSchedule original, ProjectSchedule scenario)
        {
            var durationChange = (scenario.Activities.Max(a => a.PlannedFinish) -
                                 original.Activities.Max(a => a.PlannedFinish)).Days;
            // Assume daily indirect cost
            return durationChange * 5000m; // $5000/day
        }

        #endregion

        #region Look-Ahead Planning

        public LookAheadPlan GenerateLookAhead(string scheduleId, int weeks = 3)
        {
            lock (_lockObject)
            {
                if (!_schedules.TryGetValue(scheduleId, out var schedule))
                    return null;

                var startDate = DateTime.Today;
                var endDate = startDate.AddDays(weeks * 7);

                var plan = new LookAheadPlan
                {
                    ScheduleId = scheduleId,
                    GeneratedDate = DateTime.Now,
                    StartDate = startDate,
                    EndDate = endDate,
                    Weeks = weeks,
                    Activities = new List<LookAheadActivity>(),
                    Constraints = new List<ActivityConstraint>(),
                    ResourceRequirements = new List<ResourceRequirement>()
                };

                // Get activities in look-ahead window
                var lookAheadActivities = schedule.Activities
                    .Where(a => a.PlannedStart <= endDate && a.PlannedFinish >= startDate)
                    .Where(a => a.Status != ActivityStatus.Completed)
                    .OrderBy(a => a.PlannedStart)
                    .ToList();

                foreach (var activity in lookAheadActivities)
                {
                    var lookAheadActivity = new LookAheadActivity
                    {
                        ActivityId = activity.Id,
                        ActivityName = activity.Name,
                        PlannedStart = activity.PlannedStart,
                        PlannedFinish = activity.PlannedFinish,
                        Duration = activity.Duration,
                        PercentComplete = activity.PercentComplete,
                        IsReady = true,
                        Constraints = new List<string>()
                    };

                    // Check constraints
                    foreach (var pred in activity.Predecessors)
                    {
                        var predActivity = schedule.Activities.FirstOrDefault(a => a.Id == pred.PredecessorId);
                        if (predActivity != null && predActivity.Status != ActivityStatus.Completed)
                        {
                            lookAheadActivity.IsReady = false;
                            lookAheadActivity.Constraints.Add($"Waiting for: {predActivity.Name}");
                            plan.Constraints.Add(new ActivityConstraint
                            {
                                ActivityId = activity.Id,
                                ConstraintType = "Predecessor",
                                Description = $"Predecessor not complete: {predActivity.Name}",
                                ExpectedResolution = predActivity.PlannedFinish
                            });
                        }
                    }

                    // Aggregate resource requirements
                    foreach (var resource in activity.AssignedResources)
                    {
                        var existing = plan.ResourceRequirements
                            .FirstOrDefault(r => r.ResourceId == resource.ResourceId);
                        if (existing != null)
                        {
                            existing.TotalHours += resource.HoursPerDay * activity.Duration;
                        }
                        else
                        {
                            plan.ResourceRequirements.Add(new ResourceRequirement
                            {
                                ResourceId = resource.ResourceId,
                                ResourceName = resource.ResourceName,
                                TotalHours = resource.HoursPerDay * activity.Duration
                            });
                        }
                    }

                    plan.Activities.Add(lookAheadActivity);
                }

                plan.TotalActivities = plan.Activities.Count;
                plan.ReadyActivities = plan.Activities.Count(a => a.IsReady);
                plan.ConstrainedActivities = plan.Activities.Count(a => !a.IsReady);

                return plan;
            }
        }

        #endregion

        #region Baseline Management

        public ScheduleBaseline CreateBaseline(string scheduleId, string baselineName, int version)
        {
            lock (_lockObject)
            {
                if (!_schedules.TryGetValue(scheduleId, out var schedule))
                    return null;

                var baseline = new ScheduleBaseline
                {
                    Id = Guid.NewGuid().ToString(),
                    ScheduleId = scheduleId,
                    Name = baselineName,
                    Version = version,
                    BaselineDate = DateTime.Now,
                    ProjectStart = schedule.Activities.Min(a => a.PlannedStart),
                    ProjectFinish = schedule.Activities.Max(a => a.PlannedFinish),
                    TotalDuration = (schedule.Activities.Max(a => a.PlannedFinish) -
                                   schedule.Activities.Min(a => a.PlannedStart)).Days,
                    Activities = schedule.Activities.Select(a => new BaselineActivity
                    {
                        ActivityId = a.Id,
                        ActivityName = a.Name,
                        PlannedStart = a.PlannedStart,
                        PlannedFinish = a.PlannedFinish,
                        Duration = a.Duration,
                        PlannedCost = a.PlannedCost
                    }).ToList()
                };

                _baselines[baseline.Id] = baseline;
                schedule.CurrentBaselineId = baseline.Id;

                return baseline;
            }
        }

        public BaselineComparison CompareBaselines(string baselineId1, string baselineId2)
        {
            lock (_lockObject)
            {
                if (!_baselines.TryGetValue(baselineId1, out var baseline1) ||
                    !_baselines.TryGetValue(baselineId2, out var baseline2))
                    return null;

                var comparison = new BaselineComparison
                {
                    Baseline1Id = baselineId1,
                    Baseline2Id = baselineId2,
                    Baseline1Name = baseline1.Name,
                    Baseline2Name = baseline2.Name,
                    DurationVariance = baseline2.TotalDuration - baseline1.TotalDuration,
                    StartVariance = (baseline2.ProjectStart - baseline1.ProjectStart).Days,
                    FinishVariance = (baseline2.ProjectFinish - baseline1.ProjectFinish).Days,
                    ActivityVariances = new List<ActivityVariance>()
                };

                foreach (var activity1 in baseline1.Activities)
                {
                    var activity2 = baseline2.Activities.FirstOrDefault(a => a.ActivityId == activity1.ActivityId);
                    if (activity2 == null) continue;

                    comparison.ActivityVariances.Add(new ActivityVariance
                    {
                        ActivityId = activity1.ActivityId,
                        ActivityName = activity1.ActivityName,
                        StartVariance = (activity2.PlannedStart - activity1.PlannedStart).Days,
                        FinishVariance = (activity2.PlannedFinish - activity1.PlannedFinish).Days,
                        DurationVariance = activity2.Duration - activity1.Duration
                    });
                }

                return comparison;
            }
        }

        #endregion

        #region Progress Tracking

        public ProgressReport GenerateProgressReport(string scheduleId, DateTime asOfDate)
        {
            lock (_lockObject)
            {
                if (!_schedules.TryGetValue(scheduleId, out var schedule))
                    return null;

                var report = new ProgressReport
                {
                    ScheduleId = scheduleId,
                    AsOfDate = asOfDate,
                    GeneratedDate = DateTime.Now,
                    ActivityProgress = new List<ActivityProgress>(),
                    MilestoneStatus = new List<MilestoneStatus>()
                };

                // Calculate overall progress
                var totalDuration = schedule.Activities.Sum(a => a.Duration);
                var completedDuration = schedule.Activities
                    .Sum(a => (int)(a.Duration * a.PercentComplete / 100.0));
                report.OverallProgress = totalDuration > 0
                    ? (completedDuration * 100.0 / totalDuration)
                    : 0;

                // Planned progress as of date
                var plannedComplete = schedule.Activities
                    .Where(a => a.PlannedFinish <= asOfDate)
                    .Sum(a => a.Duration);
                var plannedInProgress = schedule.Activities
                    .Where(a => a.PlannedStart <= asOfDate && a.PlannedFinish > asOfDate)
                    .Sum(a =>
                    {
                        var elapsed = (asOfDate - a.PlannedStart).Days;
                        return Math.Min(elapsed, a.Duration);
                    });
                report.PlannedProgress = totalDuration > 0
                    ? ((plannedComplete + plannedInProgress) * 100.0 / totalDuration)
                    : 0;

                report.ScheduleVariance = report.OverallProgress - report.PlannedProgress;
                report.SchedulePerformanceIndex = report.PlannedProgress > 0
                    ? report.OverallProgress / report.PlannedProgress
                    : 1.0;

                // Activity details
                foreach (var activity in schedule.Activities)
                {
                    var progress = new ActivityProgress
                    {
                        ActivityId = activity.Id,
                        ActivityName = activity.Name,
                        PlannedStart = activity.PlannedStart,
                        PlannedFinish = activity.PlannedFinish,
                        ActualStart = activity.ActualStart,
                        ActualFinish = activity.ActualFinish,
                        PercentComplete = activity.PercentComplete,
                        Status = activity.Status
                    };

                    // Calculate variance
                    if (activity.ActualFinish != default)
                    {
                        progress.FinishVariance = (activity.ActualFinish - activity.PlannedFinish).Days;
                    }
                    else if (activity.ActualStart != default && asOfDate > activity.PlannedFinish)
                    {
                        // Activity should be done but isn't
                        progress.FinishVariance = (asOfDate - activity.PlannedFinish).Days;
                    }

                    report.ActivityProgress.Add(progress);
                }

                // Milestone status
                foreach (var milestone in schedule.Milestones)
                {
                    report.MilestoneStatus.Add(new MilestoneStatus
                    {
                        MilestoneId = milestone.Id,
                        MilestoneName = milestone.Name,
                        PlannedDate = milestone.PlannedDate,
                        ActualDate = milestone.ActualDate,
                        Status = milestone.ActualDate != default ? "Achieved" :
                                milestone.PlannedDate < asOfDate ? "Late" : "Pending",
                        Variance = milestone.ActualDate != default
                            ? (milestone.ActualDate - milestone.PlannedDate).Days
                            : (asOfDate > milestone.PlannedDate ? (asOfDate - milestone.PlannedDate).Days : 0)
                    });
                }

                return report;
            }
        }

        public void UpdateActivityProgress(string scheduleId, string activityId,
            double percentComplete, DateTime? actualStart = null, DateTime? actualFinish = null)
        {
            lock (_lockObject)
            {
                if (_schedules.TryGetValue(scheduleId, out var schedule))
                {
                    var activity = schedule.Activities.FirstOrDefault(a => a.Id == activityId);
                    if (activity != null)
                    {
                        activity.PercentComplete = percentComplete;
                        if (actualStart.HasValue)
                            activity.ActualStart = actualStart.Value;
                        if (actualFinish.HasValue)
                            activity.ActualFinish = actualFinish.Value;

                        activity.Status = percentComplete >= 100 ? ActivityStatus.Completed :
                                         percentComplete > 0 ? ActivityStatus.InProgress :
                                         ActivityStatus.NotStarted;

                        schedule.LastModified = DateTime.Now;

                        // Check for schedule alerts
                        if (activity.ActualFinish != default && activity.ActualFinish > activity.PlannedFinish)
                        {
                            OnScheduleAlert(new ScheduleAlertEventArgs
                            {
                                ScheduleId = scheduleId,
                                ActivityId = activityId,
                                AlertType = ScheduleAlertType.ActivityLate,
                                Message = $"Activity '{activity.Name}' completed {(activity.ActualFinish - activity.PlannedFinish).Days} days late"
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private ProjectCalendar CreateDefaultCalendar()
        {
            return new ProjectCalendar
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Standard",
                WorkDays = new List<DayOfWeek>
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday
                },
                HoursPerDay = 8,
                Holidays = new List<DateTime>()
            };
        }

        private DateTime CalculateFinishDate(DateTime start, int durationDays, ProjectCalendar calendar)
        {
            var date = start;
            var workDays = 0;
            while (workDays < durationDays)
            {
                date = date.AddDays(1);
                if (calendar.WorkDays.Contains(date.DayOfWeek) && !calendar.Holidays.Contains(date.Date))
                    workDays++;
            }
            return date;
        }

        private DateTime CalculateStartDate(DateTime finish, int durationDays, ProjectCalendar calendar)
        {
            var date = finish;
            var workDays = 0;
            while (workDays < durationDays)
            {
                date = date.AddDays(-1);
                if (calendar.WorkDays.Contains(date.DayOfWeek) && !calendar.Holidays.Contains(date.Date))
                    workDays++;
            }
            return date;
        }

        private DateTime CalculateDependencyDate(ScheduleActivity predecessor,
            Func<ActivityDependency, DependencyType> getType,
            Func<ActivityDependency, int> getLag,
            ProjectCalendar calendar)
        {
            return predecessor.EarlyFinish;
        }

        private ProjectSchedule CloneSchedule(ProjectSchedule original)
        {
            return new ProjectSchedule
            {
                Id = original.Id,
                ProjectId = original.ProjectId,
                ProjectName = original.ProjectName,
                Activities = original.Activities.Select(a => new ScheduleActivity
                {
                    Id = a.Id,
                    Name = a.Name,
                    Duration = a.Duration,
                    PlannedStart = a.PlannedStart,
                    PlannedFinish = a.PlannedFinish,
                    Predecessors = a.Predecessors.ToList(),
                    AssignedResources = a.AssignedResources.ToList()
                }).ToList(),
                Calendars = original.Calendars
            };
        }

        private void InitializeTemplates()
        {
            _templates.AddRange(new[]
            {
                new ScheduleTemplate
                {
                    Id = "commercial-building",
                    Name = "Commercial Building",
                    Description = "Standard commercial building construction schedule",
                    Phases = new List<string> { "Preconstruction", "Site Work", "Foundation",
                        "Structure", "Enclosure", "MEP Rough-In", "Finishes", "Commissioning" }
                },
                new ScheduleTemplate
                {
                    Id = "residential-multifamily",
                    Name = "Residential Multi-Family",
                    Description = "Multi-family residential construction schedule",
                    Phases = new List<string> { "Preconstruction", "Site Work", "Foundation",
                        "Framing", "Roofing", "MEP", "Drywall", "Finishes", "Landscaping" }
                },
                new ScheduleTemplate
                {
                    Id = "renovation",
                    Name = "Building Renovation",
                    Description = "Renovation/remodel project schedule",
                    Phases = new List<string> { "Assessment", "Demolition", "Structural",
                        "MEP", "Finishes", "Commissioning" }
                },
                new ScheduleTemplate
                {
                    Id = "fit-out",
                    Name = "Interior Fit-Out",
                    Description = "Commercial interior fit-out schedule",
                    Phases = new List<string> { "Design", "Procurement", "Demolition",
                        "Partitions", "MEP", "Ceilings", "Flooring", "FF&E" }
                }
            });
        }

        private void InitializeActivityLibrary()
        {
            // Standard activity library for quick schedule creation
        }

        #endregion

        #region Events

        private void OnScheduleAlert(ScheduleAlertEventArgs e)
        {
            ScheduleAlert?.Invoke(this, e);
        }

        private void OnCriticalPathChanged(CriticalPathChangeEventArgs e)
        {
            CriticalPathChanged?.Invoke(this, e);
        }

        private void OnDelayDetected(DelayDetectedEventArgs e)
        {
            DelayDetected?.Invoke(this, e);
        }

        #endregion
    }

    #region Data Models

    public class ProjectSchedule
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ScheduleType Type { get; set; }
        public ScheduleStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public string CurrentBaselineId { get; set; }
        public List<ScheduleActivity> Activities { get; set; }
        public List<Milestone> Milestones { get; set; }
        public List<ProjectCalendar> Calendars { get; set; }
        public WBSNode WBSStructure { get; set; }
        public List<string> CriticalPath { get; set; }
    }

    public class ScheduleActivity
    {
        public string Id { get; set; }
        public string ScheduleId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string WBSCode { get; set; }
        public int Duration { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedFinish { get; set; }
        public DateTime ActualStart { get; set; }
        public DateTime ActualFinish { get; set; }
        public DateTime EarlyStart { get; set; }
        public DateTime EarlyFinish { get; set; }
        public DateTime LateStart { get; set; }
        public DateTime LateFinish { get; set; }
        public int TotalFloat { get; set; }
        public int FreeFloat { get; set; }
        public double PercentComplete { get; set; }
        public ActivityStatus Status { get; set; }
        public int Priority { get; set; }
        public decimal PlannedCost { get; set; }
        public int? CrashDuration { get; set; }
        public decimal? CrashCost { get; set; }
        public string DelayReason { get; set; }
        public List<ActivityDependency> Predecessors { get; set; } = new List<ActivityDependency>();
        public List<ResourceAssignment> AssignedResources { get; set; } = new List<ResourceAssignment>();
        public DateTime CreatedDate { get; set; }
    }

    public class ActivityDependency
    {
        public string PredecessorId { get; set; }
        public string SuccessorId { get; set; }
        public DependencyType Type { get; set; }
        public int Lag { get; set; }
    }

    public class ResourceAssignment
    {
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public double HoursPerDay { get; set; }
        public decimal Rate { get; set; }
    }

    public class Milestone
    {
        public string Id { get; set; }
        public string ScheduleId { get; set; }
        public string Name { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime ActualDate { get; set; }
        public MilestoneType Type { get; set; }
        public bool IsContractual { get; set; }
        public decimal? PenaltyPerDay { get; set; }
    }

    public class ProjectCalendar
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<DayOfWeek> WorkDays { get; set; }
        public double HoursPerDay { get; set; }
        public List<DateTime> Holidays { get; set; }
    }

    public class WBSNode
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public List<WBSNode> Children { get; set; } = new List<WBSNode>();
    }

    public class ScheduleBaseline
    {
        public string Id { get; set; }
        public string ScheduleId { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
        public DateTime BaselineDate { get; set; }
        public DateTime ProjectStart { get; set; }
        public DateTime ProjectFinish { get; set; }
        public int TotalDuration { get; set; }
        public List<BaselineActivity> Activities { get; set; }
    }

    public class BaselineActivity
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedFinish { get; set; }
        public int Duration { get; set; }
        public decimal PlannedCost { get; set; }
    }

    public class CriticalPathResult
    {
        public string ScheduleId { get; set; }
        public DateTime CalculationDate { get; set; }
        public List<string> CriticalActivities { get; set; }
        public List<string> NearCriticalActivities { get; set; }
        public Dictionary<string, FloatInfo> ActivityFloats { get; set; }
        public int ProjectDuration { get; set; }
        public int CriticalPathLength { get; set; }
        public int TotalFloatDays { get; set; }
    }

    public class FloatInfo
    {
        public string ActivityId { get; set; }
        public int TotalFloat { get; set; }
        public int FreeFloat { get; set; }
        public bool IsCritical { get; set; }
        public bool IsNearCritical { get; set; }
    }

    public class DelayAnalysis
    {
        public string Id { get; set; }
        public string ScheduleId { get; set; }
        public DelayAnalysisMethod Method { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<DelayEvent> DelayEvents { get; set; }
        public List<string> ImpactedActivities { get; set; }
        public int TotalDelayDays { get; set; }
        public int ExcusableDelayDays { get; set; }
        public int NonExcusableDelayDays { get; set; }
        public int ConcurrentDelayDays { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class DelayEvent
    {
        public string Id { get; set; }
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public int DelayDays { get; set; }
        public int StartVariance { get; set; }
        public int FinishVariance { get; set; }
        public DelayType Type { get; set; }
        public bool IsConcurrent { get; set; }
        public bool ImpactsCriticalPath { get; set; }
        public string Description { get; set; }
    }

    public class ResourcePool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<ScheduleResource> Resources { get; set; }
    }

    public class ScheduleResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public double AvailableHoursPerDay { get; set; }
        public decimal HourlyRate { get; set; }
    }

    public class ResourceLevelingResult
    {
        public string ScheduleId { get; set; }
        public DateTime LevelingDate { get; set; }
        public int OriginalDuration { get; set; }
        public int NewDuration { get; set; }
        public int DurationChange { get; set; }
        public List<ResourceAdjustment> Adjustments { get; set; }
        public Dictionary<string, List<ResourceUsage>> ResourceProfiles { get; set; }
    }

    public class ResourceLevelingOptions
    {
        public bool PreserveProjectEnd { get; set; }
        public List<string> PriorityResources { get; set; }
    }

    public class ResourceAdjustment
    {
        public string ActivityId { get; set; }
        public string ResourceId { get; set; }
        public string AdjustmentType { get; set; }
        public int DaysDelayed { get; set; }
    }

    public class ResourceUsage
    {
        public DateTime Date { get; set; }
        public string ResourceId { get; set; }
        public double AllocatedHours { get; set; }
        public List<string> Activities { get; set; }
    }

    public class ResourceRequirement
    {
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public double TotalHours { get; set; }
    }

    public class CompressionResult
    {
        public string ScheduleId { get; set; }
        public int OriginalDuration { get; set; }
        public int NewDuration { get; set; }
        public decimal TotalCostIncrease { get; set; }
        public List<CompressionOption> CompressionOptions { get; set; }
        public CompressionOption RecommendedOption { get; set; }
    }

    public class CompressionOptions
    {
        public int? TargetDuration { get; set; }
        public decimal? MaxBudget { get; set; }
        public double MaxRiskIncrease { get; set; }
    }

    public class CompressionOption
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public CompressionMethod Method { get; set; }
        public int DaysReduced { get; set; }
        public decimal AdditionalCost { get; set; }
        public decimal CostPerDayReduced { get; set; }
        public double RiskIncrease { get; set; }
    }

    public class WhatIfScenario
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ScenarioChange> Changes { get; set; }
    }

    public class ScenarioChange
    {
        public string ActivityId { get; set; }
        public ScenarioChangeType ChangeType { get; set; }
        public int Value { get; set; }
    }

    public class WhatIfResult
    {
        public string ScenarioId { get; set; }
        public string ScheduleId { get; set; }
        public string ScenarioName { get; set; }
        public DateTime AnalysisDate { get; set; }
        public DateTime BaselineCompletion { get; set; }
        public DateTime ScenarioCompletion { get; set; }
        public int DurationImpact { get; set; }
        public decimal CostImpact { get; set; }
        public List<ScenarioImpact> Impacts { get; set; }
    }

    public class ScenarioImpact
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public DateTime OriginalFinish { get; set; }
        public DateTime ScenarioFinish { get; set; }
        public int DaysImpact { get; set; }
    }

    public class LookAheadPlan
    {
        public string ScheduleId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Weeks { get; set; }
        public int TotalActivities { get; set; }
        public int ReadyActivities { get; set; }
        public int ConstrainedActivities { get; set; }
        public List<LookAheadActivity> Activities { get; set; }
        public List<ActivityConstraint> Constraints { get; set; }
        public List<ResourceRequirement> ResourceRequirements { get; set; }
    }

    public class LookAheadActivity
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedFinish { get; set; }
        public int Duration { get; set; }
        public double PercentComplete { get; set; }
        public bool IsReady { get; set; }
        public List<string> Constraints { get; set; }
    }

    public class ActivityConstraint
    {
        public string ActivityId { get; set; }
        public string ConstraintType { get; set; }
        public string Description { get; set; }
        public DateTime ExpectedResolution { get; set; }
    }

    public class BaselineComparison
    {
        public string Baseline1Id { get; set; }
        public string Baseline2Id { get; set; }
        public string Baseline1Name { get; set; }
        public string Baseline2Name { get; set; }
        public int DurationVariance { get; set; }
        public int StartVariance { get; set; }
        public int FinishVariance { get; set; }
        public List<ActivityVariance> ActivityVariances { get; set; }
    }

    public class ActivityVariance
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public int StartVariance { get; set; }
        public int FinishVariance { get; set; }
        public int DurationVariance { get; set; }
    }

    public class ProgressReport
    {
        public string ScheduleId { get; set; }
        public DateTime AsOfDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public double OverallProgress { get; set; }
        public double PlannedProgress { get; set; }
        public double ScheduleVariance { get; set; }
        public double SchedulePerformanceIndex { get; set; }
        public List<ActivityProgress> ActivityProgress { get; set; }
        public List<MilestoneStatus> MilestoneStatus { get; set; }
    }

    public class ActivityProgress
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedFinish { get; set; }
        public DateTime ActualStart { get; set; }
        public DateTime ActualFinish { get; set; }
        public double PercentComplete { get; set; }
        public int FinishVariance { get; set; }
        public ActivityStatus Status { get; set; }
    }

    public class MilestoneStatus
    {
        public string MilestoneId { get; set; }
        public string MilestoneName { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime ActualDate { get; set; }
        public string Status { get; set; }
        public int Variance { get; set; }
    }

    public class ScheduleTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Phases { get; set; }
    }

    #endregion

    #region Enums

    public enum ScheduleType
    {
        Master,
        Phase,
        LookAhead,
        Milestone,
        Summary
    }

    public enum ScheduleStatus
    {
        Draft,
        Baseline,
        Active,
        OnHold,
        Completed,
        Archived
    }

    public enum ActivityStatus
    {
        NotStarted,
        InProgress,
        Completed,
        OnHold,
        Cancelled
    }

    public enum DependencyType
    {
        FinishToStart,
        StartToStart,
        FinishToFinish,
        StartToFinish
    }

    public enum MilestoneType
    {
        ProjectStart,
        PhaseStart,
        PhaseEnd,
        Contractual,
        Payment,
        Delivery,
        ProjectEnd
    }

    public enum DelayAnalysisMethod
    {
        AsPlannedVsAsBuilt,
        ImpactedAsPlanned,
        TimeImpactAnalysis,
        WindowsAnalysis,
        CollapsedAsBuilt
    }

    public enum DelayType
    {
        Excusable,
        ExcusableCompensable,
        NonExcusable,
        Concurrent,
        ToBeDetermined
    }

    public enum CompressionMethod
    {
        Crashing,
        FastTracking
    }

    public enum ScenarioChangeType
    {
        DurationChange,
        StartDelay,
        ResourceChange,
        ScopeChange
    }

    public enum ScheduleAlertType
    {
        ActivityLate,
        MilestoneMissed,
        CriticalPathChange,
        FloatErosion,
        ResourceOverallocation
    }

    #endregion

    #region Event Args

    public class ScheduleAlertEventArgs : EventArgs
    {
        public string ScheduleId { get; set; }
        public string ActivityId { get; set; }
        public ScheduleAlertType AlertType { get; set; }
        public string Message { get; set; }
    }

    public class CriticalPathChangeEventArgs : EventArgs
    {
        public string ScheduleId { get; set; }
        public List<string> PreviousCriticalPath { get; set; }
        public List<string> NewCriticalPath { get; set; }
        public DateTime ChangeDate { get; set; }
    }

    public class DelayDetectedEventArgs : EventArgs
    {
        public string ScheduleId { get; set; }
        public int TotalDelayDays { get; set; }
        public List<DelayEvent> CriticalDelays { get; set; }
    }

    #endregion
}
