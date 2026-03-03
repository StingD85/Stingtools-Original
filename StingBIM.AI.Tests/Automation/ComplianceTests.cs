// ============================================================================
// StingBIM AI Tests - Automated Compliance Checker Tests
// Validates fire safety, accessibility, structural, and MEP compliance checks
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Automation.Compliance;

namespace StingBIM.AI.Tests.Automation
{
    [TestFixture]
    public class ComplianceTests
    {
        private AutomatedComplianceChecker _checker;

        [SetUp]
        public void SetUp()
        {
            _checker = new AutomatedComplianceChecker();
        }

        #region Helper Methods

        private BuildingModel CreateSmallResidentialBuilding()
        {
            return new BuildingModel
            {
                BuildingId = "BLDG-001",
                BuildingName = "Small Residential House",
                BuildingType = "Residential",
                OccupancyType = "residential",
                TotalArea = 200,
                DesignOccupancy = 10,
                NumberOfStoreys = 1,
                Location = new BuildingLocation { Country = "Kenya", City = "Nairobi", Region = "East Africa" },
                Spaces = new List<Space>
                {
                    new Space { SpaceId = "SP-001", Name = "Living Room", Area = 30, UseType = "residential" },
                    new Space { SpaceId = "SP-002", Name = "Bedroom 1", Area = 15, UseType = "residential" },
                    new Space { SpaceId = "SP-003", Name = "Kitchen", Area = 12, UseType = "residential" }
                }
            };
        }

        private BuildingModel CreateMediumOfficeBuilding()
        {
            return new BuildingModel
            {
                BuildingId = "BLDG-002",
                BuildingName = "Medium Office Building",
                BuildingType = "Commercial",
                OccupancyType = "office",
                TotalArea = 3000,
                DesignOccupancy = 200,
                NumberOfStoreys = 4,
                Location = new BuildingLocation { Country = "Kenya", City = "Nairobi", Region = "East Africa" },
                Spaces = new List<Space>
                {
                    new Space { SpaceId = "SP-010", Name = "Open Plan Office A", Area = 400, UseType = "office" },
                    new Space { SpaceId = "SP-011", Name = "Open Plan Office B", Area = 350, UseType = "office" },
                    new Space { SpaceId = "SP-012", Name = "Meeting Room 1", Area = 40, UseType = "assembly" },
                    new Space { SpaceId = "SP-013", Name = "Reception", Area = 80, UseType = "office" }
                }
            };
        }

        private BuildingModel CreateLargeHighRiseBuilding()
        {
            return new BuildingModel
            {
                BuildingId = "BLDG-003",
                BuildingName = "High-Rise Tower",
                BuildingType = "Commercial",
                OccupancyType = "office",
                TotalArea = 25000,
                DesignOccupancy = 1500,
                NumberOfStoreys = 15,
                Location = new BuildingLocation { Country = "Kenya", City = "Nairobi", Region = "East Africa" },
                Spaces = new List<Space>
                {
                    new Space { SpaceId = "SP-100", Name = "Ground Floor Lobby", Area = 600, UseType = "assembly" },
                    new Space { SpaceId = "SP-101", Name = "Typical Office Floor", Area = 1200, UseType = "office" },
                    new Space { SpaceId = "SP-102", Name = "Server Room", Area = 100, UseType = "storage" },
                    new Space { SpaceId = "SP-103", Name = "Conference Centre", Area = 300, UseType = "assembly" }
                }
            };
        }

        private BuildingModel CreateSmallBuildingWithNoSpaces()
        {
            return new BuildingModel
            {
                BuildingId = "BLDG-004",
                BuildingName = "Small Warehouse",
                BuildingType = "Industrial",
                OccupancyType = "storage",
                TotalArea = 400,
                DesignOccupancy = 5,
                NumberOfStoreys = 1,
                Location = new BuildingLocation { Country = "Uganda", City = "Kampala", Region = "East Africa" },
                Spaces = null
            };
        }

        #endregion

        #region Fire Safety - Travel Distance Tests

        [Test]
        public async Task CheckFireSafety_SmallBuilding_NoSprinklerRequired()
        {
            // Arrange
            var model = CreateSmallResidentialBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "KEBS");

            // Assert
            result.Should().NotBeNull();
            result.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.SprinklerChecks.Should().NotBeNull();

