// StingBIM.AI.UI.Controls.KeyboardShortcutsOverlay
// Overlay showing all keyboard shortcuts
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Overlay that displays all keyboard shortcuts.
    /// Triggered by F1 or ? key.
    /// </summary>
    public partial class KeyboardShortcutsOverlay : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Event fired when the overlay should close.
        /// </summary>
        public event EventHandler CloseRequested;

        public KeyboardShortcutsOverlay()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                // Focus for keyboard input
                Focus();
                Keyboard.Focus(this);
            };
        }

        #region Public Methods

        /// <summary>
        /// Shows the shortcuts overlay.
        /// </summary>
        public void Show()
        {
            Visibility = Visibility.Visible;
            SearchTextBox.Clear();
            Focus();
            Logger.Debug("Keyboard shortcuts overlay shown");
        }

        /// <summary>
        /// Hides the shortcuts overlay.
        /// </summary>
        public void Hide()
        {
            Visibility = Visibility.Collapsed;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            Logger.Debug("Keyboard shortcuts overlay hidden");
        }

        /// <summary>
        /// Toggles the overlay visibility.
        /// </summary>
        public void Toggle()
        {
            if (Visibility == Visibility.Visible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        #endregion

        #region Event Handlers

        private void Root_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F1)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Backdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only close if clicking directly on the backdrop, not on the content
            if (e.OriginalSource == sender)
            {
                Hide();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Could implement filtering of shortcuts based on search text
            FilterShortcuts(SearchTextBox.Text);
        }

        #endregion

        #region Private Methods

        private void FilterShortcuts(string searchText)
        {
            // For now, just show/hide based on search
            // In a full implementation, would dynamically filter the shortcuts list
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ShortcutsGrid.Visibility = Visibility.Visible;
                return;
            }

            // Simple visibility toggle - full implementation would filter individual items
            searchText = searchText.ToLowerInvariant();

            // This is a simplified implementation
            // A full version would iterate through shortcuts and show/hide each
        }

        #endregion
    }
}
