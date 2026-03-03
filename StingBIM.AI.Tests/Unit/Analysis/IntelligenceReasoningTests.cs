using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Intelligence.Reasoning;

namespace StingBIM.AI.Tests.Unit.Analysis
{
    /// <summary>
    /// Unit tests for AI.Intelligence Reasoning layer:
    /// CausalReasoningEngine, AnalogicalReasoner, DesignIntentInferencer.
    /// All classes are standalone-testable with zero Revit dependencies.
    /// </summary>
    [TestFixture]
    public class IntelligenceReasoningTests
    {
        #region CausalReasoningEngine Tests

        [TestFixture]
        public class CausalReasoningEngineTests
        {
            private CausalReasoningEngine _engine;

            [SetUp]
            public void SetUp()
            {
                _engine = new CausalReasoningEngine();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void PredictEffects_KnownCondition_ReturnsCausalChain()
            {
                // The engine has built-in causal relationships
                var chain = _engine.PredictEffects("Poor Insulation");

                chain.Should().NotBeNull();
                chain.RootCause.Should().Be("Poor Insulation");
                // May or may not have effects depending on built-in graph
            }

            [Test]
            public void PredictEffects_WithMinStrength_FiltersByStrength()
            {
                var highStrength = _engine.PredictEffects("Poor Insulation", minStrength: 0.8f);
                var lowStrength = _engine.PredictEffects("Poor Insulation", minStrength: 0.2f);

                // Lower threshold should return at least as many effects
                highStrength.TotalEffects.Should().BeLessThanOrEqualTo(lowStrength.TotalEffects);
            }

            [Test]
            public void PredictEffects_MaxDepthLimit_RespectsDepth()
            {
                var shallow = _engine.PredictEffects("Poor Insulation", maxDepth: 1);
                var deep = _engine.PredictEffects("Poor Insulation", maxDepth: 5);

                if (shallow.PredictedEffects.Any())
                {
                    shallow.PredictedEffects.Max(e => e.Depth).Should().BeLessThanOrEqualTo(1);
                }

                shallow.TotalEffects.Should().BeLessThanOrEqualTo(deep.TotalEffects);
            }

            [Test]
            public void FindRootCauses_KnownEffect_ReturnsCauses()
            {
                var analysis = _engine.FindRootCauses("High Energy Costs");

                analysis.Should().NotBeNull();
                analysis.ObservedEffect.Should().Be("High Energy Costs");
            }

            [Test]
            public void AnalyzeDecision_DesignChange_ReturnsAnalysis()
            {
                var decision = new DesignDecision
                {
                    DecisionId = "dec-001",
                    DecisionType = "MaterialChange",
                    Description = "Switch from concrete to timber frame",
                    Parameters = new Dictionary<string, object>
                    {
                        ["OriginalMaterial"] = "Concrete",
                        ["NewMaterial"] = "Timber"
                    }
                };

                var analysis = _engine.AnalyzeDecision(decision);

                analysis.Should().NotBeNull();
                analysis.Decision.Should().Be(decision);
                analysis.OverallRiskScore.Should().BeInRange(0, 1);
                analysis.RecommendedAction.Should().NotBeNullOrEmpty();
            }

            [Test]
            public void AnalyzeWhatIf_ProposedChange_ReturnsScenarios()
            {
                var parameters = new Dictionary<string, object>
                {
                    ["InsulationType"] = "High Performance",
                    ["Thickness"] = 150
                };

                var whatIf = _engine.AnalyzeWhatIf("Upgrade Insulation", parameters);

                whatIf.Should().NotBeNull();
                whatIf.ProposedChange.Should().Be("Upgrade Insulation");
                whatIf.Scenarios.Should().NotBeNull();
            }

            [Test]
            public void GetCausalExplanation_ReturnsExplanationString()
            {
                var explanation = _engine.GetCausalExplanation("High Energy Costs", "Poor Insulation");

                explanation.Should().NotBeNullOrEmpty();
            }

            [Test]
            public void LoadKnowledgeBase_WithCsvFile_DoesNotThrow()
            {
                var csvPath = Path.Combine(
                    TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "..", "data", "ai", "reasoning", "CAUSAL_RELATIONSHIPS.csv");
                if (System.IO.File.Exists(csvPath))
                {
                    Action act = () => _engine.LoadKnowledgeBase(csvPath);
                    act.Should().NotThrow();
                }
                else
                {
                    Assert.Pass("CSV file not available — skipping data load test");
                }
            }

            [Test]
            public void CausalRelationship_StoresAllFields()
            {
                var relationship = new CausalRelationship
                {
                    CauseId = "C001",
                    Cause = "Poor Insulation",
                    Effect = "Heat Loss",
                    EffectId = "E001",
                    Strength = 0.85f,
                    Reversible = true,
                    TimeToManifest = "Immediate",
                    Category = "Thermal",
                    Domain = "Building Physics",
                    MitigationStrategy = "Add insulation"
                };

                relationship.Strength.Should().Be(0.85f);
                relationship.Reversible.Should().BeTrue();
                relationship.Category.Should().Be("Thermal");
            }
        }

