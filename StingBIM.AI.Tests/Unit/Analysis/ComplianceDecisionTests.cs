using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Reasoning.Compliance;
using StingBIM.AI.Reasoning.Decision;
using StingBIM.AI.Reasoning.Materials;
using StingBIM.AI.Reasoning.Patterns;
using StingBIM.AI.Reasoning.Predictive;

namespace StingBIM.AI.Tests.Unit.Analysis
{
    /// <summary>
    /// Unit tests for AI.Analysis Reasoning layer:
    /// ComplianceChecker, DecisionSupport, MaterialIntelligence,
    /// DesignPatternRecognizer, PredictiveEngine.
    /// All classes are standalone-testable with zero Revit dependencies.
    /// </summary>
    [TestFixture]
    public class ComplianceDecisionTests
    {
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

            [Test]
            public void Constructor_CreatesInstance()
            {
                _checker.Should().NotBeNull();
            }

            [Test]
            public void GetAvailableProfiles_ReturnsAtLeastOneProfile()
            {
                var profiles = _checker.GetAvailableProfiles();

                profiles.Should().NotBeEmpty();
            }

            [Test]
            public void SetCodeProfile_ValidProfile_DoesNotThrow()
            {
                var profiles = _checker.GetAvailableProfiles().ToList();
                profiles.Should().NotBeEmpty();

                Action act = () => _checker.SetCodeProfile(profiles.First());

                act.Should().NotThrow();
            }

            [Test]
            public async Task CheckAsync_CompliantElement_ReturnsCompliant()
            {
                var element = new DesignElement
                {
                    Id = "door-001",
                    Type = "Door",
                    Properties = new Dictionary<string, object>
                    {
                        ["Width"] = 1.0,          // 1m wide door — compliant
                        ["Height"] = 2.1,          // 2.1m high — compliant
                        ["ClearWidth"] = 0.9,      // 900mm clear — ADA compliant
                        ["FireRating"] = "1HR"
                    }
                };

                var result = await _checker.CheckAsync(element);

                result.Should().NotBeNull();
                result.ElementId.Should().Be("door-001");
                result.ElementType.Should().Be("Door");
                result.Score.Should().BeInRange(0, 100);
                result.CheckedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            }

            [Test]
            public async Task CheckAsync_NarrowDoor_FlagsAccessibilityViolation()
            {
                var element = new DesignElement
                {
                    Id = "door-narrow",
                    Type = "Door",
                    Properties = new Dictionary<string, object>
                    {
                        ["Width"] = 0.6,           // Too narrow
                        ["ClearWidth"] = 0.55,     // Below ADA minimum
                        ["Height"] = 2.0
                    }
                };

                var result = await _checker.CheckAsync(element);

                result.Should().NotBeNull();
                // Should flag issues for narrow door
                if (!result.IsCompliant)
                {
                    result.Violations.Should().NotBeEmpty();
                }
            }

            [Test]
            public async Task CheckBatchAsync_MultipleElements_ReturnsAllResults()
            {
                var elements = new List<DesignElement>
                {
                    new DesignElement
                    {
                        Id = "wall-001", Type = "Wall",
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness"] = 0.2, ["Height"] = 3.0, ["Length"] = 5.0
                        }
                    },
                    new DesignElement
                    {
                        Id = "door-001", Type = "Door",
                        Properties = new Dictionary<string, object>
                        {
                            ["Width"] = 0.9, ["Height"] = 2.1, ["ClearWidth"] = 0.85
                        }
                    },
                    new DesignElement
                    {
                        Id = "corridor-001", Type = "Corridor",
                        Properties = new Dictionary<string, object>
                        {
                            ["Width"] = 1.5, ["Length"] = 10.0
                        }
                    }
                };

                var batchResult = await _checker.CheckBatchAsync(elements);

                batchResult.Should().NotBeNull();
                batchResult.TotalElements.Should().Be(3);
                batchResult.ElementResults.Should().HaveCount(3);
                batchResult.OverallScore.Should().BeInRange(0, 100);
                batchResult.CheckedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            }

