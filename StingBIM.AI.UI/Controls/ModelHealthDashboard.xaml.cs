// StingBIM.AI.UI.Controls.ModelHealthDashboard
// Model health overview and issue tracking dashboard
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Dashboard showing model health metrics and issues.
    /// </summary>
    public partial class ModelHealthDashboard : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private ObservableCollection<ModelIssue> _allIssues;
        private ObservableCollection<ModelIssue> _filteredIssues;
        private string _currentFilter = "All";
        private DateTime _lastChecked;

        /// <summary>
        /// Event fired when an issue is selected.
        /// </summary>
        public event EventHandler<ModelIssue> IssueSelected;

        /// <summary>
        /// Event fired when fix is requested for an issue.
        /// </summary>
        public event EventHandler<ModelIssue> FixRequested;

        /// <summary>
        /// Event fired when fix all is requested.
        /// </summary>
        public event EventHandler FixAllRequested;

        /// <summary>
        /// Event fired when refresh is requested.
        /// </summary>
        public event EventHandler RefreshRequested;

        /// <summary>
        /// Event fired when export is requested.
        /// </summary>
        public event EventHandler ExportRequested;

        /// <summary>
        /// Event fired when close is requested.
        /// </summary>
        public event EventHandler CloseRequested;

        public ModelHealthDashboard()
        {
            InitializeComponent();

            _allIssues = new ObservableCollection<ModelIssue>();
            _filteredIssues = new ObservableCollection<ModelIssue>();
            IssuesList.ItemsSource = _filteredIssues;

            _lastChecked = DateTime.Now;

            // Load sample data for demonstration
            LoadSampleData();
            UpdateDisplay();
        }

        #region Public Methods

        /// <summary>
        /// Updates the dashboard with new health data.
        /// </summary>
        public void UpdateHealth(ModelHealthData health)
        {
            _allIssues.Clear();
            foreach (var issue in health.Issues)
            {
                _allIssues.Add(issue);
            }

            _lastChecked = DateTime.Now;

            UpdateDisplay();
            UpdateMetrics(health);
            FilterIssues();

            Logger.Info($"Model health updated: Score={health.Score}, Issues={health.Issues.Count}");
        }

        /// <summary>
        /// Adds an issue to the dashboard.
        /// </summary>
        public void AddIssue(ModelIssue issue)
        {
            _allIssues.Add(issue);
            FilterIssues();
            UpdateMetrics();
        }

        /// <summary>
        /// Removes an issue from the dashboard.
        /// </summary>
        public void RemoveIssue(string issueId)
        {
            var issue = _allIssues.FirstOrDefault(i => i.Id == issueId);
            if (issue != null)
            {
                _allIssues.Remove(issue);
                FilterIssues();
                UpdateMetrics();
            }
        }

        /// <summary>
        /// Clears all issues.
        /// </summary>
        public void ClearIssues()
        {
            _allIssues.Clear();
            _filteredIssues.Clear();
            UpdateMetrics();
            UpdateDisplay();
        }

        #endregion

        #region Private Methods

        private void LoadSampleData()
        {
            _allIssues = new ObservableCollection<ModelIssue>
            {
                new ModelIssue
                {
                    Id = "1",
                    Title = "Duplicate room boundaries",
                    Description = "Found overlapping room separation lines in Level 1",
                    Severity = IssueSeverity.Warning,
                    Category = "Rooms",
                    AffectedCount = 3,
                    CanAutoFix = true
                },
                new ModelIssue
                {
                    Id = "2",
                    Title = "Missing fire rating parameter",
                    Description = "Fire rating not specified for exterior doors",
                    Severity = IssueSeverity.Warning,
                    Category = "Parameters",
                    AffectedCount = 8,
                    CanAutoFix = false
                },
                new ModelIssue
                {
                    Id = "3",
                    Title = "Wall height inconsistency",
                    Description = "Interior walls have varying heights on same level",
                    Severity = IssueSeverity.Info,
                    Category = "Walls",
                    AffectedCount = 5,
                    CanAutoFix = true
                },
                new ModelIssue
                {
                    Id = "4",
                    Title = "Unconnected ducts",
                    Description = "HVAC ducts not connected to diffusers",
                    Severity = IssueSeverity.Error,
                    Category = "MEP",
                    AffectedCount = 2,
                    CanAutoFix = false
                }
            };
        }

        private void UpdateDisplay()
        {
            LastCheckedText.Text = $"Last checked: {GetRelativeTime(_lastChecked)}";
        }

        private void UpdateMetrics(ModelHealthData health = null)
        {
            var errors = _allIssues.Count(i => i.Severity == IssueSeverity.Error);
            var warnings = _allIssues.Count(i => i.Severity == IssueSeverity.Warning);
            var info = _allIssues.Count(i => i.Severity == IssueSeverity.Info);

            ErrorCountText.Text = errors.ToString();
            WarningCountText.Text = warnings.ToString();

            // Calculate score (simple formula)
            var score = Math.Max(0, 100 - (errors * 10) - (warnings * 3) - info);
            ScoreText.Text = score.ToString();

            // Update score ring color
            if (score >= 80)
            {
                ScoreRing.Stroke = (Brush)FindResource("SuccessBrush");
            }
            else if (score >= 50)
            {
                ScoreRing.Stroke = (Brush)FindResource("WarningBrush");
            }
            else
            {
                ScoreRing.Stroke = (Brush)FindResource("ErrorBrush");
            }

            // Update score ring progress (simple approximation)
            ScoreRing.StrokeDashArray = new DoubleCollection { score * 2.5, 250 };

            if (health != null)
            {
                MissingParamsText.Text = health.MissingParameters.ToString();
                ElementCountText.Text = health.TotalElements.ToString("N0");
            }

            // Update empty state
            NoIssuesState.Visibility = _filteredIssues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FilterIssues()
        {
            _filteredIssues.Clear();

            var filtered = _allIssues.AsEnumerable();

            if (_currentFilter != "All")
            {
                var severity = _currentFilter switch
                {
                    "Error" => IssueSeverity.Error,
                    "Warning" => IssueSeverity.Warning,
                    "Info" => IssueSeverity.Info,
                    _ => IssueSeverity.Info
                };
                filtered = filtered.Where(i => i.Severity == severity);
            }

            foreach (var issue in filtered.OrderByDescending(i => i.Severity))
            {
                _filteredIssues.Add(issue);
            }

            NoIssuesState.Visibility = _filteredIssues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string GetRelativeTime(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;

            if (span.TotalSeconds < 30) return "Just now";
            if (span.TotalMinutes < 1) return $"{(int)span.TotalSeconds}s ago";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";

            return dateTime.ToString("MMM d, HH:mm");
        }

        #endregion

        #region Event Handlers

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
            _lastChecked = DateTime.Now;
            UpdateDisplay();
        }

        private void IssueFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is string filter)
            {
                _currentFilter = filter;
                FilterIssues();
            }
        }

        private void Issue_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ModelIssue issue)
            {
                IssueSelected?.Invoke(this, issue);
            }
        }

        private void FixIssue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModelIssue issue)
            {
                FixRequested?.Invoke(this, issue);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ExportRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FixAllButton_Click(object sender, RoutedEventArgs e)
        {
            FixAllRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Model health summary data.
    /// </summary>
    public class ModelHealthData
    {
        public int Score { get; set; } = 100;
        public int TotalElements { get; set; }
        public int MissingParameters { get; set; }
        public List<ModelIssue> Issues { get; set; } = new List<ModelIssue>();
    }

    /// <summary>
    /// A model issue or warning.
    /// </summary>
    public class ModelIssue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Category { get; set; }
        public int AffectedCount { get; set; }
        public bool CanAutoFix { get; set; }
        public List<int> AffectedElementIds { get; set; } = new List<int>();

        public Brush SeverityBackground => Severity switch
        {
            IssueSeverity.Error => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
            IssueSeverity.Warning => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
            IssueSeverity.Info => new SolidColorBrush(Color.FromRgb(219, 234, 254)),
            _ => new SolidColorBrush(Color.FromRgb(229, 231, 235))
        };

        public Brush SeverityForeground => Severity switch
        {
            IssueSeverity.Error => new SolidColorBrush(Color.FromRgb(185, 28, 28)),
            IssueSeverity.Warning => new SolidColorBrush(Color.FromRgb(146, 64, 14)),
            IssueSeverity.Info => new SolidColorBrush(Color.FromRgb(30, 64, 175)),
            _ => new SolidColorBrush(Color.FromRgb(75, 85, 99))
        };

        public string SeverityIcon => Severity switch
        {
            IssueSeverity.Error => "M13,13H11V7H13M13,17H11V15H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z",
            IssueSeverity.Warning => "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16",
            IssueSeverity.Info => "M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z",
            _ => "M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"
        };
    }

    /// <summary>
    /// Issue severity levels.
    /// </summary>
    public enum IssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    #endregion
}
