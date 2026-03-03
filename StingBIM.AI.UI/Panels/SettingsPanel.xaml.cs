// StingBIM.AI.UI.Panels.SettingsPanel
// Configuration panel for AI assistant settings
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - Settings Panel

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.NLP.Dialogue;
using StingBIM.AI.NLP.Voice;
using StingBIM.AI.UI.Themes;

namespace StingBIM.AI.UI.Panels
{
    /// <summary>
    /// Settings panel for configuring AI assistant behavior.
    /// </summary>
    public partial class SettingsPanel : Window
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly AISettings _settings;
        private readonly string _settingsPath;

        /// <summary>
        /// Event fired when settings are saved.
        /// </summary>
        public event EventHandler<AISettings> SettingsSaved;

        /// <summary>
        /// Event fired when conversation history should be cleared.
        /// </summary>
        public event EventHandler ClearHistoryRequested;

        /// <summary>
        /// Event fired when learning data should be reset.
        /// </summary>
        public event EventHandler ClearLearningRequested;

        public SettingsPanel(AISettings settings = null, string settingsPath = null)
        {
            InitializeComponent();

            _settingsPath = settingsPath ?? GetDefaultSettingsPath();
            _settings = settings ?? LoadSettings() ?? new AISettings();

            LoadAudioDevices();
            ApplySettingsToUI();

            Logger.Info("SettingsPanel initialized");
        }

        #region Settings Management

