using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.Core.Config
{
    /// <summary>
    /// Singleton configuration manager for StingBIM
    /// Handles loading, saving, and validation of application settings
    /// Supports hot reload for development scenarios
    /// </summary>
    public sealed class StingBIMConfig
    {
        #region Singleton Implementation
        
        private static readonly Lazy<StingBIMConfig> _instance = 
            new Lazy<StingBIMConfig>(() => new StingBIMConfig());
        
        /// <summary>
        /// Gets the singleton instance of StingBIMConfig
        /// </summary>
        public static StingBIMConfig Instance => _instance.Value;
        
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        #endregion

        #region Private Fields
        
        private readonly string _configFilePath;
        private FileSystemWatcher _fileWatcher;
        private ConfigData _currentConfig;
        private readonly object _configLock = new object();
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Private constructor for singleton pattern
        /// Initializes configuration and sets up file watching
        /// </summary>
        private StingBIMConfig()
        {
            try
            {
                // Set config file path
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string stingBIMFolder = Path.Combine(appDataPath, "StingBIM");
                
                // Create directory if it doesn't exist
                if (!Directory.Exists(stingBIMFolder))
                {
                    Directory.CreateDirectory(stingBIMFolder);
                    _logger.Info($"Created StingBIM configuration directory: {stingBIMFolder}");
                }
                
                _configFilePath = Path.Combine(stingBIMFolder, "StingBIM.config.json");
                
                // Load or create configuration
                LoadConfiguration();
                
                // Setup file watcher for hot reload
                SetupFileWatcher(stingBIMFolder);
                
                _logger.Info("StingBIMConfig initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize StingBIMConfig");
                throw;
            }
        }
        
        #endregion

        #region Public Properties
        
        /// <summary>
        /// Gets the data directory path for parameters, schedules, materials, etc.
        /// </summary>
        public string DataDirectory
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.DataDirectory ?? 
                           Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                      "StingBIM", "Data");
                }
            }
        }
        
        /// <summary>
        /// Gets the parameters file path (MR_PARAMETERS.txt)
        /// </summary>
        public string ParametersFilePath
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.ParametersFilePath ?? 
                           Path.Combine(DataDirectory, "MR_PARAMETERS.txt");
                }
            }
        }
        
        /// <summary>
        /// Gets whether GPU acceleration is enabled for batch operations
        /// </summary>
        public bool EnableGPUAcceleration
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.EnableGPUAcceleration ?? true;
                }
            }
        }
        
        /// <summary>
        /// Gets the batch processing size for element operations
        /// </summary>
        public int BatchProcessingSize
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.BatchProcessingSize ?? 1000;
                }
            }
        }
        
        /// <summary>
        /// Gets whether AI features are enabled
        /// </summary>
        public bool EnableAIFeatures
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.EnableAIFeatures ?? true;
                }
            }
        }
        
        /// <summary>
        /// Gets the log level for NLog
        /// </summary>
        public string LogLevel
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.LogLevel ?? "Info";
                }
            }
        }
        
        /// <summary>
        /// Gets whether to enable performance metrics collection
        /// </summary>
        public bool EnablePerformanceMetrics
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.EnablePerformanceMetrics ?? true;
                }
            }
        }
        
        /// <summary>
        /// Gets the cache size limit in MB for parameter caching
        /// </summary>
        public int CacheSizeLimitMB
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.CacheSizeLimitMB ?? 500;
                }
            }
        }
        
        /// <summary>
        /// Gets custom settings dictionary for extensions
        /// </summary>
        public Dictionary<string, object> CustomSettings
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfig?.CustomSettings ?? new Dictionary<string, object>();
                }
            }
        }
        
        #endregion

        #region Configuration Management
        
        /// <summary>
        /// Loads configuration from file or creates default configuration
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    // Load existing configuration
                    string json = File.ReadAllText(_configFilePath);
                    _currentConfig = JsonConvert.DeserializeObject<ConfigData>(json);
                    
                    // Validate configuration
                    if (ValidateConfiguration(_currentConfig))
                    {
                        _logger.Info($"Configuration loaded from: {_configFilePath}");
                    }
                    else
                    {
                        _logger.Warn("Configuration validation failed, creating default configuration");
                        CreateDefaultConfiguration();
                    }
                }
                else
                {
                    // Create default configuration
                    CreateDefaultConfiguration();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load configuration, using defaults");
                CreateDefaultConfiguration();
            }
        }
        
        /// <summary>
        /// Creates and saves default configuration
        /// </summary>
        private void CreateDefaultConfiguration()
        {
            try
            {
                _currentConfig = new ConfigData
                {
                    DataDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                        "StingBIM", "Data"),
                    ParametersFilePath = null, // Will use default from DataDirectory
                    EnableGPUAcceleration = true,
                    BatchProcessingSize = 1000,
                    EnableAIFeatures = true,
                    LogLevel = "Info",
                    EnablePerformanceMetrics = true,
                    CacheSizeLimitMB = 500,
                    CustomSettings = new Dictionary<string, object>()
                };
                
                SaveConfiguration();
                _logger.Info("Default configuration created and saved");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create default configuration");
                throw;
            }
        }
        
        /// <summary>
        /// Saves current configuration to file
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                lock (_configLock)
                {
                    string json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                    File.WriteAllText(_configFilePath, json);
                    _logger.Info("Configuration saved successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save configuration");
                throw;
            }
        }
        
        /// <summary>
        /// Validates configuration data
        /// </summary>
        /// <param name="config">Configuration to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private bool ValidateConfiguration(ConfigData config)
        {
            if (config == null)
            {
                _logger.Error("Configuration is null");
                return false;
            }
            
            // Validate data directory
            if (string.IsNullOrWhiteSpace(config.DataDirectory))
            {
                _logger.Error("DataDirectory is null or empty");
                return false;
            }
            
            // Validate batch size
            if (config.BatchProcessingSize < 1 || config.BatchProcessingSize > 100000)
            {
                _logger.Error($"Invalid BatchProcessingSize: {config.BatchProcessingSize}");
                return false;
            }
            
            // Validate cache size
            if (config.CacheSizeLimitMB < 10 || config.CacheSizeLimitMB > 10000)
            {
                _logger.Error($"Invalid CacheSizeLimitMB: {config.CacheSizeLimitMB}");
                return false;
            }
            
            // Validate log level
            var validLogLevels = new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };
            if (!Array.Exists(validLogLevels, level => level.Equals(config.LogLevel, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.Error($"Invalid LogLevel: {config.LogLevel}");
                return false;
            }
            
            return true;
        }
        
        #endregion

        #region File Watching (Hot Reload)
        
        /// <summary>
        /// Sets up file system watcher for configuration changes
        /// </summary>
        /// <param name="watchDirectory">Directory to watch</param>
        private void SetupFileWatcher(string watchDirectory)
        {
            try
            {
                _fileWatcher = new FileSystemWatcher(watchDirectory, "StingBIM.config.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                
                _fileWatcher.Changed += OnConfigFileChanged;
                _logger.Debug("File watcher setup for configuration hot reload");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to setup file watcher, hot reload disabled");
            }
        }
        
        /// <summary>
        /// Handles configuration file change events
        /// </summary>
        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Debounce: Wait a bit to ensure file write is complete
                System.Threading.Thread.Sleep(100);
                
                _logger.Info("Configuration file changed, reloading...");
                LoadConfiguration();
                
                // Raise event for subscribers
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reload configuration after file change");
            }
        }
        
        #endregion

        #region Events
        
        /// <summary>
        /// Event raised when configuration is reloaded
        /// </summary>
        public event EventHandler ConfigurationChanged;
        
        #endregion

        #region Update Methods
        
        /// <summary>
        /// Updates the data directory path
        /// </summary>
        /// <param name="path">New data directory path</param>
        public void UpdateDataDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Data directory path cannot be null or empty", nameof(path));
            
            lock (_configLock)
            {
                _currentConfig.DataDirectory = path;
                SaveConfiguration();
                _logger.Info($"Data directory updated to: {path}");
            }
        }
        
        /// <summary>
        /// Updates GPU acceleration setting
        /// </summary>
        /// <param name="enabled">Whether to enable GPU acceleration</param>
        public void UpdateGPUAcceleration(bool enabled)
        {
            lock (_configLock)
            {
                _currentConfig.EnableGPUAcceleration = enabled;
                SaveConfiguration();
                _logger.Info($"GPU acceleration set to: {enabled}");
            }
        }
        
        /// <summary>
        /// Updates batch processing size
        /// </summary>
        /// <param name="size">New batch size</param>
        public void UpdateBatchProcessingSize(int size)
        {
            if (size < 1 || size > 100000)
                throw new ArgumentOutOfRangeException(nameof(size), "Batch size must be between 1 and 100000");
            
            lock (_configLock)
            {
                _currentConfig.BatchProcessingSize = size;
                SaveConfiguration();
                _logger.Info($"Batch processing size updated to: {size}");
            }
        }
        
        /// <summary>
        /// Updates a custom setting
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <param name="value">Setting value</param>
        public void UpdateCustomSetting(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
            lock (_configLock)
            {
                _currentConfig.CustomSettings[key] = value;
                SaveConfiguration();
                _logger.Debug($"Custom setting updated: {key}");
            }
        }
        
        /// <summary>
        /// Gets a custom setting value
        /// </summary>
        /// <typeparam name="T">Expected value type</typeparam>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Default value if key doesn't exist</param>
        /// <returns>Setting value or default</returns>
        public T GetCustomSetting<T>(string key, T defaultValue = default)
        {
            lock (_configLock)
            {
                if (_currentConfig.CustomSettings.TryGetValue(key, out object value))
                {
                    try
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch
                    {
                        _logger.Warn($"Failed to convert custom setting '{key}' to type {typeof(T).Name}");
                        return defaultValue;
                    }
                }
                
                return defaultValue;
            }
        }
        
        #endregion

        #region Cleanup
        
        /// <summary>
        /// Disposes resources (file watcher)
        /// </summary>
        public void Dispose()
        {
            _fileWatcher?.Dispose();
            _logger.Info("StingBIMConfig disposed");
        }
        
        #endregion

        #region ConfigData Class
        
        /// <summary>
        /// Internal class for storing configuration data
        /// </summary>
        private class ConfigData
        {
            public string DataDirectory { get; set; }
            public string ParametersFilePath { get; set; }
            public bool EnableGPUAcceleration { get; set; }
            public int BatchProcessingSize { get; set; }
            public bool EnableAIFeatures { get; set; }
            public string LogLevel { get; set; }
            public bool EnablePerformanceMetrics { get; set; }
            public int CacheSizeLimitMB { get; set; }
            public Dictionary<string, object> CustomSettings { get; set; }
        }
        
        #endregion
    }
}
