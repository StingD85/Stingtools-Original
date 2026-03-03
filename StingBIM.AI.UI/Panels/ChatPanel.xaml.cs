// StingBIM.AI.UI.Panels.ChatPanel
// Main dockable chat interface for AI assistant
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NLog;
using StingBIM.AI.NLP.Dialogue;
using StingBIM.AI.NLP.Domain;
using StingBIM.AI.NLP.Voice;
using StingBIM.AI.UI.Controls;

namespace StingBIM.AI.UI.Panels
{
    /// <summary>
    /// Main chat panel for interacting with the AI assistant.
    /// Provides text and voice input with real-time responses.
    /// </summary>
    public partial class ChatPanel : Window
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConversationManager _conversationManager;
        private readonly SpeechRecognizer _speechRecognizer;
        private readonly ObservableCollection<ChatMessage> _messages;
        private readonly string _sessionId;

        private bool _isVoiceActive;
        private bool _isProcessing;
        private CancellationTokenSource _processingCts;

        /// <summary>
        /// Event fired when a command is ready to execute.
        /// </summary>
        public event EventHandler<DesignCommand> CommandExecute;

        /// <summary>
        /// Event fired when settings should be opened.
        /// </summary>
        public event EventHandler SettingsRequested;

        public ChatPanel(
            ConversationManager conversationManager,
            SpeechRecognizer speechRecognizer = null)
        {
            InitializeComponent();

            _conversationManager = conversationManager ?? throw new ArgumentNullException(nameof(conversationManager));
            _speechRecognizer = speechRecognizer;
            _messages = new ObservableCollection<ChatMessage>();
            _sessionId = Guid.NewGuid().ToString();

            // Bind messages to container
            MessagesContainer.ItemsSource = _messages;

            // Subscribe to events
            _conversationManager.CommandReady += OnCommandReady;

            if (_speechRecognizer != null)
            {
                _speechRecognizer.TranscriptionComplete += OnTranscriptionComplete;
                _speechRecognizer.SpeechDetected += OnSpeechDetected;
                _speechRecognizer.Error += OnSpeechError;
            }
            else
            {
                // Hide voice button if no speech recognizer
                VoiceButton.Visibility = Visibility.Collapsed;
            }

            // Setup input handling
            InputTextBox.TextChanged += InputTextBox_TextChanged;

            // Add welcome message
            AddWelcomeMessage();

            Logger.Info("ChatPanel initialized");
        }

        #region Message Handling

        private void AddWelcomeMessage()
        {
            AddAssistantMessage(
                "Hello! I'm your StingBIM AI assistant. I can help you design and modify your Revit model, " +
                "provide expert BIM consulting across 12 specialist domains, and offer intelligent " +
                "design analysis, optimization, and decision support.\n\n" +
                "Design & Modeling:\n" +
                "- \"Create a 4x5 meter bedroom\"\n" +
                "- \"Add a window to the south wall\"\n\n" +
                "Model Information:\n" +
                "- \"Generate BOQ\" (Bill of Quantities)\n" +
                "- \"Material takeoff\"\n" +
                "- \"What materials are used?\"\n" +
                "- \"Show parameters\"\n\n" +
                "BIM Consulting:\n" +
                "- \"What beam size for a 6m span?\"\n" +
                "- \"Recommend material for exterior walls\"\n\n" +
                "BIM Intelligence:\n" +
                "- \"Analyze my design\" - spatial quality & patterns\n" +
                "- \"Optimize this layout\" - multi-objective optimization\n" +
                "- \"Compare steel vs concrete\" - decision support\n" +
                "- \"What should I do next?\" - predictive guidance\n\n" +
                "Facilities Management:\n" +
                "- \"Generate a maintenance schedule\"\n" +
                "- \"Predict equipment failures\"\n\n" +
                "Collaboration:\n" +
                "- \"Get agent recommendations\"\n" +
                "- \"Negotiate design options\"\n\n" +
                "Or ask me anything — \"What is BIM?\", \"Help\"\n\n" +
                "How can I help you today?",
                suggestions: new[] { "Generate BOQ", "Analyze my design", "Help" });
        }

