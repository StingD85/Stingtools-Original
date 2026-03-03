// StingBIM.AI.Tests.Agents.MEPAgentTests
// Tests for the MEP (Mechanical, Electrical, Plumbing) specialist agent
// Covers: HVAC evaluation, electrical requirements, plumbing, suggestions, validation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.AI.Agents.Framework;
using StingBIM.AI.Agents.Specialists;

namespace StingBIM.AI.Tests.Agents
{
    [TestFixture]
    public class MEPAgentTests
    {
        private MEPAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _agent = new MEPAgent();
        }

        #region Agent Properties Tests

        [Test]
        public void AgentId_ShouldBeMEP001()
        {
            _agent.AgentId.Should().Be("MEP-001");
        }

        [Test]
        public void Specialty_ShouldBeMEPEngineering()
        {
            _agent.Specialty.Should().Be("MEP Engineering");
        }

        [Test]
        public void ExpertiseLevel_ShouldBeHigh()
        {
            _agent.ExpertiseLevel.Should().BeGreaterOrEqualTo(0.8f);
        }

        [Test]
        public void IsActive_ShouldBeTrue()
        {
            _agent.IsActive.Should().BeTrue();
        }

        #endregion

        #region EvaluateAsync Basic Tests

        [Test]
        public async Task EvaluateAsync_EmptyProposal_ShouldReturnPositiveScore()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST001",
                Description = "Empty proposal"
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Should().NotBeNull();
            opinion.AgentId.Should().Be("MEP-001");
            opinion.Specialty.Should().Be("MEP Engineering");
            opinion.Score.Should().BeGreaterOrEqualTo(0.7f);
        }

        [Test]
        public async Task EvaluateAsync_ShouldReturnOpinionWithTimestamp()
        {
            // Arrange
            var proposal = CreateProposalWithRoom();

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task EvaluateAsync_ShouldGenerateSummary()
        {
            // Arrange
            var proposal = CreateProposalWithRoom();

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Summary.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region HVAC Evaluation Tests

        [Test]
        public async Task EvaluateAsync_RoomWithLowCeiling_ShouldReportDuctRoutingIssue()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST002",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Office" },
                        Geometry = new GeometryInfo
                        {
                            Height = 2.5, // Too low for ducts
                            Width = 5.0,
                            Length = 6.0
                        }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Issues.Should().Contain(i => i.Code == "MEP-HVAC-001");
            opinion.Issues.Should().Contain(i => i.Description.Contains("ceiling void"));
        }

        [Test]
        public async Task EvaluateAsync_RoomWithAdequateCeiling_ShouldNotReportDuctIssue()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST003",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Office" },
                        Geometry = new GeometryInfo
                        {
                            Height = 3.0, // Adequate height
                            Width = 5.0,
                            Length = 6.0
                        }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Issues.Should().NotContain(i => i.Code == "MEP-HVAC-001");
            opinion.Strengths.Should().Contain(s => s.Contains("HVAC"));
        }

        [Test]
        public async Task EvaluateAsync_KitchenWithoutExhaust_ShouldReportVentilationIssue()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST004",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object>
                        {
                            ["RoomType"] = "Kitchen",
                            ["HasExhaust"] = false
                        },
                        Geometry = new GeometryInfo { Height = 3.0, Width = 4.0, Length = 4.0 }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Issues.Should().Contain(i =>
                i.Code == "MEP-HVAC-010" &&
                i.Description.Contains("Kitchen") &&
                i.Description.Contains("exhaust"));
        }

        #endregion

        #region Electrical Evaluation Tests

        [Test]
        public async Task EvaluateAsync_Bathroom_ShouldReportGFCIRequirement()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST005",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Bathroom" },
                        Geometry = new GeometryInfo { Height = 2.7, Width = 3.0, Length = 3.0 }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Issues.Should().Contain(i =>
                i.Code == "MEP-ELEC-010" &&
                i.Description.Contains("GFCI"));
        }

        [Test]
        public async Task EvaluateAsync_WithOfficeContext_ShouldUseOfficeLoa()
        {
            // Arrange
            var proposal = CreateProposalWithRoom();
            var context = new EvaluationContext
            {
                ProjectType = "Office",
                BuildingCode = "IBC 2021"
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal, context);

            // Assert
            opinion.Strengths.Should().Contain(s => s.Contains("Electrical load assessment"));
        }

        #endregion

        #region Plumbing Evaluation Tests

        [Test]
        public async Task EvaluateAsync_MultipleWetRooms_ShouldRecommendStackGrouping()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST006",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Bathroom" },
                        Geometry = new GeometryInfo { Height = 2.7, Width = 2.5, Length = 3.0 }
                    },
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Kitchen" },
                        Geometry = new GeometryInfo { Height = 3.0, Width = 4.0, Length = 4.0 }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Issues.Should().Contain(i =>
                i.Code == "MEP-PLMB-001" &&
                i.Description.Contains("plumbing stack"));
        }

        #endregion

        #region Service Coordination Tests

        [Test]
        public async Task EvaluateAsync_MultiStoryWithoutRiser_ShouldReportCoordinationIssue()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST007",
                Parameters = new Dictionary<string, object>
                {
                    ["NumberOfFloors"] = 3,
                    ["HasMEPRiser"] = false
                },
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Office" },
                        Geometry = new GeometryInfo { Height = 3.0, Width = 5.0, Length = 5.0 }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Issues.Should().Contain(i =>
                i.Code == "MEP-COORD-001" &&
                i.Description.Contains("vertical MEP risers"));
        }

        #endregion

        #region SuggestAsync Tests

        [Test]
        public async Task SuggestAsync_RoomTask_ShouldSuggestHVACRouting()
        {
            // Arrange
            var context = new DesignContext
            {
                CurrentTask = "Create new room"
            };

            // Act
            var suggestions = (await _agent.SuggestAsync(context)).ToList();

            // Assert
            suggestions.Should().Contain(s => s.Title.Contains("HVAC"));
            suggestions.Should().Contain(s => s.Title.Contains("outlet"));
        }

        [Test]
        public async Task SuggestAsync_KitchenTask_ShouldSuggestKitchenSpecificItems()
        {
            // Arrange
            var context = new DesignContext
            {
                CurrentTask = "Design kitchen layout"
            };

            // Act
            var suggestions = (await _agent.SuggestAsync(context)).ToList();

            // Assert
            suggestions.Should().Contain(s =>
                s.Title.Contains("Kitchen ventilation") &&
                s.Type == SuggestionType.CodeCompliance);
            suggestions.Should().Contain(s =>
                s.Title.Contains("Kitchen electrical") &&
                s.Description.Contains("circuit"));
        }

        [Test]
        public async Task SuggestAsync_BathroomTask_ShouldSuggestBathroomSpecificItems()
        {
            // Arrange
            var context = new DesignContext
            {
                CurrentTask = "Design bathroom"
            };

            // Act
            var suggestions = (await _agent.SuggestAsync(context)).ToList();

            // Assert
            suggestions.Should().Contain(s => s.Title.Contains("Bathroom ventilation"));
            suggestions.Should().Contain(s =>
                s.Title.Contains("GFCI") &&
                s.Confidence > 0.9f);
            suggestions.Should().Contain(s =>
                s.Title.Contains("Plumbing stack") &&
                s.Type == SuggestionType.CostSaving);
        }

        [Test]
        public async Task SuggestAsync_ToiletTask_ShouldTriggerBathroomSuggestions()
        {
            // Arrange
            var context = new DesignContext
            {
                CurrentTask = "Add toilet room"
            };

            // Act
            var suggestions = (await _agent.SuggestAsync(context)).ToList();

            // Assert
            suggestions.Should().Contain(s => s.Title.Contains("GFCI"));
        }

        [Test]
        public async Task SuggestAsync_ShouldAlwaysIncludeMEPCoordinationZone()
        {
            // Arrange
            var context = new DesignContext
            {
                CurrentTask = "General design"
            };

            // Act
            var suggestions = (await _agent.SuggestAsync(context)).ToList();

            // Assert
            suggestions.Should().Contain(s =>
                s.Title.Contains("MEP coordination zone") &&
                s.Type == SuggestionType.BestPractice);
        }

        [Test]
        public async Task SuggestAsync_ShouldProvideConfidenceAndImpactScores()
        {
            // Arrange
            var context = new DesignContext { CurrentTask = "Design bathroom" };

            // Act
            var suggestions = (await _agent.SuggestAsync(context)).ToList();

            // Assert
            suggestions.Should().OnlyContain(s =>
                s.Confidence > 0 && s.Confidence <= 1.0f &&
                s.Impact > 0 && s.Impact <= 1.0f);
        }

        #endregion

        #region ValidateAction Tests

        [Test]
        public void ValidateAction_CreateRoomWithLowHeight_ShouldReturnWarning()
        {
            // Arrange
            var action = new DesignAction
            {
                ActionType = "CreateRoom",
                Parameters = new Dictionary<string, object>
                {
                    ["Height"] = 2.5 // Below recommended
                }
            };

            // Act
            var result = _agent.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Contains("duct routing"));
        }

        [Test]
        public void ValidateAction_CreateRoomWithAdequateHeight_ShouldReturnValid()
        {
            // Arrange
            var action = new DesignAction
            {
                ActionType = "CreateRoom",
                Parameters = new Dictionary<string, object>
                {
                    ["Height"] = 3.0
                }
            };

            // Act
            var result = _agent.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().BeEmpty();
        }

        [Test]
        public void ValidateAction_CreateBathroom_ShouldReturnRequirements()
        {
            // Arrange
            var action = new DesignAction
            {
                ActionType = "CreateBathroom",
                Parameters = new Dictionary<string, object>
                {
                    ["RoomType"] = "Bathroom"
                }
            };

            // Act
            var result = _agent.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().Contain(w =>
                w.Contains("GFCI") &&
                w.Contains("exhaust"));
        }

        [Test]
        public void ValidateAction_CreateKitchen_ShouldReturnRequirements()
        {
            // Arrange
            var action = new DesignAction
            {
                ActionType = "CreateKitchen",
                Parameters = new Dictionary<string, object>
                {
                    ["RoomType"] = "Kitchen"
                }
            };

            // Act
            var result = _agent.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().Contain(w =>
                w.Contains("Range hood") &&
                w.Contains("circuit"));
        }

        [Test]
        public void ValidateAction_UnknownActionType_ShouldReturnValid()
        {
            // Arrange
            var action = new DesignAction
            {
                ActionType = "SomeOtherAction",
                Parameters = new Dictionary<string, object>()
            };

            // Act
            var result = _agent.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Feedback Integration Tests

        [Test]
        public async Task EvaluateAsync_AfterReceivingArchitecturalFeedback_ShouldBeRevised()
        {
            // Arrange
            var proposal = CreateProposalWithRoom();

            // First evaluation
            await _agent.EvaluateAsync(proposal);

            // Receive feedback
            var archFeedback = new AgentOpinion
            {
                AgentId = "ARCH-001",
                Specialty = "Architectural Design",
                Score = 0.8f,
                Issues = new List<DesignIssue>
                {
                    new DesignIssue
                    {
                        Code = "ARCH-CEIL-001",
                        Description = "Ceiling height constrained by architectural requirements"
                    }
                }
            };
            _agent.ReceiveFeedback(archFeedback);

            // Act - Second evaluation
            var revisedOpinion = await _agent.EvaluateAsync(proposal);

            // Assert
            revisedOpinion.IsRevised.Should().BeTrue();
            revisedOpinion.Issues.Should().Contain(i =>
                i.Code.Contains("ARCH-CONFLICT"));
        }

        [Test]
        public async Task EvaluateAsync_AfterReceivingStructuralFeedback_ShouldBeRevised()
        {
            // Arrange
            var proposal = CreateProposalWithRoom();

            // Receive structural feedback
            var structFeedback = new AgentOpinion
            {
                AgentId = "STRUCT-001",
                Specialty = "Structural Engineering",
                Score = 0.85f,
                Issues = new List<DesignIssue>
                {
                    new DesignIssue
                    {
                        Code = "STRUCT-BEAM-001",
                        Description = "Structural beam in ceiling zone"
                    }
                }
            };
            _agent.ReceiveFeedback(structFeedback);

            // Act
            var revisedOpinion = await _agent.EvaluateAsync(proposal);

            // Assert
            revisedOpinion.IsRevised.Should().BeTrue();
            revisedOpinion.Issues.Should().Contain(i =>
                i.Code.Contains("STRUCT-CONFLICT"));
        }

        [Test]
        public void ReceiveFeedback_MultipleFeedback_ShouldAcceptAll()
        {
            // Arrange
            var feedback1 = new AgentOpinion { AgentId = "AGENT1", Score = 0.8f };
            var feedback2 = new AgentOpinion { AgentId = "AGENT2", Score = 0.7f };

            // Act - Should not throw
            _agent.ReceiveFeedback(feedback1);
            _agent.ReceiveFeedback(feedback2);

            // Assert - Feedback is processed during next evaluation
        }

        #endregion

        #region Score Calculation Tests

        [Test]
        public async Task EvaluateAsync_WithCriticalIssue_ShouldReduceScoreSignificantly()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST-CRIT",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object>
                        {
                            ["RoomType"] = "Kitchen",
                            ["HasExhaust"] = false
                        },
                        Geometry = new GeometryInfo
                        {
                            Height = 2.3, // Very low ceiling
                            Width = 4.0,
                            Length = 4.0
                        }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Score.Should().BeLessThan(0.9f);
            opinion.Issues.Should().HaveCountGreaterThan(1);
        }

        [Test]
        public async Task EvaluateAsync_WellDesignedRoom_ShouldHaveHighScore()
        {
            // Arrange
            var proposal = new DesignProposal
            {
                ProposalId = "TEST-GOOD",
                Parameters = new Dictionary<string, object>
                {
                    ["PlumbingStackLocation"] = new { X = 5.0, Y = 5.0 }
                },
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object> { ["RoomType"] = "Office" },
                        Geometry = new GeometryInfo
                        {
                            Height = 3.2, // Good ceiling height
                            Width = 5.0,
                            Length = 6.0
                        }
                    }
                }
            };

            // Act
            var opinion = await _agent.EvaluateAsync(proposal);

            // Assert
            opinion.Score.Should().BeGreaterOrEqualTo(0.8f);
        }

        #endregion

        #region Cancellation Tests

        [Test]
        public async Task EvaluateAsync_WithCancellation_ShouldRespectToken()
        {
            // Arrange
            var proposal = CreateProposalWithRoom();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _agent.EvaluateAsync(proposal, cancellationToken: cts.Token));
        }

        [Test]
        public async Task SuggestAsync_WithCancellation_ShouldRespectToken()
        {
            // Arrange
            var context = new DesignContext { CurrentTask = "Test" };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _agent.SuggestAsync(context, cts.Token));
        }

        #endregion

        #region Helper Methods

        private DesignProposal CreateProposalWithRoom()
        {
            return new DesignProposal
            {
                ProposalId = "TEST-ROOM",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        Parameters = new Dictionary<string, object>
                        {
                            ["RoomType"] = "General"
                        },
                        Geometry = new GeometryInfo
                        {
                            Height = 2.8,
                            Width = 4.0,
                            Length = 5.0
                        }
                    }
                }
            };
        }

        #endregion
    }
}