            // Small building (200 m2, 10 occupants, 1 storey) should not require sprinklers
            var sprinklerReq = result.SprinklerChecks.FirstOrDefault(c => c.RuleId.Contains("SPRINKLER_REQ"));
            sprinklerReq.Should().NotBeNull();
            sprinklerReq.Status.Should().Be(CheckStatus.Pass,
                "building with 200 m2 area, 10 occupants, and 1 storey does not trigger sprinkler thresholds");
        }

        [Test]
        public async Task CheckFireSafety_LargeBuilding_SprinklerRequired()
        {
            // Arrange
            var model = CreateLargeHighRiseBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            result.SprinklerChecks.Should().NotBeNull();

            var sprinklerReq = result.SprinklerChecks.FirstOrDefault(c => c.RuleId.Contains("SPRINKLER_REQ"));
            sprinklerReq.Should().NotBeNull();
            sprinklerReq.Status.Should().Be(CheckStatus.Warning,
                "building with 25000 m2, 1500 occupants, 15 storeys triggers all sprinkler thresholds");
            sprinklerReq.Message.Should().Contain("Automatic sprinkler system required");
        }

        [Test]
        public async Task CheckFireSafety_TravelDistance_SmallSpaces_ShouldPass()
        {
            // Arrange - small spaces have short estimated travel distances
            var model = CreateSmallResidentialBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "KEBS");

            // Assert
            result.TravelDistanceChecks.Should().NotBeNull();
            result.TravelDistanceChecks.Should().NotBeEmpty();

