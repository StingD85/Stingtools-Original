using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.Core.Config;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Schedules
{
    /// <summary>
    /// Loads schedule templates from CSV files
    /// Supports 146 schedule templates across multiple disciplines
    /// Provides caching and async operations
    /// </summary>
    public class ScheduleLoader
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<ScheduleLoader>();
        private readonly string _dataDirectory;
        private ScheduleTemplateCollection _cachedTemplates;
        private readonly object _cacheLock = new object();
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Gets whether templates are currently cached
        /// </summary>
        public bool IsCached
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cachedTemplates != null;
                }
            }
        }
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new ScheduleLoader
        /// </summary>
        /// <param name="dataDirectory">Directory containing schedule CSV files</param>
        public ScheduleLoader(string dataDirectory = null)
        {
            _dataDirectory = dataDirectory ?? GetDefaultDataDirectory();
            _logger.Info($"ScheduleLoader initialized. Data directory: {_dataDirectory}");
        }
        
        #endregion

        #region Load Methods
        
        /// <summary>
        /// Loads all schedule templates from CSV files
        /// </summary>
        /// <returns>Collection of schedule templates</returns>
        public ScheduleTemplateCollection Load()
        {
            lock (_cacheLock)
            {
                if (_cachedTemplates != null)
                {
                    _logger.Debug("Returning cached schedule templates");
                    return _cachedTemplates;
                }
            }
            
            using (_logger.StartPerformanceTimer("Load all schedules"))
            {
                _logger.Info("Loading schedule templates from CSV files...");
                
                var templates = new List<ScheduleTemplate>();
                
                try
                {
                    // Load Architecture schedules
                    templates.AddRange(LoadArchitectureSchedules());
                    
                    // Load MEP schedules
                    templates.AddRange(LoadMEPSchedules());
                    
                    // Load Material Takeoff schedules
                    templates.AddRange(LoadMaterialTakeoffSchedules());
                    
                    // Load FM (Facility Management) schedules
                    templates.AddRange(LoadFMSchedules());
                    
                    var collection = new ScheduleTemplateCollection(templates);
                    
                    lock (_cacheLock)
                    {
                        _cachedTemplates = collection;
                    }
                    
                    _logger.Info($"Loaded {templates.Count} schedule templates");
                    return collection;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load schedule templates");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Loads schedule templates asynchronously
        /// </summary>
        public async Task<ScheduleTemplateCollection> LoadAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Load(), cancellationToken);
        }
        
        /// <summary>
        /// Loads schedule templates with progress reporting
        /// </summary>
        public async Task<ScheduleTemplateCollection> LoadWithProgressAsync(
            IProgress<LoadProgress> progress,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                progress?.Report(new LoadProgress { Stage = "Initializing", PercentComplete = 0 });
                
                var templates = new List<ScheduleTemplate>();
                int totalSteps = 4;
                int currentStep = 0;
                
                // Load Architecture
                progress?.Report(new LoadProgress { Stage = "Loading Architecture schedules", PercentComplete = 0 });
                templates.AddRange(LoadArchitectureSchedules());
                currentStep++;
                progress?.Report(new LoadProgress { Stage = "Architecture loaded", PercentComplete = (currentStep * 100) / totalSteps });
                
                // Load MEP
                progress?.Report(new LoadProgress { Stage = "Loading MEP schedules", PercentComplete = (currentStep * 100) / totalSteps });
                templates.AddRange(LoadMEPSchedules());
                currentStep++;
                progress?.Report(new LoadProgress { Stage = "MEP loaded", PercentComplete = (currentStep * 100) / totalSteps });
                
                // Load Material Takeoffs
                progress?.Report(new LoadProgress { Stage = "Loading Material Takeoff schedules", PercentComplete = (currentStep * 100) / totalSteps });
                templates.AddRange(LoadMaterialTakeoffSchedules());
                currentStep++;
                progress?.Report(new LoadProgress { Stage = "Material Takeoffs loaded", PercentComplete = (currentStep * 100) / totalSteps });
                
                // Load FM
                progress?.Report(new LoadProgress { Stage = "Loading FM schedules", PercentComplete = (currentStep * 100) / totalSteps });
                templates.AddRange(LoadFMSchedules());
                currentStep++;
                progress?.Report(new LoadProgress { Stage = "Complete", PercentComplete = 100 });
                
                var collection = new ScheduleTemplateCollection(templates);
                
                lock (_cacheLock)
                {
                    _cachedTemplates = collection;
                }
                
                return collection;
                
            }, cancellationToken);
        }
        
        #endregion

        #region Discipline-Specific Loaders
        
        /// <summary>
        /// Loads Architecture schedule templates
        /// </summary>
        private List<ScheduleTemplate> LoadArchitectureSchedules()
        {
            var templates = new List<ScheduleTemplate>();
            
            try
            {
                // Load comprehensive architecture schedules
                var comprehensiveFile = Path.Combine(_dataDirectory, "ARCH_SCHEDULES_COMPREHENSIVE_ENHANCED.csv");
                if (File.Exists(comprehensiveFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(comprehensiveFile, ScheduleType.Regular, "Architecture"));
                    _logger.Debug($"Loaded architecture comprehensive schedules: {comprehensiveFile}");
                }
                
                // Load construction schedules
                var constructionFile = Path.Combine(_dataDirectory, "ARCH_CONSTRUCTION_SCHEDULES_ENHANCED.csv");
                if (File.Exists(constructionFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(constructionFile, ScheduleType.Regular, "Architecture"));
                    _logger.Debug($"Loaded architecture construction schedules: {constructionFile}");
                }
                
                // Load design schedules
                var designFile = Path.Combine(_dataDirectory, "ARCH_SCHEDULES_DESIGN_ENHANCED.csv");
                if (File.Exists(designFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(designFile, ScheduleType.Regular, "Architecture"));
                    _logger.Debug($"Loaded architecture design schedules: {designFile}");
                }
                
                // Load regulatory schedules
                var regulatoryFile = Path.Combine(_dataDirectory, "ARCH_PROJECT_REGULATORY_SCHEDULES_ENHANCED.csv");
                if (File.Exists(regulatoryFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(regulatoryFile, ScheduleType.Regular, "Architecture"));
                    _logger.Debug($"Loaded architecture regulatory schedules: {regulatoryFile}");
                }
                
                _logger.Info($"Loaded {templates.Count} architecture schedules");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load architecture schedules");
            }
            
            return templates;
        }
        
        /// <summary>
        /// Loads MEP schedule templates
        /// </summary>
        private List<ScheduleTemplate> LoadMEPSchedules()
        {
            var templates = new List<ScheduleTemplate>();
            
            try
            {
                // Load mechanical schedules
                var mechanicalFile = Path.Combine(_dataDirectory, "MEP_MECHANICAL_SCHEDULES_ENHANCED.csv");
                if (File.Exists(mechanicalFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(mechanicalFile, ScheduleType.Regular, "MEP-Mechanical"));
                    _logger.Debug($"Loaded MEP mechanical schedules: {mechanicalFile}");
                }
                
                // Load plumbing schedules
                var plumbingFile = Path.Combine(_dataDirectory, "MEP_PLUMBING_SCHEDULES_ENHANCED.csv");
                if (File.Exists(plumbingFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(plumbingFile, ScheduleType.Regular, "MEP-Plumbing"));
                    _logger.Debug($"Loaded MEP plumbing schedules: {plumbingFile}");
                }
                
                _logger.Info($"Loaded {templates.Count} MEP schedules");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load MEP schedules");
            }
            
            return templates;
        }
        
        /// <summary>
        /// Loads Material Takeoff schedule templates
        /// </summary>
        private List<ScheduleTemplate> LoadMaterialTakeoffSchedules()
        {
            var templates = new List<ScheduleTemplate>();
            
            try
            {
                var materialFile = Path.Combine(_dataDirectory, "MATERIAL_TAKEOFF_SCHEDULES.csv");
                if (File.Exists(materialFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(materialFile, ScheduleType.MaterialTakeoff, "Materials"));
                    _logger.Debug($"Loaded material takeoff schedules: {materialFile}");
                }
                
                _logger.Info($"Loaded {templates.Count} material takeoff schedules");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load material takeoff schedules");
            }
            
            return templates;
        }
        
        /// <summary>
        /// Loads Facility Management schedule templates
        /// </summary>
        private List<ScheduleTemplate> LoadFMSchedules()
        {
            var templates = new List<ScheduleTemplate>();
            
            try
            {
                var fmFile = Path.Combine(_dataDirectory, "FM_REVIT_SCHEDULES_ENHANCED.csv");
                if (File.Exists(fmFile))
                {
                    templates.AddRange(LoadSchedulesFromFile(fmFile, ScheduleType.Regular, "Facility Management"));
                    _logger.Debug($"Loaded FM schedules: {fmFile}");
                }
                
                _logger.Info($"Loaded {templates.Count} FM schedules");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load FM schedules");
            }
            
            return templates;
        }
        
        #endregion

        #region File Loading
        
        /// <summary>
        /// Loads schedules from a CSV file
        /// </summary>
        private List<ScheduleTemplate> LoadSchedulesFromFile(
            string filePath,
            ScheduleType scheduleType,
            string discipline)
        {
            var templates = new List<ScheduleTemplate>();
            
            try
            {
                _logger.Debug($"Loading schedules from: {filePath}");
                
                // Read all lines (UTF-8 encoding)
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                
                // Skip header line
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    
                    try
                    {
                        var template = ScheduleTemplate.FromCsvLine(line, scheduleType, discipline);
                        templates.Add(template);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, $"Failed to parse line {i}: {line}");
                    }
                }
                
                _logger.Debug($"Loaded {templates.Count} schedules from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load file: {filePath}");
            }
            
            return templates;
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Gets the default data directory
        /// </summary>
        private string GetDefaultDataDirectory()
        {
            var config = StingBIMConfig.Instance;
            var dataDir = Path.Combine(config.DataDirectory, "Schedules");
            
            // Also check project directory
            if (!Directory.Exists(dataDir))
            {
                dataDir = "/mnt/project";
            }
            
            if (!Directory.Exists(dataDir))
            {
                _logger.Warn($"Data directory not found: {dataDir}");
            }
            
            return dataDir;
        }
        
        /// <summary>
        /// Clears the cached templates
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedTemplates = null;
                _logger.Debug("Schedule template cache cleared");
            }
        }
        
        /// <summary>
        /// Reloads templates from files
        /// </summary>
        public ScheduleTemplateCollection Reload()
        {
            ClearCache();
            return Load();
        }
        
        /// <summary>
        /// Reloads templates asynchronously
        /// </summary>
        public async Task<ScheduleTemplateCollection> ReloadAsync(CancellationToken cancellationToken = default)
        {
            ClearCache();
            return await LoadAsync(cancellationToken);
        }
        
        /// <summary>
        /// Gets statistics about loaded templates
        /// </summary>
        public LoadStatistics GetStatistics()
        {
            var templates = Load();
            return new LoadStatistics(templates);
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a ScheduleLoader with default settings
        /// </summary>
        public static ScheduleLoader CreateDefault()
        {
            return new ScheduleLoader();
        }
        
        /// <summary>
        /// Creates a ScheduleLoader with custom data directory
        /// </summary>
        public static ScheduleLoader CreateWithPath(string dataDirectory)
        {
            return new ScheduleLoader(dataDirectory);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Collection of schedule templates with lookup capabilities
    /// </summary>
    public class ScheduleTemplateCollection
    {
        private readonly List<ScheduleTemplate> _templates;
        private readonly Dictionary<string, ScheduleTemplate> _templatesByName;
        private readonly Dictionary<string, List<ScheduleTemplate>> _templatesByDiscipline;
        private readonly Dictionary<ScheduleType, List<ScheduleTemplate>> _templatesByType;
        
        public ScheduleTemplateCollection(IEnumerable<ScheduleTemplate> templates)
        {
            _templates = new List<ScheduleTemplate>(templates);
            _templatesByName = _templates.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            
            // Index by discipline
            _templatesByDiscipline = _templates
                .GroupBy(t => t.Discipline)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Index by type
            _templatesByType = _templates
                .GroupBy(t => t.ScheduleType)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
        
        /// <summary>
        /// Gets the total number of templates
        /// </summary>
        public int Count => _templates.Count;
        
        /// <summary>
        /// Gets all templates
        /// </summary>
        public List<ScheduleTemplate> GetAll() => new List<ScheduleTemplate>(_templates);
        
        /// <summary>
        /// Gets a template by name
        /// </summary>
        public ScheduleTemplate GetByName(string name)
        {
            return _templatesByName.TryGetValue(name, out var template) ? template : null;
        }
        
        /// <summary>
        /// Gets templates by discipline
        /// </summary>
        public List<ScheduleTemplate> GetByDiscipline(string discipline)
        {
            return _templatesByDiscipline.TryGetValue(discipline, out var templates) 
                ? new List<ScheduleTemplate>(templates) 
                : new List<ScheduleTemplate>();
        }
        
        /// <summary>
        /// Gets templates by type
        /// </summary>
        public List<ScheduleTemplate> GetByType(ScheduleType scheduleType)
        {
            return _templatesByType.TryGetValue(scheduleType, out var templates)
                ? new List<ScheduleTemplate>(templates)
                : new List<ScheduleTemplate>();
        }
        
        /// <summary>
        /// Searches templates by keyword
        /// </summary>
        public List<ScheduleTemplate> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return GetAll();
            
            var lowerKeyword = keyword.ToLowerInvariant();
            return _templates.Where(t => 
                t.Name.ToLowerInvariant().Contains(lowerKeyword) ||
                t.CategoryName.ToLowerInvariant().Contains(lowerKeyword) ||
                t.Discipline.ToLowerInvariant().Contains(lowerKeyword))
                .ToList();
        }
        
        /// <summary>
        /// Gets all disciplines
        /// </summary>
        public List<string> GetDisciplines() => new List<string>(_templatesByDiscipline.Keys);
        
        /// <summary>
        /// Gets all schedule types
        /// </summary>
        public List<ScheduleType> GetScheduleTypes() => new List<ScheduleType>(_templatesByType.Keys);
    }
    
    /// <summary>
    /// Load progress information
    /// </summary>
    public class LoadProgress
    {
        public string Stage { get; set; }
        public int PercentComplete { get; set; }
        
        public override string ToString() => $"{Stage}: {PercentComplete}%";
    }
    
    /// <summary>
    /// Load statistics
    /// </summary>
    public class LoadStatistics
    {
        public int TotalTemplates { get; }
        public int ArchitectureCount { get; }
        public int MEPCount { get; }
        public int MaterialsCount { get; }
        public int FMCount { get; }
        public Dictionary<ScheduleType, int> CountByType { get; }
        public Dictionary<string, int> CountByDiscipline { get; }
        
        public LoadStatistics(ScheduleTemplateCollection templates)
        {
            TotalTemplates = templates.Count;
            ArchitectureCount = templates.GetByDiscipline("Architecture").Count;
            MEPCount = templates.GetByDiscipline("MEP-Mechanical").Count + 
                      templates.GetByDiscipline("MEP-Plumbing").Count;
            MaterialsCount = templates.GetByType(ScheduleType.MaterialTakeoff).Count;
            FMCount = templates.GetByDiscipline("Facility Management").Count;
            
            CountByType = templates.GetScheduleTypes()
                .ToDictionary(t => t, t => templates.GetByType(t).Count);
            
            CountByDiscipline = templates.GetDisciplines()
                .ToDictionary(d => d, d => templates.GetByDiscipline(d).Count);
        }
        
        public override string ToString()
        {
            return $"Total: {TotalTemplates} schedules\n" +
                   $"  Architecture: {ArchitectureCount}\n" +
                   $"  MEP: {MEPCount}\n" +
                   $"  Materials: {MaterialsCount}\n" +
                   $"  FM: {FMCount}";
        }
    }
}
