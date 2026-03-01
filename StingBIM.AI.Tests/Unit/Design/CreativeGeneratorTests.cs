using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Creation.Creative;
using StingBIM.AI.Design.Generative;
using StingBIM.AI.Construction.Sequencing;
using StingBIM.AI.Portfolio.Analytics;

namespace StingBIM.AI.Tests.Unit.Design
{
    /// <summary>
    /// Unit tests for AI.Design layer:
    /// CreativeGenerator (design variation, pattern/style application),
    /// GenerativeDesignEngine (genetic algorithm, multi-objective optimization),
    /// ConstructionSequencingEngine (schedule, cost, risk),
    /// PortfolioAnalyticsEngine (benchmarking, prediction, trends).
    /// All classes are standalone-testable with zero Revit dependencies.
    /// </summary>
    [TestFixture]
    public class CreativeGeneratorTests
    {
        #region CreativeGenerator Tests

        [TestFixture]
        public class CreativeGeneratorCoreTests
        {
            private CreativeGenerator _generator;

            [SetUp]
            public void SetUp()
            {
                _generator = new CreativeGenerator();
            }

            [Test]
            public void Constructor_Default_CreatesInstance()
            {
                _generator.Should().NotBeNull();
            }

            [Test]
            public void Constructor_WithConfig_CreatesInstance()
            {
                var config = new CreativeConfiguration
                {
                    MaxVariations = 20,
                    NoveltyWeight = 0.5,
                    AllowExperimental = false
                };

                var gen = new CreativeGenerator(config);

                gen.Should().NotBeNull();
            }

            [Test]
            public void GenerateVariations_SimpleLayout_ReturnsVariations()
            {
                var layout = new Layout
                {
                    LayoutId = "test-layout",
                    TotalArea = 100.0,
                    Width = 10.0,
                    Length = 10.0,
                    Spaces = new List<Space>
                    {
                        new Space { SpaceId = "s1", Name = "Living", Area = 30.0, Function = "Living" },
                        new Space { SpaceId = "s2", Name = "Kitchen", Area = 15.0, Function = "Kitchen" }
                    }
                };

                var constraints = new GenerationConstraints
                {
                    MaxArea = 120.0,
                    MinArea = 80.0,
                    AllowRotation = true,
                    AllowMirroring = true
                };

                var result = _generator.GenerateVariations(layout, constraints, count: 3);

                result.Should().NotBeNull();
                result.OriginalLayout.Should().Be(layout);
                result.Variations.Should().NotBeNull();
            }

            [Test]
            public void GenerateSpaceAlternatives_SingleSpace_ReturnsAlternatives()
            {
                var space = new Space
                {
                    SpaceId = "office-1",
                    Name = "Open Office",
                    Area = 50.0,
                    Function = "Office"
                };

                var requirements = new SpaceRequirements();

                var alternatives = _generator.GenerateSpaceAlternatives(space, requirements);

                alternatives.Should().NotBeNull();
                alternatives.OriginalSpace.Should().Be(space);
            }

            [Test]
            public void GetSuggestions_WithContext_ReturnsSuggestions()
            {
                var context = new StingBIM.AI.Creation.Creative.DesignContext();

                var suggestions = _generator.GetSuggestions(context);

                suggestions.Should().NotBeNull();
                suggestions.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            }

            [Test]
            public void BlendDesigns_TwoLayouts_ReturnsBlendResult()
            {
                var layouts = new List<Layout>
                {
                    new Layout
                    {
                        LayoutId = "layout-1",
                        TotalArea = 100.0,
                        Width = 10.0, Length = 10.0,
                        Spaces = new List<Space>
                        {
                            new Space { SpaceId = "s1", Area = 50.0, Function = "Office" }
                        }
                    },
                    new Layout
                    {
                        LayoutId = "layout-2",
                        TotalArea = 120.0,
                        Width = 12.0, Length = 10.0,
                        Spaces = new List<Space>
                        {
                            new Space { SpaceId = "s2", Area = 60.0, Function = "Office" }
                        }
                    }
                };

                var parameters = new BlendParameters();

                var result = _generator.BlendDesigns(layouts, parameters);

                result.Should().NotBeNull();
            }

