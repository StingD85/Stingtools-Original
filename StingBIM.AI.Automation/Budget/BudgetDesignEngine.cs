// StingBIM.AI.Automation.Budget.BudgetDesignEngine
// Budget-constrained design: budget → 3 design options with full BOQ
// v4 Prompt Reference: Phase 7 — Budget Design + Exports

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Automation.Budget
{
    /// <summary>
    /// Budget-constrained design engine.
    /// Given a budget, generates 3 design options (Economy, Standard, Premium)
    /// with full BOQ breakdown, cost comparison, and value engineering suggestions.
    /// </summary>
    public class BudgetDesignEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly RegionalCostDatabase _costDb;
        private string _region;
        private string _currency;

        // Tier cost multipliers relative to Standard
        private const double ECONOMY_FACTOR = 0.70;
        private const double STANDARD_FACTOR = 1.00;
        private const double PREMIUM_FACTOR = 1.45;

        // Cost breakdown percentages
        private const decimal PRELIMINARIES_RATE = 0.12m;
        private const decimal CONTINGENCY_RATE = 0.05m;
        private const decimal OVERHEADS_RATE = 0.08m;
        private const decimal PROFIT_RATE = 0.05m;
        private const decimal VAT_RATE = 0.18m; // Uganda 18%

        public BudgetDesignEngine(string costCsvPath = null, string region = "Uganda")
        {
            _costDb = new RegionalCostDatabase();
            _region = region;
            _currency = RegionalCostDatabase.RegionCurrencies.GetValueOrDefault(region, "UGX");

            if (!string.IsNullOrEmpty(costCsvPath) && File.Exists(costCsvPath))
            {
                _costDb.LoadFromCsv(costCsvPath);
            }
            else
            {
                // Try default path
                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StingBIM", "data", "ai", "costs", "CONSTRUCTION_COSTS_AFRICA.csv");
                if (File.Exists(defaultPath))
                    _costDb.LoadFromCsv(defaultPath);
            }
        }

        /// <summary>
        /// Generate 3 design options for a given budget and building program.
        /// </summary>
        public BudgetDesignResult GenerateOptions(
            decimal totalBudget, BuildingProgram program)
        {
            Logger.Info($"Generating budget options: {_currency} {totalBudget:N0} for {program.BuildingType}");

            var result = new BudgetDesignResult
            {
                TotalBudget = totalBudget,
                Currency = _currency,
                Region = _region,
                GeneratedAt = DateTime.Now
            };

            try
            {
                // Generate three tiers
                result.EconomyOption = GenerateOption("Economy", program, ECONOMY_FACTOR);
                result.StandardOption = GenerateOption("Standard", program, STANDARD_FACTOR);
                result.PremiumOption = GenerateOption("Premium", program, PREMIUM_FACTOR);

                // Check budget fit
                result.EconomyOption.WithinBudget = result.EconomyOption.GrandTotal <= totalBudget;
                result.StandardOption.WithinBudget = result.StandardOption.GrandTotal <= totalBudget;
                result.PremiumOption.WithinBudget = result.PremiumOption.GrandTotal <= totalBudget;

                // Generate value engineering if over budget
                if (!result.StandardOption.WithinBudget)
                {
                    result.ValueEngineeringSuggestions = GenerateValueEngineering(
                        result.StandardOption, totalBudget);
                }

                // Summary
                var bestFit = result.PremiumOption.WithinBudget ? "Premium"
                    : result.StandardOption.WithinBudget ? "Standard"
                    : result.EconomyOption.WithinBudget ? "Economy"
                    : "None (budget too low)";
                result.Summary = $"Budget: {_currency} {totalBudget:N0}\n" +
                    $"Best fit: {bestFit}\n" +
                    $"Economy: {_currency} {result.EconomyOption.GrandTotal:N0} " +
                    $"({(result.EconomyOption.WithinBudget ? "Within budget" : "Over")})\n" +
                    $"Standard: {_currency} {result.StandardOption.GrandTotal:N0} " +
                    $"({(result.StandardOption.WithinBudget ? "Within budget" : "Over")})\n" +
                    $"Premium: {_currency} {result.PremiumOption.GrandTotal:N0} " +
                    $"({(result.PremiumOption.WithinBudget ? "Within budget" : "Over")})";

                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Budget design generation failed");
                result.Summary = $"Error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Estimate construction cost for the current Revit model.
        /// </summary>
        public ModelCostEstimate EstimateModelCost(Document doc)
        {
            var estimate = new ModelCostEstimate
            {
                Region = _region,
                Currency = _currency,
                GeneratedAt = DateTime.Now
            };

            if (doc == null)
            {
                estimate.Summary = "No active model to estimate.";
                return estimate;
            }

            try
            {
                // Collect element quantities by category
                var categories = new[] { "Walls", "Floors", "Roofs", "Doors", "Windows",
                    "Structural Columns", "Structural Framing", "Ceilings", "Rooms" };

                foreach (var catName in categories)
                {
                    try
                    {
                        var builtIn = ResolveBuiltInCategory(catName);
                        if (builtIn == null) continue;

                        var collector = new FilteredElementCollector(doc)
                            .OfCategory(builtIn.Value)
                            .WhereElementIsNotElementType();

                        var elements = collector.ToList();
                        if (elements.Count == 0) continue;

                        var catEstimate = new CategoryCostEstimate
                        {
                            Category = catName,
                            ElementCount = elements.Count
                        };

                        // Group by type
                        var typeGroups = elements.GroupBy(e => e.Name ?? "Unknown");
                        foreach (var group in typeGroups)
                        {
                            var qty = ComputeQuantity(group.ToList(), catName);
                            var costEst = _costDb.EstimateElementCost(
                                catName, group.Key, qty.Quantity, qty.Unit, _region);

                            catEstimate.TypeEstimates.Add(new TypeCostEstimate
                            {
                                TypeName = group.Key,
                                Count = group.Count(),
                                Quantity = qty.Quantity,
                                Unit = qty.Unit,
                                UnitRate = costEst.UnitRate,
                                TotalCost = costEst.TotalCost,
                                IsEstimated = costEst.IsEstimated
                            });
                        }

                        catEstimate.SubTotal = catEstimate.TypeEstimates.Sum(t => t.TotalCost);
                        estimate.Categories.Add(catEstimate);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Skip category {catName}: {ex.Message}");
                    }
                }

                // Calculate totals
                estimate.DirectCost = estimate.Categories.Sum(c => c.SubTotal);
                estimate.Preliminaries = estimate.DirectCost * PRELIMINARIES_RATE;
                estimate.Contingency = estimate.DirectCost * CONTINGENCY_RATE;
                estimate.Overheads = estimate.DirectCost * OVERHEADS_RATE;
                estimate.Profit = estimate.DirectCost * PROFIT_RATE;
                estimate.SubTotal = estimate.DirectCost + estimate.Preliminaries +
                    estimate.Contingency + estimate.Overheads + estimate.Profit;
                estimate.VAT = estimate.SubTotal * VAT_RATE;
                estimate.GrandTotal = estimate.SubTotal + estimate.VAT;

                // Cost per m² (if floor area available)
                var totalFloorArea = ComputeTotalFloorArea(doc);
                if (totalFloorArea > 0)
                    estimate.CostPerSquareMeter = estimate.GrandTotal / (decimal)totalFloorArea;

                estimate.Summary = $"Estimated Construction Cost: {_currency} {estimate.GrandTotal:N0}\n" +
                    $"Direct cost: {_currency} {estimate.DirectCost:N0}\n" +
                    $"Preliminaries (12%): {_currency} {estimate.Preliminaries:N0}\n" +
                    $"Contingency (5%): {_currency} {estimate.Contingency:N0}\n" +
                    $"Overheads & Profit (13%): {_currency} {(estimate.Overheads + estimate.Profit):N0}\n" +
                    $"VAT (18%): {_currency} {estimate.VAT:N0}\n" +
                    (totalFloorArea > 0
                        ? $"Cost per m²: {_currency} {estimate.CostPerSquareMeter:N0}"
                        : "");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Model cost estimation failed");
                estimate.Summary = $"Error: {ex.Message}";
            }

            return estimate;
        }

        /// <summary>
        /// Check if cumulative cost is approaching budget threshold (80%).
        /// Used by ProactiveAdvisor after each element creation.
        /// </summary>
        public BudgetAlert CheckBudgetAlert(decimal cumulativeCost, decimal projectBudget)
        {
            if (projectBudget <= 0) return null;

            var percentage = cumulativeCost / projectBudget * 100;

            if (percentage >= 100)
            {
                return new BudgetAlert
                {
                    Severity = AlertSeverity.Critical,
                    Percentage = percentage,
                    Message = $"Budget EXCEEDED: {_currency} {cumulativeCost:N0} of {_currency} {projectBudget:N0} " +
                        $"({percentage:F0}%). Over by {_currency} {(cumulativeCost - projectBudget):N0}.",
                    Suggestions = new List<string>
                    {
                        "Value engineer", "Reduce scope", "Review cost breakdown"
                    }
                };
            }

            if (percentage >= 80)
            {
                var remaining = projectBudget - cumulativeCost;
                return new BudgetAlert
                {
                    Severity = AlertSeverity.Warning,
                    Percentage = percentage,
                    Message = $"Budget Alert: {percentage:F0}% used ({_currency} {cumulativeCost:N0} " +
                        $"of {_currency} {projectBudget:N0}). Remaining: {_currency} {remaining:N0}.",
                    Suggestions = new List<string>
                    {
                        "Value engineer", "Reduce scope", "Ignore"
                    }
                };
            }

            return null;
        }

        #region Private Helpers

        private DesignOption GenerateOption(string tier, BuildingProgram program, double costFactor)
        {
            var option = new DesignOption
            {
                TierName = tier,
                Description = GetTierDescription(tier)
            };

            // Calculate per-room costs based on room types and areas
            foreach (var room in program.Rooms)
            {
                var area = room.AreaM2 > 0 ? room.AreaM2 : GetDefaultArea(room.RoomType);

                // Estimate cost per m² for this room type
                var baseCostPerM2 = GetBaseCostPerM2(room.RoomType);
                var tieredCost = baseCostPerM2 * (decimal)costFactor;
                var roomCost = tieredCost * (decimal)area;

                option.LineItems.Add(new BudgetLineItem
                {
                    Category = room.RoomType,
                    Description = $"{room.Name ?? room.RoomType} ({area:F0} m²)",
                    Quantity = area,
                    Unit = "m²",
                    UnitRate = tieredCost,
                    Amount = roomCost,
                    Specification = GetTierSpec(tier, room.RoomType)
                });
            }

            // Add site works, externals, services
            var totalBuildingCost = option.LineItems.Sum(l => l.Amount);
            option.LineItems.Add(new BudgetLineItem
            {
                Category = "Site Works",
                Description = "Site preparation, drainage, landscaping",
                Quantity = 1,
                Unit = "item",
                UnitRate = totalBuildingCost * 0.08m,
                Amount = totalBuildingCost * 0.08m,
                Specification = tier == "Premium" ? "Full landscaping + irrigation" : "Basic site works"
            });
            option.LineItems.Add(new BudgetLineItem
            {
                Category = "MEP Services",
                Description = "Electrical, plumbing, HVAC, fire protection",
                Quantity = 1,
                Unit = "item",
                UnitRate = totalBuildingCost * 0.25m,
                Amount = totalBuildingCost * 0.25m,
                Specification = GetMEPSpec(tier)
            });

            // Totals
            option.DirectCost = option.LineItems.Sum(l => l.Amount);
            option.Preliminaries = option.DirectCost * PRELIMINARIES_RATE;
            option.Contingency = option.DirectCost * CONTINGENCY_RATE;
            option.Overheads = option.DirectCost * OVERHEADS_RATE;
            option.Profit = option.DirectCost * PROFIT_RATE;
            option.SubTotal = option.DirectCost + option.Preliminaries +
                option.Contingency + option.Overheads + option.Profit;
            option.VAT = option.SubTotal * VAT_RATE;
            option.GrandTotal = option.SubTotal + option.VAT;

            return option;
        }

        private List<ValueEngineeringSuggestion> GenerateValueEngineering(
            DesignOption standardOption, decimal budget)
        {
            var suggestions = new List<ValueEngineeringSuggestion>();
            var overBudget = standardOption.GrandTotal - budget;

            // Sort line items by amount descending — largest savings potential first
            var sortedItems = standardOption.LineItems
                .OrderByDescending(l => l.Amount).ToList();

            decimal cumulativeSaving = 0;
            foreach (var item in sortedItems)
            {
                if (cumulativeSaving >= overBudget) break;

                var saving = item.Amount * 0.15m; // Assume 15% savings possible per line
                suggestions.Add(new ValueEngineeringSuggestion
                {
                    Category = item.Category,
                    CurrentSpec = item.Specification,
                    ProposedSpec = GetEconomyAlternative(item.Category),
                    PotentialSaving = saving,
                    ImpactLevel = saving > overBudget * 0.3m ? "High" : "Medium"
                });
                cumulativeSaving += saving;
            }

            return suggestions;
        }

        private decimal GetBaseCostPerM2(string roomType)
        {
            // Base cost per m² in UGX for Standard tier
            var type = roomType?.ToLowerInvariant() ?? "";
            if (type.Contains("bedroom")) return 1_800_000m;
            if (type.Contains("bathroom") || type.Contains("ensuite")) return 2_500_000m;
            if (type.Contains("kitchen")) return 2_200_000m;
            if (type.Contains("living") || type.Contains("lounge")) return 1_600_000m;
            if (type.Contains("dining")) return 1_500_000m;
            if (type.Contains("office")) return 2_000_000m;
            if (type.Contains("corridor") || type.Contains("hall")) return 1_200_000m;
            if (type.Contains("store") || type.Contains("storage")) return 1_000_000m;
            if (type.Contains("garage")) return 900_000m;
            if (type.Contains("conference") || type.Contains("meeting")) return 2_100_000m;
            if (type.Contains("reception") || type.Contains("lobby")) return 1_800_000m;
            return 1_500_000m; // Default
        }

        private double GetDefaultArea(string roomType)
        {
            var type = roomType?.ToLowerInvariant() ?? "";
            if (type.Contains("master") && type.Contains("bed")) return 20;
            if (type.Contains("bedroom")) return 14;
            if (type.Contains("bathroom")) return 6;
            if (type.Contains("ensuite")) return 5;
            if (type.Contains("kitchen")) return 12;
            if (type.Contains("living")) return 24;
            if (type.Contains("dining")) return 16;
            if (type.Contains("office")) return 15;
            if (type.Contains("corridor")) return 8;
            if (type.Contains("store")) return 4;
            if (type.Contains("garage")) return 36;
            return 12;
        }

        private string GetTierDescription(string tier)
        {
            return tier switch
            {
                "Economy" => "Basic finishes, standard fixtures, minimal landscaping. " +
                    "Block walls, cement screed floors, basic plumbing.",
                "Standard" => "Good quality finishes, branded fixtures, standard landscaping. " +
                    "Plastered walls, ceramic tile floors, quality sanitary ware.",
                "Premium" => "High-end finishes, premium fixtures, full landscaping + automation. " +
                    "Skim-coat walls, porcelain tiles, imported sanitary ware, smart home.",
                _ => ""
            };
        }

        private string GetTierSpec(string tier, string roomType)
        {
            var rt = roomType?.ToLowerInvariant() ?? "";
            return tier switch
            {
                "Economy" when rt.Contains("bed") => "Cement screed floor, emulsion paint, timber door",
                "Economy" when rt.Contains("bath") => "Ceramic tiles, basic fixtures, PVC door",
                "Economy" when rt.Contains("kitchen") => "Granite worktop, basic cabinets, ceramic splashback",
                "Economy" => "Cement screed, emulsion paint, basic fixtures",
                "Standard" when rt.Contains("bed") => "Ceramic tile floor, silk paint, flush door",
                "Standard" when rt.Contains("bath") => "Full porcelain tiles, branded fixtures, aluminium door",
                "Standard" when rt.Contains("kitchen") => "Granite worktop, custom cabinets, glass splashback",
                "Standard" => "Ceramic tiles, quality paint, standard fixtures",
                "Premium" when rt.Contains("bed") => "Porcelain tile, designer paint, solid wood door, AC",
                "Premium" when rt.Contains("bath") => "Italian porcelain, Grohe/Duravit fixtures, frameless shower",
                "Premium" when rt.Contains("kitchen") => "Quartz worktop, German cabinets, under-cabinet lighting",
                "Premium" => "Premium tiles, designer finishes, smart controls",
                _ => "Standard specification"
            };
        }

        private string GetMEPSpec(string tier)
        {
            return tier switch
            {
                "Economy" => "Basic electrical, gravity plumbing, natural ventilation, manual fire alarm",
                "Standard" => "Full electrical with DB, pressurised plumbing, split AC in bedrooms, fire alarm + detectors",
                "Premium" => "Smart electrical, full HVAC, solar PV, fire sprinklers, home automation, CCTV",
                _ => ""
            };
        }

        private string GetEconomyAlternative(string category)
        {
            var cat = category?.ToLowerInvariant() ?? "";
            if (cat.Contains("bed")) return "Reduce to cement screed, smaller room";
            if (cat.Contains("bath")) return "Basic tiles, local fixtures, smaller layout";
            if (cat.Contains("kitchen")) return "Formica worktop, basic cabinets";
            if (cat.Contains("mep")) return "Reduce AC scope, basic electrical only";
            if (cat.Contains("site")) return "Minimal landscaping, gravel driveway";
            return "Use economy-grade materials";
        }

        private BuiltInCategory? ResolveBuiltInCategory(string name)
        {
            return name switch
            {
                "Walls" => BuiltInCategory.OST_Walls,
                "Floors" => BuiltInCategory.OST_Floors,
                "Roofs" => BuiltInCategory.OST_Roofs,
                "Doors" => BuiltInCategory.OST_Doors,
                "Windows" => BuiltInCategory.OST_Windows,
                "Structural Columns" => BuiltInCategory.OST_StructuralColumns,
                "Structural Framing" => BuiltInCategory.OST_StructuralFraming,
                "Ceilings" => BuiltInCategory.OST_Ceilings,
                "Rooms" => BuiltInCategory.OST_Rooms,
                _ => null
            };
        }

        private (double Quantity, string Unit) ComputeQuantity(List<Element> elements, string category)
        {
            var cat = category.ToLowerInvariant();
            if (cat.Contains("wall") || cat.Contains("floor") || cat.Contains("roof") || cat.Contains("ceiling"))
            {
                double totalArea = 0;
                foreach (var e in elements)
                {
                    var areaParam = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null)
                        totalArea += areaParam.AsDouble() * 0.0929; // sq ft → m²
                }
                return (totalArea, "m²");
            }
            if (cat.Contains("column") || cat.Contains("beam") || cat.Contains("framing"))
            {
                double totalVol = 0;
                foreach (var e in elements)
                {
                    var volParam = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (volParam != null)
                        totalVol += volParam.AsDouble() * 0.0283; // cu ft → m³
                }
                return (totalVol > 0 ? totalVol : elements.Count, totalVol > 0 ? "m³" : "nr");
            }
            return (elements.Count, "nr");
        }

        private double ComputeTotalFloorArea(Document doc)
        {
            try
            {
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .ToList();

                double total = 0;
                foreach (var r in rooms)
                {
                    var areaParam = r.get_Parameter(BuiltInParameter.ROOM_AREA);
                    if (areaParam != null)
                        total += areaParam.AsDouble() * 0.0929;
                }
                return total;
            }
            catch { return 0; }
        }

        #endregion
    }

    #region Budget Data Types

    public class BudgetDesignResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; }
        public decimal TotalBudget { get; set; }
        public string Currency { get; set; }
        public string Region { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DesignOption EconomyOption { get; set; }
        public DesignOption StandardOption { get; set; }
        public DesignOption PremiumOption { get; set; }
        public List<ValueEngineeringSuggestion> ValueEngineeringSuggestions { get; set; }
            = new List<ValueEngineeringSuggestion>();
    }

    public class DesignOption
    {
        public string TierName { get; set; }
        public string Description { get; set; }
        public List<BudgetLineItem> LineItems { get; set; } = new List<BudgetLineItem>();
        public decimal DirectCost { get; set; }
        public decimal Preliminaries { get; set; }
        public decimal Contingency { get; set; }
        public decimal Overheads { get; set; }
        public decimal Profit { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VAT { get; set; }
        public decimal GrandTotal { get; set; }
        public bool WithinBudget { get; set; }
    }

    public class BudgetLineItem
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitRate { get; set; }
        public decimal Amount { get; set; }
        public string Specification { get; set; }
    }

    public class ValueEngineeringSuggestion
    {
        public string Category { get; set; }
        public string CurrentSpec { get; set; }
        public string ProposedSpec { get; set; }
        public decimal PotentialSaving { get; set; }
        public string ImpactLevel { get; set; }
    }

    public class BuildingProgram
    {
        public string BuildingType { get; set; } = "Residential";
        public List<RoomBrief> Rooms { get; set; } = new List<RoomBrief>();
        public int Stories { get; set; } = 1;
    }

    public class RoomBrief
    {
        public string RoomType { get; set; }
        public string Name { get; set; }
        public double AreaM2 { get; set; }
    }

    public class ModelCostEstimate
    {
        public string Summary { get; set; }
        public string Region { get; set; }
        public string Currency { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<CategoryCostEstimate> Categories { get; set; } = new List<CategoryCostEstimate>();
        public decimal DirectCost { get; set; }
        public decimal Preliminaries { get; set; }
        public decimal Contingency { get; set; }
        public decimal Overheads { get; set; }
        public decimal Profit { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VAT { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal CostPerSquareMeter { get; set; }
    }

    public class CategoryCostEstimate
    {
        public string Category { get; set; }
        public int ElementCount { get; set; }
        public List<TypeCostEstimate> TypeEstimates { get; set; } = new List<TypeCostEstimate>();
        public decimal SubTotal { get; set; }
    }

    public class TypeCostEstimate
    {
        public string TypeName { get; set; }
        public int Count { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitRate { get; set; }
        public decimal TotalCost { get; set; }
        public bool IsEstimated { get; set; }
    }

    public class BudgetAlert
    {
        public AlertSeverity Severity { get; set; }
        public decimal Percentage { get; set; }
        public string Message { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    #endregion
}
