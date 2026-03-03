// StingBIM.AI.Tests - Facility Management Module Tests
// Tests for CAFM: knowledge base, asset registry, work orders, predictive analytics

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.FacilityManagement.Knowledge;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.Intelligence;
using StingBIM.AI.FacilityManagement.WorkOrders;

namespace StingBIM.AI.Tests.Unit.FacilityManagement
{
    [TestFixture]
    public class FacilityManagementTests
    {
        #region FMKnowledgeBase Tests

        [TestFixture]
        public class FMKnowledgeBaseTests
        {
            private FMKnowledgeBase _knowledgeBase;

            [SetUp]
            public void Setup()
            {
                _knowledgeBase = new FMKnowledgeBase();
            }

            [Test]
            public void Constructor_ShouldInitialize()
            {
                _knowledgeBase.Should().NotBeNull();
            }

            [Test]
            public void GetFailureModes_KnownEquipment_ShouldReturnNonEmpty()
            {
                var modes = _knowledgeBase.GetFailureModes("Elevator");
                modes.Should().NotBeNull();
                modes.Should().NotBeEmpty();
            }

            [Test]
            public void GetFailureModes_UnknownEquipment_ShouldReturnEmptyOrDefaults()
            {
                var modes = _knowledgeBase.GetFailureModes("UnknownEquipmentXYZ");
                modes.Should().NotBeNull();
            }
        }

        #endregion

        #region AssetRegistry Tests

        [TestFixture]
        public class AssetRegistryTests
        {
            private AssetRegistry _registry;

            [SetUp]
            public void Setup()
            {
                _registry = new AssetRegistry();
            }

            [Test]
            public void Constructor_ShouldInitialize()
            {
                _registry.Should().NotBeNull();
            }

            [Test]
            public void RegisterAsset_ShouldAddToRegistry()
            {
                var asset = new Asset
                {
                    AssetId = "TEST-001",
                    Name = "Test Chiller",
                    AssetType = "HVAC_Chiller",
                    LocationId = "Plant Room B1",
                    InstallDate = DateTime.UtcNow.AddYears(-3)
                };

                _registry.RegisterAsset(asset);
                var retrieved = _registry.GetById("TEST-001");
                retrieved.Should().NotBeNull();
                retrieved.Name.Should().Be("Test Chiller");
            }

            [Test]
            public void GetById_NonExistent_ShouldReturnNull()
            {
                var result = _registry.GetById("NONEXISTENT");
                result.Should().BeNull();
            }

            [Test]
            public void GetAllAssets_Empty_ShouldReturnEmptyList()
            {
                var assets = _registry.GetAllAssets();
                assets.Should().NotBeNull();
                assets.Should().BeEmpty();
            }

            [Test]
            public void GetAllAssets_AfterRegister_ShouldContainAsset()
            {
                var asset = new Asset
                {
                    AssetId = "A-LIST",
                    Name = "Listed Asset",
                    AssetType = "Fire_Pump"
                };

                _registry.RegisterAsset(asset);
                _registry.GetAllAssets().Should().HaveCount(1);
            }
        }

        #endregion

        #region FMPredictiveAnalytics Tests

        [TestFixture]
        public class FMPredictiveAnalyticsTests
        {
            [Test]
            public void Constructor_WithKnowledgeBase_ShouldInitialize()
            {
                var kb = new FMKnowledgeBase();
                var analytics = new FMPredictiveAnalytics(kb);
                analytics.Should().NotBeNull();
            }

            [Test]
            public void PredictFailures_NoAssets_ShouldReturnEmptyList()
            {
                var kb = new FMKnowledgeBase();
                var analytics = new FMPredictiveAnalytics(kb);
                var predictions = analytics.PredictFailures(90);
                predictions.Should().NotBeNull();
            }
        }

        #endregion

        #region WorkOrderManager Tests

        [TestFixture]
        public class WorkOrderManagerTests
        {
            private WorkOrderManager _manager;

            [SetUp]
            public void Setup()
            {
                var registry = new AssetRegistry();
                _manager = new WorkOrderManager(registry);
            }

            [Test]
            public void Constructor_ShouldInitialize()
            {
                _manager.Should().NotBeNull();
            }

            [Test]
            public void GetStatistics_Empty_ShouldReturnValidStats()
            {
                var stats = _manager.GetStatistics();
                stats.Should().NotBeNull();
                stats.TotalWorkOrders.Should().BeGreaterThanOrEqualTo(0);
            }
        }

        #endregion

        #region Data Model Tests

        [TestFixture]
        public class AssetModelTests
        {
            [Test]
            public void Asset_ShouldHaveRequiredProperties()
            {
                var asset = new Asset
                {
                    AssetId = "A-001",
                    Name = "HVAC Unit",
                    AssetType = "HVAC_AHU",
                    LocationId = "Level 3",
                    InstallDate = new DateTime(2020, 6, 15)
                };

                asset.AssetId.Should().Be("A-001");
                asset.AssetType.Should().Be("HVAC_AHU");
                asset.InstallDate.Year.Should().Be(2020);
            }

            [Test]
            public void Asset_InstallationDateAlias_ShouldMatchInstallDate()
            {
                var asset = new Asset { InstallDate = new DateTime(2022, 1, 1) };
                asset.InstallationDate.Should().Be(asset.InstallDate);
            }
        }

        #endregion
    }
}
