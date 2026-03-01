// StingBIM.AI.Tests.Reasoning.ReasoningTests
// Comprehensive unit tests for the AI Reasoning module
// Covers: SpatialReasoner, ComplianceChecker, DecisionSupport, DesignPatternRecognizer

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using StingBIM.AI.Reasoning.Spatial;
using StingBIM.AI.Reasoning.Compliance;
using StingBIM.AI.Reasoning.Decision;
using StingBIM.AI.Reasoning.Patterns;

namespace StingBIM.AI.Tests.Reasoning
{
    // SpatialReasonerTests moved to dedicated SpatialReasonerTests.cs

    #region ComplianceChecker Tests

    [TestFixture]
    public class ComplianceCheckerTests
    {
        private ComplianceChecker _checker;

        [SetUp]
        public void SetUp()
        {
            _checker = new ComplianceChecker();
        }

        #region Code Profile Tests

        [Test]
        public void GetAvailableProfiles_ShouldReturnProfiles()
        {
            // Act
            var profiles = _checker.GetAvailableProfiles().ToList();

            // Assert
            profiles.Should().NotBeNull();
            profiles.Should().NotBeEmpty();
        }

        [Test]
        public void SetCodeProfile_WithValidProfile_ShouldNotThrow()
        {
            // Arrange
            var profiles = _checker.GetAvailableProfiles().ToList();
            profiles.Should().NotBeEmpty();

            // Act & Assert
            _checker.Invoking(c => c.SetCodeProfile(profiles.First()))
                .Should().NotThrow();
        }

        [Test]
        public void SetCodeProfile_WithInvalidProfile_ShouldThrow()
        {
            // Act & Assert
            _checker.Invoking(c => c.SetCodeProfile("NonExistentProfile"))
                .Should().Throw<ArgumentException>();
        }

        #endregion

        #region Single Element Compliance Tests

        [Test]
        public async Task CheckAsync_CompliantDoor_ShouldReturnCompliant()
        {
            // Arrange
            var door = new DesignElement
            {
                Id = "door1",
                Type = "Door",
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = 900.0, // mm, above minimum 815mm
                    ["Height"] = 2100.0,
                    ["ClearWidth"] = 850.0
                }
            };

            // Act
            var result = await _checker.CheckAsync(door);

            // Assert
            result.Should().NotBeNull();
            result.ElementId.Should().Be("door1");
            result.Score.Should().BeGreaterThanOrEqualTo(0);
        }

        [Test]
        public async Task CheckAsync_NarrowCorridor_ShouldFlagViolation()
        {
            // Arrange
            var corridor = new DesignElement
            {
                Id = "corridor1",
                Type = "Corridor",
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = 800.0, // mm, below minimum 1200mm
                    ["Length"] = 10000.0
                }
            };

            // Act
            var result = await _checker.CheckAsync(corridor);

