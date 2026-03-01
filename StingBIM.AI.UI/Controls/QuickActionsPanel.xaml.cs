// StingBIM.AI.UI.Controls.QuickActionsPanel
// Quick actions panel with common BIM commands
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// A panel displaying quick action buttons for common BIM operations.
    /// Allows users to quickly execute commands without typing.
    /// </summary>
    public partial class QuickActionsPanel : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Event fired when a quick action is selected.
        /// </summary>
        public event EventHandler<QuickActionEventArgs> ActionSelected;

        /// <summary>
        /// Event fired when the panel should be closed.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>
        /// Gets or sets the list of available actions.
        /// </summary>
        public List<QuickAction> Actions { get; set; }

        public QuickActionsPanel()
        {
            InitializeComponent();
            Actions = GetDefaultActions();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string command)
            {
                Logger.Debug($"Quick action selected: {command}");

                ActionSelected?.Invoke(this, new QuickActionEventArgs
                {
                    Command = command,
                    Action = Actions.FirstOrDefault(a => a.Command == command)
                });
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLowerInvariant() ?? string.Empty;

            // Filter visible buttons based on search
            FilterButtons(CreatePanel, searchText);
            FilterButtons(AnalyzePanel, searchText);
            FilterButtons(ParametersPanel, searchText);
            FilterButtons(SchedulesPanel, searchText);
        }

        private void FilterButtons(WrapPanel panel, string searchText)
        {
            foreach (var child in panel.Children)
            {
                if (child is Button button)
                {
                    var tag = button.Tag?.ToString()?.ToLowerInvariant() ?? string.Empty;
                    var content = GetButtonText(button).ToLowerInvariant();

                    var matches = string.IsNullOrEmpty(searchText) ||
                                  tag.Contains(searchText) ||
                                  content.Contains(searchText);

                    button.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private string GetButtonText(Button button)
        {
            if (button.Content is StackPanel stack)
            {
                var textBlock = stack.Children.OfType<TextBlock>().FirstOrDefault();
                return textBlock?.Text ?? string.Empty;
            }
            return button.Content?.ToString() ?? string.Empty;
        }

        private List<QuickAction> GetDefaultActions()
        {
            return new List<QuickAction>
            {
                // Create actions
                new QuickAction
                {
                    Command = "Create a wall",
                    Category = "Create",
                    Icon = "Wall",
                    Description = "Create a new wall element"
                },
                new QuickAction
                {
                    Command = "Create a floor",
                    Category = "Create",
                    Icon = "Floor",
                    Description = "Create a new floor element"
                },
                new QuickAction
                {
                    Command = "Create a room",
                    Category = "Create",
                    Icon = "Room",
                    Description = "Create a new room"
                },
                new QuickAction
                {
                    Command = "Add a door",
                    Category = "Create",
                    Icon = "Door",
                    Description = "Add a door to a wall"
                },
                new QuickAction
                {
                    Command = "Add a window",
                    Category = "Create",
                    Icon = "Window",
                    Description = "Add a window to a wall"
                },

                // Analyze actions
                new QuickAction
                {
                    Command = "Check code compliance",
                    Category = "Analyze",
                    Icon = "Check",
                    Description = "Check model against building codes"
                },
                new QuickAction
                {
                    Command = "Analyze spatial layout",
                    Category = "Analyze",
                    Icon = "Spatial",
                    Description = "Analyze room layouts and circulation"
                },
                new QuickAction
                {
                    Command = "Review MEP coordination",
                    Category = "Analyze",
                    Icon = "MEP",
                    Description = "Review MEP system coordination"
                },
                new QuickAction
                {
                    Command = "Calculate areas",
                    Category = "Analyze",
                    Icon = "Calculator",
                    Description = "Calculate room and floor areas"
                },

                // Parameter actions
                new QuickAction
                {
                    Command = "Show element parameters",
                    Category = "Parameters",
                    Icon = "Info",
                    Description = "Display parameters of selected element"
                },
                new QuickAction
                {
                    Command = "Set parameter value",
                    Category = "Parameters",
                    Icon = "Edit",
                    Description = "Set a parameter value on selected elements"
                },
                new QuickAction
                {
                    Command = "Apply material",
                    Category = "Parameters",
                    Icon = "Material",
                    Description = "Apply a material to selected elements"
                },

                // Schedule actions
                new QuickAction
                {
                    Command = "Create door schedule",
                    Category = "Schedules",
                    Icon = "Schedule",
                    Description = "Generate a door schedule"
                },
                new QuickAction
                {
                    Command = "Create room schedule",
                    Category = "Schedules",
                    Icon = "Schedule",
                    Description = "Generate a room schedule"
                },
                new QuickAction
                {
                    Command = "Create wall schedule",
                    Category = "Schedules",
                    Icon = "Schedule",
                    Description = "Generate a wall schedule"
                }
            };
        }
    }

    /// <summary>
    /// Represents a quick action command.
    /// </summary>
    public class QuickAction
    {
        public string Command { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public string Shortcut { get; set; }
        public bool RequiresSelection { get; set; }
    }

    /// <summary>
    /// Event arguments for quick action selection.
    /// </summary>
    public class QuickActionEventArgs : EventArgs
    {
        public string Command { get; set; }
        public QuickAction Action { get; set; }
    }
}
