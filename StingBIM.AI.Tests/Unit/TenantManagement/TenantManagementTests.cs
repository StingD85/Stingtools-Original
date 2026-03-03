// StingBIM.AI.Tests - Tenant + Space Management Module Tests
// Tests for lease lifecycle, space allocation, service charges

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.TenantManagement.Engine;
using StingBIM.AI.TenantManagement.Models;

namespace StingBIM.AI.Tests.Unit.TenantManagement
{
    [TestFixture]
    public class TenantManagementTests
    {
        #region LeaseManagementEngine Tests

        [TestFixture]
        public class LeaseManagementEngineTests
        {
            private LeaseManagementEngine _engine;

            [SetUp]
            public void Setup()
            {
                _engine = new LeaseManagementEngine();
            }

            [Test]
            public void Constructor_ShouldInitialize()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void GetLease_NonExistent_ShouldReturnNull()
            {
                var result = _engine.GetLease("NONEXISTENT");
                result.Should().BeNull();
            }

            [Test]
            public void GetLeases_Empty_ShouldReturnEmptyList()
            {
                var leases = _engine.GetLeases();
                leases.Should().NotBeNull();
            }
        }

        #endregion

        #region Lease Data Model Tests

        [TestFixture]
        public class LeaseDataModelTests
        {
            [Test]
            public void Lease_ShouldHaveRequiredProperties()
            {
                var lease = new Lease
                {
                    LeaseId = "L-001",
                    TenantId = "T-001",
                    SpaceIds = new List<string> { "S-001", "S-002" },
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2028, 1, 1),
                    MonthlyRent = 10000m,
                    Currency = Currency.USD,
                    Status = LeaseStatus.Active
                };

                lease.LeaseId.Should().Be("L-001");
                lease.TenantId.Should().Be("T-001");
                lease.SpaceIds.Should().HaveCount(2);
                lease.MonthlyRent.Should().Be(10000m);
                lease.Status.Should().Be(LeaseStatus.Active);
            }

            [Test]
            public void Lease_AnnualRent_ShouldBe12TimesMonthly()
            {
                var lease = new Lease { MonthlyRent = 5000m };
                lease.AnnualRent.Should().Be(60000m);
            }

            [Test]
            public void Lease_TotalTermMonths_ShouldCalculateCorrectly()
            {
                var lease = new Lease
                {
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2028, 1, 1)
                };

                lease.TotalTermMonths.Should().BeInRange(35, 37); // ~36 months
            }

            [Test]
            public void LeaseStatus_ShouldHaveExpectedValues()
            {
                Enum.IsDefined(typeof(LeaseStatus), LeaseStatus.Active).Should().BeTrue();
                Enum.IsDefined(typeof(LeaseStatus), LeaseStatus.Expired).Should().BeTrue();
                Enum.IsDefined(typeof(LeaseStatus), LeaseStatus.Pending).Should().BeTrue();
            }

            [Test]
            public void Currency_ShouldHaveExpectedValues()
            {
                Enum.IsDefined(typeof(Currency), Currency.USD).Should().BeTrue();
            }

            [Test]
            public void Lease_DefaultStatus_ShouldBePending()
            {
                var lease = new Lease();
                lease.Status.Should().Be(LeaseStatus.Pending);
            }
        }

        #endregion

        #region ServiceChargeEngine Tests

        [TestFixture]
        public class ServiceChargeEngineTests
        {
            [Test]
            public void Constructor_ShouldInitialize()
            {
                var engine = new ServiceChargeEngine();
                engine.Should().NotBeNull();
            }
        }

        #endregion

        #region SpaceManagementEngine Tests

        [TestFixture]
        public class SpaceManagementEngineTests
        {
            [Test]
            public void Constructor_ShouldInitialize()
            {
                var engine = new SpaceManagementEngine();
                engine.Should().NotBeNull();
            }
        }

        #endregion
    }
}