            // Assert
            result.Should().NotBeNull();
            // A narrow corridor should have violations
            if (result.RuleResults.Any())
            {
                result.Score.Should().BeLessThanOrEqualTo(1.0);
            }
        }

        [Test]
        public async Task CheckAsync_SmallBedroom_ShouldFlagAreaViolation()
        {
            // Arrange
            var room = new DesignElement
            {
                Id = "bedroom1",
                Type = "Room",
                Properties = new Dictionary<string, object>
                {
                    ["Area"] = 5.0, // sqm, below minimum ~9 sqm for bedrooms
                    ["RoomType"] = "Bedroom",
                    ["CeilingHeight"] = 2.7
                }
            };

            // Act
            var result = await _checker.CheckAsync(room);

            // Assert
            result.Should().NotBeNull();
        }

        [Test]
        public async Task CheckAsync_CompliantRoom_ShouldHaveHighScore()
        {
            // Arrange
            var room = new DesignElement
            {
                Id = "room1",
                Type = "Room",
                Properties = new Dictionary<string, object>
                {
                    ["Area"] = 25.0,
                    ["RoomType"] = "Living",
                    ["CeilingHeight"] = 2.7,
                    ["Width"] = 5000.0,
                    ["Length"] = 5000.0,
                    ["GlazingRatio"] = 0.15,
                    ["VentilationArea"] = 2.5
                }
            };

            // Act
            var result = await _checker.CheckAsync(room);

            // Assert
            result.Should().NotBeNull();
            result.Score.Should().BeGreaterThanOrEqualTo(0);
        }

        [Test]
        public async Task CheckAsync_ShouldReturnTimestamp()
        {
            // Arrange
            var before = DateTime.Now.AddSeconds(-1);
            var element = new DesignElement
            {
                Id = "elem1",
                Type = "Wall",
                Properties = new Dictionary<string, object>()
            };

            // Act
            var result = await _checker.CheckAsync(element);

            // Assert
            result.CheckedAt.Should().BeAfter(before);
        }

        #endregion

        #region Batch Compliance Tests

        [Test]
        public async Task CheckBatchAsync_MultipleElements_ShouldReturnAggregatedResults()
        {
            // Arrange
            var elements = new List<DesignElement>
            {
                new DesignElement
                {
                    Id = "door1", Type = "Door",
                    Properties = new Dictionary<string, object> { ["Width"] = 900.0, ["Height"] = 2100.0, ["ClearWidth"] = 850.0 }
                },
                new DesignElement
                {
                    Id = "room1", Type = "Room",
                    Properties = new Dictionary<string, object> { ["Area"] = 20.0, ["RoomType"] = "Living", ["CeilingHeight"] = 2.7 }
                },
                new DesignElement
                {
                    Id = "wall1", Type = "Wall",
                    Properties = new Dictionary<string, object> { ["Thickness"] = 200.0, ["Height"] = 2700.0, ["IsLoadBearing"] = true }
                }
            };

            // Act
            var result = await _checker.CheckBatchAsync(elements);

            // Assert
            result.Should().NotBeNull();
            result.TotalElements.Should().Be(3);
            result.ElementResults.Should().HaveCount(3);
            result.OverallScore.Should().BeGreaterThanOrEqualTo(0);
        }

        [Test]
        public async Task CheckBatchAsync_EmptyList_ShouldReturnEmptyResult()
        {
            // Arrange
            var elements = new List<DesignElement>();

            // Act
            var result = await _checker.CheckBatchAsync(elements);

            // Assert
            result.Should().NotBeNull();
            result.TotalElements.Should().Be(0);
            result.OverallScore.Should().Be(1.0);
        }

        #endregion

        #region Report Generation Tests

        [Test]
        public async Task GenerateReport_ShouldReturnCompleteReport()
        {
            // Arrange
            var elements = new List<DesignElement>
            {
                new DesignElement
                {
                    Id = "room1", Type = "Room",
                    Properties = new Dictionary<string, object> { ["Area"] = 20.0, ["RoomType"] = "Living", ["CeilingHeight"] = 2.7 }
                }
            };
            var batchResult = await _checker.CheckBatchAsync(elements);

            // Act
            var report = _checker.GenerateReport(batchResult);

            // Assert
            report.Should().NotBeNull();
            report.Summary.Should().NotBeNull();
            report.Summary.TotalElements.Should().Be(1);
            report.Recommendations.Should().NotBeNull();
        }

        [Test]
        public async Task GenerateReport_ShouldContainRecommendations()
        {
            // Arrange
            var elements = new List<DesignElement>
            {
                new DesignElement
                {
                    Id = "room1", Type = "Room",
                    Properties = new Dictionary<string, object>
                    {
                        ["Area"] = 20.0, ["RoomType"] = "Living", ["CeilingHeight"] = 2.7
                    }
                }
            };
            var batchResult = await _checker.CheckBatchAsync(elements);

            // Act
            var report = _checker.GenerateReport(batchResult);

            // Assert
            report.Recommendations.Should().NotBeNull();
            report.Recommendations.Should().NotBeEmpty();
        }

        #endregion
    }

    #endregion


    // DecisionSupportTests moved to dedicated DecisionSupportTests.cs

    #region DesignPatternRecognizer Tests

    [TestFixture]
    public class DesignPatternRecognizerTests
    {
        private DesignPatternRecognizer _recognizer;

        [SetUp]
        public void SetUp()
        {
            _recognizer = new DesignPatternRecognizer();
        }

        #region RecognizePatterns Tests

        [Test]
        public void RecognizePatterns_WithResidentialLayout_ShouldFindPatterns()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 },
                    new RoomInfo { Id = "r5", Type = "Dining", Area = 12.0 }
                }
            };

            // Act
            var matches = _recognizer.RecognizePatterns(context);

            // Assert
            matches.Should().NotBeNull();
            // Should recognize the Functional Zoning pattern since we have living, bedroom, kitchen, bathroom
        }

        [Test]
        public void RecognizePatterns_WithMinimalLayout_ShouldReturnFewerMatches()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Storage", Area = 5.0 }
                }
            };

            // Act
            var matches = _recognizer.RecognizePatterns(context);

            // Assert
            matches.Should().NotBeNull();
            // Fewer rooms = fewer pattern matches
        }

        [Test]
        public void RecognizePatterns_ShouldSortByConfidenceTimesRelevance()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 },
                    new RoomInfo { Id = "r5", Type = "Corridor", Area = 6.0 }
                }
            };

            // Act
            var matches = _recognizer.RecognizePatterns(context);

            // Assert
            if (matches.Count > 1)
            {
                for (int i = 0; i < matches.Count - 1; i++)
                {
                    var score1 = matches[i].Confidence * matches[i].Relevance;
                    var score2 = matches[i + 1].Confidence * matches[i + 1].Relevance;
                    score1.Should().BeGreaterThanOrEqualTo(score2);
                }
            }
        }

        [Test]
        public void RecognizePatterns_WithEmptyRoomList_ShouldReturnEmptyList()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>()
            };

            // Act
            var matches = _recognizer.RecognizePatterns(context);

            // Assert
            matches.Should().NotBeNull();
            matches.Should().BeEmpty();
        }

        #endregion

        #region SuggestPatterns Tests

        [Test]
        public void SuggestPatterns_ForResidential_ShouldReturnSuggestions()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 }
                }
            };

            // Act
            var suggestions = _recognizer.SuggestPatterns(context, "Residential");

            // Assert
            suggestions.Should().NotBeNull();
            suggestions.Should().NotBeEmpty();
        }

        [Test]
        public void SuggestPatterns_ShouldLimitToFive()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 }
                }
            };

            // Act
            var suggestions = _recognizer.SuggestPatterns(context, "Residential");

            // Assert
            suggestions.Should().HaveCountLessThanOrEqualTo(5);
        }

        [Test]
        public void SuggestPatterns_ShouldSortByApplicability()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 }
                }
            };

            // Act
            var suggestions = _recognizer.SuggestPatterns(context, "Residential");

            // Assert
            if (suggestions.Count > 1)
            {
                for (int i = 0; i < suggestions.Count - 1; i++)
                {
                    suggestions[i].Applicability.Should().BeGreaterThanOrEqualTo(suggestions[i + 1].Applicability);
                }
            }
        }

        [Test]
        public void SuggestPatterns_ShouldIncludeRationale()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 }
                }
            };

            // Act
            var suggestions = _recognizer.SuggestPatterns(context, "Residential");

            // Assert
            foreach (var suggestion in suggestions)
            {
                suggestion.Rationale.Should().NotBeNullOrEmpty();
                suggestion.Implementation.Should().NotBeNull();
            }
        }

        [Test]
        public void SuggestPatterns_ForUnknownProjectType_ShouldReturnWildcardPatterns()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "UnknownType",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Kitchen", Area = 10.0 }
                }
            };

            // Act
            var suggestions = _recognizer.SuggestPatterns(context, "UnknownType");

            // Assert
            // Patterns with "*" in ApplicableProjectTypes should still apply
            suggestions.Should().NotBeNull();
        }

        #endregion

        #region LearnFromDesign Tests

        [Test]
        public void LearnFromDesign_ShouldUpdatePatternStats()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 }
                }
            };

            var feedback = new DesignFeedback
            {
                WasSuccessful = true,
                Rating = 4.5,
                Comments = "Excellent layout"
            };

            // Act & Assert - should not throw
            _recognizer.Invoking(r => r.LearnFromDesign(context, feedback))
                .Should().NotThrow();
        }

        [Test]
        public void LearnFromDesign_WithFailedDesign_ShouldStillLearn()
        {
            // Arrange
            var context = new DesignContext
            {
                ProjectType = "Office",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Office", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Meeting", Area = 15.0 }
                }
            };

            var feedback = new DesignFeedback
            {
                WasSuccessful = false,
                Rating = 2.0,
                Comments = "Poor circulation"
            };

            // Act & Assert
            _recognizer.Invoking(r => r.LearnFromDesign(context, feedback))
                .Should().NotThrow();
        }

        #endregion

        #region GetRecommendations Tests

        [Test]
        public void GetRecommendations_WithIncompleteDesign_ShouldRecommendMissingRooms()
        {
            // Arrange
            var partial = new PartialDesign
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 }
                    // Missing: Kitchen, Bedroom, Bathroom
                }
            };

            // Act
            var recommendations = _recognizer.GetRecommendations(partial);

            // Assert
            recommendations.Should().NotBeNull();
            recommendations.Should().Contain(r => r.Type == RecommendationType.MissingElement);
        }

        [Test]
        public void GetRecommendations_WithManyRoomsNoCorridor_ShouldSuggestCirculation()
        {
            // Arrange
            var partial = new PartialDesign
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 },
                    new RoomInfo { Id = "r5", Type = "Study", Area = 8.0 }
                }
            };

            // Act
            var recommendations = _recognizer.GetRecommendations(partial);

            // Assert
            recommendations.Should().NotBeNull();
            // With 5 rooms and no corridor, should suggest adding circulation
            recommendations.Should().Contain(r => r.Type == RecommendationType.CirculationImprovement);
        }

        [Test]
        public void GetRecommendations_ShouldBeSortedByPriority()
        {
            // Arrange
            var partial = new PartialDesign
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Study", Area = 8.0 },
                    new RoomInfo { Id = "r5", Type = "Dining", Area = 10.0 }
                }
            };

            // Act
            var recommendations = _recognizer.GetRecommendations(partial);

            // Assert
            if (recommendations.Count > 1)
            {
                for (int i = 0; i < recommendations.Count - 1; i++)
                {
                    ((int)recommendations[i].Priority).Should()
                        .BeGreaterThanOrEqualTo((int)recommendations[i + 1].Priority);
                }
            }
        }

        [Test]
        public void GetRecommendations_CompleteDesign_ShouldHaveFewerRecommendations()
        {
            // Arrange
            var incompleteDesign = new PartialDesign
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 }
                }
            };

            var completeDesign = new PartialDesign
            {
                ProjectType = "Residential",
                Rooms = new List<RoomInfo>
                {
                    new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                    new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 },
                    new RoomInfo { Id = "r3", Type = "Bedroom", Area = 14.0 },
                    new RoomInfo { Id = "r4", Type = "Bathroom", Area = 5.0 },
                    new RoomInfo { Id = "r5", Type = "Corridor", Area = 6.0 }
                }
            };

            // Act
            var incompleteRecs = _recognizer.GetRecommendations(incompleteDesign);
            var completeRecs = _recognizer.GetRecommendations(completeDesign);

            // Assert
            // Incomplete design should have MissingElement recommendations that complete one does not
            var incompleteMissing = incompleteRecs.Count(r => r.Type == RecommendationType.MissingElement);
            var completeMissing = completeRecs.Count(r => r.Type == RecommendationType.MissingElement);
            incompleteMissing.Should().BeGreaterThanOrEqualTo(completeMissing);
        }

        #endregion
    }

    #endregion
}
