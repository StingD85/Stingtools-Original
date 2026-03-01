// =========================================================================
// StingBIM.AI.Knowledge - Standards Integration Module
// Connects AI reasoning to StingBIM.Standards library (32 standards)
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using StingBIM.Standards;
using CIBSE = StingBIM.Standards.CIBSE;
using EAS = StingBIM.Standards.EAS;
using KEBS = StingBIM.Standards.KEBS;
using UNBS = StingBIM.Standards.UNBS;

namespace StingBIM.AI.Knowledge.Standards
{
    /// <summary>
    /// Integrates building standards from StingBIM.Standards with AI reasoning.
    /// Dynamically loads all 32 standards from StandardsAPI and provides
    /// intelligent code interpretation, live compliance calculations,
    /// and region-aware guidance.
    /// </summary>
    public class StandardsIntegration
    {
        private readonly Dictionary<string, StandardsProfile> _profiles;
        private readonly Dictionary<string, List<StandardRequirement>> _requirements;
        private readonly Dictionary<string, CodeInterpretation> _interpretations;
        private readonly Dictionary<string, RegionalAdaptation> _regionalAdaptations;
        private readonly List<CompliancePath> _compliancePaths;

        // Live connection to StandardsAPI
        private readonly List<StandardInfo> _allStandards;
        private readonly StandardsCalculationBridge _calculationBridge;

        public StandardsIntegration()
        {
            _profiles = new Dictionary<string, StandardsProfile>();
            _requirements = new Dictionary<string, List<StandardRequirement>>();
            _interpretations = new Dictionary<string, CodeInterpretation>();
            _regionalAdaptations = new Dictionary<string, RegionalAdaptation>();
            _compliancePaths = new List<CompliancePath>();

            // Load real standard metadata from StandardsAPI
            _allStandards = StandardsAPI.GetAllStandards();
            _calculationBridge = new StandardsCalculationBridge();

            LoadProfilesFromStandardsAPI();
            LoadRequirementsFromStandards();
            InitializeInterpretations();
            LoadRegionalAdaptationsFromStandards();
            InitializeCompliancePaths();
        }

        /// <summary>
        /// All 32 standards loaded from StandardsAPI.
        /// </summary>
        public IReadOnlyList<StandardInfo> AllStandards => _allStandards;

        /// <summary>
        /// Available live calculation types.
        /// </summary>
        public IReadOnlyList<string> AvailableCalculations => _calculationBridge.AvailableCalculations;

        #region Initialization - Dynamic from StandardsAPI

        private void LoadProfilesFromStandardsAPI()
        {
            // Group the 32 standards from StandardsAPI into logical profiles
            var byDiscipline = _allStandards
                .GroupBy(s => NormalizeDiscipline(s.Discipline))
                .ToDictionary(g => g.Key, g => g.ToList());

            // --- Electrical ---
            AddProfileFromStandards("NEC_Electrical", "NEC / Electrical Standards",
                "US National Electrical Code and related electrical standards",
                new[] { "NEC 2023", "NFPA 70" },
                new[] { "US", "International" },
                new[] { Discipline.Electrical });

            // --- HVAC / Energy ---
            AddProfileFromStandards("ASHRAE", "ASHRAE Standards",
                "American Society of Heating, Refrigerating and Air-Conditioning Engineers",
                new[] { "ASHRAE", "IMC 2021", "SMACNA" },
                new[] { "US", "International" },
                new[] { Discipline.HVAC, Discipline.Energy });

            // --- MEP / Building Services ---
            AddProfileFromStandards("CIBSE", "CIBSE Guides",
                "Chartered Institution of Building Services Engineers",
                new[] { "CIBSE" },
                new[] { "UK", "International", "EAC", "Commonwealth" },
                new[] { Discipline.HVAC, Discipline.Electrical, Discipline.Lighting });

            // --- Plumbing ---
            AddProfileFromStandards("IPC", "International Plumbing Code",
                "Plumbing design and drainage standards",
                new[] { "IPC 2021" },
                new[] { "US", "International" },
                new[] { Discipline.Plumbing });

            // --- European Structural ---
            AddProfileFromStandards("Eurocodes", "European Structural Standards",
                "European standards for structural design (EN 1990-1999)",
                new[] { "Eurocodes", "Eurocodes Complete" },
                new[] { "EU", "UK", "International" },
                new[] { Discipline.Structural });

            // --- British Standards ---
            AddProfileFromStandards("BS", "British Standards",
                "UK building and construction standards",
                new[] { "BS" },
                new[] { "UK", "EAC", "Commonwealth" },
                new[] { Discipline.Electrical, Discipline.Structural });

            // --- US Structural ---
            AddProfileFromStandards("US_Structural", "US Structural Standards",
                "American structural design codes",
                new[] { "ACI", "AISC" },
                new[] { "US" },
                new[] { Discipline.Structural });

            // --- IBC ---
            AddProfileFromStandards("IBC", "International Building Code",
                "Model building code for US jurisdictions",
                new[] { "IBC 2021" },
                new[] { "US" },
                new[] { Discipline.Architecture, Discipline.FireSafety });

            // --- Fire Safety ---
            AddProfileFromStandards("NFPA", "NFPA Fire Protection",
                "National Fire Protection Association standards",
                new[] { "NFPA 13", "NFPA 72", "NFPA 101" },
                new[] { "US", "International" },
                new[] { Discipline.FireSafety });

            // --- ISO / Quality ---
            AddProfileFromStandards("ISO", "ISO Management Standards",
                "Quality, environment, safety, and BIM information management",
                new[] { "ISO 9001", "ISO 14001", "ISO 45001", "ISO 19650" },
                new[] { "Global" },
                new[] { Discipline.Architecture });

            // --- Green Building ---
            AddProfileFromStandards("GreenBuilding", "Green Building & Sustainability",
                "LEED, BREEAM, Green Star, EDGE rating systems",
                new[] { "Green Building" },
                new[] { "Global" },
                new[] { Discipline.Energy, Discipline.Architecture });

            // --- Materials ---
            AddProfileFromStandards("ASTM", "ASTM Material Standards",
                "American Society for Testing and Materials",
                new[] { "ASTM" },
                new[] { "US", "International" },
                new[] { Discipline.Structural });

            // --- East African Community ---
            AddProfileFromStandards("EAC", "East African Community Standards",
                "Regional standards for East Africa",
                new[] { "EAS", "UNBS", "KEBS", "TBS", "RSB", "BBN", "SSBS" },
                new[] { "Uganda", "Kenya", "Tanzania", "Rwanda", "Burundi", "South Sudan", "East Africa" },
                new[] { Discipline.Architecture, Discipline.Structural, Discipline.Electrical, Discipline.Plumbing });

            // --- ECOWAS ---
            AddProfileFromStandards("ECOWAS", "West African Standards",
                "Economic Community of West African States building codes",
                new[] { "ECOWAS" },
                new[] { "West Africa", "Nigeria", "Ghana", "Senegal" },
                new[] { Discipline.Architecture, Discipline.Structural });

            // --- Southern Africa ---
            AddProfileFromStandards("SANS", "South African Standards",
                "South African National Standards and CIDB",
                new[] { "SANS", "CIDB" },
                new[] { "South Africa", "ZA" },
                new[] { Discipline.Architecture, Discipline.Structural });
        }

        private void AddProfileFromStandards(
            string profileId, string name, string description,
            string[] standardShortNames, string[] regions, Discipline[] disciplines)
        {
            var refs = new List<StandardReference>();
            foreach (var shortName in standardShortNames)
            {
                var info = _allStandards.FirstOrDefault(s =>
                    s.ShortName.Equals(shortName, StringComparison.OrdinalIgnoreCase));
                if (info != null)
                {
                    refs.Add(new StandardReference
                    {
                        Code = info.ShortName,
                        Title = info.FullName,
                        Version = "Current"
                    });
                }
            }

            if (refs.Count > 0)
            {
                AddProfile(new StandardsProfile
                {
                    ProfileId = profileId,
                    Name = name,
                    Description = description,
                    Standards = refs.ToArray(),
                    ApplicableRegions = regions,
                    PrimaryDisciplines = disciplines
                });
            }
        }

        private void LoadRequirementsFromStandards()
        {
            LoadFireSafetyRequirements();
            LoadAccessibilityRequirements();
            LoadEnergyRequirements();
            LoadVentilationRequirements();
            LoadStructuralRequirements();
            LoadElectricalRequirements();
            LoadPlumbingRequirements();
            LoadWaterStorageRequirements();
        }

        private void LoadFireSafetyRequirements()
        {
            var reqs = new List<StandardRequirement>
            {
                // IBC fire safety
                new StandardRequirement
                {
                    RequirementId = "FS-001",
                    StandardCode = "IBC 2021",
                    Section = "1006",
                    Title = "Means of Egress Width",
                    Description = "Minimum width of means of egress components",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinCorridorWidth"] = 1118,
                        ["MinStairWidth"] = 1118,
                        ["MinDoorWidth"] = 813
                    },
                    ApplicableOccupancies = new[] { "A", "B", "E", "I", "R" }
                },
                new StandardRequirement
                {
                    RequirementId = "FS-002",
                    StandardCode = "IBC 2021",
                    Section = "1017",
                    Title = "Dead End Corridors",
                    Description = "Maximum dead end corridor length",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxDeadEndLength"] = 6100,
                        ["MaxDeadEndUnsprinklered"] = 6100
                    },
                    ApplicableOccupancies = new[] { "A", "B", "E", "I", "R" }
                },
                new StandardRequirement
                {
                    RequirementId = "FS-003",
                    StandardCode = "IBC 2021",
                    Section = "1004",
                    Title = "Occupant Load",
                    Description = "Occupant load factors for egress calculation",
                    Criteria = new Dictionary<string, object>
                    {
                        ["Assembly_Standing"] = 0.46,
                        ["Assembly_Seated"] = 0.65,
                        ["Business"] = 9.3,
                        ["Educational"] = 1.9,
                        ["Residential"] = 18.6
                    }
                }
            };