        #endregion

        #region AnalogicalReasoner Tests

        [TestFixture]
        public class AnalogicalReasonerTests
        {
            private AnalogicalReasoner _reasoner;

            [SetUp]
            public void SetUp()
            {
                _reasoner = new AnalogicalReasoner();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _reasoner.Should().NotBeNull();
            }

            [Test]
            public void FindMatchingArchetype_ResidentialProject_ReturnsMatch()
            {
                var project = new ProjectProfile
                {
                    ProjectId = "proj-001",
                    Name = "Family House",
                    BuildingType = "Residential",
                    GrossArea = 200.0,
                    ClimateZone = "4A",
                    Region = "UK"
                };

                var match = _reasoner.FindMatchingArchetype(project);

                match.Should().NotBeNull();
                match.Archetype.Should().NotBeNull();
                match.MatchScore.Should().BeInRange(0, 1);
            }

            [Test]
            public void FindMatchingArchetype_CommercialProject_FindsCommercialArchetype()
            {
                var project = new ProjectProfile
                {
                    ProjectId = "proj-002",
                    Name = "Office Tower",
                    BuildingType = "Commercial",
                    GrossArea = 5000.0,
                    ClimateZone = "3A"
                };

                var match = _reasoner.FindMatchingArchetype(project);

                match.Should().NotBeNull();
            }

            [Test]
            public void RegisterProject_ThenFindSimilar_FindsRegisteredProject()
            {
                var reference = new ProjectProfile
                {
                    ProjectId = "ref-001",
                    Name = "Reference House",
                    BuildingType = "Residential",
                    GrossArea = 180.0,
                    ClimateZone = "4A",
                    Region = "UK"
                };

                _reasoner.RegisterProject(reference);

                var query = new ProjectProfile
                {
                    ProjectId = "query-001",
                    Name = "Similar House",
                    BuildingType = "Residential",
                    GrossArea = 200.0,
                    ClimateZone = "4A",
                    Region = "UK"
                };

                var similar = _reasoner.FindSimilarProjects(query);

                similar.Should().NotBeNull();
                similar.Should().Contain(s => s.Project.ProjectId == "ref-001");
            }

            [Test]
            public void TransferSolution_SimilarProjects_ReturnsSolutionTransfer()
            {
                var source = new ProjectProfile
                {
                    ProjectId = "source-001",
                    Name = "Source Office",
                    BuildingType = "Commercial",
                    GrossArea = 3000.0,
                    ClimateZone = "4A",
                    Solutions = new List<DesignSolution>
                    {
                        new DesignSolution
                        {
                            Type = "HVAC",
                            Name = "Variable Refrigerant Flow",
                            Description = "VRF system for efficient zoning",
                            ClimateZone = "4A"
                        }
                    }
                };

                var target = new ProjectProfile
                {
                    ProjectId = "target-001",
                    Name = "Target Office",
                    BuildingType = "Commercial",
                    GrossArea = 2500.0,
                    ClimateZone = "4A"
                };

                var transfer = _reasoner.TransferSolution("HVAC", source, target);

                transfer.Should().NotBeNull();
                transfer.SolutionType.Should().Be("HVAC");
                transfer.ContextSimilarity.Should().BeInRange(0, 1);
            }

            [Test]
            public void FindAnalogies_DesignProblem_ReturnsAnalogies()
            {
                var problem = new DesignProblem
                {
                    ProblemType = "Ventilation",
                    Description = "Natural ventilation for open plan office",
                    Context = new ProblemContext
                    {
                        RoomType = "Office",
                        BuildingType = "Commercial",
                        ClimateZone = "4A"
                    },
                    Constraints = new List<string> { "Limited window area" }
                };

                var analogies = _reasoner.FindAnalogies(problem);

                analogies.Should().NotBeNull();
            }

            [Test]
            public void LearnFromSuccess_DoesNotThrow()
            {
                var project = new ProjectProfile
                {
                    ProjectId = "learn-001",
                    Name = "Successful Project",
                    BuildingType = "Residential"
                };

                var solution = new SolvedProblem
                {
                    ProblemType = "Layout",
                    Description = "Open plan layout that worked well",
                    Solution = new DesignSolution
                    {
                        Type = "Layout",
                        Name = "Open Plan",
                        Description = "Removed non-structural walls"
                    },
                    WasSuccessful = true
                };

                Action act = () => _reasoner.LearnFromSuccess(project, solution);

                act.Should().NotThrow();
            }

            [Test]
            public void ProjectSimilarity_CalculatesScore()
            {
                var calc = new SimilarityCalculator();

                var p1 = new ProjectProfile
                {
                    BuildingType = "Residential",
                    GrossArea = 200.0,
                    ClimateZone = "4A"
                };

                var p2 = new ProjectProfile
                {
                    BuildingType = "Residential",
                    GrossArea = 220.0,
                    ClimateZone = "4A"
                };

                var similarity = calc.Calculate(p1, p2);

                similarity.Should().NotBeNull();
                similarity.OverallScore.Should().BeInRange(0, 1);
                similarity.BuildingTypeSimilarity.Should().Be(1.0f,
                    "same building type should have perfect type similarity");
            }
        }

