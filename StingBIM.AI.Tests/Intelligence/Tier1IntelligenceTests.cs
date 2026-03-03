// ===================================================================
// StingBIM Tier 1-4 Intelligence Modules - Comprehensive Tests
// Tests for all 20 new intelligence engines
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace StingBIM.AI.Tests.Intelligence
{
    /// <summary>
    /// Comprehensive tests for Tier 1-4 Intelligence modules
    /// </summary>
    public class Tier1IntelligenceTests
    {
        #region Schedule Intelligence Tests

        [Fact]
        public void ScheduleIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void ScheduleIntelligence_CreateSchedule_CreatesValidSchedule()
        {
            var engine = StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance;
            var schedule = engine.CreateSchedule("proj-001", "Test Project",
                StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleType.Master);

            Assert.NotNull(schedule);
            Assert.NotNull(schedule.Id);
            Assert.Equal("proj-001", schedule.ProjectId);
            Assert.Equal("Test Project", schedule.ProjectName);
            Assert.NotEmpty(schedule.Calendars);
        }

        [Fact]
        public void ScheduleIntelligence_AddActivity_AddsActivityToSchedule()
        {
            var engine = StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance;
            var schedule = engine.CreateSchedule("proj-002", "Test Project 2",
                StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleType.Master);

            var activity = engine.AddActivity(schedule.Id, new StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleActivity
            {
                Name = "Foundation Work",
                Duration = 10,
                PlannedStart = DateTime.Today,
                Predecessors = new List<StingBIM.AI.Intelligence.ScheduleIntelligence.ActivityDependency>()
            });

            Assert.NotNull(activity);
            Assert.Equal("Foundation Work", activity.Name);
            Assert.Equal(10, activity.Duration);
        }

        [Fact]
        public async Task ScheduleIntelligence_CalculateCriticalPath_ReturnsResult()
        {
            var engine = StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance;
            var schedule = engine.CreateSchedule("proj-003", "CPM Test",
                StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleType.Master);

            // Add activities
            var a1 = engine.AddActivity(schedule.Id, new StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleActivity
            {
                Name = "Activity 1",
                Duration = 5,
                PlannedStart = DateTime.Today,
                Predecessors = new List<StingBIM.AI.Intelligence.ScheduleIntelligence.ActivityDependency>()
            });

            var a2 = engine.AddActivity(schedule.Id, new StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleActivity
            {
                Name = "Activity 2",
                Duration = 10,
                Predecessors = new List<StingBIM.AI.Intelligence.ScheduleIntelligence.ActivityDependency>
                {
                    new StingBIM.AI.Intelligence.ScheduleIntelligence.ActivityDependency
                    {
                        PredecessorId = a1.Id,
                        Type = StingBIM.AI.Intelligence.ScheduleIntelligence.DependencyType.FinishToStart
                    }
                }
            });

            var result = await engine.CalculateCriticalPathAsync(schedule.Id);

            Assert.NotNull(result);
            Assert.True(result.ProjectDuration > 0);
        }

        [Fact]
        public void ScheduleIntelligence_GenerateLookAhead_ReturnsValidPlan()
        {
            var engine = StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance;
            var schedule = engine.CreateSchedule("proj-004", "Look Ahead Test",
                StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleType.Master);

            engine.AddActivity(schedule.Id, new StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleActivity
            {
                Name = "Near Term Activity",
                Duration = 5,
                PlannedStart = DateTime.Today.AddDays(3),
                Predecessors = new List<StingBIM.AI.Intelligence.ScheduleIntelligence.ActivityDependency>()
            });

            var lookAhead = engine.GenerateLookAhead(schedule.Id, 3);

            Assert.NotNull(lookAhead);
            Assert.Equal(3, lookAhead.Weeks);
        }

        #endregion

        #region Clash Intelligence Tests

        [Fact]
        public void ClashIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void ClashIntelligence_CreateProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance;
            var project = engine.CreateProject("proj-clash-001", "Clash Test Project");

            Assert.NotNull(project);
            Assert.NotNull(project.Id);
            Assert.Equal("Clash Test Project", project.ProjectName);
        }

        [Fact]
        public void ClashIntelligence_CreateClashTest_CreatesValidTest()
        {
            var engine = StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance;
            var project = engine.CreateProject("proj-clash-002", "Clash Test Project 2");

            var test = engine.CreateClashTest(project.Id, new StingBIM.AI.Intelligence.ClashIntelligence.ClashTestDefinition
            {
                Name = "MEP vs Structure",
                Description = "Test MEP against structural elements",
                ClashType = StingBIM.AI.Intelligence.ClashIntelligence.ClashType.Hard,
                Tolerance = 0.0
            });

            Assert.NotNull(test);
            Assert.Equal("MEP vs Structure", test.Name);
        }

        [Fact]
        public async Task ClashIntelligence_RunClashDetection_ReturnsResults()
        {
            var engine = StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance;
            var project = engine.CreateProject("proj-clash-003", "Clash Detection Test");

            var test = engine.CreateClashTest(project.Id, new StingBIM.AI.Intelligence.ClashIntelligence.ClashTestDefinition
            {
                Name = "Full Clash Test",
                ClashType = StingBIM.AI.Intelligence.ClashIntelligence.ClashType.Hard
            });

            var result = await engine.RunClashDetectionAsync(test.Id);

            Assert.NotNull(result);
            Assert.NotNull(result.Clashes);
            Assert.NotNull(result.Statistics);
        }

        [Fact]
        public void ClashIntelligence_GenerateReport_CreatesValidReport()
        {
            var engine = StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance;
            var project = engine.CreateProject("proj-clash-004", "Clash Report Test");

            var test = engine.CreateClashTest(project.Id, new StingBIM.AI.Intelligence.ClashIntelligence.ClashTestDefinition
            {
                Name = "Report Test",
                ClashType = StingBIM.AI.Intelligence.ClashIntelligence.ClashType.Hard
            });

            var detection = engine.RunClashDetectionAsync(test.Id).Result;
            var report = engine.GenerateClashReport(detection.Id, new StingBIM.AI.Intelligence.ClashIntelligence.ClashReportOptions
            {
                ReportType = StingBIM.AI.Intelligence.ClashIntelligence.ClashReportType.Executive,
                IncludeStatistics = true
            });

            Assert.NotNull(report);
            Assert.NotEmpty(report.Sections);
        }

        #endregion

        #region Stakeholder Intelligence Tests

        [Fact]
        public void StakeholderIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void StakeholderIntelligence_AddStakeholder_CreatesValidStakeholder()
        {
            var engine = StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderIntelligenceEngine.Instance;

            var stakeholder = engine.AddStakeholder(new StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderInfo
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Title = "Project Manager",
                Influence = 8,
                Interest = 7
            });

            Assert.NotNull(stakeholder);
            Assert.Equal("John", stakeholder.FirstName);
            Assert.Equal("Doe", stakeholder.LastName);
            Assert.Equal(StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderQuadrant.ManageClosely, stakeholder.Quadrant);
        }

        [Fact]
        public void StakeholderIntelligence_CreateProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderIntelligenceEngine.Instance;
            var project = engine.CreateProject("stake-proj-001", "Stakeholder Test Project");

            Assert.NotNull(project);
            Assert.Equal("Stakeholder Test Project", project.ProjectName);
        }

        [Fact]
        public void StakeholderIntelligence_GenerateStakeholderMap_ReturnsValidMap()
        {
            var engine = StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderIntelligenceEngine.Instance;
            var project = engine.CreateProject("stake-proj-002", "Mapping Test");

            var stakeholder = engine.AddStakeholder(new StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderInfo
            {
                FirstName = "Jane",
                LastName = "Smith",
                Influence = 9,
                Interest = 8
            });

            engine.AssignStakeholderToProject(project.Id, stakeholder.Id,
                StingBIM.AI.Intelligence.StakeholderIntelligence.ProjectRole.Owner);

            var map = engine.GenerateStakeholderMap(project.Id);

            Assert.NotNull(map);
            Assert.NotNull(map.Quadrants);
        }

        #endregion

        #region Change Management Intelligence Tests

        [Fact]
        public void ChangeManagementIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeManagementIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeManagementIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void ChangeManagementIntelligence_CreateProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeManagementIntelligenceEngine.Instance;
            var project = engine.CreateProject("change-proj-001", "Change Test", 1000000m, 365);

            Assert.NotNull(project);
            Assert.Equal(1000000m, project.OriginalBudget);
            Assert.Equal(365, project.OriginalDuration);
        }

        [Fact]
        public void ChangeManagementIntelligence_CreateChangeRequest_CreatesValidRequest()
        {
            var engine = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeManagementIntelligenceEngine.Instance;
            var project = engine.CreateProject("change-proj-002", "CR Test", 500000m, 180);

            var request = engine.CreateChangeRequest(new StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeRequestInput
            {
                ProjectId = project.Id,
                Title = "Add new conference room",
                Description = "Client requested additional conference room on 3rd floor",
                Category = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeCategory.Scope,
                Priority = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangePriority.Medium,
                RequestedBy = "Client Rep"
            });

            Assert.NotNull(request);
            Assert.StartsWith("CR-", request.Number);
            Assert.Equal(StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeRequestStatus.Draft, request.Status);
        }

        [Fact]
        public async Task ChangeManagementIntelligence_AnalyzeImpact_ReturnsAnalysis()
        {
            var engine = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeManagementIntelligenceEngine.Instance;
            var project = engine.CreateProject("change-proj-003", "Impact Test", 750000m, 240);

            var request = engine.CreateChangeRequest(new StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeRequestInput
            {
                ProjectId = project.Id,
                Title = "Design change",
                Category = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeCategory.Design,
                Priority = StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangePriority.High,
                RequestedBy = "Architect",
                AffectedAreas = new List<string> { "Level 2", "Level 3" },
                AffectedDisciplines = new List<string> { "Architectural", "MEP" }
            });

            var analysis = await engine.AnalyzeImpactAsync(request.Id);

            Assert.NotNull(analysis);
            Assert.NotNull(analysis.CostImpact);
            Assert.NotNull(analysis.ScheduleImpact);
            Assert.True(analysis.OverallImpactScore > 0);
        }

        #endregion

        #region Communication Intelligence Tests

        [Fact]
        public void CommunicationIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.CommunicationIntelligence.CommunicationIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.CommunicationIntelligence.CommunicationIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void CommunicationIntelligence_CreateRFI_CreatesValidRFI()
        {
            var engine = StingBIM.AI.Intelligence.CommunicationIntelligence.CommunicationIntelligenceEngine.Instance;
            var project = engine.CreateProject("comm-proj-001", "RFI Test");

            var rfi = engine.CreateRFI(new StingBIM.AI.Intelligence.CommunicationIntelligence.RFIInput
            {
                ProjectId = project.Id,
                Subject = "Foundation detail clarification",
                Question = "Please clarify the rebar spacing at foundation corners",
                Category = StingBIM.AI.Intelligence.CommunicationIntelligence.RFICategory.Clarification,
                Priority = StingBIM.AI.Intelligence.CommunicationIntelligence.RFIPriority.High,
                RequestedBy = "Site Engineer",
                AssignedTo = "Structural Engineer"
            });

            Assert.NotNull(rfi);
            Assert.StartsWith("RFI-", rfi.Number);
            Assert.Equal(StingBIM.AI.Intelligence.CommunicationIntelligence.RFIStatus.Draft, rfi.Status);
        }

        [Fact]
        public void CommunicationIntelligence_CreateSubmittal_CreatesValidSubmittal()
        {
            var engine = StingBIM.AI.Intelligence.CommunicationIntelligence.CommunicationIntelligenceEngine.Instance;
            var project = engine.CreateProject("comm-proj-002", "Submittal Test");

            var submittal = engine.CreateSubmittal(new StingBIM.AI.Intelligence.CommunicationIntelligence.SubmittalInput
            {
                ProjectId = project.Id,
                Title = "HVAC Equipment",
                SpecSection = "15700",
                Category = StingBIM.AI.Intelligence.CommunicationIntelligence.SubmittalCategory.Mechanical,
                Type = StingBIM.AI.Intelligence.CommunicationIntelligence.SubmittalType.ProductData,
                Priority = StingBIM.AI.Intelligence.CommunicationIntelligence.SubmittalPriority.High,
                SubmittedBy = "MEP Contractor"
            });

            Assert.NotNull(submittal);
            Assert.StartsWith("SUB-", submittal.Number);
            Assert.NotEmpty(submittal.ReviewPath);
        }

        [Fact]
        public void CommunicationIntelligence_GetBallInCourtReport_ReturnsReport()
        {
            var engine = StingBIM.AI.Intelligence.CommunicationIntelligence.CommunicationIntelligenceEngine.Instance;
            var project = engine.CreateProject("comm-proj-003", "BIC Test");

            var report = engine.GetBallInCourtReport(project.Id);

            Assert.NotNull(report);
            Assert.NotNull(report.ByPerson);
            Assert.NotNull(report.OverdueItems);
        }

        #endregion

        #region Safety Intelligence Tests

        [Fact]
        public void SafetyIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void SafetyIntelligence_CreateJHA_CreatesValidJHA()
        {
            var engine = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            var project = engine.CreateProject("safety-proj-001", "JHA Test");

            var jha = engine.CreateJHA(new StingBIM.AI.Intelligence.SafetyIntelligence.JHAInput
            {
                ProjectId = project.Id,
                TaskName = "Roof Installation",
                TaskType = StingBIM.AI.Intelligence.SafetyIntelligence.TaskType.RoofWork,
                WorkArea = "Building A Roof",
                PreparedBy = "Safety Manager"
            });

            Assert.NotNull(jha);
            Assert.StartsWith("JHA-", jha.Number);
            Assert.NotEmpty(jha.RequiredPPE);
        }

        [Fact]
        public void SafetyIntelligence_ReportIncident_CreatesValidIncident()
        {
            var engine = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            var project = engine.CreateProject("safety-proj-002", "Incident Test");

            var incident = engine.ReportIncident(new StingBIM.AI.Intelligence.SafetyIntelligence.IncidentReport
            {
                ProjectId = project.Id,
                Type = StingBIM.AI.Intelligence.SafetyIntelligence.IncidentType.NearMiss,
                Severity = StingBIM.AI.Intelligence.SafetyIntelligence.IncidentSeverity.Minor,
                DateTime = DateTime.Now,
                Location = "Level 2 East",
                Description = "Near miss - falling material",
                ReportedBy = "Site Supervisor"
            });

            Assert.NotNull(incident);
            Assert.StartsWith("INC-", incident.Number);
            Assert.Equal(StingBIM.AI.Intelligence.SafetyIntelligence.IncidentStatus.Reported, incident.Status);
        }

        [Fact]
        public void SafetyIntelligence_CreateInspection_CreatesValidInspection()
        {
            var engine = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            var project = engine.CreateProject("safety-proj-003", "Inspection Test");

            var inspection = engine.CreateInspection(new StingBIM.AI.Intelligence.SafetyIntelligence.InspectionInput
            {
                ProjectId = project.Id,
                Type = StingBIM.AI.Intelligence.SafetyIntelligence.InspectionType.Weekly,
                Area = "Building A",
                Inspector = "Safety Officer"
            });

            Assert.NotNull(inspection);
            Assert.StartsWith("INS-", inspection.Number);
            Assert.NotEmpty(inspection.Checklist);
        }

        [Fact]
        public void SafetyIntelligence_GetSafetyMetrics_ReturnsMetrics()
        {
            var engine = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            var project = engine.CreateProject("safety-proj-004", "Metrics Test");

            var metrics = engine.GetSafetyMetrics(project.Id, 100000);

            Assert.NotNull(metrics);
            Assert.Equal(100000, metrics.TotalManhours);
            Assert.NotNull(metrics.Statistics);
        }

        [Fact]
        public async Task SafetyIntelligence_AssessJHARisks_ReturnsAssessment()
        {
            var engine = StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance;
            var project = engine.CreateProject("safety-proj-005", "Risk Assessment Test");

            var jha = engine.CreateJHA(new StingBIM.AI.Intelligence.SafetyIntelligence.JHAInput
            {
                ProjectId = project.Id,
                TaskName = "Steel Erection",
                TaskType = StingBIM.AI.Intelligence.SafetyIntelligence.TaskType.SteelErection,
                PreparedBy = "Safety Manager"
            });

            engine.AddJHAStep(jha.Id, new StingBIM.AI.Intelligence.SafetyIntelligence.JHAStepInput
            {
                Description = "Lift steel beam",
                Hazards = new List<StingBIM.AI.Intelligence.SafetyIntelligence.Hazard>
                {
                    new StingBIM.AI.Intelligence.SafetyIntelligence.Hazard
                    {
                        Id = "h1",
                        Description = "Struck by falling beam",
                        Severity = StingBIM.AI.Intelligence.SafetyIntelligence.SeverityLevel.Severe,
                        Likelihood = StingBIM.AI.Intelligence.SafetyIntelligence.LikelihoodLevel.Possible
                    }
                },
                Controls = new List<StingBIM.AI.Intelligence.SafetyIntelligence.HazardControl>
                {
                    new StingBIM.AI.Intelligence.SafetyIntelligence.HazardControl
                    {
                        HazardId = "h1",
                        Type = StingBIM.AI.Intelligence.SafetyIntelligence.ControlType.Engineering,
                        Description = "Use tag lines"
                    }
                }
            });

            var assessment = await engine.AssessJHARisksAsync(jha.Id);

            Assert.NotNull(assessment);
            Assert.NotEmpty(assessment.StepAssessments);
        }

        #endregion
    }
}
