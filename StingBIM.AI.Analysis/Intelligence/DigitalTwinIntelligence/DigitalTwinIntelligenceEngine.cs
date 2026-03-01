// ============================================================================
// StingBIM Digital Twin Intelligence Engine
// IoT sensor integration, real-time monitoring, operational analytics
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DigitalTwinIntelligence
{
    #region Enums

    public enum DigitalTwinStatus
    {
        Active,
        Inactive,
        Maintenance,
        Synchronizing,
        Error,
        Initializing,
        Offline
    }

    public enum SensorType
    {
        Temperature,
        Humidity,
        Occupancy,
        CO2,
        Light,
        Motion,
        Pressure,
        Flow,
        Power,
        Voltage,
        Current,
        Vibration,
        Sound,
        AirQuality,
        WaterLevel,
        Smoke,
        Gas,
        Door,
        Window,
        HVAC,
        Custom
    }

    public enum SensorStatus
    {
        Online,
        Offline,
        LowBattery,
        Error,
        Calibrating,
        Maintenance,
        Unknown
    }

    public enum AlertSeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Info
    }

    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Resolved,
        Escalated,
        Suppressed
    }

    public enum MaintenancePredictionConfidence
    {
        VeryHigh,
        High,
        Medium,
        Low,
        VeryLow
    }

    public enum OperationalMetricType
    {
        EnergyConsumption,
        WaterConsumption,
        OccupancyRate,
        ComfortIndex,
        AirQualityIndex,
        EquipmentEfficiency,
        SystemUptime,
        ResponseTime,
        ThermalComfort,
        LightingEfficiency
    }

    public enum DataAggregationType
    {
        Average,
        Sum,
        Min,
        Max,
        Count,
        Median,
        StandardDeviation
    }

    public enum TrendDirection
    {
        Increasing,
        Decreasing,
        Stable,
        Fluctuating,
        Unknown
    }

    #endregion

    #region Data Models

    public class DigitalTwin
    {
        public string TwinId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string FacilityId { get; set; }
        public string ModelId { get; set; }
        public DigitalTwinStatus Status { get; set; }
        public TwinConfiguration Configuration { get; set; }
        public TwinMetadata Metadata { get; set; }
        public List<string> SensorIds { get; set; } = new List<string>();
        public List<string> AssetIds { get; set; } = new List<string>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastSyncDate { get; set; }
        public DateTime LastUpdatedDate { get; set; } = DateTime.UtcNow;
    }

    public class TwinConfiguration
    {
        public int SyncIntervalSeconds { get; set; } = 60;
        public int DataRetentionDays { get; set; } = 365;
        public bool EnableRealTimeUpdates { get; set; } = true;
        public bool EnablePredictiveMaintenance { get; set; } = true;
        public bool EnableAlerts { get; set; } = true;
        public double AlertThresholdMultiplier { get; set; } = 1.0;
        public List<string> EnabledFeatures { get; set; } = new List<string>();
    }

    public class TwinMetadata
    {
        public string Version { get; set; }
        public string ModelType { get; set; }
        public string ModelSource { get; set; }
        public double ModelAccuracy { get; set; }
        public DateTime ModelTrainedDate { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    public class Sensor
    {
        public string SensorId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public SensorType Type { get; set; }
        public SensorStatus Status { get; set; }
        public string TwinId { get; set; }
        public string AssetId { get; set; }
        public string SpaceId { get; set; }
        public SensorLocation Location { get; set; }
        public SensorSpecification Specification { get; set; }
        public SensorCalibration Calibration { get; set; }
        public SensorReading LastReading { get; set; }
        public List<SensorThreshold> Thresholds { get; set; } = new List<SensorThreshold>();
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public DateTime InstallDate { get; set; }
        public DateTime LastCommunication { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class SensorLocation
    {
        public string BuildingId { get; set; }
        public string FloorId { get; set; }
        public string ZoneId { get; set; }
        public string RoomId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string MountingType { get; set; }
    }

    public class SensorSpecification
    {
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string FirmwareVersion { get; set; }
        public string Protocol { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double Resolution { get; set; }
        public double Accuracy { get; set; }
        public string Unit { get; set; }
        public int SamplingRateMs { get; set; }
        public int BatteryCapacityPercent { get; set; }
    }

    public class SensorCalibration
    {
        public DateTime LastCalibrationDate { get; set; }
        public DateTime NextCalibrationDate { get; set; }
        public double CalibrationOffset { get; set; }
        public double CalibrationScale { get; set; }
        public string CalibratedBy { get; set; }
        public string CalibrationMethod { get; set; }
    }

    public class SensorReading
    {
        public string ReadingId { get; set; } = Guid.NewGuid().ToString();
        public string SensorId { get; set; }
        public double Value { get; set; }
        public double RawValue { get; set; }
        public string Unit { get; set; }
        public double Quality { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class SensorThreshold
    {
        public string ThresholdId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public AlertSeverity Severity { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string AlertMessage { get; set; }
        public int CooldownMinutes { get; set; } = 15;
    }

    public class SensorDataBatch
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public string SensorId { get; set; }
        public List<SensorReading> Readings { get; set; } = new List<SensorReading>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ReadingCount { get; set; }
        public DataStatistics Statistics { get; set; }
    }

    public class DataStatistics
    {
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Sum { get; set; }
        public double StandardDeviation { get; set; }
        public double Median { get; set; }
        public int Count { get; set; }
    }

    public class Alert
    {
        public string AlertId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public AlertSeverity Severity { get; set; }
        public AlertStatus Status { get; set; }
        public string TwinId { get; set; }
        public string SensorId { get; set; }
        public string AssetId { get; set; }
        public string ThresholdId { get; set; }
        public double TriggerValue { get; set; }
        public double ThresholdValue { get; set; }
        public string AlertRule { get; set; }
        public List<AlertAction> Actions { get; set; } = new List<AlertAction>();
        public List<AlertNote> Notes { get; set; } = new List<AlertNote>();
        public string AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedDate { get; set; }
        public string ResolvedBy { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class AlertAction
    {
        public string ActionId { get; set; } = Guid.NewGuid().ToString();
        public string ActionType { get; set; }
        public string Target { get; set; }
        public string Status { get; set; }
        public DateTime ExecutedDate { get; set; }
        public string Result { get; set; }
    }

    public class AlertNote
    {
        public string NoteId { get; set; } = Guid.NewGuid().ToString();
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class OperationalAnalytics
    {
        public string AnalyticsId { get; set; } = Guid.NewGuid().ToString();
        public string TwinId { get; set; }
        public DateTime AnalysisPeriodStart { get; set; }
        public DateTime AnalysisPeriodEnd { get; set; }
        public Dictionary<OperationalMetricType, MetricAnalysis> Metrics { get; set; } = new Dictionary<OperationalMetricType, MetricAnalysis>();
        public List<OperationalInsight> Insights { get; set; } = new List<OperationalInsight>();
        public List<AnomalyDetection> Anomalies { get; set; } = new List<AnomalyDetection>();
        public PerformanceScore OverallScore { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    }

    public class MetricAnalysis
    {
        public OperationalMetricType MetricType { get; set; }
        public double CurrentValue { get; set; }
        public double AverageValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double StandardDeviation { get; set; }
        public TrendDirection Trend { get; set; }
        public double TrendPercentage { get; set; }
        public double TargetValue { get; set; }
        public double VarianceFromTarget { get; set; }
        public string Unit { get; set; }
    }

    public class OperationalInsight
    {
        public string InsightId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public double ImpactScore { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<string> RelatedSensors { get; set; } = new List<string>();
    }

    public class AnomalyDetection
    {
        public string AnomalyId { get; set; } = Guid.NewGuid().ToString();
        public string SensorId { get; set; }
        public string AnomalyType { get; set; }
        public double AnomalyScore { get; set; }
        public double ExpectedValue { get; set; }
        public double ActualValue { get; set; }
        public DateTime DetectedDate { get; set; }
        public string Explanation { get; set; }
    }

    public class PerformanceScore
    {
        public double OverallScore { get; set; }
        public double EnergyScore { get; set; }
        public double ComfortScore { get; set; }
        public double EfficiencyScore { get; set; }
        public double ReliabilityScore { get; set; }
        public string Grade { get; set; }
        public List<string> StrengthAreas { get; set; } = new List<string>();
        public List<string> ImprovementAreas { get; set; } = new List<string>();
    }

    public class MaintenancePrediction
    {
        public string PredictionId { get; set; } = Guid.NewGuid().ToString();
        public string AssetId { get; set; }
        public string AssetName { get; set; }
        public string TwinId { get; set; }
        public MaintenancePredictionConfidence Confidence { get; set; }
        public double ConfidenceScore { get; set; }
        public DateTime PredictedFailureDate { get; set; }
        public int DaysUntilFailure { get; set; }
        public string FailureMode { get; set; }
        public string FailureReason { get; set; }
        public double FailureProbability { get; set; }
        public double RiskScore { get; set; }
        public double EstimatedDowntimeHours { get; set; }
        public double EstimatedRepairCost { get; set; }
        public List<string> ContributingFactors { get; set; } = new List<string>();
        public List<MaintenanceRecommendation> Recommendations { get; set; } = new List<MaintenanceRecommendation>();
        public List<string> SupportingEvidence { get; set; } = new List<string>();
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    }

    public class MaintenanceRecommendation
    {
        public string RecommendationId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public string ActionType { get; set; }
        public double EstimatedCost { get; set; }
        public double EstimatedDurationHours { get; set; }
        public double RiskReduction { get; set; }
        public DateTime RecommendedDate { get; set; }
    }

    public class DigitalTwinResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ResultId { get; set; }
        public object Data { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SensorDataResult
    {
        public string SensorId { get; set; }
        public bool Success { get; set; }
        public int ProcessedReadings { get; set; }
        public int FailedReadings { get; set; }
        public List<Alert> GeneratedAlerts { get; set; } = new List<Alert>();
        public DataStatistics Statistics { get; set; }
    }

    public class RealTimeMonitoringData
    {
        public string TwinId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, SensorReading> SensorReadings { get; set; } = new Dictionary<string, SensorReading>();
        public Dictionary<string, double> ComputedMetrics { get; set; } = new Dictionary<string, double>();
        public List<Alert> ActiveAlerts { get; set; } = new List<Alert>();
        public SystemHealthStatus HealthStatus { get; set; }
    }

    public class SystemHealthStatus
    {
        public string OverallStatus { get; set; }
        public int OnlineSensors { get; set; }
        public int OfflineSensors { get; set; }
        public int ActiveAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public double SystemUptime { get; set; }
        public double DataQuality { get; set; }
    }

    #endregion

    #region Engine

    public sealed class DigitalTwinIntelligenceEngine
    {
        private static readonly Lazy<DigitalTwinIntelligenceEngine> _instance =
            new Lazy<DigitalTwinIntelligenceEngine>(() => new DigitalTwinIntelligenceEngine());

        public static DigitalTwinIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, DigitalTwin> _twins;
        private readonly ConcurrentDictionary<string, Sensor> _sensors;
        private readonly ConcurrentDictionary<string, List<SensorReading>> _sensorHistory;
        private readonly ConcurrentDictionary<string, Alert> _alerts;
        private readonly ConcurrentDictionary<string, MaintenancePrediction> _predictions;
        private readonly ConcurrentDictionary<string, OperationalAnalytics> _analytics;
        private readonly object _syncLock = new object();

        private DigitalTwinIntelligenceEngine()
        {
            _twins = new ConcurrentDictionary<string, DigitalTwin>();
            _sensors = new ConcurrentDictionary<string, Sensor>();
            _sensorHistory = new ConcurrentDictionary<string, List<SensorReading>>();
            _alerts = new ConcurrentDictionary<string, Alert>();
            _predictions = new ConcurrentDictionary<string, MaintenancePrediction>();
            _analytics = new ConcurrentDictionary<string, OperationalAnalytics>();
        }

        #region Digital Twin Management

        public DigitalTwinResult CreateDigitalTwin(DigitalTwin twin)
        {
            if (twin == null)
                return new DigitalTwinResult { Success = false, Message = "Digital twin cannot be null" };

            if (string.IsNullOrEmpty(twin.TwinId))
                twin.TwinId = Guid.NewGuid().ToString();

            twin.CreatedDate = DateTime.UtcNow;
            twin.LastUpdatedDate = DateTime.UtcNow;
            twin.Status = DigitalTwinStatus.Initializing;
            twin.Configuration ??= new TwinConfiguration();
            twin.Metadata ??= new TwinMetadata { Version = "1.0" };

            if (_twins.TryAdd(twin.TwinId, twin))
            {
                twin.Status = DigitalTwinStatus.Active;
                return new DigitalTwinResult
                {
                    Success = true,
                    Message = "Digital twin created successfully",
                    ResultId = twin.TwinId,
                    Data = twin
                };
            }

            return new DigitalTwinResult { Success = false, Message = "Failed to create digital twin" };
        }

        public DigitalTwin GetDigitalTwin(string twinId)
        {
            _twins.TryGetValue(twinId, out var twin);
            return twin;
        }

        public List<DigitalTwin> GetAllDigitalTwins()
        {
            return _twins.Values.ToList();
        }

        public DigitalTwinResult UpdateDigitalTwinStatus(string twinId, DigitalTwinStatus status)
        {
            if (!_twins.TryGetValue(twinId, out var twin))
                return new DigitalTwinResult { Success = false, Message = "Digital twin not found" };

            var previousStatus = twin.Status;
            twin.Status = status;
            twin.LastUpdatedDate = DateTime.UtcNow;

            return new DigitalTwinResult
            {
                Success = true,
                Message = $"Status updated from {previousStatus} to {status}",
                ResultId = twinId,
                Data = twin
            };
        }

        public DigitalTwinResult SynchronizeDigitalTwin(string twinId)
        {
            if (!_twins.TryGetValue(twinId, out var twin))
                return new DigitalTwinResult { Success = false, Message = "Digital twin not found" };

            twin.Status = DigitalTwinStatus.Synchronizing;

            var sensors = _sensors.Values.Where(s => s.TwinId == twinId).ToList();
            var onlineSensors = sensors.Count(s => s.Status == SensorStatus.Online);
            var offlineSensors = sensors.Count(s => s.Status == SensorStatus.Offline);

            twin.LastSyncDate = DateTime.UtcNow;
            twin.LastUpdatedDate = DateTime.UtcNow;
            twin.Status = DigitalTwinStatus.Active;

            return new DigitalTwinResult
            {
                Success = true,
                Message = $"Synchronized {onlineSensors} sensors ({offlineSensors} offline)",
                ResultId = twinId,
                Data = new { OnlineSensors = onlineSensors, OfflineSensors = offlineSensors }
            };
        }

        #endregion

        #region Sensor Management

        public DigitalTwinResult RegisterSensor(Sensor sensor)
        {
            if (sensor == null)
                return new DigitalTwinResult { Success = false, Message = "Sensor cannot be null" };

            if (string.IsNullOrEmpty(sensor.SensorId))
                sensor.SensorId = Guid.NewGuid().ToString();

            sensor.CreatedDate = DateTime.UtcNow;
            sensor.Status = SensorStatus.Online;
            sensor.Location ??= new SensorLocation();
            sensor.Specification ??= new SensorSpecification();
            sensor.Calibration ??= new SensorCalibration();

            if (_sensors.TryAdd(sensor.SensorId, sensor))
            {
                _sensorHistory.TryAdd(sensor.SensorId, new List<SensorReading>());

                if (!string.IsNullOrEmpty(sensor.TwinId))
                {
                    var twin = GetDigitalTwin(sensor.TwinId);
                    if (twin != null && !twin.SensorIds.Contains(sensor.SensorId))
                    {
                        twin.SensorIds.Add(sensor.SensorId);
                    }
                }

                return new DigitalTwinResult
                {
                    Success = true,
                    Message = "Sensor registered successfully",
                    ResultId = sensor.SensorId,
                    Data = sensor
                };
            }

            return new DigitalTwinResult { Success = false, Message = "Failed to register sensor" };
        }

        public Sensor GetSensor(string sensorId)
        {
            _sensors.TryGetValue(sensorId, out var sensor);
            return sensor;
        }

        public List<Sensor> GetSensorsByTwin(string twinId)
        {
            return _sensors.Values.Where(s => s.TwinId == twinId).ToList();
        }

        public List<Sensor> GetSensorsByType(SensorType type)
        {
            return _sensors.Values.Where(s => s.Type == type).ToList();
        }

        public DigitalTwinResult UpdateSensorStatus(string sensorId, SensorStatus status)
        {
            if (!_sensors.TryGetValue(sensorId, out var sensor))
                return new DigitalTwinResult { Success = false, Message = "Sensor not found" };

            var previousStatus = sensor.Status;
            sensor.Status = status;
            sensor.LastCommunication = DateTime.UtcNow;

            if (status == SensorStatus.Offline && previousStatus == SensorStatus.Online)
            {
                CreateAlert(new Alert
                {
                    Name = $"Sensor Offline: {sensor.Name}",
                    Description = $"Sensor {sensor.Name} has gone offline",
                    Severity = AlertSeverity.High,
                    Status = AlertStatus.Active,
                    SensorId = sensorId,
                    TwinId = sensor.TwinId
                });
            }

            return new DigitalTwinResult
            {
                Success = true,
                Message = $"Sensor status updated from {previousStatus} to {status}",
                ResultId = sensorId,
                Data = sensor
            };
        }

        #endregion

        #region Sensor Data Processing

        public SensorDataResult ProcessSensorData(string sensorId, SensorReading reading)
        {
            var result = new SensorDataResult { SensorId = sensorId };

            if (!_sensors.TryGetValue(sensorId, out var sensor))
            {
                result.Success = false;
                result.FailedReadings = 1;
                return result;
            }

            reading.SensorId = sensorId;
            reading.Timestamp = reading.Timestamp == default ? DateTime.UtcNow : reading.Timestamp;

            if (sensor.Calibration != null)
            {
                reading.RawValue = reading.Value;
                reading.Value = (reading.Value * sensor.Calibration.CalibrationScale) + sensor.Calibration.CalibrationOffset;
            }

            reading.Unit = sensor.Specification?.Unit ?? reading.Unit;

            sensor.LastReading = reading;
            sensor.LastCommunication = DateTime.UtcNow;
            sensor.Status = SensorStatus.Online;

            if (_sensorHistory.TryGetValue(sensorId, out var history))
            {
                lock (_syncLock)
                {
                    history.Add(reading);
                    if (history.Count > 10000)
                    {
                        history.RemoveRange(0, history.Count - 10000);
                    }
                }
            }

            result.GeneratedAlerts = CheckThresholds(sensor, reading);
            result.ProcessedReadings = 1;
            result.Success = true;

            return result;
        }

        public SensorDataResult ProcessSensorDataBatch(string sensorId, SensorDataBatch batch)
        {
            var result = new SensorDataResult { SensorId = sensorId };

            if (!_sensors.TryGetValue(sensorId, out var sensor))
            {
                result.Success = false;
                result.FailedReadings = batch.Readings?.Count ?? 0;
                return result;
            }

            foreach (var reading in batch.Readings ?? new List<SensorReading>())
            {
                var singleResult = ProcessSensorData(sensorId, reading);
                result.ProcessedReadings += singleResult.ProcessedReadings;
                result.FailedReadings += singleResult.FailedReadings;
                result.GeneratedAlerts.AddRange(singleResult.GeneratedAlerts);
            }

            if (_sensorHistory.TryGetValue(sensorId, out var history) && history.Count > 0)
            {
                result.Statistics = CalculateStatistics(history.TakeLast(batch.Readings?.Count ?? 100).ToList());
            }

            result.Success = result.FailedReadings == 0;
            return result;
        }

        public List<SensorReading> GetSensorHistory(string sensorId, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!_sensorHistory.TryGetValue(sensorId, out var history))
                return new List<SensorReading>();

            var start = startDate ?? DateTime.MinValue;
            var end = endDate ?? DateTime.MaxValue;

            return history.Where(r => r.Timestamp >= start && r.Timestamp <= end).ToList();
        }

        public DataStatistics GetSensorStatistics(string sensorId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var readings = GetSensorHistory(sensorId, startDate, endDate);
            return CalculateStatistics(readings);
        }

        private DataStatistics CalculateStatistics(List<SensorReading> readings)
        {
            if (readings == null || readings.Count == 0)
                return new DataStatistics();

            var values = readings.Select(r => r.Value).ToList();
            var sortedValues = values.OrderBy(v => v).ToList();

            return new DataStatistics
            {
                Count = values.Count,
                Sum = values.Sum(),
                Average = values.Average(),
                Min = values.Min(),
                Max = values.Max(),
                Median = sortedValues.Count % 2 == 0
                    ? (sortedValues[sortedValues.Count / 2 - 1] + sortedValues[sortedValues.Count / 2]) / 2
                    : sortedValues[sortedValues.Count / 2],
                StandardDeviation = CalculateStandardDeviation(values)
            };
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / (values.Count - 1));
        }

        private List<Alert> CheckThresholds(Sensor sensor, SensorReading reading)
        {
            var alerts = new List<Alert>();

            foreach (var threshold in sensor.Thresholds.Where(t => t.IsEnabled))
            {
                bool triggered = false;
                string condition = "";

                if (reading.Value < threshold.MinValue)
                {
                    triggered = true;
                    condition = $"below minimum ({reading.Value} < {threshold.MinValue})";
                }
                else if (reading.Value > threshold.MaxValue)
                {
                    triggered = true;
                    condition = $"above maximum ({reading.Value} > {threshold.MaxValue})";
                }

                if (triggered)
                {
                    var alert = new Alert
                    {
                        Name = threshold.Name ?? $"Threshold Alert: {sensor.Name}",
                        Description = threshold.AlertMessage ?? $"Sensor {sensor.Name} value {condition}",
                        Severity = threshold.Severity,
                        Status = AlertStatus.Active,
                        SensorId = sensor.SensorId,
                        TwinId = sensor.TwinId,
                        ThresholdId = threshold.ThresholdId,
                        TriggerValue = reading.Value,
                        ThresholdValue = reading.Value < threshold.MinValue ? threshold.MinValue : threshold.MaxValue
                    };

                    alerts.Add(alert);
                    CreateAlert(alert);
                }
            }

            return alerts;
        }

        #endregion

        #region Alert Management

        public DigitalTwinResult CreateAlert(Alert alert)
        {
            if (alert == null)
                return new DigitalTwinResult { Success = false, Message = "Alert cannot be null" };

            if (string.IsNullOrEmpty(alert.AlertId))
                alert.AlertId = Guid.NewGuid().ToString();

            alert.CreatedDate = DateTime.UtcNow;
            alert.Status = AlertStatus.Active;

            if (_alerts.TryAdd(alert.AlertId, alert))
            {
                return new DigitalTwinResult
                {
                    Success = true,
                    Message = "Alert created",
                    ResultId = alert.AlertId,
                    Data = alert
                };
            }

            return new DigitalTwinResult { Success = false, Message = "Failed to create alert" };
        }

        public List<Alert> GetActiveAlerts(string twinId = null)
        {
            var query = _alerts.Values.Where(a => a.Status == AlertStatus.Active);
            if (!string.IsNullOrEmpty(twinId))
                query = query.Where(a => a.TwinId == twinId);
            return query.OrderByDescending(a => a.Severity).ThenByDescending(a => a.CreatedDate).ToList();
        }

        public DigitalTwinResult AcknowledgeAlert(string alertId, string acknowledgedBy)
        {
            if (!_alerts.TryGetValue(alertId, out var alert))
                return new DigitalTwinResult { Success = false, Message = "Alert not found" };

            alert.Status = AlertStatus.Acknowledged;
            alert.AcknowledgedBy = acknowledgedBy;
            alert.AcknowledgedDate = DateTime.UtcNow;

            return new DigitalTwinResult
            {
                Success = true,
                Message = "Alert acknowledged",
                ResultId = alertId,
                Data = alert
            };
        }

        public DigitalTwinResult ResolveAlert(string alertId, string resolvedBy, string resolution = null)
        {
            if (!_alerts.TryGetValue(alertId, out var alert))
                return new DigitalTwinResult { Success = false, Message = "Alert not found" };

            alert.Status = AlertStatus.Resolved;
            alert.ResolvedBy = resolvedBy;
            alert.ResolvedDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(resolution))
            {
                alert.Notes.Add(new AlertNote
                {
                    Author = resolvedBy,
                    Content = resolution
                });
            }

            return new DigitalTwinResult
            {
                Success = true,
                Message = "Alert resolved",
                ResultId = alertId,
                Data = alert
            };
        }

        #endregion

        #region Operational Analytics

        public OperationalAnalytics AnalyzeOperations(string twinId, DateTime startDate, DateTime endDate)
        {
            var twin = GetDigitalTwin(twinId);
            if (twin == null) return null;

            var analytics = new OperationalAnalytics
            {
                TwinId = twinId,
                AnalysisPeriodStart = startDate,
                AnalysisPeriodEnd = endDate
            };

            var sensors = GetSensorsByTwin(twinId);

            foreach (var metricType in Enum.GetValues<OperationalMetricType>())
            {
                analytics.Metrics[metricType] = AnalyzeMetric(metricType, sensors, startDate, endDate);
            }

            analytics.Insights = GenerateInsights(analytics, sensors);
            analytics.Anomalies = DetectAnomalies(sensors, startDate, endDate);
            analytics.OverallScore = CalculatePerformanceScore(analytics);

            _analytics.TryAdd(analytics.AnalyticsId, analytics);

            return analytics;
        }

        private MetricAnalysis AnalyzeMetric(OperationalMetricType metricType, List<Sensor> sensors, DateTime startDate, DateTime endDate)
        {
            var relevantSensors = GetSensorsForMetric(metricType, sensors);
            var allReadings = new List<double>();

            foreach (var sensor in relevantSensors)
            {
                var history = GetSensorHistory(sensor.SensorId, startDate, endDate);
                allReadings.AddRange(history.Select(r => r.Value));
            }

            if (allReadings.Count == 0)
            {
                return new MetricAnalysis
                {
                    MetricType = metricType,
                    Trend = TrendDirection.Unknown
                };
            }

            var stats = CalculateStatistics(allReadings.Select(v => new SensorReading { Value = v }).ToList());
            var trend = CalculateTrend(allReadings);

            return new MetricAnalysis
            {
                MetricType = metricType,
                CurrentValue = allReadings.LastOrDefault(),
                AverageValue = stats.Average,
                MinValue = stats.Min,
                MaxValue = stats.Max,
                StandardDeviation = stats.StandardDeviation,
                Trend = trend.Direction,
                TrendPercentage = trend.Percentage
            };
        }

        private List<Sensor> GetSensorsForMetric(OperationalMetricType metricType, List<Sensor> sensors)
        {
            return metricType switch
            {
                OperationalMetricType.EnergyConsumption => sensors.Where(s => s.Type == SensorType.Power || s.Type == SensorType.Current).ToList(),
                OperationalMetricType.OccupancyRate => sensors.Where(s => s.Type == SensorType.Occupancy || s.Type == SensorType.Motion).ToList(),
                OperationalMetricType.ComfortIndex => sensors.Where(s => s.Type == SensorType.Temperature || s.Type == SensorType.Humidity).ToList(),
                OperationalMetricType.AirQualityIndex => sensors.Where(s => s.Type == SensorType.CO2 || s.Type == SensorType.AirQuality).ToList(),
                OperationalMetricType.ThermalComfort => sensors.Where(s => s.Type == SensorType.Temperature).ToList(),
                OperationalMetricType.LightingEfficiency => sensors.Where(s => s.Type == SensorType.Light).ToList(),
                _ => sensors
            };
        }

        private (TrendDirection Direction, double Percentage) CalculateTrend(List<double> values)
        {
            if (values.Count < 2)
                return (TrendDirection.Unknown, 0);

            var firstHalf = values.Take(values.Count / 2).Average();
            var secondHalf = values.Skip(values.Count / 2).Average();

            var percentage = firstHalf != 0 ? ((secondHalf - firstHalf) / firstHalf) * 100 : 0;

            var direction = percentage switch
            {
                > 5 => TrendDirection.Increasing,
                < -5 => TrendDirection.Decreasing,
                _ => TrendDirection.Stable
            };

            return (direction, Math.Abs(percentage));
        }

        private List<OperationalInsight> GenerateInsights(OperationalAnalytics analytics, List<Sensor> sensors)
        {
            var insights = new List<OperationalInsight>();

            if (analytics.Metrics.TryGetValue(OperationalMetricType.EnergyConsumption, out var energy))
            {
                if (energy.Trend == TrendDirection.Increasing && energy.TrendPercentage > 10)
                {
                    insights.Add(new OperationalInsight
                    {
                        Title = "Rising Energy Consumption",
                        Description = $"Energy consumption has increased by {energy.TrendPercentage:F1}% during the analysis period",
                        Category = "Energy",
                        ImpactScore = 0.8,
                        ConfidenceScore = 0.9,
                        Recommendations = new List<string>
                        {
                            "Review HVAC scheduling and setpoints",
                            "Check for equipment running during unoccupied hours",
                            "Consider energy audit for major systems"
                        }
                    });
                }
            }

            if (analytics.Metrics.TryGetValue(OperationalMetricType.ComfortIndex, out var comfort))
            {
                if (comfort.StandardDeviation > 2)
                {
                    insights.Add(new OperationalInsight
                    {
                        Title = "Temperature Variability Detected",
                        Description = "Significant temperature variations detected across monitored spaces",
                        Category = "Comfort",
                        ImpactScore = 0.6,
                        ConfidenceScore = 0.85,
                        Recommendations = new List<string>
                        {
                            "Check HVAC zone balancing",
                            "Verify thermostat calibration",
                            "Review air distribution patterns"
                        }
                    });
                }
            }

            return insights;
        }

        private List<AnomalyDetection> DetectAnomalies(List<Sensor> sensors, DateTime startDate, DateTime endDate)
        {
            var anomalies = new List<AnomalyDetection>();

            foreach (var sensor in sensors)
            {
                var history = GetSensorHistory(sensor.SensorId, startDate, endDate);
                if (history.Count < 10) continue;

                var stats = CalculateStatistics(history);
                var threshold = stats.StandardDeviation * 3;

                foreach (var reading in history.Where(r => Math.Abs(r.Value - stats.Average) > threshold))
                {
                    anomalies.Add(new AnomalyDetection
                    {
                        SensorId = sensor.SensorId,
                        AnomalyType = "Statistical Outlier",
                        AnomalyScore = Math.Abs(reading.Value - stats.Average) / stats.StandardDeviation,
                        ExpectedValue = stats.Average,
                        ActualValue = reading.Value,
                        DetectedDate = reading.Timestamp,
                        Explanation = $"Value {reading.Value:F2} deviates significantly from average {stats.Average:F2}"
                    });
                }
            }

            return anomalies.OrderByDescending(a => a.AnomalyScore).Take(20).ToList();
        }

        private PerformanceScore CalculatePerformanceScore(OperationalAnalytics analytics)
        {
            var score = new PerformanceScore();
            var scores = new List<double>();

            foreach (var metric in analytics.Metrics.Values)
            {
                var metricScore = CalculateMetricScore(metric);
                scores.Add(metricScore);
            }

            score.OverallScore = scores.Count > 0 ? scores.Average() : 50;
            score.EnergyScore = analytics.Metrics.TryGetValue(OperationalMetricType.EnergyConsumption, out var e) ? CalculateMetricScore(e) : 50;
            score.ComfortScore = analytics.Metrics.TryGetValue(OperationalMetricType.ComfortIndex, out var c) ? CalculateMetricScore(c) : 50;
            score.EfficiencyScore = analytics.Metrics.TryGetValue(OperationalMetricType.EquipmentEfficiency, out var ef) ? CalculateMetricScore(ef) : 50;
            score.ReliabilityScore = analytics.Metrics.TryGetValue(OperationalMetricType.SystemUptime, out var r) ? CalculateMetricScore(r) : 50;

            score.Grade = score.OverallScore >= 90 ? "A" :
                          score.OverallScore >= 80 ? "B" :
                          score.OverallScore >= 70 ? "C" :
                          score.OverallScore >= 60 ? "D" : "F";

            return score;
        }

        private double CalculateMetricScore(MetricAnalysis metric)
        {
            if (metric.TargetValue > 0)
            {
                var variance = Math.Abs(metric.CurrentValue - metric.TargetValue) / metric.TargetValue;
                return Math.Max(0, 100 - (variance * 100));
            }
            return 70;
        }

        #endregion

        #region Predictive Maintenance

        public MaintenancePrediction PredictMaintenance(string assetId, string twinId)
        {
            var twin = GetDigitalTwin(twinId);
            if (twin == null) return null;

            var sensors = _sensors.Values
                .Where(s => s.TwinId == twinId && s.AssetId == assetId)
                .ToList();

            var prediction = new MaintenancePrediction
            {
                AssetId = assetId,
                TwinId = twinId
            };

            var healthIndicators = new List<double>();
            var contributingFactors = new List<string>();

            foreach (var sensor in sensors)
            {
                var history = GetSensorHistory(sensor.SensorId, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow);
                if (history.Count < 10) continue;

                var stats = CalculateStatistics(history);
                var trend = CalculateTrend(history.Select(r => r.Value).ToList());

                var degradationRate = CalculateDegradationRate(history);
                healthIndicators.Add(100 - degradationRate);

                if (degradationRate > 20)
                {
                    contributingFactors.Add($"{sensor.Name}: {degradationRate:F1}% degradation detected");
                }

                if (sensor.Type == SensorType.Vibration && trend.Direction == TrendDirection.Increasing)
                {
                    contributingFactors.Add($"Increasing vibration levels on {sensor.Name}");
                    prediction.FailureMode = "Mechanical Wear";
                }

                if (sensor.Type == SensorType.Temperature && stats.Max > (sensor.Specification?.MaxValue ?? 100) * 0.9)
                {
                    contributingFactors.Add($"Temperature approaching limits on {sensor.Name}");
                    prediction.FailureMode = "Overheating";
                }
            }

            if (healthIndicators.Count > 0)
            {
                var avgHealth = healthIndicators.Average();
                prediction.ConfidenceScore = Math.Min(0.95, sensors.Count * 0.15);
                prediction.FailureProbability = Math.Max(0, (100 - avgHealth) / 100);
                prediction.RiskScore = prediction.FailureProbability * 100;

                var daysUntilFailure = (int)(avgHealth / 0.5);
                prediction.DaysUntilFailure = Math.Max(1, daysUntilFailure);
                prediction.PredictedFailureDate = DateTime.UtcNow.AddDays(prediction.DaysUntilFailure);

                prediction.Confidence = prediction.ConfidenceScore >= 0.8 ? MaintenancePredictionConfidence.High :
                                        prediction.ConfidenceScore >= 0.6 ? MaintenancePredictionConfidence.Medium :
                                        prediction.ConfidenceScore >= 0.4 ? MaintenancePredictionConfidence.Low :
                                        MaintenancePredictionConfidence.VeryLow;
            }

            prediction.ContributingFactors = contributingFactors;
            prediction.FailureReason = contributingFactors.Count > 0 ? string.Join("; ", contributingFactors.Take(3)) : "Normal wear and tear";

            prediction.Recommendations = GenerateMaintenanceRecommendations(prediction);

            _predictions.TryAdd(prediction.PredictionId, prediction);

            return prediction;
        }

        private double CalculateDegradationRate(List<SensorReading> history)
        {
            if (history.Count < 10) return 0;

            var firstQuarter = history.Take(history.Count / 4).Select(r => r.Value).Average();
            var lastQuarter = history.Skip(history.Count * 3 / 4).Select(r => r.Value).Average();

            if (firstQuarter == 0) return 0;

            return Math.Abs((lastQuarter - firstQuarter) / firstQuarter * 100);
        }

        private List<MaintenanceRecommendation> GenerateMaintenanceRecommendations(MaintenancePrediction prediction)
        {
            var recommendations = new List<MaintenanceRecommendation>();

            if (prediction.FailureProbability > 0.7)
            {
                recommendations.Add(new MaintenanceRecommendation
                {
                    Title = "Immediate Inspection Required",
                    Description = "High failure probability detected. Schedule immediate inspection.",
                    Priority = "Critical",
                    ActionType = "Inspection",
                    EstimatedDurationHours = 2,
                    RecommendedDate = DateTime.UtcNow.AddDays(1),
                    RiskReduction = 0.5
                });
            }

            if (prediction.FailureMode == "Mechanical Wear")
            {
                recommendations.Add(new MaintenanceRecommendation
                {
                    Title = "Component Replacement",
                    Description = "Replace worn mechanical components before failure",
                    Priority = "High",
                    ActionType = "Replacement",
                    EstimatedDurationHours = 4,
                    RecommendedDate = DateTime.UtcNow.AddDays(prediction.DaysUntilFailure / 2),
                    RiskReduction = 0.8
                });
            }

            if (prediction.FailureMode == "Overheating")
            {
                recommendations.Add(new MaintenanceRecommendation
                {
                    Title = "Cooling System Check",
                    Description = "Inspect and clean cooling systems to prevent overheating",
                    Priority = "High",
                    ActionType = "Maintenance",
                    EstimatedDurationHours = 2,
                    RecommendedDate = DateTime.UtcNow.AddDays(3),
                    RiskReduction = 0.6
                });
            }

            recommendations.Add(new MaintenanceRecommendation
            {
                Title = "Scheduled Preventive Maintenance",
                Description = "Perform regular preventive maintenance to extend asset life",
                Priority = "Medium",
                ActionType = "Preventive",
                EstimatedDurationHours = 3,
                RecommendedDate = DateTime.UtcNow.AddDays(Math.Min(30, prediction.DaysUntilFailure)),
                RiskReduction = 0.3
            });

            return recommendations;
        }

        public List<MaintenancePrediction> GetMaintenancePredictions(string twinId)
        {
            return _predictions.Values
                .Where(p => p.TwinId == twinId)
                .OrderByDescending(p => p.RiskScore)
                .ToList();
        }

        #endregion

        #region Real-Time Monitoring

        public RealTimeMonitoringData GetRealTimeData(string twinId)
        {
            var twin = GetDigitalTwin(twinId);
            if (twin == null) return null;

            var data = new RealTimeMonitoringData
            {
                TwinId = twinId,
                Timestamp = DateTime.UtcNow
            };

            var sensors = GetSensorsByTwin(twinId);
            foreach (var sensor in sensors)
            {
                if (sensor.LastReading != null)
                {
                    data.SensorReadings[sensor.SensorId] = sensor.LastReading;
                }
            }

            data.ActiveAlerts = GetActiveAlerts(twinId);

            data.HealthStatus = new SystemHealthStatus
            {
                OverallStatus = data.ActiveAlerts.Any(a => a.Severity == AlertSeverity.Critical) ? "Critical" :
                                data.ActiveAlerts.Any(a => a.Severity == AlertSeverity.High) ? "Warning" : "Normal",
                OnlineSensors = sensors.Count(s => s.Status == SensorStatus.Online),
                OfflineSensors = sensors.Count(s => s.Status == SensorStatus.Offline),
                ActiveAlerts = data.ActiveAlerts.Count,
                CriticalAlerts = data.ActiveAlerts.Count(a => a.Severity == AlertSeverity.Critical),
                SystemUptime = 99.5,
                DataQuality = sensors.Count > 0 ? (double)sensors.Count(s => s.LastReading != null) / sensors.Count * 100 : 0
            };

            return data;
        }

        #endregion

        #region Utility Methods

        public void ClearAllData()
        {
            lock (_syncLock)
            {
                _twins.Clear();
                _sensors.Clear();
                _sensorHistory.Clear();
                _alerts.Clear();
                _predictions.Clear();
                _analytics.Clear();
            }
        }

        #endregion
    }

    #endregion
}
