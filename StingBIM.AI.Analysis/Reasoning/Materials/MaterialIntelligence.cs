// =========================================================================
// StingBIM.AI.Reasoning - Material Intelligence System
// Smart material recommendations with performance analysis
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Reasoning.Materials
{
    /// <summary>
    /// Intelligent material selection and recommendation engine.
    /// Integrates with StingBIM.Data.Materials for comprehensive analysis.
    /// </summary>
    public class MaterialIntelligence
    {
        private readonly Dictionary<string, MaterialProfile> _materialProfiles;
        private readonly Dictionary<string, List<MaterialSubstitution>> _substitutions;
        private readonly Dictionary<string, MaterialPerformanceData> _performanceData;
        private readonly Dictionary<string, RegionalAvailability> _regionalData;
        private readonly List<MaterialRule> _selectionRules;

        public MaterialIntelligence()
        {
            _materialProfiles = new Dictionary<string, MaterialProfile>();
            _substitutions = new Dictionary<string, List<MaterialSubstitution>>();
            _performanceData = new Dictionary<string, MaterialPerformanceData>();
            _regionalData = new Dictionary<string, RegionalAvailability>();
            _selectionRules = new List<MaterialRule>();

            InitializeMaterialProfiles();
            InitializeSubstitutions();
            InitializePerformanceData();
            InitializeRegionalData();
            InitializeSelectionRules();
        }

        #region Initialization

        private void InitializeMaterialProfiles()
        {
            // Concrete profiles
            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "CONC-C30",
                Name = "Concrete C30/37",
                Category = MaterialCategory.Structural,
                Properties = new Dictionary<string, double>
                {
                    ["CompressiveStrength"] = 30, // MPa
                    ["Density"] = 2400, // kg/m³
                    ["ThermalConductivity"] = 1.7, // W/mK
                    ["EmbodiedCarbon"] = 0.11, // kgCO2/kg
                    ["FireResistance"] = 240 // minutes
                },
                Applications = new[] { "Foundations", "Columns", "Beams", "Slabs" },
                Sustainability = SustainabilityRating.Medium,
                CostTier = CostTier.Medium
            });

            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "CONC-C40",
                Name = "Concrete C40/50",
                Category = MaterialCategory.Structural,
                Properties = new Dictionary<string, double>
                {
                    ["CompressiveStrength"] = 40,
                    ["Density"] = 2450,
                    ["ThermalConductivity"] = 1.8,
                    ["EmbodiedCarbon"] = 0.13,
                    ["FireResistance"] = 240
                },
                Applications = new[] { "High-rise columns", "Transfer beams", "Post-tensioned slabs" },
                Sustainability = SustainabilityRating.Medium,
                CostTier = CostTier.MediumHigh
            });

            // Steel profiles
            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "STEEL-S355",
                Name = "Structural Steel S355",
                Category = MaterialCategory.Structural,
                Properties = new Dictionary<string, double>
                {
                    ["YieldStrength"] = 355, // MPa
                    ["Density"] = 7850,
                    ["ThermalConductivity"] = 50,
                    ["EmbodiedCarbon"] = 1.55,
                    ["FireResistance"] = 15 // unprotected
                },
                Applications = new[] { "Beams", "Columns", "Trusses", "Connections" },
                Sustainability = SustainabilityRating.Low,
                CostTier = CostTier.High
            });

            // Masonry profiles
            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "BRICK-CLAY",
                Name = "Clay Brick",
                Category = MaterialCategory.Masonry,
                Properties = new Dictionary<string, double>
                {
                    ["CompressiveStrength"] = 20,
                    ["Density"] = 1800,
                    ["ThermalConductivity"] = 0.77,
                    ["EmbodiedCarbon"] = 0.22,
                    ["FireResistance"] = 120
                },
                Applications = new[] { "External walls", "Internal walls", "Facades" },
                Sustainability = SustainabilityRating.Medium,
                CostTier = CostTier.Medium
            });

            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "BLOCK-CONCRETE",
                Name = "Concrete Block",
                Category = MaterialCategory.Masonry,
                Properties = new Dictionary<string, double>
                {
                    ["CompressiveStrength"] = 7.3,
                    ["Density"] = 1400,
                    ["ThermalConductivity"] = 0.51,
                    ["EmbodiedCarbon"] = 0.08,
                    ["FireResistance"] = 120
                },
                Applications = new[] { "Load-bearing walls", "Partition walls", "Foundations" },
                Sustainability = SustainabilityRating.MediumHigh,
                CostTier = CostTier.Low
            });

            // Insulation profiles
            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "INS-MINERALWOOL",
                Name = "Mineral Wool Insulation",
                Category = MaterialCategory.Insulation,
                Properties = new Dictionary<string, double>
                {
                    ["ThermalConductivity"] = 0.035,
                    ["Density"] = 30,
                    ["EmbodiedCarbon"] = 1.2,
                    ["FireResistance"] = 60,
                    ["RValue"] = 2.86 // per 100mm
                },
                Applications = new[] { "Wall cavities", "Roof insulation", "Floor insulation" },
                Sustainability = SustainabilityRating.Medium,
                CostTier = CostTier.Medium
            });

            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "INS-PIR",
                Name = "PIR Insulation Board",
                Category = MaterialCategory.Insulation,
                Properties = new Dictionary<string, double>
                {
                    ["ThermalConductivity"] = 0.022,
                    ["Density"] = 32,
                    ["EmbodiedCarbon"] = 3.4,
                    ["FireResistance"] = 30,
                    ["RValue"] = 4.55
                },
                Applications = new[] { "Flat roofs", "Wall insulation", "Floor insulation" },
                Sustainability = SustainabilityRating.Low,
                CostTier = CostTier.High
            });

            // Timber profiles
            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "TIMBER-SOFTWOOD",
                Name = "Softwood Timber C24",
                Category = MaterialCategory.Timber,
                Properties = new Dictionary<string, double>
                {
                    ["BendingStrength"] = 24, // MPa
                    ["Density"] = 420,
                    ["ThermalConductivity"] = 0.13,
                    ["EmbodiedCarbon"] = -1.0, // Carbon negative
                    ["FireResistance"] = 30
                },
                Applications = new[] { "Roof trusses", "Floor joists", "Stud walls" },
                Sustainability = SustainabilityRating.High,
                CostTier = CostTier.Medium
            });

            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "TIMBER-CLT",
                Name = "Cross-Laminated Timber",
                Category = MaterialCategory.Timber,
                Properties = new Dictionary<string, double>
                {
                    ["BendingStrength"] = 24,
                    ["Density"] = 480,
                    ["ThermalConductivity"] = 0.12,
                    ["EmbodiedCarbon"] = -0.7,
                    ["FireResistance"] = 60
                },
                Applications = new[] { "Floor panels", "Wall panels", "Roof panels" },
                Sustainability = SustainabilityRating.VeryHigh,
                CostTier = CostTier.High
            });

            // Glass profiles
            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "GLASS-DGU",
                Name = "Double Glazed Unit",
                Category = MaterialCategory.Glass,
                Properties = new Dictionary<string, double>
                {
                    ["UValue"] = 1.1, // W/m²K
                    ["SHGC"] = 0.63, // Solar heat gain coefficient
                    ["VLT"] = 0.8, // Visible light transmittance
                    ["EmbodiedCarbon"] = 1.44,
                    ["Density"] = 2500
                },
                Applications = new[] { "Windows", "Curtain walls", "Skylights" },
                Sustainability = SustainabilityRating.Medium,
                CostTier = CostTier.High
            });

            AddMaterialProfile(new MaterialProfile
            {
                MaterialId = "GLASS-TGU",
                Name = "Triple Glazed Unit",
                Category = MaterialCategory.Glass,
                Properties = new Dictionary<string, double>
                {
                    ["UValue"] = 0.6,
                    ["SHGC"] = 0.5,
                    ["VLT"] = 0.7,
                    ["EmbodiedCarbon"] = 2.0,
                    ["Density"] = 2500
                },
                Applications = new[] { "High-performance facades", "Passive house windows" },
                Sustainability = SustainabilityRating.MediumHigh,
                CostTier = CostTier.VeryHigh
            });
        }

        private void InitializeSubstitutions()
        {
            // Concrete substitutions
            AddSubstitution("CONC-C30", new MaterialSubstitution
            {
                AlternativeId = "CONC-GGBS30",
                Reason = "Lower carbon alternative using ground granite blast furnace slag",
                CarbonReduction = 0.35,
                CostImpact = -0.05,
                PerformanceImpact = "Slower early strength gain, improved long-term durability"
            });

            AddSubstitution("CONC-C30", new MaterialSubstitution
            {
                AlternativeId = "CONC-PFA30",
                Reason = "Lower carbon using pulverized fuel ash",
                CarbonReduction = 0.25,
                CostImpact = -0.03,
                PerformanceImpact = "Slower strength gain, improved workability"
            });

            // Steel substitutions
            AddSubstitution("STEEL-S355", new MaterialSubstitution
            {
                AlternativeId = "STEEL-RECYCLED-S355",
                Reason = "High recycled content steel with same performance",
                CarbonReduction = 0.6,
                CostImpact = 0.05,
                PerformanceImpact = "Equivalent structural performance"
            });

            AddSubstitution("STEEL-S355", new MaterialSubstitution
            {
                AlternativeId = "TIMBER-GLULAM",
                Reason = "Engineered timber alternative for suitable spans",
                CarbonReduction = 1.5, // Can be carbon negative
                CostImpact = 0.15,
                PerformanceImpact = "Limited span capacity, fire protection needed"
            });

            // Insulation substitutions
            AddSubstitution("INS-PIR", new MaterialSubstitution
            {
                AlternativeId = "INS-WOODFIBRE",
                Reason = "Natural fiber insulation with carbon storage",
                CarbonReduction = 0.8,
                CostImpact = 0.2,
                PerformanceImpact = "Good hygrothermal performance, thicker profile needed"
            });

            AddSubstitution("INS-MINERALWOOL", new MaterialSubstitution
            {
                AlternativeId = "INS-SHEEPWOOL",
                Reason = "Natural sustainable insulation",
                CarbonReduction = 0.9,
                CostImpact = 0.4,
                PerformanceImpact = "Excellent moisture regulation, similar thermal performance"
            });
        }

        private void InitializePerformanceData()
        {
            // Fire performance data
            AddPerformanceData("CONC-C30", new MaterialPerformanceData
            {
                MaterialId = "CONC-C30",
                FireRating = "REI 240",
                AcousticRating = 52, // dB Rw
                DurabilityClass = "DC-2",
                MaintenanceInterval = 50, // years
                ExpectedLifespan = 100,
                WeatherResistance = WeatherResistance.Excellent
            });

            AddPerformanceData("STEEL-S355", new MaterialPerformanceData
            {
                MaterialId = "STEEL-S355",
                FireRating = "R 15 (unprotected)",
                AcousticRating = 35,
                DurabilityClass = "DC-3 (with protection)",
                MaintenanceInterval = 20,
                ExpectedLifespan = 75,
                WeatherResistance = WeatherResistance.Poor
            });

            AddPerformanceData("TIMBER-CLT", new MaterialPerformanceData
            {
                MaterialId = "TIMBER-CLT",
                FireRating = "REI 60",
                AcousticRating = 38,
                DurabilityClass = "DC-2 (protected)",
                MaintenanceInterval = 10,
                ExpectedLifespan = 60,
                WeatherResistance = WeatherResistance.Moderate
            });

            AddPerformanceData("BRICK-CLAY", new MaterialPerformanceData
            {
                MaterialId = "BRICK-CLAY",
                FireRating = "REI 120",
                AcousticRating = 45,
                DurabilityClass = "DC-1",
                MaintenanceInterval = 30,
                ExpectedLifespan = 150,
                WeatherResistance = WeatherResistance.Excellent
            });
        }

        private void InitializeRegionalData()
        {
            // East African availability
            AddRegionalData("EAC", new RegionalAvailability
            {
                Region = "EAC",
                AvailableMaterials = new Dictionary<string, AvailabilityLevel>
                {
                    ["CONC-C30"] = AvailabilityLevel.ReadilyAvailable,
                    ["CONC-C40"] = AvailabilityLevel.Available,
                    ["STEEL-S355"] = AvailabilityLevel.LimitedImport,
                    ["BRICK-CLAY"] = AvailabilityLevel.ReadilyAvailable,
                    ["BLOCK-CONCRETE"] = AvailabilityLevel.ReadilyAvailable,
                    ["TIMBER-SOFTWOOD"] = AvailabilityLevel.Available,
                    ["TIMBER-CLT"] = AvailabilityLevel.SpecialOrder,
                    ["INS-MINERALWOOL"] = AvailabilityLevel.Available,
                    ["GLASS-DGU"] = AvailabilityLevel.Available,
                    ["GLASS-TGU"] = AvailabilityLevel.SpecialOrder
                },
                LocalAlternatives = new Dictionary<string, string>
                {
                    ["TIMBER-SOFTWOOD"] = "TIMBER-EUCALYPTUS",
                    ["BRICK-CLAY"] = "BRICK-INTERLOCKING"
                },
                LeadTimeDays = new Dictionary<string, int>
                {
                    ["CONC-C30"] = 1,
                    ["STEEL-S355"] = 30,
                    ["TIMBER-CLT"] = 90
                }
            });

            // UK availability
            AddRegionalData("UK", new RegionalAvailability
            {
                Region = "UK",
                AvailableMaterials = new Dictionary<string, AvailabilityLevel>
                {
                    ["CONC-C30"] = AvailabilityLevel.ReadilyAvailable,
                    ["CONC-C40"] = AvailabilityLevel.ReadilyAvailable,
                    ["STEEL-S355"] = AvailabilityLevel.ReadilyAvailable,
                    ["BRICK-CLAY"] = AvailabilityLevel.ReadilyAvailable,
                    ["TIMBER-CLT"] = AvailabilityLevel.Available,
                    ["INS-PIR"] = AvailabilityLevel.ReadilyAvailable,
                    ["GLASS-TGU"] = AvailabilityLevel.Available
                },
                LeadTimeDays = new Dictionary<string, int>
                {
                    ["CONC-C30"] = 1,
                    ["STEEL-S355"] = 7,
                    ["TIMBER-CLT"] = 14
                }
            });
        }

        private void InitializeSelectionRules()
        {
            // Fire safety rules
            _selectionRules.Add(new MaterialRule
            {
                RuleId = "FIRE-001",
                Name = "High-rise fire resistance",
                Condition = ctx => ctx.BuildingHeight > 18,
                Requirement = "Materials must achieve minimum REI 120",
                AffectedCategories = new[] { MaterialCategory.Structural },
                Priority = RulePriority.Critical
            });

            _selectionRules.Add(new MaterialRule
            {
                RuleId = "FIRE-002",
                Name = "Escape route materials",
                Condition = ctx => ctx.ElementType == "EscapeRoute",
                Requirement = "Non-combustible materials required",
                AffectedCategories = new[] { MaterialCategory.Structural, MaterialCategory.Insulation },
                Priority = RulePriority.Critical
            });

            // Sustainability rules
            _selectionRules.Add(new MaterialRule
            {
                RuleId = "SUST-001",
                Name = "Carbon reduction target",
                Condition = ctx => ctx.SustainabilityTarget == SustainabilityTarget.NetZero,
                Requirement = "Embodied carbon < 500 kgCO2e/m²",
                AffectedCategories = new[] { MaterialCategory.Structural, MaterialCategory.Masonry },
                Priority = RulePriority.High
            });

            // Regional rules
            _selectionRules.Add(new MaterialRule
            {
                RuleId = "REG-001",
                Name = "Local material preference",
                Condition = ctx => ctx.Region == "EAC",
                Requirement = "Prefer locally available materials",
                AffectedCategories = new[] { MaterialCategory.Masonry, MaterialCategory.Timber },
                Priority = RulePriority.Medium
            });

            // Acoustic rules
            _selectionRules.Add(new MaterialRule
            {
                RuleId = "ACOU-001",
                Name = "Party wall acoustic",
                Condition = ctx => ctx.ElementType == "PartyWall",
                Requirement = "Minimum Rw 45 dB",
                AffectedCategories = new[] { MaterialCategory.Masonry, MaterialCategory.Insulation },
                Priority = RulePriority.High
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get intelligent material recommendations for a given context.
        /// </summary>
        public MaterialRecommendation GetRecommendation(MaterialSelectionContext context)
        {
            var recommendation = new MaterialRecommendation
            {
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            // Gather applicable rules
            var applicableRules = _selectionRules
                .Where(r => r.Condition(context))
                .OrderByDescending(r => r.Priority)
                .ToList();

            recommendation.ApplicableRules = applicableRules;

            // Find suitable materials
            var suitableMaterials = FindSuitableMaterials(context, applicableRules);

            // Rank materials by multiple criteria
            var rankedMaterials = RankMaterials(suitableMaterials, context);

            recommendation.PrimaryRecommendation = rankedMaterials.FirstOrDefault();
            recommendation.Alternatives = rankedMaterials.Skip(1).Take(3).ToList();

            // Add substitutions for sustainability
            if (recommendation.PrimaryRecommendation != null)
            {
                recommendation.SustainableAlternatives = GetSustainableAlternatives(
                    recommendation.PrimaryRecommendation.MaterialId);
            }

            // Add regional considerations
            if (_regionalData.ContainsKey(context.Region))
            {
                recommendation.RegionalNotes = GetRegionalNotes(context.Region, rankedMaterials);
            }

            // Generate rationale
            recommendation.Rationale = GenerateRationale(recommendation, context);

            return recommendation;
        }

        /// <summary>
        /// Analyze the environmental impact of material choices.
        /// </summary>
        public EnvironmentalAnalysis AnalyzeEnvironmentalImpact(List<MaterialQuantity> materials)
        {
            var analysis = new EnvironmentalAnalysis();

            double totalEmbodiedCarbon = 0;
            double totalMass = 0;
            var breakdownByCategory = new Dictionary<MaterialCategory, double>();

            foreach (var mq in materials)
            {
                if (_materialProfiles.TryGetValue(mq.MaterialId, out var profile))
                {
                    double mass = mq.Volume * profile.Properties.GetValueOrDefault("Density", 1000);
                    double carbon = mass * profile.Properties.GetValueOrDefault("EmbodiedCarbon", 0);

                    totalEmbodiedCarbon += carbon;
                    totalMass += mass;

                    if (!breakdownByCategory.ContainsKey(profile.Category))
                        breakdownByCategory[profile.Category] = 0;
                    breakdownByCategory[profile.Category] += carbon;
                }
            }

            analysis.TotalEmbodiedCarbon = totalEmbodiedCarbon / 1000; // tonnes CO2e
            analysis.CarbonIntensity = totalMass > 0 ? totalEmbodiedCarbon / totalMass : 0;
            analysis.BreakdownByCategory = breakdownByCategory;

            // Calculate potential savings
            analysis.PotentialSavings = CalculatePotentialSavings(materials);

            // Rating
            analysis.Rating = analysis.CarbonIntensity switch
            {
                < 0.1 => "A - Excellent",
                < 0.2 => "B - Good",
                < 0.3 => "C - Average",
                < 0.5 => "D - Below Average",
                _ => "E - Poor"
            };

            return analysis;
        }

        /// <summary>
        /// Get cost comparison for material options.
        /// </summary>
        public CostComparison CompareCosts(string primaryMaterialId, string region)
        {
            var comparison = new CostComparison
            {
                PrimaryMaterialId = primaryMaterialId,
                Region = region
            };

            if (!_materialProfiles.TryGetValue(primaryMaterialId, out var primary))
                return comparison;

            comparison.PrimaryCost = GetRegionalCost(primaryMaterialId, region);

            // Get alternatives
            if (_substitutions.TryGetValue(primaryMaterialId, out var subs))
            {
                comparison.AlternativeCosts = subs.ToDictionary(
                    s => s.AlternativeId,
                    s => GetRegionalCost(s.AlternativeId, region) ??
                         comparison.PrimaryCost * (1 + s.CostImpact)
                );
            }

            // Calculate lifecycle costs
            comparison.LifecycleCosts = CalculateLifecycleCosts(primaryMaterialId, region);

            return comparison;
        }

        /// <summary>
        /// Validate material selection against building codes.
        /// </summary>
        public MaterialValidation ValidateMaterialSelection(
            string materialId,
            string elementType,
            MaterialSelectionContext context)
        {
            var validation = new MaterialValidation
            {
                MaterialId = materialId,
                ElementType = elementType,
                IsValid = true,
                Warnings = new List<string>(),
                Errors = new List<string>()
            };

            if (!_materialProfiles.TryGetValue(materialId, out var profile))
            {
                validation.IsValid = false;
                validation.Errors.Add($"Unknown material: {materialId}");
                return validation;
            }

            // Check against applicable rules
            foreach (var rule in _selectionRules.Where(r => r.Condition(context)))
            {
                var ruleCheck = CheckMaterialAgainstRule(profile, rule, context);
                if (!ruleCheck.Passed)
                {
                    if (rule.Priority == RulePriority.Critical)
                    {
                        validation.IsValid = false;
                        validation.Errors.Add($"{rule.Name}: {ruleCheck.Message}");
                    }
                    else
                    {
                        validation.Warnings.Add($"{rule.Name}: {ruleCheck.Message}");
                    }
                }
            }

            // Check regional availability
            if (_regionalData.TryGetValue(context.Region, out var regional))
            {
                if (regional.AvailableMaterials.TryGetValue(materialId, out var availability))
                {
                    if (availability == AvailabilityLevel.Unavailable)
                    {
                        validation.Warnings.Add($"Material not available in {context.Region}");
                        if (regional.LocalAlternatives.TryGetValue(materialId, out var alt))
                        {
                            validation.SuggestedAlternative = alt;
                        }
                    }
                    else if (availability == AvailabilityLevel.SpecialOrder)
                    {
                        validation.Warnings.Add($"Special order required - lead time: " +
                            $"{regional.LeadTimeDays.GetValueOrDefault(materialId, 60)} days");
                    }
                }
            }

            return validation;
        }

        /// <summary>
        /// Get material specification for documentation.
        /// </summary>
        public MaterialSpecification GetSpecification(string materialId)
        {
            if (!_materialProfiles.TryGetValue(materialId, out var profile))
                return null;

            var spec = new MaterialSpecification
            {
                MaterialId = materialId,
                Name = profile.Name,
                Category = profile.Category,
                Properties = profile.Properties,
                Applications = profile.Applications.ToList()
            };

            if (_performanceData.TryGetValue(materialId, out var perf))
            {
                spec.FireRating = perf.FireRating;
                spec.AcousticRating = perf.AcousticRating;
                spec.DurabilityClass = perf.DurabilityClass;
                spec.ExpectedLifespan = perf.ExpectedLifespan;
            }

            if (_substitutions.TryGetValue(materialId, out var subs))
            {
                spec.Substitutions = subs;
            }

            return spec;
        }

        /// <summary>
        /// Find materials matching specific criteria.
        /// </summary>
        public List<MaterialMatch> FindMaterials(MaterialSearchCriteria criteria)
        {
            var matches = new List<MaterialMatch>();

            foreach (var profile in _materialProfiles.Values)
            {
                var score = CalculateMatchScore(profile, criteria);
                if (score > criteria.MinimumScore)
                {
                    matches.Add(new MaterialMatch
                    {
                        MaterialId = profile.MaterialId,
                        Name = profile.Name,
                        Category = profile.Category,
                        MatchScore = score,
                        MatchedCriteria = GetMatchedCriteria(profile, criteria)
                    });
                }
            }

            return matches.OrderByDescending(m => m.MatchScore).ToList();
        }

        #endregion

        #region Private Methods

        private void AddMaterialProfile(MaterialProfile profile)
        {
            _materialProfiles[profile.MaterialId] = profile;
        }

        private void AddSubstitution(string materialId, MaterialSubstitution sub)
        {
            if (!_substitutions.ContainsKey(materialId))
                _substitutions[materialId] = new List<MaterialSubstitution>();
            _substitutions[materialId].Add(sub);
        }

        private void AddPerformanceData(string materialId, MaterialPerformanceData data)
        {
            _performanceData[materialId] = data;
        }

        private void AddRegionalData(string region, RegionalAvailability data)
        {
            _regionalData[region] = data;
        }

        private List<MaterialProfile> FindSuitableMaterials(
            MaterialSelectionContext context,
            List<MaterialRule> rules)
        {
            return _materialProfiles.Values
                .Where(p => IsSuitableForContext(p, context, rules))
                .ToList();
        }

        private bool IsSuitableForContext(
            MaterialProfile profile,
            MaterialSelectionContext context,
            List<MaterialRule> rules)
        {
            // Check category match
            if (context.RequiredCategory.HasValue &&
                profile.Category != context.RequiredCategory.Value)
                return false;

            // Check application match
            if (!string.IsNullOrEmpty(context.Application) &&
                !profile.Applications.Any(a => a.Contains(context.Application, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Check critical rules
            foreach (var rule in rules.Where(r => r.Priority == RulePriority.Critical))
            {
                if (!CheckMaterialAgainstRule(profile, rule, context).Passed)
                    return false;
            }

            return true;
        }

        private List<RankedMaterial> RankMaterials(
            List<MaterialProfile> materials,
            MaterialSelectionContext context)
        {
            return materials.Select(m => new RankedMaterial
            {
                MaterialId = m.MaterialId,
                Name = m.Name,
                Category = m.Category,
                Score = CalculateOverallScore(m, context),
                Scores = new Dictionary<string, double>
                {
                    ["Performance"] = CalculatePerformanceScore(m, context),
                    ["Sustainability"] = CalculateSustainabilityScore(m),
                    ["Cost"] = CalculateCostScore(m, context),
                    ["Availability"] = CalculateAvailabilityScore(m, context.Region)
                }
            })
            .OrderByDescending(r => r.Score)
            .ToList();
        }

        private double CalculateOverallScore(MaterialProfile profile, MaterialSelectionContext context)
        {
            double perfScore = CalculatePerformanceScore(profile, context);
            double sustScore = CalculateSustainabilityScore(profile);
            double costScore = CalculateCostScore(profile, context);
            double availScore = CalculateAvailabilityScore(profile, context.Region);

            // Weighted average based on priorities
            double perfWeight = context.PerformancePriority / 10.0;
            double sustWeight = context.SustainabilityPriority / 10.0;
            double costWeight = context.CostPriority / 10.0;
            double availWeight = 0.2;

            double totalWeight = perfWeight + sustWeight + costWeight + availWeight;

            return (perfScore * perfWeight + sustScore * sustWeight +
                    costScore * costWeight + availScore * availWeight) / totalWeight;
        }

        private double CalculatePerformanceScore(MaterialProfile profile, MaterialSelectionContext context)
        {
            double score = 0.5;

            if (_performanceData.TryGetValue(profile.MaterialId, out var perf))
            {
                // Fire resistance
                if (context.RequiredFireRating > 0 && perf.FireRating != null)
                {
                    var fireMinutes = ExtractFireMinutes(perf.FireRating);
                    if (fireMinutes >= context.RequiredFireRating)
                        score += 0.2;
                }

                // Acoustic
                if (context.RequiredAcousticRating > 0)
                {
                    if (perf.AcousticRating >= context.RequiredAcousticRating)
                        score += 0.15;
                }

                // Durability
                score += (perf.ExpectedLifespan / 100.0) * 0.15;
            }

            return Math.Min(1.0, score);
        }

        private double CalculateSustainabilityScore(MaterialProfile profile)
        {
            return profile.Sustainability switch
            {
                SustainabilityRating.VeryHigh => 1.0,
                SustainabilityRating.High => 0.8,
                SustainabilityRating.MediumHigh => 0.6,
                SustainabilityRating.Medium => 0.4,
                SustainabilityRating.Low => 0.2,
                _ => 0.1
            };
        }

        private double CalculateCostScore(MaterialProfile profile, MaterialSelectionContext context)
        {
            // Lower cost = higher score
            return profile.CostTier switch
            {
                CostTier.VeryLow => 1.0,
                CostTier.Low => 0.8,
                CostTier.Medium => 0.6,
                CostTier.MediumHigh => 0.4,
                CostTier.High => 0.2,
                CostTier.VeryHigh => 0.1,
                _ => 0.5
            };
        }

        private double CalculateAvailabilityScore(MaterialProfile profile, string region)
        {
            if (_regionalData.TryGetValue(region, out var regional))
            {
                if (regional.AvailableMaterials.TryGetValue(profile.MaterialId, out var avail))
                {
                    return avail switch
                    {
                        AvailabilityLevel.ReadilyAvailable => 1.0,
                        AvailabilityLevel.Available => 0.8,
                        AvailabilityLevel.LimitedImport => 0.5,
                        AvailabilityLevel.SpecialOrder => 0.3,
                        AvailabilityLevel.Unavailable => 0.0,
                        _ => 0.5
                    };
                }
            }
            return 0.5; // Unknown region
        }

        private List<MaterialSubstitution> GetSustainableAlternatives(string materialId)
        {
            if (_substitutions.TryGetValue(materialId, out var subs))
            {
                return subs.Where(s => s.CarbonReduction > 0.2).ToList();
            }
            return new List<MaterialSubstitution>();
        }

        private List<string> GetRegionalNotes(string region, List<RankedMaterial> materials)
        {
            var notes = new List<string>();

            if (_regionalData.TryGetValue(region, out var regional))
            {
                foreach (var mat in materials.Take(3))
                {
                    if (regional.AvailableMaterials.TryGetValue(mat.MaterialId, out var avail))
                    {
                        if (avail == AvailabilityLevel.LimitedImport)
                            notes.Add($"{mat.Name}: Import required, consider local alternatives");
                        else if (avail == AvailabilityLevel.SpecialOrder)
                            notes.Add($"{mat.Name}: Special order - plan lead time accordingly");
                    }

                    if (regional.LocalAlternatives.TryGetValue(mat.MaterialId, out var alt))
                        notes.Add($"Local alternative for {mat.Name}: {alt}");
                }
            }

            return notes;
        }

        private string GenerateRationale(MaterialRecommendation rec, MaterialSelectionContext context)
        {
            var parts = new List<string>();

            if (rec.PrimaryRecommendation != null)
            {
                parts.Add($"Recommended {rec.PrimaryRecommendation.Name} based on:");

                var scores = rec.PrimaryRecommendation.Scores;
                if (scores["Performance"] > 0.7)
                    parts.Add("- Excellent performance characteristics");
                if (scores["Sustainability"] > 0.6)
                    parts.Add("- Good sustainability profile");
                if (scores["Cost"] > 0.6)
                    parts.Add("- Competitive cost");
                if (scores["Availability"] > 0.7)
                    parts.Add($"- Readily available in {context.Region}");
            }

            if (rec.ApplicableRules.Any())
            {
                parts.Add($"Complies with {rec.ApplicableRules.Count} applicable rules including " +
                    string.Join(", ", rec.ApplicableRules.Take(2).Select(r => r.Name)));
            }

            return string.Join(" ", parts);
        }

        private (bool Passed, string Message) CheckMaterialAgainstRule(
            MaterialProfile profile,
            MaterialRule rule,
            MaterialSelectionContext context)
        {
            // Simplified rule checking
            switch (rule.RuleId)
            {
                case "FIRE-001":
                    if (_performanceData.TryGetValue(profile.MaterialId, out var perf))
                    {
                        var minutes = ExtractFireMinutes(perf.FireRating);
                        if (minutes < 120)
                            return (false, $"Fire rating {perf.FireRating} below REI 120 requirement");
                    }
                    break;

                case "FIRE-002":
                    if (profile.Category == MaterialCategory.Timber &&
                        profile.MaterialId != "TIMBER-CLT")
                        return (false, "Combustible material not permitted in escape routes");
                    break;

                case "ACOU-001":
                    if (_performanceData.TryGetValue(profile.MaterialId, out var perfAcou))
                    {
                        if (perfAcou.AcousticRating < 45)
                            return (false, $"Acoustic rating {perfAcou.AcousticRating} dB below 45 dB requirement");
                    }
                    break;
            }

            return (true, "Passed");
        }

        private int ExtractFireMinutes(string fireRating)
        {
            if (string.IsNullOrEmpty(fireRating)) return 0;

            var match = System.Text.RegularExpressions.Regex.Match(fireRating, @"\d+");
            return match.Success ? int.Parse(match.Value) : 0;
        }

        private Dictionary<string, double> CalculatePotentialSavings(List<MaterialQuantity> materials)
        {
            var savings = new Dictionary<string, double>();

            foreach (var mq in materials)
            {
                if (_substitutions.TryGetValue(mq.MaterialId, out var subs))
                {
                    var bestSub = subs.OrderByDescending(s => s.CarbonReduction).FirstOrDefault();
                    if (bestSub != null)
                    {
                        var profile = _materialProfiles.GetValueOrDefault(mq.MaterialId);
                        if (profile != null)
                        {
                            double mass = mq.Volume * profile.Properties.GetValueOrDefault("Density", 1000);
                            double carbon = mass * profile.Properties.GetValueOrDefault("EmbodiedCarbon", 0);
                            double potentialSaving = carbon * bestSub.CarbonReduction;
                            savings[mq.MaterialId] = potentialSaving / 1000; // tonnes
                        }
                    }
                }
            }

            return savings;
        }

        private double? GetRegionalCost(string materialId, string region)
        {
            // Simplified cost lookup - would integrate with StingBIM.Data.Materials
            var baseCosts = new Dictionary<string, double>
            {
                ["CONC-C30"] = 100, // per m³
                ["CONC-C40"] = 120,
                ["STEEL-S355"] = 2500, // per tonne
                ["BRICK-CLAY"] = 0.5, // per unit
                ["BLOCK-CONCRETE"] = 1.2,
                ["TIMBER-SOFTWOOD"] = 400, // per m³
                ["TIMBER-CLT"] = 800,
                ["INS-MINERALWOOL"] = 30, // per m²
                ["INS-PIR"] = 45,
                ["GLASS-DGU"] = 80, // per m²
                ["GLASS-TGU"] = 150
            };

            var regionalMultipliers = new Dictionary<string, double>
            {
                ["UK"] = 1.0,
                ["EAC"] = 0.7,
                ["US"] = 1.1
            };

            if (baseCosts.TryGetValue(materialId, out var cost))
            {
                var multiplier = regionalMultipliers.GetValueOrDefault(region, 1.0);
                return cost * multiplier;
            }

            return null;
        }

        private Dictionary<string, double> CalculateLifecycleCosts(string materialId, string region)
        {
            var lifecycle = new Dictionary<string, double>();

            var baseCost = GetRegionalCost(materialId, region) ?? 0;
            lifecycle["Initial"] = baseCost;

            if (_performanceData.TryGetValue(materialId, out var perf))
            {
                // Maintenance costs over 30 years
                var maintenanceCycles = 30.0 / perf.MaintenanceInterval;
                lifecycle["Maintenance30Yr"] = baseCost * 0.1 * maintenanceCycles;

                // End of life
                lifecycle["Disposal"] = baseCost * 0.05;

                // Total lifecycle
                lifecycle["Total30Yr"] = lifecycle["Initial"] + lifecycle["Maintenance30Yr"];
            }

            return lifecycle;
        }

        private double CalculateMatchScore(MaterialProfile profile, MaterialSearchCriteria criteria)
        {
            double score = 0;
            int criteriaCount = 0;

            if (criteria.Category.HasValue)
            {
                criteriaCount++;
                if (profile.Category == criteria.Category.Value) score += 1;
            }

            if (criteria.MaxEmbodiedCarbon.HasValue)
            {
                criteriaCount++;
                var carbon = profile.Properties.GetValueOrDefault("EmbodiedCarbon", 1);
                if (carbon <= criteria.MaxEmbodiedCarbon.Value) score += 1;
            }

            if (criteria.MinFireResistance.HasValue)
            {
                criteriaCount++;
                var fire = profile.Properties.GetValueOrDefault("FireResistance", 0);
                if (fire >= criteria.MinFireResistance.Value) score += 1;
            }

            if (criteria.MinSustainability.HasValue)
            {
                criteriaCount++;
                if ((int)profile.Sustainability >= (int)criteria.MinSustainability.Value) score += 1;
            }

            if (criteria.MaxCostTier.HasValue)
            {
                criteriaCount++;
                if ((int)profile.CostTier <= (int)criteria.MaxCostTier.Value) score += 1;
            }

            return criteriaCount > 0 ? score / criteriaCount : 0;
        }

        private List<string> GetMatchedCriteria(MaterialProfile profile, MaterialSearchCriteria criteria)
        {
            var matched = new List<string>();

            if (criteria.Category.HasValue && profile.Category == criteria.Category.Value)
                matched.Add($"Category: {criteria.Category.Value}");

            if (criteria.MaxEmbodiedCarbon.HasValue)
            {
                var carbon = profile.Properties.GetValueOrDefault("EmbodiedCarbon", 1);
                if (carbon <= criteria.MaxEmbodiedCarbon.Value)
                    matched.Add($"Embodied carbon: {carbon} kgCO2/kg");
            }

            if (criteria.MinFireResistance.HasValue)
            {
                var fire = profile.Properties.GetValueOrDefault("FireResistance", 0);
                if (fire >= criteria.MinFireResistance.Value)
                    matched.Add($"Fire resistance: {fire} minutes");
            }

            return matched;
        }

        #endregion
    }

    #region Supporting Types

    public class MaterialProfile
    {
        public string MaterialId { get; set; }
        public string Name { get; set; }
        public MaterialCategory Category { get; set; }
        public Dictionary<string, double> Properties { get; set; } = new();
        public string[] Applications { get; set; } = Array.Empty<string>();
        public SustainabilityRating Sustainability { get; set; }
        public CostTier CostTier { get; set; }
    }

    public class MaterialSubstitution
    {
        public string AlternativeId { get; set; }
        public string Reason { get; set; }
        public double CarbonReduction { get; set; }
        public double CostImpact { get; set; }
        public string PerformanceImpact { get; set; }
    }

    public class MaterialPerformanceData
    {
        public string MaterialId { get; set; }
        public string FireRating { get; set; }
        public double AcousticRating { get; set; }
        public string DurabilityClass { get; set; }
        public int MaintenanceInterval { get; set; }
        public int ExpectedLifespan { get; set; }
        public WeatherResistance WeatherResistance { get; set; }
    }

    public class RegionalAvailability
    {
        public string Region { get; set; }
        public Dictionary<string, AvailabilityLevel> AvailableMaterials { get; set; } = new();
        public Dictionary<string, string> LocalAlternatives { get; set; } = new();
        public Dictionary<string, int> LeadTimeDays { get; set; } = new();
    }

    public class MaterialRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public Func<MaterialSelectionContext, bool> Condition { get; set; }
        public string Requirement { get; set; }
        public MaterialCategory[] AffectedCategories { get; set; }
        public RulePriority Priority { get; set; }
    }

    public class MaterialSelectionContext
    {
        public string Region { get; set; } = "UK";
        public string ElementType { get; set; }
        public string Application { get; set; }
        public MaterialCategory? RequiredCategory { get; set; }
        public double BuildingHeight { get; set; }
        public SustainabilityTarget SustainabilityTarget { get; set; }
        public int RequiredFireRating { get; set; }
        public int RequiredAcousticRating { get; set; }
        public int PerformancePriority { get; set; } = 5;
        public int SustainabilityPriority { get; set; } = 5;
        public int CostPriority { get; set; } = 5;
    }

    public class MaterialRecommendation
    {
        public MaterialSelectionContext Context { get; set; }
        public DateTime Timestamp { get; set; }
        public RankedMaterial PrimaryRecommendation { get; set; }
        public List<RankedMaterial> Alternatives { get; set; } = new();
        public List<MaterialSubstitution> SustainableAlternatives { get; set; } = new();
        public List<MaterialRule> ApplicableRules { get; set; } = new();
        public List<string> RegionalNotes { get; set; } = new();
        public string Rationale { get; set; }
    }

    public class RankedMaterial
    {
        public string MaterialId { get; set; }
        public string Name { get; set; }
        public MaterialCategory Category { get; set; }
        public double Score { get; set; }
        public Dictionary<string, double> Scores { get; set; } = new();
    }

    public class EnvironmentalAnalysis
    {
        public double TotalEmbodiedCarbon { get; set; }
        public double CarbonIntensity { get; set; }
        public Dictionary<MaterialCategory, double> BreakdownByCategory { get; set; } = new();
        public Dictionary<string, double> PotentialSavings { get; set; } = new();
        public string Rating { get; set; }
    }

    public class CostComparison
    {
        public string PrimaryMaterialId { get; set; }
        public string Region { get; set; }
        public double? PrimaryCost { get; set; }
        public Dictionary<string, double?> AlternativeCosts { get; set; } = new();
        public Dictionary<string, double> LifecycleCosts { get; set; } = new();
    }

    public class MaterialValidation
    {
        public string MaterialId { get; set; }
        public string ElementType { get; set; }
        public bool IsValid { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public string SuggestedAlternative { get; set; }
    }

    public class MaterialSpecification
    {
        public string MaterialId { get; set; }
        public string Name { get; set; }
        public MaterialCategory Category { get; set; }
        public Dictionary<string, double> Properties { get; set; } = new();
        public List<string> Applications { get; set; } = new();
        public string FireRating { get; set; }
        public double AcousticRating { get; set; }
        public string DurabilityClass { get; set; }
        public int ExpectedLifespan { get; set; }
        public List<MaterialSubstitution> Substitutions { get; set; } = new();
    }

    public class MaterialSearchCriteria
    {
        public MaterialCategory? Category { get; set; }
        public double? MaxEmbodiedCarbon { get; set; }
        public int? MinFireResistance { get; set; }
        public SustainabilityRating? MinSustainability { get; set; }
        public CostTier? MaxCostTier { get; set; }
        public double MinimumScore { get; set; } = 0.5;
    }

    public class MaterialMatch
    {
        public string MaterialId { get; set; }
        public string Name { get; set; }
        public MaterialCategory Category { get; set; }
        public double MatchScore { get; set; }
        public List<string> MatchedCriteria { get; set; } = new();
    }

    public class MaterialQuantity
    {
        public string MaterialId { get; set; }
        public double Volume { get; set; }
        public string Unit { get; set; }
    }

    public enum MaterialCategory
    {
        Structural,
        Masonry,
        Insulation,
        Timber,
        Glass,
        Roofing,
        Cladding,
        Flooring,
        Finishes
    }

    public enum SustainabilityRating
    {
        VeryLow,
        Low,
        Medium,
        MediumHigh,
        High,
        VeryHigh
    }

    public enum CostTier
    {
        VeryLow,
        Low,
        Medium,
        MediumHigh,
        High,
        VeryHigh
    }

    public enum AvailabilityLevel
    {
        ReadilyAvailable,
        Available,
        LimitedImport,
        SpecialOrder,
        Unavailable
    }

    public enum WeatherResistance
    {
        Poor,
        Moderate,
        Good,
        Excellent
    }

    public enum SustainabilityTarget
    {
        Standard,
        Improved,
        LowCarbon,
        NetZero
    }

    public enum RulePriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
