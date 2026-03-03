// =============================================================================
// StingBIM.AI.Collaboration - Collaboration Analytics Engine
// Comprehensive analytics for team productivity, model health,
// collaboration patterns, and performance insights
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.Analytics
{
    /// <summary>
    /// Analytics engine for tracking and reporting on team collaboration metrics.
    /// </summary>
    public class CollaborationAnalytics
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Data stores
        private readonly ConcurrentDictionary<string, List<SessionRecord>> _sessions = new();
        private readonly ConcurrentDictionary<string, List<SyncRecord>> _syncs = new();
        private readonly ConcurrentDictionary<string, List<ConflictRecord>> _conflicts = new();
        private readonly ConcurrentDictionary<string, List<ActivityMetric>> _activities = new();
        private readonly ConcurrentDictionary<DateTime, DailyStats> _dailyStats = new();

        // Current session
        private SessionRecord? _currentSession;
        private readonly string _projectId;
        private readonly string _projectName;

        public CollaborationAnalytics(string projectId, string projectName)
        {
            _projectId = projectId;
            _projectName = projectName;
        }

        #region Session Tracking

        /// <summary>
        /// Start tracking a new work session
        /// </summary>
        public void StartSession(string userId, string username)
        {
            _currentSession = new SessionRecord
            {
                SessionId = Guid.NewGuid().ToString(),
                UserId = userId,
                Username = username,
                ProjectId = _projectId,
                StartTime = DateTime.UtcNow
            };

            if (!_sessions.ContainsKey(userId))
                _sessions[userId] = new List<SessionRecord>();

            _sessions[userId].Add(_currentSession);
            Logger.Info($"Started tracking session for {username}");
        }

        /// <summary>
        /// End current work session
        /// </summary>
        public SessionSummary EndSession()
        {
            if (_currentSession == null)
                return new SessionSummary();

            _currentSession.EndTime = DateTime.UtcNow;
            _currentSession.Duration = _currentSession.EndTime.Value - _currentSession.StartTime;

            var summary = new SessionSummary
            {
                SessionId = _currentSession.SessionId,
                Duration = _currentSession.Duration,
                ElementsEdited = _currentSession.ElementsEdited,
                ViewsAccessed = _currentSession.ViewsAccessed,
                SyncCount = _currentSession.SyncCount,
                ConflictsEncountered = _currentSession.ConflictsEncountered,
                CollaborationScore = CalculateCollaborationScore(_currentSession)
            };

            UpdateDailyStats(_currentSession);
            _currentSession = null;

            return summary;
        }

        /// <summary>
        /// Record an activity in the current session
        /// </summary>
        public void RecordActivity(string userId, string activityType, string? elementId = null, string? details = null)
        {
            if (_currentSession != null && _currentSession.UserId == userId)
            {
                _currentSession.Activities.Add(new SessionActivity
                {
                    Timestamp = DateTime.UtcNow,
                    ActivityType = activityType,
                    ElementId = elementId,
                    Details = details
                });

                if (activityType == "Edit" && elementId != null)
                {
                    _currentSession.ElementsEdited++;
                }
                else if (activityType == "ViewAccess")
                {
                    _currentSession.ViewsAccessed++;
                }
            }

            // Store in activity metrics
            var metric = new ActivityMetric
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                ActivityType = activityType,
                ElementId = elementId,
                Details = details
            };

            if (!_activities.ContainsKey(userId))
                _activities[userId] = new List<ActivityMetric>();

            _activities[userId].Add(metric);

            // Cleanup old data (keep 30 days)
            CleanupOldData();
        }

        /// <summary>
        /// Record a sync operation
        /// </summary>
        public void RecordSync(string userId, int elementsModified, int conflictsResolved, TimeSpan duration)
        {
            var record = new SyncRecord
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                ElementsModified = elementsModified,
                ConflictsResolved = conflictsResolved,
                Duration = duration
            };

            if (!_syncs.ContainsKey(userId))
                _syncs[userId] = new List<SyncRecord>();

            _syncs[userId].Add(record);

            if (_currentSession != null && _currentSession.UserId == userId)
            {
                _currentSession.SyncCount++;
            }
        }

        /// <summary>
        /// Record a conflict
        /// </summary>
        public void RecordConflict(ConflictRecord conflict)
        {
            var key = $"{conflict.User1Id}_{conflict.User2Id}";
            if (!_conflicts.ContainsKey(key))
                _conflicts[key] = new List<ConflictRecord>();

            _conflicts[key].Add(conflict);

            if (_currentSession != null &&
                (_currentSession.UserId == conflict.User1Id || _currentSession.UserId == conflict.User2Id))
            {
                _currentSession.ConflictsEncountered++;
            }
        }

        #endregion

        #region Reports

        /// <summary>
        /// Generate a comprehensive daily report
        /// </summary>
        public DailyReport GenerateDailyReport(DateTime date)
        {
            var report = new DailyReport
            {
                Date = date.Date,
                ProjectId = _projectId,
                ProjectName = _projectName,
                GeneratedAt = DateTime.UtcNow
            };

            // Get all sessions for the day
            var daySessions = _sessions.Values
                .SelectMany(s => s)
                .Where(s => s.StartTime.Date == date.Date)
                .ToList();

            report.TotalSessions = daySessions.Count;
            report.UniqueUsers = daySessions.Select(s => s.UserId).Distinct().Count();
            report.TotalWorkHours = daySessions.Sum(s => s.Duration.TotalHours);
            report.AverageSessionDuration = daySessions.Any()
                ? TimeSpan.FromHours(daySessions.Average(s => s.Duration.TotalHours))
                : TimeSpan.Zero;

            // Productivity metrics
            report.TotalElementsEdited = daySessions.Sum(s => s.ElementsEdited);
            report.TotalViewsAccessed = daySessions.Sum(s => s.ViewsAccessed);
            report.TotalSyncs = daySessions.Sum(s => s.SyncCount);
            report.TotalConflicts = daySessions.Sum(s => s.ConflictsEncountered);

            // User breakdown
            report.UserBreakdown = daySessions
                .GroupBy(s => s.UserId)
                .Select(g => new UserDayMetrics
                {
                    UserId = g.Key,
                    Username = g.First().Username,
                    SessionCount = g.Count(),
                    TotalHours = g.Sum(s => s.Duration.TotalHours),
                    ElementsEdited = g.Sum(s => s.ElementsEdited),
                    SyncCount = g.Sum(s => s.SyncCount),
                    Conflicts = g.Sum(s => s.ConflictsEncountered)
                })
                .OrderByDescending(u => u.TotalHours)
                .ToList();

            // Calculate team collaboration score
            report.TeamCollaborationScore = CalculateTeamCollaborationScore(daySessions);

            // Hourly activity distribution
            report.HourlyActivity = GetHourlyActivityDistribution(date);

            // Peak collaboration periods
            report.PeakCollaborationPeriods = IdentifyPeakCollaborationPeriods(daySessions);

            return report;
        }

        /// <summary>
        /// Generate weekly trend report
        /// </summary>
        public WeeklyTrendReport GenerateWeeklyTrendReport(DateTime weekStart)
        {
            var report = new WeeklyTrendReport
            {
                WeekStart = weekStart.Date,
                WeekEnd = weekStart.Date.AddDays(6),
                ProjectId = _projectId,
                GeneratedAt = DateTime.UtcNow
            };

            // Get daily reports for the week
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.Date.AddDays(i);
                report.DailyBreakdown.Add(GenerateDailyReport(date));
            }

            // Calculate trends
            var dailyEdits = report.DailyBreakdown.Select(d => (double)d.TotalElementsEdited).ToList();
            report.ProductivityTrend = CalculateTrend(dailyEdits);

            var dailyConflicts = report.DailyBreakdown.Select(d => (double)d.TotalConflicts).ToList();
            report.ConflictTrend = CalculateTrend(dailyConflicts);

            var dailyScores = report.DailyBreakdown.Select(d => d.TeamCollaborationScore).ToList();
            report.CollaborationTrend = CalculateTrend(dailyScores);

            // Insights
            report.Insights = GenerateWeeklyInsights(report);

            return report;
        }

        /// <summary>
        /// Get user performance metrics
        /// </summary>
        public UserPerformanceMetrics GetUserPerformance(string userId, TimeSpan period)
        {
            var cutoff = DateTime.UtcNow - period;

            var userSessions = _sessions.GetValueOrDefault(userId, new List<SessionRecord>())
                .Where(s => s.StartTime >= cutoff)
                .ToList();

            var userSyncs = _syncs.GetValueOrDefault(userId, new List<SyncRecord>())
                .Where(s => s.Timestamp >= cutoff)
                .ToList();

            var metrics = new UserPerformanceMetrics
            {
                UserId = userId,
                Period = period,
                TotalSessions = userSessions.Count,
                TotalWorkHours = userSessions.Sum(s => s.Duration.TotalHours),
                AverageSessionDuration = userSessions.Any()
                    ? TimeSpan.FromHours(userSessions.Average(s => s.Duration.TotalHours))
                    : TimeSpan.Zero,
                TotalElementsEdited = userSessions.Sum(s => s.ElementsEdited),
                ElementsPerHour = userSessions.Sum(s => s.Duration.TotalHours) > 0
                    ? userSessions.Sum(s => s.ElementsEdited) / userSessions.Sum(s => s.Duration.TotalHours)
                    : 0,
                TotalSyncs = userSyncs.Count,
                AverageSyncFrequency = CalculateAverageSyncFrequency(userSyncs),
                ConflictRate = CalculateConflictRate(userId, cutoff),
                CollaborationScore = CalculateUserCollaborationScore(userId, cutoff)
            };

            // Calculate percentiles (how user compares to team)
            metrics.ProductivityPercentile = CalculateProductivityPercentile(userId, period);
            metrics.CollaborationPercentile = CalculateCollaborationPercentile(userId, period);

            return metrics;
        }

        /// <summary>
        /// Get real-time team dashboard data
        /// </summary>
        public TeamDashboard GetTeamDashboard()
        {
            var dashboard = new TeamDashboard
            {
                Timestamp = DateTime.UtcNow,
                ProjectId = _projectId,
                ProjectName = _projectName
            };

            // Active users (sessions in last hour)
            var hourAgo = DateTime.UtcNow.AddHours(-1);
            dashboard.ActiveUsers = _sessions.Values
                .SelectMany(s => s)
                .Where(s => s.EndTime == null || s.EndTime > hourAgo)
                .Select(s => new ActiveUserInfo
                {
                    UserId = s.UserId,
                    Username = s.Username,
                    SessionStart = s.StartTime,
                    CurrentActivity = GetCurrentActivity(s)
                })
                .ToList();

            // Today's stats
            var todaysSessions = _sessions.Values
                .SelectMany(s => s)
                .Where(s => s.StartTime.Date == DateTime.Today)
                .ToList();

            dashboard.TodayStats = new TodayStats
            {
                TotalEdits = todaysSessions.Sum(s => s.ElementsEdited),
                TotalSyncs = todaysSessions.Sum(s => s.SyncCount),
                TotalConflicts = todaysSessions.Sum(s => s.ConflictsEncountered),
                ActiveHours = todaysSessions.Sum(s => s.Duration.TotalHours)
            };

            // Recent activity feed
            dashboard.RecentActivity = GetRecentActivityFeed(20);

            // Current hotspots
            dashboard.Hotspots = IdentifyCurrentHotspots();

            // Health indicators
            dashboard.HealthIndicators = CalculateHealthIndicators();

            return dashboard;
        }

        /// <summary>
        /// Get conflict analysis
        /// </summary>
        public ConflictAnalysis GetConflictAnalysis(TimeSpan period)
        {
            var cutoff = DateTime.UtcNow - period;
            var recentConflicts = _conflicts.Values
                .SelectMany(c => c)
                .Where(c => c.Timestamp >= cutoff)
                .ToList();

            var analysis = new ConflictAnalysis
            {
                Period = period,
                TotalConflicts = recentConflicts.Count,
                ResolvedConflicts = recentConflicts.Count(c => c.WasResolved),
                ResolutionRate = recentConflicts.Any()
                    ? (double)recentConflicts.Count(c => c.WasResolved) / recentConflicts.Count
                    : 1.0
            };

            // Conflicts by category
            analysis.ConflictsByCategory = recentConflicts
                .GroupBy(c => c.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            // Most contentious elements
            analysis.ContentiousElements = recentConflicts
                .GroupBy(c => c.ElementId)
                .Select(g => new ContentiousElement
                {
                    ElementId = g.Key,
                    ElementName = g.First().ElementName,
                    ConflictCount = g.Count(),
                    InvolvedUsers = g.SelectMany(c => new[] { c.User1Id, c.User2Id }).Distinct().ToList()
                })
                .OrderByDescending(e => e.ConflictCount)
                .Take(10)
                .ToList();

            // User pairs with most conflicts
            analysis.ConflictPairs = recentConflicts
                .GroupBy(c => (Math.Min(c.User1Id.GetHashCode(), c.User2Id.GetHashCode()),
                              Math.Max(c.User1Id.GetHashCode(), c.User2Id.GetHashCode())))
                .Select(g => new ConflictPair
                {
                    User1 = g.First().User1Id,
                    User2 = g.First().User2Id,
                    ConflictCount = g.Count(),
                    ResolvedCount = g.Count(c => c.WasResolved)
                })
                .OrderByDescending(p => p.ConflictCount)
                .Take(5)
                .ToList();

            // Conflict trends by hour
            analysis.HourlyDistribution = recentConflicts
                .GroupBy(c => c.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            // Recommendations
            analysis.Recommendations = GenerateConflictRecommendations(analysis);

            return analysis;
        }

        #endregion

        #region Helper Methods

        private double CalculateCollaborationScore(SessionRecord session)
        {
            // Score based on:
            // - Regular syncs (good)
            // - Low conflicts (good)
            // - Consistent activity (good)

            double score = 50; // Base score

            // Sync frequency bonus (sync every 30 min is ideal)
            if (session.Duration.TotalMinutes > 30)
            {
                var expectedSyncs = session.Duration.TotalMinutes / 30;
                var syncRatio = session.SyncCount / expectedSyncs;
                score += Math.Min(20, syncRatio * 20);
            }

            // Low conflict bonus
            if (session.ConflictsEncountered == 0)
            {
                score += 20;
            }
            else
            {
                score -= Math.Min(20, session.ConflictsEncountered * 5);
            }

            // Activity consistency bonus
            if (session.ElementsEdited > 0)
            {
                score += Math.Min(10, session.ElementsEdited / 10.0);
            }

            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateTeamCollaborationScore(List<SessionRecord> sessions)
        {
            if (!sessions.Any()) return 0;
            return sessions.Average(s => CalculateCollaborationScore(s));
        }

        private Dictionary<int, int> GetHourlyActivityDistribution(DateTime date)
        {
            var distribution = new Dictionary<int, int>();
            for (int h = 0; h < 24; h++) distribution[h] = 0;

            foreach (var activities in _activities.Values)
            {
                foreach (var activity in activities.Where(a => a.Timestamp.Date == date.Date))
                {
                    distribution[activity.Timestamp.Hour]++;
                }
            }

            return distribution;
        }

        private List<CollaborationPeriod> IdentifyPeakCollaborationPeriods(List<SessionRecord> sessions)
        {
            var periods = new List<CollaborationPeriod>();

            // Group by hour and find hours with multiple users
            var hourlyUsers = sessions
                .SelectMany(s => Enumerable.Range(s.StartTime.Hour, (int)Math.Ceiling(s.Duration.TotalHours)))
                .GroupBy(h => h % 24)
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Count())
                .Take(3);

            foreach (var group in hourlyUsers)
            {
                periods.Add(new CollaborationPeriod
                {
                    StartHour = group.Key,
                    EndHour = (group.Key + 1) % 24,
                    ActiveUsers = group.Count(),
                    Description = $"{group.Key}:00 - {(group.Key + 1) % 24}:00"
                });
            }

            return periods;
        }

        private string CalculateTrend(List<double> values)
        {
            if (values.Count < 2) return "stable";

            var firstHalf = values.Take(values.Count / 2).Average();
            var secondHalf = values.Skip(values.Count / 2).Average();

            var change = (secondHalf - firstHalf) / (firstHalf + 0.001);

            if (change > 0.1) return "increasing";
            if (change < -0.1) return "decreasing";
            return "stable";
        }

        private List<string> GenerateWeeklyInsights(WeeklyTrendReport report)
        {
            var insights = new List<string>();

            if (report.ProductivityTrend == "increasing")
            {
                insights.Add("Team productivity is trending upward this week");
            }
            else if (report.ProductivityTrend == "decreasing")
            {
                insights.Add("Consider reviewing workflows - productivity has decreased");
            }

            if (report.ConflictTrend == "increasing")
            {
                insights.Add("Conflicts are increasing - consider better workset organization");
            }

            var totalConflicts = report.DailyBreakdown.Sum(d => d.TotalConflicts);
            var totalSyncs = report.DailyBreakdown.Sum(d => d.TotalSyncs);
            if (totalSyncs > 0 && (double)totalConflicts / totalSyncs > 0.2)
            {
                insights.Add("High conflict-to-sync ratio suggests coordination issues");
            }

            return insights;
        }

        private TimeSpan CalculateAverageSyncFrequency(List<SyncRecord> syncs)
        {
            if (syncs.Count < 2) return TimeSpan.FromMinutes(30);

            var intervals = new List<TimeSpan>();
            for (int i = 1; i < syncs.Count; i++)
            {
                intervals.Add(syncs[i].Timestamp - syncs[i - 1].Timestamp);
            }

            return TimeSpan.FromMinutes(intervals.Average(i => i.TotalMinutes));
        }

        private double CalculateConflictRate(string userId, DateTime cutoff)
        {
            var userConflicts = _conflicts.Values
                .SelectMany(c => c)
                .Where(c => c.Timestamp >= cutoff)
                .Count(c => c.User1Id == userId || c.User2Id == userId);

            var userSyncs = _syncs.GetValueOrDefault(userId, new List<SyncRecord>())
                .Count(s => s.Timestamp >= cutoff);

            return userSyncs > 0 ? (double)userConflicts / userSyncs : 0;
        }

        private double CalculateUserCollaborationScore(string userId, DateTime cutoff)
        {
            var userSessions = _sessions.GetValueOrDefault(userId, new List<SessionRecord>())
                .Where(s => s.StartTime >= cutoff)
                .ToList();

            if (!userSessions.Any()) return 0;

            return userSessions.Average(s => CalculateCollaborationScore(s));
        }

        private double CalculateProductivityPercentile(string userId, TimeSpan period)
        {
            // Compare user's productivity to all users
            var cutoff = DateTime.UtcNow - period;

            var allUserEdits = _sessions
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Where(s => s.StartTime >= cutoff).Sum(s => s.ElementsEdited));

            var userEdits = allUserEdits.GetValueOrDefault(userId, 0);
            var belowCount = allUserEdits.Values.Count(v => v < userEdits);

            return allUserEdits.Count > 0 ? (double)belowCount / allUserEdits.Count * 100 : 50;
        }

        private double CalculateCollaborationPercentile(string userId, TimeSpan period)
        {
            var cutoff = DateTime.UtcNow - period;

            var allUserScores = _sessions
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => CalculateUserCollaborationScore(kvp.Key, cutoff));

            var userScore = allUserScores.GetValueOrDefault(userId, 0);
            var belowCount = allUserScores.Values.Count(v => v < userScore);

            return allUserScores.Count > 0 ? (double)belowCount / allUserScores.Count * 100 : 50;
        }

        private string GetCurrentActivity(SessionRecord session)
        {
            if (session.Activities.Any())
            {
                var lastActivity = session.Activities.Last();
                return $"{lastActivity.ActivityType}: {lastActivity.Details ?? ""}";
            }
            return "Active";
        }

        private List<RecentActivityItem> GetRecentActivityFeed(int count)
        {
            return _activities.Values
                .SelectMany(a => a)
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .Select(a => new RecentActivityItem
                {
                    UserId = a.UserId,
                    Timestamp = a.Timestamp,
                    ActivityType = a.ActivityType,
                    Details = a.Details
                })
                .ToList();
        }

        private List<ActivityHotspot> IdentifyCurrentHotspots()
        {
            var hourAgo = DateTime.UtcNow.AddHours(-1);

            return _activities.Values
                .SelectMany(a => a)
                .Where(a => a.Timestamp >= hourAgo && !string.IsNullOrEmpty(a.ElementId))
                .GroupBy(a => a.ElementId)
                .Where(g => g.Select(a => a.UserId).Distinct().Count() >= 2)
                .Select(g => new ActivityHotspot
                {
                    ElementId = g.Key!,
                    ActivityCount = g.Count(),
                    ActiveUsers = g.Select(a => a.UserId).Distinct().ToList()
                })
                .OrderByDescending(h => h.ActivityCount)
                .Take(5)
                .ToList();
        }

        private HealthIndicators CalculateHealthIndicators()
        {
            var hourAgo = DateTime.UtcNow.AddHours(-1);

            var recentConflicts = _conflicts.Values
                .SelectMany(c => c)
                .Count(c => c.Timestamp >= hourAgo);

            var recentSyncs = _syncs.Values
                .SelectMany(s => s)
                .Count(s => s.Timestamp >= hourAgo);

            return new HealthIndicators
            {
                SyncHealth = recentSyncs >= 2 ? "Good" : recentSyncs >= 1 ? "Fair" : "Low Activity",
                ConflictLevel = recentConflicts == 0 ? "None" : recentConflicts <= 2 ? "Low" : "High",
                TeamActivity = _sessions.Values
                    .SelectMany(s => s)
                    .Count(s => s.EndTime == null || s.EndTime > hourAgo) >= 2 ? "Active" : "Quiet"
            };
        }

        private List<string> GenerateConflictRecommendations(ConflictAnalysis analysis)
        {
            var recommendations = new List<string>();

            if (analysis.ResolutionRate < 0.8)
            {
                recommendations.Add("Improve conflict resolution - consider team coordination meeting");
            }

            if (analysis.ContentiousElements.Any(e => e.ConflictCount >= 3))
            {
                var element = analysis.ContentiousElements.First();
                recommendations.Add($"Element '{element.ElementName}' has frequent conflicts - consider workset isolation");
            }

            if (analysis.ConflictPairs.Any(p => p.ConflictCount >= 3))
            {
                var pair = analysis.ConflictPairs.First();
                recommendations.Add($"Schedule coordination between users with high conflict rate");
            }

            if (analysis.HourlyDistribution.Any(h => h.Value >= 3))
            {
                var peakHour = analysis.HourlyDistribution.OrderByDescending(h => h.Value).First().Key;
                recommendations.Add($"High conflict rate at {peakHour}:00 - consider staggered sync schedules");
            }

            return recommendations;
        }

        private void UpdateDailyStats(SessionRecord session)
        {
            var date = session.StartTime.Date;
            var stats = _dailyStats.GetOrAdd(date, _ => new DailyStats { Date = date });

            stats.SessionCount++;
            stats.TotalHours += session.Duration.TotalHours;
            stats.TotalEdits += session.ElementsEdited;
            stats.TotalSyncs += session.SyncCount;
            stats.TotalConflicts += session.ConflictsEncountered;
        }

        private void CleanupOldData()
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);

            foreach (var activities in _activities.Values)
            {
                activities.RemoveAll(a => a.Timestamp < cutoff);
            }

            foreach (var syncs in _syncs.Values)
            {
                syncs.RemoveAll(s => s.Timestamp < cutoff);
            }

            foreach (var sessions in _sessions.Values)
            {
                sessions.RemoveAll(s => s.StartTime < cutoff);
            }
        }

        #endregion
    }

    #region Data Models

    public class SessionRecord
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int ElementsEdited { get; set; }
        public int ViewsAccessed { get; set; }
        public int SyncCount { get; set; }
        public int ConflictsEncountered { get; set; }
        public List<SessionActivity> Activities { get; set; } = new();
    }

    public class SessionActivity
    {
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string? ElementId { get; set; }
        public string? Details { get; set; }
    }

    public class SessionSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public int ElementsEdited { get; set; }
        public int ViewsAccessed { get; set; }
        public int SyncCount { get; set; }
        public int ConflictsEncountered { get; set; }
        public double CollaborationScore { get; set; }
    }

    public class SyncRecord
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int ElementsModified { get; set; }
        public int ConflictsResolved { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ConflictRecord
    {
        public string ConflictId { get; set; } = Guid.NewGuid().ToString();
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string User1Id { get; set; } = string.Empty;
        public string User2Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool WasResolved { get; set; }
        public string? Resolution { get; set; }
    }

    public class ActivityMetric
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string? ElementId { get; set; }
        public string? Details { get; set; }
    }

    public class DailyStats
    {
        public DateTime Date { get; set; }
        public int SessionCount { get; set; }
        public double TotalHours { get; set; }
        public int TotalEdits { get; set; }
        public int TotalSyncs { get; set; }
        public int TotalConflicts { get; set; }
    }

    public class DailyReport
    {
        public DateTime Date { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public int TotalSessions { get; set; }
        public int UniqueUsers { get; set; }
        public double TotalWorkHours { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public int TotalElementsEdited { get; set; }
        public int TotalViewsAccessed { get; set; }
        public int TotalSyncs { get; set; }
        public int TotalConflicts { get; set; }
        public List<UserDayMetrics> UserBreakdown { get; set; } = new();
        public double TeamCollaborationScore { get; set; }
        public Dictionary<int, int> HourlyActivity { get; set; } = new();
        public List<CollaborationPeriod> PeakCollaborationPeriods { get; set; } = new();
    }

    public class UserDayMetrics
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public double TotalHours { get; set; }
        public int ElementsEdited { get; set; }
        public int SyncCount { get; set; }
        public int Conflicts { get; set; }
    }

    public class CollaborationPeriod
    {
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public int ActiveUsers { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class WeeklyTrendReport
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public List<DailyReport> DailyBreakdown { get; set; } = new();
        public string ProductivityTrend { get; set; } = string.Empty;
        public string ConflictTrend { get; set; } = string.Empty;
        public string CollaborationTrend { get; set; } = string.Empty;
        public List<string> Insights { get; set; } = new();
    }

    public class UserPerformanceMetrics
    {
        public string UserId { get; set; } = string.Empty;
        public TimeSpan Period { get; set; }
        public int TotalSessions { get; set; }
        public double TotalWorkHours { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public int TotalElementsEdited { get; set; }
        public double ElementsPerHour { get; set; }
        public int TotalSyncs { get; set; }
        public TimeSpan AverageSyncFrequency { get; set; }
        public double ConflictRate { get; set; }
        public double CollaborationScore { get; set; }
        public double ProductivityPercentile { get; set; }
        public double CollaborationPercentile { get; set; }
    }

    public class TeamDashboard
    {
        public DateTime Timestamp { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public List<ActiveUserInfo> ActiveUsers { get; set; } = new();
        public TodayStats TodayStats { get; set; } = new();
        public List<RecentActivityItem> RecentActivity { get; set; } = new();
        public List<ActivityHotspot> Hotspots { get; set; } = new();
        public HealthIndicators HealthIndicators { get; set; } = new();
    }

    public class ActiveUserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; }
        public string CurrentActivity { get; set; } = string.Empty;
    }

    public class TodayStats
    {
        public int TotalEdits { get; set; }
        public int TotalSyncs { get; set; }
        public int TotalConflicts { get; set; }
        public double ActiveHours { get; set; }
    }

    public class RecentActivityItem
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    public class ActivityHotspot
    {
        public string ElementId { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public List<string> ActiveUsers { get; set; } = new();
    }

    public class HealthIndicators
    {
        public string SyncHealth { get; set; } = string.Empty;
        public string ConflictLevel { get; set; } = string.Empty;
        public string TeamActivity { get; set; } = string.Empty;
    }

    public class ConflictAnalysis
    {
        public TimeSpan Period { get; set; }
        public int TotalConflicts { get; set; }
        public int ResolvedConflicts { get; set; }
        public double ResolutionRate { get; set; }
        public Dictionary<string, int> ConflictsByCategory { get; set; } = new();
        public List<ContentiousElement> ContentiousElements { get; set; } = new();
        public List<ConflictPair> ConflictPairs { get; set; } = new();
        public Dictionary<int, int> HourlyDistribution { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ContentiousElement
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public int ConflictCount { get; set; }
        public List<string> InvolvedUsers { get; set; } = new();
    }

    public class ConflictPair
    {
        public string User1 { get; set; } = string.Empty;
        public string User2 { get; set; } = string.Empty;
        public int ConflictCount { get; set; }
        public int ResolvedCount { get; set; }
    }

    #endregion
}
