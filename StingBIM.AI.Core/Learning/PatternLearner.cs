// StingBIM.AI.Core.Learning.PatternLearner
// Learns patterns from user behavior and feedback
// Master Proposal Reference: Part 2.2 Strategy 1 - Compound Learning Loops (Loop 2: Session)

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Core.Learning
{
    /// <summary>
    /// Learns patterns from user sessions and feedback.
    /// Implements Loop 2 (Session) of the Triple Learning Loop.
    /// </summary>
    public class PatternLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, LearnedPattern> _patterns;
        private readonly object _lock = new object();

        public PatternLearner()
        {
            _patterns = new Dictionary<string, LearnedPattern>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Analyzes a session to extract patterns.
        /// </summary>
        public IEnumerable<LearnedPattern> AnalyzeSession(ProjectSession session)
        {
            Logger.Info($"Analyzing session with {session.Actions.Count} actions");

            var extractedPatterns = new List<LearnedPattern>();

            // Extract sequential patterns
            extractedPatterns.AddRange(ExtractSequentialPatterns(session.Actions));

            // Extract preference patterns
            extractedPatterns.AddRange(ExtractPreferencePatterns(session.Actions));

            // Extract correction patterns
            extractedPatterns.AddRange(ExtractCorrectionPatterns(session.Actions));

            // Update stored patterns
            foreach (var pattern in extractedPatterns)
            {
                UpdatePattern(pattern);
            }

            Logger.Info($"Extracted {extractedPatterns.Count} patterns from session");
            return extractedPatterns;
        }

        /// <summary>
        /// Extracts sequential action patterns (A followed by B).
        /// </summary>
        private IEnumerable<LearnedPattern> ExtractSequentialPatterns(IList<UserAction> actions)
        {
            var patterns = new List<LearnedPattern>();

            for (int i = 0; i < actions.Count - 1; i++)
            {
                var current = actions[i];
                var next = actions[i + 1];

                // Check if these actions commonly occur together
                var patternKey = $"SEQ:{current.ActionType}→{next.ActionType}";
                patterns.Add(new LearnedPattern
                {
                    PatternType = PatternType.Sequential,
                    Key = patternKey,
                    Description = $"After {current.ActionType}, user often does {next.ActionType}",
                    Confidence = 0.5f,
                    Occurrences = 1,
                    Context = new Dictionary<string, object>
                    {
                        { "TriggerAction", current.ActionType },
                        { "FollowingAction", next.ActionType },
                        { "TimeDelta", (next.Timestamp - current.Timestamp).TotalSeconds }
                    }
                });
            }

            return patterns;
        }

        /// <summary>
        /// Extracts user preference patterns.
        /// </summary>
        private IEnumerable<LearnedPattern> ExtractPreferencePatterns(IList<UserAction> actions)
        {
            var patterns = new List<LearnedPattern>();

            // Group actions by type and analyze parameters
            var groupedActions = actions.GroupBy(a => a.ActionType);

            foreach (var group in groupedActions)
            {
                var actionType = group.Key;
                var groupActions = group.ToList();

                // Find common parameter values
                var parameterGroups = groupActions
                    .SelectMany(a => a.Parameters)
                    .GroupBy(p => p.Key)
                    .ToList();

                foreach (var paramGroup in parameterGroups)
                {
                    var mostCommonValue = paramGroup
                        .GroupBy(p => p.Value?.ToString())
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();

                    if (mostCommonValue != null && mostCommonValue.Count() >= 2)
                    {
                        var patternKey = $"PREF:{actionType}:{paramGroup.Key}={mostCommonValue.Key}";
                        patterns.Add(new LearnedPattern
                        {
                            PatternType = PatternType.Preference,
                            Key = patternKey,
                            Description = $"User prefers {paramGroup.Key}={mostCommonValue.Key} for {actionType}",
                            Confidence = mostCommonValue.Count() / (float)paramGroup.Count(),
                            Occurrences = mostCommonValue.Count(),
                            Context = new Dictionary<string, object>
                            {
                                { "ActionType", actionType },
                                { "Parameter", paramGroup.Key },
                                { "PreferredValue", mostCommonValue.Key }
                            }
                        });
                    }
                }
            }

            return patterns;
        }

        /// <summary>
        /// Extracts patterns from user corrections.
        /// </summary>
        private IEnumerable<LearnedPattern> ExtractCorrectionPatterns(IList<UserAction> actions)
        {
            var patterns = new List<LearnedPattern>();

            // Find undo actions and their preceding actions
            var undoActions = actions
                .Select((action, index) => new { Action = action, Index = index })
                .Where(x => x.Action.ActionType.Contains("Undo", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var undoAction in undoActions)
            {
                if (undoAction.Index > 0)
                {
                    var problematicAction = actions[undoAction.Index - 1];
                    var patternKey = $"CORR:{problematicAction.ActionType}";

                    patterns.Add(new LearnedPattern
                    {
                        PatternType = PatternType.Correction,
                        Key = patternKey,
                        Description = $"User often undoes {problematicAction.ActionType}",
                        Confidence = 0.6f,
                        Occurrences = 1,
                        Context = new Dictionary<string, object>
                        {
                            { "ProblematicAction", problematicAction.ActionType },
                            { "Parameters", problematicAction.Parameters }
                        }
                    });
                }
            }

            return patterns;
        }

        /// <summary>
        /// Updates a stored pattern with new occurrence data.
        /// </summary>
        private void UpdatePattern(LearnedPattern newPattern)
        {
            lock (_lock)
            {
                if (_patterns.TryGetValue(newPattern.Key, out var existing))
                {
                    // Update existing pattern
                    existing.Occurrences += newPattern.Occurrences;
                    existing.LastSeen = DateTime.Now;

                    // Adjust confidence based on occurrences
                    existing.Confidence = Math.Min(0.95f, existing.Confidence + 0.05f);
                }
                else
                {
                    // Add new pattern
                    newPattern.FirstSeen = DateTime.Now;
                    newPattern.LastSeen = DateTime.Now;
                    _patterns[newPattern.Key] = newPattern;
                }
            }
        }

        /// <summary>
        /// Gets patterns for a specific action type.
        /// </summary>
        public IEnumerable<LearnedPattern> GetPatternsForAction(string actionType)
        {
            lock (_lock)
            {
                return _patterns.Values
                    .Where(p => p.Key.Contains(actionType, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.Confidence)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all learned patterns.
        /// </summary>
        public IEnumerable<LearnedPattern> GetAllPatterns()
        {
            lock (_lock)
            {
                return _patterns.Values.ToList();
            }
        }

        /// <summary>
        /// Predicts the next likely action based on patterns.
        /// </summary>
        public string PredictNextAction(string currentAction)
        {
            lock (_lock)
            {
                var sequentialPatterns = _patterns.Values
                    .Where(p => p.PatternType == PatternType.Sequential)
                    .Where(p => p.Context.GetValueOrDefault("TriggerAction")?.ToString() == currentAction)
                    .OrderByDescending(p => p.Confidence * p.Occurrences)
                    .FirstOrDefault();

                return sequentialPatterns?.Context.GetValueOrDefault("FollowingAction")?.ToString();
            }
        }
    }

    /// <summary>
    /// Represents a learned pattern from user behavior.
    /// </summary>
    public class LearnedPattern
    {
        public string Key { get; set; }
        public PatternType PatternType { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public int Occurrences { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Types of learned patterns.
    /// </summary>
    public enum PatternType
    {
        Sequential,   // A followed by B
        Preference,   // User prefers X value
        Correction,   // User often corrects Y
        Temporal,     // Time-based pattern
        Contextual    // Context-dependent pattern
    }

    /// <summary>
    /// Represents a user session with actions.
    /// </summary>
    public class ProjectSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public string ProjectId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public IList<UserAction> Actions { get; set; } = new List<UserAction>();
    }

    /// <summary>
    /// Represents a user action in a session.
    /// </summary>
    public class UserAction
    {
        public string ActionId { get; set; }
        public string ActionType { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public bool WasUndone { get; set; }
        public bool WasModified { get; set; }
    }
}
