// ============================================================================
// StingBIM AI - Digital Twin Hub
// Real-time IoT sensor integration, live model sync, and scenario simulation
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DigitalTwin
{
    /// <summary>
    /// Digital Twin Hub providing real-time building data integration,
    /// IoT sensor connectivity, live model synchronization, and what-if simulations.
    /// </summary>
    public sealed class DigitalTwinHub
    {
        private static readonly Lazy<DigitalTwinHub> _instance =
            new Lazy<DigitalTwinHub>(() => new DigitalTwinHub());
        public static DigitalTwinHub Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<string, SensorDevice> _sensors = new();
        private readonly ConcurrentDictionary<string, SensorReading> _latestReadings = new();
        private readonly ConcurrentDictionary<string, List<SensorReading>> _readingHistory = new();
        private readonly ConcurrentDictionary<string, TwinElement> _twinElements = new();
        private readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();
        private readonly List<Scenario> _scenarios = new();
        private readonly Dictionary<string, ThresholdRule> _thresholdRules = new();

        private CancellationTokenSource _monitoringCts;
        private bool _isMonitoring;

        public event EventHandler<SensorEventArgs> SensorDataReceived;
        public event EventHandler<AlertEventArgs> AlertTriggered;
        public event EventHandler<TwinEventArgs> TwinStateChanged;
        public event EventHandler<ScenarioEventArgs> ScenarioCompleted;

        private DigitalTwinHub()
        {
            InitializeDefaultThresholds();
        }

        #region Initialization

        private void InitializeDefaultThresholds()
        {
            // Temperature thresholds
            _thresholdRules["temp_high"] = new ThresholdRule
            {
                RuleId = "temp_high",
                SensorType = SensorType.Temperature,
                Condition = ThresholdCondition.GreaterThan,
                Value = 28.0,
                AlertSeverity = AlertSeverity.Warning,
                Message = "Temperature exceeds comfort threshold"
            };

            _thresholdRules["temp_critical"] = new ThresholdRule
            {
                RuleId = "temp_critical",
                SensorType = SensorType.Temperature,
                Condition = ThresholdCondition.GreaterThan,
                Value = 35.0,
                AlertSeverity = AlertSeverity.Critical,
                Message = "Critical temperature level - equipment at risk"
            };

            // Humidity thresholds
            _thresholdRules["humidity_high"] = new ThresholdRule
            {
                RuleId = "humidity_high",
                SensorType = SensorType.Humidity,
                Condition = ThresholdCondition.GreaterThan,
                Value = 70.0,
                AlertSeverity = AlertSeverity.Warning,
                Message = "High humidity - mold risk"
            };

            // CO2 thresholds (ASHRAE)
            _thresholdRules["co2_elevated"] = new ThresholdRule
            {
                RuleId = "co2_elevated",
                SensorType = SensorType.CO2,
                Condition = ThresholdCondition.GreaterThan,
                Value = 1000.0,
                AlertSeverity = AlertSeverity.Warning,
                Message = "CO2 levels elevated - increase ventilation"
            };

            _thresholdRules["co2_high"] = new ThresholdRule
            {
                RuleId = "co2_high",
                SensorType = SensorType.CO2,
                Condition = ThresholdCondition.GreaterThan,
                Value = 2000.0,
                AlertSeverity = AlertSeverity.Critical,
                Message = "CO2 levels dangerous - evacuate area"
            };

            // Occupancy
            _thresholdRules["occupancy_max"] = new ThresholdRule
            {
                RuleId = "occupancy_max",
                SensorType = SensorType.Occupancy,
                Condition = ThresholdCondition.GreaterThanPercent,
                Value = 100.0,
                AlertSeverity = AlertSeverity.Critical,
                Message = "Maximum occupancy exceeded"
            };

            // Energy consumption
            _thresholdRules["energy_peak"] = new ThresholdRule
            {
                RuleId = "energy_peak",
                SensorType = SensorType.Energy,
                Condition = ThresholdCondition.GreaterThanPercent,
                Value = 90.0,
                AlertSeverity = AlertSeverity.Warning,
                Message = "Approaching peak energy demand"
            };

            // Water leak detection
            _thresholdRules["water_leak"] = new ThresholdRule
            {
                RuleId = "water_leak",
                SensorType = SensorType.WaterLeak,
                Condition = ThresholdCondition.Equals,
                Value = 1.0,
                AlertSeverity = AlertSeverity.Critical,
                Message = "Water leak detected!"
            };
        }

        #endregion

        #region Sensor Management

        /// <summary>
        /// Register a new IoT sensor device
        /// </summary>
        public SensorDevice RegisterSensor(SensorRegistration registration)
        {
            var sensor = new SensorDevice
            {
                SensorId = registration.SensorId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Type = registration.Type,
                Location = registration.Location,
                ElementId = registration.ElementId,
                SpaceId = registration.SpaceId,
                Floor = registration.Floor,
                Zone = registration.Zone,
                Unit = GetDefaultUnit(registration.Type),
                MinValue = registration.MinValue,
                MaxValue = registration.MaxValue,
                ReadingInterval = registration.ReadingInterval ?? TimeSpan.FromMinutes(5),
                Status = SensorStatus.Online,
                RegisteredDate = DateTime.UtcNow,
                Metadata = registration.Metadata ?? new Dictionary<string, string>()
            };

            _sensors[sensor.SensorId] = sensor;
            _readingHistory[sensor.SensorId] = new List<SensorReading>();

            return sensor;
        }

        private string GetDefaultUnit(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => "°C",
                SensorType.Humidity => "%RH",
                SensorType.CO2 => "ppm",
                SensorType.Pressure => "Pa",
                SensorType.Light => "lux",
                SensorType.Occupancy => "count",
                SensorType.Motion => "bool",
                SensorType.Energy => "kWh",
                SensorType.Water => "L",
                SensorType.Gas => "m³",
                SensorType.Noise => "dB",
                SensorType.AirQuality => "AQI",
                SensorType.WaterLeak => "bool",
                SensorType.DoorContact => "bool",
                SensorType.Vibration => "mm/s",
                _ => "unit"
            };
        }

        /// <summary>
        /// Process incoming sensor reading
        /// </summary>
        public void ProcessSensorReading(string sensorId, double value, DateTime? timestamp = null)
        {
            if (!_sensors.TryGetValue(sensorId, out var sensor))
                return;

            var reading = new SensorReading
            {
                ReadingId = Guid.NewGuid().ToString(),
                SensorId = sensorId,
                Value = value,
                Unit = sensor.Unit,
                Timestamp = timestamp ?? DateTime.UtcNow,
                Quality = DetermineReadingQuality(sensor, value)
            };

            // Update latest reading
            _latestReadings[sensorId] = reading;

            // Add to history (keep last 1000 readings per sensor)
            if (_readingHistory.TryGetValue(sensorId, out var history))
            {
                lock (_lock)
                {
                    history.Add(reading);
                    if (history.Count > 1000)
                        history.RemoveAt(0);
                }
            }

            // Update sensor status
            sensor.LastReading = reading;
            sensor.LastReadingTime = reading.Timestamp;

            // Check thresholds
            CheckThresholds(sensor, reading);

            // Update linked twin element
            UpdateTwinElement(sensor, reading);

            // Fire event
            SensorDataReceived?.Invoke(this, new SensorEventArgs
            {
                Sensor = sensor,
                Reading = reading
            });
        }

        private ReadingQuality DetermineReadingQuality(SensorDevice sensor, double value)
        {
            if (sensor.MinValue.HasValue && value < sensor.MinValue.Value)
                return ReadingQuality.OutOfRange;
            if (sensor.MaxValue.HasValue && value > sensor.MaxValue.Value)
                return ReadingQuality.OutOfRange;
            return ReadingQuality.Good;
        }

        private void CheckThresholds(SensorDevice sensor, SensorReading reading)
        {
            foreach (var rule in _thresholdRules.Values.Where(r => r.SensorType == sensor.Type))
            {
                var triggered = rule.Condition switch
                {
                    ThresholdCondition.GreaterThan => reading.Value > rule.Value,
                    ThresholdCondition.LessThan => reading.Value < rule.Value,
                    ThresholdCondition.Equals => Math.Abs(reading.Value - rule.Value) < 0.01,
                    ThresholdCondition.GreaterThanPercent => sensor.MaxValue.HasValue &&
                        (reading.Value / sensor.MaxValue.Value * 100) > rule.Value,
                    ThresholdCondition.LessThanPercent => sensor.MaxValue.HasValue &&
                        (reading.Value / sensor.MaxValue.Value * 100) < rule.Value,
                    _ => false
                };

                if (triggered)
                {
                    CreateAlert(sensor, reading, rule);
                }
            }
        }

        private void CreateAlert(SensorDevice sensor, SensorReading reading, ThresholdRule rule)
        {
            var alertKey = $"{sensor.SensorId}_{rule.RuleId}";

            // Don't create duplicate alerts
            if (_activeAlerts.ContainsKey(alertKey))
                return;

            var alert = new Alert
            {
                AlertId = Guid.NewGuid().ToString(),
                SensorId = sensor.SensorId,
                RuleId = rule.RuleId,
                Severity = rule.AlertSeverity,
                Message = rule.Message,
                Value = reading.Value,
                Threshold = rule.Value,
                Location = sensor.Location,
                SpaceId = sensor.SpaceId,
                ElementId = sensor.ElementId,
                Timestamp = DateTime.UtcNow,
                Status = AlertStatus.Active
            };

            _activeAlerts[alertKey] = alert;

            AlertTriggered?.Invoke(this, new AlertEventArgs { Alert = alert });
        }

        /// <summary>
        /// Acknowledge an alert
        /// </summary>
        public void AcknowledgeAlert(string alertId, string acknowledgedBy, string notes = null)
        {
            var alert = _activeAlerts.Values.FirstOrDefault(a => a.AlertId == alertId);
            if (alert != null)
            {
                alert.Status = AlertStatus.Acknowledged;
                alert.AcknowledgedBy = acknowledgedBy;
                alert.AcknowledgedTime = DateTime.UtcNow;
                alert.Notes = notes;
            }
        }

        /// <summary>
        /// Resolve an alert
        /// </summary>
        public void ResolveAlert(string alertId, string resolvedBy, string resolution)
        {
            var alertKey = _activeAlerts.FirstOrDefault(kvp => kvp.Value.AlertId == alertId).Key;
            if (alertKey != null && _activeAlerts.TryRemove(alertKey, out var alert))
            {
                alert.Status = AlertStatus.Resolved;
                alert.ResolvedBy = resolvedBy;
                alert.ResolvedTime = DateTime.UtcNow;
                alert.Resolution = resolution;
            }
        }

        #endregion

        #region Twin Element Management

        /// <summary>
        /// Register a building element as part of the digital twin
        /// </summary>
        public TwinElement RegisterTwinElement(TwinElementRegistration registration)
        {
            var element = new TwinElement
            {
                ElementId = registration.ElementId,
                Name = registration.Name,
                Category = registration.Category,
                Type = registration.Type,
                Location = registration.Location,
                Floor = registration.Floor,
                Space = registration.Space,
                Properties = registration.Properties ?? new Dictionary<string, object>(),
                LinkedSensors = new List<string>(),
                State = TwinElementState.Normal,
                LastUpdated = DateTime.UtcNow
            };

            _twinElements[element.ElementId] = element;

            return element;
        }

        /// <summary>
        /// Link a sensor to a twin element
        /// </summary>
        public void LinkSensorToElement(string sensorId, string elementId)
        {
            if (_sensors.TryGetValue(sensorId, out var sensor) &&
                _twinElements.TryGetValue(elementId, out var element))
            {
                sensor.ElementId = elementId;
                if (!element.LinkedSensors.Contains(sensorId))
                    element.LinkedSensors.Add(sensorId);
            }
        }

        private void UpdateTwinElement(SensorDevice sensor, SensorReading reading)
        {
            if (string.IsNullOrEmpty(sensor.ElementId))
                return;

            if (_twinElements.TryGetValue(sensor.ElementId, out var element))
            {
                // Update element properties based on sensor type
                var propertyKey = $"sensor_{sensor.Type.ToString().ToLower()}";
                element.Properties[propertyKey] = reading.Value;
                element.Properties[$"{propertyKey}_timestamp"] = reading.Timestamp;
                element.LastUpdated = DateTime.UtcNow;

                // Determine element state
                var previousState = element.State;
                element.State = DetermineElementState(element);

                if (element.State != previousState)
                {
                    TwinStateChanged?.Invoke(this, new TwinEventArgs
                    {
                        Element = element,
                        PreviousState = previousState,
                        NewState = element.State
                    });
                }
            }
        }

        private TwinElementState DetermineElementState(TwinElement element)
        {
            // Check for any active alerts on linked sensors
            var hasActiveAlerts = element.LinkedSensors.Any(sensorId =>
                _activeAlerts.Values.Any(a => a.SensorId == sensorId && a.Status == AlertStatus.Active));

            if (hasActiveAlerts)
            {
                var criticalAlert = element.LinkedSensors.Any(sensorId =>
                    _activeAlerts.Values.Any(a => a.SensorId == sensorId &&
                        a.Status == AlertStatus.Active &&
                        a.Severity == AlertSeverity.Critical));

                return criticalAlert ? TwinElementState.Critical : TwinElementState.Warning;
            }

            return TwinElementState.Normal;
        }

        #endregion

        #region Real-Time Monitoring

        /// <summary>
        /// Start real-time monitoring of all sensors
        /// </summary>
        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await Task.Run(async () =>
            {
                while (!_monitoringCts.Token.IsCancellationRequested)
                {
                    // Check for stale sensors
                    foreach (var sensor in _sensors.Values)
                    {
                        var timeSinceReading = DateTime.UtcNow - sensor.LastReadingTime;
                        if (timeSinceReading > sensor.ReadingInterval * 3)
                        {
                            sensor.Status = SensorStatus.Offline;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), _monitoringCts.Token);
                }
            }, _monitoringCts.Token);
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _monitoringCts?.Cancel();
            _isMonitoring = false;
        }

        #endregion

        #region Scenario Simulation

        /// <summary>
        /// Create a what-if scenario for simulation
        /// </summary>
        public Scenario CreateScenario(ScenarioDefinition definition)
        {
            var scenario = new Scenario
            {
                ScenarioId = Guid.NewGuid().ToString(),
                Name = definition.Name,
                Description = definition.Description,
                Type = definition.Type,
                Parameters = definition.Parameters ?? new Dictionary<string, object>(),
                AffectedElements = definition.AffectedElements ?? new List<string>(),
                AffectedSensors = definition.AffectedSensors ?? new List<string>(),
                Status = ScenarioStatus.Created,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = definition.CreatedBy
            };

            lock (_lock)
            {
                _scenarios.Add(scenario);
            }

            return scenario;
        }

        /// <summary>
        /// Run a scenario simulation
        /// </summary>
        public async Task<ScenarioResult> RunScenarioAsync(string scenarioId)
        {
            var scenario = _scenarios.FirstOrDefault(s => s.ScenarioId == scenarioId);
            if (scenario == null)
                throw new KeyNotFoundException($"Scenario {scenarioId} not found");

            scenario.Status = ScenarioStatus.Running;
            scenario.StartTime = DateTime.UtcNow;

            var result = new ScenarioResult
            {
                ScenarioId = scenarioId,
                StartTime = DateTime.UtcNow,
                Impacts = new List<ScenarioImpact>(),
                Recommendations = new List<string>()
            };

            await Task.Run(() =>
            {
                // Simulate scenario based on type
                switch (scenario.Type)
                {
                    case ScenarioType.EquipmentFailure:
                        SimulateEquipmentFailure(scenario, result);
                        break;
                    case ScenarioType.OccupancyChange:
                        SimulateOccupancyChange(scenario, result);
                        break;
                    case ScenarioType.EnergyOptimization:
                        SimulateEnergyOptimization(scenario, result);
                        break;
                    case ScenarioType.EmergencyEvacuation:
                        SimulateEmergencyEvacuation(scenario, result);
                        break;
                    case ScenarioType.MaintenanceImpact:
                        SimulateMaintenanceImpact(scenario, result);
                        break;
                    case ScenarioType.ClimateChange:
                        SimulateClimateChange(scenario, result);
                        break;
                }
            });

            scenario.Status = ScenarioStatus.Completed;
            scenario.EndTime = DateTime.UtcNow;
            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            ScenarioCompleted?.Invoke(this, new ScenarioEventArgs { Scenario = scenario, Result = result });

            return result;
        }

        private void SimulateEquipmentFailure(Scenario scenario, ScenarioResult result)
        {
            var affectedZone = scenario.Parameters.TryGetValue("zone", out var zone) ? zone.ToString() : "Zone A";

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "HVAC",
                Description = $"AHU failure in {affectedZone}",
                Severity = ImpactSeverity.High,
                AffectedSpaces = 12,
                EstimatedRecoveryTime = TimeSpan.FromHours(4),
                CostImpact = 15000m
            });

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "Comfort",
                Description = "Temperature rise in affected areas",
                Severity = ImpactSeverity.Medium,
                AffectedSpaces = 12,
                MetricChange = "+5°C within 2 hours"
            });

            result.Recommendations.Add("Activate backup HVAC units for critical areas");
            result.Recommendations.Add("Increase ventilation in adjacent zones");
            result.Recommendations.Add("Notify occupants in affected areas");
            result.Recommendations.Add("Schedule emergency maintenance team");
        }

        private void SimulateOccupancyChange(Scenario scenario, ScenarioResult result)
        {
            var changePercent = scenario.Parameters.TryGetValue("change_percent", out var pct) ?
                Convert.ToDouble(pct) : 50.0;

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "Energy",
                Description = $"Energy consumption change with {changePercent}% occupancy shift",
                Severity = changePercent > 30 ? ImpactSeverity.Medium : ImpactSeverity.Low,
                MetricChange = $"{changePercent * 0.6:F1}% energy change"
            });

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "HVAC",
                Description = "HVAC load adjustment required",
                Severity = ImpactSeverity.Low,
                MetricChange = $"{changePercent * 0.7:F1}% cooling load change"
            });

            result.Recommendations.Add($"Adjust HVAC setpoints for {changePercent}% occupancy");
            result.Recommendations.Add("Update lighting schedules");
            result.Recommendations.Add("Review elevator dispatch algorithms");
        }

        private void SimulateEnergyOptimization(Scenario scenario, ScenarioResult result)
        {
            result.Impacts.Add(new ScenarioImpact
            {
                Category = "Energy",
                Description = "Potential energy savings identified",
                Severity = ImpactSeverity.Low,
                CostImpact = -25000m, // Savings
                MetricChange = "15-20% reduction achievable"
            });

            result.Recommendations.Add("Implement demand-controlled ventilation");
            result.Recommendations.Add("Optimize chiller sequencing");
            result.Recommendations.Add("Install occupancy sensors in low-traffic areas");
            result.Recommendations.Add("Adjust lighting schedules based on daylight availability");
            result.Recommendations.Add("Consider thermal mass pre-cooling during off-peak hours");
        }

        private void SimulateEmergencyEvacuation(Scenario scenario, ScenarioResult result)
        {
            var occupantCount = scenario.Parameters.TryGetValue("occupants", out var occ) ?
                Convert.ToInt32(occ) : 500;

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "Safety",
                Description = $"Evacuation of {occupantCount} occupants",
                Severity = ImpactSeverity.Critical,
                AffectedSpaces = _twinElements.Count,
                EstimatedRecoveryTime = TimeSpan.FromMinutes(15)
            });

            result.Recommendations.Add("Activate all emergency lighting");
            result.Recommendations.Add("Open all emergency exits");
            result.Recommendations.Add("Recall all elevators to ground floor");
            result.Recommendations.Add("Activate public address system");
            result.Recommendations.Add("Deploy floor wardens to assembly points");
        }

        private void SimulateMaintenanceImpact(Scenario scenario, ScenarioResult result)
        {
            var systemType = scenario.Parameters.TryGetValue("system", out var sys) ?
                sys.ToString() : "HVAC";

            result.Impacts.Add(new ScenarioImpact
            {
                Category = systemType,
                Description = $"Planned {systemType} maintenance downtime",
                Severity = ImpactSeverity.Medium,
                EstimatedRecoveryTime = TimeSpan.FromHours(8),
                CostImpact = 5000m
            });

            result.Recommendations.Add($"Schedule {systemType} maintenance during low-occupancy period");
            result.Recommendations.Add("Prepare backup systems");
            result.Recommendations.Add("Notify affected building users 48 hours in advance");
            result.Recommendations.Add("Stage replacement parts before maintenance window");
        }

        private void SimulateClimateChange(Scenario scenario, ScenarioResult result)
        {
            var tempIncrease = scenario.Parameters.TryGetValue("temp_increase", out var temp) ?
                Convert.ToDouble(temp) : 2.0;

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "Energy",
                Description = $"Cooling load increase with {tempIncrease}°C ambient rise",
                Severity = ImpactSeverity.High,
                MetricChange = $"{tempIncrease * 8:F0}% cooling energy increase"
            });

            result.Impacts.Add(new ScenarioImpact
            {
                Category = "Equipment",
                Description = "HVAC equipment stress",
                Severity = ImpactSeverity.Medium,
                MetricChange = $"15% reduction in equipment lifespan"
            });

            result.Recommendations.Add("Evaluate chiller capacity upgrade");
            result.Recommendations.Add("Consider passive cooling strategies");
            result.Recommendations.Add("Improve building envelope thermal performance");
            result.Recommendations.Add("Install solar shading devices");
        }

        #endregion

        #region Analytics & Reporting

        /// <summary>
        /// Get real-time dashboard data
        /// </summary>
        public DigitalTwinDashboard GetDashboard()
        {
            return new DigitalTwinDashboard
            {
                GeneratedAt = DateTime.UtcNow,
                TotalSensors = _sensors.Count,
                OnlineSensors = _sensors.Values.Count(s => s.Status == SensorStatus.Online),
                OfflineSensors = _sensors.Values.Count(s => s.Status == SensorStatus.Offline),
                TotalElements = _twinElements.Count,
                ActiveAlerts = _activeAlerts.Values.Count(a => a.Status == AlertStatus.Active),
                CriticalAlerts = _activeAlerts.Values.Count(a =>
                    a.Status == AlertStatus.Active && a.Severity == AlertSeverity.Critical),
                SensorsByType = _sensors.Values.GroupBy(s => s.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ElementsByState = _twinElements.Values.GroupBy(e => e.State)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentAlerts = _activeAlerts.Values
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .ToList(),
                EnvironmentalSummary = GetEnvironmentalSummary()
            };
        }

        private EnvironmentalSummary GetEnvironmentalSummary()
        {
            var tempSensors = _sensors.Values.Where(s => s.Type == SensorType.Temperature).ToList();
            var humiditySensors = _sensors.Values.Where(s => s.Type == SensorType.Humidity).ToList();
            var co2Sensors = _sensors.Values.Where(s => s.Type == SensorType.CO2).ToList();

            return new EnvironmentalSummary
            {
                AverageTemperature = tempSensors.Any() ?
                    tempSensors.Average(s => s.LastReading?.Value ?? 22.0) : 22.0,
                AverageHumidity = humiditySensors.Any() ?
                    humiditySensors.Average(s => s.LastReading?.Value ?? 50.0) : 50.0,
                AverageCO2 = co2Sensors.Any() ?
                    co2Sensors.Average(s => s.LastReading?.Value ?? 400.0) : 400.0,
                TotalOccupancy = _sensors.Values
                    .Where(s => s.Type == SensorType.Occupancy)
                    .Sum(s => (int)(s.LastReading?.Value ?? 0)),
                TotalEnergyToday = _sensors.Values
                    .Where(s => s.Type == SensorType.Energy)
                    .Sum(s => s.LastReading?.Value ?? 0)
            };
        }

        /// <summary>
        /// Get sensor history for analysis
        /// </summary>
        public List<SensorReading> GetSensorHistory(string sensorId, DateTime? from = null, DateTime? to = null)
        {
            if (!_readingHistory.TryGetValue(sensorId, out var history))
                return new List<SensorReading>();

            var query = history.AsEnumerable();
            if (from.HasValue)
                query = query.Where(r => r.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(r => r.Timestamp <= to.Value);

            return query.ToList();
        }

        /// <summary>
        /// Get space analytics
        /// </summary>
        public SpaceAnalytics GetSpaceAnalytics(string spaceId)
        {
            var spaceSensors = _sensors.Values.Where(s => s.SpaceId == spaceId).ToList();

            return new SpaceAnalytics
            {
                SpaceId = spaceId,
                AnalyzedAt = DateTime.UtcNow,
                SensorCount = spaceSensors.Count,
                CurrentTemperature = spaceSensors.FirstOrDefault(s => s.Type == SensorType.Temperature)?.LastReading?.Value,
                CurrentHumidity = spaceSensors.FirstOrDefault(s => s.Type == SensorType.Humidity)?.LastReading?.Value,
                CurrentCO2 = spaceSensors.FirstOrDefault(s => s.Type == SensorType.CO2)?.LastReading?.Value,
                CurrentOccupancy = (int)(spaceSensors.FirstOrDefault(s => s.Type == SensorType.Occupancy)?.LastReading?.Value ?? 0),
                ActiveAlerts = _activeAlerts.Values.Where(a => a.SpaceId == spaceId && a.Status == AlertStatus.Active).ToList(),
                ComfortScore = CalculateComfortScore(spaceSensors)
            };
        }

        private double CalculateComfortScore(List<SensorDevice> sensors)
        {
            double score = 100;

            var temp = sensors.FirstOrDefault(s => s.Type == SensorType.Temperature)?.LastReading?.Value;
            if (temp.HasValue)
            {
                // Optimal: 21-24°C
                if (temp < 18 || temp > 28) score -= 30;
                else if (temp < 20 || temp > 25) score -= 15;
            }

            var humidity = sensors.FirstOrDefault(s => s.Type == SensorType.Humidity)?.LastReading?.Value;
            if (humidity.HasValue)
            {
                // Optimal: 40-60%
                if (humidity < 30 || humidity > 70) score -= 20;
                else if (humidity < 40 || humidity > 60) score -= 10;
            }

            var co2 = sensors.FirstOrDefault(s => s.Type == SensorType.CO2)?.LastReading?.Value;
            if (co2.HasValue)
            {
                // Optimal: <800ppm
                if (co2 > 1500) score -= 30;
                else if (co2 > 1000) score -= 15;
                else if (co2 > 800) score -= 5;
            }

            return Math.Max(0, score);
        }

        #endregion
    }

    #region Data Models

    public class SensorDevice
    {
        public string SensorId { get; set; }
        public string Name { get; set; }
        public SensorType Type { get; set; }
        public string Location { get; set; }
        public string ElementId { get; set; }
        public string SpaceId { get; set; }
        public string Floor { get; set; }
        public string Zone { get; set; }
        public string Unit { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public TimeSpan ReadingInterval { get; set; }
        public SensorStatus Status { get; set; }
        public DateTime RegisteredDate { get; set; }
        public DateTime LastReadingTime { get; set; }
        public SensorReading LastReading { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class SensorRegistration
    {
        public string SensorId { get; set; }
        public string Name { get; set; }
        public SensorType Type { get; set; }
        public string Location { get; set; }
        public string ElementId { get; set; }
        public string SpaceId { get; set; }
        public string Floor { get; set; }
        public string Zone { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public TimeSpan? ReadingInterval { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class SensorReading
    {
        public string ReadingId { get; set; }
        public string SensorId { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
        public ReadingQuality Quality { get; set; }
    }

    public class ThresholdRule
    {
        public string RuleId { get; set; }
        public SensorType SensorType { get; set; }
        public ThresholdCondition Condition { get; set; }
        public double Value { get; set; }
        public AlertSeverity AlertSeverity { get; set; }
        public string Message { get; set; }
    }

    public class Alert
    {
        public string AlertId { get; set; }
        public string SensorId { get; set; }
        public string RuleId { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public string Location { get; set; }
        public string SpaceId { get; set; }
        public string ElementId { get; set; }
        public DateTime Timestamp { get; set; }
        public AlertStatus Status { get; set; }
        public string AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedTime { get; set; }
        public string ResolvedBy { get; set; }
        public DateTime? ResolvedTime { get; set; }
        public string Resolution { get; set; }
        public string Notes { get; set; }
    }

    public class TwinElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public string Floor { get; set; }
        public string Space { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public List<string> LinkedSensors { get; set; }
        public TwinElementState State { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TwinElementRegistration
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public string Floor { get; set; }
        public string Space { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class Scenario
    {
        public string ScenarioId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ScenarioType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public List<string> AffectedElements { get; set; }
        public List<string> AffectedSensors { get; set; }
        public ScenarioStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class ScenarioDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ScenarioType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public List<string> AffectedElements { get; set; }
        public List<string> AffectedSensors { get; set; }
        public string CreatedBy { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public List<ScenarioImpact> Impacts { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ScenarioImpact
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public ImpactSeverity Severity { get; set; }
        public int AffectedSpaces { get; set; }
        public TimeSpan? EstimatedRecoveryTime { get; set; }
        public decimal? CostImpact { get; set; }
        public string MetricChange { get; set; }
    }

    public class DigitalTwinDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalSensors { get; set; }
        public int OnlineSensors { get; set; }
        public int OfflineSensors { get; set; }
        public int TotalElements { get; set; }
        public int ActiveAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public Dictionary<SensorType, int> SensorsByType { get; set; }
        public Dictionary<TwinElementState, int> ElementsByState { get; set; }
        public List<Alert> RecentAlerts { get; set; }
        public EnvironmentalSummary EnvironmentalSummary { get; set; }
    }

    public class EnvironmentalSummary
    {
        public double AverageTemperature { get; set; }
        public double AverageHumidity { get; set; }
        public double AverageCO2 { get; set; }
        public int TotalOccupancy { get; set; }
        public double TotalEnergyToday { get; set; }
    }

    public class SpaceAnalytics
    {
        public string SpaceId { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public int SensorCount { get; set; }
        public double? CurrentTemperature { get; set; }
        public double? CurrentHumidity { get; set; }
        public double? CurrentCO2 { get; set; }
        public int CurrentOccupancy { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
        public double ComfortScore { get; set; }
    }

    #endregion

    #region Event Args

    public class SensorEventArgs : EventArgs
    {
        public SensorDevice Sensor { get; set; }
        public SensorReading Reading { get; set; }
    }

    public class AlertEventArgs : EventArgs
    {
        public Alert Alert { get; set; }
    }

    public class TwinEventArgs : EventArgs
    {
        public TwinElement Element { get; set; }
        public TwinElementState PreviousState { get; set; }
        public TwinElementState NewState { get; set; }
    }

    public class ScenarioEventArgs : EventArgs
    {
        public Scenario Scenario { get; set; }
        public ScenarioResult Result { get; set; }
    }

    #endregion

    #region Enums

    public enum SensorType
    {
        Temperature,
        Humidity,
        CO2,
        Pressure,
        Light,
        Occupancy,
        Motion,
        Energy,
        Water,
        Gas,
        Noise,
        AirQuality,
        WaterLeak,
        DoorContact,
        Vibration,
        Smoke,
        Fire
    }

    public enum SensorStatus
    {
        Online,
        Offline,
        Maintenance,
        Error
    }

    public enum ReadingQuality
    {
        Good,
        Uncertain,
        OutOfRange,
        Error
    }

    public enum ThresholdCondition
    {
        GreaterThan,
        LessThan,
        Equals,
        GreaterThanPercent,
        LessThanPercent
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Resolved
    }

    public enum TwinElementState
    {
        Normal,
        Warning,
        Critical,
        Maintenance,
        Offline
    }

    public enum ScenarioType
    {
        EquipmentFailure,
        OccupancyChange,
        EnergyOptimization,
        EmergencyEvacuation,
        MaintenanceImpact,
        ClimateChange,
        SecurityBreach,
        FireEmergency
    }

    public enum ScenarioStatus
    {
        Created,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public enum ImpactSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
