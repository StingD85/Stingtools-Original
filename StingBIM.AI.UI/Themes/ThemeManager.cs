// StingBIM.AI.UI.Themes.ThemeManager
// Manages theme switching between Light and Dark modes
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - Theme Management

using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.UI.Themes
{
    /// <summary>
    /// Manages application themes (Light/Dark mode).
    /// Provides theme switching and persistence.
    /// </summary>
    public class ThemeManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static ThemeManager _instance;
        private static readonly object _lock = new object();

        private AppTheme _currentTheme;
        private readonly string _preferencePath;

        /// <summary>
        /// Gets the singleton instance of the ThemeManager.
        /// </summary>
        public static ThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ThemeManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Event fired when the theme changes.
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        /// <summary>
        /// Gets the current theme.
        /// </summary>
        public AppTheme CurrentTheme => _currentTheme;

        /// <summary>
        /// Gets whether the current theme is dark.
        /// </summary>
        public bool IsDarkTheme => _currentTheme == AppTheme.Dark;

        private ThemeManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _preferencePath = Path.Combine(appData, "StingBIM", "AI", "theme.json");

            // Load saved preference or default to Dark
            _currentTheme = LoadThemePreference();
            Logger.Info($"ThemeManager initialized with theme: {_currentTheme}");
        }

        /// <summary>
        /// Sets the application theme.
        /// </summary>
        /// <param name="theme">The theme to apply.</param>
        /// <param name="savePreference">Whether to save the preference to disk.</param>
        public void SetTheme(AppTheme theme, bool savePreference = true)
        {
            if (_currentTheme == theme)
                return;

            var oldTheme = _currentTheme;
            _currentTheme = theme;

            ApplyTheme(theme);

            if (savePreference)
            {
                SaveThemePreference(theme);
            }

            Logger.Info($"Theme changed from {oldTheme} to {theme}");
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, theme));
        }

        /// <summary>
        /// Toggles between Light and Dark themes.
        /// </summary>
        public void ToggleTheme()
        {
            SetTheme(_currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
        }

        /// <summary>
        /// Sets the theme based on a string value (for settings binding).
        /// </summary>
        /// <param name="themeName">Theme name: "Dark", "Light", or "System".</param>
        public void SetTheme(string themeName)
        {
            var theme = themeName?.ToLowerInvariant() switch
            {
                "light" => AppTheme.Light,
                "system" => GetSystemTheme(),
                _ => AppTheme.Dark
            };

            SetTheme(theme);
        }

        /// <summary>
        /// Applies the specified theme to all application windows.
        /// </summary>
        private void ApplyTheme(AppTheme theme)
        {
            var themeUri = theme switch
            {
                AppTheme.Light => new Uri("pack://application:,,,/StingBIM.AI.UI;component/Themes/LightTheme.xaml"),
                _ => new Uri("pack://application:,,,/StingBIM.AI.UI;component/Themes/AITheme.xaml")
            };

            try
            {
                // Find and replace the theme dictionary in the application resources
                var newTheme = new ResourceDictionary { Source = themeUri };

                // Update application-level resources
                if (Application.Current != null)
                {
                    var mergedDicts = Application.Current.Resources.MergedDictionaries;

                    // Find and remove existing theme dictionary
                    ResourceDictionary existingTheme = null;
                    foreach (var dict in mergedDicts)
                    {
                        if (dict.Source != null &&
                            (dict.Source.OriginalString.Contains("AITheme.xaml") ||
                             dict.Source.OriginalString.Contains("LightTheme.xaml")))
                        {
                            existingTheme = dict;
                            break;
                        }
                    }

                    if (existingTheme != null)
                    {
                        mergedDicts.Remove(existingTheme);
                    }

                    // Add new theme
                    mergedDicts.Add(newTheme);
                }

                // Update all open windows
                foreach (Window window in Application.Current?.Windows ?? new WindowCollection())
                {
                    ApplyThemeToWindow(window, themeUri);
                }

                Logger.Debug($"Applied theme: {theme}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to apply theme: {theme}");
            }
        }

        /// <summary>
        /// Applies the theme to a specific window.
        /// </summary>
        public void ApplyThemeToWindow(Window window, Uri themeUri = null)
        {
            if (window == null)
                return;

            themeUri ??= _currentTheme switch
            {
                AppTheme.Light => new Uri("pack://application:,,,/StingBIM.AI.UI;component/Themes/LightTheme.xaml"),
                _ => new Uri("pack://application:,,,/StingBIM.AI.UI;component/Themes/AITheme.xaml")
            };

            try
            {
                var newTheme = new ResourceDictionary { Source = themeUri };
                var mergedDicts = window.Resources.MergedDictionaries;

                // Find and remove existing theme
                ResourceDictionary existingTheme = null;
                foreach (var dict in mergedDicts)
                {
                    if (dict.Source != null &&
                        (dict.Source.OriginalString.Contains("AITheme.xaml") ||
                         dict.Source.OriginalString.Contains("LightTheme.xaml")))
                    {
                        existingTheme = dict;
                        break;
                    }
                }

                if (existingTheme != null)
                {
                    mergedDicts.Remove(existingTheme);
                }

                // Add new theme at the beginning
                mergedDicts.Insert(0, newTheme);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to apply theme to window: {window.Title}");
            }
        }

        /// <summary>
        /// Gets the system theme preference (Windows 10/11).
        /// </summary>
        private AppTheme GetSystemTheme()
        {
            try
            {
                // Read from Windows registry
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                if (key != null)
                {
                    var value = key.GetValue("AppsUseLightTheme");
                    if (value is int useLightTheme)
                    {
                        return useLightTheme == 1 ? AppTheme.Light : AppTheme.Dark;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to read system theme preference");
            }

            return AppTheme.Dark; // Default to dark
        }

        /// <summary>
        /// Loads the saved theme preference.
        /// </summary>
        private AppTheme LoadThemePreference()
        {
            try
            {
                if (File.Exists(_preferencePath))
                {
                    var json = File.ReadAllText(_preferencePath);
                    var pref = JsonConvert.DeserializeObject<ThemePreference>(json);
                    return pref?.Theme ?? AppTheme.Dark;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load theme preference");
            }

            return AppTheme.Dark;
        }

        /// <summary>
        /// Saves the theme preference.
        /// </summary>
        private void SaveThemePreference(AppTheme theme)
        {
            try
            {
                var directory = Path.GetDirectoryName(_preferencePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var pref = new ThemePreference { Theme = theme };
                var json = JsonConvert.SerializeObject(pref, Formatting.Indented);
                File.WriteAllText(_preferencePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save theme preference");
            }
        }

        private class ThemePreference
        {
            public AppTheme Theme { get; set; }
        }
    }

    /// <summary>
    /// Available application themes.
    /// </summary>
    public enum AppTheme
    {
        /// <summary>
        /// Dark theme (default).
        /// </summary>
        Dark,

        /// <summary>
        /// Light theme.
        /// </summary>
        Light
    }

    /// <summary>
    /// Event arguments for theme change events.
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The previous theme.
        /// </summary>
        public AppTheme OldTheme { get; }

        /// <summary>
        /// The new theme.
        /// </summary>
        public AppTheme NewTheme { get; }

        public ThemeChangedEventArgs(AppTheme oldTheme, AppTheme newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }
}