        private void AddUserMessage(string text)
        {
            var message = new ChatMessage
            {
                Text = text,
                IsUser = true,
                Timestamp = DateTime.Now
            };

            Dispatcher.Invoke(() =>
            {
                _messages.Add(message);
                ScrollToBottom();
            });
        }

        private void AddAssistantMessage(string text, IEnumerable<string> suggestions = null, List<DetailSection> detailSections = null)
        {
            var message = new ChatMessage
            {
                Text = text,
                IsUser = false,
                Timestamp = DateTime.Now,
                Suggestions = suggestions?.ToList(),
                DetailSections = detailSections
            };

            Dispatcher.Invoke(() =>
            {
                _messages.Add(message);
                UpdateSuggestions(suggestions);
                ScrollToBottom();
            });
        }

        private void AddErrorMessage(string error)
        {
            var message = new ChatMessage
            {
                Text = error,
                IsUser = false,
                IsError = true,
                Timestamp = DateTime.Now
            };

            Dispatcher.Invoke(() =>
            {
                _messages.Add(message);
                ScrollToBottom();
            });
        }

        private void UpdateSuggestions(IEnumerable<string> suggestions)
        {
            if (suggestions != null && suggestions.Any())
            {
                SuggestionsContainer.ItemsSource = suggestions;
                SuggestionsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                SuggestionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ScrollToBottom()
        {
            MessagesScrollViewer.ScrollToEnd();
        }

        #endregion

        #region Input Handling

        private async void SendMessage()
        {
            var text = InputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || _isProcessing)
                return;

            InputTextBox.Text = string.Empty;
            await ProcessUserInputAsync(text);
        }

        private async Task ProcessUserInputAsync(string input)
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            _processingCts = new CancellationTokenSource();

            try
            {
                // Show user message
                AddUserMessage(input);

                // Show processing state
                SetProcessingState(true, "Thinking...");

                // Process through conversation manager
                var response = await _conversationManager.ProcessMessageAsync(
                    _sessionId,
                    input,
                    _processingCts.Token);

                // Hide processing state
                SetProcessingState(false);

                // Convert response detail sections to UI detail sections
                List<DetailSection> uiSections = null;
                if (response.DetailSections != null && response.DetailSections.Count > 0)
                {
                    uiSections = response.DetailSections.Select(s => new DetailSection
                    {
                        Header = s.Header,
                        Summary = s.Summary,
                        Items = s.Items?.Select(i => ConvertDetailItem(i)).ToList() ?? new List<DetailItem>()
                    }).ToList();
                }

                // Show response
                AddAssistantMessage(response.Message, response.Suggestions, uiSections);

                // Handle action if present
                if (response.Action != null && response.Action.IsExecutable)
                {
                    CommandExecute?.Invoke(this, response.Action);
                }

                Logger.Info($"Processed input: {input} -> {response.ResponseType}");
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Processing cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing input");
                SetProcessingState(false);
                AddErrorMessage($"Sorry, I encountered an error: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                _processingCts?.Dispose();
                _processingCts = null;
            }
        }

        private void SetProcessingState(bool isProcessing, string message = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (isProcessing)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingText.Text = message ?? "Processing...";
                    LoadingIndicator.IsActive = true;
                    StatusText.Text = "Processing...";
                    InputTextBox.IsEnabled = false;
                    SendButton.IsEnabled = false;
                }
                else
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    LoadingIndicator.IsActive = false;
                    StatusText.Text = "Ready to help";
                    InputTextBox.IsEnabled = true;
                    UpdateSendButtonState();
                }
            });
        }

        private void UpdateSendButtonState()
        {
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputTextBox.Text) && !_isProcessing;
        }

        #endregion

        #region Voice Handling

        private void StartVoiceInput()
        {
            if (_speechRecognizer == null || _isVoiceActive)
                return;

            try
            {
                _isVoiceActive = true;
                _speechRecognizer.StartListening();

                // Update UI
                Dispatcher.Invoke(() =>
                {
                    VoiceIndicator.Visibility = Visibility.Visible;
                    StatusText.Text = "Listening...";

                    // Pulse animation
                    var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5))
                    {
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    VoiceIndicator.BeginAnimation(OpacityProperty, animation);
                });

                Logger.Info("Voice input started");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start voice input");
                _isVoiceActive = false;
            }
        }

        private void StopVoiceInput()
        {
            if (_speechRecognizer == null || !_isVoiceActive)
                return;

            try
            {
                _speechRecognizer.StopListening();
                _isVoiceActive = false;

                // Update UI
                Dispatcher.Invoke(() =>
                {
                    VoiceIndicator.BeginAnimation(OpacityProperty, null);
                    VoiceIndicator.Visibility = Visibility.Collapsed;
                    StatusText.Text = "Ready to help";
                });

                Logger.Info("Voice input stopped");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping voice input");
            }
        }

        private void OnSpeechDetected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "Speech detected...";
            });
        }

        private async void OnTranscriptionComplete(object sender, TranscriptionEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Text))
                return;

            Logger.Info($"Transcription: {e.Text} (confidence: {e.Confidence:P0})");

            // Process the transcribed text
            await ProcessUserInputAsync(e.Text);
        }

        private void OnSpeechError(object sender, SpeechErrorEventArgs e)
        {
            Logger.Error($"Speech error: {e.Error}");

            Dispatcher.Invoke(() =>
            {
                StopVoiceInput();
                StatusText.Text = "Voice input error";
            });
        }

        #endregion

        #region Event Handlers

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSendButtonState();

            // Hide placeholder when text is entered
            PlaceholderText.Visibility = string.IsNullOrEmpty(InputTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle voice input
            if (_isVoiceActive)
            {
                StopVoiceInput();
            }
            else
            {
                StartVoiceInput();
            }
        }

        private void DetailSection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DetailSection section)
            {
                section.IsExpanded = !section.IsExpanded;
            }
        }

        private void Suggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string suggestion)
            {
                InputTextBox.Text = suggestion;
                SendMessage();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);

            try
            {
                var settingsPanel = new SettingsPanel();
                settingsPanel.Owner = this;
                settingsPanel.ClearHistoryRequested += (s, args) => ClearHistory();
                settingsPanel.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open settings panel");
            }
        }

        private void QuickActionsButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the quick actions panel visibility
            if (QuickActionsPanel.Visibility == Visibility.Visible)
            {
                QuickActionsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                QuickActionsPanel.Visibility = Visibility.Visible;
            }
        }

        private void CategoryToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggle)
            {
                // Find the associated WrapPanel (next sibling in the stack)
                var parent = toggle.Parent as StackPanel;
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(toggle);
                    if (index >= 0 && index + 1 < parent.Children.Count)
                    {
                        var wrapPanel = parent.Children[index + 1] as WrapPanel;
                        if (wrapPanel != null)
                        {
                            wrapPanel.Visibility = toggle.IsChecked == true
                                ? Visibility.Visible
                                : Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string actionText)
            {
                InputTextBox.Text = actionText;
                SendMessage();
            }
        }

        private void QuickActionsPanel_ActionSelected(object sender, EventArgs e)
        {
            // Handle action selected from the popup quick actions panel
            QuickActionsPopup.IsOpen = false;
        }

        private void QuickActionsPanel_CloseRequested(object sender, EventArgs e)
        {
            QuickActionsPopup.IsOpen = false;
        }

        private void OnCommandReady(object sender, CommandReadyEventArgs e)
        {
            if (e.SessionId == _sessionId && e.Command != null)
            {
                CommandExecute?.Invoke(this, e.Command);
            }
        }

        private DetailItem ConvertDetailItem(StingBIM.AI.NLP.Domain.QueryDetailItem item)
        {
            return new DetailItem
            {
                Label = item.Label,
                Value = item.Value,
                Unit = item.Unit,
                SubItems = item.SubItems?.Select(si => ConvertDetailItem(si)).ToList()
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sends a message programmatically.
        /// </summary>
        public async Task SendMessageAsync(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                await ProcessUserInputAsync(message);
            }
        }

        /// <summary>
        /// Shows feedback for a command result.
        /// </summary>
        public void ShowCommandFeedback(bool success, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (success)
                {
                    AddAssistantMessage(message);
                    StatusText.Text = "Command completed";
                }
                else
                {
                    AddErrorMessage(message);
                    StatusText.Text = "Command failed";
                }
            });

            // Provide feedback to conversation manager
            _conversationManager.ProvideFeedback(_sessionId, new CommandFeedback
            {
                Success = success,
                ErrorMessage = success ? null : message
            });
        }

        /// <summary>
        /// Shows suggestion chips after a command result (e.g., "Add a door", "Create the next room").
        /// </summary>
        public void ShowSuggestionChips(List<string> suggestions)
        {
            if (suggestions == null || suggestions.Count == 0) return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Add a panel with suggestion buttons to the chat
                    var panel = new System.Windows.Controls.WrapPanel
                    {
                        Margin = new System.Windows.Thickness(50, 4, 10, 4)
                    };

                    foreach (var suggestion in suggestions)
                    {
                        var button = new Button
                        {
                            Content = suggestion,
                            Style = (Style)FindResource("SuggestionButtonStyle"),
                            Margin = new System.Windows.Thickness(0, 0, 6, 4)
                        };
                        button.Click += Suggestion_Click;
                        panel.Children.Add(button);
                    }

                    MessagesPanel.Children.Add(panel);
                    MessagesScroll.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Failed to show suggestion chips");
                }
            });
        }

        /// <summary>
        /// Clears the chat history.
        /// </summary>
        public void ClearHistory()
        {
            Dispatcher.Invoke(() =>
            {
                _messages.Clear();
                _conversationManager.ClearHistory(_sessionId);
                AddWelcomeMessage();
            });
        }

        /// <summary>
        /// Sets the current context (selected elements, level, etc.).
        /// </summary>
        public void SetContext(string contextInfo)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = contextInfo;
            });
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            _processingCts?.Cancel();
            _processingCts?.Dispose();

            if (_speechRecognizer != null)
            {
                _speechRecognizer.TranscriptionComplete -= OnTranscriptionComplete;
                _speechRecognizer.SpeechDetected -= OnSpeechDetected;
                _speechRecognizer.Error -= OnSpeechError;
                StopVoiceInput();
            }

            _conversationManager.CommandReady -= OnCommandReady;

            base.OnClosed(e);
            Logger.Info("ChatPanel closed");
        }

        #endregion
    }

    /// <summary>
    /// Represents a chat message with optional expandable detail sections.
    /// </summary>
    public class ChatMessage : System.ComponentModel.INotifyPropertyChanged
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }
        public bool IsError { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Suggestions { get; set; }
        public DesignCommand Command { get; set; }

        /// <summary>
        /// Expandable detail sections (e.g., BOQ line items, material details).
        /// Each section has a header the user can click to expand/collapse.
        /// </summary>
        public List<DetailSection> DetailSections { get; set; }

        /// <summary>
        /// Whether this message has expandable detail sections.
        /// </summary>
        public bool HasDetails => DetailSections != null && DetailSections.Count > 0;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// An expandable/collapsible section within a chat message.
    /// Used for BOQ sections, material lists, parameter details, etc.
    /// </summary>
    public class DetailSection : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Header { get; set; }
        public string Summary { get; set; }
        public List<DetailItem> Items { get; set; } = new List<DetailItem>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ExpandIcon)));
            }
        }

        public string ExpandIcon => IsExpanded ? "▼" : "▶";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// A single detail item within an expandable section.
    /// </summary>
    public class DetailItem
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public List<DetailItem> SubItems { get; set; }
        public bool HasSubItems => SubItems != null && SubItems.Count > 0;
    }
}
