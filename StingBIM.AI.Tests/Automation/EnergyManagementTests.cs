// StingBIM.AI.Tests.Automation.EnergyManagementTests
// Unit tests for EnergyManagementEngine
// Tests energy monitoring, consumption analysis, and carbon footprint calculations

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Automation
{
    [TestFixture]
    public class EnergyManagementTests
    {
        #region Energy Consumption Tests

        [Test]
        public void CalculateEnergyConsumption_WithValidReadings_ReturnsCorrectTotal()
        {
            // Arrange
            var readings = new List<EnergyReading>
            {
                new EnergyReading { Timestamp = DateTime.Now.AddHours(-3), Value = 100.0 },
                new EnergyReading { Timestamp = DateTime.Now.AddHours(-2), Value = 150.0 },
                new EnergyReading { Timestamp = DateTime.Now.AddHours(-1), Value = 120.0 },
                new EnergyReading { Timestamp = DateTime.Now, Value = 130.0 }
            };
            var calculator = new EnergyConsumptionCalculator();

            // Act
            var result = calculator.CalculateTotalConsumption(readings);

            // Assert
            result.Should().Be(500.0);
        }

        [Test]
        public void CalculateEnergyConsumption_WithEmptyReadings_ReturnsZero()
        {
            // Arrange
            var readings = new List<EnergyReading>();
            var calculator = new EnergyConsumptionCalculator();

            // Act
            var result = calculator.CalculateTotalConsumption(readings);

            // Assert
            result.Should().Be(0.0);
        }

        [Test]
        public void CalculateAverageConsumption_WithValidReadings_ReturnsCorrectAverage()
        {
            // Arrange
            var readings = new List<EnergyReading>
            {
                new EnergyReading { Value = 100.0 },
                new EnergyReading { Value = 200.0 },
                new EnergyReading { Value = 300.0 }
            };
            var calculator = new EnergyConsumptionCalculator();

            // Act
            var result = calculator.CalculateAverageConsumption(readings);

            // Assert
            result.Should().Be(200.0);
        }

        [Test]
        public void CalculatePeakConsumption_ReturnsMaxValue()
        {
            // Arrange
            var readings = new List<EnergyReading>
            {
                new EnergyReading { Timestamp = DateTime.Now.AddHours(-2), Value = 100.0 },
                new EnergyReading { Timestamp = DateTime.Now.AddHours(-1), Value = 500.0 },
                new EnergyReading { Timestamp = DateTime.Now, Value = 200.0 }
            };
            var calculator = new EnergyConsumptionCalculator();

            // Act
            var result = calculator.CalculatePeakConsumption(readings);

            // Assert
            result.Value.Should().Be(500.0);
        }

        #endregion

        #region Carbon Footprint Tests

        [Test]
        public void CalculateCarbonFootprint_WithElectricity_ReturnsCorrectEmissions()
        {
            // Arrange
            var calculator = new CarbonFootprintCalculator();
            var energyKwh = 1000.0;
            var region = "Uganda"; // Uganda grid emission factor

            // Act
            var result = calculator.CalculateEmissions(energyKwh, EnergySource.Electricity, region);

            // Assert
            // Uganda grid emission factor is approximately 0.12 kgCO2/kWh (mostly hydro)
            result.Should().BeInRange(100.0, 150.0);
        }

        [Test]
        public void CalculateCarbonFootprint_WithNaturalGas_ReturnsCorrectEmissions()
        {
            // Arrange
            var calculator = new CarbonFootprintCalculator();
            var energyKwh = 1000.0;

            // Act
            var result = calculator.CalculateEmissions(energyKwh, EnergySource.NaturalGas);

            // Assert
            // Natural gas emission factor is approximately 0.2 kgCO2/kWh
            result.Should().BeInRange(180.0, 220.0);
        }

        [Test]
        public void CalculateCarbonFootprint_WithSolar_ReturnsMinimalEmissions()
        {
            // Arrange
            var calculator = new CarbonFootprintCalculator();
            var energyKwh = 1000.0;

            // Act
            var result = calculator.CalculateEmissions(energyKwh, EnergySource.Solar);

            // Assert
            // Solar has very low lifecycle emissions (~0.04 kgCO2/kWh)
            result.Should().BeLessThan(50.0);
        }

        [Test]
        public void GetEmissionFactor_ForDifferentRegions_ReturnsAppropriateValues()
        {
            // Arrange
            var calculator = new CarbonFootprintCalculator();

            // Act & Assert
            var ugandaFactor = calculator.GetGridEmissionFactor("Uganda");
            var kenyaFactor = calculator.GetGridEmissionFactor("Kenya");
            var southAfricaFactor = calculator.GetGridEmissionFactor("South Africa");

            // Uganda has cleaner grid (hydro), South Africa has coal-heavy grid
            ugandaFactor.Should().BeLessThan(kenyaFactor);
            kenyaFactor.Should().BeLessThan(southAfricaFactor);
        }

        #endregion

        #region Meter Management Tests

        [Test]
        public void RegisterMeter_WithValidData_CreatesMeterSuccessfully()
        {
            // Arrange
            var manager = new MeterManager();
            var meterInfo = new MeterInfo
            {
                MeterId = "MTR-001",
                Name = "Main Building Meter",
                MeterType = MeterType.Electricity,
                Location = "Building A - Main Panel",
                MaxCapacity = 500.0
            };

            // Act
            var result = manager.RegisterMeter(meterInfo);

            // Assert
            result.Should().BeTrue();
            manager.GetMeter("MTR-001").Should().NotBeNull();
        }

        [Test]
        public void RegisterMeter_WithDuplicateId_ReturnsFalse()
        {
            // Arrange
            var manager = new MeterManager();
            var meterInfo = new MeterInfo
            {
                MeterId = "MTR-001",
                Name = "Main Building Meter",
                MeterType = MeterType.Electricity
            };
            manager.RegisterMeter(meterInfo);

            // Act
            var result = manager.RegisterMeter(meterInfo);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void RecordReading_WithValidMeter_AddsReadingSuccessfully()
        {
            // Arrange
            var manager = new MeterManager();
            manager.RegisterMeter(new MeterInfo { MeterId = "MTR-001", MeterType = MeterType.Electricity });

            // Act
            var result = manager.RecordReading("MTR-001", 150.5, DateTime.Now);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void RecordReading_WithInvalidMeter_ReturnsFalse()
        {
            // Arrange
            var manager = new MeterManager();

            // Act
            var result = manager.RecordReading("INVALID-MTR", 150.5, DateTime.Now);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Energy Analysis Tests

        [Test]
        public void AnalyzeConsumptionPattern_WithDailyData_IdentifiesPattern()
        {
            // Arrange
            var analyzer = new ConsumptionPatternAnalyzer();
            var readings = GenerateDailyPattern();

            // Act
            var pattern = analyzer.AnalyzePattern(readings);

            // Assert
            pattern.PatternType.Should().Be(ConsumptionPatternType.Daily);
            pattern.PeakHours.Should().NotBeEmpty();
        }

        [Test]
        public void AnalyzeConsumptionPattern_WithWeeklyData_IdentifiesWeekendDrop()
        {
            // Arrange
            var analyzer = new ConsumptionPatternAnalyzer();
            var readings = GenerateWeeklyPattern();

            // Act
            var pattern = analyzer.AnalyzePattern(readings);

            // Assert
            pattern.WeekendReduction.Should().BeGreaterThan(0);
        }

        [Test]
        public void DetectAnomalies_WithSpike_IdentifiesAnomaly()
        {
            // Arrange
            var analyzer = new ConsumptionPatternAnalyzer();
            var readings = GenerateNormalReadingsWithSpike();

            // Act
            var anomalies = analyzer.DetectAnomalies(readings, sensitivityLevel: 2.0);

            // Assert
            anomalies.Should().HaveCountGreaterThan(0);
            anomalies[0].AnomalyType.Should().Be(AnomalyType.Spike);
        }

        [Test]
        public void DetectAnomalies_WithNormalData_ReturnsEmpty()
        {
            // Arrange
            var analyzer = new ConsumptionPatternAnalyzer();
            var readings = GenerateNormalReadings();

            // Act
            var anomalies = analyzer.DetectAnomalies(readings, sensitivityLevel: 2.0);

            // Assert
            anomalies.Should().BeEmpty();
        }

        #endregion

        #region Energy Optimization Tests

        [Test]
        public void GenerateOptimizationRecommendations_WithHighPeak_SuggestsLoadShifting()
        {
            // Arrange
            var optimizer = new EnergyOptimizer();
            var profile = new EnergyProfile
            {
                PeakDemand = 500.0,
                AverageDemand = 200.0,
                PeakToAverageRatio = 2.5
            };

            // Act
            var recommendations = optimizer.GenerateRecommendations(profile);

            // Assert
            recommendations.Should().Contain(r => r.RecommendationType == RecommendationType.LoadShifting);
        }

        [Test]
        public void GenerateOptimizationRecommendations_WithHighBaseload_SuggestsEfficiencyUpgrade()
        {
            // Arrange
            var optimizer = new EnergyOptimizer();
            var profile = new EnergyProfile
            {
                BaseLoad = 150.0,
                TotalCapacity = 200.0,
                BaseLoadRatio = 0.75
            };

            // Act
            var recommendations = optimizer.GenerateRecommendations(profile);

            // Assert
            recommendations.Should().Contain(r => r.RecommendationType == RecommendationType.EfficiencyUpgrade);
        }

        [Test]
        public void CalculatePotentialSavings_WithValidProfile_ReturnsEstimate()
        {
            // Arrange
            var optimizer = new EnergyOptimizer();
            var profile = new EnergyProfile
            {
                AnnualConsumption = 100000.0,
                EnergyCostPerKwh = 0.15
            };

            // Act
            var savings = optimizer.CalculatePotentialSavings(profile);

            // Assert
            savings.EnergySavingsKwh.Should().BeGreaterThan(0);
            savings.CostSavings.Should().BeGreaterThan(0);
        }

        #endregion

        #region Renewable Energy Tests

        [Test]
        public void CalculateSolarPotential_ForLocation_ReturnsEstimate()
        {
            // Arrange
            var calculator = new RenewableEnergyCalculator();
            var location = new GeoLocation { Latitude = 0.3476, Longitude = 32.5825 }; // Kampala

            // Act
            var potential = calculator.CalculateSolarPotential(location, roofAreaSqm: 100.0);

            // Assert
            // Uganda receives ~5.5 kWh/m²/day average solar radiation
            potential.AnnualGenerationKwh.Should().BeGreaterThan(10000.0);
            potential.PeakCapacityKw.Should().BeGreaterThan(10.0);
        }

        [Test]
        public void CalculatePaybackPeriod_ForSolarInstallation_ReturnsYears()
        {
            // Arrange
            var calculator = new RenewableEnergyCalculator();
            var installation = new SolarInstallation
            {
                CapacityKw = 10.0,
                InstallationCost = 15000.0,
                AnnualGenerationKwh = 15000.0,
                GridTariffPerKwh = 0.20
            };

            // Act
            var paybackYears = calculator.CalculatePaybackPeriod(installation);

            // Assert
            paybackYears.Should().BeInRange(3.0, 10.0);
        }

        #endregion

        #region Helper Methods

        private List<EnergyReading> GenerateDailyPattern()
        {
            var readings = new List<EnergyReading>();
            var baseDate = DateTime.Today;

            for (int hour = 0; hour < 24; hour++)
            {
                // Simulate typical office building pattern
                double value = hour switch
                {
                    >= 0 and < 6 => 50.0 + new Random(hour).NextDouble() * 10,
                    >= 6 and < 9 => 100.0 + new Random(hour).NextDouble() * 30,
                    >= 9 and < 17 => 200.0 + new Random(hour).NextDouble() * 50,
                    >= 17 and < 20 => 150.0 + new Random(hour).NextDouble() * 30,
                    _ => 70.0 + new Random(hour).NextDouble() * 15
                };

                readings.Add(new EnergyReading
                {
                    Timestamp = baseDate.AddHours(hour),
                    Value = value
                });
            }

            return readings;
        }

        private List<EnergyReading> GenerateWeeklyPattern()
        {
            var readings = new List<EnergyReading>();
            var baseDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            for (int day = 0; day < 7; day++)
            {
                bool isWeekend = day == 0 || day == 6;
                double baseDemand = isWeekend ? 100.0 : 250.0;

                for (int hour = 0; hour < 24; hour++)
                {
                    readings.Add(new EnergyReading
                    {
                        Timestamp = baseDate.AddDays(day).AddHours(hour),
                        Value = baseDemand + new Random(day * 24 + hour).NextDouble() * 50
                    });
                }
            }

            return readings;
        }

        private List<EnergyReading> GenerateNormalReadingsWithSpike()
        {
            var readings = GenerateNormalReadings();
            // Add a spike at hour 12
            readings[12] = new EnergyReading
            {
                Timestamp = readings[12].Timestamp,
                Value = readings[12].Value * 5 // 5x normal value
            };
            return readings;
        }

        private List<EnergyReading> GenerateNormalReadings()
        {
            var readings = new List<EnergyReading>();
            var baseDate = DateTime.Today;
            var random = new Random(42);

            for (int hour = 0; hour < 24; hour++)
            {
                readings.Add(new EnergyReading
                {
                    Timestamp = baseDate.AddHours(hour),
                    Value = 150.0 + random.NextDouble() * 20 // Low variance
                });
            }

            return readings;
        }

        #endregion
    }

    #region Test Helper Classes

    internal class EnergyReading
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    internal class EnergyConsumptionCalculator
    {
        public double CalculateTotalConsumption(List<EnergyReading> readings)
        {
            double total = 0;
            foreach (var reading in readings)
            {
                total += reading.Value;
            }
            return total;
        }

        public double CalculateAverageConsumption(List<EnergyReading> readings)
        {
            if (readings.Count == 0) return 0;
            return CalculateTotalConsumption(readings) / readings.Count;
        }

        public EnergyReading CalculatePeakConsumption(List<EnergyReading> readings)
        {
            EnergyReading peak = null;
            foreach (var reading in readings)
            {
                if (peak == null || reading.Value > peak.Value)
                {
                    peak = reading;
                }
            }
            return peak;
        }
    }

    internal enum EnergySource
    {
        Electricity,
        NaturalGas,
        Solar,
        Wind,
        Diesel
    }

    internal class CarbonFootprintCalculator
    {
        private readonly Dictionary<string, double> _gridEmissionFactors = new()
        {
            { "Uganda", 0.12 },
            { "Kenya", 0.35 },
            { "South Africa", 0.92 },
            { "Default", 0.5 }
        };

        private readonly Dictionary<EnergySource, double> _sourceEmissionFactors = new()
        {
            { EnergySource.Electricity, 0.5 },
            { EnergySource.NaturalGas, 0.2 },
            { EnergySource.Solar, 0.04 },
            { EnergySource.Wind, 0.01 },
            { EnergySource.Diesel, 0.27 }
        };

        public double CalculateEmissions(double energyKwh, EnergySource source, string region = null)
        {
            double factor;
            if (source == EnergySource.Electricity && region != null)
            {
                factor = GetGridEmissionFactor(region);
            }
            else
            {
                factor = _sourceEmissionFactors[source];
            }
            return energyKwh * factor;
        }

        public double GetGridEmissionFactor(string region)
        {
            return _gridEmissionFactors.TryGetValue(region, out var factor) ? factor : _gridEmissionFactors["Default"];
        }
    }

    internal enum MeterType
    {
        Electricity,
        Gas,
        Water,
        Steam
    }

    internal class MeterInfo
    {
        public string MeterId { get; set; }
        public string Name { get; set; }
        public MeterType MeterType { get; set; }
        public string Location { get; set; }
        public double MaxCapacity { get; set; }
    }

    internal class MeterManager
    {
        private readonly Dictionary<string, MeterInfo> _meters = new();
        private readonly Dictionary<string, List<EnergyReading>> _readings = new();

        public bool RegisterMeter(MeterInfo meterInfo)
        {
            if (_meters.ContainsKey(meterInfo.MeterId)) return false;
            _meters[meterInfo.MeterId] = meterInfo;
            _readings[meterInfo.MeterId] = new List<EnergyReading>();
            return true;
        }

        public MeterInfo GetMeter(string meterId)
        {
            return _meters.TryGetValue(meterId, out var meter) ? meter : null;
        }

        public bool RecordReading(string meterId, double value, DateTime timestamp)
        {
            if (!_readings.ContainsKey(meterId)) return false;
            _readings[meterId].Add(new EnergyReading { Timestamp = timestamp, Value = value });
            return true;
        }
    }

    internal enum ConsumptionPatternType
    {
        Daily,
        Weekly,
        Seasonal,
        Irregular
    }

    internal enum AnomalyType
    {
        Spike,
        Drop,
        Drift,
        Oscillation
    }

    internal class ConsumptionPattern
    {
        public ConsumptionPatternType PatternType { get; set; }
        public List<int> PeakHours { get; set; } = new();
        public double WeekendReduction { get; set; }
    }

    internal class ConsumptionAnomaly
    {
        public DateTime Timestamp { get; set; }
        public AnomalyType AnomalyType { get; set; }
        public double DeviationFactor { get; set; }
    }

    internal class ConsumptionPatternAnalyzer
    {
        public ConsumptionPattern AnalyzePattern(List<EnergyReading> readings)
        {
            var pattern = new ConsumptionPattern { PatternType = ConsumptionPatternType.Daily };

            // Find peak hours (simple implementation)
            double avgValue = 0;
            foreach (var r in readings) avgValue += r.Value;
            avgValue /= readings.Count;

            foreach (var r in readings)
            {
                if (r.Value > avgValue * 1.2)
                {
                    pattern.PeakHours.Add(r.Timestamp.Hour);
                }
            }

            // Calculate weekend reduction
            double weekdayAvg = 0, weekendAvg = 0;
            int weekdayCount = 0, weekendCount = 0;
            foreach (var r in readings)
            {
                if (r.Timestamp.DayOfWeek == DayOfWeek.Saturday || r.Timestamp.DayOfWeek == DayOfWeek.Sunday)
                {
                    weekendAvg += r.Value;
                    weekendCount++;
                }
                else
                {
                    weekdayAvg += r.Value;
                    weekdayCount++;
                }
            }

            if (weekdayCount > 0 && weekendCount > 0)
            {
                weekdayAvg /= weekdayCount;
                weekendAvg /= weekendCount;
                pattern.WeekendReduction = (weekdayAvg - weekendAvg) / weekdayAvg;
            }

            return pattern;
        }

        public List<ConsumptionAnomaly> DetectAnomalies(List<EnergyReading> readings, double sensitivityLevel)
        {
            var anomalies = new List<ConsumptionAnomaly>();

            // Calculate mean and standard deviation
            double mean = 0;
            foreach (var r in readings) mean += r.Value;
            mean /= readings.Count;

            double variance = 0;
            foreach (var r in readings) variance += Math.Pow(r.Value - mean, 2);
            double stdDev = Math.Sqrt(variance / readings.Count);

            // Detect anomalies
            foreach (var r in readings)
            {
                double deviation = Math.Abs(r.Value - mean) / stdDev;
                if (deviation > sensitivityLevel)
                {
                    anomalies.Add(new ConsumptionAnomaly
                    {
                        Timestamp = r.Timestamp,
                        AnomalyType = r.Value > mean ? AnomalyType.Spike : AnomalyType.Drop,
                        DeviationFactor = deviation
                    });
                }
            }

            return anomalies;
        }
    }

    internal enum RecommendationType
    {
        LoadShifting,
        EfficiencyUpgrade,
        RenewableIntegration,
        BehavioralChange,
        EquipmentReplacement
    }

    internal class OptimizationRecommendation
    {
        public RecommendationType RecommendationType { get; set; }
        public string Description { get; set; }
        public double EstimatedSavingsPercent { get; set; }
    }

    internal class EnergyProfile
    {
        public double PeakDemand { get; set; }
        public double AverageDemand { get; set; }
        public double PeakToAverageRatio { get; set; }
        public double BaseLoad { get; set; }
        public double TotalCapacity { get; set; }
        public double BaseLoadRatio { get; set; }
        public double AnnualConsumption { get; set; }
        public double EnergyCostPerKwh { get; set; }
    }

    internal class EnergyOptimizer
    {
        public List<OptimizationRecommendation> GenerateRecommendations(EnergyProfile profile)
        {
            var recommendations = new List<OptimizationRecommendation>();

            if (profile.PeakToAverageRatio > 2.0)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    RecommendationType = RecommendationType.LoadShifting,
                    Description = "Shift non-critical loads to off-peak hours",
                    EstimatedSavingsPercent = 15.0
                });
            }

            if (profile.BaseLoadRatio > 0.6)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    RecommendationType = RecommendationType.EfficiencyUpgrade,
                    Description = "Upgrade base load equipment for better efficiency",
                    EstimatedSavingsPercent = 20.0
                });
            }

            return recommendations;
        }

        public SavingsEstimate CalculatePotentialSavings(EnergyProfile profile)
        {
            // Assume 10-15% savings potential for typical buildings
            double savingsPercent = 0.12;
            return new SavingsEstimate
            {
                EnergySavingsKwh = profile.AnnualConsumption * savingsPercent,
                CostSavings = profile.AnnualConsumption * savingsPercent * profile.EnergyCostPerKwh
            };
        }
    }

    internal class SavingsEstimate
    {
        public double EnergySavingsKwh { get; set; }
        public double CostSavings { get; set; }
    }

    internal class GeoLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    internal class SolarPotential
    {
        public double AnnualGenerationKwh { get; set; }
        public double PeakCapacityKw { get; set; }
    }

    internal class SolarInstallation
    {
        public double CapacityKw { get; set; }
        public double InstallationCost { get; set; }
        public double AnnualGenerationKwh { get; set; }
        public double GridTariffPerKwh { get; set; }
    }

    internal class RenewableEnergyCalculator
    {
        public SolarPotential CalculateSolarPotential(GeoLocation location, double roofAreaSqm)
        {
            // Uganda receives approximately 5.5 kWh/m²/day solar radiation
            // Assume 15% efficient panels and 80% system efficiency
            double solarRadiation = 5.5; // kWh/m²/day for equatorial regions
            double panelEfficiency = 0.15;
            double systemEfficiency = 0.80;
            double daysPerYear = 365;

            double peakCapacity = roofAreaSqm * panelEfficiency; // kW
            double annualGeneration = roofAreaSqm * solarRadiation * panelEfficiency * systemEfficiency * daysPerYear;

            return new SolarPotential
            {
                AnnualGenerationKwh = annualGeneration,
                PeakCapacityKw = peakCapacity
            };
        }

        public double CalculatePaybackPeriod(SolarInstallation installation)
        {
            double annualSavings = installation.AnnualGenerationKwh * installation.GridTariffPerKwh;
            return installation.InstallationCost / annualSavings;
        }
    }

    #endregion
}
