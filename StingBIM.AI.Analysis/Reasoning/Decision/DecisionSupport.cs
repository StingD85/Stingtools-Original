// =========================================================================
// StingBIM.AI.Reasoning - Decision Support System
// Multi-criteria analysis for design trade-offs
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Reasoning.Decision
{
    /// <summary>
    /// Decision support system for evaluating design alternatives
    /// and making informed trade-offs between competing objectives.
    /// </summary>
    public class DecisionSupport
    {
        private readonly Dictionary<string, DecisionCriterion> _criteria;
        private readonly Dictionary<string, TradeOffRule> _tradeOffRules;
        private readonly Dictionary<string, RiskFactor> _riskFactors;
        private readonly List<DecisionTemplate> _templates;

        public DecisionSupport()
        {
            _criteria = new Dictionary<string, DecisionCriterion>();
            _tradeOffRules = new Dictionary<string, TradeOffRule>();
            _riskFactors = new Dictionary<string, RiskFactor>();
            _templates = new List<DecisionTemplate>();

            InitializeCriteria();
            InitializeTradeOffRules();
            InitializeRiskFactors();
            InitializeTemplates();
        }

        #region Initialization

        private void InitializeCriteria()
        {
            // Cost criteria
            AddCriterion(new DecisionCriterion
            {
                CriterionId = "InitialCost",
                Name = "Initial Construction Cost",
                Category = CriterionCategory.Cost,
                Unit = "$/m²",
                Direction = OptimizationDirection.Minimize,
                Weight = 1.0,
                Description = "Total initial construction cost per square meter"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "LifecycleCost",
                Name = "30-Year Lifecycle Cost",
                Category = CriterionCategory.Cost,
                Unit = "$/m²",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.8,
                Description = "Total cost of ownership over 30 years including maintenance"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "OperatingCost",
                Name = "Annual Operating Cost",
                Category = CriterionCategory.Cost,
                Unit = "$/m²/year",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.7,
                Description = "Annual energy and maintenance costs"
            });

            // Performance criteria
            AddCriterion(new DecisionCriterion
            {
                CriterionId = "EnergyPerformance",
                Name = "Energy Performance",
                Category = CriterionCategory.Performance,
                Unit = "kWh/m²/year",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.9,
                Description = "Annual energy consumption per square meter"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "ThermalComfort",
                Name = "Thermal Comfort",
                Category = CriterionCategory.Performance,
                Unit = "% hours comfortable",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.85,
                Description = "Percentage of occupied hours within comfort range"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "DaylightFactor",
                Name = "Daylight Factor",
                Category = CriterionCategory.Performance,
                Unit = "%",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.7,
                Description = "Average daylight factor in occupied spaces"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "AcousticPerformance",
                Name = "Acoustic Performance",
                Category = CriterionCategory.Performance,
                Unit = "STC rating",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.6,
                Description = "Sound transmission class rating"
            });

            // Sustainability criteria
            AddCriterion(new DecisionCriterion
            {
                CriterionId = "EmbodiedCarbon",
                Name = "Embodied Carbon",
                Category = CriterionCategory.Sustainability,
                Unit = "kgCO2e/m²",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.85,
                Description = "Upfront carbon emissions from materials and construction"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "OperationalCarbon",
                Name = "Operational Carbon",
                Category = CriterionCategory.Sustainability,
                Unit = "kgCO2e/m²/year",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.8,
                Description = "Annual carbon emissions from building operation"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "WaterEfficiency",
                Name = "Water Efficiency",
                Category = CriterionCategory.Sustainability,
                Unit = "L/person/day",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.6,
                Description = "Daily water consumption per occupant"
            });

            // Schedule criteria
            AddCriterion(new DecisionCriterion
            {
                CriterionId = "ConstructionDuration",
                Name = "Construction Duration",
                Category = CriterionCategory.Schedule,
                Unit = "months",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.75,
                Description = "Total construction time"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "LeadTime",
                Name = "Material Lead Time",
                Category = CriterionCategory.Schedule,
                Unit = "weeks",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.5,
                Description = "Maximum lead time for critical materials"
            });

            // Quality criteria
            AddCriterion(new DecisionCriterion
            {
                CriterionId = "Durability",
                Name = "Durability",
                Category = CriterionCategory.Quality,
                Unit = "years",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.7,
                Description = "Expected service life"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "Flexibility",
                Name = "Space Flexibility",
                Category = CriterionCategory.Quality,
                Unit = "score 1-10",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.6,
                Description = "Adaptability for future changes"
            });

            // Risk criteria
            AddCriterion(new DecisionCriterion
            {
                CriterionId = "ConstructionRisk",
                Name = "Construction Risk",
                Category = CriterionCategory.Risk,
                Unit = "score 1-10",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.65,
                Description = "Risk of construction delays or issues"
            });

            AddCriterion(new DecisionCriterion
            {
                CriterionId = "SupplyChainRisk",
                Name = "Supply Chain Risk",
                Category = CriterionCategory.Risk,
                Unit = "score 1-10",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.55,
                Description = "Risk of material availability issues"
            });
        }

        private void InitializeTradeOffRules()
        {
            // Cost vs Performance trade-offs
            AddTradeOffRule(new TradeOffRule
            {
                RuleId = "CostVsEnergy",
                Criteria1 = "InitialCost",
                Criteria2 = "EnergyPerformance",
                Relationship = TradeOffRelationship.Inverse,
                TypicalRatio = 0.05, // 5% cost increase per 10% energy reduction
                Guidance = "Higher upfront investment in envelope and systems typically yields better energy performance"
            });

            AddTradeOffRule(new TradeOffRule
            {
                RuleId = "CostVsDurability",
                Criteria1 = "InitialCost",
                Criteria2 = "Durability",
                Relationship = TradeOffRelationship.Proportional,
                TypicalRatio = 0.02, // 2% cost increase per year of added life
                Guidance = "Premium materials increase initial cost but extend service life"
            });

            AddTradeOffRule(new TradeOffRule
            {
                RuleId = "SpeedVsCost",
                Criteria1 = "ConstructionDuration",
                Criteria2 = "InitialCost",
                Relationship = TradeOffRelationship.Inverse,
                TypicalRatio = 0.03, // 3% cost increase per 10% schedule reduction
                Guidance = "Accelerated schedules require more resources and premium procurement"
            });

            AddTradeOffRule(new TradeOffRule
            {
                RuleId = "EmbodiedVsOperational",
                Criteria1 = "EmbodiedCarbon",
                Criteria2 = "OperationalCarbon",
                Relationship = TradeOffRelationship.Inverse,
                TypicalRatio = 1.0,
                Guidance = "More efficient systems often have higher embodied carbon - optimize for whole lifecycle"
            });

            AddTradeOffRule(new TradeOffRule
            {
                RuleId = "DaylightVsEnergy",
                Criteria1 = "DaylightFactor",
                Criteria2 = "EnergyPerformance",
                Relationship = TradeOffRelationship.Complex,
                Guidance = "More glazing improves daylight but may increase heating/cooling loads - optimize WWR"
            });

            AddTradeOffRule(new TradeOffRule
            {
                RuleId = "FlexibilityVsCost",
                Criteria1 = "Flexibility",
                Criteria2 = "InitialCost",
                Relationship = TradeOffRelationship.Proportional,
                TypicalRatio = 0.08,
                Guidance = "Open floor plans and raised floors increase flexibility but add cost"
            });
        }

        private void InitializeRiskFactors()
        {
            AddRiskFactor(new RiskFactor
            {
                RiskId = "MaterialAvailability",
                Name = "Material Availability Risk",
                Category = RiskCategory.SupplyChain,
                Triggers = new[]
                {
                    "Imported materials",
                    "Single source suppliers",
                    "Long lead times",
                    "Specialty items"
                },
                Mitigations = new[]
                {
                    "Use locally available alternatives",
                    "Identify multiple suppliers",
                    "Order early with staged delivery",
                    "Design for substitution flexibility"
                },
                ImpactAreas = new[] { "Schedule", "Cost" }
            });

            AddRiskFactor(new RiskFactor
            {
                RiskId = "ConstructionComplexity",
                Name = "Construction Complexity Risk",
                Category = RiskCategory.Technical,
                Triggers = new[]
                {
                    "Non-standard details",
                    "Complex geometry",
                    "Tight tolerances",
                    "Novel systems"
                },
                Mitigations = new[]
                {
                    "Simplify where possible",
                    "Early contractor involvement",
                    "Mockups and prototypes",
                    "Detailed construction sequence"
                },
                ImpactAreas = new[] { "Quality", "Schedule", "Cost" }
            });

            AddRiskFactor(new RiskFactor
            {
                RiskId = "RegulatoryApproval",
                Name = "Regulatory Approval Risk",
                Category = RiskCategory.Regulatory,
                Triggers = new[]
                {
                    "Performance-based design",
                    "Alternative compliance paths",
                    "Novel systems",
                    "Heritage constraints"
                },
                Mitigations = new[]
                {
                    "Early authority consultation",
                    "Use prescriptive path where possible",
                    "Peer review for complex items",
                    "Build in approval contingency"
                },
                ImpactAreas = new[] { "Schedule" }
            });

            AddRiskFactor(new RiskFactor
            {
                RiskId = "CostEscalation",
                Name = "Cost Escalation Risk",
                Category = RiskCategory.Financial,
                Triggers = new[]
                {
                    "Long project duration",
                    "Volatile material prices",
                    "Exchange rate exposure",
                    "Incomplete design"
                },
                Mitigations = new[]
                {
                    "Fix prices early where possible",
                    "Include escalation contingency",
                    "Use local materials",
                    "Complete design before procurement"
                },
                ImpactAreas = new[] { "Cost" }
            });
        }

        private void InitializeTemplates()
        {
            // Structural system selection
            _templates.Add(new DecisionTemplate
            {
                TemplateId = "StructuralSystem",
                Name = "Structural System Selection",
                Description = "Compare structural system alternatives",
                RelevantCriteria = new[] { "InitialCost", "ConstructionDuration", "Flexibility", "EmbodiedCarbon", "Durability" },
                TypicalAlternatives = new[]
                {
                    new AlternativeTemplate { Name = "RC Frame", Description = "Reinforced concrete frame" },
                    new AlternativeTemplate { Name = "Steel Frame", Description = "Structural steel frame" },
                    new AlternativeTemplate { Name = "Composite", Description = "Steel-concrete composite" },
                    new AlternativeTemplate { Name = "Timber", Description = "Mass timber structure" },
                    new AlternativeTemplate { Name = "Loadbearing Masonry", Description = "Loadbearing masonry walls" }
                },
                DefaultWeights = new Dictionary<string, double>
                {
                    ["InitialCost"] = 0.25,
                    ["ConstructionDuration"] = 0.15,
                    ["Flexibility"] = 0.2,
                    ["EmbodiedCarbon"] = 0.25,
                    ["Durability"] = 0.15
                }
            });

            // Facade system selection
            _templates.Add(new DecisionTemplate
            {
                TemplateId = "FacadeSystem",
                Name = "Facade System Selection",
                Description = "Compare building envelope alternatives",
                RelevantCriteria = new[] { "InitialCost", "EnergyPerformance", "DaylightFactor", "ThermalComfort", "Durability", "EmbodiedCarbon" },
                TypicalAlternatives = new[]
                {
                    new AlternativeTemplate { Name = "Brick Cavity", Description = "Traditional brick cavity wall" },
                    new AlternativeTemplate { Name = "Curtain Wall", Description = "Aluminum curtain wall system" },
                    new AlternativeTemplate { Name = "Precast", Description = "Precast concrete panels" },
                    new AlternativeTemplate { Name = "Rainscreen", Description = "Ventilated rainscreen cladding" },
                    new AlternativeTemplate { Name = "EIFS", Description = "External insulation finish system" }
                },
                DefaultWeights = new Dictionary<string, double>
                {
                    ["InitialCost"] = 0.2,
                    ["EnergyPerformance"] = 0.25,
                    ["DaylightFactor"] = 0.1,
                    ["ThermalComfort"] = 0.15,
                    ["Durability"] = 0.15,
                    ["EmbodiedCarbon"] = 0.15
                }
            });

            // HVAC system selection
            _templates.Add(new DecisionTemplate
            {
                TemplateId = "HVACSystem",
                Name = "HVAC System Selection",
                Description = "Compare mechanical system alternatives",
                RelevantCriteria = new[] { "InitialCost", "OperatingCost", "EnergyPerformance", "ThermalComfort", "Flexibility", "OperationalCarbon" },
                TypicalAlternatives = new[]
                {
                    new AlternativeTemplate { Name = "VAV", Description = "Variable air volume system" },
                    new AlternativeTemplate { Name = "VRF", Description = "Variable refrigerant flow" },
                    new AlternativeTemplate { Name = "Chilled Beam", Description = "Active chilled beam system" },
                    new AlternativeTemplate { Name = "DOAS+Radiant", Description = "Dedicated outdoor air with radiant" },
                    new AlternativeTemplate { Name = "Natural Ventilation", Description = "Mixed-mode natural ventilation" }
                },
                DefaultWeights = new Dictionary<string, double>
                {
                    ["InitialCost"] = 0.2,
                    ["OperatingCost"] = 0.2,
                    ["EnergyPerformance"] = 0.2,
                    ["ThermalComfort"] = 0.15,
                    ["Flexibility"] = 0.1,
                    ["OperationalCarbon"] = 0.15
                }
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Evaluate multiple design alternatives against weighted criteria.
        /// </summary>
        public DecisionAnalysis EvaluateAlternatives(
            List<DesignAlternative> alternatives,
            Dictionary<string, double> criteriaWeights)
        {
            var analysis = new DecisionAnalysis
            {
                Timestamp = DateTime.UtcNow,
                Alternatives = alternatives,
                CriteriaWeights = criteriaWeights
            };

            // Normalize criteria values
            var normalizedScores = NormalizeScores(alternatives);

            // Calculate weighted scores
            foreach (var alternative in alternatives)
            {
                var weightedScore = CalculateWeightedScore(
                    alternative.AlternativeId,
                    normalizedScores,
                    criteriaWeights);

                analysis.WeightedScores[alternative.AlternativeId] = weightedScore;
            }

            // Rank alternatives
            analysis.Ranking = analysis.WeightedScores
                .OrderByDescending(s => s.Value)
                .Select((kvp, idx) => new AlternativeRank
                {
                    AlternativeId = kvp.Key,
                    Rank = idx + 1,
                    Score = kvp.Value
                })
                .ToList();

            // Identify trade-offs
            analysis.TradeOffs = IdentifyTradeOffs(alternatives, criteriaWeights);

            // Calculate sensitivity
            analysis.Sensitivity = CalculateSensitivity(alternatives, criteriaWeights);

            // Generate recommendation
            analysis.Recommendation = GenerateRecommendation(analysis);

            return analysis;
        }

        /// <summary>
        /// Perform sensitivity analysis on criteria weights.
        /// </summary>
        public SensitivityAnalysis AnalyzeSensitivity(
            List<DesignAlternative> alternatives,
            Dictionary<string, double> baseWeights,
            string criterionToVary)
        {
            var sensitivity = new SensitivityAnalysis
            {
                CriterionVaried = criterionToVary,
                BaseWeights = baseWeights,
                Results = new List<SensitivityResult>()
            };

            // Vary criterion weight from 0 to 1
            for (double weight = 0; weight <= 1.0; weight += 0.1)
            {
                var adjustedWeights = AdjustWeights(baseWeights, criterionToVary, weight);
                var normalizedScores = NormalizeScores(alternatives);

                var ranking = alternatives
                    .Select(a => new
                    {
                        Id = a.AlternativeId,
                        Score = CalculateWeightedScore(a.AlternativeId, normalizedScores, adjustedWeights)
                    })
                    .OrderByDescending(x => x.Score)
                    .Select((x, idx) => new { x.Id, Rank = idx + 1 })
                    .ToList();

                sensitivity.Results.Add(new SensitivityResult
                {
                    CriterionWeight = weight,
                    TopAlternative = ranking.First().Id,
                    Rankings = ranking.ToDictionary(r => r.Id, r => r.Rank)
                });
            }

            // Find threshold points
            sensitivity.ThresholdPoints = FindThresholdPoints(sensitivity.Results);

            return sensitivity;
        }

        /// <summary>
        /// Analyze trade-offs between two specific criteria.
        /// </summary>
        public TradeOffAnalysis AnalyzeTradeOff(
            List<DesignAlternative> alternatives,
            string criterion1,
            string criterion2)
        {
            var analysis = new TradeOffAnalysis
            {
                Criterion1 = criterion1,
                Criterion2 = criterion2,
                Timestamp = DateTime.UtcNow
            };

            // Get criterion definitions
            var crit1 = _criteria.GetValueOrDefault(criterion1);
            var crit2 = _criteria.GetValueOrDefault(criterion2);

            if (crit1 == null || crit2 == null)
            {
                analysis.Message = "One or both criteria not found";
                return analysis;
            }

            // Plot alternatives on trade-off curve
            analysis.DataPoints = alternatives.Select(a => new TradeOffPoint
            {
                AlternativeId = a.AlternativeId,
                Value1 = a.CriteriaValues.GetValueOrDefault(criterion1, 0),
                Value2 = a.CriteriaValues.GetValueOrDefault(criterion2, 0)
            }).ToList();

            // Identify Pareto frontier
            analysis.ParetoFrontier = IdentifyParetoFrontier(analysis.DataPoints, crit1, crit2);

            // Get trade-off rule if exists
            var rule = _tradeOffRules.Values.FirstOrDefault(r =>
                (r.Criteria1 == criterion1 && r.Criteria2 == criterion2) ||
                (r.Criteria1 == criterion2 && r.Criteria2 == criterion1));

            if (rule != null)
            {
                analysis.TradeOffGuidance = rule.Guidance;
                analysis.TypicalRatio = rule.TypicalRatio;
            }

            return analysis;
        }

        /// <summary>
        /// Assess risks associated with a design alternative.
        /// </summary>
        public RiskAssessment AssessRisks(DesignAlternative alternative, ProjectContext context)
        {
            var assessment = new RiskAssessment
            {
                AlternativeId = alternative.AlternativeId,
                Timestamp = DateTime.UtcNow,
                IdentifiedRisks = new List<IdentifiedRisk>()
            };

            foreach (var riskFactor in _riskFactors.Values)
            {
                var triggered = CheckRiskTriggers(alternative, riskFactor, context);

                if (triggered.Any())
                {
                    assessment.IdentifiedRisks.Add(new IdentifiedRisk
                    {
                        RiskId = riskFactor.RiskId,
                        RiskName = riskFactor.Name,
                        Category = riskFactor.Category,
                        TriggeredBy = triggered,
                        Mitigations = riskFactor.Mitigations.ToList(),
                        ImpactAreas = riskFactor.ImpactAreas.ToList(),
                        Severity = CalculateRiskSeverity(triggered.Count, riskFactor)
                    });
                }
            }

            // Calculate overall risk score
            assessment.OverallRiskScore = assessment.IdentifiedRisks.Any()
                ? assessment.IdentifiedRisks.Average(r => (double)r.Severity)
                : 0;

            assessment.RiskLevel = assessment.OverallRiskScore switch
            {
                < 2 => RiskLevel.Low,
                < 4 => RiskLevel.Medium,
                < 6 => RiskLevel.High,
                _ => RiskLevel.VeryHigh
            };

            return assessment;
        }

        /// <summary>
        /// Get a decision template for common decisions.
        /// </summary>
        public DecisionTemplate GetTemplate(string templateId)
        {
            return _templates.FirstOrDefault(t => t.TemplateId == templateId);
        }

        /// <summary>
        /// Get all available decision templates.
        /// </summary>
        public List<DecisionTemplate> GetAllTemplates()
        {
            return _templates.ToList();
        }

        /// <summary>
        /// Generate a decision report.
        /// </summary>
        public DecisionReport GenerateReport(DecisionAnalysis analysis)
        {
            var report = new DecisionReport
            {
                Title = "Design Decision Analysis Report",
                Timestamp = DateTime.UtcNow,
                Sections = new List<ReportSection>()
            };

            // Executive summary
            report.Sections.Add(new ReportSection
            {
                Title = "Executive Summary",
                Content = GenerateExecutiveSummary(analysis)
            });

            // Alternatives overview
            report.Sections.Add(new ReportSection
            {
                Title = "Alternatives Evaluated",
                Content = GenerateAlternativesOverview(analysis)
            });

            // Criteria and weights
            report.Sections.Add(new ReportSection
            {
                Title = "Evaluation Criteria",
                Content = GenerateCriteriaSection(analysis)
            });

            // Results
            report.Sections.Add(new ReportSection
            {
                Title = "Analysis Results",
                Content = GenerateResultsSection(analysis)
            });

            // Trade-offs
            report.Sections.Add(new ReportSection
            {
                Title = "Trade-off Analysis",
                Content = GenerateTradeOffSection(analysis)
            });

            // Sensitivity
            report.Sections.Add(new ReportSection
            {
                Title = "Sensitivity Analysis",
                Content = GenerateSensitivitySection(analysis)
            });

            // Recommendation
            report.Sections.Add(new ReportSection
            {
                Title = "Recommendation",
                Content = analysis.Recommendation.Summary
            });

            return report;
        }

        /// <summary>
        /// Runs Monte Carlo sensitivity analysis by randomly perturbing criteria weights
        /// from probability distributions and evaluating ranking stability.
        /// Returns confidence intervals on rankings and win frequencies.
        /// </summary>
        /// <param name="alternatives">Design alternatives to evaluate</param>
        /// <param name="baseWeights">Base criteria weights (mean of distribution)</param>
        /// <param name="simulations">Number of Monte Carlo iterations (default 10,000)</param>
        /// <param name="perturbationStdDev">Standard deviation of weight perturbation as fraction of weight (default 0.15 = 15%)</param>
        public MonteCarloSensitivityResult RunMonteCarloSensitivity(
            List<DesignAlternative> alternatives,
            Dictionary<string, double> baseWeights,
            int simulations = 10000,
            double perturbationStdDev = 0.15)
        {
            var result = new MonteCarloSensitivityResult
            {
                Simulations = simulations,
                PerturbationStdDev = perturbationStdDev,
                BaseWeights = new Dictionary<string, double>(baseWeights)
            };

            var normalizedScores = NormalizeScores(alternatives);
            var rng = new Random(42); // Fixed seed for reproducibility

            // Track per-alternative statistics
            var winCounts = new Dictionary<string, int>();
            var scoreAccumulators = new Dictionary<string, List<double>>();
            var rankAccumulators = new Dictionary<string, List<int>>();

            foreach (var alt in alternatives)
            {
                winCounts[alt.AlternativeId] = 0;
                scoreAccumulators[alt.AlternativeId] = new List<double>(simulations);
                rankAccumulators[alt.AlternativeId] = new List<int>(simulations);
            }

            // Run Monte Carlo simulations
            for (int sim = 0; sim < simulations; sim++)
            {
                // Perturb weights using normal distribution
                var perturbedWeights = new Dictionary<string, double>();
                foreach (var kvp in baseWeights)
                {
                    // Box-Muller transform for normal distribution
                    double u1 = 1.0 - rng.NextDouble();
                    double u2 = 1.0 - rng.NextDouble();
                    double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

                    double perturbedWeight = kvp.Value * (1.0 + perturbationStdDev * normal);
                    perturbedWeights[kvp.Key] = Math.Max(0.01, perturbedWeight); // Ensure positive
                }

                // Normalize perturbed weights to sum to 1
                double totalWeight = perturbedWeights.Values.Sum();
                foreach (var key in perturbedWeights.Keys.ToList())
                {
                    perturbedWeights[key] /= totalWeight;
                }

                // Evaluate all alternatives with perturbed weights
                var scores = new List<(string Id, double Score)>();
                foreach (var alt in alternatives)
                {
                    double score = CalculateWeightedScore(alt.AlternativeId, normalizedScores, perturbedWeights);
                    scores.Add((alt.AlternativeId, score));
                    scoreAccumulators[alt.AlternativeId].Add(score);
                }

                // Rank
                var ranked = scores.OrderByDescending(s => s.Score).ToList();
                for (int r = 0; r < ranked.Count; r++)
                {
                    rankAccumulators[ranked[r].Id].Add(r + 1);
                }

                // Track winner
                winCounts[ranked[0].Id]++;
            }

            // Compute statistics for each alternative
            foreach (var alt in alternatives)
            {
                var altScores = scoreAccumulators[alt.AlternativeId];
                var altRanks = rankAccumulators[alt.AlternativeId];

                altScores.Sort();
                altRanks.Sort();

                int p5Index = (int)(simulations * 0.05);
                int p25Index = (int)(simulations * 0.25);
                int p50Index = (int)(simulations * 0.50);
                int p75Index = (int)(simulations * 0.75);
                int p95Index = (int)(simulations * 0.95);

                double meanScore = altScores.Average();
                double variance = altScores.Sum(s => (s - meanScore) * (s - meanScore)) / simulations;

                result.AlternativeStats.Add(new AlternativeStatistics
                {
                    AlternativeId = alt.AlternativeId,
                    AlternativeName = alt.Name,
                    WinCount = winCounts[alt.AlternativeId],
                    WinProbability = (double)winCounts[alt.AlternativeId] / simulations,
                    MeanScore = meanScore,
                    ScoreStdDev = Math.Sqrt(variance),
                    ScoreP5 = altScores[p5Index],
                    ScoreP25 = altScores[p25Index],
                    ScoreMedian = altScores[p50Index],
                    ScoreP75 = altScores[p75Index],
                    ScoreP95 = altScores[p95Index],
                    MeanRank = altRanks.Average(),
                    RankP5 = altRanks[p5Index],
                    RankP95 = altRanks[p95Index],
                    MostFrequentRank = altRanks.GroupBy(r => r).OrderByDescending(g => g.Count()).First().Key
                });
            }

            // Sort by win probability descending
            result.AlternativeStats = result.AlternativeStats
                .OrderByDescending(a => a.WinProbability)
                .ToList();

            // Compute robustness: how often does the base winner also win in Monte Carlo?
            var baseNormalized = NormalizeScores(alternatives);
            var baseWinner = alternatives
                .OrderByDescending(a => CalculateWeightedScore(a.AlternativeId, baseNormalized, baseWeights))
                .First().AlternativeId;

            result.BaseWinner = baseWinner;
            result.BaseWinnerRobustness = (double)winCounts[baseWinner] / simulations;

            return result;
        }

        /// <summary>
        /// Get guidance on a specific trade-off.
        /// </summary>
        public TradeOffGuidance GetTradeOffGuidance(string criterion1, string criterion2)
        {
            var guidance = new TradeOffGuidance
            {
                Criterion1 = criterion1,
                Criterion2 = criterion2
            };

            var rule = _tradeOffRules.Values.FirstOrDefault(r =>
                (r.Criteria1 == criterion1 && r.Criteria2 == criterion2) ||
                (r.Criteria1 == criterion2 && r.Criteria2 == criterion1));

            if (rule != null)
            {
                guidance.HasRule = true;
                guidance.Relationship = rule.Relationship;
                guidance.TypicalRatio = rule.TypicalRatio;
                guidance.Guidance = rule.Guidance;
            }
            else
            {
                guidance.HasRule = false;
                guidance.Guidance = "No specific guidance available for this trade-off combination";
            }

            // Add general strategies
            guidance.Strategies = GetTradeOffStrategies(criterion1, criterion2);

            return guidance;
        }

        #endregion

        #region Private Methods

        private void AddCriterion(DecisionCriterion criterion)
        {
            _criteria[criterion.CriterionId] = criterion;
        }

        private void AddTradeOffRule(TradeOffRule rule)
        {
            _tradeOffRules[rule.RuleId] = rule;
        }

        private void AddRiskFactor(RiskFactor factor)
        {
            _riskFactors[factor.RiskId] = factor;
        }

        private Dictionary<string, Dictionary<string, double>> NormalizeScores(
            List<DesignAlternative> alternatives)
        {
            var normalized = new Dictionary<string, Dictionary<string, double>>();

            // Get all criteria used
            var allCriteria = alternatives
                .SelectMany(a => a.CriteriaValues.Keys)
                .Distinct()
                .ToList();

            foreach (var criterion in allCriteria)
            {
                var values = alternatives
                    .Where(a => a.CriteriaValues.ContainsKey(criterion))
                    .Select(a => a.CriteriaValues[criterion])
                    .ToList();

                if (!values.Any()) continue;

                var min = values.Min();
                var max = values.Max();
                var range = max - min;

                var critDef = _criteria.GetValueOrDefault(criterion);
                var maximize = critDef?.Direction == OptimizationDirection.Maximize;

                foreach (var alt in alternatives)
                {
                    if (!normalized.ContainsKey(alt.AlternativeId))
                        normalized[alt.AlternativeId] = new Dictionary<string, double>();

                    if (alt.CriteriaValues.TryGetValue(criterion, out var value))
                    {
                        var normalizedValue = range > 0 ? (value - min) / range : 0.5;
                        // Flip if minimize is better
                        if (!maximize)
                            normalizedValue = 1 - normalizedValue;

                        normalized[alt.AlternativeId][criterion] = normalizedValue;
                    }
                }
            }

            return normalized;
        }

        private double CalculateWeightedScore(
            string alternativeId,
            Dictionary<string, Dictionary<string, double>> normalizedScores,
            Dictionary<string, double> weights)
        {
            if (!normalizedScores.TryGetValue(alternativeId, out var scores))
                return 0;

            double totalScore = 0;
            double totalWeight = 0;

            foreach (var weight in weights)
            {
                if (scores.TryGetValue(weight.Key, out var score))
                {
                    totalScore += score * weight.Value;
                    totalWeight += weight.Value;
                }
            }

            return totalWeight > 0 ? totalScore / totalWeight : 0;
        }

        private List<TradeOffInsight> IdentifyTradeOffs(
            List<DesignAlternative> alternatives,
            Dictionary<string, double> weights)
        {
            var insights = new List<TradeOffInsight>();

            var topAlternative = alternatives
                .OrderByDescending(a => a.CriteriaValues.Sum(c =>
                    c.Value * weights.GetValueOrDefault(c.Key, 0)))
                .FirstOrDefault();

            if (topAlternative == null) return insights;

            foreach (var rule in _tradeOffRules.Values)
            {
                var val1 = topAlternative.CriteriaValues.GetValueOrDefault(rule.Criteria1);
                var val2 = topAlternative.CriteriaValues.GetValueOrDefault(rule.Criteria2);

                if (val1 > 0 && val2 > 0)
                {
                    insights.Add(new TradeOffInsight
                    {
                        Criterion1 = rule.Criteria1,
                        Criterion2 = rule.Criteria2,
                        Relationship = rule.Relationship,
                        Guidance = rule.Guidance
                    });
                }
            }

            return insights.Take(3).ToList();
        }

        private Dictionary<string, double> CalculateSensitivity(
            List<DesignAlternative> alternatives,
            Dictionary<string, double> weights)
        {
            var sensitivity = new Dictionary<string, double>();

            foreach (var criterion in weights.Keys)
            {
                // Calculate how much the ranking changes with weight changes
                var highWeights = AdjustWeights(weights, criterion, weights[criterion] * 1.2);
                var lowWeights = AdjustWeights(weights, criterion, weights[criterion] * 0.8);

                var normalizedScores = NormalizeScores(alternatives);

                var baseRanking = alternatives
                    .OrderByDescending(a => CalculateWeightedScore(a.AlternativeId, normalizedScores, weights))
                    .Select(a => a.AlternativeId)
                    .ToList();

                var highRanking = alternatives
                    .OrderByDescending(a => CalculateWeightedScore(a.AlternativeId, normalizedScores, highWeights))
                    .Select(a => a.AlternativeId)
                    .ToList();

                var lowRanking = alternatives
                    .OrderByDescending(a => CalculateWeightedScore(a.AlternativeId, normalizedScores, lowWeights))
                    .Select(a => a.AlternativeId)
                    .ToList();

                // Calculate ranking change
                double rankChange = 0;
                for (int i = 0; i < baseRanking.Count; i++)
                {
                    var highPos = highRanking.IndexOf(baseRanking[i]);
                    var lowPos = lowRanking.IndexOf(baseRanking[i]);
                    rankChange += Math.Abs(highPos - i) + Math.Abs(lowPos - i);
                }

                sensitivity[criterion] = rankChange / (2.0 * baseRanking.Count);
            }

            return sensitivity;
        }

        private Dictionary<string, double> AdjustWeights(
            Dictionary<string, double> baseWeights,
            string criterionToAdjust,
            double newWeight)
        {
            var adjusted = new Dictionary<string, double>(baseWeights);
            var oldWeight = adjusted.GetValueOrDefault(criterionToAdjust, 0);
            var weightDiff = newWeight - oldWeight;

            // Adjust target criterion
            adjusted[criterionToAdjust] = newWeight;

            // Proportionally adjust others
            var otherTotal = adjusted.Where(w => w.Key != criterionToAdjust).Sum(w => w.Value);
            if (otherTotal > 0)
            {
                foreach (var key in adjusted.Keys.Where(k => k != criterionToAdjust).ToList())
                {
                    adjusted[key] = adjusted[key] * (1 - weightDiff / (otherTotal + oldWeight));
                }
            }

            // Normalize
            var total = adjusted.Values.Sum();
            if (total > 0)
            {
                foreach (var key in adjusted.Keys.ToList())
                {
                    adjusted[key] /= total;
                }
            }

            return adjusted;
        }

        private List<ThresholdPoint> FindThresholdPoints(List<SensitivityResult> results)
        {
            var thresholds = new List<ThresholdPoint>();

            for (int i = 1; i < results.Count; i++)
            {
                if (results[i].TopAlternative != results[i - 1].TopAlternative)
                {
                    thresholds.Add(new ThresholdPoint
                    {
                        Weight = (results[i].CriterionWeight + results[i - 1].CriterionWeight) / 2,
                        FromAlternative = results[i - 1].TopAlternative,
                        ToAlternative = results[i].TopAlternative
                    });
                }
            }

            return thresholds;
        }

        private List<TradeOffPoint> IdentifyParetoFrontier(
            List<TradeOffPoint> points,
            DecisionCriterion crit1,
            DecisionCriterion crit2)
        {
            var frontier = new List<TradeOffPoint>();

            foreach (var point in points)
            {
                var dominated = points.Any(p =>
                    p.AlternativeId != point.AlternativeId &&
                    IsBetterOrEqual(p.Value1, point.Value1, crit1.Direction) &&
                    IsBetterOrEqual(p.Value2, point.Value2, crit2.Direction) &&
                    (IsBetter(p.Value1, point.Value1, crit1.Direction) ||
                     IsBetter(p.Value2, point.Value2, crit2.Direction)));

                if (!dominated)
                    frontier.Add(point);
            }

            return frontier;
        }

        private bool IsBetterOrEqual(double a, double b, OptimizationDirection direction)
        {
            return direction == OptimizationDirection.Maximize ? a >= b : a <= b;
        }

        private bool IsBetter(double a, double b, OptimizationDirection direction)
        {
            return direction == OptimizationDirection.Maximize ? a > b : a < b;
        }

        private List<string> CheckRiskTriggers(
            DesignAlternative alternative,
            RiskFactor riskFactor,
            ProjectContext context)
        {
            var triggered = new List<string>();

            // Check alternative characteristics against triggers
            if (alternative.Characteristics != null)
            {
                foreach (var trigger in riskFactor.Triggers)
                {
                    if (alternative.Characteristics.Any(c =>
                        c.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
                    {
                        triggered.Add(trigger);
                    }
                }
            }

            return triggered;
        }

        private RiskSeverity CalculateRiskSeverity(int triggerCount, RiskFactor factor)
        {
            var severity = triggerCount * 2;
            severity += factor.ImpactAreas.Length;

            return severity switch
            {
                <= 2 => RiskSeverity.Low,
                <= 4 => RiskSeverity.Medium,
                <= 6 => RiskSeverity.High,
                _ => RiskSeverity.Critical
            };
        }

        private DecisionRecommendation GenerateRecommendation(DecisionAnalysis analysis)
        {
            var recommendation = new DecisionRecommendation();

            var topRanked = analysis.Ranking.FirstOrDefault();
            if (topRanked == null)
            {
                recommendation.Summary = "Insufficient data for recommendation";
                return recommendation;
            }

            var topAlternative = analysis.Alternatives
                .FirstOrDefault(a => a.AlternativeId == topRanked.AlternativeId);

            recommendation.RecommendedAlternative = topRanked.AlternativeId;
            recommendation.Confidence = CalculateRecommendationConfidence(analysis);

            var parts = new List<string>
            {
                $"Based on the weighted criteria analysis, {topAlternative?.Name ?? topRanked.AlternativeId} " +
                $"is recommended with a score of {topRanked.Score:F2}."
            };

            // Add trade-off considerations
            if (analysis.TradeOffs.Any())
            {
                parts.Add($"Key trade-offs to consider: {analysis.TradeOffs.First().Guidance}");
            }

            // Add sensitivity note
            var mostSensitive = analysis.Sensitivity
                .OrderByDescending(s => s.Value)
                .FirstOrDefault();

            if (mostSensitive.Value > 0.3)
            {
                parts.Add($"Note: Results are sensitive to {mostSensitive.Key} weighting.");
            }

            recommendation.Summary = string.Join(" ", parts);
            recommendation.Strengths = GetAlternativeStrengths(topAlternative, analysis);
            recommendation.Weaknesses = GetAlternativeWeaknesses(topAlternative, analysis);

            return recommendation;
        }

        private double CalculateRecommendationConfidence(DecisionAnalysis analysis)
        {
            if (analysis.Ranking.Count < 2) return 1.0;

            var scoreDiff = analysis.Ranking[0].Score - analysis.Ranking[1].Score;
            var avgSensitivity = analysis.Sensitivity.Values.Average();

            // Higher score difference = higher confidence
            // Lower sensitivity = higher confidence
            return Math.Min(1.0, scoreDiff * 2 + (1 - avgSensitivity));
        }

        private List<string> GetAlternativeStrengths(DesignAlternative alt, DecisionAnalysis analysis)
        {
            if (alt == null) return new List<string>();

            var strengths = new List<string>();
            var normalized = NormalizeScores(analysis.Alternatives);

            if (normalized.TryGetValue(alt.AlternativeId, out var scores))
            {
                var topScores = scores.OrderByDescending(s => s.Value).Take(3);
                foreach (var score in topScores)
                {
                    if (score.Value > 0.7)
                        strengths.Add($"Strong {score.Key} performance");
                }
            }

            return strengths;
        }

        private List<string> GetAlternativeWeaknesses(DesignAlternative alt, DecisionAnalysis analysis)
        {
            if (alt == null) return new List<string>();

            var weaknesses = new List<string>();
            var normalized = NormalizeScores(analysis.Alternatives);

            if (normalized.TryGetValue(alt.AlternativeId, out var scores))
            {
                var lowScores = scores.OrderBy(s => s.Value).Take(2);
                foreach (var score in lowScores)
                {
                    if (score.Value < 0.3)
                        weaknesses.Add($"Lower {score.Key} compared to alternatives");
                }
            }

            return weaknesses;
        }

        private List<string> GetTradeOffStrategies(string criterion1, string criterion2)
        {
            return new List<string>
            {
                "Consider phased implementation to spread costs",
                "Explore value engineering opportunities",
                "Look for synergies that improve both criteria",
                "Conduct lifecycle cost analysis for long-term perspective"
            };
        }

        // Report generation methods
        private string GenerateExecutiveSummary(DecisionAnalysis analysis)
        {
            return $"Analysis of {analysis.Alternatives.Count} alternatives against " +
                   $"{analysis.CriteriaWeights.Count} weighted criteria. " +
                   analysis.Recommendation.Summary;
        }

        private string GenerateAlternativesOverview(DecisionAnalysis analysis)
        {
            return string.Join("\n", analysis.Alternatives.Select(a =>
                $"- {a.Name}: {a.Description}"));
        }

        private string GenerateCriteriaSection(DecisionAnalysis analysis)
        {
            return string.Join("\n", analysis.CriteriaWeights.Select(w =>
                $"- {w.Key}: {w.Value:P0} weight"));
        }

        private string GenerateResultsSection(DecisionAnalysis analysis)
        {
            return string.Join("\n", analysis.Ranking.Select(r =>
                $"{r.Rank}. {r.AlternativeId}: {r.Score:F3}"));
        }

        private string GenerateTradeOffSection(DecisionAnalysis analysis)
        {
            if (!analysis.TradeOffs.Any())
                return "No significant trade-offs identified.";

            return string.Join("\n", analysis.TradeOffs.Select(t =>
                $"- {t.Criterion1} vs {t.Criterion2}: {t.Guidance}"));
        }

        private string GenerateSensitivitySection(DecisionAnalysis analysis)
        {
            var sorted = analysis.Sensitivity.OrderByDescending(s => s.Value);
            return string.Join("\n", sorted.Select(s =>
                $"- {s.Key}: {(s.Value > 0.3 ? "High" : s.Value > 0.15 ? "Medium" : "Low")} sensitivity"));
        }

        #endregion
    }

    #region Supporting Types

    public class DecisionCriterion
    {
        public string CriterionId { get; set; }
        public string Name { get; set; }
        public CriterionCategory Category { get; set; }
        public string Unit { get; set; }
        public OptimizationDirection Direction { get; set; }
        public double Weight { get; set; }
        public string Description { get; set; }
    }

    public class TradeOffRule
    {
        public string RuleId { get; set; }
        public string Criteria1 { get; set; }
        public string Criteria2 { get; set; }
        public TradeOffRelationship Relationship { get; set; }
        public double TypicalRatio { get; set; }
        public string Guidance { get; set; }
    }

    public class RiskFactor
    {
        public string RiskId { get; set; }
        public string Name { get; set; }
        public RiskCategory Category { get; set; }
        public string[] Triggers { get; set; }
        public string[] Mitigations { get; set; }
        public string[] ImpactAreas { get; set; }
    }

    public class DecisionTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] RelevantCriteria { get; set; }
        public AlternativeTemplate[] TypicalAlternatives { get; set; }
        public Dictionary<string, double> DefaultWeights { get; set; }
    }

    public class AlternativeTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class DesignAlternative
    {
        public string AlternativeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, double> CriteriaValues { get; set; } = new();
        public List<string> Characteristics { get; set; } = new();
    }

    public class ProjectContext
    {
        public string Region { get; set; }
        public string BuildingType { get; set; }
        public double Budget { get; set; }
        public int Duration { get; set; }
    }

    public class DecisionAnalysis
    {
        public DateTime Timestamp { get; set; }
        public List<DesignAlternative> Alternatives { get; set; }
        public Dictionary<string, double> CriteriaWeights { get; set; }
        public Dictionary<string, double> WeightedScores { get; set; } = new();
        public List<AlternativeRank> Ranking { get; set; } = new();
        public List<TradeOffInsight> TradeOffs { get; set; } = new();
        public Dictionary<string, double> Sensitivity { get; set; } = new();
        public DecisionRecommendation Recommendation { get; set; }
    }

    public class AlternativeRank
    {
        public string AlternativeId { get; set; }
        public int Rank { get; set; }
        public double Score { get; set; }
    }

    public class TradeOffInsight
    {
        public string Criterion1 { get; set; }
        public string Criterion2 { get; set; }
        public TradeOffRelationship Relationship { get; set; }
        public string Guidance { get; set; }
    }

    public class DecisionRecommendation
    {
        public string RecommendedAlternative { get; set; }
        public double Confidence { get; set; }
        public string Summary { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
    }

    public class SensitivityAnalysis
    {
        public string CriterionVaried { get; set; }
        public Dictionary<string, double> BaseWeights { get; set; }
        public List<SensitivityResult> Results { get; set; }
        public List<ThresholdPoint> ThresholdPoints { get; set; }
    }

    public class SensitivityResult
    {
        public double CriterionWeight { get; set; }
        public string TopAlternative { get; set; }
        public Dictionary<string, int> Rankings { get; set; }
    }

    public class ThresholdPoint
    {
        public double Weight { get; set; }
        public string FromAlternative { get; set; }
        public string ToAlternative { get; set; }
    }

    public class TradeOffAnalysis
    {
        public string Criterion1 { get; set; }
        public string Criterion2 { get; set; }
        public DateTime Timestamp { get; set; }
        public List<TradeOffPoint> DataPoints { get; set; }
        public List<TradeOffPoint> ParetoFrontier { get; set; }
        public string TradeOffGuidance { get; set; }
        public double TypicalRatio { get; set; }
        public string Message { get; set; }
    }

    public class TradeOffPoint
    {
        public string AlternativeId { get; set; }
        public double Value1 { get; set; }
        public double Value2 { get; set; }
    }

    public class RiskAssessment
    {
        public string AlternativeId { get; set; }
        public DateTime Timestamp { get; set; }
        public List<IdentifiedRisk> IdentifiedRisks { get; set; }
        public double OverallRiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class IdentifiedRisk
    {
        public string RiskId { get; set; }
        public string RiskName { get; set; }
        public RiskCategory Category { get; set; }
        public List<string> TriggeredBy { get; set; }
        public List<string> Mitigations { get; set; }
        public List<string> ImpactAreas { get; set; }
        public RiskSeverity Severity { get; set; }
    }

    public class TradeOffGuidance
    {
        public string Criterion1 { get; set; }
        public string Criterion2 { get; set; }
        public bool HasRule { get; set; }
        public TradeOffRelationship Relationship { get; set; }
        public double TypicalRatio { get; set; }
        public string Guidance { get; set; }
        public List<string> Strategies { get; set; }
    }

    public class MonteCarloSensitivityResult
    {
        public int Simulations { get; set; }
        public double PerturbationStdDev { get; set; }
        public Dictionary<string, double> BaseWeights { get; set; }
        public string BaseWinner { get; set; }
        public double BaseWinnerRobustness { get; set; }
        public List<AlternativeStatistics> AlternativeStats { get; set; } = new();
    }

    public class AlternativeStatistics
    {
        public string AlternativeId { get; set; }
        public string AlternativeName { get; set; }
        public int WinCount { get; set; }
        public double WinProbability { get; set; }
        public double MeanScore { get; set; }
        public double ScoreStdDev { get; set; }
        public double ScoreP5 { get; set; }
        public double ScoreP25 { get; set; }
        public double ScoreMedian { get; set; }
        public double ScoreP75 { get; set; }
        public double ScoreP95 { get; set; }
        public double MeanRank { get; set; }
        public int RankP5 { get; set; }
        public int RankP95 { get; set; }
        public int MostFrequentRank { get; set; }
    }

    public class DecisionReport
    {
        public string Title { get; set; }
        public DateTime Timestamp { get; set; }
        public List<ReportSection> Sections { get; set; }
    }

    public class ReportSection
    {
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public enum CriterionCategory
    {
        Cost,
        Performance,
        Sustainability,
        Schedule,
        Quality,
        Risk
    }

    public enum OptimizationDirection
    {
        Minimize,
        Maximize
    }

    public enum TradeOffRelationship
    {
        Proportional,
        Inverse,
        Complex,
        Independent
    }

    public enum RiskCategory
    {
        Technical,
        Financial,
        SupplyChain,
        Regulatory,
        Environmental,
        Operational
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    public enum RiskSeverity
    {
        Low = 1,
        Medium = 3,
        High = 5,
        Critical = 7
    }

    #endregion
}
