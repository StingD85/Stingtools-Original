// StingBIM.AI.IoT.Engine.DigitalTwinEngine
// Digital Twin synchronization engine for Revit model state management,
// sensor data overlay, predictive maintenance, what-if simulation,
// indoor environmental quality analysis, and energy/water tracking.
// Implements ISO 16484 (BAS), ISO 50001 (Energy), and ASHRAE 55 (Thermal Comfort).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.IoT.Models;

namespace StingBIM.AI.IoT.Engine
{
    /// <summary>
    /// Core Digital Twin engine that maintains a synchronized virtual representation
    /// of the physical building. Integrates real-time sensor data with the Revit model,
    /// provides predictive maintenance, scenario simulation, health scoring, and
    /// indoor environmental quality assessment.
    /// </summary>
    public class DigitalTwinEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Dependencies
        private readonly SensorIntegrationEngine _sensorEngine;

        // Digital twin state
        private DigitalTwinState _currentState;
        private readonly ConcurrentDictionary<string, List<SensorReading>> _elementSensorMap =
            new ConcurrentDictionary<string, List<SensorReading>>(StringComparer.OrdinalIgnoreCase);

        // Health baselines per sensor type (expected operating ranges)
        private readonly Dictionary<SensorCategory, (double IdealMin, double IdealMax)> _healthBaselines =
            new Dictionary<SensorCategory, (double, double)>();

        // Maintenance prediction models: sensor ID -> trend data
        private readonly ConcurrentDictionary<string, TrendAnalysis> _trendData =
            new ConcurrentDictionary<string, TrendAnalysis>(StringComparer.OrdinalIgnoreCase);

        // Zone definitions for IEQ calculations
        private readonly Dictionary<string, ZoneDefinition> _zones =
            new Dictionary<string, ZoneDefinition>(StringComparer.OrdinalIgnoreCase);

        // Energy tracking accumulators
        private readonly ConcurrentDictionary<string, List<EnergyMeterReading>> _energyHistory =
            new ConcurrentDictionary<string, List<EnergyMeterReading>>(StringComparer.OrdinalIgnoreCase);

        // Water tracking accumulators
        private readonly ConcurrentDictionary<string, List<WaterMeterReading>> _waterHistory =
            new ConcurrentDictionary<string, List<WaterMeterReading>>(StringComparer.OrdinalIgnoreCase);

        // ASHRAE 55 thermal comfort parameters
        private const double ThermalComfortMinTemp = 20.0;  // degC
        private const double ThermalComfortMaxTemp = 26.0;  // degC
        private const double ThermalComfortMinRH = 30.0;    // %
        private const double ThermalComfortMaxRH = 60.0;    // %
        private const double Co2AcceptableLimit = 1000.0;   // ppm per ASHRAE 62.1
        private const double LightMinOffice = 300.0;        // lux per EN 12464-1
        private const double LightMaxOffice = 500.0;        // lux
        private const double AcousticMaxOffice = 45.0;      // dB(A) per ASHRAE Handbook

        /// <summary>
        /// Initializes the DigitalTwinEngine with a reference to the sensor integration engine.
        /// </summary>
        /// <param name="sensorEngine">The sensor engine providing real-time data feeds.</param>
        public DigitalTwinEngine(SensorIntegrationEngine sensorEngine)
        {
            _sensorEngine = sensorEngine ?? throw new ArgumentNullException(nameof(sensorEngine));
            _currentState = new DigitalTwinState
            {
                ModelVersion = "7.0.0",
                LastSyncTime = DateTime.UtcNow
            };

            InitializeHealthBaselines();
            Logger.Info("DigitalTwinEngine initialized with model version {Version}", _currentState.ModelVersion);
        }

        /// <summary>
        /// Sets up ideal operating ranges for each sensor type, used for health scoring.
        /// Based on ASHRAE standards and equipment manufacturer specifications.
        /// </summary>
        private void InitializeHealthBaselines()
        {
            _healthBaselines[SensorCategory.Temperature] = (ThermalComfortMinTemp, ThermalComfortMaxTemp);
            _healthBaselines[SensorCategory.Humidity] = (ThermalComfortMinRH, ThermalComfortMaxRH);
            _healthBaselines[SensorCategory.CO2] = (350.0, Co2AcceptableLimit);
            _healthBaselines[SensorCategory.Light] = (LightMinOffice, LightMaxOffice);
            _healthBaselines[SensorCategory.Pressure] = (-25.0, 250.0);     // Pa, duct static
            _healthBaselines[SensorCategory.Vibration] = (0.0, 4.5);        // mm/s RMS (ISO 10816 Zone A/B)
            _healthBaselines[SensorCategory.Acoustic] = (25.0, AcousticMaxOffice);
            _healthBaselines[SensorCategory.Power] = (0.0, double.MaxValue); // kW, no upper ideal
            _healthBaselines[SensorCategory.Water] = (0.0, double.MaxValue); // L/min, no upper ideal
        }

        #region Zone Management

        /// <summary>
        /// Registers a building zone for IEQ and occupancy analysis.
        /// </summary>
        public void RegisterZone(ZoneDefinition zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            lock (_lockObject)
            {
                _zones[zone.ZoneId] = zone;
            }
            Logger.Info("Registered zone {ZoneId} ({ZoneName}) with {SensorCount} sensors, area={Area}m2",
                zone.ZoneId, zone.ZoneName, zone.SensorIds.Count, zone.AreaSqM);
        }

        #endregion

        #region Model Synchronization

