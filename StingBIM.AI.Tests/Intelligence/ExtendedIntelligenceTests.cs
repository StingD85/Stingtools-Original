// ===================================================================
// StingBIM Extended Intelligence Tests
// Comprehensive tests for all intelligence modules
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using StingBIM.AI.Intelligence.CostIntelligence;
using StingBIM.AI.Intelligence.SustainabilityIntelligence;
using StingBIM.AI.Intelligence.RiskIntelligence;
using StingBIM.AI.Intelligence.ProcurementIntelligence;
using StingBIM.AI.Intelligence.QualityIntelligence;
using StingBIM.AI.Intelligence.ResourceIntelligence;
using StingBIM.AI.Intelligence.KnowledgeManagement;
using StingBIM.AI.Intelligence.RegulatoryIntelligence;

namespace StingBIM.AI.Tests.Intelligence
{
    #region Cost Intelligence Tests

    public class CostIntelligenceEngineTests
    {
        private readonly CostIntelligenceEngine _engine;

        public CostIntelligenceEngineTests()
        {
            _engine = CostIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = CostIntelligenceEngine.Instance;
            var instance2 = CostIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void CreateProject_ReturnsValidProject()
        {
            var request = new CostProjectRequest
            {
                Name = "Test Office Building",
                ProjectType = "Office",
                Location = "New York",
                Currency = "USD"
            };

            var project = _engine.CreateProject(request);

            Assert.NotNull(project);
            Assert.NotEmpty(project.Id);
            Assert.Equal("Test Office Building", project.Name);
            Assert.Equal("USD", project.Currency);
        }

        [Fact]
        public async Task GenerateEstimateAsync_CalculatesCorrectTotals()
        {
            var project = _engine.CreateProject(new CostProjectRequest
            {
                Name = "Estimate Test",
                ProjectType = "Office",
                Location = "US",
                Currency = "USD"
            });

            var request = new CostEstimateRequest
            {
                ProjectId = project.Id,
                Name = "Schematic Design Estimate",
                EstimateType = EstimateType.SchematicDesign,
                Region = "US",
                Currency = "USD",
                GrossArea = 50000,
                ModelData = new ModelData
                {
                    ConcreteVolume = 500,
                    SteelWeight = 100,
                    FloorArea = 50000,
                    RoofArea = 12500
                },
                GeneralConditionsPercent = 8,
                OverheadProfitPercent = 10,
                ContingencyPercent = 5
            };

            var estimate = await _engine.GenerateEstimateAsync(request);

            Assert.NotNull(estimate);
            Assert.True(estimate.DirectCost > 0);
            Assert.True(estimate.TotalCost > estimate.DirectCost);
            Assert.True(estimate.CostPerSF > 0);
            Assert.True(estimate.ConfidenceLevel > 0);
        }

        [Fact]
        public void CreateVEStudy_GeneratesProposals()
        {
            var request = new VEStudyRequest
            {
                ProjectId = "test-project",
                Name = "VE Study",
                EstimatedCost = 10000000,
                TargetSavings = 500000
            };

            var study = _engine.CreateVEStudy(request);

            Assert.NotNull(study);
            Assert.NotEmpty(study.Proposals);
            Assert.True(study.TotalPotentialSavings > 0);
        }

        [Fact]
        public void CreateBudgetForecast_GeneratesMonthlyPeriods()
        {
            var request = new BudgetForecastRequest
            {
                ProjectId = "test-project",
                Name = "Project Budget",
                TotalBudget = 5000000,
                ProjectDurationMonths = 18,
                StartDate = DateTime.UtcNow,
                Currency = "USD"
            };

            var forecast = _engine.CreateBudgetForecast(request);

            Assert.NotNull(forecast);
            Assert.Equal(18, forecast.ForecastPeriods.Count);
            Assert.True(forecast.ForecastPeriods.Last().CumulativePlanned > 0);
        }

        [Fact]
        public void AnalyzeMarketConditions_ReturnsTrends()
        {
            var request = new MarketAnalysisRequest
            {
                Region = "US",
                MaterialCategories = new List<string> { "Steel", "Concrete" }
            };

            var analysis = _engine.AnalyzeMarketConditions(request);

            Assert.NotNull(analysis);
            Assert.NotEmpty(analysis.Trends);
            Assert.True(analysis.RecommendedContingency > 0);
        }

        [Fact]
        public void BenchmarkProject_CalculatesPercentiles()
        {
            var request = new BenchmarkRequest
            {
                ProjectId = "test-project",
                ProjectType = "Office",
                Location = "US",
                GrossArea = 50000,
                TotalCost = 15000000,
                StructureCost = 2500000,
                MEPCost = 4000000,
                EnvelopeCost = 3000000
            };

            var analysis = _engine.BenchmarkProject(request);

            Assert.NotNull(analysis);
            Assert.NotEmpty(analysis.Comparisons);
            Assert.NotEmpty(analysis.OverallAssessment);
        }

        [Fact]
        public void GetAvailableDatabases_ReturnsMultipleDatabases()
        {
            var databases = _engine.GetAvailableDatabases();

            Assert.NotEmpty(databases);
            Assert.True(databases.Count >= 2);
        }
    }

