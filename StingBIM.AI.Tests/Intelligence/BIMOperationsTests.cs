// ============================================================================
// StingBIM AI Tests - BIM Operations Intelligence Tests
// Comprehensive tests for BIM Consultant, Revit Intelligence, Coordination,
// Document Control, Standards Enforcer, and Project Delivery modules
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.BIMConsultant;
using StingBIM.AI.Intelligence.RevitIntelligence;
using StingBIM.AI.Intelligence.ModelCoordination;
using StingBIM.AI.Intelligence.DocumentControl;
using StingBIM.AI.Intelligence.StandardsEnforcer;
using StingBIM.AI.Intelligence.ProjectDelivery;

namespace StingBIM.AI.Tests.Intelligence
{
    #region BIM Consultant Engine Tests

    [TestFixture]
    public class BIMConsultantEngineTests
    {
        private BIMConsultantEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = BIMConsultantEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = BIMConsultantEngine.Instance;
            var instance2 = BIMConsultantEngine.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void CreateProject_ShouldCreateWithMilestones()
        {
            var request = new BIMProjectRequest
            {
                ProjectName = "Test BIM Project",
                ProjectNumber = "BIM-001",
                ClientName = "Test Client",
                ProjectType = "Commercial",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(18),
                BIMLevel = 2
            };

            var project = _engine.CreateProject(request);

            Assert.That(project, Is.Not.Null);
            Assert.That(project.ProjectName, Is.EqualTo("Test BIM Project"));
            Assert.That(project.Milestones.Count >= 6, Is.True);
        }

