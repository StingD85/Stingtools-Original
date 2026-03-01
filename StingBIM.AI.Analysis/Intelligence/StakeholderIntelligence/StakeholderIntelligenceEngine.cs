// ===================================================================
// StingBIM Stakeholder Intelligence Engine - Client CRM & Stakeholder Management
// Stakeholder mapping, communication preferences, influence analysis, meeting management
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.StakeholderIntelligence
{
    /// <summary>
    /// Comprehensive stakeholder management with CRM capabilities,
    /// influence mapping, communication tracking, and relationship intelligence
    /// </summary>
    public sealed class StakeholderIntelligenceEngine
    {
        private static readonly Lazy<StakeholderIntelligenceEngine> _instance =
            new Lazy<StakeholderIntelligenceEngine>(() => new StakeholderIntelligenceEngine());
        public static StakeholderIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, StakeholderProject> _projects;
        private readonly Dictionary<string, Stakeholder> _stakeholders;
        private readonly Dictionary<string, Organization> _organizations;
        private readonly Dictionary<string, Meeting> _meetings;
        private readonly Dictionary<string, Communication> _communications;
        private readonly List<CommunicationTemplate> _templates;
        private readonly object _lockObject = new object();

        public event EventHandler<StakeholderAlertEventArgs> StakeholderAlert;
        public event EventHandler<MeetingReminderEventArgs> MeetingReminder;
        public event EventHandler<RelationshipChangeEventArgs> RelationshipChanged;

        private StakeholderIntelligenceEngine()
        {
            _projects = new Dictionary<string, StakeholderProject>();
            _stakeholders = new Dictionary<string, Stakeholder>();
            _organizations = new Dictionary<string, Organization>();
            _meetings = new Dictionary<string, Meeting>();
            _communications = new Dictionary<string, Communication>();
            _templates = new List<CommunicationTemplate>();
            InitializeTemplates();
        }

        #region Stakeholder Management

        public Stakeholder AddStakeholder(StakeholderInfo info)
        {
            var stakeholder = new Stakeholder
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = info.FirstName,
                LastName = info.LastName,
                Email = info.Email,
                Phone = info.Phone,
                Title = info.Title,
                OrganizationId = info.OrganizationId,
                Role = info.Role,
                Influence = info.Influence,
                Interest = info.Interest,
                CommunicationPreference = info.CommunicationPreference ?? CommunicationPreference.Email,
                PreferredMeetingTime = info.PreferredMeetingTime,
                Notes = info.Notes,
                CreatedDate = DateTime.Now,
                Tags = info.Tags ?? new List<string>(),
                CustomFields = info.CustomFields ?? new Dictionary<string, string>()
            };

            // Calculate stakeholder quadrant
            stakeholder.Quadrant = DetermineQuadrant(stakeholder.Influence, stakeholder.Interest);

            lock (_lockObject)
            {
                _stakeholders[stakeholder.Id] = stakeholder;

                // Update organization if specified
                if (!string.IsNullOrEmpty(info.OrganizationId) &&
                    _organizations.TryGetValue(info.OrganizationId, out var org))
                {
                    org.StakeholderIds.Add(stakeholder.Id);
                }
            }

            return stakeholder;
        }

        public Organization AddOrganization(OrganizationInfo info)
        {
            var org = new Organization
            {
                Id = Guid.NewGuid().ToString(),
                Name = info.Name,
                Type = info.Type,
                Industry = info.Industry,
                Website = info.Website,
                Address = info.Address,
                PrimaryContactId = info.PrimaryContactId,
                StakeholderIds = new List<string>(),
                ProjectHistory = new List<string>(),
                Notes = info.Notes,
                Rating = info.Rating,
                CreatedDate = DateTime.Now
            };

            lock (_lockObject)
            {
                _organizations[org.Id] = org;
            }

            return org;
        }

        public void AssignStakeholderToProject(string projectId, string stakeholderId, ProjectRole role)
        {
            lock (_lockObject)
            {
                if (_projects.TryGetValue(projectId, out var project) &&
                    _stakeholders.TryGetValue(stakeholderId, out var stakeholder))
                {
                    var assignment = new StakeholderAssignment
                    {
                        StakeholderId = stakeholderId,
                        ProjectId = projectId,
                        Role = role,
                        AssignedDate = DateTime.Now,
                        IsActive = true
                    };

                    project.Assignments.Add(assignment);
                    stakeholder.ProjectAssignments.Add(assignment);
                }
            }
        }

        public StakeholderProject CreateProject(string projectId, string projectName)
        {
            var project = new StakeholderProject
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ProjectName = projectName,
                CreatedDate = DateTime.Now,
                Status = ProjectStatus.Active,
                Assignments = new List<StakeholderAssignment>(),
                Communications = new List<string>(),
                Meetings = new List<string>()
            };

            lock (_lockObject)
            {
                _projects[project.Id] = project;
            }

            return project;
        }

        #endregion

        #region Stakeholder Mapping & Analysis

        public StakeholderMap GenerateStakeholderMap(string projectId)
        {
            lock (_lockObject)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var map = new StakeholderMap
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,
                    Quadrants = new Dictionary<StakeholderQuadrant, List<StakeholderSummary>>()
                };

                // Initialize quadrants
                foreach (StakeholderQuadrant quadrant in Enum.GetValues<StakeholderQuadrant>())
                {
                    map.Quadrants[quadrant] = new List<StakeholderSummary>();
                }

                // Map stakeholders to quadrants
                foreach (var assignment in project.Assignments.Where(a => a.IsActive))
                {
                    if (_stakeholders.TryGetValue(assignment.StakeholderId, out var stakeholder))
                    {
                        var summary = new StakeholderSummary
                        {
                            Id = stakeholder.Id,
                            Name = $"{stakeholder.FirstName} {stakeholder.LastName}",
                            Title = stakeholder.Title,
                            Organization = GetOrganizationName(stakeholder.OrganizationId),
                            Role = assignment.Role,
                            Influence = stakeholder.Influence,
                            Interest = stakeholder.Interest,
                            EngagementLevel = CalculateEngagementLevel(stakeholder, projectId),
                            LastContact = GetLastContactDate(stakeholder.Id, projectId)
                        };

                        map.Quadrants[stakeholder.Quadrant].Add(summary);
                    }
                }

                // Calculate statistics
                var allStakeholders = map.Quadrants.Values.SelectMany(q => q).ToList();
                map.TotalStakeholders = allStakeholders.Count;
                map.HighInfluenceCount = allStakeholders.Count(s => s.Influence >= 7);
                map.LowEngagementCount = allStakeholders.Count(s => s.EngagementLevel < 0.3);
                map.NeedingAttention = allStakeholders.Count(s =>
                    s.Influence >= 7 && s.EngagementLevel < 0.5);

                return map;
            }
        }

        private StakeholderQuadrant DetermineQuadrant(int influence, int interest)
        {
            if (influence >= 5 && interest >= 5)
                return StakeholderQuadrant.ManageClosely;
            if (influence >= 5 && interest < 5)
                return StakeholderQuadrant.KeepSatisfied;
            if (influence < 5 && interest >= 5)
                return StakeholderQuadrant.KeepInformed;
            return StakeholderQuadrant.Monitor;
        }

        public async Task<InfluenceAnalysis> AnalyzeInfluenceNetworkAsync(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var analysis = new InfluenceAnalysis
                    {
                        ProjectId = projectId,
                        AnalysisDate = DateTime.Now,
                        Nodes = new List<InfluenceNode>(),
                        Connections = new List<InfluenceConnection>(),
                        KeyInfluencers = new List<string>(),
                        Recommendations = new List<string>()
                    };

                    // Build influence network
                    foreach (var assignment in project.Assignments.Where(a => a.IsActive))
                    {
                        if (_stakeholders.TryGetValue(assignment.StakeholderId, out var stakeholder))
                        {
                            var node = new InfluenceNode
                            {
                                StakeholderId = stakeholder.Id,
                                Name = $"{stakeholder.FirstName} {stakeholder.LastName}",
                                Organization = GetOrganizationName(stakeholder.OrganizationId),
                                Influence = stakeholder.Influence,
                                Interest = stakeholder.Interest,
                                Role = assignment.Role,
                                ConnectionCount = 0
                            };
                            analysis.Nodes.Add(node);
                        }
                    }

                    // Identify connections based on organization and meeting co-attendance
                    foreach (var node1 in analysis.Nodes)
                    {
                        foreach (var node2 in analysis.Nodes.Where(n => n.StakeholderId != node1.StakeholderId))
                        {
                            var strength = CalculateConnectionStrength(node1.StakeholderId, node2.StakeholderId, projectId);
                            if (strength > 0)
                            {
                                if (!analysis.Connections.Any(c =>
                                    (c.FromId == node1.StakeholderId && c.ToId == node2.StakeholderId) ||
                                    (c.FromId == node2.StakeholderId && c.ToId == node1.StakeholderId)))
                                {
                                    analysis.Connections.Add(new InfluenceConnection
                                    {
                                        FromId = node1.StakeholderId,
                                        ToId = node2.StakeholderId,
                                        Strength = strength,
                                        Type = DetermineConnectionType(node1.StakeholderId, node2.StakeholderId)
                                    });
                                    node1.ConnectionCount++;
                                    node2.ConnectionCount++;
                                }
                            }
                        }
                    }

                    // Identify key influencers
                    analysis.KeyInfluencers = analysis.Nodes
                        .OrderByDescending(n => n.Influence * (1 + n.ConnectionCount * 0.1))
                        .Take(5)
                        .Select(n => n.StakeholderId)
                        .ToList();

                    // Generate recommendations
                    GenerateInfluenceRecommendations(analysis);

                    return analysis;
                }
            });
        }

        private double CalculateConnectionStrength(string stakeholder1Id, string stakeholder2Id, string projectId)
        {
            var strength = 0.0;

            // Same organization
            if (_stakeholders.TryGetValue(stakeholder1Id, out var s1) &&
                _stakeholders.TryGetValue(stakeholder2Id, out var s2))
            {
                if (s1.OrganizationId == s2.OrganizationId && !string.IsNullOrEmpty(s1.OrganizationId))
                    strength += 0.5;
            }

            // Co-attendance at meetings
            var sharedMeetings = _meetings.Values
                .Where(m => m.ProjectId == projectId)
                .Count(m => m.AttendeeIds.Contains(stakeholder1Id) && m.AttendeeIds.Contains(stakeholder2Id));

            strength += Math.Min(sharedMeetings * 0.1, 0.5);

            return strength;
        }

        private ConnectionType DetermineConnectionType(string stakeholder1Id, string stakeholder2Id)
        {
            if (_stakeholders.TryGetValue(stakeholder1Id, out var s1) &&
                _stakeholders.TryGetValue(stakeholder2Id, out var s2))
            {
                if (s1.OrganizationId == s2.OrganizationId)
                    return ConnectionType.Organizational;
            }
            return ConnectionType.Professional;
        }

        private void GenerateInfluenceRecommendations(InfluenceAnalysis analysis)
        {
            // High influence, low engagement
            var highInfluenceLowConnect = analysis.Nodes
                .Where(n => n.Influence >= 7 && n.ConnectionCount < 2)
                .ToList();

            foreach (var node in highInfluenceLowConnect)
            {
                analysis.Recommendations.Add($"Increase engagement with {node.Name} - high influence but few connections");
            }

            // Isolated stakeholders
            var isolated = analysis.Nodes.Where(n => n.ConnectionCount == 0).ToList();
            foreach (var node in isolated)
            {
                analysis.Recommendations.Add($"Connect {node.Name} with project team - currently isolated");
            }

            // Bridge opportunities
            var orgs = analysis.Nodes.GroupBy(n => n.Organization).Where(g => g.Count() >= 2).ToList();
            if (orgs.Count >= 2)
            {
                analysis.Recommendations.Add("Consider cross-organization meetings to strengthen project alignment");
            }
        }

        #endregion

        #region Communication Management

        public Communication LogCommunication(CommunicationLog log)
        {
            var communication = new Communication
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = log.ProjectId,
                Type = log.Type,
                Direction = log.Direction,
                Subject = log.Subject,
                Content = log.Content,
                StakeholderIds = log.StakeholderIds,
                Date = log.Date ?? DateTime.Now,
                CreatedBy = log.CreatedBy,
                Attachments = log.Attachments ?? new List<string>(),
                Tags = log.Tags ?? new List<string>(),
                Sentiment = AnalyzeSentiment(log.Content),
                FollowUpRequired = log.FollowUpRequired,
                FollowUpDate = log.FollowUpDate
            };

            lock (_lockObject)
            {
                _communications[communication.Id] = communication;

                // Update project
                if (_projects.TryGetValue(log.ProjectId, out var project))
                {
                    project.Communications.Add(communication.Id);
                }

                // Update stakeholders
                foreach (var stakeholderId in log.StakeholderIds)
                {
                    if (_stakeholders.TryGetValue(stakeholderId, out var stakeholder))
                    {
                        stakeholder.CommunicationIds.Add(communication.Id);
                        stakeholder.LastContactDate = communication.Date;
                    }
                }
            }

            return communication;
        }

        public CommunicationPlan GenerateCommunicationPlan(string projectId)
        {
            lock (_lockObject)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var plan = new CommunicationPlan
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,
                    Strategies = new List<CommunicationStrategy>(),
                    Schedule = new List<ScheduledCommunication>()
                };

                // Generate strategies based on stakeholder quadrants
                foreach (var assignment in project.Assignments.Where(a => a.IsActive))
                {
                    if (_stakeholders.TryGetValue(assignment.StakeholderId, out var stakeholder))
                    {
                        var strategy = new CommunicationStrategy
                        {
                            StakeholderId = stakeholder.Id,
                            StakeholderName = $"{stakeholder.FirstName} {stakeholder.LastName}",
                            Quadrant = stakeholder.Quadrant,
                            PreferredChannel = stakeholder.CommunicationPreference,
                            Frequency = DetermineFrequency(stakeholder.Quadrant),
                            ContentType = DetermineContentType(stakeholder.Quadrant),
                            KeyMessages = GenerateKeyMessages(stakeholder, assignment.Role)
                        };
                        plan.Strategies.Add(strategy);

                        // Generate scheduled communications
                        var nextDate = DateTime.Today;
                        var frequencyDays = strategy.Frequency switch
                        {
                            CommunicationFrequency.Daily => 1,
                            CommunicationFrequency.Weekly => 7,
                            CommunicationFrequency.BiWeekly => 14,
                            CommunicationFrequency.Monthly => 30,
                            CommunicationFrequency.Quarterly => 90,
                            _ => 30
                        };

                        for (int i = 0; i < 4; i++)
                        {
                            nextDate = nextDate.AddDays(frequencyDays);
                            plan.Schedule.Add(new ScheduledCommunication
                            {
                                StakeholderId = stakeholder.Id,
                                ScheduledDate = nextDate,
                                Type = strategy.PreferredChannel == CommunicationPreference.Phone
                                    ? CommunicationType.Call : CommunicationType.Email,
                                Topic = strategy.ContentType.ToString()
                            });
                        }
                    }
                }

                return plan;
            }
        }

        private CommunicationFrequency DetermineFrequency(StakeholderQuadrant quadrant)
        {
            return quadrant switch
            {
                StakeholderQuadrant.ManageClosely => CommunicationFrequency.Weekly,
                StakeholderQuadrant.KeepSatisfied => CommunicationFrequency.BiWeekly,
                StakeholderQuadrant.KeepInformed => CommunicationFrequency.BiWeekly,
                StakeholderQuadrant.Monitor => CommunicationFrequency.Monthly,
                _ => CommunicationFrequency.Monthly
            };
        }

        private ContentType DetermineContentType(StakeholderQuadrant quadrant)
        {
            return quadrant switch
            {
                StakeholderQuadrant.ManageClosely => ContentType.DetailedReports,
                StakeholderQuadrant.KeepSatisfied => ContentType.ExecutiveSummary,
                StakeholderQuadrant.KeepInformed => ContentType.ProgressUpdates,
                StakeholderQuadrant.Monitor => ContentType.MilestoneNotifications,
                _ => ContentType.GeneralUpdates
            };
        }

        private List<string> GenerateKeyMessages(Stakeholder stakeholder, ProjectRole role)
        {
            var messages = new List<string>();

            switch (role)
            {
                case ProjectRole.Owner:
                    messages.Add("Project ROI and value delivery");
                    messages.Add("Schedule and budget status");
                    messages.Add("Risk management and mitigation");
                    break;
                case ProjectRole.Architect:
                    messages.Add("Design coordination updates");
                    messages.Add("Material and specification changes");
                    messages.Add("BIM coordination progress");
                    break;
                case ProjectRole.Contractor:
                    messages.Add("Construction schedule updates");
                    messages.Add("RFI and submittal status");
                    messages.Add("Site conditions and issues");
                    break;
                case ProjectRole.Engineer:
                    messages.Add("Technical coordination");
                    messages.Add("System design updates");
                    messages.Add("Code compliance status");
                    break;
                default:
                    messages.Add("Project progress updates");
                    messages.Add("Milestone achievements");
                    break;
            }

            return messages;
        }

        private SentimentType AnalyzeSentiment(string content)
        {
            if (string.IsNullOrEmpty(content))
                return SentimentType.Neutral;

            var positiveWords = new[] { "excellent", "great", "good", "pleased", "satisfied", "thank", "appreciate" };
            var negativeWords = new[] { "concern", "issue", "problem", "disappointed", "delay", "frustrated", "urgent" };

            var lowerContent = content.ToLower();
            var positiveCount = positiveWords.Count(w => lowerContent.Contains(w));
            var negativeCount = negativeWords.Count(w => lowerContent.Contains(w));

            if (positiveCount > negativeCount) return SentimentType.Positive;
            if (negativeCount > positiveCount) return SentimentType.Negative;
            return SentimentType.Neutral;
        }

        #endregion

        #region Meeting Management

        public Meeting ScheduleMeeting(MeetingRequest request)
        {
            var meeting = new Meeting
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                ScheduledDate = request.ScheduledDate,
                Duration = request.Duration,
                Location = request.Location,
                VirtualLink = request.VirtualLink,
                OrganizerIds = request.OrganizerIds,
                AttendeeIds = request.AttendeeIds,
                Agenda = request.Agenda ?? new List<AgendaItem>(),
                Status = MeetingStatus.Scheduled,
                CreatedDate = DateTime.Now
            };

            lock (_lockObject)
            {
                _meetings[meeting.Id] = meeting;

                if (_projects.TryGetValue(request.ProjectId, out var project))
                {
                    project.Meetings.Add(meeting.Id);
                }
            }

            return meeting;
        }

        public MeetingMinutes RecordMeetingMinutes(string meetingId, MeetingMinutesInput input)
        {
            lock (_lockObject)
            {
                if (!_meetings.TryGetValue(meetingId, out var meeting))
                    return null;

                var minutes = new MeetingMinutes
                {
                    Id = Guid.NewGuid().ToString(),
                    MeetingId = meetingId,
                    RecordedBy = input.RecordedBy,
                    RecordedDate = DateTime.Now,
                    ActualAttendees = input.ActualAttendees,
                    Absentees = meeting.AttendeeIds.Except(input.ActualAttendees).ToList(),
                    DiscussionPoints = input.DiscussionPoints,
                    Decisions = input.Decisions,
                    ActionItems = input.ActionItems.Select(a => new ActionItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Description = a.Description,
                        AssigneeId = a.AssigneeId,
                        DueDate = a.DueDate,
                        Priority = a.Priority,
                        Status = ActionItemStatus.Open,
                        CreatedDate = DateTime.Now
                    }).ToList(),
                    NextMeetingDate = input.NextMeetingDate
                };

                meeting.Minutes = minutes;
                meeting.Status = MeetingStatus.Completed;

                return minutes;
            }
        }

        public MeetingAnalytics GetMeetingAnalytics(string projectId)
        {
            lock (_lockObject)
            {
                var projectMeetings = _meetings.Values
                    .Where(m => m.ProjectId == projectId)
                    .ToList();

                var analytics = new MeetingAnalytics
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,
                    TotalMeetings = projectMeetings.Count,
                    CompletedMeetings = projectMeetings.Count(m => m.Status == MeetingStatus.Completed),
                    CancelledMeetings = projectMeetings.Count(m => m.Status == MeetingStatus.Cancelled),
                    TotalMeetingHours = projectMeetings.Sum(m => m.Duration.TotalHours),
                    AttendanceRates = new Dictionary<string, double>(),
                    MeetingsByType = new Dictionary<MeetingType, int>(),
                    ActionItemStats = new ActionItemStatistics()
                };

                // Attendance rates by stakeholder
                var allAttendees = projectMeetings
                    .SelectMany(m => m.AttendeeIds)
                    .Distinct()
                    .ToList();

                foreach (var attendeeId in allAttendees)
                {
                    var invitedCount = projectMeetings.Count(m => m.AttendeeIds.Contains(attendeeId));
                    var attendedCount = projectMeetings
                        .Where(m => m.Minutes != null)
                        .Count(m => m.Minutes.ActualAttendees.Contains(attendeeId));

                    analytics.AttendanceRates[attendeeId] = invitedCount > 0
                        ? (attendedCount * 100.0 / invitedCount) : 0;
                }

                // Meetings by type
                foreach (MeetingType type in Enum.GetValues<MeetingType>())
                {
                    analytics.MeetingsByType[type] = projectMeetings.Count(m => m.Type == type);
                }

                // Action item statistics
                var allActionItems = projectMeetings
                    .Where(m => m.Minutes != null)
                    .SelectMany(m => m.Minutes.ActionItems)
                    .ToList();

                analytics.ActionItemStats = new ActionItemStatistics
                {
                    Total = allActionItems.Count,
                    Open = allActionItems.Count(a => a.Status == ActionItemStatus.Open),
                    InProgress = allActionItems.Count(a => a.Status == ActionItemStatus.InProgress),
                    Completed = allActionItems.Count(a => a.Status == ActionItemStatus.Completed),
                    Overdue = allActionItems.Count(a => a.Status != ActionItemStatus.Completed && a.DueDate < DateTime.Today)
                };

                // Average attendance
                analytics.AverageAttendance = projectMeetings.Any()
                    ? projectMeetings.Average(m => m.AttendeeIds.Count) : 0;

                return analytics;
            }
        }

        #endregion

        #region Relationship Management

        public RelationshipScore CalculateRelationshipScore(string stakeholderId, string projectId)
        {
            lock (_lockObject)
            {
                if (!_stakeholders.TryGetValue(stakeholderId, out var stakeholder))
                    return null;

                var score = new RelationshipScore
                {
                    StakeholderId = stakeholderId,
                    ProjectId = projectId,
                    CalculatedDate = DateTime.Now,
                    Components = new Dictionary<string, double>()
                };

                // Communication frequency score
                var recentComms = _communications.Values
                    .Where(c => c.ProjectId == projectId &&
                               c.StakeholderIds.Contains(stakeholderId) &&
                               c.Date >= DateTime.Now.AddDays(-30))
                    .Count();
                score.Components["CommunicationFrequency"] = Math.Min(recentComms / 5.0, 1.0);

                // Meeting attendance score
                var meetings = _meetings.Values
                    .Where(m => m.ProjectId == projectId &&
                               m.AttendeeIds.Contains(stakeholderId) &&
                               m.Minutes != null)
                    .ToList();
                var attended = meetings.Count(m => m.Minutes.ActualAttendees.Contains(stakeholderId));
                score.Components["MeetingAttendance"] = meetings.Any()
                    ? (attended / (double)meetings.Count) : 0.5;

                // Response time score (simulated)
                score.Components["ResponseTime"] = 0.7;

                // Engagement quality score
                var comms = _communications.Values
                    .Where(c => c.ProjectId == projectId && c.StakeholderIds.Contains(stakeholderId))
                    .ToList();
                var positiveComms = comms.Count(c => c.Sentiment == SentimentType.Positive);
                var negativeComms = comms.Count(c => c.Sentiment == SentimentType.Negative);
                score.Components["EngagementQuality"] = comms.Any()
                    ? (positiveComms - negativeComms * 0.5) / comms.Count + 0.5 : 0.5;

                // Calculate overall score
                score.OverallScore = score.Components.Values.Average();
                score.Trend = DetermineRelationshipTrend(stakeholderId, projectId);

                return score;
            }
        }

        private RelationshipTrend DetermineRelationshipTrend(string stakeholderId, string projectId)
        {
            // Analyze recent vs older communication patterns
            var recentComms = _communications.Values
                .Where(c => c.ProjectId == projectId &&
                           c.StakeholderIds.Contains(stakeholderId) &&
                           c.Date >= DateTime.Now.AddDays(-30))
                .Count();

            var olderComms = _communications.Values
                .Where(c => c.ProjectId == projectId &&
                           c.StakeholderIds.Contains(stakeholderId) &&
                           c.Date >= DateTime.Now.AddDays(-60) &&
                           c.Date < DateTime.Now.AddDays(-30))
                .Count();

            if (recentComms > olderComms * 1.2) return RelationshipTrend.Improving;
            if (recentComms < olderComms * 0.8) return RelationshipTrend.Declining;
            return RelationshipTrend.Stable;
        }

        public List<RelationshipAlert> GetRelationshipAlerts(string projectId)
        {
            var alerts = new List<RelationshipAlert>();

            lock (_lockObject)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return alerts;

                foreach (var assignment in project.Assignments.Where(a => a.IsActive))
                {
                    if (_stakeholders.TryGetValue(assignment.StakeholderId, out var stakeholder))
                    {
                        // No contact in 14 days for high-influence stakeholders
                        if (stakeholder.Influence >= 7 &&
                            stakeholder.LastContactDate < DateTime.Now.AddDays(-14))
                        {
                            alerts.Add(new RelationshipAlert
                            {
                                StakeholderId = stakeholder.Id,
                                StakeholderName = $"{stakeholder.FirstName} {stakeholder.LastName}",
                                Type = AlertType.NoRecentContact,
                                Severity = AlertSeverity.High,
                                Message = "No contact in over 14 days with high-influence stakeholder",
                                RecommendedAction = "Schedule check-in call or send project update"
                            });
                        }

                        // Negative sentiment trend
                        var recentComms = _communications.Values
                            .Where(c => c.ProjectId == projectId &&
                                       c.StakeholderIds.Contains(stakeholder.Id) &&
                                       c.Date >= DateTime.Now.AddDays(-30))
                            .ToList();

                        var negativeCount = recentComms.Count(c => c.Sentiment == SentimentType.Negative);
                        if (negativeCount >= 2)
                        {
                            alerts.Add(new RelationshipAlert
                            {
                                StakeholderId = stakeholder.Id,
                                StakeholderName = $"{stakeholder.FirstName} {stakeholder.LastName}",
                                Type = AlertType.NegativeSentiment,
                                Severity = AlertSeverity.High,
                                Message = $"{negativeCount} negative communications in the last 30 days",
                                RecommendedAction = "Review concerns and schedule relationship repair meeting"
                            });
                        }

                        // Low meeting attendance
                        var meetings = _meetings.Values
                            .Where(m => m.ProjectId == projectId &&
                                       m.AttendeeIds.Contains(stakeholder.Id) &&
                                       m.Minutes != null)
                            .ToList();

                        if (meetings.Count >= 3)
                        {
                            var attended = meetings.Count(m => m.Minutes.ActualAttendees.Contains(stakeholder.Id));
                            if (attended < meetings.Count * 0.5)
                            {
                                alerts.Add(new RelationshipAlert
                                {
                                    StakeholderId = stakeholder.Id,
                                    StakeholderName = $"{stakeholder.FirstName} {stakeholder.LastName}",
                                    Type = AlertType.LowMeetingAttendance,
                                    Severity = AlertSeverity.Medium,
                                    Message = $"Attended only {attended} of {meetings.Count} meetings",
                                    RecommendedAction = "Review meeting times and consider alternative engagement methods"
                                });
                            }
                        }
                    }
                }
            }

            return alerts.OrderByDescending(a => a.Severity).ToList();
        }

        #endregion

        #region Helper Methods

        private double CalculateEngagementLevel(Stakeholder stakeholder, string projectId)
        {
            var score = 0.0;
            var factors = 0;

            // Communication factor
            var comms = _communications.Values
                .Where(c => c.ProjectId == projectId &&
                           c.StakeholderIds.Contains(stakeholder.Id) &&
                           c.Date >= DateTime.Now.AddDays(-30))
                .Count();
            score += Math.Min(comms / 10.0, 1.0);
            factors++;

            // Meeting attendance factor
            var meetings = _meetings.Values
                .Where(m => m.ProjectId == projectId &&
                           m.AttendeeIds.Contains(stakeholder.Id) &&
                           m.Minutes != null)
                .ToList();
            if (meetings.Any())
            {
                var attended = meetings.Count(m => m.Minutes.ActualAttendees.Contains(stakeholder.Id));
                score += attended / (double)meetings.Count;
                factors++;
            }

            // Recency factor
            if (stakeholder.LastContactDate != default)
            {
                var daysSinceContact = (DateTime.Now - stakeholder.LastContactDate).Days;
                score += Math.Max(0, 1 - (daysSinceContact / 30.0));
                factors++;
            }

            return factors > 0 ? score / factors : 0;
        }

        private DateTime GetLastContactDate(string stakeholderId, string projectId)
        {
            var lastComm = _communications.Values
                .Where(c => c.ProjectId == projectId && c.StakeholderIds.Contains(stakeholderId))
                .OrderByDescending(c => c.Date)
                .FirstOrDefault();

            return lastComm?.Date ?? DateTime.MinValue;
        }

        private string GetOrganizationName(string organizationId)
        {
            if (string.IsNullOrEmpty(organizationId))
                return "Independent";

            return _organizations.TryGetValue(organizationId, out var org) ? org.Name : "Unknown";
        }

        private void InitializeTemplates()
        {
            _templates.AddRange(new[]
            {
                new CommunicationTemplate
                {
                    Id = "project-update",
                    Name = "Project Update",
                    Type = CommunicationType.Email,
                    Subject = "Project Update - {{ProjectName}}",
                    Body = "Dear {{StakeholderName}},\n\nPlease find below the latest update on {{ProjectName}}..."
                },
                new CommunicationTemplate
                {
                    Id = "meeting-invite",
                    Name = "Meeting Invitation",
                    Type = CommunicationType.Email,
                    Subject = "Meeting Invitation: {{MeetingTitle}}",
                    Body = "You are invited to attend {{MeetingTitle}} on {{MeetingDate}}..."
                },
                new CommunicationTemplate
                {
                    Id = "milestone-notification",
                    Name = "Milestone Notification",
                    Type = CommunicationType.Email,
                    Subject = "Milestone Achieved - {{MilestoneName}}",
                    Body = "We are pleased to inform you that {{MilestoneName}} has been successfully completed..."
                }
            });
        }

        #endregion

        #region Events

        private void OnStakeholderAlert(StakeholderAlertEventArgs e)
        {
            StakeholderAlert?.Invoke(this, e);
        }

        private void OnMeetingReminder(MeetingReminderEventArgs e)
        {
            MeetingReminder?.Invoke(this, e);
        }

        private void OnRelationshipChanged(RelationshipChangeEventArgs e)
        {
            RelationshipChanged?.Invoke(this, e);
        }

        #endregion
    }

    #region Data Models

    public class Stakeholder
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Title { get; set; }
        public string OrganizationId { get; set; }
        public ProjectRole Role { get; set; }
        public int Influence { get; set; }
        public int Interest { get; set; }
        public StakeholderQuadrant Quadrant { get; set; }
        public CommunicationPreference CommunicationPreference { get; set; }
        public string PreferredMeetingTime { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastContactDate { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();
        public List<StakeholderAssignment> ProjectAssignments { get; set; } = new List<StakeholderAssignment>();
        public List<string> CommunicationIds { get; set; } = new List<string>();
    }

    public class StakeholderInfo
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Title { get; set; }
        public string OrganizationId { get; set; }
        public ProjectRole Role { get; set; }
        public int Influence { get; set; }
        public int Interest { get; set; }
        public CommunicationPreference? CommunicationPreference { get; set; }
        public string PreferredMeetingTime { get; set; }
        public string Notes { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, string> CustomFields { get; set; }
    }

    public class Organization
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public OrganizationType Type { get; set; }
        public string Industry { get; set; }
        public string Website { get; set; }
        public string Address { get; set; }
        public string PrimaryContactId { get; set; }
        public List<string> StakeholderIds { get; set; }
        public List<string> ProjectHistory { get; set; }
        public string Notes { get; set; }
        public int Rating { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class OrganizationInfo
    {
        public string Name { get; set; }
        public OrganizationType Type { get; set; }
        public string Industry { get; set; }
        public string Website { get; set; }
        public string Address { get; set; }
        public string PrimaryContactId { get; set; }
        public string Notes { get; set; }
        public int Rating { get; set; }
    }

    public class StakeholderProject
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<StakeholderAssignment> Assignments { get; set; }
        public List<string> Communications { get; set; }
        public List<string> Meetings { get; set; }
    }

    public class StakeholderAssignment
    {
        public string StakeholderId { get; set; }
        public string ProjectId { get; set; }
        public ProjectRole Role { get; set; }
        public DateTime AssignedDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class StakeholderMap
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public Dictionary<StakeholderQuadrant, List<StakeholderSummary>> Quadrants { get; set; }
        public int TotalStakeholders { get; set; }
        public int HighInfluenceCount { get; set; }
        public int LowEngagementCount { get; set; }
        public int NeedingAttention { get; set; }
    }

    public class StakeholderSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Organization { get; set; }
        public ProjectRole Role { get; set; }
        public int Influence { get; set; }
        public int Interest { get; set; }
        public double EngagementLevel { get; set; }
        public DateTime LastContact { get; set; }
    }

    public class InfluenceAnalysis
    {
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<InfluenceNode> Nodes { get; set; }
        public List<InfluenceConnection> Connections { get; set; }
        public List<string> KeyInfluencers { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class InfluenceNode
    {
        public string StakeholderId { get; set; }
        public string Name { get; set; }
        public string Organization { get; set; }
        public int Influence { get; set; }
        public int Interest { get; set; }
        public ProjectRole Role { get; set; }
        public int ConnectionCount { get; set; }
    }

    public class InfluenceConnection
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public double Strength { get; set; }
        public ConnectionType Type { get; set; }
    }

    public class Communication
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public CommunicationType Type { get; set; }
        public CommunicationDirection Direction { get; set; }
        public string Subject { get; set; }
        public string Content { get; set; }
        public List<string> StakeholderIds { get; set; }
        public DateTime Date { get; set; }
        public string CreatedBy { get; set; }
        public List<string> Attachments { get; set; }
        public List<string> Tags { get; set; }
        public SentimentType Sentiment { get; set; }
        public bool FollowUpRequired { get; set; }
        public DateTime? FollowUpDate { get; set; }
    }

    public class CommunicationLog
    {
        public string ProjectId { get; set; }
        public CommunicationType Type { get; set; }
        public CommunicationDirection Direction { get; set; }
        public string Subject { get; set; }
        public string Content { get; set; }
        public List<string> StakeholderIds { get; set; }
        public DateTime? Date { get; set; }
        public string CreatedBy { get; set; }
        public List<string> Attachments { get; set; }
        public List<string> Tags { get; set; }
        public bool FollowUpRequired { get; set; }
        public DateTime? FollowUpDate { get; set; }
    }

    public class CommunicationPlan
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public List<CommunicationStrategy> Strategies { get; set; }
        public List<ScheduledCommunication> Schedule { get; set; }
    }

    public class CommunicationStrategy
    {
        public string StakeholderId { get; set; }
        public string StakeholderName { get; set; }
        public StakeholderQuadrant Quadrant { get; set; }
        public CommunicationPreference PreferredChannel { get; set; }
        public CommunicationFrequency Frequency { get; set; }
        public ContentType ContentType { get; set; }
        public List<string> KeyMessages { get; set; }
    }

    public class ScheduledCommunication
    {
        public string StakeholderId { get; set; }
        public DateTime ScheduledDate { get; set; }
        public CommunicationType Type { get; set; }
        public string Topic { get; set; }
    }

    public class CommunicationTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CommunicationType Type { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public class Meeting
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public MeetingType Type { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan Duration { get; set; }
        public string Location { get; set; }
        public string VirtualLink { get; set; }
        public List<string> OrganizerIds { get; set; }
        public List<string> AttendeeIds { get; set; }
        public List<AgendaItem> Agenda { get; set; }
        public MeetingStatus Status { get; set; }
        public MeetingMinutes Minutes { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MeetingRequest
    {
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public MeetingType Type { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan Duration { get; set; }
        public string Location { get; set; }
        public string VirtualLink { get; set; }
        public List<string> OrganizerIds { get; set; }
        public List<string> AttendeeIds { get; set; }
        public List<AgendaItem> Agenda { get; set; }
    }

    public class AgendaItem
    {
        public int Order { get; set; }
        public string Topic { get; set; }
        public string Presenter { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class MeetingMinutes
    {
        public string Id { get; set; }
        public string MeetingId { get; set; }
        public string RecordedBy { get; set; }
        public DateTime RecordedDate { get; set; }
        public List<string> ActualAttendees { get; set; }
        public List<string> Absentees { get; set; }
        public List<DiscussionPoint> DiscussionPoints { get; set; }
        public List<Decision> Decisions { get; set; }
        public List<ActionItem> ActionItems { get; set; }
        public DateTime? NextMeetingDate { get; set; }
    }

    public class MeetingMinutesInput
    {
        public string RecordedBy { get; set; }
        public List<string> ActualAttendees { get; set; }
        public List<DiscussionPoint> DiscussionPoints { get; set; }
        public List<Decision> Decisions { get; set; }
        public List<ActionItemInput> ActionItems { get; set; }
        public DateTime? NextMeetingDate { get; set; }
    }

    public class DiscussionPoint
    {
        public string Topic { get; set; }
        public string Summary { get; set; }
        public List<string> ParticipantIds { get; set; }
    }

    public class Decision
    {
        public string Description { get; set; }
        public List<string> ApprovedBy { get; set; }
        public DateTime DecisionDate { get; set; }
    }

    public class ActionItem
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string AssigneeId { get; set; }
        public DateTime DueDate { get; set; }
        public ActionItemPriority Priority { get; set; }
        public ActionItemStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
    }

    public class ActionItemInput
    {
        public string Description { get; set; }
        public string AssigneeId { get; set; }
        public DateTime DueDate { get; set; }
        public ActionItemPriority Priority { get; set; }
    }

    public class MeetingAnalytics
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalMeetings { get; set; }
        public int CompletedMeetings { get; set; }
        public int CancelledMeetings { get; set; }
        public double TotalMeetingHours { get; set; }
        public double AverageAttendance { get; set; }
        public Dictionary<string, double> AttendanceRates { get; set; }
        public Dictionary<MeetingType, int> MeetingsByType { get; set; }
        public ActionItemStatistics ActionItemStats { get; set; }
    }

    public class ActionItemStatistics
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Overdue { get; set; }
    }

    public class RelationshipScore
    {
        public string StakeholderId { get; set; }
        public string ProjectId { get; set; }
        public DateTime CalculatedDate { get; set; }
        public double OverallScore { get; set; }
        public Dictionary<string, double> Components { get; set; }
        public RelationshipTrend Trend { get; set; }
    }

    public class RelationshipAlert
    {
        public string StakeholderId { get; set; }
        public string StakeholderName { get; set; }
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public string RecommendedAction { get; set; }
    }

    #endregion

    #region Enums

    public enum ProjectRole
    {
        Owner,
        Architect,
        Engineer,
        Contractor,
        Subcontractor,
        Consultant,
        ProjectManager,
        BIMManager,
        Superintendent,
        Inspector,
        RegulatorApprover,
        EndUser,
        Other
    }

    public enum OrganizationType
    {
        Owner,
        Developer,
        ArchitecturalFirm,
        EngineeringFirm,
        GeneralContractor,
        Subcontractor,
        Supplier,
        Consultant,
        Government,
        Utility,
        Other
    }

    public enum ProjectStatus
    {
        Active,
        OnHold,
        Completed,
        Cancelled
    }

    public enum StakeholderQuadrant
    {
        ManageClosely,
        KeepSatisfied,
        KeepInformed,
        Monitor
    }

    public enum CommunicationPreference
    {
        Email,
        Phone,
        InPerson,
        VideoCall,
        TextMessage
    }

    public enum CommunicationType
    {
        Email,
        Call,
        Meeting,
        Letter,
        Report,
        Presentation,
        Other
    }

    public enum CommunicationDirection
    {
        Outbound,
        Inbound,
        Internal
    }

    public enum CommunicationFrequency
    {
        Daily,
        Weekly,
        BiWeekly,
        Monthly,
        Quarterly,
        AsNeeded
    }

    public enum ContentType
    {
        DetailedReports,
        ExecutiveSummary,
        ProgressUpdates,
        MilestoneNotifications,
        GeneralUpdates
    }

    public enum SentimentType
    {
        Positive,
        Neutral,
        Negative
    }

    public enum MeetingType
    {
        Kickoff,
        Progress,
        Coordination,
        Design,
        Technical,
        Executive,
        Closeout,
        Other
    }

    public enum MeetingStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Rescheduled
    }

    public enum ActionItemPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ActionItemStatus
    {
        Open,
        InProgress,
        Completed,
        Cancelled
    }

    public enum ConnectionType
    {
        Organizational,
        Professional,
        Personal
    }

    public enum RelationshipTrend
    {
        Improving,
        Stable,
        Declining
    }

    public enum AlertType
    {
        NoRecentContact,
        NegativeSentiment,
        LowMeetingAttendance,
        MissedDeadline,
        RelationshipDecline
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Event Args

    public class StakeholderAlertEventArgs : EventArgs
    {
        public string StakeholderId { get; set; }
        public AlertType Type { get; set; }
        public string Message { get; set; }
    }

    public class MeetingReminderEventArgs : EventArgs
    {
        public string MeetingId { get; set; }
        public string Title { get; set; }
        public DateTime ScheduledDate { get; set; }
        public int MinutesUntil { get; set; }
    }

    public class RelationshipChangeEventArgs : EventArgs
    {
        public string StakeholderId { get; set; }
        public RelationshipTrend PreviousTrend { get; set; }
        public RelationshipTrend NewTrend { get; set; }
    }

    #endregion
}
