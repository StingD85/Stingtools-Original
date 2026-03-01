// =========================================================================
// StingBIM.AI.Creation - Generative Creation Engine
// AI-powered generative design for automated creation of building elements
// =========================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Creation.Generative
{
    /// <summary>
    /// Generative design engine that creates optimal building layouts,
    /// element configurations, and design alternatives using AI algorithms.
    /// </summary>
    public class GenerativeCreationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly LayoutGenerator _layoutGenerator;
        private readonly FacadeGenerator _facadeGenerator;
        private readonly StructuralGridGenerator _structuralGridGenerator;
        private readonly MEPLayoutGenerator _mepLayoutGenerator;
        private readonly VariantGenerator _variantGenerator;
        private readonly FitnessEvaluator _fitnessEvaluator;
        private readonly ConstraintSolver _constraintSolver;

        private readonly GenerativeConfiguration _configuration;
        private readonly Random _random;

        public GenerativeCreationEngine()
        {
            _layoutGenerator = new LayoutGenerator();
            _facadeGenerator = new FacadeGenerator();
            _structuralGridGenerator = new StructuralGridGenerator();
            _mepLayoutGenerator = new MEPLayoutGenerator();
            _variantGenerator = new VariantGenerator();
            _fitnessEvaluator = new FitnessEvaluator();
            _constraintSolver = new ConstraintSolver();

            _configuration = new GenerativeConfiguration();
            _random = new Random();

            Logger.Info("GenerativeCreationEngine initialized successfully");
        }

        #region Layout Generation

        /// <summary>
        /// Generates optimal floor layouts based on a program and constraints.
        /// </summary>
        public async Task<LayoutGenerationResult> GenerateLayoutAsync(
            BuildingProgram program,
            LayoutConstraints constraints,
            GenerativeOptions options,
            IProgress<GenerativeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating layout for {program.Rooms.Count} rooms");

            var result = new LayoutGenerationResult
            {
                ProgramId = program.Id,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Step 1: Initialize population
                progress?.Report(new GenerativeProgress { Stage = "Initializing", Percentage = 5 });
                var population = await InitializeLayoutPopulationAsync(program, constraints, options, cancellationToken);

                // Step 2: Run genetic algorithm
                var generationCount = options.MaxGenerations;
                for (int gen = 0; gen < generationCount; gen++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new GenerativeProgress
                    {
                        Stage = $"Generation {gen + 1}/{generationCount}",
                        Percentage = 10 + (gen * 80 / generationCount)
                    });

                    // Evaluate fitness
                    await EvaluateLayoutFitnessAsync(population, program, constraints, cancellationToken);

                    // Check for convergence
                    if (HasConverged(population, options.ConvergenceThreshold))
                    {
                        Logger.Info($"Converged at generation {gen + 1}");
                        break;
                    }

                    // Selection
                    var selected = SelectFittestLayouts(population, options.SelectionCount);

                    // Crossover
                    var offspring = await CrossoverLayoutsAsync(selected, options, cancellationToken);

                    // Mutation
                    await MutateLayoutsAsync(offspring, options.MutationRate, cancellationToken);

                    // Replace population
                    population = MergePopulations(selected, offspring, options.PopulationSize);
                }

                // Step 3: Select best layouts
                progress?.Report(new GenerativeProgress { Stage = "Selecting Best", Percentage = 95 });
                result.GeneratedLayouts = SelectBestLayouts(population, options.ResultCount);

                // Step 4: Post-process layouts
                foreach (var layout in result.GeneratedLayouts)
                {
                    await PostProcessLayoutAsync(layout, constraints, cancellationToken);
                }

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"Generated {result.GeneratedLayouts.Count} layouts");
                progress?.Report(new GenerativeProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating layout");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates room configurations within a given boundary.
        /// </summary>
        public async Task<RoomConfigurationResult> GenerateRoomConfigurationAsync(
            RoomRequirements requirements,
            BoundaryPolygon boundary,
            RoomConfigOptions options,
            IProgress<GenerativeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating room configuration for {requirements.RoomCount} rooms");

            var result = new RoomConfigurationResult
            {
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Analyze boundary
                progress?.Report(new GenerativeProgress { Stage = "Analyzing Boundary", Percentage = 10 });
                var boundaryAnalysis = AnalyzeBoundary(boundary);

                // Generate initial placements
                progress?.Report(new GenerativeProgress { Stage = "Initial Placement", Percentage = 30 });
                var initialRooms = await GenerateInitialRoomPlacementAsync(
                    requirements, boundary, boundaryAnalysis, cancellationToken);

                // Optimize placements
                progress?.Report(new GenerativeProgress { Stage = "Optimizing", Percentage = 60 });
                var optimizedRooms = await OptimizeRoomPlacementAsync(
                    initialRooms, requirements, boundary, options, cancellationToken);

                // Generate circulation
                progress?.Report(new GenerativeProgress { Stage = "Circulation", Percentage = 80 });
                result.CirculationPaths = await GenerateCirculationAsync(optimizedRooms, boundary, cancellationToken);

                // Finalize
                progress?.Report(new GenerativeProgress { Stage = "Finalizing", Percentage = 95 });
                result.GeneratedRooms = optimizedRooms;
                result.TotalArea = optimizedRooms.Sum(r => r.Area);
                result.Efficiency = result.TotalArea / boundaryAnalysis.GrossArea;

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                progress?.Report(new GenerativeProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating room configuration");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Facade Generation

        /// <summary>
        /// Generates facade designs based on performance criteria.
        /// </summary>
        public async Task<FacadeGenerationResult> GenerateFacadeAsync(
            FacadeRequirements requirements,
            PerformanceCriteria criteria,
            FacadeOptions options,
            IProgress<GenerativeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating facade design");

            var result = new FacadeGenerationResult
            {
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Generate window patterns
                progress?.Report(new GenerativeProgress { Stage = "Generating Patterns", Percentage = 20 });
                var windowPatterns = await GenerateWindowPatternsAsync(
                    requirements, criteria, cancellationToken);

                // Evaluate daylight performance
                progress?.Report(new GenerativeProgress { Stage = "Daylight Analysis", Percentage = 40 });
                foreach (var pattern in windowPatterns)
                {
                    pattern.DaylightScore = await EvaluateDaylightPerformanceAsync(pattern, criteria, cancellationToken);
                }

                // Evaluate energy performance
                progress?.Report(new GenerativeProgress { Stage = "Energy Analysis", Percentage = 60 });
                foreach (var pattern in windowPatterns)
                {
                    pattern.EnergyScore = await EvaluateEnergyPerformanceAsync(pattern, criteria, cancellationToken);
                }

                // Evaluate aesthetics
                progress?.Report(new GenerativeProgress { Stage = "Aesthetic Analysis", Percentage = 75 });
                foreach (var pattern in windowPatterns)
                {
                    pattern.AestheticScore = EvaluateAesthetics(pattern, requirements);
                }

                // Select best patterns
                progress?.Report(new GenerativeProgress { Stage = "Selecting Best", Percentage = 90 });
                result.GeneratedPatterns = SelectBestFacadePatterns(windowPatterns, options.ResultCount);

                // Generate facade elements
                foreach (var pattern in result.GeneratedPatterns)
                {
                    pattern.FacadeElements = await GenerateFacadeElementsAsync(pattern, requirements, cancellationToken);
                }

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                progress?.Report(new GenerativeProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating facade");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Structural Grid Generation

        /// <summary>
        /// Generates optimal structural grid layouts.
        /// </summary>
        public async Task<StructuralGridResult> GenerateStructuralGridAsync(
            StructuralRequirements requirements,
            BuildingFootprint footprint,
            StructuralOptions options,
            IProgress<GenerativeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating structural grid");

            var result = new StructuralGridResult
            {
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Analyze footprint
                progress?.Report(new GenerativeProgress { Stage = "Analyzing Footprint", Percentage = 15 });
                var footprintAnalysis = await AnalyzeFootprintAsync(footprint, cancellationToken);

                // Generate grid options
                progress?.Report(new GenerativeProgress { Stage = "Generating Grids", Percentage = 35 });
                var gridOptions = await GenerateGridOptionsAsync(
                    requirements, footprintAnalysis, options, cancellationToken);

                // Evaluate structural efficiency
                progress?.Report(new GenerativeProgress { Stage = "Structural Analysis", Percentage = 55 });
                foreach (var grid in gridOptions)
                {
                    grid.StructuralScore = await EvaluateStructuralEfficiencyAsync(grid, requirements, cancellationToken);
                }

                // Evaluate spatial flexibility
                progress?.Report(new GenerativeProgress { Stage = "Spatial Analysis", Percentage = 70 });
                foreach (var grid in gridOptions)
                {
                    grid.SpatialScore = EvaluateSpatialFlexibility(grid, requirements);
                }

                // Evaluate cost
                progress?.Report(new GenerativeProgress { Stage = "Cost Analysis", Percentage = 85 });
                foreach (var grid in gridOptions)
                {
                    grid.CostScore = await EstimateStructuralCostAsync(grid, requirements, cancellationToken);
                }

                // Select best grids
                result.GeneratedGrids = SelectBestGrids(gridOptions, options.ResultCount);

                // Generate structural elements
                foreach (var grid in result.GeneratedGrids)
                {
                    grid.Columns = await GenerateColumnsAsync(grid, requirements, cancellationToken);
                    grid.Beams = await GenerateBeamsAsync(grid, requirements, cancellationToken);
                }

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                progress?.Report(new GenerativeProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating structural grid");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region MEP Layout Generation

        /// <summary>
        /// Generates optimized MEP system layouts.
        /// </summary>
        public async Task<MEPLayoutResult> GenerateMEPLayoutAsync(
            MEPRequirements requirements,
            BuildingLayout buildingLayout,
            MEPOptions options,
            IProgress<GenerativeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating MEP layout");

            var result = new MEPLayoutResult
            {
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Analyze spaces
                progress?.Report(new GenerativeProgress { Stage = "Analyzing Spaces", Percentage = 10 });
                var spaceAnalysis = await AnalyzeSpacesForMEPAsync(buildingLayout, cancellationToken);

                // Generate HVAC layout
                progress?.Report(new GenerativeProgress { Stage = "HVAC Layout", Percentage = 30 });
                result.HVACLayout = await GenerateHVACLayoutAsync(
                    requirements.HVAC, spaceAnalysis, options, cancellationToken);

                // Generate plumbing layout
                progress?.Report(new GenerativeProgress { Stage = "Plumbing Layout", Percentage = 50 });
                result.PlumbingLayout = await GeneratePlumbingLayoutAsync(
                    requirements.Plumbing, spaceAnalysis, options, cancellationToken);

                // Generate electrical layout
                progress?.Report(new GenerativeProgress { Stage = "Electrical Layout", Percentage = 70 });
                result.ElectricalLayout = await GenerateElectricalLayoutAsync(
                    requirements.Electrical, spaceAnalysis, options, cancellationToken);

                // Coordinate systems
                progress?.Report(new GenerativeProgress { Stage = "Coordinating", Percentage = 85 });
                await CoordinateMEPSystemsAsync(result, buildingLayout, cancellationToken);

                // Optimize routing
                progress?.Report(new GenerativeProgress { Stage = "Optimizing Routes", Percentage = 95 });
                await OptimizeMEPRoutingAsync(result, options, cancellationToken);

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                progress?.Report(new GenerativeProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating MEP layout");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Design Variants

        /// <summary>
        /// Generates design variants from a base design.
        /// </summary>
        public async Task<VariantGenerationResult> GenerateDesignVariantsAsync(
            BaseDesign baseDesign,
            VariantParameters parameters,
            VariantOptions options,
            IProgress<GenerativeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating {options.VariantCount} design variants");

            var result = new VariantGenerationResult
            {
                BaseDesignId = baseDesign.Id,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Analyze base design
                progress?.Report(new GenerativeProgress { Stage = "Analyzing Base", Percentage = 10 });
                var baseAnalysis = await AnalyzeBaseDesignAsync(baseDesign, cancellationToken);

                // Generate variants
                var variants = new List<DesignVariant>();
                for (int i = 0; i < options.VariantCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new GenerativeProgress
                    {
                        Stage = $"Generating Variant {i + 1}/{options.VariantCount}",
                        Percentage = 15 + (i * 70 / options.VariantCount)
                    });

                    var variant = await GenerateSingleVariantAsync(
                        baseDesign, baseAnalysis, parameters, options, cancellationToken);
                    variants.Add(variant);
                }

                // Evaluate variants
                progress?.Report(new GenerativeProgress { Stage = "Evaluating Variants", Percentage = 90 });
                foreach (var variant in variants)
                {
                    variant.Scores = await EvaluateVariantAsync(variant, parameters.Objectives, cancellationToken);
                    variant.OverallScore = CalculateOverallScore(variant.Scores, parameters.Weights);
                }

                // Sort by score
                result.Variants = variants.OrderByDescending(v => v.OverallScore).ToList();

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                progress?.Report(new GenerativeProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating design variants");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Genetic Algorithm Operations

        private async Task<List<LayoutIndividual>> InitializeLayoutPopulationAsync(
            BuildingProgram program,
            LayoutConstraints constraints,
            GenerativeOptions options,
            CancellationToken cancellationToken)
        {
            var population = new List<LayoutIndividual>();

            for (int i = 0; i < options.PopulationSize; i++)
            {
                var individual = await GenerateRandomLayoutAsync(program, constraints, cancellationToken);
                population.Add(individual);
            }

            return population;
        }

        private async Task<LayoutIndividual> GenerateRandomLayoutAsync(
            BuildingProgram program,
            LayoutConstraints constraints,
            CancellationToken cancellationToken)
        {
            var individual = new LayoutIndividual
            {
                Id = Guid.NewGuid().ToString(),
                Rooms = new List<PlacedRoom>()
            };

            // Place rooms randomly within constraints
            foreach (var room in program.Rooms)
            {
                var placed = new PlacedRoom
                {
                    RoomId = room.Id,
                    RoomType = room.Type,
                    Area = room.RequiredArea,
                    Position = GenerateRandomPosition(constraints.Boundary),
                    Rotation = _random.NextDouble() * 360
                };
                individual.Rooms.Add(placed);
            }

            // Repair overlaps
            await RepairOverlapsAsync(individual, constraints, cancellationToken);

            return individual;
        }

        private async Task EvaluateLayoutFitnessAsync(
            List<LayoutIndividual> population,
            BuildingProgram program,
            LayoutConstraints constraints,
            CancellationToken cancellationToken)
        {
            foreach (var individual in population)
            {
                individual.Fitness = await CalculateLayoutFitnessAsync(
                    individual, program, constraints, cancellationToken);
            }
        }

        private async Task<double> CalculateLayoutFitnessAsync(
            LayoutIndividual individual,
            BuildingProgram program,
            LayoutConstraints constraints,
            CancellationToken cancellationToken)
        {
            double fitness = 0;

            // Area satisfaction
            double areaSatisfaction = CalculateAreaSatisfaction(individual, program);
            fitness += areaSatisfaction * constraints.Weights.AreaWeight;

            // Adjacency satisfaction
            double adjacencySatisfaction = CalculateAdjacencySatisfaction(individual, program);
            fitness += adjacencySatisfaction * constraints.Weights.AdjacencyWeight;

            // Circulation efficiency
            double circulationEfficiency = CalculateCirculationEfficiency(individual);
            fitness += circulationEfficiency * constraints.Weights.CirculationWeight;

            // Daylight access
            double daylightAccess = CalculateDaylightAccess(individual, constraints.Boundary);
            fitness += daylightAccess * constraints.Weights.DaylightWeight;

            // Constraint violations
            double violations = CalculateConstraintViolations(individual, constraints);
            fitness -= violations * constraints.Weights.PenaltyWeight;

            return fitness;
        }

        private double CalculateAreaSatisfaction(LayoutIndividual individual, BuildingProgram program)
        {
            double totalSatisfaction = 0;
            foreach (var placed in individual.Rooms)
            {
                var required = program.Rooms.FirstOrDefault(r => r.Id == placed.RoomId);
                if (required != null)
                {
                    var ratio = Math.Min(placed.Area / required.RequiredArea, 1.0);
                    totalSatisfaction += ratio;
                }
            }
            return totalSatisfaction / individual.Rooms.Count;
        }

        private double CalculateAdjacencySatisfaction(LayoutIndividual individual, BuildingProgram program)
        {
            double satisfied = 0;
            double total = 0;

            foreach (var adjacency in program.Adjacencies)
            {
                total++;
                var room1 = individual.Rooms.FirstOrDefault(r => r.RoomId == adjacency.Room1Id);
                var room2 = individual.Rooms.FirstOrDefault(r => r.RoomId == adjacency.Room2Id);

                if (room1 != null && room2 != null)
                {
                    var distance = CalculateDistance(room1.Position, room2.Position);
                    if (distance < adjacency.MaxDistance)
                    {
                        satisfied += 1.0 - (distance / adjacency.MaxDistance);
                    }
                }
            }

            return total > 0 ? satisfied / total : 1.0;
        }

        private double CalculateCirculationEfficiency(LayoutIndividual individual)
        {
            // Calculate based on corridor lengths and accessibility
            return 0.8; // Placeholder
        }

        private double CalculateDaylightAccess(LayoutIndividual individual, BoundaryPolygon boundary)
        {
            // Calculate percentage of rooms with exterior wall access
            return 0.7; // Placeholder
        }

        private double CalculateConstraintViolations(LayoutIndividual individual, LayoutConstraints constraints)
        {
            double violations = 0;

            // Check for overlaps
            for (int i = 0; i < individual.Rooms.Count; i++)
            {
                for (int j = i + 1; j < individual.Rooms.Count; j++)
                {
                    if (RoomsOverlap(individual.Rooms[i], individual.Rooms[j]))
                    {
                        violations += 1;
                    }
                }
            }

            // Check boundary violations
            foreach (var room in individual.Rooms)
            {
                if (!IsWithinBoundary(room, constraints.Boundary))
                {
                    violations += 1;
                }
            }

            return violations;
        }

        private bool HasConverged(List<LayoutIndividual> population, double threshold)
        {
            if (population.Count < 2) return false;

            var maxFitness = population.Max(i => i.Fitness);
            var minFitness = population.Min(i => i.Fitness);

            return (maxFitness - minFitness) / maxFitness < threshold;
        }

        private List<LayoutIndividual> SelectFittestLayouts(List<LayoutIndividual> population, int count)
        {
            return population.OrderByDescending(i => i.Fitness).Take(count).ToList();
        }

        private async Task<List<LayoutIndividual>> CrossoverLayoutsAsync(
            List<LayoutIndividual> parents,
            GenerativeOptions options,
            CancellationToken cancellationToken)
        {
            var offspring = new List<LayoutIndividual>();

            for (int i = 0; i < parents.Count - 1; i += 2)
            {
                var (child1, child2) = await CrossoverPairAsync(parents[i], parents[i + 1], cancellationToken);
                offspring.Add(child1);
                offspring.Add(child2);
            }

            return offspring;
        }

        private async Task<(LayoutIndividual, LayoutIndividual)> CrossoverPairAsync(
            LayoutIndividual parent1,
            LayoutIndividual parent2,
            CancellationToken cancellationToken)
        {
            var child1 = new LayoutIndividual
            {
                Id = Guid.NewGuid().ToString(),
                Rooms = new List<PlacedRoom>()
            };

            var child2 = new LayoutIndividual
            {
                Id = Guid.NewGuid().ToString(),
                Rooms = new List<PlacedRoom>()
            };

            // Single-point crossover
            int crossoverPoint = _random.Next(parent1.Rooms.Count);

            for (int i = 0; i < parent1.Rooms.Count; i++)
            {
                if (i < crossoverPoint)
                {
                    child1.Rooms.Add(CloneRoom(parent1.Rooms[i]));
                    child2.Rooms.Add(CloneRoom(parent2.Rooms[i]));
                }
                else
                {
                    child1.Rooms.Add(CloneRoom(parent2.Rooms[i]));
                    child2.Rooms.Add(CloneRoom(parent1.Rooms[i]));
                }
            }

            return (child1, child2);
        }

        private async Task MutateLayoutsAsync(
            List<LayoutIndividual> population,
            double mutationRate,
            CancellationToken cancellationToken)
        {
            foreach (var individual in population)
            {
                if (_random.NextDouble() < mutationRate)
                {
                    await MutateLayoutAsync(individual, cancellationToken);
                }
            }
        }

        private async Task MutateLayoutAsync(LayoutIndividual individual, CancellationToken cancellationToken)
        {
            // Select random room to mutate
            int roomIndex = _random.Next(individual.Rooms.Count);
            var room = individual.Rooms[roomIndex];

            // Apply random mutation
            int mutationType = _random.Next(3);
            switch (mutationType)
            {
                case 0: // Position mutation
                    room.Position = new Point2D
                    {
                        X = room.Position.X + (_random.NextDouble() - 0.5) * 2000,
                        Y = room.Position.Y + (_random.NextDouble() - 0.5) * 2000
                    };
                    break;

                case 1: // Rotation mutation
                    room.Rotation = _random.NextDouble() * 360;
                    break;

                case 2: // Swap mutation
                    if (individual.Rooms.Count > 1)
                    {
                        int otherIndex = _random.Next(individual.Rooms.Count);
                        if (otherIndex != roomIndex)
                        {
                            var tempPos = room.Position;
                            room.Position = individual.Rooms[otherIndex].Position;
                            individual.Rooms[otherIndex].Position = tempPos;
                        }
                    }
                    break;
            }
        }

        private List<LayoutIndividual> MergePopulations(
            List<LayoutIndividual> selected,
            List<LayoutIndividual> offspring,
            int targetSize)
        {
            var merged = new List<LayoutIndividual>();
            merged.AddRange(selected);
            merged.AddRange(offspring);

            return merged.OrderByDescending(i => i.Fitness).Take(targetSize).ToList();
        }

        private List<GeneratedLayout> SelectBestLayouts(List<LayoutIndividual> population, int count)
        {
            return population
                .OrderByDescending(i => i.Fitness)
                .Take(count)
                .Select(i => ConvertToGeneratedLayout(i))
                .ToList();
        }

        private GeneratedLayout ConvertToGeneratedLayout(LayoutIndividual individual)
        {
            return new GeneratedLayout
            {
                Id = individual.Id,
                Rooms = individual.Rooms.Select(r => new GeneratedRoom
                {
                    Id = r.RoomId,
                    Type = r.RoomType,
                    Area = r.Area,
                    Position = r.Position,
                    Boundary = GenerateRoomBoundary(r)
                }).ToList(),
                FitnessScore = individual.Fitness
            };
        }

        private async Task PostProcessLayoutAsync(
            GeneratedLayout layout,
            LayoutConstraints constraints,
            CancellationToken cancellationToken)
        {
            // Snap to grid
            foreach (var room in layout.Rooms)
            {
                room.Position = SnapToGrid(room.Position, constraints.GridSize);
            }

            // Generate walls
            layout.Walls = GenerateWallsFromRooms(layout.Rooms);

            // Generate doors
            layout.Doors = GenerateDoorsFromAdjacencies(layout.Rooms, layout.Walls);
        }

        #endregion

        #region Helper Methods

        private Point2D GenerateRandomPosition(BoundaryPolygon boundary)
        {
            var bounds = GetBounds(boundary);
            return new Point2D
            {
                X = bounds.MinX + _random.NextDouble() * (bounds.MaxX - bounds.MinX),
                Y = bounds.MinY + _random.NextDouble() * (bounds.MaxY - bounds.MinY)
            };
        }

        private (double MinX, double MinY, double MaxX, double MaxY) GetBounds(BoundaryPolygon boundary)
        {
            var minX = boundary.Points.Min(p => p.X);
            var minY = boundary.Points.Min(p => p.Y);
            var maxX = boundary.Points.Max(p => p.X);
            var maxY = boundary.Points.Max(p => p.Y);
            return (minX, minY, maxX, maxY);
        }

        private async Task RepairOverlapsAsync(
            LayoutIndividual individual,
            LayoutConstraints constraints,
            CancellationToken cancellationToken)
        {
            // Simple overlap repair using push-apart
            for (int iteration = 0; iteration < 100; iteration++)
            {
                bool hasOverlap = false;
                for (int i = 0; i < individual.Rooms.Count; i++)
                {
                    for (int j = i + 1; j < individual.Rooms.Count; j++)
                    {
                        if (RoomsOverlap(individual.Rooms[i], individual.Rooms[j]))
                        {
                            hasOverlap = true;
                            PushApart(individual.Rooms[i], individual.Rooms[j]);
                        }
                    }
                }
                if (!hasOverlap) break;
            }
        }

        private bool RoomsOverlap(PlacedRoom room1, PlacedRoom room2)
        {
            var distance = CalculateDistance(room1.Position, room2.Position);
            var minDistance = Math.Sqrt(room1.Area) / 2 + Math.Sqrt(room2.Area) / 2;
            return distance < minDistance;
        }

        private void PushApart(PlacedRoom room1, PlacedRoom room2)
        {
            var dx = room2.Position.X - room1.Position.X;
            var dy = room2.Position.Y - room1.Position.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < 0.001) distance = 0.001;

            var pushDistance = 100; // Push 100mm apart
            room1.Position = new Point2D
            {
                X = room1.Position.X - (dx / distance) * pushDistance,
                Y = room1.Position.Y - (dy / distance) * pushDistance
            };
            room2.Position = new Point2D
            {
                X = room2.Position.X + (dx / distance) * pushDistance,
                Y = room2.Position.Y + (dy / distance) * pushDistance
            };
        }

        private bool IsWithinBoundary(PlacedRoom room, BoundaryPolygon boundary)
        {
            return IsPointInPolygon(room.Position, boundary.Points);
        }

        private bool IsPointInPolygon(Point2D point, List<Point2D> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; j = i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                    (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private double CalculateDistance(Point2D p1, Point2D p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        private PlacedRoom CloneRoom(PlacedRoom room)
        {
            return new PlacedRoom
            {
                RoomId = room.RoomId,
                RoomType = room.RoomType,
                Area = room.Area,
                Position = new Point2D { X = room.Position.X, Y = room.Position.Y },
                Rotation = room.Rotation
            };
        }

        private Point2D SnapToGrid(Point2D point, double gridSize)
        {
            return new Point2D
            {
                X = Math.Round(point.X / gridSize) * gridSize,
                Y = Math.Round(point.Y / gridSize) * gridSize
            };
        }

        private List<Point2D> GenerateRoomBoundary(PlacedRoom room)
        {
            var halfSize = Math.Sqrt(room.Area) / 2;
            return new List<Point2D>
            {
                new Point2D { X = room.Position.X - halfSize, Y = room.Position.Y - halfSize },
                new Point2D { X = room.Position.X + halfSize, Y = room.Position.Y - halfSize },
                new Point2D { X = room.Position.X + halfSize, Y = room.Position.Y + halfSize },
                new Point2D { X = room.Position.X - halfSize, Y = room.Position.Y + halfSize }
            };
        }

        private List<GeneratedWall> GenerateWallsFromRooms(List<GeneratedRoom> rooms)
        {
            var walls = new List<GeneratedWall>();
            foreach (var room in rooms)
            {
                for (int i = 0; i < room.Boundary.Count; i++)
                {
                    var start = room.Boundary[i];
                    var end = room.Boundary[(i + 1) % room.Boundary.Count];
                    walls.Add(new GeneratedWall
                    {
                        Id = Guid.NewGuid().ToString(),
                        StartPoint = start,
                        EndPoint = end,
                        RoomId = room.Id
                    });
                }
            }
            return walls;
        }

        private List<GeneratedDoor> GenerateDoorsFromAdjacencies(
            List<GeneratedRoom> rooms,
            List<GeneratedWall> walls)
        {
            var doors = new List<GeneratedDoor>();
            // Generate doors where rooms are adjacent
            return doors;
        }

        private BoundaryAnalysis AnalyzeBoundary(BoundaryPolygon boundary)
        {
            var bounds = GetBounds(boundary);
            return new BoundaryAnalysis
            {
                GrossArea = (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY),
                Perimeter = CalculatePerimeter(boundary.Points),
                AspectRatio = (bounds.MaxX - bounds.MinX) / (bounds.MaxY - bounds.MinY)
            };
        }

        private double CalculatePerimeter(List<Point2D> points)
        {
            double perimeter = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var next = points[(i + 1) % points.Count];
                perimeter += CalculateDistance(points[i], next);
            }
            return perimeter;
        }

        private async Task<List<GeneratedRoom>> GenerateInitialRoomPlacementAsync(
            RoomRequirements requirements,
            BoundaryPolygon boundary,
            BoundaryAnalysis analysis,
            CancellationToken cancellationToken)
        {
            var rooms = new List<GeneratedRoom>();
            // Simple grid-based initial placement
            return rooms;
        }

        private async Task<List<GeneratedRoom>> OptimizeRoomPlacementAsync(
            List<GeneratedRoom> rooms,
            RoomRequirements requirements,
            BoundaryPolygon boundary,
            RoomConfigOptions options,
            CancellationToken cancellationToken)
        {
            // Use optimization algorithm to improve placement
            return rooms;
        }

        private async Task<List<CirculationPath>> GenerateCirculationAsync(
            List<GeneratedRoom> rooms,
            BoundaryPolygon boundary,
            CancellationToken cancellationToken)
        {
            return new List<CirculationPath>();
        }

        private async Task<List<WindowPattern>> GenerateWindowPatternsAsync(
            FacadeRequirements requirements,
            PerformanceCriteria criteria,
            CancellationToken cancellationToken)
        {
            var patterns = new List<WindowPattern>();
            // Generate various window arrangements
            for (int i = 0; i < 10; i++)
            {
                patterns.Add(new WindowPattern
                {
                    Id = Guid.NewGuid().ToString(),
                    WindowToWallRatio = 0.3 + _random.NextDouble() * 0.4
                });
            }
            return patterns;
        }

        private async Task<double> EvaluateDaylightPerformanceAsync(
            WindowPattern pattern,
            PerformanceCriteria criteria,
            CancellationToken cancellationToken)
        {
            return pattern.WindowToWallRatio * 0.8; // Simplified
        }

        private async Task<double> EvaluateEnergyPerformanceAsync(
            WindowPattern pattern,
            PerformanceCriteria criteria,
            CancellationToken cancellationToken)
        {
            return 1.0 - pattern.WindowToWallRatio * 0.5; // Simplified
        }

        private double EvaluateAesthetics(WindowPattern pattern, FacadeRequirements requirements)
        {
            return 0.7; // Placeholder
        }

        private List<WindowPattern> SelectBestFacadePatterns(List<WindowPattern> patterns, int count)
        {
            return patterns
                .OrderByDescending(p => p.DaylightScore * 0.4 + p.EnergyScore * 0.4 + p.AestheticScore * 0.2)
                .Take(count)
                .ToList();
        }

        private async Task<List<FacadeElement>> GenerateFacadeElementsAsync(
            WindowPattern pattern,
            FacadeRequirements requirements,
            CancellationToken cancellationToken)
        {
            return new List<FacadeElement>();
        }

        private async Task<FootprintAnalysis> AnalyzeFootprintAsync(
            BuildingFootprint footprint,
            CancellationToken cancellationToken)
        {
            return new FootprintAnalysis
            {
                Area = 1000, // Placeholder
                AspectRatio = 1.5
            };
        }

        private async Task<List<StructuralGrid>> GenerateGridOptionsAsync(
            StructuralRequirements requirements,
            FootprintAnalysis analysis,
            StructuralOptions options,
            CancellationToken cancellationToken)
        {
            var grids = new List<StructuralGrid>();
            // Generate various grid configurations
            for (int spanX = 6000; spanX <= 9000; spanX += 1500)
            {
                for (int spanY = 6000; spanY <= 9000; spanY += 1500)
                {
                    grids.Add(new StructuralGrid
                    {
                        Id = Guid.NewGuid().ToString(),
                        SpanX = spanX,
                        SpanY = spanY
                    });
                }
            }
            return grids;
        }

        private async Task<double> EvaluateStructuralEfficiencyAsync(
            StructuralGrid grid,
            StructuralRequirements requirements,
            CancellationToken cancellationToken)
        {
            // Larger spans = more efficient
            return (grid.SpanX + grid.SpanY) / 18000.0;
        }

        private double EvaluateSpatialFlexibility(StructuralGrid grid, StructuralRequirements requirements)
        {
            return grid.SpanX * grid.SpanY / 81000000.0; // Normalize
        }

        private async Task<double> EstimateStructuralCostAsync(
            StructuralGrid grid,
            StructuralRequirements requirements,
            CancellationToken cancellationToken)
        {
            // Larger spans = more expensive, so return inverse for score
            return 1.0 - (grid.SpanX + grid.SpanY) / 20000.0;
        }

        private List<StructuralGrid> SelectBestGrids(List<StructuralGrid> grids, int count)
        {
            return grids
                .OrderByDescending(g => g.StructuralScore * 0.4 + g.SpatialScore * 0.4 + g.CostScore * 0.2)
                .Take(count)
                .ToList();
        }

        private async Task<List<GeneratedColumn>> GenerateColumnsAsync(
            StructuralGrid grid,
            StructuralRequirements requirements,
            CancellationToken cancellationToken)
        {
            return new List<GeneratedColumn>();
        }

        private async Task<List<GeneratedBeam>> GenerateBeamsAsync(
            StructuralGrid grid,
            StructuralRequirements requirements,
            CancellationToken cancellationToken)
        {
            return new List<GeneratedBeam>();
        }

        private async Task<SpaceAnalysis> AnalyzeSpacesForMEPAsync(
            BuildingLayout layout,
            CancellationToken cancellationToken)
        {
            return new SpaceAnalysis();
        }

        private async Task<HVACLayout> GenerateHVACLayoutAsync(
            HVACRequirements requirements,
            SpaceAnalysis analysis,
            MEPOptions options,
            CancellationToken cancellationToken)
        {
            return new HVACLayout();
        }

        private async Task<PlumbingLayout> GeneratePlumbingLayoutAsync(
            PlumbingRequirements requirements,
            SpaceAnalysis analysis,
            MEPOptions options,
            CancellationToken cancellationToken)
        {
            return new PlumbingLayout();
        }

        private async Task<ElectricalLayout> GenerateElectricalLayoutAsync(
            ElectricalRequirements requirements,
            SpaceAnalysis analysis,
            MEPOptions options,
            CancellationToken cancellationToken)
        {
            return new ElectricalLayout();
        }

        private async Task CoordinateMEPSystemsAsync(
            MEPLayoutResult result,
            BuildingLayout layout,
            CancellationToken cancellationToken)
        {
            // Coordinate between systems
        }

        private async Task OptimizeMEPRoutingAsync(
            MEPLayoutResult result,
            MEPOptions options,
            CancellationToken cancellationToken)
        {
            // Optimize routing
        }

        private async Task<BaseDesignAnalysis> AnalyzeBaseDesignAsync(
            BaseDesign design,
            CancellationToken cancellationToken)
        {
            return new BaseDesignAnalysis();
        }

        private async Task<DesignVariant> GenerateSingleVariantAsync(
            BaseDesign baseDesign,
            BaseDesignAnalysis analysis,
            VariantParameters parameters,
            VariantOptions options,
            CancellationToken cancellationToken)
        {
            return new DesignVariant
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Variant {_random.Next(1000)}"
            };
        }

        private async Task<Dictionary<string, double>> EvaluateVariantAsync(
            DesignVariant variant,
            List<string> objectives,
            CancellationToken cancellationToken)
        {
            var scores = new Dictionary<string, double>();
            foreach (var objective in objectives)
            {
                scores[objective] = _random.NextDouble();
            }
            return scores;
        }

        private double CalculateOverallScore(Dictionary<string, double> scores, Dictionary<string, double> weights)
        {
            double total = 0;
            foreach (var kvp in scores)
            {
                if (weights.TryGetValue(kvp.Key, out var weight))
                {
                    total += kvp.Value * weight;
                }
            }
            return total;
        }

        #endregion
    }

    #region Supporting Classes and Data Models

    internal class LayoutGenerator { }
    internal class FacadeGenerator { }
    internal class StructuralGridGenerator { }
    internal class MEPLayoutGenerator { }
    internal class VariantGenerator { }
    internal class FitnessEvaluator { }
    internal class ConstraintSolver { }

    public class GenerativeConfiguration
    {
        public int DefaultPopulationSize { get; set; } = 50;
        public int DefaultMaxGenerations { get; set; } = 100;
        public double DefaultMutationRate { get; set; } = 0.1;
    }

    public class GenerativeProgress
    {
        public string Stage { get; set; }
        public int Percentage { get; set; }
    }

    public class GenerativeOptions
    {
        public int PopulationSize { get; set; } = 50;
        public int MaxGenerations { get; set; } = 100;
        public double MutationRate { get; set; } = 0.1;
        public double ConvergenceThreshold { get; set; } = 0.01;
        public int SelectionCount { get; set; } = 20;
        public int ResultCount { get; set; } = 5;
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class BuildingProgram
    {
        public string Id { get; set; }
        public List<RoomDefinition> Rooms { get; set; } = new List<RoomDefinition>();
        public List<AdjacencyRequirement> Adjacencies { get; set; } = new List<AdjacencyRequirement>();
    }

    public class RoomDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public double RequiredArea { get; set; }
    }

    public class AdjacencyRequirement
    {
        public string Room1Id { get; set; }
        public string Room2Id { get; set; }
        public double MaxDistance { get; set; }
    }

    public class LayoutConstraints
    {
        public BoundaryPolygon Boundary { get; set; }
        public double GridSize { get; set; } = 100;
        public FitnessWeights Weights { get; set; } = new FitnessWeights();
    }

    public class BoundaryPolygon
    {
        public List<Point2D> Points { get; set; } = new List<Point2D>();
    }

    public class FitnessWeights
    {
        public double AreaWeight { get; set; } = 0.3;
        public double AdjacencyWeight { get; set; } = 0.3;
        public double CirculationWeight { get; set; } = 0.2;
        public double DaylightWeight { get; set; } = 0.15;
        public double PenaltyWeight { get; set; } = 0.5;
    }

    public class LayoutIndividual
    {
        public string Id { get; set; }
        public List<PlacedRoom> Rooms { get; set; }
        public double Fitness { get; set; }
    }

    public class PlacedRoom
    {
        public string RoomId { get; set; }
        public string RoomType { get; set; }
        public double Area { get; set; }
        public Point2D Position { get; set; }
        public double Rotation { get; set; }
    }

    public class LayoutGenerationResult
    {
        public string ProgramId { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<GeneratedLayout> GeneratedLayouts { get; set; } = new List<GeneratedLayout>();
    }

    public class GeneratedLayout
    {
        public string Id { get; set; }
        public List<GeneratedRoom> Rooms { get; set; } = new List<GeneratedRoom>();
        public List<GeneratedWall> Walls { get; set; } = new List<GeneratedWall>();
        public List<GeneratedDoor> Doors { get; set; } = new List<GeneratedDoor>();
        public double FitnessScore { get; set; }
    }

    public class GeneratedRoom
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public double Area { get; set; }
        public Point2D Position { get; set; }
        public List<Point2D> Boundary { get; set; }
    }

    public class GeneratedWall
    {
        public string Id { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public string RoomId { get; set; }
    }

    public class GeneratedDoor
    {
        public string Id { get; set; }
        public Point2D Location { get; set; }
        public string WallId { get; set; }
    }

    public class RoomRequirements
    {
        public int RoomCount { get; set; }
        public List<RoomDefinition> Rooms { get; set; }
    }

    public class RoomConfigOptions
    {
        public int MaxIterations { get; set; } = 1000;
    }

    public class RoomConfigurationResult
    {
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<GeneratedRoom> GeneratedRooms { get; set; }
        public List<CirculationPath> CirculationPaths { get; set; }
        public double TotalArea { get; set; }
        public double Efficiency { get; set; }
    }

    public class CirculationPath
    {
        public List<Point2D> Points { get; set; }
        public double Width { get; set; }
    }

    public class BoundaryAnalysis
    {
        public double GrossArea { get; set; }
        public double Perimeter { get; set; }
        public double AspectRatio { get; set; }
    }

    public class FacadeRequirements
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public string Orientation { get; set; }
    }

    public class PerformanceCriteria
    {
        public double TargetDaylightFactor { get; set; }
        public double MaxSolarHeatGain { get; set; }
    }

    public class FacadeOptions
    {
        public int ResultCount { get; set; } = 5;
    }

    public class FacadeGenerationResult
    {
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<WindowPattern> GeneratedPatterns { get; set; }
    }

    public class WindowPattern
    {
        public string Id { get; set; }
        public double WindowToWallRatio { get; set; }
        public double DaylightScore { get; set; }
        public double EnergyScore { get; set; }
        public double AestheticScore { get; set; }
        public List<FacadeElement> FacadeElements { get; set; }
    }

    public class FacadeElement
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public Point2D Position { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class StructuralRequirements
    {
        public double MaxLoad { get; set; }
        public string StructureType { get; set; }
    }

    public class BuildingFootprint
    {
        public List<Point2D> Boundary { get; set; }
    }

    public class StructuralOptions
    {
        public int ResultCount { get; set; } = 3;
    }

    public class StructuralGridResult
    {
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<StructuralGrid> GeneratedGrids { get; set; }
    }

    public class StructuralGrid
    {
        public string Id { get; set; }
        public double SpanX { get; set; }
        public double SpanY { get; set; }
        public double StructuralScore { get; set; }
        public double SpatialScore { get; set; }
        public double CostScore { get; set; }
        public List<GeneratedColumn> Columns { get; set; }
        public List<GeneratedBeam> Beams { get; set; }
    }

    public class GeneratedColumn
    {
        public string Id { get; set; }
        public Point2D Location { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
    }

    public class GeneratedBeam
    {
        public string Id { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
    }

    public class FootprintAnalysis
    {
        public double Area { get; set; }
        public double AspectRatio { get; set; }
    }

    public class MEPRequirements
    {
        public HVACRequirements HVAC { get; set; }
        public PlumbingRequirements Plumbing { get; set; }
        public ElectricalRequirements Electrical { get; set; }
    }

    public class HVACRequirements { public double CoolingLoad { get; set; } }
    public class PlumbingRequirements { public int FixtureCount { get; set; } }
    public class ElectricalRequirements { public double TotalLoad { get; set; } }

    public class BuildingLayout
    {
        public List<GeneratedRoom> Rooms { get; set; }
    }

    public class MEPOptions
    {
        public bool OptimizeRouting { get; set; } = true;
    }

    public class MEPLayoutResult
    {
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public HVACLayout HVACLayout { get; set; }
        public PlumbingLayout PlumbingLayout { get; set; }
        public ElectricalLayout ElectricalLayout { get; set; }
    }

    public class HVACLayout { public List<DuctSegment> Ducts { get; set; } }
    public class PlumbingLayout { public List<PipeSegment> Pipes { get; set; } }
    public class ElectricalLayout { public List<CableSegment> Cables { get; set; } }
    public class DuctSegment { public Point2D Start { get; set; } public Point2D End { get; set; } }
    public class PipeSegment { public Point2D Start { get; set; } public Point2D End { get; set; } }
    public class CableSegment { public Point2D Start { get; set; } public Point2D End { get; set; } }
    public class SpaceAnalysis { public List<SpaceInfo> Spaces { get; set; } }
    public class SpaceInfo { public string Id { get; set; } public double Area { get; set; } }

    public class BaseDesign
    {
        public string Id { get; set; }
        public List<GeneratedRoom> Rooms { get; set; }
    }

    public class VariantParameters
    {
        public List<string> Objectives { get; set; }
        public Dictionary<string, double> Weights { get; set; }
    }

    public class VariantOptions
    {
        public int VariantCount { get; set; } = 10;
    }

    public class VariantGenerationResult
    {
        public string BaseDesignId { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<DesignVariant> Variants { get; set; }
    }

    public class DesignVariant
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, double> Scores { get; set; }
        public double OverallScore { get; set; }
    }

    public class BaseDesignAnalysis { public double TotalArea { get; set; } }

    #endregion
}
