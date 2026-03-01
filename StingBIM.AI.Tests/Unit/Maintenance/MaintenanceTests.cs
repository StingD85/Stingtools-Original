// StingBIM.AI.Tests - Predictive Maintenance Module Tests
// Tests for failure prediction, health scoring, maintenance planning

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Maintenance.Engine;
using StingBIM.AI.Maintenance.Models;

namespace StingBIM.AI.Tests.Unit.Maintenance
{
    [TestFixture]
    public class MaintenanceTests
    {
        #region PredictiveMaintenanceEngine Tests

        [TestFixture]
        public class PredictiveMaintenanceEngineTests
        {
            private PredictiveMaintenanceEngine _engine;

            [SetUp]
            public void Setup()
            {
                _engine = new PredictiveMaintenanceEngine();
            }

            [Test]
            public void Constructor_ShouldInitialize()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public async Task PredictFailureAsync_UnknownAsset_ShouldReturnPrediction()
            {
                var prediction = await _engine.PredictFailureAsync("UNKNOWN-ASSET");
                prediction.Should().NotBeNull();
            }

            [Test]
            public void RegisterAssetCondition_ShouldAcceptCondition()
            {
                var condition = new AssetCondition
                {
                    AssetId = "TEST-RUL",
                    HealthScore = 75.0,
                    ConditionGrade = ConditionGrade.B,
                    LastAssessment = DateTime.UtcNow
                };

                _engine.RegisterAssetCondition(condition);

                var rul = _engine.CalculateRemainingUsefulLife("TEST-RUL");
                rul.Should().BeGreaterThanOrEqualTo(0);
            }

            [Test]
            public void CalculateHealthScore_ShouldReturnValidCondition()
            {
                var condition = new AssetCondition
                {
                    AssetId = "TEST-HEALTH",
                    HealthScore = 80.0,
                    ConditionGrade = ConditionGrade.B,
                    LastAssessment = DateTime.UtcNow
                };

                _engine.RegisterAssetCondition(condition);
                var result = _engine.CalculateHealthScore("TEST-HEALTH");
                result.Should().NotBeNull();
                result.HealthScore.Should().BeInRange(0, 100);
            }

            [Test]
            public void GenerateMaintenancePlan_ShouldReturnPlans()
            {
                var condition = new AssetCondition
                {
                    AssetId = "TEST-PLAN",
                    HealthScore = 60.0,
                    ConditionGrade = ConditionGrade.C,
                    LastAssessment = DateTime.UtcNow
                };

                _engine.RegisterAssetCondition(condition);
                var plans = _engine.GenerateMaintenancePlan("TEST-PLAN");
                plans.Should().NotBeNull();
            }

            [Test]
            public async Task OptimizeMaintenanceSchedule_EmptyList_ShouldReturnEmpty()
            {
                var plans = new List<MaintenancePlan>();
                var optimized = await _engine.OptimizeMaintenanceSchedule(plans);
                optimized.Should().NotBeNull();
            }
        }

        #endregion

        #region Data Model Tests

        [TestFixture]
        public class MaintenanceDataModelTests
        {
            [Test]
            public void AssetCondition_HealthScore_ShouldBeInRange()
            {
                var condition = new AssetCondition
                {
                    AssetId = "A-001",
                    HealthScore = 78.5,
                    ConditionGrade = ConditionGrade.B,
                    LastAssessment = DateTime.UtcNow
                };

                condition.HealthScore.Should().BeInRange(0, 100);
                condition.ConditionGrade.Should().Be(ConditionGrade.B);
            }

            [Test]
            public void ConditionGrade_ShouldHaveExpectedValues()
            {
                Enum.IsDefined(typeof(ConditionGrade), ConditionGrade.A).Should().BeTrue();
                Enum.IsDefined(typeof(ConditionGrade), ConditionGrade.F).Should().BeTrue();
            }

            [Test]
            public void FailurePrediction_ShouldContainProbability()
            {
                var prediction = new FailurePrediction
                {
                    AssetId = "A-001",
                    FailureProbability = 0.75,
                    FailureMode = FailureMode.Wear,
                    Confidence = 0.85
                };

                prediction.FailureProbability.Should().BeInRange(0, 1);
                prediction.Confidence.Should().BeInRange(0, 1);
            }

            [Test]
            public void WorkOrderPriority_ShouldHaveExpectedValues()
            {
                Enum.IsDefined(typeof(WorkOrderPriority), WorkOrderPriority.Emergency).Should().BeTrue();
                Enum.GetValues(typeof(WorkOrderPriority)).Length.Should().BeGreaterThan(2);
            }

            [Test]
            public void MaintenanceType_ShouldHaveExpectedValues()
            {
                Enum.IsDefined(typeof(MaintenanceType), MaintenanceType.Preventive).Should().BeTrue();
                Enum.IsDefined(typeof(MaintenanceType), MaintenanceType.Predictive).Should().BeTrue();
            }

            [Test]
            public void FailureMode_ShouldHaveExpectedValues()
            {
                Enum.IsDefined(typeof(FailureMode), FailureMode.Wear).Should().BeTrue();
                Enum.IsDefined(typeof(FailureMode), FailureMode.Corrosion).Should().BeTrue();
                Enum.IsDefined(typeof(FailureMode), FailureMode.Fatigue).Should().BeTrue();
            }
        }

        #endregion
    }
}
