using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Parametric
{
    /// <summary>
    /// Generates design alternatives through parametric variation.
    /// Supports multi-objective optimization and constraint-based exploration.
    /// </summary>
    public class ParametricVariationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly VariationSettings _settings;
        private readonly ConstraintEvaluator _constraintEvaluator;
        private readonly ObjectiveEvaluator _objectiveEvaluator;
        private readonly Random _random;

        public ParametricVariationEngine(VariationSettings settings = null)
        {
            _settings = settings ?? new VariationSettings();
            _constraintEvaluator = new ConstraintEvaluator();
            _objectiveEvaluator = new ObjectiveEvaluator();
            _random = new Random(_settings.RandomSeed ?? Environment.TickCount);
        }

        /// <summary>
        /// Generate design variations based on parameter ranges.
        /// </summary>
        public async Task<VariationResult> GenerateVariationsAsync(
            DesignModel baseDesign,
            List<ParameterRange> parameterRanges,
            List<DesignConstraint> constraints,
            List<DesignObjective> objectives,
            IProgress<VariationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating variations with {parameterRanges.Count} parameters");
            var result = new VariationResult { BaseDesignId = baseDesign.Id };

            // Generate initial population
            var population = GenerateInitialPopulation(baseDesign, parameterRanges, _settings.PopulationSize);
            progress?.Report(new VariationProgress { Phase = "Initial population", PercentComplete = 10 });

            // Evaluate and filter by constraints
            var validDesigns = await EvaluatePopulationAsync(population, constraints, objectives, cancellationToken);
            result.ValidVariations = validDesigns.Count;
            progress?.Report(new VariationProgress { Phase = "Constraint evaluation", PercentComplete = 30 });

            // Optimization loop
            for (int gen = 0; gen < _settings.MaxGenerations; gen++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Selection, crossover, mutation
                var offspring = EvolvePopulation(validDesigns, parameterRanges);
                var evaluatedOffspring = await EvaluatePopulationAsync(offspring, constraints, objectives, cancellationToken);

                // Combine and select best
                validDesigns = SelectBest(validDesigns.Concat(evaluatedOffspring).ToList(), _settings.PopulationSize);

                progress?.Report(new VariationProgress
                {
                    Phase = $"Generation {gen + 1}/{_settings.MaxGenerations}",
                    PercentComplete = 30 + ((gen + 1) * 60 / _settings.MaxGenerations),
                    BestFitness = validDesigns.FirstOrDefault()?.Fitness ?? 0
                });
            }

            // Build Pareto front for multi-objective
            result.ParetoFront = BuildParetoFront(validDesigns);
            result.AllVariations = validDesigns.Take(_settings.MaxResultVariations).ToList();
            result.Statistics = CalculateStatistics(result);

            Logger.Info($"Generated {result.AllVariations.Count} valid variations, {result.ParetoFront.Count} on Pareto front");
            return result;
        }

        /// <summary>
        /// Generate variations using Latin Hypercube Sampling for better coverage.
        /// </summary>
        public async Task<VariationResult> GenerateLHSVariationsAsync(
            DesignModel baseDesign,
            List<ParameterRange> parameterRanges,
            int sampleCount,
            List<DesignConstraint> constraints,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating {sampleCount} LHS variations");
            var result = new VariationResult { BaseDesignId = baseDesign.Id };

            var samples = GenerateLHSSamples(parameterRanges, sampleCount);
            var designs = samples.Select(s => CreateDesignFromSample(baseDesign, parameterRanges, s)).ToList();

            var evaluated = await EvaluatePopulationAsync(designs, constraints, new List<DesignObjective>(), cancellationToken);
            result.AllVariations = evaluated;
            result.ValidVariations = evaluated.Count;

            return result;
        }

        /// <summary>
        /// Explore design space around a specific design.
        /// </summary>
        public async Task<List<DesignVariant>> ExploreNeighborhoodAsync(
            DesignModel design,
            List<ParameterRange> parameterRanges,
            double explorationRadius,
            int sampleCount,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Exploring neighborhood with radius {explorationRadius}");
            var variants = new List<DesignVariant>();

            for (int i = 0; i < sampleCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var variant = CreateNeighborVariant(design, parameterRanges, explorationRadius);
                variants.Add(variant);
            }

            return await Task.FromResult(variants);
        }

        /// <summary>
        /// Perform sensitivity analysis on parameters.
        /// </summary>
        public async Task<SensitivityResult> AnalyzeSensitivityAsync(
            DesignModel baseDesign,
            List<ParameterRange> parameterRanges,
            List<DesignObjective> objectives,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Performing sensitivity analysis");
            var result = new SensitivityResult();

            foreach (var param in parameterRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sensitivity = await CalculateParameterSensitivityAsync(
                    baseDesign, param, objectives, cancellationToken);
                result.ParameterSensitivities[param.ParameterName] = sensitivity;
            }

            // Rank parameters by influence
            result.RankedParameters = result.ParameterSensitivities
                .OrderByDescending(kv => kv.Value.OverallInfluence)
                .Select(kv => kv.Key)
                .ToList();

            return result;
        }

        /// <summary>
        /// Find optimal design using genetic algorithm.
        /// </summary>
        public async Task<OptimizationResult> OptimizeDesignAsync(
            DesignModel baseDesign,
            List<ParameterRange> parameterRanges,
            List<DesignConstraint> constraints,
            DesignObjective primaryObjective,
            IProgress<VariationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Optimizing for objective: {primaryObjective.Name}");
            var result = new OptimizationResult { Objective = primaryObjective };

            var objectives = new List<DesignObjective> { primaryObjective };
            var variationResult = await GenerateVariationsAsync(
                baseDesign, parameterRanges, constraints, objectives, progress, cancellationToken);

            result.BestDesign = variationResult.AllVariations
                .OrderByDescending(d => primaryObjective.Maximize ? d.Fitness : -d.Fitness)
                .FirstOrDefault();

            result.ConvergenceHistory = variationResult.AllVariations
                .Select(d => d.ObjectiveValues.GetValueOrDefault(primaryObjective.Name, 0))
                .ToList();

            return result;
        }

        #region Private Methods

        private List<DesignVariant> GenerateInitialPopulation(
            DesignModel baseDesign, List<ParameterRange> ranges, int size)
        {
            var population = new List<DesignVariant>();

            for (int i = 0; i < size; i++)
            {
                var paramValues = new Dictionary<string, double>();
                foreach (var range in ranges)
                {
                    paramValues[range.ParameterName] = GenerateRandomValue(range);
                }

                population.Add(new DesignVariant
                {
                    Id = Guid.NewGuid().ToString(),
                    BaseDesignId = baseDesign.Id,
                    ParameterValues = paramValues,
                    Generation = 0
                });
            }

            return population;
        }

        private double GenerateRandomValue(ParameterRange range)
        {
            if (range.DiscreteValues != null && range.DiscreteValues.Any())
            {
                var index = _random.Next(range.DiscreteValues.Count);
                return range.DiscreteValues[index];
            }

            var value = range.MinValue + _random.NextDouble() * (range.MaxValue - range.MinValue);
            return range.Step > 0 ? Math.Round(value / range.Step) * range.Step : value;
        }

        private async Task<List<DesignVariant>> EvaluatePopulationAsync(
            List<DesignVariant> population,
            List<DesignConstraint> constraints,
            List<DesignObjective> objectives,
            CancellationToken cancellationToken)
        {
            var validDesigns = new List<DesignVariant>();

            foreach (var design in population)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check constraints
                var constraintResults = _constraintEvaluator.Evaluate(design, constraints);
                design.ConstraintViolations = constraintResults.Where(r => !r.IsSatisfied).ToList();

                if (design.ConstraintViolations.Any()) continue;

                // Evaluate objectives
                foreach (var objective in objectives)
                {
                    var value = _objectiveEvaluator.Evaluate(design, objective);
                    design.ObjectiveValues[objective.Name] = value;
                }

                // Calculate weighted fitness
                design.Fitness = CalculateFitness(design, objectives);
                validDesigns.Add(design);
            }

            return await Task.FromResult(validDesigns);
        }

        private double CalculateFitness(DesignVariant design, List<DesignObjective> objectives)
        {
            if (!objectives.Any()) return 0;

            double fitness = 0;
            double totalWeight = objectives.Sum(o => o.Weight);

            foreach (var objective in objectives)
            {
                var value = design.ObjectiveValues.GetValueOrDefault(objective.Name, 0);
                var normalized = NormalizeObjectiveValue(value, objective);
                fitness += (objective.Weight / totalWeight) * normalized;
            }

            return fitness;
        }

        private double NormalizeObjectiveValue(double value, DesignObjective objective)
        {
            var range = objective.ExpectedMax - objective.ExpectedMin;
            if (range <= 0) return objective.Maximize ? value : -value;

            var normalized = (value - objective.ExpectedMin) / range;
            return objective.Maximize ? normalized : 1 - normalized;
        }

        private List<DesignVariant> EvolvePopulation(
            List<DesignVariant> parents, List<ParameterRange> ranges)
        {
            var offspring = new List<DesignVariant>();
            var parentCount = parents.Count;

            for (int i = 0; i < parentCount; i++)
            {
                // Tournament selection
                var parent1 = TournamentSelect(parents);
                var parent2 = TournamentSelect(parents);

                // Crossover
                var child = Crossover(parent1, parent2, ranges);

                // Mutation
                if (_random.NextDouble() < _settings.MutationRate)
                    Mutate(child, ranges);

                child.Generation = Math.Max(parent1.Generation, parent2.Generation) + 1;
                offspring.Add(child);
            }

            return offspring;
        }

        private DesignVariant TournamentSelect(List<DesignVariant> population)
        {
            var tournamentSize = Math.Min(_settings.TournamentSize, population.Count);
            var tournament = population.OrderBy(_ => _random.Next()).Take(tournamentSize);
            return tournament.OrderByDescending(d => d.Fitness).First();
        }

        private DesignVariant Crossover(DesignVariant parent1, DesignVariant parent2, List<ParameterRange> ranges)
        {
            var child = new DesignVariant
            {
                Id = Guid.NewGuid().ToString(),
                BaseDesignId = parent1.BaseDesignId,
                ParameterValues = new Dictionary<string, double>()
            };

            foreach (var range in ranges)
            {
                // Uniform crossover
                var useParent1 = _random.NextDouble() < 0.5;
                child.ParameterValues[range.ParameterName] = useParent1
                    ? parent1.ParameterValues.GetValueOrDefault(range.ParameterName, range.DefaultValue)
                    : parent2.ParameterValues.GetValueOrDefault(range.ParameterName, range.DefaultValue);
            }

            return child;
        }

        private void Mutate(DesignVariant design, List<ParameterRange> ranges)
        {
            foreach (var range in ranges)
            {
                if (_random.NextDouble() < _settings.ParameterMutationRate)
                {
                    var currentValue = design.ParameterValues.GetValueOrDefault(range.ParameterName, range.DefaultValue);
                    var mutationAmount = (range.MaxValue - range.MinValue) * _settings.MutationStrength;
                    var newValue = currentValue + (_random.NextDouble() * 2 - 1) * mutationAmount;
                    newValue = Math.Clamp(newValue, range.MinValue, range.MaxValue);

                    if (range.Step > 0)
                        newValue = Math.Round(newValue / range.Step) * range.Step;

                    design.ParameterValues[range.ParameterName] = newValue;
                }
            }
        }

        private List<DesignVariant> SelectBest(List<DesignVariant> population, int count)
        {
            return population.OrderByDescending(d => d.Fitness).Take(count).ToList();
        }

        private List<DesignVariant> BuildParetoFront(List<DesignVariant> designs)
        {
            var paretoFront = new List<DesignVariant>();

            foreach (var design in designs)
            {
                var isDominated = false;
                foreach (var other in designs)
                {
                    if (other == design) continue;
                    if (Dominates(other, design))
                    {
                        isDominated = true;
                        break;
                    }
                }

                if (!isDominated)
                    paretoFront.Add(design);
            }

            return paretoFront;
        }

        private bool Dominates(DesignVariant a, DesignVariant b)
        {
            // a dominates b if a is at least as good in all objectives and better in at least one
            bool atLeastOneBetter = false;

            foreach (var (objective, valueA) in a.ObjectiveValues)
            {
                var valueB = b.ObjectiveValues.GetValueOrDefault(objective, 0);
                if (valueA < valueB) return false;
                if (valueA > valueB) atLeastOneBetter = true;
            }

            return atLeastOneBetter;
        }

        private List<double[]> GenerateLHSSamples(List<ParameterRange> ranges, int sampleCount)
        {
            var samples = new List<double[]>();
            var paramCount = ranges.Count;

            // Generate intervals
            var intervals = new List<List<int>>();
            for (int p = 0; p < paramCount; p++)
            {
                var permutation = Enumerable.Range(0, sampleCount).OrderBy(_ => _random.Next()).ToList();
                intervals.Add(permutation);
            }

            // Create samples
            for (int i = 0; i < sampleCount; i++)
            {
                var sample = new double[paramCount];
                for (int p = 0; p < paramCount; p++)
                {
                    var interval = intervals[p][i];
                    var range = ranges[p];
                    var u = (interval + _random.NextDouble()) / sampleCount;
                    sample[p] = range.MinValue + u * (range.MaxValue - range.MinValue);
                }
                samples.Add(sample);
            }

            return samples;
        }

        private DesignVariant CreateDesignFromSample(
            DesignModel baseDesign, List<ParameterRange> ranges, double[] sample)
        {
            var variant = new DesignVariant
            {
                Id = Guid.NewGuid().ToString(),
                BaseDesignId = baseDesign.Id,
                ParameterValues = new Dictionary<string, double>()
            };

            for (int i = 0; i < ranges.Count; i++)
            {
                variant.ParameterValues[ranges[i].ParameterName] = sample[i];
            }

            return variant;
        }

        private DesignVariant CreateNeighborVariant(
            DesignModel design, List<ParameterRange> ranges, double radius)
        {
            var variant = new DesignVariant
            {
                Id = Guid.NewGuid().ToString(),
                BaseDesignId = design.Id,
                ParameterValues = new Dictionary<string, double>()
            };

            foreach (var range in ranges)
            {
                var baseValue = design.Parameters.GetValueOrDefault(range.ParameterName, range.DefaultValue);
                var maxDelta = (range.MaxValue - range.MinValue) * radius;
                var delta = (_random.NextDouble() * 2 - 1) * maxDelta;
                var newValue = Math.Clamp(baseValue + delta, range.MinValue, range.MaxValue);
                variant.ParameterValues[range.ParameterName] = newValue;
            }

            return variant;
        }

        private async Task<ParameterSensitivity> CalculateParameterSensitivityAsync(
            DesignModel baseDesign, ParameterRange param, List<DesignObjective> objectives,
            CancellationToken cancellationToken)
        {
            var sensitivity = new ParameterSensitivity { ParameterName = param.ParameterName };
            var samplePoints = 10;
            var step = (param.MaxValue - param.MinValue) / (samplePoints - 1);

            foreach (var objective in objectives)
            {
                var values = new List<(double param, double objective)>();

                for (int i = 0; i < samplePoints; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var paramValue = param.MinValue + i * step;
                    var variant = new DesignVariant
                    {
                        BaseDesignId = baseDesign.Id,
                        ParameterValues = new Dictionary<string, double> { [param.ParameterName] = paramValue }
                    };

                    var objValue = _objectiveEvaluator.Evaluate(variant, objective);
                    values.Add((paramValue, objValue));
                }

                // Calculate gradient (sensitivity)
                var avgGradient = 0.0;
                for (int i = 1; i < values.Count; i++)
                {
                    var dParam = values[i].param - values[i - 1].param;
                    var dObj = values[i].objective - values[i - 1].objective;
                    avgGradient += Math.Abs(dObj / dParam);
                }
                avgGradient /= (values.Count - 1);

                sensitivity.ObjectiveSensitivities[objective.Name] = avgGradient;
            }

            sensitivity.OverallInfluence = sensitivity.ObjectiveSensitivities.Values.Average();
            return await Task.FromResult(sensitivity);
        }

        private VariationStatistics CalculateStatistics(VariationResult result)
        {
            var stats = new VariationStatistics
            {
                TotalGenerated = result.AllVariations.Count + (result.ParetoFront?.Count ?? 0),
                ValidVariations = result.ValidVariations,
                ParetoFrontSize = result.ParetoFront?.Count ?? 0
            };

            if (result.AllVariations.Any())
            {
                stats.BestFitness = result.AllVariations.Max(v => v.Fitness);
                stats.AverageFitness = result.AllVariations.Average(v => v.Fitness);
                stats.FitnessStdDev = CalculateStdDev(result.AllVariations.Select(v => v.Fitness));
            }

            return stats;
        }

        private double CalculateStdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            var avg = list.Average();
            var sumSquares = list.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquares / (list.Count - 1));
        }

        #endregion
    }

    #region Supporting Classes

    internal class ConstraintEvaluator
    {
        public List<ConstraintResult> Evaluate(DesignVariant design, List<DesignConstraint> constraints)
        {
            return constraints.Select(c => EvaluateConstraint(design, c)).ToList();
        }

        private ConstraintResult EvaluateConstraint(DesignVariant design, DesignConstraint constraint)
        {
            var paramValue = design.ParameterValues.GetValueOrDefault(constraint.ParameterName, 0);
            var isSatisfied = constraint.Type switch
            {
                ConstraintType.MinValue => paramValue >= constraint.Value,
                ConstraintType.MaxValue => paramValue <= constraint.Value,
                ConstraintType.Equals => Math.Abs(paramValue - constraint.Value) < constraint.Tolerance,
                ConstraintType.Range => paramValue >= constraint.MinValue && paramValue <= constraint.MaxValue,
                ConstraintType.Expression => EvaluateExpression(design, constraint.Expression),
                _ => true
            };

            return new ConstraintResult
            {
                Constraint = constraint,
                IsSatisfied = isSatisfied,
                ActualValue = paramValue,
                Violation = isSatisfied ? 0 : CalculateViolation(paramValue, constraint)
            };
        }

        private bool EvaluateExpression(DesignVariant design, string expression)
        {
            // Simplified expression evaluation
            return true;
        }

        private double CalculateViolation(double value, DesignConstraint constraint)
        {
            return constraint.Type switch
            {
                ConstraintType.MinValue => Math.Max(0, constraint.Value - value),
                ConstraintType.MaxValue => Math.Max(0, value - constraint.Value),
                ConstraintType.Range => Math.Max(0, Math.Max(constraint.MinValue - value, value - constraint.MaxValue)),
                _ => 0
            };
        }
    }

    internal class ObjectiveEvaluator
    {
        public double Evaluate(DesignVariant design, DesignObjective objective)
        {
            return objective.Type switch
            {
                ObjectiveType.Parameter => design.ParameterValues.GetValueOrDefault(objective.ParameterName, 0),
                ObjectiveType.Formula => EvaluateFormula(design, objective.Formula),
                ObjectiveType.Simulation => RunSimulation(design, objective),
                ObjectiveType.Custom => objective.CustomEvaluator?.Invoke(design) ?? 0,
                _ => 0
            };
        }

        private double EvaluateFormula(DesignVariant design, string formula)
        {
            // Simplified formula evaluation - would use expression parser
            return 0;
        }

        private double RunSimulation(DesignVariant design, DesignObjective objective)
        {
            // Would integrate with simulation engine
            return 0;
        }
    }

    #endregion

    #region Data Models

    public class VariationSettings
    {
        public int PopulationSize { get; set; } = 100;
        public int MaxGenerations { get; set; } = 50;
        public double MutationRate { get; set; } = 0.1;
        public double ParameterMutationRate { get; set; } = 0.2;
        public double MutationStrength { get; set; } = 0.1;
        public int TournamentSize { get; set; } = 3;
        public int MaxResultVariations { get; set; } = 50;
        public int? RandomSeed { get; set; }
    }

    public class VariationProgress
    {
        public string Phase { get; set; }
        public int PercentComplete { get; set; }
        public double BestFitness { get; set; }
    }

    public class VariationResult
    {
        public string BaseDesignId { get; set; }
        public int ValidVariations { get; set; }
        public List<DesignVariant> AllVariations { get; set; } = new();
        public List<DesignVariant> ParetoFront { get; set; } = new();
        public VariationStatistics Statistics { get; set; }
    }

    public class VariationStatistics
    {
        public int TotalGenerated { get; set; }
        public int ValidVariations { get; set; }
        public int ParetoFrontSize { get; set; }
        public double BestFitness { get; set; }
        public double AverageFitness { get; set; }
        public double FitnessStdDev { get; set; }
    }

    public class DesignVariant
    {
        public string Id { get; set; }
        public string BaseDesignId { get; set; }
        public int Generation { get; set; }
        public Dictionary<string, double> ParameterValues { get; set; } = new();
        public Dictionary<string, double> ObjectiveValues { get; set; } = new();
        public double Fitness { get; set; }
        public List<ConstraintResult> ConstraintViolations { get; set; } = new();
    }

    public class DesignModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, double> Parameters { get; set; } = new();
    }

    public class ParameterRange
    {
        public string ParameterName { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double DefaultValue { get; set; }
        public double Step { get; set; }
        public List<double> DiscreteValues { get; set; }
        public string Unit { get; set; }
    }

    public class DesignConstraint
    {
        public string Name { get; set; }
        public string ParameterName { get; set; }
        public ConstraintType Type { get; set; }
        public double Value { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double Tolerance { get; set; } = 0.001;
        public string Expression { get; set; }
    }

    public class ConstraintResult
    {
        public DesignConstraint Constraint { get; set; }
        public bool IsSatisfied { get; set; }
        public double ActualValue { get; set; }
        public double Violation { get; set; }
    }

    public class DesignObjective
    {
        public string Name { get; set; }
        public string ParameterName { get; set; }
        public ObjectiveType Type { get; set; }
        public bool Maximize { get; set; } = true;
        public double Weight { get; set; } = 1.0;
        public double ExpectedMin { get; set; }
        public double ExpectedMax { get; set; }
        public string Formula { get; set; }
        public Func<DesignVariant, double> CustomEvaluator { get; set; }
    }

    public class SensitivityResult
    {
        public Dictionary<string, ParameterSensitivity> ParameterSensitivities { get; } = new();
        public List<string> RankedParameters { get; set; } = new();
    }

    public class ParameterSensitivity
    {
        public string ParameterName { get; set; }
        public Dictionary<string, double> ObjectiveSensitivities { get; } = new();
        public double OverallInfluence { get; set; }
    }

    public class OptimizationResult
    {
        public DesignObjective Objective { get; set; }
        public DesignVariant BestDesign { get; set; }
        public List<double> ConvergenceHistory { get; set; } = new();
    }

    public enum ConstraintType { MinValue, MaxValue, Equals, Range, Expression }
    public enum ObjectiveType { Parameter, Formula, Simulation, Custom }

    #endregion
}
