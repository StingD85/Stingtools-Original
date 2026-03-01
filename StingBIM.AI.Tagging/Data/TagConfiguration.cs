// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagConfiguration.cs - Centralized configuration management
// Follows StingBIMConfig pattern with JSON persistence and hot-reload

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Data
{
    /// <summary>
    /// Centralized configuration for the SuperIntelligent Tagging System.
    /// Supports both project-level settings (persisted in Revit extensible storage)
    /// and user-level settings (persisted in %APPDATA%/StingBIM/).
    /// Follows the StingBIMConfig singleton pattern with FileSystemWatcher hot-reload.
    /// </summary>
    public sealed class TagConfiguration
    {
        private static readonly Lazy<TagConfiguration> _instance =
            new Lazy<TagConfiguration>(() => new TagConfiguration());
        public static TagConfiguration Instance => _instance.Value;

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _settingsLock = new object();
        private TagSettings _settings;
        private FileSystemWatcher _configWatcher;
        private string _configFilePath;
        private DateTime _lastLoadTime;

        #region Settings Properties

        /// <summary>Current settings snapshot. Thread-safe.</summary>
        public TagSettings Settings
        {
            get { lock (_settingsLock) { return _settings; } }
        }

        #endregion

        #region Initialization

        private TagConfiguration()
        {
            _settings = new TagSettings();
            InitializeConfigPath();
            LoadSettings();
            InitializeFileWatcher();
        }

        private void InitializeConfigPath()
        {
            try
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StingBIM");

                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                _configFilePath = Path.Combine(appDataPath, "TaggingConfig.json");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize config path");
                _configFilePath = null;
            }
        }

        private void InitializeFileWatcher()
        {
            if (string.IsNullOrEmpty(_configFilePath)) return;

            try
            {
                string directory = Path.GetDirectoryName(_configFilePath);
                string fileName = Path.GetFileName(_configFilePath);

                _configWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _configWatcher.Changed += OnConfigFileChanged;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to initialize config file watcher");
            }
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce: ignore if last load was less than 1 second ago
            if ((DateTime.UtcNow - _lastLoadTime).TotalSeconds < 1.0) return;

            Logger.Info("Tagging configuration file changed, reloading...");
            LoadSettings();
        }

        #endregion

        #region Load / Save

        /// <summary>
        /// Loads settings from the JSON config file. Creates defaults if not found.
        /// </summary>
        public void LoadSettings()
        {
            lock (_settingsLock)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_configFilePath) && File.Exists(_configFilePath))
                    {
                        string json = File.ReadAllText(_configFilePath);
                        _settings = JsonConvert.DeserializeObject<TagSettings>(json) ?? new TagSettings();
                        Logger.Info("Tagging configuration loaded from {0}", _configFilePath);
                    }
                    else
                    {
                        _settings = new TagSettings();
                        SaveSettings();
                        Logger.Info("Default tagging configuration created");
                    }
                    _lastLoadTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load tagging configuration, using defaults");
                    _settings = new TagSettings();
                }
            }
        }

        /// <summary>
        /// Saves current settings to the JSON config file.
        /// </summary>
        public void SaveSettings()
        {
            lock (_settingsLock)
            {
                try
                {
                    if (string.IsNullOrEmpty(_configFilePath)) return;

                    string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                    File.WriteAllText(_configFilePath, json);
                    _lastLoadTime = DateTime.UtcNow;
                    Logger.Info("Tagging configuration saved to {0}", _configFilePath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to save tagging configuration");
                }
            }
        }

        /// <summary>
        /// Updates settings with a modification action and auto-saves.
        /// </summary>
        public void UpdateSettings(Action<TagSettings> modifier)
        {
            lock (_settingsLock)
            {
                modifier(_settings);
                SaveSettings();
            }
        }

        #endregion
    }

    /// <summary>
    /// Complete settings for the SuperIntelligent Tagging System.
    /// </summary>
    public class TagSettings
    {
        #region Placement Settings

        /// <summary>Default placement strategy when none is specified.</summary>
        public PlacementStrategy DefaultPlacementStrategy { get; set; } = PlacementStrategy.Automatic;

        /// <summary>Default alignment mode.</summary>
        public AlignmentMode DefaultAlignmentMode { get; set; } = AlignmentMode.Relaxed;

        /// <summary>Minimum clearance between tags in model units (default ~2mm).</summary>
        public double CollisionClearance { get; set; } = 0.002;

        /// <summary>Maximum number of candidate positions to evaluate per tag.</summary>
        public int MaxCandidatePositions { get; set; } = 24;

        /// <summary>Maximum fallback levels before flagging for manual review.</summary>
        public int MaxFallbackLevels { get; set; } = 5;

        /// <summary>Default leader distance threshold in model units.</summary>
        public double DefaultLeaderDistanceThreshold { get; set; } = 0.01;

        /// <summary>Whether to replace existing tags by default.</summary>
        public bool DefaultReplaceExisting { get; set; } = false;

        /// <summary>Whether to include linked file elements by default.</summary>
        public bool DefaultIncludeLinkedFiles { get; set; } = false;

        /// <summary>Whether to enable cluster detection by default.</summary>
        public bool DefaultEnableClusterDetection { get; set; } = true;

        #endregion

        #region Intelligence Settings

        /// <summary>Whether the learning engine is enabled.</summary>
        public bool LearningEnabled { get; set; } = true;

        /// <summary>Minimum observations before learning predictions are used.</summary>
        public int LearningConfidenceThreshold { get; set; } = 10;

        /// <summary>Confidence decay rate per day without reinforcement (0.0-1.0).</summary>
        public double ConfidenceDecayRate { get; set; } = 0.02;

        /// <summary>Weight for the intelligence score in placement decisions (0.0-1.0).</summary>
        public double IntelligenceScoreWeight { get; set; } = 0.3;

        #endregion

        #region Quality Settings

        /// <summary>Whether to auto-run quality checks after batch placement.</summary>
        public bool AutoRunQualityCheck { get; set; } = true;

        /// <summary>Collision overlap sensitivity (0.0-1.0). Lower = more sensitive.</summary>
        public double ClashSensitivity { get; set; } = 0.5;

        /// <summary>Whether to auto-fix non-destructive issues (stale text, etc.).</summary>
        public bool AutoFixNonDestructive { get; set; } = false;

        /// <summary>Expected tag format patterns per category (for UnexpectedValue checks).</summary>
        public Dictionary<string, string> ExpectedFormats { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Doors", @"^D-?\d{1,4}[A-Z]?$" },
            { "Windows", @"^W-?\d{1,4}[A-Z]?$" },
            { "Rooms", @"^.+$" }
        };

        #endregion

        #region Score Weights

        /// <summary>Weight for proximity score component (0.0-1.0).</summary>
        public double ProximityWeight { get; set; } = 0.25;

        /// <summary>Weight for collision score component (0.0-1.0).</summary>
        public double CollisionWeight { get; set; } = 0.30;

        /// <summary>Weight for alignment score component (0.0-1.0).</summary>
        public double AlignmentWeight { get; set; } = 0.15;

        /// <summary>Weight for leader score component (0.0-1.0).</summary>
        public double LeaderWeight { get; set; } = 0.10;

        /// <summary>Weight for readability score component (0.0-1.0).</summary>
        public double ReadabilityWeight { get; set; } = 0.10;

        /// <summary>Weight for template priority compliance (0.0-1.0).</summary>
        public double TemplatePriorityWeight { get; set; } = 0.10;

        #endregion

        #region View Coordination Settings

        /// <summary>Whether to enforce cross-view tag content consistency.</summary>
        public bool EnforceCrossViewConsistency { get; set; } = true;

        /// <summary>Default batch processing thread count (1 = sequential).</summary>
        public int BatchProcessingThreads { get; set; } = 1;

        /// <summary>Whether to account for sheet layout during placement.</summary>
        public bool SheetAwarePlacement { get; set; } = true;

        #endregion

        #region Cluster Settings

        /// <summary>Distance threshold for proximity-based clustering (model units).</summary>
        public double ClusterEpsilon { get; set; } = 0.5;

        /// <summary>Minimum elements to form a cluster.</summary>
        public int ClusterMinPoints { get; set; } = 3;

        /// <summary>Default cluster tagging strategy.</summary>
        public ClusterTagStrategy DefaultClusterStrategy { get; set; } = ClusterTagStrategy.TagTypical;

        /// <summary>Parameter name to write cluster count into (for TagWithCount strategy).</summary>
        public string ClusterCountParameterName { get; set; } = "Comments";

        #endregion

        #region Category Default Overrides

        /// <summary>Per-category default template names.</summary>
        public Dictionary<string, string> CategoryDefaultTemplates { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Per-category default tag family names.</summary>
        public Dictionary<string, string> CategoryDefaultTagFamilies { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Per-view-type default settings.</summary>
        public Dictionary<TagViewType, ViewTypeDefaults> ViewTypeSettings { get; set; } =
            new Dictionary<TagViewType, ViewTypeDefaults>();

        #endregion
    }

    /// <summary>
    /// Default settings specific to a view type.
    /// </summary>
    public class ViewTypeDefaults
    {
        /// <summary>Default alignment mode for this view type.</summary>
        public AlignmentMode Alignment { get; set; } = AlignmentMode.Relaxed;

        /// <summary>Default tag orientation for this view type.</summary>
        public TagOrientation Orientation { get; set; } = TagOrientation.Horizontal;

        /// <summary>Whether tags follow element rotation in this view type.</summary>
        public bool FollowElementRotation { get; set; }

        /// <summary>Default placement strategy for this view type.</summary>
        public PlacementStrategy PlacementStrategy { get; set; } = PlacementStrategy.Automatic;

        /// <summary>Whether to enable strict alignment rails.</summary>
        public bool UseAlignmentRails { get; set; }
    }
}
