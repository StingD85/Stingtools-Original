using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Families
{
    /// <summary>
    /// Comprehensive family management system for Revit.
    /// Handles family loading, type creation/editing, intelligent swapping,
    /// and family library management with AI-powered matching.
    /// </summary>
    public class FamilyManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, LoadedFamilyDefinition> _loadedFamilies;
        private readonly Dictionary<string, FamilyTypeDefinition> _loadedTypes;
        private readonly Dictionary<string, FamilyLibraryEntry> _familyLibrary;
        private readonly Dictionary<string, TypeSubstitutionRule> _substitutionRules;
        private readonly List<FamilyOperation> _operationHistory;
        private readonly object _lock = new object();

        public event EventHandler<FamilyLoadedEventArgs> FamilyLoaded;
        public event EventHandler<TypeCreatedEventArgs> TypeCreated;
        public event EventHandler<FamilySwappedEventArgs> FamilySwapped;

        public FamilyManager()
        {
            _loadedFamilies = new Dictionary<string, LoadedFamilyDefinition>(StringComparer.OrdinalIgnoreCase);
            _loadedTypes = new Dictionary<string, FamilyTypeDefinition>(StringComparer.OrdinalIgnoreCase);
            _familyLibrary = InitializeFamilyLibrary();
            _substitutionRules = InitializeSubstitutionRules();
            _operationHistory = new List<FamilyOperation>();

            Logger.Info("FamilyManager initialized with {0} library entries, {1} substitution rules",
                _familyLibrary.Count, _substitutionRules.Count);
        }

        #region Family Library

        private Dictionary<string, FamilyLibraryEntry> InitializeFamilyLibrary()
        {
            return new Dictionary<string, FamilyLibraryEntry>(StringComparer.OrdinalIgnoreCase)
            {
                // Door Families
                ["Single_Flush_Door"] = new FamilyLibraryEntry
                {
                    FamilyName = "Single Flush Door",
                    Category = "Doors",
                    FileName = "Single-Flush.rfa",
                    Description = "Standard single leaf flush door",
                    AvailableWidths = new List<double> { 700, 750, 800, 850, 900, 1000, 1100, 1200 },
                    AvailableHeights = new List<double> { 2000, 2100, 2200, 2400 },
                    Parameters = new List<string> { "Width", "Height", "Frame_Material", "Leaf_Material", "Fire_Rating" },
                    Tags = new List<string> { "interior", "standard", "flush" }
                },
                ["Single_Panel_Door"] = new FamilyLibraryEntry
                {
                    FamilyName = "Single Panel Door",
                    Category = "Doors",
                    FileName = "Single-Panel.rfa",
                    Description = "Single leaf door with raised panel",
                    AvailableWidths = new List<double> { 700, 800, 900, 1000 },
                    AvailableHeights = new List<double> { 2000, 2100 },
                    Parameters = new List<string> { "Width", "Height", "Panel_Style", "Frame_Material" },
                    Tags = new List<string> { "interior", "residential", "panel" }
                },
                ["Double_Door"] = new FamilyLibraryEntry
                {
                    FamilyName = "Double Door",
                    Category = "Doors",
                    FileName = "Double-Door.rfa",
                    Description = "Double leaf door",
                    AvailableWidths = new List<double> { 1400, 1600, 1800, 2000, 2400 },
                    AvailableHeights = new List<double> { 2100, 2200, 2400 },
                    Parameters = new List<string> { "Width", "Height", "Leaf_Width", "Frame_Material" },
                    Tags = new List<string> { "double", "entrance", "commercial" }
                },
                ["Sliding_Door"] = new FamilyLibraryEntry
                {
                    FamilyName = "Sliding Door",
                    Category = "Doors",
                    FileName = "Sliding-Door.rfa",
                    Description = "Sliding door with track",
                    AvailableWidths = new List<double> { 900, 1000, 1200, 1500, 1800 },
                    AvailableHeights = new List<double> { 2100, 2200 },
                    Parameters = new List<string> { "Width", "Height", "Track_Type", "Glazing" },
                    Tags = new List<string> { "sliding", "interior", "space-saving" }
                },
                ["Fire_Door"] = new FamilyLibraryEntry
                {
                    FamilyName = "Fire Rated Door",
                    Category = "Doors",
                    FileName = "Fire-Door.rfa",
                    Description = "Fire rated door with closer and seals",
                    AvailableWidths = new List<double> { 800, 900, 1000 },
                    AvailableHeights = new List<double> { 2100 },
                    FireRatings = new List<string> { "FD30", "FD60", "FD90", "FD120" },
                    Parameters = new List<string> { "Width", "Height", "Fire_Rating", "Vision_Panel" },
                    Tags = new List<string> { "fire", "rated", "safety", "commercial" }
                },
                ["Entrance_Door_Glazed"] = new FamilyLibraryEntry
                {
                    FamilyName = "Entrance Door Glazed",
                    Category = "Doors",
                    FileName = "Entrance-Glazed.rfa",
                    Description = "Glazed entrance door with sidelights option",
                    AvailableWidths = new List<double> { 900, 1000, 1200 },
                    AvailableHeights = new List<double> { 2100, 2200, 2400 },
                    Parameters = new List<string> { "Width", "Height", "Glazing_Type", "Sidelights", "Transom" },
                    Tags = new List<string> { "entrance", "glazed", "exterior" }
                },

                // Window Families
                ["Fixed_Window"] = new FamilyLibraryEntry
                {
                    FamilyName = "Fixed Window",
                    Category = "Windows",
                    FileName = "Fixed-Window.rfa",
                    Description = "Non-operable fixed glazing",
                    AvailableWidths = new List<double> { 600, 900, 1200, 1500, 1800, 2100, 2400 },
                    AvailableHeights = new List<double> { 600, 900, 1200, 1500, 1800, 2100 },
                    Parameters = new List<string> { "Width", "Height", "Glazing_Type", "Frame_Material", "U_Value" },
                    Tags = new List<string> { "fixed", "picture", "non-operable" }
                },
                ["Casement_Window"] = new FamilyLibraryEntry
                {
                    FamilyName = "Casement Window",
                    Category = "Windows",
                    FileName = "Casement-Window.rfa",
                    Description = "Side-hinged operable window",
                    AvailableWidths = new List<double> { 600, 750, 900, 1050, 1200 },
                    AvailableHeights = new List<double> { 900, 1050, 1200, 1350, 1500 },
                    Parameters = new List<string> { "Width", "Height", "Hinge_Side", "Glazing_Type", "Handle_Type" },
                    Tags = new List<string> { "casement", "operable", "hinged" }
                },
                ["Sliding_Window"] = new FamilyLibraryEntry
                {
                    FamilyName = "Sliding Window",
                    Category = "Windows",
                    FileName = "Sliding-Window.rfa",
                    Description = "Horizontal sliding window",
                    AvailableWidths = new List<double> { 1200, 1500, 1800, 2100, 2400, 3000 },
                    AvailableHeights = new List<double> { 900, 1050, 1200, 1500 },
                    Parameters = new List<string> { "Width", "Height", "Panels", "Glazing_Type", "Track_Type" },
                    Tags = new List<string> { "sliding", "horizontal", "operable" }
                },
                ["Awning_Window"] = new FamilyLibraryEntry
                {
                    FamilyName = "Awning Window",
                    Category = "Windows",
                    FileName = "Awning-Window.rfa",
                    Description = "Top-hinged window opening outward",
                    AvailableWidths = new List<double> { 600, 900, 1200, 1500 },
                    AvailableHeights = new List<double> { 450, 600, 750, 900 },
                    Parameters = new List<string> { "Width", "Height", "Opening_Angle", "Glazing_Type" },
                    Tags = new List<string> { "awning", "ventilation", "rain-proof" }
                },
                ["Louvre_Window"] = new FamilyLibraryEntry
                {
                    FamilyName = "Louvre Window",
                    Category = "Windows",
                    FileName = "Louvre-Window.rfa",
                    Description = "Louvred window for ventilation (Africa-common)",
                    AvailableWidths = new List<double> { 600, 900, 1200 },
                    AvailableHeights = new List<double> { 600, 900, 1200, 1500 },
                    Parameters = new List<string> { "Width", "Height", "Blade_Count", "Blade_Material", "Blade_Angle" },
                    Tags = new List<string> { "louvre", "ventilation", "tropical", "africa" }
                },

                // Column Families
                ["Rectangular_Column"] = new FamilyLibraryEntry
                {
                    FamilyName = "Rectangular Column",
                    Category = "Columns",
                    FileName = "Rectangular-Column.rfa",
                    Description = "Rectangular concrete/steel column",
                    AvailableSizes = new List<string> { "200x200", "250x250", "300x300", "300x450", "300x600", "400x400", "450x450", "500x500", "600x600" },
                    Parameters = new List<string> { "Width", "Depth", "Material", "Reinforcement" },
                    Tags = new List<string> { "rectangular", "structural", "concrete" }
                },
                ["Circular_Column"] = new FamilyLibraryEntry
                {
                    FamilyName = "Circular Column",
                    Category = "Columns",
                    FileName = "Circular-Column.rfa",
                    Description = "Circular concrete/steel column",
                    AvailableDiameters = new List<double> { 250, 300, 350, 400, 450, 500, 600, 750, 900 },
                    Parameters = new List<string> { "Diameter", "Material", "Reinforcement" },
                    Tags = new List<string> { "circular", "round", "structural" }
                },
                ["Steel_UC_Column"] = new FamilyLibraryEntry
                {
                    FamilyName = "Steel UC Column",
                    Category = "Structural Columns",
                    FileName = "Steel-UC.rfa",
                    Description = "Universal Column steel section",
                    AvailableSizes = new List<string> { "152x152x23", "152x152x30", "203x203x46", "203x203x60", "254x254x73", "254x254x89", "305x305x97", "305x305x118" },
                    Parameters = new List<string> { "Section_Size", "Grade", "Fire_Protection" },
                    Tags = new List<string> { "steel", "UC", "universal", "structural" }
                },

                // Furniture Families
                ["Office_Desk"] = new FamilyLibraryEntry
                {
                    FamilyName = "Office Desk",
                    Category = "Furniture",
                    FileName = "Office-Desk.rfa",
                    Description = "Standard office workstation desk",
                    AvailableSizes = new List<string> { "1200x600", "1400x700", "1600x800", "1800x800" },
                    Parameters = new List<string> { "Width", "Depth", "Height", "Material", "Cable_Management" },
                    Tags = new List<string> { "desk", "office", "workstation" }
                },
                ["Office_Chair"] = new FamilyLibraryEntry
                {
                    FamilyName = "Office Chair",
                    Category = "Furniture",
                    FileName = "Office-Chair.rfa",
                    Description = "Ergonomic office chair",
                    Parameters = new List<string> { "Seat_Height", "Armrest", "Headrest", "Material" },
                    Tags = new List<string> { "chair", "office", "seating", "ergonomic" }
                },

                // Plumbing Fixtures
                ["WC_Suite"] = new FamilyLibraryEntry
                {
                    FamilyName = "WC Suite",
                    Category = "Plumbing Fixtures",
                    FileName = "WC-Suite.rfa",
                    Description = "Close-coupled WC with cistern",
                    Parameters = new List<string> { "Flush_Type", "Pan_Type", "Height", "Projection" },
                    Tags = new List<string> { "toilet", "WC", "sanitary" }
                },
                ["Wash_Basin"] = new FamilyLibraryEntry
                {
                    FamilyName = "Wash Basin",
                    Category = "Plumbing Fixtures",
                    FileName = "Wash-Basin.rfa",
                    Description = "Wall-mounted or pedestal wash basin",
                    AvailableWidths = new List<double> { 450, 500, 550, 600, 650, 700 },
                    Parameters = new List<string> { "Width", "Depth", "Mounting_Type", "Tap_Holes" },
                    Tags = new List<string> { "basin", "sink", "sanitary", "bathroom" }
                },

                // Lighting Fixtures
                ["Recessed_Downlight"] = new FamilyLibraryEntry
                {
                    FamilyName = "Recessed Downlight",
                    Category = "Lighting Fixtures",
                    FileName = "Recessed-Downlight.rfa",
                    Description = "Recessed LED downlight",
                    AvailableSizes = new List<string> { "100mm", "125mm", "150mm", "175mm", "200mm" },
                    Parameters = new List<string> { "Cutout_Diameter", "Wattage", "Color_Temperature", "Beam_Angle" },
                    Tags = new List<string> { "downlight", "recessed", "LED", "ceiling" }
                },
                ["Surface_Panel_Light"] = new FamilyLibraryEntry
                {
                    FamilyName = "Surface Panel Light",
                    Category = "Lighting Fixtures",
                    FileName = "Surface-Panel.rfa",
                    Description = "Surface mounted LED panel",
                    AvailableSizes = new List<string> { "300x300", "600x600", "300x1200", "600x1200" },
                    Parameters = new List<string> { "Size", "Wattage", "Color_Temperature", "Lumens" },
                    Tags = new List<string> { "panel", "surface", "LED", "office" }
                },

                // MEP Equipment
                ["Split_AC_Unit"] = new FamilyLibraryEntry
                {
                    FamilyName = "Split AC Indoor Unit",
                    Category = "Mechanical Equipment",
                    FileName = "Split-AC-Indoor.rfa",
                    Description = "Wall-mounted split AC indoor unit",
                    AvailableCapacities = new List<string> { "9000BTU", "12000BTU", "18000BTU", "24000BTU" },
                    Parameters = new List<string> { "Capacity", "Width", "Height", "Depth", "Brand" },
                    Tags = new List<string> { "AC", "split", "cooling", "HVAC" }
                }
            };
        }

        private Dictionary<string, TypeSubstitutionRule> InitializeSubstitutionRules()
        {
            return new Dictionary<string, TypeSubstitutionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["Door_Width_Substitute"] = new TypeSubstitutionRule
                {
                    RuleId = "SUB001",
                    SourceCategory = "Doors",
                    TargetCategory = "Doors",
                    MatchCriteria = MatchCriteria.ClosestDimension,
                    DimensionParameter = "Width",
                    Tolerance = 50, // 50mm tolerance
                    PreferSameFamily = true
                },
                ["Window_Size_Substitute"] = new TypeSubstitutionRule
                {
                    RuleId = "SUB002",
                    SourceCategory = "Windows",
                    TargetCategory = "Windows",
                    MatchCriteria = MatchCriteria.ClosestDimension,
                    DimensionParameter = "Width,Height",
                    Tolerance = 100,
                    PreferSameFamily = true
                },
                ["Column_Size_Substitute"] = new TypeSubstitutionRule
                {
                    RuleId = "SUB003",
                    SourceCategory = "Columns",
                    TargetCategory = "Columns",
                    MatchCriteria = MatchCriteria.NextLargerSize,
                    DimensionParameter = "Width,Depth",
                    Tolerance = 0, // Must be equal or larger
                    AllowTypeCreation = true
                },
                ["Fire_Rating_Substitute"] = new TypeSubstitutionRule
                {
                    RuleId = "SUB004",
                    SourceCategory = "Doors",
                    TargetCategory = "Doors",
                    MatchCriteria = MatchCriteria.ParameterMatch,
                    ParameterToMatch = "Fire_Rating",
                    RequireExactMatch = false, // Can use higher rating
                    PreferHigherRating = true
                }
            };
        }

        #endregion

        #region Family Loading

        /// <summary>
        /// Loads a family into the Revit document.
        /// </summary>
        public async Task<FamilyLoadResult> LoadFamilyAsync(
            string familyPath,
            object revitDocument, // Autodesk.Revit.DB.Document
            LoadFamilyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Loading family: {0}", familyPath);
            options ??= new LoadFamilyOptions();

            var result = new FamilyLoadResult
            {
                FamilyPath = familyPath,
                LoadTime = DateTime.UtcNow
            };

            await Task.Run(() =>
            {
                try
                {
                    // In real implementation, this would use Revit API:
                    // doc.LoadFamily(familyPath, out Family family);

                    // Simulated family loading
                    string familyName = System.IO.Path.GetFileNameWithoutExtension(familyPath);

                    var familyDef = new LoadedFamilyDefinition
                    {
                        FamilyId = Guid.NewGuid().ToString("N"),
                        FamilyName = familyName,
                        FilePath = familyPath,
                        Category = DetermineCategoryFromPath(familyPath),
                        LoadedTime = DateTime.UtcNow
                    };

                    // Load types from family
                    familyDef.Types = DiscoverFamilyTypes(familyPath, familyDef);

                    lock (_lock)
                    {
                        _loadedFamilies[familyName] = familyDef;
                        foreach (var type in familyDef.Types)
                        {
                            _loadedTypes[type.FullTypeName] = type;
                        }
                    }

                    result.Success = true;
                    result.FamilyDefinition = familyDef;
                    result.TypeCount = familyDef.Types.Count;

                    OnFamilyLoaded(familyDef);

                    LogOperation(new FamilyOperation
                    {
                        OperationType = FamilyOperationType.Load,
                        FamilyName = familyName,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load family: {0}", familyPath);
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                }
            }, cancellationToken);

            return result;
        }

        /// <summary>
        /// Loads multiple families from a directory.
        /// </summary>
        public async Task<BatchLoadResult> LoadFamiliesFromDirectoryAsync(
            string directoryPath,
            object revitDocument,
            string searchPattern = "*.rfa",
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Loading families from directory: {0}", directoryPath);

            var result = new BatchLoadResult
            {
                DirectoryPath = directoryPath,
                Results = new List<FamilyLoadResult>()
            };

            // In real implementation, would enumerate files
            // var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);

            // Simulated batch loading
            await Task.Run(() =>
            {
                result.TotalFamilies = 0;
                result.SuccessfulLoads = 0;
                result.FailedLoads = 0;
            }, cancellationToken);

            return result;
        }

        private string DetermineCategoryFromPath(string familyPath)
        {
            var pathLower = familyPath.ToLower();

            if (pathLower.Contains("door")) return "Doors";
            if (pathLower.Contains("window")) return "Windows";
            if (pathLower.Contains("column")) return "Columns";
            if (pathLower.Contains("furniture")) return "Furniture";
            if (pathLower.Contains("plumbing") || pathLower.Contains("fixture")) return "Plumbing Fixtures";
            if (pathLower.Contains("lighting") || pathLower.Contains("light")) return "Lighting Fixtures";
            if (pathLower.Contains("mechanical") || pathLower.Contains("hvac")) return "Mechanical Equipment";

            return "Generic Models";
        }

        private List<FamilyTypeDefinition> DiscoverFamilyTypes(string familyPath, LoadedFamilyDefinition family)
        {
            var types = new List<FamilyTypeDefinition>();

            // In real implementation, would iterate through family.Types
            // Simulated type discovery based on library entry
            string familyKey = family.FamilyName.Replace(" ", "_");

            if (_familyLibrary.TryGetValue(familyKey, out var libraryEntry))
            {
                // Generate types from library entry
                if (libraryEntry.AvailableWidths != null && libraryEntry.AvailableHeights != null)
                {
                    foreach (var width in libraryEntry.AvailableWidths)
                    {
                        foreach (var height in libraryEntry.AvailableHeights)
                        {
                            types.Add(new FamilyTypeDefinition
                            {
                                TypeId = Guid.NewGuid().ToString("N"),
                                TypeName = $"{width}x{height}",
                                FullTypeName = $"{family.FamilyName} : {width}x{height}",
                                FamilyName = family.FamilyName,
                                Category = family.Category,
                                Parameters = new Dictionary<string, object>
                                {
                                    ["Width"] = width,
                                    ["Height"] = height
                                }
                            });
                        }
                    }
                }
                else if (libraryEntry.AvailableSizes != null)
                {
                    foreach (var size in libraryEntry.AvailableSizes)
                    {
                        types.Add(new FamilyTypeDefinition
                        {
                            TypeId = Guid.NewGuid().ToString("N"),
                            TypeName = size,
                            FullTypeName = $"{family.FamilyName} : {size}",
                            FamilyName = family.FamilyName,
                            Category = family.Category,
                            Parameters = new Dictionary<string, object> { ["Size"] = size }
                        });
                    }
                }
            }

            // If no library entry, create default type
            if (types.Count == 0)
            {
                types.Add(new FamilyTypeDefinition
                {
                    TypeId = Guid.NewGuid().ToString("N"),
                    TypeName = "Default",
                    FullTypeName = $"{family.FamilyName} : Default",
                    FamilyName = family.FamilyName,
                    Category = family.Category,
                    Parameters = new Dictionary<string, object>()
                });
            }

            return types;
        }

        #endregion

        #region Type Management

        /// <summary>
        /// Creates a new type within a loaded family.
        /// </summary>
        public async Task<TypeCreationResult> CreateTypeAsync(
            string familyName,
            string newTypeName,
            Dictionary<string, object> parameters,
            object revitDocument,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Creating type '{0}' in family '{1}'", newTypeName, familyName);

            var result = new TypeCreationResult
            {
                FamilyName = familyName,
                TypeName = newTypeName
            };

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_loadedFamilies.TryGetValue(familyName, out var family))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Family not loaded: {familyName}";
                        return;
                    }

                    // Check if type already exists
                    string fullTypeName = $"{familyName} : {newTypeName}";
                    if (_loadedTypes.ContainsKey(fullTypeName))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Type already exists: {fullTypeName}";
                        return;
                    }

                    // In real implementation, would use Revit API:
                    // FamilySymbol newType = family.GetFamilySymbolIds()
                    //    .Select(id => doc.GetElement(id) as FamilySymbol)
                    //    .First()
                    //    .Duplicate(newTypeName) as FamilySymbol;

                    var newType = new FamilyTypeDefinition
                    {
                        TypeId = Guid.NewGuid().ToString("N"),
                        TypeName = newTypeName,
                        FullTypeName = fullTypeName,
                        FamilyName = familyName,
                        Category = family.Category,
                        Parameters = new Dictionary<string, object>(parameters),
                        IsCustomCreated = true,
                        CreatedTime = DateTime.UtcNow
                    };

                    family.Types.Add(newType);
                    _loadedTypes[fullTypeName] = newType;

                    result.Success = true;
                    result.CreatedType = newType;

                    OnTypeCreated(newType);

                    LogOperation(new FamilyOperation
                    {
                        OperationType = FamilyOperationType.CreateType,
                        FamilyName = familyName,
                        TypeName = newTypeName,
                        Success = true
                    });
                }
            }, cancellationToken);

            return result;
        }

        /// <summary>
        /// Modifies parameters of an existing type.
        /// </summary>
        public async Task<TypeModificationResult> ModifyTypeAsync(
            string fullTypeName,
            Dictionary<string, object> parameterChanges,
            object revitDocument,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Modifying type: {0}", fullTypeName);

            var result = new TypeModificationResult
            {
                FullTypeName = fullTypeName
            };

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_loadedTypes.TryGetValue(fullTypeName, out var type))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Type not found: {fullTypeName}";
                        return;
                    }

                    // In real implementation, would use Revit API:
                    // foreach (var param in parameterChanges)
                    // {
                    //     familySymbol.LookupParameter(param.Key).Set(param.Value);
                    // }

                    result.PreviousValues = new Dictionary<string, object>();

                    foreach (var change in parameterChanges)
                    {
                        if (type.Parameters.TryGetValue(change.Key, out var oldValue))
                        {
                            result.PreviousValues[change.Key] = oldValue;
                        }
                        type.Parameters[change.Key] = change.Value;
                    }

                    type.ModifiedTime = DateTime.UtcNow;
                    result.Success = true;

                    LogOperation(new FamilyOperation
                    {
                        OperationType = FamilyOperationType.ModifyType,
                        FamilyName = type.FamilyName,
                        TypeName = type.TypeName,
                        Success = true,
                        Details = $"Modified {parameterChanges.Count} parameters"
                    });
                }
            }, cancellationToken);

            return result;
        }

        /// <summary>
        /// Duplicates an existing type with new name.
        /// </summary>
        public async Task<TypeCreationResult> DuplicateTypeAsync(
            string sourceFullTypeName,
            string newTypeName,
            Dictionary<string, object> parameterOverrides = null,
            object revitDocument = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Duplicating type '{0}' as '{1}'", sourceFullTypeName, newTypeName);

            var result = new TypeCreationResult { TypeName = newTypeName };

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_loadedTypes.TryGetValue(sourceFullTypeName, out var sourceType))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Source type not found: {sourceFullTypeName}";
                        return;
                    }

                    // Copy parameters
                    var newParams = new Dictionary<string, object>(sourceType.Parameters);

                    // Apply overrides
                    if (parameterOverrides != null)
                    {
                        foreach (var kvp in parameterOverrides)
                        {
                            newParams[kvp.Key] = kvp.Value;
                        }
                    }

                    var newType = new FamilyTypeDefinition
                    {
                        TypeId = Guid.NewGuid().ToString("N"),
                        TypeName = newTypeName,
                        FullTypeName = $"{sourceType.FamilyName} : {newTypeName}",
                        FamilyName = sourceType.FamilyName,
                        Category = sourceType.Category,
                        Parameters = newParams,
                        IsCustomCreated = true,
                        CreatedTime = DateTime.UtcNow,
                        DuplicatedFrom = sourceFullTypeName
                    };

                    if (_loadedFamilies.TryGetValue(sourceType.FamilyName, out var family))
                    {
                        family.Types.Add(newType);
                    }

                    _loadedTypes[newType.FullTypeName] = newType;

                    result.Success = true;
                    result.FamilyName = sourceType.FamilyName;
                    result.CreatedType = newType;

                    OnTypeCreated(newType);
                }
            }, cancellationToken);

            return result;
        }

        /// <summary>
        /// Auto-creates a type matching required dimensions if it doesn't exist.
        /// </summary>
        public async Task<TypeCreationResult> AutoCreateTypeIfNeededAsync(
            string familyName,
            Dictionary<string, object> requiredDimensions,
            object revitDocument,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Auto-creating type in family '{0}' if needed", familyName);

            // First check if matching type already exists
            var existingType = FindMatchingType(familyName, requiredDimensions);
            if (existingType != null)
            {
                return new TypeCreationResult
                {
                    Success = true,
                    FamilyName = familyName,
                    TypeName = existingType.TypeName,
                    CreatedType = existingType,
                    WasExisting = true
                };
            }

            // Generate type name from dimensions
            string typeName = GenerateTypeName(requiredDimensions);

            // Create new type
            return await CreateTypeAsync(familyName, typeName, requiredDimensions, revitDocument, cancellationToken);
        }

        private FamilyTypeDefinition FindMatchingType(string familyName, Dictionary<string, object> dimensions)
        {
            lock (_lock)
            {
                var familyTypes = _loadedTypes.Values
                    .Where(t => t.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var type in familyTypes)
                {
                    bool allMatch = true;

                    foreach (var dim in dimensions)
                    {
                        if (type.Parameters.TryGetValue(dim.Key, out var typeValue))
                        {
                            double reqValue = Convert.ToDouble(dim.Value);
                            double actualValue = Convert.ToDouble(typeValue);

                            if (Math.Abs(reqValue - actualValue) > 1) // 1mm tolerance
                            {
                                allMatch = false;
                                break;
                            }
                        }
                        else
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                        return type;
                }
            }

            return null;
        }

        private string GenerateTypeName(Dictionary<string, object> dimensions)
        {
            var parts = new List<string>();

            if (dimensions.TryGetValue("Width", out var width))
                parts.Add($"{Convert.ToInt32(width)}");

            if (dimensions.TryGetValue("Height", out var height))
                parts.Add($"{Convert.ToInt32(height)}");

            if (dimensions.TryGetValue("Depth", out var depth))
                parts.Add($"{Convert.ToInt32(depth)}");

            return parts.Count > 0 ? string.Join("x", parts) : $"Custom_{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        #endregion

        #region Family Swapping

        /// <summary>
        /// Swaps all instances of one family/type with another.
        /// </summary>
        public async Task<SwapResult> SwapFamilyInstancesAsync(
            string sourceFamilyType,
            string targetFamilyType,
            object revitDocument,
            SwapOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Swapping '{0}' with '{1}'", sourceFamilyType, targetFamilyType);
            options ??= new SwapOptions();

            var result = new SwapResult
            {
                SourceType = sourceFamilyType,
                TargetType = targetFamilyType,
                SwappedInstances = new List<SwappedInstance>()
            };

            await Task.Run(() =>
            {
                // In real implementation, would:
                // 1. Find all instances of source type
                // 2. Get target family symbol
                // 3. Swap each instance using Element.ChangeTypeId()

                // Simulated swap
                result.Success = true;
                result.TotalInstancesFound = 0;
                result.SuccessfulSwaps = 0;

                OnFamilySwapped(sourceFamilyType, targetFamilyType, result.SuccessfulSwaps);

                LogOperation(new FamilyOperation
                {
                    OperationType = FamilyOperationType.Swap,
                    FamilyName = sourceFamilyType,
                    TargetFamilyName = targetFamilyType,
                    Success = true,
                    Details = $"Swapped {result.SuccessfulSwaps} instances"
                });
            }, cancellationToken);

            return result;
        }

        /// <summary>
        /// Finds the best matching replacement type for an element.
        /// </summary>
        public FamilyTypeDefinition FindBestMatchingType(
            string category,
            Dictionary<string, object> requiredProperties,
            MatchPreferences preferences = null)
        {
            preferences ??= new MatchPreferences();

            lock (_lock)
            {
                var candidates = _loadedTypes.Values
                    .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (candidates.Count == 0)
                    return null;

                // Score each candidate
                var scored = candidates
                    .Select(c => new
                    {
                        Type = c,
                        Score = CalculateMatchScore(c, requiredProperties, preferences)
                    })
                    .Where(s => s.Score > preferences.MinimumScore)
                    .OrderByDescending(s => s.Score)
                    .ToList();

                return scored.FirstOrDefault()?.Type;
            }
        }

        /// <summary>
        /// Suggests alternative families/types based on requirements.
        /// </summary>
        public List<TypeSuggestion> SuggestAlternatives(
            string category,
            Dictionary<string, object> requirements,
            int maxSuggestions = 5)
        {
            var suggestions = new List<TypeSuggestion>();

            lock (_lock)
            {
                var candidates = _loadedTypes.Values
                    .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Also check library for unloaded families
                var libraryMatches = _familyLibrary.Values
                    .Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Score loaded types
                foreach (var candidate in candidates)
                {
                    var score = CalculateMatchScore(candidate, requirements, new MatchPreferences());
                    if (score > 0.5)
                    {
                        suggestions.Add(new TypeSuggestion
                        {
                            TypeDefinition = candidate,
                            MatchScore = score,
                            IsLoaded = true,
                            ReasonForSuggestion = GetSuggestionReason(candidate, requirements)
                        });
                    }
                }

                // Add library suggestions for unloaded families
                foreach (var libEntry in libraryMatches)
                {
                    if (!_loadedFamilies.ContainsKey(libEntry.FamilyName.Replace(" ", "_")))
                    {
                        var score = CalculateLibraryMatchScore(libEntry, requirements);
                        if (score > 0.5)
                        {
                            suggestions.Add(new TypeSuggestion
                            {
                                FamilyName = libEntry.FamilyName,
                                LibraryEntry = libEntry,
                                MatchScore = score,
                                IsLoaded = false,
                                RequiresLoading = true,
                                ReasonForSuggestion = $"Library family matching {category} requirements"
                            });
                        }
                    }
                }
            }

            return suggestions
                .OrderByDescending(s => s.MatchScore)
                .Take(maxSuggestions)
                .ToList();
        }

        private double CalculateMatchScore(
            FamilyTypeDefinition type,
            Dictionary<string, object> requirements,
            MatchPreferences preferences)
        {
            double score = 0.5; // Base score

            foreach (var req in requirements)
            {
                if (type.Parameters.TryGetValue(req.Key, out var typeValue))
                {
                    if (req.Value is double reqDouble && typeValue is double typeDouble)
                    {
                        double diff = Math.Abs(reqDouble - typeDouble);
                        double tolerance = preferences.DimensionTolerance > 0 ? preferences.DimensionTolerance : 50;

                        if (diff <= tolerance)
                            score += 0.2;
                        else if (diff <= tolerance * 2)
                            score += 0.1;
                    }
                    else if (req.Value?.ToString() == typeValue?.ToString())
                    {
                        score += 0.2; // Exact string match
                    }
                }
            }

            // Bonus for same family preference
            if (preferences.PreferredFamily != null &&
                type.FamilyName.Equals(preferences.PreferredFamily, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.15;
            }

            return Math.Min(score, 1.0);
        }

        private double CalculateLibraryMatchScore(FamilyLibraryEntry entry, Dictionary<string, object> requirements)
        {
            double score = 0.4; // Base score for library (lower than loaded)

            if (requirements.TryGetValue("Width", out var reqWidth))
            {
                double width = Convert.ToDouble(reqWidth);
                if (entry.AvailableWidths?.Any(w => Math.Abs(w - width) <= 50) == true)
                    score += 0.2;
            }

            if (requirements.TryGetValue("Height", out var reqHeight))
            {
                double height = Convert.ToDouble(reqHeight);
                if (entry.AvailableHeights?.Any(h => Math.Abs(h - height) <= 50) == true)
                    score += 0.2;
            }

            return score;
        }

        private string GetSuggestionReason(FamilyTypeDefinition type, Dictionary<string, object> requirements)
        {
            var reasons = new List<string>();

            foreach (var req in requirements)
            {
                if (type.Parameters.TryGetValue(req.Key, out var value))
                {
                    if (req.Value?.ToString() == value?.ToString())
                        reasons.Add($"Exact {req.Key} match");
                    else
                        reasons.Add($"Close {req.Key} match");
                }
            }

            return reasons.Count > 0 ? string.Join(", ", reasons) : "Category match";
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Gets all loaded families.
        /// </summary>
        public IEnumerable<LoadedFamilyDefinition> GetLoadedFamilies()
        {
            lock (_lock)
            {
                return _loadedFamilies.Values.ToList();
            }
        }

        /// <summary>
        /// Gets all types for a family.
        /// </summary>
        public IEnumerable<FamilyTypeDefinition> GetFamilyTypes(string familyName)
        {
            lock (_lock)
            {
                return _loadedTypes.Values
                    .Where(t => t.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets types by category.
        /// </summary>
        public IEnumerable<FamilyTypeDefinition> GetTypesByCategory(string category)
        {
            lock (_lock)
            {
                return _loadedTypes.Values
                    .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets library entries by category.
        /// </summary>
        public IEnumerable<FamilyLibraryEntry> GetLibraryEntriesByCategory(string category)
        {
            return _familyLibrary.Values
                .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Searches library by tags.
        /// </summary>
        public IEnumerable<FamilyLibraryEntry> SearchLibraryByTags(params string[] tags)
        {
            return _familyLibrary.Values
                .Where(e => tags.Any(tag => e.Tags?.Contains(tag, StringComparer.OrdinalIgnoreCase) == true))
                .ToList();
        }

        /// <summary>
        /// Gets operation history.
        /// </summary>
        public IEnumerable<FamilyOperation> GetOperationHistory()
        {
            lock (_lock)
            {
                return _operationHistory.ToList();
            }
        }

        #endregion

        #region Utilities

        private void LogOperation(FamilyOperation operation)
        {
            operation.Timestamp = DateTime.UtcNow;
            lock (_lock)
            {
                _operationHistory.Add(operation);
            }
        }

        private void OnFamilyLoaded(LoadedFamilyDefinition family)
        {
            FamilyLoaded?.Invoke(this, new FamilyLoadedEventArgs(family));
        }

        private void OnTypeCreated(FamilyTypeDefinition type)
        {
            TypeCreated?.Invoke(this, new TypeCreatedEventArgs(type));
        }

        private void OnFamilySwapped(string source, string target, int count)
        {
            FamilySwapped?.Invoke(this, new FamilySwappedEventArgs(source, target, count));
        }

        #endregion
    }

    #region Data Models

    public class LoadedFamilyDefinition
    {
        public string FamilyId { get; set; }
        public string FamilyName { get; set; }
        public string FilePath { get; set; }
        public string Category { get; set; }
        public DateTime LoadedTime { get; set; }
        public List<FamilyTypeDefinition> Types { get; set; } = new List<FamilyTypeDefinition>();
    }

    public class FamilyTypeDefinition
    {
        public string TypeId { get; set; }
        public string TypeName { get; set; }
        public string FullTypeName { get; set; }
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public bool IsCustomCreated { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? ModifiedTime { get; set; }
        public string DuplicatedFrom { get; set; }
    }

    public class FamilyLibraryEntry
    {
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public string FileName { get; set; }
        public string Description { get; set; }
        public List<double> AvailableWidths { get; set; }
        public List<double> AvailableHeights { get; set; }
        public List<double> AvailableDiameters { get; set; }
        public List<string> AvailableSizes { get; set; }
        public List<string> AvailableCapacities { get; set; }
        public List<string> FireRatings { get; set; }
        public List<string> Parameters { get; set; }
        public List<string> Tags { get; set; }
    }

    public class TypeSubstitutionRule
    {
        public string RuleId { get; set; }
        public string SourceCategory { get; set; }
        public string TargetCategory { get; set; }
        public MatchCriteria MatchCriteria { get; set; }
        public string DimensionParameter { get; set; }
        public string ParameterToMatch { get; set; }
        public double Tolerance { get; set; }
        public bool PreferSameFamily { get; set; }
        public bool RequireExactMatch { get; set; }
        public bool PreferHigherRating { get; set; }
        public bool AllowTypeCreation { get; set; }
    }

    public enum MatchCriteria
    {
        ExactMatch,
        ClosestDimension,
        NextLargerSize,
        ParameterMatch
    }

    public class LoadFamilyOptions
    {
        public bool OverwriteExisting { get; set; } = false;
        public bool LoadParameterValues { get; set; } = true;
    }

    public class FamilyLoadResult
    {
        public string FamilyPath { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public LoadedFamilyDefinition FamilyDefinition { get; set; }
        public int TypeCount { get; set; }
        public DateTime LoadTime { get; set; }
    }

    public class BatchLoadResult
    {
        public string DirectoryPath { get; set; }
        public int TotalFamilies { get; set; }
        public int SuccessfulLoads { get; set; }
        public int FailedLoads { get; set; }
        public List<FamilyLoadResult> Results { get; set; }
    }

    public class TypeCreationResult
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public FamilyTypeDefinition CreatedType { get; set; }
        public bool WasExisting { get; set; }
    }

    public class TypeModificationResult
    {
        public string FullTypeName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> PreviousValues { get; set; }
    }

    public class SwapOptions
    {
        public bool PreserveLocation { get; set; } = true;
        public bool PreserveRotation { get; set; } = true;
        public bool MatchParameters { get; set; } = true;
        public bool SelectSwapped { get; set; } = false;
    }

    public class SwapResult
    {
        public string SourceType { get; set; }
        public string TargetType { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalInstancesFound { get; set; }
        public int SuccessfulSwaps { get; set; }
        public List<SwappedInstance> SwappedInstances { get; set; }
    }

    public class SwappedInstance
    {
        public string InstanceId { get; set; }
        public string OriginalType { get; set; }
        public string NewType { get; set; }
    }

    public class MatchPreferences
    {
        public double MinimumScore { get; set; } = 0.5;
        public double DimensionTolerance { get; set; } = 50;
        public string PreferredFamily { get; set; }
        public bool AllowDifferentFamily { get; set; } = true;
    }

    public class TypeSuggestion
    {
        public FamilyTypeDefinition TypeDefinition { get; set; }
        public string FamilyName { get; set; }
        public FamilyLibraryEntry LibraryEntry { get; set; }
        public double MatchScore { get; set; }
        public bool IsLoaded { get; set; }
        public bool RequiresLoading { get; set; }
        public string ReasonForSuggestion { get; set; }
    }

    public class FamilyOperation
    {
        public FamilyOperationType OperationType { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string TargetFamilyName { get; set; }
        public bool Success { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum FamilyOperationType
    {
        Load,
        CreateType,
        ModifyType,
        DuplicateType,
        Swap,
        Delete
    }

    public class FamilyLoadedEventArgs : EventArgs
    {
        public LoadedFamilyDefinition Family { get; }
        public FamilyLoadedEventArgs(LoadedFamilyDefinition family) { Family = family; }
    }

    public class TypeCreatedEventArgs : EventArgs
    {
        public FamilyTypeDefinition Type { get; }
        public TypeCreatedEventArgs(FamilyTypeDefinition type) { Type = type; }
    }

    public class FamilySwappedEventArgs : EventArgs
    {
        public string SourceType { get; }
        public string TargetType { get; }
        public int SwapCount { get; }
        public FamilySwappedEventArgs(string source, string target, int count)
        {
            SourceType = source;
            TargetType = target;
            SwapCount = count;
        }
    }

    #endregion
}
