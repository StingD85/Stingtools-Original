// ============================================================================
// StingBIM.AI.Creation - Unified Create Panel Controller
// Central hub for all element creation, family management, and import operations
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Panel
{
    /// <summary>
    /// Unified Create Panel Controller
    /// Single interface for all creation operations in Revit
    /// </summary>
    public sealed class CreatePanelController
    {
        private static readonly Lazy<CreatePanelController> _instance =
            new Lazy<CreatePanelController>(() => new CreatePanelController());
        public static CreatePanelController Instance => _instance.Value;

        // ISO 19650 Parameter Constants
        private const string PARAM_CREATED_BY = "MR_CREATED_BY";
        private const string PARAM_CREATION_DATE = "MR_CREATION_DATE";
        private const string PARAM_CREATION_METHOD = "MR_CREATION_METHOD";
        private const string PARAM_PROJECT_CODE = "MR_PROJECT_CODE";

        private readonly Dictionary<string, CreatorInfo> _creators;
        private readonly Dictionary<string, ImporterInfo> _importers;
        private readonly List<CreationRecord> _history;
        private readonly object _lockObject = new object();

        private PanelState _state = PanelState.Ready;
        private CreatorInfo _activeCreator;
        private Dictionary<string, object> _parameters = new Dictionary<string, object>();

        public event EventHandler<CreationEventArgs> ElementCreated;
        public event EventHandler<CreationEventArgs> CreationFailed;
        public event EventHandler<PanelStateEventArgs> StateChanged;

        public PanelState State => _state;
        public CreatorInfo ActiveCreator => _activeCreator;

        private CreatePanelController()
        {
            _creators = InitializeCreators();
            _importers = InitializeImporters();
            _history = new List<CreationRecord>();
        }

        #region Initialization

        private Dictionary<string, CreatorInfo> InitializeCreators()
        {
            return new Dictionary<string, CreatorInfo>(StringComparer.OrdinalIgnoreCase)
            {
                // Architectural
                ["Wall"] = new CreatorInfo
                {
                    Name = "Wall",
                    Category = CreationCategory.Architectural,
                    Description = "Create walls with parameter assignment",
                    Icon = "wall",
                    Keywords = new[] { "wall", "partition" },
                    Required = new[] { "Location", "Height", "Type" },
                    Optional = new[] { "BaseOffset", "TopOffset", "Structural" }
                },
                ["Floor"] = new CreatorInfo
                {
                    Name = "Floor",
                    Category = CreationCategory.Architectural,
                    Description = "Create floors and slabs",
                    Icon = "floor",
                    Keywords = new[] { "floor", "slab" },
                    Required = new[] { "Boundary", "Type" },
                    Optional = new[] { "Offset", "Structural" }
                },
                ["Door"] = new CreatorInfo
                {
                    Name = "Door",
                    Category = CreationCategory.Architectural,
                    Description = "Place doors with code compliance",
                    Icon = "door",
                    Keywords = new[] { "door", "entry" },
                    Required = new[] { "HostWall", "Location" },
                    Optional = new[] { "Width", "Height", "SwingDirection" }
                },
                ["Window"] = new CreatorInfo
                {
                    Name = "Window",
                    Category = CreationCategory.Architectural,
                    Description = "Place windows with daylighting",
                    Icon = "window",
                    Keywords = new[] { "window", "glazing" },
                    Required = new[] { "HostWall", "Location" },
                    Optional = new[] { "Width", "Height", "SillHeight" }
                },
                ["Roof"] = new CreatorInfo
                {
                    Name = "Roof",
                    Category = CreationCategory.Architectural,
                    Description = "Create roofs with drainage",
                    Icon = "roof",
                    Keywords = new[] { "roof" },
                    Required = new[] { "Footprint", "Type" },
                    Optional = new[] { "Slope", "Overhang" }
                },
                ["Ceiling"] = new CreatorInfo
                {
                    Name = "Ceiling",
                    Category = CreationCategory.Architectural,
                    Description = "Create ceilings with grid layout",
                    Icon = "ceiling",
                    Keywords = new[] { "ceiling" },
                    Required = new[] { "Room", "Type" },
                    Optional = new[] { "Height" }
                },
                ["Stair"] = new CreatorInfo
                {
                    Name = "Stair",
                    Category = CreationCategory.Architectural,
                    Description = "Create code-compliant stairs",
                    Icon = "stair",
                    Keywords = new[] { "stair", "stairs" },
                    Required = new[] { "BaseLevel", "TopLevel" },
                    Optional = new[] { "Width", "RiserHeight" }
                },
                ["CurtainWall"] = new CreatorInfo
                {
                    Name = "CurtainWall",
                    Category = CreationCategory.Architectural,
                    Description = "Create curtain walls",
                    Icon = "curtainwall",
                    Keywords = new[] { "curtain", "facade" },
                    Required = new[] { "Location", "Height" },
                    Optional = new[] { "GridU", "GridV" }
                },
                ["Room"] = new CreatorInfo
                {
                    Name = "Room",
                    Category = CreationCategory.Architectural,
                    Description = "Place rooms with naming",
                    Icon = "room",
                    Keywords = new[] { "room", "space" },
                    Required = new[] { "Location" },
                    Optional = new[] { "Name", "Number" }
                },

                // Structural
                ["Column"] = new CreatorInfo
                {
                    Name = "Column",
                    Category = CreationCategory.Structural,
                    Description = "Place structural columns",
                    Icon = "column",
                    Keywords = new[] { "column", "pillar" },
                    Required = new[] { "Location", "BaseLevel", "TopLevel" },
                    Optional = new[] { "Type", "Rotation" }
                },
                ["Beam"] = new CreatorInfo
                {
                    Name = "Beam",
                    Category = CreationCategory.Structural,
                    Description = "Place structural beams",
                    Icon = "beam",
                    Keywords = new[] { "beam", "girder" },
                    Required = new[] { "StartPoint", "EndPoint" },
                    Optional = new[] { "Type" }
                },
                ["Foundation"] = new CreatorInfo
                {
                    Name = "Foundation",
                    Category = CreationCategory.Structural,
                    Description = "Create foundations",
                    Icon = "foundation",
                    Keywords = new[] { "foundation", "footing" },
                    Required = new[] { "Location", "Type" },
                    Optional = new[] { "Width", "Depth" }
                },

                // MEP
                ["Duct"] = new CreatorInfo
                {
                    Name = "Duct",
                    Category = CreationCategory.MEP,
                    Description = "Create ductwork",
                    Icon = "duct",
                    Keywords = new[] { "duct", "hvac" },
                    Required = new[] { "StartPoint", "EndPoint", "System" },
                    Optional = new[] { "Size", "Shape" }
                },
                ["Pipe"] = new CreatorInfo
                {
                    Name = "Pipe",
                    Category = CreationCategory.MEP,
                    Description = "Create piping",
                    Icon = "pipe",
                    Keywords = new[] { "pipe", "plumbing" },
                    Required = new[] { "StartPoint", "EndPoint", "System" },
                    Optional = new[] { "Diameter", "Material" }
                },
                ["CableTray"] = new CreatorInfo
                {
                    Name = "CableTray",
                    Category = CreationCategory.MEP,
                    Description = "Create cable trays",
                    Icon = "cabletray",
                    Keywords = new[] { "cable", "tray" },
                    Required = new[] { "StartPoint", "EndPoint" },
                    Optional = new[] { "Width", "Height" }
                },
                ["Fixture"] = new CreatorInfo
                {
                    Name = "Fixture",
                    Category = CreationCategory.MEP,
                    Description = "Place MEP fixtures",
                    Icon = "fixture",
                    Keywords = new[] { "fixture", "toilet", "sink", "light" },
                    Required = new[] { "Location", "Family" },
                    Optional = new[] { "HostElement", "Rotation" }
                },

                // Generic
                ["Family"] = new CreatorInfo
                {
                    Name = "Family",
                    Category = CreationCategory.Generic,
                    Description = "Place any family instance",
                    Icon = "family",
                    Keywords = new[] { "family", "component" },
                    Required = new[] { "Family", "Location" },
                    Optional = new[] { "Type", "Rotation", "Host" }
                },
                ["ModelLine"] = new CreatorInfo
                {
                    Name = "ModelLine",
                    Category = CreationCategory.Generic,
                    Description = "Create model lines",
                    Icon = "line",
                    Keywords = new[] { "line", "curve" },
                    Required = new[] { "Points" },
                    Optional = new[] { "Style" }
                }
            };
        }

        private Dictionary<string, ImporterInfo> InitializeImporters()
        {
            return new Dictionary<string, ImporterInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["DWG"] = new ImporterInfo
                {
                    Name = "DWG Import",
                    Description = "Import AutoCAD files to BIM",
                    Extensions = new[] { ".dwg", ".dxf" },
                    Icon = "dwg",
                    Features = new[] { "Layer mapping", "Wall conversion", "Block mapping" }
                },
                ["PDF"] = new ImporterInfo
                {
                    Name = "PDF Import",
                    Description = "Extract BIM from PDF drawings",
                    Extensions = new[] { ".pdf" },
                    Icon = "pdf",
                    Features = new[] { "Floor plan recognition", "OCR", "Scale detection" }
                },
                ["Image"] = new ImporterInfo
                {
                    Name = "Image Import",
                    Description = "Convert scanned drawings",
                    Extensions = new[] { ".png", ".jpg", ".jpeg", ".tiff" },
                    Icon = "image",
                    Features = new[] { "Line detection", "Symbol recognition" }
                },
                ["PointCloud"] = new ImporterInfo
                {
                    Name = "Point Cloud",
                    Description = "Import point cloud data",
                    Extensions = new[] { ".rcp", ".rcs", ".pts", ".e57" },
                    Icon = "pointcloud",
                    Features = new[] { "Scan-to-BIM", "Plane detection" }
                },
                ["IFC"] = new ImporterInfo
                {
                    Name = "IFC Import",
                    Description = "Import IFC models",
                    Extensions = new[] { ".ifc" },
                    Icon = "ifc",
                    Features = new[] { "Element mapping", "Parameter transfer" }
                },
                ["Excel"] = new ImporterInfo
                {
                    Name = "Excel Data",
                    Description = "Create from spreadsheet",
                    Extensions = new[] { ".xlsx", ".csv" },
                    Icon = "excel",
                    Features = new[] { "Room data", "Parameter import" }
                }
            };
        }

        #endregion

        #region State Management

        public void SetState(PanelState newState)
        {
            var oldState = _state;
            _state = newState;
            OnStateChanged(oldState, newState);
        }

        public void SelectCreator(string name)
        {
            if (_creators.TryGetValue(name, out var creator))
            {
                _activeCreator = creator;
                _parameters.Clear();
                SetState(PanelState.CreatorSelected);
            }
        }

        public void SetParameter(string name, object value)
        {
            _parameters[name] = value;
        }

        public void ClearSelection()
        {
            _activeCreator = null;
            _parameters.Clear();
            SetState(PanelState.Ready);
        }

        #endregion

        #region Creation Methods

        /// <summary>
        /// Create element with specified parameters
        /// </summary>
        public async Task<CreationResult> CreateAsync(
            string creatorName,
            Dictionary<string, object> parameters,
            CreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new CreationOptions();
            var result = new CreationResult { CreatorName = creatorName };

            try
            {
                if (!_creators.TryGetValue(creatorName, out var creator))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Unknown creator: {creatorName}";
                    return result;
                }

                SetState(PanelState.Creating);

                // Validate required parameters
                var missing = creator.Required.Where(p => !parameters.ContainsKey(p)).ToList();
                if (missing.Any())
                {
                    result.Success = false;
                    result.ErrorMessage = $"Missing: {string.Join(", ", missing)}";
                    return result;
                }

                // Add standard parameters
                parameters[PARAM_CREATED_BY] = options.UserName ?? "StingBIM";
                parameters[PARAM_CREATION_DATE] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                parameters[PARAM_CREATION_METHOD] = "CreatePanel";

                if (!string.IsNullOrEmpty(options.ProjectCode))
                {
                    parameters[PARAM_PROJECT_CODE] = options.ProjectCode;
                }

                // Create element
                var element = await DispatchCreationAsync(creator, parameters, cancellationToken);

                result.Success = element != null;
                result.Element = element;
                result.Parameters = parameters;

                // Record history
                RecordHistory(creatorName, parameters, result.Success, element?.Id);

                if (result.Success)
                {
                    OnElementCreated(result);
                }
                else
                {
                    OnCreationFailed(result);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                OnCreationFailed(result);
            }
            finally
            {
                SetState(PanelState.Ready);
            }

            return result;
        }

        /// <summary>
        /// Create from natural language command
        /// </summary>
        public async Task<CreationResult> CreateFromPromptAsync(
            string prompt,
            CreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new CreationOptions();

            // Parse the prompt
            var parsed = ParsePrompt(prompt);
            if (parsed == null || string.IsNullOrEmpty(parsed.CreatorName))
            {
                return new CreationResult
                {
                    Success = false,
                    ErrorMessage = $"Could not understand: {prompt}"
                };
            }

            return await CreateAsync(parsed.CreatorName, parsed.Parameters, options, cancellationToken);
        }

        /// <summary>
        /// Place family instance
        /// </summary>
        public async Task<CreationResult> PlaceFamilyAsync(
            string familyName,
            string typeName,
            Point3D location,
            PlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new PlacementOptions();

            var parameters = new Dictionary<string, object>
            {
                ["Family"] = familyName,
                ["Type"] = typeName,
                ["Location"] = location,
                ["Rotation"] = options.Rotation
            };

            if (!string.IsNullOrEmpty(options.HostElement))
            {
                parameters["Host"] = options.HostElement;
            }

            return await CreateAsync("Family", parameters, options.ToCreationOptions(), cancellationToken);
        }

        /// <summary>
        /// Import external file
        /// </summary>
        public async Task<ImportResult> ImportAsync(
            string filePath,
            ImportOptions options = null,
            IProgress<ImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ImportOptions();
            var result = new ImportResult { FilePath = filePath };

            try
            {
                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                var importer = _importers.Values.FirstOrDefault(i => i.Extensions.Contains(ext));

                if (importer == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"No importer for: {ext}";
                    return result;
                }

                SetState(PanelState.Importing);
                progress?.Report(new ImportProgress { Stage = "Starting", Percentage = 0 });

                // Dispatch to importer
                result = await DispatchImportAsync(importer, filePath, options, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                SetState(PanelState.Ready);
            }

            return result;
        }

        /// <summary>
        /// Quick create with minimal parameters
        /// </summary>
        public async Task<CreationResult> QuickCreateAsync(
            string elementType,
            Point3D location,
            CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object>
            {
                ["Location"] = location
            };

            return await CreateAsync(elementType, parameters, null, cancellationToken);
        }

        #endregion

        #region Search and Query

        /// <summary>
        /// Search creators by keyword
        /// </summary>
        public List<CreatorInfo> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _creators.Values.ToList();
            }

            var q = query.ToLowerInvariant();
            return _creators.Values
                .Where(c =>
                    c.Name.ToLower().Contains(q) ||
                    c.Description.ToLower().Contains(q) ||
                    c.Keywords.Any(k => k.Contains(q)))
                .OrderByDescending(c => c.Keywords.Count(k => k.Contains(q)))
                .ToList();
        }

        /// <summary>
        /// Get creators by category
        /// </summary>
        public List<CreatorInfo> GetByCategory(CreationCategory category)
        {
            return _creators.Values
                .Where(c => c.Category == category)
                .OrderBy(c => c.Name)
                .ToList();
        }

        /// <summary>
        /// Get all categories
        /// </summary>
        public List<CategoryInfo> GetCategories()
        {
            return Enum.GetValues(typeof(CreationCategory))
                .Cast<CreationCategory>()
                .Select(c => new CategoryInfo
                {
                    Category = c,
                    DisplayName = c.ToString(),
                    Count = _creators.Values.Count(cr => cr.Category == c)
                })
                .Where(c => c.Count > 0)
                .ToList();
        }

        /// <summary>
        /// Get all importers
        /// </summary>
        public List<ImporterInfo> GetImporters()
        {
            return _importers.Values.ToList();
        }

        /// <summary>
        /// Get creation history
        /// </summary>
        public List<CreationRecord> GetHistory(int count = 50)
        {
            lock (_lockObject)
            {
                return _history.OrderByDescending(h => h.Timestamp).Take(count).ToList();
            }
        }

        /// <summary>
        /// Get frequently used creators
        /// </summary>
        public List<CreatorUsage> GetFrequent(int count = 10)
        {
            lock (_lockObject)
            {
                return _history
                    .GroupBy(h => h.CreatorName)
                    .Select(g => new CreatorUsage
                    {
                        CreatorName = g.Key,
                        UseCount = g.Count(),
                        LastUsed = g.Max(h => h.Timestamp)
                    })
                    .OrderByDescending(u => u.UseCount)
                    .Take(count)
                    .ToList();
            }
        }

        #endregion

        #region Helper Methods

        private ParsedPrompt ParsePrompt(string prompt)
        {
            var promptLower = prompt.ToLowerInvariant();

            foreach (var creator in _creators.Values)
            {
                foreach (var keyword in creator.Keywords)
                {
                    if (promptLower.Contains(keyword))
                    {
                        return new ParsedPrompt
                        {
                            CreatorName = creator.Name,
                            Parameters = ExtractParameters(prompt, creator)
                        };
                    }
                }
            }

            return null;
        }

        private Dictionary<string, object> ExtractParameters(string prompt, CreatorInfo creator)
        {
            var parameters = new Dictionary<string, object>();

            // Extract dimensions (e.g., "3m x 4m")
            var dimPattern = @"(\d+(?:\.\d+)?)\s*(?:x|by)\s*(\d+(?:\.\d+)?)\s*(m|ft|mm)?";
            var dimMatch = System.Text.RegularExpressions.Regex.Match(prompt, dimPattern);
            if (dimMatch.Success)
            {
                double factor = dimMatch.Groups[3].Value == "ft" ? 304.8 : 1000;
                parameters["Length"] = double.Parse(dimMatch.Groups[1].Value) * factor;
                parameters["Width"] = double.Parse(dimMatch.Groups[2].Value) * factor;
            }

            // Extract height (e.g., "3m high")
            var heightPattern = @"(\d+(?:\.\d+)?)\s*(m|ft|mm)?\s*(?:high|tall|height)";
            var heightMatch = System.Text.RegularExpressions.Regex.Match(prompt, heightPattern);
            if (heightMatch.Success)
            {
                double factor = heightMatch.Groups[2].Value == "ft" ? 304.8 : 1000;
                parameters["Height"] = double.Parse(heightMatch.Groups[1].Value) * factor;
            }

            return parameters;
        }

        private async Task<CreatedElement> DispatchCreationAsync(
            CreatorInfo creator,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            // Would dispatch to actual element creator
            await Task.Delay(50, cancellationToken);

            return new CreatedElement
            {
                Id = Guid.NewGuid().ToString(),
                ElementType = creator.Name,
                Parameters = parameters.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? "")
            };
        }

        private async Task<ImportResult> DispatchImportAsync(
            ImporterInfo importer,
            string filePath,
            ImportOptions options,
            IProgress<ImportProgress> progress,
            CancellationToken cancellationToken)
        {
            // Would dispatch to actual importer
            await Task.Delay(100, cancellationToken);

            return new ImportResult
            {
                Success = true,
                FilePath = filePath,
                ElementsCreated = 0
            };
        }

        private void RecordHistory(string creatorName, Dictionary<string, object> parameters, bool success, string elementId)
        {
            lock (_lockObject)
            {
                _history.Add(new CreationRecord
                {
                    Timestamp = DateTime.Now,
                    CreatorName = creatorName,
                    Parameters = new Dictionary<string, object>(parameters),
                    Success = success,
                    ElementId = elementId
                });

                while (_history.Count > 1000)
                {
                    _history.RemoveAt(0);
                }
            }
        }

        private void OnElementCreated(CreationResult result)
        {
            ElementCreated?.Invoke(this, new CreationEventArgs { Result = result });
        }

        private void OnCreationFailed(CreationResult result)
        {
            CreationFailed?.Invoke(this, new CreationEventArgs { Result = result });
        }

        private void OnStateChanged(PanelState oldState, PanelState newState)
        {
            StateChanged?.Invoke(this, new PanelStateEventArgs
            {
                OldState = oldState,
                NewState = newState
            });
        }

        #endregion
    }

    #region Data Models

    public enum CreationCategory { Architectural, Structural, MEP, Site, Generic }
    public enum PanelState { Ready, CreatorSelected, ParameterInput, Creating, Importing, Error }

    public class CreatorInfo
    {
        public string Name { get; set; }
        public CreationCategory Category { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string[] Keywords { get; set; }
        public string[] Required { get; set; }
        public string[] Optional { get; set; }
    }

    public class ImporterInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] Extensions { get; set; }
        public string Icon { get; set; }
        public string[] Features { get; set; }
    }

    public class CategoryInfo
    {
        public CreationCategory Category { get; set; }
        public string DisplayName { get; set; }
        public int Count { get; set; }
    }

    public class ParsedPrompt
    {
        public string CreatorName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class CreatedElement
    {
        public string Id { get; set; }
        public string ElementType { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public class CreationResult
    {
        public bool Success { get; set; }
        public string CreatorName { get; set; }
        public string ErrorMessage { get; set; }
        public CreatedElement Element { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string ErrorMessage { get; set; }
        public int ElementsCreated { get; set; }
        public List<CreatedElement> Elements { get; set; } = new List<CreatedElement>();
    }

    public class ImportProgress
    {
        public string Stage { get; set; }
        public int Percentage { get; set; }
    }

    public class CreationOptions
    {
        public string UserName { get; set; }
        public string ProjectCode { get; set; }
        public bool AutoAssign { get; set; } = true;
        public bool Validate { get; set; } = true;
    }

    public class ImportOptions : CreationOptions
    {
        public bool AutoConvert { get; set; } = true;
        public bool PreserveOriginal { get; set; } = true;
        public string TargetLevel { get; set; }
    }

    public class PlacementOptions
    {
        public double Rotation { get; set; }
        public string HostElement { get; set; }

        public CreationOptions ToCreationOptions() => new CreationOptions();
    }

    public class CreationRecord
    {
        public DateTime Timestamp { get; set; }
        public string CreatorName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public bool Success { get; set; }
        public string ElementId { get; set; }
    }

    public class CreatorUsage
    {
        public string CreatorName { get; set; }
        public int UseCount { get; set; }
        public DateTime LastUsed { get; set; }
    }

    public class CreationEventArgs : EventArgs
    {
        public CreationResult Result { get; set; }
    }

    public class PanelStateEventArgs : EventArgs
    {
        public PanelState OldState { get; set; }
        public PanelState NewState { get; set; }
    }

    #endregion
}