            // EAS fire safety - from live standards
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "FS-EAS-001",
                    StandardCode = "EAS",
                    Section = "Fire Safety",
                    Title = "Fire Resistance Rating",
                    Description = "Minimum fire resistance rating per building height and occupancy (EAS)",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinRating_LowRise"] = 30,
                        ["MinRating_MidRise"] = 60,
                        ["MinRating_HighRise_15m"] = 60,
                        ["MinRating_HighRise_28m"] = 90,
                        ["MinRating_HighRise_45m"] = 120
                    }
                });

                reqs.Add(new StandardRequirement
                {
                    RequirementId = "FS-EAS-002",
                    StandardCode = "EAS",
                    Section = "Fire Safety",
                    Title = "Maximum Travel Distance",
                    Description = "Maximum travel distance to exit (EAS), increased 25% if sprinklered",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxDistance_Residential"] = 30.0,
                        ["MaxDistance_Office"] = 40.0,
                        ["MaxDistance_Assembly"] = 20.0,
                        ["MaxDistance_Industrial"] = 35.0,
                        ["SprinklerIncreaseFactor"] = 1.25
                    }
                });
            }
            catch { /* Standards not available */ }

            // UNBS fire safety - from live standards
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "FS-UNBS-001",
                    StandardCode = "UNBS",
                    Section = "DUS 449",
                    Title = "Required Number of Exits (Uganda)",
                    Description = "Minimum exits based on occupancy per UNBS",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinExits_Under50"] = 1,
                        ["MinExits_Under500"] = 2,
                        ["MinExits_Under1000"] = 3,
                        ["MinExits_Over1000"] = 4
                    }
                });
            }
            catch { /* Standards not available */ }

            // KEBS fire safety
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "FS-KEBS-001",
                    StandardCode = "KEBS",
                    Section = "Fire Safety",
                    Title = "Fire Safety Category (Kenya)",
                    Description = "Fire systems required per building category (KEBS)",
                    Criteria = new Dictionary<string, object>
                    {
                        ["HighRiseThreshold_m"] = 30.0,
                        ["MediumRiseThreshold_m"] = 10.0,
                        ["HighOccupancyThreshold"] = 1000,
                        ["MediumOccupancyThreshold"] = 100
                    }
                });
            }
            catch { /* Standards not available */ }

            AddRequirements("FireSafety", reqs);
        }

        private void LoadAccessibilityRequirements()
        {
            var reqs = new List<StandardRequirement>
            {
                new StandardRequirement
                {
                    RequirementId = "AC-001",
                    StandardCode = "ADA/IBC",
                    Section = "1104",
                    Title = "Accessible Routes",
                    Description = "Requirements for accessible routes",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinClearWidth"] = 915,
                        ["MinPassingWidth"] = 1525,
                        ["MaxSlope"] = 0.05,
                        ["MaxRampSlope"] = 0.083
                    }
                },
                new StandardRequirement
                {
                    RequirementId = "AC-002",
                    StandardCode = "ADA",
                    Section = "603",
                    Title = "Accessible Toilet Rooms",
                    Description = "Requirements for accessible toilet facilities",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinTurningSpace"] = 1525,
                        ["MinClearFloorAtWC"] = 1525,
                        ["GrabBarHeight"] = 838
                    }
                }
            };

            // UNBS accessibility
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "AC-UNBS-001",
                    StandardCode = "UNBS",
                    Section = "Accessibility",
                    Title = "Ramp Requirements (Uganda)",
                    Description = "Accessibility ramp slope and dimensions per UNBS",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxRampSlope"] = 0.083,
                        ["MinRampWidth"] = 1200
                    }
                });
            }
            catch { /* Standards not available */ }

            AddRequirements("Accessibility", reqs);
        }

        private void LoadEnergyRequirements()
        {
            var reqs = new List<StandardRequirement>
            {
                new StandardRequirement
                {
                    RequirementId = "EN-001",
                    StandardCode = "ASHRAE 90.1",
                    Section = "5.5",
                    Title = "Building Envelope Requirements",
                    Description = "Thermal performance requirements for building envelope",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxWallUValue_4A"] = 0.48,
                        ["MaxRoofUValue_4A"] = 0.27,
                        ["MaxWindowUValue_4A"] = 2.56,
                        ["MaxSHGC_4A"] = 0.40
                    }
                },
                new StandardRequirement
                {
                    RequirementId = "EN-002",
                    StandardCode = "ASHRAE 90.1",
                    Section = "6.5",
                    Title = "HVAC System Efficiency",
                    Description = "Minimum efficiency requirements for HVAC systems",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinCOP_Chiller"] = 5.5,
                        ["MinEER_SplitSystem"] = 11.0,
                        ["MinAFUE_Boiler"] = 0.80
                    }
                }
            };

            // CIBSE recommended U-values
            try
            {
                var uValues = CIBSE.CIBSEStandards.GuideC_ReferenceData.ThermalProperties
                    .GetRecommendedUValues();
                if (uValues != null && uValues.Count > 0)
                {
                    var criteria = new Dictionary<string, object>();
                    foreach (var kv in uValues)
                        criteria[$"MaxUValue_{kv.Key}"] = kv.Value;

                    reqs.Add(new StandardRequirement
                    {
                        RequirementId = "EN-CIBSE-001",
                        StandardCode = "CIBSE Guide C",
                        Section = "Thermal Properties",
                        Title = "Recommended U-Values (CIBSE)",
                        Description = "Maximum recommended U-values for building elements per CIBSE Guide C",
                        Criteria = criteria
                    });
                }
            }
            catch { /* CIBSE not available */ }

            // CIBSE energy use intensity benchmarks
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "EN-CIBSE-002",
                    StandardCode = "CIBSE Guide L",
                    Section = "Sustainability",
                    Title = "Energy Use Intensity Benchmarks (CIBSE)",
                    Description = "Good-practice energy use intensity targets per building type",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxEUI_Office_NV_kWhm2"] = 80.0,
                        ["MaxEUI_Office_AC_kWhm2"] = 200.0,
                        ["MaxEUI_Retail_kWhm2"] = 200.0,
                        ["MaxEUI_Hospital_kWhm2"] = 350.0
                    }
                });
            }
            catch { /* CIBSE not available */ }

            AddRequirements("Energy", reqs);
        }

        private void LoadVentilationRequirements()
        {
            var reqs = new List<StandardRequirement>
            {
                new StandardRequirement
                {
                    RequirementId = "VE-001",
                    StandardCode = "ASHRAE 62.1",
                    Section = "6.2",
                    Title = "Minimum Ventilation Rates",
                    Description = "Outdoor air requirements for acceptable IAQ",
                    Criteria = new Dictionary<string, object>
                    {
                        ["Office_PerPerson"] = 2.5,
                        ["Office_PerArea"] = 0.3,
                        ["Classroom_PerPerson"] = 5.0,
                        ["Classroom_PerArea"] = 0.6
                    }
                }
            };

            // CIBSE fresh air requirements
            try
            {
                var spaceTypes = new[] { "Office", "Conference", "Classroom", "Gymnasium", "Operating Theatre" };
                var criteria = new Dictionary<string, object>();
                foreach (var space in spaceTypes)
                {
                    try
                    {
                        var rate = CIBSE.CIBSEStandards.GuideA_EnvironmentalDesign.IndoorAirQuality
                            .GetFreshAirRequirement(space);
                        criteria[$"{space}_LsPerPerson"] = rate;
                    }
                    catch { /* space type not recognized */ }
                }

                if (criteria.Count > 0)
                {
                    reqs.Add(new StandardRequirement
                    {
                        RequirementId = "VE-CIBSE-001",
                        StandardCode = "CIBSE Guide A",
                        Section = "Indoor Air Quality",
                        Title = "Fresh Air Requirements (CIBSE)",
                        Description = "Minimum fresh air rates per person by space type per CIBSE Guide A",
                        Criteria = criteria
                    });
                }
            }
            catch { /* CIBSE not available */ }

            AddRequirements("Ventilation", reqs);
        }

        private void LoadStructuralRequirements()
        {
            var reqs = new List<StandardRequirement>
            {
                new StandardRequirement
                {
                    RequirementId = "ST-001",
                    StandardCode = "EN 1990",
                    Section = "4.1",
                    Title = "Load Combinations",
                    Description = "Combinations of actions for ultimate limit state",
                    Criteria = new Dictionary<string, object>
                    {
                        ["PermanentFactor"] = 1.35,
                        ["VariableFactor"] = 1.5,
                        ["CombinationFactor"] = 0.7
                    }
                },
                new StandardRequirement
                {
                    RequirementId = "ST-002",
                    StandardCode = "EN 1991-1-1",
                    Section = "6.3",
                    Title = "Imposed Loads on Floors",
                    Description = "Characteristic values of imposed floor loads",
                    Criteria = new Dictionary<string, object>
                    {
                        ["Residential_qk"] = 1.5,
                        ["Office_qk"] = 2.5,
                        ["Assembly_qk"] = 4.0,
                        ["Storage_qk"] = 7.5
                    }
                }
            };

            // EAS minimum live loads
            try
            {
                var occupancies = new Dictionary<string, EAS.EASStandards.OccupancyClass>
                {
                    ["Residential"] = EAS.EASStandards.OccupancyClass.Residential,
                    ["Educational"] = EAS.EASStandards.OccupancyClass.Educational,
                    ["Assembly"] = EAS.EASStandards.OccupancyClass.Assembly,
                    ["Industrial"] = EAS.EASStandards.OccupancyClass.Industrial,
                    ["Storage"] = EAS.EASStandards.OccupancyClass.Storage
                };

                var criteria = new Dictionary<string, object>();
                foreach (var (name, occ) in occupancies)
                {
                    var load = EAS.EASStandards.GetMinimumLiveLoad(occ);
                    criteria[$"{name}_kNm2"] = load;
                }

                reqs.Add(new StandardRequirement
                {
                    RequirementId = "ST-EAS-001",
                    StandardCode = "EAS",
                    Section = "Building Code",
                    Title = "Minimum Live Loads (East Africa)",
                    Description = "Minimum imposed floor loads per occupancy class per EAS",
                    Criteria = criteria
                });
            }
            catch { /* EAS not available */ }

            // EAS minimum ceiling heights
            try
            {
                var occupancies = new Dictionary<string, EAS.EASStandards.OccupancyClass>
                {
                    ["Residential"] = EAS.EASStandards.OccupancyClass.Residential,
                    ["Educational"] = EAS.EASStandards.OccupancyClass.Educational,
                    ["Assembly"] = EAS.EASStandards.OccupancyClass.Assembly,
                    ["Industrial"] = EAS.EASStandards.OccupancyClass.Industrial
                };

                var criteria = new Dictionary<string, object>();
                foreach (var (name, occ) in occupancies)
                {
                    var height = EAS.EASStandards.GetMinimumCeilingHeight(occ);
                    criteria[$"MinCeilingHeight_{name}_m"] = height;
                }

                reqs.Add(new StandardRequirement
                {
                    RequirementId = "ST-EAS-002",
                    StandardCode = "EAS",
                    Section = "Building Code",
                    Title = "Minimum Ceiling Heights (East Africa)",
                    Description = "Minimum ceiling heights per occupancy class per EAS",
                    Criteria = criteria
                });
            }
            catch { /* EAS not available */ }

            AddRequirements("Structural", reqs);
        }

        private void LoadElectricalRequirements()
        {
            var reqs = new List<StandardRequirement>();

            // CIBSE lighting requirements
            try
            {
                var spaces = new[] { "Corridor", "Staircase", "Office", "Retail", "Hospital" };
                var criteria = new Dictionary<string, object>();
                foreach (var space in spaces)
                {
                    try
                    {
                        var lux = CIBSE.CIBSEStandards.GuideK_Electricity.LightingDesign
                            .GetRecommendedIlluminance(space);
                        criteria[$"MinIlluminance_{space}_lux"] = lux;
                    }
                    catch { /* space not recognized */ }
                }

                if (criteria.Count > 0)
                {
                    reqs.Add(new StandardRequirement
                    {
                        RequirementId = "EL-CIBSE-001",
                        StandardCode = "CIBSE Guide K",
                        Section = "Lighting Design",
                        Title = "Minimum Illuminance Levels (CIBSE)",
                        Description = "Recommended illuminance by space type per CIBSE Guide K",
                        Criteria = criteria
                    });
                }
            }
            catch { /* CIBSE not available */ }

            // CIBSE load density
            try
            {
                var buildings = new[] { "Residential", "Office_NaturalVent", "Office_AirCon", "Retail", "Hospital" };
                var criteria = new Dictionary<string, object>();
                foreach (var btype in buildings)
                {
                    try
                    {
                        var density = CIBSE.CIBSEStandards.GuideK_Electricity.LoadEstimation
                            .GetLoadDensity(btype);
                        criteria[$"LoadDensity_{btype}_Wm2"] = density;
                    }
                    catch { /* building type not recognized */ }
                }

                if (criteria.Count > 0)
                {
                    reqs.Add(new StandardRequirement
                    {
                        RequirementId = "EL-CIBSE-002",
                        StandardCode = "CIBSE Guide K",
                        Section = "Load Estimation",
                        Title = "Electrical Load Density (CIBSE)",
                        Description = "Electrical load density by building type per CIBSE Guide K",
                        Criteria = criteria
                    });
                }
            }
            catch { /* CIBSE not available */ }

            // KEBS earthing requirements
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "EL-KEBS-001",
                    StandardCode = "KEBS",
                    Section = "Electrical",
                    Title = "Earth Resistance Limits (Kenya)",
                    Description = "Maximum earth resistance by building type per KEBS",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxResistance_Residential_ohm"] = KEBS.KEBSStandards.GetMaximumEarthResistance("Residential"),
                        ["MaxResistance_Commercial_ohm"] = KEBS.KEBSStandards.GetMaximumEarthResistance("Commercial"),
                        ["MaxResistance_Industrial_ohm"] = KEBS.KEBSStandards.GetMaximumEarthResistance("Industrial"),
                        ["MaxResistance_Critical_ohm"] = KEBS.KEBSStandards.GetMaximumEarthResistance("Critical")
                    }
                });
            }
            catch { /* KEBS not available */ }

            if (reqs.Count > 0)
                AddRequirements("Electrical", reqs);
        }

        private void LoadPlumbingRequirements()
        {
            var reqs = new List<StandardRequirement>();

            // EAS water efficiency
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "PL-EAS-001",
                    StandardCode = "EAS",
                    Section = "Plumbing Fixtures",
                    Title = "Maximum Flush Volumes (East Africa)",
                    Description = "Water efficiency requirements for plumbing fixtures per EAS",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MaxFlush_Standard_L"] = EAS.EASStandards.GetMaximumFlushVolume(
                            EAS.EASStandards.WaterEfficiencyRating.Standard),
                        ["MaxFlush_HighEfficiency_L"] = EAS.EASStandards.GetMaximumFlushVolume(
                            EAS.EASStandards.WaterEfficiencyRating.HighEfficiency),
                        ["MaxFlush_UltraHigh_L"] = EAS.EASStandards.GetMaximumFlushVolume(
                            EAS.EASStandards.WaterEfficiencyRating.UltraHighEfficiency)
                    }
                });
            }
            catch { /* EAS not available */ }

            if (reqs.Count > 0)
                AddRequirements("Plumbing", reqs);
        }

        private void LoadWaterStorageRequirements()
        {
            var reqs = new List<StandardRequirement>();

            // KEBS water storage
            try
            {
                reqs.Add(new StandardRequirement
                {
                    RequirementId = "WS-KEBS-001",
                    StandardCode = "KEBS",
                    Section = "Water Supply",
                    Title = "Minimum Water Storage (Kenya)",
                    Description = "Minimum water storage per person per day by occupancy type per KEBS",
                    Criteria = new Dictionary<string, object>
                    {
                        ["Residential_LPerPersonDay"] = KEBS.KEBSStandards.GetMinimumWaterStorage("Residential"),
                        ["Office_LPerPersonDay"] = KEBS.KEBSStandards.GetMinimumWaterStorage("Office"),
                        ["School_LPerPersonDay"] = KEBS.KEBSStandards.GetMinimumWaterStorage("School"),
                        ["Hospital_LPerPersonDay"] = KEBS.KEBSStandards.GetMinimumWaterStorage("Hospital"),
                        ["Hotel_LPerPersonDay"] = KEBS.KEBSStandards.GetMinimumWaterStorage("Hotel")
                    }
                });
            }
            catch { /* KEBS not available */ }

            if (reqs.Count > 0)
                AddRequirements("WaterStorage", reqs);
        }

        private void InitializeInterpretations()
        {
            AddInterpretation(new CodeInterpretation
            {
                InterpretationId = "INT-001",
                StandardCode = "IBC 2021",
                Topic = "Sprinkler Trade-offs",
                Contexts = new[]
                {
                    new InterpretationContext
                    {
                        Condition = "Building is fully sprinklered (NFPA 13)",
                        Implications = new[]
                        {
                            "Travel distance may be increased by 50%",
                            "Dead end corridors may be increased to 50 feet",
                            "Occupant load factor may be reduced"
                        },
                        References = new[] { "IBC 903.3", "IBC 1017.3" }
                    }
                }
            });

            AddInterpretation(new CodeInterpretation
            {
                InterpretationId = "INT-002",
                StandardCode = "ASHRAE 90.1",
                Topic = "Envelope Trade-offs",
                Contexts = new[]
                {
                    new InterpretationContext
                    {
                        Condition = "Using Energy Cost Budget method",
                        Implications = new[]
                        {
                            "Trade-offs allowed between envelope, HVAC, and lighting",
                            "Total building energy cost must not exceed baseline",
                            "Some mandatory provisions still apply"
                        },
                        References = new[] { "ASHRAE 90.1 Section 11" }
                    }
                }
            });

            AddInterpretation(new CodeInterpretation
            {
                InterpretationId = "INT-003",
                StandardCode = "EN 1992",
                Topic = "Concrete Cover Requirements",
                Contexts = new[]
                {
                    new InterpretationContext
                    {
                        Condition = "XC1 exposure class (indoor dry)",
                        Implications = new[]
                        {
                            "Minimum cover: 15mm + tolerance",
                            "Design working life: 50 years standard",
                            "Concrete class: minimum C20/25"
                        },
                        References = new[] { "EN 1992-1-1 4.4.1" }
                    },
                    new InterpretationContext
                    {
                        Condition = "XC4 exposure class (cyclic wet/dry)",
                        Implications = new[]
                        {
                            "Minimum cover: 35mm + tolerance",
                            "May require increased concrete strength",
                            "Consider surface protection"
                        },
                        References = new[] { "EN 1992-1-1 4.4.1" }
                    }
                }
            });

            // EAS / East African interpretation
            AddInterpretation(new CodeInterpretation
            {
                InterpretationId = "INT-EAS-001",
                StandardCode = "EAS",
                Topic = "Sprinkler Trade-offs",
                Contexts = new[]
                {
                    new InterpretationContext
                    {
                        Condition = "Building is sprinklered per EAS fire safety requirements",
                        Implications = new[]
                        {
                            "Maximum travel distance increased by 25% per EAS fire code",
                            "Fire resistance rating may be reduced for certain occupancy classes",
                            "Consider water supply reliability when specifying sprinklers in East Africa"
                        },
                        References = new[] { "EAS Fire Safety" }
                    }
                }
            });

            // CIBSE passive cooling interpretation
            AddInterpretation(new CodeInterpretation
            {
                InterpretationId = "INT-CIBSE-001",
                StandardCode = "CIBSE",
                Topic = "Passive Cooling",
                Contexts = new[]
                {
                    new InterpretationContext
                    {
                        Condition = "Tropical highland climate (East Africa)",
                        Implications = new[]
                        {
                            "Natural ventilation often sufficient per CIBSE Guide B assessment",
                            "Split AC recommended only for areas >100m² with unreliable power",
                            "Solar shading critical - consider brise-soleil and deep overhangs"
                        },
                        References = new[] { "CIBSE Guide B" }
                    }
                }
            });
        }

        private void LoadRegionalAdaptationsFromStandards()
        {
            // East African adaptations - enriched with data from UNBS, KEBS, EAS
            var eacAdaptations = new List<AdaptationRule>
            {
                new AdaptationRule
                {
                    Topic = "Climate Considerations",
                    Modifications = new[]
                    {
                        "Thermal insulation requirements reduced for tropical highland climate",
                        "Emphasis on natural ventilation and passive cooling",
                        "Solar shading requirements increased"
                    }
                },
                new AdaptationRule
                {
                    Topic = "Seismic Design",
                    Modifications = new[]
                    {
                        "Apply seismic provisions per regional hazard maps",
                        "Kampala: Seismic Zone 2 (moderate)",
                        "Nairobi: Seismic Zone 2-3 (moderate to high)"
                    }
                },
                new AdaptationRule
                {
                    Topic = "Power Reliability",
                    Modifications = new[]
                    {
                        "Backup power mandatory for essential facilities",
                        "Solar PV encouraged for all commercial buildings",
                        "Power factor correction required > 100 kVA"
                    }
                },
                new AdaptationRule
                {
                    Topic = "Water Storage",
                    Modifications = new[]
                    {
                        "Minimum 3-day water storage for residential",
                        "Minimum 7-day storage for healthcare facilities",
                        "Rainwater harvesting required for new buildings > 500m²"
                    }
                }
            };

            // Pull concrete and material data from EAS/KEBS
            try
            {
                var concreteInfo = new List<string>();
                var grades = new[] {
                    ("C20/25", KEBS.KEBSStandards.ConcreteGrade.C20_25),
                    ("C25/30", KEBS.KEBSStandards.ConcreteGrade.C25_30),
                    ("C30/37", KEBS.KEBSStandards.ConcreteGrade.C30_37)
                };
                foreach (var (name, grade) in grades)
                {
                    var (cement, sand, aggregate) = KEBS.KEBSStandards.GetMixProportions(grade);
                    concreteInfo.Add($"{name}: Mix ratio {cement}:{sand}:{aggregate} (cement:sand:aggregate)");
                }

                eacAdaptations.Add(new AdaptationRule
                {
                    Topic = "Concrete Mix Design",
                    Modifications = concreteInfo.ToArray()
                });
            }
            catch { /* KEBS not available */ }

            // UNBS wind speed data
            try
            {
                var windInfo = new List<string>();
                var regions = new[] { "Kampala", "Entebbe", "Mbarara", "Gulu", "Mbale" };
                foreach (var region in regions)
                {
                    try
                    {
                        var speed = UNBS.UNBSStandards.Structural.GetWindSpeed(region);
                        windInfo.Add($"{region}: Design wind speed {speed} m/s");
                    }
                    catch { /* region not found */ }
                }
                if (windInfo.Count > 0)
                {
                    eacAdaptations.Add(new AdaptationRule
                    {
                        Topic = "Wind Loading (Uganda)",
                        Modifications = windInfo.ToArray()
                    });
                }
            }
            catch { /* UNBS not available */ }

            // CIBSE carbon intensity data for Africa
            try
            {
                var carbonData = CIBSE.CIBSEStandards.GuideL_Sustainability.CarbonEmissions
                    .GetGridCarbonIntensity();
                var carbonInfo = new List<string>();
                var africanCountries = new[] { "Uganda", "Kenya", "Tanzania", "South Africa", "Ethiopia" };
                foreach (var country in africanCountries)
                {
                    if (carbonData.TryGetValue(country, out var intensity))
                        carbonInfo.Add($"{country} grid carbon intensity: {intensity} kgCO2/kWh");
                }
                if (carbonInfo.Count > 0)
                {
                    eacAdaptations.Add(new AdaptationRule
                    {
                        Topic = "Carbon Emissions",
                        Modifications = carbonInfo.ToArray()
                    });
                }
            }
            catch { /* CIBSE not available */ }

            AddRegionalAdaptation(new RegionalAdaptation
            {
                AdaptationId = "EAC-ADAPT",
                Region = "East Africa",
                BaseStandards = new[] { "BS", "Eurocodes", "EAS", "UNBS", "KEBS", "TBS", "RSB", "BBN", "SSBS" },
                Adaptations = eacAdaptations
            });

            // UK Building Regulations
            AddRegionalAdaptation(new RegionalAdaptation
            {
                AdaptationId = "UK-BR",
                Region = "United Kingdom",
                BaseStandards = new[] { "Eurocodes", "BS", "CIBSE" },
                Adaptations = new List<AdaptationRule>
                {
                    new AdaptationRule
                    {
                        Topic = "Part L - Conservation of Fuel and Power",
                        Modifications = new[]
                        {
                            "New buildings must achieve nearly zero energy",
                            "Fabric Energy Efficiency Standard (FEES) applies",
                            "Air tightness testing mandatory"
                        }
                    },
                    new AdaptationRule
                    {
                        Topic = "Part B - Fire Safety",
                        Modifications = new[]
                        {
                            "Combustible cladding ban above 18m",
                            "Sprinklers mandatory in new blocks of flats > 11m",
                            "Second staircase required in residential > 30m"
                        }
                    }
                }
            });

            // West Africa
            AddRegionalAdaptation(new RegionalAdaptation
            {
                AdaptationId = "ECOWAS-ADAPT",
                Region = "West Africa",
                BaseStandards = new[] { "ECOWAS" },
                Adaptations = new List<AdaptationRule>
                {
                    new AdaptationRule
                    {
                        Topic = "Climate Considerations",
                        Modifications = new[]
                        {
                            "Hot humid climate requires maximum natural ventilation",
                            "High rainfall intensity affects drainage and roof design",
                            "Termite protection mandatory for timber elements"
                        }
                    },
                    new AdaptationRule
                    {
                        Topic = "Infrastructure",
                        Modifications = new[]
                        {
                            "Backup power and water storage essential",
                            "Local material preference: laterite blocks, reinforced concrete",
                            "Road access and site drainage critical considerations"
                        }
                    }
                }
            });

            // South Africa
            AddRegionalAdaptation(new RegionalAdaptation
            {
                AdaptationId = "ZA-ADAPT",
                Region = "South Africa",
                BaseStandards = new[] { "SANS", "CIDB" },
                Adaptations = new List<AdaptationRule>
                {
                    new AdaptationRule
                    {
                        Topic = "SANS 10400 Building Regulations",
                        Modifications = new[]
                        {
                            "SANS 10400 Part XA: Energy efficiency mandatory",
                            "SANS 10400 Part S: Access for disabled persons",
                            "SANS 10400 Part T: Fire protection per SANS 10090"
                        }
                    },
                    new AdaptationRule
                    {
                        Topic = "Seismic",
                        Modifications = new[]
                        {
                            "SANS 10160-4 applies for seismic actions",
                            "Western Cape region has highest seismic hazard",
                            "Most of South Africa is low seismic risk"
                        }
                    }
                }
            });
        }

        private void InitializeCompliancePaths()
        {
            _compliancePaths.Add(new CompliancePath
            {
                PathId = "PRESCRIPTIVE",
                Name = "Prescriptive Compliance",
                Description = "Follow specific code requirements exactly",
                Advantages = new[]
                {
                    "Straightforward to verify",
                    "Lower documentation burden",
                    "Faster approval process"
                },
                Disadvantages = new[]
                {
                    "Less design flexibility",
                    "May result in over-design",
                    "Cannot optimize across systems"
                },
                ApplicableStandards = new[] { "IBC", "ASHRAE 90.1 Section 5-10", "KEBS", "UNBS" }
            });

            _compliancePaths.Add(new CompliancePath
            {
                PathId = "PERFORMANCE",
                Name = "Performance-Based Compliance",
                Description = "Demonstrate equivalent or better performance",
                Advantages = new[]
                {
                    "Maximum design flexibility",
                    "Can optimize for specific conditions",
                    "Allows innovative solutions"
                },
                Disadvantages = new[]
                {
                    "Requires detailed analysis",
                    "Higher documentation burden",
                    "May require peer review"
                },
                ApplicableStandards = new[] { "ASHRAE 90.1 Section 11", "IBC Chapter 14", "SANS 10400" }
            });

            _compliancePaths.Add(new CompliancePath
            {
                PathId = "TRADEOFF",
                Name = "Trade-off Compliance",
                Description = "Trade performance between building components",
                Advantages = new[]
                {
                    "Moderate flexibility",
                    "Easier than full performance path",
                    "Allows some optimization"
                },
                Disadvantages = new[]
                {
                    "Limited to specific trade-offs",
                    "Requires calculation",
                    "Not available for all requirements"
                },
                ApplicableStandards = new[] { "ASHRAE 90.1 Appendix C", "Green Building" }
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get applicable standards for a project context.
        /// Uses StandardsAPI.GetStandardsForLocation() for location-aware lookup.
        /// </summary>
        public ApplicableStandards GetApplicableStandards(ProjectContext context)
        {
            var applicable = new ApplicableStandards
            {
                Region = context.Region,
                BuildingType = context.BuildingType,
                Timestamp = DateTime.UtcNow
            };

            // Use StandardsAPI for location-based standard discovery
            var apiStandards = StandardsAPI.GetStandardsForLocation(context.Region);
            applicable.LocationStandards = apiStandards;

            // Get matching profiles from our enriched profile set
            var regionalProfiles = _profiles.Values
                .Where(p => p.ApplicableRegions.Any(r =>
                    r.Equals(context.Region, StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("Global", StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("International", StringComparison.OrdinalIgnoreCase) ||
                    context.Region.Contains(r, StringComparison.OrdinalIgnoreCase) ||
                    r.Contains(context.Region, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            applicable.Profiles = regionalProfiles;

            // Get discipline-specific standards
            applicable.StandardsByDiscipline = new Dictionary<Discipline, List<StandardReference>>();
            foreach (var discipline in Enum.GetValues<Discipline>())
            {
                var disciplineStandards = regionalProfiles
                    .Where(p => p.PrimaryDisciplines.Contains(discipline))
                    .SelectMany(p => p.Standards)
                    .Distinct()
                    .ToList();

                if (disciplineStandards.Any())
                    applicable.StandardsByDiscipline[discipline] = disciplineStandards;
            }

            // Get regional adaptations
            var adaptation = _regionalAdaptations.Values
                .FirstOrDefault(a =>
                    a.Region.Contains(context.Region, StringComparison.OrdinalIgnoreCase) ||
                    context.Region.Contains(a.Region, StringComparison.OrdinalIgnoreCase));
            applicable.RegionalAdaptation = adaptation;

            return applicable;
        }

        /// <summary>
        /// Get requirements for a specific topic.
        /// </summary>
        public RequirementSet GetRequirements(string topic, ProjectContext context = null)
        {
            var requirementSet = new RequirementSet
            {
                Topic = topic,
                Requirements = new List<StandardRequirement>()
            };

            if (_requirements.TryGetValue(topic, out var requirements))
            {
                var filtered = requirements;

                if (!string.IsNullOrEmpty(context?.OccupancyType))
                {
                    filtered = requirements.Where(r =>
                        r.ApplicableOccupancies == null ||
                        r.ApplicableOccupancies.Contains(context.OccupancyType))
                        .ToList();
                }

                // Filter by region if context provides one
                if (!string.IsNullOrEmpty(context?.Region))
                {
                    var locationStandards = StandardsAPI.GetStandardsForLocation(context.Region);
                    var locationCodes = new HashSet<string>(
                        locationStandards.Select(s => s.ShortName),
                        StringComparer.OrdinalIgnoreCase);

                    // Include requirements from applicable standards + international ones
                    filtered = filtered.Where(r =>
                        locationCodes.Any(c => r.StandardCode.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                        r.StandardCode.Contains("IBC", StringComparison.OrdinalIgnoreCase) ||
                        r.StandardCode.Contains("ADA", StringComparison.OrdinalIgnoreCase) ||
                        r.StandardCode.Contains("ASHRAE", StringComparison.OrdinalIgnoreCase) ||
                        r.StandardCode.Contains("EN ", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                requirementSet.Requirements = filtered;
            }

            requirementSet.Interpretations = _interpretations.Values
                .Where(i => i.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return requirementSet;
        }

        /// <summary>
        /// Check compliance against a specific requirement.
        /// Enhanced with numeric type coercion for robust comparison.
        /// </summary>
        public ComplianceCheck CheckCompliance(
            string requirementId,
            Dictionary<string, object> actualValues)
        {
            var check = new ComplianceCheck
            {
                RequirementId = requirementId,
                Timestamp = DateTime.UtcNow,
                Results = new List<ComplianceResult>()
            };

            var requirement = _requirements.Values
                .SelectMany(r => r)
                .FirstOrDefault(r => r.RequirementId == requirementId);

            if (requirement == null)
            {
                check.OverallResult = ComplianceStatus.Unknown;
                check.Message = $"Requirement {requirementId} not found";
                return check;
            }

            check.Requirement = requirement;

            foreach (var criterion in requirement.Criteria)
            {
                var result = new ComplianceResult
                {
                    Criterion = criterion.Key,
                    RequiredValue = criterion.Value
                };

                if (actualValues.TryGetValue(criterion.Key, out var actual))
                {
                    result.ActualValue = actual;
                    result.Status = EvaluateCriterion(criterion.Key, criterion.Value, actual);

                    if (result.Status == ComplianceStatus.NonCompliant)
                    {
                        result.Recommendation = GenerateRecommendation(criterion.Key, criterion.Value, actual);
                    }
                }
                else
                {
                    result.Status = ComplianceStatus.NotChecked;
                    result.Message = "Value not provided";
                }

                check.Results.Add(result);
            }

            if (check.Results.All(r => r.Status == ComplianceStatus.Compliant))
                check.OverallResult = ComplianceStatus.Compliant;
            else if (check.Results.Any(r => r.Status == ComplianceStatus.NonCompliant))
                check.OverallResult = ComplianceStatus.NonCompliant;
            else
                check.OverallResult = ComplianceStatus.Partial;

            return check;
        }

        /// <summary>
        /// Perform a live calculation using StandardsAPI.
        /// Bridges AI reasoning to real engineering calculations.
        /// </summary>
        public LiveCalculationResult PerformLiveCalculation(
            string calculationType,
            Dictionary<string, object> parameters)
        {
            return _calculationBridge.Calculate(calculationType, parameters);
        }

        /// <summary>
        /// Run multi-standard compliance check via StandardsAPI.
        /// Delegates to the real 32-standard compliance engine.
        /// </summary>
        public MultiStandardComplianceResult RunMultiStandardCompliance(
            string projectLocation,
            string buildingType,
            ProjectData projectData)
        {
            var apiReport = StandardsAPI.CheckMultiStandardCompliance(
                projectLocation, buildingType, projectData);

            return new MultiStandardComplianceResult
            {
                Success = apiReport.Success,
                ErrorMessage = apiReport.ErrorMessage,
                ProjectLocation = apiReport.ProjectLocation,
                BuildingType = apiReport.BuildingType,
                CheckedDate = apiReport.CheckedDate,
                ApplicableStandards = apiReport.ApplicableStandards,
                OverallCompliant = apiReport.OverallCompliant,
                CompliancePercentage = apiReport.CompliancePercentage,
                StandardResults = apiReport.Results?.Select(r => new StandardComplianceDetail
                {
                    StandardName = r.StandardName,
                    IsCompliant = r.IsCompliant,
                    CheckedItems = r.CheckedItems,
                    Issues = r.Issues
                }).ToList() ?? new List<StandardComplianceDetail>()
            };
        }

        /// <summary>
        /// Get intelligent interpretation of a code section.
        /// </summary>
        public CodeGuidance GetCodeGuidance(string standardCode, string topic, DesignContext context = null)
        {
            var guidance = new CodeGuidance
            {
                StandardCode = standardCode,
                Topic = topic,
                Timestamp = DateTime.UtcNow
            };

            guidance.Requirements = _requirements.Values
                .SelectMany(r => r)
                .Where(r => r.StandardCode.Contains(standardCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            guidance.Interpretations = _interpretations.Values
                .Where(i => i.StandardCode.Contains(standardCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            guidance.ContextualAdvice = GenerateContextualAdvice(standardCode, topic, context);

            guidance.CompliancePaths = _compliancePaths
                .Where(p => p.ApplicableStandards.Any(s =>
                    s.Contains(standardCode, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Add available live calculations for this standard
            guidance.AvailableCalculations = _calculationBridge.GetCalculationsForStandard(standardCode);

            return guidance;
        }

        /// <summary>
        /// Find equivalent requirements across standards.
        /// </summary>
        public EquivalenceMapping FindEquivalentRequirements(
            string sourceStandard,
            string targetStandard,
            string topic)
        {
            var mapping = new EquivalenceMapping
            {
                SourceStandard = sourceStandard,
                TargetStandard = targetStandard,
                Topic = topic,
                Mappings = new List<RequirementMapping>()
            };

            var equivalences = new Dictionary<string, List<(string Source, string Target, double Equivalence)>>
            {
                ["FireSafety"] = new List<(string, string, double)>
                {
                    ("IBC 1006", "BS 9999 Section 11", 0.85),
                    ("IBC 1017", "BS 9999 Section 15", 0.80),
                    ("IBC 1004", "EAS Fire Safety", 0.75),
                    ("NFPA 13", "EAS Sprinkler", 0.70)
                },
                ["Structural"] = new List<(string, string, double)>
                {
                    ("ASCE 7", "EN 1991", 0.90),
                    ("ACI 318", "EN 1992", 0.85),
                    ("ASCE 7", "EAS Building Code", 0.70),
                    ("EN 1991", "EAS Building Code", 0.75)
                },
                ["Energy"] = new List<(string, string, double)>
                {
                    ("ASHRAE 90.1", "UK Part L", 0.75),
                    ("ASHRAE 90.1", "EN 15603", 0.80),
                    ("ASHRAE 90.1", "CIBSE Guide L", 0.80),
                    ("CIBSE Guide L", "SANS 10400-XA", 0.70)
                },
                ["Electrical"] = new List<(string, string, double)>
                {
                    ("NEC 2023", "BS 7671", 0.80),
                    ("NEC 2023", "KEBS Electrical", 0.65),
                    ("BS 7671", "UNBS Electrical", 0.75)
                },
                ["Plumbing"] = new List<(string, string, double)>
                {
                    ("IPC 2021", "CIBSE Guide G", 0.80),
                    ("IPC 2021", "UNBS Plumbing", 0.65),
                    ("CIBSE Guide G", "KEBS Water Supply", 0.70)
                }
            };

            if (equivalences.TryGetValue(topic, out var topicEquivalences))
            {
                foreach (var (source, target, equiv) in topicEquivalences)
                {
                    if ((source.Contains(sourceStandard, StringComparison.OrdinalIgnoreCase) &&
                         target.Contains(targetStandard, StringComparison.OrdinalIgnoreCase)) ||
                        (source.Contains(targetStandard, StringComparison.OrdinalIgnoreCase) &&
                         target.Contains(sourceStandard, StringComparison.OrdinalIgnoreCase)))
                    {
                        mapping.Mappings.Add(new RequirementMapping
                        {
                            SourceSection = source,
                            TargetSection = target,
                            EquivalenceLevel = equiv,
                            Notes = equiv >= 0.80
                                ? "High equivalence - direct mapping with minor adjustments"
                                : "Partial equivalence - verify specific criteria and regional amendments"
                        });
                    }
                }
            }

            return mapping;
        }

        /// <summary>
        /// Get regional adaptation guidance.
        /// </summary>
        public RegionalGuidance GetRegionalGuidance(string region, string topic)
        {
            var guidance = new RegionalGuidance
            {
                Region = region,
                Topic = topic
            };

            var adaptation = _regionalAdaptations.Values
                .FirstOrDefault(a => a.Region.Contains(region, StringComparison.OrdinalIgnoreCase));

            if (adaptation != null)
            {
                guidance.Adaptation = adaptation;
                guidance.TopicSpecificRules = adaptation.Adaptations
                    .Where(a => a.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            guidance.PracticalAdvice = GenerateRegionalAdvice(region, topic);

            // Include applicable standards from StandardsAPI
            guidance.ApplicableStandards = StandardsAPI.GetStandardsForLocation(region);

            return guidance;
        }

        #endregion

        #region Private Methods

        private void AddProfile(StandardsProfile profile)
        {
            _profiles[profile.ProfileId] = profile;
        }

        private void AddRequirements(string topic, List<StandardRequirement> requirements)
        {
            if (_requirements.TryGetValue(topic, out var existing))
                existing.AddRange(requirements);
            else
                _requirements[topic] = requirements;
        }

        private void AddInterpretation(CodeInterpretation interpretation)
        {
            _interpretations[interpretation.InterpretationId] = interpretation;
        }

        private void AddRegionalAdaptation(RegionalAdaptation adaptation)
        {
            _regionalAdaptations[adaptation.AdaptationId] = adaptation;
        }

        private ComplianceStatus EvaluateCriterion(string criterion, object required, object actual)
        {
            // Coerce both values to double for numeric comparison
            if (TryToDouble(required, out var reqDouble) && TryToDouble(actual, out var actDouble))
            {
                if (criterion.StartsWith("Min"))
                    return actDouble >= reqDouble ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
                if (criterion.StartsWith("Max"))
                    return actDouble <= reqDouble ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;

                var tolerance = Math.Abs(reqDouble) * 0.05;
                return Math.Abs(actDouble - reqDouble) <= tolerance
                    ? ComplianceStatus.Compliant
                    : ComplianceStatus.NonCompliant;
            }

            return string.Equals(required?.ToString(), actual?.ToString(), StringComparison.OrdinalIgnoreCase)
                ? ComplianceStatus.Compliant
                : ComplianceStatus.NonCompliant;
        }

        private static bool TryToDouble(object value, out double result)
        {
            result = 0;
            if (value is double d) { result = d; return true; }
            if (value is int i) { result = i; return true; }
            if (value is float f) { result = f; return true; }
            if (value is long l) { result = l; return true; }
            if (value is decimal m) { result = (double)m; return true; }
            return double.TryParse(value?.ToString(), out result);
        }

        private string GenerateRecommendation(string criterion, object required, object actual)
        {
            if (criterion.StartsWith("Min"))
                return $"Increase {criterion.Replace("Min", "")} from {actual} to at least {required}";
            if (criterion.StartsWith("Max"))
                return $"Reduce {criterion.Replace("Max", "")} from {actual} to no more than {required}";
            return $"Adjust {criterion} from {actual} to {required}";
        }

        private List<string> GenerateContextualAdvice(string standardCode, string topic, DesignContext context)
        {
            var advice = new List<string>();

            if (standardCode.Contains("ASHRAE"))
            {
                advice.Add("Consider climate zone when selecting prescriptive requirements");
                advice.Add("Energy modeling may provide more flexibility than prescriptive path");
                if (context?.ClimateZone != null)
                    advice.Add($"Climate zone '{context.ClimateZone}' - verify envelope and HVAC requirements match");
            }

            if (standardCode.Contains("IBC"))
            {
                advice.Add("Check local amendments - many jurisdictions modify IBC requirements");
                if (context?.HasSprinklers == true)
                    advice.Add("Sprinklered building - significant code trade-offs available (travel distance, dead ends, occupant load)");
                else
                    advice.Add("Consider sprinkler systems for code trade-offs on travel distance and egress width");
            }

            if (standardCode.Contains("EN") || standardCode.Contains("Eurocode"))
            {
                advice.Add("Use appropriate National Annexes for region-specific parameters");
                advice.Add("Combination factors vary by load type and situation");
            }

            if (standardCode.Contains("CIBSE"))
            {
                advice.Add("CIBSE guides provide best-practice benchmarks for tropical and temperate climates");
                advice.Add("Guide B HVAC sizing accounts for African climate conditions");
            }

            if (standardCode.Contains("EAS") || standardCode.Contains("UNBS") || standardCode.Contains("KEBS"))
            {
                advice.Add("Verify local material availability when specifying per standards");
                advice.Add("Consider unreliable power/water supply in all designs");
                advice.Add("Regional standards adopt BS/Eurocodes with tropical climate modifications");
            }

            if (topic.Contains("Fire", StringComparison.OrdinalIgnoreCase))
                advice.Add("Early fire service consultation can identify compliance paths");

            if (topic.Contains("Energy", StringComparison.OrdinalIgnoreCase))
                advice.Add("Whole-building energy modeling provides maximum design flexibility");

            if (topic.Contains("Water", StringComparison.OrdinalIgnoreCase))
                advice.Add("Factor in water supply reliability when sizing storage and backup systems");

            return advice;
        }

        private List<string> GenerateRegionalAdvice(string region, string topic)
        {
            var advice = new List<string>();

            bool isEastAfrican = new[] { "EAC", "Uganda", "Kenya", "Tanzania", "Rwanda", "Burundi", "South Sudan", "East Africa" }
                .Any(r => region.Contains(r, StringComparison.OrdinalIgnoreCase));

            if (isEastAfrican)
            {
                advice.Add("Local material availability should influence structural system selection");
                advice.Add("Consider backup power and water storage in all designs");
                advice.Add("Passive cooling strategies are often more practical than HVAC");

                if (topic.Contains("Structure", StringComparison.OrdinalIgnoreCase) ||
                    topic.Contains("Structural", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add("Reinforced concrete frame is typically most cost-effective");
                    advice.Add("Steel may require import - consider lead time and cost");
                    advice.Add("Apply EAS minimum live loads per occupancy class");
                }

                if (topic.Contains("Electric", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add("Generator backup required for hospitals, data centers, telecoms");
                    advice.Add("Solar PV strongly recommended for commercial buildings");
                }

                if (topic.Contains("Water", StringComparison.OrdinalIgnoreCase) ||
                    topic.Contains("Plumb", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add("KEBS mandates rainwater harvesting in Nairobi for plots > 100m²");
                    advice.Add("Size water storage for 3-day minimum (residential) per UNBS");
                }
            }

            bool isWestAfrican = new[] { "ECOWAS", "Nigeria", "Ghana", "Senegal", "West Africa" }
                .Any(r => region.Contains(r, StringComparison.OrdinalIgnoreCase));

            if (isWestAfrican)
            {
                advice.Add("ECOWAS building standards apply across member states");
                advice.Add("Hot humid climate - maximize natural ventilation and shading");
                advice.Add("Termite protection mandatory for all timber elements");
            }

            bool isSouthAfrican = new[] { "South Africa", "ZA", "SANS" }
                .Any(r => region.Contains(r, StringComparison.OrdinalIgnoreCase));

            if (isSouthAfrican)
            {
                advice.Add("SANS 10400 is the primary building regulation - check all applicable parts");
                advice.Add("CIDB contractor grading applies to all public-sector projects");
                if (topic.Contains("Fire", StringComparison.OrdinalIgnoreCase))
                    advice.Add("SANS 10090 and SANS 10400-T govern fire protection requirements");
            }

            if (region.Contains("UK", StringComparison.OrdinalIgnoreCase))
            {
                advice.Add("Part L 2021 requires significant improvement over previous standards");
                advice.Add("Consider overheating risk under TM59 methodology");
                if (topic.Contains("Fire", StringComparison.OrdinalIgnoreCase))
                    advice.Add("Post-Grenfell regulations have significantly tightened cladding requirements");
            }

            return advice;
        }

        private static string NormalizeDiscipline(string discipline)
        {
            if (string.IsNullOrEmpty(discipline)) return "General";
            return discipline switch
            {
                "HVAC/Energy" => "HVAC",
                "Structural Steel" => "Structural",
                "Concrete" => "Structural",
                "Electrical Safety" => "Electrical",
                _ => discipline
            };
        }

        #endregion
    }

    #region Standards Calculation Bridge

    /// <summary>
    /// Bridges AI reasoning to live StandardsAPI calculations.
    /// Maps string-based calculation requests to typed StandardsAPI method calls.
    /// </summary>
    public class StandardsCalculationBridge
    {
        private static readonly Dictionary<string, string[]> _calculationsByStandard = new()
        {
            ["NEC"] = new[] { "CableSize", "CircuitBreaker", "GroundingSize" },
            ["CIBSE"] = new[] { "CoolingLoad", "Ventilation", "Lighting" },
            ["IPC"] = new[] { "PipeSize", "DrainageSize", "WaterHeater" },
            ["ASHRAE"] = new[] { "EnergyConsumption" },
            ["Eurocodes"] = new[] { "SteelBeam" },
            ["NFPA"] = new[] { "SprinklerSystem" }
        };

        public IReadOnlyList<string> AvailableCalculations =>
            _calculationsByStandard.SelectMany(kv => kv.Value.Select(v => $"{kv.Key}.{v}")).ToList();

        public List<string> GetCalculationsForStandard(string standardCode)
        {
            return _calculationsByStandard
                .Where(kv => standardCode.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                .SelectMany(kv => kv.Value.Select(v => $"{kv.Key}.{v}"))
                .ToList();
        }

        public LiveCalculationResult Calculate(string calculationType, Dictionary<string, object> parameters)
        {
            try
            {
                return calculationType switch
                {
                    "NEC.CableSize" => CalculateCableSize(parameters),
                    "NEC.CircuitBreaker" => CalculateCircuitBreaker(parameters),
                    "NEC.GroundingSize" => CalculateGroundingSize(parameters),
                    "CIBSE.CoolingLoad" => CalculateCoolingLoad(parameters),
                    "CIBSE.Ventilation" => CalculateVentilation(parameters),
                    "CIBSE.Lighting" => CalculateLighting(parameters),
                    "IPC.PipeSize" => CalculatePipeSize(parameters),
                    "IPC.DrainageSize" => CalculateDrainageSize(parameters),
                    "IPC.WaterHeater" => CalculateWaterHeater(parameters),
                    "ASHRAE.EnergyConsumption" => CalculateEnergy(parameters),
                    "Eurocodes.SteelBeam" => CalculateSteelBeam(parameters),
                    "NFPA.SprinklerSystem" => CalculateSprinkler(parameters),
                    _ => new LiveCalculationResult
                    {
                        Success = false,
                        ErrorMessage = $"Unknown calculation type: {calculationType}. Available: {string.Join(", ", AvailableCalculations)}"
                    }
                };
            }
            catch (Exception ex)
            {
                return new LiveCalculationResult
                {
                    Success = false,
                    ErrorMessage = $"Calculation error: {ex.Message}",
                    CalculationType = calculationType
                };
            }
        }

        private LiveCalculationResult CalculateCableSize(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateCableSize(
                GetDouble(p, "VoltageV"),
                GetDouble(p, "CurrentA"),
                GetDouble(p, "LengthM"),
                GetString(p, "ConductorType", "Copper"),
                GetString(p, "InsulationType", "THHN"),
                GetInt(p, "ConduitFill", 3),
                GetDouble(p, "AmbientTempC", 30));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "NEC.CableSize",
                StandardReference = result.NECReference,
                Results = new Dictionary<string, object>
                {
                    ["SizeAWG"] = result.SizeAWG,
                    ["SizeMM2"] = result.SizeMM2,
                    ["Ampacity"] = result.Ampacity,
                    ["VoltageDropPercent"] = result.VoltageDropPercent,
                    ["IsNECCompliant"] = result.IsNECCompliant,
                    ["DeratingFactor"] = result.DeratingFactor
                },
                Warnings = result.Warnings
            };
        }

        private LiveCalculationResult CalculateCircuitBreaker(Dictionary<string, object> p)
        {
            var result = StandardsAPI.VerifyCircuitBreaker(
                GetDouble(p, "LoadCurrentA"),
                GetDouble(p, "VoltageV"),
                GetString(p, "BreakerType", "Standard"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "NEC.CircuitBreaker",
                StandardReference = result.NECReference,
                Results = new Dictionary<string, object>
                {
                    ["RecommendedBreakerSizeA"] = result.RecommendedBreakerSizeA,
                    ["IsCompliant"] = result.IsCompliant
                }
            };
        }

        private LiveCalculationResult CalculateGroundingSize(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateGroundingSize(
                GetDouble(p, "ServiceCurrentA"),
                GetString(p, "ServiceEntryType"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "NEC.GroundingSize",
                StandardReference = result.NECReference,
                Results = new Dictionary<string, object>
                {
                    ["GroundingConductorSize"] = result.GroundingConductorSize,
                    ["IsCompliant"] = result.IsCompliant
                }
            };
        }

        private LiveCalculationResult CalculateCoolingLoad(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateCoolingLoad(
                GetDouble(p, "FloorAreaM2"),
                GetString(p, "BuildingType"),
                GetString(p, "ClimateZone"),
                GetDouble(p, "OccupantCount"),
                GetDouble(p, "EquipmentLoadW"),
                GetString(p, "Orientation", "N"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "CIBSE.CoolingLoad",
                StandardReference = result.CIBSEReference,
                Results = new Dictionary<string, object>
                {
                    ["CoolingLoadKW"] = result.CoolingLoadKW,
                    ["HeatingLoadKW"] = result.HeatingLoadKW,
                    ["VentilationLPS"] = result.VentilationLPS,
                    ["RecommendedSystem"] = result.RecommendedSystem
                }
            };
        }

        private LiveCalculationResult CalculateVentilation(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateVentilation(
                GetDouble(p, "FloorAreaM2"),
                GetDouble(p, "OccupantCount"),
                GetString(p, "SpaceType"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "CIBSE.Ventilation",
                StandardReference = result.CIBSEReference,
                Results = new Dictionary<string, object>
                {
                    ["FreshAirLPS"] = result.FreshAirLPS,
                    ["FreshAirM3H"] = result.FreshAirM3H,
                    ["AirChangesPerHour"] = result.AirChangesPerHour
                }
            };
        }

        private LiveCalculationResult CalculateLighting(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateLighting(
                GetDouble(p, "FloorAreaM2"),
                GetString(p, "SpaceType"),
                GetDouble(p, "CeilingHeightM"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "CIBSE.Lighting",
                StandardReference = result.CIBSEReference,
                Results = new Dictionary<string, object>
                {
                    ["IlluminanceLux"] = result.IlluminanceLux,
                    ["PowerDensityWM2"] = result.PowerDensityWM2
                }
            };
        }

        private LiveCalculationResult CalculatePipeSize(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculatePlumbingPipeSize(
                GetDouble(p, "FlowRateLPS"),
                GetDouble(p, "LengthM"),
                GetString(p, "PipeType"),
                GetString(p, "FluidType", "Water"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "IPC.PipeSize",
                StandardReference = result.IPCReference,
                Results = new Dictionary<string, object>
                {
                    ["PipeDiameterMM"] = result.PipeDiameterMM,
                    ["PipeDiameterInch"] = result.PipeDiameterInch,
                    ["VelocityMPS"] = result.VelocityMPS,
                    ["IsIPCCompliant"] = result.IsIPCCompliant
                }
            };
        }

        private LiveCalculationResult CalculateDrainageSize(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateDrainageSize(
                GetInt(p, "FixtureUnits"),
                GetDouble(p, "SlopePercent"),
                GetString(p, "PipeType", "PVC"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "IPC.DrainageSize",
                StandardReference = result.IPCReference,
                Results = new Dictionary<string, object>
                {
                    ["DrainDiameterMM"] = result.DrainDiameterMM,
                    ["DrainDiameterInch"] = result.DrainDiameterInch,
                    ["IsIPCCompliant"] = result.IsIPCCompliant
                }
            };
        }

        private LiveCalculationResult CalculateWaterHeater(Dictionary<string, object> p)
        {
            var result = StandardsAPI.CalculateWaterHeaterSize(
                GetInt(p, "OccupantCount"),
                GetString(p, "BuildingType"),
                GetDouble(p, "RecoveryRateGPH"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "IPC.WaterHeater",
                StandardReference = result.IPCReference,
                Results = new Dictionary<string, object>
                {
                    ["StorageCapacityGallons"] = result.StorageCapacityGallons,
                    ["StorageCapacityLiters"] = result.StorageCapacityLiters
                }
            };
        }

        private LiveCalculationResult CalculateEnergy(Dictionary<string, object> p)
        {
            var result = StandardsAPI.EstimateEnergyConsumption(
                GetDouble(p, "FloorAreaM2"),
                GetString(p, "BuildingType"),
                GetString(p, "ClimateZone"),
                GetString(p, "HVACSystem"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "ASHRAE.EnergyConsumption",
                StandardReference = result.ASHRAEReference,
                Results = new Dictionary<string, object>
                {
                    ["AnnualEnergyKWH"] = result.AnnualEnergyKWH,
                    ["EnergyPerAreaKWHM2"] = result.EnergyPerAreaKWHM2
                }
            };
        }

        private LiveCalculationResult CalculateSteelBeam(Dictionary<string, object> p)
        {
            var result = StandardsAPI.DesignSteelBeam(
                GetDouble(p, "SpanM"),
                GetDouble(p, "LoadKNM"),
                GetString(p, "SteelGrade"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "Eurocodes.SteelBeam",
                StandardReference = result.EurocodeReference,
                Results = new Dictionary<string, object>
                {
                    ["SectionSize"] = result.SectionSize,
                    ["IsAdequate"] = result.IsAdequate
                }
            };
        }

        private LiveCalculationResult CalculateSprinkler(Dictionary<string, object> p)
        {
            var result = StandardsAPI.DesignSprinklerSystem(
                GetDouble(p, "AreaM2"),
                GetString(p, "OccupancyType"),
                GetString(p, "HazardClass"));

            return new LiveCalculationResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                CalculationType = "NFPA.SprinklerSystem",
                StandardReference = result.NFPAReference,
                Results = new Dictionary<string, object>
                {
                    ["FlowRateGPM"] = result.FlowRateGPM,
                    ["NumberOfHeads"] = result.NumberOfHeads
                }
            };
        }

        // Parameter extraction helpers
        private static double GetDouble(Dictionary<string, object> p, string key, double defaultValue = 0)
        {
            if (!p.TryGetValue(key, out var val)) return defaultValue;
            if (val is double d) return d;
            if (val is int i) return i;
            if (val is float f) return f;
            if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int defaultValue = 0)
        {
            if (!p.TryGetValue(key, out var val)) return defaultValue;
            if (val is int i) return i;
            if (val is double d) return (int)d;
            if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }

        private static string GetString(Dictionary<string, object> p, string key, string defaultValue = "")
        {
            if (!p.TryGetValue(key, out var val)) return defaultValue;
            return val?.ToString() ?? defaultValue;
        }
    }

    #endregion

    #region Supporting Types

    public class StandardsProfile
    {
        public string ProfileId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public StandardReference[] Standards { get; set; }
        public string[] ApplicableRegions { get; set; }
        public Discipline[] PrimaryDisciplines { get; set; }
    }

    public class StandardReference
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
    }

    public class StandardRequirement
    {
        public string RequirementId { get; set; }
        public string StandardCode { get; set; }
        public string Section { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Criteria { get; set; }
        public string[] ApplicableOccupancies { get; set; }
    }

    public class CodeInterpretation
    {
        public string InterpretationId { get; set; }
        public string StandardCode { get; set; }
        public string Topic { get; set; }
        public InterpretationContext[] Contexts { get; set; }
    }

    public class InterpretationContext
    {
        public string Condition { get; set; }
        public string[] Implications { get; set; }
        public string[] References { get; set; }
    }

    public class RegionalAdaptation
    {
        public string AdaptationId { get; set; }
        public string Region { get; set; }
        public string[] BaseStandards { get; set; }
        public List<AdaptationRule> Adaptations { get; set; }
    }

    public class AdaptationRule
    {
        public string Topic { get; set; }
        public string[] Modifications { get; set; }
    }

    public class CompliancePath
    {
        public string PathId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] Advantages { get; set; }
        public string[] Disadvantages { get; set; }
        public string[] ApplicableStandards { get; set; }
    }

    public class ProjectContext
    {
        public string Region { get; set; }
        public string BuildingType { get; set; }
        public string OccupancyType { get; set; }
        public double BuildingArea { get; set; }
        public double BuildingHeight { get; set; }
    }

    public class DesignContext
    {
        public string Phase { get; set; }
        public string BuildingType { get; set; }
        public bool HasSprinklers { get; set; }
        public string ClimateZone { get; set; }
    }

    public class ApplicableStandards
    {
        public string Region { get; set; }
        public string BuildingType { get; set; }
        public DateTime Timestamp { get; set; }
        public List<StandardsProfile> Profiles { get; set; }
        public Dictionary<Discipline, List<StandardReference>> StandardsByDiscipline { get; set; }
        public RegionalAdaptation RegionalAdaptation { get; set; }
        /// <summary>
        /// Standards returned by StandardsAPI.GetStandardsForLocation().
        /// </summary>
        public List<StandardInfo> LocationStandards { get; set; }
    }

    public class RequirementSet
    {
        public string Topic { get; set; }
        public List<StandardRequirement> Requirements { get; set; }
        public List<CodeInterpretation> Interpretations { get; set; }
    }

    public class ComplianceCheck
    {
        public string RequirementId { get; set; }
        public StandardRequirement Requirement { get; set; }
        public DateTime Timestamp { get; set; }
        public ComplianceStatus OverallResult { get; set; }
        public string Message { get; set; }
        public List<ComplianceResult> Results { get; set; }
    }

    public class ComplianceResult
    {
        public string Criterion { get; set; }
        public object RequiredValue { get; set; }
        public object ActualValue { get; set; }
        public ComplianceStatus Status { get; set; }
        public string Message { get; set; }
        public string Recommendation { get; set; }
    }

    public class CodeGuidance
    {
        public string StandardCode { get; set; }
        public string Topic { get; set; }
        public DateTime Timestamp { get; set; }
        public List<StandardRequirement> Requirements { get; set; }
        public List<CodeInterpretation> Interpretations { get; set; }
        public List<string> ContextualAdvice { get; set; }
        public List<CompliancePath> CompliancePaths { get; set; }
        /// <summary>
        /// Live calculations available for this standard via StandardsAPI.
        /// </summary>
        public List<string> AvailableCalculations { get; set; }
    }

    public class EquivalenceMapping
    {
        public string SourceStandard { get; set; }
        public string TargetStandard { get; set; }
        public string Topic { get; set; }
        public List<RequirementMapping> Mappings { get; set; }
    }

    public class RequirementMapping
    {
        public string SourceSection { get; set; }
        public string TargetSection { get; set; }
        public double EquivalenceLevel { get; set; }
        public string Notes { get; set; }
    }

    public class RegionalGuidance
    {
        public string Region { get; set; }
        public string Topic { get; set; }
        public RegionalAdaptation Adaptation { get; set; }
        public List<AdaptationRule> TopicSpecificRules { get; set; }
        public List<string> PracticalAdvice { get; set; }
        /// <summary>
        /// Standards applicable to this region from StandardsAPI.
        /// </summary>
        public List<StandardInfo> ApplicableStandards { get; set; }
    }

    /// <summary>
    /// Result from a live StandardsAPI calculation.
    /// </summary>
    public class LiveCalculationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string CalculationType { get; set; }
        public string StandardReference { get; set; }
        public Dictionary<string, object> Results { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Result from multi-standard compliance check via StandardsAPI.
    /// </summary>
    public class MultiStandardComplianceResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string ProjectLocation { get; set; }
        public string BuildingType { get; set; }
        public DateTime CheckedDate { get; set; }
        public List<string> ApplicableStandards { get; set; }
        public bool OverallCompliant { get; set; }
        public double CompliancePercentage { get; set; }
        public List<StandardComplianceDetail> StandardResults { get; set; }
    }

    public class StandardComplianceDetail
    {
        public string StandardName { get; set; }
        public bool IsCompliant { get; set; }
        public List<string> CheckedItems { get; set; }
        public List<string> Issues { get; set; }
    }

    public enum Discipline
    {
        Architecture,
        Structural,
        HVAC,
        Electrical,
        Plumbing,
        FireSafety,
        Energy,
        Lighting,
        Acoustics
    }

    public enum ComplianceStatus
    {
        Compliant,
        NonCompliant,
        Partial,
        NotChecked,
        Unknown
    }

    #endregion
}
