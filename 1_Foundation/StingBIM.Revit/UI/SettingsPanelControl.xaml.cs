// ============================================================================
// StingBIM AI - Settings Panel Dockable Control
// Lightweight UserControl for Revit's IDockablePaneProvider
// ============================================================================

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.Revit.UI
{
    public partial class SettingsPanelControl : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _settingsPath;

        public SettingsPanelControl()
        {
            InitializeComponent();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsPath = Path.Combine(appData, "StingBIM", "AI", "settings.json");

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonConvert.DeserializeObject<SimpleSettings>(json);
                    if (settings != null)
                    {
                        LearningCheckBox.IsChecked = settings.EnableLearning;
                        VoiceCheckBox.IsChecked = settings.EnableVoice;
                        ProactiveCheckBox.IsChecked = settings.EnableProactive;
                        ConfirmCheckBox.IsChecked = settings.ConfirmChanges;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load settings");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new SimpleSettings
                {
                    EnableLearning = LearningCheckBox.IsChecked ?? true,
                    EnableVoice = VoiceCheckBox.IsChecked ?? true,
                    EnableProactive = ProactiveCheckBox.IsChecked ?? true,
                    ConfirmChanges = ConfirmCheckBox.IsChecked ?? true,
                    Region = (RegionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "East Africa",
                    Verbosity = (VerbosityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal"
                };

                var dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);

                Logger.Info("Settings saved");
                MessageBox.Show("Settings saved successfully.", "StingBIM AI",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save settings");
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    internal class SimpleSettings
    {
        public bool EnableLearning { get; set; } = true;
        public bool EnableVoice { get; set; } = true;
        public bool EnableProactive { get; set; } = true;
        public bool ConfirmChanges { get; set; } = true;
        public string Region { get; set; } = "East Africa";
        public string Verbosity { get; set; } = "Normal";
    }
}
