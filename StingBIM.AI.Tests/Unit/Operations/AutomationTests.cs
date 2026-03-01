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
    /// Unit tests for AI.Operations automation layer:
    /// AgentCoordinator (MessageBus, consensus, conflict resolution),
    /// WorkflowAutomationEngine, ModelHealthMonitor.
    /// All classes are standalone-testable with zero Revit dependencies.
    /// </summary>
    [TestFixture]
    public class AutomationTests
    {
        #region DesignProposal & Data Model Tests

        [TestFixture]
        public class DataModelTests
        {
            [Test]
            public void DesignProposal_Defaults_AreCorrect()
            {
                var proposal = new DesignProposal();

                proposal.Elements.Should().NotBeNull();
                proposal.Elements.Should().BeEmpty();
                proposal.Modifications.Should().NotBeNull();
                proposal.Parameters.Should().NotBeNull();
                proposal.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
            }

            [Test]
            public void ProposedElement_Defaults_AreCorrect()
            {
                var element = new ProposedElement();

                element.Parameters.Should().NotBeNull();
            }

            [Test]
            public void ProposedModification_StoresOldAndNewValues()
            {
                var modification = new ProposedModification
                {
                    ElementId = "wall-123",
                    ModificationType = "Resize",
                    OldValues = new Dictionary<string, object> { ["Height"] = 3.0 },
                    NewValues = new Dictionary<string, object> { ["Height"] = 3.5 }
                };

                modification.OldValues["Height"].Should().Be(3.0);
                modification.NewValues["Height"].Should().Be(3.5);
            }

            [Test]
            public void GeometryInfo_StoresAllDimensions()
            {
                var geo = new GeometryInfo
                {
                    X = 1.0, Y = 2.0, Z = 3.0,
                    Width = 4.0, Height = 5.0, Length = 6.0,
                    Rotation = 45.0
                };

                geo.X.Should().Be(1.0);
                geo.Y.Should().Be(2.0);
                geo.Z.Should().Be(3.0);
                geo.Width.Should().Be(4.0);
                geo.Height.Should().Be(5.0);
                geo.Length.Should().Be(6.0);
                geo.Rotation.Should().Be(45.0);
            }

            [Test]
            public void EvaluationContext_StoresProjectMetadata()
            {
                var context = new EvaluationContext
                {
                    ProjectType = "Commercial",
                    BuildingCode = "IBC 2021",
                    ClimateZone = "4A",
                    ProjectParameters = new Dictionary<string, object>
                    {
                        ["GrossFloorArea"] = 5000.0,
                        ["Floors"] = 3
                    },
                    PreviousIssues = new List<string> { "HVAC clearance" }
                };

                context.ProjectType.Should().Be("Commercial");
                context.BuildingCode.Should().Be("IBC 2021");
                context.ProjectParameters.Should().ContainKey("GrossFloorArea");
                context.PreviousIssues.Should().Contain("HVAC clearance");
            }

            [Test]
            public void DesignContext_StoresUserIntent()
            {
                var context = new DesignContext
                {
                    CurrentTask = "Floor plan design",
                    UserIntent = "Create efficient office layout",
                    SelectedElementIds = new List<string> { "elem-1", "elem-2" },
                    CurrentState = new Dictionary<string, object>
                    {
                        ["Phase"] = "Schematic Design"
                    }
                };

                context.CurrentTask.Should().Be("Floor plan design");
                context.SelectedElementIds.Should().HaveCount(2);
            }

            [Test]
            public void DesignAction_StoresActionDetails()
            {
                var action = new DesignAction
                {
                    ActionType = "Create",
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Height"] = 3.0,
                        ["Material"] = "Concrete"
                    }
                };

                action.ActionType.Should().Be("Create");
                action.Parameters.Should().ContainKey("Material");
            }
        }

        #endregion

        #region DesignIssue Tests

        [TestFixture]
        public class DesignIssueTests
        {
            [Test]
            public void DesignIssue_StoresAllFields()
            {
                var issue = new DesignIssue
                {
                    Code = "ARCH-001",
                    Description = "Ceiling height below minimum",
                    Severity = IssueSeverity.Error,
                    Location = "Room 101",
                    Standard = "IBC 2021 Section 1208.2",
                    SuggestedFix = "Increase ceiling height to 2.4m minimum",
                    Details = new Dictionary<string, object>
                    {
                        ["CurrentHeight"] = 2.1,
                        ["RequiredHeight"] = 2.4
                    }
                };

                issue.Code.Should().Be("ARCH-001");
                issue.Severity.Should().Be(IssueSeverity.Error);
                issue.SuggestedFix.Should().Contain("2.4m");
                issue.Details.Should().ContainKey("CurrentHeight");
            }

            [Test]
            public void IssueSeverity_AllValues_AreDefined()
            {
                var values = Enum.GetValues<IssueSeverity>();

                values.Should().Contain(IssueSeverity.Info);
                values.Should().Contain(IssueSeverity.Warning);
                values.Should().Contain(IssueSeverity.Error);
                values.Should().Contain(IssueSeverity.Critical);
            }
        }

        #endregion

        #region AgentSuggestion Tests

        [TestFixture]
        public class AgentSuggestionTests
        {
            [Test]
            public void AgentSuggestion_StoresAllFields()
            {
                var suggestion = new AgentSuggestion
                {
                    AgentId = "SUSTAIN-001",
                    Title = "Add natural ventilation",
                    Description = "Consider cross-ventilation by adding openable windows on opposite walls",
                    Type = SuggestionType.Sustainability,
                    Confidence = 0.85f,
                    Impact = 0.7f,
                    Prerequisites = new List<string> { "Windows on two walls" }
                };

                suggestion.Type.Should().Be(SuggestionType.Sustainability);
                suggestion.Confidence.Should().Be(0.85f);
                suggestion.Impact.Should().Be(0.7f);
                suggestion.Prerequisites.Should().Contain("Windows on two walls");
            }

            [Test]
            public void SuggestionType_AllValues_AreDefined()
            {
                var values = Enum.GetValues<SuggestionType>();

                values.Should().Contain(SuggestionType.Improvement);
                values.Should().Contain(SuggestionType.Alternative);
                values.Should().Contain(SuggestionType.Warning);
                values.Should().Contain(SuggestionType.BestPractice);
                values.Should().Contain(SuggestionType.CodeCompliance);
                values.Should().Contain(SuggestionType.CostSaving);
                values.Should().Contain(SuggestionType.Sustainability);
            }
        }

        #endregion

        #region AgentOpinion Scoring Tests

        [TestFixture]
        public class AgentOpinionScoringTests
        {
            [Test]
            public void Score_Boundary_07IsPositive()
            {
                var opinion = new AgentOpinion { Score = 0.7f };

                opinion.IsPositive.Should().BeTrue();
            }

            [Test]
            public void Score_JustBelow_07IsNegative()
            {
                var opinion = new AgentOpinion { Score = 0.69f };

                opinion.IsPositive.Should().BeFalse();
            }

            [Test]
            public void AspectScores_StoresMultipleDimensions()
            {
                var opinion = new AgentOpinion
                {
                    Score = 0.75f,
                    AspectScores = new Dictionary<string, float>
                    {
                        ["SpatialQuality"] = 0.8f,
                        ["NaturalLight"] = 0.6f,
                        ["Circulation"] = 0.9f,
                        ["Proportions"] = 0.7f
                    }
                };

                opinion.AspectScores.Should().HaveCount(4);
                opinion.AspectScores["NaturalLight"].Should().Be(0.6f);
            }

            [Test]
            public void Timestamp_IsSetToNow()
            {
                var opinion = new AgentOpinion();

                opinion.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
            }

            [Test]
            public void Strengths_StoresPositiveAspects()
            {
                var opinion = new AgentOpinion
                {
                    Strengths = new List<string>
                    {
                        "Good natural lighting",
                        "Efficient circulation",
                        "Appropriate room proportions"
                    }
                };

                opinion.Strengths.Should().HaveCount(3);
            }

            [Test]
            public void IsRevised_DefaultIsFalse()
            {
                var opinion = new AgentOpinion();

                opinion.IsRevised.Should().BeFalse();
            }
        }

        #endregion

        #region Cross-Agent Feedback Tests

        [TestFixture]
        public class CrossAgentFeedbackTests
        {
            [Test]
            public async Task Agent_AfterReceivingFeedback_MayReviseOpinion()
            {
                var archAgent = new ArchitecturalAgent();
                var structAgent = new StructuralAgent();

                var proposal = new DesignProposal
                {
                    ProposalId = "feedback-test",
                    Elements = new List<ProposedElement>
                    {
                        new ProposedElement
                        {
                            ElementType = "Wall",
                            Geometry = new GeometryInfo { Height = 3.0, Width = 0.2, Length = 5.0 },
                            Parameters = new Dictionary<string, object>
                            {
                                ["Height"] = 3.0, ["Width"] = 0.2, ["Length"] = 5.0
                            }
                        }
                    }
                };

                // First evaluation
                var structOpinion = await structAgent.EvaluateAsync(proposal);

                // Architectural agent receives structural feedback
                archAgent.ReceiveFeedback(structOpinion);

                // Re-evaluate — may adjust score
                var archOpinion = await archAgent.EvaluateAsync(proposal);

                archOpinion.Should().NotBeNull();
                // The agent should incorporate feedback (test that it doesn't crash)
            }

            [Test]
            public async Task AllAgents_CanGiveAndReceiveFeedback()
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

                var proposal = new DesignProposal
                {
                    ProposalId = "cross-feedback",
                    Elements = new List<ProposedElement>
                    {
                        new ProposedElement
                        {
                            ElementType = "Wall",
                            Geometry = new GeometryInfo { Height = 3.0, Width = 0.2, Length = 5.0 },
                            Parameters = new Dictionary<string, object>
                            {
                                ["Height"] = 3.0, ["Width"] = 0.2, ["Length"] = 5.0
                            }
                        }
                    }
                };

                // All agents evaluate
                var opinions = new List<AgentOpinion>();
                foreach (var agent in agents)
                {
                    opinions.Add(await agent.EvaluateAsync(proposal));
                }

                // Each agent receives feedback from all others
                foreach (var agent in agents)
                {
                    foreach (var opinion in opinions.Where(o => o.AgentId != agent.AgentId))
                    {
                        agent.ReceiveFeedback(opinion);
                    }
                }

                // Verify no crashes and agents still function
                foreach (var agent in agents)
                {
                    var result = agent.ValidateAction(new DesignAction
                    {
                        ActionType = "Create", ElementType = "Wall"
                    });
                    result.Should().NotBeNull();
                }
            }
        }

        #endregion
    }
}
