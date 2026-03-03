// StingBIM.AI.IoT.Models.IoTModels
// Core data models for IoT sensor integration, digital twin state management,
// BMS protocol endpoints, and specialized meter readings.
// Supports ISO 16484 (Building Automation), ISO 50001 (Energy Management),
// and ASHRAE Guideline 36 high-performance sequences of operation.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StingBIM.AI.IoT.Models
{
    #region Enumerations

    /// <summary>
    /// Data quality classification for sensor readings following ISO 16484 conventions.
    /// Quality is assessed through range validation, rate-of-change checks, and staleness detection.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SensorDataQuality
    {
        /// <summary>Reading within expected range with valid calibration.</summary>
        Good = 0,

        /// <summary>Reading within range but sensor needs recalibration or has minor issues.</summary>
        Uncertain = 1,

        /// <summary>Reading out of range or sensor communication failure detected.</summary>
        Bad = 2,

        /// <summary>Reading has not been updated within the expected polling interval.</summary>
        Stale = 3
    }

    /// <summary>
    /// Communication protocols supported for Building Management System integration.
    /// Each protocol has distinct addressing, authentication, and data encoding schemes.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IoTProtocol
    {
        /// <summary>BACnet (ASHRAE 135) - primary BAS protocol for HVAC, lighting, access.</summary>
        BACnet = 0,

        /// <summary>Modbus RTU/TCP - legacy industrial protocol for meters and controllers.</summary>
        Modbus = 1,

        /// <summary>MQTT - lightweight publish/subscribe for IoT gateways and edge devices.</summary>
        MQTT = 2,

        /// <summary>OPC Unified Architecture - industrial interoperability standard.</summary>
        OPC_UA = 3,

        /// <summary>REST/HTTP API - web service endpoints for cloud-connected devices.</summary>
        REST = 4,

        /// <summary>KNX - European standard for building automation (lighting, blinds, HVAC).</summary>
        KNX = 5,

        /// <summary>LonWorks - distributed control networking for building systems.</summary>
        LonWorks = 6
    }

    /// <summary>
    /// Classification of sensor types found in modern smart buildings.
    /// Categories align with ASHRAE Guideline 36 monitoring point types.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SensorCategory
    {
        /// <summary>Temperature sensors (air, surface, pipe, outdoor) in degrees C or F.</summary>
        Temperature = 0,

        /// <summary>Relative humidity sensors, expressed as percentage (0-100%).</summary>
        Humidity = 1,

        /// <summary>CO2 concentration sensors in parts per million (ppm).</summary>
        CO2 = 2,

        /// <summary>Occupancy detection sensors (PIR, ultrasonic, camera-based).</summary>
        Occupancy = 3,

        /// <summary>Illuminance sensors measuring light levels in lux.</summary>
        Light = 4,

        /// <summary>Electrical power meters measuring kW, kWh, power factor.</summary>
        Power = 5,

        /// <summary>Water flow meters measuring liters, flow rate, pressure.</summary>
        Water = 6,

        /// <summary>Pressure sensors for duct static, pipe, or room differential (Pa).</summary>
        Pressure = 7,

        /// <summary>Vibration sensors for rotating equipment health (mm/s RMS).</summary>
        Vibration = 8,

        /// <summary>Acoustic sensors measuring sound pressure level in dB(A).</summary>
        Acoustic = 9,

        /// <summary>Smoke detectors (photoelectric, ionization) returning alarm state.</summary>
        Smoke = 10,

        /// <summary>Gas detection sensors (CO, NOx, VOC, refrigerant leak).</summary>
        Gas = 11,

        /// <summary>Motion/presence sensors for security and lighting control.</summary>
        Motion = 12,

        /// <summary>Door/window contact sensors reporting open/closed state.</summary>
        Door = 13
    }

    /// <summary>
    /// Alert severity levels for IoT threshold violations and anomaly detections.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AlertSeverity
    {
        /// <summary>Informational notification, no action required.</summary>
        Info = 0,

        /// <summary>Warning condition that may require attention if persistent.</summary>
        Warning = 1,

        /// <summary>Critical condition requiring prompt investigation.</summary>
        Critical = 2,

        /// <summary>Emergency condition requiring immediate response (life safety).</summary>
        Emergency = 3
    }

    /// <summary>
    /// Clash classification for MEP interference detection.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClashType
    {
        /// <summary>Physical geometry intersection between elements.</summary>
        Hard = 0,

        /// <summary>Clearance zone overlap (insulation, access, maintenance).</summary>
        Soft = 1,

        /// <summary>Minimum clearance requirement violation per code.</summary>
        Clearance = 2
    }

    /// <summary>
    /// MEP discipline classification for clash detection priority.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MepDiscipline
    {
        Structural = 0,
        Plumbing = 1,
        FireProtection = 2,
        HVAC = 3,
        Electrical = 4,
        LowVoltage = 5
    }

    /// <summary>
    /// Commissioning phase tracking per ASHRAE Guideline 0.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommissioningPhase
    {
        PreDesign = 0,
        Design = 1,
        Construction = 2,
        PreFunctional = 3,
        Functional = 4,
        Seasonal = 5,
        Ongoing = 6
    }

    /// <summary>
    /// Status of a commissioning test or checklist item.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TestStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Passed = 2,
        Failed = 3,
        Deferred = 4,
        NotApplicable = 5
    }

    #endregion

    #region Core Sensor Models

    /// <summary>
    /// Represents a single sensor reading captured from a BMS endpoint.
    /// Includes timestamp, raw value, engineering unit, and data quality assessment.
    /// </summary>
    public class SensorReading
    {
        /// <summary>UTC timestamp when the reading was captured at the sensor.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Unique identifier of the sensor that produced this reading.</summary>
        public string SensorId { get; set; } = string.Empty;

        /// <summary>Numeric value of the reading in the sensor's engineering unit.</summary>
        public double Value { get; set; }

        /// <summary>Engineering unit string (e.g., "degC", "ppm", "lux", "Pa", "kW").</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Data quality assessment based on validation checks.</summary>
        public SensorDataQuality Quality { get; set; } = SensorDataQuality.Good;

        /// <summary>Optional raw value before engineering unit conversion.</summary>
        public double? RawValue { get; set; }

        /// <summary>Metadata tags for additional context (zone, floor, system).</summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Validates the reading against basic sanity checks.
        /// Returns true if the reading appears reasonable.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(SensorId)) return false;
            if (Timestamp == default) return false;
            if (double.IsNaN(Value) || double.IsInfinity(Value)) return false;
            return true;
        }

        public override string ToString() =>
            $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {SensorId}: {Value:F2} {Unit} ({Quality})";
    }

    /// <summary>
    /// Complete definition of a sensor including its type, location, calibration,
    /// and binding to a Revit element for digital twin synchronization.
    /// </summary>
    public class SensorDefinition
    {
        /// <summary>Unique sensor identifier (typically from BMS point naming convention).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Human-readable sensor name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Sensor category classification.</summary>
        public SensorCategory Type { get; set; }

        /// <summary>Engineering unit for readings from this sensor.</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Physical location description (building/floor/zone/room).</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>Revit element UniqueId that this sensor is associated with.</summary>
        public string ElementId { get; set; } = string.Empty;

        /// <summary>Date of last sensor calibration.</summary>
        public DateTime CalibrationDate { get; set; }

        /// <summary>Sensor accuracy as percentage of full scale (e.g., 0.5 = 0.5%).</summary>
        public double Accuracy { get; set; }

        /// <summary>Minimum expected reading value for range validation.</summary>
        public double MinRange { get; set; }

        /// <summary>Maximum expected reading value for range validation.</summary>
        public double MaxRange { get; set; }

        /// <summary>Maximum acceptable rate of change per minute for anomaly detection.</summary>
        public double MaxRateOfChange { get; set; }

        /// <summary>Maximum seconds between readings before marking as Stale.</summary>
        public int StaleThresholdSeconds { get; set; } = 300;

        /// <summary>Alert thresholds: low-low, low, high, high-high.</summary>
        public AlertThresholds Thresholds { get; set; } = new AlertThresholds();

        /// <summary>Protocol-specific address or point name for polling.</summary>
        public string PointAddress { get; set; } = string.Empty;

        /// <summary>Whether this sensor is currently active and should be polled.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Checks whether calibration is still valid (within 12 months).
        /// </summary>
        public bool IsCalibrationCurrent() =>
            CalibrationDate != default && (DateTime.UtcNow - CalibrationDate).TotalDays <= 365;

        public override string ToString() => $"{Id} ({Name}) [{Type}] @ {Location}";
    }

    /// <summary>
    /// Four-level alert thresholds for a sensor (low-low, low, high, high-high).
    /// Null values indicate that threshold level is not configured.
    /// </summary>
    public class AlertThresholds
    {
        public double? LowLow { get; set; }
        public double? Low { get; set; }
        public double? High { get; set; }
        public double? HighHigh { get; set; }

        /// <summary>
        /// Evaluates a reading value against the configured thresholds.
        /// Returns null if no threshold is violated, otherwise the severity.
        /// </summary>
        public AlertSeverity? Evaluate(double value)
        {
            if (HighHigh.HasValue && value >= HighHigh.Value) return AlertSeverity.Emergency;
            if (LowLow.HasValue && value <= LowLow.Value) return AlertSeverity.Emergency;
            if (High.HasValue && value >= High.Value) return AlertSeverity.Critical;
            if (Low.HasValue && value <= Low.Value) return AlertSeverity.Critical;
            return null;
        }
    }

    #endregion

    #region BMS Endpoint

    /// <summary>
    /// Configuration for a Building Management System communication endpoint.
    /// Defines the protocol, network address, credentials, and polling parameters.
    /// </summary>
    public class BmsEndpoint
    {
        /// <summary>Unique endpoint identifier.</summary>
        public string EndpointId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>Communication protocol used by this endpoint.</summary>
        public IoTProtocol Protocol { get; set; }

        /// <summary>Network address (IP, hostname, or URL).</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>Network port number.</summary>
        public int Port { get; set; }

        /// <summary>Authentication username (if required).</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Authentication password or API key (if required).</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Polling interval in milliseconds.</summary>
        public int PollIntervalMs { get; set; } = 5000;

        /// <summary>Connection timeout in milliseconds.</summary>
        public int TimeoutMs { get; set; } = 10000;

        /// <summary>Maximum number of retry attempts on communication failure.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Whether this endpoint is currently enabled for polling.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Last successful communication timestamp.</summary>
        public DateTime? LastSuccessfulPoll { get; set; }

        /// <summary>Consecutive failure count for circuit breaker logic.</summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>Sensor IDs served by this endpoint.</summary>
        public List<string> SensorIds { get; set; } = new List<string>();

        /// <summary>Protocol-specific configuration properties.</summary>
        public Dictionary<string, string> Properties { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Determines if the endpoint is in a healthy communication state.
        /// Unhealthy after 5 consecutive failures.
        /// </summary>
        public bool IsHealthy => ConsecutiveFailures < 5;

        public override string ToString() =>
            $"{Protocol}://{Address}:{Port} ({SensorIds.Count} sensors, {(IsHealthy ? "healthy" : "unhealthy")})";
    }

    #endregion

    #region Digital Twin State

    /// <summary>
    /// Snapshot of the complete digital twin state, including model version,
    /// synchronization timestamp, all sensor states, and computed health scores.
    /// </summary>
    public class DigitalTwinState
    {
        /// <summary>Semantic version of the Revit model that this twin represents.</summary>
        public string ModelVersion { get; set; } = "1.0.0";

        /// <summary>UTC timestamp of the last successful synchronization.</summary>
        public DateTime LastSyncTime { get; set; }

        /// <summary>Current state of each sensor, keyed by sensor ID.</summary>
        public Dictionary<string, SensorReading> SensorStates { get; set; } =
            new Dictionary<string, SensorReading>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Overall building health score (0.0 to 100.0).</summary>
        public double HealthScore { get; set; } = 100.0;

        /// <summary>Per-element health scores, keyed by Revit element UniqueId.</summary>
        public Dictionary<string, double> ElementHealthScores { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Active (unacknowledged) alerts.</summary>
        public List<IoTAlert> ActiveAlerts { get; set; } = new List<IoTAlert>();

        /// <summary>Number of sensors currently reporting Good quality.</summary>
        public int HealthySensorCount =>
            SensorStates.Values.Count(r => r.Quality == SensorDataQuality.Good);

        /// <summary>Number of sensors currently reporting Bad or Stale quality.</summary>
        public int UnhealthySensorCount =>
            SensorStates.Values.Count(r => r.Quality == SensorDataQuality.Bad || r.Quality == SensorDataQuality.Stale);

        /// <summary>Total number of registered sensors in the twin.</summary>
        public int TotalSensorCount => SensorStates.Count;
    }

    #endregion

    #region Alerts

    /// <summary>
    /// Represents an IoT alert generated when a sensor reading exceeds configured
    /// thresholds or an anomaly is detected.
    /// </summary>
    public class IoTAlert
    {
        /// <summary>Unique alert identifier.</summary>
        public string AlertId { get; set; } = Guid.NewGuid().ToString("N")[..16];

        /// <summary>Sensor that triggered the alert.</summary>
        public string SensorId { get; set; } = string.Empty;

        /// <summary>Alert severity level.</summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>Human-readable alert message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the alert was generated.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Whether the alert has been acknowledged by an operator.</summary>
        public bool Acknowledged { get; set; }

        /// <summary>UTC timestamp when the alert was acknowledged.</summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>Username of the operator who acknowledged the alert.</summary>
        public string AcknowledgedBy { get; set; } = string.Empty;

        /// <summary>The sensor reading value that triggered the alert.</summary>
        public double TriggerValue { get; set; }

        /// <summary>The threshold value that was exceeded.</summary>
        public double ThresholdValue { get; set; }

        /// <summary>Revit element UniqueId associated with this alert (if any).</summary>
        public string ElementId { get; set; } = string.Empty;

        public override string ToString() =>
            $"[{Severity}] {SensorId}: {Message} ({Timestamp:yyyy-MM-dd HH:mm:ss})";
    }

    #endregion

    #region Specialized Meter Readings

    /// <summary>
    /// Electrical energy meter reading with kWh totalization, peak demand, and power factor.
    /// Extends SensorReading with energy-specific fields.
    /// </summary>
    public class EnergyMeterReading : SensorReading
    {
        /// <summary>Cumulative energy consumption in kilowatt-hours.</summary>
        public double KWh { get; set; }

        /// <summary>Peak demand in kilowatts during the measurement interval.</summary>
        public double PeakDemandKW { get; set; }

        /// <summary>Power factor (0.0 to 1.0) indicating reactive power efficiency.</summary>
        public double PowerFactor { get; set; } = 1.0;

        /// <summary>Voltage reading in volts (line-to-neutral or line-to-line).</summary>
        public double Voltage { get; set; }

        /// <summary>Current reading in amperes.</summary>
        public double Current { get; set; }

        /// <summary>Reactive power in kVAR.</summary>
        public double ReactiveKVAR => PeakDemandKW * Math.Sqrt(1 - PowerFactor * PowerFactor);
    }

    /// <summary>
    /// Water meter reading with volumetric totalization and flow rate.
    /// Extends SensorReading with water-specific fields.
    /// </summary>
    public class WaterMeterReading : SensorReading
    {
        /// <summary>Cumulative volume in liters.</summary>
        public double Liters { get; set; }

        /// <summary>Instantaneous flow rate in liters per minute.</summary>
        public double FlowRate { get; set; }

        /// <summary>Water temperature in degrees Celsius (for hot water systems).</summary>
        public double? Temperature { get; set; }

        /// <summary>Line pressure in kPa.</summary>
        public double? Pressure { get; set; }

        /// <summary>Converts liters to cubic meters.</summary>
        public double CubicMeters => Liters / 1000.0;
    }

    /// <summary>
    /// Occupancy sensor reading with count and density calculation.
    /// Extends SensorReading with occupancy-specific fields.
    /// </summary>
    public class OccupancyReading : SensorReading
    {
        /// <summary>Number of occupants detected.</summary>
        public int Count { get; set; }

        /// <summary>Occupancy density in persons per square meter.</summary>
        public double DensityPerSqM { get; set; }

        /// <summary>Zone area in square meters used for density calculation.</summary>
        public double ZoneAreaSqM { get; set; }

        /// <summary>Maximum occupancy capacity for the zone.</summary>
        public int MaxCapacity { get; set; }

        /// <summary>Utilization ratio (0.0 to 1.0) of zone capacity.</summary>
        public double Utilization => MaxCapacity > 0 ? (double)Count / MaxCapacity : 0.0;
    }

    #endregion

    #region Clash Detection Models

    /// <summary>
    /// Represents a detected clash between two building elements.
    /// </summary>
    public class ClashResult
    {
        public string ClashId { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string ElementIdA { get; set; } = string.Empty;
        public string ElementIdB { get; set; } = string.Empty;
        public MepDiscipline DisciplineA { get; set; }
        public MepDiscipline DisciplineB { get; set; }
        public ClashType Type { get; set; }
        public double OverlapDistance { get; set; }
        public double PointX { get; set; }
        public double PointY { get; set; }
        public double PointZ { get; set; }
        public string Description { get; set; } = string.Empty;
        public string SuggestedResolution { get; set; } = string.Empty;
        public bool IsResolved { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        public override string ToString() =>
            $"Clash {ClashId}: {DisciplineA} vs {DisciplineB} ({Type}, {OverlapDistance:F1}mm)";
    }

    /// <summary>
    /// Oriented bounding box for spatial clash detection.
    /// </summary>
    public class BoundingBoxInfo
    {
        public string ElementId { get; set; } = string.Empty;
        public MepDiscipline Discipline { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public double ClearanceRequired { get; set; }

        public bool Intersects(BoundingBoxInfo other)
        {
            return MinX <= other.MaxX && MaxX >= other.MinX
                && MinY <= other.MaxY && MaxY >= other.MinY
                && MinZ <= other.MaxZ && MaxZ >= other.MinZ;
        }

        public double OverlapVolume(BoundingBoxInfo other)
        {
            double overlapX = Math.Max(0, Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX));
            double overlapY = Math.Max(0, Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY));
            double overlapZ = Math.Max(0, Math.Min(MaxZ, other.MaxZ) - Math.Max(MinZ, other.MinZ));
            return overlapX * overlapY * overlapZ;
        }
    }

    #endregion

    #region Commissioning Models

    /// <summary>
    /// Commissioning plan for a building system or the entire building.
    /// </summary>
    public class CommissioningPlan
    {
        public string PlanId { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public CommissioningPhase CurrentPhase { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? CompletionDate { get; set; }
        public List<CxChecklist> Checklists { get; set; } = new List<CxChecklist>();
        public List<PunchListItem> PunchList { get; set; } = new List<PunchListItem>();
        public Dictionary<string, string> DesignIntent { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public double CompletionPercentage
        {
            get
            {
                int total = 0, completed = 0;
                foreach (var checklist in Checklists)
                {
                    foreach (var item in checklist.Items)
                    {
                        total++;
                        if (item.Status == TestStatus.Passed || item.Status == TestStatus.NotApplicable)
                            completed++;
                    }
                }
                return total > 0 ? (double)completed / total * 100.0 : 0.0;
            }
        }
    }

    /// <summary>
    /// A commissioning checklist for a specific system (HVAC, electrical, plumbing, etc.).
    /// </summary>
    public class CxChecklist
    {
        public string ChecklistId { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string SystemId { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public CommissioningPhase Phase { get; set; }
        public List<CxChecklistItem> Items { get; set; } = new List<CxChecklistItem>();
    }

    /// <summary>
    /// Individual checklist item within a commissioning checklist.
    /// </summary>
    public class CxChecklistItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Description { get; set; } = string.Empty;
        public TestStatus Status { get; set; } = TestStatus.NotStarted;
        public string Criteria { get; set; } = string.Empty;
        public string ActualResult { get; set; } = string.Empty;
        public string TestedBy { get; set; } = string.Empty;
        public DateTime? TestedDate { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Deficiency item on the commissioning punch list.
    /// </summary>
    public class PunchListItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SystemId { get; set; } = string.Empty;
        public AlertSeverity Priority { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public bool IsResolved { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedDate { get; set; }
        public string PhotoPath { get; set; } = string.Empty;
        public string ElementId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Scenario parameters for digital twin what-if simulation.
    /// </summary>
    public class ScenarioParams
    {
        public string ScenarioName { get; set; } = string.Empty;
        public Dictionary<string, double> SetpointChanges { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public int? OccupancyOverride { get; set; }
        public double? OutdoorTempOverride { get; set; }
        public double? HumidityOverride { get; set; }
        public TimeSpan SimulationDuration { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan TimeStep { get; set; } = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Result of a digital twin simulation scenario.
    /// </summary>
    public class ScenarioResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public double PredictedEnergyKWh { get; set; }
        public double PredictedCostUSD { get; set; }
        public double ComfortScore { get; set; }
        public double PeakDemandKW { get; set; }
        public Dictionary<string, List<double>> TimeSeriesData { get; set; } =
            new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        public List<string> Warnings { get; set; } = new List<string>();
    }

    #endregion
}
