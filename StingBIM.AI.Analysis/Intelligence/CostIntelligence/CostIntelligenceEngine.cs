// ===================================================================
// StingBIM Cost Intelligence Engine
// Advanced cost estimation, value engineering, and budget management
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.CostIntelligence
{
    /// <summary>
    /// Comprehensive cost intelligence engine providing advanced estimation,
    /// value engineering, budget forecasting, and market rate analysis
    /// </summary>
    public sealed class CostIntelligenceEngine
    {
        private static readonly Lazy<CostIntelligenceEngine> _instance =
            new Lazy<CostIntelligenceEngine>(() => new CostIntelligenceEngine());
        public static CostIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, CostProject> _projects;
        private readonly ConcurrentDictionary<string, CostDatabase> _costDatabases;
        private readonly ConcurrentDictionary<string, ValueEngineeringStudy> _veStudies;
        private readonly ConcurrentDictionary<string, BudgetForecast> _forecasts;
        private readonly object _lockObject = new object();

        public event EventHandler<CostAlertEventArgs> CostAlertRaised;
        public event EventHandler<BudgetEventArgs> BudgetThresholdExceeded;

        private CostIntelligenceEngine()
        {
            _projects = new ConcurrentDictionary<string, CostProject>();
            _costDatabases = new ConcurrentDictionary<string, CostDatabase>();
            _veStudies = new ConcurrentDictionary<string, ValueEngineeringStudy>();
            _forecasts = new ConcurrentDictionary<string, BudgetForecast>();

            InitializeCostDatabases();
            InitializeMarketRates();
        }

        #region Cost Database Initialization

        private void InitializeCostDatabases()
        {
            // RSMeans-style cost data
            var rsMeansData = new CostDatabase
            {
                Id = "RSMEANS-2026",
                Name = "RS Means Construction Cost Data 2026",
                Region = "North America",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<CostCategory>
                {
                    new CostCategory
                    {
                        Code = "03",
                        Name = "Concrete",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "03 11 13", Name = "Structural Cast-in-Place Concrete Forming", Unit = "SFCA", MaterialCost = 2.45m, LaborCost = 8.75m, EquipmentCost = 0.35m },
                            new CostItem { Code = "03 21 00", Name = "Reinforcement Bars", Unit = "Ton", MaterialCost = 1250.00m, LaborCost = 485.00m, EquipmentCost = 45.00m },
                            new CostItem { Code = "03 31 00", Name = "Structural Concrete 4000 PSI", Unit = "CY", MaterialCost = 145.00m, LaborCost = 42.00m, EquipmentCost = 28.00m },
                            new CostItem { Code = "03 35 00", Name = "Concrete Finishing", Unit = "SF", MaterialCost = 0.15m, LaborCost = 1.85m, EquipmentCost = 0.12m },
                            new CostItem { Code = "03 41 00", Name = "Precast Structural Concrete", Unit = "SF", MaterialCost = 28.50m, LaborCost = 12.25m, EquipmentCost = 8.75m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "04",
                        Name = "Masonry",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "04 21 13", Name = "Brick Masonry", Unit = "SF", MaterialCost = 8.25m, LaborCost = 14.50m, EquipmentCost = 0.85m },
                            new CostItem { Code = "04 22 00", Name = "Concrete Unit Masonry", Unit = "SF", MaterialCost = 4.75m, LaborCost = 9.25m, EquipmentCost = 0.45m },
                            new CostItem { Code = "04 43 00", Name = "Stone Masonry", Unit = "SF", MaterialCost = 32.00m, LaborCost = 28.50m, EquipmentCost = 2.15m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "05",
                        Name = "Metals",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "05 12 00", Name = "Structural Steel Framing", Unit = "Ton", MaterialCost = 2850.00m, LaborCost = 1250.00m, EquipmentCost = 485.00m },
                            new CostItem { Code = "05 21 00", Name = "Steel Joists", Unit = "Ton", MaterialCost = 2450.00m, LaborCost = 985.00m, EquipmentCost = 325.00m },
                            new CostItem { Code = "05 31 00", Name = "Steel Decking", Unit = "SF", MaterialCost = 3.85m, LaborCost = 1.45m, EquipmentCost = 0.35m },
                            new CostItem { Code = "05 50 00", Name = "Metal Fabrications", Unit = "LB", MaterialCost = 2.85m, LaborCost = 3.25m, EquipmentCost = 0.45m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "06",
                        Name = "Wood, Plastics, Composites",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "06 11 00", Name = "Wood Framing", Unit = "MBF", MaterialCost = 1450.00m, LaborCost = 685.00m, EquipmentCost = 125.00m },
                            new CostItem { Code = "06 16 00", Name = "Sheathing", Unit = "SF", MaterialCost = 1.25m, LaborCost = 0.85m, EquipmentCost = 0.08m },
                            new CostItem { Code = "06 20 00", Name = "Finish Carpentry", Unit = "LF", MaterialCost = 4.50m, LaborCost = 8.75m, EquipmentCost = 0.25m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "07",
                        Name = "Thermal and Moisture Protection",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "07 21 00", Name = "Thermal Insulation", Unit = "SF", MaterialCost = 1.85m, LaborCost = 0.95m, EquipmentCost = 0.05m },
                            new CostItem { Code = "07 41 00", Name = "Roof Panels", Unit = "SF", MaterialCost = 12.50m, LaborCost = 4.85m, EquipmentCost = 1.25m },
                            new CostItem { Code = "07 52 00", Name = "Modified Bituminous Membrane Roofing", Unit = "SF", MaterialCost = 3.45m, LaborCost = 2.85m, EquipmentCost = 0.35m },
                            new CostItem { Code = "07 62 00", Name = "Sheet Metal Flashing", Unit = "SF", MaterialCost = 4.25m, LaborCost = 6.50m, EquipmentCost = 0.45m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "08",
                        Name = "Openings",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "08 11 00", Name = "Metal Doors and Frames", Unit = "EA", MaterialCost = 485.00m, LaborCost = 185.00m, EquipmentCost = 25.00m },
                            new CostItem { Code = "08 14 00", Name = "Wood Doors", Unit = "EA", MaterialCost = 325.00m, LaborCost = 145.00m, EquipmentCost = 15.00m },
                            new CostItem { Code = "08 41 00", Name = "Entrances and Storefronts", Unit = "SF", MaterialCost = 45.00m, LaborCost = 18.50m, EquipmentCost = 4.25m },
                            new CostItem { Code = "08 51 00", Name = "Metal Windows", Unit = "SF", MaterialCost = 38.50m, LaborCost = 12.75m, EquipmentCost = 2.85m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "09",
                        Name = "Finishes",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "09 21 00", Name = "Plaster and Gypsum Board", Unit = "SF", MaterialCost = 1.45m, LaborCost = 2.85m, EquipmentCost = 0.15m },
                            new CostItem { Code = "09 30 00", Name = "Tiling", Unit = "SF", MaterialCost = 8.50m, LaborCost = 9.25m, EquipmentCost = 0.35m },
                            new CostItem { Code = "09 51 00", Name = "Acoustical Ceilings", Unit = "SF", MaterialCost = 2.85m, LaborCost = 2.45m, EquipmentCost = 0.25m },
                            new CostItem { Code = "09 65 00", Name = "Resilient Flooring", Unit = "SF", MaterialCost = 4.25m, LaborCost = 2.15m, EquipmentCost = 0.12m },
                            new CostItem { Code = "09 91 00", Name = "Painting", Unit = "SF", MaterialCost = 0.45m, LaborCost = 1.25m, EquipmentCost = 0.08m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "21",
                        Name = "Fire Suppression",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "21 13 00", Name = "Fire Suppression Sprinkler Systems", Unit = "SF", MaterialCost = 2.85m, LaborCost = 3.45m, EquipmentCost = 0.35m },
                            new CostItem { Code = "21 22 00", Name = "Clean-Agent Fire Extinguishing", Unit = "SF", MaterialCost = 8.50m, LaborCost = 4.25m, EquipmentCost = 0.85m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "22",
                        Name = "Plumbing",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "22 11 00", Name = "Facility Water Distribution", Unit = "LF", MaterialCost = 12.50m, LaborCost = 18.75m, EquipmentCost = 2.25m },
                            new CostItem { Code = "22 13 00", Name = "Facility Sanitary Sewerage", Unit = "LF", MaterialCost = 15.25m, LaborCost = 22.50m, EquipmentCost = 3.45m },
                            new CostItem { Code = "22 42 00", Name = "Commercial Plumbing Fixtures", Unit = "EA", MaterialCost = 850.00m, LaborCost = 285.00m, EquipmentCost = 45.00m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "23",
                        Name = "HVAC",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "23 31 00", Name = "HVAC Ducts and Casings", Unit = "LB", MaterialCost = 2.45m, LaborCost = 4.85m, EquipmentCost = 0.45m },
                            new CostItem { Code = "23 64 00", Name = "Packaged Water Chillers", Unit = "Ton", MaterialCost = 1450.00m, LaborCost = 485.00m, EquipmentCost = 185.00m },
                            new CostItem { Code = "23 73 00", Name = "Indoor Central-Station Air-Handling Units", Unit = "CFM", MaterialCost = 8.50m, LaborCost = 4.25m, EquipmentCost = 1.25m },
                            new CostItem { Code = "23 82 00", Name = "Convection Heating and Cooling Units", Unit = "EA", MaterialCost = 2850.00m, LaborCost = 685.00m, EquipmentCost = 125.00m }
                        }
                    },
                    new CostCategory
                    {
                        Code = "26",
                        Name = "Electrical",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "26 05 00", Name = "Common Work Results for Electrical", Unit = "SF", MaterialCost = 4.25m, LaborCost = 8.50m, EquipmentCost = 0.85m },
                            new CostItem { Code = "26 24 00", Name = "Switchboards and Panelboards", Unit = "EA", MaterialCost = 4850.00m, LaborCost = 1250.00m, EquipmentCost = 285.00m },
                            new CostItem { Code = "26 51 00", Name = "Interior Lighting", Unit = "EA", MaterialCost = 285.00m, LaborCost = 145.00m, EquipmentCost = 25.00m },
                            new CostItem { Code = "26 27 00", Name = "Low-Voltage Distribution Equipment", Unit = "EA", MaterialCost = 3250.00m, LaborCost = 850.00m, EquipmentCost = 185.00m }
                        }
                    }
                }
            };

            _costDatabases.TryAdd(rsMeansData.Id, rsMeansData);

            // Africa Regional Cost Data
            var africaData = new CostDatabase
            {
                Id = "AFRICA-REGIONAL-2026",
                Name = "Africa Regional Construction Costs 2026",
                Region = "East Africa",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                RegionalFactors = new Dictionary<string, decimal>
                {
                    { "Uganda", 0.65m },
                    { "Kenya", 0.72m },
                    { "Tanzania", 0.68m },
                    { "Rwanda", 0.70m },
                    { "South Africa", 0.55m },
                    { "Nigeria", 0.58m },
                    { "Ghana", 0.62m },
                    { "Ethiopia", 0.60m }
                },
                Categories = new List<CostCategory>
                {
                    new CostCategory
                    {
                        Code = "STRUCT",
                        Name = "Structural Works",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "STRUCT-01", Name = "Reinforced Concrete Frame", Unit = "M3", MaterialCost = 185.00m, LaborCost = 45.00m, EquipmentCost = 25.00m },
                            new CostItem { Code = "STRUCT-02", Name = "Steel Structure", Unit = "Ton", MaterialCost = 2200.00m, LaborCost = 450.00m, EquipmentCost = 185.00m },
                            new CostItem { Code = "STRUCT-03", Name = "Masonry Walls", Unit = "M2", MaterialCost = 35.00m, LaborCost = 18.00m, EquipmentCost = 2.50m }
                        }
                    }
                }
            };

            _costDatabases.TryAdd(africaData.Id, africaData);

            // UK/Europe Cost Data
            var ukData = new CostDatabase
            {
                Id = "UK-SPON-2026",
                Name = "Spon's Architects' and Builders' Price Book 2026",
                Region = "United Kingdom",
                Currency = "GBP",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<CostCategory>
                {
                    new CostCategory
                    {
                        Code = "1",
                        Name = "Substructure",
                        Items = new List<CostItem>
                        {
                            new CostItem { Code = "1.1", Name = "Standard Foundations", Unit = "M2", MaterialCost = 85.00m, LaborCost = 65.00m, EquipmentCost = 15.00m },
                            new CostItem { Code = "1.2", Name = "Basement Construction", Unit = "M2", MaterialCost = 245.00m, LaborCost = 185.00m, EquipmentCost = 45.00m }
                        }
                    }
                }
            };

            _costDatabases.TryAdd(ukData.Id, ukData);
        }

        private readonly Dictionary<string, MarketRate> _marketRates = new Dictionary<string, MarketRate>();

        private void InitializeMarketRates()
        {
            _marketRates["LABOR_GENERAL"] = new MarketRate { Category = "Labor", Type = "General Construction", RatePerHour = 35.00m, Region = "US Average" };
            _marketRates["LABOR_SKILLED"] = new MarketRate { Category = "Labor", Type = "Skilled Trades", RatePerHour = 55.00m, Region = "US Average" };
            _marketRates["LABOR_SPECIALIST"] = new MarketRate { Category = "Labor", Type = "Specialist/MEP", RatePerHour = 75.00m, Region = "US Average" };
            _marketRates["EQUIPMENT_CRANE"] = new MarketRate { Category = "Equipment", Type = "Tower Crane", RatePerDay = 1250.00m, Region = "US Average" };
            _marketRates["EQUIPMENT_EXCAVATOR"] = new MarketRate { Category = "Equipment", Type = "Excavator", RatePerDay = 485.00m, Region = "US Average" };
            _marketRates["MATERIAL_STEEL"] = new MarketRate { Category = "Material", Type = "Structural Steel", RatePerTon = 2850.00m, Region = "US Average", Volatility = 0.15m };
            _marketRates["MATERIAL_CONCRETE"] = new MarketRate { Category = "Material", Type = "Ready-Mix Concrete", RatePerCY = 145.00m, Region = "US Average", Volatility = 0.08m };
            _marketRates["MATERIAL_LUMBER"] = new MarketRate { Category = "Material", Type = "Framing Lumber", RatePerMBF = 1450.00m, Region = "US Average", Volatility = 0.25m };
        }

        #endregion

        #region Cost Estimation

        public async Task<CostEstimate> GenerateEstimateAsync(CostEstimateRequest request)
        {
            var estimate = new CostEstimate
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                EstimateType = request.EstimateType,
                CreatedDate = DateTime.UtcNow,
                Currency = request.Currency ?? "USD",
                Status = EstimateStatus.Draft
            };

            // Select appropriate cost database
            var database = SelectCostDatabase(request.Region, request.Currency);
            estimate.CostDatabaseId = database.Id;

            // Calculate quantities from model data
            var quantities = await CalculateQuantitiesAsync(request.ModelData);
            estimate.QuantityTakeoff = quantities;

            // Apply unit costs
            foreach (var item in quantities.Items)
            {
                var costItem = FindCostItem(database, item.CostCode);
                if (costItem != null)
                {
                    item.MaterialCost = item.Quantity * costItem.MaterialCost;
                    item.LaborCost = item.Quantity * costItem.LaborCost;
                    item.EquipmentCost = item.Quantity * costItem.EquipmentCost;
                    item.TotalCost = item.MaterialCost + item.LaborCost + item.EquipmentCost;
                }
            }

            // Apply regional factors
            if (database.RegionalFactors != null && database.RegionalFactors.ContainsKey(request.Region))
            {
                var factor = database.RegionalFactors[request.Region];
                foreach (var item in quantities.Items)
                {
                    item.LaborCost *= factor;
                    item.TotalCost = item.MaterialCost + item.LaborCost + item.EquipmentCost;
                }
            }

            // Calculate summaries by division
            estimate.DivisionSummaries = CalculateDivisionSummaries(quantities);

            // Calculate totals
            estimate.DirectCost = quantities.Items.Sum(i => i.TotalCost);
            estimate.GeneralConditions = estimate.DirectCost * (request.GeneralConditionsPercent / 100m);
            estimate.OverheadProfit = (estimate.DirectCost + estimate.GeneralConditions) * (request.OverheadProfitPercent / 100m);
            estimate.Contingency = (estimate.DirectCost + estimate.GeneralConditions + estimate.OverheadProfit) * (request.ContingencyPercent / 100m);
            estimate.Escalation = CalculateEscalation(estimate.DirectCost, request.ProjectDuration, request.EscalationRate);
            estimate.TotalCost = estimate.DirectCost + estimate.GeneralConditions + estimate.OverheadProfit + estimate.Contingency + estimate.Escalation;

            // Generate cost per SF
            if (request.GrossArea > 0)
            {
                estimate.CostPerSF = estimate.TotalCost / request.GrossArea;
            }

            // Confidence scoring
            estimate.ConfidenceLevel = CalculateConfidenceLevel(request.EstimateType, quantities);

            lock (_lockObject)
            {
                if (_projects.TryGetValue(request.ProjectId, out var project))
                {
                    project.Estimates.Add(estimate);
                }
            }

            return estimate;
        }

        private CostDatabase SelectCostDatabase(string region, string currency)
        {
            if (region?.Contains("Africa") == true || new[] { "Uganda", "Kenya", "Tanzania", "Rwanda", "Nigeria", "Ghana", "Ethiopia", "South Africa" }.Contains(region))
            {
                return _costDatabases["AFRICA-REGIONAL-2026"];
            }
            if (region == "UK" || region == "United Kingdom" || currency == "GBP")
            {
                return _costDatabases["UK-SPON-2026"];
            }
            return _costDatabases["RSMEANS-2026"];
        }

        private async Task<QuantityTakeoff> CalculateQuantitiesAsync(ModelData modelData)
        {
            var takeoff = new QuantityTakeoff
            {
                Id = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                Items = new List<QuantityItem>()
            };

            await Task.Run(() =>
            {
                // Calculate concrete quantities
                if (modelData?.ConcreteVolume > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "03 31 00",
                        Description = "Structural Concrete 4000 PSI",
                        Quantity = modelData.ConcreteVolume,
                        Unit = "CY"
                    });
                }

                // Calculate steel quantities
                if (modelData?.SteelWeight > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "05 12 00",
                        Description = "Structural Steel Framing",
                        Quantity = modelData.SteelWeight,
                        Unit = "Ton"
                    });
                }

                // Calculate wall areas
                if (modelData?.ExteriorWallArea > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "04 21 13",
                        Description = "Exterior Brick Masonry",
                        Quantity = modelData.ExteriorWallArea,
                        Unit = "SF"
                    });
                }

                // Calculate floor areas
                if (modelData?.FloorArea > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "09 65 00",
                        Description = "Resilient Flooring",
                        Quantity = modelData.FloorArea,
                        Unit = "SF"
                    });
                }

                // Calculate roof area
                if (modelData?.RoofArea > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "07 52 00",
                        Description = "Modified Bituminous Membrane Roofing",
                        Quantity = modelData.RoofArea,
                        Unit = "SF"
                    });
                }

                // Calculate MEP systems
                if (modelData?.PlumbingFixtureCount > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "22 42 00",
                        Description = "Commercial Plumbing Fixtures",
                        Quantity = modelData.PlumbingFixtureCount,
                        Unit = "EA"
                    });
                }

                if (modelData?.HVACTonnage > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "23 64 00",
                        Description = "Packaged Water Chillers",
                        Quantity = modelData.HVACTonnage,
                        Unit = "Ton"
                    });
                }

                if (modelData?.LightingFixtureCount > 0)
                {
                    takeoff.Items.Add(new QuantityItem
                    {
                        CostCode = "26 51 00",
                        Description = "Interior Lighting",
                        Quantity = modelData.LightingFixtureCount,
                        Unit = "EA"
                    });
                }
            });

            return takeoff;
        }

        private CostItem FindCostItem(CostDatabase database, string costCode)
        {
            foreach (var category in database.Categories)
            {
                var item = category.Items.FirstOrDefault(i => i.Code == costCode);
                if (item != null) return item;
            }
            return null;
        }

        private List<DivisionSummary> CalculateDivisionSummaries(QuantityTakeoff takeoff)
        {
            var summaries = takeoff.Items
                .GroupBy(i => i.CostCode.Substring(0, 2))
                .Select(g => new DivisionSummary
                {
                    DivisionCode = g.Key,
                    DivisionName = GetDivisionName(g.Key),
                    MaterialCost = g.Sum(i => i.MaterialCost),
                    LaborCost = g.Sum(i => i.LaborCost),
                    EquipmentCost = g.Sum(i => i.EquipmentCost),
                    TotalCost = g.Sum(i => i.TotalCost)
                })
                .OrderBy(s => s.DivisionCode)
                .ToList();

            return summaries;
        }

        private string GetDivisionName(string code)
        {
            var divisions = new Dictionary<string, string>
            {
                { "01", "General Requirements" },
                { "02", "Existing Conditions" },
                { "03", "Concrete" },
                { "04", "Masonry" },
                { "05", "Metals" },
                { "06", "Wood, Plastics, Composites" },
                { "07", "Thermal and Moisture Protection" },
                { "08", "Openings" },
                { "09", "Finishes" },
                { "10", "Specialties" },
                { "11", "Equipment" },
                { "12", "Furnishings" },
                { "13", "Special Construction" },
                { "14", "Conveying Equipment" },
                { "21", "Fire Suppression" },
                { "22", "Plumbing" },
                { "23", "HVAC" },
                { "25", "Integrated Automation" },
                { "26", "Electrical" },
                { "27", "Communications" },
                { "28", "Electronic Safety and Security" },
                { "31", "Earthwork" },
                { "32", "Exterior Improvements" },
                { "33", "Utilities" }
            };

            return divisions.TryGetValue(code, out var name) ? name : "Unknown Division";
        }

        private decimal CalculateEscalation(decimal baseCost, int months, decimal annualRate)
        {
            var years = months / 12.0m;
            var factor = (decimal)Math.Pow((double)(1 + annualRate / 100), (double)years) - 1;
            return baseCost * factor;
        }

        private decimal CalculateConfidenceLevel(EstimateType type, QuantityTakeoff takeoff)
        {
            var baseConfidence = type switch
            {
                EstimateType.Conceptual => 0.60m,
                EstimateType.SchematicDesign => 0.70m,
                EstimateType.DesignDevelopment => 0.80m,
                EstimateType.ConstructionDocuments => 0.90m,
                EstimateType.Bid => 0.95m,
                _ => 0.65m
            };

            // Adjust based on quantity completeness
            var quantityFactor = takeoff.Items.Count > 20 ? 1.0m : takeoff.Items.Count / 20.0m;

            return Math.Min(baseConfidence * quantityFactor * 1.1m, 0.98m);
        }

        #endregion

        #region Value Engineering

        public ValueEngineeringStudy CreateVEStudy(VEStudyRequest request)
        {
            var study = new ValueEngineeringStudy
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                CreatedDate = DateTime.UtcNow,
                TargetSavings = request.TargetSavings,
                Status = VEStatus.InProgress,
                Proposals = new List<VEProposal>()
            };

            // Generate VE proposals based on cost analysis
            GenerateVEProposals(study, request);

            _veStudies.TryAdd(study.Id, study);
            return study;
        }

        private void GenerateVEProposals(ValueEngineeringStudy study, VEStudyRequest request)
        {
            // Structural VE opportunities
            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Structural",
                Title = "Optimize Concrete Mix Design",
                Description = "Use high-performance concrete to reduce member sizes and reinforcement",
                OriginalCost = request.EstimatedCost * 0.15m,
                ProposedCost = request.EstimatedCost * 0.13m,
                Savings = request.EstimatedCost * 0.02m,
                ImplementationRisk = RiskLevel.Low,
                QualityImpact = QualityImpact.Improved,
                ScheduleImpact = 0,
                Status = VEProposalStatus.Proposed
            });

            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Structural",
                Title = "Value Engineer Foundation System",
                Description = "Consider mat foundation vs individual footings based on soil conditions",
                OriginalCost = request.EstimatedCost * 0.08m,
                ProposedCost = request.EstimatedCost * 0.065m,
                Savings = request.EstimatedCost * 0.015m,
                ImplementationRisk = RiskLevel.Medium,
                QualityImpact = QualityImpact.Equivalent,
                ScheduleImpact = -5,
                Status = VEProposalStatus.Proposed
            });

            // Envelope VE opportunities
            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Envelope",
                Title = "Optimize Curtain Wall System",
                Description = "Use unitized curtain wall system for faster installation and reduced field labor",
                OriginalCost = request.EstimatedCost * 0.12m,
                ProposedCost = request.EstimatedCost * 0.105m,
                Savings = request.EstimatedCost * 0.015m,
                ImplementationRisk = RiskLevel.Low,
                QualityImpact = QualityImpact.Improved,
                ScheduleImpact = -15,
                Status = VEProposalStatus.Proposed
            });

            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Envelope",
                Title = "Alternative Roofing System",
                Description = "Consider TPO single-ply membrane vs built-up roofing",
                OriginalCost = request.EstimatedCost * 0.04m,
                ProposedCost = request.EstimatedCost * 0.032m,
                Savings = request.EstimatedCost * 0.008m,
                ImplementationRisk = RiskLevel.Low,
                QualityImpact = QualityImpact.Equivalent,
                ScheduleImpact = -3,
                Status = VEProposalStatus.Proposed
            });

            // MEP VE opportunities
            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Mechanical",
                Title = "Variable Refrigerant Flow System",
                Description = "Replace conventional chilled water system with VRF for zones under 50,000 SF",
                OriginalCost = request.EstimatedCost * 0.10m,
                ProposedCost = request.EstimatedCost * 0.085m,
                Savings = request.EstimatedCost * 0.015m,
                ImplementationRisk = RiskLevel.Medium,
                QualityImpact = QualityImpact.Improved,
                ScheduleImpact = -10,
                Status = VEProposalStatus.Proposed
            });

            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Electrical",
                Title = "LED Lighting Throughout",
                Description = "Upgrade all lighting to LED with integrated daylight harvesting controls",
                OriginalCost = request.EstimatedCost * 0.05m,
                ProposedCost = request.EstimatedCost * 0.055m,
                Savings = request.EstimatedCost * -0.005m, // Higher first cost
                LifecycleSavings = request.EstimatedCost * 0.02m, // But lifecycle savings
                ImplementationRisk = RiskLevel.Low,
                QualityImpact = QualityImpact.Improved,
                ScheduleImpact = 0,
                Status = VEProposalStatus.Proposed
            });

            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Plumbing",
                Title = "Pre-fabricated Bathroom Pods",
                Description = "Use modular pre-fabricated bathroom units for repetitive layouts",
                OriginalCost = request.EstimatedCost * 0.03m,
                ProposedCost = request.EstimatedCost * 0.027m,
                Savings = request.EstimatedCost * 0.003m,
                ImplementationRisk = RiskLevel.Low,
                QualityImpact = QualityImpact.Improved,
                ScheduleImpact = -20,
                Status = VEProposalStatus.Proposed
            });

            // Finishes VE opportunities
            study.Proposals.Add(new VEProposal
            {
                Id = Guid.NewGuid().ToString(),
                Category = "Finishes",
                Title = "Polished Concrete Floors",
                Description = "Use polished concrete in lieu of carpet/VCT in back-of-house areas",
                OriginalCost = request.EstimatedCost * 0.025m,
                ProposedCost = request.EstimatedCost * 0.018m,
                Savings = request.EstimatedCost * 0.007m,
                ImplementationRisk = RiskLevel.Low,
                QualityImpact = QualityImpact.Equivalent,
                ScheduleImpact = -5,
                Status = VEProposalStatus.Proposed
            });

            // Calculate total potential savings
            study.TotalPotentialSavings = study.Proposals.Sum(p => p.Savings);
            study.TotalLifecycleSavings = study.Proposals.Sum(p => p.LifecycleSavings);
        }

        public VEProposal EvaluateVEProposal(string studyId, string proposalId, VEEvaluationRequest request)
        {
            if (!_veStudies.TryGetValue(studyId, out var study)) return null;

            var proposal = study.Proposals.FirstOrDefault(p => p.Id == proposalId);
            if (proposal == null) return null;

            proposal.Status = request.Approved ? VEProposalStatus.Approved : VEProposalStatus.Rejected;
            proposal.EvaluationNotes = request.Notes;
            proposal.EvaluatedBy = request.EvaluatedBy;
            proposal.EvaluatedDate = DateTime.UtcNow;

            // Recalculate study totals
            study.ApprovedSavings = study.Proposals
                .Where(p => p.Status == VEProposalStatus.Approved)
                .Sum(p => p.Savings);

            return proposal;
        }

        #endregion

        #region Budget Management

        public BudgetForecast CreateBudgetForecast(BudgetForecastRequest request)
        {
            var forecast = new BudgetForecast
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                CreatedDate = DateTime.UtcNow,
                ForecastPeriods = new List<ForecastPeriod>(),
                TotalBudget = request.TotalBudget,
                Currency = request.Currency ?? "USD"
            };

            // Generate monthly forecast based on S-curve distribution
            GenerateForecastPeriods(forecast, request);

            // Calculate cash flow
            CalculateCashFlow(forecast);

            _forecasts.TryAdd(forecast.Id, forecast);
            return forecast;
        }

        private void GenerateForecastPeriods(BudgetForecast forecast, BudgetForecastRequest request)
        {
            var totalMonths = request.ProjectDurationMonths;
            var totalBudget = request.TotalBudget;

            // S-curve distribution factors (cumulative)
            for (int month = 1; month <= totalMonths; month++)
            {
                var progress = (double)month / totalMonths;
                var sCurveFactor = 1 / (1 + Math.Exp(-10 * (progress - 0.5))); // Sigmoid function
                var monthlyFactor = sCurveFactor - (month > 1 ? 1 / (1 + Math.Exp(-10 * ((double)(month - 1) / totalMonths - 0.5))) : 0);

                var period = new ForecastPeriod
                {
                    Period = month,
                    PeriodDate = request.StartDate.AddMonths(month - 1),
                    PlannedSpend = totalBudget * (decimal)monthlyFactor,
                    CumulativePlanned = totalBudget * (decimal)sCurveFactor
                };

                // Distribute by cost category
                period.CategoryBreakdown = new Dictionary<string, decimal>
                {
                    { "Labor", period.PlannedSpend * 0.40m },
                    { "Materials", period.PlannedSpend * 0.45m },
                    { "Equipment", period.PlannedSpend * 0.10m },
                    { "Other", period.PlannedSpend * 0.05m }
                };

                forecast.ForecastPeriods.Add(period);
            }
        }

        private void CalculateCashFlow(BudgetForecast forecast)
        {
            decimal cumulativeActual = 0;
            foreach (var period in forecast.ForecastPeriods)
            {
                period.CumulativeActual = cumulativeActual + period.ActualSpend;
                cumulativeActual = period.CumulativeActual;

                // Calculate variance
                period.Variance = period.ActualSpend - period.PlannedSpend;
                period.CumulativeVariance = period.CumulativeActual - period.CumulativePlanned;

                // Calculate performance indices
                if (period.CumulativePlanned > 0)
                {
                    period.CPI = period.CumulativeActual > 0
                        ? period.CumulativePlanned / period.CumulativeActual
                        : 1.0m;
                }
            }
        }

        public BudgetStatus UpdateActualCosts(string forecastId, ActualCostUpdate update)
        {
            if (!_forecasts.TryGetValue(forecastId, out var forecast)) return null;

            var period = forecast.ForecastPeriods.FirstOrDefault(p => p.Period == update.Period);
            if (period == null) return null;

            period.ActualSpend = update.ActualSpend;
            period.ActualCategoryBreakdown = update.CategoryBreakdown;

            // Recalculate cash flow
            CalculateCashFlow(forecast);

            // Check budget thresholds
            var status = new BudgetStatus
            {
                ForecastId = forecastId,
                CurrentPeriod = update.Period,
                TotalBudget = forecast.TotalBudget,
                TotalPlanned = forecast.ForecastPeriods.Sum(p => p.PlannedSpend),
                TotalActual = forecast.ForecastPeriods.Sum(p => p.ActualSpend),
                CPI = period.CPI,
                Status = DetermineBudgetHealth(period.CPI)
            };

            // Raise alert if threshold exceeded
            if (status.Status == BudgetHealth.Critical || status.Status == BudgetHealth.Warning)
            {
                BudgetThresholdExceeded?.Invoke(this, new BudgetEventArgs
                {
                    ForecastId = forecastId,
                    Status = status,
                    Message = $"Budget {status.Status}: CPI = {period.CPI:F2}"
                });
            }

            return status;
        }

        private BudgetHealth DetermineBudgetHealth(decimal cpi)
        {
            if (cpi >= 0.95m) return BudgetHealth.OnTrack;
            if (cpi >= 0.90m) return BudgetHealth.Warning;
            if (cpi >= 0.80m) return BudgetHealth.AtRisk;
            return BudgetHealth.Critical;
        }

        public EarnedValueAnalysis CalculateEarnedValue(string forecastId, int period)
        {
            if (!_forecasts.TryGetValue(forecastId, out var forecast)) return null;

            var currentPeriod = forecast.ForecastPeriods.FirstOrDefault(p => p.Period == period);
            if (currentPeriod == null) return null;

            var completedPeriods = forecast.ForecastPeriods.Where(p => p.Period <= period).ToList();

            var bcws = completedPeriods.Sum(p => p.PlannedSpend); // Planned Value
            var acwp = completedPeriods.Sum(p => p.ActualSpend); // Actual Cost
            var bcwp = bcws * 0.95m; // Earned Value (simplified - should come from progress)

            return new EarnedValueAnalysis
            {
                ForecastId = forecastId,
                Period = period,
                BCWS_PlannedValue = bcws,
                ACWP_ActualCost = acwp,
                BCWP_EarnedValue = bcwp,
                CostVariance = bcwp - acwp,
                ScheduleVariance = bcwp - bcws,
                CPI = acwp > 0 ? bcwp / acwp : 1.0m,
                SPI = bcws > 0 ? bcwp / bcws : 1.0m,
                EAC_EstimateAtCompletion = acwp > 0 ? forecast.TotalBudget / (bcwp / acwp) : forecast.TotalBudget,
                VAC_VarianceAtCompletion = forecast.TotalBudget - (acwp > 0 ? forecast.TotalBudget / (bcwp / acwp) : forecast.TotalBudget),
                TCPI = (forecast.TotalBudget - bcwp) / (forecast.TotalBudget - acwp)
            };
        }

        #endregion

        #region Market Analysis

        public MarketAnalysis AnalyzeMarketConditions(MarketAnalysisRequest request)
        {
            var analysis = new MarketAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                Region = request.Region,
                AnalysisDate = DateTime.UtcNow,
                Trends = new List<MarketTrend>(),
                Recommendations = new List<string>()
            };

            // Analyze material trends
            analysis.Trends.Add(new MarketTrend
            {
                Category = "Steel",
                CurrentRate = _marketRates["MATERIAL_STEEL"].RatePerTon,
                TrendDirection = TrendDirection.Increasing,
                ChangePercent = 8.5m,
                Volatility = _marketRates["MATERIAL_STEEL"].Volatility,
                Forecast6Month = _marketRates["MATERIAL_STEEL"].RatePerTon * 1.05m,
                Forecast12Month = _marketRates["MATERIAL_STEEL"].RatePerTon * 1.08m
            });

            analysis.Trends.Add(new MarketTrend
            {
                Category = "Concrete",
                CurrentRate = _marketRates["MATERIAL_CONCRETE"].RatePerCY,
                TrendDirection = TrendDirection.Stable,
                ChangePercent = 2.1m,
                Volatility = _marketRates["MATERIAL_CONCRETE"].Volatility,
                Forecast6Month = _marketRates["MATERIAL_CONCRETE"].RatePerCY * 1.01m,
                Forecast12Month = _marketRates["MATERIAL_CONCRETE"].RatePerCY * 1.025m
            });

            analysis.Trends.Add(new MarketTrend
            {
                Category = "Lumber",
                CurrentRate = _marketRates["MATERIAL_LUMBER"].RatePerMBF,
                TrendDirection = TrendDirection.Decreasing,
                ChangePercent = -5.2m,
                Volatility = _marketRates["MATERIAL_LUMBER"].Volatility,
                Forecast6Month = _marketRates["MATERIAL_LUMBER"].RatePerMBF * 0.97m,
                Forecast12Month = _marketRates["MATERIAL_LUMBER"].RatePerMBF * 0.95m
            });

            analysis.Trends.Add(new MarketTrend
            {
                Category = "Skilled Labor",
                CurrentRate = _marketRates["LABOR_SKILLED"].RatePerHour,
                TrendDirection = TrendDirection.Increasing,
                ChangePercent = 4.5m,
                Volatility = 0.08m,
                Forecast6Month = _marketRates["LABOR_SKILLED"].RatePerHour * 1.02m,
                Forecast12Month = _marketRates["LABOR_SKILLED"].RatePerHour * 1.045m
            });

            // Generate recommendations
            foreach (var trend in analysis.Trends)
            {
                if (trend.TrendDirection == TrendDirection.Increasing && trend.ChangePercent > 5)
                {
                    analysis.Recommendations.Add($"Consider early procurement of {trend.Category} to lock in current pricing");
                }
                if (trend.Volatility > 0.20m)
                {
                    analysis.Recommendations.Add($"Include additional contingency for {trend.Category} due to high market volatility");
                }
            }

            // Overall market assessment
            analysis.OverallAssessment = DetermineMarketAssessment(analysis.Trends);
            analysis.RecommendedContingency = CalculateRecommendedContingency(analysis.Trends);

            return analysis;
        }

        private MarketAssessment DetermineMarketAssessment(List<MarketTrend> trends)
        {
            var avgChange = trends.Average(t => t.ChangePercent);
            var avgVolatility = trends.Average(t => t.Volatility);

            if (avgChange > 5 || avgVolatility > 0.15m)
                return MarketAssessment.Challenging;
            if (avgChange > 2 || avgVolatility > 0.10m)
                return MarketAssessment.Moderate;
            return MarketAssessment.Favorable;
        }

        private decimal CalculateRecommendedContingency(List<MarketTrend> trends)
        {
            var baseContingency = 5.0m; // 5% base
            var volatilityAdder = trends.Average(t => t.Volatility) * 20; // Add based on volatility
            var trendAdder = Math.Max(0, trends.Average(t => t.ChangePercent)) * 0.5m; // Add for increasing trends

            return Math.Round(baseContingency + volatilityAdder + trendAdder, 1);
        }

        #endregion

        #region Cost Benchmarking

        public BenchmarkAnalysis BenchmarkProject(BenchmarkRequest request)
        {
            var analysis = new BenchmarkAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                AnalysisDate = DateTime.UtcNow,
                ProjectType = request.ProjectType,
                Location = request.Location,
                ProjectCostPerSF = request.TotalCost / request.GrossArea,
                Comparisons = new List<BenchmarkComparison>()
            };

            // Industry benchmarks by building type
            var benchmarks = GetIndustryBenchmarks(request.ProjectType);

            foreach (var benchmark in benchmarks)
            {
                var comparison = new BenchmarkComparison
                {
                    Category = benchmark.Category,
                    ProjectValue = GetProjectValueForCategory(request, benchmark.Category),
                    BenchmarkLow = benchmark.LowValue,
                    BenchmarkMedian = benchmark.MedianValue,
                    BenchmarkHigh = benchmark.HighValue,
                    Unit = benchmark.Unit
                };

                comparison.PercentileRank = CalculatePercentile(
                    comparison.ProjectValue,
                    comparison.BenchmarkLow,
                    comparison.BenchmarkMedian,
                    comparison.BenchmarkHigh);

                comparison.Assessment = DetermineBenchmarkAssessment(comparison.PercentileRank);

                analysis.Comparisons.Add(comparison);
            }

            // Overall ranking
            analysis.OverallPercentile = analysis.Comparisons.Average(c => c.PercentileRank);
            analysis.OverallAssessment = DetermineOverallAssessment(analysis.OverallPercentile);

            return analysis;
        }

        private List<IndustryBenchmark> GetIndustryBenchmarks(string projectType)
        {
            var benchmarks = new List<IndustryBenchmark>();

            switch (projectType.ToLower())
            {
                case "office":
                    benchmarks.Add(new IndustryBenchmark { Category = "Total Cost/SF", LowValue = 185, MedianValue = 285, HighValue = 450, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Structure Cost/SF", LowValue = 28, MedianValue = 42, HighValue = 68, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "MEP Cost/SF", LowValue = 45, MedianValue = 72, HighValue = 115, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Envelope Cost/SF", LowValue = 35, MedianValue = 55, HighValue = 95, Unit = "$/SF" });
                    break;
                case "healthcare":
                    benchmarks.Add(new IndustryBenchmark { Category = "Total Cost/SF", LowValue = 350, MedianValue = 550, HighValue = 850, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Structure Cost/SF", LowValue = 45, MedianValue = 72, HighValue = 110, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "MEP Cost/SF", LowValue = 95, MedianValue = 165, HighValue = 265, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Envelope Cost/SF", LowValue = 45, MedianValue = 75, HighValue = 125, Unit = "$/SF" });
                    break;
                case "education":
                    benchmarks.Add(new IndustryBenchmark { Category = "Total Cost/SF", LowValue = 225, MedianValue = 325, HighValue = 485, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Structure Cost/SF", LowValue = 32, MedianValue = 48, HighValue = 72, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "MEP Cost/SF", LowValue = 55, MedianValue = 85, HighValue = 135, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Envelope Cost/SF", LowValue = 38, MedianValue = 58, HighValue = 95, Unit = "$/SF" });
                    break;
                case "residential":
                    benchmarks.Add(new IndustryBenchmark { Category = "Total Cost/SF", LowValue = 145, MedianValue = 225, HighValue = 385, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Structure Cost/SF", LowValue = 22, MedianValue = 35, HighValue = 55, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "MEP Cost/SF", LowValue = 35, MedianValue = 55, HighValue = 95, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Envelope Cost/SF", LowValue = 28, MedianValue = 45, HighValue = 78, Unit = "$/SF" });
                    break;
                default:
                    benchmarks.Add(new IndustryBenchmark { Category = "Total Cost/SF", LowValue = 175, MedianValue = 275, HighValue = 425, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Structure Cost/SF", LowValue = 28, MedianValue = 45, HighValue = 70, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "MEP Cost/SF", LowValue = 45, MedianValue = 75, HighValue = 120, Unit = "$/SF" });
                    benchmarks.Add(new IndustryBenchmark { Category = "Envelope Cost/SF", LowValue = 32, MedianValue = 52, HighValue = 88, Unit = "$/SF" });
                    break;
            }

            return benchmarks;
        }

        private decimal GetProjectValueForCategory(BenchmarkRequest request, string category)
        {
            return category switch
            {
                "Total Cost/SF" => request.TotalCost / request.GrossArea,
                "Structure Cost/SF" => request.StructureCost / request.GrossArea,
                "MEP Cost/SF" => request.MEPCost / request.GrossArea,
                "Envelope Cost/SF" => request.EnvelopeCost / request.GrossArea,
                _ => 0
            };
        }

        private decimal CalculatePercentile(decimal value, decimal low, decimal median, decimal high)
        {
            if (value <= low) return 10;
            if (value <= median) return 10 + (value - low) / (median - low) * 40;
            if (value <= high) return 50 + (value - median) / (high - median) * 40;
            return 90 + Math.Min((value - high) / high * 10, 10);
        }

        private string DetermineBenchmarkAssessment(decimal percentile)
        {
            if (percentile <= 25) return "Below Market - Excellent Value";
            if (percentile <= 50) return "Below Median - Good Value";
            if (percentile <= 75) return "Above Median - Premium";
            return "Above Market - High Cost";
        }

        private string DetermineOverallAssessment(decimal percentile)
        {
            if (percentile <= 30) return "Project costs are well below market average - excellent cost efficiency";
            if (percentile <= 50) return "Project costs are below market median - good value";
            if (percentile <= 70) return "Project costs are above median - consider value engineering";
            return "Project costs are significantly above market - detailed cost review recommended";
        }

        #endregion

        #region Helper Methods

        public CostProject CreateProject(CostProjectRequest request)
        {
            var project = new CostProject
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                ProjectType = request.ProjectType,
                Location = request.Location,
                Currency = request.Currency ?? "USD",
                CreatedDate = DateTime.UtcNow,
                Estimates = new List<CostEstimate>(),
                VEStudies = new List<string>(),
                Forecasts = new List<string>()
            };

            _projects.TryAdd(project.Id, project);
            return project;
        }

        public CostProject GetProject(string projectId)
        {
            _projects.TryGetValue(projectId, out var project);
            return project;
        }

        public List<CostDatabase> GetAvailableDatabases()
        {
            return _costDatabases.Values.ToList();
        }

        public Dictionary<string, MarketRate> GetCurrentMarketRates()
        {
            return new Dictionary<string, MarketRate>(_marketRates);
        }

        #endregion
    }

    #region Data Models

    public class CostProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public string Currency { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<CostEstimate> Estimates { get; set; }
        public List<string> VEStudies { get; set; }
        public List<string> Forecasts { get; set; }
    }

    public class CostDatabase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Region { get; set; }
        public string Currency { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<string, decimal> RegionalFactors { get; set; }
        public List<CostCategory> Categories { get; set; }
    }

    public class CostCategory
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public List<CostItem> Items { get; set; }
    }

    public class CostItem
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal EquipmentCost { get; set; }
        public decimal TotalCost => MaterialCost + LaborCost + EquipmentCost;
    }

    public class MarketRate
    {
        public string Category { get; set; }
        public string Type { get; set; }
        public decimal RatePerHour { get; set; }
        public decimal RatePerDay { get; set; }
        public decimal RatePerTon { get; set; }
        public decimal RatePerCY { get; set; }
        public decimal RatePerMBF { get; set; }
        public string Region { get; set; }
        public decimal Volatility { get; set; }
    }

    public class CostEstimateRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public EstimateType EstimateType { get; set; }
        public string Region { get; set; }
        public string Currency { get; set; }
        public ModelData ModelData { get; set; }
        public decimal GrossArea { get; set; }
        public decimal GeneralConditionsPercent { get; set; } = 8.0m;
        public decimal OverheadProfitPercent { get; set; } = 10.0m;
        public decimal ContingencyPercent { get; set; } = 5.0m;
        public int ProjectDuration { get; set; } = 24;
        public decimal EscalationRate { get; set; } = 3.0m;
    }

    public class ModelData
    {
        public decimal ConcreteVolume { get; set; }
        public decimal SteelWeight { get; set; }
        public decimal ExteriorWallArea { get; set; }
        public decimal InteriorWallArea { get; set; }
        public decimal FloorArea { get; set; }
        public decimal RoofArea { get; set; }
        public int PlumbingFixtureCount { get; set; }
        public decimal HVACTonnage { get; set; }
        public int LightingFixtureCount { get; set; }
        public int DoorCount { get; set; }
        public int WindowCount { get; set; }
    }

    public class CostEstimate
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public EstimateType EstimateType { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Currency { get; set; }
        public string CostDatabaseId { get; set; }
        public EstimateStatus Status { get; set; }
        public QuantityTakeoff QuantityTakeoff { get; set; }
        public List<DivisionSummary> DivisionSummaries { get; set; }
        public decimal DirectCost { get; set; }
        public decimal GeneralConditions { get; set; }
        public decimal OverheadProfit { get; set; }
        public decimal Contingency { get; set; }
        public decimal Escalation { get; set; }
        public decimal TotalCost { get; set; }
        public decimal CostPerSF { get; set; }
        public decimal ConfidenceLevel { get; set; }
    }

    public class QuantityTakeoff
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<QuantityItem> Items { get; set; }
    }

    public class QuantityItem
    {
        public string CostCode { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal EquipmentCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class DivisionSummary
    {
        public string DivisionCode { get; set; }
        public string DivisionName { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal EquipmentCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class VEStudyRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal TargetSavings { get; set; }
    }

    public class ValueEngineeringStudy
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal TargetSavings { get; set; }
        public decimal TotalPotentialSavings { get; set; }
        public decimal TotalLifecycleSavings { get; set; }
        public decimal ApprovedSavings { get; set; }
        public VEStatus Status { get; set; }
        public List<VEProposal> Proposals { get; set; }
    }

    public class VEProposal
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal OriginalCost { get; set; }
        public decimal ProposedCost { get; set; }
        public decimal Savings { get; set; }
        public decimal LifecycleSavings { get; set; }
        public RiskLevel ImplementationRisk { get; set; }
        public QualityImpact QualityImpact { get; set; }
        public int ScheduleImpact { get; set; }
        public VEProposalStatus Status { get; set; }
        public string EvaluationNotes { get; set; }
        public string EvaluatedBy { get; set; }
        public DateTime? EvaluatedDate { get; set; }
    }

    public class VEEvaluationRequest
    {
        public bool Approved { get; set; }
        public string Notes { get; set; }
        public string EvaluatedBy { get; set; }
    }

    public class BudgetForecastRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public decimal TotalBudget { get; set; }
        public int ProjectDurationMonths { get; set; }
        public DateTime StartDate { get; set; }
        public string Currency { get; set; }
    }

    public class BudgetForecast
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal TotalBudget { get; set; }
        public string Currency { get; set; }
        public List<ForecastPeriod> ForecastPeriods { get; set; }
    }

    public class ForecastPeriod
    {
        public int Period { get; set; }
        public DateTime PeriodDate { get; set; }
        public decimal PlannedSpend { get; set; }
        public decimal ActualSpend { get; set; }
        public decimal CumulativePlanned { get; set; }
        public decimal CumulativeActual { get; set; }
        public decimal Variance { get; set; }
        public decimal CumulativeVariance { get; set; }
        public decimal CPI { get; set; }
        public Dictionary<string, decimal> CategoryBreakdown { get; set; }
        public Dictionary<string, decimal> ActualCategoryBreakdown { get; set; }
    }

    public class ActualCostUpdate
    {
        public int Period { get; set; }
        public decimal ActualSpend { get; set; }
        public Dictionary<string, decimal> CategoryBreakdown { get; set; }
    }

    public class BudgetStatus
    {
        public string ForecastId { get; set; }
        public int CurrentPeriod { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalPlanned { get; set; }
        public decimal TotalActual { get; set; }
        public decimal CPI { get; set; }
        public BudgetHealth Status { get; set; }
    }

    public class EarnedValueAnalysis
    {
        public string ForecastId { get; set; }
        public int Period { get; set; }
        public decimal BCWS_PlannedValue { get; set; }
        public decimal ACWP_ActualCost { get; set; }
        public decimal BCWP_EarnedValue { get; set; }
        public decimal CostVariance { get; set; }
        public decimal ScheduleVariance { get; set; }
        public decimal CPI { get; set; }
        public decimal SPI { get; set; }
        public decimal EAC_EstimateAtCompletion { get; set; }
        public decimal VAC_VarianceAtCompletion { get; set; }
        public decimal TCPI { get; set; }
    }

    public class MarketAnalysisRequest
    {
        public string Region { get; set; }
        public List<string> MaterialCategories { get; set; }
    }

    public class MarketAnalysis
    {
        public string Id { get; set; }
        public string Region { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<MarketTrend> Trends { get; set; }
        public MarketAssessment OverallAssessment { get; set; }
        public decimal RecommendedContingency { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class MarketTrend
    {
        public string Category { get; set; }
        public decimal CurrentRate { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public decimal ChangePercent { get; set; }
        public decimal Volatility { get; set; }
        public decimal Forecast6Month { get; set; }
        public decimal Forecast12Month { get; set; }
    }

    public class BenchmarkRequest
    {
        public string ProjectId { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public decimal GrossArea { get; set; }
        public decimal TotalCost { get; set; }
        public decimal StructureCost { get; set; }
        public decimal MEPCost { get; set; }
        public decimal EnvelopeCost { get; set; }
    }

    public class BenchmarkAnalysis
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public decimal ProjectCostPerSF { get; set; }
        public List<BenchmarkComparison> Comparisons { get; set; }
        public decimal OverallPercentile { get; set; }
        public string OverallAssessment { get; set; }
    }

    public class BenchmarkComparison
    {
        public string Category { get; set; }
        public decimal ProjectValue { get; set; }
        public decimal BenchmarkLow { get; set; }
        public decimal BenchmarkMedian { get; set; }
        public decimal BenchmarkHigh { get; set; }
        public string Unit { get; set; }
        public decimal PercentileRank { get; set; }
        public string Assessment { get; set; }
    }

    public class IndustryBenchmark
    {
        public string Category { get; set; }
        public decimal LowValue { get; set; }
        public decimal MedianValue { get; set; }
        public decimal HighValue { get; set; }
        public string Unit { get; set; }
    }

    public class CostProjectRequest
    {
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public string Currency { get; set; }
    }

    public class CostAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
        public decimal Threshold { get; set; }
        public decimal ActualValue { get; set; }
    }

    public class BudgetEventArgs : EventArgs
    {
        public string ForecastId { get; set; }
        public BudgetStatus Status { get; set; }
        public string Message { get; set; }
    }

    public enum EstimateType
    {
        Conceptual,
        SchematicDesign,
        DesignDevelopment,
        ConstructionDocuments,
        Bid,
        AsBuilt
    }

    public enum EstimateStatus
    {
        Draft,
        InReview,
        Approved,
        Superseded,
        Archived
    }

    public enum VEStatus
    {
        InProgress,
        Complete,
        Implemented
    }

    public enum VEProposalStatus
    {
        Proposed,
        UnderReview,
        Approved,
        Rejected,
        Implemented
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High
    }

    public enum QualityImpact
    {
        Improved,
        Equivalent,
        Reduced
    }

    public enum BudgetHealth
    {
        OnTrack,
        Warning,
        AtRisk,
        Critical
    }

    public enum TrendDirection
    {
        Increasing,
        Stable,
        Decreasing
    }

    public enum MarketAssessment
    {
        Favorable,
        Moderate,
        Challenging
    }

    #endregion
}
