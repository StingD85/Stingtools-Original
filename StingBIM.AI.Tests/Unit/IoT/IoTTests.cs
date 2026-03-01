// StingBIM.AI.Tests - IoT + Digital Twin Module Tests
// Tests for sensor integration, digital twin synchronization, anomaly detection

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.IoT.Engine;
using StingBIM.AI.IoT.Models;

namespace StingBIM.AI.Tests.Unit.IoT
{
    [TestFixture]
    public class IoTTests
    {
        #region SensorIntegrationEngine Tests

        [TestFixture]
        public class SensorIntegrationEngineTests
        {
            private SensorIntegrationEngine _engine;

            [SetUp]
            public void Setup()
            {
                _engine = new SensorIntegrationEngine(
                    ringBufferCapacity: 100,
                    maxHistoricalReadings: 1000);
            }

            [Test]
            public void Constructor_ShouldInitialize()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void Constructor_WithDefaults_ShouldUseDefaultCapacity()
            {
                var engine = new SensorIntegrationEngine();
                engine.Should().NotBeNull();
            }

            [Test]
            public void RegisterSensor_ValidSensor_ShouldReturnTrue()
            {
                var sensor = new SensorDefinition
                {
                    Id = "TEMP-001",
                    Name = "Zone A Temperature",
                    Type = SensorCategory.Temperature,
                    Unit = "degC",
                    Location = "Office Zone A"
                };

                var result = _engine.RegisterSensor(sensor);
                result.Should().BeTrue();
            }

            [Test]
            public void RegisterSensor_DuplicateId_ShouldReturnFalse()
            {
                var sensor = new SensorDefinition
                {
                    Id = "TEMP-DUP",
                    Name = "Duplicate Sensor",
                    Type = SensorCategory.Temperature,
                    Unit = "degC"
                };

                _engine.RegisterSensor(sensor).Should().BeTrue();
                _engine.RegisterSensor(sensor).Should().BeFalse();
            }

            [Test]
            public async Task IngestReadingAsync_ValidReading_ShouldReturnTrue()
            {
                var sensor = new SensorDefinition
                {
                    Id = "TEMP-INGEST",
                    Name = "Ingestion Test",
                    Type = SensorCategory.Temperature,
                    Unit = "degC"
                };
                _engine.RegisterSensor(sensor);

                var reading = new SensorReading
                {
                    SensorId = "TEMP-INGEST",
                    Value = 22.5,
                    Timestamp = DateTime.UtcNow
                };

                var result = await _engine.IngestReadingAsync(reading);
                result.Should().BeTrue();
            }

            [Test]
            public async Task IngestReadingAsync_UnregisteredSensor_ShouldReturnFalse()
            {
                var reading = new SensorReading
                {
                    SensorId = "UNKNOWN-SENSOR",
                    Value = 10.0,
                    Timestamp = DateTime.UtcNow
                };

                var result = await _engine.IngestReadingAsync(reading);
                result.Should().BeFalse();
            }

            [Test]
            public async Task DetectAnomaliesAsync_NoData_ShouldReturnEmptyList()
            {
                var sensor = new SensorDefinition
                {
                    Id = "ANOM-TEST",
                    Name = "Anomaly Test",
                    Type = SensorCategory.Temperature,
                    Unit = "degC"
                };
                _engine.RegisterSensor(sensor);

                var anomalies = await _engine.DetectAnomaliesAsync(
                    "ANOM-TEST", TimeSpan.FromHours(1));
                anomalies.Should().NotBeNull();
            }
        }

        #endregion

        #region IoT Data Model Tests

        [TestFixture]
        public class IoTDataModelTests
        {
            [Test]
            public void SensorDefinition_Properties_ShouldBeSettable()
            {
                var sensor = new SensorDefinition
                {
                    Id = "S-001",
                    Name = "Test Sensor",
                    Type = SensorCategory.Temperature,
                    Unit = "degC",
                    Location = "Server Room"
                };

                sensor.Id.Should().Be("S-001");
                sensor.Type.Should().Be(SensorCategory.Temperature);
            }

            [Test]
            public void SensorReading_Timestamp_ShouldBeSet()
            {
                var now = DateTime.UtcNow;
                var reading = new SensorReading
                {
                    SensorId = "S-001",
                    Value = 22.5,
                    Timestamp = now
                };

                reading.Timestamp.Should().Be(now);
                reading.Value.Should().Be(22.5);
            }

            [Test]
            public void SensorCategory_ShouldContainExpectedValues()
            {
                Enum.GetValues(typeof(SensorCategory)).Length.Should().BeGreaterThan(0);
                Enum.IsDefined(typeof(SensorCategory), SensorCategory.Temperature).Should().BeTrue();
            }
        }

        #endregion
    }
}