            [Test]
            public void PatternCategory_AllValues_AreDefined()
            {
                var values = Enum.GetValues<PatternCategory>();

                values.Should().Contain(PatternCategory.Residential);
                values.Should().Contain(PatternCategory.Commercial);
                values.Should().Contain(PatternCategory.Healthcare);
            }

            [Test]
            public void VariationType_AllValues_AreDefined()
            {
                var values = Enum.GetValues<VariationType>();

                values.Should().Contain(VariationType.PatternBased);
                values.Should().Contain(VariationType.StyleBased);
                values.Should().Contain(VariationType.TransformBased);
                values.Should().Contain(VariationType.Random);
            }
        }

        #endregion

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

            [Test]
            public void Constructor_CreatesInstance()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void AddConstraint_DoesNotThrow()
            {
                var constraint = new DesignConstraint
                {
                    ConstraintId = "c1",
                    Name = "Max Area",
                    Category = "Space",
                    Type = ConstraintType.Maximum,
                    ParameterName = "TotalArea",
                    MaxValue = 500.0,
                    IsHard = true,
                    IsEnabled = true
                };

                Action act = () => _engine.AddConstraint(constraint);

                act.Should().NotThrow();
            }

            [Test]
            public void AddObjective_DoesNotThrow()
            {
                var objective = new DesignObjective
                {
                    ObjectiveId = "obj-1",
                    Name = "Minimize Cost",
                    Category = "Cost",
                    Direction = OptimizationDirection.Minimize,
                    Weight = 0.5,
                    IsEnabled = true
                };

                Action act = () => _engine.AddObjective(objective);

                act.Should().NotThrow();
            }

            [Test]
            public void EvaluateDesign_SingleVariant_ReturnsEvaluation()
            {
                var variant = new DesignVariant
                {
                    VariantId = "v1",
                    PatternId = "basic-grid",
                    Parameters = new Dictionary<string, double>
                    {
                        ["TotalArea"] = 200.0,
                        ["Floors"] = 2.0,
                        ["CorridorWidth"] = 1.5
                    },
                    Rooms = new List<RoomInstance>
                    {
                        new RoomInstance
                        {
                            RoomId = "r1", RoomType = "Office",
                            Area = 30.0, Width = 6.0, Height = 5.0,
                            Position = (0, 0)
                        }
                    }
                };

                var program = new DesignProgram
                {
                    ProgramId = "prog-1",
                    BuildingType = "Commercial",
                    TotalArea = 200.0,
                    Floors = 2
                };

                var evaluation = _engine.EvaluateDesign(variant, program);

                evaluation.Should().NotBeNull();
                evaluation.VariantId.Should().Be("v1");
                evaluation.OverallScore.Should().BeInRange(0, 1);
            }

            [Test]
            public void CompareVariants_MultipleVariants_ReturnsComparison()
            {
                var variants = new List<DesignVariant>
                {
                    new DesignVariant
                    {
                        VariantId = "v1",
                        Parameters = new Dictionary<string, double>
                        {
                            ["TotalArea"] = 200.0, ["Efficiency"] = 0.75
                        },
                        Rooms = new List<RoomInstance>
                        {
                            new RoomInstance { RoomId = "r1", RoomType = "Office", Area = 30.0, Width = 6.0, Height = 5.0, Position = (0, 0) }
                        }
                    },
                    new DesignVariant
                    {
                        VariantId = "v2",
                        Parameters = new Dictionary<string, double>
                        {
                            ["TotalArea"] = 220.0, ["Efficiency"] = 0.80
                        },
                        Rooms = new List<RoomInstance>
                        {
                            new RoomInstance { RoomId = "r2", RoomType = "Office", Area = 35.0, Width = 7.0, Height = 5.0, Position = (0, 0) }
                        }
                    }
                };

                var comparison = _engine.CompareVariants(variants);

                comparison.Should().NotBeNull();
                comparison.VariantCount.Should().Be(2);
                comparison.ParetoFront.Should().NotBeNull();
            }