    #endregion

    #region Sustainability Intelligence Tests

    public class SustainabilityIntelligenceEngineTests
    {
        private readonly SustainabilityIntelligenceEngine _engine;

        public SustainabilityIntelligenceEngineTests()
        {
            _engine = SustainabilityIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = SustainabilityIntelligenceEngine.Instance;
            var instance2 = SustainabilityIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void GetAvailableFrameworks_ReturnsMultipleFrameworks()
        {
            var frameworks = _engine.GetAvailableFrameworks();

            Assert.NotEmpty(frameworks);
            Assert.Contains(frameworks, f => f.Name.Contains("LEED"));
            Assert.Contains(frameworks, f => f.Name.Contains("BREEAM"));
            Assert.Contains(frameworks, f => f.Name.Contains("WELL"));
        }

        [Fact]
        public async Task AnalyzeCarbonFootprintAsync_CalculatesTotals()
        {
            var request = new CarbonAnalysisRequest
            {
                ProjectId = "test-project",
                BuildingType = "Office",
                GrossArea = 50000,
                Scope = "Whole Building",
                MaterialQuantities = new List<MaterialQuantity>
                {
                    new MaterialQuantity { EPDId = "EPD-CONCRETE-4000", Quantity = 500, Unit = "m³" },
                    new MaterialQuantity { EPDId = "EPD-STEEL-STRUCTURAL", Quantity = 100, Unit = "tonne" }
                },
                EnergyData = new EnergyData
                {
                    AnnualElectricity_kWh = 1000000,
                    AnnualGas_Therms = 10000
                },
                BuildingLifespan = 60
            };

            var analysis = await _engine.AnalyzeCarbonFootprintAsync(request);

            Assert.NotNull(analysis);
            Assert.True(analysis.TotalEmbodiedCarbon > 0);
            Assert.True(analysis.TotalOperationalCarbon > 0);
            Assert.True(analysis.WholeLifeCarbon > 0);
            Assert.NotEmpty(analysis.Recommendations);
        }

        [Fact]
        public void AssessCertification_ReturnsScores()
        {
            var request = new CertificationAssessmentRequest
            {
                ProjectId = "test-project",
                FrameworkId = "LEED-V4.1-BDC",
                CreditInputs = new List<CreditInput>
                {
                    new CreditInput { CreditCode = "LT-5", Status = CreditStatusType.Achieved, AchievedPoints = 5 },
                    new CreditInput { CreditCode = "SS-4", Status = CreditStatusType.Targeted, TargetPoints = 3 }
                }
            };

            var assessment = _engine.AssessCertification(request);

            Assert.NotNull(assessment);
            Assert.True(assessment.TotalAchievedPoints >= 0);
            Assert.NotNull(assessment.ProjectedLevel);
        }

        [Fact]
        public void ModelEnergy_CalculatesSavings()
        {
            var request = new EnergyModelRequest
            {
                ProjectId = "test-project",
                BuildingType = "Office",
                Location = "Chicago",
                GrossArea = 50000,
                EnvelopeEfficiency = new EnvelopeEfficiency
                {
                    WallRValue = 25,
                    RoofRValue = 35,
                    WindowUFactor = 0.28m,
                    ACH50 = 1.5m
                },
                HVACEfficiency = new HVACEfficiency
                {
                    CoolingEER = 16,
                    HeatingEfficiency = 92,
                    HasEnergyRecovery = true,
                    HasVariableSpeed = true
                },
                RenewableCapacity_kW = 100
            };

            var result = _engine.ModelEnergy(request);

            Assert.NotNull(result);
            Assert.True(result.EnergySavingsPercent > 0);
            Assert.True(result.ProposedEUI < result.BaselineEUI);
            Assert.NotEmpty(result.EndUseBreakdown);
        }

        [Fact]
        public void PerformLCA_CalculatesImpacts()
        {
            var request = new LCARequest
            {
                ProjectId = "test-project",
                StudyPeriod = 60,
                GrossArea = 50000,
                Materials = new List<MaterialQuantity>
                {
                    new MaterialQuantity { EPDId = "EPD-CONCRETE-4000", Quantity = 500, Unit = "m³" }
                },
                AnnualEnergy_kWh = 1000000
            };

            var lca = _engine.PerformLCA(request);

            Assert.NotNull(lca);
            Assert.True(lca.TotalGWP > 0);
            Assert.NotEmpty(lca.LifeCycleStages);
            Assert.NotEmpty(lca.ImpactCategories);
        }

        [Fact]
        public void AnalyzeWaterUse_CalculatesSavings()
        {
            var request = new WaterAnalysisRequest
            {
                ProjectId = "test-project",
                Occupants = 500,
                WaterClosetCount = 20,
                UrinalCount = 10,
                LavatoryCount = 25,
                KitchenFaucetCount = 5,
                ShowerCount = 4,
                HighEfficiencyFixtures = true,
                RoofArea = 12500,
                AnnualRainfall = 40
            };

            var analysis = _engine.AnalyzeWaterUse(request);

            Assert.NotNull(analysis);
            Assert.True(analysis.WaterSavingsPercent > 0);
            Assert.True(analysis.RainwaterHarvestPotential > 0);
        }
    }

