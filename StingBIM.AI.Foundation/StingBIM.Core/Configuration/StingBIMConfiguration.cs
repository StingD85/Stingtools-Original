// ============================================================================
// StingBIM Core - Configuration Management
// Centralized configuration for all StingBIM modules
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.Core.Configuration
{
    /// <summary>
    /// Centralized configuration management for StingBIM.
    /// Supports JSON configuration files with environment overrides.
    /// </summary>
    public class StingBIMConfiguration
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<StingBIMConfiguration> _instance =
            new Lazy<StingBIMConfiguration>(() => new StingBIMConfiguration());

        private readonly Dictionary<string, object> _settings;
        private string _configPath;
        private DateTime _lastLoaded;

        public static StingBIMConfiguration Instance => _instance.Value;

        // AI Configuration
        public AIConfiguration AI { get; private set; }

        // Revit Configuration
        public RevitConfiguration Revit { get; private set; }

        // Data Configuration
        public DataConfiguration Data { get; private set; }

        // Standards Configuration
        public StandardsConfiguration Standards { get; private set; }

        private StingBIMConfiguration()
        {
            _settings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _configPath = GetConfigPath();

            AI = new AIConfiguration();
            Revit = new RevitConfiguration();
            Data = new DataConfiguration();
            Standards = new StandardsConfiguration();

            LoadConfiguration();
        }

        /// <summary>
        /// Initializes configuration with an optional custom path.
        /// Forces the singleton to be created and optionally reloads from the specified path.
        /// </summary>
        public static void Initialize(string configPath = null)
        {
            var instance = Instance;
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                instance._configPath = configPath;
                instance.LoadConfiguration();
            }
        }

        /// <summary>
        /// Loads configuration from JSON file.
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonConvert.DeserializeObject<ConfigurationRoot>(json);

                    if (config != null)
                    {
                        AI = config.AI ?? new AIConfiguration();
                        Revit = config.Revit ?? new RevitConfiguration();
                        Data = config.Data ?? new DataConfiguration();
                        Standards = config.Standards ?? new StandardsConfiguration();
                    }

                    _lastLoaded = DateTime.UtcNow;
                    Logger.Info($"Configuration loaded from: {_configPath}");
                }
                else
                {
                    Logger.Warn($"Configuration file not found: {_configPath}. Using defaults.");
                    SaveDefaultConfiguration();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load configuration. Using defaults.");
            }
        }

        /// <summary>
        /// Saves current configuration to file.
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                var config = new ConfigurationRoot
                {
                    AI = AI,
                    Revit = Revit,
                    Data = Data,
                    Standards = Standards
                };

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);

                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_configPath, json);
                Logger.Info($"Configuration saved to: {_configPath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save configuration.");
            }
        }

        /// <summary>
        /// Gets a custom setting value.
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue = default)
        {
            if (_settings.TryGetValue(key, out var value) && value is T typed)
            {
                return typed;
            }
            return defaultValue;
        }

        /// <summary>
        /// Sets a custom setting value.
        /// </summary>
        public void SetSetting(string key, object value)
        {
            _settings[key] = value;
        }

        private string GetConfigPath()
        {
            // Check environment variable first
            var envPath = Environment.GetEnvironmentVariable("STINGBIM_CONFIG");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            // Default to AppData location
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "StingBIM", "config", "stingbim.json");
        }

        private void SaveDefaultConfiguration()
        {
            SaveConfiguration();
        }

        private class ConfigurationRoot
        {
            public AIConfiguration AI { get; set; }
            public RevitConfiguration Revit { get; set; }
            public DataConfiguration Data { get; set; }
            public StandardsConfiguration Standards { get; set; }
        }
    }

    /// <summary>
    /// AI-specific configuration settings.
    /// </summary>
    public class AIConfiguration
    {
        public string ModelsPath { get; set; } = "models";
        public string KnowledgeBasePath { get; set; } = "data/ai";
        public int MaxWorkingMemoryItems { get; set; } = 7;
        public int EpisodicMemoryRetentionDays { get; set; } = 30;
        public float ConsensusThreshold { get; set; } = 0.7f;
        public int MaxConsensusRounds { get; set; } = 3;
        public int AgentTimeoutSeconds { get; set; } = 5;
        public bool EnableVoiceCommands { get; set; } = true;
        public bool EnableLearning { get; set; } = true;

        // Model paths
        public string LanguageModelPath { get; set; } = "models/phi-3-mini-4k.onnx";
        public string EmbeddingModelPath { get; set; } = "models/all-MiniLM-L6-v2.onnx";
        public string SpeechModelPath { get; set; } = "models/whisper-tiny.onnx";
    }

    /// <summary>
    /// Revit-specific configuration settings.
    /// </summary>
    public class RevitConfiguration
    {
        public string SharedParametersPath { get; set; } = "data/parameters/SharedParameters.txt";
        public bool AutoCreateParameters { get; set; } = true;
        public bool EnableTransactionLogging { get; set; } = true;
        public int MaxUndoOperations { get; set; } = 50;
        public bool SafetyValidationEnabled { get; set; } = true;
    }

    /// <summary>
    /// Data storage configuration settings.
    /// </summary>
    public class DataConfiguration
    {
        public string DatabasePath { get; set; } = "data/stingbim.db";
        public string MaterialsPath { get; set; } = "data/ai/materials";
        public string FormulasPath { get; set; } = "data/ai/parameters";
        public string ScheduleTemplatesPath { get; set; } = "data/templates/schedules";
        public bool EnableCaching { get; set; } = true;
        public int CacheExpirationMinutes { get; set; } = 60;
    }

    /// <summary>
    /// Building standards configuration settings.
    /// </summary>
    public class StandardsConfiguration
    {
        public string DefaultRegion { get; set; } = "International";
        public string StandardsDataPath { get; set; } = "data/ai/standards";
        public List<string> EnabledStandards { get; set; } = new List<string>
        {
            "IBC", "ASHRAE", "ADA", "ISO19650"
        };
        public bool StrictComplianceMode { get; set; } = false;
    }
}
