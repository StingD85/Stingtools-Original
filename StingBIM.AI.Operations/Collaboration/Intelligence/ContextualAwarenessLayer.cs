// ===================================================================
// StingBIM.AI.Collaboration - Contextual Awareness Intelligence Layer
// Provides deep understanding of project context, user behavior,
// environmental factors, and situational awareness
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Context Models

    /// <summary>
    /// Project context representing current state and environment
    /// </summary>
    public class ProjectContext
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public ProjectPhase Phase { get; set; }
        public ProjectHealth Health { get; set; } = new();
        public List<ActiveWorkstream> ActiveWorkstreams { get; set; } = new();
        public List<string> CriticalPaths { get; set; } = new();
        public Dictionary<string, double> RiskScores { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public WeatherContext? Weather { get; set; }
        public ScheduleContext Schedule { get; set; } = new();
        public BudgetContext Budget { get; set; } = new();
        public TeamContext Team { get; set; } = new();
    }

    /// <summary>
    /// Project phase enumeration
    /// </summary>
    public enum ProjectPhase
    {
        Conception,
        SchematicDesign,
        DesignDevelopment,
        ConstructionDocuments,
        Bidding,
        Construction,
        Commissioning,
        Handover,
        Operations
    }

    /// <summary>
    /// Project health metrics
    /// </summary>
    public class ProjectHealth
    {
        public double OverallScore { get; set; } = 100;
        public double ScheduleHealth { get; set; } = 100;
        public double BudgetHealth { get; set; } = 100;
        public double QualityHealth { get; set; } = 100;
        public double SafetyHealth { get; set; } = 100;
        public double TeamHealth { get; set; } = 100;
        public List<HealthIssue> Issues { get; set; } = new();
        public HealthTrend Trend { get; set; } = HealthTrend.Stable;
    }

    /// <summary>
    /// Health trend
    /// </summary>
    public enum HealthTrend
    {
        Improving,
        Stable,
        Declining,
        Critical
    }

    /// <summary>
    /// Health issue
    /// </summary>
    public class HealthIssue
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Impact { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Active workstream in project
    /// </summary>
    public class ActiveWorkstream
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public int ActiveUsers { get; set; }
        public double Progress { get; set; }
        public WorkstreamStatus Status { get; set; }
        public List<string> BlockedBy { get; set; } = new();
    }

    /// <summary>
    /// Workstream status
    /// </summary>
    public enum WorkstreamStatus
    {
        OnTrack,
        AtRisk,
        Blocked,
        Completed,
        NotStarted
    }

    /// <summary>
    /// Weather context for field operations
    /// </summary>
    public class WeatherContext
    {
        public double Temperature { get; set; }
        public string Condition { get; set; } = string.Empty;
        public double WindSpeed { get; set; }
        public double Precipitation { get; set; }
        public double Humidity { get; set; }
        public bool IsSafeForWork { get; set; } = true;
        public List<string> Restrictions { get; set; } = new();
        public WeatherForecast[] Forecast { get; set; } = Array.Empty<WeatherForecast>();
    }

    /// <summary>
    /// Weather forecast entry
    /// </summary>
    public class WeatherForecast
    {
        public DateTime Date { get; set; }
        public double HighTemp { get; set; }
        public double LowTemp { get; set; }
        public string Condition { get; set; } = string.Empty;
        public double PrecipitationChance { get; set; }
    }

    /// <summary>
    /// Schedule context
    /// </summary>
    public class ScheduleContext
    {
        public DateTime ProjectStart { get; set; }
        public DateTime ProjectEnd { get; set; }
        public DateTime CurrentMilestone { get; set; }
        public string CurrentMilestoneName { get; set; } = string.Empty;
        public int DaysToMilestone { get; set; }
        public double ScheduleVariance { get; set; }
        public List<UpcomingDeadline> UpcomingDeadlines { get; set; } = new();
        public List<string> DelayedActivities { get; set; } = new();
    }

    /// <summary>
    /// Upcoming deadline
    /// </summary>
    public class UpcomingDeadline
    {
        public string Name { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public int DaysRemaining { get; set; }
        public double CompletionPercentage { get; set; }
        public string Priority { get; set; } = "Normal";
    }

    /// <summary>
    /// Budget context
    /// </summary>
    public class BudgetContext
    {
        public decimal TotalBudget { get; set; }
        public decimal SpentToDate { get; set; }
        public decimal CommittedCost { get; set; }
        public decimal ForecastAtCompletion { get; set; }
        public double CostVariancePercent { get; set; }
        public Dictionary<string, decimal> CategorySpending { get; set; } = new();
        public List<BudgetRisk> Risks { get; set; } = new();
    }

    /// <summary>
    /// Budget risk
    /// </summary>
    public class BudgetRisk
    {
        public string Description { get; set; } = string.Empty;
        public decimal PotentialImpact { get; set; }
        public double Probability { get; set; }
        public string Mitigation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Team context
    /// </summary>
    public class TeamContext
    {
        public int TotalMembers { get; set; }
        public int ActiveNow { get; set; }
        public Dictionary<string, int> ByDiscipline { get; set; } = new();
        public Dictionary<string, int> ByCompany { get; set; } = new();
        public double AverageWorkload { get; set; }
        public List<TeamMemberWorkload> OverloadedMembers { get; set; } = new();
        public List<ExpertiseGap> ExpertiseGaps { get; set; } = new();
    }

    /// <summary>
    /// Team member workload
    /// </summary>
    public class TeamMemberWorkload
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AssignedTasks { get; set; }
        public double WorkloadScore { get; set; }
        public List<string> BlockedTasks { get; set; } = new();
    }

    /// <summary>
    /// Expertise gap in team
    /// </summary>
    public class ExpertiseGap
    {
        public string Area { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// User context for personalization
    /// </summary>
    public class UserContext
    {
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public List<string> RecentProjects { get; set; } = new();
        public List<string> Expertise { get; set; } = new();
        public UserBehaviorProfile Behavior { get; set; } = new();
        public UserPreferences Preferences { get; set; } = new();
        public List<string> CurrentFocus { get; set; } = new();
        public DateTime LastActive { get; set; }
        public string? CurrentLocation { get; set; }
    }

    /// <summary>
    /// User behavior profile
    /// </summary>
    public class UserBehaviorProfile
    {
        public string PreferredWorkTime { get; set; } = "business_hours";
        public double AverageSessionDuration { get; set; }
        public List<string> FrequentActions { get; set; } = new();
        public List<string> FrequentSearches { get; set; } = new();
        public Dictionary<string, int> FeatureUsage { get; set; } = new();
        public double ResponseTimeAvg { get; set; }
        public double TaskCompletionRate { get; set; }
    }

    /// <summary>
    /// User preferences
    /// </summary>
    public class UserPreferences
    {
        public string NotificationLevel { get; set; } = "normal";
        public bool EmailDigest { get; set; } = true;
        public List<string> WatchedItems { get; set; } = new();
        public string DefaultView { get; set; } = "dashboard";
        public string Language { get; set; } = "en";
        public string Timezone { get; set; } = "UTC";
    }

    /// <summary>
    /// Spatial context for location-aware features
    /// </summary>
    public class SpatialContext
    {
        public string CurrentArea { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public List<NearbyElement> NearbyElements { get; set; } = new();
        public List<ActiveIssueNearby> NearbyIssues { get; set; } = new();
        public List<OngoingWork> OngoingWorkNearby { get; set; } = new();
        public List<SafetyHazard> NearbyHazards { get; set; } = new();
    }

    /// <summary>
    /// Nearby element
    /// </summary>
    public class NearbyElement
    {
        public string ElementId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Distance { get; set; }
    }

    /// <summary>
    /// Active issue nearby
    /// </summary>
    public class ActiveIssueNearby
    {
        public string IssueId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public double Distance { get; set; }
    }

    /// <summary>
    /// Ongoing work nearby
    /// </summary>
    public class OngoingWork
    {
        public string ActivityId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public double Distance { get; set; }
    }

    /// <summary>
    /// Safety hazard
    /// </summary>
    public class SafetyHazard
    {
        public string HazardId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public double Distance { get; set; }
    }

    /// <summary>
    /// Temporal context for time-aware features
    /// </summary>
    public class TemporalContext
    {
        public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
        public string TimeOfDay { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public bool IsWorkingHours { get; set; }
        public bool IsHoliday { get; set; }
        public string CurrentShift { get; set; } = string.Empty;
        public int MinutesToShiftEnd { get; set; }
        public List<ScheduledEvent> UpcomingEvents { get; set; } = new();
    }

    /// <summary>
    /// Scheduled event
    /// </summary>
    public class ScheduledEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public string Type { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new();
    }

    #endregion

    #region Context Awareness Engine

    /// <summary>
    /// Contextual awareness intelligence layer
    /// </summary>
    public class ContextualAwarenessLayer : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, ProjectContext> _projectContexts = new();
        private readonly ConcurrentDictionary<string, UserContext> _userContexts = new();
        private readonly ConcurrentDictionary<string, SpatialContext> _spatialContexts = new();
        private readonly ConcurrentDictionary<string, List<ContextEvent>> _eventHistory = new();
        private readonly SemaphoreSlim _updateLock = new(1, 1);
        private Timer? _contextRefreshTimer;

        public event EventHandler<ContextChangeEvent>? ContextChanged;
        public event EventHandler<ContextAlert>? AlertTriggered;

        public ContextualAwarenessLayer(ILogger? logger = null)
        {
            _logger = logger;
            StartContextRefresh();
            _logger?.LogInformation("ContextualAwarenessLayer initialized");
        }

        #region Project Context

        /// <summary>
        /// Get or build project context
        /// </summary>
        public async Task<ProjectContext> GetProjectContextAsync(
            string projectId,
            bool forceRefresh = false,
            CancellationToken ct = default)
        {
            if (!forceRefresh && _projectContexts.TryGetValue(projectId, out var cached))
            {
                if ((DateTime.UtcNow - cached.LastUpdated).TotalMinutes < 5)
                    return cached;
            }

            var context = await BuildProjectContextAsync(projectId, ct);
            _projectContexts[projectId] = context;

            return context;
        }

        private async Task<ProjectContext> BuildProjectContextAsync(
            string projectId,
            CancellationToken ct)
        {
            var context = new ProjectContext
            {
                ProjectId = projectId,
                Phase = await DetermineProjectPhaseAsync(projectId, ct),
                LastUpdated = DateTime.UtcNow
            };

            // Build health metrics
            context.Health = await CalculateProjectHealthAsync(projectId, ct);

            // Build schedule context
            context.Schedule = await BuildScheduleContextAsync(projectId, ct);

            // Build budget context
            context.Budget = await BuildBudgetContextAsync(projectId, ct);

            // Build team context
            context.Team = await BuildTeamContextAsync(projectId, ct);

            // Identify active workstreams
            context.ActiveWorkstreams = await IdentifyActiveWorkstreamsAsync(projectId, ct);

            // Calculate risk scores
            context.RiskScores = await CalculateRiskScoresAsync(context, ct);

            // Identify critical paths
            context.CriticalPaths = IdentifyCriticalPaths(context);

            _logger?.LogDebug("Built context for project {ProjectId}: Health={Health:F1}",
                projectId, context.Health.OverallScore);

            return context;
        }

        private async Task<ProjectPhase> DetermineProjectPhaseAsync(
            string projectId,
            CancellationToken ct)
        {
            // Would analyze project milestones and activities
            // For now, return a default
            await Task.Delay(1, ct);
            return ProjectPhase.Construction;
        }

        private async Task<ProjectHealth> CalculateProjectHealthAsync(
            string projectId,
            CancellationToken ct)
        {
            var health = new ProjectHealth();

            // Calculate individual health metrics
            health.ScheduleHealth = await CalculateScheduleHealthAsync(projectId, ct);
            health.BudgetHealth = await CalculateBudgetHealthAsync(projectId, ct);
            health.QualityHealth = await CalculateQualityHealthAsync(projectId, ct);
            health.SafetyHealth = await CalculateSafetyHealthAsync(projectId, ct);
            health.TeamHealth = await CalculateTeamHealthAsync(projectId, ct);

            // Calculate overall score (weighted average)
            health.OverallScore =
                health.ScheduleHealth * 0.25 +
                health.BudgetHealth * 0.25 +
                health.QualityHealth * 0.20 +
                health.SafetyHealth * 0.15 +
                health.TeamHealth * 0.15;

            // Determine trend
            health.Trend = DetermineHealthTrend(projectId, health.OverallScore);

            // Identify issues
            health.Issues = IdentifyHealthIssues(health);

            return health;
        }

        private async Task<double> CalculateScheduleHealthAsync(string projectId, CancellationToken ct)
        {
            // Would analyze schedule data
            await Task.Delay(1, ct);
            return 85.0;
        }

        private async Task<double> CalculateBudgetHealthAsync(string projectId, CancellationToken ct)
        {
            await Task.Delay(1, ct);
            return 90.0;
        }

        private async Task<double> CalculateQualityHealthAsync(string projectId, CancellationToken ct)
        {
            await Task.Delay(1, ct);
            return 88.0;
        }

        private async Task<double> CalculateSafetyHealthAsync(string projectId, CancellationToken ct)
        {
            await Task.Delay(1, ct);
            return 95.0;
        }

        private async Task<double> CalculateTeamHealthAsync(string projectId, CancellationToken ct)
        {
            await Task.Delay(1, ct);
            return 82.0;
        }

        private HealthTrend DetermineHealthTrend(string projectId, double currentScore)
        {
            // Would compare with historical data
            return HealthTrend.Stable;
        }

        private List<HealthIssue> IdentifyHealthIssues(ProjectHealth health)
        {
            var issues = new List<HealthIssue>();

            if (health.ScheduleHealth < 70)
            {
                issues.Add(new HealthIssue
                {
                    Category = "Schedule",
                    Description = "Project is behind schedule",
                    Impact = (100 - health.ScheduleHealth) / 100,
                    RecommendedAction = "Review critical path and allocate additional resources"
                });
            }

            if (health.BudgetHealth < 70)
            {
                issues.Add(new HealthIssue
                {
                    Category = "Budget",
                    Description = "Budget variance exceeds threshold",
                    Impact = (100 - health.BudgetHealth) / 100,
                    RecommendedAction = "Conduct cost review and identify savings opportunities"
                });
            }

            if (health.TeamHealth < 70)
            {
                issues.Add(new HealthIssue
                {
                    Category = "Team",
                    Description = "Team workload imbalance detected",
                    Impact = (100 - health.TeamHealth) / 100,
                    RecommendedAction = "Redistribute tasks and consider additional staffing"
                });
            }

            return issues;
        }

        private async Task<ScheduleContext> BuildScheduleContextAsync(
            string projectId,
            CancellationToken ct)
        {
            return new ScheduleContext
            {
                ProjectStart = DateTime.UtcNow.AddMonths(-6),
                ProjectEnd = DateTime.UtcNow.AddMonths(12),
                CurrentMilestoneName = "Foundation Complete",
                DaysToMilestone = 14,
                ScheduleVariance = -2.5, // 2.5 days behind
                UpcomingDeadlines = new List<UpcomingDeadline>
                {
                    new() { Name = "RFI Response Due", DueDate = DateTime.UtcNow.AddDays(3), DaysRemaining = 3, Priority = "High" },
                    new() { Name = "Submittal Review", DueDate = DateTime.UtcNow.AddDays(5), DaysRemaining = 5, Priority = "Normal" }
                }
            };
        }

        private async Task<BudgetContext> BuildBudgetContextAsync(
            string projectId,
            CancellationToken ct)
        {
            return new BudgetContext
            {
                TotalBudget = 15000000m,
                SpentToDate = 5500000m,
                CommittedCost = 7200000m,
                ForecastAtCompletion = 15200000m,
                CostVariancePercent = 1.3
            };
        }

        private async Task<TeamContext> BuildTeamContextAsync(
            string projectId,
            CancellationToken ct)
        {
            return new TeamContext
            {
                TotalMembers = 45,
                ActiveNow = 12,
                ByDiscipline = new Dictionary<string, int>
                {
                    ["Architecture"] = 8,
                    ["Structure"] = 6,
                    ["MEP"] = 12,
                    ["Construction"] = 15,
                    ["Management"] = 4
                },
                AverageWorkload = 0.75
            };
        }

        private async Task<List<ActiveWorkstream>> IdentifyActiveWorkstreamsAsync(
            string projectId,
            CancellationToken ct)
        {
            return new List<ActiveWorkstream>
            {
                new()
                {
                    Id = "ws-1",
                    Name = "MEP Coordination",
                    Discipline = "MEP",
                    ActiveUsers = 5,
                    Progress = 0.65,
                    Status = WorkstreamStatus.OnTrack
                },
                new()
                {
                    Id = "ws-2",
                    Name = "Structural Review",
                    Discipline = "Structure",
                    ActiveUsers = 3,
                    Progress = 0.45,
                    Status = WorkstreamStatus.AtRisk,
                    BlockedBy = new List<string> { "Pending geotechnical report" }
                }
            };
        }

        private async Task<Dictionary<string, double>> CalculateRiskScoresAsync(
            ProjectContext context,
            CancellationToken ct)
        {
            return new Dictionary<string, double>
            {
                ["schedule"] = 0.3,
                ["budget"] = 0.2,
                ["quality"] = 0.15,
                ["safety"] = 0.1,
                ["coordination"] = 0.25
            };
        }

        private List<string> IdentifyCriticalPaths(ProjectContext context)
        {
            return new List<string>
            {
                "Foundation -> Structure -> Envelope",
                "MEP Rough-in -> Drywall -> Finishes"
            };
        }

        #endregion

        #region User Context

        /// <summary>
        /// Get or build user context
        /// </summary>
        public async Task<UserContext> GetUserContextAsync(
            string userId,
            CancellationToken ct = default)
        {
            if (_userContexts.TryGetValue(userId, out var cached))
            {
                if ((DateTime.UtcNow - cached.LastActive).TotalMinutes < 15)
                    return cached;
            }

            var context = await BuildUserContextAsync(userId, ct);
            _userContexts[userId] = context;

            return context;
        }

        private async Task<UserContext> BuildUserContextAsync(
            string userId,
            CancellationToken ct)
        {
            var context = new UserContext
            {
                UserId = userId,
                LastActive = DateTime.UtcNow
            };

            // Build behavior profile
            context.Behavior = await AnalyzeUserBehaviorAsync(userId, ct);

            // Determine current focus
            context.CurrentFocus = await DetermineUserFocusAsync(userId, ct);

            return context;
        }

        private async Task<UserBehaviorProfile> AnalyzeUserBehaviorAsync(
            string userId,
            CancellationToken ct)
        {
            // Would analyze user activity history
            return new UserBehaviorProfile
            {
                PreferredWorkTime = "business_hours",
                AverageSessionDuration = 45.0,
                FrequentActions = new List<string> { "view_issues", "create_comment", "upload_document" },
                TaskCompletionRate = 0.92
            };
        }

        private async Task<List<string>> DetermineUserFocusAsync(
            string userId,
            CancellationToken ct)
        {
            // Would analyze recent activity
            return new List<string> { "MEP_Coordination", "Issue_Resolution" };
        }

        /// <summary>
        /// Update user context with new activity
        /// </summary>
        public void RecordUserActivity(string userId, string action, Dictionary<string, object>? metadata = null)
        {
            if (_userContexts.TryGetValue(userId, out var context))
            {
                context.LastActive = DateTime.UtcNow;

                if (!context.Behavior.FeatureUsage.ContainsKey(action))
                    context.Behavior.FeatureUsage[action] = 0;
                context.Behavior.FeatureUsage[action]++;
            }

            // Record event
            RecordContextEvent(userId, "user_activity", action, metadata);
        }

        #endregion

        #region Spatial Context

        /// <summary>
        /// Update spatial context based on location
        /// </summary>
        public async Task<SpatialContext> UpdateSpatialContextAsync(
            string userId,
            double latitude,
            double longitude,
            string? modelId = null,
            CancellationToken ct = default)
        {
            var context = new SpatialContext();

            // Determine current area
            context.CurrentArea = await DetermineAreaFromLocationAsync(latitude, longitude, ct);

            // Find nearby elements
            if (modelId != null)
            {
                context.NearbyElements = await FindNearbyElementsAsync(modelId, latitude, longitude, ct);
            }

            // Find nearby issues
            context.NearbyIssues = await FindNearbyIssuesAsync(latitude, longitude, ct);

            // Find ongoing work
            context.OngoingWorkNearby = await FindOngoingWorkAsync(latitude, longitude, ct);

            // Check for hazards
            context.NearbyHazards = await CheckNearbyHazardsAsync(latitude, longitude, ct);

            _spatialContexts[userId] = context;

            // Alert if hazards detected
            if (context.NearbyHazards.Any(h => h.Severity == "High"))
            {
                AlertTriggered?.Invoke(this, new ContextAlert
                {
                    Type = "safety_hazard",
                    Severity = "High",
                    Message = $"High severity hazard detected: {context.NearbyHazards.First(h => h.Severity == "High").Description}",
                    UserId = userId
                });
            }

            return context;
        }

        private async Task<string> DetermineAreaFromLocationAsync(
            double lat, double lon,
            CancellationToken ct)
        {
            // Would use GIS data or model coordinates
            return "Building A - Level 2";
        }

        private async Task<List<NearbyElement>> FindNearbyElementsAsync(
            string modelId, double lat, double lon,
            CancellationToken ct)
        {
            return new List<NearbyElement>();
        }

        private async Task<List<ActiveIssueNearby>> FindNearbyIssuesAsync(
            double lat, double lon,
            CancellationToken ct)
        {
            return new List<ActiveIssueNearby>();
        }

        private async Task<List<OngoingWork>> FindOngoingWorkAsync(
            double lat, double lon,
            CancellationToken ct)
        {
            return new List<OngoingWork>();
        }

        private async Task<List<SafetyHazard>> CheckNearbyHazardsAsync(
            double lat, double lon,
            CancellationToken ct)
        {
            return new List<SafetyHazard>();
        }

        #endregion

        #region Temporal Context

        /// <summary>
        /// Get temporal context
        /// </summary>
        public TemporalContext GetTemporalContext(string? timezone = null)
        {
            var now = DateTime.UtcNow;
            var localTime = now; // Would convert based on timezone

            return new TemporalContext
            {
                CurrentTime = now,
                TimeOfDay = GetTimeOfDay(localTime),
                DayOfWeek = localTime.DayOfWeek.ToString(),
                IsWorkingHours = IsWorkingHours(localTime),
                CurrentShift = DetermineShift(localTime)
            };
        }

        private string GetTimeOfDay(DateTime time)
        {
            return time.Hour switch
            {
                >= 5 and < 12 => "morning",
                >= 12 and < 17 => "afternoon",
                >= 17 and < 21 => "evening",
                _ => "night"
            };
        }

        private bool IsWorkingHours(DateTime time)
        {
            return time.DayOfWeek != DayOfWeek.Saturday &&
                   time.DayOfWeek != DayOfWeek.Sunday &&
                   time.Hour >= 8 && time.Hour < 18;
        }

        private string DetermineShift(DateTime time)
        {
            return time.Hour switch
            {
                >= 6 and < 14 => "Day Shift",
                >= 14 and < 22 => "Swing Shift",
                _ => "Night Shift"
            };
        }

        #endregion

        #region Context Intelligence

        /// <summary>
        /// Get contextual recommendations for user
        /// </summary>
        public async Task<List<ContextualRecommendation>> GetContextualRecommendationsAsync(
            string userId,
            string projectId,
            CancellationToken ct = default)
        {
            var recommendations = new List<ContextualRecommendation>();

            var userContext = await GetUserContextAsync(userId, ct);
            var projectContext = await GetProjectContextAsync(projectId, ct: ct);
            var temporalContext = GetTemporalContext();

            // Time-based recommendations
            if (!temporalContext.IsWorkingHours)
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Type = "timing",
                    Priority = 0.3,
                    Title = "Off-hours activity",
                    Description = "Consider scheduling non-urgent tasks for regular working hours",
                    Action = "schedule_later"
                });
            }

            // Health-based recommendations
            foreach (var issue in projectContext.Health.Issues)
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Type = "project_health",
                    Priority = issue.Impact,
                    Title = $"{issue.Category} Alert",
                    Description = issue.Description,
                    Action = issue.RecommendedAction
                });
            }

            // Workload-based recommendations
            if (userContext.Behavior.TaskCompletionRate < 0.7)
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Type = "workload",
                    Priority = 0.6,
                    Title = "Task backlog detected",
                    Description = "You have pending tasks that may need attention",
                    Action = "review_tasks"
                });
            }

            // Deadline-based recommendations
            foreach (var deadline in projectContext.Schedule.UpcomingDeadlines.Where(d => d.DaysRemaining <= 3))
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Type = "deadline",
                    Priority = deadline.Priority == "High" ? 0.9 : 0.7,
                    Title = $"Upcoming deadline: {deadline.Name}",
                    Description = $"Due in {deadline.DaysRemaining} days",
                    Action = $"view_deadline:{deadline.Name}"
                });
            }

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }

        /// <summary>
        /// Predict user's next action
        /// </summary>
        public async Task<List<PredictedAction>> PredictNextActionsAsync(
            string userId,
            CancellationToken ct = default)
        {
            var userContext = await GetUserContextAsync(userId, ct);
            var predictions = new List<PredictedAction>();

            // Based on behavior patterns
            foreach (var action in userContext.Behavior.FrequentActions.Take(3))
            {
                predictions.Add(new PredictedAction
                {
                    Action = action,
                    Confidence = 0.8,
                    Reason = "Frequently used"
                });
            }

            // Based on current focus
            if (userContext.CurrentFocus.Contains("Issue_Resolution"))
            {
                predictions.Add(new PredictedAction
                {
                    Action = "resolve_issue",
                    Confidence = 0.75,
                    Reason = "Current focus area"
                });
            }

            return predictions;
        }

        /// <summary>
        /// Detect context anomalies
        /// </summary>
        public async Task<List<ContextAnomaly>> DetectAnomaliesAsync(
            string projectId,
            CancellationToken ct = default)
        {
            var anomalies = new List<ContextAnomaly>();
            var context = await GetProjectContextAsync(projectId, ct: ct);

            // Check for sudden health drops
            if (context.Health.Trend == HealthTrend.Critical)
            {
                anomalies.Add(new ContextAnomaly
                {
                    Type = "health_drop",
                    Severity = "High",
                    Description = "Project health is declining rapidly",
                    DetectedAt = DateTime.UtcNow
                });
            }

            // Check for workstream blocks
            foreach (var ws in context.ActiveWorkstreams.Where(w => w.Status == WorkstreamStatus.Blocked))
            {
                anomalies.Add(new ContextAnomaly
                {
                    Type = "workstream_blocked",
                    Severity = "Medium",
                    Description = $"Workstream '{ws.Name}' is blocked: {string.Join(", ", ws.BlockedBy)}",
                    DetectedAt = DateTime.UtcNow
                });
            }

            return anomalies;
        }

        #endregion

        #region Event Management

        private void RecordContextEvent(string entityId, string eventType, string action, Dictionary<string, object>? metadata)
        {
            var events = _eventHistory.GetOrAdd(entityId, _ => new List<ContextEvent>());
            lock (events)
            {
                events.Add(new ContextEvent
                {
                    EventType = eventType,
                    Action = action,
                    Metadata = metadata ?? new Dictionary<string, object>(),
                    Timestamp = DateTime.UtcNow
                });

                // Keep only last 100 events
                if (events.Count > 100)
                    events.RemoveAt(0);
            }
        }

        private void StartContextRefresh()
        {
            _contextRefreshTimer = new Timer(
                async _ => await RefreshStaleContextsAsync(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        private async Task RefreshStaleContextsAsync()
        {
            var staleThreshold = DateTime.UtcNow.AddMinutes(-10);

            foreach (var kvp in _projectContexts.Where(c => c.Value.LastUpdated < staleThreshold))
            {
                try
                {
                    await GetProjectContextAsync(kvp.Key, forceRefresh: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to refresh context for project {ProjectId}", kvp.Key);
                }
            }
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _contextRefreshTimer?.Dispose();
            _updateLock.Dispose();
            _logger?.LogInformation("ContextualAwarenessLayer disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Support Models

    public class ContextualRecommendation
    {
        public string Type { get; set; } = string.Empty;
        public double Priority { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    public class PredictedAction
    {
        public string Action { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ContextAnomaly
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }

    public class ContextEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ContextChangeEvent : EventArgs
    {
        public string EntityId { get; set; } = string.Empty;
        public string ContextType { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }

    public class ContextAlert : EventArgs
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
    }

    #endregion
}
