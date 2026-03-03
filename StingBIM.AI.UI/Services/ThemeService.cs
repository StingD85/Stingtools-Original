// StingBIM.AI.UI.Services.ThemeService
// Theme management service for switching between visual themes
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Service for managing application themes including high contrast mode.
    /// </summary>
    public sealed class ThemeService : INotifyPropertyChanged
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<ThemeService> _instance =
            new Lazy<ThemeService>(() => new ThemeService());

        public static ThemeService Instance => _instance.Value;

        private ThemeMode _currentTheme;
        private readonly string _settingsPath;
        private ResourceDictionary _currentThemeResources;

        /// <summary>
        /// Event fired when the theme changes.
        /// </summary>
        public event EventHandler<ThemeMode> ThemeChanged;

        /// <summary>
        /// PropertyChanged event for data binding support.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the current theme mode.
        /// </summary>
        public ThemeMode CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDarkMode));
                    OnPropertyChanged(nameof(IsHighContrastMode));
                    ThemeChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Gets whether dark mode is active.
        /// </summary>
        public bool IsDarkMode => _currentTheme == ThemeMode.Dark || _currentTheme == ThemeMode.HighContrastDark;

        /// <summary>
        /// Gets whether high contrast mode is active.
        /// </summary>
        public bool IsHighContrastMode => _currentTheme == ThemeMode.HighContrastDark || _currentTheme == ThemeMode.HighContrastLight;

        /// <summary>
        /// Gets available themes.
        /// </summary>
        public List<ThemeInfo> AvailableThemes { get; }

        private ThemeService()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "theme_settings.json");

            AvailableThemes = new List<ThemeInfo>
            {
                new ThemeInfo { Mode = ThemeMode.Dark, Name = "Dark", Description = "Dark theme (default)", Icon = "M12,3A9,9 0 0,0 3,12A9,9 0 0,0 12,21A9,9 0 0,0 21,12A9,9 0 0,0 12,3Z" },
                new ThemeInfo { Mode = ThemeMode.Light, Name = "Light", Description = "Light theme", Icon = "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9Z" },
                new ThemeInfo { Mode = ThemeMode.HighContrastDark, Name = "High Contrast Dark", Description = "High contrast dark theme for accessibility", Icon = "M12,4.5A2.5,2.5 0 0,0 9.5,7A2.5,2.5 0 0,0 12,9.5A2.5,2.5 0 0,0 14.5,7A2.5,2.5 0 0,0 12,4.5M12,2A5,5 0 0,1 17,7C17,8.11 16.68,9.15 16.12,10.03L18.37,12.28C18.76,12.67 18.76,13.3 18.37,13.69C17.97,14.09 17.34,14.09 16.95,13.69L14.7,11.44C13.82,12 12.78,12.32 11.67,12.32H11.66C8.91,12.32 6.67,10.08 6.67,7.33V7A5,5 0 0,1 11.67,2H12M4.87,19.5L7.54,16.83C6.35,15.78 5.5,14.35 5.16,12.72L3.7,14.18C2.32,15.56 2.32,17.82 3.7,19.2C4.09,19.59 4.72,19.59 5.11,19.2L4.87,19.5Z" },
                new ThemeInfo { Mode = ThemeMode.HighContrastLight, Name = "High Contrast Light", Description = "High contrast light theme for accessibility", Icon = "M12,8A4,4 0 0,0 8,12A4,4 0 0,0 12,16A4,4 0 0,0 16,12A4,4 0 0,0 12,8M12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6A6,6 0 0,1 18,12A6,6 0 0,1 12,18M20,8.69V4H15.31L12,0.69L8.69,4H4V8.69L0.69,12L4,15.31V20H8.69L12,23.31L15.31,20H20V15.31L23.31,12L20,8.69Z" },
                new ThemeInfo { Mode = ThemeMode.System, Name = "System", Description = "Follow system theme", Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20V4Z" }
            };

            LoadSettings();
        }

        #region Public Methods

        /// <summary>
        /// Sets the application theme.
        /// </summary>
        public void SetTheme(ThemeMode theme)
        {
            try
            {
                var actualTheme = theme;
                if (theme == ThemeMode.System)
                {
                    actualTheme = DetectSystemTheme();
                }

                ApplyTheme(actualTheme);
                CurrentTheme = theme;
                SaveSettings();

                Logger.Info($"Theme changed to: {theme} (applied: {actualTheme})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set theme: {theme}");
            }
        }

        /// <summary>
        /// Toggles between dark and light modes.
        /// </summary>
        public void ToggleDarkMode()
        {
            if (IsDarkMode)
            {
                SetTheme(IsHighContrastMode ? ThemeMode.HighContrastLight : ThemeMode.Light);
            }
            else
            {
                SetTheme(IsHighContrastMode ? ThemeMode.HighContrastDark : ThemeMode.Dark);
            }
        }

        /// <summary>
        /// Toggles high contrast mode.
        /// </summary>
        public void ToggleHighContrast()
        {
            if (IsHighContrastMode)
            {
                SetTheme(IsDarkMode ? ThemeMode.Dark : ThemeMode.Light);
            }
            else
            {
                SetTheme(IsDarkMode ? ThemeMode.HighContrastDark : ThemeMode.HighContrastLight);
            }
        }

        /// <summary>
        /// Gets a brush from the current theme.
        /// </summary>
        public System.Windows.Media.Brush GetBrush(string key)
        {
            if (_currentThemeResources?.Contains(key) == true)
            {
                return _currentThemeResources[key] as System.Windows.Media.Brush;
            }
            return Application.Current.TryFindResource(key) as System.Windows.Media.Brush;
        }

        /// <summary>
        /// Gets a color from the current theme.
        /// </summary>
        public System.Windows.Media.Color? GetColor(string key)
        {
            if (_currentThemeResources?.Contains(key) == true)
            {
                return _currentThemeResources[key] as System.Windows.Media.Color?;
            }
            var resource = Application.Current.TryFindResource(key);
            return resource as System.Windows.Media.Color?;
        }

        #endregion

        #region Private Methods

        private void ApplyTheme(ThemeMode theme)
        {
            // Remove existing theme resources
            if (_currentThemeResources != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(_currentThemeResources);
            }

            // Create new theme resources
            _currentThemeResources = new ResourceDictionary();

            switch (theme)
            {
                case ThemeMode.Dark:
                    ApplyDarkTheme(_currentThemeResources);
                    break;

                case ThemeMode.Light:
                    ApplyLightTheme(_currentThemeResources);
                    break;

                case ThemeMode.HighContrastDark:
                    ApplyHighContrastDarkTheme(_currentThemeResources);
                    break;

                case ThemeMode.HighContrastLight:
                    ApplyHighContrastLightTheme(_currentThemeResources);
                    break;
            }

            Application.Current.Resources.MergedDictionaries.Add(_currentThemeResources);
        }

        private void ApplyDarkTheme(ResourceDictionary dict)
        {
            // Dark theme colors (default)
            dict["BackgroundColor"] = System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x2E);
            dict["SurfaceColor"] = System.Windows.Media.Color.FromRgb(0x16, 0x21, 0x3E);
            dict["CardColor"] = System.Windows.Media.Color.FromRgb(0x1F, 0x2B, 0x47);
            dict["ForegroundColor"] = System.Windows.Media.Color.FromRgb(0xE4, 0xE4, 0xE4);
            dict["ForegroundSecondaryColor"] = System.Windows.Media.Color.FromRgb(0xA0, 0xA0, 0xA0);
            dict["AccentColor"] = System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6);
            dict["BorderColor"] = System.Windows.Media.Color.FromRgb(0x2D, 0x37, 0x48);

            CreateBrushes(dict);
        }

        private void ApplyLightTheme(ResourceDictionary dict)
        {
            // Light theme colors
            dict["BackgroundColor"] = System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5);
            dict["SurfaceColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF);
            dict["CardColor"] = System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8);
            dict["ForegroundColor"] = System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A);
            dict["ForegroundSecondaryColor"] = System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60);
            dict["AccentColor"] = System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB);
            dict["BorderColor"] = System.Windows.Media.Color.FromRgb(0xD0, 0xD0, 0xD0);

            CreateBrushes(dict);
        }

        private void ApplyHighContrastDarkTheme(ResourceDictionary dict)
        {
            // High contrast dark colors
            dict["BackgroundColor"] = System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00);
            dict["SurfaceColor"] = System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A);
            dict["CardColor"] = System.Windows.Media.Color.FromRgb(0x26, 0x26, 0x26);
            dict["ForegroundColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF);
            dict["ForegroundSecondaryColor"] = System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0);
            dict["AccentColor"] = System.Windows.Media.Color.FromRgb(0x00, 0xD4, 0xFF);
            dict["BorderColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF);

            // High visibility status colors
            dict["SuccessColor"] = System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x7F);
            dict["WarningColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0x00);
            dict["ErrorColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0x33, 0x33);
            dict["FocusColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0x00);

            CreateBrushes(dict);
            CreateHighContrastBrushes(dict);
        }

        private void ApplyHighContrastLightTheme(ResourceDictionary dict)
        {
            // High contrast light colors
            dict["BackgroundColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF);
            dict["SurfaceColor"] = System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF);
            dict["CardColor"] = System.Windows.Media.Color.FromRgb(0xF0, 0xF0, 0xF0);
            dict["ForegroundColor"] = System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00);
            dict["ForegroundSecondaryColor"] = System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20);
            dict["AccentColor"] = System.Windows.Media.Color.FromRgb(0x00, 0x00, 0xCC);
            dict["BorderColor"] = System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00);

            // High visibility status colors
            dict["SuccessColor"] = System.Windows.Media.Color.FromRgb(0x00, 0x80, 0x00);
            dict["WarningColor"] = System.Windows.Media.Color.FromRgb(0xB8, 0x86, 0x0B);
            dict["ErrorColor"] = System.Windows.Media.Color.FromRgb(0xCC, 0x00, 0x00);
            dict["FocusColor"] = System.Windows.Media.Color.FromRgb(0x00, 0x00, 0xFF);

            CreateBrushes(dict);
            CreateHighContrastBrushes(dict);
        }

        private void CreateBrushes(ResourceDictionary dict)
        {
            dict["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["BackgroundColor"]);
            dict["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["SurfaceColor"]);
            dict["CardBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["CardColor"]);
            dict["ForegroundBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["ForegroundColor"]);
            dict["ForegroundSecondaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["ForegroundSecondaryColor"]);
            dict["AccentBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["AccentColor"]);
            dict["BorderBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["BorderColor"]);
        }

        private void CreateHighContrastBrushes(ResourceDictionary dict)
        {
            dict["SuccessBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["SuccessColor"]);
            dict["WarningBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["WarningColor"]);
            dict["ErrorBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["ErrorColor"]);
            dict["FocusBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dict["FocusColor"]);
        }

        private ThemeMode DetectSystemTheme()
        {
            try
            {
                // Try to detect Windows theme using registry
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        // Check if Windows high contrast is enabled
                        if (SystemParameters.HighContrast)
                        {
                            return intValue == 0 ? ThemeMode.HighContrastDark : ThemeMode.HighContrastLight;
                        }
                        return intValue == 0 ? ThemeMode.Dark : ThemeMode.Light;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not detect system theme: {ex.Message}");
            }

            return ThemeMode.Dark; // Default to dark
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonConvert.DeserializeObject<ThemeSettings>(json);
                    if (settings != null)
                    {
                        SetTheme(settings.Theme);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not load theme settings: {ex.Message}");
            }

            // Default theme
            SetTheme(ThemeMode.Dark);
        }

        private void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new ThemeSettings { Theme = _currentTheme };
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not save theme settings: {ex.Message}");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Available theme modes.
    /// </summary>
    public enum ThemeMode
    {
        Dark,
        Light,
        HighContrastDark,
        HighContrastLight,
        System
    }

    /// <summary>
    /// Information about a theme.
    /// </summary>
    public class ThemeInfo
    {
        public ThemeMode Mode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }

    /// <summary>
    /// Theme settings for persistence.
    /// </summary>
    internal class ThemeSettings
    {
        public ThemeMode Theme { get; set; }
    }
}
