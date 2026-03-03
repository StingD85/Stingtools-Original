// StingBIM.AI.UI.Controls.CommandPalette
// Ctrl+K style command launcher with fuzzy search
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
    /// Command palette for quick access to all commands via fuzzy search.
    /// </summary>
    public partial class CommandPalette : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private List<PaletteCommand> _allCommands;
        private List<PaletteCommand> _recentCommands;
        private ObservableCollection<PaletteCommand> _filteredCommands;
        private string _currentCategory = "All";

        /// <summary>
        /// Event fired when a command is selected.
        /// </summary>
        public event EventHandler<PaletteCommand> CommandSelected;

        /// <summary>
        /// Event fired when the palette should close.
        /// </summary>
        public event EventHandler CloseRequested;

        public CommandPalette()
        {
            InitializeComponent();

            _allCommands = new List<PaletteCommand>();
            _recentCommands = new List<PaletteCommand>();
            _filteredCommands = new ObservableCollection<PaletteCommand>();

            CommandsList.ItemsSource = _filteredCommands;

            LoadDefaultCommands();
            UpdateFilter();

            Loaded += (s, e) =>
            {
                SearchTextBox.Focus();
                Keyboard.Focus(SearchTextBox);
            };
        }

        #region Public Methods

        /// <summary>
        /// Shows the command palette and focuses the search box.
        /// </summary>
        public void Show()
        {
            Visibility = Visibility.Visible;
            SearchTextBox.Clear();
            SearchTextBox.Focus();
            UpdateFilter();
        }

        /// <summary>
        /// Hides the command palette.
        /// </summary>
        public void Hide()
        {
            Visibility = Visibility.Collapsed;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Adds a command to the palette.
        /// </summary>
        public void AddCommand(PaletteCommand command)
        {
            _allCommands.Add(command);
            UpdateFilter();
        }

        /// <summary>
        /// Adds a command to recent history.
        /// </summary>
        public void AddToRecent(PaletteCommand command)
        {
            _recentCommands.Remove(command);
            _recentCommands.Insert(0, command);

            // Keep only last 10 recent commands
            while (_recentCommands.Count > 10)
            {
                _recentCommands.RemoveAt(_recentCommands.Count - 1);
            }
        }

        /// <summary>
        /// Clears the search and resets the view.
        /// </summary>
        public void Reset()
        {
            SearchTextBox.Clear();
            AllTab.IsChecked = true;
            _currentCategory = "All";
            UpdateFilter();
        }

        #endregion

        #region Private Methods

        private void LoadDefaultCommands()
        {
            var accentBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            var successBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            var warningBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8));
            var infoBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
            var purpleBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247));

            _allCommands = new List<PaletteCommand>
            {
                // Create Commands
                new PaletteCommand
                {
                    Id = "create-wall",
                    Name = "Create Wall",
                    Description = "Create a new wall element",
                    Category = "Create",
                    Command = "Create a wall",
                    Shortcut = "Ctrl+Shift+W",
                    IconPath = "M3,16H12V21H3V16M2,10H8V15H2V10M9,10H15V15H9V10M16,10H22V15H16V10M13,16H22V21H13V16M3,4H22V9H3V4Z",
                    IconBackground = accentBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "create-door",
                    Name = "Create Door",
                    Description = "Insert a door into a wall",
                    Category = "Create",
                    Command = "Create a door",
                    IconPath = "M12,3L2,12H5V20H19V12H22L12,3M12,8.75A2.25,2.25 0 0,1 14.25,11A2.25,2.25 0 0,1 12,13.25A2.25,2.25 0 0,1 9.75,11A2.25,2.25 0 0,1 12,8.75Z",
                    IconBackground = accentBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "create-window",
                    Name = "Create Window",
                    Description = "Insert a window into a wall",
                    Category = "Create",
                    Command = "Create a window",
                    IconPath = "M3,2H21A1,1 0 0,1 22,3V21A1,1 0 0,1 21,22H3A1,1 0 0,1 2,21V3A1,1 0 0,1 3,2M11,4V11H4V4H11M4,20H11V13H4V20M20,20V13H13V20H20M20,4H13V11H20V4Z",
                    IconBackground = accentBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "create-floor",
                    Name = "Create Floor",
                    Description = "Create a floor slab",
                    Category = "Create",
                    Command = "Create a floor",
                    IconPath = "M12,7L22,12L12,17L2,12L12,7M12,3L2,8L12,13L22,8L12,3Z",
                    IconBackground = accentBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "create-room",
                    Name = "Create Room",
                    Description = "Place a room in enclosed area",
                    Category = "Create",
                    Command = "Create a room",
                    IconPath = "M12,3L2,12H5V20H19V12H22L12,3M12,8.75A2.25,2.25 0 0,1 14.25,11A2.25,2.25 0 0,1 12,13.25A2.25,2.25 0 0,1 9.75,11A2.25,2.25 0 0,1 12,8.75M12,15C13.5,15 16,15.9 16,17.5V18H8V17.5C8,15.9 10.5,15 12,15Z",
                    IconBackground = accentBrush,
                    IconForeground = Brushes.White
                },

                // Analyze Commands
                new PaletteCommand
                {
                    Id = "analyze-model",
                    Name = "Analyze Model",
                    Description = "Run comprehensive model analysis",
                    Category = "Analyze",
                    Command = "Analyze the model",
                    Shortcut = "Ctrl+Shift+A",
                    IconPath = "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M9,17H7V10H9V17M13,17H11V7H13V17M17,17H15V13H17V17Z",
                    IconBackground = successBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "check-compliance",
                    Name = "Check Compliance",
                    Description = "Verify building code compliance",
                    Category = "Analyze",
                    Command = "Check compliance",
                    IconPath = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,16.5L6.5,12L7.91,10.59L11,13.67L16.59,8.09L18,9.5L11,16.5Z",
                    IconBackground = successBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "find-clashes",
                    Name = "Find Clashes",
                    Description = "Detect element clashes and conflicts",
                    Category = "Analyze",
                    Command = "Find clashes",
                    IconPath = "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16",
                    IconBackground = warningBrush,
                    IconForeground = Brushes.Black
                },
                new PaletteCommand
                {
                    Id = "calculate-area",
                    Name = "Calculate Areas",
                    Description = "Calculate room and floor areas",
                    Category = "Analyze",
                    Command = "Calculate areas",
                    IconPath = "M3,3H11V11H3V3M13,3H21V11H13V3M3,13H11V21H3V13M18,13V15H20V17H18V21H16V17H14V15H16V13H18Z",
                    IconBackground = successBrush,
                    IconForeground = Brushes.White
                },

                // Modify Commands
                new PaletteCommand
                {
                    Id = "move-element",
                    Name = "Move Element",
                    Description = "Move selected elements",
                    Category = "Modify",
                    Command = "Move selected elements",
                    IconPath = "M13,6V11H18V7.75L22.25,12L18,16.25V13H13V18H16.25L12,22.25L7.75,18H11V13H6V16.25L1.75,12L6,7.75V11H11V6H7.75L12,1.75L16.25,6H13Z",
                    IconBackground = infoBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "copy-element",
                    Name = "Copy Element",
                    Description = "Copy selected elements",
                    Category = "Modify",
                    Command = "Copy selected elements",
                    Shortcut = "Ctrl+C",
                    IconPath = "M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z",
                    IconBackground = infoBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "delete-element",
                    Name = "Delete Element",
                    Description = "Delete selected elements",
                    Category = "Modify",
                    Command = "Delete selected elements",
                    Shortcut = "Delete",
                    IconPath = "M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "rotate-element",
                    Name = "Rotate Element",
                    Description = "Rotate selected elements",
                    Category = "Modify",
                    Command = "Rotate selected elements",
                    IconPath = "M12,5V1L7,6L12,11V7A6,6 0 0,1 18,13A6,6 0 0,1 12,19A6,6 0 0,1 6,13H4A8,8 0 0,0 12,21A8,8 0 0,0 20,13A8,8 0 0,0 12,5Z",
                    IconBackground = infoBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "apply-material",
                    Name = "Apply Material",
                    Description = "Apply material to elements",
                    Category = "Modify",
                    Command = "Apply material",
                    IconPath = "M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M12,3A9,9 0 0,0 3,12A9,9 0 0,0 12,21A1.5,1.5 0 0,0 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.5C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16A5,5 0 0,0 21,11C21,6.58 16.97,3 12,3Z",
                    IconBackground = purpleBrush,
                    IconForeground = Brushes.White
                },

                // View Commands
                new PaletteCommand
                {
                    Id = "zoom-fit",
                    Name = "Zoom to Fit",
                    Description = "Fit all elements in view",
                    Category = "View",
                    Command = "Zoom to fit",
                    Shortcut = "ZF",
                    IconPath = "M15.5,14L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5M9.5,14A4.5,4.5 0 0,0 14,9.5A4.5,4.5 0 0,0 9.5,5A4.5,4.5 0 0,0 5,9.5A4.5,4.5 0 0,0 9.5,14M12,10H10V12H8V10H6V8H8V6H10V8H12V10Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "zoom-selection",
                    Name = "Zoom to Selection",
                    Description = "Zoom to selected elements",
                    Category = "View",
                    Command = "Zoom to selection",
                    Shortcut = "ZS",
                    IconPath = "M15.5,14L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5M9.5,14A4.5,4.5 0 0,0 14,9.5A4.5,4.5 0 0,0 9.5,5A4.5,4.5 0 0,0 5,9.5A4.5,4.5 0 0,0 9.5,14Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "show-3d",
                    Name = "3D View",
                    Description = "Switch to 3D view",
                    Category = "View",
                    Command = "Show 3D view",
                    IconPath = "M12,2L22,8.5V15.5L12,22L2,15.5V8.5L12,2M12,4.15L4,9.07V14.93L12,19.85L20,14.93V9.07L12,4.15Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "show-plan",
                    Name = "Plan View",
                    Description = "Switch to plan view",
                    Category = "View",
                    Command = "Show plan view",
                    IconPath = "M3,3H21V21H3V3M5,5V19H19V5H5Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                    IconForeground = Brushes.White
                },

                // Schedules & Reports
                new PaletteCommand
                {
                    Id = "generate-schedule",
                    Name = "Generate Schedule",
                    Description = "Create a new schedule",
                    Category = "Create",
                    Command = "Generate schedule",
                    IconPath = "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20M10,13H7V11H10V13M14,13H11V11H14V13M10,16H7V14H10V16M14,16H11V14H14V16M10,19H7V17H10V19M14,19H11V17H14V19Z",
                    IconBackground = purpleBrush,
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "export-report",
                    Name = "Export Report",
                    Description = "Export model report",
                    Category = "Analyze",
                    Command = "Export report",
                    IconPath = "M14,2L20,8V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V4A2,2 0 0,1 6,2H14M18,20V9H13V4H6V20H18M12,19L8,15H10.5V12H13.5V15H16L12,19Z",
                    IconBackground = successBrush,
                    IconForeground = Brushes.White
                },

                // AI Executive Tasks
                new PaletteCommand
                {
                    Id = "ai-parse-brief",
                    Name = "Parse Project Brief",
                    Description = "AI extracts requirements from project description",
                    Category = "AI",
                    Command = "Parse this project brief",
                    IconPath = "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M13.5,16V18H10.5V16H13.5M13.5,14H10.5V12H13.5V14M13,9V3.5L18.5,9H13Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-generate-bep",
                    Name = "Generate BIM Execution Plan",
                    Description = "AI creates complete 9-section BEP",
                    Category = "AI",
                    Command = "Generate a BIM Execution Plan",
                    IconPath = "M6,2H18A2,2 0 0,1 20,4V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V4A2,2 0 0,1 6,2M6,4V8H18V4H6M6,10V14H18V10H6M6,16V20H18V16H6Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-construction-schedule",
                    Name = "Create Construction Schedule",
                    Description = "AI generates 4D timeline with phases",
                    Category = "AI",
                    Command = "Create a construction schedule",
                    IconPath = "M19,19H5V8H19M16,1V3H8V1H6V3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3H18V1M17,12H12V17H17V12Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-maintenance-schedule",
                    Name = "Create Maintenance Schedule",
                    Description = "AI generates preventive maintenance program",
                    Category = "AI",
                    Command = "Create a maintenance schedule",
                    IconPath = "M12,3A4,4 0 0,1 16,7C16,7.73 15.81,8.41 15.46,9H18C18.95,9 19.75,9.67 19.95,10.56C21.96,18.57 22,18.78 22,19A2,2 0 0,1 20,21H4A2,2 0 0,1 2,19C2,18.78 2.04,18.57 4.05,10.56C4.25,9.67 5.05,9 6,9H8.54C8.19,8.41 8,7.73 8,7A4,4 0 0,1 12,3M12,5A2,2 0 0,0 10,7A2,2 0 0,0 12,9A2,2 0 0,0 14,7A2,2 0 0,0 12,5Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-cost-tracking",
                    Name = "Setup Cost Tracking (5D)",
                    Description = "AI configures budget breakdown and earned value",
                    Category = "AI",
                    Command = "Track project costs",
                    IconPath = "M7,15H9C9,16.08 10.37,17 12,17C13.63,17 15,16.08 15,15C15,13.9 13.96,13.5 11.76,12.97C9.64,12.44 7,11.78 7,9C7,7.21 8.47,5.69 10.5,5.18V3H13.5V5.18C15.53,5.69 17,7.21 17,9H15C15,7.92 13.63,7 12,7C10.37,7 9,7.92 9,9C9,10.1 10.04,10.5 12.24,11.03C14.36,11.56 17,12.22 17,15C17,16.79 15.53,18.31 13.5,18.82V21H10.5V18.82C8.47,18.31 7,16.79 7,15Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-progress-tracking",
                    Name = "Track Project Progress",
                    Description = "AI shows SPI/CPI metrics and earned value",
                    Category = "AI",
                    Command = "Track project progress",
                    IconPath = "M13,2.03V2.05L13,4.05C17.39,4.59 20.5,8.58 19.96,12.97C19.5,16.61 16.64,19.5 13,19.93V21.93C18.5,21.38 22.5,16.5 21.95,11C21.5,6.25 17.73,2.5 13,2.03M11,2.06C9.05,2.25 7.19,3 5.67,4.26L7.1,5.74C8.22,4.84 9.57,4.26 11,4.06V2.06M4.26,5.67C3,7.19 2.25,9.04 2.05,11H4.05C4.24,9.58 4.8,8.23 5.69,7.1L4.26,5.67M2.06,13C2.26,14.96 3.03,16.81 4.27,18.33L5.69,16.9C4.81,15.77 4.24,14.42 4.06,13H2.06M7.1,18.37L5.67,19.74C7.18,21 9.04,21.79 11,22V20C9.58,19.82 8.23,19.25 7.1,18.37M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-coordination-report",
                    Name = "Generate Coordination Report",
                    Description = "AI creates clash detection summary",
                    Category = "AI",
                    Command = "Generate coordination report",
                    IconPath = "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M9,17H7V10H9V17M13,17H11V7H13V17M17,17H15V13H17V17Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                    IconForeground = Brushes.Black
                },
                new PaletteCommand
                {
                    Id = "ai-cost-report",
                    Name = "Generate Cost Report",
                    Description = "AI creates monthly budget analysis",
                    Category = "AI",
                    Command = "Generate cost report",
                    IconPath = "M3,22V8H7V22H3M10,22V2H14V22H10M17,22V14H21V22H17Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                    IconForeground = Brushes.Black
                },
                new PaletteCommand
                {
                    Id = "ai-recommendations",
                    Name = "Get AI Recommendations",
                    Description = "AI provides actionable project recommendations",
                    Category = "AI",
                    Command = "Provide project recommendations",
                    IconPath = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M12,10.5A1.5,1.5 0 0,1 13.5,12A1.5,1.5 0 0,1 12,13.5A1.5,1.5 0 0,1 10.5,12A1.5,1.5 0 0,1 12,10.5M7.5,10.5A1.5,1.5 0 0,1 9,12A1.5,1.5 0 0,1 7.5,13.5A1.5,1.5 0 0,1 6,12A1.5,1.5 0 0,1 7.5,10.5M16.5,10.5A1.5,1.5 0 0,1 18,12A1.5,1.5 0 0,1 16.5,13.5A1.5,1.5 0 0,1 15,12A1.5,1.5 0 0,1 16.5,10.5Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    IconForeground = Brushes.White
                },

                // AI Knowledge Queries
                new PaletteCommand
                {
                    Id = "ai-ask-lod",
                    Name = "What is LOD?",
                    Description = "AI explains Level of Development",
                    Category = "AI",
                    Command = "What is LOD 350?",
                    IconPath = "M15.07,11.25L14.17,12.17C13.45,12.89 13,13.5 13,15H11V14.5C11,13.39 11.45,12.39 12.17,11.67L13.41,10.41C13.78,10.05 14,9.55 14,9C14,7.89 13.1,7 12,7A2,2 0 0,0 10,9H8A4,4 0 0,1 12,5A4,4 0 0,1 16,9C16,9.88 15.64,10.67 15.07,11.25M13,19H11V17H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12C22,6.47 17.5,2 12,2Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-ask-parameters",
                    Name = "Search Parameters",
                    Description = "AI finds parameter definitions",
                    Category = "AI",
                    Command = "What parameters are available for walls?",
                    IconPath = "M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "ai-ask-formula",
                    Name = "Engineering Formulas",
                    Description = "AI explains calculation formulas",
                    Category = "AI",
                    Command = "How do I calculate voltage drop?",
                    IconPath = "M19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M7,7H9V9H7V7M7,11H9V13H7V11M7,15H9V17H7V15M17,17H11V15H17V17M17,13H11V11H17V13M17,9H11V7H17V9Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    IconForeground = Brushes.White
                },

                // Settings & Help
                new PaletteCommand
                {
                    Id = "open-settings",
                    Name = "Settings",
                    Description = "Open settings panel",
                    Category = "View",
                    Command = "Open settings",
                    Shortcut = "Ctrl+,",
                    IconPath = "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    IconForeground = Brushes.White
                },
                new PaletteCommand
                {
                    Id = "show-help",
                    Name = "Help",
                    Description = "Show help and documentation",
                    Category = "View",
                    Command = "Show help",
                    Shortcut = "F1",
                    IconPath = "M15.07,11.25L14.17,12.17C13.45,12.89 13,13.5 13,15H11V14.5C11,13.39 11.45,12.39 12.17,11.67L13.41,10.41C13.78,10.05 14,9.55 14,9C14,7.89 13.1,7 12,7A2,2 0 0,0 10,9H8A4,4 0 0,1 12,5A4,4 0 0,1 16,9C16,9.88 15.64,10.67 15.07,11.25M13,19H11V17H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12C22,6.47 17.5,2 12,2Z",
                    IconBackground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    IconForeground = Brushes.White
                }
            };

            Logger.Debug($"Loaded {_allCommands.Count} default commands");
        }

        private void UpdateFilter()
        {
            var searchText = SearchTextBox?.Text?.ToLowerInvariant() ?? "";
            var filtered = _allCommands.AsEnumerable();

            // Filter by category
            if (_currentCategory == "Recent")
            {
                filtered = _recentCommands;
            }
            else if (_currentCategory != "All")
            {
                filtered = filtered.Where(c => c.Category == _currentCategory);
            }

            // Filter by search text (fuzzy matching)
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(c =>
                    FuzzyMatch(c.Name, searchText) ||
                    FuzzyMatch(c.Description, searchText) ||
                    FuzzyMatch(c.Command, searchText) ||
                    (c.Shortcut?.ToLowerInvariant().Contains(searchText) ?? false)
                ).OrderByDescending(c => GetMatchScore(c, searchText));
            }

            _filteredCommands.Clear();
            foreach (var cmd in filtered.Take(20))
            {
                _filteredCommands.Add(cmd);
            }

            // Update UI
            EmptyState.Visibility = _filteredCommands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CommandsList.Visibility = _filteredCommands.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultCountText.Text = $"{_filteredCommands.Count} command{(_filteredCommands.Count == 1 ? "" : "s")}";

            // Select first item
            if (_filteredCommands.Count > 0)
            {
                CommandsList.SelectedIndex = 0;
            }
        }

        private static bool FuzzyMatch(string text, string search)
        {
            if (string.IsNullOrEmpty(text)) return false;
            text = text.ToLowerInvariant();

            // Simple fuzzy: check if all search characters appear in order
            int textIndex = 0;
            foreach (char c in search)
            {
                textIndex = text.IndexOf(c, textIndex);
                if (textIndex < 0) return false;
                textIndex++;
            }
            return true;
        }

        private static int GetMatchScore(PaletteCommand cmd, string search)
        {
            int score = 0;
            var nameLower = cmd.Name.ToLowerInvariant();

            // Exact match in name
            if (nameLower.Contains(search)) score += 100;

            // Starts with search
            if (nameLower.StartsWith(search)) score += 50;

            // Word starts with search
            var words = nameLower.Split(' ');
            if (words.Any(w => w.StartsWith(search))) score += 25;

            return score;
        }

        private void ExecuteSelectedCommand()
        {
            if (CommandsList.SelectedItem is PaletteCommand command)
            {
                AddToRecent(command);
                CommandSelected?.Invoke(this, command);
                Hide();
                Logger.Info($"Command executed from palette: {command.Name}");
            }
        }

        #endregion

        #region Event Handlers

        private void Root_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateFilter();
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    if (CommandsList.SelectedIndex < _filteredCommands.Count - 1)
                    {
                        CommandsList.SelectedIndex++;
                        CommandsList.ScrollIntoView(CommandsList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (CommandsList.SelectedIndex > 0)
                    {
                        CommandsList.SelectedIndex--;
                        CommandsList.ScrollIntoView(CommandsList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ExecuteSelectedCommand();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Autocomplete with selected command
                    if (CommandsList.SelectedItem is PaletteCommand cmd)
                    {
                        SearchTextBox.Text = cmd.Name;
                        SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
                    }
                    e.Handled = true;
                    break;

                case Key.Escape:
                    Hide();
                    e.Handled = true;
                    break;
            }
        }

        private void CategoryTab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is string category)
            {
                _currentCategory = category;
                UpdateFilter();
            }
        }

        private void CommandsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Could show preview or details here
        }

        private void CommandsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectedCommand();
        }

        #endregion
    }

    /// <summary>
    /// Represents a command in the palette.
    /// </summary>
    public class PaletteCommand
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Command { get; set; }
        public string Shortcut { get; set; }
        public string IconPath { get; set; }
        public Brush IconBackground { get; set; }
        public Brush IconForeground { get; set; }

        public Visibility ShortcutVisibility =>
            string.IsNullOrEmpty(Shortcut) ? Visibility.Collapsed : Visibility.Visible;

        public override bool Equals(object obj)
        {
            return obj is PaletteCommand other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }
    }
}
