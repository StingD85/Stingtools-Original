using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Formulas
{
    /// <summary>
    /// Central library for all formula definitions loaded from FORMULAS_WITH_DEPENDENCIES.csv.
    /// Manages 52+ professional formulas with dependency tracking and validation.
    /// </summary>
    /// <remarks>
    /// Features:
    /// - Load formulas from CSV
    /// - Dependency graph management
    /// - Formula lookup and search
    /// - Discipline-based filtering
    /// - Formula validation
    /// - Execution order calculation
    /// </remarks>
    public class FormulaLibrary
    {
        #region Private Fields

        private static readonly StingBIMLogger _logger = StingBIMLogger.For<FormulaLibrary>();
        private readonly Dictionary<string, FormulaDefinition> _formulasByName;
        private readonly Dictionary<string, List<FormulaDefinition>> _formulasByDiscipline;
        private readonly Dictionary<string, List<FormulaDefinition>> _formulasByParameter;
        private readonly List<FormulaDefinition> _allFormulas;
        private readonly object _lock = new object();
        private bool _isLoaded;
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<FormulaLibrary>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the total number of formulas in the library.
        /// </summary>
        public int Count => _allFormulas.Count;

        /// <summary>
        /// Gets whether the library has been loaded.
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Gets all unique disciplines in the library.
        /// </summary>
        public IEnumerable<string> Disciplines => _formulasByDiscipline.Keys;

        /// <summary>
        /// Gets all formulas (read-only).
        /// </summary>
        public IReadOnlyList<FormulaDefinition> AllFormulas => _allFormulas.AsReadOnly();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FormulaLibrary"/> class.
        /// </summary>
        public FormulaLibrary()
        {
            _formulasByName = new Dictionary<string, FormulaDefinition>(StringComparer.OrdinalIgnoreCase);
            _formulasByDiscipline = new Dictionary<string, List<FormulaDefinition>>(StringComparer.OrdinalIgnoreCase);
            _formulasByParameter = new Dictionary<string, List<FormulaDefinition>>(StringComparer.OrdinalIgnoreCase);
            _allFormulas = new List<FormulaDefinition>();
            _isLoaded = false;
        }

        #endregion

        #region Public Methods - Loading

        /// <summary>
        /// Loads formulas from CSV file asynchronously.
        /// </summary>
        /// <param name="csvFilePath">Path to FORMULAS_WITH_DEPENDENCIES.csv.</param>
        /// <returns>Number of formulas loaded.</returns>
        public async Task<int> LoadFromCsvAsync(string csvFilePath)
        {
            if (string.IsNullOrWhiteSpace(csvFilePath))
                throw new ArgumentException("CSV file path cannot be null or empty", nameof(csvFilePath));

            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"Formula CSV file not found: {csvFilePath}");

            try
            {
                _logger.Info($"Loading formulas from {Path.GetFileName(csvFilePath)}...");

                var formulas = await Task.Run(() => ParseCsvFile(csvFilePath));

                lock (_lock)
                {
                    Clear();

                    foreach (var formula in formulas)
                    {
                        AddFormula(formula);
                    }

                    _isLoaded = true;
                }

                _logger.Info($"Formula library loaded: {Count} formulas");
                return Count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load formula library: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads formulas synchronously (wrapper for async method).
        /// </summary>
        /// <param name="csvFilePath">Path to CSV file.</param>
        /// <returns>Number of formulas loaded.</returns>
        public int LoadFromCsv(string csvFilePath)
        {
            return LoadFromCsvAsync(csvFilePath).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Clears all formulas from the library.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _formulasByName.Clear();
                _formulasByDiscipline.Clear();
                _formulasByParameter.Clear();
                _allFormulas.Clear();
                _isLoaded = false;

                _logger.Debug("Formula library cleared");
            }
        }

        #endregion

        #region Public Methods - Lookup

        /// <summary>
        /// Gets a formula by its parameter name.
        /// </summary>
        /// <param name="parameterName">Parameter name (e.g., "CST_CALC_VOLUME_M3").</param>
        /// <returns>Formula definition if found; otherwise, null.</returns>
        public FormulaDefinition GetByParameterName(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return null;

            lock (_lock)
            {
                return _formulasByName.TryGetValue(parameterName, out var formula) ? formula : null;
            }
        }

        /// <summary>
        /// Checks if a formula exists for a parameter.
        /// </summary>
        /// <param name="parameterName">Parameter name.</param>
        /// <returns>True if formula exists; otherwise, false.</returns>
        public bool Exists(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return false;

            lock (_lock)
            {
                return _formulasByName.ContainsKey(parameterName);
            }
        }

        #endregion

        #region Public Methods - Filtering

        /// <summary>
        /// Gets all formulas for a specific discipline.
        /// </summary>
        /// <param name="discipline">Discipline name (e.g., "Architecture", "MEP", "Structural").</param>
        /// <returns>List of formulas in the discipline.</returns>
        public List<FormulaDefinition> GetByDiscipline(string discipline)
        {
            if (string.IsNullOrWhiteSpace(discipline))
                return new List<FormulaDefinition>();

            lock (_lock)
            {
                return _formulasByDiscipline.TryGetValue(discipline, out var formulas)
                    ? new List<FormulaDefinition>(formulas)
                    : new List<FormulaDefinition>();
            }
        }

        /// <summary>
        /// Gets all formulas that use a specific input parameter.
        /// </summary>
        /// <param name="inputParameter">Input parameter name.</param>
        /// <returns>List of formulas that depend on this parameter.</returns>
        public List<FormulaDefinition> GetByInputParameter(string inputParameter)
        {
            if (string.IsNullOrWhiteSpace(inputParameter))
                return new List<FormulaDefinition>();

            lock (_lock)
            {
                return _formulasByParameter.TryGetValue(inputParameter, out var formulas)
                    ? new List<FormulaDefinition>(formulas)
                    : new List<FormulaDefinition>();
            }
        }

        /// <summary>
        /// Filters formulas based on a predicate.
        /// </summary>
        /// <param name="predicate">Filter predicate.</param>
        /// <returns>Filtered list of formulas.</returns>
        public List<FormulaDefinition> Filter(Func<FormulaDefinition, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            lock (_lock)
            {
                return _allFormulas.Where(predicate).ToList();
            }
        }

        #endregion

        #region Public Methods - Search

        /// <summary>
        /// Searches for formulas by text query (searches parameter name, description).
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="caseSensitive">Whether search should be case-sensitive.</param>
        /// <returns>List of matching formulas.</returns>
        public List<FormulaDefinition> Search(string query, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<FormulaDefinition>();

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            lock (_lock)
            {
                return _allFormulas.Where(f =>
                    (f.ParameterName?.IndexOf(query, comparison) >= 0) ||
                    (f.Description?.IndexOf(query, comparison) >= 0) ||
                    (f.Formula?.IndexOf(query, comparison) >= 0)
                ).ToList();
            }
        }

        #endregion

        #region Public Methods - Dependency Analysis

        /// <summary>
        /// Gets all formulas that depend on a specific parameter (direct dependencies).
        /// </summary>
        /// <param name="parameterName">Parameter name.</param>
        /// <returns>List of dependent formulas.</returns>
        public List<FormulaDefinition> GetDependents(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return new List<FormulaDefinition>();

            lock (_lock)
            {
                return _allFormulas
                    .Where(f => f.InputParameters.Contains(parameterName, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all dependencies for a formula (parameters it uses).
        /// </summary>
        /// <param name="parameterName">Parameter name.</param>
        /// <returns>List of input parameters.</returns>
        public List<string> GetDependencies(string parameterName)
        {
            var formula = GetByParameterName(parameterName);
            return formula?.InputParameters.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Checks if a formula has circular dependencies.
        /// </summary>
        /// <param name="parameterName">Parameter name to check.</param>
        /// <returns>True if circular dependency detected; otherwise, false.</returns>
        public bool HasCircularDependency(string parameterName)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recursionStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return HasCircularDependencyRecursive(parameterName, visited, recursionStack);
        }

        #endregion

        #region Public Methods - Statistics

        /// <summary>
        /// Gets statistics about formulas in the library.
        /// </summary>
        /// <returns>Library statistics.</returns>
        public FormulaLibraryStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new FormulaLibraryStatistics
                {
                    TotalFormulas = Count,
                    DisciplineCount = _formulasByDiscipline.Count,
                    AverageDependencies = _allFormulas.Average(f => f.InputParameters.Count),
                    MaxDependencyLevel = _allFormulas.Max(f => f.DependencyLevel),
                    FormulasByDiscipline = _formulasByDiscipline.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Count),
                    FormulasByDependencyLevel = _allFormulas
                        .GroupBy(f => f.DependencyLevel)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        #endregion

        #region Private Methods - CSV Parsing

        /// <summary>
        /// Parses the CSV file and extracts formula definitions.
        /// </summary>
        private List<FormulaDefinition> ParseCsvFile(string csvFilePath)
        {
            var formulas = new List<FormulaDefinition>();

            try
            {
                using (var reader = new StreamReader(csvFilePath))
                {
                    // Read header
                    string headerLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        throw new InvalidDataException("CSV file is empty");
                    }

                    var headers = ParseCsvLine(headerLine);
                    var columnMap = MapColumns(headers);

                    // Read data rows
                    int lineNumber = 1;
                    while (!reader.EndOfStream)
                    {
                        lineNumber++;
                        string line = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            var values = ParseCsvLine(line);
                            var formula = CreateFormulaFromCsvRow(values, columnMap);

                            if (formula != null)
                            {
                                formulas.Add(formula);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"Failed to parse line {lineNumber}: {ex.Message}");
                        }
                    }
                }

                return formulas;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error parsing CSV file: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Parses a CSV line handling quoted fields.
        /// </summary>
        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Add last field
            fields.Add(currentField.ToString().Trim());

            return fields;
        }

        /// <summary>
        /// Maps CSV column headers to indices.
        /// </summary>
        private Dictionary<string, int> MapColumns(List<string> headers)
        {
            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                columnMap[headers[i]] = i;
            }

            return columnMap;
        }

        /// <summary>
        /// Creates a formula definition from a CSV row.
        /// </summary>
        private FormulaDefinition CreateFormulaFromCsvRow(List<string> values, Dictionary<string, int> columnMap)
        {
            try
            {
                var formula = new FormulaDefinition();

                // Required fields
                formula.Discipline = GetColumnValue(values, columnMap, "Discipline");
                formula.ParameterName = GetColumnValue(values, columnMap, "Parameter_Name");
                formula.DataType = GetColumnValue(values, columnMap, "Data_Type");
                formula.Formula = GetColumnValue(values, columnMap, "Revit_Formula");
                formula.Description = GetColumnValue(values, columnMap, "Description");
                formula.Unit = GetColumnValue(values, columnMap, "Unit");
                formula.ParameterGuid = GetColumnValue(values, columnMap, "Parameter_GUID");
                formula.ParameterDescription = GetColumnValue(values, columnMap, "Parameter_Description");

                // Parse dependency level
                string depLevelStr = GetColumnValue(values, columnMap, "Dependency_Level");
                if (int.TryParse(depLevelStr, out int depLevel))
                {
                    formula.DependencyLevel = depLevel;
                }

                // Parse input parameters (comma-separated list)
                string inputParamsStr = GetColumnValue(values, columnMap, "Input_Parameters");
                if (!string.IsNullOrWhiteSpace(inputParamsStr))
                {
                    formula.InputParameters = inputParamsStr
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(formula.ParameterName))
                    return null;

                if (string.IsNullOrWhiteSpace(formula.Formula))
                    return null;

                return formula;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to create formula from CSV row: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a column value from CSV row.
        /// </summary>
        private string GetColumnValue(List<string> values, Dictionary<string, int> columnMap, string columnName)
        {
            if (columnMap.TryGetValue(columnName, out int index) && index < values.Count)
            {
                return values[index];
            }

            return string.Empty;
        }

        #endregion

        #region Private Methods - Indexing

        /// <summary>
        /// Adds a formula to the library (internal use).
        /// </summary>
        private void AddFormula(FormulaDefinition formula)
        {
            if (formula == null)
                return;

            // Add to main collection
            _allFormulas.Add(formula);

            // Index by parameter name
            if (!string.IsNullOrWhiteSpace(formula.ParameterName))
            {
                _formulasByName[formula.ParameterName] = formula;
            }

            // Index by discipline
            if (!string.IsNullOrWhiteSpace(formula.Discipline))
            {
                if (!_formulasByDiscipline.ContainsKey(formula.Discipline))
                {
                    _formulasByDiscipline[formula.Discipline] = new List<FormulaDefinition>();
                }
                _formulasByDiscipline[formula.Discipline].Add(formula);
            }

            // Index by input parameters
            foreach (var inputParam in formula.InputParameters)
            {
                if (!_formulasByParameter.ContainsKey(inputParam))
                {
                    _formulasByParameter[inputParam] = new List<FormulaDefinition>();
                }
                _formulasByParameter[inputParam].Add(formula);
            }
        }

        #endregion

        #region Private Methods - Dependency Analysis

        /// <summary>
        /// Recursive check for circular dependencies.
        /// </summary>
        private bool HasCircularDependencyRecursive(
            string parameterName,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            if (recursionStack.Contains(parameterName))
                return true; // Circular dependency detected

            if (visited.Contains(parameterName))
                return false; // Already checked, no circular dependency

            visited.Add(parameterName);
            recursionStack.Add(parameterName);

            var dependencies = GetDependencies(parameterName);

            foreach (var dependency in dependencies)
            {
                if (HasCircularDependencyRecursive(dependency, visited, recursionStack))
                {
                    return true;
                }
            }

            recursionStack.Remove(parameterName);

            return false;
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Creates and loads a formula library from CSV file.
        /// </summary>
        /// <param name="csvFilePath">Path to CSV file.</param>
        /// <returns>Loaded formula library.</returns>
        public static async Task<FormulaLibrary> CreateAndLoadAsync(string csvFilePath)
        {
            var library = new FormulaLibrary();
            await library.LoadFromCsvAsync(csvFilePath);
            return library;
        }

        /// <summary>
        /// Creates and loads a formula library synchronously.
        /// </summary>
        /// <param name="csvFilePath">Path to CSV file.</param>
        /// <returns>Loaded formula library.</returns>
        public static FormulaLibrary CreateAndLoad(string csvFilePath)
        {
            return CreateAndLoadAsync(csvFilePath).GetAwaiter().GetResult();
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Statistics about the formula library.
    /// </summary>
    public class FormulaLibraryStatistics
    {
        public int TotalFormulas { get; set; }
        public int DisciplineCount { get; set; }
        public double AverageDependencies { get; set; }
        public int MaxDependencyLevel { get; set; }
        public Dictionary<string, int> FormulasByDiscipline { get; set; }
        public Dictionary<int, int> FormulasByDependencyLevel { get; set; }

        public override string ToString()
        {
            return $"Formulas: {TotalFormulas}, Disciplines: {DisciplineCount}, Max Depth: {MaxDependencyLevel}";
        }
    }

    /// <summary>
    /// Represents a formula definition.
    /// </summary>
    public class FormulaDefinition
    {
        public string Discipline { get; set; }
        public string ParameterName { get; set; }
        public string DataType { get; set; }
        public string Formula { get; set; }
        public string Description { get; set; }
        public List<string> InputParameters { get; set; }
        public string Unit { get; set; }
        public string ParameterGuid { get; set; }
        public string ParameterDescription { get; set; }
        public int DependencyLevel { get; set; }

        public FormulaDefinition()
        {
            InputParameters = new List<string>();
        }

        public override string ToString()
        {
            return $"{ParameterName}: {Formula} (Level {DependencyLevel})";
        }
    }

    #endregion
}
