using NUnit.Framework;
using FluentAssertions;
using StingBIM.Standards;
using StingBIM.Standards.ASHRAE;
using StingBIM.Standards.NEC2023;

namespace StingBIM.AI.Tests.Unit.Foundation
{
    [TestFixture]
    public class StandardsCalculationTests
    {
        #region ASHRAE Standards Tests

        [TestFixture]
        public class ASHRAETests
        {
            [Test]
            public void CalculateHeatingLoad_ValidInputs_ReturnsExpectedResult()
            {
                // Q = U × A × ΔT
                double uValue = 0.5;   // W/m²K
                double area = 20.0;    // m²
                double indoorTemp = 22.0;
                double outdoorTemp = -5.0;

                var result = ASHRAEStandards.CalculateHeatingLoad(uValue, area, indoorTemp, outdoorTemp);

                // Q = 0.5 × 20 × (22 - (-5)) = 0.5 × 20 × 27 = 270 W
                result.Should().BeApproximately(270.0, 1.0);
            }

            [Test]
            public void CalculateCoolingLoad_ValidInputs_ReturnsPositive()
            {
                double uValue = 0.35;
                double area = 15.0;
                double cltd = 12.0;

                var result = ASHRAEStandards.CalculateCoolingLoad(uValue, area, cltd);

                // Q = U × A × CLTD = 0.35 × 15 × 12 = 63
                result.Should().BeApproximately(63.0, 1.0);
            }

            [Test]
            public void CalculateCoolingLoad_WithLatitudeModifier_ScalesResult()
            {
                double uValue = 0.35;
                double area = 15.0;
                double cltd = 12.0;
                double lm = 1.2; // latitude modifier

                var result = ASHRAEStandards.CalculateCoolingLoad(uValue, area, cltd, lm);

                // Q = U × A × CLTD × LM = 0.35 × 15 × 12 × 1.2 = 75.6
                result.Should().BeApproximately(75.6, 1.0);
            }

            [Test]
            public void CalculateSolarHeatGain_ValidInputs_ReturnsProduct()
            {
                double area = 10.0;
                double shgc = 0.4;
                double solarRadiation = 800.0;

                var result = ASHRAEStandards.CalculateSolarHeatGain(area, shgc, solarRadiation);

                // Q = A × SHGC × I = 10 × 0.4 × 800 = 3200
                result.Should().BeApproximately(3200.0, 1.0);
            }

            [TestCase("Office", 75)]   // Office workers ~75W sensible
            [TestCase("Gym", 210)]      // Exercise ~210W sensible
            public void CalculateOccupantLoad_ByActivity_ReturnsReasonableHeatGain(string activity, double minExpected)
            {
                var result = ASHRAEStandards.CalculateOccupantLoad(1, activity);

                result.Should().BeGreaterThanOrEqualTo(minExpected * 0.5,
                    "occupant heat gain should be reasonable for activity type");
            }

            [Test]
            public void CalculateEquipmentLoad_DefaultFactors_ReturnsWatts()
            {
                double watts = 1000.0;

                var result = ASHRAEStandards.CalculateEquipmentLoad(watts);

                // Default: usageFactor=1.0, radiationFactor=0.6 → 1000 × 1.0 × 0.6 = 600
                result.Should().BeApproximately(600.0, 1.0);
            }

            [Test]
            public void CalculateEquipmentLoad_CustomFactors_ScalesCorrectly()
            {
                double watts = 500.0;
                double usageFactor = 0.8;
                double radiationFactor = 0.5;

                var result = ASHRAEStandards.CalculateEquipmentLoad(watts, usageFactor, radiationFactor);

                // 500 × 0.8 × 0.5 = 200
                result.Should().BeApproximately(200.0, 1.0);
            }

            [Test]
            public void CalculateLightingLoad_DefaultFactors_ReturnsWatts()
            {
                double watts = 2000.0;

                var result = ASHRAEStandards.CalculateLightingLoad(watts);

                // Default: ballastFactor=1.0, usageFactor=1.0 → 2000
                result.Should().BeApproximately(2000.0, 1.0);
            }

            [Test]
            public void CalculateEER_ValidInputs_ReturnsCorrectRatio()
            {
                double coolingBTU = 36000;  // 3 tons
                double powerWatts = 3000;

                var result = ASHRAEStandards.CalculateEER(coolingBTU, powerWatts);

                // EER = BTU/h ÷ Watts = 36000/3000 = 12
                result.Should().BeApproximately(12.0, 0.01);
            }

            [Test]
            public void CalculateCOP_ValidInputs_ReturnsCorrectRatio()
            {
                double capacityBTU = 36000;
                double powerWatts = 3000;

                var result = ASHRAEStandards.CalculateCOP(capacityBTU, powerWatts);

                // COP = BTU/h × 0.293071 / Watts = 36000 × 0.293071 / 3000 ≈ 3.52
                result.Should().BeApproximately(3.52, 0.1);
            }

