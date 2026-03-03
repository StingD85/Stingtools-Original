using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Agents.Framework;
using StingBIM.AI.Agents.Specialists;

namespace StingBIM.AI.Tests.Unit.Operations
{
    /// <summary>
    /// Unit tests for the multi-agent system:
    /// 6 specialist agents (Architectural, Structural, MEP, Cost, Safety, Sustainability)
    /// and AgentCoordinator orchestration.
    /// All agents are standalone-testable with zero Revit dependencies.
    /// </summary>
    [TestFixture]
    public class AgentTests
    {
        #region Test Data Helpers

        private static DesignProposal CreateWallProposal(double height = 3.0, double width = 0.2, double length = 5.0)
        {
            return new DesignProposal
            {
                ProposalId = $"test-{Guid.NewGuid():N}",
                Description = "Test wall proposal",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Wall",
                        FamilyName = "Basic Wall",
                        Geometry = new GeometryInfo { Height = height, Width = width, Length = length },
                        Parameters = new Dictionary<string, object>
                        {
                            ["Height"] = height,
                            ["Width"] = width,
                            ["Length"] = length,
                            ["Material"] = "Concrete"
                        }
                    }
                }
            };
        }

        private static DesignProposal CreateRoomProposal(double area = 15.0, double ceilingHeight = 2.7)
        {
            return new DesignProposal
            {
                ProposalId = $"test-{Guid.NewGuid():N}",
                Description = "Test room proposal",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Room",
                        FamilyName = "Room",
                        Geometry = new GeometryInfo
                        {
                            Width = Math.Sqrt(area),
                            Length = Math.Sqrt(area),
                            Height = ceilingHeight
                        },
                        Parameters = new Dictionary<string, object>
                        {
                            ["Area"] = area,
                            ["CeilingHeight"] = ceilingHeight,
                            ["Name"] = "Living Room"
                        }
                    }
                }
            };
        }

        private static DesignProposal CreateMultiElementProposal()
        {
            return new DesignProposal
            {
                ProposalId = $"test-{Guid.NewGuid():N}",
                Description = "Multi-element proposal",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Wall",
                        Geometry = new GeometryInfo { Height = 3.0, Width = 0.2, Length = 5.0 },
                        Parameters = new Dictionary<string, object>
                        {
                            ["Height"] = 3.0, ["Width"] = 0.2, ["Length"] = 5.0, ["Material"] = "Concrete"
                        }
                    },
                    new ProposedElement
                    {
                        ElementType = "Door",
                        Geometry = new GeometryInfo { Height = 2.1, Width = 0.9 },
                        Parameters = new Dictionary<string, object>
                        {
                            ["Height"] = 2.1, ["Width"] = 0.9
                        }
                    },
                    new ProposedElement
                    {
                        ElementType = "Window",
                        Geometry = new GeometryInfo { Height = 1.2, Width = 1.5 },
                        Parameters = new Dictionary<string, object>
                        {
                            ["Height"] = 1.2, ["Width"] = 1.5, ["SillHeight"] = 0.9
                        }
                    }
                }
            };
        }

        #endregion

        #region ArchitecturalAgent Tests

        [TestFixture]
        public class ArchitecturalAgentTests
        {
            private ArchitecturalAgent _agent;

            [SetUp]
            public void SetUp()
            {
                _agent = new ArchitecturalAgent();
            }

            [Test]
            public void Properties_AreCorrect()
            {
                _agent.AgentId.Should().Be("ARCH-001");
                _agent.Specialty.Should().Be("Architectural Design");
                _agent.ExpertiseLevel.Should().Be(0.9f);
                _agent.IsActive.Should().BeTrue();
            }

            [Test]
            public async Task EvaluateAsync_StandardWall_ReturnsPositiveOpinion()
            {
                var proposal = CreateWallProposal(height: 3.0, width: 0.2, length: 5.0);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.AgentId.Should().Be("ARCH-001");
                opinion.Score.Should().BeGreaterThan(0);
                opinion.Score.Should().BeLessOrEqualTo(1.0f);
            }

            [Test]
            public async Task EvaluateAsync_LowCeilingRoom_FlagsIssue()
            {
                // Ceiling height 2.0m is below minimum 2.4m
                var proposal = CreateRoomProposal(ceilingHeight: 2.0);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
                opinion.Score.Should().BeLessThan(1.0f);
            }

            [Test]
            public async Task EvaluateAsync_TinyRoom_FlagsIssue()
            {
                // Room area 4m² is below minimum 6m²
                var proposal = CreateRoomProposal(area: 4.0);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
            }

            [Test]
            public async Task EvaluateAsync_WithContext_UsesProjectType()
            {
                var proposal = CreateMultiElementProposal();
                var context = new EvaluationContext
                {
                    ProjectType = "Residential",
                    BuildingCode = "IBC 2021",
                    ClimateZone = "3A"
                };

                var opinion = await _agent.EvaluateAsync(proposal, context);

                opinion.Should().NotBeNull();
                opinion.AspectScores.Should().NotBeEmpty();
            }

            [Test]
            public async Task SuggestAsync_ReturnsArchitecturalSuggestions()
            {
                var context = new DesignContext
                {
                    CurrentTask = "Design floor plan",
                    UserIntent = "Create residential layout"
                };

                var suggestions = (await _agent.SuggestAsync(context)).ToList();

                suggestions.Should().NotBeEmpty();
                suggestions.Should().AllSatisfy(s => s.AgentId.Should().Be("ARCH-001"));
            }

            [Test]
            public void ValidateAction_CreateWall_ReturnsValid()
            {
                var action = new DesignAction
                {
                    ActionType = "Create",
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Height"] = 3.0,
                        ["Width"] = 0.2
                    }
                };

                var result = _agent.ValidateAction(action);

                result.IsValid.Should().BeTrue();
            }

            [Test]
            public void ReceiveFeedback_UpdatesFeedbackList()
            {
                var feedback = new AgentOpinion
                {
                    AgentId = "STRUCT-001",
                    Specialty = "Structural",
                    Score = 0.8f,
                    Issues = new List<DesignIssue>
                    {
                        new DesignIssue { Description = "Load path issue", Severity = IssueSeverity.Warning }
                    }
                };

                // Should not throw
                _agent.ReceiveFeedback(feedback);
            }

            [Test]
            public async Task EvaluateAsync_EmptyProposal_HandlesGracefully()
            {
                var proposal = new DesignProposal
                {
                    ProposalId = "empty",
                    Elements = new List<ProposedElement>()
                };

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.Score.Should().BeGreaterThan(0);
            }

            [Test]
            public async Task EvaluateAsync_Cancellation_ThrowsOperationCanceled()
            {
                var proposal = CreateWallProposal();
                using var cts = new CancellationTokenSource();
                cts.Cancel();

                Func<Task> act = () => _agent.EvaluateAsync(proposal, cancellationToken: cts.Token);

                await act.Should().ThrowAsync<OperationCanceledException>();
            }
        }

        #endregion

        #region StructuralAgent Tests

        [TestFixture]
        public class StructuralAgentTests
        {
            private StructuralAgent _agent;

            [SetUp]
            public void SetUp()
            {
                _agent = new StructuralAgent();
            }

            [Test]
            public void Properties_AreCorrect()
            {
                _agent.AgentId.Should().Be("STRUCT-001");
                _agent.Specialty.Should().Contain("Structural");
                _agent.ExpertiseLevel.Should().Be(0.95f);
                _agent.IsActive.Should().BeTrue();
            }

            [Test]
            public async Task EvaluateAsync_WallWithAdequateThickness_ScoresWell()
            {
                // 0.2m thickness meets MinLoadBearingWallThickness
                var proposal = CreateWallProposal(height: 3.0, width: 0.2, length: 5.0);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.Score.Should().BeGreaterThan(0);
            }

            [Test]
            public async Task EvaluateAsync_ThinWall_FlagsStructuralIssue()
            {
                // 0.05m is below MinLoadBearingWallThickness (0.2m)
                var proposal = CreateWallProposal(width: 0.05);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
            }

            [Test]
            public async Task EvaluateAsync_LongUnsupportedWall_FlagsSpanIssue()
            {
                // 10m exceeds MaxWallSpanWithoutSupport (6.0m)
                var proposal = CreateWallProposal(length: 10.0);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
            }

            [Test]
            public void ValidateAction_CreateColumn_ReturnsValid()
            {
                var action = new DesignAction
                {
                    ActionType = "Create",
                    ElementType = "Column",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Height"] = 3.0,
                        ["Size"] = "400x400"
                    }
                };

                var result = _agent.ValidateAction(action);

                result.IsValid.Should().BeTrue();
            }
        }

        #endregion

        #region MEPAgent Tests

        [TestFixture]
        public class MEPAgentTests
        {
            private MEPAgent _agent;

            [SetUp]
            public void SetUp()
            {
                _agent = new MEPAgent();
            }

            [Test]
            public void Properties_AreCorrect()
            {
                _agent.AgentId.Should().Be("MEP-001");
                _agent.ExpertiseLevel.Should().Be(0.9f);
                _agent.IsActive.Should().BeTrue();
            }

            [Test]
            public async Task EvaluateAsync_StandardRoom_EvaluatesMEP()
            {
                var proposal = CreateRoomProposal(area: 20.0, ceilingHeight: 2.7);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.AgentId.Should().Be("MEP-001");
            }

            [Test]
            public async Task EvaluateAsync_LowCeiling_FlagsDuctClearance()
            {
                // 2.3m < MinCeilingClearanceForDucts (2.4m)
                var proposal = CreateRoomProposal(ceilingHeight: 2.3);

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
            }

            [Test]
            public async Task SuggestAsync_ReturnsMEPSuggestions()
            {
                var context = new DesignContext { CurrentTask = "Design MEP layout" };

                var suggestions = (await _agent.SuggestAsync(context)).ToList();

                suggestions.Should().NotBeEmpty();
                suggestions.Should().AllSatisfy(s => s.AgentId.Should().Be("MEP-001"));
            }
        }

        #endregion

        #region CostAgent Tests

        [TestFixture]
        public class CostAgentTests
        {
            private CostAgent _agent;

            [SetUp]
            public void SetUp()
            {
                _agent = new CostAgent();
            }

            [Test]
            public void Properties_AreCorrect()
            {
                _agent.AgentId.Should().Be("COST-001");
                _agent.ExpertiseLevel.Should().Be(0.85f);
            }

            [Test]
            public async Task EvaluateAsync_ConcretteWall_EvaluatesCost()
            {
                var proposal = CreateWallProposal();

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.AgentId.Should().Be("COST-001");
                opinion.Score.Should().BeGreaterThan(0);
            }

            [Test]
            public async Task EvaluateAsync_MultiElement_EvaluatesValueEngineering()
            {
                var proposal = CreateMultiElementProposal();

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.AspectScores.Should().NotBeEmpty();
            }

            [Test]
            public async Task SuggestAsync_ReturnsCostSuggestions()
            {
                var context = new DesignContext { CurrentTask = "Optimize costs" };

                var suggestions = (await _agent.SuggestAsync(context)).ToList();

                suggestions.Should().NotBeEmpty();
            }
        }

        #endregion

        #region SafetyAgent Tests

        [TestFixture]
        public class SafetyAgentTests
        {
            private SafetyAgent _agent;

            [SetUp]
            public void SetUp()
            {
                _agent = new SafetyAgent();
            }

            [Test]
            public void Properties_AreCorrect()
            {
                _agent.AgentId.Should().Be("SAFETY-001");
                _agent.ExpertiseLevel.Should().Be(0.95f);
            }

            [Test]
            public async Task EvaluateAsync_NarrowDoor_FlagsAccessibility()
            {
                var proposal = new DesignProposal
                {
                    ProposalId = "narrow-door",
                    Elements = new List<ProposedElement>
                    {
                        new ProposedElement
                        {
                            ElementType = "Door",
                            Geometry = new GeometryInfo { Height = 2.1, Width = 0.6 },
                            Parameters = new Dictionary<string, object>
                            {
                                ["Height"] = 2.1,
                                ["Width"] = 0.6 // Below min accessible door width 0.815m
                            }
                        }
                    }
                };

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
            }

            [Test]
            public async Task EvaluateAsync_StandardDoor_PassesSafety()
            {
                var proposal = new DesignProposal
                {
                    ProposalId = "standard-door",
                    Elements = new List<ProposedElement>
                    {
                        new ProposedElement
                        {
                            ElementType = "Door",
                            Geometry = new GeometryInfo { Height = 2.1, Width = 0.9 },
                            Parameters = new Dictionary<string, object>
                            {
                                ["Height"] = 2.1,
                                ["Width"] = 0.9
                            }
                        }
                    }
                };

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Score.Should().BeGreaterThan(0.5f);
            }

            [Test]
            public void ValidateAction_CreateDoor_ValidatesWidth()
            {
                var action = new DesignAction
                {
                    ActionType = "Create",
                    ElementType = "Door",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Width"] = 0.9
                    }
                };

                var result = _agent.ValidateAction(action);

                result.IsValid.Should().BeTrue();
            }
        }

        #endregion

        #region SustainabilityAgent Tests

        [TestFixture]
        public class SustainabilityAgentTests
        {
            private SustainabilityAgent _agent;

            [SetUp]
            public void SetUp()
            {
                _agent = new SustainabilityAgent();
            }

            [Test]
            public void Properties_AreCorrect()
            {
                _agent.AgentId.Should().Be("SUSTAIN-001");
                _agent.ExpertiseLevel.Should().Be(0.88f);
            }

            [Test]
            public async Task EvaluateAsync_WallWithMaterial_EvaluatesSustainability()
            {
                var proposal = CreateWallProposal();

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Should().NotBeNull();
                opinion.AgentId.Should().Be("SUSTAIN-001");
            }

            [Test]
            public async Task EvaluateAsync_LargeWindows_ChecksWindowToWallRatio()
            {
                // Large window area may exceed MaxWindowToWallRatio (0.40)
                var proposal = new DesignProposal
                {
                    ProposalId = "large-windows",
                    Elements = new List<ProposedElement>
                    {
                        new ProposedElement
                        {
                            ElementType = "Window",
                            Geometry = new GeometryInfo { Height = 2.5, Width = 4.0 },
                            Parameters = new Dictionary<string, object>
                            {
                                ["Height"] = 2.5, ["Width"] = 4.0,
                                ["WindowToWallRatio"] = 0.55 // Exceeds 0.40 limit
                            }
                        }
                    }
                };

                var opinion = await _agent.EvaluateAsync(proposal);

                opinion.Issues.Should().NotBeEmpty();
            }

            [Test]
            public async Task SuggestAsync_ReturnsSustainabilitySuggestions()
            {
                var context = new DesignContext { CurrentTask = "Improve energy efficiency" };

                var suggestions = (await _agent.SuggestAsync(context)).ToList();

                suggestions.Should().NotBeEmpty();
            }
        }

        #endregion

        #region AgentOpinion Data Class Tests

        [TestFixture]
        public class AgentOpinionTests
        {
            [Test]
            public void IsPositive_ScoreAbove07_ReturnsTrue()
            {
                var opinion = new AgentOpinion { Score = 0.8f };

                opinion.IsPositive.Should().BeTrue();
            }

            [Test]
            public void IsPositive_ScoreBelow07_ReturnsFalse()
            {
                var opinion = new AgentOpinion { Score = 0.5f };

                opinion.IsPositive.Should().BeFalse();
            }

            [Test]
            public void HasCriticalIssues_WithCritical_ReturnsTrue()
            {
                var opinion = new AgentOpinion
                {
                    Issues = new List<DesignIssue>
                    {
                        new DesignIssue { Severity = IssueSeverity.Critical, Description = "Structural failure risk" }
                    }
                };

                opinion.HasCriticalIssues.Should().BeTrue();
            }

            [Test]
            public void HasCriticalIssues_NoCritical_ReturnsFalse()
            {
                var opinion = new AgentOpinion
                {
                    Issues = new List<DesignIssue>
                    {
                        new DesignIssue { Severity = IssueSeverity.Warning, Description = "Minor concern" }
                    }
                };

                opinion.HasCriticalIssues.Should().BeFalse();
            }
        }

        #endregion

        #region ValidationResult Data Class Tests

        [TestFixture]
        public class ValidationResultTests
        {
            [Test]
            public void Valid_ReturnsIsValidTrue()
            {
                var result = ValidationResult.Valid();

                result.IsValid.Should().BeTrue();
                result.Issues.Should().BeEmpty();
            }

            [Test]
            public void Invalid_ReturnsIsValidFalseWithIssue()
            {
                var result = ValidationResult.Invalid("Wall too thin", IssueSeverity.Error);

                result.IsValid.Should().BeFalse();
                result.Issues.Should().HaveCount(1);
                result.Issues[0].Description.Should().Be("Wall too thin");
                result.Issues[0].Severity.Should().Be(IssueSeverity.Error);
            }
        }

        #endregion

        #region Multi-Agent Coordination Tests

        [TestFixture]
        public class MultiAgentCoordinationTests
        {
            [Test]
            public async Task AllAgents_EvaluateSameProposal_ReturnDifferentPerspectives()
            {
                var agents = new IDesignAgent[]
                {
                    new ArchitecturalAgent(),
                    new StructuralAgent(),
                    new MEPAgent(),
                    new CostAgent(),
                    new SafetyAgent(),
                    new SustainabilityAgent()
                };

                var proposal = CreateMultiElementProposal();
                var opinions = new List<AgentOpinion>();

                foreach (var agent in agents)
                {
                    var opinion = await agent.EvaluateAsync(proposal);
                    opinions.Add(opinion);
                }

                opinions.Should().HaveCount(6);
                opinions.Select(o => o.AgentId).Should().OnlyHaveUniqueItems();
                opinions.Should().AllSatisfy(o =>
                {
                    o.Score.Should().BeInRange(0, 1.0f);
                });
            }

            [Test]
            public async Task AllAgents_ImplementIDesignAgent_Correctly()
            {
                var agents = new IDesignAgent[]
                {
                    new ArchitecturalAgent(),
                    new StructuralAgent(),
                    new MEPAgent(),
                    new CostAgent(),
                    new SafetyAgent(),
                    new SustainabilityAgent()
                };

                foreach (var agent in agents)
                {
                    agent.AgentId.Should().NotBeNullOrEmpty();
                    agent.Specialty.Should().NotBeNullOrEmpty();
                    agent.ExpertiseLevel.Should().BeInRange(0, 1.0f);
                    agent.IsActive.Should().BeTrue();

                    var proposal = CreateWallProposal();
                    var opinion = await agent.EvaluateAsync(proposal);
                    opinion.Should().NotBeNull();

                    var suggestions = await agent.SuggestAsync(new DesignContext { CurrentTask = "Review" });
                    suggestions.Should().NotBeNull();
                }
            }
        }

        #endregion
    }
}
