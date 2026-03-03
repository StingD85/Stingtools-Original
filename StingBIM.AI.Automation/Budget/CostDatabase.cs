// StingBIM.AI.Automation.Budget.CostDatabase
// Regional construction cost database — reads CONSTRUCTION_COSTS_AFRICA.csv
// v4 Prompt Reference: Phase 7 — Budget Design + Exports

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NLog;

namespace StingBIM.AI.Automation.Budget
{
    /// <summary>
    /// Regional construction cost database loaded from CONSTRUCTION_COSTS_AFRICA.csv.
    /// Provides unit rate lookup by item code, category, region, and description.
    /// Supports 10 African regions with local currency rates.
    /// UGX is the primary currency per spec; USD secondary.
    /// </summary>
    public class RegionalCostDatabase
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<CostItem> _items = new List<CostItem>();
        private readonly Dictionary<string, CostItem> _byCode = new Dictionary<string, CostItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<CostItem>> _byCategory = new Dictionary<string, List<CostItem>>(StringComparer.OrdinalIgnoreCase);
        private bool _isLoaded;

        // Supported regions and their currency codes
        public static readonly Dictionary<string, string> RegionCurrencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Kenya", "KES" }, { "Uganda", "UGX" }, { "Tanzania", "TZS" },
            { "Rwanda", "RWF" }, { "Ethiopia", "ETB" }, { "Nigeria", "NGN" },
            { "Ghana", "GHS" }, { "SouthAfrica", "ZAR" }, { "Egypt", "EGP" },
            { "Morocco", "MAD" }
        };

        // Approximate USD exchange rates (2026 estimates)
        private static readonly Dictionary<string, decimal> UsdRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "KES", 155.0m }, { "UGX", 3800.0m }, { "TZS", 2600.0m },
            { "RWF", 1350.0m }, { "ETB", 120.0m }, { "NGN", 1600.0m },
            { "GHS", 15.5m }, { "ZAR", 18.5m }, { "EGP", 50.0m },
            { "MAD", 10.0m }, { "USD", 1.0m }
        };

        /// <summary>
        /// Load cost data from CSV file.
        /// Expected columns: ItemCode, Category, SubCategory, Description, Unit,
        /// then one column per region (Kenya_KES, Uganda_UGX, etc.).
        /// </summary>
        public void LoadFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Logger.Warn($"Cost database not found: {csvPath}");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length < 2)
                {
                    Logger.Warn("Cost CSV has no data rows");
                    return;
                }

                var headers = ParseCsvLine(lines[0]);
                var regionColumns = MapRegionColumns(headers);

                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var cols = ParseCsvLine(lines[i]);
                        if (cols.Length < 5) continue;

                        var item = new CostItem
                        {
                            ItemCode = cols[0].Trim(),
                            Category = cols[1].Trim(),
                            SubCategory = cols[2].Trim(),
                            Description = cols[3].Trim(),
                            Unit = cols[4].Trim(),
                            RegionalRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                        };

                        foreach (var (region, colIndex) in regionColumns)
                        {
                            if (colIndex < cols.Length)
                            {
                                var rateStr = cols[colIndex].Trim().Replace(",", "");
                                if (decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
                                {
                                    item.RegionalRates[region] = rate;
                                }
                            }
                        }

                        _items.Add(item);
                        _byCode[item.ItemCode] = item;

                        if (!_byCategory.ContainsKey(item.Category))
                            _byCategory[item.Category] = new List<CostItem>();
                        _byCategory[item.Category].Add(item);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Skipping cost CSV row {i}: {ex.Message}");
                    }
                }

                _isLoaded = true;
                Logger.Info($"Cost database loaded: {_items.Count} items from {csvPath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load cost database");
            }
        }

        /// <summary>
        /// Get the unit rate for an item in a specific region.
        /// </summary>
        public decimal GetRate(string itemCode, string region = "Uganda")
        {
            if (!_isLoaded) return 0;

            if (_byCode.TryGetValue(itemCode, out var item))
            {
                if (item.RegionalRates.TryGetValue(region, out var rate))
                    return rate;

                // Fallback: try Uganda as default
                if (item.RegionalRates.TryGetValue("Uganda", out var ugRate))
                    return ugRate;

                // Fallback: return first available rate
                return item.RegionalRates.Values.FirstOrDefault();
            }
            return 0;
        }

        /// <summary>
        /// Find the best matching cost item by description and category.
        /// Uses fuzzy matching when exact code is not available.
        /// </summary>
        public CostItem FindBestMatch(string description, string category = null)
        {
            if (!_isLoaded) return null;

            var descLower = description?.ToLowerInvariant() ?? "";

            // Try category filter first
            IEnumerable<CostItem> candidates = _items;
            if (!string.IsNullOrEmpty(category) && _byCategory.ContainsKey(category))
                candidates = _byCategory[category];

            // Exact description match
            var exact = candidates.FirstOrDefault(c =>
                c.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Contains match
            var contains = candidates.FirstOrDefault(c =>
                c.Description.ToLowerInvariant().Contains(descLower) ||
                descLower.Contains(c.Description.ToLowerInvariant()));
            if (contains != null) return contains;

            // Keyword overlap match
            var descWords = descLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bestMatch = candidates
                .Select(c => new
                {
                    Item = c,
                    Score = descWords.Count(w => c.Description.ToLowerInvariant().Contains(w))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return bestMatch?.Item;
        }

        /// <summary>
        /// Estimate cost for a BIM element category/type in a given region.
        /// Maps Revit categories to cost items automatically.
        /// </summary>
        public CostEstimate EstimateElementCost(string category, string typeName,
            double quantity, string unit, string region = "Uganda")
        {
            var estimate = new CostEstimate
            {
                Category = category,
                TypeName = typeName,
                Quantity = quantity,
                Unit = unit,
                Region = region
            };

            // Map Revit category to cost database category
            var costCategory = MapRevitCategoryToCost(category);
            var searchDesc = $"{costCategory} {typeName}".Trim();

            var match = FindBestMatch(searchDesc, costCategory);
            if (match != null)
            {
                var rate = match.RegionalRates.GetValueOrDefault(region, 0);
                estimate.UnitRate = rate;
                estimate.TotalCost = rate * (decimal)quantity;
                estimate.ItemCode = match.ItemCode;
                estimate.MatchedDescription = match.Description;
                estimate.Currency = RegionCurrencies.GetValueOrDefault(region, "UGX");
            }
            else
            {
                // Use category average as fallback
                estimate.UnitRate = GetCategoryAverage(costCategory, region);
                estimate.TotalCost = estimate.UnitRate * (decimal)quantity;
                estimate.Currency = RegionCurrencies.GetValueOrDefault(region, "UGX");
                estimate.IsEstimated = true;
            }

            return estimate;
        }

        /// <summary>
        /// Convert amount between currencies via USD.
        /// </summary>
        public decimal ConvertCurrency(decimal amount, string fromCurrency, string toCurrency)
        {
            if (fromCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase))
                return amount;

            var fromRate = UsdRates.GetValueOrDefault(fromCurrency, 1.0m);
            var toRate = UsdRates.GetValueOrDefault(toCurrency, 1.0m);

            if (fromRate == 0) return 0;
            var usd = amount / fromRate;
            return usd * toRate;
        }

        /// <summary>
        /// Get all items in a category.
        /// </summary>
        public List<CostItem> GetByCategory(string category)
        {
            return _byCategory.GetValueOrDefault(category, new List<CostItem>());
        }

        /// <summary>
        /// Get all available categories.
        /// </summary>
        public List<string> GetCategories()
        {
            return _byCategory.Keys.OrderBy(k => k).ToList();
        }

        /// <summary>
        /// Get all items.
        /// </summary>
        public List<CostItem> GetAll()
        {
            return _items.ToList();
        }

        public bool IsLoaded => _isLoaded;

        #region Private Helpers

        private decimal GetCategoryAverage(string category, string region)
        {
            if (!_byCategory.ContainsKey(category)) return 0;

            var rates = _byCategory[category]
                .Where(c => c.RegionalRates.ContainsKey(region))
                .Select(c => c.RegionalRates[region])
                .ToList();

            return rates.Count > 0 ? rates.Average() : 0;
        }

        private string MapRevitCategoryToCost(string revitCategory)
        {
            var cat = revitCategory?.ToLowerInvariant() ?? "";
            if (cat.Contains("wall")) return "Masonry";
            if (cat.Contains("floor") || cat.Contains("slab")) return "Concrete";
            if (cat.Contains("roof")) return "Roofing";
            if (cat.Contains("door")) return "Doors";
            if (cat.Contains("window")) return "Windows";
            if (cat.Contains("column") || cat.Contains("beam")) return "Steel";
            if (cat.Contains("ceiling")) return "Drywall";
            if (cat.Contains("pipe") || cat.Contains("plumbing")) return "Plumbing";
            if (cat.Contains("duct") || cat.Contains("hvac")) return "HVAC";
            if (cat.Contains("conduit") || cat.Contains("electrical")) return "Electrical";
            if (cat.Contains("paint") || cat.Contains("finish")) return "Paint";
            return "Concrete"; // Default
        }

        private Dictionary<string, int> MapRegionColumns(string[] headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 5; i < headers.Length; i++)
            {
                var h = headers[i].Trim();
                // Headers like "Kenya_KES", "Uganda_UGX"
                var parts = h.Split('_');
                if (parts.Length >= 1)
                {
                    var region = parts[0];
                    map[region] = i;
                }
            }
            return map;
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        #endregion
    }

    #region Cost Data Types

    public class CostItem
    {
        public string ItemCode { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public Dictionary<string, decimal> RegionalRates { get; set; } = new Dictionary<string, decimal>();
    }

    public class CostEstimate
    {
        public string Category { get; set; }
        public string TypeName { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public string Region { get; set; }
        public decimal UnitRate { get; set; }
        public decimal TotalCost { get; set; }
        public string Currency { get; set; }
        public string ItemCode { get; set; }
        public string MatchedDescription { get; set; }
        public bool IsEstimated { get; set; }
    }

    #endregion
}
