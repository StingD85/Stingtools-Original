// StingBIM.AI.Reasoning.Predictive.PredictiveEngine
// Predictive completion engine for anticipating user actions
// Master Proposal Reference: Part 2.2 Strategy 4 - Predictive Modeling

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Reasoning.Predictive
{
    /// <summary>
    /// Predictive engine that anticipates user actions and provides intelligent completions.
    /// Learns from user patterns to improve predictions over time.
    /// </summary>
    public class PredictiveEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ActionSequenceModel _sequenceModel;
        private readonly ParameterPredictor _parameterPredictor;
        private readonly ContextAnalyzer _contextAnalyzer;
        private readonly PatternDatabase _patternDatabase;

        // Configuration
        public int MaxPredictions { get; set; } = 5;
        public double MinConfidence { get; set; } = 0.3;
        public int SequenceWindowSize { get; set; } = 10;

        public PredictiveEngine()
        {
            _sequenceModel = new ActionSequenceModel();
            _parameterPredictor = new ParameterPredictor();
            _contextAnalyzer = new ContextAnalyzer();
            _patternDatabase = new PatternDatabase();

            InitializeDefaultPatterns();
        }

        #region Public API

        /// <summary>
        /// Predicts the next likely actions based on current context.
        /// </summary>
        public async Task<IEnumerable<PredictedAction>> PredictNextActionsAsync(PredictionContext context)
        {
            return await Task.Run(() =>
            {
                Logger.Debug($"Predicting next actions for context: {context.CurrentAction}");

                var predictions = new List<PredictedAction>();

                // Get sequence-based predictions
                var sequencePredictions = _sequenceModel.PredictNext(
                    context.RecentActions,
                    MaxPredictions);

                foreach (var seqPred in sequencePredictions)
                {
                    var prediction = new PredictedAction
                    {
                        ActionType = seqPred.ActionType,
                        Confidence = seqPred.Confidence,
                        Source = PredictionSource.SequenceModel
                    };

                    // Predict parameters for this action
                    prediction.Parameters = _parameterPredictor.PredictParameters(
                        seqPred.ActionType,
                        context);

                    predictions.Add(prediction);
                }

                // Add context-based predictions
                var contextPredictions = _contextAnalyzer.AnalyzeAndPredict(context);
                foreach (var ctxPred in contextPredictions)
                {
                    if (!predictions.Any(p => p.ActionType == ctxPred.ActionType))
                    {
                        predictions.Add(ctxPred);
                    }
                    else
                    {
                        // Boost confidence if both models predict the same action
                        var existing = predictions.First(p => p.ActionType == ctxPred.ActionType);
                        existing.Confidence = Math.Min(1.0, existing.Confidence + ctxPred.Confidence * 0.3);
                    }
                }

                // Add pattern-based predictions
                var patternPredictions = _patternDatabase.MatchPatterns(context);
                foreach (var patternPred in patternPredictions)
                {
                    if (!predictions.Any(p => p.ActionType == patternPred.ActionType))
                    {
                        predictions.Add(patternPred);
                    }
                }

                // Filter and rank
                var result = predictions
                    .Where(p => p.Confidence >= MinConfidence)
                    .OrderByDescending(p => p.Confidence)
                    .Take(MaxPredictions)
                    .ToList();

                Logger.Debug($"Generated {result.Count} predictions");
                return result;
            });
        }

        /// <summary>
        /// Predicts command completion for partial input.
        /// </summary>
        public IEnumerable<CommandCompletion> PredictCompletion(string partialInput, PredictionContext context)
        {
            var completions = new List<CommandCompletion>();

            // Tokenize partial input
            var tokens = TokenizeInput(partialInput);

            // Get completions based on input stage
            if (tokens.Length == 0 || (tokens.Length == 1 && !partialInput.EndsWith(" ")))
            {
                // Completing action/verb
                completions.AddRange(GetActionCompletions(tokens.FirstOrDefault() ?? "", context));
            }
            else if (tokens.Length >= 1)
            {
                // Completing parameters
                completions.AddRange(GetParameterCompletions(tokens, context));
            }

            return completions.OrderByDescending(c => c.Score).Take(10);
        }

        /// <summary>
        /// Records a user action for learning.
        /// </summary>
        public void RecordAction(UserAction action, PredictionContext context)
        {
            _sequenceModel.AddAction(action);
            _parameterPredictor.LearnParameters(action, context);
            _patternDatabase.UpdatePatterns(action, context);

            Logger.Debug($"Recorded action: {action.ActionType}");
        }

        /// <summary>
        /// Gets proactive suggestions based on current state.
        /// </summary>
        public IEnumerable<ProactiveSuggestion> GetProactiveSuggestions(PredictionContext context)
        {
            var suggestions = new List<ProactiveSuggestion>();

            // Analyze what the user is working on
            var workContext = _contextAnalyzer.AnalyzeWorkContext(context);

            // Suggest based on incomplete patterns
            var incompletePatterns = _patternDatabase.FindIncompletePatterns(context);
            foreach (var pattern in incompletePatterns)
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Title = pattern.SuggestionTitle,
                    Description = pattern.SuggestionDescription,
                    Actions = pattern.RemainingActions,
                    Confidence = pattern.CompletionConfidence,
                    Category = SuggestionCategory.PatternCompletion
                });
            }

            // Suggest based on common follow-ups
            var lastAction = context.RecentActions.LastOrDefault();
            if (lastAction != null)
            {
                var followUps = GetCommonFollowUps(lastAction.ActionType);
                foreach (var followUp in followUps)
                {
                    suggestions.Add(new ProactiveSuggestion
                    {
                        Title = followUp.Title,
                        Description = followUp.Description,
                        Actions = new List<string> { followUp.ActionType },
                        Confidence = followUp.Frequency,
                        Category = SuggestionCategory.CommonFollowUp
                    });
                }
            }

            // Suggest based on context (selected elements, current view, etc.)
            suggestions.AddRange(GetContextBasedSuggestions(context));

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .Take(5);
        }

        #endregion

        #region Private Methods

        private void InitializeDefaultPatterns()
        {
            // Initialize common design patterns
            _patternDatabase.AddPattern(new DesignPattern
            {
                Name = "CreateRoomSequence",
                Description = "Standard room creation workflow",
                ActionSequence = new[] { "CreateWall", "CreateWall", "CreateWall", "CreateWall", "CreateFloor", "CreateRoom" },
                SuggestionTitle = "Complete room creation",
                IsCommon = true
            });

            _patternDatabase.AddPattern(new DesignPattern
            {
                Name = "DoorInWall",
                Description = "Add door after wall creation",
                ActionSequence = new[] { "CreateWall", "CreateDoor" },
                TriggerAction = "CreateWall",
                SuggestionTitle = "Add door to wall",
                SuggestionDescription = "Walls typically need doors for access",
                IsCommon = true
            });

            _patternDatabase.AddPattern(new DesignPattern
            {
                Name = "WindowSequence",
                Description = "Add windows to room",
                ActionSequence = new[] { "CreateRoom", "CreateWindow", "CreateWindow" },
                TriggerAction = "CreateRoom",
                SuggestionTitle = "Add windows",
                SuggestionDescription = "Habitable rooms need natural light",
                IsCommon = true
            });

            _patternDatabase.AddPattern(new DesignPattern
            {
                Name = "DuplicateWithOffset",
                Description = "Create copies of element",
                ActionSequence = new[] { "SelectElement", "Copy", "Paste" },
                SuggestionTitle = "Copy selected element",
                IsCommon = true
            });

            _patternDatabase.AddPattern(new DesignPattern
            {
                Name = "BathroomComplete",
                Description = "Complete bathroom fit-out",
                ActionSequence = new[] { "CreateRoom", "SetRoomType:Bathroom", "AddPlumbing", "AddFixtures" },
                SuggestionTitle = "Complete bathroom setup",
                SuggestionDescription = "Add plumbing and fixtures to bathroom",
                IsCommon = true
            });

            // Initialize action sequence model with common transitions
            _sequenceModel.AddTransition("CreateWall", "CreateWall", 0.4);
            _sequenceModel.AddTransition("CreateWall", "CreateDoor", 0.3);
            _sequenceModel.AddTransition("CreateWall", "CreateWindow", 0.2);
            _sequenceModel.AddTransition("CreateWall", "CreateFloor", 0.1);

            _sequenceModel.AddTransition("CreateRoom", "CreateDoor", 0.35);
            _sequenceModel.AddTransition("CreateRoom", "CreateWindow", 0.3);
            _sequenceModel.AddTransition("CreateRoom", "CreateRoom", 0.2);
            _sequenceModel.AddTransition("CreateRoom", "SetRoomType", 0.15);

            _sequenceModel.AddTransition("CreateDoor", "CreateDoor", 0.2);
            _sequenceModel.AddTransition("CreateDoor", "CreateWindow", 0.25);
            _sequenceModel.AddTransition("CreateDoor", "CreateWall", 0.3);
            _sequenceModel.AddTransition("CreateDoor", "CreateRoom", 0.25);

            _sequenceModel.AddTransition("SelectElement", "Move", 0.3);
            _sequenceModel.AddTransition("SelectElement", "Copy", 0.2);
            _sequenceModel.AddTransition("SelectElement", "Delete", 0.15);
            _sequenceModel.AddTransition("SelectElement", "EditProperties", 0.2);
            _sequenceModel.AddTransition("SelectElement", "Modify", 0.15);

            // Second-order transitions: P(next | prev, current)
            // After two consecutive walls, next is likely a door or floor (room enclosure pattern)
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateWall", "CreateDoor", 0.35);
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateWall", "CreateFloor", 0.25);
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateWall", "CreateWall", 0.25);
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateWall", "CreateWindow", 0.15);

            // After wall then door, likely another door or window
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateDoor", "CreateWindow", 0.35);
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateDoor", "CreateDoor", 0.25);
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateDoor", "CreateWall", 0.25);
            _sequenceModel.AddSecondOrderTransition("CreateWall", "CreateDoor", "CreateRoom", 0.15);

            // After room then door, likely set room type or add window
            _sequenceModel.AddSecondOrderTransition("CreateRoom", "CreateDoor", "CreateWindow", 0.4);
            _sequenceModel.AddSecondOrderTransition("CreateRoom", "CreateDoor", "SetRoomType", 0.3);
            _sequenceModel.AddSecondOrderTransition("CreateRoom", "CreateDoor", "CreateRoom", 0.3);

            // After room then set type, likely create another room or add door
            _sequenceModel.AddSecondOrderTransition("CreateRoom", "SetRoomType", "CreateRoom", 0.4);
            _sequenceModel.AddSecondOrderTransition("CreateRoom", "SetRoomType", "CreateDoor", 0.3);
            _sequenceModel.AddSecondOrderTransition("CreateRoom", "SetRoomType", "CreateWindow", 0.3);

            // After select then move, likely select again or edit
            _sequenceModel.AddSecondOrderTransition("SelectElement", "Move", "SelectElement", 0.4);
            _sequenceModel.AddSecondOrderTransition("SelectElement", "Move", "EditProperties", 0.3);
            _sequenceModel.AddSecondOrderTransition("SelectElement", "Move", "Move", 0.3);

            Logger.Info("Initialized default prediction patterns with higher-order Markov chains");
        }

        private string[] TokenizeInput(string input)
        {
            return input
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
        }

        private IEnumerable<CommandCompletion> GetActionCompletions(string partial, PredictionContext context)
        {
            var completions = new List<CommandCompletion>();

            // Common actions
            var actions = new[]
            {
                ("create", "Create element", 1.0),
                ("select", "Select element", 0.9),
                ("move", "Move element", 0.8),
                ("copy", "Copy element", 0.75),
                ("delete", "Delete element", 0.7),
                ("modify", "Modify element", 0.7),
                ("show", "Show/display", 0.6),
                ("hide", "Hide element", 0.6),
                ("zoom", "Zoom view", 0.5),
                ("undo", "Undo last action", 0.8),
                ("redo", "Redo action", 0.7)
            };

            foreach (var (action, desc, baseScore) in actions)
            {
                if (action.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new CommandCompletion
                    {
                        Text = action,
                        Description = desc,
                        Score = baseScore * (1 - (double)partial.Length / action.Length * 0.3),
                        CompletionType = CompletionType.Action
                    });
                }
            }

            // Boost based on recent context
            var lastAction = context.RecentActions.LastOrDefault();
            if (lastAction != null)
            {
                var predictions = _sequenceModel.PredictNext(context.RecentActions, 3);
                foreach (var pred in predictions)
                {
                    var existing = completions.FirstOrDefault(c =>
                        c.Text.Equals(pred.ActionType, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Score += pred.Confidence * 0.3;
                    }
                }
            }

            return completions;
        }

        private IEnumerable<CommandCompletion> GetParameterCompletions(string[] tokens, PredictionContext context)
        {
            var completions = new List<CommandCompletion>();
            var action = tokens[0].ToLowerInvariant();
            var currentParam = tokens.Length > 1 ? tokens.Last() : "";

            switch (action)
            {
                case "create":
                    if (tokens.Length == 1 || (tokens.Length == 2 && !currentParam.EndsWith(" ")))
                    {
                        // Complete element type
                        var types = new[]
                        {
                            ("wall", "Create wall", 1.0),
                            ("room", "Create room", 0.95),
                            ("door", "Create door", 0.9),
                            ("window", "Create window", 0.85),
                            ("floor", "Create floor", 0.8),
                            ("column", "Create column", 0.7),
                            ("beam", "Create beam", 0.65),
                            ("stair", "Create stair", 0.6)
                        };

                        foreach (var (type, desc, score) in types)
                        {
                            if (tokens.Length == 1 || type.StartsWith(currentParam, StringComparison.OrdinalIgnoreCase))
                            {
                                completions.Add(new CommandCompletion
                                {
                                    Text = $"{action} {type}",
                                    Description = desc,
                                    Score = score,
                                    CompletionType = CompletionType.ElementType
                                });
                            }
                        }
                    }
                    else if (tokens.Length >= 2)
                    {
                        // Complete dimensions or properties
                        var elementType = tokens[1].ToLowerInvariant();
                        var predictedParams = _parameterPredictor.PredictParameters(
                            $"Create{char.ToUpper(elementType[0])}{elementType.Substring(1)}",
                            context);

                        foreach (var param in predictedParams)
                        {
                            completions.Add(new CommandCompletion
                            {
                                Text = $"{action} {elementType} {param.Value}",
                                Description = $"With {param.Key}: {param.Value}",
                                Score = 0.8,
                                CompletionType = CompletionType.Parameter
                            });
                        }
                    }
                    break;

                case "select":
                    // Complete with available element types or IDs
                    var selectables = new[] { "all walls", "all doors", "all windows", "last", "similar" };
                    foreach (var sel in selectables)
                    {
                        if (tokens.Length == 1 || sel.StartsWith(currentParam, StringComparison.OrdinalIgnoreCase))
                        {
                            completions.Add(new CommandCompletion
                            {
                                Text = $"{action} {sel}",
                                Description = $"Select {sel}",
                                Score = 0.8,
                                CompletionType = CompletionType.Target
                            });
                        }
                    }
                    break;

                case "move":
                case "copy":
                    // Complete with direction/distance
                    var directions = new[] { "north", "south", "east", "west", "up", "down" };
                    foreach (var dir in directions)
                    {
                        completions.Add(new CommandCompletion
                        {
                            Text = $"{action} {dir} 1m",
                            Description = $"{action} 1 meter {dir}",
                            Score = 0.75,
                            CompletionType = CompletionType.Direction
                        });
                    }
                    break;
            }

            return completions;
        }

        private IEnumerable<FollowUpAction> GetCommonFollowUps(string actionType)
        {
            var followUps = new List<FollowUpAction>();

            switch (actionType.ToLowerInvariant())
            {
                case "createwall":
                    followUps.Add(new FollowUpAction
                    {
                        ActionType = "CreateDoor",
                        Title = "Add door",
                        Description = "Add a door to the wall",
                        Frequency = 0.6
                    });
                    followUps.Add(new FollowUpAction
                    {
                        ActionType = "CreateWindow",
                        Title = "Add window",
                        Description = "Add a window to the wall",
                        Frequency = 0.5
                    });
                    break;

                case "createroom":
                    followUps.Add(new FollowUpAction
                    {
                        ActionType = "SetRoomType",
                        Title = "Set room type",
                        Description = "Specify the room function (bedroom, kitchen, etc.)",
                        Frequency = 0.7
                    });
                    followUps.Add(new FollowUpAction
                    {
                        ActionType = "CreateDoor",
                        Title = "Add door",
                        Description = "Add entrance door to the room",
                        Frequency = 0.65
                    });
                    break;

                case "selectelement":
                    followUps.Add(new FollowUpAction
                    {
                        ActionType = "EditProperties",
                        Title = "Edit properties",
                        Description = "Modify selected element properties",
                        Frequency = 0.5
                    });
                    followUps.Add(new FollowUpAction
                    {
                        ActionType = "Move",
                        Title = "Move element",
                        Description = "Move selected element",
                        Frequency = 0.4
                    });
                    break;
            }

            return followUps;
        }

        private IEnumerable<ProactiveSuggestion> GetContextBasedSuggestions(PredictionContext context)
        {
            var suggestions = new List<ProactiveSuggestion>();

            // Suggest based on selected elements
            if (context.SelectedElements?.Any() == true)
            {
                var selectedTypes = context.SelectedElements
                    .GroupBy(e => e.ElementType)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (selectedTypes != null)
                {
                    suggestions.Add(new ProactiveSuggestion
                    {
                        Title = $"Work with {selectedTypes.Key}s",
                        Description = $"You have {selectedTypes.Count()} {selectedTypes.Key}(s) selected",
                        Actions = new List<string> { "Move", "Copy", "EditProperties" },
                        Confidence = 0.6,
                        Category = SuggestionCategory.ContextBased
                    });
                }
            }

            // Suggest based on current view
            if (context.CurrentView == "FloorPlan" && context.RecentActions.Count == 0)
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Start designing",
                    Description = "Begin by creating walls or rooms",
                    Actions = new List<string> { "CreateWall", "CreateRoom" },
                    Confidence = 0.5,
                    Category = SuggestionCategory.GettingStarted
                });
            }

            return suggestions;
        }

        #endregion
    }

    #region Supporting Types

    public class PredictionContext
    {
        public string CurrentAction { get; set; }
        public List<UserAction> RecentActions { get; set; } = new();
        public List<SelectedElement> SelectedElements { get; set; } = new();
        public string CurrentView { get; set; }
        public string ProjectType { get; set; }
        public Dictionary<string, object> State { get; set; } = new();
    }

    public class UserAction
    {
        public string ActionType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool WasSuccessful { get; set; } = true;
    }

    public class SelectedElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class PredictedAction
    {
        public string ActionType { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public PredictionSource Source { get; set; }
        public string Description { get; set; }
    }

    public enum PredictionSource
    {
        SequenceModel,
        ContextAnalysis,
        PatternMatch,
        UserHistory
    }

    public class CommandCompletion
    {
        public string Text { get; set; }
        public string Description { get; set; }
        public double Score { get; set; }
        public CompletionType CompletionType { get; set; }
    }

    public enum CompletionType
    {
        Action,
        ElementType,
        Parameter,
        Target,
        Direction,
        Value
    }

    public class ProactiveSuggestion
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Actions { get; set; } = new();
        public double Confidence { get; set; }
        public SuggestionCategory Category { get; set; }
    }

    public enum SuggestionCategory
    {
        PatternCompletion,
        CommonFollowUp,
        ContextBased,
        GettingStarted
    }

    public class DesignPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] ActionSequence { get; set; }
        public string TriggerAction { get; set; }
        public string SuggestionTitle { get; set; }
        public string SuggestionDescription { get; set; }
        public bool IsCommon { get; set; }
    }

    public class FollowUpAction
    {
        public string ActionType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Frequency { get; set; }
    }

    internal class ActionSequenceModel
    {
        // First-order: P(next | current)
        private readonly Dictionary<string, Dictionary<string, double>> _transitions = new();
        // Second-order: P(next | prev, current)  key = "prev|current"
        private readonly Dictionary<string, Dictionary<string, double>> _secondOrder = new();
        // Third-order: P(next | prev2, prev1, current)  key = "prev2|prev1|current"
        private readonly Dictionary<string, Dictionary<string, double>> _thirdOrder = new();

        private readonly List<UserAction> _history = new();

        // Blending weights for combining predictions from different orders
        // Higher orders get more weight when they have sufficient data
        private const double FirstOrderWeight = 0.3;
        private const double SecondOrderWeight = 0.4;
        private const double ThirdOrderWeight = 0.3;
        private const int MinSamplesForSecondOrder = 3;
        private const int MinSamplesForThirdOrder = 5;

        public void AddTransition(string fromAction, string toAction, double probability)
        {
            if (!_transitions.ContainsKey(fromAction))
            {
                _transitions[fromAction] = new Dictionary<string, double>();
            }
            _transitions[fromAction][toAction] = probability;
        }

        /// <summary>
        /// Adds a second-order transition: P(next | prev, current).
        /// </summary>
        public void AddSecondOrderTransition(string prevAction, string currentAction, string nextAction, double probability)
        {
            var key = $"{prevAction}|{currentAction}";
            if (!_secondOrder.ContainsKey(key))
            {
                _secondOrder[key] = new Dictionary<string, double>();
            }
            _secondOrder[key][nextAction] = probability;
        }

        public void AddAction(UserAction action)
        {
            // Update first-order transitions
            if (_history.Count > 0)
            {
                var prev = _history.Last().ActionType;
                UpdateTransitionTable(_transitions, prev, action.ActionType);
            }

            // Update second-order transitions
            if (_history.Count >= 2)
            {
                var prev1 = _history[_history.Count - 1].ActionType;
                var prev2 = _history[_history.Count - 2].ActionType;
                var key2 = $"{prev2}|{prev1}";
                UpdateTransitionTable(_secondOrder, key2, action.ActionType);
            }

            // Update third-order transitions
            if (_history.Count >= 3)
            {
                var prev1 = _history[_history.Count - 1].ActionType;
                var prev2 = _history[_history.Count - 2].ActionType;
                var prev3 = _history[_history.Count - 3].ActionType;
                var key3 = $"{prev3}|{prev2}|{prev1}";
                UpdateTransitionTable(_thirdOrder, key3, action.ActionType);
            }

            _history.Add(action);
            if (_history.Count > 100)
            {
                _history.RemoveAt(0);
            }
        }

        private void UpdateTransitionTable(Dictionary<string, Dictionary<string, double>> table, string key, string nextAction)
        {
            if (!table.ContainsKey(key))
            {
                table[key] = new Dictionary<string, double>();
            }

            if (table[key].ContainsKey(nextAction))
            {
                table[key][nextAction] += 0.1;
            }
            else
            {
                table[key][nextAction] = 0.1;
            }

            // Normalize
            var total = table[key].Values.Sum();
            foreach (var k in table[key].Keys.ToList())
            {
                table[key][k] /= total;
            }
        }

        /// <summary>
        /// Predicts next actions using blended higher-order Markov chains.
        /// Combines first-order, second-order, and third-order predictions with
        /// adaptive weights based on data availability.
        /// </summary>
        public IEnumerable<(string ActionType, double Confidence)> PredictNext(List<UserAction> recentActions, int count)
        {
            if (recentActions == null || recentActions.Count == 0)
            {
                return new[] { ("CreateWall", 0.3), ("CreateRoom", 0.3) };
            }

            var blended = new Dictionary<string, double>();

            // First-order prediction: P(next | current)
            var lastAction = recentActions.Last().ActionType;
            double w1 = FirstOrderWeight;
            if (_transitions.TryGetValue(lastAction, out var firstOrder))
            {
                foreach (var (action, prob) in firstOrder)
                {
                    blended[action] = blended.GetValueOrDefault(action, 0.0) + prob * w1;
                }
            }

            // Second-order prediction: P(next | prev, current)
            double w2 = 0;
            if (recentActions.Count >= 2)
            {
                var prev = recentActions[recentActions.Count - 2].ActionType;
                var key2 = $"{prev}|{lastAction}";
                if (_secondOrder.TryGetValue(key2, out var secondOrder) && _history.Count >= MinSamplesForSecondOrder)
                {
                    w2 = SecondOrderWeight;
                    foreach (var (action, prob) in secondOrder)
                    {
                        blended[action] = blended.GetValueOrDefault(action, 0.0) + prob * w2;
                    }
                }
            }

            // Third-order prediction: P(next | prev2, prev1, current)
            double w3 = 0;
            if (recentActions.Count >= 3)
            {
                var prev1 = recentActions[recentActions.Count - 2].ActionType;
                var prev2 = recentActions[recentActions.Count - 3].ActionType;
                var key3 = $"{prev2}|{prev1}|{lastAction}";
                if (_thirdOrder.TryGetValue(key3, out var thirdOrder) && _history.Count >= MinSamplesForThirdOrder)
                {
                    w3 = ThirdOrderWeight;
                    foreach (var (action, prob) in thirdOrder)
                    {
                        blended[action] = blended.GetValueOrDefault(action, 0.0) + prob * w3;
                    }
                }
            }

            // Normalize blended scores by total weight used
            var totalWeight = w1 + w2 + w3;
            if (totalWeight > 0 && blended.Count > 0)
            {
                foreach (var key in blended.Keys.ToList())
                {
                    blended[key] /= totalWeight;
                }

                return blended
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => (kvp.Key, kvp.Value));
            }

            // Fallback to first-order only
            if (_transitions.TryGetValue(lastAction, out var fallback))
            {
                return fallback
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => (kvp.Key, kvp.Value));
            }

            return Enumerable.Empty<(string, double)>();
        }

        /// <summary>
        /// Gets statistics about the transition model for diagnostics.
        /// </summary>
        public (int FirstOrder, int SecondOrder, int ThirdOrder, int HistorySize) GetModelStats()
        {
            return (_transitions.Count, _secondOrder.Count, _thirdOrder.Count, _history.Count);
        }
    }

    internal class ParameterPredictor
    {
        private readonly Dictionary<string, Dictionary<string, List<object>>> _parameterHistory = new();

        public void LearnParameters(UserAction action, PredictionContext context)
        {
            if (!_parameterHistory.ContainsKey(action.ActionType))
            {
                _parameterHistory[action.ActionType] = new Dictionary<string, List<object>>();
            }

            foreach (var param in action.Parameters)
            {
                if (!_parameterHistory[action.ActionType].ContainsKey(param.Key))
                {
                    _parameterHistory[action.ActionType][param.Key] = new List<object>();
                }
                _parameterHistory[action.ActionType][param.Key].Add(param.Value);
            }
        }

        public Dictionary<string, object> PredictParameters(string actionType, PredictionContext context)
        {
            var predicted = new Dictionary<string, object>();

            // Use common defaults based on action type
            switch (actionType.ToLowerInvariant())
            {
                case "createwall":
                    predicted["Length"] = "4m";
                    predicted["Height"] = "2.7m";
                    predicted["Type"] = "Generic - 200mm";
                    break;

                case "createroom":
                    predicted["Width"] = "4m";
                    predicted["Length"] = "4m";
                    predicted["Height"] = "2.7m";
                    break;

                case "createdoor":
                    predicted["Width"] = "0.9m";
                    predicted["Height"] = "2.1m";
                    predicted["Type"] = "Single Flush";
                    break;

                case "createwindow":
                    predicted["Width"] = "1.2m";
                    predicted["Height"] = "1.5m";
                    predicted["SillHeight"] = "0.9m";
                    break;
            }

            // Override with learned values
            if (_parameterHistory.TryGetValue(actionType, out var history))
            {
                foreach (var param in history)
                {
                    var mostCommon = param.Value
                        .GroupBy(v => v?.ToString())
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key;

                    if (mostCommon != null)
                    {
                        predicted[param.Key] = mostCommon;
                    }
                }
            }

            return predicted;
        }
    }

    internal class ContextAnalyzer
    {
        public IEnumerable<PredictedAction> AnalyzeAndPredict(PredictionContext context)
        {
            var predictions = new List<PredictedAction>();

            // If elements are selected, suggest modification actions
            if (context.SelectedElements?.Any() == true)
            {
                predictions.Add(new PredictedAction
                {
                    ActionType = "Move",
                    Confidence = 0.5,
                    Source = PredictionSource.ContextAnalysis,
                    Description = "Move selected elements"
                });

                predictions.Add(new PredictedAction
                {
                    ActionType = "EditProperties",
                    Confidence = 0.4,
                    Source = PredictionSource.ContextAnalysis,
                    Description = "Edit properties"
                });
            }

            // If nothing selected and empty project, suggest starting actions
            if ((context.SelectedElements == null || !context.SelectedElements.Any()) &&
                (context.RecentActions == null || !context.RecentActions.Any()))
            {
                predictions.Add(new PredictedAction
                {
                    ActionType = "CreateWall",
                    Confidence = 0.6,
                    Source = PredictionSource.ContextAnalysis,
                    Description = "Start by creating walls"
                });
            }

            return predictions;
        }

        public object AnalyzeWorkContext(PredictionContext context)
        {
            return new
            {
                IsStarting = context.RecentActions?.Count < 3,
                IsModifying = context.SelectedElements?.Any() == true,
                CurrentFocus = context.RecentActions?.LastOrDefault()?.ActionType
            };
        }
    }

    internal class PatternDatabase
    {
        private readonly List<DesignPattern> _patterns = new();

        public void AddPattern(DesignPattern pattern)
        {
            _patterns.Add(pattern);
        }

        public void UpdatePatterns(UserAction action, PredictionContext context)
        {
            // Update pattern frequencies based on observed actions
            // This would involve more sophisticated pattern mining in a full implementation
        }

        public IEnumerable<PredictedAction> MatchPatterns(PredictionContext context)
        {
            var predictions = new List<PredictedAction>();

            if (context.RecentActions == null || !context.RecentActions.Any())
                return predictions;

            var recentActionTypes = context.RecentActions
                .TakeLast(5)
                .Select(a => a.ActionType)
                .ToList();

            foreach (var pattern in _patterns.Where(p => p.IsCommon))
            {
                // Check if recent actions match start of pattern
                var matchLength = FindMatchLength(recentActionTypes, pattern.ActionSequence);

                if (matchLength > 0 && matchLength < pattern.ActionSequence.Length)
                {
                    var nextAction = pattern.ActionSequence[matchLength];
                    predictions.Add(new PredictedAction
                    {
                        ActionType = nextAction,
                        Confidence = 0.3 + (matchLength * 0.1),
                        Source = PredictionSource.PatternMatch,
                        Description = pattern.SuggestionDescription ?? $"Continue: {pattern.Name}"
                    });
                }
            }

            return predictions;
        }

        public IEnumerable<IncompletePattern> FindIncompletePatterns(PredictionContext context)
        {
            var incomplete = new List<IncompletePattern>();

            if (context.RecentActions == null || !context.RecentActions.Any())
                return incomplete;

            var recentActionTypes = context.RecentActions
                .TakeLast(5)
                .Select(a => a.ActionType)
                .ToList();

            foreach (var pattern in _patterns)
            {
                var matchLength = FindMatchLength(recentActionTypes, pattern.ActionSequence);

                if (matchLength > 0 && matchLength < pattern.ActionSequence.Length)
                {
                    incomplete.Add(new IncompletePattern
                    {
                        PatternName = pattern.Name,
                        SuggestionTitle = pattern.SuggestionTitle,
                        SuggestionDescription = pattern.SuggestionDescription,
                        RemainingActions = pattern.ActionSequence.Skip(matchLength).ToList(),
                        CompletionConfidence = (double)matchLength / pattern.ActionSequence.Length
                    });
                }
            }

            return incomplete;
        }

        private int FindMatchLength(List<string> actions, string[] pattern)
        {
            if (actions.Count == 0 || pattern.Length == 0)
                return 0;

            var maxMatch = 0;
            for (int start = 0; start < actions.Count; start++)
            {
                var match = 0;
                for (int i = 0; i < Math.Min(actions.Count - start, pattern.Length); i++)
                {
                    if (actions[start + i].Equals(pattern[i], StringComparison.OrdinalIgnoreCase))
                    {
                        match++;
                    }
                    else
                    {
                        break;
                    }
                }
                maxMatch = Math.Max(maxMatch, match);
            }

            return maxMatch;
        }
    }

    internal class IncompletePattern
    {
        public string PatternName { get; set; }
        public string SuggestionTitle { get; set; }
        public string SuggestionDescription { get; set; }
        public List<string> RemainingActions { get; set; }
        public double CompletionConfidence { get; set; }
    }

    #endregion
}