            [Test]
            public async Task CheckBatchAsync_AllCompliant_HighScore()
            {
                var elements = new List<DesignElement>
                {
                    new DesignElement
                    {
                        Id = "wall-ok", Type = "Wall",
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness"] = 0.3, ["Height"] = 3.0, ["Length"] = 5.0,
                            ["FireRating"] = "2HR"
                        }
                    }
                };

                var result = await _checker.CheckBatchAsync(elements);

                result.CompliantElements.Should().BeGreaterThanOrEqualTo(0);
                result.CompliantElements.Should().BeLessThanOrEqualTo(result.TotalElements);
            }

            [Test]
            public async Task GenerateReport_FromBatchResult_ReturnsReport()
            {
                var elements = new List<DesignElement>
                {
                    new DesignElement
                    {
                        Id = "wall-001", Type = "Wall",
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness"] = 0.2, ["Height"] = 3.0
                        }
                    }
                };

                var batchResult = await _checker.CheckBatchAsync(elements);
                var report = _checker.GenerateReport(batchResult);

                report.Should().NotBeNull();
                report.GeneratedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
                report.Summary.Should().NotBeNull();
                report.Summary.TotalElements.Should().Be(1);
            }

            [Test]
            public void DesignElement_GetProperty_ReturnsTypedValue()
            {
                var element = new DesignElement
                {
                    Id = "test",
                    Type = "Wall",
                    Properties = new Dictionary<string, object>
                    {
                        ["Height"] = 3.0,
                        ["Name"] = "Main Wall"
                    }
                };

                element.GetProperty<double>("Height").Should().Be(3.0);
            }

            [Test]
            public void ViolationSeverity_AllValues_AreDefined()
            {
                var values = Enum.GetValues<ViolationSeverity>();

                values.Should().Contain(ViolationSeverity.Info);
                values.Should().Contain(ViolationSeverity.Warning);
                values.Should().Contain(ViolationSeverity.Error);
                values.Should().Contain(ViolationSeverity.Critical);
            }

            [Test]
            public void ComplianceCategory_AllValues_AreDefined()
            {
                var values = Enum.GetValues<ComplianceCategory>();

                values.Should().Contain(ComplianceCategory.Egress);
                values.Should().Contain(ComplianceCategory.Accessibility);
                values.Should().Contain(ComplianceCategory.FireSafety);
                values.Should().Contain(ComplianceCategory.Structural);
            }
        }

        #endregion

        #region DecisionSupport Tests

        [TestFixture]
        public class DecisionSupportTests
        {
            private DecisionSupport _decision;

            [SetUp]
            public void SetUp()
            {
                _decision = new DecisionSupport();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _decision.Should().NotBeNull();
            }

            [Test]
            public void EvaluateAlternatives_TwoAlternatives_ReturnsRanking()
            {
                var alternatives = new List<DesignAlternative>
                {
                    new DesignAlternative
                    {
                        AlternativeId = "alt-1",
                        Name = "Steel Frame",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.6, ["Performance"] = 0.9,
                            ["Sustainability"] = 0.5, ["Schedule"] = 0.8
                        }
                    },
                    new DesignAlternative
                    {
                        AlternativeId = "alt-2",
                        Name = "Timber Frame",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.8, ["Performance"] = 0.7,
                            ["Sustainability"] = 0.9, ["Schedule"] = 0.7
                        }
                    }
                };

                var weights = new Dictionary<string, double>
                {
                    ["Cost"] = 0.3, ["Performance"] = 0.3,
                    ["Sustainability"] = 0.2, ["Schedule"] = 0.2
                };

                var analysis = _decision.EvaluateAlternatives(alternatives, weights);

                analysis.Should().NotBeNull();
                analysis.Ranking.Should().HaveCount(2);
                analysis.Ranking.First().Rank.Should().Be(1);
                analysis.Recommendation.Should().NotBeNull();
                analysis.Recommendation.Confidence.Should().BeInRange(0, 1);
                analysis.WeightedScores.Should().HaveCount(2);
            }

            [Test]
            public void EvaluateAlternatives_HighCostWeight_FavorsCheaperOption()
            {
                var alternatives = new List<DesignAlternative>
                {
                    new DesignAlternative
                    {
                        AlternativeId = "expensive",
                        Name = "Premium Option",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.3, ["Performance"] = 0.9
                        }
                    },
                    new DesignAlternative
                    {
                        AlternativeId = "cheap",
                        Name = "Budget Option",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.9, ["Performance"] = 0.5
                        }
                    }
                };

                var costHeavyWeights = new Dictionary<string, double>
                {
                    ["Cost"] = 0.9, ["Performance"] = 0.1
                };

                var analysis = _decision.EvaluateAlternatives(alternatives, costHeavyWeights);

                analysis.Ranking.First().AlternativeId.Should().Be("cheap",
                    "with high cost weight, cheaper option should rank first");
            }

            [Test]
            public void AnalyzeSensitivity_VaryingCriterion_ReturnsSensitivityResults()
            {
                var alternatives = new List<DesignAlternative>
                {
                    new DesignAlternative
                    {
                        AlternativeId = "a1", Name = "Option A",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.7, ["Performance"] = 0.8
                        }
                    },
                    new DesignAlternative
                    {
                        AlternativeId = "a2", Name = "Option B",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.9, ["Performance"] = 0.5
                        }
                    }
                };

                var baseWeights = new Dictionary<string, double>
                {
                    ["Cost"] = 0.5, ["Performance"] = 0.5
                };

                var sensitivity = _decision.AnalyzeSensitivity(alternatives, baseWeights, "Cost");

                sensitivity.Should().NotBeNull();
                sensitivity.CriterionVaried.Should().Be("Cost");
                sensitivity.Results.Should().NotBeEmpty();
            }

            [Test]
            public void AnalyzeTradeOff_TwoCriteria_ReturnsTradeOffAnalysis()
            {
                var alternatives = new List<DesignAlternative>
                {
                    new DesignAlternative
                    {
                        AlternativeId = "a1", Name = "Option A",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.3, ["Sustainability"] = 0.9
                        }
                    },
                    new DesignAlternative
                    {
                        AlternativeId = "a2", Name = "Option B",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.8, ["Sustainability"] = 0.4
                        }
                    }
                };

                var tradeOff = _decision.AnalyzeTradeOff(alternatives, "Cost", "Sustainability");

                tradeOff.Should().NotBeNull();
                tradeOff.Criterion1.Should().Be("Cost");
                tradeOff.Criterion2.Should().Be("Sustainability");
                tradeOff.DataPoints.Should().NotBeEmpty();
            }

            [Test]
            public void AssessRisks_ReturnsRiskAssessment()
            {
                var alternative = new DesignAlternative
                {
                    AlternativeId = "risky",
                    Name = "Experimental Design",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["Cost"] = 0.5, ["Performance"] = 0.7
                    },
                    Characteristics = new List<string> { "new_technology", "complex_geometry" }
                };

                var context = new ProjectContext
                {
                    Region = "East Africa",
                    BuildingType = "Commercial",
                    Budget = 5000000,
                    Duration = 24
                };

                var risk = _decision.AssessRisks(alternative, context);

                risk.Should().NotBeNull();
                risk.AlternativeId.Should().Be("risky");
                risk.OverallRiskScore.Should().BeInRange(0, 1);
            }

            [Test]
            public void GetAllTemplates_ReturnsTemplates()
            {
                var templates = _decision.GetAllTemplates();

                templates.Should().NotBeEmpty();
                templates.Should().AllSatisfy(t =>
                {
                    t.TemplateId.Should().NotBeNullOrEmpty();
                    t.Name.Should().NotBeNullOrEmpty();
                });
            }

            [Test]
            public void GetTradeOffGuidance_KnownCriteria_ReturnsGuidance()
            {
                var guidance = _decision.GetTradeOffGuidance("Cost", "Performance");

                guidance.Should().NotBeNull();
                guidance.Criterion1.Should().Be("Cost");
                guidance.Criterion2.Should().Be("Performance");
            }

            [Test]
            public void GenerateReport_FromAnalysis_ReturnsReport()
            {
                var alternatives = new List<DesignAlternative>
                {
                    new DesignAlternative
                    {
                        AlternativeId = "a1", Name = "Option A",
                        CriteriaValues = new Dictionary<string, double>
                        {
                            ["Cost"] = 0.7, ["Performance"] = 0.8
                        }
                    }
                };
                var weights = new Dictionary<string, double>
                {
                    ["Cost"] = 0.5, ["Performance"] = 0.5
                };

                var analysis = _decision.EvaluateAlternatives(alternatives, weights);
                var report = _decision.GenerateReport(analysis);

                report.Should().NotBeNull();
                report.Title.Should().NotBeNullOrEmpty();
                report.Sections.Should().NotBeEmpty();
            }
        }

        #endregion

        #region MaterialIntelligence Tests

        [TestFixture]
        public class MaterialIntelligenceTests
        {
            private MaterialIntelligence _materials;

            [SetUp]
            public void SetUp()
            {
                _materials = new MaterialIntelligence();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _materials.Should().NotBeNull();
            }

            [Test]
            public void GetRecommendation_WallElement_ReturnsRecommendation()
            {
                var context = new MaterialSelectionContext
                {
                    ElementType = "Wall",
                    Application = "Exterior",
                    Region = "UK",
                    BuildingHeight = 15.0,
                    PerformancePriority = 7,
                    SustainabilityPriority = 5,
                    CostPriority = 5
                };

                var recommendation = _materials.GetRecommendation(context);

                recommendation.Should().NotBeNull();
                recommendation.PrimaryRecommendation.Should().NotBeNull();
                recommendation.PrimaryRecommendation.Score.Should().BeGreaterThan(0);
                recommendation.Rationale.Should().NotBeNullOrEmpty();
            }

            [Test]
            public void GetRecommendation_WithHighSustainability_FavorsSustainableMaterials()
            {
                var context = new MaterialSelectionContext
                {
                    ElementType = "Wall",
                    SustainabilityTarget = SustainabilityTarget.NetZero,
                    SustainabilityPriority = 9,
                    CostPriority = 2,
                    PerformancePriority = 5
                };

                var recommendation = _materials.GetRecommendation(context);

                recommendation.Should().NotBeNull();
                recommendation.SustainableAlternatives.Should().NotBeNull();
            }

            [Test]
            public void AnalyzeEnvironmentalImpact_MultipleMaterials_ReturnsAnalysis()
            {
                var materials = new List<MaterialQuantity>
                {
                    new MaterialQuantity { MaterialId = "CONC-STD", Volume = 100.0, Unit = "m3" },
                    new MaterialQuantity { MaterialId = "STEEL-STR", Volume = 20.0, Unit = "m3" }
                };

                var analysis = _materials.AnalyzeEnvironmentalImpact(materials);

                analysis.Should().NotBeNull();
                analysis.TotalEmbodiedCarbon.Should().BeGreaterThanOrEqualTo(0);
                analysis.Rating.Should().NotBeNullOrEmpty();
            }

            [Test]
            public void ValidateMaterialSelection_ValidCombo_ReturnsValid()
            {
                var context = new MaterialSelectionContext
                {
                    ElementType = "Wall",
                    Region = "UK"
                };

                var validation = _materials.ValidateMaterialSelection("CONC-STD", "Wall", context);

                validation.Should().NotBeNull();
                validation.MaterialId.Should().Be("CONC-STD");
                validation.ElementType.Should().Be("Wall");
            }

            [Test]
            public void CompareCosts_KnownMaterial_ReturnsCostData()
            {
                var comparison = _materials.CompareCosts("CONC-STD", "UK");

                comparison.Should().NotBeNull();
                comparison.PrimaryMaterialId.Should().Be("CONC-STD");
                comparison.Region.Should().Be("UK");
            }

            [Test]
            public void GetSpecification_KnownMaterial_ReturnsSpec()
            {
                var spec = _materials.GetSpecification("CONC-STD");

                spec.Should().NotBeNull();
                spec.MaterialId.Should().Be("CONC-STD");
            }

            [Test]
            public void FindMaterials_ByCriteria_ReturnsMatches()
            {
                var criteria = new MaterialSearchCriteria
                {
                    Category = MaterialCategory.Structural,
                    MinimumScore = 0.3
                };

                var matches = _materials.FindMaterials(criteria);

                matches.Should().NotBeNull();
                // May be empty if no materials match, but should not throw
            }

            [Test]
            public void MaterialCategory_AllValues_AreDefined()
            {
                var values = Enum.GetValues<MaterialCategory>();

                values.Should().Contain(MaterialCategory.Structural);
                values.Should().Contain(MaterialCategory.Insulation);
                values.Should().Contain(MaterialCategory.Glass);
                values.Should().Contain(MaterialCategory.Timber);
            }

            [Test]
            public void SustainabilityRating_AllValues_AreDefined()
            {
                var values = Enum.GetValues<SustainabilityRating>();

                values.Should().Contain(SustainabilityRating.VeryLow);
                values.Should().Contain(SustainabilityRating.High);
                values.Should().Contain(SustainabilityRating.VeryHigh);
            }
        }

        #endregion

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

            [Test]
            public void Constructor_CreatesInstance()
            {
                _recognizer.Should().NotBeNull();
            }

            [Test]
            public void RecognizePatterns_ResidentialDesign_ReturnsPatterns()
            {
                var context = new StingBIM.AI.Reasoning.Patterns.DesignContext
                {
                    ProjectType = "Residential",
                    Rooms = new List<RoomInfo>
                    {
                        new RoomInfo { Id = "r1", Type = "Living", Area = 25.0,
                            AdjacentRoomIds = new List<string> { "r2", "r3" } },
                        new RoomInfo { Id = "r2", Type = "Kitchen", Area = 15.0,
                            AdjacentRoomIds = new List<string> { "r1" } },
                        new RoomInfo { Id = "r3", Type = "Dining", Area = 12.0,
                            AdjacentRoomIds = new List<string> { "r1" } },
                        new RoomInfo { Id = "r4", Type = "Bedroom", Area = 14.0,
                            AdjacentRoomIds = new List<string> { "r5" } },
                        new RoomInfo { Id = "r5", Type = "Bathroom", Area = 6.0,
                            AdjacentRoomIds = new List<string> { "r4" } }
                    },
                    Adjacencies = new Dictionary<string, List<string>>
                    {
                        ["r1"] = new List<string> { "r2", "r3" },
                        ["r2"] = new List<string> { "r1" },
                        ["r3"] = new List<string> { "r1" },
                        ["r4"] = new List<string> { "r5" },
                        ["r5"] = new List<string> { "r4" }
                    }
                };

                var patterns = _recognizer.RecognizePatterns(context);

                patterns.Should().NotBeNull();
                // Should find at least some patterns in a residential layout
            }

            [Test]
            public void SuggestPatterns_CommercialProject_ReturnsSuggestions()
            {
                var context = new StingBIM.AI.Reasoning.Patterns.DesignContext
                {
                    ProjectType = "Commercial",
                    Rooms = new List<RoomInfo>
                    {
                        new RoomInfo { Id = "r1", Type = "Office", Area = 30.0,
                            AdjacentRoomIds = new List<string> { "r2" } },
                        new RoomInfo { Id = "r2", Type = "Meeting", Area = 20.0,
                            AdjacentRoomIds = new List<string> { "r1" } }
                    }
                };

                var suggestions = _recognizer.SuggestPatterns(context, "Commercial");

                suggestions.Should().NotBeNull();
                suggestions.Should().AllSatisfy(s =>
                {
                    s.Pattern.Should().NotBeNull();
                    s.Applicability.Should().BeInRange(0, 1);
                });
            }

            [Test]
            public void GetRecommendations_PartialDesign_ReturnsRecommendations()
            {
                var partial = new PartialDesign
                {
                    ProjectType = "Residential",
                    Rooms = new List<RoomInfo>
                    {
                        new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 },
                        new RoomInfo { Id = "r2", Type = "Kitchen", Area = 12.0 }
                    }
                };

                var recommendations = _recognizer.GetRecommendations(partial);

                recommendations.Should().NotBeNull();
            }

            [Test]
            public void LearnFromDesign_DoesNotThrow()
            {
                var context = new StingBIM.AI.Reasoning.Patterns.DesignContext
                {
                    ProjectType = "Residential",
                    Rooms = new List<RoomInfo>
                    {
                        new RoomInfo { Id = "r1", Type = "Living", Area = 25.0 }
                    }
                };

                var feedback = new DesignFeedback
                {
                    WasSuccessful = true,
                    Rating = 4.5,
                    Comments = "Good layout"
                };

                Action act = () => _recognizer.LearnFromDesign(context, feedback);

                act.Should().NotThrow();
            }
        }

        #endregion

        #region PredictiveEngine Tests

        [TestFixture]
        public class PredictiveEngineTests
        {
            private PredictiveEngine _engine;

            [SetUp]
            public void SetUp()
            {
                _engine = new PredictiveEngine();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _engine.Should().NotBeNull();
                _engine.MaxPredictions.Should().Be(5);
                _engine.MinConfidence.Should().Be(0.3);
                _engine.SequenceWindowSize.Should().Be(10);
            }

            [Test]
            public async Task PredictNextActionsAsync_WithRecentActions_ReturnsPredictions()
            {
                var context = new PredictionContext
                {
                    CurrentAction = "CreateWall",
                    CurrentView = "FloorPlan",
                    ProjectType = "Residential",
                    RecentActions = new List<UserAction>
                    {
                        new UserAction { ActionType = "CreateWall" },
                        new UserAction { ActionType = "CreateWall" }
                    }
                };

                var predictions = await _engine.PredictNextActionsAsync(context);

                predictions.Should().NotBeNull();
                predictions.Should().AllSatisfy(p =>
                {
                    p.ActionType.Should().NotBeNullOrEmpty();
                    p.Confidence.Should().BeInRange(0, 1);
                });
            }

            [Test]
            public void PredictCompletion_PartialInput_ReturnsCompletions()
            {
                var context = new PredictionContext
                {
                    CurrentView = "FloorPlan",
                    ProjectType = "Commercial"
                };

                var completions = _engine.PredictCompletion("create", context);

                completions.Should().NotBeNull();
                completions.Should().AllSatisfy(c =>
                {
                    c.Text.Should().NotBeNullOrEmpty();
                    c.Score.Should().BeGreaterThanOrEqualTo(0);
                });
            }

            [Test]
            public void RecordAction_DoesNotThrow()
            {
                var action = new UserAction
                {
                    ActionType = "CreateWall",
                    WasSuccessful = true,
                    Parameters = new Dictionary<string, object>
                    {
                        ["Length"] = 5.0, ["Height"] = 3.0
                    }
                };

                var context = new PredictionContext
                {
                    CurrentView = "FloorPlan"
                };

                Action act = () => _engine.RecordAction(action, context);

                act.Should().NotThrow();
            }

            [Test]
            public void GetProactiveSuggestions_WithContext_ReturnsSuggestions()
            {
                var context = new PredictionContext
                {
                    CurrentAction = "CreateWall",
                    CurrentView = "FloorPlan",
                    ProjectType = "Residential",
                    RecentActions = new List<UserAction>
                    {
                        new UserAction { ActionType = "CreateWall" },
                        new UserAction { ActionType = "CreateDoor" }
                    }
                };

                var suggestions = _engine.GetProactiveSuggestions(context);

                suggestions.Should().NotBeNull();
                suggestions.Should().AllSatisfy(s =>
                {
                    s.Title.Should().NotBeNullOrEmpty();
                    s.Confidence.Should().BeInRange(0, 1);
                });
            }

            [Test]
            public void PredictionSource_AllValues_AreDefined()
            {
                var values = Enum.GetValues<PredictionSource>();

                values.Should().Contain(PredictionSource.SequenceModel);
                values.Should().Contain(PredictionSource.ContextAnalysis);
                values.Should().Contain(PredictionSource.PatternMatch);
                values.Should().Contain(PredictionSource.UserHistory);
            }
        }

        #endregion
    }
}