            [Test]
            public void GetRecommendations_ForProgram_ReturnsRecommendations()
            {
                var program = new DesignProgram
                {
                    ProgramId = "prog-1",
                    BuildingType = "Residential",
                    TotalArea = 150.0,
                    Floors = 1,
                    Rooms = new List<RoomRequirement>
                    {
                        new RoomRequirement { RoomType = "Living", MinArea = 20.0, MaxArea = 35.0, Quantity = 1 },
                        new RoomRequirement { RoomType = "Bedroom", MinArea = 12.0, MaxArea = 20.0, Quantity = 2 },
                        new RoomRequirement { RoomType = "Kitchen", MinArea = 10.0, MaxArea = 18.0, Quantity = 1 }
                    }
                };

                var recommendations = _engine.GetRecommendations(program);

                recommendations.Should().NotBeNull();
            }

            [Test]
            public async Task GenerateDesignVariantsAsync_SmallPopulation_ReturnsResult()
            {
                var program = new DesignProgram
                {
                    ProgramId = "prog-1",
                    BuildingType = "Commercial",
                    TotalArea = 300.0,
                    Floors = 1,
                    Rooms = new List<RoomRequirement>
                    {
                        new RoomRequirement { RoomType = "Office", MinArea = 20.0, MaxArea = 40.0, Quantity = 4 }
                    }
                };

                var options = new GenerationOptions
                {
                    PopulationSize = 10,
                    MaxGenerations = 5,
                    ResultCount = 3
                };

                var result = await _engine.GenerateDesignVariantsAsync(program, options);

                result.Should().NotBeNull();
                result.Program.Should().Be(program);
                result.GenerationsCompleted.Should().BeGreaterThan(0);
            }

            [Test]
            public void ConstraintType_AllValues_AreDefined()
            {
                var values = Enum.GetValues<ConstraintType>();

                values.Should().Contain(ConstraintType.Minimum);
                values.Should().Contain(ConstraintType.Maximum);
                values.Should().Contain(ConstraintType.Range);
                values.Should().Contain(ConstraintType.Required);
            }

            [Test]
            public void SelectionMethod_AllValues_AreDefined()
            {
                var values = Enum.GetValues<SelectionMethod>();

                values.Should().Contain(SelectionMethod.Tournament);
                values.Should().Contain(SelectionMethod.Roulette);
                values.Should().Contain(SelectionMethod.Elite);
            }
        }

        #endregion

        #region ConstructionSequencingEngine Tests

        [TestFixture]
        public class ConstructionSequencingEngineTests
        {
            private ConstructionSequencingEngine _engine;

            [SetUp]
            public void SetUp()
            {
                _engine = new ConstructionSequencingEngine();
            }

            [Test]
            public void Constructor_CreatesInstanceWithDefaultActivities()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void GetActivitiesByPhase_SitePreparation_ReturnsActivities()
            {
                var activities = _engine.GetActivitiesByPhase(ConstructionPhase.SitePreparation);

                activities.Should().NotBeNull();
                activities.Should().NotBeEmpty("default activities should include site preparation");
            }

            [Test]
            public void GetActivitiesByPhase_AllPhases_ReturnActivities()
            {
                var allActivities = new List<ConstructionActivity>();

                foreach (ConstructionPhase phase in Enum.GetValues<ConstructionPhase>())
                {
                    var phaseActivities = _engine.GetActivitiesByPhase(phase);
                    allActivities.AddRange(phaseActivities);
                }

                allActivities.Should().NotBeEmpty("should have activities across phases");
            }