            [Test]
            public void CalculateCoolingTons_12000BTU_Returns1Ton()
            {
                var result = ASHRAEStandards.CalculateCoolingTons(12000);

                result.Should().BeApproximately(1.0, 0.001);
            }

            [Test]
            public void CalculateHeatingMBH_1000BTU_Returns1MBH()
            {
                var result = ASHRAEStandards.CalculateHeatingMBH(1000);

                result.Should().BeApproximately(1.0, 0.001);
            }

            [Test]
            public void CalculateAnnualEnergy_KnownValues_ReturnsKWh()
            {
                double powerKW = 10.0;
                double hoursPerYear = 8760;
                double loadFactor = 0.5;

                var result = ASHRAEStandards.CalculateAnnualEnergy(powerKW, hoursPerYear, loadFactor);

                // 10 × 8760 × 0.5 = 43,800 kWh
                result.Should().BeApproximately(43800.0, 1.0);
            }

            [Test]
            public void CalculateMinimumOAFraction_ValidInputs_ReturnsRatio()
            {
                double outdoorAir = 200;
                double supplyAir = 1000;

                var result = ASHRAEStandards.CalculateMinimumOAFraction(outdoorAir, supplyAir);

                result.Should().BeApproximately(0.2, 0.001);
            }

            [Test]
            public void CalculateSensibleHeatRatio_ValidInputs_ReturnsRatio()
            {
                double sensible = 8000;
                double total = 10000;

                var result = ASHRAEStandards.CalculateSensibleHeatRatio(sensible, total);

                result.Should().BeApproximately(0.8, 0.001);
            }

            [Test]
            public void CalculateMixedAirTemperature_ValidInputs_ReturnsWeightedAverage()
            {
                double outdoorTemp = 35.0;
                double returnTemp = 24.0;
                double oaFraction = 0.3;

                var result = ASHRAEStandards.CalculateMixedAirTemperature(outdoorTemp, returnTemp, oaFraction);

                // Mixed = OA × 0.3 + RA × 0.7 = 35×0.3 + 24×0.7 = 10.5 + 16.8 = 27.3
                result.Should().BeApproximately(27.3, 0.1);
            }

