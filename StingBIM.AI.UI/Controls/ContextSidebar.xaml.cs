// StingBIM.AI.UI.Controls.ContextSidebar
// Displays current Revit context, selection, and model statistics
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Sidebar panel that displays current Revit context information including
    /// active view, selection, model statistics, and command history.
    /// </summary>
    public partial class ContextSidebar : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ObservableCollection<RecentCommand> _recentCommands;
        private readonly ObservableCollection<UndoItem> _undoHistory;
        private readonly ObservableCollection<string> _selectionBreakdown;

        /// <summary>
        /// Event fired when a recent command is clicked for re-execution.
        /// </summary>
        public event EventHandler<string> CommandRequested;

        /// <summary>
        /// Event fired when an undo action is requested.
        /// </summary>
        public event EventHandler<UndoItem> UndoRequested;

        /// <summary>
        /// Event fired when clear selection is requested.
        /// </summary>
        public event EventHandler ClearSelectionRequested;

        /// <summary>
        /// Event fired when context refresh is requested.
        /// </summary>
        public event EventHandler RefreshRequested;

        public ContextSidebar()
        {
            InitializeComponent();

            _recentCommands = new ObservableCollection<RecentCommand>();
            _undoHistory = new ObservableCollection<UndoItem>();
            _selectionBreakdown = new ObservableCollection<string>();

            RecentCommandsList.ItemsSource = _recentCommands;
            UndoHistoryList.ItemsSource = _undoHistory;
            SelectionBreakdown.ItemsSource = _selectionBreakdown;

            UpdateEmptyStates();
        }

        #region Public Methods

        /// <summary>
        /// Updates the connection status display.
        /// </summary>
        public void SetConnectionStatus(bool isConnected, string statusText = null)
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionIndicator.Fill = isConnected
                    ? (Brush)FindResource("SuccessBrush")
                    : (Brush)FindResource("ErrorBrush");

                ConnectionStatusText.Text = statusText ?? (isConnected ? "Connected to Revit" : "Not connected");
            });
        }

        /// <summary>
        /// Updates the active view information.
        /// </summary>
        public void SetActiveView(string viewName, string viewType, string level)
        {
            Dispatcher.Invoke(() =>
            {
                ViewNameText.Text = viewName ?? "No active view";
                ViewTypeText.Text = viewType ?? "-";
                LevelText.Text = level ?? "-";
            });
        }

        /// <summary>
        /// Updates the selection information.
        /// </summary>
        public void SetSelection(int count, Dictionary<string, int> breakdown = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (count == 0)
                {
                    SelectionCountText.Text = "No selection";
                    ClearSelectionButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SelectionCountText.Text = count == 1
                        ? "1 element selected"
                        : $"{count} elements selected";
                    ClearSelectionButton.Visibility = Visibility.Visible;
                }

                _selectionBreakdown.Clear();
                if (breakdown != null)
                {
                    foreach (var kvp in breakdown.OrderByDescending(x => x.Value).Take(5))
                    {
                        _selectionBreakdown.Add($"{kvp.Key}: {kvp.Value}");
                    }
                }
            });
        }

        /// <summary>
        /// Updates the model statistics.
        /// </summary>
        public void SetModelStats(ModelStatistics stats)
        {
            Dispatcher.Invoke(() =>
            {
                WallCountText.Text = stats.WallCount.ToString("N0");
                DoorCountText.Text = stats.DoorCount.ToString("N0");
                WindowCountText.Text = stats.WindowCount.ToString("N0");
                RoomCountText.Text = stats.RoomCount.ToString("N0");
                LevelCountText.Text = stats.LevelCount.ToString("N0");
                SheetCountText.Text = stats.SheetCount.ToString("N0");
            });
        }

        /// <summary>
        /// Adds a command to the recent commands list.
        /// </summary>
        public void AddRecentCommand(string command, string displayText = null)
        {
            Dispatcher.Invoke(() =>
            {
                // Remove if already exists
                var existing = _recentCommands.FirstOrDefault(c => c.Command == command);
                if (existing != null)
                {
                    _recentCommands.Remove(existing);
                }

                // Add at beginning
                _recentCommands.Insert(0, new RecentCommand
                {
                    Command = command,
                    DisplayText = displayText ?? TruncateCommand(command),
                    Timestamp = DateTime.Now
                });

                // Keep only last 5
                while (_recentCommands.Count > 5)
                {
                    _recentCommands.RemoveAt(_recentCommands.Count - 1);
                }

                UpdateEmptyStates();
            });
        }

        /// <summary>
        /// Adds an item to the undo history.
        /// </summary>
        public void AddUndoItem(string description, object data = null)
        {
            Dispatcher.Invoke(() =>
            {
                _undoHistory.Insert(0, new UndoItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Description = description,
                    Data = data,
                    Timestamp = DateTime.Now
                });

                // Keep only last 10
                while (_undoHistory.Count > 10)
                {
                    _undoHistory.RemoveAt(_undoHistory.Count - 1);
                }

                UpdateEmptyStates();
            });
        }

        /// <summary>
        /// Clears the undo history.
        /// </summary>
        public void ClearUndoHistory()
        {
            Dispatcher.Invoke(() =>
            {
                _undoHistory.Clear();
                UpdateEmptyStates();
            });
        }

        /// <summary>
        /// Clears all context data (e.g., when document closes).
        /// </summary>
        public void ClearAll()
        {
            Dispatcher.Invoke(() =>
            {
                SetConnectionStatus(false);
                SetActiveView(null, null, null);
                SetSelection(0);
                SetModelStats(new ModelStatistics());
                _recentCommands.Clear();
                _undoHistory.Clear();
                _selectionBreakdown.Clear();
                UpdateEmptyStates();
            });
        }

        #endregion

        #region Private Methods

        private void UpdateEmptyStates()
        {
            NoRecentCommandsText.Visibility = _recentCommands.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            NoUndoHistoryText.Visibility = _undoHistory.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static string TruncateCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return string.Empty;
            return command.Length > 30 ? command.Substring(0, 30) + "..." : command;
        }

        #endregion

        #region Event Handlers

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
            Logger.Debug("Context refresh requested");
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelectionRequested?.Invoke(this, EventArgs.Empty);
            Logger.Debug("Clear selection requested");
        }

        private void RecentCommand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string command)
            {
                CommandRequested?.Invoke(this, command);
                Logger.Debug($"Recent command requested: {command}");
            }
        }

        private void UndoItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UndoItem item)
            {
                UndoRequested?.Invoke(this, item);
                _undoHistory.Remove(item);
                UpdateEmptyStates();
                Logger.Debug($"Undo requested: {item.Description}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a recent command in the sidebar.
    /// </summary>
    public class RecentCommand
    {
        public string Command { get; set; }
        public string DisplayText { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents an undoable action.
    /// </summary>
    public class UndoItem
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Model statistics for display in the context sidebar.
    /// </summary>
    public class ModelStatistics
    {
        public int WallCount { get; set; }
        public int DoorCount { get; set; }
        public int WindowCount { get; set; }
        public int RoomCount { get; set; }
        public int LevelCount { get; set; }
        public int SheetCount { get; set; }
    }
}