    #endregion

    #region Risk Intelligence Tests

    public class RiskIntelligenceEngineTests
    {
        private readonly RiskIntelligenceEngine _engine;

        public RiskIntelligenceEngineTests()
        {
            _engine = RiskIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = RiskIntelligenceEngine.Instance;
            var instance2 = RiskIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void CreateProject_CreatesRegisterWithRisks()
        {
            var request = new RiskProjectRequest
            {
                Name = "Test Project",
                ProjectType = "Commercial",
                ContractValue = 10000000,
                Duration = 18,
                Location = "New York",
                AutoPopulateRisks = true
            };

            var project = _engine.CreateProject(request);

            Assert.NotNull(project);
            Assert.NotEmpty(project.Registers);

            var register = _engine.GetRegister(project.Registers.First());
            Assert.NotNull(register);
            Assert.NotEmpty(register.Risks);
        }

        [Fact]
        public async Task AssessProjectRisksAsync_CalculatesExposure()
        {
            var project = _engine.CreateProject(new RiskProjectRequest
            {
                Name = "Assessment Test",
                ContractValue = 5000000,
                AutoPopulateRisks = true
            });

            var assessment = await _engine.AssessProjectRisksAsync(project.Id);

            Assert.NotNull(assessment);
            Assert.True(assessment.TotalRisks > 0);
            Assert.True(assessment.TotalExposure > 0);
            Assert.NotEmpty(assessment.TopRisks);
            Assert.NotEmpty(assessment.Recommendations);
        }

        [Fact]
        public void AddRisk_CalculatesRiskScore()
        {
            var project = _engine.CreateProject(new RiskProjectRequest
            {
                Name = "Add Risk Test",
                ContractValue = 1000000,
                AutoPopulateRisks = false
            });

            var registerId = project.Registers.First();

            var risk = _engine.AddRisk(registerId, new RiskRequest
            {
                Category = "CONSTRUCTION",
                Title = "Test Risk",
                Description = "A test risk for unit testing",
                Probability = 0.5m,
                Impact = 4,
                EstimatedCost = 100000,
                Owner = "Project Manager",
                CreatedBy = "Test User"
            });

            Assert.NotNull(risk);
            Assert.True(risk.RiskScore > 0);
            Assert.Equal(50000, risk.ExpectedValue);
        }

        [Fact]
        public void RunMonteCarloSimulation_ReturnsDistribution()
        {
            var project = _engine.CreateProject(new RiskProjectRequest
            {
                Name = "Monte Carlo Test",
                ContractValue = 10000000,
                AutoPopulateRisks = true
            });

            var result = _engine.RunMonteCarloSimulation(project.Id, 1000);

            Assert.NotNull(result);
            Assert.True(result.Mean > 0);
            Assert.True(result.P90 > result.P50);
            Assert.True(result.P50 > result.P10);
        }

        [Fact]
        public void AnalyzeInsuranceRequirements_ReturnsCoverages()
        {
            var project = _engine.CreateProject(new RiskProjectRequest
            {
                Name = "Insurance Test",
                ContractValue = 25000000,
                AutoPopulateRisks = true
            });

            var analysis = _engine.AnalyzeInsuranceRequirements(project.Id);

            Assert.NotNull(analysis);
            Assert.NotEmpty(analysis.RequiredCoverages);
            Assert.True(analysis.TotalRecommendedCoverage > 0);
        }

        [Fact]
        public void AssessSafetyRisks_IdentifiesHazards()
        {
            var assessment = _engine.AssessSafetyRisks("test-project", new SafetyAssessmentRequest
            {
                PlannedActivities = new List<string>
                {
                    "excavation work",
                    "steel erection",
                    "roofing installation"
                }
            });

            Assert.NotNull(assessment);
            Assert.NotEmpty(assessment.HazardAnalysis);
            Assert.NotEmpty(assessment.RequiredPrograms);
            Assert.NotEmpty(assessment.TrainingRequirements);
        }
    }

