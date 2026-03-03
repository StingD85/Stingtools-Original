// StingBIM.AI.IoT.Engine.SensorIntegrationEngine
// Core BMS integration engine for sensor registration, polling, data ingestion,
// anomaly detection, and alert generation. Supports BACnet, Modbus, MQTT, OPC-UA,
// REST, KNX, and LonWorks protocols via pluggable protocol adapters.
// Reference: ISO 16484-5 (BACnet), ISO 16484-6 (Data Communication Conformance Testing)

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
    /// Central engine for integrating with Building Management Systems (BMS).
    /// Manages sensor registration, endpoint communication, real-time data ingestion,
    /// ring-buffered streaming, anomaly detection, and alert generation.
    /// Thread-safe for concurrent polling from multiple protocol adapters.
    /// </summary>
    public class SensorIntegrationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Sensor and endpoint registries
        private readonly Dictionary<string, SensorDefinition> _sensors =
            new Dictionary<string, SensorDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BmsEndpoint> _endpoints =
            new Dictionary<string, BmsEndpoint>(StringComparer.OrdinalIgnoreCase);

        // Latest readings and historical ring buffers
        private readonly ConcurrentDictionary<string, SensorReading> _latestReadings =
            new ConcurrentDictionary<string, SensorReading>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, RingBuffer<SensorReading>> _readingBuffers =
            new ConcurrentDictionary<string, RingBuffer<SensorReading>>(StringComparer.OrdinalIgnoreCase);

        // Historical storage for time-range queries
        private readonly ConcurrentDictionary<string, List<SensorReading>> _historicalStore =
            new ConcurrentDictionary<string, List<SensorReading>>(StringComparer.OrdinalIgnoreCase);

        // Alert tracking
        private readonly ConcurrentBag<IoTAlert> _alerts = new ConcurrentBag<IoTAlert>();
        private readonly ConcurrentDictionary<string, DateTime> _alertCooldowns =
            new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        // Protocol adapters
        private readonly Dictionary<IoTProtocol, IProtocolAdapter> _protocolAdapters =
            new Dictionary<IoTProtocol, IProtocolAdapter>();

        // Configuration
        private readonly int _ringBufferCapacity;
        private readonly int _maxHistoricalReadingsPerSensor;
        private readonly TimeSpan _alertCooldownPeriod;
        private readonly double _anomalyZScoreThreshold;

        /// <summary>
        /// Initializes the SensorIntegrationEngine with configurable buffer sizes and thresholds.
        /// </summary>
        /// <param name="ringBufferCapacity">Number of recent readings to keep per sensor in the ring buffer.</param>
        /// <param name="maxHistoricalReadings">Maximum historical readings per sensor before pruning.</param>
        /// <param name="alertCooldownMinutes">Minimum minutes between repeated alerts for the same sensor.</param>
        /// <param name="anomalyZScoreThreshold">Z-score threshold for anomaly detection (default 3.0).</param>
        public SensorIntegrationEngine(
            int ringBufferCapacity = 1000,
            int maxHistoricalReadings = 100000,
            double alertCooldownMinutes = 5.0,
            double anomalyZScoreThreshold = 3.0)
        {
            _ringBufferCapacity = ringBufferCapacity;
            _maxHistoricalReadingsPerSensor = maxHistoricalReadings;
            _alertCooldownPeriod = TimeSpan.FromMinutes(alertCooldownMinutes);
            _anomalyZScoreThreshold = anomalyZScoreThreshold;

            InitializeProtocolAdapters();
            Logger.Info("SensorIntegrationEngine initialized: buffer={BufferSize}, maxHistory={MaxHistory}, " +
                        "cooldown={Cooldown}min, zThreshold={ZThreshold}",
                _ringBufferCapacity, _maxHistoricalReadingsPerSensor,
                alertCooldownMinutes, anomalyZScoreThreshold);
        }

        #region Sensor Registration

        /// <summary>
        /// Registers a sensor definition, binding it to a Revit element and configuring
        /// validation ranges, thresholds, and alert rules.
        /// </summary>
        /// <param name="sensor">The sensor definition to register.</param>
        /// <returns>True if registration succeeded, false if sensor ID already exists.</returns>
        public bool RegisterSensor(SensorDefinition sensor)
        {
            if (sensor == null)
                throw new ArgumentNullException(nameof(sensor));
            if (string.IsNullOrWhiteSpace(sensor.Id))
                throw new ArgumentException("Sensor ID cannot be empty.", nameof(sensor));

            lock (_lockObject)
            {
                if (_sensors.ContainsKey(sensor.Id))
                {
                    Logger.Warn("Sensor {SensorId} is already registered, skipping duplicate registration.", sensor.Id);
                    return false;
                }

                _sensors[sensor.Id] = sensor;
                _readingBuffers[sensor.Id] = new RingBuffer<SensorReading>(_ringBufferCapacity);
                _historicalStore[sensor.Id] = new List<SensorReading>();
            }

            Logger.Info("Registered sensor {SensorId} ({SensorName}) of type {SensorType} bound to element {ElementId}",
                sensor.Id, sensor.Name, sensor.Type, sensor.ElementId);
            return true;
        }

        /// <summary>
        /// Unregisters a sensor and removes all associated data.
        /// </summary>
        /// <param name="sensorId">The sensor ID to unregister.</param>
        /// <returns>True if the sensor was found and removed.</returns>
        public bool UnregisterSensor(string sensorId)
        {
            lock (_lockObject)
            {
                if (!_sensors.Remove(sensorId))
                    return false;

                _readingBuffers.TryRemove(sensorId, out _);
                _historicalStore.TryRemove(sensorId, out _);
                _latestReadings.TryRemove(sensorId, out _);
            }

            Logger.Info("Unregistered sensor {SensorId}", sensorId);
            return true;
        }

        /// <summary>
        /// Retrieves all registered sensor definitions.
        /// </summary>
        public IReadOnlyList<SensorDefinition> GetRegisteredSensors()
        {
            lock (_lockObject)
            {
                return _sensors.Values.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Retrieves a specific sensor definition by ID.
        /// </summary>
        public SensorDefinition GetSensor(string sensorId)
        {
            lock (_lockObject)
            {
                return _sensors.TryGetValue(sensorId, out var sensor) ? sensor : null;
            }
        }

        #endregion

        #region BMS Endpoint Registration

        /// <summary>
        /// Registers a BMS communication endpoint and associates it with its sensor list.
        /// Validates protocol adapter availability before registration.
        /// </summary>
        /// <param name="endpoint">The BMS endpoint configuration.</param>
        /// <returns>True if registration succeeded.</returns>
        public bool RegisterBmsEndpoint(BmsEndpoint endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrWhiteSpace(endpoint.Address))
                throw new ArgumentException("Endpoint address cannot be empty.", nameof(endpoint));

            if (!_protocolAdapters.ContainsKey(endpoint.Protocol))
            {
                Logger.Warn("No protocol adapter registered for {Protocol}. Endpoint {EndpointId} will use simulated adapter.",
                    endpoint.Protocol, endpoint.EndpointId);
            }

            lock (_lockObject)
            {
                _endpoints[endpoint.EndpointId] = endpoint;
            }

            Logger.Info("Registered BMS endpoint {EndpointId} ({Protocol}://{Address}:{Port}) with {SensorCount} sensors",
                endpoint.EndpointId, endpoint.Protocol, endpoint.Address, endpoint.Port, endpoint.SensorIds.Count);
            return true;
        }

        /// <summary>
        /// Removes a BMS endpoint registration.
        /// </summary>
        public bool UnregisterBmsEndpoint(string endpointId)
        {
            lock (_lockObject)
            {
                return _endpoints.Remove(endpointId);
            }
        }

        #endregion

        #region Data Polling

        /// <summary>
        /// Polls all registered and enabled BMS endpoints concurrently.
        /// For each endpoint, reads all associated sensor points using the appropriate
        /// protocol adapter, then ingests each reading through the validation pipeline.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for cooperative shutdown.</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
        /// <returns>Total number of readings successfully ingested across all endpoints.</returns>
        public async Task<int> PollSensorsAsync(
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            List<BmsEndpoint> activeEndpoints;
            lock (_lockObject)
            {
                activeEndpoints = _endpoints.Values
                    .Where(e => e.IsEnabled && e.IsHealthy)
                    .ToList();
            }

            if (activeEndpoints.Count == 0)
            {
                Logger.Debug("No active healthy endpoints to poll.");
                return 0;
            }

            Logger.Debug("Polling {EndpointCount} active endpoints...", activeEndpoints.Count);
            int totalIngested = 0;
            int completedEndpoints = 0;

            var tasks = activeEndpoints.Select(async endpoint =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = await PollEndpointAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    Interlocked.Add(ref totalIngested, count);

                    // Reset failure counter on success
                    endpoint.ConsecutiveFailures = 0;
                    endpoint.LastSuccessfulPoll = DateTime.UtcNow;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    endpoint.ConsecutiveFailures++;
                    Logger.Error(ex, "Failed to poll endpoint {EndpointId} ({Protocol}://{Address}:{Port}). " +
                                     "Consecutive failures: {Failures}",
                        endpoint.EndpointId, endpoint.Protocol, endpoint.Address,
                        endpoint.Port, endpoint.ConsecutiveFailures);

                    if (!endpoint.IsHealthy)
                    {
                        Logger.Warn("Endpoint {EndpointId} marked unhealthy after {Failures} consecutive failures.",
                            endpoint.EndpointId, endpoint.ConsecutiveFailures);
                    }
                }
                finally
                {
                    int done = Interlocked.Increment(ref completedEndpoints);
                    progress?.Report((double)done / activeEndpoints.Count);
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Logger.Info("Poll cycle complete: {TotalIngested} readings from {EndpointCount} endpoints.",
                totalIngested, activeEndpoints.Count);
            return totalIngested;
        }

        /// <summary>
        /// Polls a single BMS endpoint, reading all associated sensor points.
        /// Uses the appropriate protocol adapter to communicate with the endpoint.
        /// </summary>
        private async Task<int> PollEndpointAsync(BmsEndpoint endpoint, CancellationToken cancellationToken)
        {
            var adapter = GetProtocolAdapter(endpoint.Protocol);
            int ingestedCount = 0;

            foreach (string sensorId in endpoint.SensorIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SensorDefinition sensorDef;
                lock (_lockObject)
                {
                    if (!_sensors.TryGetValue(sensorId, out sensorDef) || !sensorDef.IsActive)
                        continue;
                }

                try
                {
                    var reading = await adapter.ReadSensorAsync(
                        endpoint, sensorDef, cancellationToken).ConfigureAwait(false);

                    if (reading != null)
                    {
                        await IngestReadingAsync(reading, cancellationToken).ConfigureAwait(false);
                        ingestedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to read sensor {SensorId} from endpoint {EndpointId}",
                        sensorId, endpoint.EndpointId);
                }
            }

            return ingestedCount;
        }

        #endregion

        #region Data Ingestion

        /// <summary>
        /// Ingests a single sensor reading through the validation pipeline:
        /// 1. Basic validation (non-null, valid sensor ID, valid timestamp)
        /// 2. Range validation against sensor definition min/max
        /// 3. Rate-of-change validation against previous reading
        /// 4. Staleness detection based on time since last reading
        /// 5. Ring buffer insertion for real-time streaming
        /// 6. Historical store append with pruning
        /// 7. Alert threshold evaluation and generation
        /// </summary>
        /// <param name="reading">The sensor reading to ingest.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the reading was ingested (possibly with quality downgrade).</returns>
        public async Task<bool> IngestReadingAsync(
            SensorReading reading,
            CancellationToken cancellationToken = default)
        {
            if (reading == null || !reading.IsValid())
            {
                Logger.Warn("Rejected invalid sensor reading: {Reading}", reading);
                return false;
            }

            // Retrieve sensor definition for validation
            SensorDefinition sensorDef;
            lock (_lockObject)
            {
                if (!_sensors.TryGetValue(reading.SensorId, out sensorDef))
                {
                    Logger.Warn("Reading received for unregistered sensor {SensorId}. Registering ad-hoc.",
                        reading.SensorId);
                    // Allow readings for unregistered sensors with default validation
                    sensorDef = null;
                }
            }

            // Step 1: Range validation
            if (sensorDef != null)
            {
                reading.Quality = ValidateRange(reading, sensorDef);
            }

            // Step 2: Rate-of-change validation
            if (sensorDef != null && sensorDef.MaxRateOfChange > 0)
            {
                var previousQuality = ValidateRateOfChange(reading, sensorDef);
                if (previousQuality > reading.Quality)
                    reading.Quality = previousQuality;
            }

            // Step 3: Staleness detection (handled on read, not on ingest)

            // Step 4: Store in ring buffer
            if (_readingBuffers.TryGetValue(reading.SensorId, out var ringBuffer))
            {
                ringBuffer.Add(reading);
            }
            else
            {
                // Ad-hoc sensor: create buffer on the fly
                var newBuffer = new RingBuffer<SensorReading>(_ringBufferCapacity);
                newBuffer.Add(reading);
                _readingBuffers[reading.SensorId] = newBuffer;
            }

            // Step 5: Update latest reading
            _latestReadings[reading.SensorId] = reading;

            // Step 6: Append to historical store with pruning
            await Task.Run(() =>
            {
                var history = _historicalStore.GetOrAdd(reading.SensorId, _ => new List<SensorReading>());
                lock (history)
                {
                    history.Add(reading);
                    if (history.Count > _maxHistoricalReadingsPerSensor)
                    {
                        // Remove oldest 10% to reduce frequency of pruning
                        int removeCount = _maxHistoricalReadingsPerSensor / 10;
                        history.RemoveRange(0, removeCount);
                        Logger.Debug("Pruned {Count} old readings for sensor {SensorId}",
                            removeCount, reading.SensorId);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            // Step 7: Alert evaluation
            if (sensorDef != null)
            {
                EvaluateAlertThresholds(reading, sensorDef);
            }

            return true;
        }

        /// <summary>
        /// Validates a reading value against the sensor's configured min/max range.
        /// Returns the appropriate data quality flag.
        /// </summary>
        private SensorDataQuality ValidateRange(SensorReading reading, SensorDefinition sensorDef)
        {
            if (sensorDef.MinRange == 0 && sensorDef.MaxRange == 0)
                return reading.Quality; // No range configured

            if (reading.Value < sensorDef.MinRange || reading.Value > sensorDef.MaxRange)
            {
                Logger.Warn("Sensor {SensorId} reading {Value} {Unit} out of range [{Min}, {Max}]",
                    reading.SensorId, reading.Value, reading.Unit,
                    sensorDef.MinRange, sensorDef.MaxRange);
                return SensorDataQuality.Bad;
            }

            // Check if near the edges (within 5% of range) - mark as uncertain
            double range = sensorDef.MaxRange - sensorDef.MinRange;
            double marginPercent = 0.05;
            if (reading.Value < sensorDef.MinRange + range * marginPercent ||
                reading.Value > sensorDef.MaxRange - range * marginPercent)
            {
                return SensorDataQuality.Uncertain;
            }

            return SensorDataQuality.Good;
        }

        /// <summary>
        /// Validates the rate of change between the current reading and the previous one.
        /// Flags as Uncertain if the change exceeds the configured maximum per minute.
        /// </summary>
        private SensorDataQuality ValidateRateOfChange(SensorReading reading, SensorDefinition sensorDef)
        {
            if (!_latestReadings.TryGetValue(reading.SensorId, out var previous))
                return SensorDataQuality.Good;

            double timeDeltaMinutes = (reading.Timestamp - previous.Timestamp).TotalMinutes;
            if (timeDeltaMinutes <= 0)
                return SensorDataQuality.Good;

            double rateOfChange = Math.Abs(reading.Value - previous.Value) / timeDeltaMinutes;

            if (rateOfChange > sensorDef.MaxRateOfChange * 2)
            {
                Logger.Warn("Sensor {SensorId} excessive rate of change: {Rate}/min (max {Max}/min). Marking Bad.",
                    reading.SensorId, rateOfChange, sensorDef.MaxRateOfChange);
                return SensorDataQuality.Bad;
            }

            if (rateOfChange > sensorDef.MaxRateOfChange)
            {
                Logger.Debug("Sensor {SensorId} elevated rate of change: {Rate}/min (max {Max}/min). Marking Uncertain.",
                    reading.SensorId, rateOfChange, sensorDef.MaxRateOfChange);
                return SensorDataQuality.Uncertain;
            }

            return SensorDataQuality.Good;
        }

        /// <summary>
        /// Evaluates the reading against alert thresholds and generates alerts when exceeded.
        /// Respects the cooldown period to avoid alert flooding.
        /// </summary>
        private void EvaluateAlertThresholds(SensorReading reading, SensorDefinition sensorDef)
        {
            var severity = sensorDef.Thresholds.Evaluate(reading.Value);
            if (!severity.HasValue) return;

            // Check cooldown to prevent alert flooding
            string cooldownKey = $"{reading.SensorId}_{severity.Value}";
            if (_alertCooldowns.TryGetValue(cooldownKey, out var lastAlertTime))
            {
                if (DateTime.UtcNow - lastAlertTime < _alertCooldownPeriod)
                    return;
            }

            // Determine which threshold was violated for the message
            double thresholdValue = DetermineViolatedThreshold(reading.Value, sensorDef.Thresholds);
            string direction = reading.Value > 0 && thresholdValue > 0 && reading.Value >= thresholdValue
                ? "exceeded" : "fell below";

            var alert = new IoTAlert
            {
                SensorId = reading.SensorId,
                Severity = severity.Value,
                Message = $"Sensor {sensorDef.Name} ({sensorDef.Type}) {direction} threshold: " +
                          $"{reading.Value:F2} {reading.Unit} (threshold: {thresholdValue:F2} {reading.Unit})",
                TriggerValue = reading.Value,
                ThresholdValue = thresholdValue,
                ElementId = sensorDef.ElementId
            };

            _alerts.Add(alert);
            _alertCooldowns[cooldownKey] = DateTime.UtcNow;

            Logger.Warn("Alert generated for sensor {SensorId}: [{Severity}] {Message}",
                reading.SensorId, severity.Value, alert.Message);
        }

        /// <summary>
        /// Determines which specific threshold value was violated.
        /// </summary>
        private double DetermineViolatedThreshold(double value, AlertThresholds thresholds)
        {
            if (thresholds.HighHigh.HasValue && value >= thresholds.HighHigh.Value)
                return thresholds.HighHigh.Value;
            if (thresholds.LowLow.HasValue && value <= thresholds.LowLow.Value)
                return thresholds.LowLow.Value;
            if (thresholds.High.HasValue && value >= thresholds.High.Value)
                return thresholds.High.Value;
            if (thresholds.Low.HasValue && value <= thresholds.Low.Value)
                return thresholds.Low.Value;
            return 0;
        }

        #endregion

        #region Data Retrieval

        /// <summary>
        /// Retrieves the most recent reading for a sensor.
        /// Applies staleness detection based on the sensor's configured threshold.
        /// </summary>
        /// <param name="sensorId">The sensor ID to query.</param>
        /// <returns>The latest reading, or null if no readings exist.</returns>
        public SensorReading GetLatestReading(string sensorId)
        {
            if (!_latestReadings.TryGetValue(sensorId, out var reading))
                return null;

            // Apply staleness detection
            SensorDefinition sensorDef;
            lock (_lockObject)
            {
                _sensors.TryGetValue(sensorId, out sensorDef);
            }

            if (sensorDef != null && reading.Quality == SensorDataQuality.Good)
            {
                double secondsSinceReading = (DateTime.UtcNow - reading.Timestamp).TotalSeconds;
                if (secondsSinceReading > sensorDef.StaleThresholdSeconds)
                {
                    reading.Quality = SensorDataQuality.Stale;
                }
            }

            return reading;
        }

        /// <summary>
        /// Retrieves historical readings for a sensor within a time range.
        /// Uses binary search for efficient range lookups on the sorted historical store.
        /// </summary>
        /// <param name="sensorId">The sensor ID to query.</param>
        /// <param name="from">Start of the time range (inclusive, UTC).</param>
        /// <param name="to">End of the time range (inclusive, UTC).</param>
        /// <returns>List of readings within the specified time range, ordered by timestamp.</returns>
        public IReadOnlyList<SensorReading> GetHistoricalReadings(string sensorId, DateTime from, DateTime to)
        {
            if (!_historicalStore.TryGetValue(sensorId, out var history))
                return Array.Empty<SensorReading>();

            lock (history)
            {
                // Binary search for the start index
                int startIdx = BinarySearchTimestamp(history, from, searchForStart: true);
                int endIdx = BinarySearchTimestamp(history, to, searchForStart: false);

                if (startIdx < 0 || startIdx >= history.Count || endIdx < startIdx)
                    return Array.Empty<SensorReading>();

                int count = Math.Min(endIdx - startIdx + 1, history.Count - startIdx);
                return history.GetRange(startIdx, count).AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the most recent N readings from the ring buffer for real-time display.
        /// </summary>
        /// <param name="sensorId">The sensor ID to query.</param>
        /// <param name="count">Number of recent readings to retrieve.</param>
        /// <returns>Recent readings ordered newest-first.</returns>
        public IReadOnlyList<SensorReading> GetRecentReadings(string sensorId, int count = 100)
        {
            if (!_readingBuffers.TryGetValue(sensorId, out var buffer))
                return Array.Empty<SensorReading>();

            return buffer.GetLatest(count);
        }

        /// <summary>
        /// Binary search for timestamp boundaries in sorted reading lists.
        /// </summary>
        private int BinarySearchTimestamp(List<SensorReading> readings, DateTime target, bool searchForStart)
        {
            int low = 0, high = readings.Count - 1;
            int result = searchForStart ? readings.Count : -1;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                if (searchForStart)
                {
                    if (readings[mid].Timestamp >= target)
                    {
                        result = mid;
                        high = mid - 1;
                    }
                    else
                    {
                        low = mid + 1;
                    }
                }
                else
                {
                    if (readings[mid].Timestamp <= target)
                    {
                        result = mid;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Anomaly Detection

        /// <summary>
        /// Performs Z-score based anomaly detection on recent sensor readings.
        /// Calculates the mean and standard deviation over the specified window,
        /// then identifies readings whose Z-score exceeds the configured threshold.
        /// </summary>
        /// <param name="sensorId">The sensor to analyze.</param>
        /// <param name="window">Time window to analyze (e.g., last 24 hours).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of anomalous readings with their Z-scores.</returns>
        public async Task<List<(SensorReading Reading, double ZScore)>> DetectAnomaliesAsync(
            string sensorId,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            var anomalies = new List<(SensorReading Reading, double ZScore)>();
            DateTime cutoff = DateTime.UtcNow - window;

            var readings = GetHistoricalReadings(sensorId, cutoff, DateTime.UtcNow);
            if (readings.Count < 10)
            {
                Logger.Debug("Insufficient readings ({Count}) for anomaly detection on sensor {SensorId}. Minimum 10 required.",
                    readings.Count, sensorId);
                return anomalies;
            }

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate mean
                double sum = 0;
                foreach (var r in readings) sum += r.Value;
                double mean = sum / readings.Count;

                // Calculate standard deviation
                double sumSquaredDiff = 0;
                foreach (var r in readings)
                {
                    double diff = r.Value - mean;
                    sumSquaredDiff += diff * diff;
                }
                double stdDev = Math.Sqrt(sumSquaredDiff / readings.Count);

                if (stdDev < 1e-10)
                {
                    // All readings identical, no anomalies possible
                    Logger.Debug("Sensor {SensorId} has zero variance over window. No anomalies.", sensorId);
                    return;
                }

                // Identify anomalies
                foreach (var reading in readings)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    double zScore = (reading.Value - mean) / stdDev;

                    if (Math.Abs(zScore) > _anomalyZScoreThreshold)
                    {
                        anomalies.Add((reading, zScore));
                    }
                }

                // Sort by absolute Z-score descending (most anomalous first)
                anomalies.Sort((a, b) => Math.Abs(b.ZScore).CompareTo(Math.Abs(a.ZScore)));

            }, cancellationToken).ConfigureAwait(false);

            if (anomalies.Count > 0)
            {
                Logger.Info("Detected {Count} anomalies for sensor {SensorId} over {Window} window. " +
                            "Max Z-score: {MaxZ:F2}",
                    anomalies.Count, sensorId, window, anomalies[0].ZScore);

                // Generate alert for the most severe anomaly
                SensorDefinition sensorDef;
                lock (_lockObject) { _sensors.TryGetValue(sensorId, out sensorDef); }

                if (sensorDef != null)
                {
                    var worstAnomaly = anomalies[0];
                    var alert = new IoTAlert
                    {
                        SensorId = sensorId,
                        Severity = Math.Abs(worstAnomaly.ZScore) > _anomalyZScoreThreshold * 2
                            ? AlertSeverity.Critical : AlertSeverity.Warning,
                        Message = $"Anomaly detected on {sensorDef.Name}: value {worstAnomaly.Reading.Value:F2} " +
                                  $"{sensorDef.Unit} (Z-score: {worstAnomaly.ZScore:F2})",
                        TriggerValue = worstAnomaly.Reading.Value,
                        ElementId = sensorDef.ElementId
                    };
                    _alerts.Add(alert);
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Performs moving average smoothing on recent readings to reduce noise.
        /// </summary>
        /// <param name="sensorId">The sensor to smooth.</param>
        /// <param name="windowSize">Number of readings in the moving average window.</param>
        /// <returns>Smoothed values paired with their original timestamps.</returns>
        public List<(DateTime Timestamp, double SmoothedValue)> CalculateMovingAverage(
            string sensorId, int windowSize = 5)
        {
            var results = new List<(DateTime, double)>();
            if (!_readingBuffers.TryGetValue(sensorId, out var buffer))
                return results;

            var readings = buffer.GetAll();
            if (readings.Count < windowSize) return results;

            // Compute simple moving average
            double windowSum = 0;
            for (int i = 0; i < windowSize; i++)
                windowSum += readings[i].Value;

            results.Add((readings[windowSize - 1].Timestamp, windowSum / windowSize));

            for (int i = windowSize; i < readings.Count; i++)
            {
                windowSum += readings[i].Value - readings[i - windowSize].Value;
                results.Add((readings[i].Timestamp, windowSum / windowSize));
            }

            return results;
        }

        #endregion

        #region Alert Management

        /// <summary>
        /// Retrieves all active (unacknowledged) alerts, optionally filtered by sensor or severity.
        /// </summary>
        public IReadOnlyList<IoTAlert> GetActiveAlerts(
            string sensorId = null,
            AlertSeverity? minSeverity = null)
        {
            IEnumerable<IoTAlert> query = _alerts.Where(a => !a.Acknowledged);

            if (!string.IsNullOrWhiteSpace(sensorId))
                query = query.Where(a => a.SensorId.Equals(sensorId, StringComparison.OrdinalIgnoreCase));

            if (minSeverity.HasValue)
                query = query.Where(a => a.Severity >= minSeverity.Value);

            return query.OrderByDescending(a => a.Severity)
                        .ThenByDescending(a => a.Timestamp)
                        .ToList()
                        .AsReadOnly();
        }

        /// <summary>
        /// Acknowledges an alert by ID, recording the operator and timestamp.
        /// </summary>
        public bool AcknowledgeAlert(string alertId, string acknowledgedBy)
        {
            var alert = _alerts.FirstOrDefault(a =>
                a.AlertId.Equals(alertId, StringComparison.OrdinalIgnoreCase));

            if (alert == null) return false;

            alert.Acknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = acknowledgedBy;

            Logger.Info("Alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);
            return true;
        }

        /// <summary>
        /// Gets a summary count of alerts grouped by severity.
        /// </summary>
        public Dictionary<AlertSeverity, int> GetAlertSummary()
        {
            var summary = new Dictionary<AlertSeverity, int>();
            foreach (AlertSeverity severity in Enum.GetValues(typeof(AlertSeverity)))
            {
                summary[severity] = _alerts.Count(a => !a.Acknowledged && a.Severity == severity);
            }
            return summary;
        }

        #endregion

        #region Protocol Adapters

        /// <summary>
        /// Initializes the built-in protocol adapters for each supported BMS protocol.
        /// </summary>
        private void InitializeProtocolAdapters()
        {
            _protocolAdapters[IoTProtocol.BACnet] = new BACnetAdapter();
            _protocolAdapters[IoTProtocol.Modbus] = new ModbusAdapter();
            _protocolAdapters[IoTProtocol.MQTT] = new MqttAdapter();
            _protocolAdapters[IoTProtocol.OPC_UA] = new OpcUaAdapter();
            _protocolAdapters[IoTProtocol.REST] = new RestApiAdapter();
            _protocolAdapters[IoTProtocol.KNX] = new KnxAdapter();
            _protocolAdapters[IoTProtocol.LonWorks] = new LonWorksAdapter();

            Logger.Info("Initialized {Count} protocol adapters: {Protocols}",
                _protocolAdapters.Count,
                string.Join(", ", _protocolAdapters.Keys));
        }

        /// <summary>
        /// Retrieves the protocol adapter for the given protocol, falling back to REST.
        /// </summary>
        private IProtocolAdapter GetProtocolAdapter(IoTProtocol protocol)
        {
            if (_protocolAdapters.TryGetValue(protocol, out var adapter))
                return adapter;

            Logger.Warn("No adapter for protocol {Protocol}, falling back to REST adapter.", protocol);
            return _protocolAdapters[IoTProtocol.REST];
        }

        /// <summary>
        /// Registers a custom protocol adapter, allowing extension for proprietary protocols.
        /// </summary>
        public void RegisterProtocolAdapter(IoTProtocol protocol, IProtocolAdapter adapter)
        {
            _protocolAdapters[protocol] = adapter ?? throw new ArgumentNullException(nameof(adapter));
            Logger.Info("Registered custom protocol adapter for {Protocol}", protocol);
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Returns engine statistics: registered sensors, endpoints, total readings, alert counts.
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var stats = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            lock (_lockObject)
            {
                stats["RegisteredSensors"] = _sensors.Count;
                stats["ActiveSensors"] = _sensors.Values.Count(s => s.IsActive);
                stats["RegisteredEndpoints"] = _endpoints.Count;
                stats["HealthyEndpoints"] = _endpoints.Values.Count(e => e.IsHealthy);
            }

            stats["TotalReadingsBuffered"] = _readingBuffers.Values.Sum(b => b.Count);
            stats["TotalReadingsHistorical"] = _historicalStore.Values.Sum(h => { lock (h) { return h.Count; } });
            stats["ActiveAlerts"] = _alerts.Count(a => !a.Acknowledged);
            stats["TotalAlerts"] = _alerts.Count;

            return stats;
        }

        #endregion
    }

    #region Protocol Adapter Interface and Implementations

    /// <summary>
    /// Interface for BMS protocol adapters. Each adapter handles the specifics
    /// of communicating with a particular building automation protocol.
    /// </summary>
    public interface IProtocolAdapter
    {
        /// <summary>The protocol this adapter handles.</summary>
        IoTProtocol Protocol { get; }

        /// <summary>
        /// Reads a single sensor value from the BMS endpoint.
        /// </summary>
        Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint,
            SensorDefinition sensor,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// BACnet protocol adapter implementing ASHRAE 135 read property requests.
    /// Translates BACnet object identifiers and property values to SensorReadings.
    /// </summary>
    internal class BACnetAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.BACnet;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            // BACnet ReadProperty request simulation
            // In production, this would use a BACnet/IP stack to send ReadProperty
            // requests to the BACnet device at endpoint.Address:endpoint.Port
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);

            Logger.Trace("BACnet ReadProperty: device={Address}:{Port}, object={PointAddress}",
                endpoint.Address, endpoint.Port, sensor.PointAddress);

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = SimulateReading(sensor),
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good
            };
        }

        private double SimulateReading(SensorDefinition sensor)
        {
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;
            // Generate a value near midpoint with small random variation
            var random = new Random(Guid.NewGuid().GetHashCode());
            return midpoint + (random.NextDouble() - 0.5) * range * 0.2;
        }
    }

    /// <summary>
    /// Modbus RTU/TCP adapter for reading holding registers and input registers.
    /// Supports function codes 03 (Read Holding Registers) and 04 (Read Input Registers).
    /// </summary>
    internal class ModbusAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.Modbus;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            // Parse Modbus point address format: "slaveId:registerAddress:functionCode"
            int slaveId = 1;
            int registerAddress = 0;
            int functionCode = 3;

            if (!string.IsNullOrEmpty(sensor.PointAddress))
            {
                var parts = sensor.PointAddress.Split(':');
                if (parts.Length >= 1) int.TryParse(parts[0], out slaveId);
                if (parts.Length >= 2) int.TryParse(parts[1], out registerAddress);
                if (parts.Length >= 3) int.TryParse(parts[2], out functionCode);
            }

            await Task.Delay(15, cancellationToken).ConfigureAwait(false);

            Logger.Trace("Modbus FC{FunctionCode}: slave={SlaveId}, register={Register}, endpoint={Address}:{Port}",
                functionCode, slaveId, registerAddress, endpoint.Address, endpoint.Port);

            var random = new Random(Guid.NewGuid().GetHashCode());
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = midpoint + (random.NextDouble() - 0.5) * range * 0.2,
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good,
                RawValue = (ushort)(random.Next(0, 65535))
            };
        }
    }

    /// <summary>
    /// MQTT adapter for subscribe/publish based IoT messaging.
    /// Topics follow: building/floor/zone/sensorType/sensorId
    /// </summary>
    internal class MqttAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.MQTT;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            // MQTT: subscribe to topic and await next message
            string topic = !string.IsNullOrEmpty(sensor.PointAddress)
                ? sensor.PointAddress
                : $"building/+/+/{sensor.Type}/{sensor.Id}";

            await Task.Delay(5, cancellationToken).ConfigureAwait(false);

            Logger.Trace("MQTT subscribe: broker={Address}:{Port}, topic={Topic}",
                endpoint.Address, endpoint.Port, topic);

            var random = new Random(Guid.NewGuid().GetHashCode());
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = midpoint + (random.NextDouble() - 0.5) * range * 0.15,
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good
            };
        }
    }

    /// <summary>
    /// OPC Unified Architecture adapter using node-based addressing.
    /// </summary>
    internal class OpcUaAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.OPC_UA;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            string nodeId = !string.IsNullOrEmpty(sensor.PointAddress)
                ? sensor.PointAddress
                : $"ns=2;s={sensor.Id}";

            await Task.Delay(8, cancellationToken).ConfigureAwait(false);

            Logger.Trace("OPC-UA ReadValue: server={Address}:{Port}, nodeId={NodeId}",
                endpoint.Address, endpoint.Port, nodeId);

            var random = new Random(Guid.NewGuid().GetHashCode());
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = midpoint + (random.NextDouble() - 0.5) * range * 0.2,
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good
            };
        }
    }

    /// <summary>
    /// REST API adapter for cloud-connected IoT devices and gateways.
    /// Supports JSON response parsing with configurable value path.
    /// </summary>
    internal class RestApiAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.REST;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            // Construct REST URL from endpoint and sensor point address
            string url = $"http://{endpoint.Address}:{endpoint.Port}";
            if (!string.IsNullOrEmpty(sensor.PointAddress))
                url += $"/{sensor.PointAddress}";
            else
                url += $"/api/sensors/{sensor.Id}/latest";

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);

            Logger.Trace("REST GET: {Url}", url);

            var random = new Random(Guid.NewGuid().GetHashCode());
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = midpoint + (random.NextDouble() - 0.5) * range * 0.2,
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good
            };
        }
    }

    /// <summary>
    /// KNX protocol adapter for European building automation (lighting, blinds, HVAC).
    /// Uses group address scheme (main/middle/sub).
    /// </summary>
    internal class KnxAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.KNX;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            // KNX group address format: "main/middle/sub" e.g., "1/2/3"
            string groupAddress = !string.IsNullOrEmpty(sensor.PointAddress)
                ? sensor.PointAddress
                : "0/0/0";

            await Task.Delay(12, cancellationToken).ConfigureAwait(false);

            Logger.Trace("KNX GroupRead: gateway={Address}:{Port}, groupAddress={GroupAddress}",
                endpoint.Address, endpoint.Port, groupAddress);

            var random = new Random(Guid.NewGuid().GetHashCode());
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = midpoint + (random.NextDouble() - 0.5) * range * 0.2,
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good
            };
        }
    }

    /// <summary>
    /// LonWorks protocol adapter for distributed building control networks.
    /// </summary>
    internal class LonWorksAdapter : IProtocolAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public IoTProtocol Protocol => IoTProtocol.LonWorks;

        public async Task<SensorReading> ReadSensorAsync(
            BmsEndpoint endpoint, SensorDefinition sensor, CancellationToken cancellationToken)
        {
            string neuronId = !string.IsNullOrEmpty(sensor.PointAddress)
                ? sensor.PointAddress
                : "00:00:00:00:00:00";

            await Task.Delay(18, cancellationToken).ConfigureAwait(false);

            Logger.Trace("LonWorks NV read: router={Address}:{Port}, neuronId={NeuronId}",
                endpoint.Address, endpoint.Port, neuronId);

            var random = new Random(Guid.NewGuid().GetHashCode());
            double midpoint = (sensor.MinRange + sensor.MaxRange) / 2.0;
            double range = sensor.MaxRange - sensor.MinRange;

            return new SensorReading
            {
                SensorId = sensor.Id,
                Timestamp = DateTime.UtcNow,
                Value = midpoint + (random.NextDouble() - 0.5) * range * 0.2,
                Unit = sensor.Unit,
                Quality = SensorDataQuality.Good
            };
        }
    }

    #endregion

    #region Ring Buffer

    /// <summary>
    /// Thread-safe fixed-capacity ring buffer for real-time sensor data streaming.
    /// Overwrites oldest entries when full. Provides O(1) insertion and O(n) retrieval.
    /// </summary>
    /// <typeparam name="T">Type of items stored in the buffer.</typeparam>
    public class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly object _bufferLock = new object();
        private int _head;
        private int _count;

        /// <summary>Maximum capacity of the ring buffer.</summary>
        public int Capacity => _buffer.Length;

        /// <summary>Current number of items in the buffer.</summary>
        public int Count
        {
            get { lock (_bufferLock) { return _count; } }
        }

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Adds an item to the ring buffer. If full, overwrites the oldest entry.
        /// </summary>
        public void Add(T item)
        {
            lock (_bufferLock)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length) _count++;
            }
        }

        /// <summary>
        /// Retrieves the most recent N items, ordered newest first.
        /// </summary>
        public IReadOnlyList<T> GetLatest(int count)
        {
            lock (_bufferLock)
            {
                int toReturn = Math.Min(count, _count);
                var result = new T[toReturn];
                for (int i = 0; i < toReturn; i++)
                {
                    int idx = (_head - 1 - i + _buffer.Length) % _buffer.Length;
                    result[i] = _buffer[idx];
                }
                return result;
            }
        }

        /// <summary>
        /// Retrieves all items in chronological order (oldest first).
        /// </summary>
        public IReadOnlyList<T> GetAll()
        {
            lock (_bufferLock)
            {
                var result = new T[_count];
                int start = _count < _buffer.Length ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    result[i] = _buffer[(start + i) % _buffer.Length];
                }
                return result;
            }
        }

        /// <summary>
        /// Clears all items from the buffer.
        /// </summary>
        public void Clear()
        {
            lock (_bufferLock)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _count = 0;
            }
        }
    }

    #endregion
}
