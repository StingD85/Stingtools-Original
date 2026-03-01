// =============================================================================
// StingBIM.AI.Collaboration - Voice Command System
// Hands-free BIM operations using speech recognition and natural language
// Integrates with Windows Speech API, Azure Cognitive Services, or offline models
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.Voice
{
    /// <summary>
    /// Voice command system for hands-free BIM operations.
    /// Supports wake words, natural language commands, and spoken responses.
    /// </summary>
    public class VoiceCommandSystem : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Speech engines
        private SpeechRecognitionEngine? _recognizer;
        private SpeechSynthesizer? _synthesizer;
        private bool _isListening;
        private bool _isAwake;
        private DateTime _lastWakeTime;

        // Command processing
        private readonly ConcurrentDictionary<string, VoiceCommandDefinition> _commands = new();
        private readonly List<VoiceCommandHandler> _handlers = new();
        private readonly BIMCommandInterpreter _interpreter;

        // Configuration
        private readonly VoiceConfig _config;
        private Timer? _sleepTimer;

        // Context
        private string? _currentViewId;
        private string? _currentElementId;
        private List<string> _selectedElements = new();
        private ConversationContext _conversationContext = new();

        // Events
        public event EventHandler<VoiceCommandEventArgs>? CommandRecognized;
        public event EventHandler<VoiceResponseEventArgs>? ResponseGenerated;
        public event EventHandler<WakeWordEventArgs>? WakeWordDetected;
        public event EventHandler<ListeningStateEventArgs>? ListeningStateChanged;
        public event EventHandler<BIMActionEventArgs>? BIMActionRequested;

        public bool IsListening => _isListening;
        public bool IsAwake => _isAwake;
        public VoiceConfig Config => _config;

        public VoiceCommandSystem(VoiceConfig? config = null)
        {
            _config = config ?? new VoiceConfig();
            _interpreter = new BIMCommandInterpreter();
            InitializeCommands();
        }

        #region Initialization

        /// <summary>
        /// Start the voice command system
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                // Initialize speech recognizer
                _recognizer = new SpeechRecognitionEngine();

                // Build grammar
                var grammar = BuildGrammar();
                _recognizer.LoadGrammar(grammar);

                // Set up event handlers
                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                _recognizer.SpeechDetected += OnSpeechDetected;

                // Configure audio
                _recognizer.SetInputToDefaultAudioDevice();

                // Initialize synthesizer
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                _synthesizer.Rate = _config.SpeechRate;
                _synthesizer.Volume = _config.SpeechVolume;

                // Start recognizing
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                _isListening = true;

                // Sleep timer (auto-sleep after inactivity)
                _sleepTimer = new Timer(CheckSleepTimeout, null,
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                Logger.Info("Voice command system started");
                ListeningStateChanged?.Invoke(this, new ListeningStateEventArgs(true, false));

                if (_config.AnnounceStartup)
                {
                    await SpeakAsync("StingBIM voice commands ready. Say 'Hey StingBIM' to begin.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start voice command system");
                throw;
            }
        }

        /// <summary>
        /// Stop the voice command system
        /// </summary>
        public void Stop()
        {
            _recognizer?.RecognizeAsyncStop();
            _sleepTimer?.Dispose();
            _isListening = false;
            _isAwake = false;

            Logger.Info("Voice command system stopped");
            ListeningStateChanged?.Invoke(this, new ListeningStateEventArgs(false, false));
        }

        private Grammar BuildGrammar()
        {
            var choices = new Choices();

            // Wake words
            foreach (var wakeWord in _config.WakeWords)
            {
                choices.Add(wakeWord);
            }

            // Command patterns
            foreach (var cmd in _commands.Values)
            {
                foreach (var pattern in cmd.Patterns)
                {
                    choices.Add(pattern);
                }
            }

            // Add natural language elements
            AddBIMVocabulary(choices);

            var gb = new GrammarBuilder(choices);
            gb.Culture = _recognizer!.RecognizerInfo.Culture;

            return new Grammar(gb);
        }

        private void AddBIMVocabulary(Choices choices)
        {
            // Categories
            var categories = new[] { "walls", "doors", "windows", "floors", "ceilings",
                "roofs", "columns", "beams", "pipes", "ducts", "equipment", "furniture" };

            // Actions
            var actions = new[] { "select", "hide", "show", "isolate", "delete", "copy",
                "move", "rotate", "mirror", "align", "create", "modify", "edit" };

            // Directions
            var directions = new[] { "left", "right", "up", "down", "north", "south",
                "east", "west", "above", "below" };

            // Numbers (for dimensions, counts)
            for (int i = 0; i <= 100; i++)
            {
                choices.Add(i.ToString());
            }

            // Units
            choices.Add(new Choices("meters", "centimeters", "millimeters", "feet", "inches"));

            // Build compound phrases
            foreach (var action in actions)
            {
                choices.Add(action);
                foreach (var category in categories)
                {
                    choices.Add($"{action} {category}");
                    choices.Add($"{action} all {category}");
                    choices.Add($"{action} selected {category}");
                }
            }

            // Common BIM phrases
            choices.Add(new Choices(
                "sync with central",
                "save model",
                "reload latest",
                "show conflicts",
                "who is working on this",
                "what is this element",
                "navigate to level",
                "go to view",
                "zoom to fit",
                "zoom in",
                "zoom out",
                "pan left",
                "pan right",
                "orbit view",
                "reset view",
                "section box",
                "take screenshot",
                "start measuring",
                "add dimension",
                "add tag",
                "create room",
                "calculate area"
            ));
        }

        private void InitializeCommands()
        {
            // Navigation commands
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "navigate_view",
                Patterns = new[] { "go to *", "navigate to *", "open view *", "show view *" },
                Handler = HandleNavigateViewAsync,
                Description = "Navigate to a view"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "navigate_level",
                Patterns = new[] { "go to level *", "show level *", "navigate to floor *" },
                Handler = HandleNavigateLevelAsync,
                Description = "Navigate to a level"
            });

            // Selection commands
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "select_elements",
                Patterns = new[] { "select *", "pick *", "choose *" },
                Handler = HandleSelectElementsAsync,
                Description = "Select elements"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "select_all",
                Patterns = new[] { "select all *", "select everything" },
                Handler = HandleSelectAllAsync,
                Description = "Select all elements of a type"
            });

            // Visibility commands
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "hide_elements",
                Patterns = new[] { "hide *", "turn off *", "make * invisible" },
                Handler = HandleHideElementsAsync,
                Description = "Hide elements"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "show_elements",
                Patterns = new[] { "show *", "turn on *", "make * visible", "unhide *" },
                Handler = HandleShowElementsAsync,
                Description = "Show elements"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "isolate_elements",
                Patterns = new[] { "isolate *", "show only *", "focus on *" },
                Handler = HandleIsolateElementsAsync,
                Description = "Isolate elements"
            });

            // Information commands
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "what_is",
                Patterns = new[] { "what is this", "what is selected", "element info", "tell me about *" },
                Handler = HandleWhatIsAsync,
                Description = "Get element information"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "who_working",
                Patterns = new[] { "who is working on *", "who owns *", "who has *" },
                Handler = HandleWhoWorkingAsync,
                Description = "Check who is working on elements"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "show_conflicts",
                Patterns = new[] { "show conflicts", "any conflicts", "check for conflicts" },
                Handler = HandleShowConflictsAsync,
                Description = "Show predicted conflicts"
            });

            // Sync commands
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "sync",
                Patterns = new[] { "sync with central", "synchronize", "sync now", "save to central" },
                Handler = HandleSyncAsync,
                Description = "Sync with central model"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "sync_status",
                Patterns = new[] { "sync status", "am I synced", "when did I last sync" },
                Handler = HandleSyncStatusAsync,
                Description = "Check sync status"
            });

            // View manipulation
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "zoom",
                Patterns = new[] { "zoom in", "zoom out", "zoom to fit", "zoom to selection" },
                Handler = HandleZoomAsync,
                Description = "Zoom controls"
            });

            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "section_box",
                Patterns = new[] { "create section box", "apply section box", "remove section box", "toggle section box" },
                Handler = HandleSectionBoxAsync,
                Description = "Section box controls"
            });

            // Creation commands
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "create_element",
                Patterns = new[] { "create *", "add *", "place *", "insert *" },
                Handler = HandleCreateElementAsync,
                Description = "Create new elements"
            });

            // Measurement
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "measure",
                Patterns = new[] { "measure", "start measuring", "measure distance", "measure area" },
                Handler = HandleMeasureAsync,
                Description = "Start measurement tool"
            });

            // Help
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "help",
                Patterns = new[] { "help", "what can you do", "list commands", "show commands" },
                Handler = HandleHelpAsync,
                Description = "Show help"
            });

            // Cancel/Stop
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "cancel",
                Patterns = new[] { "cancel", "stop", "never mind", "abort" },
                Handler = HandleCancelAsync,
                Description = "Cancel current operation"
            });

            // Sleep
            RegisterCommand(new VoiceCommandDefinition
            {
                Name = "sleep",
                Patterns = new[] { "go to sleep", "stop listening", "goodbye", "bye" },
                Handler = HandleSleepAsync,
                Description = "Put voice system to sleep"
            });
        }

        #endregion

        #region Command Registration

        /// <summary>
        /// Register a custom voice command
        /// </summary>
        public void RegisterCommand(VoiceCommandDefinition command)
        {
            _commands[command.Name] = command;
            Logger.Debug($"Registered voice command: {command.Name}");
        }

        /// <summary>
        /// Register a command handler
        /// </summary>
        public void AddHandler(VoiceCommandHandler handler)
        {
            _handlers.Add(handler);
        }

        #endregion

        #region Speech Recognition Handlers

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < _config.MinConfidence)
            {
                Logger.Debug($"Low confidence recognition: {e.Result.Text} ({e.Result.Confidence:P})");
                return;
            }

            var text = e.Result.Text;
            Logger.Info($"Recognized: {text} ({e.Result.Confidence:P})");

            // Check for wake word
            if (!_isAwake)
            {
                if (_config.WakeWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)))
                {
                    WakeUp();
                    return;
                }
                return; // Not awake and no wake word
            }

            // Process command
            _ = Task.Run(() => ProcessCommandAsync(text));
        }

        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            if (_isAwake && _config.AcknowledgeUnrecognized)
            {
                _ = SpeakAsync("I didn't catch that. Could you repeat?");
            }
        }

        private void OnSpeechDetected(object? sender, SpeechDetectedEventArgs e)
        {
            // Reset sleep timer when speech is detected
            if (_isAwake)
            {
                _lastWakeTime = DateTime.UtcNow;
            }
        }

        #endregion

        #region Command Processing

        private async Task ProcessCommandAsync(string text)
        {
            try
            {
                // Find matching command
                var match = FindMatchingCommand(text);

                if (match != null)
                {
                    var args = new VoiceCommandEventArgs(text, match.Command, match.Parameters);
                    CommandRecognized?.Invoke(this, args);

                    if (!args.Handled)
                    {
                        // Execute command handler
                        var response = await match.Command.Handler(match.Parameters, _conversationContext);

                        if (!string.IsNullOrEmpty(response.SpokenResponse))
                        {
                            await SpeakAsync(response.SpokenResponse);
                        }

                        if (response.BIMAction != null)
                        {
                            BIMActionRequested?.Invoke(this, new BIMActionEventArgs(response.BIMAction));
                        }

                        // Update context
                        _conversationContext.LastCommand = text;
                        _conversationContext.LastResponse = response;
                    }
                }
                else
                {
                    // Try natural language interpretation
                    var interpreted = _interpreter.Interpret(text, _conversationContext);

                    if (interpreted.Success)
                    {
                        var response = await ExecuteInterpretedCommandAsync(interpreted);
                        if (!string.IsNullOrEmpty(response))
                        {
                            await SpeakAsync(response);
                        }
                    }
                    else if (_config.AcknowledgeUnrecognized)
                    {
                        await SpeakAsync("I'm not sure what you mean. Try saying 'help' for available commands.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing command: {text}");
                await SpeakAsync("Sorry, I encountered an error processing that command.");
            }
        }

        private CommandMatch? FindMatchingCommand(string text)
        {
            foreach (var cmd in _commands.Values)
            {
                foreach (var pattern in cmd.Patterns)
                {
                    var match = MatchPattern(text, pattern);
                    if (match != null)
                    {
                        return new CommandMatch
                        {
                            Command = cmd,
                            Parameters = match
                        };
                    }
                }
            }
            return null;
        }

        private Dictionary<string, string>? MatchPattern(string text, string pattern)
        {
            // Convert pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", "(.+)")
                .Replace("\\?", "(.+)?") + "$";

            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            var match = regex.Match(text);

            if (match.Success)
            {
                var parameters = new Dictionary<string, string>();

                // Extract captured groups
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    parameters[$"param{i}"] = match.Groups[i].Value;
                }

                return parameters;
            }

            return null;
        }

        private async Task<string> ExecuteInterpretedCommandAsync(InterpretedCommand interpreted)
        {
            var action = new BIMAction
            {
                ActionType = interpreted.ActionType,
                TargetCategory = interpreted.Category,
                TargetElements = interpreted.ElementIds,
                Parameters = interpreted.Parameters
            };

            BIMActionRequested?.Invoke(this, new BIMActionEventArgs(action));

            return interpreted.ConfirmationMessage;
        }

        #endregion

        #region Command Handlers

        private async Task<VoiceCommandResponse> HandleNavigateViewAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var viewName = parameters.GetValueOrDefault("param1", "");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Navigating to {viewName}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.NavigateToView,
                    Parameters = new Dictionary<string, object> { ["viewName"] = viewName }
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleNavigateLevelAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var level = parameters.GetValueOrDefault("param1", "");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Going to level {level}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.NavigateToLevel,
                    Parameters = new Dictionary<string, object> { ["level"] = level }
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleSelectElementsAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var target = parameters.GetValueOrDefault("param1", "");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Selecting {target}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.SelectElements,
                    TargetCategory = target
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleSelectAllAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var category = parameters.GetValueOrDefault("param1", "elements");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Selecting all {category}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.SelectAll,
                    TargetCategory = category
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleHideElementsAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var target = parameters.GetValueOrDefault("param1", "selected elements");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Hiding {target}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.HideElements,
                    TargetCategory = target
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleShowElementsAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var target = parameters.GetValueOrDefault("param1", "hidden elements");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Showing {target}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.ShowElements,
                    TargetCategory = target
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleIsolateElementsAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var target = parameters.GetValueOrDefault("param1", "selected elements");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Isolating {target}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.IsolateElements,
                    TargetCategory = target
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleWhatIsAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Getting element information",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.GetElementInfo
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleWhoWorkingAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var target = parameters.GetValueOrDefault("param1", "this element");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Checking who is working on {target}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.CheckOwnership,
                    TargetCategory = target
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleShowConflictsAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Checking for conflicts",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.ShowConflicts
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleSyncAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Starting sync with central",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.SyncWithCentral
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleSyncStatusAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Checking sync status",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.GetSyncStatus
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleZoomAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var zoomType = context.LastCommand?.ToLower() switch
            {
                var s when s?.Contains("in") == true => "in",
                var s when s?.Contains("out") == true => "out",
                var s when s?.Contains("fit") == true => "fit",
                var s when s?.Contains("selection") == true => "selection",
                _ => "fit"
            };

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Zooming {zoomType}",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.Zoom,
                    Parameters = new Dictionary<string, object> { ["zoomType"] = zoomType }
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleSectionBoxAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Applying section box to selection",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.SectionBox
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleCreateElementAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var elementType = parameters.GetValueOrDefault("param1", "element");

            return new VoiceCommandResponse
            {
                SpokenResponse = $"Starting {elementType} creation. Click to place.",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.CreateElement,
                    TargetCategory = elementType
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleMeasureAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Starting measurement. Click two points.",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.Measure
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleHelpAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            var helpText = "You can say things like: " +
                "Go to level 2. " +
                "Select all walls. " +
                "Hide ducts. " +
                "Sync with central. " +
                "Who is working on this. " +
                "Show conflicts. " +
                "Zoom to fit. " +
                "Or say 'go to sleep' when done.";

            return new VoiceCommandResponse
            {
                SpokenResponse = helpText
            };
        }

        private async Task<VoiceCommandResponse> HandleCancelAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            return new VoiceCommandResponse
            {
                SpokenResponse = "Cancelled",
                BIMAction = new BIMAction
                {
                    ActionType = BIMActionType.Cancel
                }
            };
        }

        private async Task<VoiceCommandResponse> HandleSleepAsync(
            Dictionary<string, string> parameters, ConversationContext context)
        {
            GoToSleep();

            return new VoiceCommandResponse
            {
                SpokenResponse = "Going to sleep. Say 'Hey StingBIM' to wake me up."
            };
        }

        #endregion

        #region Wake/Sleep

        private void WakeUp()
        {
            _isAwake = true;
            _lastWakeTime = DateTime.UtcNow;

            WakeWordDetected?.Invoke(this, new WakeWordEventArgs());
            ListeningStateChanged?.Invoke(this, new ListeningStateEventArgs(true, true));

            _ = SpeakAsync(_config.WakeResponse);
            Logger.Info("Voice system woke up");
        }

        private void GoToSleep()
        {
            _isAwake = false;
            ListeningStateChanged?.Invoke(this, new ListeningStateEventArgs(true, false));
            Logger.Info("Voice system went to sleep");
        }

        private void CheckSleepTimeout(object? state)
        {
            if (_isAwake && (DateTime.UtcNow - _lastWakeTime).TotalSeconds > _config.SleepTimeout)
            {
                GoToSleep();
                _ = SpeakAsync("I'm going to sleep due to inactivity.");
            }
        }

        #endregion

        #region Speech Synthesis

        /// <summary>
        /// Speak text aloud
        /// </summary>
        public async Task SpeakAsync(string text)
        {
            if (_synthesizer == null || !_config.EnableSpeech) return;

            try
            {
                ResponseGenerated?.Invoke(this, new VoiceResponseEventArgs(text));

                await Task.Run(() =>
                {
                    _synthesizer.Speak(text);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error speaking text");
            }
        }

        /// <summary>
        /// Stop current speech
        /// </summary>
        public void StopSpeaking()
        {
            _synthesizer?.SpeakAsyncCancelAll();
        }

        #endregion

        #region Context Update

        /// <summary>
        /// Update current view context
        /// </summary>
        public void SetCurrentView(string viewId, string viewName)
        {
            _currentViewId = viewId;
            _conversationContext.CurrentView = viewName;
        }

        /// <summary>
        /// Update selected elements context
        /// </summary>
        public void SetSelectedElements(List<string> elementIds, string? elementType = null)
        {
            _selectedElements = elementIds;
            _conversationContext.SelectedElementCount = elementIds.Count;
            _conversationContext.SelectedElementType = elementType;

            if (_currentElementId != elementIds.FirstOrDefault())
            {
                _currentElementId = elementIds.FirstOrDefault();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Stop();
            _recognizer?.Dispose();
            _synthesizer?.Dispose();
            _sleepTimer?.Dispose();
        }

        #endregion
    }

    #region Supporting Classes

    public class VoiceConfig
    {
        public string[] WakeWords { get; set; } = new[] { "Hey StingBIM", "StingBIM", "Hey Sting" };
        public string WakeResponse { get; set; } = "Yes, I'm listening.";
        public double MinConfidence { get; set; } = 0.7;
        public int SleepTimeout { get; set; } = 60; // seconds
        public bool EnableSpeech { get; set; } = true;
        public int SpeechRate { get; set; } = 0; // -10 to 10
        public int SpeechVolume { get; set; } = 100; // 0 to 100
        public bool AcknowledgeUnrecognized { get; set; } = true;
        public bool AnnounceStartup { get; set; } = true;
    }

    public class VoiceCommandDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string[] Patterns { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = string.Empty;
        public Func<Dictionary<string, string>, ConversationContext, Task<VoiceCommandResponse>> Handler { get; set; } = null!;
    }

    public class VoiceCommandResponse
    {
        public string? SpokenResponse { get; set; }
        public BIMAction? BIMAction { get; set; }
        public bool Success { get; set; } = true;
    }

    public class BIMAction
    {
        public BIMActionType ActionType { get; set; }
        public string? TargetCategory { get; set; }
        public List<string>? TargetElements { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public enum BIMActionType
    {
        NavigateToView,
        NavigateToLevel,
        SelectElements,
        SelectAll,
        HideElements,
        ShowElements,
        IsolateElements,
        GetElementInfo,
        CheckOwnership,
        ShowConflicts,
        SyncWithCentral,
        GetSyncStatus,
        Zoom,
        SectionBox,
        CreateElement,
        Measure,
        Cancel
    }

    public class ConversationContext
    {
        public string? LastCommand { get; set; }
        public VoiceCommandResponse? LastResponse { get; set; }
        public string? CurrentView { get; set; }
        public int SelectedElementCount { get; set; }
        public string? SelectedElementType { get; set; }
    }

    public class CommandMatch
    {
        public VoiceCommandDefinition Command { get; set; } = null!;
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    public class InterpretedCommand
    {
        public bool Success { get; set; }
        public BIMActionType ActionType { get; set; }
        public string? Category { get; set; }
        public List<string>? ElementIds { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public string ConfirmationMessage { get; set; } = string.Empty;
    }

    public class BIMCommandInterpreter
    {
        public InterpretedCommand Interpret(string text, ConversationContext context)
        {
            // Simple NLP interpretation
            var lower = text.ToLower();

            if (lower.Contains("select") || lower.Contains("pick"))
            {
                return new InterpretedCommand
                {
                    Success = true,
                    ActionType = BIMActionType.SelectElements,
                    Category = ExtractCategory(lower),
                    ConfirmationMessage = $"Selecting {ExtractCategory(lower)}"
                };
            }

            if (lower.Contains("hide"))
            {
                return new InterpretedCommand
                {
                    Success = true,
                    ActionType = BIMActionType.HideElements,
                    Category = ExtractCategory(lower),
                    ConfirmationMessage = $"Hiding {ExtractCategory(lower)}"
                };
            }

            return new InterpretedCommand { Success = false };
        }

        private string ExtractCategory(string text)
        {
            var categories = new[] { "walls", "doors", "windows", "floors", "ceilings",
                "columns", "beams", "pipes", "ducts", "equipment" };

            foreach (var cat in categories)
            {
                if (text.Contains(cat)) return cat;
            }

            return "elements";
        }
    }

    public delegate Task<bool> VoiceCommandHandler(string command, Dictionary<string, string> parameters);

    #endregion

    #region Event Args

    public class VoiceCommandEventArgs : EventArgs
    {
        public string RawText { get; }
        public VoiceCommandDefinition? Command { get; }
        public Dictionary<string, string> Parameters { get; }
        public bool Handled { get; set; }

        public VoiceCommandEventArgs(string text, VoiceCommandDefinition? command, Dictionary<string, string> parameters)
        {
            RawText = text;
            Command = command;
            Parameters = parameters;
        }
    }

    public class VoiceResponseEventArgs : EventArgs
    {
        public string Text { get; }
        public VoiceResponseEventArgs(string text) => Text = text;
    }

    public class WakeWordEventArgs : EventArgs { }

    public class ListeningStateEventArgs : EventArgs
    {
        public bool IsListening { get; }
        public bool IsAwake { get; }
        public ListeningStateEventArgs(bool listening, bool awake)
        {
            IsListening = listening;
            IsAwake = awake;
        }
    }

    public class BIMActionEventArgs : EventArgs
    {
        public BIMAction Action { get; }
        public BIMActionEventArgs(BIMAction action) => Action = action;
    }

    #endregion
}
