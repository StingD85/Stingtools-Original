// StingBIM.AI.Intelligence.Reasoning.DesignIntentInferencer
// Infers design intent from user actions and context
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Enhancement

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Intelligence.Reasoning
{
    #region Intent Recognition

    /// <summary>
    /// Infers the underlying design intent from user actions.
    /// Goes beyond "what" to understand "why".
    /// </summary>
    public class DesignIntentInferencer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, List<IntentPattern>> _intentPatterns;
        private readonly Dictionary<string, List<string>> _intentHierarchy;
        private readonly List<DesignAction> _recentActions;
        private readonly int _maxHistorySize;

        public DesignIntentInferencer(int maxHistorySize = 50)
        {
            _intentPatterns = new Dictionary<string, List<IntentPattern>>();
            _intentHierarchy = new Dictionary<string, List<string>>();
            _recentActions = new List<DesignAction>();
            _maxHistorySize = maxHistorySize;

            InitializeIntentPatterns();
            InitializeIntentHierarchy();
        }

        /// <summary>
        /// Records an action and infers intent.
        /// </summary>
        public InferredIntent InferIntent(DesignAction action, DesignContext context)
        {
            _recentActions.Add(action);
            while (_recentActions.Count > _maxHistorySize)
            {
                _recentActions.RemoveAt(0);
            }

            var inference = new InferredIntent
            {
                Action = action,
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            // Layer 1: Direct action intent
            var directIntent = InferDirectIntent(action);
            inference.PrimaryIntent = directIntent;

            // Layer 2: Contextual intent (why in this context)
            var contextualIntents = InferContextualIntent(action, context);
            inference.ContextualIntents = contextualIntents;

            // Layer 3: Strategic intent (bigger picture goals)
            var strategicIntent = InferStrategicIntent(action, context);
            inference.StrategicIntent = strategicIntent;

            // Layer 4: Check for intent patterns in action sequence
            var patternIntents = MatchIntentPatterns();
            inference.PatternBasedIntents = patternIntents;

            // Calculate overall confidence
            inference.Confidence = CalculateConfidence(inference);

            // Generate explanation
            inference.Explanation = GenerateIntentExplanation(inference);

            Logger.Debug($"Inferred intent: {inference.PrimaryIntent.Name} (confidence: {inference.Confidence:P0})");

            return inference;
        }

        /// <summary>
        /// Gets likely next actions based on inferred intent.
        /// </summary>
        public List<PredictedAction> PredictNextActions(InferredIntent currentIntent, int maxPredictions = 5)
        {
            var predictions = new List<PredictedAction>();

            // Based on intent, predict what user likely wants to do next
            var intentName = currentIntent.PrimaryIntent?.Name ?? "";

            switch (intentName)
            {
                case "CreateRoom":
                    predictions.Add(new PredictedAction { Action = "AddWalls", Probability = 0.9f, Rationale = "Rooms need enclosing walls" });
                    predictions.Add(new PredictedAction { Action = "AddDoor", Probability = 0.8f, Rationale = "Rooms need entry points" });
                    predictions.Add(new PredictedAction { Action = "AddWindow", Probability = 0.6f, Rationale = "Habitable rooms need natural light" });
                    break;

                case "CreateCirculation":
                    predictions.Add(new PredictedAction { Action = "ConnectRooms", Probability = 0.85f, Rationale = "Circulation connects spaces" });
                    predictions.Add(new PredictedAction { Action = "AddDoors", Probability = 0.7f, Rationale = "Access points needed" });
                    break;

                case "AddMEPSystem":
                    predictions.Add(new PredictedAction { Action = "RouteToEquipment", Probability = 0.8f, Rationale = "Systems need equipment connections" });
                    predictions.Add(new PredictedAction { Action = "AddTerminals", Probability = 0.7f, Rationale = "Distribution needs endpoints" });
                    break;

                case "OptimizeEnergy":
                    predictions.Add(new PredictedAction { Action = "AddInsulation", Probability = 0.75f, Rationale = "Reduce thermal losses" });
                    predictions.Add(new PredictedAction { Action = "OptimizeGlazing", Probability = 0.7f, Rationale = "Balance light and heat" });
                    break;
            }

            // Add pattern-based predictions
            if (currentIntent.PatternBasedIntents.Any())
            {
                var patternIntent = currentIntent.PatternBasedIntents.First();
                if (_intentPatterns.TryGetValue(patternIntent.Name, out var patterns))
                {
                    var nextInPattern = patterns
                        .Where(p => p.Sequence.Length > _recentActions.Count)
                        .Select(p => p.Sequence.Skip(_recentActions.Count).FirstOrDefault())
                        .Where(s => s != null)
                        .FirstOrDefault();

                    if (nextInPattern != null)
                    {
                        predictions.Add(new PredictedAction
                        {
                            Action = nextInPattern,
                            Probability = 0.8f,
                            Rationale = $"Follows {patternIntent.Name} pattern"
                        });
                    }
                }
            }

            return predictions.OrderByDescending(p => p.Probability).Take(maxPredictions).ToList();
        }

        /// <summary>
        /// Validates if an action aligns with stated or inferred intent.
        /// </summary>
        public IntentAlignment CheckIntentAlignment(DesignAction proposedAction, InferredIntent currentIntent)
        {
            var alignment = new IntentAlignment
            {
                ProposedAction = proposedAction,
                CurrentIntent = currentIntent
            };

            // Check if action supports the intent
            var supportLevel = CalculateSupportLevel(proposedAction, currentIntent);
            alignment.AlignmentScore = supportLevel;

            if (supportLevel > 0.7f)
            {
                alignment.Status = AlignmentStatus.StronglyAligned;
                alignment.Message = $"Action strongly supports {currentIntent.PrimaryIntent?.Name} intent";
            }
            else if (supportLevel > 0.4f)
            {
                alignment.Status = AlignmentStatus.ModeratelyAligned;
                alignment.Message = $"Action moderately supports intent";
            }
            else if (supportLevel > 0.1f)
            {
                alignment.Status = AlignmentStatus.WeaklyAligned;
                alignment.Message = $"Action has weak connection to current intent";
            }
            else
            {
                alignment.Status = AlignmentStatus.Misaligned;
                alignment.Message = $"Action may not support {currentIntent.PrimaryIntent?.Name} - consider if this is intended";
                alignment.Suggestions = GenerateAlignmentSuggestions(proposedAction, currentIntent);
            }

            return alignment;
        }

        private Intent InferDirectIntent(DesignAction action)
        {
            // Map action to direct intent
            var intentMap = new Dictionary<string, (string Name, string Description)>
            {
                ["PlaceRoom"] = ("CreateRoom", "User is defining a functional space"),
                ["PlaceWall"] = ("DefineEnclosure", "User is creating spatial boundaries"),
                ["PlaceDoor"] = ("CreateAccess", "User is providing entry/exit points"),
                ["PlaceWindow"] = ("ProvideLight", "User is adding natural light and ventilation"),
                ["PlaceDuct"] = ("DistributeAir", "User is routing air distribution"),
                ["PlacePipe"] = ("RoutePlumbing", "User is routing water/waste"),
                ["PlaceFixture"] = ("AddFunction", "User is adding functional equipment"),
                ["DeleteElement"] = ("RemoveElement", "User is removing an element"),
                ["MoveElement"] = ("RelocateElement", "User is repositioning an element"),
                ["ModifyParameter"] = ("RefineDesign", "User is adjusting design parameters")
            };

            if (intentMap.TryGetValue(action.ActionType, out var intentInfo))
            {
                return new Intent
                {
                    Name = intentInfo.Name,
                    Description = intentInfo.Description,
                    Confidence = 0.9f,
                    Source = IntentSource.DirectAction
                };
            }

            return new Intent
            {
                Name = "UnknownIntent",
                Description = "Action intent unclear",
                Confidence = 0.3f,
                Source = IntentSource.DirectAction
            };
        }

        private List<Intent> InferContextualIntent(DesignAction action, DesignContext context)
        {
            var intents = new List<Intent>();

            // Room type specific intents
            if (!string.IsNullOrEmpty(context?.RoomType))
            {
                var roomIntents = GetRoomTypeIntents(action, context.RoomType);
                intents.AddRange(roomIntents);
            }

            // Spatial context intents
            if (context?.NearbyElements?.Any() == true)
            {
                var spatialIntents = GetSpatialContextIntents(action, context.NearbyElements);
                intents.AddRange(spatialIntents);
            }

            // Phase-specific intents
            if (!string.IsNullOrEmpty(context?.ProjectPhase))
            {
                var phaseIntents = GetPhaseIntents(action, context.ProjectPhase);
                intents.AddRange(phaseIntents);
            }

            return intents;
        }

        private Intent InferStrategicIntent(DesignAction action, DesignContext context)
        {
            // Look at sequence of recent actions to infer higher-level goal
            var recentCategories = _recentActions.TakeLast(10)
                .Select(a => a.ElementCategory)
                .ToList();

            // Detect strategic patterns
            if (recentCategories.Count(c => c == "Rooms") > 3)
            {
                return new Intent
                {
                    Name = "SpacePlanning",
                    Description = "User is in space planning phase, defining room layout",
                    Confidence = 0.7f,
                    Source = IntentSource.PatternAnalysis
                };
            }

            if (recentCategories.Count(c => c == "Walls" || c == "Doors" || c == "Windows") > 5)
            {
                return new Intent
                {
                    Name = "EnvelopeDesign",
                    Description = "User is defining building envelope and openings",
                    Confidence = 0.7f,
                    Source = IntentSource.PatternAnalysis
                };
            }

            if (recentCategories.Count(c => c == "Ducts" || c == "Pipes" || c == "Equipment") > 3)
            {
                return new Intent
                {
                    Name = "MEPCoordination",
                    Description = "User is routing MEP systems",
                    Confidence = 0.7f,
                    Source = IntentSource.PatternAnalysis
                };
            }

            return new Intent
            {
                Name = "GeneralDesign",
                Description = "User is in general design mode",
                Confidence = 0.5f,
                Source = IntentSource.Default
            };
        }

        private List<Intent> MatchIntentPatterns()
        {
            var matches = new List<Intent>();
            var recentActionTypes = _recentActions.TakeLast(5)
                .Select(a => a.ActionType)
                .ToArray();

            foreach (var patternGroup in _intentPatterns)
            {
                foreach (var pattern in patternGroup.Value)
                {
                    if (MatchesPattern(recentActionTypes, pattern.Sequence))
                    {
                        matches.Add(new Intent
                        {
                            Name = patternGroup.Key,
                            Description = pattern.Description,
                            Confidence = pattern.Confidence,
                            Source = IntentSource.PatternAnalysis
                        });
                    }
                }
            }

            return matches;
        }

        private bool MatchesPattern(string[] recent, string[] pattern)
        {
            if (recent.Length < pattern.Length) return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (recent[recent.Length - pattern.Length + i] != pattern[i])
                    return false;
            }
            return true;
        }

        private List<Intent> GetRoomTypeIntents(DesignAction action, string roomType)
        {
            var intents = new List<Intent>();

            var roomIntentMap = new Dictionary<string, List<(string ActionType, string Intent, string Description)>>
            {
                ["Bathroom"] = new List<(string, string, string)>
                {
                    ("PlaceFixture", "AddPlumbing", "Adding plumbing fixtures for bathroom function"),
                    ("PlaceWindow", "ProvidePrivacyVentilation", "Adding ventilation while maintaining privacy")
                },
                ["Kitchen"] = new List<(string, string, string)>
                {
                    ("PlaceFixture", "AddKitchenFunction", "Adding cooking/prep functionality"),
                    ("PlaceWindow", "ProvideWorkingLight", "Adding task lighting for food prep")
                },
                ["Bedroom"] = new List<(string, string, string)>
                {
                    ("PlaceWindow", "ProvideRestfulEnvironment", "Adding light while considering sleep quality"),
                    ("PlaceDoor", "EnsurePrivacy", "Providing private access to sleeping area")
                }
            };

            if (roomIntentMap.TryGetValue(roomType, out var mappings))
            {
                var match = mappings.FirstOrDefault(m => m.ActionType == action.ActionType);
                if (match != default)
                {
                    intents.Add(new Intent
                    {
                        Name = match.Intent,
                        Description = match.Description,
                        Confidence = 0.75f,
                        Source = IntentSource.ContextualAnalysis
                    });
                }
            }

            return intents;
        }

        private List<Intent> GetSpatialContextIntents(DesignAction action, List<string> nearbyElements)
        {
            var intents = new List<Intent>();

            // If placing door near corridor, intent is likely circulation
            if (action.ActionType == "PlaceDoor" && nearbyElements.Contains("Corridor"))
            {
                intents.Add(new Intent
                {
                    Name = "ImproveCirculation",
                    Description = "Creating connection to circulation path",
                    Confidence = 0.8f,
                    Source = IntentSource.ContextualAnalysis
                });
            }

            // If placing window on south wall, intent might be solar gain
            if (action.ActionType == "PlaceWindow" && action.Parameters?.ContainsKey("Orientation") == true)
            {
                var orientation = action.Parameters["Orientation"]?.ToString();
                if (orientation == "South")
                {
                    intents.Add(new Intent
                    {
                        Name = "MaximizeSolarGain",
                        Description = "Optimizing passive solar heating",
                        Confidence = 0.7f,
                        Source = IntentSource.ContextualAnalysis
                    });
                }
            }

            return intents;
        }

        private List<Intent> GetPhaseIntents(DesignAction action, string phase)
        {
            var intents = new List<Intent>();

            switch (phase.ToLower())
            {
                case "concept":
                    intents.Add(new Intent
                    {
                        Name = "ExploreOptions",
                        Description = "User is exploring design options in concept phase",
                        Confidence = 0.6f,
                        Source = IntentSource.ContextualAnalysis
                    });
                    break;

                case "developed":
                    intents.Add(new Intent
                    {
                        Name = "RefineDesign",
                        Description = "User is refining design decisions",
                        Confidence = 0.6f,
                        Source = IntentSource.ContextualAnalysis
                    });
                    break;

                case "technical":
                    intents.Add(new Intent
                    {
                        Name = "DetailDesign",
                        Description = "User is adding technical detail",
                        Confidence = 0.6f,
                        Source = IntentSource.ContextualAnalysis
                    });
                    break;
            }

            return intents;
        }

        private float CalculateConfidence(InferredIntent inference)
        {
            var confidences = new List<float>();

            if (inference.PrimaryIntent != null)
                confidences.Add(inference.PrimaryIntent.Confidence);

            if (inference.ContextualIntents?.Any() == true)
                confidences.Add(inference.ContextualIntents.Average(i => i.Confidence));

            if (inference.StrategicIntent != null)
                confidences.Add(inference.StrategicIntent.Confidence * 0.8f);

            if (inference.PatternBasedIntents?.Any() == true)
                confidences.Add(inference.PatternBasedIntents.Max(i => i.Confidence));

            return confidences.Any() ? confidences.Average() : 0.5f;
        }

        private string GenerateIntentExplanation(InferredIntent inference)
        {
            var parts = new List<string>();

            if (inference.PrimaryIntent != null)
            {
                parts.Add($"Primary intent: {inference.PrimaryIntent.Description}");
            }

            if (inference.ContextualIntents?.Any() == true)
            {
                var contextIntent = inference.ContextualIntents.First();
                parts.Add($"In this context: {contextIntent.Description}");
            }

            if (inference.StrategicIntent != null && inference.StrategicIntent.Name != "GeneralDesign")
            {
                parts.Add($"Supports broader goal: {inference.StrategicIntent.Description}");
            }

            return string.Join(". ", parts);
        }

        private float CalculateSupportLevel(DesignAction action, InferredIntent intent)
        {
            // Check if action supports the inferred intent
            if (intent.PrimaryIntent == null) return 0.5f;

            var supportMatrix = new Dictionary<string, List<string>>
            {
                ["CreateRoom"] = new List<string> { "PlaceWall", "PlaceDoor", "PlaceWindow", "PlaceFloor" },
                ["DefineEnclosure"] = new List<string> { "PlaceWall", "PlaceDoor", "PlaceWindow" },
                ["CreateAccess"] = new List<string> { "PlaceDoor", "PlaceStair", "PlaceRamp" },
                ["AddMEPSystem"] = new List<string> { "PlaceDuct", "PlacePipe", "PlaceEquipment", "PlaceFixture" },
                ["OptimizeEnergy"] = new List<string> { "ModifyInsulation", "ModifyGlazing", "PlaceShading" }
            };

            if (supportMatrix.TryGetValue(intent.PrimaryIntent.Name, out var supportingActions))
            {
                return supportingActions.Contains(action.ActionType) ? 0.9f : 0.3f;
            }

            return 0.5f;
        }

        private List<string> GenerateAlignmentSuggestions(DesignAction action, InferredIntent intent)
        {
            var suggestions = new List<string>();

            suggestions.Add($"Consider if {action.ActionType} aligns with your goal of {intent.PrimaryIntent?.Name}");
            suggestions.Add($"Alternative actions that might better serve your intent");

            return suggestions;
        }

        private void InitializeIntentPatterns()
        {
            _intentPatterns["RoomLayout"] = new List<IntentPattern>
            {
                new IntentPattern
                {
                    Sequence = new[] { "PlaceRoom", "PlaceWall", "PlaceWall", "PlaceDoor" },
                    Description = "Creating enclosed room with access",
                    Confidence = 0.85f
                }
            };

            _intentPatterns["MEPRouting"] = new List<IntentPattern>
            {
                new IntentPattern
                {
                    Sequence = new[] { "PlaceEquipment", "PlaceDuct", "PlaceDuct", "PlaceDiffuser" },
                    Description = "Routing HVAC from equipment to terminals",
                    Confidence = 0.8f
                }
            };

            _intentPatterns["PlumbingLayout"] = new List<IntentPattern>
            {
                new IntentPattern
                {
                    Sequence = new[] { "PlaceFixture", "PlacePipe", "PlacePipe" },
                    Description = "Connecting plumbing fixtures",
                    Confidence = 0.8f
                }
            };
        }

        private void InitializeIntentHierarchy()
        {
            _intentHierarchy["SpacePlanning"] = new List<string>
            {
                "CreateRoom", "DefineCirculation", "EstablishZones"
            };

            _intentHierarchy["EnvelopeDesign"] = new List<string>
            {
                "DefineEnclosure", "CreateAccess", "ProvideLight"
            };

            _intentHierarchy["MEPDesign"] = new List<string>
            {
                "AddMEPSystem", "DistributeAir", "RoutePlumbing", "WireElectrical"
            };
        }
    }

    #endregion

    #region Intent Types

    /// <summary>
    /// An inferred design intent.
    /// </summary>
    public class InferredIntent
    {
        public DesignAction Action { get; set; }
        public DesignContext Context { get; set; }
        public DateTime Timestamp { get; set; }

        public Intent PrimaryIntent { get; set; }
        public List<Intent> ContextualIntents { get; set; }
        public Intent StrategicIntent { get; set; }
        public List<Intent> PatternBasedIntents { get; set; }

        public float Confidence { get; set; }
        public string Explanation { get; set; }
    }

    /// <summary>
    /// A single intent.
    /// </summary>
    public class Intent
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public IntentSource Source { get; set; }
    }

    public enum IntentSource
    {
        DirectAction,       // Directly from the action itself
        ContextualAnalysis, // From surrounding context
        PatternAnalysis,    // From action sequence patterns
        UserStated,         // User explicitly stated intent
        Default             // Default assumption
    }

    /// <summary>
    /// A pattern that indicates a particular intent.
    /// </summary>
    public class IntentPattern
    {
        public string[] Sequence { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Alignment between action and intent.
    /// </summary>
    public class IntentAlignment
    {
        public DesignAction ProposedAction { get; set; }
        public InferredIntent CurrentIntent { get; set; }
        public AlignmentStatus Status { get; set; }
        public float AlignmentScore { get; set; }
        public string Message { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public enum AlignmentStatus
    {
        StronglyAligned,
        ModeratelyAligned,
        WeaklyAligned,
        Misaligned
    }

    /// <summary>
    /// A predicted next action.
    /// </summary>
    public class PredictedAction
    {
        public string Action { get; set; }
        public float Probability { get; set; }
        public string Rationale { get; set; }
    }

    /// <summary>
    /// A design action.
    /// </summary>
    public class DesignAction
    {
        public string ActionId { get; set; }
        public string ActionType { get; set; }
        public string ElementCategory { get; set; }
        public string ElementId { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Design context for intent inference.
    /// </summary>
    public class DesignContext
    {
        public string RoomType { get; set; }
        public string BuildingType { get; set; }
        public string ProjectPhase { get; set; }
        public List<string> NearbyElements { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    #endregion
}
