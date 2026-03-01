using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.AI.Reasoning.Spatial;

namespace StingBIM.AI.Tests.Reasoning
{
    /// <summary>
    /// Unit tests for SpatialReasoner class.
    /// Tests spatial queries, collision detection, adjacency analysis, and layout optimization.
    /// </summary>
    [TestFixture]
    public class SpatialReasonerTests
    {
        private SpatialReasoner _reasoner;

        [SetUp]
        public void Setup()
        {
            _reasoner = new SpatialReasoner();
        }

        #region Entity Management Tests

        [Test]
        public void AddEntity_ValidEntity_ShouldBeRetrievable()
        {
            // Arrange
            var entity = CreateRoom("room-1", "Bedroom", 0, 0, 4, 5);

            // Act
            _reasoner.AddEntity(entity);
            var entities = _reasoner.GetEntities().ToList();

            // Assert
            entities.Should().HaveCount(1);
            entities[0].Id.Should().Be("room-1");
        }

        [Test]
        public void GetEntities_ByType_ShouldFilter()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Bedroom", 0, 0, 4, 5));
            _reasoner.AddEntity(CreateRoom("room-2", "Kitchen", 5, 0, 4, 4));
            _reasoner.AddEntity(CreateEntity("wall-1", "Wall", 0, 0, 0.2, 5));

            // Act
            var rooms = _reasoner.GetEntities("Bedroom").ToList();

            // Assert
            rooms.Should().HaveCount(1);
            rooms[0].Id.Should().Be("room-1");
        }

        [Test]
        public void RemoveEntity_ShouldRemoveFromReasoner()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Bedroom", 0, 0, 4, 5));
            _reasoner.AddEntity(CreateRoom("room-2", "Kitchen", 5, 0, 4, 4));

            // Act
            _reasoner.RemoveEntity("room-1");
            var entities = _reasoner.GetEntities().ToList();

