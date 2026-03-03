// =========================================================================
// StingBIM.AI.Knowledge - Context-Aware Memory System
// Learns user preferences and adapts to working patterns
// =========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Knowledge.Memory
{
    /// <summary>
    /// Context-aware memory system that learns from user interactions
    /// and adapts AI behavior to individual preferences and patterns.
    /// </summary>
    public class ContextMemory
    {
        private readonly Dictionary<string, UserProfile> _userProfiles;
        private readonly Dictionary<string, ProjectContext> _projectContexts;
        private readonly List<InteractionRecord> _interactionHistory;
        private readonly Dictionary<string, PreferenceCluster> _preferenceClusters;
        private readonly SessionContext _currentSession;
        private readonly MemoryConfiguration _config;
        private readonly string _profileStoragePath;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private const int MaxHistorySize = 10000;
        private const int ShortTermMemorySize = 50;

        public ContextMemory(MemoryConfiguration config = null)
        {
            _config = config ?? new MemoryConfiguration();
            _userProfiles = new Dictionary<string, UserProfile>();
            _projectContexts = new Dictionary<string, ProjectContext>();
            _interactionHistory = new List<InteractionRecord>();
            _preferenceClusters = new Dictionary<string, PreferenceCluster>();
            _currentSession = new SessionContext();

            _profileStoragePath = _config.StoragePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "user_profiles.json");

            InitializePreferenceClusters();
            LoadUserProfiles();
        }

        #region Initialization

        private void InitializePreferenceClusters()
        {
            // Design style preferences
            _preferenceClusters["DesignStyle"] = new PreferenceCluster
            {
                ClusterId = "DesignStyle",
                Dimensions = new[]
                {
                    "Minimalist-Ornate",
                    "Modern-Traditional",
                    "Open-Compartmentalized",
                    "Formal-Casual"
                },
                DefaultValues = new Dictionary<string, double>
                {
                    ["Minimalist-Ornate"] = 0.5,
                    ["Modern-Traditional"] = 0.5,
                    ["Open-Compartmentalized"] = 0.5,
                    ["Formal-Casual"] = 0.5
                }
            };

            // Workflow preferences
            _preferenceClusters["Workflow"] = new PreferenceCluster
            {
                ClusterId = "Workflow",
                Dimensions = new[]
                {
                    "Guidance-Autonomy",
                    "Detailed-Summary",
                    "Cautious-Fast",
                    "Interactive-Batch"
                },
                DefaultValues = new Dictionary<string, double>
                {
                    ["Guidance-Autonomy"] = 0.5,
                    ["Detailed-Summary"] = 0.5,
                    ["Cautious-Fast"] = 0.5,
                    ["Interactive-Batch"] = 0.5
                }
            };

            // Technical preferences
            _preferenceClusters["Technical"] = new PreferenceCluster
            {
                ClusterId = "Technical",
                Dimensions = new[]
                {
                    "Metric-Imperial",
                    "Precision-Approximate",
                    "Standards-Creative",
                    "Manual-Automated"
                },
                DefaultValues = new Dictionary<string, double>
                {
                    ["Metric-Imperial"] = 0.0, // 0 = Metric
                    ["Precision-Approximate"] = 0.7,
                    ["Standards-Creative"] = 0.3,
                    ["Manual-Automated"] = 0.6
                }
            };

            // Communication preferences
            _preferenceClusters["Communication"] = new PreferenceCluster
            {
                ClusterId = "Communication",
                Dimensions = new[]
                {
                    "Verbose-Concise",
                    "Technical-Simple",
                    "Proactive-Reactive",
                    "Formal-Casual"
                },
                DefaultValues = new Dictionary<string, double>
                {
                    ["Verbose-Concise"] = 0.4,
                    ["Technical-Simple"] = 0.6,
                    ["Proactive-Reactive"] = 0.5,
                    ["Formal-Casual"] = 0.3
                }
            };
        }

        #endregion

        #region User Profile Management

        /// <summary>
        /// Get or create a user profile.
        /// </summary>
        public UserProfile GetUserProfile(string userId)
        {
            if (!_userProfiles.TryGetValue(userId, out var profile))
            {
                profile = CreateDefaultProfile(userId);
                _userProfiles[userId] = profile;
            }
            return profile;
        }

        /// <summary>
        /// Update user profile based on observed behavior.
        /// </summary>
        public void UpdateUserProfile(string userId, BehaviorObservation observation)
        {
            var profile = GetUserProfile(userId);

            // Update skill assessment
            if (observation.SkillIndicators != null)
            {
                foreach (var skill in observation.SkillIndicators)
                {
                    UpdateSkillLevel(profile, skill.Key, skill.Value);
                }
            }

            // Update preferences based on choices
            if (observation.PreferenceSignals != null)
            {
                foreach (var signal in observation.PreferenceSignals)
                {
                    UpdatePreference(profile, signal.ClusterId, signal.Dimension, signal.Value);
                }
            }

            // Update interaction patterns
            profile.InteractionCount++;
            profile.LastInteraction = DateTime.UtcNow;

            // Update frequently used features
            if (!string.IsNullOrEmpty(observation.FeatureUsed))
            {
                if (!profile.FeatureUsageCount.ContainsKey(observation.FeatureUsed))
                    profile.FeatureUsageCount[observation.FeatureUsed] = 0;
                profile.FeatureUsageCount[observation.FeatureUsed]++;
            }

            // Persist updated profiles (throttled: every 10 interactions)
            if (profile.InteractionCount % 10 == 0)
            {
                SaveUserProfiles();
            }
        }

        private UserProfile CreateDefaultProfile(string userId)
        {
            var profile = new UserProfile
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastInteraction = DateTime.UtcNow,
                SkillLevels = new Dictionary<string, SkillLevel>
                {
                    ["Revit"] = SkillLevel.Intermediate,
                    ["BIM"] = SkillLevel.Intermediate,
                    ["Architecture"] = SkillLevel.Intermediate,
                    ["Structural"] = SkillLevel.Beginner,
                    ["MEP"] = SkillLevel.Beginner
                },
                Preferences = new Dictionary<string, Dictionary<string, double>>(),
                FeatureUsageCount = new Dictionary<string, int>(),
                CustomSettings = new Dictionary<string, object>()
            };

            // Copy default preferences
            foreach (var cluster in _preferenceClusters.Values)
            {
                profile.Preferences[cluster.ClusterId] =
                    new Dictionary<string, double>(cluster.DefaultValues);
            }

            return profile;
        }

        private void UpdateSkillLevel(UserProfile profile, string skill, double indicator)
        {
            // Bayesian update of skill assessment
            var current = profile.SkillLevels.GetValueOrDefault(skill, SkillLevel.Intermediate);
            var currentValue = (double)current / 4.0;

            // Weighted moving average
            var newValue = currentValue * 0.9 + indicator * 0.1;

            profile.SkillLevels[skill] = newValue switch
            {
                < 0.2 => SkillLevel.Novice,
                < 0.4 => SkillLevel.Beginner,
                < 0.6 => SkillLevel.Intermediate,
                < 0.8 => SkillLevel.Advanced,
                _ => SkillLevel.Expert
            };
        }

        private void UpdatePreference(UserProfile profile, string clusterId, string dimension, double value)
        {
            if (!profile.Preferences.ContainsKey(clusterId))
                profile.Preferences[clusterId] = new Dictionary<string, double>();

            var current = profile.Preferences[clusterId].GetValueOrDefault(dimension, 0.5);

            // Exponential moving average for preference learning
            profile.Preferences[clusterId][dimension] = current * 0.85 + value * 0.15;
        }

        /// <summary>
        /// Saves all user profiles to persistent storage.
        /// </summary>
        public void SaveUserProfiles()
        {
            try
            {
                var dir = Path.GetDirectoryName(_profileStoragePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_userProfiles, _jsonOptions);
                File.WriteAllText(_profileStoragePath, json);
            }
            catch (Exception)
            {
                // Silently fail on persistence errors to avoid disrupting the main workflow
            }
        }

        private void LoadUserProfiles()
        {
            try
            {
                if (File.Exists(_profileStoragePath))
                {
                    var json = File.ReadAllText(_profileStoragePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, UserProfile>>(json, _jsonOptions);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded)
                        {
                            _userProfiles[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Start fresh if loading fails
            }
        }

        #endregion

        #region Project Context

        /// <summary>
        /// Get or create project context.
        /// </summary>
        public ProjectContext GetProjectContext(string projectId)
        {
            if (!_projectContexts.TryGetValue(projectId, out var context))
            {
                context = new ProjectContext
                {
                    ProjectId = projectId,
                    CreatedAt = DateTime.UtcNow,
                    DesignDecisions = new List<DesignDecision>(),
                    ActiveConstraints = new List<DesignConstraint>(),
                    RecentElements = new Queue<ElementReference>(),
                    SpatialContext = new SpatialContext()
                };
                _projectContexts[projectId] = context;
            }
            return context;
        }

        /// <summary>
        /// Record a design decision for future reference.
        /// </summary>
        public void RecordDesignDecision(string projectId, DesignDecision decision)
        {
            var context = GetProjectContext(projectId);
            decision.Timestamp = DateTime.UtcNow;
            context.DesignDecisions.Add(decision);

            // Update related constraints
            if (decision.ImpliedConstraints != null)
            {
                foreach (var constraint in decision.ImpliedConstraints)
                {
                    if (!context.ActiveConstraints.Any(c => c.ConstraintId == constraint.ConstraintId))
                    {
                        context.ActiveConstraints.Add(constraint);
                    }
                }
            }
        }

        /// <summary>
        /// Get relevant past decisions for current context.
        /// </summary>
        public List<DesignDecision> GetRelevantDecisions(string projectId, string context, int limit = 5)
        {
            var projectContext = GetProjectContext(projectId);

            return projectContext.DesignDecisions
                .Where(d => IsDecisionRelevant(d, context))
                .OrderByDescending(d => CalculateRelevanceScore(d, context))
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Track recently modified elements for context awareness.
        /// </summary>
        public void TrackElement(string projectId, ElementReference element)
        {
            var context = GetProjectContext(projectId);

            context.RecentElements.Enqueue(element);
            while (context.RecentElements.Count > 20)
                context.RecentElements.Dequeue();

            // Update spatial context
            if (element.Location != null)
            {
                context.SpatialContext.LastEditLocation = element.Location;
                context.SpatialContext.RecentEditLocations.Add(element.Location);
                if (context.SpatialContext.RecentEditLocations.Count > 10)
                    context.SpatialContext.RecentEditLocations.RemoveAt(0);
            }
        }

        #endregion

        #region Session Context

        /// <summary>
        /// Record an interaction in the current session.
        /// </summary>
        public void RecordInteraction(InteractionRecord interaction)
        {
            interaction.Timestamp = DateTime.UtcNow;
            interaction.SessionId = _currentSession.SessionId;

            _interactionHistory.Add(interaction);
            _currentSession.Interactions.Add(interaction);

            // Maintain history size
            while (_interactionHistory.Count > MaxHistorySize)
                _interactionHistory.RemoveAt(0);

            // Update session patterns
            UpdateSessionPatterns(interaction);
        }

        /// <summary>
        /// Get short-term context from recent interactions.
        /// </summary>
        public ShortTermContext GetShortTermContext()
        {
            var recentInteractions = _currentSession.Interactions
                .TakeLast(ShortTermMemorySize)
                .ToList();

            return new ShortTermContext
            {
                RecentCommands = recentInteractions
                    .Where(i => i.Type == InteractionType.Command)
                    .Select(i => i.Content)
                    .ToList(),

                RecentEntities = recentInteractions
                    .SelectMany(i => i.MentionedEntities ?? Enumerable.Empty<string>())
                    .Distinct()
                    .ToList(),

                CurrentTopic = InferCurrentTopic(recentInteractions),

                RecentErrors = recentInteractions
                    .Where(i => i.Type == InteractionType.Error)
                    .Select(i => i.Content)
                    .ToList(),

                SessionDuration = DateTime.UtcNow - _currentSession.StartTime,
                InteractionCount = _currentSession.Interactions.Count
            };
        }

        /// <summary>
        /// Predict likely next action based on patterns.
        /// </summary>
        public List<ActionPrediction> PredictNextActions(string userId, string projectId)
        {
            var predictions = new List<ActionPrediction>();
            var profile = GetUserProfile(userId);
            var projectContext = GetProjectContext(projectId);

            // Pattern-based predictions
            var recentCommands = _currentSession.Interactions
                .Where(i => i.Type == InteractionType.Command)
                .Select(i => i.CommandType)
                .TakeLast(5)
                .ToList();

            var patternPredictions = PredictFromPatterns(recentCommands);
            predictions.AddRange(patternPredictions);

            // Context-based predictions
            if (projectContext.RecentElements.Any())
            {
                var lastElement = projectContext.RecentElements.Last();
                var contextPredictions = PredictFromContext(lastElement);
                predictions.AddRange(contextPredictions);
            }

            // User habit predictions
            var habitPredictions = PredictFromHabits(profile);
            predictions.AddRange(habitPredictions);

            return predictions
                .GroupBy(p => p.ActionType)
                .Select(g => new ActionPrediction
                {
                    ActionType = g.Key,
                    Confidence = g.Max(p => p.Confidence),
                    Rationale = g.First().Rationale
                })
                .OrderByDescending(p => p.Confidence)
                .Take(5)
                .ToList();
        }

        #endregion

        #region Learning and Adaptation

        /// <summary>
        /// Learn from user feedback on AI suggestions.
        /// </summary>
        public void LearnFromFeedback(string userId, FeedbackRecord feedback)
        {
            var profile = GetUserProfile(userId);

            // Update preference based on acceptance/rejection
            if (feedback.SuggestionType != null)
            {
                var adjustment = feedback.Accepted ? 0.1 : -0.1;

                if (feedback.PreferenceSignals != null)
                {
                    foreach (var signal in feedback.PreferenceSignals)
                    {
                        var current = profile.Preferences
                            .GetValueOrDefault(signal.ClusterId, new Dictionary<string, double>())
                            .GetValueOrDefault(signal.Dimension, 0.5);

                        var newValue = Math.Clamp(current + adjustment * signal.Value, 0.0, 1.0);
                        UpdatePreference(profile, signal.ClusterId, signal.Dimension, newValue);
                    }
                }
            }

            // Record interaction outcome
            RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Feedback,
                Content = feedback.Accepted ? "Accepted" : "Rejected",
                Context = feedback.SuggestionType,
                Outcome = feedback.Accepted ? InteractionOutcome.Success : InteractionOutcome.Rejected
            });
        }

        /// <summary>
        /// Get personalized recommendations based on learned preferences.
        /// </summary>
        public PersonalizedRecommendations GetRecommendations(string userId, string projectId, string context)
        {
            var profile = GetUserProfile(userId);
            var projectContext = GetProjectContext(projectId);

            var recommendations = new PersonalizedRecommendations
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow
            };

            // Communication style
            recommendations.CommunicationStyle = new CommunicationStyle
            {
                Verbosity = profile.Preferences
                    .GetValueOrDefault("Communication", new Dictionary<string, double>())
                    .GetValueOrDefault("Verbose-Concise", 0.5),
                TechnicalLevel = profile.Preferences
                    .GetValueOrDefault("Communication", new Dictionary<string, double>())
                    .GetValueOrDefault("Technical-Simple", 0.6),
                Proactivity = profile.Preferences
                    .GetValueOrDefault("Communication", new Dictionary<string, double>())
                    .GetValueOrDefault("Proactive-Reactive", 0.5)
            };

            // Workflow adaptations
            recommendations.WorkflowAdaptations = new WorkflowAdaptations
            {
                GuidanceLevel = profile.Preferences
                    .GetValueOrDefault("Workflow", new Dictionary<string, double>())
                    .GetValueOrDefault("Guidance-Autonomy", 0.5),
                BatchOperations = profile.Preferences
                    .GetValueOrDefault("Workflow", new Dictionary<string, double>())
                    .GetValueOrDefault("Interactive-Batch", 0.5) > 0.5,
                AutomationLevel = profile.Preferences
                    .GetValueOrDefault("Technical", new Dictionary<string, double>())
                    .GetValueOrDefault("Manual-Automated", 0.6)
            };

            // Feature suggestions based on skill level
            recommendations.SuggestedFeatures = GetSkillAppropriateFeatures(profile);

            // Context-aware suggestions
            recommendations.ContextualSuggestions = GetContextualSuggestions(
                profile, projectContext, context);

            return recommendations;
        }

        #endregion

        #region Private Methods

        private void UpdateSessionPatterns(InteractionRecord interaction)
        {
            if (interaction.Type == InteractionType.Command)
            {
                if (!_currentSession.CommandFrequency.ContainsKey(interaction.CommandType))
                    _currentSession.CommandFrequency[interaction.CommandType] = 0;
                _currentSession.CommandFrequency[interaction.CommandType]++;
            }

            // Track command sequences
            if (_currentSession.LastCommand != null && interaction.Type == InteractionType.Command)
            {
                var sequence = $"{_currentSession.LastCommand}→{interaction.CommandType}";
                if (!_currentSession.CommandSequences.ContainsKey(sequence))
                    _currentSession.CommandSequences[sequence] = 0;
                _currentSession.CommandSequences[sequence]++;
            }

            if (interaction.Type == InteractionType.Command)
                _currentSession.LastCommand = interaction.CommandType;
        }

        private bool IsDecisionRelevant(DesignDecision decision, string context)
        {
            if (string.IsNullOrEmpty(context)) return true;

            // Check topic overlap
            if (decision.Topics != null && decision.Topics.Any(t =>
                context.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Check element type relevance
            if (decision.AffectedElementTypes != null && decision.AffectedElementTypes.Any(e =>
                context.Contains(e, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        private double CalculateRelevanceScore(DesignDecision decision, string context)
        {
            double score = 0;

            // Recency factor
            var age = DateTime.UtcNow - decision.Timestamp;
            score += Math.Max(0, 1 - age.TotalDays / 30);

            // Topic match
            if (decision.Topics != null)
            {
                var matches = decision.Topics.Count(t =>
                    context.Contains(t, StringComparison.OrdinalIgnoreCase));
                score += matches * 0.3;
            }

            // Importance factor
            score += decision.Importance * 0.2;

            return score;
        }

        private string InferCurrentTopic(List<InteractionRecord> recentInteractions)
        {
            // Simple topic inference from recent entities and commands
            var entities = recentInteractions
                .SelectMany(i => i.MentionedEntities ?? Enumerable.Empty<string>())
                .GroupBy(e => e)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return entities?.Key ?? "General";
        }

        private List<ActionPrediction> PredictFromPatterns(List<string> recentCommands)
        {
            var predictions = new List<ActionPrediction>();

            // Common patterns
            var patterns = new Dictionary<string, List<string>>
            {
                ["CreateWall"] = new List<string> { "CreateDoor", "CreateWindow", "SetParameter" },
                ["CreateRoom"] = new List<string> { "CreateDoor", "SetRoomName", "SetRoomNumber" },
                ["SelectElement"] = new List<string> { "ModifyElement", "DeleteElement", "CopyElement" },
                ["CreateFloor"] = new List<string> { "CreateRoom", "CreateWall", "SetMaterial" },
                ["SetMaterial"] = new List<string> { "SetParameter", "CreateSchedule" }
            };

            if (recentCommands.Any())
            {
                var lastCommand = recentCommands.Last();
                if (patterns.TryGetValue(lastCommand, out var likely))
                {
                    for (int i = 0; i < likely.Count; i++)
                    {
                        predictions.Add(new ActionPrediction
                        {
                            ActionType = likely[i],
                            Confidence = 0.8 - (i * 0.15),
                            Rationale = $"Common follow-up to {lastCommand}"
                        });
                    }
                }
            }

            // Session sequence patterns
            foreach (var seq in _currentSession.CommandSequences.OrderByDescending(s => s.Value).Take(3))
            {
                var parts = seq.Key.Split('→');
                if (parts.Length == 2 && recentCommands.LastOrDefault() == parts[0])
                {
                    predictions.Add(new ActionPrediction
                    {
                        ActionType = parts[1],
                        Confidence = Math.Min(0.9, 0.5 + seq.Value * 0.1),
                        Rationale = "Frequent pattern in this session"
                    });
                }
            }

            return predictions;
        }

        private List<ActionPrediction> PredictFromContext(ElementReference lastElement)
        {
            var predictions = new List<ActionPrediction>();

            var elementTypePredictions = new Dictionary<string, List<(string Action, double Confidence)>>
            {
                ["Wall"] = new List<(string, double)>
                {
                    ("CreateDoor", 0.7), ("CreateWindow", 0.6), ("JoinGeometry", 0.4)
                },
                ["Room"] = new List<(string, double)>
                {
                    ("PlaceFurniture", 0.6), ("SetFinishes", 0.5), ("CalculateArea", 0.4)
                },
                ["Floor"] = new List<(string, double)>
                {
                    ("SetMaterial", 0.5), ("CreateOpening", 0.4), ("ModifyBoundary", 0.3)
                }
            };

            if (elementTypePredictions.TryGetValue(lastElement.ElementType, out var typePredictions))
            {
                foreach (var (action, confidence) in typePredictions)
                {
                    predictions.Add(new ActionPrediction
                    {
                        ActionType = action,
                        Confidence = confidence,
                        Rationale = $"Common action after working with {lastElement.ElementType}"
                    });
                }
            }

            return predictions;
        }

        private List<ActionPrediction> PredictFromHabits(UserProfile profile)
        {
            var predictions = new List<ActionPrediction>();

            // Suggest frequently used features
            var topFeatures = profile.FeatureUsageCount
                .OrderByDescending(f => f.Value)
                .Take(3);

            foreach (var feature in topFeatures)
            {
                predictions.Add(new ActionPrediction
                {
                    ActionType = feature.Key,
                    Confidence = 0.3 + Math.Min(0.3, feature.Value / 100.0),
                    Rationale = "Frequently used feature"
                });
            }

            return predictions;
        }

        private List<string> GetSkillAppropriateFeatures(UserProfile profile)
        {
            var features = new List<string>();
            var revitSkill = profile.SkillLevels.GetValueOrDefault("Revit", SkillLevel.Intermediate);

            switch (revitSkill)
            {
                case SkillLevel.Novice:
                case SkillLevel.Beginner:
                    features.AddRange(new[] { "Basic modeling", "Simple schedules", "Parameter editing" });
                    break;
                case SkillLevel.Intermediate:
                    features.AddRange(new[] { "Family editing", "Phasing", "Design options", "Worksets" });
                    break;
                case SkillLevel.Advanced:
                case SkillLevel.Expert:
                    features.AddRange(new[] { "Dynamo scripts", "API automation", "Complex families", "Multi-model coordination" });
                    break;
            }

            return features;
        }

        private List<string> GetContextualSuggestions(
            UserProfile profile,
            ProjectContext projectContext,
            string context)
        {
            var suggestions = new List<string>();

            // Based on recent elements
            if (projectContext.RecentElements.Any())
            {
                var recentTypes = projectContext.RecentElements
                    .Select(e => e.ElementType)
                    .Distinct()
                    .Take(3);

                foreach (var type in recentTypes)
                {
                    suggestions.Add($"Continue working with {type} elements");
                }
            }

            // Based on active constraints
            foreach (var constraint in projectContext.ActiveConstraints.Take(2))
            {
                suggestions.Add($"Consider: {constraint.Description}");
            }

            // Based on time of day patterns
            var hour = DateTime.Now.Hour;
            if (hour >= 9 && hour <= 11)
                suggestions.Add("Morning: Good time for complex modeling tasks");
            else if (hour >= 14 && hour <= 16)
                suggestions.Add("Afternoon: Consider review and coordination tasks");

            return suggestions;
        }

        #endregion
    }

    #region Supporting Types

    public class MemoryConfiguration
    {
        public int MaxHistorySize { get; set; } = 10000;
        public int ShortTermMemorySize { get; set; } = 50;
        public bool EnableLearning { get; set; } = true;
        public double LearningRate { get; set; } = 0.1;
        public string StoragePath { get; set; }
    }

    public class UserProfile
    {
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastInteraction { get; set; }
        public int InteractionCount { get; set; }
        public Dictionary<string, SkillLevel> SkillLevels { get; set; }
        public Dictionary<string, Dictionary<string, double>> Preferences { get; set; }
        public Dictionary<string, int> FeatureUsageCount { get; set; }
        public Dictionary<string, object> CustomSettings { get; set; }
    }

    public class ProjectContext
    {
        public string ProjectId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<DesignDecision> DesignDecisions { get; set; }
        public List<DesignConstraint> ActiveConstraints { get; set; }
        public Queue<ElementReference> RecentElements { get; set; }
        public SpatialContext SpatialContext { get; set; }
    }

    public class SessionContext
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public List<InteractionRecord> Interactions { get; set; } = new();
        public Dictionary<string, int> CommandFrequency { get; set; } = new();
        public Dictionary<string, int> CommandSequences { get; set; } = new();
        public string LastCommand { get; set; }
    }

    public class PreferenceCluster
    {
        public string ClusterId { get; set; }
        public string[] Dimensions { get; set; }
        public Dictionary<string, double> DefaultValues { get; set; }
    }

    public class BehaviorObservation
    {
        public Dictionary<string, double> SkillIndicators { get; set; }
        public List<PreferenceSignal> PreferenceSignals { get; set; }
        public string FeatureUsed { get; set; }
    }

    public class PreferenceSignal
    {
        public string ClusterId { get; set; }
        public string Dimension { get; set; }
        public double Value { get; set; }
    }

    public class InteractionRecord
    {
        public string SessionId { get; set; }
        public DateTime Timestamp { get; set; }
        public InteractionType Type { get; set; }
        public string Content { get; set; }
        public string CommandType { get; set; }
        public string Context { get; set; }
        public List<string> MentionedEntities { get; set; }
        public InteractionOutcome Outcome { get; set; }
    }

    public class DesignDecision
    {
        public string DecisionId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public List<string> Topics { get; set; }
        public List<string> AffectedElementTypes { get; set; }
        public List<DesignConstraint> ImpliedConstraints { get; set; }
        public double Importance { get; set; }
    }

    public class DesignConstraint
    {
        public string ConstraintId { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool IsActive { get; set; }
    }

    public class ElementReference
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public Point3D Location { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class SpatialContext
    {
        public Point3D LastEditLocation { get; set; }
        public List<Point3D> RecentEditLocations { get; set; } = new();
        public string CurrentLevel { get; set; }
        public string CurrentView { get; set; }
    }

    public class ShortTermContext
    {
        public List<string> RecentCommands { get; set; }
        public List<string> RecentEntities { get; set; }
        public string CurrentTopic { get; set; }
        public List<string> RecentErrors { get; set; }
        public TimeSpan SessionDuration { get; set; }
        public int InteractionCount { get; set; }
    }

    public class ActionPrediction
    {
        public string ActionType { get; set; }
        public double Confidence { get; set; }
        public string Rationale { get; set; }
    }

    public class FeedbackRecord
    {
        public string SuggestionType { get; set; }
        public bool Accepted { get; set; }
        public List<PreferenceSignal> PreferenceSignals { get; set; }
        public string Comment { get; set; }
    }

    public class PersonalizedRecommendations
    {
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public CommunicationStyle CommunicationStyle { get; set; }
        public WorkflowAdaptations WorkflowAdaptations { get; set; }
        public List<string> SuggestedFeatures { get; set; }
        public List<string> ContextualSuggestions { get; set; }
    }

    public class CommunicationStyle
    {
        public double Verbosity { get; set; }
        public double TechnicalLevel { get; set; }
        public double Proactivity { get; set; }
    }

    public class WorkflowAdaptations
    {
        public double GuidanceLevel { get; set; }
        public bool BatchOperations { get; set; }
        public double AutomationLevel { get; set; }
    }

    public enum SkillLevel
    {
        Novice,
        Beginner,
        Intermediate,
        Advanced,
        Expert
    }

    public enum InteractionType
    {
        Command,
        Query,
        Feedback,
        Error,
        Navigation,
        Selection
    }

    public enum InteractionOutcome
    {
        Success,
        Partial,
        Failed,
        Rejected,
        Unknown
    }

    #endregion
}
