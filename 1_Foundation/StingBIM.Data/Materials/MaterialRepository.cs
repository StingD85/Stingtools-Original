// ============================================================================
// StingBIM Data - Material Repository
// Material database with thermal, cost, and sustainability properties
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using NLog;

namespace StingBIM.Data.Materials
{
    /// <summary>
    /// Repository for building materials with comprehensive properties.
    /// Supports thermal calculations, cost estimation, and sustainability metrics.
    /// </summary>
    public class MaterialRepository
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, Material> _materials;
        private readonly Dictionary<string, List<Material>> _materialsByCategory;

        public MaterialRepository()
        {
            _materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            _materialsByCategory = new Dictionary<string, List<Material>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads materials from a CSV file.
        /// </summary>
        public void LoadFromCsv(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Warn($"Material file not found: {filePath}");
                return;
            }

            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, config);

                var records = csv.GetRecords<MaterialCsvRecord>();

                foreach (var record in records)
                {
                    var material = MapToMaterial(record);
                    AddMaterial(material);
                }

                Logger.Info($"Loaded {_materials.Count} materials from {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load materials from {filePath}");
            }
        }

        /// <summary>
        /// Gets a material by ID.
        /// </summary>
        public Material GetMaterial(string materialId)
        {
            return _materials.TryGetValue(materialId, out var material) ? material : null;
        }

        /// <summary>
        /// Gets materials by category.
        /// </summary>
        public IEnumerable<Material> GetMaterialsByCategory(string category)
        {
            return _materialsByCategory.TryGetValue(category, out var materials)
                ? materials.AsReadOnly()
                : Enumerable.Empty<Material>();
        }

        /// <summary>
        /// Searches materials by name or description.
        /// </summary>
        public IEnumerable<Material> SearchMaterials(string searchTerm)
        {
            var term = searchTerm.ToLowerInvariant();
            return _materials.Values.Where(m =>
                m.Name.ToLowerInvariant().Contains(term) ||
                (m.Description?.ToLowerInvariant().Contains(term) ?? false) ||
                (m.Category?.ToLowerInvariant().Contains(term) ?? false));
        }

        /// <summary>
        /// Gets materials suitable for a specific application.
        /// </summary>
        public IEnumerable<Material> GetMaterialsForApplication(MaterialApplication application)
        {
            return _materials.Values.Where(m => m.Applications.Contains(application));
        }

        /// <summary>
        /// Gets materials meeting thermal requirements.
        /// </summary>
        public IEnumerable<Material> GetMaterialsByThermalRequirements(
            double? minRValue = null,
            double? maxUValue = null,
            double? maxConductivity = null)
        {
            return _materials.Values.Where(m =>
                (!minRValue.HasValue || m.ThermalProperties.RValue >= minRValue.Value) &&
                (!maxUValue.HasValue || m.ThermalProperties.UValue <= maxUValue.Value) &&
                (!maxConductivity.HasValue || m.ThermalProperties.ThermalConductivity <= maxConductivity.Value));
        }

        /// <summary>
        /// Gets materials within a cost range.
        /// </summary>
        public IEnumerable<Material> GetMaterialsByCostRange(
            decimal minCost,
            decimal maxCost,
            string currency = "USD")
        {
            return _materials.Values.Where(m =>
                m.CostProperties.UnitCost >= minCost &&
                m.CostProperties.UnitCost <= maxCost &&
                m.CostProperties.Currency == currency);
        }

        /// <summary>
        /// Adds a material to the repository.
        /// </summary>
        public void AddMaterial(Material material)
        {
            _materials[material.Id] = material;

            if (!string.IsNullOrEmpty(material.Category))
            {
                if (!_materialsByCategory.ContainsKey(material.Category))
                {
                    _materialsByCategory[material.Category] = new List<Material>();
                }
                _materialsByCategory[material.Category].Add(material);
            }
        }

        /// <summary>
        /// Gets all materials.
        /// </summary>
        public IReadOnlyList<Material> GetAllMaterials()
        {
            return _materials.Values.ToList();
        }

        /// <summary>
        /// Gets all categories.
        /// </summary>
        public IEnumerable<string> GetCategories()
        {
            return _materialsByCategory.Keys.OrderBy(c => c);
        }

        /// <summary>
        /// Calculates composite R-value for layered assembly.
        /// </summary>
        public double CalculateCompositeRValue(IEnumerable<(Material material, double thickness)> layers)
        {
            double totalRValue = 0;

            foreach (var (material, thickness) in layers)
            {
                if (material.ThermalProperties.ThermalConductivity > 0)
                {
                    // R = thickness / conductivity
                    totalRValue += thickness / material.ThermalProperties.ThermalConductivity;
                }
            }

            // Add surface resistances (typical values)
            totalRValue += 0.17; // Interior surface
            totalRValue += 0.04; // Exterior surface

            return totalRValue;
        }

        private Material MapToMaterial(MaterialCsvRecord record)
        {
            return new Material
            {
                Id = record.MaterialId ?? Guid.NewGuid().ToString(),
                Name = record.Name,
                Category = record.Category,
                SubCategory = record.SubCategory,
                Description = record.Description,
                Manufacturer = record.Manufacturer,
                ThermalProperties = new ThermalProperties
                {
                    ThermalConductivity = record.ThermalConductivity,
                    SpecificHeat = record.SpecificHeat,
                    Density = record.Density,
                    RValue = record.RValue,
                    UValue = record.UValue
                },
                CostProperties = new CostProperties
                {
                    UnitCost = record.UnitCost,
                    Currency = record.Currency ?? "USD",
                    Unit = record.Unit ?? "m²",
                    InstallationCostFactor = record.InstallationCostFactor
                },
                SustainabilityProperties = new SustainabilityProperties
                {
                    EmbodiedCarbon = record.EmbodiedCarbon,
                    RecycledContent = record.RecycledContent,
                    IsRecyclable = record.IsRecyclable,
                    LifespanYears = record.LifespanYears,
                    VOCEmissions = record.VOCEmissions
                },
                PhysicalProperties = new PhysicalProperties
                {
                    Density = record.Density,
                    Thickness = record.StandardThickness,
                    Color = record.Color,
                    Texture = record.Texture
                },
                FireRating = record.FireRating,
                AcousticRating = record.AcousticRating,
                Region = record.Region ?? "International"
            };
        }
    }

    /// <summary>
    /// Building material with comprehensive properties.
    /// </summary>
    public class Material
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public ThermalProperties ThermalProperties { get; set; } = new ThermalProperties();
        public CostProperties CostProperties { get; set; } = new CostProperties();
        public SustainabilityProperties SustainabilityProperties { get; set; } = new SustainabilityProperties();
        public PhysicalProperties PhysicalProperties { get; set; } = new PhysicalProperties();
        public string FireRating { get; set; }
        public double AcousticRating { get; set; }
        public string Region { get; set; }
        public List<MaterialApplication> Applications { get; set; } = new List<MaterialApplication>();
    }

    public class ThermalProperties
    {
        public double ThermalConductivity { get; set; } // W/(m·K)
        public double SpecificHeat { get; set; } // J/(kg·K)
        public double Density { get; set; } // kg/m³
        public double RValue { get; set; } // m²·K/W
        public double UValue { get; set; } // W/(m²·K)
    }

    public class CostProperties
    {
        public decimal UnitCost { get; set; }
        public string Currency { get; set; } = "USD";
        public string Unit { get; set; } = "m²";
        public decimal InstallationCostFactor { get; set; } = 1.0m;

        public decimal TotalInstalledCost => UnitCost * InstallationCostFactor;
    }

    public class SustainabilityProperties
    {
        public double EmbodiedCarbon { get; set; } // kgCO2e/kg
        public double RecycledContent { get; set; } // percentage
        public bool IsRecyclable { get; set; }
        public int LifespanYears { get; set; }
        public string VOCEmissions { get; set; }
    }

    public class PhysicalProperties
    {
        public double Density { get; set; }
        public double Thickness { get; set; }
        public string Color { get; set; }
        public string Texture { get; set; }
    }

    public enum MaterialApplication
    {
        ExteriorWall,
        InteriorWall,
        Roof,
        Floor,
        Foundation,
        Insulation,
        Finish,
        Structure,
        Cladding,
        Waterproofing
    }

    /// <summary>
    /// CSV record mapping for material import.
    /// </summary>
    internal class MaterialCsvRecord
    {
        public string MaterialId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public double ThermalConductivity { get; set; }
        public double SpecificHeat { get; set; }
        public double Density { get; set; }
        public double RValue { get; set; }
        public double UValue { get; set; }
        public decimal UnitCost { get; set; }
        public string Currency { get; set; }
        public string Unit { get; set; }
        public decimal InstallationCostFactor { get; set; }
        public double EmbodiedCarbon { get; set; }
        public double RecycledContent { get; set; }
        public bool IsRecyclable { get; set; }
        public int LifespanYears { get; set; }
        public string VOCEmissions { get; set; }
        public double StandardThickness { get; set; }
        public string Color { get; set; }
        public string Texture { get; set; }
        public string FireRating { get; set; }
        public double AcousticRating { get; set; }
        public string Region { get; set; }
    }
}
