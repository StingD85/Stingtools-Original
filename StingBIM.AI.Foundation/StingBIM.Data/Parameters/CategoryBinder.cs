using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using StingBIM.Core.Config;
using StingBIM.Core.Logging;
using StingBIM.Core.Transactions;

namespace StingBIM.Data.Parameters
{
    /// <summary>
    /// Manages binding of parameters to Revit categories
    /// Loads bindings from 02_CATEGORY_BINDINGS.csv (10,730 mappings)
    /// Applies parameter bindings to document
    /// </summary>
    public class CategoryBinder
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<CategoryBinder>();
        private readonly Document _document;
        private readonly string _bindingsFilePath;
        private readonly IParameterRepository _parameterRepository;
        private readonly Dictionary<string, List<CategoryBinding>> _bindingsByParameter;
        private readonly object _bindingsLock = new object();
        private bool _isLoaded;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new CategoryBinder
        /// </summary>
        /// <param name="document">Revit document</param>
        /// <param name="parameterRepository">Parameter repository</param>
        /// <param name="bindingsFilePath">Path to bindings CSV file (optional)</param>
        public CategoryBinder(
            Document document,
            IParameterRepository parameterRepository,
            string bindingsFilePath = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _parameterRepository = parameterRepository ?? throw new ArgumentNullException(nameof(parameterRepository));
            
            _bindingsFilePath = bindingsFilePath ?? 
                Path.Combine(StingBIMConfig.Instance.DataDirectory, "02_CATEGORY_BINDINGS.csv");
            
            _bindingsByParameter = new Dictionary<string, List<CategoryBinding>>(StringComparer.OrdinalIgnoreCase);
            _isLoaded = false;
            
            _logger.Info($"CategoryBinder initialized for document: {document.Title}");
        }
        
        #endregion

        #region Load Bindings
        