        /// <summary>
        /// Synchronizes the digital twin model state with current sensor data.
        /// Performs a complete scan of all registered sensors, updates the twin state,
        /// recalculates health scores, and identifies any state changes since last sync.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for cooperative shutdown.</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
        /// <returns>Summary of the synchronization result.</returns>
        public async Task<SyncResult> SynchronizeModelAsync(
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            Logger.Info("Starting digital twin model synchronization...");
            var syncResult = new SyncResult { StartTime = DateTime.UtcNow };

            var sensors = _sensorEngine.GetRegisteredSensors();
            if (sensors.Count == 0)
            {
                Logger.Warn("No sensors registered. Synchronization skipped.");
                syncResult.Status = "NoSensors";
                return syncResult;
            }

            int processed = 0;

            // Phase 1: Collect latest readings for all sensors
            var newSensorStates = new Dictionary<string, SensorReading>(StringComparer.OrdinalIgnoreCase);
            var elementReadings = new Dictionary<string, List<SensorReading>>(StringComparer.OrdinalIgnoreCase);

            foreach (var sensor in sensors)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var reading = _sensorEngine.GetLatestReading(sensor.Id);
                if (reading != null)
                {
                    newSensorStates[sensor.Id] = reading;
                    syncResult.ReadingsProcessed++;

                    // Group by Revit element for element-level analysis
                    if (!string.IsNullOrWhiteSpace(sensor.ElementId))
                    {
                        if (!elementReadings.ContainsKey(sensor.ElementId))
                            elementReadings[sensor.ElementId] = new List<SensorReading>();
                        elementReadings[sensor.ElementId].Add(reading);
                    }

                    // Detect state changes since last sync
                    if (_currentState.SensorStates.TryGetValue(sensor.Id, out var previousReading))
                    {
                        if (Math.Abs(reading.Value - previousReading.Value) > 0.01)
                            syncResult.StateChanges++;
                    }
                    else
                    {
                        syncResult.NewSensors++;
                    }
                }
                else
                {
                    syncResult.MissingSensors++;
                }

                processed++;
                progress?.Report((double)processed / sensors.Count * 0.5); // First half
            }

