// =========================================================================
// StingBIM.AI.Knowledge - Standards Integration Module
// Connects AI reasoning to StingBIM.Standards library
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Knowledge.Standards
{
    /// <summary>
    /// Integrates building standards from StingBIM.Standards with AI reasoning.
    /// Provides intelligent code interpretation and compliance guidance.
    /// </summary>
    public class StandardsIntegration
    {
        private readonly Dictionary<string, StandardsProfile> _profiles;
        private readonly Dictionary<string, List<StandardRequirement>> _requirements;
        private readonly Dictionary<string, CodeInterpretation> _interpretations;
        private readonly Dictionary<string, RegionalAdaptation> _regionalAdaptations;
        private readonly List<CompliancePath> _compliancePaths;

        public StandardsIntegration()
        {
            _profiles = new Dictionary<string, StandardsProfile>();
            _requirements = new Dictionary<string, List<StandardRequirement>>();
            _interpretations = new Dictionary<string, CodeInterpretation>();
            _regionalAdaptations = new Dictionary<string, RegionalAdaptation>();
            _compliancePaths = new List<CompliancePath>();

            InitializeProfiles();
            InitializeRequirements();
            InitializeInterpretations();
            InitializeRegionalAdaptations();
            InitializeCompliancePaths();
        }

        #region Initialization

        private void InitializeProfiles()
        {
            // ASHRAE Profile
            AddProfile(new StandardsProfile
            {
                ProfileId = "ASHRAE",
                Name = "ASHRAE Standards",
                Description = "American Society of Heating, Refrigerating and Air-Conditioning Engineers",
                Standards = new[]
                {
                    new StandardReference { Code = "ASHRAE 90.1", Title = "Energy Standard for Buildings", Version = "2022" },
                    new StandardReference { Code = "ASHRAE 62.1", Title = "Ventilation for Acceptable Indoor Air Quality", Version = "2022" },
                    new StandardReference { Code = "ASHRAE 55", Title = "Thermal Environmental Conditions", Version = "2020" }
                },
                ApplicableRegions = new[] { "US", "International" },
                PrimaryDisciplines = new[] { Discipline.HVAC, Discipline.Energy }
            });

            // Eurocodes Profile
            AddProfile(new StandardsProfile
            {
                ProfileId = "Eurocodes",
                Name = "European Structural Standards",
                Description = "European standards for structural design",
                Standards = new[]
                {
                    new StandardReference { Code = "EN 1990", Title = "Basis of Structural Design", Version = "2002" },
                    new StandardReference { Code = "EN 1991", Title = "Actions on Structures", Version = "2002" },
                    new StandardReference { Code = "EN 1992", Title = "Design of Concrete Structures", Version = "2004" },
                    new StandardReference { Code = "EN 1993", Title = "Design of Steel Structures", Version = "2005" }
                },
                ApplicableRegions = new[] { "EU", "UK", "International" },
                PrimaryDisciplines = new[] { Discipline.Structural }
            });

            // IBC Profile
            AddProfile(new StandardsProfile
            {
                ProfileId = "IBC",
                Name = "International Building Code",
                Description = "Model building code for US jurisdictions",
                Standards = new[]
                {
                    new StandardReference { Code = "IBC 2021", Title = "International Building Code", Version = "2021" },
                    new StandardReference { Code = "IFC 2021", Title = "International Fire Code", Version = "2021" },
                    new StandardReference { Code = "IMC 2021", Title = "International Mechanical Code", Version = "2021" }
                },
                ApplicableRegions = new[] { "US" },
                PrimaryDisciplines = new[] { Discipline.Architecture, Discipline.FireSafety }
            });

            // British Standards Profile
            AddProfile(new StandardsProfile
            {
                ProfileId = "BS",
                Name = "British Standards",
                Description = "UK building and construction standards",
                Standards = new[]
                {
                    new StandardReference { Code = "BS 7671", Title = "Requirements for Electrical Installations", Version = "2018" },
                    new StandardReference { Code = "BS 6399", Title = "Loading for Buildings", Version = "1996" },
                    new StandardReference { Code = "BS 8110", Title = "Structural Use of Concrete", Version = "1997" }
                },
                ApplicableRegions = new[] { "UK", "EAC", "Commonwealth" },
                PrimaryDisciplines = new[] { Discipline.Electrical, Discipline.Structural }
            });

            // EAC Profile
            AddProfile(new StandardsProfile
            {
                ProfileId = "EAC",
                Name = "East African Community Standards",
                Description = "Regional standards for East Africa",
                Standards = new[]
                {
                    new StandardReference { Code = "EAS 1000", Title = "Building Code", Version = "2020" },
                    new StandardReference { Code = "UNBS", Title = "Uganda National Bureau of Standards", Version = "2022" },
                    new StandardReference { Code = "KEBS", Title = "Kenya Bureau of Standards", Version = "2022" }
                },
                ApplicableRegions = new[] { "Uganda", "Kenya", "Tanzania", "Rwanda", "Burundi" },
                PrimaryDisciplines = new[] { Discipline.Architecture, Discipline.Structural }
            });

            // CIBSE Profile
            AddProfile(new StandardsProfile
            {
                ProfileId = "CIBSE",
                Name = "CIBSE Guides",
                Description = "Chartered Institution of Building Services Engineers",
                Standards = new[]
                {
                    new StandardReference { Code = "CIBSE Guide A", Title = "Environmental Design", Version = "2021" },
                    new StandardReference { Code = "CIBSE Guide B", Title = "Heating, Ventilating, Air Conditioning", Version = "2016" },
                    new StandardReference { Code = "CIBSE Guide C", Title = "Reference Data", Version = "2007" }
                },
                ApplicableRegions = new[] { "UK", "International", "EAC" },
                PrimaryDisciplines = new[] { Discipline.HVAC, Discipline.Electrical, Discipline.Lighting }
            });
        }

        private void InitializeRequirements()
        {
            // Fire Safety Requirements
            AddRequirements("FireSafety", new List<StandardRequirement>
            {
                new StandardRequirement
                {
                    RequirementId = "FS-001",
                    StandardCode = "IBC 2021",
                    Section = "1006",
                    Title = "Means of Egress Width",
                    Description = "Minimum width of means of egress components",
                    Criteria = new Dictionary<string, object>
                    {
                        ["MinCorridorWidth"] = 1118, // mm (44 inches)
                        ["MinStairWidth"] = 1118,
                        ["MinDoorWidth"] = 813 // 32 inches
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
                        ["MaxDeadEndLength"] = 6100, // mm (20 ft) sprinklered
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
                        ["Assembly_Standing"] = 0.46, // m² per person
                        ["Assembly_Seated"] = 0.65,
                        ["Business"] = 9.3,
                        ["Educational"] = 1.9,
                        ["Residential"] = 18.6
                    }
                }
            });

            // Accessibility Requirements
            AddRequirements("Accessibility", new List<StandardRequirement>
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
                        ["MinClearWidth"] = 915, // mm (36 inches)
                        ["MinPassingWidth"] = 1525, // mm (60 inches)
                        ["MaxSlope"] = 0.05, // 1:20
                        ["MaxRampSlope"] = 0.083 // 1:12
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
                        ["MinTurningSpace"] = 1525, // mm diameter
                        ["MinClearFloorAtWC"] = 1525, // mm
                        ["GrabBarHeight"] = 838 // mm (33 inches)
                    }
                }
            });

            // Energy Requirements
            AddRequirements("Energy", new List<StandardRequirement>
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
                        ["MaxWallUValue_4A"] = 0.48, // W/m²K Climate Zone 4A
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
            });

            // Ventilation Requirements
            AddRequirements("Ventilation", new List<StandardRequirement>
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
                        ["Office_PerPerson"] = 2.5, // L/s per person
                        ["Office_PerArea"] = 0.3, // L/s per m²
                        ["Classroom_PerPerson"] = 5.0,
                        ["Classroom_PerArea"] = 0.6
                    }
                }
            });

            // Structural Requirements
            AddRequirements("Structural", new List<StandardRequirement>
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
                        ["Residential_qk"] = 1.5, // kN/m²
                        ["Office_qk"] = 2.5,
                        ["Assembly_qk"] = 4.0,
                        ["Storage_qk"] = 7.5
                    }
                }
            });
        }

        private void InitializeInterpretations()
        {
            // Context-aware interpretations
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
        }

        private void InitializeRegionalAdaptations()
        {
            // East African adaptations
            AddRegionalAdaptation(new RegionalAdaptation
            {
                AdaptationId = "EAC-ADAPT",
                Region = "East Africa",
                BaseStandards = new[] { "BS", "Eurocodes" },
                Adaptations = new List<AdaptationRule>
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
                }
            });

            // UK Building Regulations adaptations
            AddRegionalAdaptation(new RegionalAdaptation
            {
                AdaptationId = "UK-BR",
                Region = "United Kingdom",
                BaseStandards = new[] { "Eurocodes", "BS" },
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
        }

        private void InitializeCompliancePaths()
        {
            // Prescriptive path
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
                ApplicableStandards = new[] { "IBC", "ASHRAE 90.1 Section 5-10" }
            });

            // Performance path
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
                ApplicableStandards = new[] { "ASHRAE 90.1 Section 11", "IBC Chapter 14" }
            });

            // Trade-off path
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
                ApplicableStandards = new[] { "ASHRAE 90.1 Appendix C" }
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get applicable standards for a project context.
        /// </summary>
        public ApplicableStandards GetApplicableStandards(ProjectContext context)
        {
            var applicable = new ApplicableStandards
            {
                Region = context.Region,
                BuildingType = context.BuildingType,
                Timestamp = DateTime.UtcNow
            };

            // Get profiles for region
            var regionalProfiles = _profiles.Values
                .Where(p => p.ApplicableRegions.Contains(context.Region) ||
                           p.ApplicableRegions.Contains("International"))
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
            if (_regionalAdaptations.TryGetValue(context.Region, out var adaptation))
            {
                applicable.RegionalAdaptation = adaptation;
            }
            else
            {
                var found = _regionalAdaptations.Values.FirstOrDefault(a =>
                    a.Region.Contains(context.Region, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    applicable.RegionalAdaptation = found;
                }
            }

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

                // Filter by occupancy if specified
                if (!string.IsNullOrEmpty(context?.OccupancyType))
                {
                    filtered = requirements.Where(r =>
                        r.ApplicableOccupancies == null ||
                        r.ApplicableOccupancies.Contains(context.OccupancyType))
                        .ToList();
                }

                requirementSet.Requirements = filtered;
            }

            // Add interpretations
            requirementSet.Interpretations = _interpretations.Values
                .Where(i => i.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return requirementSet;
        }

        /// <summary>
        /// Check compliance against a specific requirement.
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

            // Find the requirement
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

            // Check each criterion
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

            // Determine overall result
            if (check.Results.All(r => r.Status == ComplianceStatus.Compliant))
                check.OverallResult = ComplianceStatus.Compliant;
            else if (check.Results.Any(r => r.Status == ComplianceStatus.NonCompliant))
                check.OverallResult = ComplianceStatus.NonCompliant;
            else
                check.OverallResult = ComplianceStatus.Partial;

            return check;
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

            // Get relevant requirements
            var relevantRequirements = _requirements.Values
                .SelectMany(r => r)
                .Where(r => r.StandardCode.Contains(standardCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            guidance.Requirements = relevantRequirements;

            // Get interpretations
            var interpretations = _interpretations.Values
                .Where(i => i.StandardCode.Contains(standardCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            guidance.Interpretations = interpretations;

            // Generate contextual advice
            guidance.ContextualAdvice = GenerateContextualAdvice(standardCode, topic, context);

            // Suggest compliance paths
            guidance.CompliancePaths = _compliancePaths
                .Where(p => p.ApplicableStandards.Any(s =>
                    s.Contains(standardCode, StringComparison.OrdinalIgnoreCase)))
                .ToList();

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

            // Known equivalences
            var equivalences = new Dictionary<string, List<(string Source, string Target, double Equivalence)>>
            {
                ["FireSafety"] = new List<(string, string, double)>
                {
                    ("IBC 1006", "BS 9999 Section 11", 0.85),
                    ("IBC 1017", "BS 9999 Section 15", 0.80)
                },
                ["Structural"] = new List<(string, string, double)>
                {
                    ("ASCE 7", "EN 1991", 0.90),
                    ("ACI 318", "EN 1992", 0.85)
                },
                ["Energy"] = new List<(string, string, double)>
                {
                    ("ASHRAE 90.1", "UK Part L", 0.75),
                    ("ASHRAE 90.1", "EN 15603", 0.80)
                }
            };

            if (equivalences.TryGetValue(topic, out var topicEquivalences))
            {
                foreach (var (source, target, equiv) in topicEquivalences)
                {
                    if (source.Contains(sourceStandard, StringComparison.OrdinalIgnoreCase) ||
                        target.Contains(targetStandard, StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.Mappings.Add(new RequirementMapping
                        {
                            SourceSection = source,
                            TargetSection = target,
                            EquivalenceLevel = equiv,
                            Notes = "Partial equivalence - verify specific criteria"
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

            // Find applicable adaptation
            var adaptation = _regionalAdaptations.Values
                .FirstOrDefault(a => a.Region.Contains(region, StringComparison.OrdinalIgnoreCase));

            if (adaptation != null)
            {
                guidance.Adaptation = adaptation;
                guidance.TopicSpecificRules = adaptation.Adaptations
                    .Where(a => a.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Generate practical advice
            guidance.PracticalAdvice = GenerateRegionalAdvice(region, topic);

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
            // Handle numeric comparisons
            if (required is double reqDouble && actual is double actDouble)
            {
                // Check if criterion is a minimum or maximum
                if (criterion.StartsWith("Min"))
                    return actDouble >= reqDouble ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
                if (criterion.StartsWith("Max"))
                    return actDouble <= reqDouble ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;

                // Default: must match within 5% tolerance
                var tolerance = Math.Abs(reqDouble) * 0.05;
                return Math.Abs(actDouble - reqDouble) <= tolerance
                    ? ComplianceStatus.Compliant
                    : ComplianceStatus.NonCompliant;
            }

            // Handle integer comparisons
            if (required is int reqInt && actual is int actInt)
            {
                if (criterion.StartsWith("Min"))
                    return actInt >= reqInt ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
                if (criterion.StartsWith("Max"))
                    return actInt <= reqInt ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
                return actInt == reqInt ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
            }

            // String equality
            return required.ToString() == actual.ToString()
                ? ComplianceStatus.Compliant
                : ComplianceStatus.NonCompliant;
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

            // Standard-specific advice
            if (standardCode.Contains("ASHRAE"))
            {
                advice.Add("Consider climate zone when selecting prescriptive requirements");
                advice.Add("Energy modeling may provide more flexibility than prescriptive path");
            }

            if (standardCode.Contains("IBC"))
            {
                advice.Add("Check local amendments - many jurisdictions modify IBC requirements");
                advice.Add("Sprinkler systems can provide significant code trade-offs");
            }

            if (standardCode.Contains("EN") || standardCode.Contains("Eurocode"))
            {
                advice.Add("Use appropriate National Annexes for UK-specific parameters");
                advice.Add("Combination factors vary by load type and situation");
            }

            // Topic-specific advice
            if (topic.Contains("Fire", StringComparison.OrdinalIgnoreCase))
            {
                advice.Add("Early fire service consultation can identify compliance paths");
            }

            if (topic.Contains("Energy", StringComparison.OrdinalIgnoreCase))
            {
                advice.Add("Whole-building energy modeling provides maximum design flexibility");
            }

            return advice;
        }

        private List<string> GenerateRegionalAdvice(string region, string topic)
        {
            var advice = new List<string>();

            if (region.Contains("EAC") || region.Contains("Uganda") || region.Contains("Kenya"))
            {
                advice.Add("Local material availability should influence structural system selection");
                advice.Add("Consider backup power and water storage in all designs");
                advice.Add("Passive cooling strategies are often more practical than HVAC");

                if (topic.Contains("Structure", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add("Reinforced concrete frame is typically most cost-effective");
                    advice.Add("Steel may require import - consider lead time and cost");
                }
            }

            if (region.Contains("UK"))
            {
                advice.Add("Part L 2021 requires significant improvement over previous standards");
                advice.Add("Consider overheating risk under TM59 methodology");

                if (topic.Contains("Fire", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add("Post-Grenfell regulations have significantly tightened cladding requirements");
                }
            }

            return advice;
        }

        #endregion
    }

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