    #endregion

    #region Procurement Intelligence Tests

    public class ProcurementIntelligenceEngineTests
    {
        private readonly ProcurementIntelligenceEngine _engine;

        public ProcurementIntelligenceEngineTests()
        {
            _engine = ProcurementIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = ProcurementIntelligenceEngine.Instance;
            var instance2 = ProcurementIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void GenerateProcurementPlan_CreatesStrategies()
        {
            var request = new ProcurementPlanRequest
            {
                ProjectId = "test-project",
                ProjectType = "Commercial",
                ProjectValue = 20000000,
                StartDate = DateTime.UtcNow,
                RequiredOnSiteDate = DateTime.UtcNow.AddMonths(6)
            };

            var plan = _engine.GenerateProcurementPlan(request);

            Assert.NotNull(plan);
            Assert.NotEmpty(plan.PackageStrategies);
            Assert.NotEmpty(plan.Timeline);
            Assert.NotEmpty(plan.Recommendations);
        }

        [Fact]
        public void SearchSuppliers_FiltersCorrectly()
        {
            var suppliers = _engine.SearchSuppliers(new SupplierSearchCriteria
            {
                MinimumRating = 4.0m,
                PrequalifiedOnly = true
            });

            Assert.NotEmpty(suppliers);
            Assert.All(suppliers, s => Assert.True(s.Rating >= 4.0m));
        }

        [Fact]
        public void CreateBidPackage_ReturnsValidPackage()
        {
            var project = _engine.CreateProject(new ProcurementProjectRequest
            {
                Name = "Bid Package Test",
                ProjectValue = 10000000,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(2)
            });

            var package = _engine.CreateBidPackage(new BidPackageRequest
            {
                ProjectId = project.Id,
                Name = "Concrete Package",
                Number = "BP-001",
                Scope = "All concrete work",
                EstimatedValue = 1500000,
                BidDate = DateTime.UtcNow.AddDays(30)
            });

            Assert.NotNull(package);
            Assert.Equal("Concrete Package", package.Name);
            Assert.Equal(BidPackageStatus.Draft, package.Status);
        }

        [Fact]
        public void AnalyzeBids_CalculatesStatistics()
        {
            var project = _engine.CreateProject(new ProcurementProjectRequest
            {
                Name = "Bid Analysis Test",
                ProjectValue = 5000000,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1)
            });

            var package = _engine.CreateBidPackage(new BidPackageRequest
            {
                ProjectId = project.Id,
                Name = "Test Package",
                Number = "BP-TEST",
                EstimatedValue = 500000,
                BidDate = DateTime.UtcNow
            });

            // Submit bids
            _engine.SubmitBid(package.Id, new BidSubmission
            {
                SupplierId = "SUP-001",
                TotalPrice = 450000
            });

            _engine.SubmitBid(package.Id, new BidSubmission
            {
                SupplierId = "SUP-002",
                TotalPrice = 480000
            });

            _engine.SubmitBid(package.Id, new BidSubmission
            {
                SupplierId = "SUP-003",
                TotalPrice = 520000
            });

            var analysis = _engine.AnalyzeBids(package.Id);

            Assert.NotNull(analysis);
            Assert.Equal(3, analysis.BidCount);
            Assert.Equal(450000, analysis.LowBid);
            Assert.Equal(520000, analysis.HighBid);
        }