        [Test]
        public async Task GenerateBEPAsync_ShouldGenerateCompleteBEP()
        {
            var project = _engine.CreateProject(new BIMProjectRequest
            {
                ProjectName = "BEP Test Project",
                ProjectNumber = "BEP-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var bep = await _engine.GenerateBEPAsync(project.ProjectId, new BEPRequest());

            Assert.That(bep, Is.Not.Null);
            Assert.That(bep.Goals, Is.Not.Null);
            Assert.That(bep.Goals.Count > 0, Is.True);
            Assert.That(bep.LODMatrix, Is.Not.Null);
            Assert.That(bep.ProcessDesign, Is.Not.Null);
            Assert.That(bep.TechnologyPlan, Is.Not.Null);
        }

        [Test]
        public void GetRecommendations_ShouldReturnRecommendations()
        {
            var project = _engine.CreateProject(new BIMProjectRequest
            {
                ProjectName = "Recommendations Test",
                ProjectNumber = "REC-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(6)
            });

            var recommendations = _engine.GetRecommendations(project.ProjectId);

            Assert.That(recommendations, Is.Not.Null);
            Assert.That(recommendations.Count > 0, Is.True);
        }

        [Test]
        public void GetStandards_ShouldReturnBIMStandards()
        {
            var standards = _engine.GetStandards();

            Assert.That(standards, Is.Not.Null);
            Assert.That(standards.Any(s => s.Code == "ISO19650-1"), Is.True);
            Assert.That(standards.Any(s => s.Code == "COBie"), Is.True);
        }

        [Test]
        public void GetRoles_ShouldReturnProjectRoles()
        {
            var roles = _engine.GetRoles();

            Assert.That(roles, Is.Not.Null);
            Assert.That(roles.Any(r => r.Title == "BIM Manager"), Is.True);
            Assert.That(roles.Any(r => r.Title == "BIM Coordinator"), Is.True);
        }

        [Test]
        public void AnalyzeProject_ShouldReturnSWOTAnalysis()
        {
            var project = _engine.CreateProject(new BIMProjectRequest
            {
                ProjectName = "Analysis Test",
                ProjectNumber = "ANA-001",
                BIMLevel = 2,
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var analysis = _engine.AnalyzeProject(project.ProjectId);

            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.Strengths, Is.Not.Null);
            Assert.That(analysis.Weaknesses, Is.Not.Null);
            Assert.That(analysis.Opportunities, Is.Not.Null);
        }
    }

    #endregion

    #region Revit Intelligence Engine Tests

    [TestFixture]
    public class RevitIntelligenceEngineTests
    {
        private RevitIntelligenceEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = RevitIntelligenceEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = RevitIntelligenceEngine.Instance;
            var instance2 = RevitIntelligenceEngine.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public async Task AnalyzeModelHealthAsync_ShouldReturnHealthReport()
        {
            var request = new ModelHealthRequest
            {
                ModelPath = "C:\\Test\\Model.rvt",
                ModelName = "Test Model",
                ModelData = new ModelData
                {
                    FileSize = 250,
                    ElementCount = 50000,
                    WarningCount = 50,
                    ViewCount = 200
                }
            };

            var report = await _engine.AnalyzeModelHealthAsync(request);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.OverallScore >= 0, Is.True);
            Assert.That(report.OverallScore <= 100, Is.True);
            Assert.That(report.Metrics, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeModelHealthAsync_ShouldFlagPerformanceIssues()
        {
            var request = new ModelHealthRequest
            {
                ModelPath = "C:\\Test\\LargeModel.rvt",
                ModelName = "Large Model",
                ModelData = new ModelData
                {
                    FileSize = 600, // Over threshold
                    WarningCount = 500, // Over threshold
                    InPlaceFamilyCount = 30 // Over threshold
                }
            };

            var report = await _engine.AnalyzeModelHealthAsync(request);

            Assert.That(report.Issues.Count > 0, Is.True);
            Assert.That(report.OverallScore < 80, Is.True);
        }

        [Test]
        public void AuditFamilies_ShouldReturnAuditReport()
        {
            var request = new FamilyAuditRequest
            {
                ModelName = "Test Model",
                Families = new List<FamilyInfo>
                {
                    new FamilyInfo { FamilyId = "F1", FamilyName = "Door_SingleFlush", Category = "Doors", InstanceCount = 50, FileSize = 0.5 },
                    new FamilyInfo { FamilyId = "F2", FamilyName = "Unused Family", Category = "Generic", InstanceCount = 0, FileSize = 3.0 },
                    new FamilyInfo { FamilyId = "F3", FamilyName = "InPlace Test", Category = "Generic", IsInPlace = true, InstanceCount = 1 }
                }
            };

            var report = _engine.AuditFamilies(request);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.Summary.TotalFamilies, Is.EqualTo(3));
            Assert.That(report.Summary.UnusedFamilies, Is.EqualTo(1));
            Assert.That(report.Summary.InPlaceFamilies, Is.EqualTo(1));
        }

        [Test]
        public void CreateViewTemplateLibrary_ShouldCreateLibrary()
        {
            var request = new ViewTemplateLibraryRequest
            {
                ProjectId = "TEST-001"
            };

            var library = _engine.CreateViewTemplateLibrary(request);

            Assert.That(library, Is.Not.Null);
            Assert.That(library.Templates.Count > 0, Is.True);
            Assert.That(library.Templates.Any(t => t.Discipline == "Architecture"), Is.True);
            Assert.That(library.Templates.Any(t => t.Discipline == "Mechanical"), Is.True);
        }

        [Test]
        public void GetWorksetStrategy_ShouldReturnStrategy()
        {
            var strategy = _engine.GetWorksetStrategy("Standard");

            Assert.That(strategy, Is.Not.Null);
            Assert.That(strategy.Worksets.Count > 0, Is.True);
            Assert.That(strategy.Worksets.Any(w => w.Name.Contains("Links")), Is.True);
        }

        [Test]
        public void GetPerformanceOptimizations_ShouldReturnOptimizations()
        {
            var modelData = new ModelData
            {
                FileSize = 400,
                WarningCount = 200,
                InPlaceFamilyCount = 25
            };

            var optimizations = _engine.GetPerformanceOptimizations(modelData);

            Assert.That(optimizations, Is.Not.Null);
            Assert.That(optimizations.Count > 0, Is.True);
        }
    }

    #endregion

    #region Model Coordination Engine Tests

    [TestFixture]
    public class ModelCoordinationEngineTests
    {
        private ModelCoordinationEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = ModelCoordinationEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = ModelCoordinationEngine.Instance;
            var instance2 = ModelCoordinationEngine.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void CreateFederatedModel_ShouldCreateModel()
        {
            var request = new FederatedModelRequest
            {
                ProjectId = "PROJ-001",
                Name = "Test Federated Model",
                Description = "Test federation"
            };

            var fedModel = _engine.CreateFederatedModel(request);

            Assert.That(fedModel, Is.Not.Null);
            Assert.That(fedModel.Name, Is.EqualTo("Test Federated Model"));
        }

        [Test]
        public void StartCoordinationSession_ShouldCreateSession()
        {
            var fedModel = _engine.CreateFederatedModel(new FederatedModelRequest
            {
                ProjectId = "PROJ-002",
                Name = "Session Test Model"
            });

            var session = _engine.StartCoordinationSession(new CoordinationSessionRequest
            {
                FederatedModelId = fedModel.FederatedModelId,
                Name = "Weekly Coordination",
                SessionType = CoordinationSessionType.ClashReview
            });

            Assert.That(session, Is.Not.Null);
            Assert.That(session.Status, Is.EqualTo(SessionStatus.InProgress));
        }

        [Test]
        public void RunClashTest_ShouldReturnResults()
        {
            var fedModel = _engine.CreateFederatedModel(new FederatedModelRequest
            {
                ProjectId = "PROJ-003",
                Name = "Clash Test Model"
            });

            var session = _engine.StartCoordinationSession(new CoordinationSessionRequest
            {
                FederatedModelId = fedModel.FederatedModelId,
                Name = "Clash Test Session",
                SessionType = CoordinationSessionType.ClashReview
            });

            var result = _engine.RunClashTest(session.SessionId, new ClashTestRequest
            {
                TestName = "MEP vs Structure",
                Selection1 = "MEP",
                Selection2 = "Structural",
                Tolerance = 25
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Clashes.Count > 0, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
        }

        [Test]
        public void CreateScheduleLink_ShouldCreateLink()
        {
            var link = _engine.CreateScheduleLink(new ScheduleLinkRequest
            {
                ProjectId = "PROJ-004",
                ScheduleName = "Master Schedule",
                ScheduleSource = "P6"
            });

            Assert.That(link, Is.Not.Null);
            Assert.That(link.ScheduleName, Is.EqualTo("Master Schedule"));
        }

        [Test]
        public void CreateCostLink_ShouldCreateLinkWithCategories()
        {
            var link = _engine.CreateCostLink(new CostLinkRequest
            {
                ProjectId = "PROJ-005",
                CostDatabaseName = "RS Means"
            });

            Assert.That(link, Is.Not.Null);
            Assert.That(link.CostCategories.Count > 0, Is.True);
        }

        [Test]
        public void GetCoordinationRules_ShouldReturnRules()
        {
            var rules = _engine.GetCoordinationRules();

            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.Count > 0, Is.True);
            Assert.That(rules.Any(r => r.RuleType == CoordinationRuleType.Clearance), Is.True);
        }
    }

    #endregion

    #region Document Control Center Tests

    [TestFixture]
    public class DocumentControlCenterTests
    {
        private DocumentControlCenter _center;

        [SetUp]
        public void Setup()
        {
            _center = DocumentControlCenter.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = DocumentControlCenter.Instance;
            var instance2 = DocumentControlCenter.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void CreateDrawingRegister_ShouldCreateRegister()
        {
            var register = _center.CreateDrawingRegister(new DrawingRegisterRequest
            {
                ProjectId = "DOC-001",
                ProjectName = "Document Test Project"
            });

            Assert.That(register, Is.Not.Null);
            Assert.That(register.NamingConvention, Is.Not.Null);
        }

        [Test]
        public void AddDrawing_ShouldAddToRegister()
        {
            var register = _center.CreateDrawingRegister(new DrawingRegisterRequest
            {
                ProjectId = "DOC-002",
                ProjectName = "Drawing Test"
            });

            var drawing = _center.AddDrawing(register.RegisterId, new DrawingRequest
            {
                DrawingNumber = "A-101",
                Title = "Floor Plan Level 01",
                Discipline = "Architecture",
                DrawingType = "Plan",
                Scale = "1:100",
                CreatedBy = "Architect"
            });

            Assert.That(drawing, Is.Not.Null);
            Assert.That(drawing.CurrentRevision, Is.EqualTo("A"));
        }

        [Test]
        public void CreateRFI_ShouldCreateRFI()
        {
            var rfi = _center.CreateRFI(new RFIRequest
            {
                ProjectId = "DOC-003",
                Subject = "Wall Type Clarification",
                Question = "Please clarify the wall type at grid intersection A-1",
                Priority = RFIPriority.Normal,
                Discipline = "Architecture",
                SubmittedBy = "Contractor"
            });

            Assert.That(rfi, Is.Not.Null);
            Assert.That(rfi.Status, Is.EqualTo(RFIStatus.Open));
            Assert.That(rfi.RFIId.StartsWith("RFI-"), Is.True);
        }

        [Test]
        public void CreateSubmittal_ShouldCreateSubmittal()
        {
            var submittal = _center.CreateSubmittal(new SubmittalRequest
            {
                ProjectId = "DOC-004",
                SpecSection = "08 11 00",
                Description = "Steel Doors",
                Type = SubmittalType.ShopDrawing,
                SubmittedBy = "Door Subcontractor",
                Contractor = "Main Contractor"
            });

            Assert.That(submittal, Is.Not.Null);
            Assert.That(submittal.Status, Is.EqualTo(SubmittalStatus.Pending));
        }

        [Test]
        public void CreateTransmittal_ShouldCreateTransmittal()
        {
            var transmittal = _center.CreateTransmittal(new TransmittalRequest
            {
                ProjectId = "DOC-005",
                Subject = "Design Development Drawings",
                From = "Architect",
                To = "Client",
                Purpose = TransmittalPurpose.ForReview,
                CreatedBy = "PM"
            });

            Assert.That(transmittal, Is.Not.Null);
            Assert.That(transmittal.Status, Is.EqualTo(TransmittalStatus.Draft));
        }

        [Test]
        public void CreateChangeOrder_ShouldCreateChangeOrder()
        {
            var co = _center.CreateChangeOrder(new ChangeOrderRequest
            {
                ProjectId = "DOC-006",
                Title = "Additional Outlet Requirements",
                Description = "Add 10 additional outlets per floor",
                Reason = ChangeOrderReason.OwnerRequest,
                InitiatedBy = "Owner",
                CostImpact = 25000m,
                ScheduleImpact = 5
            });

            Assert.That(co, Is.Not.Null);
            Assert.That(co.Status, Is.EqualTo(ChangeOrderStatus.Draft));
        }
    }

    #endregion

    #region BIM Standards Enforcer Tests

    [TestFixture]
    public class BIMStandardsEnforcerTests
    {
        private BIMStandardsEnforcer _enforcer;

        [SetUp]
        public void Setup()
        {
            _enforcer = BIMStandardsEnforcer.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = BIMStandardsEnforcer.Instance;
            var instance2 = BIMStandardsEnforcer.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void CreateProfile_ShouldCreateStandardsProfile()
        {
            var profile = _enforcer.CreateProfile(new StandardsProfileRequest
            {
                ProjectId = "STD-001",
                ProfileName = "Project Standards",
                ClassificationSystem = "UNICLASS2015"
            });

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.ActiveNamingRules.Count > 0, Is.True);
            Assert.That(profile.ActiveParameterRules.Count > 0, Is.True);
        }

        [Test]
        public void RunComplianceCheck_ShouldReturnReport()
        {
            var profile = _enforcer.CreateProfile(new StandardsProfileRequest
            {
                ProjectId = "STD-002",
                ProfileName = "Compliance Test"
            });

            var report = _enforcer.RunComplianceCheck(new ComplianceCheckRequest
            {
                ProfileId = profile.ProfileId,
                ModelPath = "C:\\Test\\Model.rvt",
                CheckNaming = true,
                CheckParameters = true,
                CheckModel = true,
                Items = new List<NamedItem>
                {
                    new NamedItem { ItemId = "1", Name = "Invalid Name With Spaces", Category = NamingCategory.Family }
                },
                Elements = new List<ElementData>
                {
                    new ElementData
                    {
                        ElementId = "E1",
                        Category = "Doors",
                        Parameters = new Dictionary<string, string>()
                    }
                },
                ModelData = new ModelCheckData
                {
                    HasSharedCoordinates = true,
                    WarningCount = 50
                }
            });

            Assert.That(report, Is.Not.Null);
            Assert.That(report.Summary, Is.Not.Null);
            Assert.That(report.Summary.ComplianceScore >= 0, Is.True);
        }

        [Test]
        public void ValidateName_ShouldValidateNamingConvention()
        {
            var result = _enforcer.ValidateName("Door_SingleFlush_900x2100", NamingCategory.Family);
            Assert.That(result.IsValid, Is.True);

            var invalidResult = _enforcer.ValidateName("Door With Spaces", NamingCategory.Family);
            Assert.That(invalidResult.IsValid, Is.False);
        }

        [Test]
        public void ValidateClassificationCode_ShouldValidateCode()
        {
            var validResult = _enforcer.ValidateClassificationCode("Pr_20_85", "UNICLASS2015");
            Assert.That(validResult.IsValid, Is.True);

            var invalidResult = _enforcer.ValidateClassificationCode("Invalid", "UNICLASS2015");
            Assert.That(invalidResult.IsValid, Is.False);
        }

        [Test]
        public void GetNamingRules_ShouldReturnRules()
        {
            var rules = _enforcer.GetNamingRules();
            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.Count > 0, Is.True);

            var familyRules = _enforcer.GetNamingRules(NamingCategory.Family);
            Assert.That(familyRules.All(r => r.Category == NamingCategory.Family), Is.True);
        }

        [Test]
        public void GetClassificationSystems_ShouldReturnSystems()
        {
            var systems = _enforcer.GetClassificationSystems();
            Assert.That(systems, Is.Not.Null);
            Assert.That(systems.Any(s => s.SystemId == "UNICLASS2015"), Is.True);
            Assert.That(systems.Any(s => s.SystemId == "OMNICLASS"), Is.True);
            Assert.That(systems.Any(s => s.SystemId == "MASTERFORMAT"), Is.True);
        }
    }

    #endregion

    #region Project Delivery Manager Tests

    [TestFixture]
    public class ProjectDeliveryManagerTests
    {
        private ProjectDeliveryManager _manager;

        [SetUp]
        public void Setup()
        {
            _manager = ProjectDeliveryManager.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            var instance1 = ProjectDeliveryManager.Instance;
            var instance2 = ProjectDeliveryManager.Instance;
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void CreateProject_ShouldCreateWithStages()
        {
            var project = _manager.CreateProject(new DeliveryProjectRequest
            {
                ProjectName = "Delivery Test Project",
                ProjectCode = "DEL-001",
                ClientName = "Test Client",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(18),
                TemplateId = "TPL-RIBA"
            });

            Assert.That(project, Is.Not.Null);
            Assert.That(project.Stages.Count > 0, Is.True);
        }

        [Test]
        public void CreateMilestone_ShouldCreateMilestone()
        {
            var project = _manager.CreateProject(new DeliveryProjectRequest
            {
                ProjectName = "Milestone Test",
                ProjectCode = "MIL-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var milestone = _manager.CreateMilestone(new MilestoneRequest
            {
                ProjectId = project.ProjectId,
                Name = "Client Approval",
                PlannedDate = DateTime.Today.AddMonths(3),
                Type = MilestoneType.ClientApproval,
                IsCritical = true
            });

            Assert.That(milestone, Is.Not.Null);
            Assert.That(milestone.Status, Is.EqualTo(MilestoneStatus.Pending));
        }

        [Test]
        public void CreateDeliverable_ShouldCreateDeliverable()
        {
            var project = _manager.CreateProject(new DeliveryProjectRequest
            {
                ProjectName = "Deliverable Test",
                ProjectCode = "DLV-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var deliverable = _manager.CreateDeliverable(new DeliverableRequest
            {
                ProjectId = project.ProjectId,
                Name = "Design Report",
                Type = DeliverableType.Report,
                DueDate = DateTime.Today.AddMonths(2),
                ResponsibleParty = "Architect"
            });

            Assert.That(deliverable, Is.Not.Null);
            Assert.That(deliverable.Status, Is.EqualTo(DeliverableStatus.NotStarted));
        }

        [Test]
        public void GetDashboard_ShouldReturnDashboard()
        {
            var project = _manager.CreateProject(new DeliveryProjectRequest
            {
                ProjectName = "Dashboard Test",
                ProjectCode = "DSH-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var dashboard = _manager.GetDashboard(project.ProjectId);

            Assert.That(dashboard, Is.Not.Null);
            Assert.That(dashboard.StageSummary, Is.Not.Null);
            Assert.That(dashboard.DeliverableSummary, Is.Not.Null);
            Assert.That(dashboard.MilestoneSummary, Is.Not.Null);
        }

        [Test]
        public void GetTemplates_ShouldReturnDeliveryTemplates()
        {
            var templates = _manager.GetTemplates();

            Assert.That(templates, Is.Not.Null);
            Assert.That(templates.Count >= 4, Is.True);
            Assert.That(templates.Any(t => t.Name.Contains("RIBA")), Is.True);
            Assert.That(templates.Any(t => t.Name.Contains("AIA")), Is.True);
        }

        [Test]
        public void GenerateDeliveryReport_ShouldGenerateReport()
        {
            var project = _manager.CreateProject(new DeliveryProjectRequest
            {
                ProjectName = "Report Test",
                ProjectCode = "RPT-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var report = _manager.GenerateDeliveryReport(project.ProjectId);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.Contains("PROJECT DELIVERY REPORT"), Is.True);
            Assert.That(report.Contains("Report Test"), Is.True);
        }

        [Test]
        public void CreateGateReview_ShouldCreateReview()
        {
            var project = _manager.CreateProject(new DeliveryProjectRequest
            {
                ProjectName = "Gate Review Test",
                ProjectCode = "GRT-001",
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(12)
            });

            var stageId = project.Stages.First();

            var review = _manager.CreateGateReview(new GateReviewRequest
            {
                ProjectId = project.ProjectId,
                StageId = stageId,
                ReviewName = "Stage 1 Gate Review",
                ScheduledDate = DateTime.Today.AddMonths(1),
                ReviewType = GateReviewType.StageGate
            });

            Assert.That(review, Is.Not.Null);
            Assert.That(review.Status, Is.EqualTo(GateReviewStatus.Scheduled));
            Assert.That(review.Criteria.Count > 0, Is.True);
        }
    }

    #endregion
}