        private string GetDefaultSettingsPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "StingBIM", "AI", "settings.json");
        }

        private AISettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonConvert.DeserializeObject<AISettings>(json);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load settings");
            }

            return null;
        }

        private void SaveSettings()
        {
            try
            {
                // Get values from UI
                _settings.EnableVoice = EnableVoiceCheckBox.IsChecked ?? true;
                _settings.AudioDeviceName = (AudioDeviceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                _settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
                _settings.UseWakeWord = WakeWordCheckBox.IsChecked ?? false;

                _settings.Verbosity = (VerbosityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";
                _settings.ShowSuggestions = ShowSuggestionsCheckBox.IsChecked ?? true;
                _settings.ConfirmDestructiveActions = ConfirmActionsCheckBox.IsChecked ?? true;

                _settings.EnableLearning = LearningCheckBox.IsChecked ?? true;
                _settings.EnableProactiveAssistance = ProactiveCheckBox.IsChecked ?? true;
                _settings.ConfidenceThreshold = (float)ConfidenceSlider.Value;

                // Theme settings (if ThemeCombo exists)
                try
                {
                    if (FindName("ThemeCombo") is ComboBox themeCombo)
                    {
                        var newTheme = (themeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Dark";
                        if (newTheme != _settings.Theme)
                        {
                            _settings.Theme = newTheme;
                            // Apply theme immediately
                            ThemeManager.Instance.SetTheme(newTheme);
                        }
                    }
                }
                catch
                {
                    // ThemeCombo may not exist in all versions
                }

                // Save to file
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);

                Logger.Info("Settings saved");

                // Notify listeners
                SettingsSaved?.Invoke(this, _settings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save settings");
                MessageBox.Show(
                    $"Failed to save settings: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ApplySettingsToUI()
        {
            // Voice settings
            EnableVoiceCheckBox.IsChecked = _settings.EnableVoice;
            WakeWordCheckBox.IsChecked = _settings.UseWakeWord;

            // Select audio device
            if (!string.IsNullOrEmpty(_settings.AudioDeviceName))
            {
                foreach (ComboBoxItem item in AudioDeviceCombo.Items)
                {
                    if (item.Content?.ToString() == _settings.AudioDeviceName)
                    {
                        AudioDeviceCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            // Select language
            foreach (ComboBoxItem item in LanguageCombo.Items)
            {
                if (item.Tag?.ToString() == _settings.Language)
                {
                    LanguageCombo.SelectedItem = item;
                    break;
                }
            }

            // Response settings
            foreach (ComboBoxItem item in VerbosityCombo.Items)
            {
                if (item.Tag?.ToString() == _settings.Verbosity)
                {
                    VerbosityCombo.SelectedItem = item;
                    break;
                }
            }

            ShowSuggestionsCheckBox.IsChecked = _settings.ShowSuggestions;
            ConfirmActionsCheckBox.IsChecked = _settings.ConfirmDestructiveActions;

            // AI behavior settings
            LearningCheckBox.IsChecked = _settings.EnableLearning;
            ProactiveCheckBox.IsChecked = _settings.EnableProactiveAssistance;
            ConfidenceSlider.Value = _settings.ConfidenceThreshold;

            // Theme settings (if ThemeCombo exists)
            try
            {
                if (FindName("ThemeCombo") is ComboBox themeCombo)
                {
                    foreach (ComboBoxItem item in themeCombo.Items)
                    {
                        if (item.Tag?.ToString() == _settings.Theme)
                        {
                            themeCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // ThemeCombo may not exist in all versions
            }
        }

        private void LoadAudioDevices()
        {
            try
            {
                AudioDeviceCombo.Items.Clear();

                // Add default option
                AudioDeviceCombo.Items.Add(new ComboBoxItem
                {
                    Content = "System Default",
                    Tag = -1
                });

                // Get available audio devices
                var devices = SpeechRecognizer.GetAudioDevices();

                foreach (var device in devices)
                {
                    AudioDeviceCombo.Items.Add(new ComboBoxItem
                    {
                        Content = device.Name,
                        Tag = device.DeviceIndex
                    });
                }

                // Select first item if nothing selected
                if (AudioDeviceCombo.SelectedItem == null && AudioDeviceCombo.Items.Count > 0)
                {
                    AudioDeviceCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load audio devices");
                AudioDeviceCombo.Items.Add(new ComboBoxItem { Content = "No devices found" });
            }
        }

        #endregion

        #region Event Handlers

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all conversation history?\n\nThis cannot be undone.",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ClearHistoryRequested?.Invoke(this, EventArgs.Empty);
                MessageBox.Show("Conversation history cleared.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearLearningButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all AI learning data?\n\n" +
                "This will erase learned preferences and patterns.\nThis cannot be undone.",
                "Reset Learning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ClearLearningRequested?.Invoke(this, EventArgs.Empty);
                MessageBox.Show("Learning data reset.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        /// <summary>
        /// Gets the current settings.
        /// </summary>
        public AISettings GetSettings() => _settings;
    }

    /// <summary>
    /// AI assistant settings.
    /// </summary>
    public class AISettings
    {
        // Voice settings
        public bool EnableVoice { get; set; } = true;
        public string AudioDeviceName { get; set; }
        public int AudioDeviceIndex { get; set; } = -1;
        public string Language { get; set; } = "en";
        public bool UseWakeWord { get; set; } = false;
        public string WakeWord { get; set; } = "hey sting";

        // Response settings
        public string Verbosity { get; set; } = "Normal";
        public bool ShowSuggestions { get; set; } = true;
        public bool ConfirmDestructiveActions { get; set; } = true;
        public bool PlaySounds { get; set; } = true;

        // AI behavior
        public bool EnableLearning { get; set; } = true;
        public bool EnableProactiveAssistance { get; set; } = true;
        public float ConfidenceThreshold { get; set; } = 0.6f;

        // Appearance
        public string Theme { get; set; } = "Dark";
        public bool CompactMode { get; set; } = false;

        // Performance
        public bool UseGPU { get; set; } = true;
        public int MaxHistoryLength { get; set; } = 100;

        /// <summary>
        /// Gets verbosity as enum.
        /// </summary>
        public ResponseVerbosity GetVerbosity()
        {
            return Verbosity switch
            {
                "Minimal" => ResponseVerbosity.Minimal,
                "Detailed" => ResponseVerbosity.Detailed,
                _ => ResponseVerbosity.Normal
            };
        }
    }
}
