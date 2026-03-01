using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Materials
{
    /// <summary>
    /// Loads material definitions from Excel files (BLE_MATERIALS.xlsx, MEP_MATERIALS.xlsx).
    /// Supports parsing 2,450+ materials with validation and error handling.
    /// </summary>
    /// <remarks>
    /// Excel file format:
    /// - Multiple worksheets (one per category or discipline)
    /// - Headers in row 1
    /// - Material data starting row 2
    /// - Standard columns: Code, Name, Category, Discipline, etc.
    /// </remarks>
    public class MaterialLoader
    {
        #region Private Fields

        private readonly string _dataDirectory;
        private readonly List<string> _excelFiles;
        private readonly MaterialLoaderOptions _options;
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<MaterialLoader>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the data directory path.
        /// </summary>
        public string DataDirectory => _dataDirectory;

        /// <summary>
        /// Gets the list of Excel files to load.
        /// </summary>
        public IReadOnlyList<string> ExcelFiles => _excelFiles.AsReadOnly();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialLoader"/> class.
        /// </summary>
        /// <param name="dataDirectory">Directory containing Excel files.</param>
        /// <param name="options">Loader options (optional).</param>
        public MaterialLoader(string dataDirectory, MaterialLoaderOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
                throw new ArgumentException("Data directory cannot be null or empty", nameof(dataDirectory));

            if (!Directory.Exists(dataDirectory))
                throw new DirectoryNotFoundException($"Data directory not found: {dataDirectory}");

            _dataDirectory = dataDirectory;
            _options = options ?? MaterialLoaderOptions.Default;
            _excelFiles = new List<string>();

            DiscoverExcelFiles();
        }

        #endregion

        #region Public Methods - Loading

        /// <summary>
        /// Loads all materials from all Excel files asynchronously.
        /// </summary>
        /// <returns>List of material definitions.</returns>
        public async Task<List<MaterialDefinition>> LoadAllMaterialsAsync()
        {
            var allMaterials = new List<MaterialDefinition>();

            try
            {
                _logger.Info($"Loading materials from {_excelFiles.Count} Excel files...");

                foreach (var filePath in _excelFiles)
                {
                    try
                    {
                        var materials = await LoadFromFileAsync(filePath);
                        allMaterials.AddRange(materials);

                        _logger.Debug($"Loaded {materials.Count} materials from {Path.GetFileName(filePath)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");

                        if (!_options.ContinueOnError)
                            throw;
                    }
                }

                _logger.Info($"Total materials loaded: {allMaterials.Count}");
                return allMaterials;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load materials: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads all materials synchronously (wrapper for async method).
        /// </summary>
        /// <returns>List of material definitions.</returns>
        public List<MaterialDefinition> LoadAllMaterials()
        {
            return LoadAllMaterialsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads materials from a specific Excel file asynchronously.
        /// </summary>
        /// <param name="filePath">Path to Excel file.</param>
        /// <returns>List of material definitions from the file.</returns>
        public async Task<List<MaterialDefinition>> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");

            return await Task.Run(() =>
            {
                var materials = new List<MaterialDefinition>();

                try
                {
                    // NOTE: In production, use a library like EPPlus or ClosedXML to read Excel
                    // For this implementation, we'll simulate the Excel reading logic

                    _logger.Debug($"Parsing Excel file: {Path.GetFileName(filePath)}");

                    // Simulated Excel reading (replace with actual library in production)
                    var worksheets = GetWorksheetsFromFile(filePath);

                    foreach (var worksheet in worksheets)
                    {
                        var sheetMaterials = ParseWorksheet(worksheet, filePath);
                        materials.AddRange(sheetMaterials);
                    }

                    if (_options.ValidateOnLoad)
                    {
                        materials = ValidateMaterials(materials);
                    }

                    return materials;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error parsing {Path.GetFileName(filePath)}: {ex.Message}");
                    throw;
                }
            });
        }

        #endregion

        #region Private Methods - Discovery

        /// <summary>
        /// Discovers Excel files in the data directory.
        /// </summary>
        private void DiscoverExcelFiles()
        {
            _excelFiles.Clear();

            // Look for specific material files
            var bleFile = Path.Combine(_dataDirectory, "BLE_MATERIALS.xlsx");
            var mepFile = Path.Combine(_dataDirectory, "MEP_MATERIALS.xlsx");

            if (File.Exists(bleFile))
            {
                _excelFiles.Add(bleFile);
                _logger.Debug($"Found BLE materials file: {bleFile}");
            }

            if (File.Exists(mepFile))
            {
                _excelFiles.Add(mepFile);
                _logger.Debug($"Found MEP materials file: {mepFile}");
            }

            // Also scan for any other *MATERIAL*.xlsx files
            if (_options.AutoDiscoverFiles)
            {
                var additionalFiles = Directory.GetFiles(_dataDirectory, "*MATERIAL*.xlsx", SearchOption.TopDirectoryOnly)
                    .Where(f => !_excelFiles.Contains(f, StringComparer.OrdinalIgnoreCase));

                foreach (var file in additionalFiles)
                {
                    _excelFiles.Add(file);
                    _logger.Debug($"Auto-discovered material file: {Path.GetFileName(file)}");
                }
            }

            if (_excelFiles.Count == 0)
            {
                _logger.Warn($"No material Excel files found in {_dataDirectory}");
            }
        }

        #endregion

        #region Private Methods - Parsing

        /// <summary>
        /// Gets worksheets from an Excel file using ClosedXML.
        /// </summary>
        private List<MaterialWorksheet> GetWorksheetsFromFile(string filePath)
        {
            var worksheets = new List<MaterialWorksheet>();

            try
            {
                using var workbook = new XLWorkbook(filePath);

                foreach (var ws in workbook.Worksheets)
                {
                    var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                    var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

                    var columns = new List<string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        columns.Add(ws.Cell(1, c).GetString().Trim());
                    }

                    worksheets.Add(new MaterialWorksheet
                    {
                        Name = ws.Name,
                        RowCount = Math.Max(0, lastRow - 1), // Exclude header row
                        Columns = columns,
                        WorksheetRef = ws
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to open Excel file: {Path.GetFileName(filePath)}");

                // Fallback: return empty list so caller can handle gracefully
                return worksheets;
            }

            return worksheets;
        }

        /// <summary>
        /// Parses a worksheet into material definitions.
        /// </summary>
        private List<MaterialDefinition> ParseWorksheet(MaterialWorksheet worksheet, string sourceFile)
        {
            var materials = new List<MaterialDefinition>();

            try
            {
                _logger.Debug($"Parsing worksheet '{worksheet.Name}' ({worksheet.RowCount} rows)");

                // In production, iterate through actual Excel rows
                // For simulation, create sample materials

                for (int i = 0; i < worksheet.RowCount; i++)
                {
                    try
                    {
                        var material = CreateMaterialFromRow(worksheet, i);

                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Failed to parse row {i + 2} in '{worksheet.Name}': {ex.Message}");

                        if (!_options.ContinueOnError)
                            throw;
                    }
                }

                return materials;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error parsing worksheet '{worksheet.Name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a material definition from an Excel row using ClosedXML.
        /// </summary>
        private MaterialDefinition CreateMaterialFromRow(MaterialWorksheet worksheet, int rowIndex)
        {
            int excelRow = rowIndex + 2; // +1 for 1-based index, +1 to skip header

            string GetCell(int col) => worksheet.WorksheetRef?.Cell(excelRow, col).GetString()?.Trim() ?? string.Empty;

            // Map columns by header name for resilience against column reordering
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < worksheet.Columns.Count; i++)
                colMap[worksheet.Columns[i]] = i + 1; // 1-based Excel column

            string Col(string name) => colMap.TryGetValue(name, out var idx)
                ? (worksheet.WorksheetRef?.Cell(excelRow, idx).GetString()?.Trim() ?? string.Empty)
                : string.Empty;
            double ColNum(string name) => double.TryParse(Col(name), out var v) ? v : 0.0;

            string code = Col("Code");
            string materialName = Col("Name");

            // Skip empty rows
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(materialName))
                return null;

            // Fallback code generation if not in file
            if (string.IsNullOrWhiteSpace(code))
                code = $"{worksheet.Name.Substring(0, Math.Min(4, worksheet.Name.Length)).ToUpper()}-{rowIndex + 1:D3}";

            var material = new MaterialDefinition
            {
                Code = code,
                Name = string.IsNullOrWhiteSpace(materialName) ? $"{worksheet.Name} Material {rowIndex + 1}" : materialName,
                Category = string.IsNullOrWhiteSpace(Col("Category")) ? worksheet.Name : Col("Category"),
                Discipline = string.IsNullOrWhiteSpace(Col("Discipline")) ? DetermineDiscipline(worksheet.Name) : Col("Discipline"),
                Description = Col("Description"),
                Manufacturer = Col("Manufacturer"),
                Standard = Col("Standard"),
                ThermalResistance = ColNum("ThermalResistance"),
                ThermalConductivity = ColNum("ThermalConductivity"),
                Density = ColNum("Density"),
                SpecificHeat = ColNum("SpecificHeat"),
                FireRating = Col("FireRating"),
                Cost = ColNum("Cost"),
                CostUnit = string.IsNullOrWhiteSpace(Col("CostUnit")) ? "UGX/m2" : Col("CostUnit"),
                Application = Col("Application")
            };

            return material;
        }

        #endregion

        #region Private Methods - Validation

        /// <summary>
        /// Validates loaded materials and removes invalid ones.
        /// </summary>
        private List<MaterialDefinition> ValidateMaterials(List<MaterialDefinition> materials)
        {
            var validMaterials = new List<MaterialDefinition>();
            int invalidCount = 0;

            foreach (var material in materials)
            {
                if (ValidateMaterial(material))
                {
                    validMaterials.Add(material);
                }
                else
                {
                    invalidCount++;
                    _logger.Warn($"Invalid material skipped: {material.Code ?? "(no code)"}");
                }
            }

            if (invalidCount > 0)
            {
                _logger.Warn($"Skipped {invalidCount} invalid materials during validation");
            }

            return validMaterials;
        }

        /// <summary>
        /// Validates a single material definition.
        /// </summary>
        private bool ValidateMaterial(MaterialDefinition material)
        {
            if (material == null)
                return false;

            // Required fields
            if (string.IsNullOrWhiteSpace(material.Code))
                return false;

            if (string.IsNullOrWhiteSpace(material.Name))
                return false;

            // Optional: Add more validation rules
            return true;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Determines discipline from category name.
        /// </summary>
        private string DetermineDiscipline(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "General";

            category = category.ToLower();

            if (category.Contains("pipe") || category.Contains("duct") || category.Contains("cable") || category.Contains("equipment"))
                return "MEP";

            if (category.Contains("concrete") || category.Contains("steel") || category.Contains("masonry"))
                return "Structural";

            if (category.Contains("finish") || category.Contains("insulation"))
                return "Architecture";

            return "General";
        }

        /// <summary>
        /// Gets a sample manufacturer name.
        /// </summary>
        private string GetSampleManufacturer(int index)
        {
            var manufacturers = new[] { "BuildCo", "StructMat", "MEPSupply", "GlobalMaterials", "LocalSource" };
            return manufacturers[index % manufacturers.Length];
        }

        /// <summary>
        /// Gets a sample standard name.
        /// </summary>
        private string GetSampleStandard(string category)
        {
            var standards = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Concrete", "BS 8110" },
                { "Steel", "BS 5950" },
                { "Masonry", "BS 5628" },
                { "Pipes", "BS EN 1329" },
                { "Ducts", "SMACNA" },
                { "Cables", "IEC 60227" }
            };

            return standards.TryGetValue(category, out var standard) ? standard : "ISO 19650";
        }

        /// <summary>
        /// Gets a sample thermal resistance value.
        /// </summary>
        private double GetSampleThermalResistance(string category, int index)
        {
            if (category.ToLower().Contains("insulation"))
                return 2.0 + (index * 0.1);

            if (category.ToLower().Contains("concrete"))
                return 0.1 + (index * 0.01);

            return 0.5 + (index * 0.05);
        }

        /// <summary>
        /// Gets a sample fire rating.
        /// </summary>
        private string GetSampleFireRating(int index)
        {
            var ratings = new[] { "A1", "A2", "B", "C", "D", "E", "F" };
            return ratings[index % ratings.Length];
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Options for material loader behavior.
    /// </summary>
    public class MaterialLoaderOptions
    {
        /// <summary>
        /// Gets or sets whether to continue loading if an error occurs.
        /// </summary>
        public bool ContinueOnError { get; set; }

        /// <summary>
        /// Gets or sets whether to validate materials after loading.
        /// </summary>
        public bool ValidateOnLoad { get; set; }

        /// <summary>
        /// Gets or sets whether to auto-discover Excel files in the directory.
        /// </summary>
        public bool AutoDiscoverFiles { get; set; }

        /// <summary>
        /// Gets the default loader options.
        /// </summary>
        public static MaterialLoaderOptions Default => new MaterialLoaderOptions
        {
            ContinueOnError = true,
            ValidateOnLoad = true,
            AutoDiscoverFiles = true
        };
    }

    /// <summary>
    /// Represents a worksheet from an Excel file (internal use).
    /// </summary>
    internal class MaterialWorksheet
    {
        public string Name { get; set; }
        public int RowCount { get; set; }
        public List<string> Columns { get; set; }
        /// <summary>ClosedXML worksheet reference for reading cell data.</summary>
        internal IXLWorksheet WorksheetRef { get; set; }
    }

    #endregion
}