        [Fact]
        public void CreateContract_ReturnsValidContract()
        {
            var project = _engine.CreateProject(new ProcurementProjectRequest
            {
                Name = "Contract Test",
                ProjectValue = 1000000,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1)
            });

            var contract = _engine.CreateContract(new ContractRequest
            {
                ProjectId = project.Id,
                SupplierId = "SUP-001",
                ContractNumber = "C-2026-001",
                ContractType = ContractType.LumpSum,
                ContractValue = 500000,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6)
            });

            Assert.NotNull(contract);
            Assert.Equal(500000, contract.OriginalValue);
            Assert.Equal(ContractStatus.Draft, contract.Status);
        }
    }

    #endregion

    #region Quality Intelligence Tests

    public class QualityIntelligenceEngineTests
    {
        private readonly QualityIntelligenceEngine _engine;

        public QualityIntelligenceEngineTests()
        {
            _engine = QualityIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = QualityIntelligenceEngine.Instance;
            var instance2 = QualityIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void CreateQualityPlan_GeneratesInspectionPoints()
        {
            var project = _engine.CreateProject(new QualityProjectRequest
            {
                Name = "Quality Plan Test",
                ProjectType = "Commercial"
            });

            var plan = _engine.CreateQualityPlan(new QualityPlanRequest
            {
                ProjectId = project.Id,
                Name = "Project Quality Plan",
                ProjectType = "Commercial"
            });

            Assert.NotNull(plan);
            Assert.NotEmpty(plan.InspectionPoints);
            Assert.NotEmpty(plan.TestingRequirements);
            Assert.NotEmpty(plan.QualityObjectives);
        }

        [Fact]
        public void GetInspectionTemplates_ReturnsMultipleTemplates()
        {
            var templates = _engine.GetInspectionTemplates();

            Assert.NotEmpty(templates);
            Assert.Contains(templates, t => t.Name.Contains("Concrete"));
            Assert.Contains(templates, t => t.Name.Contains("Steel"));
        }

        [Fact]
        public void CreateDefect_ReturnsValidDefect()
        {
            var project = _engine.CreateProject(new QualityProjectRequest
            {
                Name = "Defect Test",
                ProjectType = "Commercial"
            });

            var defect = _engine.CreateDefect(new DefectRequest
            {
                ProjectId = project.Id,
                Title = "Test Defect",
                Description = "A test defect for unit testing",
                Location = "Floor 2",
                Category = "Finishes",
                Severity = DefectSeverity.Major,
                Trade = "Drywall",
                IdentifiedBy = "QC Inspector"
            });

            Assert.NotNull(defect);
            Assert.Equal(DefectStatus.Open, defect.Status);
            Assert.NotEmpty(defect.History);
        }

        [Fact]
        public void AnalyzeDefects_CalculatesMetrics()
        {
            var project = _engine.CreateProject(new QualityProjectRequest
            {
                Name = "Defect Analysis Test",
                ProjectType = "Commercial"
            });

            // Create some defects
            for (int i = 0; i < 5; i++)
            {
                _engine.CreateDefect(new DefectRequest
                {
                    ProjectId = project.Id,
                    Title = $"Defect {i}",
                    Description = "Test defect",
                    Location = "Floor 1",
                    Category = "Finishes",
                    Severity = i % 2 == 0 ? DefectSeverity.Major : DefectSeverity.Minor,
                    IdentifiedBy = "Inspector"
                });
            }

            var analytics = _engine.AnalyzeDefects(project.Id);

            Assert.NotNull(analytics);
            Assert.Equal(5, analytics.TotalDefects);
            Assert.Equal(5, analytics.OpenDefects);
            Assert.NotEmpty(analytics.DefectsBySeverity);
        }

        [Fact]
        public void CreateCommissioningPlan_GeneratesSystems()
        {
            var plan = _engine.CreateCommissioningPlan(new CommissioningPlanRequest
            {
                ProjectId = "test-project",
                Name = "Cx Plan"
            });

            Assert.NotNull(plan);
            Assert.NotEmpty(plan.Systems);
            Assert.NotEmpty(plan.Schedule);
        }
    }

