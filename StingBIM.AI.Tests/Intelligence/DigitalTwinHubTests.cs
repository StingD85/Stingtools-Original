// ============================================================================
// StingBIM AI Tests - Digital Twin Hub Tests
// Unit tests for IoT sensor integration, real-time monitoring, and scenarios
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.DigitalTwin;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class DigitalTwinHubTests
    {
        private DigitalTwinHub _hub;

        [SetUp]
        public void Setup()
        {
            _hub = DigitalTwinHub.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = DigitalTwinHub.Instance;
            var instance2 = DigitalTwinHub.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        #region Sensor Registration Tests

        [Test]
        public void RegisterSensor_ShouldCreateSensorDevice()
        {
            // Arrange
            var registration = new SensorRegistration
            {
                Name = "Temperature Sensor 1",
                Type = SensorType.Temperature,
                Location = "Office 101",
                Floor = "Level 1",
                Zone = "Zone A"
            };

            // Act
            var sensor = _hub.RegisterSensor(registration);

            // Assert
            Assert.That(sensor, Is.Not.Null);
            Assert.That(sensor.SensorId, Is.Not.Null);
            Assert.That(sensor.Name, Is.EqualTo("Temperature Sensor 1"));
            Assert.That(sensor.Type, Is.EqualTo(SensorType.Temperature));
            Assert.That(sensor.Unit, Is.EqualTo("°C"));
            Assert.That(sensor.Status, Is.EqualTo(SensorStatus.Online));
        }

        [Test]
        public void RegisterSensor_ShouldAssignCorrectUnitsByType()
        {
            // Arrange & Act
            var tempSensor = _hub.RegisterSensor(new SensorRegistration { Name = "Temp", Type = SensorType.Temperature });
            var humiditySensor = _hub.RegisterSensor(new SensorRegistration { Name = "Humidity", Type = SensorType.Humidity });
            var co2Sensor = _hub.RegisterSensor(new SensorRegistration { Name = "CO2", Type = SensorType.CO2 });
            var energySensor = _hub.RegisterSensor(new SensorRegistration { Name = "Energy", Type = SensorType.Energy });

            // Assert
            Assert.That(tempSensor.Unit, Is.EqualTo("°C"));
            Assert.That(humiditySensor.Unit, Is.EqualTo("%RH"));
            Assert.That(co2Sensor.Unit, Is.EqualTo("ppm"));
            Assert.That(energySensor.Unit, Is.EqualTo("kWh"));
        }

        [Test]
        public void RegisterSensor_WithCustomId_ShouldUseProvidedId()
        {
            // Arrange
            var registration = new SensorRegistration
            {
                SensorId = "CUSTOM-SENSOR-001",
                Name = "Custom Sensor",
                Type = SensorType.Motion
            };

            // Act
            var sensor = _hub.RegisterSensor(registration);

            // Assert
            Assert.That(sensor.SensorId, Is.EqualTo("CUSTOM-SENSOR-001"));
        }

        #endregion

        #region Sensor Reading Tests

        [Test]
        public void ProcessSensorReading_ShouldUpdateLatestReading()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Test Temp Sensor",
                Type = SensorType.Temperature,
                SpaceId = "SPACE-001"
            });

            // Act
            _hub.ProcessSensorReading(sensor.SensorId, 23.5);

            // Assert
            Assert.That(sensor.LastReading, Is.Not.Null);
            Assert.That(sensor.LastReading.Value, Is.EqualTo(23.5));
        }

        [Test]
        public void ProcessSensorReading_ShouldFireSensorDataReceivedEvent()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Event Test Sensor",
                Type = SensorType.Humidity
            });

            SensorEventArgs receivedArgs = null;
            _hub.SensorDataReceived += (s, e) => receivedArgs = e;

            // Act
            _hub.ProcessSensorReading(sensor.SensorId, 55.0);

            // Assert
            Assert.That(receivedArgs, Is.Not.Null);
            Assert.That(receivedArgs.Sensor.SensorId, Is.EqualTo(sensor.SensorId));
            Assert.That(receivedArgs.Reading.Value, Is.EqualTo(55.0));
        }

        [Test]
        public void ProcessSensorReading_ShouldDetermineReadingQuality()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Quality Test Sensor",
                Type = SensorType.Temperature,
                MinValue = 0,
                MaxValue = 50
            });

            // Act - Normal reading
            _hub.ProcessSensorReading(sensor.SensorId, 25.0);
            var normalQuality = sensor.LastReading.Quality;

            // Act - Out of range reading
            _hub.ProcessSensorReading(sensor.SensorId, 100.0);
            var outOfRangeQuality = sensor.LastReading.Quality;

            // Assert
            Assert.That(normalQuality, Is.EqualTo(ReadingQuality.Good));
            Assert.That(outOfRangeQuality, Is.EqualTo(ReadingQuality.OutOfRange));
        }

        #endregion

        #region Alert Tests

        [Test]
        public void ProcessSensorReading_ShouldTriggerAlertForHighTemperature()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Alert Test Temp Sensor",
                Type = SensorType.Temperature
            });

            AlertEventArgs alertArgs = null;
            _hub.AlertTriggered += (s, e) => alertArgs = e;

            // Act - Send reading above threshold (28°C)
            _hub.ProcessSensorReading(sensor.SensorId, 30.0);

            // Assert
            Assert.That(alertArgs, Is.Not.Null);
            Assert.That(alertArgs.Alert.Severity, Is.EqualTo(AlertSeverity.Warning));
            Assert.That(alertArgs.Alert.Message.Contains("comfort threshold"), Is.True);
        }

        [Test]
        public void ProcessSensorReading_ShouldTriggerCriticalAlertForDangerousTemperature()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Critical Alert Temp Sensor",
                Type = SensorType.Temperature
            });

            AlertEventArgs alertArgs = null;
            _hub.AlertTriggered += (s, e) => alertArgs = e;

            // Act - Send reading above critical threshold (35°C)
            _hub.ProcessSensorReading(sensor.SensorId, 40.0);

            // Assert
            Assert.That(alertArgs, Is.Not.Null);
            Assert.That(alertArgs.Alert.Severity, Is.EqualTo(AlertSeverity.Critical));
        }

        [Test]
        public void AcknowledgeAlert_ShouldUpdateAlertStatus()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Acknowledge Test Sensor",
                Type = SensorType.CO2
            });

            Alert createdAlert = null;
            _hub.AlertTriggered += (s, e) => createdAlert = e.Alert;

            // Trigger alert
            _hub.ProcessSensorReading(sensor.SensorId, 1500.0);

            // Act
            _hub.AcknowledgeAlert(createdAlert.AlertId, "John Doe", "Investigating issue");

            // Assert
            Assert.That(createdAlert.Status, Is.EqualTo(AlertStatus.Acknowledged));
            Assert.That(createdAlert.AcknowledgedBy, Is.EqualTo("John Doe"));
            Assert.That(createdAlert.AcknowledgedTime, Is.Not.Null);
        }

        #endregion

        #region Twin Element Tests

        [Test]
        public void RegisterTwinElement_ShouldCreateElement()
        {
            // Arrange
            var registration = new TwinElementRegistration
            {
                ElementId = "AHU-001",
                Name = "Air Handling Unit 1",
                Category = "HVAC",
                Type = "AHU",
                Floor = "Level 2",
                Space = "Mechanical Room"
            };

            // Act
            var element = _hub.RegisterTwinElement(registration);

            // Assert
            Assert.That(element, Is.Not.Null);
            Assert.That(element.ElementId, Is.EqualTo("AHU-001"));
            Assert.That(element.Name, Is.EqualTo("Air Handling Unit 1"));
            Assert.That(element.State, Is.EqualTo(TwinElementState.Normal));
        }

        [Test]
        public void LinkSensorToElement_ShouldEstablishConnection()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Link Test Sensor",
                Type = SensorType.Vibration
            });

            var element = _hub.RegisterTwinElement(new TwinElementRegistration
            {
                ElementId = "PUMP-001",
                Name = "Chilled Water Pump",
                Category = "Mechanical"
            });

            // Act
            _hub.LinkSensorToElement(sensor.SensorId, element.ElementId);

            // Assert
            Assert.That(sensor.ElementId, Is.EqualTo(element.ElementId));
            Assert.That(element.LinkedSensors.Contains(sensor.SensorId), Is.True);
        }

        #endregion

        #region Scenario Tests

        [Test]
        public void CreateScenario_ShouldCreateScenarioDefinition()
        {
            // Arrange
            var definition = new ScenarioDefinition
            {
                Name = "HVAC Failure Test",
                Description = "Simulate AHU failure in Zone A",
                Type = ScenarioType.EquipmentFailure,
                Parameters = new Dictionary<string, object> { { "zone", "Zone A" } },
                CreatedBy = "Test User"
            };

            // Act
            var scenario = _hub.CreateScenario(definition);

            // Assert
            Assert.That(scenario, Is.Not.Null);
            Assert.That(scenario.ScenarioId, Is.Not.Null);
            Assert.That(scenario.Name, Is.EqualTo("HVAC Failure Test"));
            Assert.That(scenario.Type, Is.EqualTo(ScenarioType.EquipmentFailure));
            Assert.That(scenario.Status, Is.EqualTo(ScenarioStatus.Created));
        }

        [Test]
        public async Task RunScenarioAsync_ShouldSimulateEquipmentFailure()
        {
            // Arrange
            var definition = new ScenarioDefinition
            {
                Name = "Equipment Failure Scenario",
                Type = ScenarioType.EquipmentFailure,
                Parameters = new Dictionary<string, object> { { "zone", "Zone B" } },
                CreatedBy = "Test User"
            };
            var scenario = _hub.CreateScenario(definition);

            // Act
            var result = await _hub.RunScenarioAsync(scenario.ScenarioId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Impacts.Count > 0, Is.True);
            Assert.That(result.Recommendations.Count > 0, Is.True);
            Assert.That(result.Impacts.Exists(i => i.Category == "HVAC"), Is.True);
        }

        [Test]
        public async Task RunScenarioAsync_ShouldSimulateEmergencyEvacuation()
        {
            // Arrange
            var definition = new ScenarioDefinition
            {
                Name = "Emergency Evacuation",
                Type = ScenarioType.EmergencyEvacuation,
                Parameters = new Dictionary<string, object> { { "occupants", 1000 } },
                CreatedBy = "Test User"
            };
            var scenario = _hub.CreateScenario(definition);

            // Act
            var result = await _hub.RunScenarioAsync(scenario.ScenarioId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Impacts.Exists(i => i.Category == "Safety"), Is.True);
            Assert.That(result.Recommendations.Exists(r => r.Contains("emergency")), Is.True);
        }

        [Test]
        public async Task RunScenarioAsync_ShouldFireScenarioCompletedEvent()
        {
            // Arrange
            var definition = new ScenarioDefinition
            {
                Name = "Event Test Scenario",
                Type = ScenarioType.OccupancyChange,
                CreatedBy = "Test User"
            };
            var scenario = _hub.CreateScenario(definition);

            ScenarioEventArgs eventArgs = null;
            _hub.ScenarioCompleted += (s, e) => eventArgs = e;

            // Act
            await _hub.RunScenarioAsync(scenario.ScenarioId);

            // Assert
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Scenario.ScenarioId, Is.EqualTo(scenario.ScenarioId));
            Assert.That(eventArgs.Scenario.Status, Is.EqualTo(ScenarioStatus.Completed));
        }

        #endregion

        #region Dashboard Tests

        [Test]
        public void GetDashboard_ShouldReturnDashboardData()
        {
            // Arrange - Register some sensors
            _hub.RegisterSensor(new SensorRegistration { Name = "Dashboard Test 1", Type = SensorType.Temperature });
            _hub.RegisterSensor(new SensorRegistration { Name = "Dashboard Test 2", Type = SensorType.Humidity });

            // Act
            var dashboard = _hub.GetDashboard();

            // Assert
            Assert.That(dashboard, Is.Not.Null);
            Assert.That(dashboard.TotalSensors > 0, Is.True);
            Assert.That(dashboard.SensorsByType, Is.Not.Null);
            Assert.That(dashboard.EnvironmentalSummary, Is.Not.Null);
        }

        [Test]
        public void GetSpaceAnalytics_ShouldReturnSpaceData()
        {
            // Arrange
            var spaceId = "SPACE-ANALYTICS-TEST";
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "Space Analytics Sensor",
                Type = SensorType.Temperature,
                SpaceId = spaceId
            });
            _hub.ProcessSensorReading(sensor.SensorId, 22.5);

            // Act
            var analytics = _hub.GetSpaceAnalytics(spaceId);

            // Assert
            Assert.That(analytics, Is.Not.Null);
            Assert.That(analytics.SpaceId, Is.EqualTo(spaceId));
            Assert.That(analytics.SensorCount, Is.EqualTo(1));
            Assert.That(analytics.CurrentTemperature, Is.EqualTo(22.5));
        }

        [Test]
        public void GetSensorHistory_ShouldReturnReadingHistory()
        {
            // Arrange
            var sensor = _hub.RegisterSensor(new SensorRegistration
            {
                Name = "History Test Sensor",
                Type = SensorType.Energy
            });

            // Add multiple readings
            for (int i = 0; i < 5; i++)
            {
                _hub.ProcessSensorReading(sensor.SensorId, 100 + i * 10);
            }

            // Act
            var history = _hub.GetSensorHistory(sensor.SensorId);

            // Assert
            Assert.That(history, Is.Not.Null);
            Assert.That(history.Count, Is.EqualTo(5));
        }

        #endregion
    }
}
