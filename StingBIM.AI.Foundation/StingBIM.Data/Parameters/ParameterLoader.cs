using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.Core.Config;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Parameters
{
    /// <summary>
    /// Loads parameter definitions from shared parameter file (MR_PARAMETERS.txt)
    /// Supports async loading, caching, and progress reporting
    /// Handles UTF-16 encoding and 818 parameters
    /// </summary>
    public class ParameterLoader : IParameterRepository
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<ParameterLoader>();
        private readonly string _parametersFilePath;
        private ParameterDefinitionCollection _cachedParameters;
        private Dictionary<int, string> _groupDefinitions;
        private readonly object _cacheLock = new object();
        private DateTime _lastLoadTime;
        private bool _isLoaded;
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Gets whether parameters have been loaded
        /// </summary>
        public bool IsLoaded => _isLoaded;
        
        /// <summary>
        /// Gets the number of loaded parameters
        /// </summary>
        public int ParameterCount
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cachedParameters?.Count ?? 0;
                }
            }
        }
        
        /// <summary>
        /// Gets the last load time
        /// </summary>
        public DateTime LastLoadTime => _lastLoadTime;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new ParameterLoader
        /// </summary>
        /// <param name="parametersFilePath">Path to MR_PARAMETERS.txt file (optional, uses config if null)</param>
        public ParameterLoader(string parametersFilePath = null)
        {
            _parametersFilePath = parametersFilePath ?? StingBIMConfig.Instance.ParametersFilePath;
            _groupDefinitions = new Dictionary<int, string>();
            _isLoaded = false;
            
            _logger.Info($"ParameterLoader initialized with file: {_parametersFilePath}");
        }
        
        #endregion

        #region Load Methods
        
        /// <summary>
        /// Loads all parameters from file
        /// </summary>
        /// <returns>Parameter collection</returns>
        public ParameterDefinitionCollection Load()
        {
            using (_logger.StartPerformanceTimer("LoadParameters"))
            {
                try
                {
                    _logger.Info("Starting parameter load...");
                    
                    // Validate file exists
                    if (!File.Exists(_parametersFilePath))
                    {
                        throw new FileNotFoundException($"Parameters file not found: {_parametersFilePath}");
                    }
                    
                    // Read file content (UTF-16 encoding)
                    string[] lines = File.ReadAllLines(_parametersFilePath, Encoding.Unicode);
                    _logger.Debug($"Read {lines.Length} lines from parameters file");
                    
                    // Parse file
                    var parameters = ParseParameterFile(lines);
                    
                    // Cache results
                    lock (_cacheLock)
                    {
                        _cachedParameters = parameters;
                        _lastLoadTime = DateTime.Now;
                        _isLoaded = true;
                    }
                    
                    _logger.Info($"Successfully loaded {parameters.Count} parameters");
                    return parameters;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load parameters");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Loads parameters asynchronously with cancellation support
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Parameter collection</returns>
        public async Task<ParameterDefinitionCollection> LoadAsync(CancellationToken cancellationToken = default)
        {
            using (_logger.StartPerformanceTimer("LoadParametersAsync"))
            {
                try
                {
                    _logger.Info("Starting async parameter load...");
                    
                    // Validate file exists
                    if (!File.Exists(_parametersFilePath))
                    {
                        throw new FileNotFoundException($"Parameters file not found: {_parametersFilePath}");
                    }
                    
                    // Read file content asynchronously (UTF-16 encoding)
                    string[] lines = await Task.Run(() => 
                        File.ReadAllLines(_parametersFilePath, Encoding.Unicode), 
                        cancellationToken);
                    
                    _logger.Debug($"Read {lines.Length} lines from parameters file");
                    
                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Parse file
                    var parameters = await Task.Run(() => 
                        ParseParameterFile(lines), 
                        cancellationToken);
                    
                    // Cache results
                    lock (_cacheLock)
                    {
                        _cachedParameters = parameters;
                        _lastLoadTime = DateTime.Now;
                        _isLoaded = true;
                    }
                    
                    _logger.Info($"Successfully loaded {parameters.Count} parameters asynchronously");
                    return parameters;
                }
                catch (OperationCanceledException)
                {
                    _logger.Warn("Parameter load was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load parameters asynchronously");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Loads parameters with progress reporting
        /// </summary>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Parameter collection</returns>
        public async Task<ParameterDefinitionCollection> LoadWithProgressAsync(
            IProgress<LoadProgress> progress,
            CancellationToken cancellationToken = default)
        {
            using (_logger.StartPerformanceTimer("LoadParametersWithProgress"))
            {
                try
                {
                    progress?.Report(new LoadProgress { Stage = "Initializing", PercentComplete = 0 });
                    _logger.Info("Starting parameter load with progress reporting...");
                    
                    // Validate file exists
                    if (!File.Exists(_parametersFilePath))
                    {
                        throw new FileNotFoundException($"Parameters file not found: {_parametersFilePath}");
                    }
                    
                    progress?.Report(new LoadProgress { Stage = "Reading file", PercentComplete = 10 });
                    
                    // Read file content asynchronously
                    string[] lines = await Task.Run(() => 
                        File.ReadAllLines(_parametersFilePath, Encoding.Unicode), 
                        cancellationToken);
                    
                    _logger.Debug($"Read {lines.Length} lines from parameters file");
                    
                    progress?.Report(new LoadProgress { Stage = "Parsing groups", PercentComplete = 20 });
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Parse groups first
                    _groupDefinitions = ParseGroupDefinitions(lines);
                    _logger.Debug($"Parsed {_groupDefinitions.Count} group definitions");
                    
                    progress?.Report(new LoadProgress { Stage = "Parsing parameters", PercentComplete = 30 });
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Parse parameters with progress
                    var parameters = await ParseParameterFileWithProgressAsync(lines, progress, cancellationToken);
                    
                    progress?.Report(new LoadProgress { Stage = "Caching results", PercentComplete = 95 });
                    
                    // Cache results
                    lock (_cacheLock)
                    {
                        _cachedParameters = parameters;
                        _lastLoadTime = DateTime.Now;
                        _isLoaded = true;
                    }
                    
                    progress?.Report(new LoadProgress 
                    { 
                        Stage = "Complete", 
                        PercentComplete = 100,
                        ParametersLoaded = parameters.Count
                    });
                    
                    _logger.Info($"Successfully loaded {parameters.Count} parameters with progress reporting");
                    return parameters;
                }
                catch (OperationCanceledException)
                {
                    _logger.Warn("Parameter load with progress was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load parameters with progress");
                    throw;
                }
            }
        }
        
        #endregion

        #region Parsing Methods
        
        /// <summary>
        /// Parses the entire parameter file
        /// </summary>
        private ParameterDefinitionCollection ParseParameterFile(string[] lines)
        {
            var parameters = new ParameterDefinitionCollection();
            
            // Parse group definitions first
            _groupDefinitions = ParseGroupDefinitions(lines);
            _logger.Debug($"Parsed {_groupDefinitions.Count} group definitions");
            
            // Parse parameter definitions
            int paramCount = 0;
            int errorCount = 0;
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                if (line.StartsWith("PARAM", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var param = ParameterDefinition.FromSharedParameterLine(line, _groupDefinitions);
                        
                        if (param.IsValid())
                        {
                            parameters.Add(param);
                            paramCount++;
                        }
                        else
                        {
                            _logger.Warn($"Invalid parameter definition: {line}");
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to parse parameter line: {line}");
                        errorCount++;
                    }
                }
            }
            
            _logger.Info($"Parsed {paramCount} parameters, {errorCount} errors");
            return parameters;
        }
        
        /// <summary>
        /// Parses parameter file with progress reporting
        /// </summary>
        private async Task<ParameterDefinitionCollection> ParseParameterFileWithProgressAsync(
            string[] lines,
            IProgress<LoadProgress> progress,
            CancellationToken cancellationToken)
        {
            var parameters = new ParameterDefinitionCollection();
            
            // Count parameter lines
            int totalParams = lines.Count(l => l.StartsWith("PARAM", StringComparison.OrdinalIgnoreCase));
            int processedParams = 0;
            int errorCount = 0;
            
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                if (line.StartsWith("PARAM", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var param = ParameterDefinition.FromSharedParameterLine(line, _groupDefinitions);
                        
                        if (param.IsValid())
                        {
                            parameters.Add(param);
                            processedParams++;
                            
                            // Report progress every 10 parameters
                            if (processedParams % 10 == 0)
                            {
                                int percentComplete = 30 + (int)((processedParams / (double)totalParams) * 65);
                                progress?.Report(new LoadProgress
                                {
                                    Stage = "Parsing parameters",
                                    PercentComplete = percentComplete,
                                    ParametersLoaded = processedParams,
                                    TotalParameters = totalParams
                                });
                                
                                // Yield to allow UI updates
                                await Task.Delay(1, cancellationToken);
                            }
                        }
                        else
                        {
                            _logger.Warn($"Invalid parameter definition: {line}");
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to parse parameter line: {line}");
                        errorCount++;
                    }
                }
            }
            
            _logger.Info($"Parsed {processedParams} parameters, {errorCount} errors");
            return parameters;
        }
        
        /// <summary>
        /// Parses group definitions from file
        /// Format: GROUP ID NAME
        /// </summary>
        private Dictionary<int, string> ParseGroupDefinitions(string[] lines)
        {
            var groups = new Dictionary<int, string>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                if (line.StartsWith("GROUP", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var parts = line.Split('\t');
                        if (parts.Length >= 3)
                        {
                            int groupId = int.Parse(parts[1]);
                            string groupName = parts[2];
                            groups[groupId] = groupName;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to parse group line: {line}");
                    }
                }
            }
            
            return groups;
        }
        
        #endregion

        #region Cache Management
        
        /// <summary>
        /// Gets cached parameters (loads if not cached)
        /// </summary>
        /// <returns>Parameter collection</returns>
        public ParameterDefinitionCollection GetParameters()
        {
            lock (_cacheLock)
            {
                if (_cachedParameters == null || !_isLoaded)
                {
                    _logger.Debug("Cache miss, loading parameters");
                    return Load();
                }
                
                _logger.Debug("Cache hit, returning cached parameters");
                return _cachedParameters;
            }
        }
        
        /// <summary>
        /// Gets cached parameters asynchronously (loads if not cached)
        /// </summary>
        public async Task<ParameterDefinitionCollection> GetParametersAsync(CancellationToken cancellationToken = default)
        {
            lock (_cacheLock)
            {
                if (_cachedParameters != null && _isLoaded)
                {
                    _logger.Debug("Cache hit, returning cached parameters");
                    return _cachedParameters;
                }
            }
            
            _logger.Debug("Cache miss, loading parameters asynchronously");
            return await LoadAsync(cancellationToken);
        }
        
        /// <summary>
        /// Clears the parameter cache
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedParameters = null;
                _isLoaded = false;
                _lastLoadTime = DateTime.MinValue;
                _logger.Info("Parameter cache cleared");
            }
        }
        
        /// <summary>
        /// Reloads parameters from file
        /// </summary>
        public ParameterDefinitionCollection Reload()
        {
            ClearCache();
            return Load();
        }
        
        /// <summary>
        /// Reloads parameters asynchronously
        /// </summary>
        public async Task<ParameterDefinitionCollection> ReloadAsync(CancellationToken cancellationToken = default)
        {
            ClearCache();
            return await LoadAsync(cancellationToken);
        }
        
        #endregion

        #region IParameterRepository Implementation
        
        /// <summary>
        /// Gets a parameter by GUID
        /// </summary>
        public ParameterDefinition GetByGuid(Guid guid)
        {
            var parameters = GetParameters();
            return parameters.GetByGuid(guid);
        }
        
        /// <summary>
        /// Gets a parameter by name
        /// </summary>
        public ParameterDefinition GetByName(string name)
        {
            var parameters = GetParameters();
            return parameters.GetByName(name);
        }
        
        /// <summary>
        /// Gets all parameters for a discipline
        /// </summary>
        public List<ParameterDefinition> GetByDiscipline(string discipline)
        {
            var parameters = GetParameters();
            return parameters.GetByDiscipline(discipline);
        }
        
        /// <summary>
        /// Gets all parameters for a system
        /// </summary>
        public List<ParameterDefinition> GetBySystem(string system)
        {
            var parameters = GetParameters();
            return parameters.GetBySystem(system);
        }
        
        /// <summary>
        /// Gets all parameters
        /// </summary>
        public List<ParameterDefinition> GetAll()
        {
            var parameters = GetParameters();
            return parameters.GetAll();
        }
        
        /// <summary>
        /// Searches parameters by keyword
        /// </summary>
        public List<ParameterDefinition> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ParameterDefinition>();
            
            var parameters = GetParameters();
            var allParams = parameters.GetAll();
            
            return allParams.Where(p =>
                p.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (p.Description?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }
        
        #endregion

        #region Statistics
        
        /// <summary>
        /// Gets loading statistics
        /// </summary>
        public LoadStatistics GetStatistics()
        {
            lock (_cacheLock)
            {
                var parameters = _cachedParameters?.GetAll() ?? new List<ParameterDefinition>();
                
                return new LoadStatistics
                {
                    IsLoaded = _isLoaded,
                    LastLoadTime = _lastLoadTime,
                    TotalParameters = parameters.Count,
                    GroupCount = _groupDefinitions.Count,
                    DisciplineCount = _cachedParameters?.GetDisciplines().Count ?? 0,
                    SystemCount = _cachedParameters?.GetSystems().Count ?? 0,
                    ParametersByDataType = parameters
                        .GroupBy(p => p.DataType)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a ParameterLoader with default configuration
        /// </summary>
        public static ParameterLoader CreateDefault()
        {
            return new ParameterLoader();
        }
        
        /// <summary>
        /// Creates a ParameterLoader with custom file path
        /// </summary>
        public static ParameterLoader CreateWithPath(string filePath)
        {
            return new ParameterLoader(filePath);
        }
        
        #endregion
    }
    
    #region Support Classes
    
    /// <summary>
    /// Represents load progress information
    /// </summary>
    public class LoadProgress
    {
        public string Stage { get; set; }
        public int PercentComplete { get; set; }
        public int ParametersLoaded { get; set; }
        public int TotalParameters { get; set; }
        
        public override string ToString()
        {
            return $"{Stage}: {PercentComplete}% ({ParametersLoaded}/{TotalParameters})";
        }
    }
    
    /// <summary>
    /// Loading statistics
    /// </summary>
    public class LoadStatistics
    {
        public bool IsLoaded { get; set; }
        public DateTime LastLoadTime { get; set; }
        public int TotalParameters { get; set; }
        public int GroupCount { get; set; }
        public int DisciplineCount { get; set; }
        public int SystemCount { get; set; }
        public Dictionary<string, int> ParametersByDataType { get; set; }
    }
    
    #endregion
}
