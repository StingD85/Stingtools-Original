// ============================================================================
// StingBIM AI - VDC Coordination Center
// Enhanced clash grouping, assignment, issue tracking, and coordination meetings
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Intelligence.VDC
{
    /// <summary>
    /// VDC Coordination Center providing comprehensive clash management,
    /// issue tracking, meeting coordination, and multi-discipline workflow management.
    /// </summary>
    public sealed class VDCCoordinationCenter
    {
        private static readonly Lazy<VDCCoordinationCenter> _instance =
            new Lazy<VDCCoordinationCenter>(() => new VDCCoordinationCenter());
        public static VDCCoordinationCenter Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, ClashGroup> _clashGroups = new();
        private readonly Dictionary<string, CoordinationIssue> _issues = new();
        private readonly Dictionary<string, CoordinationMeeting> _meetings = new();
        private readonly Dictionary<string, ActionItem> _actionItems = new();
        private readonly Dictionary<string, ModelVersion> _modelVersions = new();
        private readonly List<CoordinationLog> _coordinationLog = new();

        public event EventHandler<CoordinationEventArgs> IssueCreated;
        public event EventHandler<CoordinationEventArgs> IssueResolved;
        public event EventHandler<CoordinationEventArgs> MeetingScheduled;
        public event EventHandler<CoordinationEventArgs> ActionItemDue;

        private VDCCoordinationCenter() { }

        #region Clash Grouping

        /// <summary>
        /// Automatically group clashes by various criteria
        /// </summary>
        public async Task<ClashGroupingResult> GroupClashesAsync(List<RawClash> clashes, GroupingStrategy strategy)
        {
            return await Task.Run(() =>
            {
                var result = new ClashGroupingResult
                {
                    GroupedAt = DateTime.UtcNow,
                    Strategy = strategy,
                    Groups = new List<ClashGroup>(),
                    Statistics = new ClashStatistics()
                };

                IEnumerable<IGrouping<string, RawClash>> groupedClashes = strategy switch
                {
                    GroupingStrategy.ByLocation => clashes.GroupBy(c => $"{c.Level}_{c.Zone}"),
                    GroupingStrategy.ByTradePair => clashes.GroupBy(c => $"{c.PrimaryTrade}_{c.SecondaryTrade}"),
                    GroupingStrategy.BySeverity => clashes.GroupBy(c => c.Severity.ToString()),
                    GroupingStrategy.ByGrid => clashes.GroupBy(c => c.GridLocation ?? "Unknown"),
                    GroupingStrategy.BySystem => clashes.GroupBy(c => c.SystemType ?? "General"),
                    GroupingStrategy.ByResponsibleParty => clashes.GroupBy(c => c.ResponsibleParty ?? "Unassigned"),
                    GroupingStrategy.Smart => SmartGroupClashes(clashes),
                    _ => clashes.GroupBy(c => c.Level ?? "Unknown")
                };

                foreach (var group in groupedClashes)
                {
                    var clashGroup = new ClashGroup
                    {
                        GroupId = Guid.NewGuid().ToString(),
                        GroupName = GenerateGroupName(group.Key, strategy),
                        GroupKey = group.Key,
                        Strategy = strategy,
                        Clashes = group.ToList(),
                        TotalClashes = group.Count(),
                        CreatedDate = DateTime.UtcNow,
                        Status = ClashGroupStatus.New,
                        Priority = DetermineGroupPriority(group.ToList()),
                        AssignedTo = DetermineResponsibleParty(group.ToList())
                    };

                    // Calculate group statistics
                    clashGroup.BySeverity = group.GroupBy(c => c.Severity)
                        .ToDictionary(g => g.Key, g => g.Count());
                    clashGroup.ByTrade = group.GroupBy(c => c.PrimaryTrade)
                        .ToDictionary(g => g.Key, g => g.Count());

                    result.Groups.Add(clashGroup);

                    lock (_lock)
                    {
                        _clashGroups[clashGroup.GroupId] = clashGroup;
                    }
                }

                // Calculate overall statistics
                result.Statistics.TotalClashes = clashes.Count;
                result.Statistics.TotalGroups = result.Groups.Count;
                result.Statistics.BySeverity = clashes.GroupBy(c => c.Severity)
                    .ToDictionary(g => g.Key, g => g.Count());
                result.Statistics.ByTrade = clashes.GroupBy(c => c.PrimaryTrade)
                    .ToDictionary(g => g.Key, g => g.Count());
                result.Statistics.ByLevel = clashes.GroupBy(c => c.Level ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());
                result.Statistics.AverageClashesPerGroup = result.Groups.Count > 0 ?
                    (double)clashes.Count / result.Groups.Count : 0;

                return result;
            });
        }

        private IEnumerable<IGrouping<string, RawClash>> SmartGroupClashes(List<RawClash> clashes)
        {
            // Smart grouping: Combine location + trade pair for optimal grouping
            return clashes.GroupBy(c =>
            {
                var location = $"{c.Level ?? "L00"}_{c.Zone ?? "Z00"}";
                var trades = GetNormalizedTradePair(c.PrimaryTrade, c.SecondaryTrade);
                return $"{location}|{trades}";
            });
        }

        private string GetNormalizedTradePair(string trade1, string trade2)
        {
            // Normalize trade pair to consistent order
            var trades = new[] { trade1 ?? "Unknown", trade2 ?? "Unknown" }.OrderBy(t => t).ToArray();
            return $"{trades[0]}_{trades[1]}";
        }

        private string GenerateGroupName(string key, GroupingStrategy strategy)
        {
            return strategy switch
            {
                GroupingStrategy.ByLocation => $"Location: {key.Replace("_", " - ")}",
                GroupingStrategy.ByTradePair => $"Trades: {key.Replace("_", " vs ")}",
                GroupingStrategy.BySeverity => $"Severity: {key}",
                GroupingStrategy.ByGrid => $"Grid: {key}",
                GroupingStrategy.BySystem => $"System: {key}",
                GroupingStrategy.ByResponsibleParty => $"Assigned: {key}",
                GroupingStrategy.Smart => $"Coordination: {key.Replace("|", " / ")}",
                _ => key
            };
        }

        private ClashPriority DetermineGroupPriority(List<RawClash> clashes)
        {
            if (clashes.Any(c => c.Severity == ClashSeverity.Critical))
                return ClashPriority.Critical;
            if (clashes.Count > 20 || clashes.Any(c => c.Severity == ClashSeverity.High))
                return ClashPriority.High;
            if (clashes.Count > 10 || clashes.Any(c => c.Severity == ClashSeverity.Medium))
                return ClashPriority.Medium;
            return ClashPriority.Low;
        }

        private string DetermineResponsibleParty(List<RawClash> clashes)
        {
            // Determine most common responsible trade
            var tradeCounts = clashes
                .GroupBy(c => c.PrimaryTrade)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return tradeCounts?.Key ?? "Unassigned";
        }

        /// <summary>
        /// Assign a clash group to a responsible party
        /// </summary>
        public void AssignClashGroup(string groupId, string assignedTo, DateTime? dueDate = null)
        {
            lock (_lock)
            {
                if (_clashGroups.TryGetValue(groupId, out var group))
                {
                    group.AssignedTo = assignedTo;
                    group.DueDate = dueDate;
                    group.Status = ClashGroupStatus.Assigned;

                    LogCoordinationAction(groupId, "ClashGroup", "Assigned",
                        $"Assigned to {assignedTo}", assignedTo);
                }
            }
        }

        /// <summary>
        /// Update clash group status
        /// </summary>
        public void UpdateClashGroupStatus(string groupId, ClashGroupStatus status, string updatedBy, string notes = null)
        {
            lock (_lock)
            {
                if (_clashGroups.TryGetValue(groupId, out var group))
                {
                    var previousStatus = group.Status;
                    group.Status = status;
                    group.LastUpdated = DateTime.UtcNow;
                    group.StatusNotes = notes;

                    if (status == ClashGroupStatus.Resolved)
                        group.ResolvedDate = DateTime.UtcNow;

                    LogCoordinationAction(groupId, "ClashGroup", "StatusChange",
                        $"Status changed from {previousStatus} to {status}", updatedBy);
                }
            }
        }

        #endregion

        #region Coordination Issues

        /// <summary>
        /// Create a coordination issue from a clash group or manual entry
        /// </summary>
        public CoordinationIssue CreateIssue(IssueCreationRequest request)
        {
            var issue = new CoordinationIssue
            {
                IssueId = $"CI-{DateTime.UtcNow:yyyyMMdd}-{_issues.Count + 1:D4}",
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                Priority = request.Priority,
                Status = IssueStatus.Open,
                CreatedBy = request.CreatedBy,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = request.AssignedTo,
                DueDate = request.DueDate,
                AffectedDisciplines = request.AffectedDisciplines ?? new List<string>(),
                RelatedClashGroups = request.RelatedClashGroups ?? new List<string>(),
                RelatedElements = request.RelatedElements ?? new List<string>(),
                Location = request.Location,
                Level = request.Level,
                Zone = request.Zone,
                Attachments = request.Attachments ?? new List<string>(),
                Comments = new List<IssueComment>(),
                History = new List<IssueHistoryEntry>()
            };

            // Estimate resolution effort
            issue.EstimatedHours = EstimateResolutionEffort(issue);

            lock (_lock)
            {
                _issues[issue.IssueId] = issue;
            }

            // Add initial history
            AddIssueHistory(issue.IssueId, "Created", request.CreatedBy, "Issue created");

            IssueCreated?.Invoke(this, new CoordinationEventArgs
            {
                Type = CoordinationEventType.IssueCreated,
                EntityId = issue.IssueId,
                Message = $"Issue {issue.IssueId} created: {issue.Title}"
            });

            return issue;
        }

        private double EstimateResolutionEffort(CoordinationIssue issue)
        {
            double baseHours = issue.Type switch
            {
                IssueType.Clash => 2.0,
                IssueType.Clearance => 1.5,
                IssueType.Coordination => 4.0,
                IssueType.DesignConflict => 8.0,
                IssueType.RFI => 2.0,
                IssueType.Submittal => 1.0,
                _ => 3.0
            };

            // Adjust by priority
            var priorityMultiplier = issue.Priority switch
            {
                IssuePriority.Critical => 0.5, // Rush job
                IssuePriority.High => 0.75,
                IssuePriority.Medium => 1.0,
                IssuePriority.Low => 1.25,
                _ => 1.0
            };

            // Adjust by affected disciplines
            var disciplineMultiplier = 1.0 + (issue.AffectedDisciplines.Count * 0.2);

            return baseHours * priorityMultiplier * disciplineMultiplier;
        }

        /// <summary>
        /// Add a comment to an issue
        /// </summary>
        public void AddIssueComment(string issueId, string author, string comment, List<string> attachments = null)
        {
            lock (_lock)
            {
                if (_issues.TryGetValue(issueId, out var issue))
                {
                    issue.Comments.Add(new IssueComment
                    {
                        CommentId = Guid.NewGuid().ToString(),
                        Author = author,
                        Text = comment,
                        Timestamp = DateTime.UtcNow,
                        Attachments = attachments ?? new List<string>()
                    });

                    issue.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Update issue status
        /// </summary>
        public void UpdateIssueStatus(string issueId, IssueStatus status, string updatedBy, string notes = null)
        {
            lock (_lock)
            {
                if (_issues.TryGetValue(issueId, out var issue))
                {
                    var previousStatus = issue.Status;
                    issue.Status = status;
                    issue.LastUpdated = DateTime.UtcNow;

                    if (status == IssueStatus.Resolved || status == IssueStatus.Closed)
                    {
                        issue.ResolvedDate = DateTime.UtcNow;
                        issue.ResolvedBy = updatedBy;
                        issue.Resolution = notes;

                        IssueResolved?.Invoke(this, new CoordinationEventArgs
                        {
                            Type = CoordinationEventType.IssueResolved,
                            EntityId = issueId,
                            Message = $"Issue {issueId} resolved"
                        });
                    }

                    AddIssueHistory(issueId, "StatusChange", updatedBy,
                        $"Status changed from {previousStatus} to {status}. {notes}");
                }
            }
        }

        private void AddIssueHistory(string issueId, string action, string user, string description)
        {
            if (_issues.TryGetValue(issueId, out var issue))
            {
                issue.History.Add(new IssueHistoryEntry
                {
                    EntryId = Guid.NewGuid().ToString(),
                    Action = action,
                    User = user,
                    Timestamp = DateTime.UtcNow,
                    Description = description
                });
            }
        }

        #endregion

        #region Coordination Meetings

        /// <summary>
        /// Schedule a coordination meeting
        /// </summary>
        public CoordinationMeeting ScheduleMeeting(MeetingRequest request)
        {
            var meeting = new CoordinationMeeting
            {
                MeetingId = $"CM-{DateTime.UtcNow:yyyyMMdd}-{_meetings.Count + 1:D3}",
                Title = request.Title,
                Type = request.Type,
                ScheduledDate = request.ScheduledDate,
                Duration = request.Duration ?? TimeSpan.FromHours(1),
                Location = request.Location,
                Organizer = request.Organizer,
                Attendees = request.Attendees ?? new List<MeetingAttendee>(),
                Agenda = request.Agenda ?? new List<AgendaItem>(),
                RelatedIssues = request.RelatedIssues ?? new List<string>(),
                RelatedClashGroups = request.RelatedClashGroups ?? new List<string>(),
                Status = MeetingStatus.Scheduled,
                CreatedDate = DateTime.UtcNow,
                Notes = request.Notes
            };

            // Auto-generate agenda from related issues
            if (!meeting.Agenda.Any() && meeting.RelatedIssues.Any())
            {
                int order = 1;
                foreach (var issueId in meeting.RelatedIssues)
                {
                    if (_issues.TryGetValue(issueId, out var issue))
                    {
                        meeting.Agenda.Add(new AgendaItem
                        {
                            Order = order++,
                            Topic = issue.Title,
                            Duration = TimeSpan.FromMinutes(10),
                            Presenter = issue.AssignedTo,
                            ReferenceId = issueId
                        });
                    }
                }
            }

            lock (_lock)
            {
                _meetings[meeting.MeetingId] = meeting;
            }

            MeetingScheduled?.Invoke(this, new CoordinationEventArgs
            {
                Type = CoordinationEventType.MeetingScheduled,
                EntityId = meeting.MeetingId,
                Message = $"Meeting scheduled: {meeting.Title} on {meeting.ScheduledDate:g}"
            });

            return meeting;
        }

        /// <summary>
        /// Record meeting minutes and action items
        /// </summary>
        public void RecordMeetingMinutes(string meetingId, MeetingMinutes minutes)
        {
            lock (_lock)
            {
                if (_meetings.TryGetValue(meetingId, out var meeting))
                {
                    meeting.Minutes = minutes;
                    meeting.Status = MeetingStatus.Completed;
                    meeting.ActualDuration = minutes.ActualDuration;

                    // Create action items
                    foreach (var action in minutes.ActionItems)
                    {
                        var actionItem = new ActionItem
                        {
                            ActionId = $"AI-{DateTime.UtcNow:yyyyMMdd}-{_actionItems.Count + 1:D4}",
                            Description = action.Description,
                            AssignedTo = action.AssignedTo,
                            DueDate = action.DueDate,
                            Priority = action.Priority,
                            Status = ActionItemStatus.Open,
                            SourceMeetingId = meetingId,
                            RelatedIssueId = action.RelatedIssueId,
                            CreatedDate = DateTime.UtcNow
                        };

                        _actionItems[actionItem.ActionId] = actionItem;
                    }
                }
            }
        }

        /// <summary>
        /// Get upcoming meetings
        /// </summary>
        public List<CoordinationMeeting> GetUpcomingMeetings(int days = 7)
        {
            var cutoff = DateTime.UtcNow.AddDays(days);
            return _meetings.Values
                .Where(m => m.ScheduledDate >= DateTime.UtcNow && m.ScheduledDate <= cutoff)
                .Where(m => m.Status == MeetingStatus.Scheduled)
                .OrderBy(m => m.ScheduledDate)
                .ToList();
        }

        #endregion

        #region Action Items

        /// <summary>
        /// Update action item status
        /// </summary>
        public void UpdateActionItem(string actionId, ActionItemStatus status, string notes = null)
        {
            lock (_lock)
            {
                if (_actionItems.TryGetValue(actionId, out var item))
                {
                    item.Status = status;
                    item.LastUpdated = DateTime.UtcNow;
                    item.Notes = notes;

                    if (status == ActionItemStatus.Completed)
                        item.CompletedDate = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Get overdue action items
        /// </summary>
        public List<ActionItem> GetOverdueActionItems()
        {
            return _actionItems.Values
                .Where(a => a.Status == ActionItemStatus.Open || a.Status == ActionItemStatus.InProgress)
                .Where(a => a.DueDate < DateTime.UtcNow)
                .OrderBy(a => a.DueDate)
                .ToList();
        }

        /// <summary>
        /// Get action items by assignee
        /// </summary>
        public List<ActionItem> GetActionItemsByAssignee(string assignee)
        {
            return _actionItems.Values
                .Where(a => a.AssignedTo == assignee)
                .Where(a => a.Status != ActionItemStatus.Completed && a.Status != ActionItemStatus.Cancelled)
                .OrderBy(a => a.DueDate)
                .ToList();
        }

        #endregion

        #region Model Coordination

        /// <summary>
        /// Register a model version for coordination
        /// </summary>
        public ModelVersion RegisterModelVersion(ModelVersionRegistration registration)
        {
            var version = new ModelVersion
            {
                VersionId = Guid.NewGuid().ToString(),
                ModelName = registration.ModelName,
                Discipline = registration.Discipline,
                Version = registration.Version,
                FilePath = registration.FilePath,
                FileSize = registration.FileSize,
                Checksum = registration.Checksum,
                Author = registration.Author,
                UploadDate = DateTime.UtcNow,
                Description = registration.Description,
                LOD = registration.LOD,
                Phase = registration.Phase,
                Status = ModelVersionStatus.Current
            };

            // Mark previous version as superseded
            var previousVersions = _modelVersions.Values
                .Where(v => v.ModelName == registration.ModelName && v.Status == ModelVersionStatus.Current);
            foreach (var prev in previousVersions)
            {
                prev.Status = ModelVersionStatus.Superseded;
            }

            lock (_lock)
            {
                _modelVersions[version.VersionId] = version;
            }

            return version;
        }

        /// <summary>
        /// Get federated model status
        /// </summary>
        public FederatedModelStatus GetFederatedModelStatus()
        {
            var currentModels = _modelVersions.Values
                .Where(v => v.Status == ModelVersionStatus.Current)
                .ToList();

            return new FederatedModelStatus
            {
                GeneratedAt = DateTime.UtcNow,
                TotalModels = currentModels.Count,
                ModelsByDiscipline = currentModels.GroupBy(m => m.Discipline)
                    .ToDictionary(g => g.Key, g => g.ToList()),
                OldestModel = currentModels.OrderBy(m => m.UploadDate).FirstOrDefault(),
                NewestModel = currentModels.OrderByDescending(m => m.UploadDate).FirstOrDefault(),
                ModelHealth = CalculateModelHealth(currentModels),
                Warnings = GenerateModelWarnings(currentModels)
            };
        }

        private double CalculateModelHealth(List<ModelVersion> models)
        {
            if (!models.Any()) return 0;

            double health = 100;

            // Check for stale models (>7 days old)
            var staleCount = models.Count(m => (DateTime.UtcNow - m.UploadDate).TotalDays > 7);
            health -= staleCount * 10;

            // Check for LOD consistency
            var lodVariance = models.Select(m => m.LOD).Distinct().Count();
            if (lodVariance > 2) health -= 15;

            // Check for phase consistency
            var phaseVariance = models.Select(m => m.Phase).Distinct().Count();
            if (phaseVariance > 1) health -= 10;

            return Math.Max(0, health);
        }

        private List<string> GenerateModelWarnings(List<ModelVersion> models)
        {
            var warnings = new List<string>();

            // Check for stale models
            var staleModels = models.Where(m => (DateTime.UtcNow - m.UploadDate).TotalDays > 7).ToList();
            foreach (var model in staleModels)
            {
                warnings.Add($"{model.ModelName} ({model.Discipline}) is {(DateTime.UtcNow - model.UploadDate).TotalDays:F0} days old");
            }

            // Check for missing disciplines
            var expectedDisciplines = new[] { "Architectural", "Structural", "Mechanical", "Electrical", "Plumbing" };
            var presentDisciplines = models.Select(m => m.Discipline).Distinct();
            var missingDisciplines = expectedDisciplines.Except(presentDisciplines);
            foreach (var missing in missingDisciplines)
            {
                warnings.Add($"No current {missing} model in federation");
            }

            return warnings;
        }

        #endregion

        #region Reporting & Analytics

        /// <summary>
        /// Generate coordination dashboard
        /// </summary>
        public CoordinationDashboard GetDashboard()
        {
            lock (_lock)
            {
                return new CoordinationDashboard
                {
                    GeneratedAt = DateTime.UtcNow,
                    OpenIssues = _issues.Values.Count(i => i.Status == IssueStatus.Open),
                    InProgressIssues = _issues.Values.Count(i => i.Status == IssueStatus.InProgress),
                    ResolvedThisWeek = _issues.Values.Count(i =>
                        i.ResolvedDate.HasValue && i.ResolvedDate.Value >= DateTime.UtcNow.AddDays(-7)),
                    OverdueIssues = _issues.Values.Count(i =>
                        i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed &&
                        i.DueDate.HasValue && i.DueDate.Value < DateTime.UtcNow),
                    TotalClashGroups = _clashGroups.Count,
                    UnresolvedClashGroups = _clashGroups.Values.Count(g =>
                        g.Status != ClashGroupStatus.Resolved),
                    UpcomingMeetings = GetUpcomingMeetings(7).Count,
                    OverdueActionItems = GetOverdueActionItems().Count,
                    IssuesByPriority = _issues.Values
                        .Where(i => i.Status != IssueStatus.Closed)
                        .GroupBy(i => i.Priority)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    IssuesByDiscipline = _issues.Values
                        .Where(i => i.Status != IssueStatus.Closed)
                        .SelectMany(i => i.AffectedDisciplines)
                        .GroupBy(d => d)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    RecentActivity = GetRecentActivity(10)
                };
            }
        }

        private List<CoordinationLog> GetRecentActivity(int count)
        {
            return _coordinationLog
                .OrderByDescending(l => l.Timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Generate coordination report
        /// </summary>
        public CoordinationReport GenerateReport(DateTime fromDate, DateTime toDate)
        {
            lock (_lock)
            {
                var issuesInPeriod = _issues.Values
                    .Where(i => i.CreatedDate >= fromDate && i.CreatedDate <= toDate)
                    .ToList();

                var resolvedInPeriod = _issues.Values
                    .Where(i => i.ResolvedDate.HasValue &&
                        i.ResolvedDate.Value >= fromDate && i.ResolvedDate.Value <= toDate)
                    .ToList();

                return new CoordinationReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    PeriodStart = fromDate,
                    PeriodEnd = toDate,
                    IssuesCreated = issuesInPeriod.Count,
                    IssuesResolved = resolvedInPeriod.Count,
                    AverageResolutionTime = resolvedInPeriod.Any() ?
                        TimeSpan.FromHours(resolvedInPeriod
                            .Where(i => i.ResolvedDate.HasValue)
                            .Average(i => (i.ResolvedDate.Value - i.CreatedDate).TotalHours)) :
                        TimeSpan.Zero,
                    MeetingsHeld = _meetings.Values.Count(m =>
                        m.Status == MeetingStatus.Completed &&
                        m.ScheduledDate >= fromDate && m.ScheduledDate <= toDate),
                    ActionItemsCompleted = _actionItems.Values.Count(a =>
                        a.CompletedDate.HasValue &&
                        a.CompletedDate.Value >= fromDate && a.CompletedDate.Value <= toDate),
                    TopIssueTypes = issuesInPeriod
                        .GroupBy(i => i.Type)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TopAffectedDisciplines = issuesInPeriod
                        .SelectMany(i => i.AffectedDisciplines)
                        .GroupBy(d => d)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        private void LogCoordinationAction(string entityId, string entityType, string action,
            string description, string user)
        {
            _coordinationLog.Add(new CoordinationLog
            {
                LogId = Guid.NewGuid().ToString(),
                EntityId = entityId,
                EntityType = entityType,
                Action = action,
                Description = description,
                User = user,
                Timestamp = DateTime.UtcNow
            });
        }

        #endregion
    }

    #region Data Models

    public class RawClash
    {
        public string ClashId { get; set; }
        public string Name { get; set; }
        public ClashSeverity Severity { get; set; }
        public string PrimaryElementId { get; set; }
        public string SecondaryElementId { get; set; }
        public string PrimaryTrade { get; set; }
        public string SecondaryTrade { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string GridLocation { get; set; }
        public string SystemType { get; set; }
        public string ResponsibleParty { get; set; }
        public double Distance { get; set; }
        public Point3D Location { get; set; }
    }

    public class ClashGroup
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string GroupKey { get; set; }
        public GroupingStrategy Strategy { get; set; }
        public List<RawClash> Clashes { get; set; }
        public int TotalClashes { get; set; }
        public ClashGroupStatus Status { get; set; }
        public ClashPriority Priority { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string StatusNotes { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; }
        public Dictionary<string, int> ByTrade { get; set; }
    }

    public class ClashGroupingResult
    {
        public DateTime GroupedAt { get; set; }
        public GroupingStrategy Strategy { get; set; }
        public List<ClashGroup> Groups { get; set; }
        public ClashStatistics Statistics { get; set; }
    }

    public class ClashStatistics
    {
        public int TotalClashes { get; set; }
        public int TotalGroups { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; }
        public Dictionary<string, int> ByTrade { get; set; }
        public Dictionary<string, int> ByLevel { get; set; }
        public double AverageClashesPerGroup { get; set; }
    }

    public class CoordinationIssue
    {
        public string IssueId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueType Type { get; set; }
        public IssuePriority Priority { get; set; }
        public IssueStatus Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string ResolvedBy { get; set; }
        public string Resolution { get; set; }
        public double EstimatedHours { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> RelatedClashGroups { get; set; }
        public List<string> RelatedElements { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public List<string> Attachments { get; set; }
        public List<IssueComment> Comments { get; set; }
        public List<IssueHistoryEntry> History { get; set; }
    }

    public class IssueCreationRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueType Type { get; set; }
        public IssuePriority Priority { get; set; }
        public string CreatedBy { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> RelatedClashGroups { get; set; }
        public List<string> RelatedElements { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class IssueComment
    {
        public string CommentId { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class IssueHistoryEntry
    {
        public string EntryId { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
    }

    public class CoordinationMeeting
    {
        public string MeetingId { get; set; }
        public string Title { get; set; }
        public MeetingType Type { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan? ActualDuration { get; set; }
        public string Location { get; set; }
        public string Organizer { get; set; }
        public List<MeetingAttendee> Attendees { get; set; }
        public List<AgendaItem> Agenda { get; set; }
        public List<string> RelatedIssues { get; set; }
        public List<string> RelatedClashGroups { get; set; }
        public MeetingStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Notes { get; set; }
        public MeetingMinutes Minutes { get; set; }
    }

    public class MeetingRequest
    {
        public string Title { get; set; }
        public MeetingType Type { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan? Duration { get; set; }
        public string Location { get; set; }
        public string Organizer { get; set; }
        public List<MeetingAttendee> Attendees { get; set; }
        public List<AgendaItem> Agenda { get; set; }
        public List<string> RelatedIssues { get; set; }
        public List<string> RelatedClashGroups { get; set; }
        public string Notes { get; set; }
    }

    public class MeetingAttendee
    {
        public string Name { get; set; }
        public string Company { get; set; }
        public string Discipline { get; set; }
        public string Role { get; set; }
        public bool Required { get; set; }
        public AttendanceStatus Attendance { get; set; }
    }

    public class AgendaItem
    {
        public int Order { get; set; }
        public string Topic { get; set; }
        public TimeSpan Duration { get; set; }
        public string Presenter { get; set; }
        public string ReferenceId { get; set; }
    }

    public class MeetingMinutes
    {
        public string Summary { get; set; }
        public List<string> Decisions { get; set; }
        public List<ActionItemInput> ActionItems { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class ActionItemInput
    {
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public ActionItemPriority Priority { get; set; }
        public string RelatedIssueId { get; set; }
    }

    public class ActionItem
    {
        public string ActionId { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public ActionItemPriority Priority { get; set; }
        public ActionItemStatus Status { get; set; }
        public string SourceMeetingId { get; set; }
        public string RelatedIssueId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Notes { get; set; }
    }

    public class ModelVersion
    {
        public string VersionId { get; set; }
        public string ModelName { get; set; }
        public string Discipline { get; set; }
        public string Version { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
        public string Author { get; set; }
        public DateTime UploadDate { get; set; }
        public string Description { get; set; }
        public int LOD { get; set; }
        public string Phase { get; set; }
        public ModelVersionStatus Status { get; set; }
    }

    public class ModelVersionRegistration
    {
        public string ModelName { get; set; }
        public string Discipline { get; set; }
        public string Version { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public int LOD { get; set; }
        public string Phase { get; set; }
    }

    public class FederatedModelStatus
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalModels { get; set; }
        public Dictionary<string, List<ModelVersion>> ModelsByDiscipline { get; set; }
        public ModelVersion OldestModel { get; set; }
        public ModelVersion NewestModel { get; set; }
        public double ModelHealth { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class CoordinationDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public int OpenIssues { get; set; }
        public int InProgressIssues { get; set; }
        public int ResolvedThisWeek { get; set; }
        public int OverdueIssues { get; set; }
        public int TotalClashGroups { get; set; }
        public int UnresolvedClashGroups { get; set; }
        public int UpcomingMeetings { get; set; }
        public int OverdueActionItems { get; set; }
        public Dictionary<IssuePriority, int> IssuesByPriority { get; set; }
        public Dictionary<string, int> IssuesByDiscipline { get; set; }
        public List<CoordinationLog> RecentActivity { get; set; }
    }

    public class CoordinationReport
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int IssuesCreated { get; set; }
        public int IssuesResolved { get; set; }
        public TimeSpan AverageResolutionTime { get; set; }
        public int MeetingsHeld { get; set; }
        public int ActionItemsCompleted { get; set; }
        public Dictionary<IssueType, int> TopIssueTypes { get; set; }
        public Dictionary<string, int> TopAffectedDisciplines { get; set; }
    }

    public class CoordinationLog
    {
        public string LogId { get; set; }
        public string EntityId { get; set; }
        public string EntityType { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CoordinationEventArgs : EventArgs
    {
        public CoordinationEventType Type { get; set; }
        public string EntityId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum GroupingStrategy
    {
        ByLocation,
        ByTradePair,
        BySeverity,
        ByGrid,
        BySystem,
        ByResponsibleParty,
        Smart
    }

    public enum ClashSeverity { Low, Medium, High, Critical }
    public enum ClashGroupStatus { New, Assigned, InProgress, Resolved, Deferred }
    public enum ClashPriority { Low, Medium, High, Critical }

    public enum IssueType { Clash, Clearance, Coordination, DesignConflict, RFI, Submittal, Other }
    public enum IssuePriority { Low, Medium, High, Critical }
    public enum IssueStatus { Open, InProgress, OnHold, Resolved, Closed }

    public enum MeetingType { Coordination, ClashResolution, DesignReview, Kickoff, Progress, Closeout }
    public enum MeetingStatus { Scheduled, InProgress, Completed, Cancelled, Postponed }
    public enum AttendanceStatus { Pending, Confirmed, Declined, Attended, NoShow }

    public enum ActionItemPriority { Low, Medium, High, Critical }
    public enum ActionItemStatus { Open, InProgress, Completed, Cancelled, Deferred }

    public enum ModelVersionStatus { Current, Superseded, Archived }

    public enum CoordinationEventType { IssueCreated, IssueResolved, MeetingScheduled, ActionItemDue }

    #endregion
}