            // Assert
            entities.Should().HaveCount(1);
            entities[0].Id.Should().Be("room-2");
        }

        [Test]
        public void RemoveEntity_NonExisting_ShouldNotThrow()
        {
            // Act
            Action act = () => _reasoner.RemoveEntity("nonexistent");

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Spatial Query Tests

        [Test]
        public void FindNearby_WithinRadius_ShouldReturn()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 5, 0, 4, 4)); // 1m away
            _reasoner.AddEntity(CreateRoom("room-3", "Room", 20, 0, 4, 4)); // 16m away

            // Act
            var nearby = _reasoner.FindNearby(new Point3D { X = 0, Y = 0, Z = 0 }, 10).ToList();

            // Assert
            nearby.Should().HaveCount(2); // room-1 and room-2
            nearby.Should().Contain(e => e.Id == "room-1");
            nearby.Should().Contain(e => e.Id == "room-2");
            nearby.Should().NotContain(e => e.Id == "room-3");
        }

        [Test]
        public void FindInBounds_EntitiesInBounds_ShouldReturn()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 5, 5, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-3", "Room", 20, 20, 4, 4));

            var searchBounds = new BoundingBox
            {
                Min = new Point3D { X = -1, Y = -1, Z = 0 },
                Max = new Point3D { X = 10, Y = 10, Z = 5 }
            };

            // Act
            var found = _reasoner.FindInBounds(searchBounds).ToList();

            // Assert
            found.Should().HaveCount(2);
            found.Should().NotContain(e => e.Id == "room-3");
        }

        #endregion

        #region Collision Detection Tests

        [Test]
        public void CheckCollisions_NoCollision_ShouldReturnFalse()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));

            var proposedBounds = new BoundingBox
            {
                Min = new Point3D { X = 10, Y = 0, Z = 0 },
                Max = new Point3D { X = 14, Y = 4, Z = 2.7 }
            };

            // Act
            var result = _reasoner.CheckCollisions(proposedBounds);

            // Assert
            result.HasCollision.Should().BeFalse();
            result.CollidingEntities.Should().BeEmpty();
        }

        [Test]
        public void CheckCollisions_WithCollision_ShouldReturnTrue()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));

            var proposedBounds = new BoundingBox
            {
                Min = new Point3D { X = 2, Y = 0, Z = 0 },
                Max = new Point3D { X = 6, Y = 4, Z = 2.7 }
            };

            // Act
            var result = _reasoner.CheckCollisions(proposedBounds);

            // Assert
            result.HasCollision.Should().BeTrue();
            result.CollidingEntities.Should().HaveCount(1);
            result.CollidingEntities[0].Id.Should().Be("room-1");
        }

        [Test]
        public void CheckCollisions_ExcludeEntity_ShouldNotCollideWithSelf()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));

            var sameBounds = new BoundingBox
            {
                Min = new Point3D { X = 0, Y = 0, Z = 0 },
                Max = new Point3D { X = 4, Y = 4, Z = 2.7 }
            };

            // Act
            var result = _reasoner.CheckCollisions(sameBounds, excludeEntityId: "room-1");

            // Assert
            result.HasCollision.Should().BeFalse();
        }

        [Test]
        public void CheckCollisions_MultipleCollisions_ShouldReturnAll()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 5, 0, 4, 4));

            var proposedBounds = new BoundingBox
            {
                Min = new Point3D { X = 2, Y = 0, Z = 0 },
                Max = new Point3D { X = 7, Y = 4, Z = 2.7 }
            };

            // Act
            var result = _reasoner.CheckCollisions(proposedBounds);

            // Assert
            result.HasCollision.Should().BeTrue();
            result.CollidingEntities.Should().HaveCount(2);
        }

        #endregion

        #region Relationship Tests

        [Test]
        public void GetRelationship_AdjacentRooms_ShouldReturnAdjacent()
        {
            // Arrange - Two rooms sharing a wall (adjacent)
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 4.1, 0, 4, 4)); // 0.1m gap

            // Act
            var relationship = _reasoner.GetRelationship("room-1", "room-2");

            // Assert
            relationship.Should().NotBeNull();
            relationship.Type.Should().Be(RelationshipType.Adjacent);
        }

        [Test]
        public void GetRelationship_NearRooms_ShouldReturnNear()
        {
            // Arrange - Two rooms 2m apart
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 6, 0, 4, 4)); // 2m gap

            // Act
            var relationship = _reasoner.GetRelationship("room-1", "room-2");

            // Assert
            relationship.Should().NotBeNull();
            relationship.Type.Should().Be(RelationshipType.Near);
        }

        [Test]
        public void GetRelationship_DistantRooms_ShouldReturnDistant()
        {
            // Arrange - Two rooms far apart
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 50, 0, 4, 4));

            // Act
            var relationship = _reasoner.GetRelationship("room-1", "room-2");

            // Assert
            relationship.Should().NotBeNull();
            relationship.Type.Should().Be(RelationshipType.Distant);
        }

        [Test]
        public void AreAdjacent_AdjacentRooms_ShouldReturnTrue()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 4.2, 0, 4, 4));

            // Act
            var result = _reasoner.AreAdjacent("room-1", "room-2");

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void AreAdjacent_DistantRooms_ShouldReturnFalse()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 20, 0, 4, 4));

            // Act
            var result = _reasoner.AreAdjacent("room-1", "room-2");

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void FindAdjacent_ShouldReturnAllAdjacent()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("center", "Room", 5, 5, 4, 4));
            _reasoner.AddEntity(CreateRoom("north", "Room", 5, 9.1, 4, 4)); // Adjacent
            _reasoner.AddEntity(CreateRoom("east", "Room", 9.1, 5, 4, 4)); // Adjacent
            _reasoner.AddEntity(CreateRoom("far", "Room", 20, 20, 4, 4)); // Not adjacent

            // Act
            var adjacent = _reasoner.FindAdjacent("center").ToList();

            // Assert
            adjacent.Should().HaveCount(2);
            adjacent.Should().Contain(e => e.Id == "north");
            adjacent.Should().Contain(e => e.Id == "east");
        }

        #endregion

        #region Spatial Quality Analysis Tests

        [Test]
        public void AnalyzeSpatialQuality_SquareRoom_ShouldHaveGoodProportions()
        {
            // Arrange - 4x4m room with 2.7m ceiling
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4, 2.7));

            // Act
            var analysis = _reasoner.AnalyzeSpatialQuality("room-1");

            // Assert
            analysis.Area.Should().BeApproximately(16, 0.1);
            analysis.AspectRatio.Should().Be(1);
            analysis.CeilingHeight.Should().Be(2.7);
            analysis.HeightScore.Should().Be(1); // 2.7m is ideal
        }

        [Test]
        public void AnalyzeSpatialQuality_ElongatedRoom_ShouldRecommendSubdivision()
        {
            // Arrange - Very elongated room (3x10m)
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 3, 10, 2.7));

            // Act
            var analysis = _reasoner.AnalyzeSpatialQuality("room-1");

            // Assert
            analysis.AspectRatio.Should().BeGreaterThan(2.5);
            analysis.Recommendations.Should().Contain(r => r.Contains("elongated"));
        }

        [Test]
        public void AnalyzeSpatialQuality_LowCeiling_ShouldRecommendRaising()
        {
            // Arrange - Room with low ceiling
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4, 2.2));

            // Act
            var analysis = _reasoner.AnalyzeSpatialQuality("room-1");

            // Assert
            analysis.CeilingHeight.Should().Be(2.2);
            analysis.Recommendations.Should().Contain(r => r.Contains("low") || r.Contains("ceiling"));
        }

        [Test]
        public void AnalyzeSpatialQuality_NonExistingRoom_ShouldReturnEmptyAnalysis()
        {
            // Act
            var analysis = _reasoner.AnalyzeSpatialQuality("nonexistent");

            // Assert
            analysis.Area.Should().Be(0);
            analysis.Volume.Should().Be(0);
        }

        #endregion

        #region Adjacency Analysis Tests

        [Test]
        public void AnalyzeAdjacencies_SatisfiedRequirements_ShouldReportSuccess()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("kitchen", "Kitchen", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("dining", "Dining", 4.1, 0, 4, 4)); // Adjacent

            var requirements = new AdjacencyRequirements
            {
                MustBeAdjacent = new List<string> { "Dining" }
            };

            // Act
            var analysis = _reasoner.AnalyzeAdjacencies("kitchen", requirements);

            // Assert
            analysis.SatisfiedRequirements.Should().Contain("Dining");
            analysis.UnmetRequirements.Should().BeEmpty();
            analysis.Score.Should().Be(1);
        }

        [Test]
        public void AnalyzeAdjacencies_UnmetRequirements_ShouldReportFailure()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("kitchen", "Kitchen", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("dining", "Dining", 20, 0, 4, 4)); // Not adjacent

            var requirements = new AdjacencyRequirements
            {
                MustBeAdjacent = new List<string> { "Dining" }
            };

            // Act
            var analysis = _reasoner.AnalyzeAdjacencies("kitchen", requirements);

            // Assert
            analysis.UnmetRequirements.Should().Contain("Dining");
            analysis.Score.Should().Be(0);
        }

        [Test]
        public void AnalyzeAdjacencies_ViolatedProhibition_ShouldReportViolation()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("kitchen", "Kitchen", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("bedroom", "Bedroom", 4.1, 0, 4, 4)); // Adjacent but prohibited

            var requirements = new AdjacencyRequirements
            {
                MustNotBeAdjacent = new List<string> { "Bedroom" }
            };

            // Act
            var analysis = _reasoner.AnalyzeAdjacencies("kitchen", requirements);

            // Assert
            analysis.Violations.Should().HaveCount(1);
            analysis.Violations[0].Should().Contain("Bedroom");
        }

        #endregion

        #region Circulation Analysis Tests

        [Test]
        public void AnalyzeCirculation_DirectlyConnected_ShouldBeReachable()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 4.1, 0, 4, 4));

            // Act
            var analysis = _reasoner.AnalyzeCirculation("room-1", "room-2");

            // Assert
            analysis.IsReachable.Should().BeTrue();
            analysis.DirectDistance.Should().BeGreaterThan(0);
        }

        [Test]
        public void AnalyzeCirculation_NonExistingEntity_ShouldNotBeReachable()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));

            // Act
            var analysis = _reasoner.AnalyzeCirculation("room-1", "nonexistent");

            // Assert
            analysis.IsReachable.Should().BeFalse();
            analysis.Reason.Should().Contain("not found");
        }

        #endregion

        #region Placement Suggestion Tests

        [Test]
        public void SuggestPlacement_NoCollisions_ShouldSuggestPosition()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("existing", "Room", 0, 0, 4, 4));

            // Act
            var suggestion = _reasoner.SuggestPlacement("Bedroom", 4, 5);

            // Assert
            suggestion.Position.Should().NotBeNull();
            suggestion.Score.Should().BeGreaterThan(0);
        }

        [Test]
        public void SuggestPlacement_WithAdjacencyRequirement_ShouldConsiderRequirement()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("kitchen", "Kitchen", 0, 0, 4, 4));

            var requirements = new AdjacencyRequirements
            {
                MustBeAdjacent = new List<string> { "Kitchen" }
            };

            // Act
            var suggestion = _reasoner.SuggestPlacement("Dining", 4, 4, requirements);

            // Assert
            suggestion.Position.Should().NotBeNull();
            // Position should be near kitchen
            var distance = Math.Sqrt(
                Math.Pow(suggestion.Position.X - 2, 2) +
                Math.Pow(suggestion.Position.Y - 2, 2));
            distance.Should().BeLessThan(10); // Reasonably close
        }

        [Test]
        public void SuggestPlacement_ShouldReturnConfidenceLevel()
        {
            // Act
            var suggestion = _reasoner.SuggestPlacement("Room", 4, 4);

            // Assert
            suggestion.Confidence.Should().BeOneOf(Confidence.Low, Confidence.Medium, Confidence.High);
        }

        #endregion

        #region Layout Optimization Tests

        [Test]
        public void OptimizeLayout_ShouldReturnOriginalScore()
        {
            // Arrange
            _reasoner.AddEntity(CreateRoom("room-1", "Room", 0, 0, 4, 4));
            _reasoner.AddEntity(CreateRoom("room-2", "Room", 4.1, 0, 4, 4));

            var parameters = new LayoutOptimizationParams
            {
                AdjacencyMatrix = new Dictionary<string, List<string>>()
            };

            // Act
            var result = _reasoner.OptimizeLayout(parameters);

            // Assert
            result.OriginalScore.Should().BeGreaterThan(0);
            result.PotentialScore.Should().BeGreaterOrEqualTo(result.OriginalScore);
        }

        [Test]
        public void OptimizeLayout_WithNarrowCorridor_ShouldSuggestWidening()
        {
            // Arrange
            _reasoner.AddEntity(CreateEntity("corridor-1", "Corridor", 0, 0, 1.0, 10)); // Narrow corridor

            var parameters = new LayoutOptimizationParams();

            // Act
            var result = _reasoner.OptimizeLayout(parameters);

            // Assert
            result.Suggestions.Should().Contain(s =>
                s.Type == SuggestionType.CirculationImprovement);
        }

        #endregion

        #region Helper Methods

        private SpatialEntity CreateRoom(string id, string type, double x, double y, double width, double length, double height = 2.7)
        {
            return new SpatialEntity
            {
                Id = id,
                Name = type,
                EntityType = type,
                BoundingBox = new BoundingBox
                {
                    Min = new Point3D { X = x, Y = y, Z = 0 },
                    Max = new Point3D { X = x + width, Y = y + length, Z = height }
                }
            };
        }

        private SpatialEntity CreateEntity(string id, string type, double x, double y, double width, double length)
        {
            return new SpatialEntity
            {
                Id = id,
                Name = type,
                EntityType = type,
                BoundingBox = new BoundingBox
                {
                    Min = new Point3D { X = x, Y = y, Z = 0 },
                    Max = new Point3D { X = x + width, Y = y + length, Z = 2.7 }
                }
            };
        }

        #endregion
    }
}