            // Phase 2: Calculate element health scores
            var elementHealthScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in elementReadings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double healthScore = await Task.Run(() =>
                    CalculateElementHealthFromReadings(kvp.Value), cancellationToken).ConfigureAwait(false);
                elementHealthScores[kvp.Key] = healthScore;
            }

            progress?.Report(0.75);

            // Phase 3: Calculate overall building health score
            double buildingHealth = elementHealthScores.Count > 0
                ? elementHealthScores.Values.Average()
                : 100.0;

            // Factor in data quality
            int totalReadings = newSensorStates.Count;
            int goodReadings = newSensorStates.Values.Count(r => r.Quality == SensorDataQuality.Good);
            double dataQualityFactor = totalReadings > 0 ? (double)goodReadings / totalReadings : 1.0;
            buildingHealth *= (0.7 + 0.3 * dataQualityFactor); // 30% weight on data quality

            // Phase 4: Update twin state atomically
            lock (_lockObject)
            {
                _currentState = new DigitalTwinState
                {
                    ModelVersion = _currentState.ModelVersion,
                    LastSyncTime = DateTime.UtcNow,
                    SensorStates = newSensorStates,
                    HealthScore = Math.Round(buildingHealth, 1),
                    ElementHealthScores = elementHealthScores,
                    ActiveAlerts = _sensorEngine.GetActiveAlerts().ToList()
                };
            }

            // Update element sensor map for overlay rendering
            foreach (var kvp in elementReadings)
            {
                _elementSensorMap[kvp.Key] = kvp.Value;
            }

            syncResult.EndTime = DateTime.UtcNow;
            syncResult.BuildingHealthScore = buildingHealth;
            syncResult.Status = "Complete";

            progress?.Report(1.0);

            Logger.Info("Synchronization complete: {Readings} readings, {Changes} state changes, " +
                        "health={Health:F1}%, duration={Duration}ms",
                syncResult.ReadingsProcessed, syncResult.StateChanges,
                buildingHealth, syncResult.Duration.TotalMilliseconds);

            return syncResult;
        }

        /// <summary>
        /// Calculates element health score (0-100) from its associated sensor readings.
        /// Compares each reading against ideal baselines for its sensor type.
        /// </summary>
        private double CalculateElementHealthFromReadings(List<SensorReading> readings)
        {
            if (readings == null || readings.Count == 0) return 100.0;

            double totalScore = 0;
            int scoredCount = 0;

            foreach (var reading in readings)
            {
                var sensorDef = _sensorEngine.GetSensor(reading.SensorId);
                if (sensorDef == null) continue;

                double score = 100.0;

                // Penalize for bad data quality
                switch (reading.Quality)
                {
                    case SensorDataQuality.Bad: score -= 40; break;
                    case SensorDataQuality.Stale: score -= 20; break;
                    case SensorDataQuality.Uncertain: score -= 10; break;
                }

                // Check against health baselines
                if (_healthBaselines.TryGetValue(sensorDef.Type, out var baseline))
                {
                    if (reading.Value < baseline.IdealMin)
                    {
                        double deviation = (baseline.IdealMin - reading.Value) / Math.Max(baseline.IdealMin, 1.0);
                        score -= Math.Min(50, deviation * 100);
                    }
                    else if (reading.Value > baseline.IdealMax && baseline.IdealMax < double.MaxValue)
                    {
                        double deviation = (reading.Value - baseline.IdealMax) / Math.Max(baseline.IdealMax, 1.0);
                        score -= Math.Min(50, deviation * 100);
                    }
                }

                // Check calibration currency
                if (!sensorDef.IsCalibrationCurrent())
                    score -= 5;

                totalScore += Math.Max(0, Math.Min(100, score));
                scoredCount++;
            }

            return scoredCount > 0 ? totalScore / scoredCount : 100.0;
        }

        #endregion

        #region Twin State Access

        /// <summary>
        /// Returns the current digital twin snapshot (thread-safe copy).
        /// </summary>
        public DigitalTwinState GetTwinState()
        {
            lock (_lockObject)
            {
                return _currentState;
            }
        }

        /// <summary>
        /// Calculates a health score for a specific Revit element based on sensor data.
        /// Health reflects operating condition vs. baselines, data quality, and trend direction.
        /// </summary>
        /// <param name="elementId">Revit element UniqueId.</param>
        /// <returns>Health score 0.0 (failed) to 100.0 (perfect).</returns>
        public double CalculateHealthScore(string elementId)
        {
            lock (_lockObject)
            {
                if (_currentState.ElementHealthScores.TryGetValue(elementId, out var score))
                    return score;
            }

            // If not in the cached state, calculate on the fly
            if (_elementSensorMap.TryGetValue(elementId, out var readings))
            {
                return CalculateElementHealthFromReadings(readings);
            }

            return 100.0; // No data means no degradation detected
        }

        #endregion

        #region Sensor Data Overlay

        /// <summary>
        /// Creates color-coded sensor data overlay parameters for a Revit view.
        /// Maps each sensor reading to a color gradient (blue=cold/low, green=good, red=hot/high)
        /// and returns element-color assignments for Revit view filter application.
        /// </summary>
        /// <param name="viewId">Revit view UniqueId to overlay.</param>
        /// <returns>Dictionary mapping element UniqueIds to overlay color and label data.</returns>
        public Dictionary<string, SensorOverlay> OverlaySensorDataOnView(string viewId)
        {
            var overlays = new Dictionary<string, SensorOverlay>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _elementSensorMap)
            {
                string elementId = kvp.Key;
                var readings = kvp.Value;

                if (readings == null || readings.Count == 0) continue;

                // Use the primary sensor reading (first one, or temperature if available)
                var primaryReading = readings.FirstOrDefault(r =>
                {
                    var def = _sensorEngine.GetSensor(r.SensorId);
                    return def?.Type == SensorCategory.Temperature;
                }) ?? readings[0];

                var sensorDef = _sensorEngine.GetSensor(primaryReading.SensorId);
                if (sensorDef == null) continue;

                // Calculate normalized value (0.0 = min, 1.0 = max)
                double normalizedValue = 0.5;
                if (sensorDef.MaxRange > sensorDef.MinRange)
                {
                    normalizedValue = (primaryReading.Value - sensorDef.MinRange) /
                                      (sensorDef.MaxRange - sensorDef.MinRange);
                    normalizedValue = Math.Max(0.0, Math.Min(1.0, normalizedValue));
                }

                // Map to color: blue (0.0) -> green (0.5) -> red (1.0)
                var color = CalculateHeatmapColor(normalizedValue);

                // Calculate health-based color for quality overlay
                double health = CalculateHealthScore(elementId);
                var healthColor = CalculateHealthColor(health);

                overlays[elementId] = new SensorOverlay
                {
                    ElementId = elementId,
                    PrimaryValue = primaryReading.Value,
                    PrimaryUnit = primaryReading.Unit,
                    PrimarySensorType = sensorDef.Type,
                    NormalizedValue = normalizedValue,
                    HeatmapColorR = color.R,
                    HeatmapColorG = color.G,
                    HeatmapColorB = color.B,
                    HealthScore = health,
                    HealthColorR = healthColor.R,
                    HealthColorG = healthColor.G,
                    HealthColorB = healthColor.B,
                    Label = $"{primaryReading.Value:F1} {primaryReading.Unit}",
                    Quality = primaryReading.Quality,
                    Timestamp = primaryReading.Timestamp,
                    AllReadings = readings.Select(r => new ReadingSummary
                    {
                        SensorId = r.SensorId,
                        Value = r.Value,
                        Unit = r.Unit,
                        Quality = r.Quality
                    }).ToList()
                };
            }

            Logger.Info("Generated sensor overlay for view {ViewId}: {Count} elements mapped",
                viewId, overlays.Count);
            return overlays;
        }

        /// <summary>
        /// Maps a normalized value (0-1) to a blue-green-red heatmap color.
        /// </summary>
        private (byte R, byte G, byte B) CalculateHeatmapColor(double normalized)
        {
            byte r, g, b;
            if (normalized < 0.5)
            {
                // Blue to Green
                double t = normalized * 2.0;
                r = 0;
                g = (byte)(255 * t);
                b = (byte)(255 * (1.0 - t));
            }
            else
            {
                // Green to Red
                double t = (normalized - 0.5) * 2.0;
                r = (byte)(255 * t);
                g = (byte)(255 * (1.0 - t));
                b = 0;
            }
            return (r, g, b);
        }

        /// <summary>
        /// Maps health score (0-100) to a red-yellow-green color.
        /// </summary>
        private (byte R, byte G, byte B) CalculateHealthColor(double health)
        {
            double normalized = health / 100.0;
            byte r, g;
            if (normalized < 0.5)
            {
                r = 255;
                g = (byte)(255 * normalized * 2.0);
            }
            else
            {
                r = (byte)(255 * (1.0 - normalized) * 2.0);
                g = 255;
            }
            return (r, g, 0);
        }

        #endregion

        #region Predictive Maintenance

        /// <summary>
        /// Predicts maintenance needs for a Revit element using linear regression
        /// on historical sensor trends. Identifies degradation patterns and estimates
        /// time-to-failure based on the trend trajectory.
        /// </summary>
        /// <param name="elementId">Revit element UniqueId.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Maintenance prediction with confidence and recommended actions.</returns>
        public async Task<MaintenancePrediction> PredictMaintenanceAsync(
            string elementId,
            CancellationToken cancellationToken = default)
        {
            var prediction = new MaintenancePrediction { ElementId = elementId };

            // Get all sensors associated with this element
            var sensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.ElementId.Equals(elementId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sensors.Count == 0)
            {
                prediction.Status = "NoSensors";
                prediction.Confidence = 0;
                return prediction;
            }

            await Task.Run(() =>
            {
                double worstHealth = 100.0;
                double? earliestFailureDays = null;

                foreach (var sensor in sensors)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Get 30 days of historical data
                    var history = _sensorEngine.GetHistoricalReadings(
                        sensor.Id,
                        DateTime.UtcNow.AddDays(-30),
                        DateTime.UtcNow);

                    if (history.Count < 20) continue;

                    // Perform linear regression on the time series
                    var regression = CalculateLinearRegression(history);

                    // Store trend data
                    _trendData[sensor.Id] = new TrendAnalysis
                    {
                        SensorId = sensor.Id,
                        Slope = regression.Slope,
                        Intercept = regression.Intercept,
                        RSquared = regression.RSquared,
                        MeanValue = regression.MeanY,
                        StdDeviation = regression.StdDevY,
                        TrendDirection = regression.Slope > 0.001 ? "Increasing" :
                                         regression.Slope < -0.001 ? "Decreasing" : "Stable"
                    };

                    // Check for degradation patterns
                    if (_healthBaselines.TryGetValue(sensor.Type, out var baseline))
                    {
                        // Project when value will exit the acceptable range
                        double currentValue = history[history.Count - 1].Value;
                        double dailyChange = regression.Slope * 86400; // slope per second * seconds per day

                        if (Math.Abs(dailyChange) > 0.001)
                        {
                            double? daysToLimit = null;
                            if (dailyChange > 0 && baseline.IdealMax < double.MaxValue)
                            {
                                daysToLimit = (baseline.IdealMax - currentValue) / dailyChange;
                            }
                            else if (dailyChange < 0)
                            {
                                daysToLimit = (baseline.IdealMin - currentValue) / dailyChange;
                            }

                            if (daysToLimit.HasValue && daysToLimit.Value > 0)
                            {
                                if (!earliestFailureDays.HasValue || daysToLimit.Value < earliestFailureDays.Value)
                                    earliestFailureDays = daysToLimit.Value;
                            }
                        }

                        // Current health contribution
                        double sensorHealth = 100.0;
                        if (currentValue < baseline.IdealMin || currentValue > baseline.IdealMax)
                            sensorHealth = 60.0;
                        if (worstHealth > sensorHealth)
                            worstHealth = sensorHealth;
                    }

                    // Check vibration-specific patterns (bearing degradation)
                    if (sensor.Type == SensorCategory.Vibration && regression.Slope > 0.01)
                    {
                        prediction.Recommendations.Add(
                            $"Vibration increasing on {sensor.Name}: {regression.Slope:F4} mm/s per day. " +
                            "Inspect bearings and alignment.");
                    }

                    // Check temperature drift (heat exchanger fouling)
                    if (sensor.Type == SensorCategory.Temperature && Math.Abs(regression.Slope) > 0.005)
                    {
                        prediction.Recommendations.Add(
                            $"Temperature drift detected on {sensor.Name}: {regression.Slope * 86400:F2} degC/day. " +
                            "Check for fouling or reduced flow.");
                    }
                }

                prediction.CurrentHealthScore = worstHealth;
                prediction.EstimatedDaysToFailure = earliestFailureDays.HasValue
                    ? (int)Math.Ceiling(earliestFailureDays.Value) : (int?)null;
                prediction.Confidence = sensors.Count > 3 ? 0.85 : sensors.Count > 1 ? 0.65 : 0.4;
                prediction.Status = earliestFailureDays.HasValue && earliestFailureDays.Value < 30
                    ? "MaintenanceRecommended"
                    : worstHealth < 80 ? "MonitorClosely" : "Healthy";

                // Add general recommendations based on health
                if (prediction.CurrentHealthScore < 50)
                    prediction.Recommendations.Insert(0, "URGENT: Equipment health below 50%. Schedule immediate inspection.");
                else if (prediction.CurrentHealthScore < 75)
                    prediction.Recommendations.Insert(0, "Schedule preventive maintenance within 2 weeks.");

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Maintenance prediction for element {ElementId}: health={Health:F1}%, " +
                        "daysToFailure={Days}, status={Status}",
                elementId, prediction.CurrentHealthScore,
                prediction.EstimatedDaysToFailure, prediction.Status);

            return prediction;
        }

        /// <summary>
        /// Performs linear regression (least squares) on a time series of sensor readings.
        /// X-axis is seconds since the first reading, Y-axis is the sensor value.
        /// </summary>
        private RegressionResult CalculateLinearRegression(IReadOnlyList<SensorReading> readings)
        {
            int n = readings.Count;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            DateTime t0 = readings[0].Timestamp;

            for (int i = 0; i < n; i++)
            {
                double x = (readings[i].Timestamp - t0).TotalSeconds;
                double y = readings[i].Value;
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                sumY2 += y * y;
            }

            double meanX = sumX / n;
            double meanY = sumY / n;
            double denominator = sumX2 - sumX * sumX / n;

            double slope = denominator != 0 ? (sumXY - sumX * sumY / n) / denominator : 0;
            double intercept = meanY - slope * meanX;

            // R-squared goodness of fit
            double ssTot = sumY2 - sumY * sumY / n;
            double ssRes = 0;
            for (int i = 0; i < n; i++)
            {
                double x = (readings[i].Timestamp - t0).TotalSeconds;
                double predicted = slope * x + intercept;
                double residual = readings[i].Value - predicted;
                ssRes += residual * residual;
            }
            double rSquared = ssTot > 0 ? 1.0 - ssRes / ssTot : 0;

            // Standard deviation of Y
            double varY = 0;
            for (int i = 0; i < n; i++)
            {
                double diff = readings[i].Value - meanY;
                varY += diff * diff;
            }
            double stdDevY = Math.Sqrt(varY / n);

            return new RegressionResult
            {
                Slope = slope,
                Intercept = intercept,
                RSquared = rSquared,
                MeanY = meanY,
                StdDevY = stdDevY
            };
        }

        #endregion

        #region Scenario Simulation

        /// <summary>
        /// Runs a what-if simulation by adjusting setpoints, occupancy, or outdoor conditions.
        /// Estimates energy impact, comfort score, and peak demand changes using a simplified
        /// building energy balance model.
        /// </summary>
        /// <param name="scenario">Scenario parameters (setpoint changes, occupancy, weather).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Simulation result with predicted energy, cost, and comfort impacts.</returns>
        public async Task<ScenarioResult> SimulateScenarioAsync(
            ScenarioParams scenario,
            CancellationToken cancellationToken = default)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            Logger.Info("Starting scenario simulation: {ScenarioName}", scenario.ScenarioName);
            var result = new ScenarioResult { ScenarioName = scenario.ScenarioName };

            await Task.Run(() =>
            {
                // Build baseline from current sensor data
                double baselineTemp = GetAverageReading(SensorCategory.Temperature) ?? 22.0;
                double baselineHumidity = GetAverageReading(SensorCategory.Humidity) ?? 50.0;
                double baselinePowerKW = GetAverageReading(SensorCategory.Power) ?? 100.0;
                double baselineOccupancy = GetAverageReading(SensorCategory.Occupancy) ?? 50.0;
                double outdoorTemp = scenario.OutdoorTempOverride ?? 30.0;

                // Apply setpoint changes
                double newSetpoint = baselineTemp;
                if (scenario.SetpointChanges.TryGetValue("CoolingSetpoint", out double coolingDelta))
                    newSetpoint += coolingDelta;
                if (scenario.SetpointChanges.TryGetValue("HeatingSetpoint", out double heatingDelta))
                    newSetpoint += heatingDelta;

                // Occupancy impact (more people = more internal heat gain)
                double occupancyFactor = scenario.OccupancyOverride.HasValue
                    ? (double)scenario.OccupancyOverride.Value / Math.Max(baselineOccupancy, 1.0)
                    : 1.0;

                // Simplified energy model
                // Energy = f(deltaT, occupancy, humidity, building envelope)
                int steps = (int)(scenario.SimulationDuration.TotalMinutes / scenario.TimeStep.TotalMinutes);
                var tempSeries = new List<double>(steps);
                var energySeries = new List<double>(steps);
                var comfortSeries = new List<double>(steps);

                double totalEnergyKWh = 0;
                double peakDemandKW = 0;
                double comfortSum = 0;

                double currentTemp = baselineTemp;
                double humidity = scenario.HumidityOverride ?? baselineHumidity;

                for (int step = 0; step < steps; step++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Thermal model: Newton's law of cooling with internal gains
                    double thermalMass = 500.0; // kJ/degC (building thermal mass)
                    double envelopeUA = 200.0;   // W/K (overall heat loss coefficient)
                    double internalGainW = 75.0 * baselineOccupancy * occupancyFactor; // 75W per person
                    double solarGainW = 5000.0 * Math.Max(0, Math.Sin(Math.PI * step / steps)); // simplified solar

                    // Heat balance: Q_dot = UA*(T_out - T_in) + Q_internal + Q_solar - Q_cooling
                    double heatFlowW = envelopeUA * (outdoorTemp - currentTemp) + internalGainW + solarGainW;
                    double coolingRequiredW = 0;

                    if (currentTemp > newSetpoint)
                    {
                        coolingRequiredW = heatFlowW + (currentTemp - newSetpoint) * thermalMass * 1000
                                           / scenario.TimeStep.TotalSeconds;
                        coolingRequiredW = Math.Max(0, coolingRequiredW);
                    }

                    double stepEnergyKWh = coolingRequiredW / 1000.0 * scenario.TimeStep.TotalHours;
                    double currentPowerKW = coolingRequiredW / 1000.0 + baselinePowerKW * 0.3; // 30% base load

                    // Update temperature
                    double netHeatW = heatFlowW - coolingRequiredW;
                    double deltaT = netHeatW / (thermalMass * 1000) * scenario.TimeStep.TotalSeconds;
                    currentTemp += deltaT;

                    // ASHRAE 55 comfort calculation
                    double comfort = CalculateThermalComfortScore(currentTemp, humidity);

                    tempSeries.Add(currentTemp);
                    energySeries.Add(stepEnergyKWh);
                    comfortSeries.Add(comfort);

                    totalEnergyKWh += stepEnergyKWh;
                    if (currentPowerKW > peakDemandKW) peakDemandKW = currentPowerKW;
                    comfortSum += comfort;
                }

                result.PredictedEnergyKWh = Math.Round(totalEnergyKWh, 2);
                result.PeakDemandKW = Math.Round(peakDemandKW, 2);
                result.ComfortScore = steps > 0 ? Math.Round(comfortSum / steps, 1) : 0;
                result.PredictedCostUSD = Math.Round(totalEnergyKWh * 0.12, 2); // $0.12/kWh average

                result.TimeSeriesData["Temperature_degC"] = tempSeries;
                result.TimeSeriesData["Energy_kWh"] = energySeries;
                result.TimeSeriesData["Comfort_pct"] = comfortSeries;

                // Add warnings
                if (result.ComfortScore < 70)
                    result.Warnings.Add("Predicted comfort score below 70%. Occupant complaints likely.");
                if (result.PeakDemandKW > baselinePowerKW * 1.5)
                    result.Warnings.Add($"Peak demand {result.PeakDemandKW:F0} kW exceeds 150% of baseline. " +
                                        "Check demand charges.");
                if (occupancyFactor > 1.5)
                    result.Warnings.Add("Occupancy override is 150%+ of baseline. Verify ventilation rates per ASHRAE 62.1.");

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Scenario '{Name}' complete: energy={Energy:F1} kWh, peak={Peak:F1} kW, " +
                        "comfort={Comfort:F1}%, cost=${Cost:F2}",
                scenario.ScenarioName, result.PredictedEnergyKWh,
                result.PeakDemandKW, result.ComfortScore, result.PredictedCostUSD);

            return result;
        }

        /// <summary>
        /// Calculates ASHRAE 55 thermal comfort score (0-100) based on temperature and humidity.
        /// Uses a simplified PMV-based model.
        /// </summary>
        private double CalculateThermalComfortScore(double tempC, double humidityPct)
        {
            double score = 100.0;

            // Temperature penalty
            if (tempC < ThermalComfortMinTemp)
                score -= Math.Min(50, (ThermalComfortMinTemp - tempC) * 10);
            else if (tempC > ThermalComfortMaxTemp)
                score -= Math.Min(50, (tempC - ThermalComfortMaxTemp) * 10);

            // Humidity penalty
            if (humidityPct < ThermalComfortMinRH)
                score -= Math.Min(20, (ThermalComfortMinRH - humidityPct) * 0.5);
            else if (humidityPct > ThermalComfortMaxRH)
                score -= Math.Min(30, (humidityPct - ThermalComfortMaxRH) * 0.75);

            return Math.Max(0, Math.Min(100, score));
        }

        #endregion

        #region Energy and Water Tracking

        /// <summary>
        /// Tracks energy consumption over a time range, aggregated by zone and system.
        /// Returns kWh totals with peak demand and cost estimates.
        /// </summary>
        /// <param name="from">Start of time range (UTC).</param>
        /// <param name="to">End of time range (UTC).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Energy consumption breakdown.</returns>
        public async Task<EnergyTrackingResult> TrackEnergyConsumptionAsync(
            DateTime from, DateTime to,
            CancellationToken cancellationToken = default)
        {
            var result = new EnergyTrackingResult { From = from, To = to };

            var powerSensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.Type == SensorCategory.Power)
                .ToList();

            await Task.Run(() =>
            {
                double totalKWh = 0;
                double peakKW = 0;

                foreach (var sensor in powerSensors)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var readings = _sensorEngine.GetHistoricalReadings(sensor.Id, from, to);
                    if (readings.Count < 2) continue;

                    // Trapezoidal integration for kWh
                    double sensorKWh = 0;
                    for (int i = 1; i < readings.Count; i++)
                    {
                        double dt = (readings[i].Timestamp - readings[i - 1].Timestamp).TotalHours;
                        double avgKW = (readings[i].Value + readings[i - 1].Value) / 2.0;
                        sensorKWh += avgKW * dt;

                        if (readings[i].Value > peakKW)
                            peakKW = readings[i].Value;
                    }

                    totalKWh += sensorKWh;
                    result.BySystem[sensor.Name] = Math.Round(sensorKWh, 2);

                    // Zone attribution
                    string zone = sensor.Location;
                    if (!string.IsNullOrWhiteSpace(zone))
                    {
                        if (!result.ByZone.ContainsKey(zone))
                            result.ByZone[zone] = 0;
                        result.ByZone[zone] += sensorKWh;
                    }
                }

                result.TotalKWh = Math.Round(totalKWh, 2);
                result.PeakDemandKW = Math.Round(peakKW, 2);
                result.AveragePowerKW = (to - from).TotalHours > 0
                    ? Math.Round(totalKWh / (to - from).TotalHours, 2) : 0;

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Energy tracking {From} to {To}: total={KWh} kWh, peak={Peak} kW",
                from, to, result.TotalKWh, result.PeakDemandKW);

            return result;
        }

        /// <summary>
        /// Performs water balance analysis comparing supply to consumption.
        /// Identifies potential leaks when consumption exceeds expected usage patterns.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Water balance analysis result.</returns>
        public async Task<WaterBalanceResult> WaterBalanceAnalysisAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new WaterBalanceResult { AnalysisTime = DateTime.UtcNow };

            var waterSensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.Type == SensorCategory.Water)
                .ToList();

            await Task.Run(() =>
            {
                double totalSupplyLiters = 0;
                double totalConsumptionLiters = 0;

                foreach (var sensor in waterSensors)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var readings = _sensorEngine.GetHistoricalReadings(
                        sensor.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

                    if (readings.Count < 2) continue;

                    // Calculate total volume from flow rate integration
                    double sensorLiters = 0;
                    for (int i = 1; i < readings.Count; i++)
                    {
                        double dtMinutes = (readings[i].Timestamp - readings[i - 1].Timestamp).TotalMinutes;
                        double avgFlowLpm = (readings[i].Value + readings[i - 1].Value) / 2.0;
                        sensorLiters += avgFlowLpm * dtMinutes;
                    }

                    // Classify as supply or consumption based on naming convention
                    bool isSupply = sensor.Name.Contains("Supply", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Main", StringComparison.OrdinalIgnoreCase);
                    if (isSupply)
                        totalSupplyLiters += sensorLiters;
                    else
                        totalConsumptionLiters += sensorLiters;

                    result.ByMeter[sensor.Name] = Math.Round(sensorLiters, 1);
                }

                result.TotalSupplyLiters = Math.Round(totalSupplyLiters, 1);
                result.TotalConsumptionLiters = Math.Round(totalConsumptionLiters, 1);
                result.UnaccountedLiters = Math.Round(totalSupplyLiters - totalConsumptionLiters, 1);
                result.LeakProbability = totalSupplyLiters > 0
                    ? Math.Min(1.0, Math.Max(0, result.UnaccountedLiters / totalSupplyLiters))
                    : 0;

                if (result.LeakProbability > 0.15)
                    result.Warnings.Add($"Unaccounted water loss of {result.UnaccountedLiters:F0}L " +
                                        $"({result.LeakProbability:P0}). Possible leak detected.");

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Water balance: supply={Supply}L, consumption={Consumption}L, " +
                        "unaccounted={Unaccounted}L, leak probability={Leak:P0}",
                result.TotalSupplyLiters, result.TotalConsumptionLiters,
                result.UnaccountedLiters, result.LeakProbability);

            return result;
        }

        #endregion

        #region Indoor Environmental Quality

        /// <summary>
        /// Calculates a composite Indoor Environmental Quality (IEQ) score for a zone.
        /// Includes temperature, humidity, CO2, illuminance, and acoustic sub-scores
        /// per ASHRAE 55, ASHRAE 62.1, EN 12464-1, and ASHRAE Handbook guidelines.
        /// </summary>
        /// <param name="zoneId">Zone identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>IEQ analysis with individual sub-scores and composite score.</returns>
        public async Task<IeqResult> IndoorEnvironmentalQualityAsync(
            string zoneId,
            CancellationToken cancellationToken = default)
        {
            var result = new IeqResult { ZoneId = zoneId };

            ZoneDefinition zone;
            lock (_lockObject)
            {
                if (!_zones.TryGetValue(zoneId, out zone))
                {
                    Logger.Warn("Zone {ZoneId} not found. Returning default IEQ.", zoneId);
                    result.CompositeScore = 0;
                    return result;
                }
            }

            result.ZoneName = zone.ZoneName;

            await Task.Run(() =>
            {
                double tempScore = 100, humidityScore = 100, co2Score = 100;
                double lightScore = 100, acousticScore = 100;
                int factors = 0;

                foreach (string sensorId in zone.SensorIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var reading = _sensorEngine.GetLatestReading(sensorId);
                    var sensorDef = _sensorEngine.GetSensor(sensorId);
                    if (reading == null || sensorDef == null) continue;
                    if (reading.Quality == SensorDataQuality.Bad) continue;

                    switch (sensorDef.Type)
                    {
                        case SensorCategory.Temperature:
                            tempScore = CalculateThermalComfortScore(reading.Value,
                                GetAverageReading(SensorCategory.Humidity) ?? 50.0);
                            result.Temperature = reading.Value;
                            factors++;
                            break;

                        case SensorCategory.Humidity:
                            humidityScore = 100.0;
                            result.Humidity = reading.Value;
                            if (reading.Value < ThermalComfortMinRH)
                                humidityScore -= (ThermalComfortMinRH - reading.Value) * 2;
                            else if (reading.Value > ThermalComfortMaxRH)
                                humidityScore -= (reading.Value - ThermalComfortMaxRH) * 2;
                            humidityScore = Math.Max(0, humidityScore);
                            factors++;
                            break;

                        case SensorCategory.CO2:
                            result.Co2Ppm = reading.Value;
                            co2Score = 100.0;
                            if (reading.Value > Co2AcceptableLimit)
                                co2Score -= Math.Min(80, (reading.Value - Co2AcceptableLimit) * 0.1);
                            else if (reading.Value > 800)
                                co2Score -= (reading.Value - 800) * 0.05;
                            co2Score = Math.Max(0, co2Score);
                            factors++;
                            break;

                        case SensorCategory.Light:
                            result.Illuminance = reading.Value;
                            lightScore = 100.0;
                            if (reading.Value < LightMinOffice)
                                lightScore -= Math.Min(60, (LightMinOffice - reading.Value) / LightMinOffice * 60);
                            else if (reading.Value > LightMaxOffice * 2)
                                lightScore -= Math.Min(30, (reading.Value - LightMaxOffice * 2) / LightMaxOffice * 30);
                            lightScore = Math.Max(0, lightScore);
                            factors++;
                            break;

                        case SensorCategory.Acoustic:
                            result.NoiseLevel = reading.Value;
                            acousticScore = 100.0;
                            if (reading.Value > AcousticMaxOffice)
                                acousticScore -= Math.Min(60, (reading.Value - AcousticMaxOffice) * 3);
                            acousticScore = Math.Max(0, acousticScore);
                            factors++;
                            break;
                    }
                }

                result.ThermalComfortScore = Math.Round(tempScore, 1);
                result.HumidityScore = Math.Round(humidityScore, 1);
                result.AirQualityScore = Math.Round(co2Score, 1);
                result.LightingScore = Math.Round(lightScore, 1);
                result.AcousticScore = Math.Round(acousticScore, 1);

                // Weighted composite (ASHRAE 55 weights thermal comfort most heavily)
                if (factors > 0)
                {
                    result.CompositeScore = Math.Round(
                        tempScore * 0.30 +
                        humidityScore * 0.15 +
                        co2Score * 0.25 +
                        lightScore * 0.15 +
                        acousticScore * 0.15, 1);
                }

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("IEQ for zone {ZoneId}: composite={Score:F1}%, temp={Temp:F1}, " +
                        "humidity={Hum:F1}, CO2={CO2:F0}, light={Light:F0}, acoustic={Acoustic:F1}",
                zoneId, result.CompositeScore, result.ThermalComfortScore,
                result.HumidityScore, result.AirQualityScore, result.LightingScore, result.AcousticScore);

            return result;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the average of the latest readings across all sensors of a given type.
        /// </summary>
        private double? GetAverageReading(SensorCategory category)
        {
            var sensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.Type == category)
                .ToList();

            if (sensors.Count == 0) return null;

            double sum = 0;
            int count = 0;
            foreach (var sensor in sensors)
            {
                var reading = _sensorEngine.GetLatestReading(sensor.Id);
                if (reading != null && reading.Quality != SensorDataQuality.Bad)
                {
                    sum += reading.Value;
                    count++;
                }
            }

            return count > 0 ? sum / count : (double?)null;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Zone definition for IEQ and occupancy analysis.
    /// </summary>
    public class ZoneDefinition
    {
        public string ZoneId { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public double AreaSqM { get; set; }
        public int MaxOccupancy { get; set; }
        public string FloorLevel { get; set; } = string.Empty;
        public List<string> SensorIds { get; set; } = new List<string>();
        public List<string> ElementIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of a model synchronization operation.
    /// </summary>
    public class SyncResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string Status { get; set; } = string.Empty;
        public int ReadingsProcessed { get; set; }
        public int StateChanges { get; set; }
        public int NewSensors { get; set; }
        public int MissingSensors { get; set; }
        public double BuildingHealthScore { get; set; }
    }

    /// <summary>
    /// Sensor data overlay information for a single Revit element.
    /// </summary>
    public class SensorOverlay
    {
        public string ElementId { get; set; } = string.Empty;
        public double PrimaryValue { get; set; }
        public string PrimaryUnit { get; set; } = string.Empty;
        public SensorCategory PrimarySensorType { get; set; }
        public double NormalizedValue { get; set; }
        public byte HeatmapColorR { get; set; }
        public byte HeatmapColorG { get; set; }
        public byte HeatmapColorB { get; set; }
        public double HealthScore { get; set; }
        public byte HealthColorR { get; set; }
        public byte HealthColorG { get; set; }
        public byte HealthColorB { get; set; }
        public string Label { get; set; } = string.Empty;
        public SensorDataQuality Quality { get; set; }
        public DateTime Timestamp { get; set; }
        public List<ReadingSummary> AllReadings { get; set; } = new List<ReadingSummary>();
    }

    /// <summary>
    /// Summary of a single sensor reading for overlay display.
    /// </summary>
    public class ReadingSummary
    {
        public string SensorId { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public SensorDataQuality Quality { get; set; }
    }

    /// <summary>
    /// Predictive maintenance result for an element.
    /// </summary>
    public class MaintenancePrediction
    {
        public string ElementId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double CurrentHealthScore { get; set; }
        public int? EstimatedDaysToFailure { get; set; }
        public double Confidence { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Linear regression result for trend analysis.
    /// </summary>
    public class RegressionResult
    {
        public double Slope { get; set; }
        public double Intercept { get; set; }
        public double RSquared { get; set; }
        public double MeanY { get; set; }
        public double StdDevY { get; set; }
    }

    /// <summary>
    /// Trend analysis data for a sensor.
    /// </summary>
    public class TrendAnalysis
    {
        public string SensorId { get; set; } = string.Empty;
        public double Slope { get; set; }
        public double Intercept { get; set; }
        public double RSquared { get; set; }
        public double MeanValue { get; set; }
        public double StdDeviation { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
    }

    /// <summary>
    /// Energy consumption tracking result.
    /// </summary>
    public class EnergyTrackingResult
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public double TotalKWh { get; set; }
        public double PeakDemandKW { get; set; }
        public double AveragePowerKW { get; set; }
        public Dictionary<string, double> ByZone { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> BySystem { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Water balance analysis result.
    /// </summary>
    public class WaterBalanceResult
    {
        public DateTime AnalysisTime { get; set; }
        public double TotalSupplyLiters { get; set; }
        public double TotalConsumptionLiters { get; set; }
        public double UnaccountedLiters { get; set; }
        public double LeakProbability { get; set; }
        public Dictionary<string, double> ByMeter { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Indoor Environmental Quality analysis result.
    /// </summary>
    public class IeqResult
    {
        public string ZoneId { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public double CompositeScore { get; set; }
        public double ThermalComfortScore { get; set; }
        public double HumidityScore { get; set; }
        public double AirQualityScore { get; set; }
        public double LightingScore { get; set; }
        public double AcousticScore { get; set; }
        public double? Temperature { get; set; }
        public double? Humidity { get; set; }
        public double? Co2Ppm { get; set; }
        public double? Illuminance { get; set; }
        public double? NoiseLevel { get; set; }
    }

    #endregion
}