    #endregion

    #region Resource Intelligence Tests

    public class ResourceIntelligenceEngineTests
    {
        private readonly ResourceIntelligenceEngine _engine;

        public ResourceIntelligenceEngineTests()
        {
            _engine = ResourceIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = ResourceIntelligenceEngine.Instance;
            var instance2 = ResourceIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void AddTeamMember_CreatesValidMember()
        {
            var member = _engine.AddTeamMember(new TeamMemberRequest
            {
                EmployeeId = "EMP-001",
                Name = "John Doe",
                Email = "john@example.com",
                Title = "BIM Manager",
                Department = "Design",
                Location = "New York",
                HireDate = DateTime.UtcNow.AddYears(-2),
                HourlyRate = 75,
                Skills = new List<MemberSkillInput>
                {
                    new MemberSkillInput { SkillId = "BIM-001", Level = SkillLevel.Advanced, YearsExperience = 5 }
                }
            });

            Assert.NotNull(member);
            Assert.Equal("John Doe", member.Name);
            Assert.NotEmpty(member.Skills);
        }

        [Fact]
        public void FindResources_MatchesSkills()
        {
            // Add a member with specific skills
            _engine.AddTeamMember(new TeamMemberRequest
            {
                EmployeeId = "EMP-002",
                Name = "Jane Smith",
                Title = "Structural Engineer",
                Skills = new List<MemberSkillInput>
                {
                    new MemberSkillInput { SkillId = "TECH-001", Level = SkillLevel.Expert, YearsExperience = 10 }
                }
            });

            var matches = _engine.FindResources(new ResourceSearchCriteria
            {
                RequiredSkills = new List<SkillRequirement>
                {
                    new SkillRequirement { SkillId = "TECH-001", MinimumLevel = SkillLevel.Advanced, Required = true }
                },
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(3),
                MinimumMatchScore = 50
            });

            Assert.NotEmpty(matches);
        }

        [Fact]
        public void AnalyzeWorkload_ReturnsWeeklyBreakdown()
        {
            var member = _engine.AddTeamMember(new TeamMemberRequest
            {
                EmployeeId = "EMP-003",
                Name = "Bob Wilson",
                Title = "Project Engineer"
            });

            // Assign to a project
            _engine.AssignToProject(new AssignmentRequest
            {
                MemberId = member.Id,
                ProjectId = "project-1",
                ProjectName = "Test Project",
                Role = "Lead Engineer",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                AllocationPercent = 50,
                AssignedBy = "Manager"
            });

            var analysis = _engine.AnalyzeWorkload(member.Id);

            Assert.NotNull(analysis);
            Assert.Equal(12, analysis.WeeklyBreakdown.Count);
        }

        [Fact]
        public void CreateTrainingPlan_RecommendsGourses()
        {
            var member = _engine.AddTeamMember(new TeamMemberRequest
            {
                EmployeeId = "EMP-004",
                Name = "Alice Brown",
                Title = "Junior Designer",
                Skills = new List<MemberSkillInput>
                {
                    new MemberSkillInput { SkillId = "BIM-001", Level = SkillLevel.Beginner, YearsExperience = 1 }
                }
            });

            var plan = _engine.CreateTrainingPlan(new TrainingPlanRequest
            {
                MemberId = member.Id,
                Name = "BIM Development Plan",
                TargetCompletionDate = DateTime.UtcNow.AddYears(1),
                TargetSkills = new List<SkillTarget>
                {
                    new SkillTarget { SkillId = "BIM-001", TargetLevel = SkillLevel.Advanced, Priority = 1 }
                }
            });

            Assert.NotNull(plan);
            Assert.NotEmpty(plan.Goals);
            Assert.NotEmpty(plan.Courses);
        }

        [Fact]
        public void ForecastCapacity_CalculatesMonthlyCapacity()
        {
            // Add some team members
            _engine.AddTeamMember(new TeamMemberRequest
            {
                EmployeeId = "EMP-005",
                Name = "Team Member 1",
                Title = "Engineer"
            });

            var forecast = _engine.ForecastCapacity(new CapacityForecastRequest
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6)
            });

