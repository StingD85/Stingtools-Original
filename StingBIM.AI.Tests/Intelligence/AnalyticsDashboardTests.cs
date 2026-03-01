// ============================================================================
// StingBIM AI Tests - Analytics Dashboard Tests
// Unit tests for project KPIs, dashboards, and executive reporting
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using StingBIM.AI.Intelligence.Analytics;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class AnalyticsDashboardTests
    {
        private AnalyticsDashboard _dashboard;

        [SetUp]
        public void Setup()
        {
            _dashboard = AnalyticsDashboard.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = AnalyticsDashboard.Instance;
            var instance2 = AnalyticsDashboard.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        #region Project Registration Tests

        [Test]
        public void RegisterProject_ShouldCreateProject()
        {
            // Arrange
            var config = new ProjectConfig
            {
                ProjectName = "Analytics Test Project",
                ProjectCode = "ATP-001",
                ClientName = "Test Client",
                Location = "New York, NY",
                ProjectType = "Commercial Office",
                TotalBudget = 25000000m,
                PlannedDuration = 24,
                StartDate = DateTime.Today,
                PlannedEndDate = DateTime.Today.AddMonths(24),
                GrossArea = 50000
            };

            // Act
            var project = _dashboard.RegisterProject(config);

            // Assert
            Assert.That(project, Is.Not.Null);
            Assert.That(project.ProjectId, Is.Not.Null);
            Assert.That(project.ProjectName, Is.EqualTo("Analytics Test Project"));
            Assert.That(project.TotalBudget, Is.EqualTo(25000000m));
        }

        #endregion

        #region KPI Definition Tests

        [Test]
        public void GetAvailableKPIs_ShouldReturnPredefinedKPIs()
        {
            // Act
            var kpis = _dashboard.GetAvailableKPIs();

            // Assert
            Assert.That(kpis, Is.Not.Null);
            Assert.That(kpis.Count >= 9, Is.True);
            Assert.That(kpis.Exists(k => k.Code == "SPI"), Is.True);
            Assert.That(kpis.Exists(k => k.Code == "CPI"), Is.True);
            Assert.That(kpis.Exists(k => k.Code == "CLASH_RATE"), Is.True);
            Assert.That(kpis.Exists(k => k.Code == "MODEL_HEALTH"), Is.True);
        }

        [Test]
        public void DefineCustomKPI_ShouldCreateKPI()
        {
            // Arrange
            var definition = new KPIDefinition
            {
                Code = "CUSTOM_KPI_001",
                Name = "Custom Safety Index",
                Description = "Custom safety performance metric",
                Category = KPICategory.Quality,
                Unit = "index",
                TargetValue = 100,
                WarningThreshold = 80,
                CriticalThreshold = 60,
                HigherIsBetter = true
            };

            // Act
            var kpi = _dashboard.DefineCustomKPI(definition);

            // Assert
            Assert.That(kpi, Is.Not.Null);
            Assert.That(kpi.Code, Is.EqualTo("CUSTOM_KPI_001"));
            Assert.That(kpi.Category, Is.EqualTo(KPICategory.Quality));
        }

        #endregion

        #region KPI Recording Tests

        [Test]
        public void RecordKPIValue_ShouldStoreValue()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "KPI Recording Test",
                TotalBudget = 1000000m
            });

            // Act
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.95, "On track");

            // Assert - Get dashboard to verify
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);
            Assert.That(dashboardData, Is.Not.Null);
        }

        [Test]
        public void RecordKPIValue_ShouldTriggerAlertWhenBelowThreshold()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Alert Test Project",
                TotalBudget = 5000000m
            });

            KPIAlertEventArgs alertArgs = null;
            _dashboard.KPIAlertTriggered += (s, e) => alertArgs = e;

            // Act - Record CPI below critical threshold (0.85)
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 0.80);

            // Assert
            Assert.That(alertArgs, Is.Not.Null);
            Assert.That(alertArgs.KPICode, Is.EqualTo("CPI"));
            Assert.That(alertArgs.Level, Is.EqualTo(AlertLevel.Critical));
        }

        [Test]
        public void RecordKPIValue_ShouldTriggerWarningAlert()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Warning Alert Test",
                TotalBudget = 3000000m
            });

            KPIAlertEventArgs alertArgs = null;
            _dashboard.KPIAlertTriggered += (s, e) => alertArgs = e;

            // Act - Record SPI at warning level (between 0.9 and 0.95)
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.92);

            // Assert
            Assert.That(alertArgs, Is.Not.Null);
            Assert.That(alertArgs.Level, Is.EqualTo(AlertLevel.Warning));
        }

        #endregion

        #region Cost Update Tests

        [Test]
        public void UpdateProjectCosts_ShouldUpdateCostData()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Cost Update Test",
                TotalBudget = 10000000m
            });

            var costs = new CostUpdate
            {
                CommittedCost = 5000000m,
                ActualCost = 4500000m,
                ApprovedChanges = 250000m,
                PendingChanges = 100000m,
                Contingency = 500000m,
                ContingencyUsed = 150000m
            };

            // Act
            _dashboard.UpdateProjectCosts(project.ProjectId, costs);
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);

            // Assert
            Assert.That(dashboardData.CostSummary, Is.Not.Null);
            Assert.That(dashboardData.CostSummary.TotalBudget, Is.EqualTo(10000000m));
        }

        #endregion

        #region Schedule Update Tests

        [Test]
        public void UpdateProjectSchedule_ShouldUpdateScheduleData()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Schedule Update Test",
                TotalBudget = 8000000m,
                PlannedDuration = 18
            });

            var schedule = new ScheduleUpdate
            {
                PlannedProgress = 50,
                ActualProgress = 48,
                PlannedDuration = 18,
                ActualDuration = 9,
                ForecastDuration = 19,
                CriticalActivities = 25,
                DelayedActivities = 3
            };

            // Act
            _dashboard.UpdateProjectSchedule(project.ProjectId, schedule);
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);

            // Assert
            Assert.That(dashboardData.ScheduleSummary, Is.Not.Null);
            Assert.That(dashboardData.ScheduleSummary.PlannedProgress, Is.EqualTo(50));
            Assert.That(dashboardData.ScheduleSummary.ActualProgress, Is.EqualTo(48));
        }

        #endregion

        #region Model Metrics Tests

        [Test]
        public void UpdateModelMetrics_ShouldUpdateMetrics()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Model Metrics Test",
                TotalBudget = 6000000m
            });

            var metrics = new ModelMetrics
            {
                TotalElements = 50000,
                ModelledElements = 45000,
                ClashCount = 150,
                ResolvedClashes = 120,
                WarningsCount = 85,
                ErrorsCount = 5,
                ModelSize = 850
            };

            // Act
            _dashboard.UpdateModelMetrics(project.ProjectId, metrics);
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);

            // Assert
            Assert.That(dashboardData.ModelSummary, Is.Not.Null);
            Assert.That(dashboardData.ModelSummary.TotalElements, Is.EqualTo(50000));
            Assert.That(dashboardData.ModelSummary.ClashCount, Is.EqualTo(150));
        }

        #endregion

        #region Quality Metrics Tests

        [Test]
        public void UpdateQualityMetrics_ShouldUpdateMetrics()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Quality Metrics Test",
                TotalBudget = 7000000m
            });

            var quality = new QualityMetrics
            {
                InspectionsCompleted = 50,
                InspectionsPassed = 45,
                PunchItems = 120,
                PunchItemsClosed = 80,
                NCRsRaised = 10,
                NCRsClosed = 8,
                RFIsSubmitted = 35,
                RFIsAnswered = 30
            };

            // Act
            _dashboard.UpdateQualityMetrics(project.ProjectId, quality);
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);

            // Assert
            Assert.That(dashboardData.QualitySummary, Is.Not.Null);
            Assert.That(dashboardData.QualitySummary.InspectionsCompleted, Is.EqualTo(50));
        }

        #endregion

        #region Dashboard Tests

        [Test]
        public void GetProjectDashboard_ShouldReturnCompleteDashboard()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Dashboard Test",
                TotalBudget = 15000000m,
                ProjectType = "Healthcare"
            });

            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.98);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 1.02);

            // Act
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);

            // Assert
            Assert.That(dashboardData, Is.Not.Null);
            Assert.That(dashboardData.ProjectId, Is.EqualTo(project.ProjectId));
            Assert.That(dashboardData.KPIs, Is.Not.Null);
            Assert.That(dashboardData.HealthScore, Is.Not.Null);
        }

        [Test]
        public void GetProjectDashboard_ShouldCalculateHealthScore()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Health Score Test",
                TotalBudget = 20000000m
            });

            // Good KPI values
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 1.0);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 1.0);
            _dashboard.RecordKPIValue(project.ProjectId, "MODEL_HEALTH", 95);

            // Act
            var dashboardData = _dashboard.GetProjectDashboard(project.ProjectId);

            // Assert
            Assert.That(dashboardData.HealthScore >= 0, Is.True);
            Assert.That(dashboardData.HealthScore <= 100, Is.True);
        }

        #endregion

        #region Portfolio Overview Tests

        [Test]
        public void GetPortfolioOverview_ShouldReturnOverview()
        {
            // Arrange
            _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Portfolio Project 1",
                TotalBudget = 10000000m,
                ProjectType = "Commercial"
            });

            _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Portfolio Project 2",
                TotalBudget = 15000000m,
                ProjectType = "Residential"
            });

            // Act
            var overview = _dashboard.GetPortfolioOverview();

            // Assert
            Assert.That(overview, Is.Not.Null);
            Assert.That(overview.TotalProjects >= 2, Is.True);
            Assert.That(overview.TotalBudget >= 25000000m, Is.True);
            Assert.That(overview.ProjectsByType, Is.Not.Null);
        }

        [Test]
        public void GetPortfolioOverview_ShouldCalculateAverageHealth()
        {
            // Arrange
            var project1 = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Health Calc Project 1",
                TotalBudget = 5000000m
            });
            _dashboard.RecordKPIValue(project1.ProjectId, "SPI", 1.0);
            _dashboard.RecordKPIValue(project1.ProjectId, "CPI", 1.0);

            var project2 = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Health Calc Project 2",
                TotalBudget = 5000000m
            });
            _dashboard.RecordKPIValue(project2.ProjectId, "SPI", 0.9);
            _dashboard.RecordKPIValue(project2.ProjectId, "CPI", 0.95);

            // Act
            var overview = _dashboard.GetPortfolioOverview();

            // Assert
            Assert.That(overview.AverageHealthScore >= 0, Is.True);
        }

        #endregion

        #region Executive Report Tests

        [Test]
        public void GenerateExecutiveReport_ShouldGenerateReport()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Executive Report Test",
                TotalBudget = 30000000m,
                ProjectType = "Mixed Use"
            });

            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.95);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 0.98);
            _dashboard.UpdateProjectCosts(project.ProjectId, new CostUpdate
            {
                CommittedCost = 15000000m,
                ActualCost = 14000000m
            });

            // Act
            var report = _dashboard.GenerateExecutiveReport(project.ProjectId);

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.Length > 0, Is.True);
            Assert.That(report.Contains("EXECUTIVE PROJECT REPORT"), Is.True);
            Assert.That(report.Contains(project.ProjectName), Is.True);
        }

        [Test]
        public void GenerateExecutiveReport_ShouldIncludeRecommendations()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Recommendations Test",
                TotalBudget = 12000000m
            });

            // Record poor KPIs to trigger recommendations
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.85);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 0.88);

            // Act
            var report = _dashboard.GenerateExecutiveReport(project.ProjectId);

            // Assert
            Assert.That(report.Contains("RECOMMENDATIONS"), Is.True);
        }

        #endregion

        #region Trend Analysis Tests

        [Test]
        public void GetKPITrend_ShouldReturnHistoricalData()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Trend Analysis Test",
                TotalBudget = 8000000m
            });

            // Record multiple values over time
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.92);
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.94);
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.96);
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.98);

            // Act
            var trend = _dashboard.GetKPITrend(project.ProjectId, "SPI");

            // Assert
            Assert.That(trend, Is.Not.Null);
            Assert.That(trend.DataPoints.Count >= 4, Is.True);
            Assert.That(trend.TrendDirection >= 0, Is.True); // Improving trend
        }

        [Test]
        public void CreateSnapshot_ShouldCaptureCurrentState()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Snapshot Test",
                TotalBudget = 9000000m
            });

            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.97);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 1.01);

            // Act
            var snapshot = _dashboard.CreateSnapshot(project.ProjectId, "Weekly Status");

            // Assert
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.SnapshotId, Is.Not.Null);
            Assert.That(snapshot.Name, Is.EqualTo("Weekly Status"));
            Assert.That(snapshot.KPIValues.Count > 0, Is.True);
        }

        #endregion

        #region Benchmark Tests

        [Test]
        public void GetBenchmarks_ShouldReturnBenchmarkData()
        {
            // Act
            var benchmarks = _dashboard.GetBenchmarks("Commercial");

            // Assert
            Assert.That(benchmarks, Is.Not.Null);
            Assert.That(benchmarks.Count > 0, Is.True);
            Assert.That(benchmarks.ContainsKey("SPI"), Is.True);
            Assert.That(benchmarks.ContainsKey("CPI"), Is.True);
        }

        [Test]
        public void CompareToBaseline_ShouldReturnComparison()
        {
            // Arrange
            var project = _dashboard.RegisterProject(new ProjectConfig
            {
                ProjectName = "Baseline Comparison Test",
                TotalBudget = 11000000m
            });

            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.95);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 1.02);

            // Create baseline snapshot
            _dashboard.CreateSnapshot(project.ProjectId, "Baseline");

            // Update KPIs
            _dashboard.RecordKPIValue(project.ProjectId, "SPI", 0.98);
            _dashboard.RecordKPIValue(project.ProjectId, "CPI", 1.05);

            // Act
            var comparison = _dashboard.CompareToBaseline(project.ProjectId);

            // Assert
            Assert.That(comparison, Is.Not.Null);
        }

        #endregion
    }
}
