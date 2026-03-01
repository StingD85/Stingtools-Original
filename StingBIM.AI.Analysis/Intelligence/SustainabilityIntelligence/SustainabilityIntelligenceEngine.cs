// ===================================================================
// StingBIM Sustainability Intelligence Engine
// Carbon footprint, LEED/BREEAM/WELL scoring, lifecycle assessment
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.SustainabilityIntelligence
{
    /// <summary>
    /// Comprehensive sustainability intelligence for green building certification,
    /// carbon analysis, energy modeling, and lifecycle assessment
    /// </summary>
    public sealed class SustainabilityIntelligenceEngine
    {
        private static readonly Lazy<SustainabilityIntelligenceEngine> _instance =
            new Lazy<SustainabilityIntelligenceEngine>(() => new SustainabilityIntelligenceEngine());
        public static SustainabilityIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, SustainabilityProject> _projects;
        private readonly ConcurrentDictionary<string, CertificationFramework> _frameworks;
        private readonly ConcurrentDictionary<string, MaterialEPD> _epds;
        private readonly object _lockObject = new object();

        public event EventHandler<SustainabilityAlertEventArgs> SustainabilityAlertRaised;

        private SustainabilityIntelligenceEngine()
        {
            _projects = new ConcurrentDictionary<string, SustainabilityProject>();
            _frameworks = new ConcurrentDictionary<string, CertificationFramework>();
            _epds = new ConcurrentDictionary<string, MaterialEPD>();

            InitializeCertificationFrameworks();
            InitializeMaterialEPDs();
        }

        #region Certification Framework Initialization

        private void InitializeCertificationFrameworks()
        {
            // LEED v4.1 BD+C
            var leedV4 = new CertificationFramework
            {
                Id = "LEED-V4.1-BDC",
                Name = "LEED v4.1 Building Design and Construction",
                Version = "4.1",
                Organization = "USGBC",
                MaxPoints = 110,
                CertificationLevels = new List<CertificationLevel>
                {
                    new CertificationLevel { Name = "Certified", MinPoints = 40, MaxPoints = 49 },
                    new CertificationLevel { Name = "Silver", MinPoints = 50, MaxPoints = 59 },
                    new CertificationLevel { Name = "Gold", MinPoints = 60, MaxPoints = 79 },
                    new CertificationLevel { Name = "Platinum", MinPoints = 80, MaxPoints = 110 }
                },
                Categories = new List<CreditCategory>
                {
                    new CreditCategory
                    {
                        Code = "IP",
                        Name = "Integrative Process",
                        MaxPoints = 1,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "IP-1", Name = "Integrative Process", MaxPoints = 1, Description = "Early analysis and design integration" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "LT",
                        Name = "Location and Transportation",
                        MaxPoints = 16,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "LT-1", Name = "LEED for Neighborhood Development Location", MaxPoints = 16, Description = "Located in LEED-ND certified development" },
                            new Credit { Code = "LT-2", Name = "Sensitive Land Protection", MaxPoints = 1, Description = "Avoid development on sensitive lands" },
                            new Credit { Code = "LT-3", Name = "High-Priority Site", MaxPoints = 2, Description = "Located on high-priority site" },
                            new Credit { Code = "LT-4", Name = "Surrounding Density and Diverse Uses", MaxPoints = 5, Description = "Located near existing infrastructure" },
                            new Credit { Code = "LT-5", Name = "Access to Quality Transit", MaxPoints = 5, Description = "Located near public transit" },
                            new Credit { Code = "LT-6", Name = "Bicycle Facilities", MaxPoints = 1, Description = "Bicycle storage and facilities" },
                            new Credit { Code = "LT-7", Name = "Reduced Parking Footprint", MaxPoints = 1, Description = "Minimize parking capacity" },
                            new Credit { Code = "LT-8", Name = "Electric Vehicles", MaxPoints = 1, Description = "EV charging infrastructure" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "SS",
                        Name = "Sustainable Sites",
                        MaxPoints = 10,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "SS-P1", Name = "Construction Activity Pollution Prevention", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "SS-1", Name = "Site Assessment", MaxPoints = 1, Description = "Site survey and analysis" },
                            new Credit { Code = "SS-2", Name = "Site Development - Protect or Restore Habitat", MaxPoints = 2, Description = "Native vegetation" },
                            new Credit { Code = "SS-3", Name = "Open Space", MaxPoints = 1, Description = "Outdoor space access" },
                            new Credit { Code = "SS-4", Name = "Rainwater Management", MaxPoints = 3, Description = "Stormwater management" },
                            new Credit { Code = "SS-5", Name = "Heat Island Reduction", MaxPoints = 2, Description = "Reduce heat island effect" },
                            new Credit { Code = "SS-6", Name = "Light Pollution Reduction", MaxPoints = 1, Description = "Minimize light pollution" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "WE",
                        Name = "Water Efficiency",
                        MaxPoints = 11,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "WE-P1", Name = "Outdoor Water Use Reduction", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "WE-P2", Name = "Indoor Water Use Reduction", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "WE-P3", Name = "Building-Level Water Metering", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "WE-1", Name = "Outdoor Water Use Reduction", MaxPoints = 2, Description = "50% reduction" },
                            new Credit { Code = "WE-2", Name = "Indoor Water Use Reduction", MaxPoints = 6, Description = "Up to 50% reduction" },
                            new Credit { Code = "WE-3", Name = "Cooling Tower Water Use", MaxPoints = 2, Description = "Optimize cooling tower water" },
                            new Credit { Code = "WE-4", Name = "Water Metering", MaxPoints = 1, Description = "Subsystem metering" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "EA",
                        Name = "Energy and Atmosphere",
                        MaxPoints = 33,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "EA-P1", Name = "Fundamental Commissioning and Verification", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "EA-P2", Name = "Minimum Energy Performance", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "EA-P3", Name = "Building-Level Energy Metering", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "EA-P4", Name = "Fundamental Refrigerant Management", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "EA-1", Name = "Enhanced Commissioning", MaxPoints = 6, Description = "Enhanced Cx process" },
                            new Credit { Code = "EA-2", Name = "Optimize Energy Performance", MaxPoints = 18, Description = "Energy cost savings" },
                            new Credit { Code = "EA-3", Name = "Advanced Energy Metering", MaxPoints = 1, Description = "Advanced metering" },
                            new Credit { Code = "EA-4", Name = "Demand Response", MaxPoints = 2, Description = "Demand response capability" },
                            new Credit { Code = "EA-5", Name = "Renewable Energy", MaxPoints = 5, Description = "On-site renewable energy" },
                            new Credit { Code = "EA-6", Name = "Enhanced Refrigerant Management", MaxPoints = 1, Description = "Low-impact refrigerants" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "MR",
                        Name = "Materials and Resources",
                        MaxPoints = 13,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "MR-P1", Name = "Storage and Collection of Recyclables", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "MR-P2", Name = "Construction and Demolition Waste Management Planning", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "MR-1", Name = "Building Life-Cycle Impact Reduction", MaxPoints = 5, Description = "Whole building LCA" },
                            new Credit { Code = "MR-2", Name = "Building Product Disclosure and Optimization - EPD", MaxPoints = 2, Description = "EPD sourcing" },
                            new Credit { Code = "MR-3", Name = "Building Product Disclosure and Optimization - Sourcing", MaxPoints = 2, Description = "Responsible sourcing" },
                            new Credit { Code = "MR-4", Name = "Building Product Disclosure and Optimization - Material Ingredients", MaxPoints = 2, Description = "Material ingredient reporting" },
                            new Credit { Code = "MR-5", Name = "Construction and Demolition Waste Management", MaxPoints = 2, Description = "Divert C&D waste" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "EQ",
                        Name = "Indoor Environmental Quality",
                        MaxPoints = 16,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "EQ-P1", Name = "Minimum Indoor Air Quality Performance", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "EQ-P2", Name = "Environmental Tobacco Smoke Control", MaxPoints = 0, IsPrerequisite = true },
                            new Credit { Code = "EQ-1", Name = "Enhanced Indoor Air Quality Strategies", MaxPoints = 2, Description = "Enhanced IAQ" },
                            new Credit { Code = "EQ-2", Name = "Low-Emitting Materials", MaxPoints = 3, Description = "VOC limits" },
                            new Credit { Code = "EQ-3", Name = "Construction Indoor Air Quality Management Plan", MaxPoints = 1, Description = "IAQ during construction" },
                            new Credit { Code = "EQ-4", Name = "Indoor Air Quality Assessment", MaxPoints = 2, Description = "IAQ testing" },
                            new Credit { Code = "EQ-5", Name = "Thermal Comfort", MaxPoints = 1, Description = "ASHRAE 55 compliance" },
                            new Credit { Code = "EQ-6", Name = "Interior Lighting", MaxPoints = 2, Description = "Lighting quality" },
                            new Credit { Code = "EQ-7", Name = "Daylight", MaxPoints = 3, Description = "Daylight access" },
                            new Credit { Code = "EQ-8", Name = "Quality Views", MaxPoints = 1, Description = "Views to outdoors" },
                            new Credit { Code = "EQ-9", Name = "Acoustic Performance", MaxPoints = 1, Description = "Acoustic design" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "IN",
                        Name = "Innovation",
                        MaxPoints = 6,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "IN-1", Name = "Innovation", MaxPoints = 5, Description = "Innovative strategies" },
                            new Credit { Code = "IN-2", Name = "LEED Accredited Professional", MaxPoints = 1, Description = "LEED AP on team" }
                        }
                    },
                    new CreditCategory
                    {
                        Code = "RP",
                        Name = "Regional Priority",
                        MaxPoints = 4,
                        Credits = new List<Credit>
                        {
                            new Credit { Code = "RP-1", Name = "Regional Priority Credits", MaxPoints = 4, Description = "Regionally important credits" }
                        }
                    }
                }
            };

            _frameworks.TryAdd(leedV4.Id, leedV4);

            // BREEAM
            var breeam = new CertificationFramework
            {
                Id = "BREEAM-NC-2018",
                Name = "BREEAM New Construction 2018",
                Version = "2018",
                Organization = "BRE Global",
                MaxPoints = 100,
                CertificationLevels = new List<CertificationLevel>
                {
                    new CertificationLevel { Name = "Pass", MinPoints = 30, MaxPoints = 44 },
                    new CertificationLevel { Name = "Good", MinPoints = 45, MaxPoints = 54 },
                    new CertificationLevel { Name = "Very Good", MinPoints = 55, MaxPoints = 69 },
                    new CertificationLevel { Name = "Excellent", MinPoints = 70, MaxPoints = 84 },
                    new CertificationLevel { Name = "Outstanding", MinPoints = 85, MaxPoints = 100 }
                },
                Categories = new List<CreditCategory>
                {
                    new CreditCategory { Code = "Man", Name = "Management", MaxPoints = 12, Weight = 0.12m },
                    new CreditCategory { Code = "Hea", Name = "Health and Wellbeing", MaxPoints = 15, Weight = 0.15m },
                    new CreditCategory { Code = "Ene", Name = "Energy", MaxPoints = 19, Weight = 0.19m },
                    new CreditCategory { Code = "Tra", Name = "Transport", MaxPoints = 8, Weight = 0.08m },
                    new CreditCategory { Code = "Wat", Name = "Water", MaxPoints = 6, Weight = 0.06m },
                    new CreditCategory { Code = "Mat", Name = "Materials", MaxPoints = 13.5m, Weight = 0.135m },
                    new CreditCategory { Code = "Wst", Name = "Waste", MaxPoints = 7.5m, Weight = 0.075m },
                    new CreditCategory { Code = "LE", Name = "Land Use and Ecology", MaxPoints = 10, Weight = 0.10m },
                    new CreditCategory { Code = "Pol", Name = "Pollution", MaxPoints = 9, Weight = 0.09m },
                    new CreditCategory { Code = "Inn", Name = "Innovation", MaxPoints = 10, Weight = 0.10m }
                }
            };

            _frameworks.TryAdd(breeam.Id, breeam);

            // WELL v2
            var well = new CertificationFramework
            {
                Id = "WELL-V2",
                Name = "WELL Building Standard v2",
                Version = "2.0",
                Organization = "IWBI",
                MaxPoints = 100,
                CertificationLevels = new List<CertificationLevel>
                {
                    new CertificationLevel { Name = "Bronze", MinPoints = 40, MaxPoints = 49 },
                    new CertificationLevel { Name = "Silver", MinPoints = 50, MaxPoints = 59 },
                    new CertificationLevel { Name = "Gold", MinPoints = 60, MaxPoints = 79 },
                    new CertificationLevel { Name = "Platinum", MinPoints = 80, MaxPoints = 100 }
                },
                Categories = new List<CreditCategory>
                {
                    new CreditCategory { Code = "A", Name = "Air", MaxPoints = 14 },
                    new CreditCategory { Code = "W", Name = "Water", MaxPoints = 8 },
                    new CreditCategory { Code = "N", Name = "Nourishment", MaxPoints = 11 },
                    new CreditCategory { Code = "L", Name = "Light", MaxPoints = 11 },
                    new CreditCategory { Code = "M", Name = "Movement", MaxPoints = 9 },
                    new CreditCategory { Code = "T", Name = "Thermal Comfort", MaxPoints = 7 },
                    new CreditCategory { Code = "S", Name = "Sound", MaxPoints = 8 },
                    new CreditCategory { Code = "Ma", Name = "Materials", MaxPoints = 9 },
                    new CreditCategory { Code = "Mi", Name = "Mind", MaxPoints = 13 },
                    new CreditCategory { Code = "C", Name = "Community", MaxPoints = 10 }
                }
            };

            _frameworks.TryAdd(well.Id, well);

            // Green Star (Australia)
            var greenStar = new CertificationFramework
            {
                Id = "GREENSTAR-DESIGN",
                Name = "Green Star Design & As Built",
                Version = "1.3",
                Organization = "GBCA",
                MaxPoints = 100,
                CertificationLevels = new List<CertificationLevel>
                {
                    new CertificationLevel { Name = "4 Star", MinPoints = 45, MaxPoints = 59 },
                    new CertificationLevel { Name = "5 Star", MinPoints = 60, MaxPoints = 74 },
                    new CertificationLevel { Name = "6 Star", MinPoints = 75, MaxPoints = 100 }
                }
            };

            _frameworks.TryAdd(greenStar.Id, greenStar);

            // EDGE (IFC)
            var edge = new CertificationFramework
            {
                Id = "EDGE-V3",
                Name = "EDGE Certification",
                Version = "3.0",
                Organization = "IFC/GBCI",
                MaxPoints = 100,
                CertificationLevels = new List<CertificationLevel>
                {
                    new CertificationLevel { Name = "EDGE Certified", MinPoints = 20, MaxPoints = 39 },
                    new CertificationLevel { Name = "EDGE Advanced", MinPoints = 40, MaxPoints = 59 },
                    new CertificationLevel { Name = "EDGE Zero Carbon", MinPoints = 60, MaxPoints = 100 }
                }
            };

            _frameworks.TryAdd(edge.Id, edge);
        }

        private void InitializeMaterialEPDs()
        {
            // Environmental Product Declarations database
            var epds = new List<MaterialEPD>
            {
                new MaterialEPD
                {
                    Id = "EPD-CONCRETE-4000",
                    MaterialName = "Ready-Mix Concrete 4000 PSI",
                    FunctionalUnit = "1 m³",
                    GWP_A1A3 = 320.5m, // kg CO2e
                    GWP_A4 = 15.2m,
                    GWP_A5 = 8.5m,
                    GWP_B = 0,
                    GWP_C = 12.3m,
                    GWP_D = -5.2m,
                    ODP = 0.000012m,
                    AP = 0.85m,
                    EP = 0.045m,
                    POCP = 0.028m,
                    PrimaryEnergy_Renewable = 45.2m,
                    PrimaryEnergy_NonRenewable = 1850.5m,
                    WaterUse = 185.0m,
                    ServiceLife = 75,
                    EPDProgram = "NSF International"
                },
                new MaterialEPD
                {
                    Id = "EPD-STEEL-STRUCTURAL",
                    MaterialName = "Structural Steel (Hot-Rolled)",
                    FunctionalUnit = "1 tonne",
                    GWP_A1A3 = 1850.0m,
                    GWP_A4 = 45.5m,
                    GWP_A5 = 28.2m,
                    GWP_B = 0,
                    GWP_C = 18.5m,
                    GWP_D = -450.0m,
                    ODP = 0.000085m,
                    AP = 4.85m,
                    EP = 0.28m,
                    POCP = 0.185m,
                    PrimaryEnergy_Renewable = 850.5m,
                    PrimaryEnergy_NonRenewable = 22500.0m,
                    WaterUse = 28500.0m,
                    ServiceLife = 100,
                    EPDProgram = "UL Environment",
                    RecycledContent = 0.25m
                },
                new MaterialEPD
                {
                    Id = "EPD-ALUMINUM-CURTAINWALL",
                    MaterialName = "Aluminum Curtain Wall Framing",
                    FunctionalUnit = "1 m²",
                    GWP_A1A3 = 125.5m,
                    GWP_A4 = 8.5m,
                    GWP_A5 = 12.2m,
                    GWP_B = 0,
                    GWP_C = 4.5m,
                    GWP_D = -42.5m,
                    ODP = 0.000025m,
                    AP = 0.95m,
                    EP = 0.065m,
                    POCP = 0.042m,
                    PrimaryEnergy_Renewable = 125.5m,
                    PrimaryEnergy_NonRenewable = 1850.0m,
                    WaterUse = 450.0m,
                    ServiceLife = 40,
                    EPDProgram = "IBU",
                    RecycledContent = 0.35m
                },
                new MaterialEPD
                {
                    Id = "EPD-GYPSUM-BOARD",
                    MaterialName = "Gypsum Board 5/8\"",
                    FunctionalUnit = "1 m²",
                    GWP_A1A3 = 3.85m,
                    GWP_A4 = 0.45m,
                    GWP_A5 = 0.28m,
                    GWP_B = 0,
                    GWP_C = 0.12m,
                    GWP_D = -0.05m,
                    ODP = 0.0000008m,
                    AP = 0.018m,
                    EP = 0.0025m,
                    POCP = 0.0012m,
                    PrimaryEnergy_Renewable = 2.5m,
                    PrimaryEnergy_NonRenewable = 48.5m,
                    WaterUse = 8.5m,
                    ServiceLife = 50,
                    EPDProgram = "UL Environment",
                    RecycledContent = 0.15m
                },
                new MaterialEPD
                {
                    Id = "EPD-INSULATION-MINERAL",
                    MaterialName = "Mineral Wool Insulation",
                    FunctionalUnit = "1 m² at R-19",
                    GWP_A1A3 = 4.25m,
                    GWP_A4 = 0.35m,
                    GWP_A5 = 0.18m,
                    GWP_B = 0,
                    GWP_C = 0.08m,
                    GWP_D = -0.02m,
                    ODP = 0.0000002m,
                    AP = 0.025m,
                    EP = 0.0018m,
                    POCP = 0.0008m,
                    PrimaryEnergy_Renewable = 1.8m,
                    PrimaryEnergy_NonRenewable = 52.5m,
                    WaterUse = 12.5m,
                    ServiceLife = 75,
                    EPDProgram = "NSF International",
                    RecycledContent = 0.40m
                }
            };

            foreach (var epd in epds)
            {
                _epds.TryAdd(epd.Id, epd);
            }
        }

        #endregion

        #region Carbon Footprint Analysis

        public async Task<CarbonFootprintAnalysis> AnalyzeCarbonFootprintAsync(CarbonAnalysisRequest request)
        {
            var analysis = new CarbonFootprintAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                AnalysisDate = DateTime.UtcNow,
                AnalysisScope = request.Scope,
                MaterialBreakdown = new List<MaterialCarbonImpact>(),
                OperationalCarbon = new OperationalCarbonAnalysis(),
                Recommendations = new List<CarbonReduction>()
            };

            await Task.Run(() =>
            {
                // Calculate embodied carbon (A1-A3 Product Stage)
                foreach (var material in request.MaterialQuantities)
                {
                    if (_epds.TryGetValue(material.EPDId, out var epd))
                    {
                        var impact = new MaterialCarbonImpact
                        {
                            MaterialName = epd.MaterialName,
                            Quantity = material.Quantity,
                            Unit = material.Unit,
                            GWP_ProductStage = material.Quantity * epd.GWP_A1A3,
                            GWP_Transport = material.Quantity * epd.GWP_A4,
                            GWP_Construction = material.Quantity * epd.GWP_A5,
                            GWP_EndOfLife = material.Quantity * epd.GWP_C,
                            GWP_Benefits = material.Quantity * epd.GWP_D,
                            RecycledContent = epd.RecycledContent
                        };

                        impact.TotalGWP = impact.GWP_ProductStage + impact.GWP_Transport +
                                         impact.GWP_Construction + impact.GWP_EndOfLife + impact.GWP_Benefits;

                        analysis.MaterialBreakdown.Add(impact);
                    }
                }

                // Calculate operational carbon
                if (request.EnergyData != null)
                {
                    analysis.OperationalCarbon = CalculateOperationalCarbon(request.EnergyData, request.BuildingLifespan);
                }

                // Total embodied carbon
                analysis.TotalEmbodiedCarbon = analysis.MaterialBreakdown.Sum(m => m.TotalGWP);
                analysis.TotalEmbodiedCarbonPerSF = request.GrossArea > 0
                    ? analysis.TotalEmbodiedCarbon / request.GrossArea
                    : 0;

                // Total operational carbon
                analysis.TotalOperationalCarbon = analysis.OperationalCarbon.TotalLifetimeCarbon;

                // Whole life carbon
                analysis.WholeLifeCarbon = analysis.TotalEmbodiedCarbon + analysis.TotalOperationalCarbon;
                analysis.WholeLifeCarbonPerSF = request.GrossArea > 0
                    ? analysis.WholeLifeCarbon / request.GrossArea
                    : 0;

                // Benchmark comparison
                analysis.Benchmark = GetCarbonBenchmark(request.BuildingType);
                analysis.BenchmarkComparison = analysis.WholeLifeCarbonPerSF / analysis.Benchmark.MedianValue;

                // Generate reduction recommendations
                GenerateCarbonReductionRecommendations(analysis);
            });

            return analysis;
        }

        private OperationalCarbonAnalysis CalculateOperationalCarbon(EnergyData energyData, int buildingLifespan)
        {
            var operational = new OperationalCarbonAnalysis
            {
                AnnualElectricity_kWh = energyData.AnnualElectricity_kWh,
                AnnualGas_Therms = energyData.AnnualGas_Therms,
                ElectricityGridFactor = energyData.ElectricityGridFactor ?? 0.42m, // kg CO2e/kWh (US average)
                GasFactor = 5.3m // kg CO2e/therm
            };

            operational.AnnualElectricityCarbon = operational.AnnualElectricity_kWh * operational.ElectricityGridFactor;
            operational.AnnualGasCarbon = operational.AnnualGas_Therms * operational.GasFactor;
            operational.AnnualOperationalCarbon = operational.AnnualElectricityCarbon + operational.AnnualGasCarbon;
            operational.TotalLifetimeCarbon = operational.AnnualOperationalCarbon * buildingLifespan;

            // Project grid decarbonization
            var decarbonizationRate = 0.02m; // 2% annual reduction
            decimal projectedLifetimeCarbon = 0;
            for (int year = 1; year <= buildingLifespan; year++)
            {
                var yearFactor = (decimal)Math.Pow((double)(1 - decarbonizationRate), year);
                projectedLifetimeCarbon += operational.AnnualOperationalCarbon * yearFactor;
            }
            operational.ProjectedLifetimeCarbon_GridDecarbonization = projectedLifetimeCarbon;

            return operational;
        }

        private CarbonBenchmark GetCarbonBenchmark(string buildingType)
        {
            var benchmarks = new Dictionary<string, CarbonBenchmark>
            {
                { "office", new CarbonBenchmark { BuildingType = "Office", LowValue = 35, MedianValue = 55, HighValue = 85, Unit = "kg CO2e/SF/year" } },
                { "healthcare", new CarbonBenchmark { BuildingType = "Healthcare", LowValue = 65, MedianValue = 95, HighValue = 145, Unit = "kg CO2e/SF/year" } },
                { "education", new CarbonBenchmark { BuildingType = "Education", LowValue = 40, MedianValue = 62, HighValue = 95, Unit = "kg CO2e/SF/year" } },
                { "residential", new CarbonBenchmark { BuildingType = "Residential", LowValue = 25, MedianValue = 42, HighValue = 68, Unit = "kg CO2e/SF/year" } },
                { "retail", new CarbonBenchmark { BuildingType = "Retail", LowValue = 45, MedianValue = 72, HighValue = 115, Unit = "kg CO2e/SF/year" } }
            };

            return benchmarks.TryGetValue(buildingType.ToLower(), out var benchmark)
                ? benchmark
                : new CarbonBenchmark { BuildingType = "Generic", LowValue = 40, MedianValue = 60, HighValue = 90, Unit = "kg CO2e/SF/year" };
        }

        private void GenerateCarbonReductionRecommendations(CarbonFootprintAnalysis analysis)
        {
            // Structural carbon reductions
            if (analysis.MaterialBreakdown.Any(m => m.MaterialName.Contains("Concrete")))
            {
                var concreteCarbon = analysis.MaterialBreakdown
                    .Where(m => m.MaterialName.Contains("Concrete"))
                    .Sum(m => m.TotalGWP);

                analysis.Recommendations.Add(new CarbonReduction
                {
                    Category = "Structural",
                    Strategy = "Use Low-Carbon Concrete",
                    Description = "Specify concrete with supplementary cementitious materials (fly ash, slag) to reduce cement content",
                    PotentialReduction = concreteCarbon * 0.30m,
                    ImplementationCost = CostImpact.Neutral,
                    Difficulty = ImplementationDifficulty.Low
                });

                analysis.Recommendations.Add(new CarbonReduction
                {
                    Category = "Structural",
                    Strategy = "Optimize Structural Design",
                    Description = "Use post-tensioned concrete or optimize member sizing to reduce concrete volume",
                    PotentialReduction = concreteCarbon * 0.15m,
                    ImplementationCost = CostImpact.Savings,
                    Difficulty = ImplementationDifficulty.Medium
                });
            }

            // Steel carbon reductions
            if (analysis.MaterialBreakdown.Any(m => m.MaterialName.Contains("Steel")))
            {
                var steelCarbon = analysis.MaterialBreakdown
                    .Where(m => m.MaterialName.Contains("Steel"))
                    .Sum(m => m.TotalGWP);

                analysis.Recommendations.Add(new CarbonReduction
                {
                    Category = "Structural",
                    Strategy = "Specify High Recycled Content Steel",
                    Description = "Use EAF steel with 90%+ recycled content instead of BOF steel",
                    PotentialReduction = steelCarbon * 0.40m,
                    ImplementationCost = CostImpact.SlightIncrease,
                    Difficulty = ImplementationDifficulty.Low
                });
            }

            // Operational carbon reductions
            if (analysis.OperationalCarbon.AnnualOperationalCarbon > 0)
            {
                analysis.Recommendations.Add(new CarbonReduction
                {
                    Category = "Energy",
                    Strategy = "On-Site Solar PV",
                    Description = "Install rooftop photovoltaic system to offset electricity consumption",
                    PotentialReduction = analysis.OperationalCarbon.AnnualElectricityCarbon * 0.50m * 60, // 60 year lifetime
                    ImplementationCost = CostImpact.Investment,
                    Difficulty = ImplementationDifficulty.Medium
                });

                analysis.Recommendations.Add(new CarbonReduction
                {
                    Category = "Energy",
                    Strategy = "Enhanced Building Envelope",
                    Description = "Improve insulation and glazing performance to reduce heating/cooling loads",
                    PotentialReduction = analysis.OperationalCarbon.AnnualOperationalCarbon * 0.20m * 60,
                    ImplementationCost = CostImpact.SlightIncrease,
                    Difficulty = ImplementationDifficulty.Low
                });

                analysis.Recommendations.Add(new CarbonReduction
                {
                    Category = "Energy",
                    Strategy = "Electrify Building Systems",
                    Description = "Replace gas heating with high-efficiency heat pumps",
                    PotentialReduction = analysis.OperationalCarbon.AnnualGasCarbon * 0.70m * 60,
                    ImplementationCost = CostImpact.SlightIncrease,
                    Difficulty = ImplementationDifficulty.Medium
                });
            }

            // Sort by reduction potential
            analysis.Recommendations = analysis.Recommendations
                .OrderByDescending(r => r.PotentialReduction)
                .ToList();

            analysis.TotalReductionPotential = analysis.Recommendations.Sum(r => r.PotentialReduction);
        }

        #endregion

        #region Certification Tracking

        public CertificationAssessment AssessCertification(CertificationAssessmentRequest request)
        {
            if (!_frameworks.TryGetValue(request.FrameworkId, out var framework))
                return null;

            var assessment = new CertificationAssessment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                FrameworkId = framework.Id,
                FrameworkName = framework.Name,
                AssessmentDate = DateTime.UtcNow,
                CategoryScores = new List<CategoryScore>(),
                CreditStatus = new List<CreditStatus>()
            };

            decimal totalPoints = 0;
            decimal maxPossiblePoints = 0;

            foreach (var category in framework.Categories)
            {
                var categoryScore = new CategoryScore
                {
                    CategoryCode = category.Code,
                    CategoryName = category.Name,
                    MaxPoints = category.MaxPoints,
                    AchievedPoints = 0,
                    TargetPoints = 0
                };

                if (category.Credits != null)
                {
                    foreach (var credit in category.Credits)
                    {
                        var creditInput = request.CreditInputs?.FirstOrDefault(c => c.CreditCode == credit.Code);
                        var status = new CreditStatus
                        {
                            CreditCode = credit.Code,
                            CreditName = credit.Name,
                            MaxPoints = credit.MaxPoints,
                            IsPrerequisite = credit.IsPrerequisite,
                            Status = creditInput?.Status ?? (credit.IsPrerequisite ? CreditStatusType.Required : CreditStatusType.NotPursued),
                            AchievedPoints = creditInput?.AchievedPoints ?? 0,
                            TargetPoints = creditInput?.TargetPoints ?? 0,
                            Documentation = creditInput?.Documentation,
                            Notes = creditInput?.Notes
                        };

                        assessment.CreditStatus.Add(status);

                        categoryScore.AchievedPoints += status.AchievedPoints;
                        categoryScore.TargetPoints += status.TargetPoints;

                        if (!credit.IsPrerequisite)
                        {
                            maxPossiblePoints += credit.MaxPoints;
                        }
                    }
                }
                else
                {
                    maxPossiblePoints += category.MaxPoints;
                }

                totalPoints += categoryScore.AchievedPoints;
                assessment.CategoryScores.Add(categoryScore);
            }

            assessment.TotalAchievedPoints = totalPoints;
            assessment.MaxPossiblePoints = maxPossiblePoints;

            // Determine certification level
            foreach (var level in framework.CertificationLevels.OrderByDescending(l => l.MinPoints))
            {
                if (totalPoints >= level.MinPoints)
                {
                    assessment.ProjectedLevel = level.Name;
                    break;
                }
            }

            // Calculate gap to next level
            var currentLevelIndex = framework.CertificationLevels.FindIndex(l => l.Name == assessment.ProjectedLevel);
            if (currentLevelIndex >= 0 && currentLevelIndex < framework.CertificationLevels.Count - 1)
            {
                var nextLevel = framework.CertificationLevels[currentLevelIndex + 1];
                assessment.PointsToNextLevel = nextLevel.MinPoints - totalPoints;
                assessment.NextLevelName = nextLevel.Name;
            }

            // Identify opportunity credits
            assessment.OpportunityCredits = assessment.CreditStatus
                .Where(c => !c.IsPrerequisite && c.Status == CreditStatusType.NotPursued)
                .OrderByDescending(c => c.MaxPoints)
                .Take(10)
                .ToList();

            return assessment;
        }

        public SustainabilityProject CreateProject(SustainabilityProjectRequest request)
        {
            var project = new SustainabilityProject
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                BuildingType = request.BuildingType,
                Location = request.Location,
                GrossArea = request.GrossArea,
                CreatedDate = DateTime.UtcNow,
                TargetCertifications = request.TargetCertifications ?? new List<string>(),
                CarbonTarget = request.CarbonTarget,
                Assessments = new List<string>()
            };

            _projects.TryAdd(project.Id, project);
            return project;
        }

        #endregion

        #region Energy Modeling

        public EnergyModelResult ModelEnergy(EnergyModelRequest request)
        {
            var result = new EnergyModelResult
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ModelDate = DateTime.UtcNow,
                ClimateZone = DetermineClimateZone(request.Location),
                EndUseBreakdown = new Dictionary<string, decimal>(),
                MonthlyConsumption = new List<MonthlyEnergy>()
            };

            // Calculate baseline energy use
            var baselineEUI = GetBaselineEUI(request.BuildingType, result.ClimateZone);
            result.BaselineEUI = baselineEUI;
            result.BaselineAnnualEnergy = baselineEUI * request.GrossArea;

            // Apply efficiency measures
            decimal efficiencyFactor = 1.0m;

            // Envelope efficiency
            if (request.EnvelopeEfficiency != null)
            {
                var envelopeReduction = CalculateEnvelopeReduction(request.EnvelopeEfficiency);
                efficiencyFactor *= (1 - envelopeReduction);
            }

            // HVAC efficiency
            if (request.HVACEfficiency != null)
            {
                var hvacReduction = CalculateHVACReduction(request.HVACEfficiency);
                efficiencyFactor *= (1 - hvacReduction);
            }

            // Lighting efficiency
            if (request.LightingEfficiency != null)
            {
                var lightingReduction = CalculateLightingReduction(request.LightingEfficiency);
                efficiencyFactor *= (1 - lightingReduction);
            }

            result.ProposedEUI = baselineEUI * efficiencyFactor;
            result.ProposedAnnualEnergy = result.ProposedEUI * request.GrossArea;
            result.EnergySavingsPercent = (1 - efficiencyFactor) * 100;

            // End use breakdown
            var heatingPercent = GetHeatingPercent(result.ClimateZone);
            var coolingPercent = GetCoolingPercent(result.ClimateZone);
            var lightingPercent = 0.18m;
            var plugLoadPercent = 0.22m;
            var otherPercent = 1 - heatingPercent - coolingPercent - lightingPercent - plugLoadPercent;

            result.EndUseBreakdown["Heating"] = result.ProposedAnnualEnergy * heatingPercent;
            result.EndUseBreakdown["Cooling"] = result.ProposedAnnualEnergy * coolingPercent;
            result.EndUseBreakdown["Lighting"] = result.ProposedAnnualEnergy * lightingPercent;
            result.EndUseBreakdown["Plug Loads"] = result.ProposedAnnualEnergy * plugLoadPercent;
            result.EndUseBreakdown["Other"] = result.ProposedAnnualEnergy * otherPercent;

            // Monthly profile
            var monthlyFactors = GetMonthlyFactors(result.ClimateZone);
            for (int month = 1; month <= 12; month++)
            {
                result.MonthlyConsumption.Add(new MonthlyEnergy
                {
                    Month = month,
                    MonthName = new DateTime(2026, month, 1).ToString("MMMM"),
                    Energy_kWh = result.ProposedAnnualEnergy * monthlyFactors[month - 1] / 12
                });
            }

            // Calculate renewable energy offset
            if (request.RenewableCapacity_kW > 0)
            {
                var annualPVProduction = request.RenewableCapacity_kW * GetSolarCapacityFactor(request.Location) * 8760;
                result.RenewableGeneration = annualPVProduction;
                result.RenewableOffsetPercent = Math.Min(annualPVProduction / result.ProposedAnnualEnergy * 100, 100);
                result.NetEnergy = Math.Max(result.ProposedAnnualEnergy - annualPVProduction, 0);
            }
            else
            {
                result.NetEnergy = result.ProposedAnnualEnergy;
            }

            // Energy cost
            result.AnnualEnergyCost = result.NetEnergy * (request.ElectricityRate ?? 0.12m);
            result.BaselineEnergyCost = result.BaselineAnnualEnergy * (request.ElectricityRate ?? 0.12m);
            result.AnnualCostSavings = result.BaselineEnergyCost - result.AnnualEnergyCost;

            return result;
        }

        private string DetermineClimateZone(string location)
        {
            // Simplified climate zone determination
            var hotClimateLocations = new[] { "Miami", "Phoenix", "Houston", "Lagos", "Nairobi", "Kampala", "Dar es Salaam" };
            var coldClimateLocations = new[] { "Minneapolis", "Chicago", "Boston", "Denver", "Toronto" };
            var mixedClimateLocations = new[] { "Atlanta", "Dallas", "Los Angeles", "San Francisco" };

            if (hotClimateLocations.Any(l => location.Contains(l, StringComparison.OrdinalIgnoreCase)))
                return "Hot-Humid";
            if (coldClimateLocations.Any(l => location.Contains(l, StringComparison.OrdinalIgnoreCase)))
                return "Cold";
            if (mixedClimateLocations.Any(l => location.Contains(l, StringComparison.OrdinalIgnoreCase)))
                return "Mixed";

            return "Mixed"; // Default
        }

        private decimal GetBaselineEUI(string buildingType, string climateZone)
        {
            // EUI in kBtu/SF/year
            var euis = new Dictionary<string, Dictionary<string, decimal>>
            {
                { "office", new Dictionary<string, decimal> { { "Hot-Humid", 75 }, { "Cold", 85 }, { "Mixed", 78 } } },
                { "healthcare", new Dictionary<string, decimal> { { "Hot-Humid", 185 }, { "Cold", 210 }, { "Mixed", 195 } } },
                { "education", new Dictionary<string, decimal> { { "Hot-Humid", 65 }, { "Cold", 78 }, { "Mixed", 70 } } },
                { "residential", new Dictionary<string, decimal> { { "Hot-Humid", 45 }, { "Cold", 65 }, { "Mixed", 52 } } },
                { "retail", new Dictionary<string, decimal> { { "Hot-Humid", 85 }, { "Cold", 95 }, { "Mixed", 88 } } }
            };

            if (euis.TryGetValue(buildingType.ToLower(), out var typeEuis))
            {
                if (typeEuis.TryGetValue(climateZone, out var eui))
                    return eui;
            }

            return 75; // Default office
        }

        private decimal GetHeatingPercent(string climateZone) => climateZone switch
        {
            "Cold" => 0.35m,
            "Hot-Humid" => 0.08m,
            _ => 0.22m
        };

        private decimal GetCoolingPercent(string climateZone) => climateZone switch
        {
            "Cold" => 0.12m,
            "Hot-Humid" => 0.35m,
            _ => 0.22m
        };

        private decimal[] GetMonthlyFactors(string climateZone)
        {
            return climateZone switch
            {
                "Cold" => new decimal[] { 1.3m, 1.25m, 1.1m, 0.9m, 0.8m, 0.85m, 0.9m, 0.88m, 0.82m, 0.95m, 1.1m, 1.25m },
                "Hot-Humid" => new decimal[] { 0.85m, 0.82m, 0.88m, 0.95m, 1.1m, 1.25m, 1.3m, 1.28m, 1.15m, 1.0m, 0.88m, 0.84m },
                _ => new decimal[] { 1.1m, 1.05m, 0.95m, 0.88m, 0.92m, 1.05m, 1.12m, 1.1m, 0.98m, 0.9m, 0.95m, 1.05m }
            };
        }

        private decimal GetSolarCapacityFactor(string location)
        {
            // Simplified capacity factor based on location
            var highSolarLocations = new[] { "Phoenix", "Las Vegas", "Nairobi", "Kampala" };
            var lowSolarLocations = new[] { "Seattle", "London", "Portland" };

            if (highSolarLocations.Any(l => location.Contains(l, StringComparison.OrdinalIgnoreCase)))
                return 0.22m;
            if (lowSolarLocations.Any(l => location.Contains(l, StringComparison.OrdinalIgnoreCase)))
                return 0.14m;

            return 0.18m;
        }

        private decimal CalculateEnvelopeReduction(EnvelopeEfficiency envelope)
        {
            decimal reduction = 0;

            // Wall R-value impact
            if (envelope.WallRValue >= 30) reduction += 0.08m;
            else if (envelope.WallRValue >= 20) reduction += 0.05m;

            // Roof R-value impact
            if (envelope.RoofRValue >= 40) reduction += 0.06m;
            else if (envelope.RoofRValue >= 30) reduction += 0.04m;

            // Window U-factor impact
            if (envelope.WindowUFactor <= 0.25m) reduction += 0.06m;
            else if (envelope.WindowUFactor <= 0.30m) reduction += 0.04m;

            // Air tightness
            if (envelope.ACH50 <= 1.0m) reduction += 0.05m;
            else if (envelope.ACH50 <= 2.0m) reduction += 0.03m;

            return reduction;
        }

        private decimal CalculateHVACReduction(HVACEfficiency hvac)
        {
            decimal reduction = 0;

            // Cooling efficiency
            if (hvac.CoolingEER >= 18) reduction += 0.08m;
            else if (hvac.CoolingEER >= 14) reduction += 0.05m;

            // Heating efficiency
            if (hvac.HeatingEfficiency >= 95) reduction += 0.06m;
            else if (hvac.HeatingEfficiency >= 90) reduction += 0.04m;

            // Energy recovery
            if (hvac.HasEnergyRecovery) reduction += 0.05m;

            // Variable speed
            if (hvac.HasVariableSpeed) reduction += 0.04m;

            return reduction;
        }

        private decimal CalculateLightingReduction(LightingEfficiency lighting)
        {
            decimal reduction = 0;

            // LPD reduction
            if (lighting.LPD <= 0.6m) reduction += 0.06m;
            else if (lighting.LPD <= 0.8m) reduction += 0.04m;

            // Daylight controls
            if (lighting.HasDaylightControls) reduction += 0.03m;

            // Occupancy sensors
            if (lighting.HasOccupancySensors) reduction += 0.02m;

            return reduction;
        }

        #endregion

        #region Life Cycle Assessment

        public LifeCycleAssessment PerformLCA(LCARequest request)
        {
            var lca = new LifeCycleAssessment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                AssessmentDate = DateTime.UtcNow,
                StudyPeriod = request.StudyPeriod,
                FunctionalUnit = $"1 m² of building over {request.StudyPeriod} years",
                ImpactCategories = new List<ImpactCategory>(),
                LifeCycleStages = new List<LifeCycleStage>()
            };

            // Calculate impacts by life cycle stage
            var productStage = new LifeCycleStage
            {
                Stage = "A1-A3 Product",
                GWP = 0,
                ODP = 0,
                AP = 0,
                EP = 0,
                POCP = 0
            };

            var constructionStage = new LifeCycleStage
            {
                Stage = "A4-A5 Construction",
                GWP = 0,
                ODP = 0,
                AP = 0,
                EP = 0,
                POCP = 0
            };

            var useStage = new LifeCycleStage
            {
                Stage = "B1-B7 Use",
                GWP = 0,
                ODP = 0,
                AP = 0,
                EP = 0,
                POCP = 0
            };

            var endOfLifeStage = new LifeCycleStage
            {
                Stage = "C1-C4 End of Life",
                GWP = 0,
                ODP = 0,
                AP = 0,
                EP = 0,
                POCP = 0
            };

            var benefitsStage = new LifeCycleStage
            {
                Stage = "D Benefits",
                GWP = 0,
                ODP = 0,
                AP = 0,
                EP = 0,
                POCP = 0
            };

            // Process materials
            foreach (var material in request.Materials)
            {
                if (_epds.TryGetValue(material.EPDId, out var epd))
                {
                    productStage.GWP += material.Quantity * epd.GWP_A1A3;
                    productStage.ODP += material.Quantity * epd.ODP;
                    productStage.AP += material.Quantity * epd.AP;
                    productStage.EP += material.Quantity * epd.EP;
                    productStage.POCP += material.Quantity * epd.POCP;

                    constructionStage.GWP += material.Quantity * (epd.GWP_A4 + epd.GWP_A5);

                    // Replacement cycles during use phase
                    int replacements = request.StudyPeriod / epd.ServiceLife - 1;
                    if (replacements > 0)
                    {
                        useStage.GWP += replacements * material.Quantity * epd.GWP_A1A3;
                    }

                    endOfLifeStage.GWP += material.Quantity * epd.GWP_C;
                    benefitsStage.GWP += material.Quantity * epd.GWP_D;
                }
            }

            // Add operational energy impacts
            if (request.AnnualEnergy_kWh > 0)
            {
                var gridFactor = 0.42m; // kg CO2e/kWh
                useStage.GWP += request.AnnualEnergy_kWh * gridFactor * request.StudyPeriod;
            }

            lca.LifeCycleStages.Add(productStage);
            lca.LifeCycleStages.Add(constructionStage);
            lca.LifeCycleStages.Add(useStage);
            lca.LifeCycleStages.Add(endOfLifeStage);
            lca.LifeCycleStages.Add(benefitsStage);

            // Total impacts
            lca.TotalGWP = lca.LifeCycleStages.Sum(s => s.GWP);
            lca.TotalODP = lca.LifeCycleStages.Sum(s => s.ODP);
            lca.TotalAP = lca.LifeCycleStages.Sum(s => s.AP);
            lca.TotalEP = lca.LifeCycleStages.Sum(s => s.EP);
            lca.TotalPOCP = lca.LifeCycleStages.Sum(s => s.POCP);

            // Per functional unit
            if (request.GrossArea > 0)
            {
                lca.GWP_PerFU = lca.TotalGWP / request.GrossArea;
            }

            // Impact categories summary
            lca.ImpactCategories.Add(new ImpactCategory { Name = "Global Warming Potential", Abbreviation = "GWP", Value = lca.TotalGWP, Unit = "kg CO2e" });
            lca.ImpactCategories.Add(new ImpactCategory { Name = "Ozone Depletion Potential", Abbreviation = "ODP", Value = lca.TotalODP, Unit = "kg CFC-11e" });
            lca.ImpactCategories.Add(new ImpactCategory { Name = "Acidification Potential", Abbreviation = "AP", Value = lca.TotalAP, Unit = "kg SO2e" });
            lca.ImpactCategories.Add(new ImpactCategory { Name = "Eutrophication Potential", Abbreviation = "EP", Value = lca.TotalEP, Unit = "kg Ne" });
            lca.ImpactCategories.Add(new ImpactCategory { Name = "Photochemical Ozone Creation", Abbreviation = "POCP", Value = lca.TotalPOCP, Unit = "kg O3e" });

            return lca;
        }

        #endregion

        #region Water Analysis

        public WaterAnalysis AnalyzeWaterUse(WaterAnalysisRequest request)
        {
            var analysis = new WaterAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                AnalysisDate = DateTime.UtcNow,
                FixtureBreakdown = new List<FixtureWaterUse>()
            };

            // Baseline fixture water use
            var fixtures = new List<FixtureWaterUse>
            {
                new FixtureWaterUse { FixtureType = "Water Closet", BaselineRate = 1.6m, Unit = "GPF", DailyUses = 3, Count = request.WaterClosetCount },
                new FixtureWaterUse { FixtureType = "Urinal", BaselineRate = 1.0m, Unit = "GPF", DailyUses = 3, Count = request.UrinalCount },
                new FixtureWaterUse { FixtureType = "Lavatory Faucet", BaselineRate = 0.5m, Unit = "GPM", DurationMinutes = 0.25m, DailyUses = 3, Count = request.LavatoryCount },
                new FixtureWaterUse { FixtureType = "Kitchen Faucet", BaselineRate = 2.2m, Unit = "GPM", DurationMinutes = 4, DailyUses = 1, Count = request.KitchenFaucetCount },
                new FixtureWaterUse { FixtureType = "Shower", BaselineRate = 2.5m, Unit = "GPM", DurationMinutes = 8, DailyUses = 1, Count = request.ShowerCount }
            };

            decimal baselineDaily = 0;
            decimal proposedDaily = 0;

            foreach (var fixture in fixtures)
            {
                if (fixture.Count > 0)
                {
                    // Calculate baseline daily use
                    if (fixture.Unit == "GPF")
                    {
                        fixture.BaselineDailyUse = fixture.BaselineRate * fixture.DailyUses * fixture.Count * request.Occupants;
                    }
                    else // GPM
                    {
                        fixture.BaselineDailyUse = fixture.BaselineRate * fixture.DurationMinutes * fixture.DailyUses * fixture.Count * request.Occupants;
                    }

                    baselineDaily += fixture.BaselineDailyUse;

                    // Apply efficiency measures
                    var efficiencyFactor = request.HighEfficiencyFixtures ? 0.70m : 1.0m;
                    fixture.ProposedRate = fixture.BaselineRate * efficiencyFactor;
                    fixture.ProposedDailyUse = fixture.BaselineDailyUse * efficiencyFactor;
                    proposedDaily += fixture.ProposedDailyUse;

                    analysis.FixtureBreakdown.Add(fixture);
                }
            }

            analysis.BaselineDailyUse = baselineDaily;
            analysis.ProposedDailyUse = proposedDaily;
            analysis.BaselineAnnualUse = baselineDaily * 260; // Work days
            analysis.ProposedAnnualUse = proposedDaily * 260;
            analysis.WaterSavingsPercent = (1 - proposedDaily / baselineDaily) * 100;
            analysis.AnnualSavings_Gallons = analysis.BaselineAnnualUse - analysis.ProposedAnnualUse;

            // Rainwater harvesting potential
            if (request.RoofArea > 0 && request.AnnualRainfall > 0)
            {
                analysis.RainwaterHarvestPotential = request.RoofArea * request.AnnualRainfall * 0.623m * 0.80m; // 80% capture efficiency
            }

            // Graywater reuse potential
            if (request.EnableGraywaterReuse)
            {
                var lavatoryWater = analysis.FixtureBreakdown
                    .Where(f => f.FixtureType == "Lavatory Faucet")
                    .Sum(f => f.ProposedDailyUse);
                analysis.GraywaterReusePotential = lavatoryWater * 260 * 0.70m; // 70% recovery
            }

            // Net water use
            analysis.NetAnnualUse = analysis.ProposedAnnualUse -
                                    (analysis.RainwaterHarvestPotential ?? 0) -
                                    (analysis.GraywaterReusePotential ?? 0);

            // Cost savings
            var waterRate = request.WaterRate ?? 0.005m; // $/gallon
            analysis.AnnualCostSavings = analysis.AnnualSavings_Gallons * waterRate;

            return analysis;
        }

        #endregion

        #region Helper Methods

        public List<CertificationFramework> GetAvailableFrameworks()
        {
            return _frameworks.Values.ToList();
        }

        public CertificationFramework GetFramework(string frameworkId)
        {
            _frameworks.TryGetValue(frameworkId, out var framework);
            return framework;
        }

        public List<MaterialEPD> GetAvailableEPDs()
        {
            return _epds.Values.ToList();
        }

        public SustainabilityProject GetProject(string projectId)
        {
            _projects.TryGetValue(projectId, out var project);
            return project;
        }

        #endregion
    }

    #region Data Models

    public class SustainabilityProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string BuildingType { get; set; }
        public string Location { get; set; }
        public decimal GrossArea { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> TargetCertifications { get; set; }
        public decimal? CarbonTarget { get; set; }
        public List<string> Assessments { get; set; }
    }

    public class SustainabilityProjectRequest
    {
        public string Name { get; set; }
        public string BuildingType { get; set; }
        public string Location { get; set; }
        public decimal GrossArea { get; set; }
        public List<string> TargetCertifications { get; set; }
        public decimal? CarbonTarget { get; set; }
    }

    public class CertificationFramework
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Organization { get; set; }
        public decimal MaxPoints { get; set; }
        public List<CertificationLevel> CertificationLevels { get; set; }
        public List<CreditCategory> Categories { get; set; }
    }

    public class CertificationLevel
    {
        public string Name { get; set; }
        public decimal MinPoints { get; set; }
        public decimal MaxPoints { get; set; }
    }

    public class CreditCategory
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal MaxPoints { get; set; }
        public decimal Weight { get; set; }
        public List<Credit> Credits { get; set; }
    }

    public class Credit
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal MaxPoints { get; set; }
        public string Description { get; set; }
        public bool IsPrerequisite { get; set; }
    }

    public class MaterialEPD
    {
        public string Id { get; set; }
        public string MaterialName { get; set; }
        public string FunctionalUnit { get; set; }
        public decimal GWP_A1A3 { get; set; }
        public decimal GWP_A4 { get; set; }
        public decimal GWP_A5 { get; set; }
        public decimal GWP_B { get; set; }
        public decimal GWP_C { get; set; }
        public decimal GWP_D { get; set; }
        public decimal ODP { get; set; }
        public decimal AP { get; set; }
        public decimal EP { get; set; }
        public decimal POCP { get; set; }
        public decimal PrimaryEnergy_Renewable { get; set; }
        public decimal PrimaryEnergy_NonRenewable { get; set; }
        public decimal WaterUse { get; set; }
        public int ServiceLife { get; set; }
        public string EPDProgram { get; set; }
        public decimal RecycledContent { get; set; }
    }

    public class CarbonAnalysisRequest
    {
        public string ProjectId { get; set; }
        public string BuildingType { get; set; }
        public decimal GrossArea { get; set; }
        public string Scope { get; set; }
        public List<MaterialQuantity> MaterialQuantities { get; set; }
        public EnergyData EnergyData { get; set; }
        public int BuildingLifespan { get; set; } = 60;
    }

    public class MaterialQuantity
    {
        public string EPDId { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
    }

    public class EnergyData
    {
        public decimal AnnualElectricity_kWh { get; set; }
        public decimal AnnualGas_Therms { get; set; }
        public decimal? ElectricityGridFactor { get; set; }
    }

    public class CarbonFootprintAnalysis
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public string AnalysisScope { get; set; }
        public List<MaterialCarbonImpact> MaterialBreakdown { get; set; }
        public OperationalCarbonAnalysis OperationalCarbon { get; set; }
        public decimal TotalEmbodiedCarbon { get; set; }
        public decimal TotalEmbodiedCarbonPerSF { get; set; }
        public decimal TotalOperationalCarbon { get; set; }
        public decimal WholeLifeCarbon { get; set; }
        public decimal WholeLifeCarbonPerSF { get; set; }
        public CarbonBenchmark Benchmark { get; set; }
        public decimal BenchmarkComparison { get; set; }
        public List<CarbonReduction> Recommendations { get; set; }
        public decimal TotalReductionPotential { get; set; }
    }

    public class MaterialCarbonImpact
    {
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal GWP_ProductStage { get; set; }
        public decimal GWP_Transport { get; set; }
        public decimal GWP_Construction { get; set; }
        public decimal GWP_EndOfLife { get; set; }
        public decimal GWP_Benefits { get; set; }
        public decimal TotalGWP { get; set; }
        public decimal RecycledContent { get; set; }
    }

    public class OperationalCarbonAnalysis
    {
        public decimal AnnualElectricity_kWh { get; set; }
        public decimal AnnualGas_Therms { get; set; }
        public decimal ElectricityGridFactor { get; set; }
        public decimal GasFactor { get; set; }
        public decimal AnnualElectricityCarbon { get; set; }
        public decimal AnnualGasCarbon { get; set; }
        public decimal AnnualOperationalCarbon { get; set; }
        public decimal TotalLifetimeCarbon { get; set; }
        public decimal ProjectedLifetimeCarbon_GridDecarbonization { get; set; }
    }

    public class CarbonBenchmark
    {
        public string BuildingType { get; set; }
        public decimal LowValue { get; set; }
        public decimal MedianValue { get; set; }
        public decimal HighValue { get; set; }
        public string Unit { get; set; }
    }

    public class CarbonReduction
    {
        public string Category { get; set; }
        public string Strategy { get; set; }
        public string Description { get; set; }
        public decimal PotentialReduction { get; set; }
        public CostImpact ImplementationCost { get; set; }
        public ImplementationDifficulty Difficulty { get; set; }
    }

    public class CertificationAssessmentRequest
    {
        public string ProjectId { get; set; }
        public string FrameworkId { get; set; }
        public List<CreditInput> CreditInputs { get; set; }
    }

    public class CreditInput
    {
        public string CreditCode { get; set; }
        public CreditStatusType Status { get; set; }
        public decimal AchievedPoints { get; set; }
        public decimal TargetPoints { get; set; }
        public string Documentation { get; set; }
        public string Notes { get; set; }
    }

    public class CertificationAssessment
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string FrameworkId { get; set; }
        public string FrameworkName { get; set; }
        public DateTime AssessmentDate { get; set; }
        public List<CategoryScore> CategoryScores { get; set; }
        public List<CreditStatus> CreditStatus { get; set; }
        public decimal TotalAchievedPoints { get; set; }
        public decimal MaxPossiblePoints { get; set; }
        public string ProjectedLevel { get; set; }
        public decimal PointsToNextLevel { get; set; }
        public string NextLevelName { get; set; }
        public List<CreditStatus> OpportunityCredits { get; set; }
    }

    public class CategoryScore
    {
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public decimal MaxPoints { get; set; }
        public decimal AchievedPoints { get; set; }
        public decimal TargetPoints { get; set; }
    }

    public class CreditStatus
    {
        public string CreditCode { get; set; }
        public string CreditName { get; set; }
        public decimal MaxPoints { get; set; }
        public bool IsPrerequisite { get; set; }
        public CreditStatusType Status { get; set; }
        public decimal AchievedPoints { get; set; }
        public decimal TargetPoints { get; set; }
        public string Documentation { get; set; }
        public string Notes { get; set; }
    }

    public class EnergyModelRequest
    {
        public string ProjectId { get; set; }
        public string BuildingType { get; set; }
        public string Location { get; set; }
        public decimal GrossArea { get; set; }
        public EnvelopeEfficiency EnvelopeEfficiency { get; set; }
        public HVACEfficiency HVACEfficiency { get; set; }
        public LightingEfficiency LightingEfficiency { get; set; }
        public decimal RenewableCapacity_kW { get; set; }
        public decimal? ElectricityRate { get; set; }
    }

    public class EnvelopeEfficiency
    {
        public decimal WallRValue { get; set; }
        public decimal RoofRValue { get; set; }
        public decimal WindowUFactor { get; set; }
        public decimal WindowSHGC { get; set; }
        public decimal ACH50 { get; set; }
    }

    public class HVACEfficiency
    {
        public decimal CoolingEER { get; set; }
        public decimal HeatingEfficiency { get; set; }
        public bool HasEnergyRecovery { get; set; }
        public bool HasVariableSpeed { get; set; }
    }

    public class LightingEfficiency
    {
        public decimal LPD { get; set; }
        public bool HasDaylightControls { get; set; }
        public bool HasOccupancySensors { get; set; }
    }

    public class EnergyModelResult
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime ModelDate { get; set; }
        public string ClimateZone { get; set; }
        public decimal BaselineEUI { get; set; }
        public decimal ProposedEUI { get; set; }
        public decimal BaselineAnnualEnergy { get; set; }
        public decimal ProposedAnnualEnergy { get; set; }
        public decimal EnergySavingsPercent { get; set; }
        public Dictionary<string, decimal> EndUseBreakdown { get; set; }
        public List<MonthlyEnergy> MonthlyConsumption { get; set; }
        public decimal RenewableGeneration { get; set; }
        public decimal RenewableOffsetPercent { get; set; }
        public decimal NetEnergy { get; set; }
        public decimal BaselineEnergyCost { get; set; }
        public decimal AnnualEnergyCost { get; set; }
        public decimal AnnualCostSavings { get; set; }
    }

    public class MonthlyEnergy
    {
        public int Month { get; set; }
        public string MonthName { get; set; }
        public decimal Energy_kWh { get; set; }
    }

    public class LCARequest
    {
        public string ProjectId { get; set; }
        public int StudyPeriod { get; set; } = 60;
        public decimal GrossArea { get; set; }
        public List<MaterialQuantity> Materials { get; set; }
        public decimal AnnualEnergy_kWh { get; set; }
    }

    public class LifeCycleAssessment
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public int StudyPeriod { get; set; }
        public string FunctionalUnit { get; set; }
        public List<ImpactCategory> ImpactCategories { get; set; }
        public List<LifeCycleStage> LifeCycleStages { get; set; }
        public decimal TotalGWP { get; set; }
        public decimal TotalODP { get; set; }
        public decimal TotalAP { get; set; }
        public decimal TotalEP { get; set; }
        public decimal TotalPOCP { get; set; }
        public decimal GWP_PerFU { get; set; }
    }

    public class ImpactCategory
    {
        public string Name { get; set; }
        public string Abbreviation { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; }
    }

    public class LifeCycleStage
    {
        public string Stage { get; set; }
        public decimal GWP { get; set; }
        public decimal ODP { get; set; }
        public decimal AP { get; set; }
        public decimal EP { get; set; }
        public decimal POCP { get; set; }
    }

    public class WaterAnalysisRequest
    {
        public string ProjectId { get; set; }
        public int Occupants { get; set; }
        public int WaterClosetCount { get; set; }
        public int UrinalCount { get; set; }
        public int LavatoryCount { get; set; }
        public int KitchenFaucetCount { get; set; }
        public int ShowerCount { get; set; }
        public bool HighEfficiencyFixtures { get; set; }
        public decimal RoofArea { get; set; }
        public decimal AnnualRainfall { get; set; }
        public bool EnableGraywaterReuse { get; set; }
        public decimal? WaterRate { get; set; }
    }

    public class WaterAnalysis
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<FixtureWaterUse> FixtureBreakdown { get; set; }
        public decimal BaselineDailyUse { get; set; }
        public decimal ProposedDailyUse { get; set; }
        public decimal BaselineAnnualUse { get; set; }
        public decimal ProposedAnnualUse { get; set; }
        public decimal WaterSavingsPercent { get; set; }
        public decimal AnnualSavings_Gallons { get; set; }
        public decimal? RainwaterHarvestPotential { get; set; }
        public decimal? GraywaterReusePotential { get; set; }
        public decimal NetAnnualUse { get; set; }
        public decimal AnnualCostSavings { get; set; }
    }

    public class FixtureWaterUse
    {
        public string FixtureType { get; set; }
        public decimal BaselineRate { get; set; }
        public decimal ProposedRate { get; set; }
        public string Unit { get; set; }
        public int DailyUses { get; set; }
        public decimal DurationMinutes { get; set; }
        public int Count { get; set; }
        public decimal BaselineDailyUse { get; set; }
        public decimal ProposedDailyUse { get; set; }
    }

    public class SustainabilityAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
    }

    public enum CreditStatusType
    {
        NotPursued,
        Targeted,
        InProgress,
        Achieved,
        Denied,
        Required
    }

    public enum CostImpact
    {
        Savings,
        Neutral,
        SlightIncrease,
        Investment
    }

    public enum ImplementationDifficulty
    {
        Low,
        Medium,
        High
    }

    #endregion
}
