// StingBIM.AI.UI.Controls.ChatSearchBar
// Search bar for finding text within chat messages
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Search bar for searching within chat messages.
    /// </summary>
    public partial class ChatSearchBar : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private List<SearchResult> _results;
        private int _currentIndex;

        /// <summary>
        /// Event fired when a search match should be highlighted/scrolled to.
        /// </summary>
        public event EventHandler<SearchResult> NavigateToResult;

        /// <summary>
        /// Event fired when the search text changes.
        /// </summary>
        public event EventHandler<string> SearchTextChanged;

        /// <summary>
        /// Event fired when the search bar should be closed.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>
        /// Gets the current search text.
        /// </summary>
        public string SearchText => SearchTextBox.Text;

        /// <summary>
        /// Gets the current result index (1-based).
        /// </summary>
        public int CurrentIndex => _currentIndex + 1;

        /// <summary>
        /// Gets the total number of results.
        /// </summary>
        public int TotalResults => _results?.Count ?? 0;

        public ChatSearchBar()
        {
            InitializeComponent();
            _results = new List<SearchResult>();
            _currentIndex = -1;

            SearchTextBox.TextChanged += (s, e) => UpdatePlaceholder();
        }

        #region Public Methods

        /// <summary>
        /// Focuses the search input.
        /// </summary>
        public void FocusSearch()
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        /// <summary>
        /// Sets the search results from external search.
        /// </summary>
        public void SetResults(IEnumerable<SearchResult> results)
        {
            _results = results?.ToList() ?? new List<SearchResult>();
            _currentIndex = _results.Count > 0 ? 0 : -1;

            UpdateUI();

            if (_currentIndex >= 0)
            {
                NavigateToResult?.Invoke(this, _results[_currentIndex]);
            }
        }

        /// <summary>
        /// Clears the search and results.
        /// </summary>
        public void Clear()
        {
            SearchTextBox.Text = string.Empty;
            _results.Clear();
            _currentIndex = -1;
            UpdateUI();
        }

        /// <summary>
        /// Navigates to the next result.
        /// </summary>
        public void GoToNext()
        {
            if (_results.Count == 0) return;

            _currentIndex++;
            if (_currentIndex >= _results.Count)
            {
                _currentIndex = 0;
            }

            UpdateUI();
            NavigateToResult?.Invoke(this, _results[_currentIndex]);
        }

        /// <summary>
        /// Navigates to the previous result.
        /// </summary>
        public void GoToPrevious()
        {
            if (_results.Count == 0) return;

            _currentIndex--;
            if (_currentIndex < 0)
            {
                _currentIndex = _results.Count - 1;
            }

            UpdateUI();
            NavigateToResult?.Invoke(this, _results[_currentIndex]);
        }

        #endregion

        #region Private Methods

        private void UpdateUI()
        {
            var hasResults = _results.Count > 0;
            var hasText = !string.IsNullOrEmpty(SearchTextBox.Text);

            PreviousButton.IsEnabled = hasResults;
            NextButton.IsEnabled = hasResults;

            if (hasText)
            {
                ResultCountText.Text = hasResults
                    ? $"{_currentIndex + 1} of {_results.Count}"
                    : "No results";
                ResultCountText.Visibility = Visibility.Visible;
            }
            else
            {
                ResultCountText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePlaceholder()
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #endregion

        #region Event Handlers

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchTextChanged?.Invoke(this, SearchTextBox.Text);
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        GoToPrevious();
                    }
                    else
                    {
                        GoToNext();
                    }
                    e.Handled = true;
                    break;

                case Key.Escape:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;

                case Key.F3:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        GoToPrevious();
                    }
                    else
                    {
                        GoToNext();
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            GoToPrevious();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            GoToNext();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// Represents a search result within the chat.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// The index of the message containing the match.
        /// </summary>
        public int MessageIndex { get; set; }

        /// <summary>
        /// The message ID if available.
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// The start position of the match within the message text.
        /// </summary>
        public int MatchStart { get; set; }

        /// <summary>
        /// The length of the match.
        /// </summary>
        public int MatchLength { get; set; }

        /// <summary>
        /// A preview of the matched text with context.
        /// </summary>
        public string Preview { get; set; }

        /// <summary>
        /// Whether this is a match in user message (true) or assistant message (false).
        /// </summary>
        public bool IsUserMessage { get; set; }
    }
}
