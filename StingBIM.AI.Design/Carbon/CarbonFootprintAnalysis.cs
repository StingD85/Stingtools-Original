using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Design.Carbon
{
    /// <summary>
    /// Comprehensive carbon footprint analysis engine for building lifecycle assessment.
    /// Calculates embodied carbon, operational carbon, and provides optimization recommendations.
    /// Includes regional material carbon factors with African market focus.
    /// </summary>
    public class CarbonFootprintAnalysisEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, MaterialCarbonFactor> _materialCarbonFactors;
        private readonly Dictionary<string, double> _energyCarbonIntensity;
        private readonly Dictionary<string, TransportEmissionFactor> _transportFactors;
        private readonly List<CarbonAssessment> _assessmentHistory;
        private readonly object _lock = new object();

        public CarbonFootprintAnalysisEngine()
        {
            _materialCarbonFactors = InitializeMaterialCarbonFactors();
            _energyCarbonIntensity = InitializeEnergyCarbonIntensity();
            _transportFactors = InitializeTransportFactors();
            _assessmentHistory = new List<CarbonAssessment>();

            Logger.Info("CarbonFootprintAnalysisEngine initialized with {0} material factors, {1} energy intensities",
                _materialCarbonFactors.Count, _energyCarbonIntensity.Count);
        }

        #region Material Carbon Factors Database

        private Dictionary<string, MaterialCarbonFactor> InitializeMaterialCarbonFactors()
        {
            // Comprehensive material carbon factors (kgCO2e per unit)
            // Based on ICE Database, EPD data, and regional African studies
            return new Dictionary<string, MaterialCarbonFactor>(StringComparer.OrdinalIgnoreCase)
            {
                // Concrete and Cement
                ["Concrete_C20"] = new MaterialCarbonFactor("Concrete C20/25", 240, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Concrete_C30"] = new MaterialCarbonFactor("Concrete C30/37", 290, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Concrete_C40"] = new MaterialCarbonFactor("Concrete C40/50", 350, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Concrete_C50"] = new MaterialCarbonFactor("Concrete C50/60", 410, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Concrete_Precast"] = new MaterialCarbonFactor("Precast Concrete", 280, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Concrete_LowCarbon"] = new MaterialCarbonFactor("Low Carbon Concrete (30% GGBS)", 180, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Concrete_UltraLowCarbon"] = new MaterialCarbonFactor("Ultra Low Carbon Concrete (50% GGBS)", 140, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Cement_OPC"] = new MaterialCarbonFactor("Ordinary Portland Cement", 0.93, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Cement_PLC"] = new MaterialCarbonFactor("Portland Limestone Cement", 0.74, "kg", CarbonCategory.Structure, "A1-A3"),

                // Steel
                ["Steel_Rebar"] = new MaterialCarbonFactor("Steel Reinforcement Bar", 1.99, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Steel_Section"] = new MaterialCarbonFactor("Steel Structural Section", 2.55, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Steel_Recycled"] = new MaterialCarbonFactor("Recycled Steel (EAF)", 0.47, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Steel_Galvanized"] = new MaterialCarbonFactor("Galvanized Steel", 2.76, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Steel_Stainless"] = new MaterialCarbonFactor("Stainless Steel", 6.15, "kg", CarbonCategory.Structure, "A1-A3"),

                // Timber and Wood Products
                ["Timber_Softwood"] = new MaterialCarbonFactor("Softwood Timber", -1.03, "kg", CarbonCategory.Structure, "A1-A3"), // Carbon negative (sequestration)
                ["Timber_Hardwood"] = new MaterialCarbonFactor("Hardwood Timber", -0.86, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Timber_CLT"] = new MaterialCarbonFactor("Cross Laminated Timber", -0.72, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Timber_Glulam"] = new MaterialCarbonFactor("Glued Laminated Timber", -0.51, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Timber_Plywood"] = new MaterialCarbonFactor("Plywood", 0.68, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Timber_MDF"] = new MaterialCarbonFactor("MDF Board", 0.72, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Timber_Particleboard"] = new MaterialCarbonFactor("Particleboard", 0.54, "kg", CarbonCategory.Finishes, "A1-A3"),

                // African Local Materials
                ["Bamboo_Structural"] = new MaterialCarbonFactor("Structural Bamboo", -1.20, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Bamboo_Laminated"] = new MaterialCarbonFactor("Laminated Bamboo", -0.85, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Adobe_Block"] = new MaterialCarbonFactor("Adobe/Mud Block", 0.022, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Laterite_Block"] = new MaterialCarbonFactor("Laterite Block", 0.045, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Compressed_Earth_Block"] = new MaterialCarbonFactor("Compressed Earth Block (CEB)", 0.035, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Stabilized_Earth_Block"] = new MaterialCarbonFactor("Stabilized Earth Block (5% cement)", 0.065, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Rammed_Earth"] = new MaterialCarbonFactor("Rammed Earth Wall", 25, "m³", CarbonCategory.Structure, "A1-A3"),
                ["Thatch_Roof"] = new MaterialCarbonFactor("Thatch Roofing", -0.45, "kg", CarbonCategory.Envelope, "A1-A3"),

                // Masonry
                ["Brick_Clay"] = new MaterialCarbonFactor("Clay Brick", 0.24, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Brick_Concrete"] = new MaterialCarbonFactor("Concrete Block", 0.10, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Brick_AAC"] = new MaterialCarbonFactor("Autoclaved Aerated Concrete Block", 0.28, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Stone_Natural"] = new MaterialCarbonFactor("Natural Stone (quarried)", 0.079, "kg", CarbonCategory.Structure, "A1-A3"),
                ["Stone_Granite"] = new MaterialCarbonFactor("Granite", 0.70, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Stone_Limestone"] = new MaterialCarbonFactor("Limestone", 0.09, "kg", CarbonCategory.Structure, "A1-A3"),

                // Insulation
                ["Insulation_Mineral_Wool"] = new MaterialCarbonFactor("Mineral Wool", 1.28, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_Glass_Wool"] = new MaterialCarbonFactor("Glass Wool", 1.35, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_EPS"] = new MaterialCarbonFactor("Expanded Polystyrene (EPS)", 3.29, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_XPS"] = new MaterialCarbonFactor("Extruded Polystyrene (XPS)", 3.49, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_PUR"] = new MaterialCarbonFactor("Polyurethane Foam", 4.26, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_Cellulose"] = new MaterialCarbonFactor("Cellulose Insulation", 0.18, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_Cork"] = new MaterialCarbonFactor("Cork Insulation", -1.56, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Insulation_Hemp"] = new MaterialCarbonFactor("Hemp Insulation", -0.35, "kg", CarbonCategory.Envelope, "A1-A3"),

                // Glass and Glazing
                ["Glass_Float"] = new MaterialCarbonFactor("Float Glass", 1.44, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Glass_Tempered"] = new MaterialCarbonFactor("Tempered Glass", 1.67, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Glass_Laminated"] = new MaterialCarbonFactor("Laminated Glass", 2.16, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Glass_IGU_Double"] = new MaterialCarbonFactor("Double Glazed Unit", 36, "m²", CarbonCategory.Envelope, "A1-A3"),
                ["Glass_IGU_Triple"] = new MaterialCarbonFactor("Triple Glazed Unit", 54, "m²", CarbonCategory.Envelope, "A1-A3"),

                // Aluminum
                ["Aluminum_Primary"] = new MaterialCarbonFactor("Primary Aluminum", 12.79, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Aluminum_Recycled"] = new MaterialCarbonFactor("Recycled Aluminum", 0.52, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Aluminum_Window_Frame"] = new MaterialCarbonFactor("Aluminum Window Frame", 156, "m²", CarbonCategory.Envelope, "A1-A3"),

                // Plastics and Membranes
                ["PVC_General"] = new MaterialCarbonFactor("PVC General", 3.10, "kg", CarbonCategory.Services, "A1-A3"),
                ["HDPE_Pipe"] = new MaterialCarbonFactor("HDPE Pipe", 1.93, "kg", CarbonCategory.Services, "A1-A3"),
                ["EPDM_Membrane"] = new MaterialCarbonFactor("EPDM Roofing Membrane", 2.73, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Bitumen_Membrane"] = new MaterialCarbonFactor("Bitumen Roofing Membrane", 0.48, "kg", CarbonCategory.Envelope, "A1-A3"),

                // Finishes
                ["Plaster_Gypsum"] = new MaterialCarbonFactor("Gypsum Plaster", 0.12, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Plasterboard"] = new MaterialCarbonFactor("Plasterboard/Drywall", 0.39, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Paint_Water_Based"] = new MaterialCarbonFactor("Water-based Paint", 2.42, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Paint_Solvent_Based"] = new MaterialCarbonFactor("Solvent-based Paint", 3.76, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Ceramic_Tiles"] = new MaterialCarbonFactor("Ceramic Floor Tiles", 0.78, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Porcelain_Tiles"] = new MaterialCarbonFactor("Porcelain Tiles", 1.24, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Carpet_Synthetic"] = new MaterialCarbonFactor("Synthetic Carpet", 5.43, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Carpet_Natural"] = new MaterialCarbonFactor("Natural Fiber Carpet", 2.89, "kg", CarbonCategory.Finishes, "A1-A3"),
                ["Vinyl_Flooring"] = new MaterialCarbonFactor("Vinyl Flooring", 2.92, "kg", CarbonCategory.Finishes, "A1-A3"),

                // Roofing
                ["Roof_Clay_Tiles"] = new MaterialCarbonFactor("Clay Roof Tiles", 0.45, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Roof_Concrete_Tiles"] = new MaterialCarbonFactor("Concrete Roof Tiles", 0.26, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Roof_Metal_Sheet"] = new MaterialCarbonFactor("Metal Roofing Sheet", 3.01, "kg", CarbonCategory.Envelope, "A1-A3"),
                ["Roof_Green"] = new MaterialCarbonFactor("Green Roof System (extensive)", 45, "m²", CarbonCategory.Envelope, "A1-A3"),

                // MEP Components
                ["Copper_Pipe"] = new MaterialCarbonFactor("Copper Pipe", 2.71, "kg", CarbonCategory.Services, "A1-A3"),
                ["Copper_Cable"] = new MaterialCarbonFactor("Copper Cable", 3.83, "kg", CarbonCategory.Services, "A1-A3"),
                ["Ductwork_Galvanized"] = new MaterialCarbonFactor("Galvanized Steel Ductwork", 2.76, "kg", CarbonCategory.Services, "A1-A3"),
                ["AC_Split_Unit"] = new MaterialCarbonFactor("Split AC Unit (typical)", 350, "unit", CarbonCategory.Services, "A1-A3"),
                ["AC_VRF_System"] = new MaterialCarbonFactor("VRF System (per kW)", 85, "kW", CarbonCategory.Services, "A1-A3"),
                ["Solar_PV_Panel"] = new MaterialCarbonFactor("Solar PV Panel", 40, "m²", CarbonCategory.Services, "A1-A3"),
                ["LED_Light_Fixture"] = new MaterialCarbonFactor("LED Light Fixture", 8.5, "unit", CarbonCategory.Services, "A1-A3"),
            };
        }

        private Dictionary<string, double> InitializeEnergyCarbonIntensity()
        {
            // Grid carbon intensity by country/region (kgCO2e per kWh)
            // Based on IEA 2024 data and regional studies
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // African Countries
                ["Uganda"] = 0.032,        // Mostly hydro
                ["Kenya"] = 0.089,         // Geothermal + hydro
                ["Ethiopia"] = 0.025,      // Hydro dominant
                ["Rwanda"] = 0.115,        // Hydro + thermal
                ["Tanzania"] = 0.485,      // Natural gas + hydro
                ["Nigeria"] = 0.437,       // Gas dominant
                ["Ghana"] = 0.342,         // Hydro + thermal
                ["South_Africa"] = 0.928,  // Coal dominant
                ["Egypt"] = 0.462,         // Natural gas
                ["Morocco"] = 0.617,       // Coal + renewables

                // Global Reference
                ["UK"] = 0.233,
                ["Germany"] = 0.385,
                ["France"] = 0.052,        // Nuclear
                ["USA"] = 0.417,
                ["China"] = 0.581,
                ["India"] = 0.708,
                ["UAE"] = 0.404,
                ["Singapore"] = 0.408,

                // Renewable Sources
                ["Solar_PV"] = 0.041,
                ["Wind_Onshore"] = 0.011,
                ["Hydro"] = 0.024,
                ["Biomass"] = 0.230,
                ["Nuclear"] = 0.012,
                ["Natural_Gas"] = 0.490,
                ["Coal"] = 0.910,
            };
        }

        private Dictionary<string, TransportEmissionFactor> InitializeTransportFactors()
        {
            return new Dictionary<string, TransportEmissionFactor>(StringComparer.OrdinalIgnoreCase)
            {
                ["Road_Truck_Small"] = new TransportEmissionFactor("Small Truck (<7.5t)", 0.213, "tkm"),
                ["Road_Truck_Medium"] = new TransportEmissionFactor("Medium Truck (7.5-16t)", 0.147, "tkm"),
                ["Road_Truck_Large"] = new TransportEmissionFactor("Large Truck (>16t)", 0.089, "tkm"),
                ["Road_Truck_Articulated"] = new TransportEmissionFactor("Articulated Truck", 0.062, "tkm"),
                ["Rail_Freight"] = new TransportEmissionFactor("Rail Freight", 0.028, "tkm"),
                ["Sea_Container"] = new TransportEmissionFactor("Container Ship", 0.016, "tkm"),
                ["Sea_Bulk"] = new TransportEmissionFactor("Bulk Carrier", 0.009, "tkm"),
                ["Air_Freight"] = new TransportEmissionFactor("Air Freight", 1.13, "tkm"),
            };
        }

        #endregion

        #region Embodied Carbon Calculation

        /// <summary>
        /// Calculates embodied carbon for a building based on material quantities.
        /// </summary>
        public async Task<EmbodiedCarbonResult> CalculateEmbodiedCarbonAsync(
            BuildingMaterialSchedule materialSchedule,
            string projectLocation = "Uganda")
        {
            Logger.Info("Calculating embodied carbon for project at {0}", projectLocation);

            var result = new EmbodiedCarbonResult
            {
                ProjectLocation = projectLocation,
                CalculationDate = DateTime.UtcNow,
                LifecycleStages = new Dictionary<string, double>()
            };

            var categoryBreakdown = new Dictionary<CarbonCategory, double>();
            var materialBreakdown = new List<MaterialCarbonBreakdown>();
            double totalA1A3 = 0;
            double totalA4 = 0;
            double totalA5 = 0;

            await Task.Run(() =>
            {
                foreach (var material in materialSchedule.Materials)
                {
                    if (_materialCarbonFactors.TryGetValue(material.MaterialCode, out var factor))
                    {
                        // A1-A3: Product stage (raw materials, transport to factory, manufacturing)
                        double carbonA1A3 = material.Quantity * factor.CarbonFactor;
                        totalA1A3 += carbonA1A3;

                        // A4: Transport to site
                        double carbonA4 = CalculateTransportCarbon(material, projectLocation);
                        totalA4 += carbonA4;

                        // A5: Construction/installation (typically 1-5% of A1-A3)
                        double carbonA5 = carbonA1A3 * GetConstructionWasteFactor(material.MaterialCode);
                        totalA5 += carbonA5;

                        // Category breakdown
                        if (!categoryBreakdown.ContainsKey(factor.Category))
                            categoryBreakdown[factor.Category] = 0;
                        categoryBreakdown[factor.Category] += carbonA1A3 + carbonA4 + carbonA5;

                        materialBreakdown.Add(new MaterialCarbonBreakdown
                        {
                            MaterialCode = material.MaterialCode,
                            MaterialName = factor.Name,
                            Quantity = material.Quantity,
                            Unit = factor.Unit,
                            CarbonA1A3 = carbonA1A3,
                            CarbonA4 = carbonA4,
                            CarbonA5 = carbonA5,
                            TotalCarbon = carbonA1A3 + carbonA4 + carbonA5,
                            Category = factor.Category
                        });
                    }
                    else
                    {
                        Logger.Warn("Unknown material code: {0}", material.MaterialCode);
                    }
                }
            });

            result.LifecycleStages["A1-A3_ProductStage"] = totalA1A3;
            result.LifecycleStages["A4_Transport"] = totalA4;
            result.LifecycleStages["A5_Construction"] = totalA5;
            result.TotalEmbodiedCarbon = totalA1A3 + totalA4 + totalA5;
            result.CategoryBreakdown = categoryBreakdown;
            result.MaterialBreakdown = materialBreakdown;

            // Calculate intensity metrics
            if (materialSchedule.GrossFloorArea > 0)
            {
                result.CarbonIntensity = result.TotalEmbodiedCarbon / materialSchedule.GrossFloorArea;
            }

            Logger.Info("Embodied carbon calculation complete: {0:N0} kgCO2e ({1:N1} kgCO2e/m²)",
                result.TotalEmbodiedCarbon, result.CarbonIntensity);

            return result;
        }

        private double CalculateTransportCarbon(MaterialQuantity material, string projectLocation)
        {
            // Estimate transport distance based on material origin
            double distance = EstimateTransportDistance(material.MaterialCode, projectLocation);
            double weight = material.Quantity * GetMaterialDensity(material.MaterialCode);

            // Assume large truck for local, sea + truck for imported
            string transportMode = distance > 500 ? "Road_Truck_Large" : "Road_Truck_Medium";
            if (distance > 2000)
            {
                // International - use sea freight for bulk
                if (_transportFactors.TryGetValue("Sea_Container", out var seaFactor))
                {
                    double seaDistance = distance * 0.8; // 80% by sea
                    double landDistance = distance * 0.2; // 20% by road
                    return (weight * seaDistance * seaFactor.EmissionFactor / 1000) +
                           (weight * landDistance * 0.089 / 1000);
                }
            }

            if (_transportFactors.TryGetValue(transportMode, out var factor))
            {
                return weight * distance * factor.EmissionFactor / 1000; // tkm to tCO2e
            }

            return 0;
        }

        private double EstimateTransportDistance(string materialCode, string location)
        {
            // Local materials (African)
            var localMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Adobe_Block", "Laterite_Block", "Compressed_Earth_Block", "Stabilized_Earth_Block",
                "Rammed_Earth", "Thatch_Roof", "Bamboo_Structural", "Bamboo_Laminated",
                "Stone_Natural", "Brick_Clay", "Brick_Concrete"
            };

            // Regional materials (Africa-produced)
            var regionalMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cement_OPC", "Concrete_C20", "Concrete_C30", "Steel_Rebar"
            };

            if (localMaterials.Contains(materialCode))
                return 50; // 50 km local

            if (regionalMaterials.Contains(materialCode))
                return 300; // 300 km regional

            // Imported materials
            return 5000; // 5000 km international average
        }

        private double GetMaterialDensity(string materialCode)
        {
            // Material densities in kg/m³ or kg/unit
            var densities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Concrete_C20"] = 2400, ["Concrete_C30"] = 2400, ["Concrete_C40"] = 2450,
                ["Steel_Rebar"] = 1, ["Steel_Section"] = 1, // Already in kg
                ["Timber_Softwood"] = 1, ["Timber_Hardwood"] = 1,
                ["Brick_Clay"] = 1, ["Brick_Concrete"] = 1,
                ["Adobe_Block"] = 1, ["Compressed_Earth_Block"] = 1,
            };

            return densities.TryGetValue(materialCode, out var density) ? density : 1;
        }

        private double GetConstructionWasteFactor(string materialCode)
        {
            // Construction waste factors (% added for wastage)
            var wasteFactors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Concrete_C20"] = 0.05, ["Concrete_C30"] = 0.05,
                ["Steel_Rebar"] = 0.02, ["Steel_Section"] = 0.03,
                ["Timber_Softwood"] = 0.10, ["Timber_Hardwood"] = 0.10,
                ["Brick_Clay"] = 0.05, ["Brick_Concrete"] = 0.05,
                ["Glass_Float"] = 0.08, ["Plasterboard"] = 0.10,
            };

            return wasteFactors.TryGetValue(materialCode, out var factor) ? factor : 0.05;
        }

        #endregion

        #region Operational Carbon Calculation

        /// <summary>
        /// Calculates operational carbon over building lifetime.
        /// </summary>
        public async Task<OperationalCarbonResult> CalculateOperationalCarbonAsync(
            BuildingEnergyProfile energyProfile,
            int buildingLifespan = 60)
        {
            Logger.Info("Calculating operational carbon for {0} year lifespan", buildingLifespan);

            var result = new OperationalCarbonResult
            {
                BuildingLifespan = buildingLifespan,
                Location = energyProfile.Location,
                CalculationDate = DateTime.UtcNow
            };

            await Task.Run(() =>
            {
                // Get grid carbon intensity for location
                double gridIntensity = _energyCarbonIntensity.TryGetValue(energyProfile.Location, out var intensity)
                    ? intensity
                    : 0.5; // Default global average

                // Annual energy consumption breakdown
                var annualBreakdown = new Dictionary<string, double>();

                // B6: Operational energy use
                double heatingCoolingCarbon = energyProfile.AnnualHeatingCoolingEnergy * gridIntensity;
                double lightingCarbon = energyProfile.AnnualLightingEnergy * gridIntensity;
                double equipmentCarbon = energyProfile.AnnualEquipmentEnergy * gridIntensity;
                double domesticHotWaterCarbon = energyProfile.AnnualDHWEnergy * gridIntensity;
                double ventilationCarbon = energyProfile.AnnualVentilationEnergy * gridIntensity;

                annualBreakdown["HeatingCooling"] = heatingCoolingCarbon;
                annualBreakdown["Lighting"] = lightingCarbon;
                annualBreakdown["Equipment"] = equipmentCarbon;
                annualBreakdown["DomesticHotWater"] = domesticHotWaterCarbon;
                annualBreakdown["Ventilation"] = ventilationCarbon;

                double totalAnnualCarbon = heatingCoolingCarbon + lightingCarbon + equipmentCarbon +
                                          domesticHotWaterCarbon + ventilationCarbon;

                // Account for on-site renewables
                if (energyProfile.OnSiteRenewableGeneration > 0)
                {
                    double renewableOffset = energyProfile.OnSiteRenewableGeneration * gridIntensity;
                    annualBreakdown["RenewableOffset"] = -renewableOffset;
                    totalAnnualCarbon -= renewableOffset;
                }

                // Apply grid decarbonization trajectory
                double lifetimeCarbon = 0;
                var yearlyProjection = new List<YearlyCarbon>();

                for (int year = 1; year <= buildingLifespan; year++)
                {
                    // Assume grid decarbonizes at 2% per year
                    double decarbonizationFactor = Math.Pow(0.98, year);
                    double yearCarbon = totalAnnualCarbon * decarbonizationFactor;
                    lifetimeCarbon += yearCarbon;

                    if (year <= 10 || year % 10 == 0) // Store key years
                    {
                        yearlyProjection.Add(new YearlyCarbon
                        {
                            Year = year,
                            AnnualCarbon = yearCarbon,
                            CumulativeCarbon = lifetimeCarbon
                        });
                    }
                }

                result.AnnualOperationalCarbon = totalAnnualCarbon;
                result.LifetimeOperationalCarbon = lifetimeCarbon;
                result.AnnualBreakdown = annualBreakdown;
                result.GridCarbonIntensity = gridIntensity;
                result.YearlyProjection = yearlyProjection;

                // Calculate intensity
                if (energyProfile.GrossFloorArea > 0)
                {
                    result.AnnualCarbonIntensity = totalAnnualCarbon / energyProfile.GrossFloorArea;
                }
            });

            Logger.Info("Operational carbon calculation complete: {0:N0} kgCO2e/year, {1:N0} kgCO2e lifetime",
                result.AnnualOperationalCarbon, result.LifetimeOperationalCarbon);

            return result;
        }

        #endregion

        #region Whole Life Carbon Assessment

        /// <summary>
        /// Performs whole life carbon assessment (WLCA) following EN 15978.
        /// </summary>
        public async Task<WholeLifeCarbonResult> PerformWholeLifeAssessmentAsync(
            BuildingMaterialSchedule materialSchedule,
            BuildingEnergyProfile energyProfile,
            int buildingLifespan = 60)
        {
            Logger.Info("Performing whole life carbon assessment");

            // Calculate embodied and operational carbon in parallel
            var embodiedTask = CalculateEmbodiedCarbonAsync(materialSchedule, energyProfile.Location);
            var operationalTask = CalculateOperationalCarbonAsync(energyProfile, buildingLifespan);

            await Task.WhenAll(embodiedTask, operationalTask);

            var embodiedResult = embodiedTask.Result;
            var operationalResult = operationalTask.Result;

            var result = new WholeLifeCarbonResult
            {
                ProjectName = materialSchedule.ProjectName,
                Location = energyProfile.Location,
                GrossFloorArea = materialSchedule.GrossFloorArea,
                BuildingLifespan = buildingLifespan,
                CalculationDate = DateTime.UtcNow,

                // Upfront carbon (A1-A5)
                UpfrontCarbon = embodiedResult.TotalEmbodiedCarbon,

                // Use stage (B)
                OperationalCarbon = operationalResult.LifetimeOperationalCarbon,
                MaintenanceCarbon = EstimateMaintenanceCarbon(materialSchedule, buildingLifespan),
                ReplacementCarbon = EstimateReplacementCarbon(materialSchedule, buildingLifespan),

                // End of life (C)
                EndOfLifeCarbon = EstimateEndOfLifeCarbon(materialSchedule),

                // Beyond lifecycle (D)
                BeyondLifecycleCarbon = EstimateBeyondLifecycleCarbon(materialSchedule),

                EmbodiedCarbonDetails = embodiedResult,
                OperationalCarbonDetails = operationalResult
            };

            // Calculate totals
            result.TotalWholeLifeCarbon = result.UpfrontCarbon + result.OperationalCarbon +
                result.MaintenanceCarbon + result.ReplacementCarbon + result.EndOfLifeCarbon;

            result.NetWholeLifeCarbon = result.TotalWholeLifeCarbon + result.BeyondLifecycleCarbon;

            // Intensity metrics
            if (materialSchedule.GrossFloorArea > 0)
            {
                result.WholeLifeCarbonIntensity = result.TotalWholeLifeCarbon / materialSchedule.GrossFloorArea;
                result.UpfrontCarbonIntensity = result.UpfrontCarbon / materialSchedule.GrossFloorArea;
            }

            // Benchmark comparison
            result.BenchmarkComparison = CompareToBenchmarks(result);

            // Store assessment
            lock (_lock)
            {
                _assessmentHistory.Add(new CarbonAssessment
                {
                    ProjectName = materialSchedule.ProjectName,
                    AssessmentDate = DateTime.UtcNow,
                    WholeLifeCarbon = result.TotalWholeLifeCarbon,
                    CarbonIntensity = result.WholeLifeCarbonIntensity
                });
            }

            Logger.Info("WLCA complete: {0:N0} kgCO2e total ({1:N1} kgCO2e/m²)",
                result.TotalWholeLifeCarbon, result.WholeLifeCarbonIntensity);

            return result;
        }

        private double EstimateMaintenanceCarbon(BuildingMaterialSchedule schedule, int lifespan)
        {
            // Maintenance typically 1-2% of embodied carbon per year
            double annualMaintenance = 0;
            foreach (var material in schedule.Materials)
            {
                if (_materialCarbonFactors.TryGetValue(material.MaterialCode, out var factor))
                {
                    double materialCarbon = material.Quantity * factor.CarbonFactor;
                    annualMaintenance += materialCarbon * 0.01; // 1% annual maintenance
                }
            }
            return annualMaintenance * lifespan;
        }

        private double EstimateReplacementCarbon(BuildingMaterialSchedule schedule, int lifespan)
        {
            // Calculate replacement cycles based on material lifespans
            var materialLifespans = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint_Water_Based"] = 7, ["Paint_Solvent_Based"] = 7,
                ["Carpet_Synthetic"] = 10, ["Carpet_Natural"] = 15,
                ["Vinyl_Flooring"] = 15, ["LED_Light_Fixture"] = 15,
                ["AC_Split_Unit"] = 15, ["AC_VRF_System"] = 20,
                ["Roof_Metal_Sheet"] = 30, ["Glass_IGU_Double"] = 25,
                ["Plasterboard"] = 30, ["Ceramic_Tiles"] = 40,
            };

            double totalReplacement = 0;
            foreach (var material in schedule.Materials)
            {
                if (_materialCarbonFactors.TryGetValue(material.MaterialCode, out var factor) &&
                    materialLifespans.TryGetValue(material.MaterialCode, out int materialLife))
                {
                    int replacements = (lifespan / materialLife) - 1;
                    if (replacements > 0)
                    {
                        totalReplacement += material.Quantity * factor.CarbonFactor * replacements;
                    }
                }
            }
            return totalReplacement;
        }

        private double EstimateEndOfLifeCarbon(BuildingMaterialSchedule schedule)
        {
            // End of life processing: demolition, waste processing, disposal
            double totalEndOfLife = 0;
            foreach (var material in schedule.Materials)
            {
                if (_materialCarbonFactors.TryGetValue(material.MaterialCode, out var factor))
                {
                    // Typically 2-5% of embodied carbon for demolition and waste processing
                    totalEndOfLife += material.Quantity * Math.Abs(factor.CarbonFactor) * 0.03;
                }
            }
            return totalEndOfLife;
        }

        private double EstimateBeyondLifecycleCarbon(BuildingMaterialSchedule schedule)
        {
            // Module D: Benefits beyond system boundary (recycling, reuse)
            double totalBenefit = 0;

            // Materials with significant recycling potential
            var recyclableFactors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Steel_Rebar"] = -0.50, // 50% carbon saved through recycling
                ["Steel_Section"] = -0.50,
                ["Aluminum_Primary"] = -0.90, // High recycling benefit
                ["Copper_Pipe"] = -0.70,
                ["Timber_Softwood"] = -0.30, // Reuse/energy recovery
                ["Timber_Hardwood"] = -0.30,
            };

            foreach (var material in schedule.Materials)
            {
                if (_materialCarbonFactors.TryGetValue(material.MaterialCode, out var factor) &&
                    recyclableFactors.TryGetValue(material.MaterialCode, out double recycleFactor))
                {
                    totalBenefit += material.Quantity * Math.Abs(factor.CarbonFactor) * recycleFactor;
                }
            }

            return totalBenefit;
        }

        private BenchmarkComparison CompareToBenchmarks(WholeLifeCarbonResult result)
        {
            // RIBA 2030 Climate Challenge targets (kgCO2e/m²)
            var benchmarks = new Dictionary<string, CarbonBenchmark>
            {
                ["RIBA_2020_Residential"] = new CarbonBenchmark { UpfrontTarget = 800, WholeLifeTarget = 1200 },
                ["RIBA_2025_Residential"] = new CarbonBenchmark { UpfrontTarget = 625, WholeLifeTarget = 975 },
                ["RIBA_2030_Residential"] = new CarbonBenchmark { UpfrontTarget = 400, WholeLifeTarget = 625 },
                ["RIBA_2020_Office"] = new CarbonBenchmark { UpfrontTarget = 950, WholeLifeTarget = 1400 },
                ["RIBA_2025_Office"] = new CarbonBenchmark { UpfrontTarget = 750, WholeLifeTarget = 1150 },
                ["RIBA_2030_Office"] = new CarbonBenchmark { UpfrontTarget = 500, WholeLifeTarget = 750 },
                ["LETI_Residential"] = new CarbonBenchmark { UpfrontTarget = 350, WholeLifeTarget = 550 },
                ["LETI_Office"] = new CarbonBenchmark { UpfrontTarget = 500, WholeLifeTarget = 750 },
            };

            var comparison = new BenchmarkComparison
            {
                ActualUpfrontIntensity = result.UpfrontCarbonIntensity,
                ActualWholeLifeIntensity = result.WholeLifeCarbonIntensity,
                BenchmarkResults = new Dictionary<string, BenchmarkResult>()
            };

            foreach (var benchmark in benchmarks)
            {
                comparison.BenchmarkResults[benchmark.Key] = new BenchmarkResult
                {
                    BenchmarkName = benchmark.Key,
                    UpfrontTarget = benchmark.Value.UpfrontTarget,
                    WholeLifeTarget = benchmark.Value.WholeLifeTarget,
                    MeetsUpfrontTarget = result.UpfrontCarbonIntensity <= benchmark.Value.UpfrontTarget,
                    MeetsWholeLifeTarget = result.WholeLifeCarbonIntensity <= benchmark.Value.WholeLifeTarget,
                    UpfrontVariance = result.UpfrontCarbonIntensity - benchmark.Value.UpfrontTarget,
                    WholeLifeVariance = result.WholeLifeCarbonIntensity - benchmark.Value.WholeLifeTarget
                };
            }

            return comparison;
        }

        #endregion

        #region Carbon Reduction Recommendations

        /// <summary>
        /// Generates carbon reduction recommendations based on assessment results.
        /// </summary>
        public List<CarbonReductionRecommendation> GenerateRecommendations(WholeLifeCarbonResult assessment)
        {
            var recommendations = new List<CarbonReductionRecommendation>();

            // Analyze material breakdown for high-carbon items
            if (assessment.EmbodiedCarbonDetails?.MaterialBreakdown != null)
            {
                var topCarbonMaterials = assessment.EmbodiedCarbonDetails.MaterialBreakdown
                    .OrderByDescending(m => m.TotalCarbon)
                    .Take(5);

                foreach (var material in topCarbonMaterials)
                {
                    var alternatives = GetLowerCarbonAlternatives(material.MaterialCode);
                    foreach (var alt in alternatives)
                    {
                        recommendations.Add(new CarbonReductionRecommendation
                        {
                            Category = RecommendationCategory.MaterialSubstitution,
                            Title = $"Replace {material.MaterialName} with {alt.AlternativeName}",
                            Description = $"Substituting {material.MaterialName} with {alt.AlternativeName} " +
                                        $"could reduce embodied carbon by {alt.CarbonReduction:P0}",
                            CurrentCarbon = material.TotalCarbon,
                            PotentialReduction = material.TotalCarbon * alt.CarbonReduction,
                            Confidence = alt.Confidence,
                            Priority = GetPriority(material.TotalCarbon * alt.CarbonReduction),
                            ImplementationNotes = alt.Notes
                        });
                    }
                }
            }

            // Operational carbon recommendations
            if (assessment.OperationalCarbonDetails != null)
            {
                var opResult = assessment.OperationalCarbonDetails;

                // Renewable energy recommendation
                if (opResult.AnnualOperationalCarbon > 10000)
                {
                    double pvPotential = EstimatePVPotential(assessment.GrossFloorArea);
                    recommendations.Add(new CarbonReductionRecommendation
                    {
                        Category = RecommendationCategory.RenewableEnergy,
                        Title = "Install rooftop solar PV system",
                        Description = $"A {pvPotential:N0} kWp PV system could offset {pvPotential * 1400 * opResult.GridCarbonIntensity:N0} kgCO2e/year",
                        CurrentCarbon = opResult.AnnualOperationalCarbon,
                        PotentialReduction = pvPotential * 1400 * opResult.GridCarbonIntensity,
                        Confidence = 0.85,
                        Priority = RecommendationPriority.High,
                        ImplementationNotes = "Assess roof orientation and shading. Consider battery storage for grid independence."
                    });
                }

                // HVAC efficiency recommendation
                if (opResult.AnnualBreakdown.TryGetValue("HeatingCooling", out double hvacCarbon) && hvacCarbon > 5000)
                {
                    recommendations.Add(new CarbonReductionRecommendation
                    {
                        Category = RecommendationCategory.EnergyEfficiency,
                        Title = "Upgrade to high-efficiency HVAC system",
                        Description = "Variable Refrigerant Flow (VRF) or heat pump systems can reduce HVAC energy by 30-40%",
                        CurrentCarbon = hvacCarbon * assessment.BuildingLifespan,
                        PotentialReduction = hvacCarbon * 0.35 * assessment.BuildingLifespan,
                        Confidence = 0.75,
                        Priority = RecommendationPriority.High,
                        ImplementationNotes = "Evaluate building thermal loads and consider passive design strategies first."
                    });
                }

                // Natural ventilation recommendation (Africa-specific)
                recommendations.Add(new CarbonReductionRecommendation
                {
                    Category = RecommendationCategory.PassiveDesign,
                    Title = "Maximize natural ventilation potential",
                    Description = "Cross-ventilation and stack effect can significantly reduce or eliminate mechanical cooling in many African climates",
                    CurrentCarbon = hvacCarbon * assessment.BuildingLifespan,
                    PotentialReduction = hvacCarbon * 0.50 * assessment.BuildingLifespan,
                    Confidence = 0.65,
                    Priority = RecommendationPriority.Medium,
                    ImplementationNotes = "Requires careful building orientation, window placement, and internal layout design."
                });
            }

            // Local materials recommendation (Africa-specific)
            recommendations.Add(new CarbonReductionRecommendation
            {
                Category = RecommendationCategory.LocalMaterials,
                Title = "Increase use of local/regional materials",
                Description = "Local materials like compressed earth blocks, bamboo, and locally-sourced stone have lower transport emissions and often lower embodied carbon",
                CurrentCarbon = assessment.EmbodiedCarbonDetails?.LifecycleStages?["A4_Transport"] ?? 0,
                PotentialReduction = (assessment.EmbodiedCarbonDetails?.LifecycleStages?["A4_Transport"] ?? 0) * 0.60,
                Confidence = 0.80,
                Priority = RecommendationPriority.Medium,
                ImplementationNotes = "Assess structural requirements and local availability. Consider hybrid construction approaches."
            });

            // Sort by potential reduction
            return recommendations.OrderByDescending(r => r.PotentialReduction).ToList();
        }

        private List<MaterialAlternative> GetLowerCarbonAlternatives(string materialCode)
        {
            var alternatives = new Dictionary<string, List<MaterialAlternative>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Concrete_C30"] = new List<MaterialAlternative>
                {
                    new MaterialAlternative { AlternativeName = "Low Carbon Concrete (30% GGBS)", CarbonReduction = 0.38, Confidence = 0.90, Notes = "Specify GGBS replacement in concrete mix" },
                    new MaterialAlternative { AlternativeName = "Ultra Low Carbon Concrete (50% GGBS)", CarbonReduction = 0.52, Confidence = 0.85, Notes = "Check availability and structural requirements" },
                },
                ["Concrete_C40"] = new List<MaterialAlternative>
                {
                    new MaterialAlternative { AlternativeName = "Low Carbon Concrete (30% GGBS)", CarbonReduction = 0.35, Confidence = 0.85, Notes = "May require mix design optimization" },
                },
                ["Steel_Section"] = new List<MaterialAlternative>
                {
                    new MaterialAlternative { AlternativeName = "Recycled Steel (EAF)", CarbonReduction = 0.82, Confidence = 0.80, Notes = "Specify high recycled content steel" },
                    new MaterialAlternative { AlternativeName = "Timber/CLT Structure", CarbonReduction = 1.0, Confidence = 0.70, Notes = "Requires structural redesign, carbon negative potential" },
                },
                ["Brick_Clay"] = new List<MaterialAlternative>
                {
                    new MaterialAlternative { AlternativeName = "Compressed Earth Block", CarbonReduction = 0.85, Confidence = 0.75, Notes = "Suitable for low-rise construction in dry climates" },
                    new MaterialAlternative { AlternativeName = "Concrete Block", CarbonReduction = 0.58, Confidence = 0.90, Notes = "Widely available alternative" },
                },
                ["Aluminum_Primary"] = new List<MaterialAlternative>
                {
                    new MaterialAlternative { AlternativeName = "Recycled Aluminum", CarbonReduction = 0.96, Confidence = 0.85, Notes = "Specify recycled content in procurement" },
                    new MaterialAlternative { AlternativeName = "Timber Window Frames", CarbonReduction = 0.90, Confidence = 0.70, Notes = "Requires appropriate treatment for durability" },
                },
                ["Insulation_EPS"] = new List<MaterialAlternative>
                {
                    new MaterialAlternative { AlternativeName = "Cellulose Insulation", CarbonReduction = 0.95, Confidence = 0.80, Notes = "Excellent performance from recycled materials" },
                    new MaterialAlternative { AlternativeName = "Cork Insulation", CarbonReduction = 1.0, Confidence = 0.75, Notes = "Carbon negative, excellent acoustic properties" },
                    new MaterialAlternative { AlternativeName = "Hemp Insulation", CarbonReduction = 0.90, Confidence = 0.70, Notes = "Growing availability in African markets" },
                },
            };

            return alternatives.TryGetValue(materialCode, out var alts) ? alts : new List<MaterialAlternative>();
        }

        private double EstimatePVPotential(double grossFloorArea)
        {
            // Estimate rooftop area available for PV (typically 40-60% of GFA for low-rise)
            double roofArea = grossFloorArea * 0.5;
            // Assume 200 Wp/m² for modern panels
            return roofArea * 0.2; // kWp
        }

        private RecommendationPriority GetPriority(double carbonReduction)
        {
            if (carbonReduction > 50000) return RecommendationPriority.Critical;
            if (carbonReduction > 20000) return RecommendationPriority.High;
            if (carbonReduction > 5000) return RecommendationPriority.Medium;
            return RecommendationPriority.Low;
        }

        #endregion

        #region Public API Methods

        /// <summary>
        /// Gets the carbon factor for a specific material.
        /// </summary>
        public MaterialCarbonFactor GetMaterialCarbonFactor(string materialCode)
        {
            return _materialCarbonFactors.TryGetValue(materialCode, out var factor) ? factor : null;
        }

        /// <summary>
        /// Gets the grid carbon intensity for a location.
        /// </summary>
        public double GetGridCarbonIntensity(string location)
        {
            return _energyCarbonIntensity.TryGetValue(location, out var intensity) ? intensity : 0.5;
        }

        /// <summary>
        /// Gets all available material codes.
        /// </summary>
        public IEnumerable<string> GetAvailableMaterialCodes()
        {
            return _materialCarbonFactors.Keys;
        }

        /// <summary>
        /// Gets carbon factors by category.
        /// </summary>
        public Dictionary<string, MaterialCarbonFactor> GetMaterialsByCategory(CarbonCategory category)
        {
            return _materialCarbonFactors
                .Where(kvp => kvp.Value.Category == category)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        #endregion
    }

    #region Data Models

    public class MaterialCarbonFactor
    {
        public string Name { get; set; }
        public double CarbonFactor { get; set; } // kgCO2e per unit
        public string Unit { get; set; }
        public CarbonCategory Category { get; set; }
        public string LifecycleStage { get; set; }

        public MaterialCarbonFactor(string name, double carbonFactor, string unit, CarbonCategory category, string lifecycleStage)
        {
            Name = name;
            CarbonFactor = carbonFactor;
            Unit = unit;
            Category = category;
            LifecycleStage = lifecycleStage;
        }
    }

    public class TransportEmissionFactor
    {
        public string Name { get; set; }
        public double EmissionFactor { get; set; } // kgCO2e per tkm
        public string Unit { get; set; }

        public TransportEmissionFactor(string name, double emissionFactor, string unit)
        {
            Name = name;
            EmissionFactor = emissionFactor;
            Unit = unit;
        }
    }

    public enum CarbonCategory
    {
        Structure,
        Envelope,
        Finishes,
        Services
    }

    public class BuildingMaterialSchedule
    {
        public string ProjectName { get; set; }
        public double GrossFloorArea { get; set; } // m²
        public List<MaterialQuantity> Materials { get; set; } = new List<MaterialQuantity>();
    }

    public class MaterialQuantity
    {
        public string MaterialCode { get; set; }
        public double Quantity { get; set; }
        public string SourceLocation { get; set; }
    }

    public class BuildingEnergyProfile
    {
        public string Location { get; set; }
        public double GrossFloorArea { get; set; } // m²
        public double AnnualHeatingCoolingEnergy { get; set; } // kWh
        public double AnnualLightingEnergy { get; set; } // kWh
        public double AnnualEquipmentEnergy { get; set; } // kWh
        public double AnnualDHWEnergy { get; set; } // kWh
        public double AnnualVentilationEnergy { get; set; } // kWh
        public double OnSiteRenewableGeneration { get; set; } // kWh
    }

    public class EmbodiedCarbonResult
    {
        public string ProjectLocation { get; set; }
        public DateTime CalculationDate { get; set; }
        public double TotalEmbodiedCarbon { get; set; } // kgCO2e
        public double CarbonIntensity { get; set; } // kgCO2e/m²
        public Dictionary<string, double> LifecycleStages { get; set; }
        public Dictionary<CarbonCategory, double> CategoryBreakdown { get; set; }
        public List<MaterialCarbonBreakdown> MaterialBreakdown { get; set; }
    }

    public class MaterialCarbonBreakdown
    {
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double CarbonA1A3 { get; set; }
        public double CarbonA4 { get; set; }
        public double CarbonA5 { get; set; }
        public double TotalCarbon { get; set; }
        public CarbonCategory Category { get; set; }
    }

    public class OperationalCarbonResult
    {
        public string Location { get; set; }
        public DateTime CalculationDate { get; set; }
        public int BuildingLifespan { get; set; }
        public double AnnualOperationalCarbon { get; set; } // kgCO2e/year
        public double LifetimeOperationalCarbon { get; set; } // kgCO2e
        public double AnnualCarbonIntensity { get; set; } // kgCO2e/m²/year
        public double GridCarbonIntensity { get; set; } // kgCO2e/kWh
        public Dictionary<string, double> AnnualBreakdown { get; set; }
        public List<YearlyCarbon> YearlyProjection { get; set; }
    }

    public class YearlyCarbon
    {
        public int Year { get; set; }
        public double AnnualCarbon { get; set; }
        public double CumulativeCarbon { get; set; }
    }

    public class WholeLifeCarbonResult
    {
        public string ProjectName { get; set; }
        public string Location { get; set; }
        public double GrossFloorArea { get; set; }
        public int BuildingLifespan { get; set; }
        public DateTime CalculationDate { get; set; }

        // Lifecycle stages
        public double UpfrontCarbon { get; set; } // A1-A5
        public double OperationalCarbon { get; set; } // B6
        public double MaintenanceCarbon { get; set; } // B2-B5
        public double ReplacementCarbon { get; set; } // B4
        public double EndOfLifeCarbon { get; set; } // C1-C4
        public double BeyondLifecycleCarbon { get; set; } // D (can be negative)

        // Totals
        public double TotalWholeLifeCarbon { get; set; }
        public double NetWholeLifeCarbon { get; set; }

        // Intensity metrics
        public double WholeLifeCarbonIntensity { get; set; } // kgCO2e/m²
        public double UpfrontCarbonIntensity { get; set; } // kgCO2e/m²

        // Detailed breakdowns
        public EmbodiedCarbonResult EmbodiedCarbonDetails { get; set; }
        public OperationalCarbonResult OperationalCarbonDetails { get; set; }
        public BenchmarkComparison BenchmarkComparison { get; set; }
    }

    public class BenchmarkComparison
    {
        public double ActualUpfrontIntensity { get; set; }
        public double ActualWholeLifeIntensity { get; set; }
        public Dictionary<string, BenchmarkResult> BenchmarkResults { get; set; }
    }

    public class CarbonBenchmark
    {
        public double UpfrontTarget { get; set; }
        public double WholeLifeTarget { get; set; }
    }

    public class BenchmarkResult
    {
        public string BenchmarkName { get; set; }
        public double UpfrontTarget { get; set; }
        public double WholeLifeTarget { get; set; }
        public bool MeetsUpfrontTarget { get; set; }
        public bool MeetsWholeLifeTarget { get; set; }
        public double UpfrontVariance { get; set; }
        public double WholeLifeVariance { get; set; }
    }

    public class CarbonAssessment
    {
        public string ProjectName { get; set; }
        public DateTime AssessmentDate { get; set; }
        public double WholeLifeCarbon { get; set; }
        public double CarbonIntensity { get; set; }
    }

    public class CarbonReductionRecommendation
    {
        public RecommendationCategory Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double CurrentCarbon { get; set; }
        public double PotentialReduction { get; set; }
        public double Confidence { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string ImplementationNotes { get; set; }
    }

    public enum RecommendationCategory
    {
        MaterialSubstitution,
        RenewableEnergy,
        EnergyEfficiency,
        PassiveDesign,
        LocalMaterials,
        CircularEconomy,
        ConstructionProcess
    }

    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class MaterialAlternative
    {
        public string AlternativeName { get; set; }
        public double CarbonReduction { get; set; }
        public double Confidence { get; set; }
        public string Notes { get; set; }
    }

    #endregion
}
