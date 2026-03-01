// StingBIM.AI.UI.Controls.AutocompletePopup
// Autocomplete suggestions popup for command input
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Autocomplete popup that shows command suggestions as the user types.
    /// </summary>
    public partial class AutocompletePopup : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ObservableCollection<AutocompleteSuggestion> _suggestions;
        private readonly List<AutocompleteSuggestion> _allSuggestions;
        private UIElement _placementTarget;
        private int _selectedIndex = -1;

        /// <summary>
        /// Event fired when a suggestion is selected.
        /// </summary>
        public event EventHandler<AutocompleteSuggestion> SuggestionSelected;

        /// <summary>
        /// Event fired when the popup is closed without selection.
        /// </summary>
        public event EventHandler Dismissed;

        /// <summary>
        /// Gets whether the popup is currently open.
        /// </summary>
        public bool IsOpen => SuggestionsPopup.IsOpen;

        public AutocompletePopup()
        {
            InitializeComponent();

            _suggestions = new ObservableCollection<AutocompleteSuggestion>();
            _allSuggestions = new List<AutocompleteSuggestion>();
            SuggestionsList.ItemsSource = _suggestions;

            // Load default BIM suggestions
            LoadDefaultSuggestions();

            SuggestionsPopup.Closed += (s, e) => Dismissed?.Invoke(this, EventArgs.Empty);
        }

        #region Public Methods

        /// <summary>
        /// Sets the target element for popup placement.
        /// </summary>
        public void SetPlacementTarget(UIElement target)
        {
            _placementTarget = target;
            SuggestionsPopup.PlacementTarget = target;
        }

        /// <summary>
        /// Shows the popup with suggestions filtered by the input text.
        /// </summary>
        public void Show(string inputText)
        {
            if (_placementTarget == null)
                return;

            FilterSuggestions(inputText);

            if (_suggestions.Count > 0)
            {
                SuggestionsPopup.IsOpen = true;
                NoSuggestionsText.Visibility = Visibility.Collapsed;
                _selectedIndex = -1;
            }
            else
            {
                Hide();
            }
        }

        /// <summary>
        /// Hides the popup.
        /// </summary>
        public void Hide()
        {
            SuggestionsPopup.IsOpen = false;
            _selectedIndex = -1;
        }

        /// <summary>
        /// Moves selection up in the list.
        /// </summary>
        public void SelectPrevious()
        {
            if (_suggestions.Count == 0) return;

            _selectedIndex--;
            if (_selectedIndex < 0)
                _selectedIndex = _suggestions.Count - 1;

            SuggestionsList.SelectedIndex = _selectedIndex;
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
        }

        /// <summary>
        /// Moves selection down in the list.
        /// </summary>
        public void SelectNext()
        {
            if (_suggestions.Count == 0) return;

            _selectedIndex++;
            if (_selectedIndex >= _suggestions.Count)
                _selectedIndex = 0;

            SuggestionsList.SelectedIndex = _selectedIndex;
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
        }

        /// <summary>
        /// Confirms the current selection.
        /// </summary>
        public void ConfirmSelection()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
            {
                var selected = _suggestions[_selectedIndex];
                SuggestionSelected?.Invoke(this, selected);
                Hide();
            }
        }

        /// <summary>
        /// Gets the currently selected suggestion, if any.
        /// </summary>
        public AutocompleteSuggestion GetSelectedSuggestion()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
            {
                return _suggestions[_selectedIndex];
            }
            return null;
        }

        /// <summary>
        /// Adds a custom suggestion.
        /// </summary>
        public void AddSuggestion(AutocompleteSuggestion suggestion)
        {
            if (!_allSuggestions.Any(s => s.Command == suggestion.Command))
            {
                _allSuggestions.Add(suggestion);
            }
        }

        /// <summary>
        /// Clears all custom suggestions and reloads defaults.
        /// </summary>
        public void ResetSuggestions()
        {
            _allSuggestions.Clear();
            LoadDefaultSuggestions();
        }

        #endregion

        #region Private Methods

        private void LoadDefaultSuggestions()
        {
            _allSuggestions.AddRange(new[]
            {
                // Create commands
                new AutocompleteSuggestion("Create a wall", "create wall", "Create a new wall element", SuggestionCategory.Create),
                new AutocompleteSuggestion("Create a floor", "create floor", "Create a new floor element", SuggestionCategory.Create),
                new AutocompleteSuggestion("Create a room", "create room", "Create a new room", SuggestionCategory.Create),
                new AutocompleteSuggestion("Add a door", "add door", "Add a door to a wall", SuggestionCategory.Create),
                new AutocompleteSuggestion("Add a window", "add window", "Add a window to a wall", SuggestionCategory.Create),
                new AutocompleteSuggestion("Create ceiling", "create ceiling", "Create a ceiling element", SuggestionCategory.Create),
                new AutocompleteSuggestion("Create roof", "create roof", "Create a roof element", SuggestionCategory.Create),
                new AutocompleteSuggestion("Create stair", "create stair", "Create a stair element", SuggestionCategory.Create),

                // Analysis commands
                new AutocompleteSuggestion("Check code compliance", "check compliance", "Validate against building codes", SuggestionCategory.Analyze),
                new AutocompleteSuggestion("Analyze spatial layout", "analyze layout", "Analyze room relationships", SuggestionCategory.Analyze),
                new AutocompleteSuggestion("Calculate areas", "calculate areas", "Calculate room and floor areas", SuggestionCategory.Analyze),
                new AutocompleteSuggestion("Review MEP coordination", "review mep", "Check MEP system coordination", SuggestionCategory.Analyze),
                new AutocompleteSuggestion("Energy analysis", "analyze energy", "Perform energy analysis", SuggestionCategory.Analyze),

                // Parameter commands
                new AutocompleteSuggestion("Show parameters", "show parameters", "Display element parameters", SuggestionCategory.Parameters),
                new AutocompleteSuggestion("Set parameter value", "set parameter", "Modify a parameter value", SuggestionCategory.Parameters),
                new AutocompleteSuggestion("Apply material", "apply material", "Apply material to elements", SuggestionCategory.Parameters),
                new AutocompleteSuggestion("Copy parameters", "copy parameters", "Copy parameters between elements", SuggestionCategory.Parameters),

                // Schedule commands
                new AutocompleteSuggestion("Create door schedule", "door schedule", "Generate a door schedule", SuggestionCategory.Schedule),
                new AutocompleteSuggestion("Create room schedule", "room schedule", "Generate a room schedule", SuggestionCategory.Schedule),
                new AutocompleteSuggestion("Create window schedule", "window schedule", "Generate a window schedule", SuggestionCategory.Schedule),
                new AutocompleteSuggestion("Create wall schedule", "wall schedule", "Generate a wall schedule", SuggestionCategory.Schedule),

                // Query commands
                new AutocompleteSuggestion("What is the area?", "what area", "Query element areas", SuggestionCategory.Query),
                new AutocompleteSuggestion("How many walls?", "count walls", "Count wall elements", SuggestionCategory.Query),
                new AutocompleteSuggestion("List all rooms", "list rooms", "Show all rooms in project", SuggestionCategory.Query),
                new AutocompleteSuggestion("Find elements by type", "find elements", "Search for elements", SuggestionCategory.Query),

                // Help commands
                new AutocompleteSuggestion("Help", "help", "Show available commands", SuggestionCategory.Help),
                new AutocompleteSuggestion("Show keyboard shortcuts", "shortcuts", "Display keyboard shortcuts", SuggestionCategory.Help),
            });
        }

        private void FilterSuggestions(string input)
        {
            _suggestions.Clear();

            if (string.IsNullOrWhiteSpace(input))
            {
                // Show top suggestions when empty
                foreach (var suggestion in _allSuggestions.Take(8))
                {
                    _suggestions.Add(suggestion);
                }
                return;
            }

            var inputLower = input.ToLowerInvariant();
            var matches = _allSuggestions
                .Where(s => s.DisplayText.ToLowerInvariant().Contains(inputLower) ||
                           s.Command.ToLowerInvariant().Contains(inputLower) ||
                           (s.Description?.ToLowerInvariant().Contains(inputLower) ?? false))
                .OrderByDescending(s => s.DisplayText.ToLowerInvariant().StartsWith(inputLower))
                .ThenBy(s => s.DisplayText)
                .Take(10);

            foreach (var match in matches)
            {
                _suggestions.Add(match);
            }

            HeaderText.Text = _suggestions.Count > 0
                ? $"{_suggestions.Count} suggestion{(_suggestions.Count != 1 ? "s" : "")}"
                : "No matches";
        }

        #endregion

        #region Event Handlers

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionsList.SelectedItem is AutocompleteSuggestion selected)
            {
                _selectedIndex = SuggestionsList.SelectedIndex;
                SuggestionSelected?.Invoke(this, selected);
                Hide();
            }
        }

        private void SuggestionsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    SelectPrevious();
                    e.Handled = true;
                    break;
                case Key.Down:
                    SelectNext();
                    e.Handled = true;
                    break;
                case Key.Enter:
                case Key.Tab:
                    ConfirmSelection();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Hide();
                    e.Handled = true;
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents an autocomplete suggestion.
    /// </summary>
    public class AutocompleteSuggestion
    {
        public string DisplayText { get; set; }
        public string Command { get; set; }
        public string Description { get; set; }
        public SuggestionCategory Category { get; set; }
        public string Shortcut { get; set; }

        public bool HasDescription => !string.IsNullOrEmpty(Description);
        public bool HasShortcut => !string.IsNullOrEmpty(Shortcut);

        public string IconPath => Category switch
        {
            SuggestionCategory.Create => "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z",
            SuggestionCategory.Analyze => "M9,20.42L2.79,14.21L5.62,11.38L9,14.77L18.88,4.88L21.71,7.71L9,20.42Z",
            SuggestionCategory.Parameters => "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M12,10.5A1.5,1.5 0 0,1 13.5,12A1.5,1.5 0 0,1 12,13.5A1.5,1.5 0 0,1 10.5,12A1.5,1.5 0 0,1 12,10.5",
            SuggestionCategory.Schedule => "M3,3H21V7H3V3M4,8H20V21H4V8M9.5,11A0.5,0.5 0 0,0 9,11.5V13H15V11.5A0.5,0.5 0 0,0 14.5,11H9.5Z",
            SuggestionCategory.Query => "M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3",
            SuggestionCategory.Help => "M11,18H13V16H11V18M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,6A4,4 0 0,0 8,10H10A2,2 0 0,1 12,8A2,2 0 0,1 14,10C14,12 11,11.75 11,15H13C13,12.75 16,12.5 16,10A4,4 0 0,0 12,6Z",
            _ => "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"
        };

        public AutocompleteSuggestion() { }

        public AutocompleteSuggestion(string displayText, string command, string description = null, SuggestionCategory category = SuggestionCategory.Other, string shortcut = null)
        {
            DisplayText = displayText;
            Command = command;
            Description = description;
            Category = category;
            Shortcut = shortcut;
        }
    }

    /// <summary>
    /// Categories for autocomplete suggestions.
    /// </summary>
    public enum SuggestionCategory
    {
        Create,
        Analyze,
        Parameters,
        Schedule,
        Query,
        Help,
        Other
    }
}
