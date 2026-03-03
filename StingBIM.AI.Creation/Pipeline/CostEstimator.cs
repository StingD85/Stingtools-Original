// StingBIM.AI.Creation.Pipeline.CostEstimator
// Estimates construction costs from CONSTRUCTION_COSTS_AFRICA.csv
// v4 Prompt Reference: Section E.1 Material Costs, Section G New Files

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NLog;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Estimates construction costs using rates from CONSTRUCTION_COSTS_AFRICA.csv.
    /// Primary currency: UGX. Secondary display: USD.
    /// </summary>
    public class CostEstimator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _cacheLock = new object();

        // Approximate exchange rate (updated periodically in config)
        private const double UGX_PER_USD = 3750.0;

        private Dictionary<string, CostRate> _rates;
        private bool _isLoaded;

        // Default rates (used when CSV is unavailable)
        private static readonly Dictionary<string, CostRate> DefaultRates =
            new Dictionary<string, CostRate>(StringComparer.OrdinalIgnoreCase)
            {
                // Walls (per m²)
                ["wall_150mm_block"] = new CostRate("150mm Concrete Block Wall", 85000, "m²", "Walls"),
                ["wall_200mm_block"] = new CostRate("200mm Concrete Block Wall", 110000, "m²", "Walls"),
                ["wall_200mm_brick"] = new CostRate("200mm Brick Wall", 135000, "m²", "Walls"),
                ["wall_partition"] = new CostRate("Partition Wall (100mm)", 65000, "m²", "Walls"),
                ["wall_cavity"] = new CostRate("Cavity Wall (275mm)", 165000, "m²", "Walls"),

                // Floors (per m²)
                ["floor_concrete_100mm"] = new CostRate("100mm Concrete Floor Slab", 75000, "m²", "Floors"),
                ["floor_concrete_150mm"] = new CostRate("150mm Concrete Floor Slab", 95000, "m²", "Floors"),
                ["floor_screed"] = new CostRate("50mm Sand/Cement Screed", 25000, "m²", "Floors"),
                ["floor_tile_ceramic"] = new CostRate("Ceramic Floor Tiles", 45000, "m²", "Finishes"),
                ["floor_tile_porcelain"] = new CostRate("Porcelain Floor Tiles", 75000, "m²", "Finishes"),

                // Roofing (per m²)
                ["roof_iron_sheet"] = new CostRate("Corrugated Iron Sheet Roof", 55000, "m²", "Roofing"),
                ["roof_aluminium"] = new CostRate("Aluminium Roofing Sheet", 85000, "m²", "Roofing"),
                ["roof_clay_tile"] = new CostRate("Clay Tile Roof", 120000, "m²", "Roofing"),
                ["roof_concrete_slab"] = new CostRate("Concrete Roof Slab", 180000, "m²", "Roofing"),

                // Structural (per m³ for concrete, per tonne for steel)
                ["concrete_c20"] = new CostRate("C20/25 Concrete", 450000, "m³", "Structure"),
                ["concrete_c25"] = new CostRate("C25/30 Concrete", 520000, "m³", "Structure"),
                ["rebar_y12"] = new CostRate("Y12 Reinforcement Bar", 3800000, "tonne", "Structure"),
                ["rebar_y16"] = new CostRate("Y16 Reinforcement Bar", 3600000, "tonne", "Structure"),
                ["steel_uc"] = new CostRate("Steel Universal Column", 7500000, "tonne", "Structure"),

                // Doors (per unit)
                ["door_internal_flush"] = new CostRate("Internal Flush Door (complete)", 350000, "nr", "Doors"),
                ["door_external"] = new CostRate("External Hardwood Door", 650000, "nr", "Doors"),
                ["door_fire_rated"] = new CostRate("Fire Rated Door (complete)", 1200000, "nr", "Doors"),

                // Windows (per unit)
                ["window_aluminium_1200"] = new CostRate("Aluminium Window 1200×1000", 450000, "nr", "Windows"),
                ["window_steel"] = new CostRate("Steel Window", 280000, "nr", "Windows"),

                // Ceilings (per m²)
                ["ceiling_plasterboard"] = new CostRate("Plasterboard Ceiling", 35000, "m²", "Ceilings"),
                ["ceiling_acoustic_tile"] = new CostRate("Acoustic Tile Ceiling", 55000, "m²", "Ceilings"),

                // Foundations (per m³)
                ["foundation_strip"] = new CostRate("Strip Foundation Concrete", 480000, "m³", "Substructure"),
                ["foundation_pad"] = new CostRate("Pad Foundation Concrete", 500000, "m³", "Substructure"),
                ["foundation_raft"] = new CostRate("Raft Foundation Concrete", 550000, "m³", "Substructure"),

                // Stairs (per flight)
                ["staircase_rc"] = new CostRate("RC Staircase (per flight)", 3500000, "flight", "Stairs"),
                ["staircase_steel"] = new CostRate("Steel Staircase (per flight)", 5500000, "flight", "Stairs")
            };

        /// <summary>
        /// Loads cost rates from CSV file.
        /// Falls back to default rates if file is unavailable.
        /// </summary>
        public void LoadRates(string csvPath = null)
        {
            lock (_cacheLock)
            {
                if (_isLoaded) return;

                _rates = new Dictionary<string, CostRate>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(csvPath) && File.Exists(csvPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(csvPath);
                        // Skip header row
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var parts = lines[i].Split(',');
                            if (parts.Length >= 4)
                            {
                                var key = parts[0].Trim().ToLowerInvariant().Replace(" ", "_");
                                var description = parts[1].Trim();
                                if (double.TryParse(parts[2].Trim(), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out var rate))
                                {
                                    var unit = parts[3].Trim();
                                    var category = parts.Length > 4 ? parts[4].Trim() : "General";
                                    _rates[key] = new CostRate(description, rate, unit, category);
                                }
                            }
                        }
                        Logger.Info($"Loaded {_rates.Count} cost rates from {csvPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to load cost rates from CSV, using defaults");
                        _rates = new Dictionary<string, CostRate>(DefaultRates, StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    _rates = new Dictionary<string, CostRate>(DefaultRates, StringComparer.OrdinalIgnoreCase);
                    Logger.Info("Using default cost rates (CSV not available)");
                }

                _isLoaded = true;
            }
        }

        /// <summary>
        /// Estimates the cost of a wall in UGX.
        /// </summary>
        public CostEstimate EstimateWallCost(double lengthMm, double heightMm, string wallType = null)
        {
            EnsureLoaded();
            var areaSqM = (lengthMm / 1000.0) * (heightMm / 1000.0);

            var rateKey = ResolveWallRateKey(wallType);
            if (_rates.TryGetValue(rateKey, out var rate))
            {
                return new CostEstimate
                {
                    Description = rate.Description,
                    Quantity = areaSqM,
                    Unit = rate.Unit,
                    UnitRateUGX = rate.RateUGX,
                    TotalUGX = areaSqM * rate.RateUGX,
                    TotalUSD = areaSqM * rate.RateUGX / UGX_PER_USD,
                    Category = rate.Category
                };
            }

            return CostEstimate.Unknown("Wall", areaSqM, "m²");
        }

        /// <summary>
        /// Estimates the cost of a floor in UGX.
        /// </summary>
        public CostEstimate EstimateFloorCost(double areaSqM, string floorType = null)
        {
            EnsureLoaded();
            var rateKey = floorType != null && _rates.ContainsKey(floorType)
                ? floorType
                : "floor_concrete_150mm";

            if (_rates.TryGetValue(rateKey, out var rate))
            {
                return new CostEstimate
                {
                    Description = rate.Description,
                    Quantity = areaSqM,
                    Unit = rate.Unit,
                    UnitRateUGX = rate.RateUGX,
                    TotalUGX = areaSqM * rate.RateUGX,
                    TotalUSD = areaSqM * rate.RateUGX / UGX_PER_USD,
                    Category = rate.Category
                };
            }

            return CostEstimate.Unknown("Floor", areaSqM, "m²");
        }

        /// <summary>
        /// Formats a cost estimate for display in the chat panel.
        /// </summary>
        public static string FormatCost(double ugx)
        {
            if (ugx >= 1_000_000_000)
                return $"UGX {ugx / 1_000_000_000:F1}B";
            if (ugx >= 1_000_000)
                return $"UGX {ugx / 1_000_000:F1}M";
            if (ugx >= 1_000)
                return $"UGX {ugx / 1_000:F0}K";
            return $"UGX {ugx:F0}";
        }

        /// <summary>
        /// Formats a cost with both UGX and USD.
        /// </summary>
        public static string FormatDualCurrency(double ugx)
        {
            var usd = ugx / UGX_PER_USD;
            return $"{FormatCost(ugx)} (${usd:N0} USD)";
        }

        private string ResolveWallRateKey(string wallType)
        {
            if (string.IsNullOrEmpty(wallType)) return "wall_200mm_block";

            var lower = wallType.ToLowerInvariant();
            if (lower.Contains("brick")) return "wall_200mm_brick";
            if (lower.Contains("partition") || lower.Contains("100")) return "wall_partition";
            if (lower.Contains("cavity")) return "wall_cavity";
            if (lower.Contains("150")) return "wall_150mm_block";
            return "wall_200mm_block";
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded) LoadRates();
        }
    }

    /// <summary>
    /// A unit cost rate from the cost database.
    /// </summary>
    public class CostRate
    {
        public string Description { get; }
        public double RateUGX { get; }
        public string Unit { get; }
        public string Category { get; }

        public CostRate(string description, double rateUGX, string unit, string category)
        {
            Description = description;
            RateUGX = rateUGX;
            Unit = unit;
            Category = category;
        }
    }

    /// <summary>
    /// Result of a cost estimation.
    /// </summary>
    public class CostEstimate
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double UnitRateUGX { get; set; }
        public double TotalUGX { get; set; }
        public double TotalUSD { get; set; }
        public string Category { get; set; }

        public string FormattedTotal => CostEstimator.FormatDualCurrency(TotalUGX);

        public static CostEstimate Unknown(string elementType, double quantity, string unit)
        {
            return new CostEstimate
            {
                Description = elementType,
                Quantity = quantity,
                Unit = unit,
                UnitRateUGX = 0,
                TotalUGX = 0,
                TotalUSD = 0,
                Category = "Unpriced"
            };
        }
    }
}
