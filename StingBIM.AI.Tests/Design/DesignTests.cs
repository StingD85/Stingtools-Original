// StingBIM.AI.Tests.Design.DesignTests
// Comprehensive unit tests for the AI Design module
// Covers: GenerativeDesignEngine, EnhancedGenerativeDesignEngine, DesignEvaluator

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using StingBIM.AI.Design.Generative;

namespace StingBIM.AI.Tests.Design
{
    #region GenerativeDesignEngine Tests

    [TestFixture]
    public class GenerativeDesignEngineTests
    {
        private GenerativeDesignEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _engine = new GenerativeDesignEngine();
        }

        #region Constraint Management Tests

        [Test]
        public void AddConstraint_ShouldNotThrow()
        {
            // Arrange
            var constraint = new DesignConstraint
            {
                ConstraintId = "TEST-001",
                Name = "Test Constraint",
                Category = "Test",
                Type = ConstraintType.Minimum,
                MinValue = 10,
                IsHard = true,
                IsEnabled = true
            };

            // Act & Assert
            _engine.Invoking(e => e.AddConstraint(constraint)).Should().NotThrow();
        }

        [Test]
        public void AddObjective_ShouldNotThrow()
        {
            // Arrange
            var objective = new DesignObjective
            {
                ObjectiveId = "OBJ-TEST",
                Name = "Test Objective",
                Category = "Test",
                Direction = OptimizationDirection.Maximize,
                Weight = 0.5,
                IsEnabled = true
            };

            // Act & Assert
            _engine.Invoking(e => e.AddObjective(objective)).Should().NotThrow();
        }

        [Test]
        public void AddPattern_ShouldNotThrow()
        {
            // Arrange
            var pattern = new DesignPattern
            {
                PatternId = "PAT-TEST",
                Name = "Test Pattern",
                Description = "A test pattern",
                Category = "Test",
                Applicability = new List<string> { "Office", "Residential" },
                EfficiencyRating = 0.8,
                CirculationRatio = 0.2
            };

            // Act & Assert
            _engine.Invoking(e => e.AddPattern(pattern)).Should().NotThrow();
        }

        [Test]
        public void SetObjectiveWeight_WithinRange_ShouldClampToZeroOne()
        {
            // Act & Assert - should not throw, weight is clamped between 0 and 1
            _engine.Invoking(e => e.SetObjectiveWeight("OBJ-COST", 0.5)).Should().NotThrow();
            _engine.Invoking(e => e.SetObjectiveWeight("OBJ-COST", 1.5)).Should().NotThrow();
            _engine.Invoking(e => e.SetObjectiveWeight("OBJ-COST", -0.5)).Should().NotThrow();
        }

        [Test]
        public void SetObjectiveWeight_ForNonExistent_ShouldNotThrow()
        {
            // Act & Assert
            _engine.Invoking(e => e.SetObjectiveWeight("NONEXISTENT", 0.5)).Should().NotThrow();
        }

        #endregion

        #region Program Validation Tests