            Assert.NotNull(forecast);
            Assert.NotEmpty(forecast.MonthlyCapacity);
        }
    }

    #endregion

    #region Knowledge Management Tests

    public class KnowledgeManagementEngineTests
    {
        private readonly KnowledgeManagementEngine _engine;

        public KnowledgeManagementEngineTests()
        {
            _engine = KnowledgeManagementEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = KnowledgeManagementEngine.Instance;
            var instance2 = KnowledgeManagementEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void CaptureLessonLearned_ReturnsValidLesson()
        {
            var lesson = _engine.CaptureLessonLearned(new LessonLearnedRequest
            {
                ProjectId = "project-001",
                ProjectName = "Test Project",
                Title = "Improved BIM Coordination",
                Category = "BIM Coordination",
                Type = LessonType.Success,
                Description = "Weekly clash detection meetings reduced field conflicts",
                Impact = "30% reduction in RFIs",
                Recommendation = "Implement weekly BIM coordination meetings on all projects",
                SubmittedBy = "Project Manager",
                Tags = new List<string> { "BIM", "Coordination", "Meetings" }
            });

            Assert.NotNull(lesson);
            Assert.Equal(LessonStatus.Draft, lesson.Status);
            Assert.NotEmpty(lesson.Tags);
        }

        [Fact]
        public void SearchLessons_FiltersCorrectly()
        {
            // Create some lessons
            _engine.CaptureLessonLearned(new LessonLearnedRequest
            {
                ProjectId = "project-002",
                ProjectName = "Project A",
                Title = "Safety Improvement",
                Category = "Safety",
                Type = LessonType.Success,
                Description = "Improved safety metrics",
                SubmittedBy = "Safety Manager"
            });

            var lessons = _engine.SearchLessons(new LessonSearchCriteria
            {
                Category = "Safety"
            });

            Assert.NotEmpty(lessons);
            Assert.All(lessons, l => Assert.Equal("Safety", l.Category));
        }

        [Fact]
        public void GetBestPractices_ReturnsActivePractices()
        {
            var practices = _engine.GetBestPractices(new BestPracticeSearchCriteria
            {
                Category = "BIM Coordination"
            });

            Assert.NotEmpty(practices);
        }

        [Fact]
        public void CreateProjectRecord_CalculatesMetrics()
        {
            var record = _engine.CreateProjectRecord(new ProjectRecordRequest
            {
                ProjectName = "Completed Project",
                ProjectType = "Office",
                Location = "New York",
                Client = "ABC Corp",
                DeliveryMethod = "Design-Build",
                StartDate = DateTime.UtcNow.AddYears(-2),
                CompletionDate = DateTime.UtcNow,
                GrossArea = 100000,
                Stories = 10,
                ContractValue = 50000000,
                FinalCost = 52000000,
                ScheduledDuration = 24,
                ActualDuration = 26
            });

            Assert.NotNull(record);
            Assert.True(record.Metrics.CostPerSF > 0);
            Assert.True(record.Metrics.CostVariance > 0);
            Assert.False(record.Metrics.OnBudget);
        }

        [Fact]
        public void GetDashboard_ReturnsStatistics()
        {
            var dashboard = _engine.GetDashboard();

            Assert.NotNull(dashboard);
            Assert.True(dashboard.TotalBestPractices > 0);
        }
    }

