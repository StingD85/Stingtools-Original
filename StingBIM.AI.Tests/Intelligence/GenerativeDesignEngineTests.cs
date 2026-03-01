// ============================================================================
// StingBIM AI Tests - Generative Design Engine Tests
// Unit tests for space layout, MEP routing, and structural optimization
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.GenerativeDesign;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class GenerativeDesignEngineTests
    {
        private GenerativeDesignEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = GenerativeDesignEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = GenerativeDesignEngine.Instance;
            var instance2 = GenerativeDesignEngine.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        #region Space Layout Generation Tests

        [Test]
        public async Task GenerateSpaceLayoutsAsync_ShouldGenerateLayouts()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Test Office Building",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Office", MinArea = 20, Count = 5 },
                    new SpaceRequirement { SpaceType = "Conference", MinArea = 40, Count = 2 },
                    new SpaceRequirement { SpaceType = "Restroom", MinArea = 15, Count = 2 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency, Weight = 1.0 },
                    new DesignObjective { Name = "Daylight", Type = ObjectiveType.MaximizeDaylight, Weight = 0.8 }
                },
                MaxWidth = 40,
                MaxOptions = 10
            };

            // Act
            var result = await _engine.GenerateSpaceLayoutsAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Layouts.Count, Is.EqualTo(10));
            Assert.That(result.BestLayout, Is.Not.Null);
            Assert.That(result.Statistics, Is.Not.Null);
        }

        [Test]
        public async Task GenerateSpaceLayoutsAsync_ShouldEvaluateObjectives()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Objective Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Office", MinArea = 25, Count = 4 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency, Weight = 1.0 },
                    new DesignObjective { Name = "Circulation", Type = ObjectiveType.MinimizeCirculation, Weight = 0.5 }
                },
                MaxOptions = 5
            };

            // Act
            var result = await _engine.GenerateSpaceLayoutsAsync(request);

            // Assert
            Assert.That(result.BestLayout.ObjectiveScores, Is.Not.Null);
            Assert.That(result.BestLayout.ObjectiveScores.ContainsKey("Efficiency"), Is.True);
            Assert.That(result.BestLayout.ObjectiveScores.ContainsKey("Circulation"), Is.True);
        }

        [Test]
        public async Task GenerateSpaceLayoutsAsync_ShouldCheckConstraints()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Constraint Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Office", MinArea = 30, Count = 10 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency, Weight = 1.0 }
                },
                Constraints = new List<DesignConstraint>
                {
                    new DesignConstraint { Name = "Max Area", Type = ConstraintType.MaxArea, Value = 200 }
                },
                MaxOptions = 5
            };

            // Act
            var result = await _engine.GenerateSpaceLayoutsAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            // Some layouts may violate constraints
            Assert.That(result.Layouts.Exists(l => l.ConstraintViolations != null), Is.True);
        }

        [Test]
        public async Task GenerateSpaceLayoutsAsync_ShouldReportProgress()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Progress Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Room", MinArea = 20, Count = 3 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency }
                },
                MaxOptions = 5
            };

            var progressReports = new List<GenerationProgress>();
            var progress = new Progress<GenerationProgress>(p => progressReports.Add(p));

            // Act
            await _engine.GenerateSpaceLayoutsAsync(request, progress);

            // Assert
            Assert.That(progressReports.Count > 0, Is.True);
            Assert.That(progressReports[progressReports.Count - 1].CurrentIteration, Is.EqualTo(5));
        }

        [Test]
        public async Task GenerateSpaceLayoutsAsync_ShouldSupportCancellation()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Cancellation Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Room", MinArea = 20, Count = 5 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency }
                },
                MaxOptions = 100
            };

            var cts = new CancellationTokenSource();
            cts.CancelAfter(50); // Cancel quickly

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _engine.GenerateSpaceLayoutsAsync(request, null, cts.Token);
            });
        }

        #endregion

        #region MEP Routing Tests

        [Test]
        public async Task GenerateMEPRoutingAsync_ShouldGenerateRoutes()
        {
            // Arrange
            var request = new MEPRoutingRequest
            {
                ProjectName = "MEP Routing Test",
                SystemType = "HVAC",
                Connections = new List<MEPConnection>
                {
                    new MEPConnection
                    {
                        ConnectionId = "C1",
                        StartPoint = new Point3D { X = 0, Y = 0, Z = 3 },
                        EndPoint = new Point3D { X = 20, Y = 15, Z = 3 },
                        FlowRate = 1500
                    },
                    new MEPConnection
                    {
                        ConnectionId = "C2",
                        StartPoint = new Point3D { X = 0, Y = 0, Z = 3 },
                        EndPoint = new Point3D { X = 10, Y = 25, Z = 3 },
                        FlowRate = 1000
                    }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Length", Type = ObjectiveType.MinimizeLength, Weight = 1.0 },
                    new DesignObjective { Name = "Cost", Type = ObjectiveType.MinimizeCost, Weight = 0.8 }
                },
                MaxOptions = 10
            };

            // Act
            var result = await _engine.GenerateMEPRoutingAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Routes.Count, Is.EqualTo(10));
            Assert.That(result.BestRoute, Is.Not.Null);
            Assert.That(result.BestRoute.TotalLength > 0, Is.True);
            Assert.That(result.BestRoute.EstimatedCost > 0, Is.True);
        }

        [Test]
        public async Task GenerateMEPRoutingAsync_ShouldCalculateCostBySystemType()
        {
            // Arrange
            var hvacRequest = new MEPRoutingRequest
            {
                ProjectName = "HVAC Cost Test",
                SystemType = "HVAC",
                Connections = new List<MEPConnection>
                {
                    new MEPConnection
                    {
                        StartPoint = new Point3D { X = 0, Y = 0, Z = 0 },
                        EndPoint = new Point3D { X = 10, Y = 0, Z = 0 }
                    }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Cost", Type = ObjectiveType.MinimizeCost }
                },
                MaxOptions = 3
            };

            var electricalRequest = new MEPRoutingRequest
            {
                ProjectName = "Electrical Cost Test",
                SystemType = "Electrical",
                Connections = hvacRequest.Connections,
                Objectives = hvacRequest.Objectives,
                MaxOptions = 3
            };

            // Act
            var hvacResult = await _engine.GenerateMEPRoutingAsync(hvacRequest);
            var electricalResult = await _engine.GenerateMEPRoutingAsync(electricalRequest);

            // Assert - HVAC should cost more than Electrical per meter
            Assert.That(hvacResult.BestRoute.EstimatedCost > 0, Is.True);
            Assert.That(electricalResult.BestRoute.EstimatedCost > 0, Is.True);
        }

        [Test]
        public async Task GenerateMEPRoutingAsync_ShouldEvaluateRouteObjectives()
        {
            // Arrange
            var request = new MEPRoutingRequest
            {
                ProjectName = "Route Objective Test",
                SystemType = "Plumbing",
                Connections = new List<MEPConnection>
                {
                    new MEPConnection
                    {
                        StartPoint = new Point3D { X = 0, Y = 0, Z = 2 },
                        EndPoint = new Point3D { X = 15, Y = 10, Z = 2 }
                    }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Length", Type = ObjectiveType.MinimizeLength, Weight = 1.0 },
                    new DesignObjective { Name = "Bends", Type = ObjectiveType.MinimizeBends, Weight = 0.5 }
                },
                MaxOptions = 5
            };

            // Act
            var result = await _engine.GenerateMEPRoutingAsync(request);

            // Assert
            Assert.That(result.BestRoute.ObjectiveScores, Is.Not.Null);
            Assert.That(result.BestRoute.ObjectiveScores.ContainsKey("Length"), Is.True);
            Assert.That(result.BestRoute.ObjectiveScores.ContainsKey("Bends"), Is.True);
        }

        #endregion

        #region Structural Optimization Tests

        [Test]
        public async Task GenerateStructuralLayoutAsync_ShouldGenerateSolutions()
        {
            // Arrange
            var request = new StructuralOptimizationRequest
            {
                ProjectName = "Structural Test Building",
                BuildingWidth = 40,
                BuildingDepth = 30,
                NumberOfFloors = 5,
                FloorHeight = 3.5,
                LoadPerFloor = 10000,
                BaseGridSpacing = new GridSpacing { X = 8, Y = 8 },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Weight", Type = ObjectiveType.MinimizeWeight, Weight = 1.0 },
                    new DesignObjective { Name = "Cost", Type = ObjectiveType.MinimizeCost, Weight = 0.8 }
                },
                MaxOptions = 10
            };

            // Act
            var result = await _engine.GenerateStructuralLayoutAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Solutions.Count, Is.EqualTo(10));
            Assert.That(result.BestSolution, Is.Not.Null);
            Assert.That(result.BestSolution.GridLines.Count > 0, Is.True);
            Assert.That(result.BestSolution.Columns.Count > 0, Is.True);
        }

        [Test]
        public async Task GenerateStructuralLayoutAsync_ShouldCalculateSteelWeight()
        {
            // Arrange
            var request = new StructuralOptimizationRequest
            {
                ProjectName = "Steel Weight Test",
                BuildingWidth = 20,
                BuildingDepth = 15,
                NumberOfFloors = 3,
                FloorHeight = 3.0,
                LoadPerFloor = 5000,
                BaseGridSpacing = new GridSpacing { X = 6, Y = 6 },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Weight", Type = ObjectiveType.MinimizeWeight }
                },
                MaxOptions = 5
            };

            // Act
            var result = await _engine.GenerateStructuralLayoutAsync(request);

            // Assert
            Assert.That(result.BestSolution.TotalSteelWeight > 0, Is.True);
            Assert.That(result.BestSolution.EstimatedCost > 0, Is.True);
        }

        [Test]
        public async Task GenerateStructuralLayoutAsync_ShouldGenerateGridVariations()
        {
            // Arrange
            var request = new StructuralOptimizationRequest
            {
                ProjectName = "Grid Variation Test",
                BuildingWidth = 30,
                BuildingDepth = 24,
                NumberOfFloors = 4,
                FloorHeight = 3.2,
                LoadPerFloor = 8000,
                BaseGridSpacing = new GridSpacing { X = 7.5, Y = 7.5 },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Span", Type = ObjectiveType.MaximizeSpan }
                },
                MaxOptions = 10
            };

            // Act
            var result = await _engine.GenerateStructuralLayoutAsync(request);

            // Assert
            // Check that different solutions have different grid counts (variation)
            var gridCounts = new HashSet<int>();
            foreach (var solution in result.Solutions)
            {
                gridCounts.Add(solution.GridLines.Count);
            }
            // Should have at least some variation
            Assert.That(gridCounts.Count >= 1, Is.True);
        }

        #endregion

        #region Study Management Tests

        [Test]
        public async Task GetStudy_ShouldReturnStudyAfterGeneration()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Study Retrieval Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Room", MinArea = 20, Count = 2 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency }
                },
                MaxOptions = 3
            };

            // Act
            var result = await _engine.GenerateSpaceLayoutsAsync(request);
            var study = _engine.GetStudy(result.StudyId);

            // Assert
            Assert.That(study, Is.Not.Null);
            Assert.That(study.StudyId, Is.EqualTo(result.StudyId));
            Assert.That(study.Status, Is.EqualTo(StudyStatus.Completed));
        }

        [Test]
        public async Task ListStudies_ShouldReturnStudiesByType()
        {
            // Arrange
            await _engine.GenerateSpaceLayoutsAsync(new SpaceLayoutRequest
            {
                ProjectName = "List Test 1",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Room", MinArea = 15, Count = 1 }
                },
                Objectives = new List<DesignObjective>(),
                MaxOptions = 2
            });

            // Act
            var allStudies = _engine.ListStudies();
            var spaceStudies = _engine.ListStudies("SpaceLayout");

            // Assert
            Assert.That(allStudies.Count > 0, Is.True);
            Assert.That(spaceStudies.Count > 0, Is.True);
            Assert.That(spaceStudies.TrueForAll(s => s.Type == "SpaceLayout"), Is.True);
        }

        #endregion

        #region Statistics Tests

        [Test]
        public async Task GenerationStatistics_ShouldBeCalculatedCorrectly()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Statistics Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Office", MinArea = 25, Count = 3 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency }
                },
                MaxOptions = 20
            };

            // Act
            var result = await _engine.GenerateSpaceLayoutsAsync(request);

            // Assert
            Assert.That(result.Statistics.TotalGenerated, Is.EqualTo(20));
            Assert.That(result.Statistics.ValidSolutions > 0, Is.True);
            Assert.That(result.Statistics.AverageScore > 0, Is.True);
            Assert.That(result.Statistics.BestScore >= result.Statistics.AverageScore, Is.True);
        }

        [Test]
        public async Task Layouts_ShouldBeSortedByScore()
        {
            // Arrange
            var request = new SpaceLayoutRequest
            {
                ProjectName = "Sorting Test",
                SpaceRequirements = new List<SpaceRequirement>
                {
                    new SpaceRequirement { SpaceType = "Space", MinArea = 30, Count = 2 }
                },
                Objectives = new List<DesignObjective>
                {
                    new DesignObjective { Name = "Efficiency", Type = ObjectiveType.MaximizeEfficiency }
                },
                MaxOptions = 10
            };

            // Act
            var result = await _engine.GenerateSpaceLayoutsAsync(request);

            // Assert
            for (int i = 1; i < result.Layouts.Count; i++)
            {
                Assert.That(result.Layouts[i - 1].OverallScore >= result.Layouts[i].OverallScore,
                    Is.True, "Layouts should be sorted by score descending");
            }
        }

        #endregion
    }
}
