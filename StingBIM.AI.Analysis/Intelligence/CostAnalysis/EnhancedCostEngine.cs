// ============================================================================
// StingBIM AI - Enhanced Cost Analysis Engine
// Real-time cost tracking with demolition, clash repair, and variance analysis
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.CostAnalysis
{
    /// <summary>
    /// Enhanced 5D cost analysis engine providing real-time cost tracking,
    /// demolition cost estimation, clash repair costing, and variance analysis.
    /// </summary>
    public sealed class EnhancedCostEngine
    {
        private static readonly Lazy<EnhancedCostEngine> _instance =
            new Lazy<EnhancedCostEngine>(() => new EnhancedCostEngine());
        public static EnhancedCostEngine Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, CostItem> _costItems = new();
        private readonly Dictionary<string, DemolitionCostItem> _demolitionItems = new();
        private readonly Dictionary<string, ClashRepairCost> _clashRepairCosts = new();
        private readonly List<CostVariance> _variances = new();
        private readonly Dictionary<string, UnitCost> _unitCosts = new();
        private readonly Dictionary<string, DisposalRate> _disposalRates = new();

        public event EventHandler<CostEventArgs> CostUpdated;
        public event EventHandler<CostEventArgs> BudgetWarning;
        public event EventHandler<CostEventArgs> VarianceDetected;

        private EnhancedCostEngine()
        {
            InitializeUnitCosts();
            InitializeDisposalRates();
        }

        #region Initialization

        private void InitializeUnitCosts()
        {
            // Construction unit costs (USD per unit)
            _unitCosts["concrete_m3"] = new UnitCost { Code = "concrete_m3", Description = "Concrete per cubic meter", Unit = "m³", Rate = 150.00m, Category = CostCategory.Structural };
            _unitCosts["rebar_kg"] = new UnitCost { Code = "rebar_kg", Description = "Reinforcement steel per kg", Unit = "kg", Rate = 1.20m, Category = CostCategory.Structural };
            _unitCosts["structural_steel_kg"] = new UnitCost { Code = "structural_steel_kg", Description = "Structural steel per kg", Unit = "kg", Rate = 2.50m, Category = CostCategory.Structural };
            _unitCosts["drywall_m2"] = new UnitCost { Code = "drywall_m2", Description = "Drywall per square meter", Unit = "m²", Rate = 35.00m, Category = CostCategory.Architectural };
            _unitCosts["paint_m2"] = new UnitCost { Code = "paint_m2", Description = "Paint per square meter", Unit = "m²", Rate = 12.00m, Category = CostCategory.Architectural };
            _unitCosts["hvac_duct_m"] = new UnitCost { Code = "hvac_duct_m", Description = "HVAC ductwork per meter", Unit = "m", Rate = 85.00m, Category = CostCategory.Mechanical };
            _unitCosts["piping_m"] = new UnitCost { Code = "piping_m", Description = "Piping per meter", Unit = "m", Rate = 65.00m, Category = CostCategory.Plumbing };
            _unitCosts["conduit_m"] = new UnitCost { Code = "conduit_m", Description = "Electrical conduit per meter", Unit = "m", Rate = 25.00m, Category = CostCategory.Electrical };
            _unitCosts["cable_tray_m"] = new UnitCost { Code = "cable_tray_m", Description = "Cable tray per meter", Unit = "m", Rate = 45.00m, Category = CostCategory.Electrical };

            // Labor costs
            _unitCosts["labor_general_hr"] = new UnitCost { Code = "labor_general_hr", Description = "General labor per hour", Unit = "hr", Rate = 35.00m, Category = CostCategory.Labor };
            _unitCosts["labor_skilled_hr"] = new UnitCost { Code = "labor_skilled_hr", Description = "Skilled labor per hour", Unit = "hr", Rate = 55.00m, Category = CostCategory.Labor };
            _unitCosts["labor_specialist_hr"] = new UnitCost { Code = "labor_specialist_hr", Description = "Specialist labor per hour", Unit = "hr", Rate = 85.00m, Category = CostCategory.Labor };

            // Demolition labor
            _unitCosts["demo_labor_hr"] = new UnitCost { Code = "demo_labor_hr", Description = "Demolition labor per hour", Unit = "hr", Rate = 45.00m, Category = CostCategory.Demolition };
            _unitCosts["demo_equipment_day"] = new UnitCost { Code = "demo_equipment_day", Description = "Demolition equipment per day", Unit = "day", Rate = 1500.00m, Category = CostCategory.Demolition };
        }

        private void InitializeDisposalRates()
        {
            // Disposal rates per ton (USD)
            _disposalRates["concrete"] = new DisposalRate { MaterialType = "concrete", RatePerTon = 45.00m, CanRecycle = true, RecycleCredit = 15.00m };
            _disposalRates["steel"] = new DisposalRate { MaterialType = "steel", RatePerTon = 30.00m, CanRecycle = true, RecycleCredit = 180.00m };
            _disposalRates["wood"] = new DisposalRate { MaterialType = "wood", RatePerTon = 65.00m, CanRecycle = true, RecycleCredit = 5.00m };
            _disposalRates["drywall"] = new DisposalRate { MaterialType = "drywall", RatePerTon = 80.00m, CanRecycle = true, RecycleCredit = 8.00m };
            _disposalRates["brick"] = new DisposalRate { MaterialType = "brick", RatePerTon = 50.00m, CanRecycle = true, RecycleCredit = 10.00m };
            _disposalRates["glass"] = new DisposalRate { MaterialType = "glass", RatePerTon = 55.00m, CanRecycle = true, RecycleCredit = 25.00m };
            _disposalRates["asbestos"] = new DisposalRate { MaterialType = "asbestos", RatePerTon = 450.00m, CanRecycle = false, RecycleCredit = 0, IsHazardous = true };
            _disposalRates["lead_paint"] = new DisposalRate { MaterialType = "lead_paint", RatePerTon = 350.00m, CanRecycle = false, RecycleCredit = 0, IsHazardous = true };
            _disposalRates["general_waste"] = new DisposalRate { MaterialType = "general_waste", RatePerTon = 95.00m, CanRecycle = false, RecycleCredit = 0 };
        }

        #endregion

        #region Real-Time Cost Tracking

        /// <summary>
        /// Calculate costs directly from model elements
        /// </summary>
        public async Task<ModelCostAnalysis> AnalyzeModelCostsAsync(ModelElements elements)
        {
            return await Task.Run(() =>
            {
                var analysis = new ModelCostAnalysis
                {
                    AnalyzedAt = DateTime.UtcNow,
                    Categories = new Dictionary<CostCategory, CategoryCost>(),
                    ElementCosts = new List<ElementCost>(),
                    Summary = new CostSummary()
                };

                foreach (var element in elements.Elements)
                {
                    var elementCost = CalculateElementCost(element);
                    analysis.ElementCosts.Add(elementCost);

                    // Aggregate by category
                    if (!analysis.Categories.TryGetValue(elementCost.Category, out var catCost))
                    {
                        catCost = new CategoryCost { Category = elementCost.Category };
                        analysis.Categories[elementCost.Category] = catCost;
                    }

                    catCost.MaterialCost += elementCost.MaterialCost;
                    catCost.LaborCost += elementCost.LaborCost;
                    catCost.ElementCount++;
                }

                // Calculate summary
                analysis.Summary.TotalMaterialCost = analysis.Categories.Values.Sum(c => c.MaterialCost);
                analysis.Summary.TotalLaborCost = analysis.Categories.Values.Sum(c => c.LaborCost);
                analysis.Summary.TotalCost = analysis.Summary.TotalMaterialCost + analysis.Summary.TotalLaborCost;
                analysis.Summary.ElementCount = analysis.ElementCosts.Count;

                return analysis;
            });
        }

        private ElementCost CalculateElementCost(ModelElement element)
        {
            var cost = new ElementCost
            {
                ElementId = element.ElementId,
                ElementType = element.ElementType,
                Category = element.Category
            };

            // Calculate based on element type
            switch (element.ElementType.ToLower())
            {
                case "wall":
                    cost.MaterialCost = (decimal)element.Area * (_unitCosts["drywall_m2"].Rate + _unitCosts["paint_m2"].Rate);
                    cost.LaborCost = (decimal)element.Area * 0.5m * _unitCosts["labor_skilled_hr"].Rate;
                    cost.Quantity = element.Area;
                    cost.Unit = "m²";
                    break;

                case "floor":
                case "slab":
                    cost.MaterialCost = (decimal)element.Volume * _unitCosts["concrete_m3"].Rate;
                    cost.MaterialCost += (decimal)element.Volume * 150 * _unitCosts["rebar_kg"].Rate; // 150kg/m³ rebar
                    cost.LaborCost = (decimal)element.Area * 0.3m * _unitCosts["labor_skilled_hr"].Rate;
                    cost.Quantity = element.Volume;
                    cost.Unit = "m³";
                    break;

                case "beam":
                case "column":
                    cost.MaterialCost = (decimal)element.Volume * _unitCosts["concrete_m3"].Rate;
                    cost.MaterialCost += (decimal)element.Volume * 200 * _unitCosts["rebar_kg"].Rate; // 200kg/m³ rebar
                    cost.LaborCost = (decimal)element.Volume * 2 * _unitCosts["labor_skilled_hr"].Rate;
                    cost.Quantity = element.Volume;
                    cost.Unit = "m³";
                    break;

                case "duct":
                    cost.MaterialCost = (decimal)element.Length * _unitCosts["hvac_duct_m"].Rate;
                    cost.LaborCost = (decimal)element.Length * 0.4m * _unitCosts["labor_specialist_hr"].Rate;
                    cost.Quantity = element.Length;
                    cost.Unit = "m";
                    break;

                case "pipe":
                    cost.MaterialCost = (decimal)element.Length * _unitCosts["piping_m"].Rate;
                    cost.LaborCost = (decimal)element.Length * 0.3m * _unitCosts["labor_specialist_hr"].Rate;
                    cost.Quantity = element.Length;
                    cost.Unit = "m";
                    break;

                case "conduit":
                    cost.MaterialCost = (decimal)element.Length * _unitCosts["conduit_m"].Rate;
                    cost.LaborCost = (decimal)element.Length * 0.2m * _unitCosts["labor_skilled_hr"].Rate;
                    cost.Quantity = element.Length;
                    cost.Unit = "m";
                    break;

                default:
                    // Generic estimation
                    cost.MaterialCost = (decimal)(element.Volume > 0 ? element.Volume * 100 : element.Area * 50);
                    cost.LaborCost = cost.MaterialCost * 0.3m;
                    cost.Quantity = element.Volume > 0 ? element.Volume : element.Area;
                    cost.Unit = element.Volume > 0 ? "m³" : "m²";
                    break;
            }

            cost.TotalCost = cost.MaterialCost + cost.LaborCost;
            return cost;
        }

        #endregion

        #region Demolition Cost Analysis

        /// <summary>
        /// Comprehensive demolition cost analysis
        /// </summary>
        public async Task<DemolitionCostAnalysis> AnalyzeDemolitionCostsAsync(DemolitionScope scope)
        {
            return await Task.Run(() =>
            {
                var analysis = new DemolitionCostAnalysis
                {
                    AnalyzedAt = DateTime.UtcNow,
                    Scope = scope,
                    Items = new List<DemolitionCostItem>(),
                    HazardousMaterials = new List<HazardousMaterialItem>(),
                    SalvageableItems = new List<SalvageItem>(),
                    Summary = new DemolitionCostSummary()
                };

                foreach (var element in scope.Elements)
                {
                    var item = CalculateDemolitionCost(element);
                    analysis.Items.Add(item);

                    lock (_lock)
                    {
                        _demolitionItems[element.ElementId] = item;
                    }

                    // Check for hazardous materials
                    if (element.ContainsAsbestos || element.ContainsLeadPaint)
                    {
                        analysis.HazardousMaterials.Add(new HazardousMaterialItem
                        {
                            ElementId = element.ElementId,
                            MaterialType = element.ContainsAsbestos ? "Asbestos" : "Lead Paint",
                            EstimatedQuantity = element.Weight * 0.05, // 5% contaminated
                            RemovalCost = (decimal)(element.Weight * 0.05) *
                                (element.ContainsAsbestos ? _disposalRates["asbestos"].RatePerTon : _disposalRates["lead_paint"].RatePerTon),
                            SpecialHandlingRequired = true
                        });
                    }

                    // Check for salvageable materials
                    if (element.MaterialType == "steel" || element.MaterialType == "brick")
                    {
                        var disposal = _disposalRates.TryGetValue(element.MaterialType, out var rate) ? rate : null;
                        if (disposal?.CanRecycle == true)
                        {
                            analysis.SalvageableItems.Add(new SalvageItem
                            {
                                ElementId = element.ElementId,
                                MaterialType = element.MaterialType,
                                EstimatedWeight = element.Weight,
                                SalvageValue = (decimal)element.Weight * disposal.RecycleCredit,
                                RecyclingFeasibility = element.Weight > 0.5 ? "High" : "Low"
                            });
                        }
                    }
                }

                // Calculate summary
                analysis.Summary.TotalDemolitionCost = analysis.Items.Sum(i => i.DemolitionCost);
                analysis.Summary.TotalDisposalCost = analysis.Items.Sum(i => i.DisposalCost);
                analysis.Summary.TotalHazmatCost = analysis.HazardousMaterials.Sum(h => h.RemovalCost);
                analysis.Summary.TotalSalvageCredit = analysis.SalvageableItems.Sum(s => s.SalvageValue);
                analysis.Summary.NetDemolitionCost =
                    analysis.Summary.TotalDemolitionCost +
                    analysis.Summary.TotalDisposalCost +
                    analysis.Summary.TotalHazmatCost -
                    analysis.Summary.TotalSalvageCredit;

                analysis.Summary.EstimatedDuration = TimeSpan.FromHours(
                    analysis.Items.Sum(i => i.EstimatedHours));

                analysis.Summary.WasteByType = analysis.Items
                    .GroupBy(i => i.MaterialType)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Weight));

                return analysis;
            });
        }

        private DemolitionCostItem CalculateDemolitionCost(DemolitionElement element)
        {
            var item = new DemolitionCostItem
            {
                ElementId = element.ElementId,
                ElementType = element.ElementType,
                MaterialType = element.MaterialType,
                Weight = element.Weight,
                Volume = element.Volume
            };

            // Demolition labor cost (based on element type)
            var laborHours = element.ElementType.ToLower() switch
            {
                "wall" => element.Area * 0.15,
                "floor" or "slab" => element.Area * 0.25,
                "beam" or "column" => element.Volume * 2.0,
                "duct" or "pipe" => element.Length * 0.1,
                _ => element.Volume * 0.5
            };

            item.EstimatedHours = laborHours;
            item.DemolitionCost = (decimal)laborHours * _unitCosts["demo_labor_hr"].Rate;

            // Equipment cost (for larger elements)
            if (element.Weight > 1.0) // Over 1 ton
            {
                item.RequiresHeavyEquipment = true;
                item.EquipmentCost = (decimal)Math.Ceiling(laborHours / 8) * _unitCosts["demo_equipment_day"].Rate;
                item.DemolitionCost += item.EquipmentCost;
            }

            // Disposal cost
            if (_disposalRates.TryGetValue(element.MaterialType.ToLower(), out var disposal))
            {
                item.DisposalCost = (decimal)element.Weight * disposal.RatePerTon;
                if (disposal.CanRecycle)
                {
                    item.RecycleCredit = (decimal)element.Weight * disposal.RecycleCredit;
                }
            }
            else
            {
                item.DisposalCost = (decimal)element.Weight * _disposalRates["general_waste"].RatePerTon;
            }

            item.TotalCost = item.DemolitionCost + item.DisposalCost - item.RecycleCredit;

            return item;
        }

        #endregion

        #region Clash Repair Costing

        /// <summary>
        /// Calculate repair costs for clashes
        /// </summary>
        public async Task<ClashRepairAnalysis> AnalyzeClashRepairCostsAsync(List<ClashInstance> clashes)
        {
            return await Task.Run(() =>
            {
                var analysis = new ClashRepairAnalysis
                {
                    AnalyzedAt = DateTime.UtcNow,
                    ClashCosts = new List<ClashRepairCost>(),
                    Summary = new ClashRepairSummary()
                };

                foreach (var clash in clashes)
                {
                    var repairCost = CalculateClashRepairCost(clash);
                    analysis.ClashCosts.Add(repairCost);

                    lock (_lock)
                    {
                        _clashRepairCosts[clash.ClashId] = repairCost;
                    }
                }

                // Calculate summary
                analysis.Summary.TotalClashes = clashes.Count;
                analysis.Summary.TotalRepairCost = analysis.ClashCosts.Sum(c => c.TotalRepairCost);
                analysis.Summary.TotalReworkHours = analysis.ClashCosts.Sum(c => c.EstimatedHours);

                analysis.Summary.CostBySeverity = analysis.ClashCosts
                    .GroupBy(c => c.Severity)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.TotalRepairCost));

                analysis.Summary.CostByDiscipline = analysis.ClashCosts
                    .GroupBy(c => c.PrimaryDiscipline)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.TotalRepairCost));

                analysis.Summary.AverageRepairCost = analysis.Summary.TotalClashes > 0
                    ? analysis.Summary.TotalRepairCost / analysis.Summary.TotalClashes
                    : 0;

                return analysis;
            });
        }

        private ClashRepairCost CalculateClashRepairCost(ClashInstance clash)
        {
            var repairCost = new ClashRepairCost
            {
                ClashId = clash.ClashId,
                ClashType = clash.ClashType,
                Severity = clash.Severity,
                PrimaryDiscipline = clash.PrimaryDiscipline,
                SecondaryDiscipline = clash.SecondaryDiscipline,
                ResolutionOptions = new List<RepairOption>()
            };

            // Calculate base repair cost based on severity and type
            var baseCost = clash.Severity switch
            {
                ClashSeverity.Critical => 2500m,
                ClashSeverity.High => 1500m,
                ClashSeverity.Medium => 750m,
                ClashSeverity.Low => 250m,
                _ => 500m
            };

            // Adjust by clash type
            var typeMultiplier = clash.ClashType switch
            {
                "Hard" => 1.5,
                "Soft" => 1.0,
                "Clearance" => 0.75,
                "Duplicate" => 0.25,
                _ => 1.0
            };

            repairCost.EstimatedHours = clash.Severity switch
            {
                ClashSeverity.Critical => 8.0,
                ClashSeverity.High => 4.0,
                ClashSeverity.Medium => 2.0,
                ClashSeverity.Low => 1.0,
                _ => 2.0
            };

            // Generate resolution options
            repairCost.ResolutionOptions.Add(new RepairOption
            {
                OptionId = "relocate_primary",
                Description = $"Relocate {clash.PrimaryDiscipline} element",
                EstimatedCost = baseCost * (decimal)typeMultiplier,
                EstimatedHours = repairCost.EstimatedHours,
                Feasibility = 0.85,
                Impact = "Minimal impact on other systems"
            });

            repairCost.ResolutionOptions.Add(new RepairOption
            {
                OptionId = "relocate_secondary",
                Description = $"Relocate {clash.SecondaryDiscipline} element",
                EstimatedCost = baseCost * (decimal)typeMultiplier * 0.9m,
                EstimatedHours = repairCost.EstimatedHours * 0.8,
                Feasibility = 0.80,
                Impact = "May require coordination with other trades"
            });

            repairCost.ResolutionOptions.Add(new RepairOption
            {
                OptionId = "resize_element",
                Description = "Resize conflicting element",
                EstimatedCost = baseCost * (decimal)typeMultiplier * 0.7m,
                EstimatedHours = repairCost.EstimatedHours * 0.5,
                Feasibility = 0.60,
                Impact = "Requires engineering review"
            });

            // Set recommended option (lowest cost with high feasibility)
            repairCost.RecommendedOption = repairCost.ResolutionOptions
                .Where(o => o.Feasibility >= 0.75)
                .OrderBy(o => o.EstimatedCost)
                .FirstOrDefault();

            repairCost.TotalRepairCost = repairCost.RecommendedOption?.EstimatedCost ?? baseCost;

            return repairCost;
        }

        #endregion

        #region Cost Variance Tracking

        /// <summary>
        /// Track cost variances against budget
        /// </summary>
        public CostVarianceReport TrackVariances(ProjectBudget budget, ModelCostAnalysis currentCosts)
        {
            var report = new CostVarianceReport
            {
                GeneratedAt = DateTime.UtcNow,
                Budget = budget,
                CurrentCosts = currentCosts,
                CategoryVariances = new Dictionary<CostCategory, decimal>(),
                Warnings = new List<BudgetWarning>()
            };

            // Calculate variances by category
            foreach (var category in currentCosts.Categories)
            {
                var budgetAmount = budget.CategoryBudgets.TryGetValue(category.Key, out var amt) ? amt : 0;
                var actualAmount = category.Value.TotalCost;
                var variance = actualAmount - budgetAmount;

                report.CategoryVariances[category.Key] = variance;

                // Track variance
                var varianceEntry = new CostVariance
                {
                    Timestamp = DateTime.UtcNow,
                    Category = category.Key,
                    BudgetedAmount = budgetAmount,
                    ActualAmount = actualAmount,
                    Variance = variance,
                    VariancePercentage = budgetAmount > 0 ? (variance / budgetAmount * 100) : 0
                };

                lock (_lock)
                {
                    _variances.Add(varianceEntry);
                }

                // Generate warnings
                if (varianceEntry.VariancePercentage > 10)
                {
                    report.Warnings.Add(new BudgetWarning
                    {
                        Severity = varianceEntry.VariancePercentage > 20 ? WarningSeverity.Critical : WarningSeverity.High,
                        Category = category.Key,
                        Message = $"{category.Key} is {varianceEntry.VariancePercentage:F1}% over budget",
                        Variance = variance,
                        Recommendations = GetVarianceRecommendations(category.Key, varianceEntry.VariancePercentage)
                    });

                    BudgetWarning?.Invoke(this, new CostEventArgs
                    {
                        Type = CostEventType.BudgetExceeded,
                        Message = $"{category.Key} over budget by {varianceEntry.VariancePercentage:F1}%"
                    });
                }
            }

            // Overall variance
            report.TotalBudget = budget.TotalBudget;
            report.TotalActual = currentCosts.Summary.TotalCost;
            report.TotalVariance = report.TotalActual - report.TotalBudget;
            report.TotalVariancePercentage = report.TotalBudget > 0
                ? (report.TotalVariance / report.TotalBudget * 100)
                : 0;

            // Forecast
            report.EstimatedAtCompletion = CalculateEAC(budget, currentCosts);
            report.VarianceAtCompletion = report.EstimatedAtCompletion - budget.TotalBudget;

            return report;
        }

        private List<string> GetVarianceRecommendations(CostCategory category, decimal variancePercentage)
        {
            var recommendations = new List<string>();

            if (variancePercentage > 20)
            {
                recommendations.Add("CRITICAL: Immediate cost review required");
                recommendations.Add("Consider value engineering opportunities");
                recommendations.Add("Review scope for potential reductions");
            }
            else if (variancePercentage > 10)
            {
                recommendations.Add("Review recent design changes for cost impact");
                recommendations.Add("Identify alternative materials or methods");
                recommendations.Add("Verify quantity calculations against model");
            }

            switch (category)
            {
                case CostCategory.Structural:
                    recommendations.Add("Evaluate structural optimization opportunities");
                    recommendations.Add("Consider alternative structural systems");
                    break;

                case CostCategory.Mechanical:
                    recommendations.Add("Review HVAC sizing and efficiency");
                    recommendations.Add("Consider prefabrication options");
                    break;

                case CostCategory.Electrical:
                    recommendations.Add("Optimize cable routing from model");
                    recommendations.Add("Review lighting efficiency");
                    break;
            }

            return recommendations;
        }

        private decimal CalculateEAC(ProjectBudget budget, ModelCostAnalysis currentCosts)
        {
            // Estimate at Completion using performance-based forecast
            var percentComplete = budget.PercentComplete > 0 ? budget.PercentComplete : 0.1;
            var performanceIndex = budget.TotalBudget * (decimal)percentComplete / currentCosts.Summary.TotalCost;

            // EAC = Budget / CPI (Cost Performance Index)
            return performanceIndex > 0 ? budget.TotalBudget / performanceIndex : budget.TotalBudget * 1.2m;
        }

        #endregion

        #region Cost Reports

        /// <summary>
        /// Generate comprehensive cost report from model
        /// </summary>
        public async Task<ComprehensiveCostReport> GenerateCostReportAsync(
            ModelElements elements,
            DemolitionScope demolitionScope,
            List<ClashInstance> clashes,
            ProjectBudget budget)
        {
            var report = new ComprehensiveCostReport
            {
                GeneratedAt = DateTime.UtcNow,
                ProjectName = budget.ProjectName
            };

            // Run analyses in parallel
            var modelTask = AnalyzeModelCostsAsync(elements);
            var demolitionTask = demolitionScope != null
                ? AnalyzeDemolitionCostsAsync(demolitionScope)
                : Task.FromResult<DemolitionCostAnalysis>(null);
            var clashTask = clashes?.Count > 0
                ? AnalyzeClashRepairCostsAsync(clashes)
                : Task.FromResult<ClashRepairAnalysis>(null);

            await Task.WhenAll(modelTask, demolitionTask, clashTask);

            report.ConstructionCosts = await modelTask;
            report.DemolitionCosts = await demolitionTask;
            report.ClashRepairCosts = await clashTask;

            // Calculate totals
            report.Summary = new ComprehensiveCostSummary
            {
                ConstructionCost = report.ConstructionCosts?.Summary.TotalCost ?? 0,
                DemolitionCost = report.DemolitionCosts?.Summary.NetDemolitionCost ?? 0,
                ClashRepairCost = report.ClashRepairCosts?.Summary.TotalRepairCost ?? 0
            };

            report.Summary.Contingency = (report.Summary.ConstructionCost + report.Summary.DemolitionCost) * 0.10m;
            report.Summary.TotalProjectCost =
                report.Summary.ConstructionCost +
                report.Summary.DemolitionCost +
                report.Summary.ClashRepairCost +
                report.Summary.Contingency;

            // Variance analysis
            if (budget != null)
            {
                report.VarianceReport = TrackVariances(budget, report.ConstructionCosts);
            }

            return report;
        }

        #endregion
    }

    #region Data Models

    public class ModelElements
    {
        public List<ModelElement> Elements { get; set; } = new();
    }

    public class ModelElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public CostCategory Category { get; set; }
        public double Length { get; set; }
        public double Area { get; set; }
        public double Volume { get; set; }
        public double Weight { get; set; }
        public string MaterialType { get; set; }
    }

    public class ModelCostAnalysis
    {
        public DateTime AnalyzedAt { get; set; }
        public Dictionary<CostCategory, CategoryCost> Categories { get; set; }
        public List<ElementCost> ElementCosts { get; set; }
        public CostSummary Summary { get; set; }
    }

    public class CategoryCost
    {
        public CostCategory Category { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal TotalCost => MaterialCost + LaborCost;
        public int ElementCount { get; set; }
    }

    public class ElementCost
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public CostCategory Category { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal TotalCost { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }

    public class CostSummary
    {
        public decimal TotalMaterialCost { get; set; }
        public decimal TotalLaborCost { get; set; }
        public decimal TotalCost { get; set; }
        public int ElementCount { get; set; }
    }

    public class DemolitionScope
    {
        public List<DemolitionElement> Elements { get; set; } = new();
        public bool HasHazardousMaterials { get; set; }
    }

    public class DemolitionElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string MaterialType { get; set; }
        public double Length { get; set; }
        public double Area { get; set; }
        public double Volume { get; set; }
        public double Weight { get; set; } // in tons
        public bool ContainsAsbestos { get; set; }
        public bool ContainsLeadPaint { get; set; }
    }

    public class DemolitionCostAnalysis
    {
        public DateTime AnalyzedAt { get; set; }
        public DemolitionScope Scope { get; set; }
        public List<DemolitionCostItem> Items { get; set; }
        public List<HazardousMaterialItem> HazardousMaterials { get; set; }
        public List<SalvageItem> SalvageableItems { get; set; }
        public DemolitionCostSummary Summary { get; set; }
    }

    public class CostItem
    {
        public string Id { get; set; }
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal EquipmentCost { get; set; }
        public string CostCode { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class DemolitionCostItem
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string MaterialType { get; set; }
        public double Weight { get; set; }
        public double Volume { get; set; }
        public double EstimatedHours { get; set; }
        public decimal DemolitionCost { get; set; }
        public decimal EquipmentCost { get; set; }
        public decimal DisposalCost { get; set; }
        public decimal RecycleCredit { get; set; }
        public decimal TotalCost { get; set; }
        public bool RequiresHeavyEquipment { get; set; }
    }

    public class HazardousMaterialItem
    {
        public string ElementId { get; set; }
        public string MaterialType { get; set; }
        public double EstimatedQuantity { get; set; }
        public decimal RemovalCost { get; set; }
        public bool SpecialHandlingRequired { get; set; }
    }

    public class SalvageItem
    {
        public string ElementId { get; set; }
        public string MaterialType { get; set; }
        public double EstimatedWeight { get; set; }
        public decimal SalvageValue { get; set; }
        public string RecyclingFeasibility { get; set; }
    }

    public class DemolitionCostSummary
    {
        public decimal TotalDemolitionCost { get; set; }
        public decimal TotalDisposalCost { get; set; }
        public decimal TotalHazmatCost { get; set; }
        public decimal TotalSalvageCredit { get; set; }
        public decimal NetDemolitionCost { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public Dictionary<string, double> WasteByType { get; set; }
    }

    public class ClashInstance
    {
        public string ClashId { get; set; }
        public string ClashType { get; set; }
        public ClashSeverity Severity { get; set; }
        public string PrimaryDiscipline { get; set; }
        public string SecondaryDiscipline { get; set; }
        public string PrimaryElementId { get; set; }
        public string SecondaryElementId { get; set; }
    }

    public class ClashRepairAnalysis
    {
        public DateTime AnalyzedAt { get; set; }
        public List<ClashRepairCost> ClashCosts { get; set; }
        public ClashRepairSummary Summary { get; set; }
    }

    public class ClashRepairCost
    {
        public string ClashId { get; set; }
        public string ClashType { get; set; }
        public ClashSeverity Severity { get; set; }
        public string PrimaryDiscipline { get; set; }
        public string SecondaryDiscipline { get; set; }
        public double EstimatedHours { get; set; }
        public decimal TotalRepairCost { get; set; }
        public List<RepairOption> ResolutionOptions { get; set; }
        public RepairOption RecommendedOption { get; set; }
    }

    public class RepairOption
    {
        public string OptionId { get; set; }
        public string Description { get; set; }
        public decimal EstimatedCost { get; set; }
        public double EstimatedHours { get; set; }
        public double Feasibility { get; set; }
        public string Impact { get; set; }
    }

    public class ClashRepairSummary
    {
        public int TotalClashes { get; set; }
        public decimal TotalRepairCost { get; set; }
        public double TotalReworkHours { get; set; }
        public decimal AverageRepairCost { get; set; }
        public Dictionary<ClashSeverity, decimal> CostBySeverity { get; set; }
        public Dictionary<string, decimal> CostByDiscipline { get; set; }
    }

    public class ProjectBudget
    {
        public string ProjectName { get; set; }
        public decimal TotalBudget { get; set; }
        public Dictionary<CostCategory, decimal> CategoryBudgets { get; set; } = new();
        public double PercentComplete { get; set; }
    }

    public class CostVariance
    {
        public DateTime Timestamp { get; set; }
        public CostCategory Category { get; set; }
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercentage { get; set; }
    }

    public class CostVarianceReport
    {
        public DateTime GeneratedAt { get; set; }
        public ProjectBudget Budget { get; set; }
        public ModelCostAnalysis CurrentCosts { get; set; }
        public Dictionary<CostCategory, decimal> CategoryVariances { get; set; }
        public List<BudgetWarning> Warnings { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalActual { get; set; }
        public decimal TotalVariance { get; set; }
        public decimal TotalVariancePercentage { get; set; }
        public decimal EstimatedAtCompletion { get; set; }
        public decimal VarianceAtCompletion { get; set; }
    }

    public class BudgetWarning
    {
        public WarningSeverity Severity { get; set; }
        public CostCategory Category { get; set; }
        public string Message { get; set; }
        public decimal Variance { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ComprehensiveCostReport
    {
        public DateTime GeneratedAt { get; set; }
        public string ProjectName { get; set; }
        public ModelCostAnalysis ConstructionCosts { get; set; }
        public DemolitionCostAnalysis DemolitionCosts { get; set; }
        public ClashRepairAnalysis ClashRepairCosts { get; set; }
        public CostVarianceReport VarianceReport { get; set; }
        public ComprehensiveCostSummary Summary { get; set; }
    }

    public class ComprehensiveCostSummary
    {
        public decimal ConstructionCost { get; set; }
        public decimal DemolitionCost { get; set; }
        public decimal ClashRepairCost { get; set; }
        public decimal Contingency { get; set; }
        public decimal TotalProjectCost { get; set; }
    }

    public class UnitCost
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public decimal Rate { get; set; }
        public CostCategory Category { get; set; }
    }

    public class DisposalRate
    {
        public string MaterialType { get; set; }
        public decimal RatePerTon { get; set; }
        public bool CanRecycle { get; set; }
        public decimal RecycleCredit { get; set; }
        public bool IsHazardous { get; set; }
    }

    public class CostEventArgs : EventArgs
    {
        public CostEventType Type { get; set; }
        public string Message { get; set; }
        public decimal Value { get; set; }
    }

    #endregion

    #region Enums

    public enum CostCategory
    {
        Structural,
        Architectural,
        Mechanical,
        Electrical,
        Plumbing,
        FireProtection,
        Civil,
        Landscape,
        Demolition,
        Labor,
        Equipment,
        General
    }

    public enum ClashSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum WarningSeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }

    public enum CostEventType
    {
        CostUpdated,
        BudgetExceeded,
        VarianceDetected,
        ThresholdBreached
    }

    #endregion
}
