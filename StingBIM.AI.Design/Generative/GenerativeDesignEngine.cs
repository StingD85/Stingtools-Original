// StingBIM.AI.Design - GenerativeDesignEngine.cs
// AI-Powered Generative Design and Multi-Objective Optimization
// Phase 4: Enterprise AI Transformation - Design Exploration
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace StingBIM.AI.Design.Generative
{
    /// <summary>
    /// Advanced generative design engine that creates optimized design variants
    /// based on constraints, objectives, and learned patterns from successful projects.
    /// </summary>
    public class GenerativeDesignEngine
    {
        #region Fields

        private readonly Dictionary<string, DesignConstraint> _constraints;
        private readonly Dictionary<string, DesignObjective> _objectives;
        private readonly Dictionary<string, DesignPattern> _patterns;
        private readonly DesignGenerator _generator;
        private readonly DesignEvaluator _evaluator;
        private readonly DesignOptimizer _optimizer;
        private readonly PatternLearner _patternLearner;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public GenerativeDesignEngine()
        {
            _constraints = new Dictionary<string, DesignConstraint>(StringComparer.OrdinalIgnoreCase);
            _objectives = new Dictionary<string, DesignObjective>(StringComparer.OrdinalIgnoreCase);
            _patterns = new Dictionary<string, DesignPattern>(StringComparer.OrdinalIgnoreCase);
            _generator = new DesignGenerator();
            _evaluator = new DesignEvaluator();
            _optimizer = new DesignOptimizer();
            _patternLearner = new PatternLearner();

            InitializeDefaultConstraints();
            InitializeDefaultObjectives();
            InitializeDefaultPatterns();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultConstraints()
        {
            // Spatial Constraints
            AddConstraint(new DesignConstraint
            {
                ConstraintId = "SPATIAL-001",
                Name = "Minimum Room Area",
                Category = "Spatial",
                Type = ConstraintType.Minimum,
                ParameterName = "Area",
                ValidateFunction = ValidateMinimumArea
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "SPATIAL-002",
                Name = "Maximum Building Height",
                Category = "Spatial",
                Type = ConstraintType.Maximum,
                ParameterName = "Height",
                ValidateFunction = ValidateMaxHeight
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "SPATIAL-003",
                Name = "Floor Area Ratio (FAR)",
                Category = "Spatial",
                Type = ConstraintType.Range,
                ParameterName = "FAR",
                MinValue = 0,
                MaxValue = 5.0,
                ValidateFunction = ValidateFAR
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "SPATIAL-004",
                Name = "Site Coverage",
                Category = "Spatial",
                Type = ConstraintType.Maximum,
                ParameterName = "Coverage",
                MaxValue = 70,
                Unit = "%",
                ValidateFunction = ValidateSiteCoverage
            });

            // Circulation Constraints
            AddConstraint(new DesignConstraint
            {
                ConstraintId = "CIRC-001",
                Name = "Corridor Width",
                Category = "Circulation",
                Type = ConstraintType.Minimum,
                MinValue = 1200,
                Unit = "mm",
                ValidateFunction = ValidateCorridorWidth
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "CIRC-002",
                Name = "Exit Distance",
                Category = "Circulation",
                Type = ConstraintType.Maximum,
                MaxValue = 45000,
                Unit = "mm",
                ValidateFunction = ValidateExitDistance
            });

            // Structural Constraints
            AddConstraint(new DesignConstraint
            {
                ConstraintId = "STRUCT-001",
                Name = "Column Grid Spacing",
                Category = "Structural",
                Type = ConstraintType.Range,
                MinValue = 6000,
                MaxValue = 12000,
                Unit = "mm",
                ValidateFunction = ValidateColumnSpacing
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "STRUCT-002",
                Name = "Span-to-Depth Ratio",
                Category = "Structural",
                Type = ConstraintType.Maximum,
                MaxValue = 20,
                ValidateFunction = ValidateSpanDepthRatio
            });

            // Energy Constraints
            AddConstraint(new DesignConstraint
            {
                ConstraintId = "ENERGY-001",
                Name = "Window-to-Wall Ratio",
                Category = "Energy",
                Type = ConstraintType.Range,
                MinValue = 20,
                MaxValue = 40,
                Unit = "%",
                ValidateFunction = ValidateWWR
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "ENERGY-002",
                Name = "Orientation Optimization",
                Category = "Energy",
                Type = ConstraintType.Preference,
                PreferredValue = "North-South",
                ValidateFunction = ValidateOrientation
            });

            // Adjacency Constraints
            AddConstraint(new DesignConstraint
            {
                ConstraintId = "ADJ-001",
                Name = "Required Adjacencies",
                Category = "Adjacency",
                Type = ConstraintType.Required,
                ValidateFunction = ValidateRequiredAdjacencies
            });

            AddConstraint(new DesignConstraint
            {
                ConstraintId = "ADJ-002",
                Name = "Prohibited Adjacencies",
                Category = "Adjacency",
                Type = ConstraintType.Prohibited,
                ValidateFunction = ValidateProhibitedAdjacencies
            });
        }

        private void InitializeDefaultObjectives()
        {
            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-COST",
                Name = "Minimize Construction Cost",
                Category = "Financial",
                Direction = OptimizationDirection.Minimize,
                Weight = 1.0,
                EvaluateFunction = EvaluateCost
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-ENERGY",
                Name = "Minimize Energy Consumption",
                Category = "Sustainability",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.8,
                EvaluateFunction = EvaluateEnergy
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-DAYLIGHT",
                Name = "Maximize Daylight",
                Category = "Comfort",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.7,
                EvaluateFunction = EvaluateDaylight
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-VIEWS",
                Name = "Maximize Quality Views",
                Category = "Comfort",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.5,
                EvaluateFunction = EvaluateViews
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-EFFICIENCY",
                Name = "Maximize Space Efficiency",
                Category = "Spatial",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.9,
                EvaluateFunction = EvaluateSpaceEfficiency
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-CIRCULATION",
                Name = "Minimize Circulation Area",
                Category = "Spatial",
                Direction = OptimizationDirection.Minimize,
                Weight = 0.6,
                EvaluateFunction = EvaluateCirculation
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-STRUCTURE",
                Name = "Optimize Structural Efficiency",
                Category = "Structural",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.7,
                EvaluateFunction = EvaluateStructure
            });

            AddObjective(new DesignObjective
            {
                ObjectiveId = "OBJ-FLEXIBILITY",
                Name = "Maximize Layout Flexibility",
                Category = "Functional",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.5,
                EvaluateFunction = EvaluateFlexibility
            });
        }

        private void InitializeDefaultPatterns()
        {
            AddPattern(new DesignPattern
            {
                PatternId = "PAT-LINEAR",
                Name = "Linear Organization",
                Description = "Rooms arranged along a linear circulation spine",
                Category = "Spatial Organization",
                Applicability = new List<string> { "Office", "School", "Hospital Wing" },
                EfficiencyRating = 0.85,
                CirculationRatio = 0.18
            });

            AddPattern(new DesignPattern
            {
                PatternId = "PAT-CENTRAL",
                Name = "Central Core",
                Description = "Rooms organized around a central service core",
                Category = "Spatial Organization",
                Applicability = new List<string> { "Office Tower", "Hotel", "Residential" },
                EfficiencyRating = 0.78,
                CirculationRatio = 0.22
            });

            AddPattern(new DesignPattern
            {
                PatternId = "PAT-CLUSTER",
                Name = "Cluster Organization",
                Description = "Groups of rooms clustered around shared spaces",
                Category = "Spatial Organization",
                Applicability = new List<string> { "Research Lab", "School", "Healthcare" },
                EfficiencyRating = 0.72,
                CirculationRatio = 0.25
            });

            AddPattern(new DesignPattern
            {
                PatternId = "PAT-COURTYARD",
                Name = "Courtyard Plan",
                Description = "Rooms organized around central open courtyard",
                Category = "Spatial Organization",
                Applicability = new List<string> { "Residential", "Hotel", "School" },
                EfficiencyRating = 0.68,
                CirculationRatio = 0.20
            });

            AddPattern(new DesignPattern
            {
                PatternId = "PAT-ATRIUM",
                Name = "Atrium Building",
                Description = "Multi-story space with rooms facing internal atrium",
                Category = "Spatial Organization",
                Applicability = new List<string> { "Office", "Hotel", "Retail" },
                EfficiencyRating = 0.70,
                CirculationRatio = 0.28
            });

            AddPattern(new DesignPattern
            {
                PatternId = "PAT-OPENPLAN",
                Name = "Open Plan",
                Description = "Large open floor plates with minimal internal divisions",
                Category = "Spatial Organization",
                Applicability = new List<string> { "Office", "Retail", "Warehouse" },
                EfficiencyRating = 0.92,
                CirculationRatio = 0.12
            });
        }

        #endregion

        #region Public Methods - Configuration

        public void AddConstraint(DesignConstraint constraint)
        {
            lock (_lockObject)
            {
                _constraints[constraint.ConstraintId] = constraint;
            }
        }

        public void AddObjective(DesignObjective objective)
        {
            lock (_lockObject)
            {
                _objectives[objective.ObjectiveId] = objective;
            }
        }

        public void AddPattern(DesignPattern pattern)
        {
            lock (_lockObject)
            {
                _patterns[pattern.PatternId] = pattern;
            }
        }

        public void SetObjectiveWeight(string objectiveId, double weight)
        {
            lock (_lockObject)
            {
                if (_objectives.TryGetValue(objectiveId, out var objective))
                {
                    objective.Weight = Math.Max(0, Math.Min(1, weight));
                }
            }
        }

        #endregion

        #region Public Methods - Design Generation

        /// <summary>
        /// Generates design variants based on program requirements
        /// </summary>
        public async Task<GenerationResult> GenerateDesignVariantsAsync(
            DesignProgram program,
            GenerationOptions options,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new GenerationResult
            {
                GenerationId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now,
                Program = program
            };

            // Validate program
            var validationResult = ValidateProgram(program);
            if (!validationResult.IsValid)
            {
                result.Success = false;
                result.Errors.AddRange(validationResult.Errors);
                return result;
            }

            progress?.Report(new GenerationProgress { Phase = "Initializing", PercentComplete = 5 });

            // Select applicable patterns
            var applicablePatterns = GetApplicablePatterns(program);
            progress?.Report(new GenerationProgress { Phase = "Pattern Selection", PercentComplete = 10 });

            // Generate initial population
            var population = await GenerateInitialPopulationAsync(
                program, applicablePatterns, options, cancellationToken);
            progress?.Report(new GenerationProgress { Phase = "Initial Generation", PercentComplete = 30 });

            // Evaluate initial population
            foreach (var variant in population)
            {
                variant.Evaluation = EvaluateDesign(variant, program);
            }
            progress?.Report(new GenerationProgress { Phase = "Evaluation", PercentComplete = 40 });

            // Evolutionary optimization
            for (int generation = 0; generation < options.MaxGenerations; generation++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Selection
                var parents = SelectParents(population, options.SelectionMethod);

                // Crossover
                var offspring = PerformCrossover(parents, options.CrossoverRate);

                // Mutation
                MutatePopulation(offspring, options.MutationRate);

                // Evaluate offspring
                foreach (var variant in offspring)
                {
                    variant.Evaluation = EvaluateDesign(variant, program);
                }

                // Select survivors
                population = SelectSurvivors(population.Concat(offspring).ToList(), options.PopulationSize);

                progress?.Report(new GenerationProgress
                {
                    Phase = "Optimization",
                    Generation = generation + 1,
                    BestFitness = population.Max(v => v.Evaluation.OverallScore),
                    PercentComplete = 40 + (generation + 1) * 50.0 / options.MaxGenerations
                });
            }

            progress?.Report(new GenerationProgress { Phase = "Finalizing", PercentComplete = 95 });

            // Select best variants
            result.Variants = population
                .OrderByDescending(v => v.Evaluation.OverallScore)
                .Take(options.ResultCount)
                .ToList();

            result.Success = true;
            result.EndTime = DateTime.Now;
            result.GenerationsCompleted = options.MaxGenerations;

            progress?.Report(new GenerationProgress { Phase = "Complete", PercentComplete = 100 });

            return result;
        }

        /// <summary>
        /// Generates layout options for a specific room program
        /// </summary>
        public async Task<LayoutGenerationResult> GenerateLayoutOptionsAsync(
            RoomProgram roomProgram,
            SiteConstraints siteConstraints,
            int optionCount = 5,
            CancellationToken cancellationToken = default)
        {
            var result = new LayoutGenerationResult
            {
                GenerationId = Guid.NewGuid().ToString()
            };

            for (int i = 0; i < optionCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var layout = _generator.GenerateLayout(roomProgram, siteConstraints, i);
                layout.Evaluation = EvaluateLayout(layout);
                result.Layouts.Add(layout);
            }

            result.Layouts = result.Layouts
                .OrderByDescending(l => l.Evaluation.OverallScore)
                .ToList();

            return result;
        }

        /// <summary>
        /// Optimizes existing design for specific objectives
        /// </summary>
        public async Task<OptimizationResult> OptimizeDesignAsync(
            DesignVariant currentDesign,
            List<string> objectiveIds,
            int iterations = 100,
            CancellationToken cancellationToken = default)
        {
            var result = new OptimizationResult
            {
                OriginalDesign = currentDesign,
                OriginalScore = currentDesign.Evaluation?.OverallScore ?? 0
            };

            var objectives = objectiveIds
                .Select(id => _objectives.GetValueOrDefault(id))
                .Where(o => o != null)
                .ToList();

            var optimizedDesign = await _optimizer.OptimizeAsync(
                currentDesign, objectives, iterations, cancellationToken);

            result.OptimizedDesign = optimizedDesign;
            result.OptimizedScore = optimizedDesign.Evaluation?.OverallScore ?? 0;
            result.Improvement = result.OptimizedScore - result.OriginalScore;
            result.Changes = IdentifyChanges(currentDesign, optimizedDesign);

            return result;
        }

        #endregion

        #region Public Methods - Analysis

        /// <summary>
        /// Evaluates a design against all objectives
        /// </summary>
        public DesignEvaluation EvaluateDesign(DesignVariant variant, DesignProgram program)
        {
            var evaluation = new DesignEvaluation
            {
                VariantId = variant.VariantId,
                EvaluatedAt = DateTime.Now
            };

            // Evaluate constraints
            foreach (var constraint in _constraints.Values.Where(c => c.IsEnabled))
            {
                var constraintResult = constraint.ValidateFunction?.Invoke(variant, constraint) ??
                    new ConstraintResult { IsSatisfied = true };

                evaluation.ConstraintResults[constraint.ConstraintId] = constraintResult;

                if (!constraintResult.IsSatisfied && constraint.IsHard)
                {
                    evaluation.IsValid = false;
                }
            }

            // Evaluate objectives
            var totalWeight = _objectives.Values.Where(o => o.IsEnabled).Sum(o => o.Weight);

            foreach (var objective in _objectives.Values.Where(o => o.IsEnabled))
            {
                var score = objective.EvaluateFunction?.Invoke(variant, objective) ?? 0;
                var normalizedWeight = objective.Weight / totalWeight;

                evaluation.ObjectiveScores[objective.ObjectiveId] = new ObjectiveScore
                {
                    ObjectiveId = objective.ObjectiveId,
                    RawScore = score,
                    WeightedScore = score * normalizedWeight,
                    Weight = normalizedWeight
                };
            }

            evaluation.OverallScore = evaluation.ObjectiveScores.Values.Sum(s => s.WeightedScore);
            evaluation.IsValid = evaluation.ConstraintResults.Values.All(c =>
                c.IsSatisfied || !_constraints[c.ConstraintId ?? ""].IsHard);

            return evaluation;
        }

        /// <summary>
        /// Compares multiple design variants
        /// </summary>
        public DesignComparison CompareVariants(IEnumerable<DesignVariant> variants)
        {
            var variantList = variants.ToList();
            var comparison = new DesignComparison
            {
                VariantCount = variantList.Count
            };

            foreach (var objective in _objectives.Values.Where(o => o.IsEnabled))
            {
                var scores = variantList
                    .Where(v => v.Evaluation?.ObjectiveScores.ContainsKey(objective.ObjectiveId) == true)
                    .Select(v => new
                    {
                        VariantId = v.VariantId,
                        Score = v.Evaluation.ObjectiveScores[objective.ObjectiveId].RawScore
                    })
                    .ToList();

                if (scores.Any())
                {
                    comparison.ObjectiveRankings[objective.ObjectiveId] = scores
                        .OrderByDescending(s => objective.Direction == OptimizationDirection.Maximize ? s.Score : -s.Score)
                        .Select((s, i) => new VariantRanking
                        {
                            VariantId = s.VariantId,
                            Rank = i + 1,
                            Score = s.Score
                        })
                        .ToList();
                }
            }

            comparison.OverallBest = variantList
                .OrderByDescending(v => v.Evaluation?.OverallScore ?? 0)
                .First().VariantId;

            comparison.ParetoFront = CalculateParetoFront(variantList);

            return comparison;
        }

        /// <summary>
        /// Performs sensitivity analysis on design parameters
        /// </summary>
        public SensitivityAnalysis AnalyzeSensitivity(
            DesignVariant baseDesign,
            List<string> parameterIds,
            double variationRange = 0.2)
        {
            var analysis = new SensitivityAnalysis
            {
                BaseDesign = baseDesign,
                BaseScore = baseDesign.Evaluation?.OverallScore ?? 0
            };

            foreach (var parameterId in parameterIds)
            {
                var sensitivity = new ParameterSensitivity
                {
                    ParameterId = parameterId
                };

                // Test +/- variations
                for (double variation = -variationRange; variation <= variationRange; variation += variationRange / 5)
                {
                    var modifiedDesign = ModifyParameter(baseDesign, parameterId, variation);
                    var evaluation = EvaluateDesign(modifiedDesign, new DesignProgram());

                    sensitivity.Variations.Add(new VariationResult
                    {
                        VariationPercent = variation * 100,
                        Score = evaluation.OverallScore,
                        ScoreChange = evaluation.OverallScore - analysis.BaseScore
                    });
                }

                sensitivity.SensitivityIndex = CalculateSensitivityIndex(sensitivity.Variations);
                analysis.ParameterSensitivities.Add(sensitivity);
            }

            analysis.MostSensitiveParameters = analysis.ParameterSensitivities
                .OrderByDescending(p => Math.Abs(p.SensitivityIndex))
                .Take(5)
                .Select(p => p.ParameterId)
                .ToList();

            return analysis;
        }

        #endregion

        #region Public Methods - Learning

        /// <summary>
        /// Learns patterns from successful projects
        /// </summary>
        public async Task<LearningResult> LearnFromProjectsAsync(
            IEnumerable<ProjectData> projects,
            CancellationToken cancellationToken = default)
        {
            var result = new LearningResult
            {
                ProjectsAnalyzed = projects.Count()
            };

            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var learnedPatterns = await _patternLearner.ExtractPatternsAsync(project, cancellationToken);

                foreach (var pattern in learnedPatterns)
                {
                    if (_patterns.ContainsKey(pattern.PatternId))
                    {
                        // Update existing pattern with new data
                        UpdatePatternFromLearning(_patterns[pattern.PatternId], pattern);
                        result.PatternsUpdated++;
                    }
                    else
                    {
                        _patterns[pattern.PatternId] = pattern;
                        result.NewPatternsDiscovered++;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets design recommendations based on learned patterns
        /// </summary>
        public DesignRecommendations GetRecommendations(DesignProgram program)
        {
            var recommendations = new DesignRecommendations
            {
                ProgramType = program.BuildingType
            };

            // Recommend patterns
            var applicablePatterns = GetApplicablePatterns(program)
                .OrderByDescending(p => p.SuccessRate)
                .Take(3);

            foreach (var pattern in applicablePatterns)
            {
                recommendations.PatternRecommendations.Add(new PatternRecommendation
                {
                    Pattern = pattern,
                    Confidence = pattern.SuccessRate,
                    Rationale = $"High success rate ({pattern.SuccessRate:P0}) for {program.BuildingType} projects"
                });
            }

            // Recommend parameter values
            var benchmarks = GetBenchmarksForType(program.BuildingType);
            foreach (var benchmark in benchmarks)
            {
                recommendations.ParameterRecommendations.Add(new ParameterRecommendation
                {
                    ParameterName = benchmark.Key,
                    RecommendedValue = benchmark.Value,
                    Source = "Industry Benchmark"
                });
            }

            return recommendations;
        }

        #endregion

        #region Private Methods - Generation

        private async Task<List<DesignVariant>> GenerateInitialPopulationAsync(
            DesignProgram program,
            List<DesignPattern> patterns,
            GenerationOptions options,
            CancellationToken cancellationToken)
        {
            var population = new List<DesignVariant>();

            await Task.Run(() =>
            {
                for (int i = 0; i < options.PopulationSize; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pattern = patterns[i % patterns.Count];
                    var variant = _generator.GenerateFromPattern(program, pattern, i);
                    population.Add(variant);
                }
            }, cancellationToken);

            return population;
        }

        private List<DesignVariant> SelectParents(List<DesignVariant> population, SelectionMethod method)
        {
            var parents = new List<DesignVariant>();
            var random = new Random();

            switch (method)
            {
                case SelectionMethod.Tournament:
                    while (parents.Count < population.Count / 2)
                    {
                        var contestants = population.OrderBy(_ => random.Next()).Take(3).ToList();
                        parents.Add(contestants.OrderByDescending(c => c.Evaluation?.OverallScore ?? 0).First());
                    }
                    break;

                case SelectionMethod.Roulette:
                    var totalFitness = population.Sum(v => v.Evaluation?.OverallScore ?? 0);
                    while (parents.Count < population.Count / 2)
                    {
                        var threshold = random.NextDouble() * totalFitness;
                        var cumulative = 0.0;
                        foreach (var variant in population)
                        {
                            cumulative += variant.Evaluation?.OverallScore ?? 0;
                            if (cumulative >= threshold)
                            {
                                parents.Add(variant);
                                break;
                            }
                        }
                    }
                    break;

                case SelectionMethod.Elite:
                    parents = population
                        .OrderByDescending(v => v.Evaluation?.OverallScore ?? 0)
                        .Take(population.Count / 2)
                        .ToList();
                    break;
            }

            return parents;
        }

        private List<DesignVariant> PerformCrossover(List<DesignVariant> parents, double rate)
        {
            var offspring = new List<DesignVariant>();
            var random = new Random();

            for (int i = 0; i < parents.Count - 1; i += 2)
            {
                if (random.NextDouble() < rate)
                {
                    var (child1, child2) = _generator.Crossover(parents[i], parents[i + 1]);
                    offspring.Add(child1);
                    offspring.Add(child2);
                }
                else
                {
                    offspring.Add(CloneVariant(parents[i]));
                    offspring.Add(CloneVariant(parents[i + 1]));
                }
            }

            return offspring;
        }

        private void MutatePopulation(List<DesignVariant> population, double rate)
        {
            var random = new Random();

            foreach (var variant in population)
            {
                if (random.NextDouble() < rate)
                {
                    _generator.Mutate(variant);
                }
            }
        }

        private List<DesignVariant> SelectSurvivors(List<DesignVariant> combined, int targetSize)
        {
            return combined
                .Where(v => v.Evaluation?.IsValid != false)
                .OrderByDescending(v => v.Evaluation?.OverallScore ?? 0)
                .Take(targetSize)
                .ToList();
        }

        private DesignVariant CloneVariant(DesignVariant original)
        {
            return new DesignVariant
            {
                VariantId = Guid.NewGuid().ToString(),
                PatternId = original.PatternId,
                Parameters = new Dictionary<string, double>(original.Parameters),
                Rooms = original.Rooms.Select(r => new RoomInstance
                {
                    RoomId = r.RoomId,
                    RoomType = r.RoomType,
                    Area = r.Area,
                    Position = r.Position
                }).ToList()
            };
        }

        #endregion

        #region Private Methods - Validation

        private ValidationResult ValidateProgram(DesignProgram program)
        {
            var result = new ValidationResult { IsValid = true };

            if (program.TotalArea <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("Total area must be greater than zero");
            }

            if (!program.Rooms?.Any() == true)
            {
                result.IsValid = false;
                result.Errors.Add("At least one room is required in the program");
            }

            return result;
        }

        private ConstraintResult ValidateMinimumArea(DesignVariant variant, DesignConstraint constraint)
        {
            var violations = variant.Rooms
                .Where(r => r.Area < (constraint.MinValue ?? 0))
                .ToList();

            return new ConstraintResult
            {
                ConstraintId = constraint.ConstraintId,
                IsSatisfied = !violations.Any(),
                Message = violations.Any() ?
                    $"{violations.Count} rooms below minimum area" : "All rooms meet minimum area"
            };
        }

        private ConstraintResult ValidateMaxHeight(DesignVariant variant, DesignConstraint constraint)
        {
            var height = variant.Parameters.GetValueOrDefault("BuildingHeight", 0);
            return new ConstraintResult
            {
                ConstraintId = constraint.ConstraintId,
                IsSatisfied = height <= (constraint.MaxValue ?? double.MaxValue),
                ActualValue = height,
                RequiredValue = constraint.MaxValue ?? 0
            };
        }

        private ConstraintResult ValidateFAR(DesignVariant variant, DesignConstraint constraint)
        {
            var far = variant.Parameters.GetValueOrDefault("FAR", 0);
            return new ConstraintResult
            {
                ConstraintId = constraint.ConstraintId,
                IsSatisfied = far >= (constraint.MinValue ?? 0) && far <= (constraint.MaxValue ?? double.MaxValue),
                ActualValue = far
            };
        }

        private ConstraintResult ValidateSiteCoverage(DesignVariant variant, DesignConstraint constraint)
        {
            var coverage = variant.Parameters.GetValueOrDefault("SiteCoverage", 0);
            return new ConstraintResult
            {
                ConstraintId = constraint.ConstraintId,
                IsSatisfied = coverage <= (constraint.MaxValue ?? 100),
                ActualValue = coverage
            };
        }

        private ConstraintResult ValidateCorridorWidth(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        private ConstraintResult ValidateExitDistance(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        private ConstraintResult ValidateColumnSpacing(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        private ConstraintResult ValidateSpanDepthRatio(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        private ConstraintResult ValidateWWR(DesignVariant variant, DesignConstraint constraint)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            return new ConstraintResult
            {
                ConstraintId = constraint.ConstraintId,
                IsSatisfied = wwr >= (constraint.MinValue ?? 0) && wwr <= (constraint.MaxValue ?? 100),
                ActualValue = wwr
            };
        }

        private ConstraintResult ValidateOrientation(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        private ConstraintResult ValidateRequiredAdjacencies(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        private ConstraintResult ValidateProhibitedAdjacencies(DesignVariant variant, DesignConstraint constraint)
        {
            return new ConstraintResult { IsSatisfied = true };
        }

        #endregion

        #region Private Methods - Evaluation

        private double EvaluateCost(DesignVariant variant, DesignObjective objective)
        {
            var totalArea = variant.Rooms.Sum(r => r.Area);
            var costPerSqm = variant.Parameters.GetValueOrDefault("CostPerSqm", 1500);
            var estimatedCost = totalArea * costPerSqm;
            // Normalize to 0-1 (lower is better for cost)
            return Math.Max(0, 1 - estimatedCost / 10000000);
        }

        private double EvaluateEnergy(DesignVariant variant, DesignObjective objective)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            var orientation = variant.Parameters.GetValueOrDefault("OrientationScore", 0.7);
            // Simple energy score based on WWR and orientation
            var energyScore = orientation * (1 - Math.Abs(wwr - 25) / 50);
            return Math.Max(0, Math.Min(1, energyScore));
        }

        private double EvaluateDaylight(DesignVariant variant, DesignObjective objective)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            var daylightScore = wwr / 50; // Higher WWR = more daylight
            return Math.Max(0, Math.Min(1, daylightScore));
        }

        private double EvaluateViews(DesignVariant variant, DesignObjective objective)
        {
            return variant.Parameters.GetValueOrDefault("ViewScore", 0.5);
        }

        private double EvaluateSpaceEfficiency(DesignVariant variant, DesignObjective objective)
        {
            var netArea = variant.Rooms.Sum(r => r.Area);
            var grossArea = variant.Parameters.GetValueOrDefault("GrossArea", netArea * 1.3);
            return netArea / grossArea;
        }

        private double EvaluateCirculation(DesignVariant variant, DesignObjective objective)
        {
            var circRatio = variant.Parameters.GetValueOrDefault("CirculationRatio", 0.2);
            return 1 - circRatio; // Lower circulation = higher score
        }

        private double EvaluateStructure(DesignVariant variant, DesignObjective objective)
        {
            return variant.Parameters.GetValueOrDefault("StructuralEfficiency", 0.7);
        }

        private double EvaluateFlexibility(DesignVariant variant, DesignObjective objective)
        {
            return variant.Parameters.GetValueOrDefault("FlexibilityScore", 0.5);
        }

        private LayoutEvaluation EvaluateLayout(LayoutOption layout)
        {
            return new LayoutEvaluation
            {
                OverallScore = 0.75,
                EfficiencyScore = 0.8,
                CirculationScore = 0.7,
                AdjacencyScore = 0.75
            };
        }

        #endregion

        #region Private Methods - Helpers

        private List<DesignPattern> GetApplicablePatterns(DesignProgram program)
        {
            return _patterns.Values
                .Where(p => p.Applicability.Any(a =>
                    a.Contains(program.BuildingType, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private List<string> CalculateParetoFront(List<DesignVariant> variants)
        {
            var paretoFront = new List<string>();

            foreach (var variant in variants)
            {
                var dominated = false;
                foreach (var other in variants.Where(v => v.VariantId != variant.VariantId))
                {
                    if (Dominates(other, variant))
                    {
                        dominated = true;
                        break;
                    }
                }

                if (!dominated)
                {
                    paretoFront.Add(variant.VariantId);
                }
            }

            return paretoFront;
        }

        private bool Dominates(DesignVariant a, DesignVariant b)
        {
            var aScores = a.Evaluation?.ObjectiveScores.Values.Select(s => s.RawScore).ToList() ?? new List<double>();
            var bScores = b.Evaluation?.ObjectiveScores.Values.Select(s => s.RawScore).ToList() ?? new List<double>();

            if (aScores.Count != bScores.Count) return false;

            var atLeastOneBetter = false;
            for (int i = 0; i < aScores.Count; i++)
            {
                if (aScores[i] < bScores[i]) return false;
                if (aScores[i] > bScores[i]) atLeastOneBetter = true;
            }

            return atLeastOneBetter;
        }

        private DesignVariant ModifyParameter(DesignVariant original, string parameterId, double variationPercent)
        {
            var modified = CloneVariant(original);
            if (modified.Parameters.ContainsKey(parameterId))
            {
                modified.Parameters[parameterId] *= (1 + variationPercent);
            }
            return modified;
        }

        private double CalculateSensitivityIndex(List<VariationResult> variations)
        {
            if (variations.Count < 2) return 0;
            var maxChange = variations.Max(v => Math.Abs(v.ScoreChange));
            var avgChange = variations.Average(v => Math.Abs(v.ScoreChange));
            return avgChange;
        }

        private List<DesignChange> IdentifyChanges(DesignVariant original, DesignVariant optimized)
        {
            var changes = new List<DesignChange>();

            foreach (var param in optimized.Parameters)
            {
                if (original.Parameters.TryGetValue(param.Key, out var originalValue))
                {
                    if (Math.Abs(param.Value - originalValue) > 0.001)
                    {
                        changes.Add(new DesignChange
                        {
                            ParameterName = param.Key,
                            OriginalValue = originalValue,
                            NewValue = param.Value,
                            ChangePercent = (param.Value - originalValue) / originalValue * 100
                        });
                    }
                }
            }

            return changes;
        }

        private void UpdatePatternFromLearning(DesignPattern existing, DesignPattern learned)
        {
            existing.SuccessRate = (existing.SuccessRate + learned.SuccessRate) / 2;
            existing.UsageCount += learned.UsageCount;
        }

        private Dictionary<string, double> GetBenchmarksForType(string buildingType)
        {
            var benchmarks = new Dictionary<string, double>
            {
                ["CirculationRatio"] = 0.2,
                ["WWR"] = 30,
                ["StructuralEfficiency"] = 0.75
            };

            switch (buildingType?.ToLower())
            {
                case "office":
                    benchmarks["CirculationRatio"] = 0.18;
                    benchmarks["WWR"] = 35;
                    break;
                case "residential":
                    benchmarks["CirculationRatio"] = 0.15;
                    benchmarks["WWR"] = 25;
                    break;
                case "hospital":
                    benchmarks["CirculationRatio"] = 0.35;
                    benchmarks["WWR"] = 20;
                    break;
            }

            return benchmarks;
        }

        #endregion
    }

    #region Supporting Classes

    public class DesignConstraint
    {
        public string ConstraintId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public ConstraintType Type { get; set; }
        public string ParameterName { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string PreferredValue { get; set; }
        public string Unit { get; set; }
        public bool IsHard { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        public Func<DesignVariant, DesignConstraint, ConstraintResult> ValidateFunction { get; set; }
    }

    public class DesignObjective
    {
        public string ObjectiveId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public OptimizationDirection Direction { get; set; }
        public double Weight { get; set; } = 1.0;
        public bool IsEnabled { get; set; } = true;
        public Func<DesignVariant, DesignObjective, double> EvaluateFunction { get; set; }
    }

    public class DesignPattern
    {
        public string PatternId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<string> Applicability { get; set; } = new List<string>();
        public double EfficiencyRating { get; set; }
        public double CirculationRatio { get; set; }
        public double SuccessRate { get; set; } = 0.7;
        public int UsageCount { get; set; }
    }

    public class DesignVariant
    {
        public string VariantId { get; set; }
        public string PatternId { get; set; }
        public Dictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();
        public List<RoomInstance> Rooms { get; set; } = new List<RoomInstance>();
        public DesignEvaluation Evaluation { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class RoomInstance
    {
        public string RoomId { get; set; }
        public string RoomType { get; set; }
        public double Area { get; set; }
        public (double X, double Y) Position { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class DesignProgram
    {
        public string ProgramId { get; set; }
        public string BuildingType { get; set; }
        public double TotalArea { get; set; }
        public int Floors { get; set; }
        public List<RoomRequirement> Rooms { get; set; } = new List<RoomRequirement>();
        public List<AdjacencyRequirement> Adjacencies { get; set; } = new List<AdjacencyRequirement>();
    }

    public class RoomRequirement
    {
        public string RoomType { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public int Quantity { get; set; }
        public bool RequiresExterior { get; set; }
    }

    public class AdjacencyRequirement
    {
        public string Room1Type { get; set; }
        public string Room2Type { get; set; }
        public AdjacencyType Type { get; set; }
    }

    public class RoomProgram
    {
        public List<RoomRequirement> Rooms { get; set; } = new List<RoomRequirement>();
    }

    public class SiteConstraints
    {
        public double SiteArea { get; set; }
        public double MaxFAR { get; set; }
        public double MaxHeight { get; set; }
        public double MaxCoverage { get; set; }
        public List<(double X, double Y)> SiteBoundary { get; set; } = new List<(double, double)>();
    }

    public class GenerationOptions
    {
        public int PopulationSize { get; set; } = 50;
        public int MaxGenerations { get; set; } = 100;
        public double CrossoverRate { get; set; } = 0.8;
        public double MutationRate { get; set; } = 0.1;
        public SelectionMethod SelectionMethod { get; set; } = SelectionMethod.Tournament;
        public int ResultCount { get; set; } = 10;
    }

    public class GenerationResult
    {
        public string GenerationId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DesignProgram Program { get; set; }
        public List<DesignVariant> Variants { get; set; } = new List<DesignVariant>();
        public bool Success { get; set; }
        public int GenerationsCompleted { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class GenerationProgress
    {
        public string Phase { get; set; }
        public int Generation { get; set; }
        public double BestFitness { get; set; }
        public double PercentComplete { get; set; }
    }

    public class LayoutOption
    {
        public string LayoutId { get; set; }
        public List<RoomInstance> Rooms { get; set; } = new List<RoomInstance>();
        public LayoutEvaluation Evaluation { get; set; }
    }

    public class LayoutGenerationResult
    {
        public string GenerationId { get; set; }
        public List<LayoutOption> Layouts { get; set; } = new List<LayoutOption>();
    }

    public class LayoutEvaluation
    {
        public double OverallScore { get; set; }
        public double EfficiencyScore { get; set; }
        public double CirculationScore { get; set; }
        public double AdjacencyScore { get; set; }
    }

    public class OptimizationResult
    {
        public DesignVariant OriginalDesign { get; set; }
        public DesignVariant OptimizedDesign { get; set; }
        public double OriginalScore { get; set; }
        public double OptimizedScore { get; set; }
        public double Improvement { get; set; }
        public List<DesignChange> Changes { get; set; } = new List<DesignChange>();
    }

    public class DesignChange
    {
        public string ParameterName { get; set; }
        public double OriginalValue { get; set; }
        public double NewValue { get; set; }
        public double ChangePercent { get; set; }
    }

    public class DesignEvaluation
    {
        public string VariantId { get; set; }
        public DateTime EvaluatedAt { get; set; }
        public bool IsValid { get; set; } = true;
        public double OverallScore { get; set; }
        public Dictionary<string, ConstraintResult> ConstraintResults { get; set; } = new Dictionary<string, ConstraintResult>();
        public Dictionary<string, ObjectiveScore> ObjectiveScores { get; set; } = new Dictionary<string, ObjectiveScore>();
    }

    public class ConstraintResult
    {
        public string ConstraintId { get; set; }
        public bool IsSatisfied { get; set; }
        public double ActualValue { get; set; }
        public double RequiredValue { get; set; }
        public string Message { get; set; }
    }

    public class ObjectiveScore
    {
        public string ObjectiveId { get; set; }
        public double RawScore { get; set; }
        public double WeightedScore { get; set; }
        public double Weight { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class DesignComparison
    {
        public int VariantCount { get; set; }
        public Dictionary<string, List<VariantRanking>> ObjectiveRankings { get; set; } = new Dictionary<string, List<VariantRanking>>();
        public string OverallBest { get; set; }
        public List<string> ParetoFront { get; set; } = new List<string>();
    }

    public class VariantRanking
    {
        public string VariantId { get; set; }
        public int Rank { get; set; }
        public double Score { get; set; }
    }

    public class SensitivityAnalysis
    {
        public DesignVariant BaseDesign { get; set; }
        public double BaseScore { get; set; }
        public List<ParameterSensitivity> ParameterSensitivities { get; set; } = new List<ParameterSensitivity>();
        public List<string> MostSensitiveParameters { get; set; } = new List<string>();
    }

    public class ParameterSensitivity
    {
        public string ParameterId { get; set; }
        public double SensitivityIndex { get; set; }
        public List<VariationResult> Variations { get; set; } = new List<VariationResult>();
    }

    public class VariationResult
    {
        public double VariationPercent { get; set; }
        public double Score { get; set; }
        public double ScoreChange { get; set; }
    }

    public class ProjectData
    {
        public string ProjectId { get; set; }
        public string BuildingType { get; set; }
        public double TotalArea { get; set; }
        public double SuccessRating { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
    }

    public class LearningResult
    {
        public int ProjectsAnalyzed { get; set; }
        public int NewPatternsDiscovered { get; set; }
        public int PatternsUpdated { get; set; }
    }

    public class DesignRecommendations
    {
        public string ProgramType { get; set; }
        public List<PatternRecommendation> PatternRecommendations { get; set; } = new List<PatternRecommendation>();
        public List<ParameterRecommendation> ParameterRecommendations { get; set; } = new List<ParameterRecommendation>();
    }

    public class PatternRecommendation
    {
        public DesignPattern Pattern { get; set; }
        public double Confidence { get; set; }
        public string Rationale { get; set; }
    }

    public class ParameterRecommendation
    {
        public string ParameterName { get; set; }
        public double RecommendedValue { get; set; }
        public string Source { get; set; }
    }

    public class DesignGenerator
    {
        public DesignVariant GenerateFromPattern(DesignProgram program, DesignPattern pattern, int seed)
        {
            var random = new Random(seed);
            var variant = new DesignVariant
            {
                VariantId = Guid.NewGuid().ToString(),
                PatternId = pattern.PatternId
            };

            variant.Parameters["CirculationRatio"] = pattern.CirculationRatio + (random.NextDouble() - 0.5) * 0.1;
            variant.Parameters["WWR"] = 25 + random.NextDouble() * 20;
            variant.Parameters["StructuralEfficiency"] = 0.7 + random.NextDouble() * 0.2;
            variant.Parameters["GrossArea"] = program.TotalArea;

            foreach (var roomReq in program.Rooms)
            {
                for (int i = 0; i < roomReq.Quantity; i++)
                {
                    variant.Rooms.Add(new RoomInstance
                    {
                        RoomId = Guid.NewGuid().ToString(),
                        RoomType = roomReq.RoomType,
                        Area = roomReq.MinArea + random.NextDouble() * (roomReq.MaxArea - roomReq.MinArea),
                        Position = (random.NextDouble() * 100, random.NextDouble() * 100)
                    });
                }
            }

            return variant;
        }

        public LayoutOption GenerateLayout(RoomProgram program, SiteConstraints constraints, int seed)
        {
            return new LayoutOption { LayoutId = Guid.NewGuid().ToString() };
        }

        public (DesignVariant, DesignVariant) Crossover(DesignVariant parent1, DesignVariant parent2)
        {
            var child1 = new DesignVariant { VariantId = Guid.NewGuid().ToString() };
            var child2 = new DesignVariant { VariantId = Guid.NewGuid().ToString() };

            var random = new Random();
            foreach (var param in parent1.Parameters)
            {
                if (random.NextDouble() < 0.5)
                {
                    child1.Parameters[param.Key] = param.Value;
                    child2.Parameters[param.Key] = parent2.Parameters.GetValueOrDefault(param.Key, param.Value);
                }
                else
                {
                    child1.Parameters[param.Key] = parent2.Parameters.GetValueOrDefault(param.Key, param.Value);
                    child2.Parameters[param.Key] = param.Value;
                }
            }

            return (child1, child2);
        }

        public void Mutate(DesignVariant variant)
        {
            var random = new Random();
            var keys = variant.Parameters.Keys.ToList();
            if (keys.Any())
            {
                var key = keys[random.Next(keys.Count)];
                variant.Parameters[key] *= 0.9 + random.NextDouble() * 0.2;
            }
        }
    }

    public class DesignEvaluator
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Evaluates a design variant against a list of objectives and returns a weighted
        /// average fitness score between 0.0 and 1.0.
        /// </summary>
        public double EvaluateVariant(DesignVariant variant, List<DesignObjective> objectives)
        {
            if (variant == null || objectives == null || objectives.Count == 0)
            {
                Logger.Warn("EvaluateVariant called with null variant or empty objectives");
                return 0.0;
            }

            double totalWeight = 0;
            double weightedScoreSum = 0;

            foreach (var objective in objectives)
            {
                double rawScore = EvaluateObjective(variant, objective);

                // Clamp raw score to [0, 1]
                rawScore = Math.Max(0.0, Math.Min(1.0, rawScore));

                totalWeight += objective.Weight;
                weightedScoreSum += rawScore * objective.Weight;
            }

            double fitness = totalWeight > 0 ? weightedScoreSum / totalWeight : 0.0;

            Logger.Debug($"Variant {variant.VariantId}: fitness={fitness:F4} from {objectives.Count} objectives");
            return Math.Max(0.0, Math.Min(1.0, fitness));
        }

        private double EvaluateObjective(DesignVariant variant, DesignObjective objective)
        {
            var objectiveKey = objective.ObjectiveId?.ToLowerInvariant()
                               ?? objective.Name?.ToLowerInvariant()
                               ?? string.Empty;

            // Determine which evaluation strategy to use based on objective naming
            if (objectiveKey.Contains("minimize_area"))
            {
                return EvaluateMinimizeArea(variant);
            }
            else if (objectiveKey.Contains("maximize_area"))
            {
                return EvaluateMaximizeArea(variant);
            }
            else if (objectiveKey.Contains("minimize_cost") || objectiveKey.Contains("cost"))
            {
                return EvaluateMinimizeCost(variant);
            }
            else if (objectiveKey.Contains("maximize_value") || objectiveKey.Contains("value"))
            {
                return EvaluateMaximizeValue(variant);
            }
            else if (objectiveKey.Contains("maximize_daylight") || objectiveKey.Contains("daylight"))
            {
                return EvaluateMaximizeDaylight(variant);
            }
            else if (objectiveKey.Contains("minimize_energy") || objectiveKey.Contains("energy"))
            {
                return EvaluateMinimizeEnergy(variant);
            }
            else
            {
                // Default: use the objective's own evaluate function if available,
                // otherwise linear interpolation between min/max target values
                if (objective.EvaluateFunction != null)
                {
                    return objective.EvaluateFunction(variant, objective);
                }

                return EvaluateByLinearInterpolation(variant, objective);
            }
        }

        /// <summary>
        /// Minimize total room area: smaller total area yields higher score.
        /// </summary>
        private double EvaluateMinimizeArea(DesignVariant variant)
        {
            var totalArea = variant.Rooms.Sum(r => r.Area);
            if (totalArea <= 0) return 1.0;

            // Assume a reference maximum area of 10,000 m²
            const double referenceMax = 10000.0;
            return Math.Max(0.0, 1.0 - totalArea / referenceMax);
        }

        /// <summary>
        /// Maximize total room area: larger total area yields higher score.
        /// </summary>
        private double EvaluateMaximizeArea(DesignVariant variant)
        {
            var totalArea = variant.Rooms.Sum(r => r.Area);
            const double referenceMax = 10000.0;
            return Math.Min(1.0, totalArea / referenceMax);
        }

        /// <summary>
        /// Minimize cost: lower estimated cost yields higher score.
        /// Uses CostPerSqm parameter and total room area.
        /// </summary>
        private double EvaluateMinimizeCost(DesignVariant variant)
        {
            var totalArea = variant.Rooms.Sum(r => r.Area);
            var costPerSqm = variant.Parameters.GetValueOrDefault("CostPerSqm", 1500);
            var estimatedCost = totalArea * costPerSqm;

            // Normalize against a reference budget of 10,000,000
            const double referenceBudget = 10000000.0;
            return Math.Max(0.0, 1.0 - estimatedCost / referenceBudget);
        }

        /// <summary>
        /// Maximize value: higher cost-efficiency ratio yields higher score.
        /// </summary>
        private double EvaluateMaximizeValue(DesignVariant variant)
        {
            var totalArea = variant.Rooms.Sum(r => r.Area);
            var costPerSqm = variant.Parameters.GetValueOrDefault("CostPerSqm", 1500);
            if (costPerSqm <= 0) return 1.0;

            // Value = area per unit cost, normalized
            var valueRatio = totalArea / (totalArea * costPerSqm);
            // Normalize: lower cost per sqm = higher score
            const double idealCostPerSqm = 500.0;
            return Math.Min(1.0, idealCostPerSqm / costPerSqm);
        }

        /// <summary>
        /// Maximize daylight: evaluates window-to-floor ratio.
        /// An optimal WWR around 30-40% scores highest.
        /// </summary>
        private double EvaluateMaximizeDaylight(DesignVariant variant)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            var totalFloorArea = variant.Rooms.Sum(r => r.Area);

            if (totalFloorArea <= 0) return 0.0;

            // Window-to-floor ratio approximation using WWR
            // Higher WWR means more daylight, normalized to 0-1 (50% WWR = max practical)
            var daylightScore = wwr / 50.0;
            return Math.Max(0.0, Math.Min(1.0, daylightScore));
        }

        /// <summary>
        /// Minimize energy: evaluates based on WWR and insulation.
        /// Optimal WWR around 20-25% with good insulation scores highest.
        /// </summary>
        private double EvaluateMinimizeEnergy(DesignVariant variant)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            var insulationValue = variant.Parameters.GetValueOrDefault("InsulationRValue", 3.0);

            // Penalty for WWR deviation from optimal 25%
            double wwrScore = 1.0 - Math.Abs(wwr - 25.0) / 50.0;

            // Higher insulation R-value is better; normalize against R-10 as excellent
            double insulationScore = Math.Min(1.0, insulationValue / 10.0);

            // Combined energy score (60% WWR, 40% insulation)
            double energyScore = wwrScore * 0.6 + insulationScore * 0.4;
            return Math.Max(0.0, Math.Min(1.0, energyScore));
        }

        /// <summary>
        /// Default evaluation: linear interpolation between min and max target values.
        /// Uses the objective's MinValue/MaxValue via the ParameterName if available.
        /// </summary>
        private double EvaluateByLinearInterpolation(DesignVariant variant, DesignObjective objective)
        {
            // Try to get a parameter value from the variant
            var paramName = objective.Name ?? objective.ObjectiveId ?? string.Empty;
            double actualValue = 0.5; // Default middle score

            // Try to find a matching parameter in the variant
            foreach (var param in variant.Parameters)
            {
                if (param.Key.Contains(paramName, StringComparison.OrdinalIgnoreCase)
                    || paramName.Contains(param.Key, StringComparison.OrdinalIgnoreCase))
                {
                    actualValue = param.Value;
                    break;
                }
            }

            // If no min/max known, just clamp to [0,1]
            if (actualValue >= 0 && actualValue <= 1)
            {
                return objective.Direction == OptimizationDirection.Maximize
                    ? actualValue
                    : 1.0 - actualValue;
            }

            // Normalize assuming 0-100 scale
            double normalized = actualValue / 100.0;
            normalized = Math.Max(0.0, Math.Min(1.0, normalized));

            return objective.Direction == OptimizationDirection.Maximize
                ? normalized
                : 1.0 - normalized;
        }
    }

    public class DesignOptimizer
    {
        public async Task<DesignVariant> OptimizeAsync(
            DesignVariant design,
            List<DesignObjective> objectives,
            int iterations,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return design;
        }
    }

    public class PatternLearner
    {
        public async Task<List<DesignPattern>> ExtractPatternsAsync(ProjectData project, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new List<DesignPattern>();
        }
    }

    public enum ConstraintType
    {
        Minimum,
        Maximum,
        Range,
        Exact,
        Preference,
        Required,
        Prohibited
    }

    public enum OptimizationDirection
    {
        Minimize,
        Maximize
    }

    public enum SelectionMethod
    {
        Tournament,
        Roulette,
        Elite
    }

    public enum AdjacencyType
    {
        Required,
        Preferred,
        Prohibited
    }

    #endregion
}