            [Test]
            public void AddActivity_CustomActivity_CanBeRetrieved()
            {
                var activity = new ConstructionActivity
                {
                    ActivityId = "CUSTOM-001",
                    Name = "Custom Inspection",
                    Phase = ConstructionPhase.Commissioning,
                    DefaultDurationDays = 2
                };

                _engine.AddActivity(activity);

                var retrieved = _engine.GetActivity("CUSTOM-001");
                retrieved.Should().NotBeNull();
                retrieved.Name.Should().Be("Custom Inspection");
            }

            [Test]
            public void CreateScheduleFromActivities_ValidActivities_ReturnsSchedule()
            {
                var phases = _engine.GetActivitiesByPhase(ConstructionPhase.SitePreparation).ToList();
                if (phases.Count == 0)
                {
                    Assert.Ignore("No default activities available");
                    return;
                }

                var activityIds = phases.Select(a => a.ActivityId).ToList();
                var startDate = new DateTime(2026, 3, 1);

                var schedule = _engine.CreateScheduleFromActivities(activityIds, startDate);

                schedule.Should().NotBeNull();
                schedule.StartDate.Should().Be(startDate);
                schedule.Activities.Should().NotBeEmpty();
            }

            [Test]
            public void AssessRisks_ForSchedule_ReturnsRiskAssessment()
            {
                var phases = _engine.GetActivitiesByPhase(ConstructionPhase.SitePreparation).ToList();
                if (phases.Count == 0)
                {
                    Assert.Ignore("No default activities available");
                    return;
                }

                var activityIds = phases.Select(a => a.ActivityId).ToList();
                var schedule = _engine.CreateScheduleFromActivities(activityIds, new DateTime(2026, 3, 1));

                var risk = _engine.AssessRisks(schedule);

                risk.Should().NotBeNull();
                risk.OverallRiskScore.Should().BeInRange(0, 1);
            }

            [Test]
            public void GetCostBreakdownByPhase_ReturnsBreakdown()
            {
                var phases = _engine.GetActivitiesByPhase(ConstructionPhase.SitePreparation).ToList();
                if (phases.Count == 0)
                {
                    Assert.Ignore("No default activities available");
                    return;
                }

                var activityIds = phases.Select(a => a.ActivityId).ToList();
                var schedule = _engine.CreateScheduleFromActivities(
                    activityIds,
                    new DateTime(2026, 3, 1),
                    new ScheduleGenerationOptions { IncludeCostEstimate = true });

                var breakdown = _engine.GetCostBreakdownByPhase(schedule);

                breakdown.Should().NotBeNull();
            }

            [Test]
            public void GetCashFlow_ReturnsProjection()
            {
                var phases = _engine.GetActivitiesByPhase(ConstructionPhase.SitePreparation).ToList();
                if (phases.Count == 0)
                {
                    Assert.Ignore("No default activities available");
                    return;
                }

                var activityIds = phases.Select(a => a.ActivityId).ToList();
                var schedule = _engine.CreateScheduleFromActivities(activityIds, new DateTime(2026, 3, 1));

                var cashFlow = _engine.GetCashFlow(schedule);

                cashFlow.Should().NotBeNull();
            }

            [Test]
            public void ConstructionPhase_AllValues_AreDefined()
            {
                var values = Enum.GetValues<ConstructionPhase>();

                values.Should().Contain(ConstructionPhase.SitePreparation);
                values.Should().Contain(ConstructionPhase.Foundation);
                values.Should().Contain(ConstructionPhase.Structure);
                values.Should().Contain(ConstructionPhase.Envelope);
                values.Should().Contain(ConstructionPhase.Commissioning);
            }

            [Test]
            public void ConstructionActivity_StoresAllFields()
            {
                var activity = new ConstructionActivity
                {
                    ActivityId = "ACT-001",
                    Name = "Excavation",
                    Phase = ConstructionPhase.SitePreparation,
                    DefaultDurationDays = 10,
                    Prerequisites = new List<string> { "SITE-CLEAR" },
                    RequiresCuring = false
                };

                activity.ActivityId.Should().Be("ACT-001");
                activity.Phase.Should().Be(ConstructionPhase.SitePreparation);
                activity.DefaultDurationDays.Should().Be(10);
            }
        }

