// ============================================================================
// StingBIM AI - Automated Quantity Takeoff
// Automatically extracts and calculates quantities from BIM models
// Generates BOQs with regional cost databases integration
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Automation.Quantities
{
    /// <summary>
    /// Automated Quantity Takeoff Engine
    /// Extracts quantities from BIM model and generates structured BOQs
    /// </summary>
    public class AutomatedQuantityTakeoff
    {
        private readonly QuantityExtractor _extractor;
        private readonly MeasurementRules _measurementRules;
        private readonly CostDatabase _costDatabase;
        private readonly BOQFormatter _formatter;
        private readonly WasteFactors _wasteFactors;

        public AutomatedQuantityTakeoff()
        {
            _extractor = new QuantityExtractor();
            _measurementRules = new MeasurementRules();
            _costDatabase = new CostDatabase();
            _formatter = new BOQFormatter();
            _wasteFactors = new WasteFactors();
        }

        #region Quantity Extraction

        /// <summary>
        /// Generate complete quantity takeoff from model
        /// </summary>
        public async Task<QuantityTakeoffResult> GenerateTakeoffAsync(
            BIMModel model,
            TakeoffOptions options = null)
        {
            options ??= TakeoffOptions.Default;

            var result = new QuantityTakeoffResult
            {
                ModelId = model.ModelId,
                GeneratedAt = DateTime.UtcNow,
                MeasurementStandard = options.MeasurementStandard
            };

            // Step 1: Extract raw quantities by category
            var rawQuantities = await ExtractQuantitiesAsync(model, options);
            result.RawQuantities = rawQuantities;

            // Step 2: Apply measurement rules
            var measuredQuantities = ApplyMeasurementRules(rawQuantities, options);

            // Step 3: Apply waste factors
            var adjustedQuantities = ApplyWasteFactors(measuredQuantities, options);

            // Step 4: Group by work section (SMM7/NRM/POMI)
            result.WorkSections = GroupByWorkSection(adjustedQuantities, options.MeasurementStandard);

            // Step 5: Apply costs if requested
            if (options.IncludeCosts)
            {
                await ApplyCostsAsync(result.WorkSections, options);
                result.CostSummary = CalculateCostSummary(result.WorkSections);
            }

            // Step 6: Generate statistics
            result.Statistics = GenerateStatistics(result);

            return result;
        }

        /// <summary>
        /// Extract quantities for specific category
        /// </summary>
        public async Task<CategoryQuantities> ExtractCategoryQuantitiesAsync(
            BIMModel model,
            string category,
            TakeoffOptions options = null)
        {
            options ??= TakeoffOptions.Default;

            var quantities = new CategoryQuantities
            {
                Category = category,
                ExtractedAt = DateTime.UtcNow
            };

            var elements = model.GetElementsByCategory(category);

            foreach (var element in elements)
            {
                var itemQuantity = await ExtractElementQuantityAsync(element, options);
                quantities.Items.Add(itemQuantity);
            }

            // Summarize by type
            quantities.TypeSummaries = quantities.Items
                .GroupBy(i => i.TypeName)
                .Select(g => new TypeQuantitySummary
                {
                    TypeName = g.Key,
                    Count = g.Count(),
                    TotalArea = g.Sum(i => i.Area ?? 0),
                    TotalVolume = g.Sum(i => i.Volume ?? 0),
                    TotalLength = g.Sum(i => i.Length ?? 0)
                })
                .ToList();

            return quantities;
        }

        private async Task<Dictionary<string, CategoryQuantities>> ExtractQuantitiesAsync(
            BIMModel model,
            TakeoffOptions options)
        {
            var quantities = new Dictionary<string, CategoryQuantities>();

            var categories = options.CategoriesToExtract ?? GetDefaultCategories();

            foreach (var category in categories)
            {
                var categoryQuantities = await ExtractCategoryQuantitiesAsync(model, category, options);
                quantities[category] = categoryQuantities;
            }

            return quantities;
        }

        private async Task<QuantityItem> ExtractElementQuantityAsync(
            BIMElement element,
            TakeoffOptions options)
        {
            await Task.Delay(1); // Simulate async operation

            var item = new QuantityItem
            {
                ElementId = element.ElementId,
                Category = element.Category,
                Family = element.Family,
                TypeName = element.TypeName,
                Level = element.Level,
                Phase = element.Phase
            };

            // Extract quantities based on category
            switch (element.Category)
            {
                case "Walls":
                    item.Area = element.GetParameter("Area");
                    item.Volume = element.GetParameter("Volume");
                    item.Length = element.GetParameter("Length");
                    item.Height = element.GetParameter("Height");
                    item.Thickness = element.GetParameter("Width");
                    break;

                case "Floors":
                case "Ceilings":
                case "Roofs":
                    item.Area = element.GetParameter("Area");
                    item.Volume = element.GetParameter("Volume");
                    item.Thickness = element.GetParameter("Thickness");
                    item.Perimeter = element.GetParameter("Perimeter");
                    break;

                case "Doors":
                case "Windows":
                    item.Count = 1;
                    item.Width = element.GetParameter("Width");
                    item.Height = element.GetParameter("Height");
                    item.Area = (item.Width ?? 0) * (item.Height ?? 0) / 1000000; // Convert to m²
                    break;

                case "Structural Columns":
                case "Structural Beams":
                    item.Volume = element.GetParameter("Volume");
                    item.Length = element.GetParameter("Length");
                    item.Weight = CalculateWeight(element);
                    break;

                case "Ducts":
                case "Pipes":
                case "Cable Trays":
                    item.Length = element.GetParameter("Length");
                    item.Area = element.GetParameter("Area"); // Surface area
                    item.Size = element.GetParameter("Size")?.ToString();
                    break;

                default:
                    item.Count = 1;
                    item.Area = element.GetParameter("Area");
                    item.Volume = element.GetParameter("Volume");
                    break;
            }

            // Get material information
            item.Material = element.GetParameter("Material");

            return item;
        }

        private double? CalculateWeight(BIMElement element)
        {
            var volume = element.GetParameter("Volume");
            var density = GetMaterialDensity(element.GetParameter("Material")?.ToString());

            if (volume.HasValue && density.HasValue)
            {
                return volume.Value * density.Value;
            }

            return null;
        }

        private double? GetMaterialDensity(string material)
        {
            if (string.IsNullOrEmpty(material)) return null;

            var densities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "Concrete", 2400 },
                { "Steel", 7850 },
                { "Timber", 600 },
                { "Brick", 1800 },
                { "Block", 1400 },
                { "Aluminum", 2700 },
                { "Glass", 2500 }
            };

            foreach (var kvp in densities)
            {
                if (material.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        #endregion

        #region Measurement Rules

        private Dictionary<string, CategoryQuantities> ApplyMeasurementRules(
            Dictionary<string, CategoryQuantities> rawQuantities,
            TakeoffOptions options)
        {
            var measured = new Dictionary<string, CategoryQuantities>();

            foreach (var kvp in rawQuantities)
            {
                var category = kvp.Key;
                var quantities = kvp.Value;

                var rules = _measurementRules.GetRulesForCategory(category, options.MeasurementStandard);

                foreach (var item in quantities.Items)
                {
                    ApplyRulesToItem(item, rules);
                }

                measured[category] = quantities;
            }

            return measured;
        }

        private void ApplyRulesToItem(QuantityItem item, List<MeasurementRule> rules)
        {
            foreach (var rule in rules)
            {
                switch (rule.RuleType)
                {
                    case MeasurementRuleType.DeductOpenings:
                        // Deduct openings > threshold from wall areas
                        if (item.OpeningArea.HasValue && rule.Threshold.HasValue)
                        {
                            if (item.OpeningArea.Value > rule.Threshold.Value)
                            {
                                item.Area = (item.Area ?? 0) - item.OpeningArea.Value;
                                item.Deductions.Add($"Opening deduction: {item.OpeningArea:F2} m²");
                            }
                        }
                        break;

                    case MeasurementRuleType.RoundUp:
                        // Round to nearest unit
                        if (item.Length.HasValue && rule.RoundingUnit.HasValue)
                        {
                            item.Length = Math.Ceiling(item.Length.Value / rule.RoundingUnit.Value) * rule.RoundingUnit.Value;
                        }
                        break;

                    case MeasurementRuleType.MinimumQuantity:
                        // Apply minimum quantity
                        if (rule.MinimumValue.HasValue)
                        {
                            if (item.Area.HasValue && item.Area.Value < rule.MinimumValue.Value)
                            {
                                item.Area = rule.MinimumValue.Value;
                                item.Adjustments.Add($"Minimum area applied: {rule.MinimumValue} m²");
                            }
                        }
                        break;
                }
            }
        }

        #endregion

        #region Waste Factors

        private Dictionary<string, CategoryQuantities> ApplyWasteFactors(
            Dictionary<string, CategoryQuantities> quantities,
            TakeoffOptions options)
        {
            if (!options.ApplyWasteFactors) return quantities;

            foreach (var kvp in quantities)
            {
                var category = kvp.Key;
                var wasteFactor = _wasteFactors.GetFactor(category);

                foreach (var item in kvp.Value.Items)
                {
                    item.WasteFactor = wasteFactor;

                    if (item.Area.HasValue)
                        item.AdjustedArea = item.Area.Value * (1 + wasteFactor);

                    if (item.Volume.HasValue)
                        item.AdjustedVolume = item.Volume.Value * (1 + wasteFactor);

                    if (item.Length.HasValue)
                        item.AdjustedLength = item.Length.Value * (1 + wasteFactor);
                }
            }

            return quantities;
        }

        #endregion

        #region BOQ Generation

        private List<WorkSection> GroupByWorkSection(
            Dictionary<string, CategoryQuantities> quantities,
            MeasurementStandard standard)
        {
            var sections = new List<WorkSection>();

            // Get work breakdown structure for standard
            var wbs = GetWorkBreakdownStructure(standard);

            foreach (var section in wbs)
            {
                var workSection = new WorkSection
                {
                    Code = section.Code,
                    Title = section.Title,
                    Description = section.Description
                };

                // Map quantities to this section
                foreach (var mapping in section.CategoryMappings)
                {
                    if (quantities.TryGetValue(mapping.Category, out var catQuantities))
                    {
                        var sectionItems = catQuantities.Items
                            .Where(i => mapping.TypeFilter == null ||
                                       i.TypeName.Contains(mapping.TypeFilter, StringComparison.OrdinalIgnoreCase))
                            .Select(i => ConvertToWorkItem(i, mapping.Unit, mapping.Description))
                            .ToList();

                        workSection.Items.AddRange(sectionItems);
                    }
                }

                if (workSection.Items.Any())
                {
                    sections.Add(workSection);
                }
            }

            return sections;
        }

        private WorkItem ConvertToWorkItem(QuantityItem source, string unit, string description)
        {
            var quantity = unit switch
            {
                "m²" => source.AdjustedArea ?? source.Area ?? 0,
                "m³" => source.AdjustedVolume ?? source.Volume ?? 0,
                "m" => source.AdjustedLength ?? source.Length ?? 0,
                "nr" => source.Count ?? 1,
                "kg" => source.Weight ?? 0,
                "t" => (source.Weight ?? 0) / 1000,
                _ => source.Count ?? 1
            };

            return new WorkItem
            {
                ItemRef = source.ElementId,
                Description = description ?? $"{source.Family} - {source.TypeName}",
                Unit = unit,
                Quantity = quantity,
                Level = source.Level,
                Material = source.Material?.ToString()
            };
        }

        private List<WorkBreakdownSection> GetWorkBreakdownStructure(MeasurementStandard standard)
        {
            return standard switch
            {
                MeasurementStandard.NRM2 => GetNRM2Structure(),
                MeasurementStandard.SMM7 => GetSMM7Structure(),
                MeasurementStandard.POMI => GetPOMIStructure(),
                _ => GetGenericStructure()
            };
        }

        private List<WorkBreakdownSection> GetNRM2Structure()
        {
            return new List<WorkBreakdownSection>
            {
                new WorkBreakdownSection { Code = "1", Title = "Preliminaries", Description = "Employer's requirements and contractor's preliminaries" },
                new WorkBreakdownSection { Code = "2", Title = "Off-site manufactured materials", Description = "Prefabricated components" },
                new WorkBreakdownSection { Code = "3", Title = "Demolitions", Description = "Demolition and alteration work", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Demolition" } } },
                new WorkBreakdownSection { Code = "4", Title = "Ground works", Description = "Site preparation and earthworks" },
                new WorkBreakdownSection { Code = "5", Title = "In-situ concrete works", Description = "Cast in-place concrete", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Structural Foundations", Unit = "m³", Description = "Concrete foundations" },
                      new CategoryMapping { Category = "Floors", TypeFilter = "Concrete", Unit = "m³", Description = "Concrete floor slabs" } } },
                new WorkBreakdownSection { Code = "6", Title = "Precast concrete", Description = "Precast/composite concrete" },
                new WorkBreakdownSection { Code = "7", Title = "Masonry", Description = "Brick and block work", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Walls", TypeFilter = "Brick", Unit = "m²", Description = "Brick walls" },
                      new CategoryMapping { Category = "Walls", TypeFilter = "Block", Unit = "m²", Description = "Block walls" } } },
                new WorkBreakdownSection { Code = "8", Title = "Structural metalwork", Description = "Structural steel", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Structural Columns", Unit = "kg", Description = "Steel columns" },
                      new CategoryMapping { Category = "Structural Beams", Unit = "kg", Description = "Steel beams" } } },
                new WorkBreakdownSection { Code = "9", Title = "Cladding and covering", Description = "External envelope" },
                new WorkBreakdownSection { Code = "10", Title = "Waterproofing", Description = "Tanking and damp-proofing" },
                new WorkBreakdownSection { Code = "11", Title = "Roof coverings", Description = "Roof finishes", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Roofs", Unit = "m²", Description = "Roof covering" } } },
                new WorkBreakdownSection { Code = "12", Title = "Carpentry", Description = "Timber work" },
                new WorkBreakdownSection { Code = "13", Title = "Structural timber", Description = "Structural timber frame" },
                new WorkBreakdownSection { Code = "14", Title = "Windows, screens and lights", Description = "Windows and glazing", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Windows", Unit = "nr", Description = "Windows" },
                      new CategoryMapping { Category = "Curtain Walls", Unit = "m²", Description = "Curtain wall glazing" } } },
                new WorkBreakdownSection { Code = "15", Title = "Doors", Description = "Internal and external doors", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Doors", Unit = "nr", Description = "Doors" } } },
                new WorkBreakdownSection { Code = "16", Title = "Stairs, walkways and balustrades", Description = "Stairs and railings", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Stairs", Unit = "nr", Description = "Staircases" },
                      new CategoryMapping { Category = "Railings", Unit = "m", Description = "Balustrades and handrails" } } },
                new WorkBreakdownSection { Code = "17", Title = "Ceilings", Description = "Ceiling finishes", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Ceilings", Unit = "m²", Description = "Ceiling" } } },
                new WorkBreakdownSection { Code = "18", Title = "Wall finishes", Description = "Internal wall finishes", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Walls", TypeFilter = "Finish", Unit = "m²", Description = "Wall finishes" } } },
                new WorkBreakdownSection { Code = "19", Title = "Floor finishes", Description = "Floor finishes", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Floors", TypeFilter = "Finish", Unit = "m²", Description = "Floor finishes" } } },
                new WorkBreakdownSection { Code = "20", Title = "Mechanical services", Description = "HVAC and plumbing", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Ducts", Unit = "m²", Description = "Ductwork" },
                      new CategoryMapping { Category = "Pipes", Unit = "m", Description = "Pipework" },
                      new CategoryMapping { Category = "Mechanical Equipment", Unit = "nr", Description = "Mechanical equipment" },
                      new CategoryMapping { Category = "Plumbing Fixtures", Unit = "nr", Description = "Sanitary fixtures" } } },
                new WorkBreakdownSection { Code = "21", Title = "Electrical services", Description = "Electrical installation", CategoryMappings = new List<CategoryMapping>
                    { new CategoryMapping { Category = "Cable Trays", Unit = "m", Description = "Cable containment" },
                      new CategoryMapping { Category = "Electrical Fixtures", Unit = "nr", Description = "Electrical fixtures" },
                      new CategoryMapping { Category = "Lighting Fixtures", Unit = "nr", Description = "Light fittings" } } }
            };
        }

        private List<WorkBreakdownSection> GetSMM7Structure()
        {
            // Standard Method of Measurement 7th Edition
            return new List<WorkBreakdownSection>
            {
                new WorkBreakdownSection { Code = "A", Title = "Preliminaries/General conditions" },
                new WorkBreakdownSection { Code = "C", Title = "Demolition/Alteration/Renovation" },
                new WorkBreakdownSection { Code = "D", Title = "Groundwork" },
                new WorkBreakdownSection { Code = "E", Title = "In situ concrete/Large precast concrete" },
                new WorkBreakdownSection { Code = "F", Title = "Masonry" },
                new WorkBreakdownSection { Code = "G", Title = "Structural/Carcassing metal/timber" },
                new WorkBreakdownSection { Code = "H", Title = "Cladding/Covering" },
                new WorkBreakdownSection { Code = "J", Title = "Waterproofing" },
                new WorkBreakdownSection { Code = "K", Title = "Linings/Sheathing/Dry partitioning" },
                new WorkBreakdownSection { Code = "L", Title = "Windows/Doors/Stairs" },
                new WorkBreakdownSection { Code = "M", Title = "Surface finishes" },
                new WorkBreakdownSection { Code = "N", Title = "Furniture/Equipment" },
                new WorkBreakdownSection { Code = "P", Title = "Building fabric sundries" },
                new WorkBreakdownSection { Code = "Q", Title = "Paving/Planting/Fencing/Site furniture" },
                new WorkBreakdownSection { Code = "R", Title = "Disposal systems" },
                new WorkBreakdownSection { Code = "S", Title = "Piped supply systems" },
                new WorkBreakdownSection { Code = "T", Title = "Mechanical heating/cooling/refrigeration" },
                new WorkBreakdownSection { Code = "U", Title = "Ventilation/Air conditioning" },
                new WorkBreakdownSection { Code = "V", Title = "Electrical supply/power/lighting" },
                new WorkBreakdownSection { Code = "W", Title = "Communications/Security/Control" },
                new WorkBreakdownSection { Code = "X", Title = "Transport" },
                new WorkBreakdownSection { Code = "Y", Title = "Mechanical/Electrical services measurement" }
            };
        }

        private List<WorkBreakdownSection> GetPOMIStructure()
        {
            // Principles of Measurement International
            return new List<WorkBreakdownSection>
            {
                new WorkBreakdownSection { Code = "01", Title = "Substructure" },
                new WorkBreakdownSection { Code = "02", Title = "Superstructure" },
                new WorkBreakdownSection { Code = "03", Title = "Finishes" },
                new WorkBreakdownSection { Code = "04", Title = "Fittings and furnishings" },
                new WorkBreakdownSection { Code = "05", Title = "Services" },
                new WorkBreakdownSection { Code = "06", Title = "External works" },
                new WorkBreakdownSection { Code = "07", Title = "Preliminaries" }
            };
        }

        private List<WorkBreakdownSection> GetGenericStructure()
        {
            return new List<WorkBreakdownSection>
            {
                new WorkBreakdownSection { Code = "A", Title = "Substructure" },
                new WorkBreakdownSection { Code = "B", Title = "Superstructure" },
                new WorkBreakdownSection { Code = "C", Title = "Internal Finishes" },
                new WorkBreakdownSection { Code = "D", Title = "Fittings and Equipment" },
                new WorkBreakdownSection { Code = "E", Title = "Mechanical Services" },
                new WorkBreakdownSection { Code = "F", Title = "Electrical Services" },
                new WorkBreakdownSection { Code = "G", Title = "External Works" }
            };
        }

        #endregion

        #region Cost Application

        private async Task ApplyCostsAsync(List<WorkSection> sections, TakeoffOptions options)
        {
            foreach (var section in sections)
            {
                foreach (var item in section.Items)
                {
                    var unitRate = await _costDatabase.GetRateAsync(
                        item.Description,
                        item.Unit,
                        options.Region,
                        options.CostDate);

                    if (unitRate != null)
                    {
                        item.Rate = unitRate.Rate;
                        item.Amount = (decimal)item.Quantity * unitRate.Rate;
                        item.RateSource = unitRate.Source;
                    }
                }

                section.SectionTotal = section.Items.Sum(i => i.Amount ?? 0);
            }
        }

        private CostSummary CalculateCostSummary(List<WorkSection> sections)
        {
            var summary = new CostSummary
            {
                DirectCost = sections.Sum(s => s.SectionTotal),
                SectionBreakdown = sections.Select(s => new SectionCost
                {
                    SectionCode = s.Code,
                    SectionTitle = s.Title,
                    Amount = s.SectionTotal
                }).ToList()
            };

            // Calculate percentages
            summary.Preliminaries = summary.DirectCost * 0.12m; // 12% typical
            summary.Contingency = summary.DirectCost * 0.05m;  // 5% typical
            summary.Overheads = summary.DirectCost * 0.08m;    // 8% typical
            summary.Profit = summary.DirectCost * 0.05m;       // 5% typical

            summary.SubTotal = summary.DirectCost + summary.Preliminaries + summary.Contingency;
            summary.TotalBeforeTax = summary.SubTotal + summary.Overheads + summary.Profit;
            summary.VAT = summary.TotalBeforeTax * 0.16m; // 16% VAT typical for East Africa
            summary.GrandTotal = summary.TotalBeforeTax + summary.VAT;

            return summary;
        }

        #endregion

        #region Statistics

        private TakeoffStatistics GenerateStatistics(QuantityTakeoffResult result)
        {
            var stats = new TakeoffStatistics
            {
                TotalElements = result.RawQuantities.Values.Sum(c => c.Items.Count),
                TotalWorkItems = result.WorkSections.Sum(s => s.Items.Count),
                CategoriesProcessed = result.RawQuantities.Count
            };

            // Area summary
            stats.TotalWallArea = result.RawQuantities
                .Where(q => q.Key == "Walls")
                .SelectMany(q => q.Value.Items)
                .Sum(i => i.Area ?? 0);

            stats.TotalFloorArea = result.RawQuantities
                .Where(q => q.Key == "Floors")
                .SelectMany(q => q.Value.Items)
                .Sum(i => i.Area ?? 0);

            stats.TotalRoofArea = result.RawQuantities
                .Where(q => q.Key == "Roofs")
                .SelectMany(q => q.Value.Items)
                .Sum(i => i.Area ?? 0);

            // Counts
            stats.DoorCount = result.RawQuantities
                .Where(q => q.Key == "Doors")
                .SelectMany(q => q.Value.Items)
                .Count();

            stats.WindowCount = result.RawQuantities
                .Where(q => q.Key == "Windows")
                .SelectMany(q => q.Value.Items)
                .Count();

            return stats;
        }

        private List<string> GetDefaultCategories()
        {
            return new List<string>
            {
                "Walls", "Floors", "Ceilings", "Roofs",
                "Doors", "Windows", "Curtain Walls",
                "Structural Columns", "Structural Beams", "Structural Foundations",
                "Stairs", "Railings",
                "Ducts", "Pipes", "Cable Trays",
                "Mechanical Equipment", "Electrical Fixtures", "Plumbing Fixtures", "Lighting Fixtures",
                "Furniture", "Casework"
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class QuantityExtractor
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Extracts quantities from a BIM model for a given category,
        /// calculating area, volume, and length, then groups by type
        /// and applies measurement rules.
        /// </summary>
        public CategoryQuantities ExtractQuantities(BIMModel model, string category, MeasurementRules rules)
        {
            Logger.Info($"Extracting quantities for category: {category}");

            var quantities = new CategoryQuantities
            {
                Category = category,
                ExtractedAt = DateTime.UtcNow
            };

            var elements = model.GetElementsByCategory(category).ToList();

            foreach (var element in elements)
            {
                var item = new QuantityItem
                {
                    ElementId = element.ElementId,
                    Category = element.Category,
                    Family = element.Family,
                    TypeName = element.TypeName,
                    Level = element.Level,
                    Phase = element.Phase,
                    Material = element.GetParameter("Material")
                };

                // Calculate Area based on category
                switch (category)
                {
                    case "Walls":
                        item.Width = element.GetParameter("Width");
                        item.Height = element.GetParameter("Height");
                        item.Length = element.GetParameter("Length");
                        item.Area = (item.Width ?? 0) * (item.Height ?? 0);
                        item.OpeningArea = element.GetParameter("OpeningArea");
                        break;

                    case "Floors":
                    case "Ceilings":
                    case "Roofs":
                        item.Width = element.GetParameter("Width");
                        item.Length = element.GetParameter("Length");
                        item.Area = (item.Width ?? 0) * (item.Length ?? 0);
                        item.Thickness = element.GetParameter("Thickness");
                        break;

                    default:
                        item.Area = element.GetParameter("Area");
                        item.Length = element.GetParameter("Length");
                        item.Count = 1;
                        break;
                }

                // Calculate Volume from Area * depth/thickness if not already set
                if (item.Area.HasValue)
                {
                    var depth = item.Thickness ?? element.GetParameter("Thickness") ?? element.GetParameter("Depth");
                    if (depth.HasValue)
                    {
                        item.Volume = item.Area.Value * depth.Value;
                    }
                }

                // Calculate Length from element parameter if not already set
                if (!item.Length.HasValue)
                {
                    item.Length = element.GetParameter("Length");
                }

                quantities.Items.Add(item);
            }

            // Apply measurement rules
            var measurementRuleList = rules.GetRulesForCategory(category, MeasurementStandard.NRM2);
            foreach (var item in quantities.Items)
            {
                foreach (var rule in measurementRuleList)
                {
                    switch (rule.RuleType)
                    {
                        case MeasurementRuleType.DeductOpenings:
                            if (item.OpeningArea.HasValue && rule.Threshold.HasValue
                                && item.OpeningArea.Value > rule.Threshold.Value)
                            {
                                item.Area = (item.Area ?? 0) - item.OpeningArea.Value;
                                item.Deductions.Add($"Opening deduction: {item.OpeningArea:F2} m²");
                            }
                            break;

                        case MeasurementRuleType.MinimumQuantity:
                            if (rule.MinimumValue.HasValue && item.Area.HasValue
                                && item.Area.Value < rule.MinimumValue.Value)
                            {
                                item.Area = rule.MinimumValue.Value;
                                item.Adjustments.Add($"Minimum area applied: {rule.MinimumValue} m²");
                            }
                            break;

                        case MeasurementRuleType.RoundUp:
                            if (item.Length.HasValue && rule.RoundingUnit.HasValue)
                            {
                                item.Length = Math.Ceiling(item.Length.Value / rule.RoundingUnit.Value)
                                             * rule.RoundingUnit.Value;
                            }
                            break;
                    }
                }
            }

            // Group by TypeName and create TypeQuantitySummary for each group
            quantities.TypeSummaries = quantities.Items
                .GroupBy(i => i.TypeName ?? "Unknown")
                .Select(g => new TypeQuantitySummary
                {
                    TypeName = g.Key,
                    Count = g.Count(),
                    TotalArea = g.Sum(i => i.Area ?? 0),
                    TotalVolume = g.Sum(i => i.Volume ?? 0),
                    TotalLength = g.Sum(i => i.Length ?? 0)
                })
                .ToList();

            Logger.Info($"Extracted {quantities.Items.Count} items, {quantities.TypeSummaries.Count} type summaries for {category}");
            return quantities;
        }
    }

    public class MeasurementRules
    {
        public List<MeasurementRule> GetRulesForCategory(string category, MeasurementStandard standard)
        {
            var rules = new List<MeasurementRule>();

            switch (category)
            {
                case "Walls":
                    rules.Add(new MeasurementRule
                    {
                        RuleType = MeasurementRuleType.DeductOpenings,
                        Threshold = 0.5 // Deduct openings > 0.5 m²
                    });
                    break;

                case "Floors":
                    rules.Add(new MeasurementRule
                    {
                        RuleType = MeasurementRuleType.MinimumQuantity,
                        MinimumValue = 1.0 // Minimum 1 m²
                    });
                    break;

                case "Pipes":
                case "Ducts":
                    rules.Add(new MeasurementRule
                    {
                        RuleType = MeasurementRuleType.RoundUp,
                        RoundingUnit = 0.5 // Round to nearest 0.5m
                    });
                    break;
            }

            return rules;
        }
    }

    public class CostDatabase
    {
        public async Task<UnitRate> GetRateAsync(string description, string unit, string region, DateTime? date)
        {
            await Task.Delay(1);

            // In real implementation, would query actual cost database
            return new UnitRate
            {
                Rate = 1000, // Placeholder
                Source = "Regional Cost Database",
                Date = date ?? DateTime.Now
            };
        }
    }

    public class BOQFormatter
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Formats a QuantityTakeoffResult into a Bill of Quantities string.
        /// Supports "text" and "csv" output formats.
        /// </summary>
        public string FormatBOQ(QuantityTakeoffResult result, string format)
        {
            Logger.Info($"Formatting BOQ in '{format}' format for model {result.ModelId}");

            return format?.ToLowerInvariant() switch
            {
                "csv" => FormatAsCSV(result),
                _ => FormatAsText(result)
            };
        }

        private string FormatAsText(QuantityTakeoffResult result)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("================================================================================");
            sb.AppendLine("                        BILL OF QUANTITIES");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"  Model: {result.ModelId}");
            sb.AppendLine($"  Generated: {result.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Standard: {result.MeasurementStandard}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            decimal grandTotal = 0m;

            foreach (var section in result.WorkSections)
            {
                sb.AppendLine($"  Section {section.Code}: {section.Title}");
                sb.AppendLine($"  {section.Description}");
                sb.AppendLine("  ------------------------------------------------------------------------------");
                sb.AppendLine($"  {"Item",-8} {"Description",-30} {"Qty",10} {"Unit",-6} {"Rate",12} {"Amount",14}");
                sb.AppendLine("  ------------------------------------------------------------------------------");

                int itemIndex = 1;
                foreach (var item in section.Items)
                {
                    var itemRef = $"{section.Code}.{itemIndex:D2}";
                    var rate = item.Rate.HasValue ? item.Rate.Value.ToString("N2") : "-";
                    var amount = item.Amount.HasValue ? item.Amount.Value.ToString("N2") : "-";

                    sb.AppendLine($"  {itemRef,-8} {Truncate(item.Description, 30),-30} {item.Quantity,10:F2} {item.Unit,-6} {rate,12} {amount,14}");
                    itemIndex++;
                }

                sb.AppendLine("  ------------------------------------------------------------------------------");
                sb.AppendLine($"  {"Section Subtotal:",-56} {section.SectionTotal,14:N2}");
                sb.AppendLine();

                grandTotal += section.SectionTotal;
            }

            sb.AppendLine("================================================================================");
            sb.AppendLine($"  {"GRAND TOTAL:",-56} {grandTotal,14:N2}");
            sb.AppendLine("================================================================================");

            if (result.CostSummary != null)
            {
                sb.AppendLine();
                sb.AppendLine("  Cost Summary:");
                sb.AppendLine($"    Direct Cost:       {result.CostSummary.DirectCost,14:N2}");
                sb.AppendLine($"    Preliminaries:     {result.CostSummary.Preliminaries,14:N2}");
                sb.AppendLine($"    Contingency:       {result.CostSummary.Contingency,14:N2}");
                sb.AppendLine($"    Overheads:         {result.CostSummary.Overheads,14:N2}");
                sb.AppendLine($"    Profit:            {result.CostSummary.Profit,14:N2}");
                sb.AppendLine($"    Total Before Tax:  {result.CostSummary.TotalBeforeTax,14:N2}");
                sb.AppendLine($"    VAT:               {result.CostSummary.VAT,14:N2}");
                sb.AppendLine($"    Grand Total:       {result.CostSummary.GrandTotal,14:N2}");
            }

            return sb.ToString();
        }

        private string FormatAsCSV(QuantityTakeoffResult result)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Section Code,Section Title,Item Ref,Description,Quantity,Unit,Rate,Amount");

            foreach (var section in result.WorkSections)
            {
                int itemIndex = 1;
                foreach (var item in section.Items)
                {
                    var itemRef = $"{section.Code}.{itemIndex:D2}";
                    var rate = item.Rate.HasValue ? item.Rate.Value.ToString("F2") : "";
                    var amount = item.Amount.HasValue ? item.Amount.Value.ToString("F2") : "";
                    var description = EscapeCsv(item.Description);

                    sb.AppendLine($"{section.Code},{EscapeCsv(section.Title)},{itemRef},{description},{item.Quantity:F2},{item.Unit},{rate},{amount}");
                    itemIndex++;
                }

                // Section subtotal row
                sb.AppendLine($"{section.Code},{EscapeCsv(section.Title)},,,,,Subtotal,{section.SectionTotal:F2}");
            }

            // Grand total row
            var grandTotal = result.WorkSections.Sum(s => s.SectionTotal);
            sb.AppendLine($",,,,,,Grand Total,{grandTotal:F2}");

            return sb.ToString();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }

    public class WasteFactors
    {
        private readonly Dictionary<string, double> _factors = new Dictionary<string, double>
        {
            { "Walls", 0.05 },       // 5% waste
            { "Floors", 0.03 },      // 3% waste
            { "Ceilings", 0.10 },    // 10% waste
            { "Roofs", 0.08 },       // 8% waste
            { "Doors", 0.00 },       // No waste for items
            { "Windows", 0.00 },
            { "Pipes", 0.10 },       // 10% waste for cutting
            { "Ducts", 0.12 },       // 12% waste
            { "Cable Trays", 0.08 }
        };

        public double GetFactor(string category)
        {
            return _factors.GetValueOrDefault(category, 0.05);
        }
    }

    #endregion

    #region Data Models

    public class BIMModel
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public List<BIMElement> Elements { get; set; } = new List<BIMElement>();

        public IEnumerable<BIMElement> GetElementsByCategory(string category)
        {
            return Elements.Where(e => e.Category == category);
        }
    }

    public class BIMElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string TypeName { get; set; }
        public string Level { get; set; }
        public string Phase { get; set; }
        public Dictionary<string, double?> Parameters { get; set; } = new Dictionary<string, double?>();

        public double? GetParameter(string name)
        {
            return Parameters.GetValueOrDefault(name);
        }
    }

    public class TakeoffOptions
    {
        public MeasurementStandard MeasurementStandard { get; set; } = MeasurementStandard.NRM2;
        public List<string> CategoriesToExtract { get; set; }
        public bool ApplyWasteFactors { get; set; } = true;
        public bool IncludeCosts { get; set; } = true;
        public string Region { get; set; } = "Kenya";
        public DateTime? CostDate { get; set; }

        public static TakeoffOptions Default => new TakeoffOptions();
    }

    public class QuantityTakeoffResult
    {
        public string ModelId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public MeasurementStandard MeasurementStandard { get; set; }
        public Dictionary<string, CategoryQuantities> RawQuantities { get; set; }
        public List<WorkSection> WorkSections { get; set; } = new List<WorkSection>();
        public CostSummary CostSummary { get; set; }
        public TakeoffStatistics Statistics { get; set; }
    }

    public class CategoryQuantities
    {
        public string Category { get; set; }
        public DateTime ExtractedAt { get; set; }
        public List<QuantityItem> Items { get; set; } = new List<QuantityItem>();
        public List<TypeQuantitySummary> TypeSummaries { get; set; } = new List<TypeQuantitySummary>();
    }

    public class QuantityItem
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string TypeName { get; set; }
        public string Level { get; set; }
        public string Phase { get; set; }
        public int? Count { get; set; }
        public double? Length { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? Thickness { get; set; }
        public double? Area { get; set; }
        public double? Volume { get; set; }
        public double? Weight { get; set; }
        public double? Perimeter { get; set; }
        public string Size { get; set; }
        public object Material { get; set; }
        public double? OpeningArea { get; set; }
        public double WasteFactor { get; set; }
        public double? AdjustedArea { get; set; }
        public double? AdjustedVolume { get; set; }
        public double? AdjustedLength { get; set; }
        public List<string> Deductions { get; set; } = new List<string>();
        public List<string> Adjustments { get; set; } = new List<string>();
    }

    public class TypeQuantitySummary
    {
        public string TypeName { get; set; }
        public int Count { get; set; }
        public double TotalArea { get; set; }
        public double TotalVolume { get; set; }
        public double TotalLength { get; set; }
    }

    public class MeasurementRule
    {
        public MeasurementRuleType RuleType { get; set; }
        public double? Threshold { get; set; }
        public double? RoundingUnit { get; set; }
        public double? MinimumValue { get; set; }
    }

    public enum MeasurementRuleType
    {
        DeductOpenings,
        RoundUp,
        MinimumQuantity,
        MaximumQuantity
    }

    public enum MeasurementStandard
    {
        NRM2,       // New Rules of Measurement 2 (UK)
        SMM7,       // Standard Method of Measurement 7
        POMI,       // Principles of Measurement International
        Custom
    }

    public class WorkSection
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<WorkItem> Items { get; set; } = new List<WorkItem>();
        public decimal SectionTotal { get; set; }
    }

    public class WorkBreakdownSection
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<CategoryMapping> CategoryMappings { get; set; } = new List<CategoryMapping>();
    }

    public class CategoryMapping
    {
        public string Category { get; set; }
        public string TypeFilter { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
    }

    public class WorkItem
    {
        public string ItemRef { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public double Quantity { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Amount { get; set; }
        public string Level { get; set; }
        public string Material { get; set; }
        public string RateSource { get; set; }
    }

    public class UnitRate
    {
        public decimal Rate { get; set; }
        public string Source { get; set; }
        public DateTime Date { get; set; }
    }

    public class CostSummary
    {
        public decimal DirectCost { get; set; }
        public decimal Preliminaries { get; set; }
        public decimal Contingency { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Overheads { get; set; }
        public decimal Profit { get; set; }
        public decimal TotalBeforeTax { get; set; }
        public decimal VAT { get; set; }
        public decimal GrandTotal { get; set; }
        public List<SectionCost> SectionBreakdown { get; set; }
    }

    public class SectionCost
    {
        public string SectionCode { get; set; }
        public string SectionTitle { get; set; }
        public decimal Amount { get; set; }
    }

    public class TakeoffStatistics
    {
        public int TotalElements { get; set; }
        public int TotalWorkItems { get; set; }
        public int CategoriesProcessed { get; set; }
        public double TotalWallArea { get; set; }
        public double TotalFloorArea { get; set; }
        public double TotalRoofArea { get; set; }
        public int DoorCount { get; set; }
        public int WindowCount { get; set; }
    }

    #endregion
}
