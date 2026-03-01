// StingBIM.AI.UI.Controls.FavoritesPanel
// Panel for managing favorite/pinned commands
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Panel for saving and accessing frequently used commands.
    /// </summary>
    public partial class FavoritesPanel : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ObservableCollection<FavoriteCommand> _favorites;
        private readonly string _savePath;

        /// <summary>
        /// Event fired when a favorite command is selected for execution.
        /// </summary>
        public event EventHandler<string> CommandSelected;

        /// <summary>
        /// Event fired when the panel requests to be closed.
        /// </summary>
        public event EventHandler CloseRequested;

        public FavoritesPanel()
        {
            InitializeComponent();

            _favorites = new ObservableCollection<FavoriteCommand>();
            _savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "favorites.json");

            FavoritesList.ItemsSource = _favorites;

            // Setup text box event
            NewFavoriteTextBox.TextChanged += NewFavoriteTextBox_TextChanged;

            // Load saved favorites
            LoadFavorites();
            UpdateEmptyState();
        }

        #region Public Methods

        /// <summary>
        /// Adds a command to favorites.
        /// </summary>
        public void AddFavorite(string command, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Check if already exists
            if (_favorites.Any(f => f.Command.Equals(command, StringComparison.OrdinalIgnoreCase)))
            {
                Services.NotificationService.Instance.ShowInfo("Already Saved", "This command is already in your favorites");
                return;
            }

            var favorite = new FavoriteCommand
            {
                Id = Guid.NewGuid().ToString(),
                Command = command,
                DisplayName = displayName ?? GetDisplayName(command),
                CreatedAt = DateTime.Now
            };

            _favorites.Insert(0, favorite);
            SaveFavorites();
            UpdateEmptyState();

            Services.NotificationService.Instance.ShowSuccess("Added", "Command added to favorites");
            Logger.Info($"Added favorite: {command}");
        }

        /// <summary>
        /// Removes a command from favorites.
        /// </summary>
        public void RemoveFavorite(string id)
        {
            var favorite = _favorites.FirstOrDefault(f => f.Id == id);
            if (favorite != null)
            {
                _favorites.Remove(favorite);
                SaveFavorites();
                UpdateEmptyState();
                Logger.Info($"Removed favorite: {favorite.Command}");
            }
        }

        /// <summary>
        /// Checks if a command is in favorites.
        /// </summary>
        public bool IsFavorite(string command)
        {
            return _favorites.Any(f => f.Command.Equals(command, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Toggles favorite status for a command.
        /// </summary>
        public void ToggleFavorite(string command)
        {
            var existing = _favorites.FirstOrDefault(f => f.Command.Equals(command, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                RemoveFavorite(existing.Id);
            }
            else
            {
                AddFavorite(command);
            }
        }

        #endregion

        #region Private Methods

        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    var json = File.ReadAllText(_savePath);
                    var favorites = JsonConvert.DeserializeObject<FavoriteCommand[]>(json);

                    if (favorites != null)
                    {
                        foreach (var fav in favorites)
                        {
                            _favorites.Add(fav);
                        }
                    }

                    Logger.Debug($"Loaded {_favorites.Count} favorites");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load favorites");
            }
        }

        private void SaveFavorites()
        {
            try
            {
                var directory = Path.GetDirectoryName(_savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var json = JsonConvert.SerializeObject(_favorites.ToArray(), Formatting.Indented);
                File.WriteAllText(_savePath, json);

                Logger.Debug($"Saved {_favorites.Count} favorites");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save favorites");
            }
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = _favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            FavoritesList.Visibility = _favorites.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string GetDisplayName(string command)
        {
            // Generate a friendly display name from the command
            if (string.IsNullOrEmpty(command))
                return string.Empty;

            // Capitalize first letter and truncate if too long
            var display = char.ToUpper(command[0]) + command.Substring(1);

            if (display.Length > 40)
            {
                display = display.Substring(0, 37) + "...";
            }

            return display;
        }

        #endregion

        #region Event Handlers

        private void NewFavoriteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(NewFavoriteTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void NewFavoriteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddCurrentInput();
                e.Handled = true;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddCurrentInput();
        }

        private void AddCurrentInput()
        {
            var command = NewFavoriteTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(command))
            {
                AddFavorite(command);
                NewFavoriteTextBox.Text = string.Empty;
            }
        }

        private void FavoritesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FavoritesList.SelectedItem is FavoriteCommand favorite)
            {
                CommandSelected?.Invoke(this, favorite.Command);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FavoriteCommand favorite)
            {
                // Show edit dialog
                var dialog = new Window
                {
                    Title = "Edit Favorite",
                    Width = 350,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.NoResize,
                    Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
                    Owner = Window.GetWindow(this)
                };

                var grid = new Grid { Margin = new Thickness(16) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var displayLabel = new TextBlock { Text = "Display Name:", Margin = new Thickness(0, 0, 0, 4) };
                var displayTextBox = new TextBox { Text = favorite.DisplayName, Margin = new Thickness(0, 0, 0, 12) };

                var commandLabel = new TextBlock { Text = "Command:", Margin = new Thickness(0, 0, 0, 4) };
                var commandTextBox = new TextBox { Text = favorite.Command, Margin = new Thickness(0, 0, 0, 16) };

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var saveButton = new Button { Content = "Save", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
                var cancelButton = new Button { Content = "Cancel", Width = 80 };

                Grid.SetRow(displayLabel, 0);
                Grid.SetRow(displayTextBox, 1);
                Grid.SetRow(commandLabel, 2);
                Grid.SetRow(commandTextBox, 3);

                grid.Children.Add(displayLabel);
                grid.Children.Add(displayTextBox);
                grid.Children.Add(commandLabel);
                grid.Children.Add(commandTextBox);

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);

                var mainPanel = new StackPanel();
                mainPanel.Children.Add(grid);
                mainPanel.Children.Add(buttonPanel);
                dialog.Content = mainPanel;

                saveButton.Click += (s, args) =>
                {
                    favorite.DisplayName = displayTextBox.Text;
                    favorite.Command = commandTextBox.Text;
                    SaveFavorites();
                    FavoritesList.Items.Refresh();
                    dialog.Close();
                };

                cancelButton.Click += (s, args) => dialog.Close();

                dialog.ShowDialog();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FavoriteCommand favorite)
            {
                RemoveFavorite(favorite.Id);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// Represents a favorited command.
    /// </summary>
    public class FavoriteCommand
    {
        public string Id { get; set; }
        public string Command { get; set; }
        public string DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool ShowCommand => !Command.Equals(DisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