        /// <summary>
        /// Loads category bindings from CSV file
        /// </summary>
        /// <returns>Number of bindings loaded</returns>
        public int LoadBindings()
        {
            using (_logger.StartPerformanceTimer("LoadCategoryBindings"))
            {
                try
                {
                    _logger.Info("Loading category bindings...");
                    
                    if (!File.Exists(_bindingsFilePath))
                    {
                        throw new FileNotFoundException($"Bindings file not found: {_bindingsFilePath}");
                    }
                    
                    // Read CSV file
                    var lines = File.ReadAllLines(_bindingsFilePath);
                    _logger.Debug($"Read {lines.Length} lines from bindings file");
                    
                    // Parse bindings
                    int bindingCount = ParseBindings(lines);
                    
                    lock (_bindingsLock)
                    {
                        _isLoaded = true;
                    }
                    
                    _logger.Info($"Loaded {bindingCount} category bindings");
                    return bindingCount;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load category bindings");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Loads bindings asynchronously
        /// </summary>
        public async Task<int> LoadBindingsAsync(CancellationToken cancellationToken = default)
        {
            using (_logger.StartPerformanceTimer("LoadCategoryBindingsAsync"))
            {
                try
                {
                    _logger.Info("Loading category bindings asynchronously...");
                    
                    if (!File.Exists(_bindingsFilePath))
                    {
                        throw new FileNotFoundException($"Bindings file not found: {_bindingsFilePath}");
                    }
                    
                    // Read CSV file asynchronously
                    var lines = await Task.Run(() => File.ReadAllLines(_bindingsFilePath), cancellationToken);
                    _logger.Debug($"Read {lines.Length} lines from bindings file");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Parse bindings
                    int bindingCount = await Task.Run(() => ParseBindings(lines), cancellationToken);
                    
                    lock (_bindingsLock)
                    {
                        _isLoaded = true;
                    }
                    
                    _logger.Info($"Loaded {bindingCount} category bindings asynchronously");
                    return bindingCount;
                }
                catch (OperationCanceledException)
                {
                    _logger.Warn("Category bindings load was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load category bindings asynchronously");
                    throw;
                }
            }
        }
        
        #endregion

        #region Parse Bindings
        
        /// <summary>
        /// Parses bindings from CSV lines
        /// Format: Parameter_Name,Revit_Category,Binding_Type,Is_Shared
        /// </summary>
        private int ParseBindings(string[] lines)
        {
            int bindingCount = 0;
            int errorCount = 0;
            bool headerSkipped = false;
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                // Skip header row
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue;
                }
                
                try
                {
                    var binding = CategoryBinding.FromCsvLine(line);
                    
                    // Add to dictionary
                    lock (_bindingsLock)
                    {
                        if (!_bindingsByParameter.ContainsKey(binding.ParameterName))
                        {
                            _bindingsByParameter[binding.ParameterName] = new List<CategoryBinding>();
                        }
                        
                        _bindingsByParameter[binding.ParameterName].Add(binding);
                    }
                    
                    bindingCount++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to parse binding line: {line}");
                    errorCount++;
                }
            }
            
            _logger.Debug($"Parsed {bindingCount} bindings, {errorCount} errors");
            return bindingCount;
        }
        
        #endregion

        #region Apply Bindings
        
        /// <summary>
        /// Applies all parameter bindings to the document
        /// </summary>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Number of successfully applied bindings</returns>
        public int ApplyAllBindings(IProgress<BindingProgress> progress = null)
        {
            using (_logger.StartPerformanceTimer("ApplyAllBindings"))
            {
                try
                {
                    if (!_isLoaded)
                    {
                        LoadBindings();
                    }
                    
                    var transactionManager = TransactionManager.For(_document);
                    int successCount = 0;
                    int totalBindings = _bindingsByParameter.Sum(kvp => kvp.Value.Count);
                    int processedCount = 0;
                    
                    _logger.Info($"Applying {totalBindings} parameter bindings...");
                    progress?.Report(new BindingProgress { Stage = "Starting", PercentComplete = 0 });
                    
                    // Apply bindings for each parameter
                    foreach (var kvp in _bindingsByParameter)
                    {
                        string parameterName = kvp.Key;
                        var bindings = kvp.Value;
                        
                        try
                        {
                            // Get parameter definition
                            var paramDef = _parameterRepository.GetByName(parameterName);
                            if (paramDef == null)
                            {
                                _logger.Warn($"Parameter not found: {parameterName}");
                                processedCount += bindings.Count;
                                continue;
                            }
                            
                            // Apply bindings for this parameter
                            bool applied = transactionManager.Execute(
                                $"Bind Parameter: {parameterName}",
                                () => ApplyParameterBinding(paramDef, bindings));
                            
                            if (applied)
                            {
                                successCount += bindings.Count;
                            }
                            
                            processedCount += bindings.Count;
                            
                            // Report progress
                            if (processedCount % 100 == 0)
                            {
                                int percentComplete = (int)((processedCount / (double)totalBindings) * 100);
                                progress?.Report(new BindingProgress
                                {
                                    Stage = "Applying bindings",
                                    PercentComplete = percentComplete,
                                    BindingsApplied = successCount,
                                    TotalBindings = totalBindings
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"Failed to apply bindings for parameter: {parameterName}");
                        }
                    }
                    
                    progress?.Report(new BindingProgress
                    {
                        Stage = "Complete",
                        PercentComplete = 100,
                        BindingsApplied = successCount,
                        TotalBindings = totalBindings
                    });
                    
                    _logger.Info($"Successfully applied {successCount}/{totalBindings} bindings");
                    return successCount;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to apply bindings");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Applies bindings for a single parameter
        /// </summary>
        private bool ApplyParameterBinding(ParameterDefinition paramDef, List<CategoryBinding> bindings)
        {
            try
            {
                // Create category set for binding
                var categorySet = _document.Application.Create.NewCategorySet();
                
                foreach (var binding in bindings)
                {
                    // Get Revit category
                    var category = GetCategoryByName(binding.RevitCategory);
                    if (category != null)
                    {
                        categorySet.Insert(category);
                    }
                    else
                    {
                        _logger.Warn($"Category not found: {binding.RevitCategory}");
                    }
                }
                
                if (categorySet.IsEmpty)
                {
                    _logger.Warn($"No valid categories found for parameter: {paramDef.Name}");
                    return false;
                }
                
                // Create external definition
                var bindingMap = _document.ParameterBindings;
                var sharedParameterFile = _document.Application.OpenSharedParameterFile();
                
                if (sharedParameterFile == null)
                {
                    _logger.Error("No shared parameter file is open");
                    return false;
                }
                
                // Find definition group
                var group = sharedParameterFile.Groups.get_Item(paramDef.GroupName) ??
                           sharedParameterFile.Groups.Create(paramDef.GroupName);
                
                // Get or create definition
                var definition = group.Definitions.get_Item(paramDef.Name) as ExternalDefinition;
                
                if (definition == null)
                {
                    // Create new definition using ForgeTypeId for Revit 2025+
                    var forgeTypeId = ToForgeTypeId(paramDef.RevitParameterType);
                    var options = new ExternalDefinitionCreationOptions(paramDef.Name, forgeTypeId)
                    {
                        GUID = paramDef.Guid,
                        Description = paramDef.Description,
                        UserModifiable = paramDef.IsUserModifiable,
                        HideWhenNoValue = paramDef.HideWhenNoValue,
                        Visible = paramDef.IsVisible
                    };

                    definition = group.Definitions.Create(options) as ExternalDefinition;
                }
                
                if (definition == null)
                {
                    _logger.Error($"Failed to create definition for: {paramDef.Name}");
                    return false;
                }
                
                // Determine binding type
                var bindingType = bindings.First().BindingType;
                Binding newBinding = bindingType.Equals("Instance", StringComparison.OrdinalIgnoreCase)
                    ? (Binding)_document.Application.Create.NewInstanceBinding(categorySet)
                    : (Binding)_document.Application.Create.NewTypeBinding(categorySet);
                
                // Apply binding
                if (bindingMap.Contains(definition))
                {
                    // Update existing binding
                    bindingMap.ReInsert(definition, newBinding, GroupTypeId.IdentityData);
                }
                else
                {
                    // Insert new binding
                    bindingMap.Insert(definition, newBinding, GroupTypeId.IdentityData);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to apply binding for parameter: {paramDef.Name}");
                return false;
            }
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Converts StingBIMParameterType to Revit ForgeTypeId for Revit 2025+ API
        /// </summary>
        private static ForgeTypeId ToForgeTypeId(StingBIMParameterType paramType)
        {
            return paramType switch
            {
                StingBIMParameterType.Text => SpecTypeId.String.Text,
                StingBIMParameterType.Integer => SpecTypeId.Int.Integer,
                StingBIMParameterType.Number => SpecTypeId.Number,
                StingBIMParameterType.Length => SpecTypeId.Length,
                StingBIMParameterType.Area => SpecTypeId.Area,
                StingBIMParameterType.Volume => SpecTypeId.Volume,
                StingBIMParameterType.Angle => SpecTypeId.Angle,
                StingBIMParameterType.URL => SpecTypeId.String.Url,
                StingBIMParameterType.YesNo => SpecTypeId.Boolean.YesNo,
                StingBIMParameterType.Currency => SpecTypeId.Currency,
                StingBIMParameterType.ElectricalCurrent => SpecTypeId.Current,
                StingBIMParameterType.ElectricalPotential => SpecTypeId.ElectricalPotential,
                StingBIMParameterType.ElectricalPower => SpecTypeId.Wattage,
                _ => SpecTypeId.String.Text
            };
        }

        /// <summary>
        /// Gets a Revit category by name
        /// </summary>
        private Category GetCategoryByName(string categoryName)
        {
            try
            {
                // Try to get category from document
                foreach (Category category in _document.Settings.Categories)
                {
                    if (category.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        return category;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get category: {categoryName}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets bindings for a specific parameter
        /// </summary>
        public List<CategoryBinding> GetBindingsForParameter(string parameterName)
        {
            lock (_bindingsLock)
            {
                return _bindingsByParameter.TryGetValue(parameterName, out var bindings)
                    ? new List<CategoryBinding>(bindings)
                    : new List<CategoryBinding>();
            }
        }
        
        /// <summary>
        /// Gets all bindings
        /// </summary>
        public Dictionary<string, List<CategoryBinding>> GetAllBindings()
        {
            lock (_bindingsLock)
            {
                return new Dictionary<string, List<CategoryBinding>>(_bindingsByParameter);
            }
        }
        
        /// <summary>
        /// Converts ParameterType to Revit ForgeTypeId for Revit 2025+ API.
        /// In Revit 2025, ParameterType enum was made internal and replaced by ForgeTypeId.
        /// </summary>
        private static ForgeTypeId ConvertToForgeTypeId(ParameterType paramType)
        {
            switch (paramType)
            {
                case ParameterType.Text: return SpecTypeId.String.Text;
                case ParameterType.Integer: return SpecTypeId.Int.Integer;
                case ParameterType.Number: return SpecTypeId.Number;
                case ParameterType.Length: return SpecTypeId.Length;
                case ParameterType.Area: return SpecTypeId.Area;
                case ParameterType.Volume: return SpecTypeId.Volume;
                case ParameterType.Angle: return SpecTypeId.Angle;
                case ParameterType.URL: return SpecTypeId.String.Url;
                case ParameterType.YesNo: return SpecTypeId.Boolean.YesNo;
                case ParameterType.Currency: return SpecTypeId.Currency;
                case ParameterType.ElectricalCurrent: return SpecTypeId.Current;
                case ParameterType.ElectricalPotential: return SpecTypeId.ElectricalPotential;
                case ParameterType.ElectricalPower: return SpecTypeId.Wattage;
                default: return SpecTypeId.String.Text;
            }
        }

        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a CategoryBinder for the specified document
        /// </summary>
        public static CategoryBinder For(Document document, IParameterRepository parameterRepository)
        {
            return new CategoryBinder(document, parameterRepository);
        }
        
        #endregion
    }
    
    #region Support Classes
    
    /// <summary>
    /// Represents a category binding
    /// </summary>
    public class CategoryBinding
    {
        public string ParameterName { get; set; }
        public string RevitCategory { get; set; }
        public string BindingType { get; set; } // "Instance" or "Type"
        public bool IsShared { get; set; }
        
        /// <summary>
        /// Creates a CategoryBinding from CSV line
        /// Format: Parameter_Name,Revit_Category,Binding_Type,Is_Shared
        /// </summary>
        public static CategoryBinding FromCsvLine(string line)
        {
            var parts = line.Split(',');
            if (parts.Length < 4)
                throw new ArgumentException($"Invalid binding line format: {line}");
            
            return new CategoryBinding
            {
                ParameterName = parts[0].Trim(),
                RevitCategory = parts[1].Trim(),
                BindingType = parts[2].Trim(),
                IsShared = bool.Parse(parts[3].Trim())
            };
        }
    }
    
    /// <summary>
    /// Binding progress information
    /// </summary>
    public class BindingProgress
    {
        public string Stage { get; set; }
        public int PercentComplete { get; set; }
        public int BindingsApplied { get; set; }
        public int TotalBindings { get; set; }
        
        public override string ToString()
        {
            return $"{Stage}: {PercentComplete}% ({BindingsApplied}/{TotalBindings})";
        }
    }
    
    #endregion
}