            [Test]
            public void CalculateVentilationRequirement_Office_ReturnsResult()
            {
                var result = ASHRAEStandards.CalculateVentilationRequirement("Office", 20, 200);

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculateDuctSizeEqualFriction_ValidAirflow_ReturnsResult()
            {
                var result = ASHRAEStandards.CalculateDuctSizeEqualFriction(1000);

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculatePsychrometricProperties_StandardConditions_ReturnsValidProperties()
            {
                var result = ASHRAEStandards.CalculatePsychrometricProperties(24.0, 50.0);

                result.Should().NotBeNull();
            }
        }

        #endregion

        #region NEC Standards Tests

        [TestFixture]
        public class NECTests
        {
            [TestCase(20, true, 20)]
            [TestCase(18, true, 20)]
            [TestCase(25, true, 30)]
            [TestCase(35, true, 40)]
            [TestCase(45, true, 50)]
            public void GetStandardBreakerSize_RoundsToNextStandardSize(
                double requiredAmps, bool roundUp, int expected)
            {
                var result = NECStandards.GetStandardBreakerSize(requiredAmps, roundUp);

                result.Should().Be(expected);
            }

            [TestCase("Bathroom", "Residential", true)]
            [TestCase("Kitchen", "Residential", true)]
            [TestCase("Garage", "Residential", true)]
            [TestCase("Outdoor", "Residential", true)]
            public void RequiresGFCI_WetLocations_ReturnsTrue(string location, string roomType, bool expected)
            {
                var result = NECStandards.RequiresGFCI(location, roomType);

                result.Should().Be(expected);
            }

            [TestCase("Bedroom", "Residential", true)]
            [TestCase("LivingRoom", "Residential", true)]
            public void RequiresAFCI_LivingSpaces_ReturnsTrue(string location, string roomType, bool expected)
            {
                var result = NECStandards.RequiresAFCI(location, roomType);

                result.Should().Be(expected);
            }

            [Test]
            public void ApplyTemperatureCorrection_HighAmbient_ReducesAmpacity()
            {
                double baseAmpacity = 100.0;
                double highTemp = 45.0; // above 30°C standard

                var result = NECStandards.ApplyTemperatureCorrection(baseAmpacity, highTemp);

                result.Should().BeLessThan(baseAmpacity,
                    "high ambient temperature should derate conductor ampacity");
            }

            [Test]
            public void ApplyTemperatureCorrection_StandardTemp_NoReduction()
            {
                double baseAmpacity = 100.0;
                double standardTemp = 30.0;

                var result = NECStandards.ApplyTemperatureCorrection(baseAmpacity, standardTemp);

                result.Should().BeApproximately(baseAmpacity, 1.0,
                    "standard ambient temperature should not significantly derate");
            }

            [Test]
            public void ApplyBundlingAdjustment_FewConductors_MinimalDerating()
            {
                double baseAmpacity = 100.0;
                int conductorCount = 3;

                var result = NECStandards.ApplyBundlingAdjustment(baseAmpacity, conductorCount);

                result.Should().BeGreaterThanOrEqualTo(baseAmpacity * 0.7,
                    "3 conductors should have minimal derating");
            }

            [Test]
            public void ApplyBundlingAdjustment_ManyConductors_SignificantDerating()
            {
                double baseAmpacity = 100.0;
                int conductorCount = 20;

                var result = NECStandards.ApplyBundlingAdjustment(baseAmpacity, conductorCount);

                result.Should().BeLessThan(baseAmpacity * 0.6,
                    "20 conductors should have significant derating");
            }

            [Test]
            public void CalculateVoltageDrop_LongRun_ExceedsThreshold()
            {
                double current = 30;
                double length = 100; // meters, long run
                string wireSize = "12";
                int voltage = 120;

                var result = NECStandards.CalculateVoltageDrop(current, length, wireSize, voltage);

                result.Should().BeGreaterThan(3.0,
                    "long run with small wire should have significant voltage drop");
            }

            [Test]
            public void CalculateLightingLoad_Office_ReturnsPositive()
            {
                double sqFt = 1000;
                string occupancyType = "Office";

                var result = NECStandards.CalculateLightingLoad(sqFt, occupancyType);

                result.Should().BeGreaterThan(0, "lighting load should be positive");
            }

            [Test]
            public void GeneratePanelScheduleLabel_FormatsCorrectly()
            {
                var result = NECStandards.GeneratePanelScheduleLabel("Panel-A", 208, 200);

                result.Should().NotBeNullOrWhiteSpace();
                result.Should().Contain("Panel-A");
            }
        }

        #endregion

        #region StandardsAPI Facade Tests

        [TestFixture]
        public class StandardsAPIFacadeTests
        {
            [Test]
            public void CalculateCableSize_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.CalculateCableSize(
                    voltageV: 240,
                    currentA: 30,
                    lengthM: 50);

                result.Should().NotBeNull();
            }

            [Test]
            public void VerifyCircuitBreaker_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.VerifyCircuitBreaker(
                    loadCurrentA: 18,
                    voltageV: 120);

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculateCoolingLoad_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.CalculateCoolingLoad(
                    floorAreaM2: 200,
                    buildingType: "Office",
                    climateZone: "Hot-Humid",
                    occupantCount: 20,
                    equipmentLoadW: 5000);

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculateVentilation_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.CalculateVentilation(
                    floorAreaM2: 100,
                    occupantCount: 10,
                    spaceType: "Office");

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculateLighting_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.CalculateLighting(
                    floorAreaM2: 50,
                    spaceType: "Office",
                    ceilingHeightM: 2.7);

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculatePlumbingPipeSize_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.CalculatePlumbingPipeSize(
                    flowRateLPS: 2.5,
                    lengthM: 30,
                    pipeType: "Copper");

                result.Should().NotBeNull();
            }

            [Test]
            public void CalculateDrainageSize_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.CalculateDrainageSize(
                    fixtureUnits: 8,
                    slopePercent: 2.0);

                result.Should().NotBeNull();
            }

            [Test]
            public void EstimateEnergyConsumption_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.EstimateEnergyConsumption(
                    floorAreaM2: 500,
                    buildingType: "Office",
                    climateZone: "Temperate",
                    hvacSystem: "Split");

                result.Should().NotBeNull();
            }

            [Test]
            public void DesignSteelBeam_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.DesignSteelBeam(
                    spanM: 8.0,
                    loadKNM: 25.0,
                    steelGrade: "S355");

                result.Should().NotBeNull();
            }

            [Test]
            public void DesignSprinklerSystem_ValidInputs_ReturnsResult()
            {
                var result = StandardsAPI.DesignSprinklerSystem(
                    areaM2: 300,
                    occupancyType: "Office",
                    hazardClass: "Light");

                result.Should().NotBeNull();
            }

            [Test]
            public void GetAllStandards_ReturnsNonEmptyList()
            {
                var standards = StandardsAPI.GetAllStandards();

                standards.Should().NotBeNull();
                standards.Should().NotBeEmpty();
            }

            [TestCase("Uganda")]
            [TestCase("Kenya")]
            [TestCase("Tanzania")]
            [TestCase("Rwanda")]
            [TestCase("USA")]
            public void GetStandardsForLocation_KnownLocations_ReturnsResults(string location)
            {
                var standards = StandardsAPI.GetStandardsForLocation(location);

                standards.Should().NotBeNull();
                standards.Should().NotBeEmpty(
                    $"location '{location}' should have applicable standards");
            }
        }

        #endregion
    }
}
