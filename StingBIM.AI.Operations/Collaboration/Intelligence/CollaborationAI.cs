// =============================================================================
// StingBIM.AI.Collaboration - Collaboration AI Engine
// Advanced AI for intelligent team collaboration, conflict resolution,
// pattern learning, and proactive recommendations
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.Intelligence
{
    /// <summary>
    /// AI engine that learns team patterns, predicts conflicts,
    /// optimizes worksets, and provides intelligent recommendations.
    /// </summary>
    public class CollaborationAI
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Learning data stores
        private readonly ConcurrentDictionary<string, UserBehaviorProfile> _userProfiles = new();
        private readonly ConcurrentDictionary<string, ElementAccessPattern> _elementPatterns = new();
        private readonly ConcurrentDictionary<string, WorksetOptimizationData> _worksetData = new();
        private readonly List<ConflictHistoryEntry> _conflictHistory = new();
        private readonly List<SyncPatternEntry> _syncHistory = new();

        // ML model weights (simplified - in production would use ONNX)
        private readonly ConflictPredictionModel _conflictModel = new();
        private readonly WorksetRecommendationModel _worksetModel = new();
        private readonly SyncTimingModel _syncModel = new();
        private readonly TeamDynamicsModel _teamModel = new();

        // Configuration
        private readonly TimeSpan _learningWindow = TimeSpan.FromDays(30);
        private readonly double _conflictThreshold = 0.7;

        #region User Behavior Learning

        /// <summary>
        /// Learn from user activity to build behavior profiles
        /// </summary>
        public void LearnFromActivity(ElementActivity activity)
        {
            var profile = _userProfiles.GetOrAdd(activity.UserId, _ => new UserBehaviorProfile
            {
                UserId = activity.UserId,
                Username = activity.Username
            });

            // Update working hours pattern
            var hour = DateTime.Now.Hour;
            profile.HourlyActivityCounts[hour]++;
            profile.TotalActivities++;

            // Update category preferences
            if (!profile.CategoryPreferences.ContainsKey(activity.Category))
                profile.CategoryPreferences[activity.Category] = 0;
            profile.CategoryPreferences[activity.Category]++;

            // Update level preferences
            if (!string.IsNullOrEmpty(activity.LevelName))
            {
                if (!profile.LevelPreferences.ContainsKey(activity.LevelName))
                    profile.LevelPreferences[activity.LevelName] = 0;
                profile.LevelPreferences[activity.LevelName]++;
            }

            // Track element access patterns
            var elementPattern = _elementPatterns.GetOrAdd(activity.ElementId, _ => new ElementAccessPattern
            {
                ElementId = activity.ElementId,
                ElementName = activity.ElementName,
                Category = activity.Category
            });

            elementPattern.AccessHistory.Add(new ElementAccess
            {
                UserId = activity.UserId,
                Timestamp = activity.Timestamp,
                ActivityType = activity.ActivityType
            });

            // Keep only recent history
            while (elementPattern.AccessHistory.Count > 1000)
            {
                elementPattern.AccessHistory.RemoveAt(0);
            }

            profile.LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Get user's preferred working areas
        /// </summary>
        public UserWorkingPreferences GetUserPreferences(string userId)
        {
            if (!_userProfiles.TryGetValue(userId, out var profile))
            {
                return new UserWorkingPreferences();
            }

            return new UserWorkingPreferences
            {
                UserId = userId,
                PreferredCategories = profile.CategoryPreferences
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => kv.Key)
                    .ToList(),
                PreferredLevels = profile.LevelPreferences
                    .OrderByDescending(kv => kv.Value)
                    .Take(3)
                    .Select(kv => kv.Key)
                    .ToList(),
                PeakHours = profile.HourlyActivityCounts
                    .Select((count, hour) => (hour, count))
                    .OrderByDescending(x => x.count)
                    .Take(4)
                    .Select(x => x.hour)
                    .ToList(),
                AverageSessionDuration = profile.AverageSessionDuration,
                TypicalSyncFrequency = profile.TypicalSyncInterval
            };
        }

        #endregion

        #region Conflict Prediction

        /// <summary>
        /// Predict potential conflicts with probability scores
        /// </summary>
        public async Task<List<AIConflictPrediction>> PredictConflictsAsync(
            string userId,
            List<string> activeElements)
        {
            var predictions = new List<AIConflictPrediction>();

            foreach (var elementId in activeElements)
            {
                if (!_elementPatterns.TryGetValue(elementId, out var pattern))
                    continue;

                // Get other users who frequently access this element
                var otherUsers = pattern.AccessHistory
                    .Where(a => a.UserId != userId)
                    .Where(a => a.Timestamp > DateTime.UtcNow.AddHours(-8))
                    .GroupBy(a => a.UserId)
                    .Select(g => new { UserId = g.Key, Count = g.Count(), LastAccess = g.Max(a => a.Timestamp) })
                    .Where(u => u.Count >= 2)
                    .ToList();

                foreach (var otherUser in otherUsers)
                {
                    // Calculate conflict probability using multiple factors
                    var probability = CalculateConflictProbability(
                        userId,
                        otherUser.UserId,
                        elementId,
                        pattern);

                    if (probability > _conflictThreshold)
                    {
                        var prediction = new AIConflictPrediction
                        {
                            ElementId = elementId,
                            ElementName = pattern.ElementName,
                            Category = pattern.Category,
                            YourUserId = userId,
                            OtherUserId = otherUser.UserId,
                            OtherUsername = GetUsername(otherUser.UserId),
                            Probability = probability,
                            PredictedTime = PredictConflictTime(userId, otherUser.UserId, pattern),
                            Severity = DetermineConflictSeverity(probability, pattern),
                            Factors = GetConflictFactors(userId, otherUser.UserId, pattern),
                            Recommendations = GenerateConflictRecommendations(userId, otherUser.UserId, pattern)
                        };

                        predictions.Add(prediction);
                    }
                }
            }

            return predictions.OrderByDescending(p => p.Probability * (int)p.Severity).ToList();
        }

        private double CalculateConflictProbability(
            string userId1,
            string userId2,
            string elementId,
            ElementAccessPattern pattern)
        {
            // Factor 1: Temporal overlap (do they work at the same time?)
            var temporalOverlap = CalculateTemporalOverlap(userId1, userId2);

            // Factor 2: Access frequency (how often do they access this element?)
            var accessFrequency = CalculateAccessFrequency(pattern, userId1, userId2);

            // Factor 3: Historical conflicts
            var historicalConflicts = GetHistoricalConflictRate(userId1, userId2);

            // Factor 4: Element contention (how many people edit this?)
            var contentionLevel = CalculateContentionLevel(pattern);

            // Factor 5: Recent activity intensity
            var recentActivity = CalculateRecentActivityIntensity(pattern);

            // Weighted combination
            var probability =
                temporalOverlap * 0.25 +
                accessFrequency * 0.25 +
                historicalConflicts * 0.20 +
                contentionLevel * 0.15 +
                recentActivity * 0.15;

            return Math.Min(1.0, probability);
        }

        private double CalculateTemporalOverlap(string userId1, string userId2)
        {
            if (!_userProfiles.TryGetValue(userId1, out var profile1) ||
                !_userProfiles.TryGetValue(userId2, out var profile2))
                return 0.5; // Unknown, assume moderate

            // Calculate overlap of working hours
            var overlap = 0;
            for (int h = 0; h < 24; h++)
            {
                if (profile1.HourlyActivityCounts[h] > 0 && profile2.HourlyActivityCounts[h] > 0)
                    overlap++;
            }

            return overlap / 24.0;
        }

        private double CalculateAccessFrequency(ElementAccessPattern pattern, string userId1, string userId2)
        {
            var recent = pattern.AccessHistory.Where(a => a.Timestamp > DateTime.UtcNow.AddDays(-7)).ToList();
            var user1Count = recent.Count(a => a.UserId == userId1);
            var user2Count = recent.Count(a => a.UserId == userId2);

            return Math.Min(1.0, (user1Count + user2Count) / 20.0);
        }

        private double GetHistoricalConflictRate(string userId1, string userId2)
        {
            var relevantConflicts = _conflictHistory
                .Where(c => c.Timestamp > DateTime.UtcNow.AddDays(-30))
                .Where(c => (c.User1Id == userId1 && c.User2Id == userId2) ||
                           (c.User1Id == userId2 && c.User2Id == userId1))
                .ToList();

            return Math.Min(1.0, relevantConflicts.Count / 10.0);
        }

        private double CalculateContentionLevel(ElementAccessPattern pattern)
        {
            var uniqueUsers = pattern.AccessHistory
                .Where(a => a.Timestamp > DateTime.UtcNow.AddDays(-7))
                .Select(a => a.UserId)
                .Distinct()
                .Count();

            return Math.Min(1.0, uniqueUsers / 5.0);
        }

        private double CalculateRecentActivityIntensity(ElementAccessPattern pattern)
        {
            var recentCount = pattern.AccessHistory
                .Count(a => a.Timestamp > DateTime.UtcNow.AddHours(-1));

            return Math.Min(1.0, recentCount / 10.0);
        }

        private DateTime? PredictConflictTime(string userId1, string userId2, ElementAccessPattern pattern)
        {
            if (!_userProfiles.TryGetValue(userId1, out var profile1) ||
                !_userProfiles.TryGetValue(userId2, out var profile2))
                return null;

            // Find the next hour when both users are likely active
            var currentHour = DateTime.Now.Hour;
            for (int i = 0; i < 24; i++)
            {
                var checkHour = (currentHour + i) % 24;
                if (profile1.HourlyActivityCounts[checkHour] > 5 &&
                    profile2.HourlyActivityCounts[checkHour] > 5)
                {
                    var predictedTime = DateTime.Today.AddHours(checkHour);
                    if (predictedTime < DateTime.Now)
                        predictedTime = predictedTime.AddDays(1);
                    return predictedTime;
                }
            }

            return null;
        }

        private ConflictSeverity DetermineConflictSeverity(double probability, ElementAccessPattern pattern)
        {
            // High value elements (structural, MEP connections) are more severe
            var categoryWeight = pattern.Category.ToLower() switch
            {
                "structural columns" => 1.5,
                "structural framing" => 1.4,
                "structural foundations" => 1.5,
                "mechanical equipment" => 1.3,
                "electrical equipment" => 1.3,
                "plumbing fixtures" => 1.2,
                "walls" => 1.1,
                _ => 1.0
            };

            var adjustedProbability = probability * categoryWeight;

            return adjustedProbability switch
            {
                >= 0.9 => ConflictSeverity.Critical,
                >= 0.75 => ConflictSeverity.High,
                >= 0.5 => ConflictSeverity.Medium,
                _ => ConflictSeverity.Low
            };
        }

        private List<string> GetConflictFactors(string userId1, string userId2, ElementAccessPattern pattern)
        {
            var factors = new List<string>();

            var temporalOverlap = CalculateTemporalOverlap(userId1, userId2);
            if (temporalOverlap > 0.5)
                factors.Add("Overlapping work schedules");

            var historical = GetHistoricalConflictRate(userId1, userId2);
            if (historical > 0.3)
                factors.Add("History of conflicts between these users");

            var contention = CalculateContentionLevel(pattern);
            if (contention > 0.5)
                factors.Add("Multiple team members working on this element");

            var recentActivity = CalculateRecentActivityIntensity(pattern);
            if (recentActivity > 0.5)
                factors.Add("High recent activity on this element");

            return factors;
        }

        private List<string> GenerateConflictRecommendations(
            string userId1,
            string userId2,
            ElementAccessPattern pattern)
        {
            var recommendations = new List<string>();

            // Coordinate with the other user
            recommendations.Add($"Coordinate with {GetUsername(userId2)} before making changes");

            // Sync recommendation
            recommendations.Add("Sync with central before starting work on this element");

            // Consider workset
            recommendations.Add($"Consider taking ownership of the workset containing this element");

            // Time-based recommendation
            var profile1 = _userProfiles.GetValueOrDefault(userId1);
            var profile2 = _userProfiles.GetValueOrDefault(userId2);
            if (profile1 != null && profile2 != null)
            {
                var lowOverlapHour = FindLowOverlapHour(profile1, profile2);
                if (lowOverlapHour.HasValue)
                {
                    recommendations.Add($"Consider working on this element around {lowOverlapHour.Value}:00 to avoid overlap");
                }
            }

            return recommendations;
        }

        private int? FindLowOverlapHour(UserBehaviorProfile profile1, UserBehaviorProfile profile2)
        {
            // Find hour where user1 is active but user2 is not
            for (int h = 0; h < 24; h++)
            {
                if (profile1.HourlyActivityCounts[h] > 5 && profile2.HourlyActivityCounts[h] < 2)
                {
                    return h;
                }
            }
            return null;
        }

        private string GetUsername(string userId)
        {
            return _userProfiles.TryGetValue(userId, out var profile)
                ? profile.Username
                : userId;
        }

        #endregion

        #region Workset Optimization

        /// <summary>
        /// Get AI recommendations for optimal workset configuration
        /// </summary>
        public WorksetOptimizationRecommendation GetWorksetOptimization()
        {
            var recommendation = new WorksetOptimizationRecommendation();

            // Analyze element access patterns to suggest workset reorganization
            var elementsByAccessPattern = _elementPatterns.Values
                .Where(p => p.AccessHistory.Count > 10)
                .GroupBy(p => GetDominantUser(p))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToList();

            foreach (var group in elementsByAccessPattern)
            {
                var userId = group.Key;
                var username = GetUsername(userId);
                var elements = group.ToList();

                // Suggest creating/assigning workset for this user's frequently accessed elements
                recommendation.WorksetSuggestions.Add(new WorksetSuggestion
                {
                    SuggestedName = $"{username}'s Working Area",
                    RecommendedOwner = username,
                    Elements = elements.Select(e => e.ElementId).ToList(),
                    Reason = $"These {elements.Count} elements are primarily edited by {username}",
                    ExpectedConflictReduction = CalculateExpectedConflictReduction(elements)
                });
            }

            // Identify elements that need shared worksets
            var highContentionElements = _elementPatterns.Values
                .Where(p => CalculateContentionLevel(p) > 0.6)
                .ToList();

            if (highContentionElements.Any())
            {
                recommendation.SharedWorksetElements = highContentionElements
                    .Select(e => e.ElementId)
                    .ToList();

                recommendation.Notes.Add(
                    $"Consider creating a 'Coordination' workset for {highContentionElements.Count} " +
                    "high-contention elements that multiple team members frequently edit");
            }

            return recommendation;
        }

        private string GetDominantUser(ElementAccessPattern pattern)
        {
            return pattern.AccessHistory
                .Where(a => a.ActivityType == ActivityType.Editing)
                .GroupBy(a => a.UserId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? string.Empty;
        }

        private double CalculateExpectedConflictReduction(List<ElementAccessPattern> elements)
        {
            // Estimate how much conflicts would be reduced by proper workset assignment
            var currentContentionSum = elements.Sum(e => CalculateContentionLevel(e));
            var averageContention = currentContentionSum / elements.Count;

            // Workset ownership typically reduces conflicts by 60-80%
            return Math.Min(0.8, averageContention * 0.7);
        }

        #endregion

        #region Sync Timing Optimization

        /// <summary>
        /// Get optimal sync timing recommendations
        /// </summary>
        public SyncTimingRecommendation GetOptimalSyncTiming(string userId)
        {
            var recommendation = new SyncTimingRecommendation { UserId = userId };

            if (!_userProfiles.TryGetValue(userId, out var profile))
            {
                recommendation.RecommendedFrequency = TimeSpan.FromMinutes(30);
                recommendation.Reason = "Default recommendation for new users";
                return recommendation;
            }

            // Analyze user's typical sync patterns and conflict history
            var userConflicts = _conflictHistory
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .ToList();

            // More conflicts = more frequent syncs needed
            var conflictsPerDay = userConflicts.Count / 30.0;
            var baseFrequency = conflictsPerDay switch
            {
                > 3 => TimeSpan.FromMinutes(15),
                > 1 => TimeSpan.FromMinutes(30),
                > 0.5 => TimeSpan.FromMinutes(45),
                _ => TimeSpan.FromHours(1)
            };

            recommendation.RecommendedFrequency = baseFrequency;

            // Find optimal times based on team activity
            var lowActivityHours = GetLowTeamActivityHours();
            recommendation.OptimalSyncTimes = lowActivityHours
                .Select(h => DateTime.Today.AddHours(h))
                .ToList();

            // Calculate urgency based on current conditions
            var activeElements = profile.CategoryPreferences.Keys.Count();
            var teamActivityLevel = GetCurrentTeamActivityLevel();

            if (teamActivityLevel > 0.7 && activeElements > 10)
            {
                recommendation.UrgencyLevel = SyncUrgency.High;
                recommendation.Reason = "High team activity and many active elements - sync soon to avoid conflicts";
            }
            else if (teamActivityLevel > 0.4 || activeElements > 5)
            {
                recommendation.UrgencyLevel = SyncUrgency.Medium;
                recommendation.Reason = "Moderate activity - sync when convenient";
            }
            else
            {
                recommendation.UrgencyLevel = SyncUrgency.Low;
                recommendation.Reason = "Low activity - sync at your normal pace";
            }

            return recommendation;
        }

        private List<int> GetLowTeamActivityHours()
        {
            // Find hours with lowest aggregate team activity
            var hourlyActivity = new int[24];

            foreach (var profile in _userProfiles.Values)
            {
                for (int h = 0; h < 24; h++)
                {
                    hourlyActivity[h] += profile.HourlyActivityCounts[h];
                }
            }

            return hourlyActivity
                .Select((count, hour) => (hour, count))
                .OrderBy(x => x.count)
                .Take(4)
                .Select(x => x.hour)
                .OrderBy(h => h)
                .ToList();
        }

        private double GetCurrentTeamActivityLevel()
        {
            var currentHour = DateTime.Now.Hour;
            var recentActivities = _userProfiles.Values
                .Sum(p => p.HourlyActivityCounts[currentHour]);

            var maxHistoricalActivity = _userProfiles.Values
                .SelectMany(p => p.HourlyActivityCounts)
                .Max();

            return maxHistoricalActivity > 0
                ? (double)recentActivities / maxHistoricalActivity
                : 0;
        }

        #endregion

        #region Team Dynamics Analysis

        /// <summary>
        /// Analyze team collaboration patterns and dynamics
        /// </summary>
        public TeamDynamicsReport AnalyzeTeamDynamics()
        {
            var report = new TeamDynamicsReport
            {
                GeneratedAt = DateTime.UtcNow,
                AnalysisPeriod = _learningWindow
            };

            // Collaboration pairs - who works together most
            var collaborationPairs = AnalyzeCollaborationPairs();
            report.StrongCollaborations = collaborationPairs
                .Where(p => p.Score > 0.7)
                .ToList();

            // Potential friction points
            report.FrictionPoints = _conflictHistory
                .GroupBy(c => (Math.Min(c.User1Id.GetHashCode(), c.User2Id.GetHashCode()),
                              Math.Max(c.User1Id.GetHashCode(), c.User2Id.GetHashCode())))
                .Where(g => g.Count() >= 3)
                .Select(g => new FrictionPoint
                {
                    User1 = GetUsername(g.First().User1Id),
                    User2 = GetUsername(g.First().User2Id),
                    ConflictCount = g.Count(),
                    LastConflict = g.Max(c => c.Timestamp),
                    CommonElements = g.Select(c => c.ElementId).Distinct().ToList()
                })
                .ToList();

            // Workload distribution
            report.WorkloadDistribution = _userProfiles.Values
                .Select(p => new UserWorkload
                {
                    Username = p.Username,
                    TotalActivities = p.TotalActivities,
                    ActiveDays = CalculateActiveDays(p),
                    PrimaryCategories = p.CategoryPreferences
                        .OrderByDescending(kv => kv.Value)
                        .Take(3)
                        .Select(kv => kv.Key)
                        .ToList()
                })
                .OrderByDescending(w => w.TotalActivities)
                .ToList();

            // Knowledge silos (users who are the only ones working on certain areas)
            report.KnowledgeSilos = IdentifyKnowledgeSilos();

            // Recommendations
            report.Recommendations = GenerateTeamRecommendations(report);

            return report;
        }

        private List<CollaborationPair> AnalyzeCollaborationPairs()
        {
            var pairs = new Dictionary<(string, string), int>();

            foreach (var pattern in _elementPatterns.Values)
            {
                var users = pattern.AccessHistory
                    .Select(a => a.UserId)
                    .Distinct()
                    .OrderBy(u => u)
                    .ToList();

                for (int i = 0; i < users.Count; i++)
                {
                    for (int j = i + 1; j < users.Count; j++)
                    {
                        var key = (users[i], users[j]);
                        pairs[key] = pairs.GetValueOrDefault(key) + 1;
                    }
                }
            }

            var maxCount = pairs.Values.Max();

            return pairs
                .Select(kv => new CollaborationPair
                {
                    User1 = GetUsername(kv.Key.Item1),
                    User2 = GetUsername(kv.Key.Item2),
                    SharedElements = kv.Value,
                    Score = (double)kv.Value / maxCount
                })
                .OrderByDescending(p => p.Score)
                .Take(10)
                .ToList();
        }

        private int CalculateActiveDays(UserBehaviorProfile profile)
        {
            // Estimate based on activity patterns
            return profile.HourlyActivityCounts.Count(c => c > 0) > 12 ? 5 : 3;
        }

        private List<KnowledgeSilo> IdentifyKnowledgeSilos()
        {
            var silos = new List<KnowledgeSilo>();

            // Group elements by their dominant user
            var elementsByUser = _elementPatterns.Values
                .GroupBy(p => GetDominantUser(p))
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var group in elementsByUser)
            {
                // Check if only one user accesses these elements
                var exclusiveElements = group
                    .Where(p => p.AccessHistory.Select(a => a.UserId).Distinct().Count() == 1)
                    .ToList();

                if (exclusiveElements.Count >= 5)
                {
                    silos.Add(new KnowledgeSilo
                    {
                        Username = GetUsername(group.Key),
                        ExclusiveElements = exclusiveElements.Count,
                        Categories = exclusiveElements
                            .Select(e => e.Category)
                            .Distinct()
                            .ToList(),
                        Risk = exclusiveElements.Count > 20 ? "High" : "Medium"
                    });
                }
            }

            return silos;
        }

        private List<string> GenerateTeamRecommendations(TeamDynamicsReport report)
        {
            var recommendations = new List<string>();

            // Address friction points
            if (report.FrictionPoints.Any())
            {
                var topFriction = report.FrictionPoints.First();
                recommendations.Add(
                    $"Schedule a coordination meeting between {topFriction.User1} and {topFriction.User2} - " +
                    $"they've had {topFriction.ConflictCount} conflicts this month");
            }

            // Address knowledge silos
            if (report.KnowledgeSilos.Any(s => s.Risk == "High"))
            {
                var highRiskSilo = report.KnowledgeSilos.First(s => s.Risk == "High");
                recommendations.Add(
                    $"Consider cross-training on {string.Join(", ", highRiskSilo.Categories.Take(2))} - " +
                    $"currently only {highRiskSilo.Username} works on {highRiskSilo.ExclusiveElements} elements in these areas");
            }

            // Workload balancing
            if (report.WorkloadDistribution.Count > 1)
            {
                var maxWorkload = report.WorkloadDistribution.First().TotalActivities;
                var minWorkload = report.WorkloadDistribution.Last().TotalActivities;

                if (maxWorkload > minWorkload * 3)
                {
                    recommendations.Add(
                        $"Workload imbalance detected: {report.WorkloadDistribution.First().Username} " +
                        $"has 3x more activity than {report.WorkloadDistribution.Last().Username}");
                }
            }

            return recommendations;
        }

        #endregion

        #region Conflict Learning

        /// <summary>
        /// Learn from a conflict that occurred
        /// </summary>
        public void LearnFromConflict(ConflictHistoryEntry conflict)
        {
            _conflictHistory.Add(conflict);

            // Update element pattern
            if (_elementPatterns.TryGetValue(conflict.ElementId, out var pattern))
            {
                pattern.ConflictCount++;
            }

            // Keep history manageable
            while (_conflictHistory.Count > 10000)
            {
                _conflictHistory.RemoveAt(0);
            }

            Logger.Info($"Learned from conflict between {conflict.User1Id} and {conflict.User2Id} on {conflict.ElementId}");
        }

        /// <summary>
        /// Learn from a successful sync
        /// </summary>
        public void LearnFromSync(SyncPatternEntry sync)
        {
            _syncHistory.Add(sync);

            // Update user profile
            if (_userProfiles.TryGetValue(sync.UserId, out var profile))
            {
                profile.SyncCount++;
                if (profile.LastSyncTime.HasValue)
                {
                    var interval = sync.Timestamp - profile.LastSyncTime.Value;
                    profile.TypicalSyncInterval = TimeSpan.FromMinutes(
                        (profile.TypicalSyncInterval.TotalMinutes + interval.TotalMinutes) / 2);
                }
                profile.LastSyncTime = sync.Timestamp;
            }

            // Keep history manageable
            while (_syncHistory.Count > 10000)
            {
                _syncHistory.RemoveAt(0);
            }
        }

        #endregion
    }

    #region AI Models

    // Simplified model classes - in production would use ONNX inference

    internal class ConflictPredictionModel
    {
        public double Predict(double[] features) => features.Average();
    }

    internal class WorksetRecommendationModel
    {
        public List<string> Recommend(string userId) => new();
    }

    internal class SyncTimingModel
    {
        public TimeSpan PredictOptimalInterval(string userId) => TimeSpan.FromMinutes(30);
    }

    internal class TeamDynamicsModel
    {
        public double CalculateCollaborationScore(string user1, string user2) => 0.5;
    }

    #endregion

    #region Data Models

    public class ElementAccessPattern
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<ElementAccess> AccessHistory { get; set; } = new();
        public int ConflictCount { get; set; }
    }

    public class ElementAccess
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public ActivityType ActivityType { get; set; }
    }

    public class WorksetOptimizationData
    {
        public string WorksetId { get; set; } = string.Empty;
        public string WorksetName { get; set; } = string.Empty;
        public Dictionary<string, int> UserAccessCounts { get; set; } = new();
    }

    public class ConflictHistoryEntry
    {
        public string ConflictId { get; set; } = Guid.NewGuid().ToString();
        public string ElementId { get; set; } = string.Empty;
        public string User1Id { get; set; } = string.Empty;
        public string User2Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public bool WasResolved { get; set; }
    }

    public class SyncPatternEntry
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int ElementsModified { get; set; }
        public int ConflictsEncountered { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class UserWorkingPreferences
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> PreferredCategories { get; set; } = new();
        public List<string> PreferredLevels { get; set; } = new();
        public List<int> PeakHours { get; set; } = new();
        public TimeSpan AverageSessionDuration { get; set; }
        public TimeSpan TypicalSyncFrequency { get; set; }
    }

    public class AIConflictPrediction
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string YourUserId { get; set; } = string.Empty;
        public string OtherUserId { get; set; } = string.Empty;
        public string OtherUsername { get; set; } = string.Empty;
        public double Probability { get; set; }
        public DateTime? PredictedTime { get; set; }
        public ConflictSeverity Severity { get; set; }
        public List<string> Factors { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class WorksetOptimizationRecommendation
    {
        public List<WorksetSuggestion> WorksetSuggestions { get; set; } = new();
        public List<string> SharedWorksetElements { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    public class WorksetSuggestion
    {
        public string SuggestedName { get; set; } = string.Empty;
        public string RecommendedOwner { get; set; } = string.Empty;
        public List<string> Elements { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public double ExpectedConflictReduction { get; set; }
    }

    public class SyncTimingRecommendation
    {
        public string UserId { get; set; } = string.Empty;
        public TimeSpan RecommendedFrequency { get; set; }
        public List<DateTime> OptimalSyncTimes { get; set; } = new();
        public SyncUrgency UrgencyLevel { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public enum SyncUrgency
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class TeamDynamicsReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan AnalysisPeriod { get; set; }
        public List<CollaborationPair> StrongCollaborations { get; set; } = new();
        public List<FrictionPoint> FrictionPoints { get; set; } = new();
        public List<UserWorkload> WorkloadDistribution { get; set; } = new();
        public List<KnowledgeSilo> KnowledgeSilos { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class CollaborationPair
    {
        public string User1 { get; set; } = string.Empty;
        public string User2 { get; set; } = string.Empty;
        public int SharedElements { get; set; }
        public double Score { get; set; }
    }

    public class FrictionPoint
    {
        public string User1 { get; set; } = string.Empty;
        public string User2 { get; set; } = string.Empty;
        public int ConflictCount { get; set; }
        public DateTime LastConflict { get; set; }
        public List<string> CommonElements { get; set; } = new();
    }

    public class UserWorkload
    {
        public string Username { get; set; } = string.Empty;
        public int TotalActivities { get; set; }
        public int ActiveDays { get; set; }
        public List<string> PrimaryCategories { get; set; } = new();
    }

    public class KnowledgeSilo
    {
        public string Username { get; set; } = string.Empty;
        public int ExclusiveElements { get; set; }
        public List<string> Categories { get; set; } = new();
        public string Risk { get; set; } = string.Empty;
    }

    #endregion
}
