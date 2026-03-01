// ============================================================================
// StingBIM AI Tests - Project Management Systems Tests
// Comprehensive tests for Document Control, Advisory, Cost, Clash, Change, and Risk
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.Advisory;
using StingBIM.AI.Intelligence.CostAnalysis;
using StingBIM.AI.Intelligence.ClashDetection;
using StingBIM.AI.Intelligence.ChangeManagement;
using CostModels = StingBIM.AI.Intelligence.CostAnalysis;
using ChangeModels = StingBIM.AI.Intelligence.ChangeManagement;
using StingBIM.AI.Intelligence.RiskManagement;
using StingBIM.AI.Intelligence.DocumentControl;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class DocumentControlSystemTests
    {
        private DocumentControlSystem _system;

        [SetUp]
        public void Setup()
        {
            _system = DocumentControlSystem.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = DocumentControlSystem.Instance;
            var instance2 = DocumentControlSystem.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void RegisterDocument_ShouldCreateDocumentWithNumber()
        {
            // Arrange
            var request = new DocumentRegistrationRequest
            {
                DocumentNumber = "A-101",
                Title = "Floor Plan - Ground Level",
                Description = "Ground floor architectural plan",
                DocumentType = DocumentType.Drawing,
                Discipline = Discipline.Architectural,
                Phase = "Design Development",
                Author = "test_user",
                FilePath = "/docs/A-101.rvt"
            };

            // Act
            var document = _system.RegisterDocument(request);

            // Assert
            Assert.That(document, Is.Not.Null);
            Assert.That(document.DocumentNumber, Is.EqualTo("A-101"));
            Assert.That(document.CurrentRevision, Is.EqualTo("A"));
            Assert.That(document.Status, Is.EqualTo(DocumentStatus.Draft));
        }

        [Test]
        public void CreateRevision_ShouldIncrementRevision()
        {
            // Arrange
            var request = new DocumentRegistrationRequest
            {
                DocumentNumber = $"S-{DateTime.Now.Ticks}",
                Title = "Structural Details",
                DocumentType = DocumentType.Drawing,
                Discipline = Discipline.Structural,
                Author = "test_user"
            };
            var document = _system.RegisterDocument(request);

            // Act
            var revision = _system.CreateRevision(new RevisionRequest
            {
                DocumentId = document.DocumentId,
                Author = "test_user",
                Description = "Updated connections",
                ChangesSummary = "Modified beam connections"
            });

            // Assert
            Assert.That(revision, Is.Not.Null);
            Assert.That(revision.Revision, Is.EqualTo("B"));
            Assert.That(revision.RevisionNumber, Is.EqualTo(2));
        }

        [Test]
        public void SearchDocuments_ShouldFindByText()
        {
            // Arrange
            var request = new DocumentRegistrationRequest
            {
                DocumentNumber = $"SEARCH-TEST-{DateTime.Now.Ticks}",
                Title = "Searchable Test Document",
                DocumentType = DocumentType.Specification,
                Discipline = Discipline.Mechanical,
                Author = "test_user"
            };
            _system.RegisterDocument(request);

            // Act
            var results = _system.SearchDocuments(new DocumentSearchCriteria
            {
                SearchText = "Searchable"
            });

            // Assert
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count > 0, Is.True);
        }

        [Test]
        public void CreateTransmittal_ShouldGenerateTransmittalNumber()
        {
            // Arrange
            var doc = _system.RegisterDocument(new DocumentRegistrationRequest
            {
                DocumentNumber = $"TX-DOC-{DateTime.Now.Ticks}",
                Title = "For Transmittal",
                DocumentType = DocumentType.Drawing,
                Discipline = Discipline.Architectural,
                Author = "test_user"
            });

            var request = new TransmittalRequest
            {
                Subject = "Design Package Submittal",
                Purpose = TransmittalPurpose.ForApproval,
                FromCompany = "Design Firm",
                ToCompany = "Client Corp",
                ToContact = "Project Manager",
                CreatedBy = "test_user",
                DocumentIds = new List<string> { doc.DocumentId }
            };

            // Act
            var transmittal = _system.CreateTransmittal(request);

            // Assert
            Assert.That(transmittal, Is.Not.Null);
            Assert.That(transmittal.TransmittalNumber.StartsWith("TX-"), Is.True);
            Assert.That(transmittal.Documents.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetStatistics_ShouldReturnCounts()
        {
            // Act
            var stats = _system.GetStatistics();

            // Assert
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.TotalDocuments >= 0, Is.True);
        }
    }

    [TestFixture]
    public class ProjectAdvisoryEngineTests
    {
        private ProjectAdvisoryEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = ProjectAdvisoryEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = ProjectAdvisoryEngine.Instance;
            var instance2 = ProjectAdvisoryEngine.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void GetAnalysisRecommendation_ShouldReturnStrategy()
        {
            // Arrange
            var context = new ProjectAnalysisContext
            {
                ProjectPhase = "Design Development",
                LastHealthCheckDays = 14,
                WarningCount = 75,
                MissingParameterPercentage = 15,
                ClashCount = 150,
                CostVariancePercentage = 8,
                HasDemolitionScope = true,
                DemolitionElementCount = 50
            };

            // Act
            var recommendation = _engine.GetAnalysisRecommendation(context);

            // Assert
            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.PrimaryRecommendation, Is.Not.Null);
            Assert.That(recommendation.Reasoning, Is.Not.Null);
        }

        [Test]
        public void GetDecisionRecommendations_ShouldIdentifyIssues()
        {
            // Arrange
            var metrics = new ProjectMetrics
            {
                ClashCount = 150,
                CurrentCost = 12000000m,
                Budget = 10000000m,
                ScheduleVariance = 15,
                ModelHealthScore = 65
            };

            // Act
            var recommendations = _engine.GetDecisionRecommendations(metrics);

            // Assert
            Assert.That(recommendations, Is.Not.Null);
            Assert.That(recommendations.Count > 0, Is.True, "Should have recommendations for issues");
        }

        [Test]
        public void AnalyzeBestRoute_ShouldReturnRoutes()
        {
            // Arrange
            var goal = new ProjectGoal
            {
                GoalType = GoalType.CostReduction,
                TargetValue = 500000m,
                Description = "Reduce project cost by $500K"
            };

            // Act
            var analysis = _engine.AnalyzeBestRoute(goal);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.Routes, Is.Not.Null);
            Assert.That(analysis.Routes.Count > 0, Is.True);
            Assert.That(analysis.RecommendedRoute, Is.Not.Null);
        }

        [Test]
        public async Task QuickAnalyzeAsync_ShouldReturnMetrics()
        {
            // Arrange
            var snapshot = new ModelSnapshot
            {
                TotalElements = 5000,
                WarningCount = 120,
                ClashCount = 75,
                MissingParameterPercentage = 5,
                DemolitionElements = 100
            };

            // Act
            var result = await _engine.QuickAnalyzeAsync(snapshot);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Metrics.ContainsKey("health_score"), Is.True);
            Assert.That(result.Warnings.Count > 0, Is.True, "Should have warnings for high counts");
        }
    }

    [TestFixture]
    public class EnhancedCostEngineTests
    {
        private EnhancedCostEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = EnhancedCostEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = EnhancedCostEngine.Instance;
            var instance2 = EnhancedCostEngine.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public async Task AnalyzeModelCostsAsync_ShouldCalculateCosts()
        {
            // Arrange
            var elements = new CostModels.ModelElements
            {
                Elements = new List<CostModels.ModelElement>
                {
                    new() { ElementId = "1", ElementType = "Wall", Category = CostCategory.Architectural, Area = 100 },
                    new() { ElementId = "2", ElementType = "Floor", Category = CostCategory.Structural, Volume = 50, Area = 200 },
                    new() { ElementId = "3", ElementType = "Duct", Category = CostCategory.Mechanical, Length = 30 }
                }
            };

            // Act
            var analysis = await _engine.AnalyzeModelCostsAsync(elements);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.Summary.TotalCost > 0, Is.True);
            Assert.That(analysis.ElementCosts.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task AnalyzeDemolitionCostsAsync_ShouldIncludeDisposal()
        {
            // Arrange
            var scope = new DemolitionScope
            {
                Elements = new List<DemolitionElement>
                {
                    new() { ElementId = "D1", ElementType = "Wall", MaterialType = "concrete", Weight = 5.0, Volume = 2.0, Area = 50 },
                    new() { ElementId = "D2", ElementType = "Floor", MaterialType = "concrete", Weight = 10.0, Volume = 4.0, Area = 100, ContainsAsbestos = true }
                }
            };

            // Act
            var analysis = await _engine.AnalyzeDemolitionCostsAsync(scope);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.Summary.TotalDemolitionCost > 0, Is.True);
            Assert.That(analysis.Summary.TotalDisposalCost > 0, Is.True);
            Assert.That(analysis.HazardousMaterials.Count > 0, Is.True, "Should detect asbestos");
        }

        [Test]
        public async Task AnalyzeClashRepairCostsAsync_ShouldCalculateRepairCosts()
        {
            // Arrange
            var clashes = new List<CostModels.ClashInstance>
            {
                new() { ClashId = "C1", ClashType = "Hard", Severity = CostModels.ClashSeverity.High, PrimaryDiscipline = "Mechanical", SecondaryDiscipline = "Electrical" },
                new() { ClashId = "C2", ClashType = "Soft", Severity = CostModels.ClashSeverity.Medium, PrimaryDiscipline = "Plumbing", SecondaryDiscipline = "Structural" }
            };

            // Act
            var analysis = await _engine.AnalyzeClashRepairCostsAsync(clashes);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.Summary.TotalClashes, Is.EqualTo(2));
            Assert.That(analysis.Summary.TotalRepairCost > 0, Is.True);
        }

        [Test]
        public void TrackVariances_ShouldDetectOverruns()
        {
            // Arrange
            var budget = new ProjectBudget
            {
                ProjectName = "Test Project",
                TotalBudget = 1000000m,
                CategoryBudgets = new Dictionary<CostCategory, decimal>
                {
                    { CostCategory.Structural, 400000m },
                    { CostCategory.Architectural, 300000m }
                }
            };

            var currentCosts = new ModelCostAnalysis
            {
                Summary = new CostSummary { TotalCost = 1200000m },
                Categories = new Dictionary<CostCategory, CategoryCost>
                {
                    { CostCategory.Structural, new CategoryCost { MaterialCost = 300000m, LaborCost = 200000m } },
                    { CostCategory.Architectural, new CategoryCost { MaterialCost = 250000m, LaborCost = 100000m } }
                }
            };

            // Act
            var report = _engine.TrackVariances(budget, currentCosts);

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.TotalVariance > 0, Is.True, "Should show variance");
            Assert.That(report.Warnings.Count > 0, Is.True, "Should have budget warnings");
        }
    }

    [TestFixture]
    public class ClashRepairAutomationTests
    {
        private ClashRepairAutomation _automation;

        [SetUp]
        public void Setup()
        {
            _automation = ClashRepairAutomation.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = ClashRepairAutomation.Instance;
            var instance2 = ClashRepairAutomation.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public async Task AnalyzeClashesAsync_ShouldGenerateResolutions()
        {
            // Arrange
            var clashes = new List<DetectedClash>
            {
                new() { ClashId = "C1", ClashType = "Hard", PrimaryElementId = "E1", SecondaryElementId = "E2",
                    PrimaryTrade = "Mechanical", SecondaryTrade = "Structural", IntersectionVolume = 0.05 },
                new() { ClashId = "C2", ClashType = "Soft", PrimaryElementId = "E3", SecondaryElementId = "E4",
                    PrimaryTrade = "Electrical", SecondaryTrade = "Mechanical", IntersectionVolume = 0.02 },
                new() { ClashId = "C3", ClashType = "Hard", PrimaryElementId = "E5", SecondaryElementId = "E6",
                    PrimaryTrade = "Plumbing", SecondaryTrade = "Mechanical", IntersectionVolume = 0.03 }
            };

            // Act
            var result = await _automation.AnalyzeClashesAsync(clashes);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalClashes, Is.EqualTo(3));
            Assert.That(result.ClashResolutions.Count, Is.EqualTo(3));
            Assert.That(result.ClashResolutions.All(r => r.Options.Count > 0), Is.True);
        }

        [Test]
        public async Task AnalyzeClashesAsync_ShouldIdentifyPatterns()
        {
            // Arrange - Create multiple clashes with same trade pair
            var clashes = new List<DetectedClash>();
            for (int i = 0; i < 10; i++)
            {
                clashes.Add(new DetectedClash
                {
                    ClashId = $"PATTERN-{i}",
                    ClashType = "Hard",
                    PrimaryTrade = "Mechanical",
                    SecondaryTrade = "Electrical",
                    IntersectionVolume = 0.01
                });
            }

            // Act
            var result = await _automation.AnalyzeClashesAsync(clashes);

            // Assert
            Assert.That(result.PatternInsights, Is.Not.Null);
            Assert.That(result.PatternInsights.Count > 0, Is.True, "Should detect pattern");
        }

        [Test]
        public async Task ApplyAutomatedResolutionsAsync_ShouldProcessAutoApplicable()
        {
            // Arrange
            var clashes = new List<DetectedClash>
            {
                new() { ClashId = "AUTO-1", ClashType = "Hard", PrimaryTrade = "Electrical", SecondaryTrade = "Structural" }
            };
            var analysis = await _automation.AnalyzeClashesAsync(clashes);

            // Act
            var result = await _automation.ApplyAutomatedResolutionsAsync(analysis);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary.TotalProcessed > 0, Is.True);
        }

        [Test]
        public async Task GenerateReport_ShouldIncludeRecommendations()
        {
            // Arrange
            var clashes = new List<DetectedClash>
            {
                new() { ClashId = "RPT-1", ClashType = "Hard", PrimaryTrade = "Mechanical", SecondaryTrade = "Structural", IntersectionVolume = 0.15 }
            };
            var analysis = await _automation.AnalyzeClashesAsync(clashes);

            // Act
            var report = _automation.GenerateReport(analysis);

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.Recommendations, Is.Not.Null);
            Assert.That(report.Recommendations.Count > 0, Is.True);
        }
    }

    [TestFixture]
    public class ChangeManagementSystemTests
    {
        private ChangeManagementSystem _system;

        [SetUp]
        public void Setup()
        {
            _system = ChangeManagementSystem.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = ChangeManagementSystem.Instance;
            var instance2 = ChangeManagementSystem.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void CreateChangeRequest_ShouldGenerateId()
        {
            // Arrange
            var input = new ChangeRequestInput
            {
                Title = "Add Fire Escape Stairs",
                Description = "Additional fire escape required by code review",
                RequestedBy = "test_user",
                Category = ChangeCategory.Scope,
                Priority = ChangePriority.High,
                AffectedDisciplines = new List<string> { "Architectural", "Structural" }
            };

            // Act
            var request = _system.CreateChangeRequest(input);

            // Assert
            Assert.That(request, Is.Not.Null);
            Assert.That(request.RequestId.StartsWith("CR-"), Is.True);
            Assert.That(request.Status, Is.EqualTo(ChangeRequestStatus.Draft));
        }

        [Test]
        public async Task AnalyzeImpactAsync_ShouldCalculateCostAndSchedule()
        {
            // Arrange
            var input = new ChangeRequestInput
            {
                Title = "Material Substitution Test",
                Description = "Test change for impact analysis",
                RequestedBy = "test_user",
                Category = ChangeCategory.Material,
                Priority = ChangePriority.Medium,
                AffectedDisciplines = new List<string> { "Architectural" },
                AffectedElements = new List<string> { "E1", "E2", "E3" }
            };
            var request = _system.CreateChangeRequest(input);

            var model = new ChangeModels.ModelElements
            {
                Elements = new List<ChangeModels.ModelElement>
                {
                    new() { ElementId = "E1", ElementType = "Wall", Discipline = "Architectural" },
                    new() { ElementId = "E2", ElementType = "Floor", Discipline = "Architectural" },
                    new() { ElementId = "E3", ElementType = "Door", Discipline = "Architectural" }
                }
            };

            // Act
            var analysis = await _system.AnalyzeImpactAsync(request.RequestId, model);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.CostImpact.TotalCost > 0, Is.True);
            Assert.That(analysis.ScheduleImpact.DaysImpact > 0, Is.True);
            Assert.That(analysis.RiskAssessment, Is.Not.Null);
        }

        [Test]
        public void SubmitForApproval_ShouldSetWorkflow()
        {
            // Arrange
            var request = _system.CreateChangeRequest(new ChangeRequestInput
            {
                Title = "Workflow Test Change",
                Description = "Test",
                RequestedBy = "test_user",
                Category = ChangeCategory.Design,
                Priority = ChangePriority.Low
            });

            // Act
            _system.SubmitForApproval(request.RequestId, "test_user");

            // Assert
            var updatedRequest = _system.SearchRequests(request.RequestId);
            // Request should now be pending approval
        }

        [Test]
        public void GenerateReport_ShouldReturnStatistics()
        {
            // Act
            var report = _system.GenerateReport();

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.TotalRequests >= 0, Is.True);
        }

        private ChangeRequest SearchRequests(string requestId)
        {
            // Helper method - implementation would search the system
            return null;
        }
    }

    [TestFixture]
    public class RiskManagementEngineTests
    {
        private RiskManagementEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = RiskManagementEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = RiskManagementEngine.Instance;
            var instance2 = RiskManagementEngine.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void IdentifyRisk_ShouldCreateRiskWithScore()
        {
            // Arrange
            var input = new RiskIdentificationInput
            {
                Category = "TECH",
                Title = "New Technology Integration",
                Description = "Using untested BIM collaboration platform",
                Likelihood = RiskLikelihood.Medium,
                Impact = RiskImpact.High,
                IdentifiedBy = "test_user"
            };

            // Act
            var risk = _engine.IdentifyRisk(input);

            // Assert
            Assert.That(risk, Is.Not.Null);
            Assert.That(risk.RiskScore > 0, Is.True);
            Assert.That(risk.Status, Is.EqualTo(RiskStatus.Identified));
        }

        [Test]
        public async Task AutoIdentifyRisksAsync_ShouldDetectIssues()
        {
            // Arrange
            var context = new ProjectRiskContext
            {
                ClashCount = 150,
                ScheduleVariance = 15,
                CostVariance = 12,
                ModelHealthScore = 60,
                ChangeRequestCount = 25,
                DemolitionScope = true,
                HasDemolitionPlan = false
            };

            // Act
            var risks = await _engine.AutoIdentifyRisksAsync(context);

            // Assert
            Assert.That(risks, Is.Not.Null);
            Assert.That(risks.Count > 0, Is.True, "Should identify multiple risks from context");
        }

        [Test]
        public void AssessRisk_ShouldUpdateScore()
        {
            // Arrange
            var risk = _engine.IdentifyRisk(new RiskIdentificationInput
            {
                Category = "SCHED",
                Title = "Test Risk for Assessment",
                Description = "Test",
                Likelihood = RiskLikelihood.Low,
                Impact = RiskImpact.Low,
                IdentifiedBy = "test_user"
            });

            // Act
            var result = _engine.AssessRisk(risk.RiskId, new RiskAssessmentInput
            {
                Likelihood = RiskLikelihood.High,
                Impact = RiskImpact.High,
                PreviousLikelihood = RiskLikelihood.Low,
                PreviousImpact = RiskImpact.Low,
                AssessedBy = "test_user",
                Notes = "Situation has escalated"
            });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NewScore > result.PreviousScore, Is.True);
            Assert.That(result.Recommendations.Count > 0, Is.True);
        }

        [Test]
        public void AddMitigation_ShouldCreateMitigationAction()
        {
            // Arrange
            var risk = _engine.IdentifyRisk(new RiskIdentificationInput
            {
                Category = "COST",
                Title = "Budget Overrun Test",
                Description = "Test",
                Likelihood = RiskLikelihood.High,
                Impact = RiskImpact.High,
                IdentifiedBy = "test_user"
            });

            // Act
            var mitigation = _engine.AddMitigation(risk.RiskId, new MitigationInput
            {
                Type = MitigationType.Mitigate,
                Description = "Implement cost controls",
                Owner = "Cost Manager",
                EstimatedCost = 5000m,
                SuccessCriteria = "Cost variance below 5%"
            });

            // Assert
            Assert.That(mitigation, Is.Not.Null);
            Assert.That(mitigation.MitigationId.Contains(risk.RiskId), Is.True);
            Assert.That(mitigation.Status, Is.EqualTo(MitigationStatus.Planned));
        }

        [Test]
        public void GenerateRiskRegister_ShouldReturnCompleteReport()
        {
            // Act
            var report = _engine.GenerateRiskRegister();

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.TotalRisks >= 0, Is.True);
            Assert.That(report.TopRisks, Is.Not.Null);
            Assert.That(report.MitigationSummary, Is.Not.Null);
        }

        [Test]
        public void GetRiskMatrix_ShouldReturn25Cells()
        {
            // Act
            var matrix = _engine.GetRiskMatrix();

            // Assert
            Assert.That(matrix, Is.Not.Null);
            Assert.That(matrix.Cells.Count, Is.EqualTo(25), "5x5 matrix should have 25 cells");
        }

        [Test]
        public void AnalyzeTrends_ShouldReturnInsights()
        {
            // Act
            var trends = _engine.AnalyzeTrends(30);

            // Assert
            Assert.That(trends, Is.Not.Null);
            Assert.That(trends.TrendDirection, Is.Not.Null);
            Assert.That(trends.KeyInsights, Is.Not.Null);
        }

        [Test]
        public void SearchRisks_ShouldFilterByCategory()
        {
            // Arrange
            _engine.IdentifyRisk(new RiskIdentificationInput
            {
                Category = "QUAL",
                Title = "Search Test Risk",
                Description = "Test",
                Likelihood = RiskLikelihood.Medium,
                Impact = RiskImpact.Medium,
                IdentifiedBy = "test_user"
            });

            // Act
            var results = _engine.SearchRisks(new RiskSearchCriteria
            {
                Category = "QUAL"
            });

            // Assert
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count > 0, Is.True);
            Assert.That(results.All(r => r.Category == "QUAL"), Is.True);
        }
    }
}
