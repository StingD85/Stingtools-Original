// StingBIM.AI.Reasoning.Optimization.DesignOptimizer
// Design optimization engine using multi-objective optimization
// Master Proposal Reference: Part 2.2 Strategy 4 - Predictive Modeling (Optimization)

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Reasoning.Optimization
{
    /// <summary>
    /// Design optimization engine that optimizes layouts for multiple objectives:
    /// space efficiency, circulation, adjacency requirements, natural light, and cost.
    /// </summary>
    public class DesignOptimizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Random _random;
        private readonly OptimizationConfig _config;
        private readonly List<OptimizationObjective> _objectives;
        private readonly List<OptimizationConstraint> _constraints;

        public DesignOptimizer(OptimizationConfig config = null)
        {
            _config = config ?? new OptimizationConfig();
            _random = new Random(_config.RandomSeed);
            _objectives = new List<OptimizationObjective>();
            _constraints = new List<OptimizationConstraint>();

            InitializeDefaultObjectives();
            InitializeDefaultConstraints();
        }

        #region Public API

        /// <summary>
        /// Optimizes a layout using genetic algorithm.
        /// </summary>
        public OptimizationResult OptimizeLayout(LayoutProblem problem)
        {
            Logger.Info($"Starting layout optimization with {problem.Rooms.Count} rooms");

            var result = new OptimizationResult
            {
                StartTime = DateTime.Now,
                Problem = problem
            };

            // Initialize population
            var population = InitializePopulation(problem, _config.PopulationSize);

            // Evaluate initial population
            foreach (var individual in population)
            {
                individual.Fitness = EvaluateFitness(individual, problem);
            }

            result.InitialBestFitness = population.Max(i => i.Fitness);

            // Evolution loop
            for (int generation = 0; generation < _config.MaxGenerations; generation++)
            {
                // Selection
                var parents = SelectParents(population);

                // Crossover
                var offspring = Crossover(parents, problem);

                // Mutation
                Mutate(offspring, problem);

                // Evaluate offspring
                foreach (var individual in offspring)
                {
                    individual.Fitness = EvaluateFitness(individual, problem);
                }

                // Replacement
                population = Replace(population, offspring);

                // Track progress
                var bestFitness = population.Max(i => i.Fitness);
                result.FitnessHistory.Add(bestFitness);

                if (generation % 10 == 0)
                {
                    Logger.Debug($"Generation {generation}: Best fitness = {bestFitness:F4}");
                }

                // Early termination if converged
                if (HasConverged(result.FitnessHistory))
                {
                    Logger.Info($"Converged at generation {generation}");
                    break;
                }
            }

            // Get best solution
            var bestIndividual = population.OrderByDescending(i => i.Fitness).First();
            result.BestSolution = bestIndividual;
            result.FinalBestFitness = bestIndividual.Fitness;
            result.EndTime = DateTime.Now;
            result.Generations = result.FitnessHistory.Count;

            // Generate detailed analysis
            result.Analysis = AnalyzeSolution(bestIndividual, problem);

            Logger.Info($"Optimization complete. Fitness improved from {result.InitialBestFitness:F4} to {result.FinalBestFitness:F4}");

            return result;
        }

        /// <summary>
        /// Optimizes room placement for a single room.
        /// </summary>
        public PlacementResult OptimizePlacement(RoomPlacementProblem problem)
        {
            var result = new PlacementResult { Room = problem.Room };

            var candidates = GeneratePlacementCandidates(problem);
            var scoredCandidates = new List<(PlacementCandidate Candidate, double Score)>();

            foreach (var candidate in candidates)
            {
                var score = EvaluatePlacement(candidate, problem);
                scoredCandidates.Add((candidate, score));
            }

            var best = scoredCandidates.OrderByDescending(c => c.Score).First();
            result.RecommendedPosition = best.Candidate.Position;
            result.RecommendedRotation = best.Candidate.Rotation;
            result.Score = best.Score;
            result.AlternativePlacements = scoredCandidates
                .OrderByDescending(c => c.Score)
                .Skip(1)
                .Take(3)
                .Select(c => new AlternativePlacement
                {
                    Position = c.Candidate.Position,
                    Rotation = c.Candidate.Rotation,
                    Score = c.Score
                })
                .ToList();

            return result;
        }

        /// <summary>
        /// Suggests improvements for an existing layout.
        /// </summary>
        public List<LayoutImprovement> SuggestImprovements(Layout currentLayout)
        {
            var improvements = new List<LayoutImprovement>();

            // Analyze current layout
            var metrics = CalculateLayoutMetrics(currentLayout);

            // Check space efficiency
            if (metrics.SpaceEfficiency < 0.75)
            {
                improvements.Add(new LayoutImprovement
                {
                    Type = ImprovementType.SpaceEfficiency,
                    Description = "Layout has low space efficiency. Consider reducing circulation area.",
                    Impact = 0.3,
                    Priority = Priority.High,
                    Suggestions = IdentifyWastedSpace(currentLayout)
                });
            }

            // Check adjacency requirements
            var adjacencyViolations = CheckAdjacencyRequirements(currentLayout);
            if (adjacencyViolations.Any())
            {
                improvements.Add(new LayoutImprovement
                {
                    Type = ImprovementType.Adjacency,
                    Description = $"Found {adjacencyViolations.Count} adjacency issues",
                    Impact = 0.25,
                    Priority = Priority.Medium,
                    Suggestions = adjacencyViolations
                });
            }

            // Check natural light access
            var lightIssues = CheckNaturalLight(currentLayout);
            if (lightIssues.Any())
            {
                improvements.Add(new LayoutImprovement
                {
                    Type = ImprovementType.NaturalLight,
                    Description = "Some rooms lack adequate natural light access",
                    Impact = 0.2,
                    Priority = Priority.Medium,
                    Suggestions = lightIssues
                });
            }

            // Check circulation efficiency
            if (metrics.CirculationEfficiency < 0.7)
            {
                improvements.Add(new LayoutImprovement
                {
                    Type = ImprovementType.Circulation,
                    Description = "Circulation paths are inefficient",
                    Impact = 0.15,
                    Priority = Priority.Low,
                    Suggestions = SuggestCirculationImprovements(currentLayout)
                });
            }

            // Check proportions
            var proportionIssues = CheckRoomProportions(currentLayout);
            if (proportionIssues.Any())
            {
                improvements.Add(new LayoutImprovement
                {
                    Type = ImprovementType.Proportions,
                    Description = "Some rooms have suboptimal proportions",
                    Impact = 0.1,
                    Priority = Priority.Low,
                    Suggestions = proportionIssues
                });
            }

            return improvements.OrderByDescending(i => i.Impact).ToList();
        }

        /// <summary>
        /// Adds a custom optimization objective.
        /// </summary>
        public void AddObjective(OptimizationObjective objective)
        {
            _objectives.Add(objective);
        }

        /// <summary>
        /// Adds a custom optimization constraint.
        /// </summary>
        public void AddConstraint(OptimizationConstraint constraint)
        {
            _constraints.Add(constraint);
        }

        #endregion

        #region Initialization

        private void InitializeDefaultObjectives()
        {
            // Space efficiency (net-to-gross ratio)
            _objectives.Add(new OptimizationObjective
            {
                Name = "SpaceEfficiency",
                Weight = 0.25,
                Evaluate = (solution, problem) =>
                {
                    var totalRoomArea = solution.RoomPlacements.Sum(r => r.Width * r.Length);
                    var boundingArea = CalculateBoundingArea(solution);
                    return totalRoomArea / Math.Max(boundingArea, 1);
                }
            });

            // Adjacency satisfaction
            _objectives.Add(new OptimizationObjective
            {
                Name = "AdjacencySatisfaction",
                Weight = 0.30,
                Evaluate = (solution, problem) =>
                {
                    var satisfied = 0;
                    var total = 0;

                    foreach (var req in problem.AdjacencyRequirements)
                    {
                        total++;
                        if (AreAdjacent(solution, req.Room1, req.Room2))
                        {
                            satisfied++;
                        }
                    }

                    return total > 0 ? (double)satisfied / total : 1.0;
                }
            });

            // Natural light access
            _objectives.Add(new OptimizationObjective
            {
                Name = "NaturalLight",
                Weight = 0.20,
                Evaluate = (solution, problem) =>
                {
                    var roomsNeedingLight = solution.RoomPlacements
                        .Where(r => RoomNeedsNaturalLight(r.RoomType))
                        .ToList();

                    if (!roomsNeedingLight.Any()) return 1.0;

                    var roomsWithLight = roomsNeedingLight
                        .Count(r => HasExteriorAccess(r, problem.BuildingOutline));

                    return (double)roomsWithLight / roomsNeedingLight.Count;
                }
            });

            // Circulation efficiency
            _objectives.Add(new OptimizationObjective
            {
                Name = "CirculationEfficiency",
                Weight = 0.15,
                Evaluate = (solution, problem) =>
                {
                    // Penalize solutions where rooms block access to other rooms
                    var accessibleRooms = CountAccessibleRooms(solution);
                    return (double)accessibleRooms / solution.RoomPlacements.Count;
                }
            });

            // Compactness
            _objectives.Add(new OptimizationObjective
            {
                Name = "Compactness",
                Weight = 0.10,
                Evaluate = (solution, problem) =>
                {
                    // Prefer more compact layouts
                    var boundingArea = CalculateBoundingArea(solution);
                    var convexHullArea = CalculateConvexHullArea(solution);
                    return boundingArea > 0 ? convexHullArea / boundingArea : 0;
                }
            });
        }

        private void InitializeDefaultConstraints()
        {
            // No overlaps
            _constraints.Add(new OptimizationConstraint
            {
                Name = "NoOverlaps",
                IsSatisfied = (solution, problem) =>
                {
                    for (int i = 0; i < solution.RoomPlacements.Count; i++)
                    {
                        for (int j = i + 1; j < solution.RoomPlacements.Count; j++)
                        {
                            if (RoomsOverlap(solution.RoomPlacements[i], solution.RoomPlacements[j]))
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                },
                Penalty = 1.0 // Maximum penalty for overlaps
            });

            // Within building boundary
            _constraints.Add(new OptimizationConstraint
            {
                Name = "WithinBoundary",
                IsSatisfied = (solution, problem) =>
                {
                    if (problem.BuildingOutline == null) return true;

                    foreach (var room in solution.RoomPlacements)
                    {
                        if (!IsWithinBoundary(room, problem.BuildingOutline))
                        {
                            return false;
                        }
                    }
                    return true;
                },
                Penalty = 0.8
            });

            // Minimum room sizes
            _constraints.Add(new OptimizationConstraint
            {
                Name = "MinimumRoomSize",
                IsSatisfied = (solution, problem) =>
                {
                    foreach (var room in solution.RoomPlacements)
                    {
                        var minArea = GetMinimumRoomArea(room.RoomType);
                        if (room.Width * room.Length < minArea)
                        {
                            return false;
                        }
                    }
                    return true;
                },
                Penalty = 0.5
            });
        }

        #endregion

        #region Genetic Algorithm

        private List<LayoutSolution> InitializePopulation(LayoutProblem problem, int size)
        {
            var population = new List<LayoutSolution>();

            for (int i = 0; i < size; i++)
            {
                var solution = GenerateRandomSolution(problem);
                population.Add(solution);
            }

            return population;
        }

        private LayoutSolution GenerateRandomSolution(LayoutProblem problem)
        {
            var solution = new LayoutSolution();

            foreach (var room in problem.Rooms)
            {
                var placement = new RoomPlacement
                {
                    RoomId = room.Id,
                    RoomType = room.Type,
                    Width = room.Width,
                    Length = room.Length,
                    X = _random.NextDouble() * (problem.MaxX - room.Width),
                    Y = _random.NextDouble() * (problem.MaxY - room.Length),
                    Rotation = _random.Next(2) * 90 // 0 or 90 degrees
                };

                solution.RoomPlacements.Add(placement);
            }

            return solution;
        }

        private List<LayoutSolution> SelectParents(List<LayoutSolution> population)
        {
            // Tournament selection
            var parents = new List<LayoutSolution>();
            var tournamentSize = 3;

            for (int i = 0; i < population.Count; i++)
            {
                var tournament = population
                    .OrderBy(x => _random.Next())
                    .Take(tournamentSize)
                    .ToList();

                parents.Add(tournament.OrderByDescending(t => t.Fitness).First());
            }

            return parents;
        }

        private List<LayoutSolution> Crossover(List<LayoutSolution> parents, LayoutProblem problem)
        {
            var offspring = new List<LayoutSolution>();

            for (int i = 0; i < parents.Count - 1; i += 2)
            {
                if (_random.NextDouble() < _config.CrossoverRate)
                {
                    var (child1, child2) = PerformCrossover(parents[i], parents[i + 1]);
                    offspring.Add(child1);
                    offspring.Add(child2);
                }
                else
                {
                    offspring.Add(parents[i].Clone());
                    offspring.Add(parents[i + 1].Clone());
                }
            }

            return offspring;
        }

        private (LayoutSolution, LayoutSolution) PerformCrossover(LayoutSolution parent1, LayoutSolution parent2)
        {
            var child1 = new LayoutSolution();
            var child2 = new LayoutSolution();

            var crossoverPoint = _random.Next(parent1.RoomPlacements.Count);

            for (int i = 0; i < parent1.RoomPlacements.Count; i++)
            {
                if (i < crossoverPoint)
                {
                    child1.RoomPlacements.Add(parent1.RoomPlacements[i].Clone());
                    child2.RoomPlacements.Add(parent2.RoomPlacements[i].Clone());
                }
                else
                {
                    child1.RoomPlacements.Add(parent2.RoomPlacements[i].Clone());
                    child2.RoomPlacements.Add(parent1.RoomPlacements[i].Clone());
                }
            }

            return (child1, child2);
        }

        private void Mutate(List<LayoutSolution> offspring, LayoutProblem problem)
        {
            foreach (var solution in offspring)
            {
                foreach (var room in solution.RoomPlacements)
                {
                    if (_random.NextDouble() < _config.MutationRate)
                    {
                        // Randomly modify position
                        room.X += (_random.NextDouble() - 0.5) * 2 * _config.MutationStep;
                        room.Y += (_random.NextDouble() - 0.5) * 2 * _config.MutationStep;

                        // Keep within bounds
                        room.X = Math.Max(0, Math.Min(room.X, problem.MaxX - room.Width));
                        room.Y = Math.Max(0, Math.Min(room.Y, problem.MaxY - room.Length));
                    }

                    if (_random.NextDouble() < _config.MutationRate * 0.5)
                    {
                        // Rotate
                        room.Rotation = (room.Rotation + 90) % 360;
                        var temp = room.Width;
                        room.Width = room.Length;
                        room.Length = temp;
                    }
                }
            }
        }

        private List<LayoutSolution> Replace(List<LayoutSolution> population, List<LayoutSolution> offspring)
        {
            // Elitism: keep best from current population
            var combined = population.Concat(offspring)
                .OrderByDescending(i => i.Fitness)
                .Take(population.Count)
                .ToList();

            return combined;
        }

        private double EvaluateFitness(LayoutSolution solution, LayoutProblem problem)
        {
            // Check constraints first
            var constraintPenalty = 0.0;
            foreach (var constraint in _constraints)
            {
                if (!constraint.IsSatisfied(solution, problem))
                {
                    constraintPenalty += constraint.Penalty;
                }
            }

            if (constraintPenalty >= 1.0)
            {
                return 0; // Infeasible solution
            }

            // Evaluate objectives
            var totalScore = 0.0;
            var totalWeight = 0.0;

            foreach (var objective in _objectives)
            {
                var score = objective.Evaluate(solution, problem);
                totalScore += score * objective.Weight;
                totalWeight += objective.Weight;
            }

            var objectiveScore = totalWeight > 0 ? totalScore / totalWeight : 0;

            return objectiveScore * (1 - constraintPenalty);
        }

        private bool HasConverged(List<double> history)
        {
            if (history.Count < 20) return false;

            var recent = history.Skip(history.Count - 10).ToList();
            var variance = recent.Average(x => Math.Pow(x - recent.Average(), 2));

            return variance < 0.0001;
        }

        #endregion

        #region Helper Methods

        private double CalculateBoundingArea(LayoutSolution solution)
        {
            if (!solution.RoomPlacements.Any()) return 0;

            var minX = solution.RoomPlacements.Min(r => r.X);
            var minY = solution.RoomPlacements.Min(r => r.Y);
            var maxX = solution.RoomPlacements.Max(r => r.X + r.Width);
            var maxY = solution.RoomPlacements.Max(r => r.Y + r.Length);

            return (maxX - minX) * (maxY - minY);
        }

        private double CalculateConvexHullArea(LayoutSolution solution)
        {
            // Simplified - use bounding box
            return CalculateBoundingArea(solution);
        }

        private bool AreAdjacent(LayoutSolution solution, string room1Id, string room2Id)
        {
            var r1 = solution.RoomPlacements.FirstOrDefault(r => r.RoomId == room1Id);
            var r2 = solution.RoomPlacements.FirstOrDefault(r => r.RoomId == room2Id);

            if (r1 == null || r2 == null) return false;

            var tolerance = 0.5; // Allow small gap
            return GetDistance(r1, r2) <= tolerance;
        }

        private double GetDistance(RoomPlacement r1, RoomPlacement r2)
        {
            var dx = Math.Max(0, Math.Max(r1.X - (r2.X + r2.Width), r2.X - (r1.X + r1.Width)));
            var dy = Math.Max(0, Math.Max(r1.Y - (r2.Y + r2.Length), r2.Y - (r1.Y + r1.Length)));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private bool RoomNeedsNaturalLight(string roomType)
        {
            var requiresLight = new[] { "bedroom", "living", "office", "kitchen", "dining" };
            return requiresLight.Any(t => roomType.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasExteriorAccess(RoomPlacement room, BuildingOutline outline)
        {
            if (outline == null) return true;

            // Check if room touches any exterior edge
            var tolerance = 0.1;
            return room.X <= tolerance ||
                   room.Y <= tolerance ||
                   room.X + room.Width >= outline.Width - tolerance ||
                   room.Y + room.Length >= outline.Length - tolerance;
        }

        private int CountAccessibleRooms(LayoutSolution solution)
        {
            // Simplified - count rooms not completely surrounded
            return solution.RoomPlacements.Count;
        }

        private bool RoomsOverlap(RoomPlacement r1, RoomPlacement r2)
        {
            return !(r1.X + r1.Width <= r2.X ||
                     r2.X + r2.Width <= r1.X ||
                     r1.Y + r1.Length <= r2.Y ||
                     r2.Y + r2.Length <= r1.Y);
        }

        private bool IsWithinBoundary(RoomPlacement room, BuildingOutline outline)
        {
            return room.X >= 0 &&
                   room.Y >= 0 &&
                   room.X + room.Width <= outline.Width &&
                   room.Y + room.Length <= outline.Length;
        }

        private double GetMinimumRoomArea(string roomType)
        {
            return roomType.ToLowerInvariant() switch
            {
                "bedroom" => 9.0,
                "bathroom" => 3.0,
                "kitchen" => 5.0,
                "living" or "living room" => 12.0,
                _ => 4.0
            };
        }

        private List<PlacementCandidate> GeneratePlacementCandidates(RoomPlacementProblem problem)
        {
            var candidates = new List<PlacementCandidate>();
            var step = 1.0; // 1 meter grid

            for (double x = 0; x <= problem.MaxX - problem.Room.Width; x += step)
            {
                for (double y = 0; y <= problem.MaxY - problem.Room.Length; y += step)
                {
                    candidates.Add(new PlacementCandidate
                    {
                        Position = new Point2D { X = x, Y = y },
                        Rotation = 0
                    });

                    // Also try rotated
                    if (problem.Room.Width != problem.Room.Length)
                    {
                        candidates.Add(new PlacementCandidate
                        {
                            Position = new Point2D { X = x, Y = y },
                            Rotation = 90
                        });
                    }
                }
            }

            return candidates;
        }

        private double EvaluatePlacement(PlacementCandidate candidate, RoomPlacementProblem problem)
        {
            var score = 1.0;

            // Check overlaps with existing rooms
            foreach (var existing in problem.ExistingRooms)
            {
                var testRoom = new RoomPlacement
                {
                    X = candidate.Position.X,
                    Y = candidate.Position.Y,
                    Width = candidate.Rotation == 90 ? problem.Room.Length : problem.Room.Width,
                    Length = candidate.Rotation == 90 ? problem.Room.Width : problem.Room.Length
                };

                if (RoomsOverlap(testRoom, existing))
                {
                    return 0; // Invalid placement
                }
            }

            // Score based on adjacency preferences
            foreach (var pref in problem.AdjacencyPreferences)
            {
                var adjacent = problem.ExistingRooms.FirstOrDefault(r => r.RoomId == pref.RoomId);
                if (adjacent != null)
                {
                    var distance = Math.Sqrt(
                        Math.Pow(candidate.Position.X - adjacent.X, 2) +
                        Math.Pow(candidate.Position.Y - adjacent.Y, 2));

                    if (pref.PreferAdjacent)
                    {
                        score += 0.2 / (1 + distance);
                    }
                    else
                    {
                        score += 0.1 * Math.Min(distance / 10, 1);
                    }
                }
            }

            return Math.Min(1.0, score);
        }

        private LayoutMetrics CalculateLayoutMetrics(Layout layout)
        {
            var totalRoomArea = layout.Rooms.Sum(r => r.Width * r.Length);
            var boundingArea = layout.Width * layout.Length;

            return new LayoutMetrics
            {
                SpaceEfficiency = totalRoomArea / boundingArea,
                CirculationEfficiency = 0.8, // Simplified
                NaturalLightRatio = 0.7 // Simplified
            };
        }

        private List<string> IdentifyWastedSpace(Layout layout)
        {
            return new List<string>
            {
                "Consider reducing corridor width",
                "Dead space in corner could be utilized"
            };
        }

        private List<string> CheckAdjacencyRequirements(Layout layout)
        {
            var violations = new List<string>();

            // Check kitchen-dining adjacency
            var kitchen = layout.Rooms.FirstOrDefault(r => r.Type.Contains("kitchen", StringComparison.OrdinalIgnoreCase));
            var dining = layout.Rooms.FirstOrDefault(r => r.Type.Contains("dining", StringComparison.OrdinalIgnoreCase));

            if (kitchen != null && dining != null)
            {
                if (!AreRoomsAdjacent(kitchen, dining))
                {
                    violations.Add("Kitchen should be adjacent to dining room");
                }
            }

            return violations;
        }

        private bool AreRoomsAdjacent(LayoutRoom r1, LayoutRoom r2)
        {
            var tolerance = 0.5;
            var dx = Math.Max(0, Math.Max(r1.X - (r2.X + r2.Width), r2.X - (r1.X + r1.Width)));
            var dy = Math.Max(0, Math.Max(r1.Y - (r2.Y + r2.Length), r2.Y - (r1.Y + r1.Length)));
            return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
        }

        private List<string> CheckNaturalLight(Layout layout)
        {
            var issues = new List<string>();

            foreach (var room in layout.Rooms)
            {
                if (RoomNeedsNaturalLight(room.Type))
                {
                    if (!room.HasExteriorWall)
                    {
                        issues.Add($"{room.Name} lacks natural light access");
                    }
                }
            }

            return issues;
        }

        private List<string> SuggestCirculationImprovements(Layout layout)
        {
            return new List<string>
            {
                "Consider central hallway for better circulation",
                "Reduce number of turns in main circulation path"
            };
        }

        private List<string> CheckRoomProportions(Layout layout)
        {
            var issues = new List<string>();

            foreach (var room in layout.Rooms)
            {
                var aspectRatio = Math.Max(room.Width, room.Length) / Math.Min(room.Width, room.Length);
                if (aspectRatio > 2.5)
                {
                    issues.Add($"{room.Name} is too elongated (aspect ratio {aspectRatio:F1}:1)");
                }
            }

            return issues;
        }

        private SolutionAnalysis AnalyzeSolution(LayoutSolution solution, LayoutProblem problem)
        {
            var analysis = new SolutionAnalysis();

            foreach (var objective in _objectives)
            {
                var score = objective.Evaluate(solution, problem);
                analysis.ObjectiveScores[objective.Name] = score;
            }

            analysis.ConstraintsSatisfied = _constraints.All(c => c.IsSatisfied(solution, problem));
            analysis.TotalArea = solution.RoomPlacements.Sum(r => r.Width * r.Length);
            analysis.BoundingArea = CalculateBoundingArea(solution);
            analysis.Efficiency = analysis.BoundingArea > 0 ? analysis.TotalArea / analysis.BoundingArea : 0;

            return analysis;
        }

        #endregion
    }

    #region Supporting Types

    public class OptimizationConfig
    {
        public int PopulationSize { get; set; } = 50;
        public int MaxGenerations { get; set; } = 100;
        public double CrossoverRate { get; set; } = 0.8;
        public double MutationRate { get; set; } = 0.1;
        public double MutationStep { get; set; } = 1.0;
        public int RandomSeed { get; set; } = 42;
    }

    public class LayoutProblem
    {
        public List<RoomDefinition> Rooms { get; set; } = new();
        public List<AdjacencyRequirement> AdjacencyRequirements { get; set; } = new();
        public BuildingOutline BuildingOutline { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public class RoomDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
    }

    public class AdjacencyRequirement
    {
        public string Room1 { get; set; }
        public string Room2 { get; set; }
        public bool Required { get; set; } = true;
    }

    public class BuildingOutline
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public List<Point2D> Points { get; set; } = new();
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class LayoutSolution
    {
        public List<RoomPlacement> RoomPlacements { get; set; } = new();
        public double Fitness { get; set; }

        public LayoutSolution Clone()
        {
            return new LayoutSolution
            {
                RoomPlacements = RoomPlacements.Select(r => r.Clone()).ToList(),
                Fitness = Fitness
            };
        }
    }

    public class RoomPlacement
    {
        public string RoomId { get; set; }
        public string RoomType { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Rotation { get; set; }

        public RoomPlacement Clone()
        {
            return new RoomPlacement
            {
                RoomId = RoomId,
                RoomType = RoomType,
                X = X,
                Y = Y,
                Width = Width,
                Length = Length,
                Rotation = Rotation
            };
        }
    }

    public class OptimizationObjective
    {
        public string Name { get; set; }
        public double Weight { get; set; }
        public Func<LayoutSolution, LayoutProblem, double> Evaluate { get; set; }
    }

    public class OptimizationConstraint
    {
        public string Name { get; set; }
        public Func<LayoutSolution, LayoutProblem, bool> IsSatisfied { get; set; }
        public double Penalty { get; set; }
    }

    public class OptimizationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public LayoutProblem Problem { get; set; }
        public LayoutSolution BestSolution { get; set; }
        public double InitialBestFitness { get; set; }
        public double FinalBestFitness { get; set; }
        public int Generations { get; set; }
        public List<double> FitnessHistory { get; set; } = new();
        public SolutionAnalysis Analysis { get; set; }
    }

    public class SolutionAnalysis
    {
        public Dictionary<string, double> ObjectiveScores { get; set; } = new();
        public bool ConstraintsSatisfied { get; set; }
        public double TotalArea { get; set; }
        public double BoundingArea { get; set; }
        public double Efficiency { get; set; }
    }

    public class RoomPlacementProblem
    {
        public RoomDefinition Room { get; set; }
        public List<RoomPlacement> ExistingRooms { get; set; } = new();
        public List<AdjacencyPreference> AdjacencyPreferences { get; set; } = new();
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public class AdjacencyPreference
    {
        public string RoomId { get; set; }
        public bool PreferAdjacent { get; set; }
    }

    public class PlacementCandidate
    {
        public Point2D Position { get; set; }
        public double Rotation { get; set; }
    }

    public class PlacementResult
    {
        public RoomDefinition Room { get; set; }
        public Point2D RecommendedPosition { get; set; }
        public double RecommendedRotation { get; set; }
        public double Score { get; set; }
        public List<AlternativePlacement> AlternativePlacements { get; set; }
    }

    public class AlternativePlacement
    {
        public Point2D Position { get; set; }
        public double Rotation { get; set; }
        public double Score { get; set; }
    }

    public class Layout
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public List<LayoutRoom> Rooms { get; set; } = new();
    }

    public class LayoutRoom
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public bool HasExteriorWall { get; set; }
    }

    public class LayoutMetrics
    {
        public double SpaceEfficiency { get; set; }
        public double CirculationEfficiency { get; set; }
        public double NaturalLightRatio { get; set; }
    }

    public class LayoutImprovement
    {
        public ImprovementType Type { get; set; }
        public string Description { get; set; }
        public double Impact { get; set; }
        public Priority Priority { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    public enum ImprovementType
    {
        SpaceEfficiency,
        Adjacency,
        NaturalLight,
        Circulation,
        Proportions
    }

    public enum Priority { Low, Medium, High }

    #endregion
}