    #endregion

    #region Regulatory Intelligence Tests

    public class RegulatoryIntelligenceEngineTests
    {
        private readonly RegulatoryIntelligenceEngine _engine;

        public RegulatoryIntelligenceEngineTests()
        {
            _engine = RegulatoryIntelligenceEngine.Instance;
        }

        [Fact]
        public void Instance_ReturnsSingleton()
        {
            var instance1 = RegulatoryIntelligenceEngine.Instance;
            var instance2 = RegulatoryIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void GetAllJurisdictions_ReturnsMultipleJurisdictions()
        {
            var jurisdictions = _engine.GetAllJurisdictions();

            Assert.NotEmpty(jurisdictions);
            Assert.Contains(jurisdictions, j => j.Name == "New York City");
            Assert.Contains(jurisdictions, j => j.Name == "Kampala");
        }

        [Fact]
        public void CreateProject_DeterminesRequiredPermits()
        {
            var project = _engine.CreateProject(new RegulatoryProjectRequest
            {
                Name = "Test Building",
                Address = "123 Main St",
                JurisdictionId = "NYC",
                ProjectType = "New Construction",
                OccupancyType = "Business",
                GrossArea = 100000,
                Stories = 15,
                ConstructionType = "Type I-A"
            });

            Assert.NotNull(project);
            Assert.NotEmpty(project.RequiredPermits);
        }

        [Fact]
        public void CreatePermit_GeneratesTimeline()
        {
            var project = _engine.CreateProject(new RegulatoryProjectRequest
            {
                Name = "Permit Test",
                JurisdictionId = "NYC",
                GrossArea = 50000
            });

            var permit = _engine.CreatePermit(new PermitRequest
            {
                ProjectId = project.Id,
                PermitType = "NB",
                PermitName = "New Building",
                JurisdictionId = "NYC"
            });

            Assert.NotNull(permit);
            Assert.NotEmpty(permit.Timeline);
            Assert.Equal(PermitStatus.Draft, permit.Status);
        }

        [Fact]
        public void AssessCompliance_IdentifiesRequirements()
        {
            var project = _engine.CreateProject(new RegulatoryProjectRequest
            {
                Name = "Compliance Test",
                JurisdictionId = "NYC",
                OccupancyType = "Business",
                GrossArea = 75000,
                Stories = 8
            });

            var assessment = _engine.AssessCompliance(project.Id);

            Assert.NotNull(assessment);
            Assert.NotEmpty(assessment.Requirements);
        }

        [Fact]
        public void ResearchCode_ReturnsRelevantResults()
        {
            var result = _engine.ResearchCode(new CodeResearchRequest
            {
                Query = "sprinkler",
                JurisdictionId = "NYC"
            });

            Assert.NotNull(result);
            Assert.NotEmpty(result.Results);
        }

        [Fact]
        public void GetCodeRequirements_FiltersbyCategory()
        {
            var requirements = _engine.GetCodeRequirements("Fire Protection");

            Assert.NotEmpty(requirements);
            Assert.All(requirements, r => Assert.Equal("Fire Protection", r.Category));
        }
    }

    #endregion
}
