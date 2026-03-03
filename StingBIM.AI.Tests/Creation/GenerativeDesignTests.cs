// StingBIM.AI.Tests - GenerativeDesignTests.cs
// Unit tests for Generative Design components
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Creation
{
    /// <summary>
    /// Unit tests for generative design algorithms including layout generation,
    /// optimization, and fitness evaluation.
    /// </summary>
    [TestFixture]
    public class GenerativeDesignTests
    {
        #region Fitness Evaluation Tests

        [Test]
        public void CalculateAreaSatisfaction_ExactMatch_Returns1()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var required = 100.0;
            var actual = 100.0;

            // Act
            var satisfaction = evaluator.CalculateAreaSatisfaction(required, actual);

            // Assert
            satisfaction.Should().Be(1.0);
        }

        [Test]
        public void CalculateAreaSatisfaction_HalfArea_ReturnsHalf()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var required = 100.0;
            var actual = 50.0;

            // Act
            var satisfaction = evaluator.CalculateAreaSatisfaction(required, actual);

            // Assert
            satisfaction.Should().BeApproximately(0.5, 0.01);
        }

        [Test]
        public void CalculateAreaSatisfaction_ExcessArea_CappedAt1()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var required = 100.0;
            var actual = 150.0;

            // Act
            var satisfaction = evaluator.CalculateAreaSatisfaction(required, actual);

            // Assert
            satisfaction.Should().Be(1.0);
        }

        [Test]
        public void CalculateAdjacencySatisfaction_RoomsAdjacent_ReturnsHigh()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var room1 = new RoomPosition { X = 0, Y = 0 };
            var room2 = new RoomPosition { X = 4000, Y = 0 };
            var maxDistance = 5000.0;

            // Act
            var satisfaction = evaluator.CalculateAdjacencySatisfaction(room1, room2, maxDistance);

            // Assert
            satisfaction.Should().BeGreaterThan(0.5);
        }

        [Test]
        public void CalculateAdjacencySatisfaction_RoomsFarApart_ReturnsLow()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var room1 = new RoomPosition { X = 0, Y = 0 };
            var room2 = new RoomPosition { X = 10000, Y = 10000 };
            var maxDistance = 5000.0;

            // Act
            var satisfaction = evaluator.CalculateAdjacencySatisfaction(room1, room2, maxDistance);

            // Assert
            satisfaction.Should().Be(0);
        }

        [Test]
        public void CheckOverlap_OverlappingRooms_ReturnsTrue()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var room1 = new RoomBounds { X = 0, Y = 0, Width = 4000, Height = 3000 };
            var room2 = new RoomBounds { X = 2000, Y = 1000, Width = 4000, Height = 3000 };

            // Act
            var overlaps = evaluator.CheckOverlap(room1, room2);

            // Assert
            overlaps.Should().BeTrue();
        }

        [Test]
        public void CheckOverlap_NonOverlappingRooms_ReturnsFalse()
        {
            // Arrange
            var evaluator = new FitnessEvaluatorTest();
            var room1 = new RoomBounds { X = 0, Y = 0, Width = 4000, Height = 3000 };
            var room2 = new RoomBounds { X = 5000, Y = 0, Width = 4000, Height = 3000 };

            // Act
            var overlaps = evaluator.CheckOverlap(room1, room2);

            // Assert
            overlaps.Should().BeFalse();
        }

        #endregion

        #region Genetic Algorithm Tests

        [Test]
        public void Selection_TournamentSelection_SelectsFitter()
        {
            // Arrange
            var selector = new SelectionOperatorTest();
            var population = new List<Individual>
            {
                new Individual { Id = "A", Fitness = 0.9 },
                new Individual { Id = "B", Fitness = 0.3 },
                new Individual { Id = "C", Fitness = 0.5 },
                new Individual { Id = "D", Fitness = 0.7 }
            };

            // Act
            var selected = selector.TournamentSelect(population, 2, 10);

            // Assert
            selected.Should().HaveCount(2);
            selected.Average(s => s.Fitness).Should().BeGreaterThan(0.5);
        }

        [Test]
        public void Crossover_SinglePoint_ProducesValidOffspring()
        {
            // Arrange
            var crossover = new CrossoverOperatorTest();
            var parent1 = new double[] { 1, 2, 3, 4, 5 };
            var parent2 = new double[] { 10, 20, 30, 40, 50 };

            // Act
            var (child1, child2) = crossover.SinglePointCrossover(parent1, parent2, 2);

            // Assert
            child1.Should().HaveCount(5);
            child2.Should().HaveCount(5);
            child1[0].Should().Be(1);
            child1[3].Should().Be(40);
        }

        [Test]
        public void Mutation_GaussianMutation_ModifiesValue()
        {
            // Arrange
            var mutator = new MutationOperatorTest(42);
            var original = 100.0;
            var stdDev = 10.0;

            // Act
            var mutations = Enumerable.Range(0, 100)
                .Select(_ => mutator.GaussianMutate(original, stdDev))
                .ToList();

            // Assert
            mutations.Should().Contain(v => Math.Abs(v - original) > 1);
            mutations.Average().Should().BeApproximately(original, 5);
        }

        [Test]
        public void CheckConvergence_DiversePopulation_ReturnsFalse()
        {
            // Arrange
            var checker = new ConvergenceCheckerTest();
            var population = new List<Individual>
            {
                new Individual { Fitness = 0.9 },
                new Individual { Fitness = 0.3 },
                new Individual { Fitness = 0.5 }
            };

            // Act
            var converged = checker.HasConverged(population, 0.1);

            // Assert
            converged.Should().BeFalse();
        }

        [Test]
        public void CheckConvergence_SimilarFitness_ReturnsTrue()
        {
            // Arrange
            var checker = new ConvergenceCheckerTest();
            var population = new List<Individual>
            {
                new Individual { Fitness = 0.89 },
                new Individual { Fitness = 0.90 },
                new Individual { Fitness = 0.91 }
            };

            // Act
            var converged = checker.HasConverged(population, 0.05);

            // Assert
            converged.Should().BeTrue();
        }

        #endregion

        #region Layout Optimization Tests

        [Test]
        public void GenerateRandomPosition_WithinBounds_ReturnsValidPosition()
        {
            // Arrange
            var generator = new PositionGeneratorTest(42);
            var bounds = new Bounds { MinX = 0, MaxX = 10000, MinY = 0, MaxY = 8000 };

            // Act
            var position = generator.GenerateRandomPosition(bounds);

            // Assert
            position.X.Should().BeInRange(0, 10000);
            position.Y.Should().BeInRange(0, 8000);
        }

        [Test]
        public void SnapToGrid_CustomGridSize_SnapsCorrectly()
        {
            // Arrange
            var snapper = new GridSnapperGA(500);
            var position = new Position { X = 1234, Y = 5678 };

            // Act
            var snapped = snapper.Snap(position);

            // Assert
            snapped.X.Should().Be(1000);
            snapped.Y.Should().Be(5500);
        }

        [Test]
        public void MergePopulations_KeepsBest_ReturnsCorrectSize()
        {
            // Arrange
            var merger = new PopulationMergerTest();
            var pop1 = Enumerable.Range(1, 10)
                .Select(i => new Individual { Id = $"A{i}", Fitness = i * 0.1 })
                .ToList();
            var pop2 = Enumerable.Range(1, 10)
                .Select(i => new Individual { Id = $"B{i}", Fitness = i * 0.05 })
                .ToList();

            // Act
            var merged = merger.Merge(pop1, pop2, 10);

            // Assert
            merged.Should().HaveCount(10);
            merged.Min(m => m.Fitness).Should().BeGreaterThan(0.3);
        }

        #endregion

        #region Structural Grid Tests

        [Test]
        public void EvaluateStructuralEfficiency_OptimalSpan_ReturnsHigh()
        {
            // Arrange
            var evaluator = new StructuralEvaluatorTest();
            var grid = new StructuralGrid { SpanX = 7500, SpanY = 7500 };

            // Act
            var efficiency = evaluator.EvaluateEfficiency(grid);

            // Assert
            efficiency.Should().BeGreaterThan(0.7);
        }

        [Test]
        public void EvaluateSpatialFlexibility_LargerSpans_ReturnsHigher()
        {
            // Arrange
            var evaluator = new StructuralEvaluatorTest();
            var gridSmall = new StructuralGrid { SpanX = 6000, SpanY = 6000 };
            var gridLarge = new StructuralGrid { SpanX = 9000, SpanY = 9000 };

            // Act
            var flexSmall = evaluator.EvaluateSpatialFlexibility(gridSmall);
            var flexLarge = evaluator.EvaluateSpatialFlexibility(gridLarge);

            // Assert
            flexLarge.Should().BeGreaterThan(flexSmall);
        }

        [Test]
        public void GenerateColumnPositions_RectangularFootprint_GeneratesGrid()
        {
            // Arrange
            var generator = new ColumnGeneratorTest();
            var footprint = new Footprint { Width = 20000, Length = 30000 };
            var spanX = 5000.0;
            var spanY = 6000.0;

            // Act
            var columns = generator.GenerateColumns(footprint, spanX, spanY);

            // Assert
            columns.Should().HaveCount(5 * 6); // 5 columns x 6 rows
        }

        #endregion

        #region Facade Generation Tests

        [Test]
        public void CalculateWindowToWallRatio_StandardFacade_ReturnsRatio()
        {
            // Arrange
            var calculator = new WWRCalculatorTest();
            var facadeArea = 100.0; // sqm
            var windowArea = 40.0; // sqm

            // Act
            var wwr = calculator.Calculate(windowArea, facadeArea);

            // Assert
            wwr.Should().BeApproximately(0.4, 0.01);
        }

        [Test]
        public void EvaluateDaylightScore_HighWWR_ReturnsHigh()
        {
            // Arrange
            var evaluator = new DaylightEvaluatorTest();
            var wwr = 0.5;

            // Act
            var score = evaluator.EvaluateDaylight(wwr);

            // Assert
            score.Should().BeGreaterThan(0.6);
        }

        [Test]
        public void EvaluateEnergyScore_HighWWR_ReturnsLower()
        {
            // Arrange
            var evaluator = new EnergyEvaluatorTest();
            var lowWWR = 0.2;
            var highWWR = 0.6;

            // Act
            var scoreLow = evaluator.EvaluateEnergy(lowWWR);
            var scoreHigh = evaluator.EvaluateEnergy(highWWR);

            // Assert
            scoreHigh.Should().BeLessThan(scoreLow);
        }

        #endregion
    }

    #region Test Helper Classes

    public class FitnessEvaluatorTest
    {
        public double CalculateAreaSatisfaction(double required, double actual)
        {
            return Math.Min(actual / required, 1.0);
        }

        public double CalculateAdjacencySatisfaction(RoomPosition room1, RoomPosition room2, double maxDistance)
        {
            var distance = Math.Sqrt(Math.Pow(room1.X - room2.X, 2) + Math.Pow(room1.Y - room2.Y, 2));
            if (distance >= maxDistance) return 0;
            return 1.0 - (distance / maxDistance);
        }

        public bool CheckOverlap(RoomBounds a, RoomBounds b)
        {
            return !(a.X + a.Width <= b.X ||
                    b.X + b.Width <= a.X ||
                    a.Y + a.Height <= b.Y ||
                    b.Y + b.Height <= a.Y);
        }
    }

    public class RoomPosition { public double X { get; set; } public double Y { get; set; } }
    public class RoomBounds { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }
    public class Individual { public string Id { get; set; } public double Fitness { get; set; } }
    public class Position { public double X { get; set; } public double Y { get; set; } }
    public class Bounds { public double MinX { get; set; } public double MaxX { get; set; } public double MinY { get; set; } public double MaxY { get; set; } }
    public class StructuralGrid { public double SpanX { get; set; } public double SpanY { get; set; } }
    public class Footprint { public double Width { get; set; } public double Length { get; set; } }
    public class ColumnPosition { public double X { get; set; } public double Y { get; set; } }

    public class SelectionOperatorTest
    {
        private readonly Random _random = new Random(42);

        public List<Individual> TournamentSelect(List<Individual> population, int selectCount, int tournamentSize)
        {
            var selected = new List<Individual>();

            for (int i = 0; i < selectCount; i++)
            {
                var tournament = population.OrderBy(_ => _random.Next()).Take(tournamentSize).ToList();
                selected.Add(tournament.OrderByDescending(t => t.Fitness).First());
            }

            return selected;
        }
    }

    public class CrossoverOperatorTest
    {
        public (double[], double[]) SinglePointCrossover(double[] parent1, double[] parent2, int crossoverPoint)
        {
            var child1 = new double[parent1.Length];
            var child2 = new double[parent2.Length];

            for (int i = 0; i < parent1.Length; i++)
            {
                if (i < crossoverPoint)
                {
                    child1[i] = parent1[i];
                    child2[i] = parent2[i];
                }
                else
                {
                    child1[i] = parent2[i];
                    child2[i] = parent1[i];
                }
            }

            return (child1, child2);
        }
    }

    public class MutationOperatorTest
    {
        private readonly Random _random;

        public MutationOperatorTest(int seed)
        {
            _random = new Random(seed);
        }

        public double GaussianMutate(double value, double stdDev)
        {
            var u1 = 1.0 - _random.NextDouble();
            var u2 = 1.0 - _random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return value + stdDev * randStdNormal;
        }
    }

    public class ConvergenceCheckerTest
    {
        public bool HasConverged(List<Individual> population, double threshold)
        {
            var maxFitness = population.Max(i => i.Fitness);
            var minFitness = population.Min(i => i.Fitness);
            return (maxFitness - minFitness) / maxFitness < threshold;
        }
    }

    public class PositionGeneratorTest
    {
        private readonly Random _random;

        public PositionGeneratorTest(int seed)
        {
            _random = new Random(seed);
        }

        public Position GenerateRandomPosition(Bounds bounds)
        {
            return new Position
            {
                X = bounds.MinX + _random.NextDouble() * (bounds.MaxX - bounds.MinX),
                Y = bounds.MinY + _random.NextDouble() * (bounds.MaxY - bounds.MinY)
            };
        }
    }

    public class GridSnapperGA
    {
        private readonly double _gridSize;

        public GridSnapperGA(double gridSize)
        {
            _gridSize = gridSize;
        }

        public Position Snap(Position position)
        {
            return new Position
            {
                X = Math.Round(position.X / _gridSize) * _gridSize,
                Y = Math.Round(position.Y / _gridSize) * _gridSize
            };
        }
    }

    public class PopulationMergerTest
    {
        public List<Individual> Merge(List<Individual> pop1, List<Individual> pop2, int targetSize)
        {
            return pop1.Concat(pop2)
                .OrderByDescending(i => i.Fitness)
                .Take(targetSize)
                .ToList();
        }
    }

    public class StructuralEvaluatorTest
    {
        public double EvaluateEfficiency(StructuralGrid grid)
        {
            // Optimal span around 7500mm
            var optimalSpan = 7500;
            var avgSpan = (grid.SpanX + grid.SpanY) / 2;
            var deviation = Math.Abs(avgSpan - optimalSpan) / optimalSpan;
            return Math.Max(0, 1 - deviation);
        }

        public double EvaluateSpatialFlexibility(StructuralGrid grid)
        {
            return (grid.SpanX * grid.SpanY) / (10000.0 * 10000.0);
        }
    }

    public class ColumnGeneratorTest
    {
        public List<ColumnPosition> GenerateColumns(Footprint footprint, double spanX, double spanY)
        {
            var columns = new List<ColumnPosition>();
            var numX = (int)Math.Ceiling(footprint.Width / spanX) + 1;
            var numY = (int)Math.Ceiling(footprint.Length / spanY) + 1;

            for (int i = 0; i < numX; i++)
            {
                for (int j = 0; j < numY; j++)
                {
                    columns.Add(new ColumnPosition { X = i * spanX, Y = j * spanY });
                }
            }

            return columns;
        }
    }

    public class WWRCalculatorTest
    {
        public double Calculate(double windowArea, double facadeArea)
        {
            return windowArea / facadeArea;
        }
    }

    public class DaylightEvaluatorTest
    {
        public double EvaluateDaylight(double wwr)
        {
            // Higher WWR = more daylight (up to a point)
            return Math.Min(wwr * 1.5, 1.0);
        }
    }

    public class EnergyEvaluatorTest
    {
        public double EvaluateEnergy(double wwr)
        {
            // Lower WWR = better energy performance
            return 1.0 - (wwr * 0.7);
        }
    }

    #endregion
}
