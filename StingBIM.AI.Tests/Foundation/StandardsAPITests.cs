using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Standards;

namespace StingBIM.AI.Tests.Foundation
{
    /// <summary>
    /// Tests for StandardsAPI facade and individual standards implementations.
    /// </summary>
    [TestFixture]
    public class StandardsAPITests
    {
        [Test]
        public void GetAllStandards_Returns34Standards()
        {
            var standards = StandardsAPI.GetAllStandards();
            standards.Should().HaveCountGreaterOrEqualTo(34,
                "all 34 international building standards should be registered");
        }

        [Test]
        public void GetAllStandards_ContainsAllRegionalStandards()
        {
            var standards = StandardsAPI.GetAllStandards();
            var names = standards.Select(s => s.ShortName).ToList();

            // East African
            names.Should().Contain("UNBS", "Uganda standards should be present");
            names.Should().Contain("KEBS", "Kenya standards should be present");
            names.Should().Contain("TBS", "Tanzania standards should be present");
            names.Should().Contain("RSB", "Rwanda standards should be present");
            names.Should().Contain("BBN", "Burundi standards should be present");
            names.Should().Contain("SSBS", "South Sudan standards should be present");
            names.Should().Contain("EAS", "East African standards should be present");

            // West African / South African
            names.Should().Contain("ECOWAS", "West African standards should be present");
            names.Should().Contain("SANS", "South African standards should be present");
        }

        [Test]
        public void GetAllStandards_ContainsInternationalStandards()
        {
            var standards = StandardsAPI.GetAllStandards();
            var names = standards.Select(s => s.ShortName).ToList();

            names.Should().Contain("NEC 2023", "US electrical code should be present");
            names.Should().Contain("ASHRAE", "HVAC standards should be present");
            names.Should().Contain("CIBSE", "Building services should be present");
            names.Should().Contain("Eurocodes", "European standards should be present");
            names.Should().Contain("ASTM", "Materials testing should be present");
        }

        [Test]
        public void GetAllStandards_HasNoDuplicateNames()
        {
            var standards = StandardsAPI.GetAllStandards();
            var names = standards.Select(s => s.ShortName).ToList();
            names.Should().OnlyHaveUniqueItems("standard names must be unique");
        }

        [Test]
        public void GetStandardsForLocation_Uganda_IncludesUNBS()
        {
            var standards = StandardsAPI.GetStandardsForLocation("Uganda");
            standards.Select(s => s.ShortName).Should().Contain("UNBS");
        }

        [Test]
        public void GetStandardsForLocation_Kenya_IncludesKEBS()
        {
            var standards = StandardsAPI.GetStandardsForLocation("Kenya");
            standards.Select(s => s.ShortName).Should().Contain("KEBS");
        }
    }
}