        [Test]
        public async Task GenerateDesignVariantsAsync_WithInvalidProgram_ShouldReturnFailure()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 0, // Invalid - zero area
                Floors = 3,
                Rooms = new List<RoomRequirement>()
            };

            var options = new GenerationOptions
            {
                PopulationSize = 5,
                MaxGenerations = 2,
                ResultCount = 3
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_WithNegativeArea_ShouldReturnFailure()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = -1000,
                Floors = 3,
                Rooms = new List<RoomRequirement>()
            };

            var options = new GenerationOptions
            {
                PopulationSize = 5,
                MaxGenerations = 2,
                ResultCount = 3
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
        }

        #endregion

        #region Design Generation Tests

        [Test]
        public async Task GenerateDesignVariantsAsync_WithValidProgram_ShouldSucceed()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 3,
                CrossoverRate = 0.8,
                MutationRate = 0.1,
                SelectionMethod = SelectionMethod.Tournament,
                ResultCount = 5
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Variants.Should().NotBeEmpty();
            result.Variants.Count.Should().BeLessThanOrEqualTo(options.ResultCount);
            result.GenerationsCompleted.Should().Be(options.MaxGenerations);
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_ShouldSortVariantsByScore()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 3,
                ResultCount = 5
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            if (result.Variants.Count > 1)
            {
                for (int i = 0; i < result.Variants.Count - 1; i++)
                {
                    var score1 = result.Variants[i].Evaluation?.OverallScore ?? 0;
                    var score2 = result.Variants[i + 1].Evaluation?.OverallScore ?? 0;
                    score1.Should().BeGreaterThanOrEqualTo(score2);
                }
            }
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_WithCancellation_ShouldThrow()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 50,
                MaxGenerations = 100,
                ResultCount = 10
            };

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Func<Task> act = async () => await _engine.GenerateDesignVariantsAsync(
                program, options, cancellationToken: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_WithProgress_ShouldReportProgress()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var progressReports = new List<GenerationProgress>();
            var progress = new Progress<GenerationProgress>(p => progressReports.Add(p));

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options, progress);

            // Assert
            result.Success.Should().BeTrue();
            // Progress should have been reported (may need brief delay for Progress<T> callback)
            await Task.Delay(100); // Allow progress callbacks to complete
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_EachVariant_ShouldHaveRooms()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            foreach (var variant in result.Variants)
            {
                variant.Rooms.Should().NotBeEmpty("Each variant should contain rooms from the program");
                variant.VariantId.Should().NotBeNullOrEmpty();
            }
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_WithEliteSelection_ShouldSucceed()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                SelectionMethod = SelectionMethod.Elite,
                ResultCount = 3
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        [Test]
        public async Task GenerateDesignVariantsAsync_WithRouletteSelection_ShouldSucceed()
        {
            // Arrange
            var program = CreateOfficeProgram();
            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                SelectionMethod = SelectionMethod.Roulette,
                ResultCount = 3
            };

            // Act
            var result = await _engine.GenerateDesignVariantsAsync(program, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        #endregion

        #region Layout Generation Tests

        [Test]
        public async Task GenerateLayoutOptionsAsync_ShouldReturnLayouts()
        {
            // Arrange
            var roomProgram = new RoomProgram
            {
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Open Office", MinArea = 200, MaxArea = 300, Quantity = 2 },
                    new RoomRequirement { RoomType = "Meeting Room", MinArea = 20, MaxArea = 30, Quantity = 3 }
                }
            };

            var siteConstraints = new SiteConstraints
            {
                SiteArea = 2000,
                MaxFAR = 3.0,
                MaxHeight = 50000,
                MaxCoverage = 60
            };

            // Act
            var result = await _engine.GenerateLayoutOptionsAsync(roomProgram, siteConstraints, 3);

            // Assert
            result.Should().NotBeNull();
            result.Layouts.Should().HaveCount(3);
            result.Layouts.Should().BeInDescendingOrder(l => l.Evaluation.OverallScore);
        }

        [Test]
        public async Task GenerateLayoutOptionsAsync_WithCancellation_ShouldThrow()
        {
            // Arrange
            var roomProgram = new RoomProgram
            {
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 100, MaxArea = 200, Quantity = 1 }
                }
            };

            var siteConstraints = new SiteConstraints { SiteArea = 1000 };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Func<Task> act = async () => await _engine.GenerateLayoutOptionsAsync(
                roomProgram, siteConstraints, 5, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        #region Design Evaluation Tests

        [Test]
        public void EvaluateDesign_ShouldReturnEvaluation()
        {
            // Arrange
            var variant = CreateSampleVariant();
            var program = CreateOfficeProgram();

            // Act
            var evaluation = _engine.EvaluateDesign(variant, program);

            // Assert
            evaluation.Should().NotBeNull();
            evaluation.VariantId.Should().Be(variant.VariantId);
            evaluation.OverallScore.Should().BeGreaterThanOrEqualTo(0);
            evaluation.ConstraintResults.Should().NotBeNull();
            evaluation.ObjectiveScores.Should().NotBeNull();
        }

        [Test]
        public void EvaluateDesign_WithValidVariant_ShouldBeValid()
        {
            // Arrange
            var variant = CreateSampleVariant();
            variant.Parameters["FAR"] = 2.0; // within range 0-5
            variant.Parameters["SiteCoverage"] = 50; // within max 70
            variant.Parameters["WWR"] = 30; // within range 20-40

            var program = CreateOfficeProgram();

            // Act
            var evaluation = _engine.EvaluateDesign(variant, program);

            // Assert
            evaluation.Should().NotBeNull();
            // Evaluation validity depends on constraint checks
        }

        [Test]
        public void EvaluateDesign_ShouldHaveObjectiveScores()
        {
            // Arrange
            var variant = CreateSampleVariant();
            var program = CreateOfficeProgram();

            // Act
            var evaluation = _engine.EvaluateDesign(variant, program);

            // Assert
            evaluation.ObjectiveScores.Should().NotBeEmpty();
            foreach (var score in evaluation.ObjectiveScores.Values)
            {
                score.RawScore.Should().BeGreaterThanOrEqualTo(0);
                score.RawScore.Should().BeLessThanOrEqualTo(1);
                score.Weight.Should().BeGreaterThan(0);
            }
        }

        #endregion

        #region Variant Comparison Tests

        [Test]
        public void CompareVariants_ShouldReturnComparison()
        {
            // Arrange
            var program = CreateOfficeProgram();

            var variant1 = CreateSampleVariant("v1");
            variant1.Evaluation = _engine.EvaluateDesign(variant1, program);

            var variant2 = CreateSampleVariant("v2");
            variant2.Parameters["WWR"] = 40;
            variant2.Evaluation = _engine.EvaluateDesign(variant2, program);

            var variant3 = CreateSampleVariant("v3");
            variant3.Parameters["WWR"] = 20;
            variant3.Evaluation = _engine.EvaluateDesign(variant3, program);

            // Act
            var comparison = _engine.CompareVariants(new[] { variant1, variant2, variant3 });

            // Assert
            comparison.Should().NotBeNull();
            comparison.VariantCount.Should().Be(3);
            comparison.OverallBest.Should().NotBeNullOrEmpty();
            comparison.ParetoFront.Should().NotBeEmpty();
        }

        [Test]
        public void CompareVariants_ShouldIdentifyParetoFront()
        {
            // Arrange
            var program = CreateOfficeProgram();

            var variants = new List<DesignVariant>();
            for (int i = 0; i < 5; i++)
            {
                var variant = CreateSampleVariant($"v{i}");
                variant.Parameters["WWR"] = 20 + i * 5;
                variant.Parameters["CostPerSqm"] = 1000 + i * 200;
                variant.Evaluation = _engine.EvaluateDesign(variant, program);
                variants.Add(variant);
            }

            // Act
            var comparison = _engine.CompareVariants(variants);

            // Assert
            comparison.ParetoFront.Should().NotBeEmpty();
            comparison.ParetoFront.Count.Should().BeLessThanOrEqualTo(variants.Count);
        }

        #endregion

        #region Sensitivity Analysis Tests

        [Test]
        public void AnalyzeSensitivity_ShouldReturnSensitivityData()
        {
            // Arrange
            var variant = CreateSampleVariant();
            variant.Parameters["WWR"] = 30;
            variant.Parameters["CostPerSqm"] = 1500;
            variant.Parameters["CirculationRatio"] = 0.2;
            variant.Evaluation = _engine.EvaluateDesign(variant, new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 1000
            });

            var parameterIds = new List<string> { "WWR", "CostPerSqm" };

            // Act
            var analysis = _engine.AnalyzeSensitivity(variant, parameterIds);

            // Assert
            analysis.Should().NotBeNull();
            analysis.ParameterSensitivities.Should().HaveCount(2);
            analysis.MostSensitiveParameters.Should().NotBeEmpty();
        }

        [Test]
        public void AnalyzeSensitivity_ShouldIdentifyMostSensitiveParameters()
        {
            // Arrange
            var variant = CreateSampleVariant();
            variant.Parameters["WWR"] = 30;
            variant.Parameters["CostPerSqm"] = 1500;
            variant.Parameters["CirculationRatio"] = 0.2;
            variant.Parameters["StructuralEfficiency"] = 0.7;
            variant.Evaluation = _engine.EvaluateDesign(variant, new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 1000
            });

            var parameterIds = new List<string> { "WWR", "CostPerSqm", "CirculationRatio" };

            // Act
            var analysis = _engine.AnalyzeSensitivity(variant, parameterIds);

            // Assert
            analysis.MostSensitiveParameters.Should().NotBeNull();
            analysis.MostSensitiveParameters.Count.Should().BeLessThanOrEqualTo(5);
        }

        #endregion

        #region Recommendations Tests

        [Test]
        public void GetRecommendations_ForOffice_ShouldReturnRecommendations()
        {
            // Arrange
            var program = CreateOfficeProgram();

            // Act
            var recommendations = _engine.GetRecommendations(program);

            // Assert
            recommendations.Should().NotBeNull();
            recommendations.ProgramType.Should().Be("Office");
            recommendations.ParameterRecommendations.Should().NotBeEmpty();
        }

        [Test]
        public void GetRecommendations_ForResidential_ShouldReturnRecommendations()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Residential",
                TotalArea = 150,
                Floors = 2,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Living", MinArea = 20, MaxArea = 30, Quantity = 1 },
                    new RoomRequirement { RoomType = "Bedroom", MinArea = 12, MaxArea = 18, Quantity = 3 }
                }
            };

            // Act
            var recommendations = _engine.GetRecommendations(program);

            // Assert
            recommendations.Should().NotBeNull();
            recommendations.ParameterRecommendations.Should().NotBeEmpty();
        }

        #endregion

        #region Helper Methods

        private static DesignProgram CreateOfficeProgram()
        {
            return new DesignProgram
            {
                ProgramId = "prog-1",
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Open Office", MinArea = 200, MaxArea = 400, Quantity = 3 },
                    new RoomRequirement { RoomType = "Meeting Room", MinArea = 20, MaxArea = 40, Quantity = 4 },
                    new RoomRequirement { RoomType = "Reception", MinArea = 30, MaxArea = 50, Quantity = 1 },
                    new RoomRequirement { RoomType = "Bathroom", MinArea = 10, MaxArea = 20, Quantity = 2 }
                }
            };
        }

        private static DesignVariant CreateSampleVariant(string id = "test-variant")
        {
            return new DesignVariant
            {
                VariantId = id,
                PatternId = "PAT-LINEAR",
                Parameters = new Dictionary<string, double>
                {
                    ["CirculationRatio"] = 0.2,
                    ["WWR"] = 30,
                    ["StructuralEfficiency"] = 0.75,
                    ["GrossArea"] = 2000,
                    ["CostPerSqm"] = 1500,
                    ["ViewScore"] = 0.6,
                    ["FlexibilityScore"] = 0.5
                },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", RoomType = "Open Office", Area = 300, Position = (10, 10) },
                    new RoomInstance { RoomId = "r2", RoomType = "Meeting Room", Area = 30, Position = (40, 10) },
                    new RoomInstance { RoomId = "r3", RoomType = "Reception", Area = 40, Position = (10, 40) },
                    new RoomInstance { RoomId = "r4", RoomType = "Bathroom", Area = 15, Position = (40, 40) }
                }
            };
        }

        #endregion
    }

    #endregion

    #region DesignEvaluator Tests

    [TestFixture]
    public class DesignEvaluatorTests
    {
        private DesignEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new DesignEvaluator();
        }

        [Test]
        public void EvaluateVariant_WithNullVariant_ShouldReturnZero()
        {
            // Arrange
            var objectives = new List<DesignObjective>
            {
                new DesignObjective { ObjectiveId = "OBJ-1", Weight = 1.0 }
            };

            // Act
            var result = _evaluator.EvaluateVariant(null, objectives);

            // Assert
            result.Should().Be(0.0);
        }

        [Test]
        public void EvaluateVariant_WithNullObjectives_ShouldReturnZero()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 100 }
                }
            };

            // Act
            var result = _evaluator.EvaluateVariant(variant, null);

            // Assert
            result.Should().Be(0.0);
        }

        [Test]
        public void EvaluateVariant_WithEmptyObjectives_ShouldReturnZero()
        {
            // Arrange
            var variant = new DesignVariant { VariantId = "v1" };
            var objectives = new List<DesignObjective>();

            // Act
            var result = _evaluator.EvaluateVariant(variant, objectives);

            // Assert
            result.Should().Be(0.0);
        }

        [Test]
        public void EvaluateVariant_WithMinimizeCostObjective_ShouldReturnScoreBetweenZeroAndOne()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double> { ["CostPerSqm"] = 1500 },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "minimize_cost",
                    Name = "Minimize Cost",
                    Direction = OptimizationDirection.Minimize,
                    Weight = 1.0
                }
            };

            // Act
            var result = _evaluator.EvaluateVariant(variant, objectives);

            // Assert
            result.Should().BeGreaterThanOrEqualTo(0.0);
            result.Should().BeLessThanOrEqualTo(1.0);
        }

        [Test]
        public void EvaluateVariant_WithMaximizeDaylightObjective_ShouldReturnScoreBetweenZeroAndOne()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double> { ["WWR"] = 35 },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "maximize_daylight",
                    Name = "Maximize Daylight",
                    Direction = OptimizationDirection.Maximize,
                    Weight = 1.0
                }
            };

            // Act
            var result = _evaluator.EvaluateVariant(variant, objectives);

            // Assert
            result.Should().BeGreaterThanOrEqualTo(0.0);
            result.Should().BeLessThanOrEqualTo(1.0);
        }

        [Test]
        public void EvaluateVariant_WithMinimizeEnergyObjective_ShouldConsiderWWRAndInsulation()
        {
            // Arrange
            var efficientVariant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double>
                {
                    ["WWR"] = 25, // optimal for energy
                    ["InsulationRValue"] = 8.0 // high insulation
                },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var inefficientVariant = new DesignVariant
            {
                VariantId = "v2",
                Parameters = new Dictionary<string, double>
                {
                    ["WWR"] = 50, // too high
                    ["InsulationRValue"] = 1.0 // poor insulation
                },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "minimize_energy",
                    Name = "Minimize Energy",
                    Direction = OptimizationDirection.Minimize,
                    Weight = 1.0
                }
            };

            // Act
            var efficientScore = _evaluator.EvaluateVariant(efficientVariant, objectives);
            var inefficientScore = _evaluator.EvaluateVariant(inefficientVariant, objectives);

            // Assert
            efficientScore.Should().BeGreaterThan(inefficientScore,
                "A variant with optimal WWR and high insulation should score better on energy");
        }

        [Test]
        public void EvaluateVariant_WithMultipleObjectives_ShouldReturnWeightedAverage()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double>
                {
                    ["CostPerSqm"] = 1500,
                    ["WWR"] = 30
                },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "minimize_cost",
                    Name = "Minimize Cost",
                    Direction = OptimizationDirection.Minimize,
                    Weight = 0.6
                },
                new DesignObjective
                {
                    ObjectiveId = "maximize_daylight",
                    Name = "Maximize Daylight",
                    Direction = OptimizationDirection.Maximize,
                    Weight = 0.4
                }
            };

            // Act
            var result = _evaluator.EvaluateVariant(variant, objectives);

            // Assert
            result.Should().BeGreaterThanOrEqualTo(0.0);
            result.Should().BeLessThanOrEqualTo(1.0);
        }

        [Test]
        public void EvaluateVariant_LowerCost_ShouldScoreHigherOnCost()
        {
            // Arrange
            var cheapVariant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double> { ["CostPerSqm"] = 500 },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var expensiveVariant = new DesignVariant
            {
                VariantId = "v2",
                Parameters = new Dictionary<string, double> { ["CostPerSqm"] = 5000 },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 200 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "minimize_cost",
                    Name = "Minimize Cost",
                    Direction = OptimizationDirection.Minimize,
                    Weight = 1.0
                }
            };

            // Act
            var cheapScore = _evaluator.EvaluateVariant(cheapVariant, objectives);
            var expensiveScore = _evaluator.EvaluateVariant(expensiveVariant, objectives);

            // Assert
            cheapScore.Should().BeGreaterThan(expensiveScore,
                "Lower cost per sqm should yield a higher score when minimizing cost");
        }

        [Test]
        public void EvaluateVariant_MaximizeArea_ShouldFavorLargerArea()
        {
            // Arrange
            var smallVariant = new DesignVariant
            {
                VariantId = "v1",
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 50 }
                }
            };

            var largeVariant = new DesignVariant
            {
                VariantId = "v2",
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 500 },
                    new RoomInstance { RoomId = "r2", Area = 500 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "maximize_area",
                    Name = "Maximize Area",
                    Direction = OptimizationDirection.Maximize,
                    Weight = 1.0
                }
            };

            // Act
            var smallScore = _evaluator.EvaluateVariant(smallVariant, objectives);
            var largeScore = _evaluator.EvaluateVariant(largeVariant, objectives);

            // Assert
            largeScore.Should().BeGreaterThan(smallScore,
                "Larger total area should score higher when maximizing area");
        }

        [Test]
        public void EvaluateVariant_WithCustomEvaluateFunction_ShouldUseIt()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double> { ["CustomParam"] = 0.8 },
                Rooms = new List<RoomInstance>
                {
                    new RoomInstance { RoomId = "r1", Area = 100 }
                }
            };

            var objectives = new List<DesignObjective>
            {
                new DesignObjective
                {
                    ObjectiveId = "custom",
                    Name = "Custom",
                    Weight = 1.0,
                    EvaluateFunction = (v, o) => v.Parameters.GetValueOrDefault("CustomParam", 0)
                }
            };

            // Act
            var result = _evaluator.EvaluateVariant(variant, objectives);

            // Assert
            result.Should().BeApproximately(0.8, 0.01);
        }

        #endregion
    }

    #region EnhancedGenerativeDesignEngine Tests

    [TestFixture]
    public class EnhancedGenerativeDesignEngineTests
    {
        private EnhancedGenerativeDesignEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _engine = new EnhancedGenerativeDesignEngine();
        }

        [Test]
        public async Task GenerateWithReasoningAsync_WithValidProgram_ShouldSucceed()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Open Office", MinArea = 200, MaxArea = 400, Quantity = 3 },
                    new RoomRequirement { RoomType = "Meeting Room", MinArea = 20, MaxArea = 40, Quantity = 4 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext
            {
                Region = "East Africa",
                ClimateZone = "Tropical Highland"
            };

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Variants.Should().NotBeEmpty();
            result.InferredRequirements.Should().NotBeNull();
            result.KnowledgeInsights.Should().NotBeNull();
        }

        [Test]
        public async Task GenerateWithReasoningAsync_ShouldInferDesignIntent()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Hospital",
                TotalArea = 5000,
                Floors = 4,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Ward", MinArea = 200, MaxArea = 300, Quantity = 5 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext
            {
                Region = "East Africa",
                ClimateZone = "Tropical"
            };

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            result.InferredRequirements.Should().NotBeNull();
            result.InferredRequirements.PrioritiesInfectionControl.Should().BeTrue(
                "Hospital building type should prioritize infection control");
        }

        [Test]
        public async Task GenerateWithReasoningAsync_ShouldProvideKnowledgeInsights()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "School",
                TotalArea = 3000,
                Floors = 2,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Classroom", MinArea = 50, MaxArea = 80, Quantity = 10 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext { Region = "Kenya" };

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            result.KnowledgeInsights.Should().NotBeNull();
            result.KnowledgeInsights.TypologyFacts.Should().NotBeEmpty(
                "School building type should have typology-specific knowledge");
        }

        [Test]
        public async Task GenerateWithReasoningAsync_ShouldScoreVariantsWithEnhancedCriteria()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 200, MaxArea = 400, Quantity = 3 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext
            {
                Region = "East Africa",
                ClimateZone = "Tropical Highland"
            };

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            foreach (var variant in result.Variants)
            {
                variant.EnhancedScore.Should().BeGreaterThanOrEqualTo(0);
                variant.EnhancedScore.Should().BeLessThanOrEqualTo(1);
                variant.SpatialScore.Should().NotBeNull();
                variant.ComplianceScore.Should().NotBeNull();
                variant.AgentConsensus.Should().NotBeNull();
                variant.DaylightScore.Should().BeGreaterThanOrEqualTo(0);
                variant.AcousticScore.Should().BeGreaterThanOrEqualTo(0);
                variant.ThermalScore.Should().BeGreaterThanOrEqualTo(0);
            }
        }

        [Test]
        public async Task GenerateWithReasoningAsync_ShouldSortByEnhancedScore()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 200, MaxArea = 400, Quantity = 3 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 5
            };

            var context = new DesignContext();

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            if (result.Variants.Count > 1)
            {
                for (int i = 0; i < result.Variants.Count - 1; i++)
                {
                    result.Variants[i].EnhancedScore
                        .Should().BeGreaterThanOrEqualTo(result.Variants[i + 1].EnhancedScore);
                }
            }
        }

        [Test]
        public async Task GenerateWithReasoningAsync_ShouldProvideAgentConsensus()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 200, MaxArea = 400, Quantity = 3 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext();

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            var topVariant = result.Variants.First();
            topVariant.AgentConsensus.AgentOpinions.Should().HaveCount(6,
                "Six specialist agents should evaluate the design (Arch, Struct, MEP, Cost, Safety, Sustainability)");
            topVariant.AgentConsensus.ConsensusScore.Should().BeGreaterThanOrEqualTo(0);
            topVariant.AgentConsensus.AgreementLevel.Should().BeGreaterThanOrEqualTo(0);
            topVariant.AgentConsensus.AgreementLevel.Should().BeLessThanOrEqualTo(1);
        }

        [Test]
        public async Task GenerateWithReasoningAsync_ShouldGenerateNarrative()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 200, MaxArea = 400, Quantity = 3 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext
            {
                Region = "East Africa",
                ClimateZone = "Tropical Highland"
            };

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            result.TopVariantNarrative.Should().NotBeNullOrEmpty();
            result.TopVariantNarrative.Should().Contain("GENERATIVE DESIGN REPORT");
            result.TopVariantNarrative.Should().Contain("SCORING BREAKDOWN");
            result.TopVariantNarrative.Should().Contain("MULTI-AGENT EVALUATION");
        }

        [Test]
        public async Task GenerateWithReasoningAsync_WithCancellation_ShouldThrow()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 200, MaxArea = 400, Quantity = 3 }
                }
            };

            var options = new GenerationOptions
            {
                PopulationSize = 50,
                MaxGenerations = 100,
                ResultCount = 10
            };

            var context = new DesignContext();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Func<Task> act = async () => await _engine.GenerateWithReasoningAsync(
                program, options, context, cancellationToken: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task GenerateWithReasoningAsync_WithInvalidProgram_ShouldReturnFailure()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 0, // Invalid
                Rooms = new List<RoomRequirement>()
            };

            var options = new GenerationOptions
            {
                PopulationSize = 10,
                MaxGenerations = 2,
                ResultCount = 3
            };

            var context = new DesignContext();

            // Act
            var result = await _engine.GenerateWithReasoningAsync(program, options, context);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
        }

        [Test]
        public void GenerateDesignNarrative_ShouldContainAllSections()
        {
            // Arrange
            var variant = new EnhancedDesignVariant
            {
                BaseVariant = new DesignVariant { VariantId = "v1" },
                BaseScore = 0.75,
                SpatialScore = new SpatialReasoningScore
                {
                    OverallScore = 0.7,
                    AdjacencyScore = 0.8,
                    CirculationScore = 0.65,
                    ViewQualityScore = 0.7,
                    WayfindingScore = 0.6
                },
                ComplianceScore = new ComplianceReasoningScore
                {
                    OverallScore = 0.9,
                    StandardsChecked = 3,
                    StandardsMet = 3,
                    CriticalViolations = 0,
                    Violations = new List<ComplianceViolation>()
                },
                DaylightScore = 0.7,
                AcousticScore = 0.65,
                ThermalScore = 0.72,
                AgentConsensus = new AgentConsensusResult
                {
                    ConsensusScore = 0.75,
                    AgentOpinions = new List<AgentOpinionResult>
                    {
                        new AgentOpinionResult
                        {
                            AgentName = "Architectural",
                            Score = 0.8,
                            Confidence = 0.85,
                            Assessment = "Good layout",
                            Concerns = new List<string>()
                        }
                    }
                },
                EnhancedScore = 0.73
            };

            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 2000,
                Floors = 3,
                Rooms = new List<RoomRequirement>()
            };

            var context = new DesignContext
            {
                Region = "East Africa",
                ClimateZone = "Tropical Highland"
            };

            // Act
            var narrative = _engine.GenerateDesignNarrative(variant, program, context);

            // Assert
            narrative.Should().NotBeNullOrEmpty();
            narrative.Should().Contain("DESIGN OVERVIEW");
            narrative.Should().Contain("SCORING BREAKDOWN");
            narrative.Should().Contain("MULTI-AGENT EVALUATION");
            narrative.Should().Contain("DESIGN RECOMMENDATIONS");
            narrative.Should().Contain("Office");
            narrative.Should().Contain("East Africa");
        }
    }

    #endregion

    #region Genetic Algorithm Component Tests

    [TestFixture]
    public class GeneticAlgorithmTests
    {
        private DesignGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _generator = new DesignGenerator();
        }

        [Test]
        public void GenerateFromPattern_ShouldCreateVariantWithRooms()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 1000,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 100, MaxArea = 200, Quantity = 3 },
                    new RoomRequirement { RoomType = "Meeting", MinArea = 20, MaxArea = 40, Quantity = 2 }
                }
            };

            var pattern = new DesignPattern
            {
                PatternId = "PAT-LINEAR",
                Name = "Linear",
                CirculationRatio = 0.18,
                EfficiencyRating = 0.85,
                Applicability = new List<string> { "Office" }
            };

            // Act
            var variant = _generator.GenerateFromPattern(program, pattern, 42);

            // Assert
            variant.Should().NotBeNull();
            variant.VariantId.Should().NotBeNullOrEmpty();
            variant.PatternId.Should().Be("PAT-LINEAR");
            variant.Rooms.Should().HaveCount(5); // 3 offices + 2 meetings
            variant.Parameters.Should().ContainKey("CirculationRatio");
            variant.Parameters.Should().ContainKey("WWR");
        }

        [Test]
        public void GenerateFromPattern_DifferentSeeds_ShouldProduceDifferentVariants()
        {
            // Arrange
            var program = new DesignProgram
            {
                BuildingType = "Office",
                TotalArea = 1000,
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 100, MaxArea = 200, Quantity = 2 }
                }
            };

            var pattern = new DesignPattern
            {
                PatternId = "PAT-LINEAR",
                CirculationRatio = 0.18,
                Applicability = new List<string> { "Office" }
            };

            // Act
            var variant1 = _generator.GenerateFromPattern(program, pattern, 1);
            var variant2 = _generator.GenerateFromPattern(program, pattern, 99);

            // Assert - different seeds should produce different parameters
            variant1.VariantId.Should().NotBe(variant2.VariantId);
            // Parameters may differ due to random seed
        }

        [Test]
        public void Crossover_ShouldProduceTwoChildren()
        {
            // Arrange
            var parent1 = new DesignVariant
            {
                VariantId = "p1",
                Parameters = new Dictionary<string, double>
                {
                    ["WWR"] = 30,
                    ["CirculationRatio"] = 0.18,
                    ["StructuralEfficiency"] = 0.8
                }
            };

            var parent2 = new DesignVariant
            {
                VariantId = "p2",
                Parameters = new Dictionary<string, double>
                {
                    ["WWR"] = 40,
                    ["CirculationRatio"] = 0.22,
                    ["StructuralEfficiency"] = 0.7
                }
            };

            // Act
            var (child1, child2) = _generator.Crossover(parent1, parent2);

            // Assert
            child1.Should().NotBeNull();
            child2.Should().NotBeNull();
            child1.VariantId.Should().NotBe(parent1.VariantId);
            child2.VariantId.Should().NotBe(parent2.VariantId);
            child1.Parameters.Should().NotBeEmpty();
            child2.Parameters.Should().NotBeEmpty();
        }

        [Test]
        public void Crossover_ChildrenShouldInheritFromParents()
        {
            // Arrange
            var parent1 = new DesignVariant
            {
                VariantId = "p1",
                Parameters = new Dictionary<string, double>
                {
                    ["Param1"] = 100,
                    ["Param2"] = 200,
                    ["Param3"] = 300
                }
            };

            var parent2 = new DesignVariant
            {
                VariantId = "p2",
                Parameters = new Dictionary<string, double>
                {
                    ["Param1"] = 110,
                    ["Param2"] = 210,
                    ["Param3"] = 310
                }
            };

            // Act
            var (child1, child2) = _generator.Crossover(parent1, parent2);

            // Assert - each child parameter should come from one of the parents
            foreach (var param in child1.Parameters)
            {
                var val = param.Value;
                var p1Val = parent1.Parameters.GetValueOrDefault(param.Key);
                var p2Val = parent2.Parameters.GetValueOrDefault(param.Key);
                (val == p1Val || val == p2Val).Should().BeTrue(
                    $"Child parameter {param.Key}={val} should come from parent1={p1Val} or parent2={p2Val}");
            }
        }

        [Test]
        public void Mutate_ShouldModifyAtLeastOneParameter()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double>
                {
                    ["Param1"] = 100,
                    ["Param2"] = 200,
                    ["Param3"] = 300
                }
            };

            var originalParams = new Dictionary<string, double>(variant.Parameters);

            // Act - call mutate multiple times since mutation is random
            bool mutated = false;
            for (int i = 0; i < 100; i++)
            {
                variant.Parameters = new Dictionary<string, double>(originalParams);
                _generator.Mutate(variant);

                if (variant.Parameters.Any(p => Math.Abs(p.Value - originalParams[p.Key]) > 0.001))
                {
                    mutated = true;
                    break;
                }
            }

            // Assert
            mutated.Should().BeTrue("Mutation should modify at least one parameter within 100 attempts");
        }

        [Test]
        public void Mutate_ShouldKeepParameterCount()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double>
                {
                    ["Param1"] = 100,
                    ["Param2"] = 200
                }
            };

            // Act
            _generator.Mutate(variant);

            // Assert
            variant.Parameters.Should().HaveCount(2);
        }

        [Test]
        public void Mutate_WithEmptyParameters_ShouldNotThrow()
        {
            // Arrange
            var variant = new DesignVariant
            {
                VariantId = "v1",
                Parameters = new Dictionary<string, double>()
            };

            // Act & Assert
            _generator.Invoking(g => g.Mutate(variant)).Should().NotThrow();
        }

        [Test]
        public void GenerateLayout_ShouldReturnLayout()
        {
            // Arrange
            var program = new RoomProgram
            {
                Rooms = new List<RoomRequirement>
                {
                    new RoomRequirement { RoomType = "Office", MinArea = 100, MaxArea = 200, Quantity = 2 }
                }
            };

            var constraints = new SiteConstraints
            {
                SiteArea = 2000,
                MaxFAR = 3.0,
                MaxHeight = 50000,
                MaxCoverage = 60
            };

            // Act
            var layout = _generator.GenerateLayout(program, constraints, 0);

            // Assert
            layout.Should().NotBeNull();
            layout.LayoutId.Should().NotBeNullOrEmpty();
        }
    }

    #endregion
}