            // Small spaces (30 m2) have sqrt(30) * sqrt(2) = ~7.7 m, well under 45 m limit for KEBS
            var livingRoomCheck = result.TravelDistanceChecks
                .FirstOrDefault(c => c.RuleId.Contains("SP-001"));
            livingRoomCheck.Should().NotBeNull();
            livingRoomCheck.Status.Should().Be(CheckStatus.Pass);
        }

        [Test]
        public async Task CheckFireSafety_TravelDistance_LargeSpace_WithWarning()
        {
            // Arrange - a space > 500 m2 should get a warning about needing closer exits
            var model = CreateLargeHighRiseBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            result.TravelDistanceChecks.Should().NotBeNull();

            // Space SP-101 has area 1200 m2, diagonal ~ sqrt(1200)*sqrt(2) ~ 49 m
            // IBC limit is 76 m, so it passes but > 500 m2 triggers warning
            var largeOfficeCheck = result.TravelDistanceChecks
                .FirstOrDefault(c => c.RuleId.Contains("SP-101"));
            largeOfficeCheck.Should().NotBeNull();
            largeOfficeCheck.Status.Should().Be(CheckStatus.Warning,
                "space > 500 m2 gets a warning even if travel distance is within limits");
        }

        [Test]
        public async Task CheckFireSafety_TravelDistance_NoSpaces_UsesBuildingLevel()
        {
            // Arrange
            var model = CreateSmallBuildingWithNoSpaces();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "EAC");

            // Assert
            result.TravelDistanceChecks.Should().NotBeNull();
            result.TravelDistanceChecks.Should().NotBeEmpty();

            // With no spaces, a building-level check is performed
            var buildingLevelCheck = result.TravelDistanceChecks
                .FirstOrDefault(c => c.RuleId.Contains("TRAVEL_BLDG"));
            buildingLevelCheck.Should().NotBeNull();
            // Building area 400 m2, diagonal ~ sqrt(400)*sqrt(2) ~ 28.3 m, under 45 m EAC limit
            buildingLevelCheck.Status.Should().Be(CheckStatus.Pass);
        }

        [Test]
        [TestCase("IBC", 76.0)]
        [TestCase("KEBS", 45.0)]
        [TestCase("SANS", 45.0)]
        [TestCase("BS", 45.0)]
        [TestCase("NBC", 30.0)]
        [TestCase("EAC", 45.0)]
        public async Task CheckFireSafety_TravelDistance_CodesHaveDifferentLimits(string codeId, double expectedMaxDistance)
        {
            // Arrange - building with no spaces to force building-level check
            var model = new BuildingModel
            {
                BuildingId = "BLDG-DIST",
                BuildingName = "Distance Test Building",
                TotalArea = 100, // sqrt(100)*sqrt(2) ~ 14.1 m
                DesignOccupancy = 10,
                NumberOfStoreys = 1,
                Location = new BuildingLocation { Country = "International" },
                Spaces = null
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, codeId);

            // Assert
            result.TravelDistanceChecks.Should().NotBeNull();
            var bldgCheck = result.TravelDistanceChecks.FirstOrDefault(c => c.RuleId.Contains("TRAVEL_BLDG"));
            bldgCheck.Should().NotBeNull();
            bldgCheck.RequiredValue.Should().Contain($"{expectedMaxDistance}");
        }

        #endregion

        #region Fire Safety - Exit Width Tests

        [Test]
        [TestCase("IBC", 813.0)]
        [TestCase("KEBS", 900.0)]
        [TestCase("BS", 900.0)]
        [TestCase("SANS", 800.0)]
        public async Task CheckFireSafety_ExitWidth_MinimumByCode(string codeId, double expectedMinWidth)
        {
            // Arrange
            var model = CreateSmallResidentialBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, codeId);

            // Assert
            result.ExitWidthChecks.Should().NotBeNull();
            var minWidthCheck = result.ExitWidthChecks.FirstOrDefault(c => c.RuleId.Contains("EXIT_WIDTH_MIN"));
            minWidthCheck.Should().NotBeNull();
            minWidthCheck.ActualValue.Should().Contain($"{expectedMinWidth:F0}");
        }

        [Test]
        public async Task CheckFireSafety_ExitWidth_HighOccupancy_WarnsLargeWidth()
        {
            // Arrange - building with high occupancy causes large required exit width
            var model = CreateLargeHighRiseBuilding(); // 1500 occupants

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            // IBC: 1500 occupants * 5.0 mm/person = 7500 mm required, which is > 2000 mm threshold
            var occCheck = result.ExitWidthChecks.FirstOrDefault(c => c.RuleId.Contains("EXIT_WIDTH_OCC"));
            occCheck.Should().NotBeNull();
            occCheck.Status.Should().Be(CheckStatus.Warning,
                "required exit width exceeds 2000 mm for 1500 occupants");
        }

        [Test]
        public async Task CheckFireSafety_ExitWidth_LowOccupancy_Passes()
        {
            // Arrange
            var model = CreateSmallResidentialBuilding(); // 10 occupants

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            // IBC: 10 occupants * 5.0 mm/person = 50 mm, well under 2000 mm threshold
            var occCheck = result.ExitWidthChecks.FirstOrDefault(c => c.RuleId.Contains("EXIT_WIDTH_OCC"));
            occCheck.Should().NotBeNull();
            occCheck.Status.Should().Be(CheckStatus.Pass);
        }

        #endregion

        #region Fire Safety - Fire Rating Tests

        [Test]
        [TestCase(1, 60)]
        [TestCase(2, 60)]
        [TestCase(3, 90)]
        [TestCase(4, 90)]
        [TestCase(5, 120)]
        [TestCase(10, 120)]
        public async Task CheckFireSafety_FireRating_RequiredMinutesByStoreys(int storeys, int expectedMinutes)
        {
            // Arrange
            var model = new BuildingModel
            {
                BuildingId = "BLDG-FR",
                BuildingName = "Fire Rating Test",
                TotalArea = 500 * storeys,
                DesignOccupancy = 50 * storeys,
                NumberOfStoreys = storeys,
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var structRating = result.FireRatingChecks
                .FirstOrDefault(c => c.RuleId.Contains("FIRE_RATING_STRUCT"));
            structRating.Should().NotBeNull();
            structRating.ActualValue.Should().Contain($"{expectedMinutes} min");
        }

        [Test]
        public async Task CheckFireSafety_FireRating_HighRise_AddsEnhancedRequirements()
        {
            // Arrange
            var model = CreateLargeHighRiseBuilding(); // 15 storeys

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var highRiseCheck = result.FireRatingChecks
                .FirstOrDefault(c => c.RuleId.Contains("FIRE_RATING_HIGHRISE"));
            highRiseCheck.Should().NotBeNull("buildings > 10 storeys get additional high-rise fire rating checks");
            highRiseCheck.Status.Should().Be(CheckStatus.Warning);
            highRiseCheck.Message.Should().Contain("pressurised stairwells");
        }

        [Test]
        public async Task CheckFireSafety_FireRating_LowRise_NoHighRiseCheck()
        {
            // Arrange
            var model = CreateSmallResidentialBuilding(); // 1 storey

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var highRiseCheck = result.FireRatingChecks
                .FirstOrDefault(c => c.RuleId.Contains("FIRE_RATING_HIGHRISE"));
            highRiseCheck.Should().BeNull("single-storey building should not trigger high-rise fire rating check");
        }

        #endregion

        #region Fire Safety - Sprinkler Tests

        [Test]
        public async Task CheckFireSafety_Sprinkler_TriggeredByArea()
        {
            // Arrange - area > 5000 m2 triggers sprinkler requirement
            var model = new BuildingModel
            {
                BuildingId = "BLDG-SPR-A",
                BuildingName = "Large Area Building",
                TotalArea = 6000,
                DesignOccupancy = 100, // Under 300 threshold
                NumberOfStoreys = 1,   // Under 3 threshold
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var sprinklerReq = result.SprinklerChecks.FirstOrDefault(c => c.RuleId.Contains("SPRINKLER_REQ"));
            sprinklerReq.Should().NotBeNull();
            sprinklerReq.Status.Should().Be(CheckStatus.Warning);
            sprinklerReq.Message.Should().Contain("total area");
        }

        [Test]
        public async Task CheckFireSafety_Sprinkler_TriggeredByOccupancy()
        {
            // Arrange - occupancy > 300 triggers sprinkler requirement
            var model = new BuildingModel
            {
                BuildingId = "BLDG-SPR-O",
                BuildingName = "High Occupancy Building",
                TotalArea = 2000, // Under 5000 threshold
                DesignOccupancy = 400,
                NumberOfStoreys = 2,  // Under 3 threshold
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var sprinklerReq = result.SprinklerChecks.FirstOrDefault(c => c.RuleId.Contains("SPRINKLER_REQ"));
            sprinklerReq.Status.Should().Be(CheckStatus.Warning);
            sprinklerReq.Message.Should().Contain("occupancy");
        }

        [Test]
        public async Task CheckFireSafety_Sprinkler_TriggeredByHeight()
        {
            // Arrange - storeys > 3 triggers sprinkler requirement
            var model = new BuildingModel
            {
                BuildingId = "BLDG-SPR-H",
                BuildingName = "Tall Building",
                TotalArea = 2000,
                DesignOccupancy = 100,
                NumberOfStoreys = 5,
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var sprinklerReq = result.SprinklerChecks.FirstOrDefault(c => c.RuleId.Contains("SPRINKLER_REQ"));
            sprinklerReq.Status.Should().Be(CheckStatus.Warning);
            sprinklerReq.Message.Should().Contain("storeys");
        }

        [Test]
        public async Task CheckFireSafety_Sprinkler_NotRequired_AllBelowThresholds()
        {
            // Arrange - all values below thresholds
            var model = new BuildingModel
            {
                BuildingId = "BLDG-SPR-N",
                BuildingName = "Small Building",
                TotalArea = 1000,
                DesignOccupancy = 50,
                NumberOfStoreys = 2,
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var sprinklerReq = result.SprinklerChecks.FirstOrDefault(c => c.RuleId.Contains("SPRINKLER_REQ"));
            sprinklerReq.Status.Should().Be(CheckStatus.Pass);
            sprinklerReq.ActualValue.Should().Contain("Not required");
        }

        [Test]
        public async Task CheckFireSafety_Sprinkler_WhenRequired_ChecksPerSpace()
        {
            // Arrange
            var model = CreateLargeHighRiseBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert - when sprinklers are required, each space gets a coverage check
            var spaceChecks = result.SprinklerChecks
                .Where(c => c.RuleId.Contains("SPRINKLER_SP-"))
                .ToList();
            spaceChecks.Should().HaveCount(model.Spaces.Count,
                "each space should have a sprinkler coverage verification check");
        }

        #endregion

        #region Fire Safety - Compartmentation Tests

        [Test]
        public async Task CheckFireSafety_Compartmentation_SmallFloor_Passes()
        {
            // Arrange - floor area within compartment limit
            var model = CreateSmallResidentialBuilding(); // 200 m2 / 1 storey = 200 m2 per floor

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert - IBC max compartment is 2500 m2, 200 m2 per floor is fine
            var compartCheck = result.CompartmentationChecks
                .FirstOrDefault(c => c.RuleId.Contains("COMPART_AREA"));
            compartCheck.Should().NotBeNull();
            compartCheck.Status.Should().Be(CheckStatus.Pass);
        }

        [Test]
        public async Task CheckFireSafety_Compartmentation_LargeFloor_Fails()
        {
            // Arrange - large building with floor area exceeding compartment limit
            var model = new BuildingModel
            {
                BuildingId = "BLDG-COMP",
                BuildingName = "Wide Building",
                TotalArea = 6000,
                DesignOccupancy = 200,
                NumberOfStoreys = 2, // 3000 m2 per floor, exceeds EAC 2000 m2 limit
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "EAC");

            // Assert - EAC max compartment is 2000 m2, floor is 3000 m2
            var compartCheck = result.CompartmentationChecks
                .FirstOrDefault(c => c.RuleId.Contains("COMPART_AREA"));
            compartCheck.Should().NotBeNull();
            compartCheck.Status.Should().Be(CheckStatus.Fail);
            compartCheck.Message.Should().Contain("exceeds maximum compartment size");
        }

        #endregion

        #region Fire Safety - Emergency Lighting Tests

        [Test]
        public async Task CheckFireSafety_EmergencyLighting_SmallSingleStorey_NotRequired()
        {
            // Arrange - area <= 300 and storeys <= 1
            var model = new BuildingModel
            {
                BuildingId = "BLDG-EL",
                BuildingName = "Small Shed",
                TotalArea = 250,
                DesignOccupancy = 5,
                NumberOfStoreys = 1,
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var lightingCheck = result.EmergencyLightingChecks
                .FirstOrDefault(c => c.RuleId.Contains("EMERG_LIGHT_REQ"));
            lightingCheck.Should().NotBeNull();
            lightingCheck.Status.Should().Be(CheckStatus.Pass);
            lightingCheck.ActualValue.Should().Contain("Not required");
        }

        [Test]
        public async Task CheckFireSafety_EmergencyLighting_LargeBuilding_Required()
        {
            // Arrange - area > 300 triggers requirement
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            var lightingCheck = result.EmergencyLightingChecks
                .FirstOrDefault(c => c.RuleId.Contains("EMERG_LIGHT_REQ"));
            lightingCheck.Should().NotBeNull();
            lightingCheck.Status.Should().Be(CheckStatus.Warning);
            lightingCheck.ActualValue.Should().Contain("Required");
        }

        [Test]
        public async Task CheckFireSafety_EmergencyLighting_MultiStorey_Required()
        {
            // Arrange - storeys > 1 triggers even if small area
            var model = new BuildingModel
            {
                BuildingId = "BLDG-EL2",
                BuildingName = "Two Storey Cottage",
                TotalArea = 200, // Under 300 threshold
                DesignOccupancy = 8,
                NumberOfStoreys = 2, // Over 1 storey threshold
                Location = new BuildingLocation { Country = "Kenya" },
                Spaces = new List<Space>()
            };

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "BS");

            // Assert
            var lightingCheck = result.EmergencyLightingChecks
                .FirstOrDefault(c => c.RuleId.Contains("EMERG_LIGHT_REQ"));
            lightingCheck.Status.Should().Be(CheckStatus.Warning);
        }

        #endregion

        #region Fire Safety - Overall Score

        [Test]
        public async Task CheckFireSafety_OverallScore_IsCalculated()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckFireSafetyAsync(model, "IBC");

            // Assert
            result.OverallScore.Should().BeGreaterThanOrEqualTo(0);
            result.OverallScore.Should().BeLessThanOrEqualTo(100);
        }

        #endregion

        #region Accessibility Tests

        [Test]
        public async Task CheckAccessibility_ReturnsAllCheckTypes()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            result.Should().NotBeNull();
            result.RouteChecks.Should().NotBeNull();
            result.DoorChecks.Should().NotBeNull();
            result.ToiletChecks.Should().NotBeNull();
            result.ParkingChecks.Should().NotBeNull();
            result.SignageChecks.Should().NotBeNull();
            result.LiftChecks.Should().NotBeNull();
        }

        [Test]
        public async Task CheckAccessibility_CorridorWidth_Minimum1200mm()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var routeWidth = result.RouteChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_ROUTE_WIDTH"));
            routeWidth.Should().NotBeNull();
            routeWidth.Status.Should().Be(CheckStatus.Pass);
            routeWidth.ActualValue.Should().Contain("1200");
        }

        [Test]
        public async Task CheckAccessibility_MultiStorey_RequiresRampOrLift()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding(); // 4 storeys

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var rampCheck = result.RouteChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_RAMP"));
            rampCheck.Should().NotBeNull("multi-storey building should have ramp gradient check");
            rampCheck.Status.Should().Be(CheckStatus.Warning);
        }

        [Test]
        public async Task CheckAccessibility_SingleStorey_NoRampRequired()
        {
            // Arrange
            var model = CreateSmallResidentialBuilding(); // 1 storey

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var rampCheck = result.RouteChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_RAMP"));
            rampCheck.Should().BeNull("single-storey building does not need ramp check");
        }

        [Test]
        [TestCase("IBC", 813.0)]
        [TestCase("BS", 800.0)]
        [TestCase("KEBS", 850.0)]
        [TestCase("SANS", 800.0)]
        public async Task CheckAccessibility_DoorWidth_VariesByCode(string codeId, double expectedMinWidth)
        {
            // Arrange
            var model = CreateSmallResidentialBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, codeId);

            // Assert
            var doorWidthCheck = result.DoorChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_DOOR_WIDTH"));
            doorWidthCheck.Should().NotBeNull();
            doorWidthCheck.ActualValue.Should().Contain($"{expectedMinWidth:F0}");
        }

        [Test]
        public async Task CheckAccessibility_DoorHandle_Height900To1100mm()
        {
            // Arrange
            var model = CreateSmallResidentialBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var handleCheck = result.DoorChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_DOOR_HANDLE"));
            handleCheck.Should().NotBeNull();
            handleCheck.Status.Should().Be(CheckStatus.Pass);
            handleCheck.ActualValue.Should().Contain("900").And.Contain("1100");
        }

        [Test]
        public async Task CheckAccessibility_Toilets_CalculatesRequiredCount()
        {
            // Arrange - 200 occupants / 25 = 8 WCs, 8 / 20 = 1 accessible WC
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var toiletCount = result.ToiletChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_TOILET_COUNT"));
            toiletCount.Should().NotBeNull();
            toiletCount.Message.Should().Contain("200 occupants");
        }

        [Test]
        public async Task CheckAccessibility_Toilets_TurningRadius1500mm()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var turningCheck = result.ToiletChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_TOILET_TURN"));
            turningCheck.Should().NotBeNull();
            turningCheck.Status.Should().Be(CheckStatus.Pass);
            turningCheck.ActualValue.Should().Contain("1500");
        }

        [Test]
        public async Task CheckAccessibility_Toilets_MultiStorey_PerFloorRequirement()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding(); // 4 storeys

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var perFloorCheck = result.ToiletChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_TOILET_FLOOR"));
            perFloorCheck.Should().NotBeNull();
            perFloorCheck.Status.Should().Be(CheckStatus.Warning);
        }

        [Test]
        public async Task CheckAccessibility_Lift_SingleStorey_NotApplicable()
        {
            // Arrange
            var model = CreateSmallResidentialBuilding(); // 1 storey

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var liftReq = result.LiftChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_LIFT_REQ"));
            liftReq.Should().NotBeNull();
            liftReq.Status.Should().Be(CheckStatus.NotApplicable);
        }

        [Test]
        public async Task CheckAccessibility_Lift_MultiStorey_Required()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding(); // 4 storeys

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var liftReq = result.LiftChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_LIFT_REQ"));
            liftReq.Should().NotBeNull();
            liftReq.Status.Should().Be(CheckStatus.Warning);
        }

        [Test]
        public async Task CheckAccessibility_Lift_LargeBuilding_AdditionalCapacity()
        {
            // Arrange - >500 occupants or >5 storeys triggers additional lift capacity check
            var model = CreateLargeHighRiseBuilding(); // 1500 occupants, 15 storeys

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var capacityCheck = result.LiftChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_LIFT_CAPACITY"));
            capacityCheck.Should().NotBeNull("large building should trigger additional lift capacity check");
            capacityCheck.Status.Should().Be(CheckStatus.Warning);
            capacityCheck.Message.Should().Contain("stretcher");
        }

        [Test]
        public async Task CheckAccessibility_Parking_CalculatesAccessibleSpaces()
        {
            // Arrange - 3000 m2 / 50 = 60 total spaces, 60 / 25 = 3 accessible spaces
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var parkingCount = result.ParkingChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_PARKING_COUNT"));
            parkingCount.Should().NotBeNull();
            parkingCount.Status.Should().Be(CheckStatus.Warning);
        }

        [Test]
        public async Task CheckAccessibility_Signage_BrailleRequired()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckAccessibilityAsync(model, "IBC");

            // Assert
            var brailleCheck = result.SignageChecks
                .FirstOrDefault(c => c.RuleId.Contains("ACCESS_SIGN_BRAILLE"));
            brailleCheck.Should().NotBeNull();
            brailleCheck.Status.Should().Be(CheckStatus.Warning);
        }

        #endregion

        #region Structural Compliance Tests

        [Test]
        public async Task CheckStructural_ReturnsAllCheckTypes()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            result.Should().NotBeNull();
            result.LoadChecks.Should().NotBeNull();
            result.MaterialChecks.Should().NotBeNull();
            result.FoundationChecks.Should().NotBeNull();
            result.SeismicChecks.Should().NotBeNull();
        }

        [Test]
        public async Task CheckStructural_LoadCalculations_OfficeUseType()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding(); // office type

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            var buildingLoad = result.LoadChecks
                .FirstOrDefault(c => c.RuleId.Contains("LOAD_BUILDING"));
            buildingLoad.Should().NotBeNull();
            buildingLoad.Status.Should().Be(CheckStatus.Pass);
            // Dead load 1.5 + office live load 2.5 = 4.0, factored * 1.5 = 6.0 kN/m2
            buildingLoad.ActualValue.Should().Contain("Dead: 1.5");
            buildingLoad.ActualValue.Should().Contain("Live: 2.5");
        }

        [Test]
        public async Task CheckStructural_LoadCalculations_AssemblySpaces_Warning()
        {
            // Arrange - model with assembly use spaces
            var model = CreateLargeHighRiseBuilding();

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            var highLoadCheck = result.LoadChecks
                .FirstOrDefault(c => c.RuleId.Contains("LOAD_HIGH_LIVE"));
            highLoadCheck.Should().NotBeNull("building with assembly/storage spaces should get warning");
            highLoadCheck.Status.Should().Be(CheckStatus.Warning);
        }

        [Test]
        public async Task CheckStructural_Materials_ConcreteGrades()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            var foundationConcrete = result.MaterialChecks
                .FirstOrDefault(c => c.RuleId.Contains("MAT_CONCRETE_FOUNDATION"));
            foundationConcrete.Should().NotBeNull();
            foundationConcrete.ActualValue.Should().Contain("C25");

            var columnConcrete = result.MaterialChecks
                .FirstOrDefault(c => c.RuleId.Contains("MAT_CONCRETE_COLUMNS"));
            columnConcrete.Should().NotBeNull();
            columnConcrete.ActualValue.Should().Contain("C30");
        }

        [Test]
        public async Task CheckStructural_Materials_TallBuilding_HigherConcreteGrade()
        {
            // Arrange - > 5 storeys should trigger higher concrete grade warning
            var model = CreateLargeHighRiseBuilding(); // 15 storeys

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            var highRiseConcrete = result.MaterialChecks
                .FirstOrDefault(c => c.RuleId.Contains("MAT_CONCRETE_HIGHRISE"));
            highRiseConcrete.Should().NotBeNull();
            highRiseConcrete.Status.Should().Be(CheckStatus.Warning);
            highRiseConcrete.RequiredValue.Should().Contain("C35");
        }

        [Test]
        public async Task CheckStructural_Foundation_DepthByStoreys()
        {
            // Arrange - 1 storey = 600mm, 2 storeys = 900mm, 3+ = 1200mm
            var model = CreateSmallResidentialBuilding(); // 1 storey

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            var depthCheck = result.FoundationChecks
                .FirstOrDefault(c => c.RuleId.Contains("FOUND_DEPTH"));
            depthCheck.Should().NotBeNull();
            depthCheck.ActualValue.Should().Contain("600");
        }

        [Test]
        public async Task CheckStructural_Foundation_MultiStorey_GeoTechRequired()
        {
            // Arrange - >= 3 storeys requires geotechnical investigation
            var model = CreateMediumOfficeBuilding(); // 4 storeys

            // Act
            var result = await _checker.CheckStructuralComplianceAsync(model, "IBC");

            // Assert
            var geoTechCheck = result.FoundationChecks
                .FirstOrDefault(c => c.RuleId.Contains("FOUND_GEOTECH"));
            geoTechCheck.Should().NotBeNull();
            geoTechCheck.Status.Should().Be(CheckStatus.Warning);
        }

        #endregion

        #region MEP Compliance Tests

        [Test]
        public async Task CheckMEP_ReturnsAllCheckTypes()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();

            // Act
            var result = await _checker.CheckMEPComplianceAsync(model, "IBC");

            // Assert
            result.Should().NotBeNull();
            result.VentilationChecks.Should().NotBeNull();
            result.ElectricalChecks.Should().NotBeNull();
            result.PlumbingChecks.Should().NotBeNull();
            result.EnergyChecks.Should().NotBeNull();
        }

        #endregion

        #region Comprehensive Compliance Check Tests

        [Test]
        public async Task CheckCompliance_WithExplicitCodes_ReturnsSummary()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding();
            var options = new ComplianceCheckOptions
            {
                CodesToCheck = new List<string> { "IBC" },
                AutoDetectCodes = false
            };

            // Act
            var report = await _checker.CheckComplianceAsync(model, options);

            // Assert
            report.Should().NotBeNull();
            report.BuildingId.Should().Be("BLDG-002");
            report.Summary.Should().NotBeNull();
            report.Summary.TotalChecks.Should().BeGreaterThan(0);
            report.OverallScore.Should().BeGreaterThanOrEqualTo(0);
            report.OverallScore.Should().BeLessThanOrEqualTo(100);
        }

        [Test]
        public async Task CheckCompliance_AutoDetectCodes_ForKenya()
        {
            // Arrange
            var model = CreateMediumOfficeBuilding(); // Kenya location
            var options = new ComplianceCheckOptions
            {
                CodesToCheck = new List<string>(),
                AutoDetectCodes = true
            };

            // Act
            var report = await _checker.CheckComplianceAsync(model, options);

            // Assert
            report.ApplicableCodes.Should().Contain("KEBS");
        }

        #endregion

        #region RuleEngine Tests

        [Test]
        public void RuleEngine_ExecuteMaxValueRule_Passes()
        {
            // Arrange
            var engine = new RuleEngine();
            var model = CreateSmallResidentialBuilding();
            var rule = new ComplianceRule
            {
                RuleId = "TEST_MAX",
                Name = "Max Test",
                CheckType = CheckType.MaxValue,
                Threshold = 100 // Placeholder value of 50 in engine is below 100
            };

            // Act
            var result = engine.ExecuteRule(model, rule);

            // Assert
            result.Passed.Should().BeTrue();
            result.Message.Should().Be("Compliant");
        }

        [Test]
        public void RuleEngine_ExecuteMinValueRule_Passes()
        {
            // Arrange
            var engine = new RuleEngine();
            var model = CreateSmallResidentialBuilding();
            var rule = new ComplianceRule
            {
                RuleId = "TEST_MIN",
                Name = "Min Test",
                CheckType = CheckType.MinValue,
                Threshold = 500 // Placeholder value of 1000 in engine is above 500
            };

            // Act
            var result = engine.ExecuteRule(model, rule);

            // Assert
            result.Passed.Should().BeTrue();
        }

        [Test]
        public void RuleEngine_ExecuteBooleanRule_Passes()
        {
            // Arrange
            var engine = new RuleEngine();
            var model = CreateSmallResidentialBuilding();
            var rule = new ComplianceRule
            {
                RuleId = "TEST_BOOL",
                Name = "Boolean Test",
                CheckType = CheckType.Boolean
            };

            // Act
            var result = engine.ExecuteRule(model, rule);

            // Assert
            result.Passed.Should().BeTrue();
            result.ActualValue.Should().Be("Yes");
        }

        #endregion

        #region ComplianceCalculator and ReportGenerator Tests

        // NOTE: ComplianceCalculator is defined as an empty class: `public class ComplianceCalculator { }`
        // No methods to test. Tests should be added when the class is implemented.

        // NOTE: ReportGenerator is defined as an empty class: `public class ReportGenerator { }`
        // No methods to test. Tests should be added when the class is implemented.

        [Test]
        public void ComplianceCalculator_CanBeInstantiated()
        {
            // ComplianceCalculator is currently an empty class
            var calculator = new ComplianceCalculator();
            calculator.Should().NotBeNull();
        }

        [Test]
        public void ReportGenerator_CanBeInstantiated()
        {
            // ReportGenerator is currently an empty class
            var generator = new ReportGenerator();
            generator.Should().NotBeNull();
        }

        #endregion
    }
}