        #endregion

        #region PortfolioAnalyticsEngine Tests

        [TestFixture]
        public class PortfolioAnalyticsEngineTests
        {
            private PortfolioAnalyticsEngine _engine;

            [SetUp]
            public void SetUp()
            {
                _engine = new PortfolioAnalyticsEngine();
            }

            [Test]
            public void Constructor_CreatesInstanceWithDefaultBenchmarks()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void AddProject_ThenGetProject_ReturnsProject()
            {
                var project = new ProjectRecord
                {
                    ProjectId = "proj-001",
                    Name = "Office Tower",
                    BuildingType = "Commercial",
                    Region = "East Africa",
                    Status = ProjectStatus.Completed,
                    SuccessScore = 85.0,
                    StartDate = new DateTime(2024, 1, 1),
                    CompletionDate = new DateTime(2025, 6, 1),
                    Metrics = new Dictionary<string, double>
                    {
                        ["CostPerSqm"] = 1200.0,
                        ["DurationMonths"] = 18.0,
                        ["EnergyPerSqm"] = 120.0
                    }
                };

                _engine.AddProject(project);

                var retrieved = _engine.GetProject("proj-001");
                retrieved.Should().NotBeNull();
                retrieved.Name.Should().Be("Office Tower");
                retrieved.SuccessScore.Should().Be(85.0);
            }