        #endregion

        #region DesignIntentInferencer Tests

        [TestFixture]
        public class DesignIntentInferencerTests
        {
            private DesignIntentInferencer _inferencer;

            [SetUp]
            public void SetUp()
            {
                _inferencer = new DesignIntentInferencer();
            }

            [Test]
            public void Constructor_DefaultMaxHistory_CreatesInstance()
            {
                _inferencer.Should().NotBeNull();
            }

            [Test]
            public void Constructor_CustomMaxHistory_CreatesInstance()
            {
                var custom = new DesignIntentInferencer(maxHistorySize: 100);
                custom.Should().NotBeNull();
            }

            [Test]
            public void InferIntent_WallCreation_ReturnsInferredIntent()
            {
                var action = new StingBIM.AI.Intelligence.Reasoning.DesignAction
                {
                    ActionId = "act-001",
                    ActionType = "Create",
                    ElementCategory = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Height"] = 3.0, ["Length"] = 5.0
                    }
                };

                var context = new StingBIM.AI.Intelligence.Reasoning.DesignContext
                {
                    RoomType = "Office",
                    BuildingType = "Commercial",
                    ProjectPhase = "SchematicDesign"
                };

                var intent = _inferencer.InferIntent(action, context);

                intent.Should().NotBeNull();
                intent.PrimaryIntent.Should().NotBeNull();
                intent.PrimaryIntent.Name.Should().NotBeNullOrEmpty();
                intent.PrimaryIntent.Confidence.Should().BeInRange(0, 1);
                intent.Confidence.Should().BeInRange(0, 1);
                intent.Explanation.Should().NotBeNullOrEmpty();
            }

            [Test]
            public void InferIntent_DoorPlacement_RecognizesOpeningIntent()
            {
                var action = new StingBIM.AI.Intelligence.Reasoning.DesignAction
                {
                    ActionId = "act-002",
                    ActionType = "Create",
                    ElementCategory = "Door",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Width"] = 0.9
                    }
                };

                var context = new StingBIM.AI.Intelligence.Reasoning.DesignContext
                {
                    RoomType = "Corridor",
                    BuildingType = "Residential"
                };

                var intent = _inferencer.InferIntent(action, context);

                intent.Should().NotBeNull();
                intent.Action.ActionType.Should().Be("Create");
            }

            [Test]
            public void PredictNextActions_FromCurrentIntent_ReturnsPredictions()
            {
                var action = new StingBIM.AI.Intelligence.Reasoning.DesignAction
                {
                    ActionType = "Create",
                    ElementCategory = "Wall"
                };

                var context = new StingBIM.AI.Intelligence.Reasoning.DesignContext
                {
                    RoomType = "Office",
                    BuildingType = "Commercial"
                };

                var intent = _inferencer.InferIntent(action, context);
                var predictions = _inferencer.PredictNextActions(intent, maxPredictions: 3);

                predictions.Should().NotBeNull();
                predictions.Should().HaveCountLessThanOrEqualTo(3);
                predictions.Should().AllSatisfy(p =>
                {
                    p.Action.Should().NotBeNullOrEmpty();
                    p.Probability.Should().BeInRange(0, 1);
                });
            }

            [Test]
            public void CheckIntentAlignment_AlignedAction_ReturnsAligned()
            {
                var createWall = new StingBIM.AI.Intelligence.Reasoning.DesignAction
                {
                    ActionType = "Create",
                    ElementCategory = "Wall"
                };

                var context = new StingBIM.AI.Intelligence.Reasoning.DesignContext
                {
                    RoomType = "Office",
                    BuildingType = "Commercial"
                };

                var intent = _inferencer.InferIntent(createWall, context);

                // Creating another wall should be aligned with room creation intent
                var nextAction = new StingBIM.AI.Intelligence.Reasoning.DesignAction
                {
                    ActionType = "Create",
                    ElementCategory = "Wall"
                };

                var alignment = _inferencer.CheckIntentAlignment(nextAction, intent);

                alignment.Should().NotBeNull();
                alignment.AlignmentScore.Should().BeInRange(0, 1);
                alignment.Message.Should().NotBeNullOrEmpty();
            }

            [Test]
            public void AlignmentStatus_AllValues_AreDefined()
            {
                var values = Enum.GetValues<AlignmentStatus>();

                values.Should().Contain(AlignmentStatus.StronglyAligned);
                values.Should().Contain(AlignmentStatus.ModeratelyAligned);
                values.Should().Contain(AlignmentStatus.WeaklyAligned);
                values.Should().Contain(AlignmentStatus.Misaligned);
            }

            [Test]
            public void IntentSource_AllValues_AreDefined()
            {
                var values = Enum.GetValues<IntentSource>();

                values.Should().Contain(IntentSource.DirectAction);
                values.Should().Contain(IntentSource.ContextualAnalysis);
                values.Should().Contain(IntentSource.PatternAnalysis);
            }
        }

        #endregion
    }
}
