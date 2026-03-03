// ============================================================================
// StingBIM AI - Chat Panel Dockable Control
// Full-featured AI assistant with 4 command phases:
//   Phase 1: Creation (walls, rooms, floors)
//   Phase 2: Analysis (compliance, materials, QA, coordination, BOQ)
//   Phase 3: Intelligence (parameters, formulas, optimization, agents)
//   Phase 4: Advanced (generative, construction, tagging, energy, standards)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using NLog;

namespace StingBIM.Revit.UI
{
    public partial class ChatPanelControl : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<ChatEntry> _history = new();
        private readonly ChatCommandRouter _router = new();

        // ExternalEvent for creation commands that modify the document
        private RevitCommandHandler _commandHandler;
        private ExternalEvent _externalEvent;

        /// <summary>
        /// Set by the Revit application to provide access to the active document.
        /// </summary>
        internal Func<Document> GetActiveDocument { get; set; }

        /// <summary>
        /// Must be called after construction to set up the ExternalEvent.
        /// Called from StingBIMApplication.RegisterDockablePanes().
        /// </summary>
        internal void InitializeExternalEvent(RevitCommandHandler handler, ExternalEvent externalEvent)
        {
            _commandHandler = handler;
            _externalEvent = externalEvent;

            // Listen for command completion
            _commandHandler.CommandCompleted += OnCommandCompleted;
            _commandHandler.CommandFailed += OnCommandFailed;
        }

        public ChatPanelControl()
        {
            InitializeComponent();
            AddAssistantMessage(
                "Welcome to StingBIM AI v7.0!\n\n" +
                "I can help you design and analyze your\nRevit model. Try these commands:\n\n" +
                "  Create elements:\n" +
                "    \"create wall 5m\"\n" +
                "    \"create bedroom 4x5\"\n\n" +
                "  Analyze & comply:\n" +
                "    \"check compliance IBC\"\n" +
                "    \"check health\"\n\n" +
                "  Calculate:\n" +
                "    \"calculate cable size 240V 30A\"\n" +
                "    \"design beam 6m span 15kn\"\n\n" +
                "  Query model:\n" +
                "    \"model summary\" \"walls\" \"rooms\"\n\n" +
                "  AI intelligence:\n" +
                "    \"ask experts\" \"optimize cost\"\n\n" +
                "Type \"help\" for full command list\n" +
                "or use the quick action buttons below.");
        }

        #region Message Display

        private void AddUserMessage(string text)
        {
            _history.Add(new ChatEntry { IsUser = true, Text = text, Timestamp = DateTime.Now });
            var bubble = CreateBubble(text, isUser: true);
            MessagesPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        private void AddAssistantMessage(string text)
        {
            _history.Add(new ChatEntry { IsUser = false, Text = text, Timestamp = DateTime.Now });
            var bubble = CreateBubble(text, isUser: false);
            MessagesPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        private Border CreateBubble(string text, bool isUser)
        {
            var bubble = new Border
            {
                Background = isUser
                    ? (Brush)FindResource("UserBubbleBrush")
                    : (Brush)FindResource("AssistantBubbleBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = isUser
                    ? new Thickness(60, 4, 8, 4)
                    : new Thickness(8, 4, 40, 4),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 380
            };

            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isUser ? Brushes.White : (Brush)FindResource("FgBrush"),
                FontSize = 12,
                LineHeight = 18,
                FontFamily = new FontFamily("Consolas, Courier New, monospace")
            };

            bubble.Child = textBlock;
            return bubble;
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                MessagesScrollViewer.ScrollToEnd();
            });
        }

        #endregion

