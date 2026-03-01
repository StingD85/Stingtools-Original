// ===================================================================
// StingBIM Family Management System
// Library indexing, search, type editing, smart placement, manufacturer data
// Comprehensive family management for Revit projects
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Families
{
    #region Enums

    public enum FamilyCategory
    {
        Doors, Windows, Furniture, Casework, Plumbing, Electrical, Mechanical,
        Lighting, StructuralColumns, StructuralFraming, GenericModels, Specialty,
        Entourage, Planting, Site, CurtainPanels, ProfileFamilies, DetailComponents,
        Annotations, Tags, Titleblocks
    }

    public enum FamilySource { System, Project, External, Manufacturer, Custom }
    public enum PlacementType { HostBased, FreeStanding, FaceBased, WorkPlaneBased, CurveBased, LineBased }
    public enum FamilyStatus { Loaded, NotLoaded, Outdated, Missing, Corrupted }

    #endregion

    #region Core Data Models

    public class FamilyDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public FamilyCategory Category { get; set; }
        public FamilySource Source { get; set; }
        public string FilePath { get; set; }
        public FamilyStatus Status { get; set; }
        public List<FamilyType> Types { get; set; } = new();
        public List<FamilyParameter> Parameters { get; set; } = new();
        public PlacementType PlacementType { get; set; }
        public string HostCategory { get; set; }
        public ManufacturerInfo Manufacturer { get; set; }
        public FamilyMetadata Metadata { get; set; }
        public DateTime LoadedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Version { get; set; }
        public byte[] Thumbnail { get; set; }
    }

    public class FamilyType
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string FamilyId { get; set; }
        public Dictionary<string, object> ParameterValues { get; set; } = new();
        public FamilyDimensions Dimensions { get; set; }
        public string Description { get; set; }
        public string ProductCode { get; set; }
        public double Cost { get; set; }
        public bool IsDefault { get; set; }
    }

    public class FamilyParameter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string ParameterType { get; set; }
        public string Group { get; set; }
        public bool IsInstance { get; set; }
        public bool IsShared { get; set; }
        public string SharedParameterGuid { get; set; }
        public object DefaultValue { get; set; }
        public bool IsReadOnly { get; set; }
        public string Formula { get; set; }
        public List<object> AllowedValues { get; set; }
        public (double Min, double Max)? Range { get; set; }
        public string Unit { get; set; }
    }

    public class FamilyDimensions
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
        public double ClearWidth { get; set; }
        public double ClearHeight { get; set; }
        public double RoughOpeningWidth { get; set; }
        public double RoughOpeningHeight { get; set; }
    }

    public class ManufacturerInfo
    {
        public string Name { get; set; }
        public string ProductLine { get; set; }
        public string ModelNumber { get; set; }
        public string Website { get; set; }
        public string ContactEmail { get; set; }
        public string WarrantyInfo { get; set; }
        public string SpecificationUrl { get; set; }
        public string BIMObjectUrl { get; set; }
        public List<string> Certifications { get; set; } = new();
        public Dictionary<string, string> SustainabilityData { get; set; } = new();
    }

    public class FamilyMetadata
    {
        public List<string> Tags { get; set; } = new();
        public List<string> Certifications { get; set; } = new();
        public string OmniClassNumber { get; set; }
        public string UniFormatCode { get; set; }
        public string MasterFormatCode { get; set; }
        public string IFCExportType { get; set; }
        public string COBieType { get; set; }
        public int UsageCount { get; set; }
        public double Rating { get; set; }
        public List<string> ApplicableCodes { get; set; } = new();
    }

    public class FamilyLibrary
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string BasePath { get; set; }
        public List<FamilyDefinition> Families { get; set; } = new();
        public Dictionary<string, List<string>> CategoryIndex { get; set; } = new();
        public Dictionary<string, List<string>> TagIndex { get; set; } = new();
        public Dictionary<string, List<string>> ManufacturerIndex { get; set; } = new();
        public DateTime LastIndexed { get; set; }
        public int TotalFamilies => Families.Count;
        public int TotalTypes => Families.Sum(f => f.Types.Count);
    }

    public class FamilySearchCriteria
    {
        public string SearchText { get; set; }
        public List<FamilyCategory> Categories { get; set; }
        public List<string> Tags { get; set; }
        public string Manufacturer { get; set; }
        public FamilyDimensionFilter DimensionFilter { get; set; }
        public PlacementType? PlacementType { get; set; }
        public FamilySource? Source { get; set; }
        public double? MaxCost { get; set; }
        public bool? HasBIMObject { get; set; }
        public string OmniClassPrefix { get; set; }
        public int MaxResults { get; set; } = 50;
        public string SortBy { get; set; } = "Relevance";
    }

    public class FamilyDimensionFilter
    {
        public (double Min, double Max)? WidthRange { get; set; }
        public (double Min, double Max)? HeightRange { get; set; }
        public (double Min, double Max)? DepthRange { get; set; }
    }

    public class FamilySearchResult
    {
        public FamilyDefinition Family { get; set; }
        public FamilyType MatchedType { get; set; }
        public double RelevanceScore { get; set; }
        public List<string> MatchedTerms { get; set; } = new();
        public string MatchReason { get; set; }
    }

    public class PlacementRequest
    {
        public string FamilyId { get; set; }
        public string TypeId { get; set; }
        public Point3D Location { get; set; }
        public int? HostElementId { get; set; }
        public double Rotation { get; set; }
        public bool FlipFacing { get; set; }
        public bool FlipHand { get; set; }
        public int LevelId { get; set; }
        public Dictionary<string, object> InstanceParameters { get; set; } = new();
        public PlacementConstraints Constraints { get; set; }
    }

    public class PlacementConstraints
    {
        public double MinClearanceLeft { get; set; }
        public double MinClearanceRight { get; set; }
        public double MinClearanceFront { get; set; }
        public double MinClearanceBack { get; set; }
        public bool RequiresClearFloor { get; set; }
        public bool RequiresWallHost { get; set; }
        public List<string> RequiredNearbyElements { get; set; }
        public List<string> ProhibitedNearbyElements { get; set; }
    }

    public class PlacementResult
    {
        public bool Success { get; set; }
        public int ElementId { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public Point3D ActualLocation { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string Error { get; set; }
        public Dictionary<string, object> AppliedParameters { get; set; } = new();
    }

    public class TypeEditRequest
    {
        public string FamilyId { get; set; }
        public string TypeId { get; set; }
        public string NewTypeName { get; set; }
        public Dictionary<string, object> ParameterChanges { get; set; } = new();
        public bool CreateDuplicate { get; set; }
    }

    #endregion

    /// <summary>
    /// Comprehensive Family Management System for StingBIM.
    /// Handles library indexing, search, type management, and smart placement.
    /// </summary>
    public sealed class FamilyManagementSystem
    {
        private static readonly Lazy<FamilyManagementSystem> _instance =
            new Lazy<FamilyManagementSystem>(() => new FamilyManagementSystem());
        public static FamilyManagementSystem Instance => _instance.Value;

        private readonly Dictionary<string, FamilyLibrary> _libraries = new();
        private readonly Dictionary<string, FamilyDefinition> _loadedFamilies = new();
        private readonly Dictionary<string, ManufacturerDatabase> _manufacturerDatabases = new();
        private readonly object _lock = new object();

        public event EventHandler<FamilyEventArgs> FamilyLoaded;
        public event EventHandler<FamilyEventArgs> FamilyModified;
        public event EventHandler<PlacementEventArgs> ElementPlaced;

        private FamilyManagementSystem()
        {
            InitializeDefaultLibraries();
            InitializeManufacturerDatabases();
        }

        #region Library Management

        /// <summary>
        /// Creates and indexes a new family library from a folder path.
        /// </summary>
        public async Task<FamilyLibrary> CreateLibraryAsync(
            string name,
            string basePath,
            IProgress<IndexingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var library = new FamilyLibrary
            {
                Name = name,
                BasePath = basePath
            };

            // Simulate indexing (in real implementation, would scan folder)
            var families = await IndexFolderAsync(basePath, progress, cancellationToken);
            library.Families = families;

            // Build indices
            BuildIndices(library);

            library.LastIndexed = DateTime.UtcNow;

            lock (_lock) { _libraries[library.Id] = library; }

            return library;
        }

        /// <summary>
        /// Refreshes the index for an existing library.
        /// </summary>
        public async Task RefreshLibraryAsync(
            string libraryId,
            IProgress<IndexingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (!_libraries.TryGetValue(libraryId, out var library))
                    return;

                Task.Run(async () =>
                {
                    var families = await IndexFolderAsync(library.BasePath, progress, cancellationToken);
                    library.Families = families;
                    BuildIndices(library);
                    library.LastIndexed = DateTime.UtcNow;
                });
            }
        }

        private async Task<List<FamilyDefinition>> IndexFolderAsync(
            string basePath,
            IProgress<IndexingProgress> progress,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var families = new List<FamilyDefinition>();

                // Add sample families for demonstration
                families.AddRange(CreateSampleDoorFamilies());
                families.AddRange(CreateSampleWindowFamilies());
                families.AddRange(CreateSampleFurnitureFamilies());
                families.AddRange(CreateSamplePlumbingFamilies());
                families.AddRange(CreateSampleLightingFamilies());
                families.AddRange(CreateSampleMEPFamilies());

                progress?.Report(new IndexingProgress
                {
                    TotalFiles = families.Count,
                    ProcessedFiles = families.Count,
                    CurrentFile = "Complete",
                    IsComplete = true
                });

                return families;
            }, cancellationToken);
        }

        private void BuildIndices(FamilyLibrary library)
        {
            library.CategoryIndex.Clear();
            library.TagIndex.Clear();
            library.ManufacturerIndex.Clear();

            foreach (var family in library.Families)
            {
                // Category index
                var categoryKey = family.Category.ToString();
                if (!library.CategoryIndex.ContainsKey(categoryKey))
                    library.CategoryIndex[categoryKey] = new List<string>();
                library.CategoryIndex[categoryKey].Add(family.Id);

                // Tag index
                foreach (var tag in family.Metadata?.Tags ?? new List<string>())
                {
                    if (!library.TagIndex.ContainsKey(tag))
                        library.TagIndex[tag] = new List<string>();
                    library.TagIndex[tag].Add(family.Id);
                }

                // Manufacturer index
                if (family.Manufacturer != null)
                {
                    var mfr = family.Manufacturer.Name;
                    if (!library.ManufacturerIndex.ContainsKey(mfr))
                        library.ManufacturerIndex[mfr] = new List<string>();
                    library.ManufacturerIndex[mfr].Add(family.Id);
                }
            }
        }

        #endregion

        #region Family Search

        /// <summary>
        /// Searches families across all libraries based on criteria.
        /// </summary>
        public List<FamilySearchResult> Search(FamilySearchCriteria criteria)
        {
            var results = new List<FamilySearchResult>();

            lock (_lock)
            {
                var allFamilies = _libraries.Values.SelectMany(l => l.Families);

                foreach (var family in allFamilies)
                {
                    var score = CalculateRelevance(family, criteria);
                    if (score > 0)
                    {
                        // Find best matching type
                        var matchedType = FindBestMatchingType(family, criteria);

                        results.Add(new FamilySearchResult
                        {
                            Family = family,
                            MatchedType = matchedType,
                            RelevanceScore = score,
                            MatchedTerms = GetMatchedTerms(family, criteria),
                            MatchReason = DetermineMatchReason(family, criteria)
                        });
                    }
                }
            }

            // Sort and limit
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .Take(criteria.MaxResults)
                .ToList();

            return results;
        }

        /// <summary>
        /// Quick search by text query.
        /// </summary>
        public List<FamilySearchResult> QuickSearch(string query, int maxResults = 20)
        {
            return Search(new FamilySearchCriteria
            {
                SearchText = query,
                MaxResults = maxResults
            });
        }

        /// <summary>
        /// Finds families similar to a given family.
        /// </summary>
        public List<FamilySearchResult> FindSimilar(string familyId, int maxResults = 10)
        {
            FamilyDefinition sourceFamily = null;
            lock (_lock)
            {
                sourceFamily = _libraries.Values
                    .SelectMany(l => l.Families)
                    .FirstOrDefault(f => f.Id == familyId);
            }

            if (sourceFamily == null) return new List<FamilySearchResult>();

            var criteria = new FamilySearchCriteria
            {
                Categories = new List<FamilyCategory> { sourceFamily.Category },
                Tags = sourceFamily.Metadata?.Tags,
                DimensionFilter = new FamilyDimensionFilter
                {
                    WidthRange = (sourceFamily.Types.FirstOrDefault()?.Dimensions?.Width * 0.8 ?? 0,
                                  sourceFamily.Types.FirstOrDefault()?.Dimensions?.Width * 1.2 ?? 10000)
                },
                MaxResults = maxResults + 1
            };

            return Search(criteria).Where(r => r.Family.Id != familyId).Take(maxResults).ToList();
        }

        /// <summary>
        /// Recommends families based on room type and requirements.
        /// </summary>
        public List<FamilySearchResult> RecommendForRoom(string roomType, List<string> requirements = null)
        {
            var recommendations = new List<FamilySearchResult>();

            var roomRequirements = GetRoomRequirements(roomType);

            foreach (var category in roomRequirements.RequiredCategories)
            {
                var results = Search(new FamilySearchCriteria
                {
                    Categories = new List<FamilyCategory> { category },
                    Tags = requirements,
                    MaxResults = 5,
                    SortBy = "Rating"
                });

                recommendations.AddRange(results);
            }

            return recommendations
                .OrderByDescending(r => r.RelevanceScore)
                .ToList();
        }

        private double CalculateRelevance(FamilyDefinition family, FamilySearchCriteria criteria)
        {
            double score = 0;

            // Category match
            if (criteria.Categories?.Contains(family.Category) == true)
                score += 30;
            else if (criteria.Categories == null)
                score += 10;
            else
                return 0; // Category doesn't match

            // Text search
            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                var searchLower = criteria.SearchText.ToLower();
                if (family.Name.ToLower().Contains(searchLower)) score += 40;
                if (family.Metadata?.Tags?.Any(t => t.ToLower().Contains(searchLower)) == true) score += 20;
                if (family.Manufacturer?.Name?.ToLower().Contains(searchLower) == true) score += 15;
                if (family.Types.Any(t => t.Name.ToLower().Contains(searchLower))) score += 25;
            }
            else
            {
                score += 20; // No text filter
            }

            // Tag match
            if (criteria.Tags?.Any() == true)
            {
                var matches = criteria.Tags.Count(t => family.Metadata?.Tags?.Contains(t) == true);
                score += matches * 10;
            }

            // Manufacturer match
            if (!string.IsNullOrEmpty(criteria.Manufacturer) &&
                family.Manufacturer?.Name?.Equals(criteria.Manufacturer, StringComparison.OrdinalIgnoreCase) == true)
                score += 25;

            // Dimension filter
            if (criteria.DimensionFilter != null)
            {
                var dims = family.Types.FirstOrDefault()?.Dimensions;
                if (dims != null)
                {
                    if (criteria.DimensionFilter.WidthRange.HasValue)
                    {
                        if (dims.Width >= criteria.DimensionFilter.WidthRange.Value.Min &&
                            dims.Width <= criteria.DimensionFilter.WidthRange.Value.Max)
                            score += 15;
                        else
                            score -= 10;
                    }
                }
            }

            // Cost filter
            if (criteria.MaxCost.HasValue)
            {
                var minTypeCost = family.Types.Min(t => t.Cost);
                if (minTypeCost <= criteria.MaxCost.Value)
                    score += 10;
            }

            // Source preference
            if (criteria.Source.HasValue && family.Source == criteria.Source.Value)
                score += 10;

            // Rating boost
            score += (family.Metadata?.Rating ?? 0) * 5;

            return score;
        }

        private FamilyType FindBestMatchingType(FamilyDefinition family, FamilySearchCriteria criteria)
        {
            if (!family.Types.Any()) return null;

            // If dimension filter, find closest match
            if (criteria.DimensionFilter?.WidthRange != null)
            {
                double targetWidth = (criteria.DimensionFilter.WidthRange.Value.Min +
                                     criteria.DimensionFilter.WidthRange.Value.Max) / 2;
                return family.Types
                    .Where(t => t.Dimensions != null)
                    .OrderBy(t => Math.Abs(t.Dimensions.Width - targetWidth))
                    .FirstOrDefault() ?? family.Types.First();
            }

            // Return default type
            return family.Types.FirstOrDefault(t => t.IsDefault) ?? family.Types.First();
        }

        private List<string> GetMatchedTerms(FamilyDefinition family, FamilySearchCriteria criteria)
        {
            var matched = new List<string>();

            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                var searchLower = criteria.SearchText.ToLower();
                if (family.Name.ToLower().Contains(searchLower)) matched.Add($"Name: {family.Name}");
                matched.AddRange(family.Metadata?.Tags?.Where(t => t.ToLower().Contains(searchLower)) ?? Enumerable.Empty<string>());
            }

            return matched;
        }

        private string DetermineMatchReason(FamilyDefinition family, FamilySearchCriteria criteria)
        {
            var reasons = new List<string>();

            if (!string.IsNullOrEmpty(criteria.SearchText) && family.Name.ToLower().Contains(criteria.SearchText.ToLower()))
                reasons.Add("Name match");

            if (criteria.Categories?.Contains(family.Category) == true)
                reasons.Add("Category match");

            if (family.Metadata?.Rating >= 4)
                reasons.Add("Highly rated");

            return string.Join(", ", reasons);
        }

        #endregion

        #region Family Loading and Type Management

        /// <summary>
        /// Loads a family into the current project.
        /// </summary>
        public async Task<bool> LoadFamilyAsync(string familyId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                FamilyDefinition family = null;
                lock (_lock)
                {
                    family = _libraries.Values
                        .SelectMany(l => l.Families)
                        .FirstOrDefault(f => f.Id == familyId);
                }

                if (family == null) return false;

                // In real implementation, would use Revit API to load
                family.Status = FamilyStatus.Loaded;
                family.LoadedDate = DateTime.UtcNow;

                lock (_lock) { _loadedFamilies[familyId] = family; }

                FamilyLoaded?.Invoke(this, new FamilyEventArgs { Family = family });
                return true;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets all loaded families in the current project.
        /// </summary>
        public List<FamilyDefinition> GetLoadedFamilies()
        {
            lock (_lock)
            {
                return _loadedFamilies.Values.ToList();
            }
        }

        /// <summary>
        /// Gets all types for a family.
        /// </summary>
        public List<FamilyType> GetFamilyTypes(string familyId)
        {
            lock (_lock)
            {
                var family = _libraries.Values
                    .SelectMany(l => l.Families)
                    .FirstOrDefault(f => f.Id == familyId);
                return family?.Types ?? new List<FamilyType>();
            }
        }

        /// <summary>
        /// Creates a new type by duplicating and modifying an existing type.
        /// </summary>
        public FamilyType CreateType(TypeEditRequest request)
        {
            lock (_lock)
            {
                var family = _libraries.Values
                    .SelectMany(l => l.Families)
                    .FirstOrDefault(f => f.Id == request.FamilyId);

                if (family == null) return null;

                var sourceType = family.Types.FirstOrDefault(t => t.Id == request.TypeId);
                if (sourceType == null) return null;

                var newType = new FamilyType
                {
                    Name = request.NewTypeName ?? $"{sourceType.Name}_Copy",
                    FamilyId = family.Id,
                    ParameterValues = new Dictionary<string, object>(sourceType.ParameterValues),
                    Dimensions = new FamilyDimensions
                    {
                        Width = sourceType.Dimensions?.Width ?? 0,
                        Height = sourceType.Dimensions?.Height ?? 0,
                        Depth = sourceType.Dimensions?.Depth ?? 0
                    }
                };

                // Apply parameter changes
                foreach (var (key, value) in request.ParameterChanges)
                {
                    newType.ParameterValues[key] = value;

                    // Update dimensions if dimension parameter
                    if (key.ToLower().Contains("width")) newType.Dimensions.Width = Convert.ToDouble(value);
                    if (key.ToLower().Contains("height")) newType.Dimensions.Height = Convert.ToDouble(value);
                    if (key.ToLower().Contains("depth")) newType.Dimensions.Depth = Convert.ToDouble(value);
                }

                family.Types.Add(newType);

                FamilyModified?.Invoke(this, new FamilyEventArgs { Family = family, Type = newType });

                return newType;
            }
        }

        /// <summary>
        /// Modifies an existing type's parameters.
        /// </summary>
        public bool ModifyType(TypeEditRequest request)
        {
            lock (_lock)
            {
                var family = _libraries.Values
                    .SelectMany(l => l.Families)
                    .FirstOrDefault(f => f.Id == request.FamilyId);

                if (family == null) return false;

                var type = family.Types.FirstOrDefault(t => t.Id == request.TypeId);
                if (type == null) return false;

                foreach (var (key, value) in request.ParameterChanges)
                {
                    type.ParameterValues[key] = value;
                }

                FamilyModified?.Invoke(this, new FamilyEventArgs { Family = family, Type = type });
                return true;
            }
        }

        #endregion

        #region Smart Placement

        /// <summary>
        /// Places a family instance with smart constraint checking.
        /// </summary>
        public async Task<PlacementResult> PlaceInstanceAsync(
            PlacementRequest request,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new PlacementResult();

                // Find family and type
                FamilyDefinition family = null;
                FamilyType type = null;

                lock (_lock)
                {
                    family = _loadedFamilies.GetValueOrDefault(request.FamilyId);
                    if (family == null)
                    {
                        family = _libraries.Values
                            .SelectMany(l => l.Families)
                            .FirstOrDefault(f => f.Id == request.FamilyId);
                    }
                }

                if (family == null)
                {
                    result.Success = false;
                    result.Error = "Family not found";
                    return result;
                }

                type = family.Types.FirstOrDefault(t => t.Id == request.TypeId)
                       ?? family.Types.FirstOrDefault();

                if (type == null)
                {
                    result.Success = false;
                    result.Error = "No types available for family";
                    return result;
                }

                // Validate constraints
                var constraintResult = ValidateConstraints(request, family, type);
                if (!constraintResult.IsValid)
                {
                    result.Success = false;
                    result.Error = constraintResult.Error;
                    result.Warnings = constraintResult.Warnings;
                    return result;
                }

                // Check host requirement
                if (family.PlacementType == PlacementType.HostBased && !request.HostElementId.HasValue)
                {
                    result.Success = false;
                    result.Error = "Host element required for this family";
                    return result;
                }

                // Simulate placement
                result.Success = true;
                result.ElementId = new Random().Next(100000, 999999);
                result.FamilyName = family.Name;
                result.TypeName = type.Name;
                result.ActualLocation = request.Location;
                result.Warnings = constraintResult.Warnings;

                // Apply instance parameters
                result.AppliedParameters = new Dictionary<string, object>(request.InstanceParameters);

                ElementPlaced?.Invoke(this, new PlacementEventArgs
                {
                    Family = family,
                    Type = type,
                    Result = result
                });

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Places multiple instances with automatic spacing.
        /// </summary>
        public async Task<List<PlacementResult>> PlaceArrayAsync(
            PlacementRequest baseRequest,
            int countX,
            int countY,
            double spacingX,
            double spacingY,
            CancellationToken cancellationToken = default)
        {
            var results = new List<PlacementResult>();

            for (int y = 0; y < countY; y++)
            {
                for (int x = 0; x < countX; x++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var request = new PlacementRequest
                    {
                        FamilyId = baseRequest.FamilyId,
                        TypeId = baseRequest.TypeId,
                        Location = new Point3D(
                            baseRequest.Location.X + x * spacingX,
                            baseRequest.Location.Y + y * spacingY,
                            baseRequest.Location.Z
                        ),
                        HostElementId = baseRequest.HostElementId,
                        Rotation = baseRequest.Rotation,
                        LevelId = baseRequest.LevelId,
                        InstanceParameters = baseRequest.InstanceParameters
                    };

                    var result = await PlaceInstanceAsync(request, cancellationToken);
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Suggests optimal placement location based on context.
        /// </summary>
        public Point3D SuggestPlacement(
            string familyId,
            int roomId,
            PlacementContext context)
        {
            FamilyDefinition family = null;
            lock (_lock)
            {
                family = _libraries.Values
                    .SelectMany(l => l.Families)
                    .FirstOrDefault(f => f.Id == familyId);
            }

            if (family == null) return context.RoomCenter;

            // Different placement strategies based on family type
            return family.Category switch
            {
                FamilyCategory.Furniture when context.RoomType.Contains("office") =>
                    CalculateOfficeDesksPlacement(family, context),
                FamilyCategory.Lighting =>
                    CalculateCenteredPlacement(context),
                FamilyCategory.Plumbing when context.RoomType.Contains("bathroom") =>
                    CalculateBathroomFixturePlacement(family, context),
                _ => context.RoomCenter
            };
        }

        private ConstraintValidationResult ValidateConstraints(
            PlacementRequest request,
            FamilyDefinition family,
            FamilyType type)
        {
            var result = new ConstraintValidationResult { IsValid = true, Warnings = new List<string>() };

            if (request.Constraints == null) return result;

            // Check clearances (would use actual room geometry)
            if (request.Constraints.RequiresClearFloor)
            {
                // Validate floor area clear
                result.Warnings.Add("Clear floor requirement noted - verify placement");
            }

            if (request.Constraints.RequiresWallHost && !request.HostElementId.HasValue)
            {
                result.IsValid = false;
                result.Error = "Wall host required";
            }

            return result;
        }

        private Point3D CalculateOfficeDesksPlacement(FamilyDefinition family, PlacementContext context)
        {
            // Place near window for daylight
            return new Point3D(
                context.RoomCenter.X,
                context.RoomCenter.Y - context.RoomDepth * 0.3,
                context.RoomCenter.Z
            );
        }

        private Point3D CalculateCenteredPlacement(PlacementContext context)
        {
            return context.RoomCenter;
        }

        private Point3D CalculateBathroomFixturePlacement(FamilyDefinition family, PlacementContext context)
        {
            // Toilets typically against back wall
            if (family.Name.ToLower().Contains("toilet"))
            {
                return new Point3D(
                    context.RoomCenter.X,
                    context.RoomCenter.Y + context.RoomDepth * 0.4,
                    context.RoomCenter.Z
                );
            }

            return context.RoomCenter;
        }

        #endregion

        #region Manufacturer Database

        /// <summary>
        /// Searches manufacturer database for products.
        /// </summary>
        public List<ManufacturerProduct> SearchManufacturerProducts(
            string query,
            string manufacturer = null,
            FamilyCategory? category = null)
        {
            var results = new List<ManufacturerProduct>();

            lock (_lock)
            {
                foreach (var db in _manufacturerDatabases.Values)
                {
                    if (manufacturer != null && !db.Name.Equals(manufacturer, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var matches = db.Products.Where(p =>
                        (category == null || p.Category == category) &&
                        (string.IsNullOrEmpty(query) ||
                         p.Name.ToLower().Contains(query.ToLower()) ||
                         p.ModelNumber.ToLower().Contains(query.ToLower()))
                    );

                    results.AddRange(matches);
                }
            }

            return results.Take(50).ToList();
        }

        /// <summary>
        /// Downloads and loads a family from manufacturer's BIM library.
        /// </summary>
        public async Task<FamilyDefinition> LoadFromManufacturerAsync(
            string manufacturerProductId,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                // Simulate downloading from manufacturer BIM library
                ManufacturerProduct product = null;
                lock (_lock)
                {
                    product = _manufacturerDatabases.Values
                        .SelectMany(db => db.Products)
                        .FirstOrDefault(p => p.Id == manufacturerProductId);
                }

                if (product == null) return null;

                // Create family from manufacturer data
                var family = new FamilyDefinition
                {
                    Name = product.Name,
                    Category = product.Category,
                    Source = FamilySource.Manufacturer,
                    Status = FamilyStatus.Loaded,
                    Manufacturer = new ManufacturerInfo
                    {
                        Name = product.ManufacturerName,
                        ModelNumber = product.ModelNumber,
                        Website = product.Website,
                        BIMObjectUrl = product.BIMObjectUrl
                    },
                    Types = new List<FamilyType>
                    {
                        new FamilyType
                        {
                            Name = product.ModelNumber,
                            Dimensions = product.Dimensions,
                            Cost = product.ListPrice,
                            IsDefault = true
                        }
                    },
                    Metadata = new FamilyMetadata
                    {
                        Tags = product.Tags,
                        Certifications = product.Certifications
                    },
                    LoadedDate = DateTime.UtcNow
                };

                lock (_lock) { _loadedFamilies[family.Id] = family; }

                return family;
            }, cancellationToken);
        }

        #endregion

        #region Initialization Helpers

        private void InitializeDefaultLibraries()
        {
            var defaultLibrary = new FamilyLibrary
            {
                Name = "StingBIM Default Library",
                BasePath = "C:/ProgramData/StingBIM/Families"
            };

            defaultLibrary.Families.AddRange(CreateSampleDoorFamilies());
            defaultLibrary.Families.AddRange(CreateSampleWindowFamilies());
            defaultLibrary.Families.AddRange(CreateSampleFurnitureFamilies());
            defaultLibrary.Families.AddRange(CreateSamplePlumbingFamilies());
            defaultLibrary.Families.AddRange(CreateSampleLightingFamilies());
            defaultLibrary.Families.AddRange(CreateSampleMEPFamilies());

            BuildIndices(defaultLibrary);
            defaultLibrary.LastIndexed = DateTime.UtcNow;

            _libraries[defaultLibrary.Id] = defaultLibrary;
        }

        private void InitializeManufacturerDatabases()
        {
            // Sample manufacturer databases
            _manufacturerDatabases["MANUFACTURER_1"] = new ManufacturerDatabase
            {
                Name = "Kohler",
                Category = "Plumbing",
                Products = new List<ManufacturerProduct>
                {
                    new ManufacturerProduct { Id = "KOH001", Name = "Cimarron Toilet", ModelNumber = "K-3609", Category = FamilyCategory.Plumbing, ManufacturerName = "Kohler", ListPrice = 450 },
                    new ManufacturerProduct { Id = "KOH002", Name = "Memoirs Lavatory", ModelNumber = "K-2241", Category = FamilyCategory.Plumbing, ManufacturerName = "Kohler", ListPrice = 380 }
                }
            };

            _manufacturerDatabases["MANUFACTURER_2"] = new ManufacturerDatabase
            {
                Name = "Steelcase",
                Category = "Furniture",
                Products = new List<ManufacturerProduct>
                {
                    new ManufacturerProduct { Id = "SC001", Name = "Think Chair", ModelNumber = "465A300", Category = FamilyCategory.Furniture, ManufacturerName = "Steelcase", ListPrice = 1200 },
                    new ManufacturerProduct { Id = "SC002", Name = "Series 1 Chair", ModelNumber = "435A00", Category = FamilyCategory.Furniture, ManufacturerName = "Steelcase", ListPrice = 450 }
                }
            };
        }

        private List<FamilyDefinition> CreateSampleDoorFamilies()
        {
            return new List<FamilyDefinition>
            {
                new FamilyDefinition
                {
                    Name = "Single Flush Door",
                    Category = FamilyCategory.Doors,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.HostBased,
                    HostCategory = "Walls",
                    Metadata = new FamilyMetadata { Tags = new List<string> { "door", "interior", "flush", "single" }, Rating = 4.5 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "762 x 2032mm", Dimensions = new FamilyDimensions { Width = 762, Height = 2032 }, IsDefault = true },
                        new FamilyType { Name = "838 x 2032mm", Dimensions = new FamilyDimensions { Width = 838, Height = 2032 } },
                        new FamilyType { Name = "914 x 2032mm", Dimensions = new FamilyDimensions { Width = 914, Height = 2032 } }
                    }
                },
                new FamilyDefinition
                {
                    Name = "Double Door",
                    Category = FamilyCategory.Doors,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.HostBased,
                    HostCategory = "Walls",
                    Metadata = new FamilyMetadata { Tags = new List<string> { "door", "double", "entry" }, Rating = 4.2 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "1524 x 2134mm", Dimensions = new FamilyDimensions { Width = 1524, Height = 2134 }, IsDefault = true },
                        new FamilyType { Name = "1829 x 2134mm", Dimensions = new FamilyDimensions { Width = 1829, Height = 2134 } }
                    }
                }
            };
        }

        private List<FamilyDefinition> CreateSampleWindowFamilies()
        {
            return new List<FamilyDefinition>
            {
                new FamilyDefinition
                {
                    Name = "Fixed Window",
                    Category = FamilyCategory.Windows,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.HostBased,
                    HostCategory = "Walls",
                    Metadata = new FamilyMetadata { Tags = new List<string> { "window", "fixed", "glazing" }, Rating = 4.0 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "1200 x 1500mm", Dimensions = new FamilyDimensions { Width = 1200, Height = 1500 }, IsDefault = true },
                        new FamilyType { Name = "1800 x 1500mm", Dimensions = new FamilyDimensions { Width = 1800, Height = 1500 } }
                    }
                },
                new FamilyDefinition
                {
                    Name = "Casement Window",
                    Category = FamilyCategory.Windows,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.HostBased,
                    HostCategory = "Walls",
                    Metadata = new FamilyMetadata { Tags = new List<string> { "window", "casement", "operable" }, Rating = 4.3 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "600 x 1200mm", Dimensions = new FamilyDimensions { Width = 600, Height = 1200 }, IsDefault = true },
                        new FamilyType { Name = "900 x 1500mm", Dimensions = new FamilyDimensions { Width = 900, Height = 1500 } }
                    }
                }
            };
        }

        private List<FamilyDefinition> CreateSampleFurnitureFamilies()
        {
            return new List<FamilyDefinition>
            {
                new FamilyDefinition
                {
                    Name = "Office Desk",
                    Category = FamilyCategory.Furniture,
                    Source = FamilySource.External,
                    PlacementType = PlacementType.FreeStanding,
                    Metadata = new FamilyMetadata { Tags = new List<string> { "desk", "office", "workstation" }, Rating = 4.5 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "1500 x 750mm", Dimensions = new FamilyDimensions { Width = 1500, Depth = 750, Height = 720 }, IsDefault = true, Cost = 450 },
                        new FamilyType { Name = "1800 x 800mm", Dimensions = new FamilyDimensions { Width = 1800, Depth = 800, Height = 720 }, Cost = 550 }
                    }
                },
                new FamilyDefinition
                {
                    Name = "Task Chair",
                    Category = FamilyCategory.Furniture,
                    Source = FamilySource.Manufacturer,
                    PlacementType = PlacementType.FreeStanding,
                    Manufacturer = new ManufacturerInfo { Name = "Steelcase", ProductLine = "Think" },
                    Metadata = new FamilyMetadata { Tags = new List<string> { "chair", "office", "ergonomic" }, Rating = 4.8 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "Standard", Dimensions = new FamilyDimensions { Width = 680, Depth = 680, Height = 1100 }, IsDefault = true, Cost = 850 }
                    }
                }
            };
        }

        private List<FamilyDefinition> CreateSamplePlumbingFamilies()
        {
            return new List<FamilyDefinition>
            {
                new FamilyDefinition
                {
                    Name = "Wall-Hung Toilet",
                    Category = FamilyCategory.Plumbing,
                    Source = FamilySource.Manufacturer,
                    PlacementType = PlacementType.HostBased,
                    HostCategory = "Walls",
                    Manufacturer = new ManufacturerInfo { Name = "Kohler", ModelNumber = "K-3609" },
                    Metadata = new FamilyMetadata { Tags = new List<string> { "toilet", "wc", "bathroom", "wall-hung" }, Rating = 4.6 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "Standard", Dimensions = new FamilyDimensions { Width = 355, Depth = 540, Height = 400 }, IsDefault = true, Cost = 650 }
                    }
                },
                new FamilyDefinition
                {
                    Name = "Pedestal Lavatory",
                    Category = FamilyCategory.Plumbing,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.FreeStanding,
                    Metadata = new FamilyMetadata { Tags = new List<string> { "sink", "lavatory", "bathroom", "pedestal" }, Rating = 4.2 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "500mm", Dimensions = new FamilyDimensions { Width = 500, Depth = 450, Height = 850 }, IsDefault = true, Cost = 320 },
                        new FamilyType { Name = "600mm", Dimensions = new FamilyDimensions { Width = 600, Depth = 500, Height = 850 }, Cost = 380 }
                    }
                }
            };
        }

        private List<FamilyDefinition> CreateSampleLightingFamilies()
        {
            return new List<FamilyDefinition>
            {
                new FamilyDefinition
                {
                    Name = "2x4 LED Troffer",
                    Category = FamilyCategory.Lighting,
                    Source = FamilySource.External,
                    PlacementType = PlacementType.FaceBased,
                    Metadata = new FamilyMetadata { Tags = new List<string> { "light", "led", "ceiling", "troffer", "office" }, Rating = 4.4 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "40W 4000K", Dimensions = new FamilyDimensions { Width = 1200, Depth = 600, Height = 100 }, IsDefault = true, Cost = 180 },
                        new FamilyType { Name = "50W 5000K", Dimensions = new FamilyDimensions { Width = 1200, Depth = 600, Height = 100 }, Cost = 210 }
                    }
                },
                new FamilyDefinition
                {
                    Name = "Downlight",
                    Category = FamilyCategory.Lighting,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.FaceBased,
                    Metadata = new FamilyMetadata { Tags = new List<string> { "light", "recessed", "downlight" }, Rating = 4.1 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "100mm 10W", Dimensions = new FamilyDimensions { Width = 100, Depth = 100, Height = 120 }, IsDefault = true, Cost = 45 },
                        new FamilyType { Name = "150mm 15W", Dimensions = new FamilyDimensions { Width = 150, Depth = 150, Height = 140 }, Cost = 65 }
                    }
                }
            };
        }

        private List<FamilyDefinition> CreateSampleMEPFamilies()
        {
            return new List<FamilyDefinition>
            {
                new FamilyDefinition
                {
                    Name = "Supply Air Diffuser",
                    Category = FamilyCategory.Mechanical,
                    Source = FamilySource.External,
                    PlacementType = PlacementType.FaceBased,
                    Metadata = new FamilyMetadata { Tags = new List<string> { "hvac", "diffuser", "air", "supply", "ceiling" }, Rating = 4.3 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "300x300 4-Way", Dimensions = new FamilyDimensions { Width = 300, Depth = 300, Height = 150 }, IsDefault = true, Cost = 85 },
                        new FamilyType { Name = "600x600 4-Way", Dimensions = new FamilyDimensions { Width = 600, Depth = 600, Height = 200 }, Cost = 125 }
                    }
                },
                new FamilyDefinition
                {
                    Name = "Electrical Outlet",
                    Category = FamilyCategory.Electrical,
                    Source = FamilySource.System,
                    PlacementType = PlacementType.FaceBased,
                    Metadata = new FamilyMetadata { Tags = new List<string> { "outlet", "receptacle", "electrical", "power" }, Rating = 4.0 },
                    Types = new List<FamilyType>
                    {
                        new FamilyType { Name = "Duplex 15A", Dimensions = new FamilyDimensions { Width = 70, Depth = 30, Height = 115 }, IsDefault = true, Cost = 15 },
                        new FamilyType { Name = "GFCI 20A", Dimensions = new FamilyDimensions { Width = 70, Depth = 40, Height = 115 }, Cost = 35 }
                    }
                }
            };
        }

        private RoomRequirements GetRoomRequirements(string roomType)
        {
            return roomType?.ToLower() switch
            {
                "office" => new RoomRequirements
                {
                    RequiredCategories = new List<FamilyCategory> { FamilyCategory.Furniture, FamilyCategory.Lighting, FamilyCategory.Electrical }
                },
                "bathroom" => new RoomRequirements
                {
                    RequiredCategories = new List<FamilyCategory> { FamilyCategory.Plumbing, FamilyCategory.Lighting, FamilyCategory.Electrical }
                },
                "conference" => new RoomRequirements
                {
                    RequiredCategories = new List<FamilyCategory> { FamilyCategory.Furniture, FamilyCategory.Lighting, FamilyCategory.Electrical }
                },
                _ => new RoomRequirements
                {
                    RequiredCategories = new List<FamilyCategory> { FamilyCategory.Lighting }
                }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class IndexingProgress
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentFile { get; set; }
        public bool IsComplete { get; set; }
    }

    public class ConstraintValidationResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class PlacementContext
    {
        public Point3D RoomCenter { get; set; }
        public double RoomWidth { get; set; }
        public double RoomDepth { get; set; }
        public string RoomType { get; set; }
        public List<int> ExistingElements { get; set; }
    }

    public class ManufacturerDatabase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Category { get; set; }
        public List<ManufacturerProduct> Products { get; set; } = new();
    }

    public class ManufacturerProduct
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ModelNumber { get; set; }
        public FamilyCategory Category { get; set; }
        public string ManufacturerName { get; set; }
        public double ListPrice { get; set; }
        public string Website { get; set; }
        public string BIMObjectUrl { get; set; }
        public FamilyDimensions Dimensions { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Certifications { get; set; } = new();
    }

    public class RoomRequirements
    {
        public List<FamilyCategory> RequiredCategories { get; set; } = new();
    }

    public class FamilyEventArgs : EventArgs
    {
        public FamilyDefinition Family { get; set; }
        public FamilyType Type { get; set; }
    }

    public class PlacementEventArgs : EventArgs
    {
        public FamilyDefinition Family { get; set; }
        public FamilyType Type { get; set; }
        public PlacementResult Result { get; set; }
    }

    #endregion
}
