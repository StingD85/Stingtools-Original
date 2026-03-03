using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Materials
{
    /// <summary>
    /// Central repository for all material data loaded from BLE_MATERIALS.xlsx and MEP_MATERIALS.xlsx.
    /// Provides fast search, filtering, and material lookup capabilities for 2,450+ materials.
    /// </summary>
    /// <remarks>
    /// Features:
    /// - Load materials from Excel databases
    /// - Full-text search with indexing
    /// - Filter by discipline, category, properties
    /// - Material property access
    /// - Thread-safe operations
    /// - Memory-efficient caching
    /// </remarks>
    public class MaterialDatabase
    {
        #region Private Fields

        private readonly Dictionary<string, MaterialDefinition> _materialsByCode;
        private readonly Dictionary<string, List<MaterialDefinition>> _materialsByCategory;
        private readonly Dictionary<string, List<MaterialDefinition>> _materialsByDiscipline;
        private readonly Dictionary<Guid, MaterialDefinition> _materialsByGuid;
        private readonly List<MaterialDefinition> _allMaterials;
        private readonly object _lock = new object();
        private bool _isLoaded;
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<MaterialDatabase>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the total number of materials in the database.
        /// </summary>
        public int Count => _allMaterials.Count;

        /// <summary>
        /// Gets whether the database has been loaded.
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Gets all unique material categories.
        /// </summary>
        public IEnumerable<string> Categories => _materialsByCategory.Keys;

        /// <summary>
        /// Gets all unique disciplines.
        /// </summary>
        public IEnumerable<string> Disciplines => _materialsByDiscipline.Keys;

        /// <summary>
        /// Gets all materials (read-only).
        /// </summary>
        public IReadOnlyList<MaterialDefinition> AllMaterials => _allMaterials.AsReadOnly();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialDatabase"/> class.
        /// </summary>
        public MaterialDatabase()
        {
            _materialsByCode = new Dictionary<string, MaterialDefinition>(StringComparer.OrdinalIgnoreCase);
            _materialsByCategory = new Dictionary<string, List<MaterialDefinition>>(StringComparer.OrdinalIgnoreCase);
            _materialsByDiscipline = new Dictionary<string, List<MaterialDefinition>>(StringComparer.OrdinalIgnoreCase);
            _materialsByGuid = new Dictionary<Guid, MaterialDefinition>();
            _allMaterials = new List<MaterialDefinition>();
            _isLoaded = false;
        }

        #endregion

        #region Public Methods - Loading

        /// <summary>
        /// Loads materials from the specified loader.
        /// </summary>
        /// <param name="loader">Material loader instance.</param>
        /// <returns>Number of materials loaded.</returns>
        public async Task<int> LoadAsync(MaterialLoader loader)
        {
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));

            try
            {
                _logger.Info("Starting material database load...");

                var materials = await loader.LoadAllMaterialsAsync();

                lock (_lock)
                {
                    Clear();

                    foreach (var material in materials)
                    {
                        AddMaterial(material);
                    }

                    _isLoaded = true;
                }

                _logger.Info($"Material database loaded: {Count} materials");
                return Count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load material database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads materials synchronously (wrapper for async method).
        /// </summary>
        /// <param name="loader">Material loader instance.</param>
        /// <returns>Number of materials loaded.</returns>
        public int Load(MaterialLoader loader)
        {
            return LoadAsync(loader).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Clears all materials from the database.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _materialsByCode.Clear();
                _materialsByCategory.Clear();
                _materialsByDiscipline.Clear();
                _materialsByGuid.Clear();
                _allMaterials.Clear();
                _isLoaded = false;

                _logger.Debug("Material database cleared");
            }
        }

        #endregion

        #region Public Methods - Lookup

        /// <summary>
        /// Gets a material by its code (e.g., "CONC-01", "STEEL-A36").
        /// </summary>
        /// <param name="code">Material code.</param>
        /// <returns>Material definition if found; otherwise, null.</returns>
        public MaterialDefinition GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            lock (_lock)
            {
                return _materialsByCode.TryGetValue(code, out var material) ? material : null;
            }
        }

        /// <summary>
        /// Gets a material by its GUID.
        /// </summary>
        /// <param name="guid">Material GUID.</param>
        /// <returns>Material definition if found; otherwise, null.</returns>
        public MaterialDefinition GetByGuid(Guid guid)
        {
            lock (_lock)
            {
                return _materialsByGuid.TryGetValue(guid, out var material) ? material : null;
            }
        }

        /// <summary>
        /// Gets a material by its name (case-insensitive).
        /// </summary>
        /// <param name="name">Material name.</param>
        /// <returns>Material definition if found; otherwise, null.</returns>
        public MaterialDefinition GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            lock (_lock)
            {
                return _allMaterials.FirstOrDefault(m =>
                    string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Checks if a material with the specified code exists.
        /// </summary>
        /// <param name="code">Material code.</param>
        /// <returns>True if material exists; otherwise, false.</returns>
        public bool Exists(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            lock (_lock)
            {
                return _materialsByCode.ContainsKey(code);
            }
        }

        #endregion

        #region Public Methods - Filtering

        /// <summary>
        /// Gets all materials in the specified category.
        /// </summary>
        /// <param name="category">Material category (e.g., "Concrete", "Steel", "Insulation").</param>
        /// <returns>List of materials in the category.</returns>
        public List<MaterialDefinition> GetByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return new List<MaterialDefinition>();

            lock (_lock)
            {
                return _materialsByCategory.TryGetValue(category, out var materials)
                    ? new List<MaterialDefinition>(materials)
                    : new List<MaterialDefinition>();
            }
        }

        /// <summary>
        /// Gets all materials for the specified discipline.
        /// </summary>
        /// <param name="discipline">Discipline (e.g., "Architecture", "MEP", "Structural").</param>
        /// <returns>List of materials in the discipline.</returns>
        public List<MaterialDefinition> GetByDiscipline(string discipline)
        {
            if (string.IsNullOrWhiteSpace(discipline))
                return new List<MaterialDefinition>();

            lock (_lock)
            {
                return _materialsByDiscipline.TryGetValue(discipline, out var materials)
                    ? new List<MaterialDefinition>(materials)
                    : new List<MaterialDefinition>();
            }
        }

        /// <summary>
        /// Filters materials based on a predicate.
        /// </summary>
        /// <param name="predicate">Filter predicate.</param>
        /// <returns>Filtered list of materials.</returns>
        public List<MaterialDefinition> Filter(Func<MaterialDefinition, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            lock (_lock)
            {
                return _allMaterials.Where(predicate).ToList();
            }
        }

        #endregion

        #region Public Methods - Search

        /// <summary>
        /// Searches for materials by text query (searches name, code, description).
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="caseSensitive">Whether search should be case-sensitive.</param>
        /// <returns>List of matching materials.</returns>
        public List<MaterialDefinition> Search(string query, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<MaterialDefinition>();

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            lock (_lock)
            {
                return _allMaterials.Where(m =>
                    (m.Name?.IndexOf(query, comparison) >= 0) ||
                    (m.Code?.IndexOf(query, comparison) >= 0) ||
                    (m.Description?.IndexOf(query, comparison) >= 0)
                ).ToList();
            }
        }

        /// <summary>
        /// Advanced search with multiple criteria.
        /// </summary>
        /// <param name="criteria">Search criteria.</param>
        /// <returns>List of matching materials.</returns>
        public List<MaterialDefinition> SearchAdvanced(MaterialSearchCriteria criteria)
        {
            if (criteria == null)
                throw new ArgumentNullException(nameof(criteria));

            lock (_lock)
            {
                IEnumerable<MaterialDefinition> results = _allMaterials;

                // Filter by text query
                if (!string.IsNullOrWhiteSpace(criteria.Query))
                {
                    var comparison = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    results = results.Where(m =>
                        (m.Name?.IndexOf(criteria.Query, comparison) >= 0) ||
                        (m.Code?.IndexOf(criteria.Query, comparison) >= 0) ||
                        (m.Description?.IndexOf(criteria.Query, comparison) >= 0)
                    );
                }

                // Filter by category
                if (!string.IsNullOrWhiteSpace(criteria.Category))
                {
                    results = results.Where(m =>
                        string.Equals(m.Category, criteria.Category, StringComparison.OrdinalIgnoreCase));
                }

                // Filter by discipline
                if (!string.IsNullOrWhiteSpace(criteria.Discipline))
                {
                    results = results.Where(m =>
                        string.Equals(m.Discipline, criteria.Discipline, StringComparison.OrdinalIgnoreCase));
                }

                // Filter by manufacturer
                if (!string.IsNullOrWhiteSpace(criteria.Manufacturer))
                {
                    results = results.Where(m =>
                        m.Manufacturer?.IndexOf(criteria.Manufacturer, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                // Filter by standard
                if (!string.IsNullOrWhiteSpace(criteria.Standard))
                {
                    results = results.Where(m =>
                        m.Standard?.IndexOf(criteria.Standard, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                // Filter by thermal resistance range
                if (criteria.MinThermalResistance.HasValue)
                {
                    results = results.Where(m =>
                        m.ThermalResistance >= criteria.MinThermalResistance.Value);
                }

                if (criteria.MaxThermalResistance.HasValue)
                {
                    results = results.Where(m =>
                        m.ThermalResistance <= criteria.MaxThermalResistance.Value);
                }

                return results.ToList();
            }
        }

        #endregion

        #region Public Methods - Statistics

        /// <summary>
        /// Gets statistics about materials in the database.
        /// </summary>
        /// <returns>Database statistics.</returns>
        public MaterialDatabaseStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new MaterialDatabaseStatistics
                {
                    TotalMaterials = Count,
                    CategoryCount = _materialsByCategory.Count,
                    DisciplineCount = _materialsByDiscipline.Count,
                    Categories = _materialsByCategory.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Count),
                    Disciplines = _materialsByDiscipline.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Count)
                };
            }
        }

        /// <summary>
        /// Gets the top N most common categories.
        /// </summary>
        /// <param name="count">Number of categories to return.</param>
        /// <returns>List of category names and material counts.</returns>
        public List<KeyValuePair<string, int>> GetTopCategories(int count = 10)
        {
            lock (_lock)
            {
                return _materialsByCategory
                    .OrderByDescending(kvp => kvp.Value.Count)
                    .Take(count)
                    .Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value.Count))
                    .ToList();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Adds a material to the database (internal use).
        /// </summary>
        private void AddMaterial(MaterialDefinition material)
        {
            if (material == null)
                return;

            // Add to main collection
            _allMaterials.Add(material);

            // Index by code
            if (!string.IsNullOrWhiteSpace(material.Code))
            {
                _materialsByCode[material.Code] = material;
            }

            // Index by GUID
            if (material.Guid != Guid.Empty)
            {
                _materialsByGuid[material.Guid] = material;
            }

            // Index by category
            if (!string.IsNullOrWhiteSpace(material.Category))
            {
                if (!_materialsByCategory.ContainsKey(material.Category))
                {
                    _materialsByCategory[material.Category] = new List<MaterialDefinition>();
                }
                _materialsByCategory[material.Category].Add(material);
            }

            // Index by discipline
            if (!string.IsNullOrWhiteSpace(material.Discipline))
            {
                if (!_materialsByDiscipline.ContainsKey(material.Discipline))
                {
                    _materialsByDiscipline[material.Discipline] = new List<MaterialDefinition>();
                }
                _materialsByDiscipline[material.Discipline].Add(material);
            }
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Creates a new material database and loads from the default loader.
        /// </summary>
        /// <param name="dataDirectory">Directory containing material Excel files.</param>
        /// <returns>Loaded material database.</returns>
        public static async Task<MaterialDatabase> CreateAndLoadAsync(string dataDirectory)
        {
            var database = new MaterialDatabase();
            var loader = new MaterialLoader(dataDirectory);
            await database.LoadAsync(loader);
            return database;
        }

        /// <summary>
        /// Creates a new material database and loads synchronously.
        /// </summary>
        /// <param name="dataDirectory">Directory containing material Excel files.</param>
        /// <returns>Loaded material database.</returns>
        public static MaterialDatabase CreateAndLoad(string dataDirectory)
        {
            return CreateAndLoadAsync(dataDirectory).GetAwaiter().GetResult();
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Search criteria for advanced material search.
    /// </summary>
    public class MaterialSearchCriteria
    {
        public string Query { get; set; }
        public string Category { get; set; }
        public string Discipline { get; set; }
        public string Manufacturer { get; set; }
        public string Standard { get; set; }
        public double? MinThermalResistance { get; set; }
        public double? MaxThermalResistance { get; set; }
        public bool CaseSensitive { get; set; }
    }

    /// <summary>
    /// Statistics about the material database.
    /// </summary>
    public class MaterialDatabaseStatistics
    {
        public int TotalMaterials { get; set; }
        public int CategoryCount { get; set; }
        public int DisciplineCount { get; set; }
        public Dictionary<string, int> Categories { get; set; }
        public Dictionary<string, int> Disciplines { get; set; }

        public override string ToString()
        {
            return $"Materials: {TotalMaterials}, Categories: {CategoryCount}, Disciplines: {DisciplineCount}";
        }
    }

    /// <summary>
    /// Represents a material definition with all properties.
    /// </summary>
    public class MaterialDefinition
    {
        public Guid Guid { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Discipline { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string Standard { get; set; }
        public double ThermalResistance { get; set; }
        public double ThermalConductivity { get; set; }
        public double Density { get; set; }
        public double SpecificHeat { get; set; }
        public string FireRating { get; set; }
        public double Cost { get; set; }
        public string CostUnit { get; set; }
        public string Application { get; set; }
        public Dictionary<string, string> CustomProperties { get; set; }

        public MaterialDefinition()
        {
            Guid = Guid.NewGuid();
            CustomProperties = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return $"{Code}: {Name} ({Category})";
        }
    }

    #endregion
}