        #region Input Handling

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                ProcessInput();
            }
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrWhiteSpace(InputTextBox.Text);
            SendButton.IsEnabled = hasText;
            PlaceholderText.Visibility = string.IsNullOrEmpty(InputTextBox.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) => ProcessInput();

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            MessagesPanel.Children.Clear();
            _history.Clear();
            AddAssistantMessage(
                "Chat cleared. How can I help?\n\n" +
                "Try: \"create wall 5m\", \"check compliance\",\n" +
                "\"model summary\", \"rooms\", or \"help\"");
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string action)
            {
                AddUserMessage(action);
                ProcessCommand(action);
            }
        }

        private void ProcessInput()
        {
            var text = InputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            InputTextBox.Text = string.Empty;
            AddUserMessage(text);

            var command = text.ToLowerInvariant().Trim();
            ProcessCommand(command);
        }

        #endregion

        #region Command Processing

        private void ProcessCommand(string command)
        {
            StatusText.Text = "Processing...";

            try
            {
                var doc = GetActiveDocument?.Invoke();

                // Try the comprehensive router first (handles all 4 phases)
                var routeResult = _router.Route(command, doc);

                if (routeResult != null)
                {
                    if (routeResult.IsCreationCommand)
                    {
                        // Creation command — needs ExternalEvent for Revit transactions
                        HandleCreationCommand(routeResult);
                    }
                    else
                    {
                        // Analysis/Intelligence/Advanced — response ready
                        AddAssistantMessage(routeResult.ResponseMessage);
                        StatusText.Text = "Ready";
                    }
                    StatusBarText.Text = $"StingBIM AI v7.0 | {DateTime.Now:HH:mm:ss}";
                    return;
                }

                // Help command works without a document
                if (command.Contains("help"))
                {
                    AddAssistantMessage(GetHelpText());
                    StatusText.Text = "Ready";
                    StatusBarText.Text = $"StingBIM AI v7.0 | {DateTime.Now:HH:mm:ss}";
                    return;
                }

                // Fallback to basic query handlers
                if (doc == null)
                {
                    AddAssistantMessage(
                        "No active document. Open a Revit model first.\n\n" +
                        "Once a model is open, you can:\n" +
                        "  - Create elements (walls, rooms, floors)\n" +
                        "  - Analyze compliance and quality\n" +
                        "  - Run engineering calculations\n" +
                        "  - Query model data\n\n" +
                        "Engineering calculations work without a\n" +
                        "model: \"calculate cable size 240V 30A\"");
                    StatusText.Text = "No document";
                    return;
                }

                string response = command switch
                {
                    var c when c.Contains("summary") || c.Contains("overview") => GetModelSummary(doc),
                    var c when c.Contains("count") => GetElementCounts(doc),
                    var c when c.Contains("level") => GetLevels(doc),
                    var c when c.Contains("area") => GetAreas(doc),
                    _ => GetFallbackResponse(command)
                };

                AddAssistantMessage(response);
                StatusText.Text = "Ready";
                StatusBarText.Text = $"StingBIM AI v7.0 | {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error: {command}");
                AddAssistantMessage($"Error: {ex.Message}");
                StatusText.Text = "Error";
            }
        }

        private void HandleCreationCommand(CommandRouteResult result)
        {
            if (_commandHandler == null || _externalEvent == null)
            {
                AddAssistantMessage(
                    "Creation commands require the ExternalEvent\n" +
                    "handler. Please restart Revit and try again.");
                return;
            }

            // Show pending message
            AddAssistantMessage(result.PendingMessage);
            StatusText.Text = "Creating...";

            // Queue the command and raise the ExternalEvent
            _commandHandler.QueueCommand(result.CreationCommand);
            _externalEvent.Raise();
        }

        private void OnCommandCompleted(string result)
        {
            Dispatcher.Invoke(() =>
            {
                AddAssistantMessage(result);
                StatusText.Text = "Ready";
            });
        }

        private void OnCommandFailed(string error)
        {
            Dispatcher.Invoke(() =>
            {
                AddAssistantMessage($"Creation failed: {error}");
                StatusText.Text = "Error";
            });
        }

        #endregion

        #region Basic Query Handlers (fallback)

        private string GetModelSummary(Document doc)
        {
            var summary = Commands.RevitModelQuery.GetCategorySummary(doc);
            var total = summary.Values.Sum();
            var warnings = Commands.RevitModelQuery.GetWarningCount(doc);

            if (total == 0)
                return $"Model: {doc.Title}\n\nNo building elements yet.";

            var r = $"Model: {doc.Title}\nElements: {total} | Warnings: {warnings}\n\n";
            foreach (var kvp in summary.OrderByDescending(x => x.Value))
                r += $"  {kvp.Key,-18} {kvp.Value,4}\n";

            var floorArea = Commands.RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Floors);
            if (floorArea > 0)
                r += $"\nFloor area: {floorArea:F1} m\u00B2";
            return r;
        }

        private string GetElementCounts(Document doc)
        {
            var summary = Commands.RevitModelQuery.GetCategorySummary(doc);
            if (summary.Count == 0)
                return "No building elements found.";

            var r = "Element Counts:\n\n";
            foreach (var kvp in summary.OrderByDescending(x => x.Value))
                r += $"  {kvp.Key,-18} {kvp.Value,5}\n";
            r += $"\n  {"TOTAL",-18} {summary.Values.Sum(),5}";
            return r;
        }

        private string GetLevels(Document doc)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level)).ToElements();
            if (levels.Count == 0) return "No levels found.";

            var r = $"Levels: {levels.Count}\n\n";
            foreach (var elem in levels.OrderBy(l => ((Level)l).Elevation))
            {
                var level = (Level)elem;
                r += $"  {level.Name}: {level.Elevation * 0.3048:F2} m\n";
            }
            return r;
        }

        private string GetAreas(Document doc)
        {
            var floorArea = Commands.RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Floors);
            var wallArea = Commands.RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Walls);
            if (floorArea > 0 || wallArea > 0)
                return $"Areas:\n  Floor: {floorArea:F1} m\u00B2\n  Wall: {wallArea:F1} m\u00B2";
            return "No floor or wall elements for area calc.";
        }

        private string GetHelpText()
        {
            return "StingBIM AI Commands\n\n" +
                   "CREATE (elements in your model):\n" +
                   "  create wall [length] [height]\n" +
                   "  create floor [width x depth]\n" +
                   "  create room/bedroom/kitchen/office...\n" +
                   "  create bedroom 4x5\n" +
                   "  auto populate parameters\n\n" +
                   "ANALYZE (read-only checks):\n" +
                   "  check compliance [IBC/NFPA/ADA/ASHRAE]\n" +
                   "  check compliance KEBS/UNBS/TBS/EAS\n" +
                   "  recommend material for [element]\n" +
                   "  run quality check\n" +
                   "  analyze coordination / clash\n" +
                   "  create boq / quantity takeoff\n\n" +
                   "CALCULATE (standards):\n" +
                   "  calculate cable size 240V 30A 50m\n" +
                   "  calculate cooling load 100m2\n" +
                   "  calculate ventilation 50m2\n" +
                   "  calculate lighting 30m2\n" +
                   "  calculate pipe size 0.5 lps\n" +
                   "  design sprinkler 200m2\n" +
                   "  design beam 6m span 15kn\n" +
                   "  estimate energy 500m2\n\n" +
                   "INTELLIGENCE:\n" +
                   "  suggest parameters for [category]\n" +
                   "  create formula [expr = ...]\n" +
                   "  optimize [cost/energy/structural]\n" +
                   "  ask experts / review design\n" +
                   "  compare / decision support\n\n" +
                   "ADVANCED:\n" +
                   "  generate design\n" +
                   "  construction schedule\n" +
                   "  analyze energy\n" +
                   "  tag placement\n\n" +
                   "QUERIES:\n" +
                   "  model summary / count elements\n" +
                   "  walls / doors / windows / rooms\n" +
                   "  levels / area / check health";
        }

        private string GetFallbackResponse(string input)
        {
            // Try to suggest similar commands based on keywords in input
            var suggestions = new List<string>();

            if (input.Contains("wall") || input.Contains("build"))
                suggestions.Add("  \"create wall 5m\"");
            if (input.Contains("room") || input.Contains("bed") || input.Contains("kitch"))
                suggestions.Add("  \"create bedroom 4x5\"");
            if (input.Contains("floor") || input.Contains("slab"))
                suggestions.Add("  \"create floor 6x8\"");
            if (input.Contains("check") || input.Contains("code") || input.Contains("standard"))
                suggestions.Add("  \"check compliance IBC\"");
            if (input.Contains("cable") || input.Contains("electric") || input.Contains("wire"))
                suggestions.Add("  \"calculate cable size 240V 30A\"");
            if (input.Contains("cool") || input.Contains("hvac") || input.Contains("air"))
                suggestions.Add("  \"calculate cooling load 100m2\"");
            if (input.Contains("pipe") || input.Contains("plumb") || input.Contains("water"))
                suggestions.Add("  \"calculate pipe size 0.5 lps\"");
            if (input.Contains("beam") || input.Contains("struct"))
                suggestions.Add("  \"design beam 6m span 15kn\"");
            if (input.Contains("review") || input.Contains("expert") || input.Contains("agent"))
                suggestions.Add("  \"ask experts review design\"");
            if (input.Contains("material"))
                suggestions.Add("  \"recommend material for walls\"");
            if (input.Contains("health") || input.Contains("quality"))
                suggestions.Add("  \"check health\" or \"run quality check\"");

            if (suggestions.Count > 0)
            {
                return $"I'm not sure what you mean by \"{input}\".\n\n" +
                       "Did you mean:\n" +
                       string.Join("\n", suggestions) + "\n\n" +
                       "Type \"help\" for all commands.";
            }

            return $"I don't recognize: \"{input}\"\n\n" +
                   "Try these commands:\n" +
                   "  \"create wall 5m\" - create elements\n" +
                   "  \"check compliance IBC\" - code checks\n" +
                   "  \"calculate cable size 240V 30A\"\n" +
                   "  \"model summary\" - view model data\n" +
                   "  \"walls\" \"rooms\" - query elements\n" +
                   "  \"check health\" - model diagnostics\n" +
                   "  \"ask experts\" - AI design review\n\n" +
                   "Type \"help\" for all commands.";
        }

        #endregion
    }

    internal class ChatEntry
    {
        public bool IsUser { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