            [Test]
            public void GetProjectsByType_AfterAdding_ReturnsFilteredProjects()
            {
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "p1", BuildingType = "Commercial",
                    Name = "Office A", Status = ProjectStatus.Completed
                });
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "p2", BuildingType = "Residential",
                    Name = "House B", Status = ProjectStatus.Completed
                });
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "p3", BuildingType = "Commercial",
                    Name = "Office C", Status = ProjectStatus.Completed
                });

                var commercial = _engine.GetProjectsByType("Commercial").ToList();

                commercial.Should().HaveCount(2);
                commercial.Should().AllSatisfy(p => p.BuildingType.Should().Be("Commercial"));
            }

            [Test]
            public void GetProjectsByRegion_AfterAdding_ReturnsFilteredProjects()
            {
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "p1", Region = "East Africa",
                    Name = "Nairobi Office", BuildingType = "Commercial"
                });
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "p2", Region = "UK",
                    Name = "London Office", BuildingType = "Commercial"
                });

                var eastAfrica = _engine.GetProjectsByRegion("East Africa").ToList();

                eastAfrica.Should().HaveCount(1);
                eastAfrica.First().ProjectId.Should().Be("p1");
            }

            [Test]
            public void BenchmarkProject_WithMetrics_ReturnsBenchmarkComparison()
            {
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "bench-001",
                    Name = "Test Office",
                    BuildingType = "Commercial",
                    Status = ProjectStatus.Completed,
                    Metrics = new Dictionary<string, double>
                    {
                        ["CostPerSqm"] = 1500.0,
                        ["DurationMonths"] = 24.0,
                        ["EnergyPerSqm"] = 150.0
                    }
                });

                var benchmark = _engine.BenchmarkProject("bench-001");

                benchmark.Should().NotBeNull();
                benchmark.ProjectId.Should().Be("bench-001");
            }

            [Test]
            public void PredictCost_ForCharacteristics_ReturnsPrediction()
            {
                // Add some reference projects first
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "ref-1", BuildingType = "Commercial",
                    Name = "Reference 1", Status = ProjectStatus.Completed,
                    Metrics = new Dictionary<string, double> { ["CostPerSqm"] = 1200.0 }
                });

                var characteristics = new ProjectCharacteristics
                {
                    BuildingType = "Commercial",
                    GrossArea = 3000.0,
                    Floors = 3,
                    Region = "East Africa"
                };

                var prediction = _engine.PredictCost(characteristics);

                prediction.Should().NotBeNull();
                prediction.PredictedCostPerSqm.Should().BeGreaterThan(0);
                prediction.PredictedTotalCost.Should().BeGreaterThan(0);
            }

            [Test]
            public void PredictSchedule_ForCharacteristics_ReturnsPrediction()
            {
                var characteristics = new ProjectCharacteristics
                {
                    BuildingType = "Residential",
                    GrossArea = 200.0,
                    Floors = 2
                };

                var prediction = _engine.PredictSchedule(characteristics);

                prediction.Should().NotBeNull();
                prediction.PredictedDurationDays.Should().BeGreaterThan(0);
            }

            [Test]
            public void PredictRisks_ForCharacteristics_ReturnsPrediction()
            {
                var characteristics = new ProjectCharacteristics
                {
                    BuildingType = "Commercial",
                    GrossArea = 5000.0,
                    Floors = 5
                };

                var prediction = _engine.PredictRisks(characteristics);

                prediction.Should().NotBeNull();
                prediction.OverallRiskScore.Should().BeInRange(0, 1);
            }

            [Test]
            public void AnalyzeTrends_ReturnsAnalysis()
            {
                var result = _engine.AnalyzeTrends();

                result.Should().NotBeNull();
            }

            [Test]
            public void GetDashboard_ReturnsDashboard()
            {
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "dash-1", Name = "Project A",
                    BuildingType = "Commercial", Status = ProjectStatus.Completed,
                    Metrics = new Dictionary<string, double> { ["CostPerSqm"] = 1000.0 }
                });

                var dashboard = _engine.GetDashboard();

                dashboard.Should().NotBeNull();
                dashboard.TotalProjects.Should().BeGreaterThan(0);
                dashboard.GeneratedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            }

            [Test]
            public void FindSimilarProjects_ReturnsResults()
            {
                _engine.AddProject(new ProjectRecord
                {
                    ProjectId = "sim-1", Name = "Similar Office",
                    BuildingType = "Commercial", Region = "East Africa",
                    Status = ProjectStatus.Completed,
                    Metrics = new Dictionary<string, double> { ["CostPerSqm"] = 1100.0 }
                });

                var characteristics = new ProjectCharacteristics
                {
                    BuildingType = "Commercial",
                    GrossArea = 3000.0,
                    Region = "East Africa"
                };

                var similar = _engine.FindSimilarProjects(characteristics);

                similar.Should().NotBeNull();
            }

            [Test]
            public void GetBestPractices_ForBuildingType_ReturnsReport()
            {
                var report = _engine.GetBestPractices("Commercial");

                report.Should().NotBeNull();
                report.BuildingType.Should().Be("Commercial");
            }

            [Test]
            public void ProjectStatus_AllValues_AreDefined()
            {
                var values = Enum.GetValues<ProjectStatus>();

                values.Should().Contain(ProjectStatus.Planning);
                values.Should().Contain(ProjectStatus.Design);
                values.Should().Contain(ProjectStatus.Construction);
                values.Should().Contain(ProjectStatus.Completed);
            }

            [Test]
            public void PerformanceRating_AllValues_AreDefined()
            {
                var values = Enum.GetValues<PerformanceRating>();

                values.Should().Contain(PerformanceRating.Excellent);
                values.Should().Contain(PerformanceRating.Good);
                values.Should().Contain(PerformanceRating.Average);
                values.Should().Contain(PerformanceRating.BelowAverage);
            }

            [Test]
            public void TrendDirection_AllValues_AreDefined()
            {
                var values = Enum.GetValues<TrendDirection>();

                values.Should().Contain(TrendDirection.Increasing);
                values.Should().Contain(TrendDirection.Stable);
                values.Should().Contain(TrendDirection.Decreasing);
            }
        }

        #endregion
    }
}
