// ===================================================================
// StingBIM Drone Intelligence Engine - UAV Operations & Aerial Analysis
// Flight planning, autonomous inspections, mapping, thermal imaging, Part 107
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DroneIntelligence
{
    #region Enums

    /// <summary>Types of UAV platforms supported</summary>
    public enum DroneType
    {
        Multirotor,           // DJI-style quadcopters
        FixedWing,            // Airplane-style for large area mapping
        VTOL,                 // Vertical takeoff/landing hybrid
        Tethered,             // Power-tethered for extended operations
        Indoor                // Small indoor inspection drones
    }

    /// <summary>Flight operation modes</summary>
    public enum FlightMode
    {
        Manual,               // Pilot-controlled flight
        Waypoint,             // Pre-programmed waypoint navigation
        Orbit,                // Circular flight around point of interest
        GridMapping,          // Systematic grid pattern for mapping
        DoubleGrid,           // Cross-hatch pattern for 3D modeling
        PerimeterFlight,      // Building perimeter inspection
        VerticalScan,         // Facade inspection top-to-bottom
        TerrainFollow,        // Maintains constant AGL altitude
        FreeRoam              // AI-assisted exploration
    }

    /// <summary>Types of construction inspections</summary>
    public enum InspectionType
    {
        ProgressMonitoring,   // General construction progress
        RoofInspection,       // Roof condition assessment
        FacadeInspection,     // Building envelope inspection
        StructuralInspection, // Steel/concrete structural review
        MEPInspection,        // Mechanical/electrical from above
        SolarPanelInspection, // Photovoltaic system inspection
        ThermalSurvey,        // Thermal imaging inspection
        SafetyAudit,          // Safety compliance from air
        SiteLogistics,        // Material staging and traffic
        StormDamage,          // Post-storm damage assessment
        QualityControl        // Visual quality verification
    }

    /// <summary>Sensor payload types</summary>
    public enum SensorType
    {
        RGB,                  // Standard visible light camera
        Thermal,              // FLIR/radiometric thermal camera
        Multispectral,        // Multiple wavelength bands
        LiDAR,                // Light detection and ranging
        Hyperspectral,        // Detailed spectral analysis
        Gas,                  // Methane/VOC detection
        Radiation             // Nuclear facility inspection
    }

    /// <summary>Thermal anomaly classifications</summary>
    public enum ThermalAnomalyType
    {
        ThermalBridging,      // Heat loss through structure
        MoistureIntrusion,    // Water infiltration detection
        InsulationDeficiency, // Missing/degraded insulation
        AirLeakage,           // HVAC air leaks
        ElectricalHotspot,    // Overheating electrical
        MechanicalOverheat,   // Equipment overheating
        SolarPanelDefect,     // PV cell hot spots
        RoofingDefect,        // Membrane failures
        HVACImbalance,        // Airflow distribution issues
        PipeLeakage           // Hot/cold water leaks
    }

    /// <summary>FAA Part 107 airspace classifications</summary>
    public enum AirspaceClass
    {
        ClassG,               // Uncontrolled (most operations)
        ClassE,               // Controlled, floor varies
        ClassD,               // Tower-controlled airport
        ClassC,               // Radar approach control
        ClassB,               // Major airports (restricted)
        Restricted,           // Military/security areas
        Prohibited,           // No-fly zones
        TFR                   // Temporary flight restriction
    }

    /// <summary>Pilot certification levels</summary>
    public enum PilotCertification
    {
        Part107,              // FAA Remote Pilot Certificate
        Part107Waiver,        // Part 107 with waivers
        Part91,               // Full pilot certificate
        Part137,              // Agricultural operations
        Recreational,         // Hobbyist (not commercial)
        Foreign               // International equivalent
    }

    /// <summary>Flight authorization status</summary>
    public enum AuthorizationStatus
    {
        NotRequired,          // Class G airspace
        Pending,              // LAANC request submitted
        Approved,             // Authorization granted
        Denied,               // Request rejected
        Expired,              // Past valid date
        Cancelled             // Operation cancelled
    }

    /// <summary>Mission completion status</summary>
    public enum MissionStatus
    {
        Draft,                // Mission planning phase
        ReadyForFlight,       // Pre-flight complete
        InProgress,           // Currently flying
        Paused,               // Temporarily halted
        Completed,            // Successfully finished
        Aborted,              // Emergency stop
        Failed,               // Technical failure
        WeatherHold           // Weather delay
    }

    /// <summary>Weather condition categories</summary>
    public enum WeatherCategory
    {
        VFR,                  // Visual flight rules (good)
        MVFR,                 // Marginal VFR
        IFR,                  // Instrument rules (no-go)
        LIFR                  // Low IFR (definitely no-go)
    }

    /// <summary>Data processing status</summary>
    public enum ProcessingStatus
    {
        Pending,              // Awaiting processing
        Processing,           // Currently being processed
        QAReview,             // Quality assurance check
        Completed,            // Processing finished
        Failed,               // Processing error
        Reprocessing          // Re-running pipeline
    }

    /// <summary>Deliverable output types</summary>
    public enum OutputType
    {
        Orthomosaic,          // 2D orthorectified map
        DSM,                  // Digital surface model
        DTM,                  // Digital terrain model
        PointCloud,           // 3D point cloud
        TexturedMesh,         // 3D textured model
        ThermalMap,           // Thermal mosaic
        ProgressReport,       // Construction progress report
        InspectionReport,     // Inspection findings
        VolumeCalculation,    // Cut/fill volumes
        ContourMap,           // Elevation contours
        NDVIMap               // Vegetation health
    }

    /// <summary>Compliance violation types</summary>
    public enum ViolationType
    {
        AltitudeExceedance,   // Above 400ft AGL
        AirspaceViolation,    // Unauthorized airspace
        BVLOSFlight,          // Beyond visual line of sight
        NightFlight,          // Without night waiver
        OverPeople,           // Flying over non-participants
        MovingVehicle,        // Flight from moving vehicle
        DrugAlcohol,          // Pilot impairment
        RecordKeeping,        // Documentation failure
        MaintenanceFailure,   // Required maintenance missed
        RegistrationLapse     // Expired registration
    }

    #endregion

    #region Data Models

    /// <summary>UAV asset information</summary>
    public class DroneAsset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string FAARegistration { get; set; }
        public DroneType Type { get; set; }
        public double MaxFlightTime { get; set; } // Minutes
        public double MaxPayload { get; set; } // kg
        public double MaxSpeed { get; set; } // m/s
        public double MaxAltitude { get; set; } // meters
        public double MaxWindResistance { get; set; } // m/s
        public List<string> InstalledSensors { get; set; } = new();
        public double TotalFlightHours { get; set; }
        public int TotalFlights { get; set; }
        public DateTime LastMaintenance { get; set; }
        public DateTime NextMaintenanceDue { get; set; }
        public DateTime RegistrationExpiry { get; set; }
        public string Status { get; set; } = "Operational";
        public List<MaintenanceRecord> MaintenanceHistory { get; set; } = new();
    }

    /// <summary>Maintenance record for drone</summary>
    public class MaintenanceRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string PerformedBy { get; set; }
        public double FlightHoursAtMaintenance { get; set; }
        public List<string> PartsReplaced { get; set; } = new();
        public double Cost { get; set; }
    }

    /// <summary>Sensor payload information</summary>
    public class SensorPayload
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public SensorType Type { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public double Resolution { get; set; } // Megapixels or equivalent
        public double SensorWidth { get; set; } // mm
        public double FocalLength { get; set; } // mm
        public double FieldOfView { get; set; } // degrees
        public double Weight { get; set; } // grams
        public bool HasGimbal { get; set; }
        public bool IsCalibrated { get; set; }
        public DateTime LastCalibration { get; set; }
        public Dictionary<string, object> Specifications { get; set; } = new();
    }

    /// <summary>Remote pilot information</summary>
    public class RemotePilot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string CertificateNumber { get; set; }
        public PilotCertification CertificationType { get; set; }
        public DateTime CertificationDate { get; set; }
        public DateTime CertificationExpiry { get; set; }
        public List<string> Waivers { get; set; } = new();
        public double TotalFlightHours { get; set; }
        public int TotalFlights { get; set; }
        public List<string> QualifiedDrones { get; set; } = new();
        public List<string> QualifiedSensors { get; set; } = new();
        public bool CurrentMedical { get; set; }
        public DateTime LastRecurrentTraining { get; set; }
        public List<PilotEndorsement> Endorsements { get; set; } = new();
    }

    /// <summary>Pilot endorsements and special qualifications</summary>
    public class PilotEndorsement
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime DateIssued { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string IssuedBy { get; set; }
    }

    /// <summary>Flight mission definition</summary>
    public class FlightMission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public MissionStatus Status { get; set; } = MissionStatus.Draft;
        public InspectionType InspectionType { get; set; }
        public FlightMode FlightMode { get; set; }
        public string DroneId { get; set; }
        public string PilotId { get; set; }
        public string VisualObserverId { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime ActualStartTime { get; set; }
        public DateTime ActualEndTime { get; set; }
        public FlightPlan FlightPlan { get; set; }
        public AirspaceAuthorization Authorization { get; set; }
        public WeatherBrief WeatherBriefing { get; set; }
        public PreFlightChecklist PreFlight { get; set; }
        public PostFlightLog PostFlight { get; set; }
        public List<string> Sensors { get; set; } = new();
        public List<CapturedData> CapturedData { get; set; } = new();
        public List<string> Notes { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Flight plan with waypoints and parameters</summary>
    public class FlightPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public FlightMode Mode { get; set; }
        public GeoPoint HomeLocation { get; set; }
        public GeoPoint TakeoffLocation { get; set; }
        public GeoPoint LandingLocation { get; set; }
        public List<Waypoint> Waypoints { get; set; } = new();
        public double Altitude { get; set; } // meters AGL
        public double Speed { get; set; } // m/s
        public double FrontOverlap { get; set; } = 75; // percent
        public double SideOverlap { get; set; } = 65; // percent
        public double GimbalPitch { get; set; } = -90; // degrees (nadir)
        public double EstimatedFlightTime { get; set; } // minutes
        public double EstimatedDistance { get; set; } // meters
        public int EstimatedPhotos { get; set; }
        public double GroundSamplingDistance { get; set; } // cm/pixel
        public List<GeoFence> GeoFences { get; set; } = new();
        public bool ReturnToHomeOnLowBattery { get; set; } = true;
        public double LowBatteryThreshold { get; set; } = 30; // percent
    }

    /// <summary>Geographic point with coordinates</summary>
    public class GeoPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; } // meters MSL
        public double AltitudeAGL { get; set; } // meters above ground
    }

    /// <summary>Flight waypoint with actions</summary>
    public class Waypoint
    {
        public int Sequence { get; set; }
        public GeoPoint Location { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; } // degrees
        public double GimbalPitch { get; set; }
        public double HoverTime { get; set; } // seconds
        public List<WaypointAction> Actions { get; set; } = new();
    }

    /// <summary>Action to perform at waypoint</summary>
    public class WaypointAction
    {
        public string Type { get; set; } // TakePhoto, StartVideo, StopVideo, RotateGimbal
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>Geofence boundary definition</summary>
    public class GeoFence
    {
        public string Name { get; set; }
        public string Type { get; set; } // Include, Exclude
        public List<GeoPoint> Boundary { get; set; } = new();
        public double MaxAltitude { get; set; }
        public double MinAltitude { get; set; }
    }

    /// <summary>Airspace authorization request and status</summary>
    public class AirspaceAuthorization
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public AirspaceClass AirspaceClass { get; set; }
        public AuthorizationStatus Status { get; set; }
        public string LANCAuthorizationId { get; set; }
        public double ApprovedAltitude { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }
        public GeoPoint CenterPoint { get; set; }
        public double Radius { get; set; } // meters
        public string ControllingFacility { get; set; }
        public string Notes { get; set; }
        public List<string> Restrictions { get; set; } = new();
    }

    /// <summary>Weather briefing for flight</summary>
    public class WeatherBrief
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime BriefingTime { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }
        public WeatherCategory Category { get; set; }
        public double Temperature { get; set; } // Celsius
        public double WindSpeed { get; set; } // m/s
        public double WindGusts { get; set; } // m/s
        public double WindDirection { get; set; } // degrees
        public double Visibility { get; set; } // meters
        public double CloudBase { get; set; } // meters AGL
        public double CloudCoverage { get; set; } // percent
        public double PrecipitationProbability { get; set; }
        public double DensityAltitude { get; set; } // meters
        public string Recommendation { get; set; }
        public bool GoNoGo { get; set; }
        public List<string> Hazards { get; set; } = new();
    }

    /// <summary>Pre-flight checklist</summary>
    public class PreFlightChecklist
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CompletedAt { get; set; }
        public string CompletedBy { get; set; }
        public bool AllItemsPassed { get; set; }
        public List<ChecklistItem> Items { get; set; } = new();
    }

    /// <summary>Individual checklist item</summary>
    public class ChecklistItem
    {
        public string Category { get; set; }
        public string Item { get; set; }
        public bool Passed { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Post-flight log</summary>
    public class PostFlightLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CompletedAt { get; set; }
        public string CompletedBy { get; set; }
        public double ActualFlightTime { get; set; } // minutes
        public double DistanceTraveled { get; set; } // meters
        public int PhotosCaptured { get; set; }
        public double VideoCaptured { get; set; } // minutes
        public double BatteryStart { get; set; } // percent
        public double BatteryEnd { get; set; }
        public int BatteryCycles { get; set; }
        public double MaxAltitude { get; set; }
        public double MaxSpeed { get; set; }
        public double MaxDistance { get; set; } // from pilot
        public List<string> Incidents { get; set; } = new();
        public List<string> Anomalies { get; set; } = new();
        public string OverallCondition { get; set; }
        public List<string> MaintenanceFlags { get; set; } = new();
    }

    /// <summary>Captured data from flight</summary>
    public class CapturedData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SensorId { get; set; }
        public SensorType SensorType { get; set; }
        public string DataType { get; set; } // Photo, Video, LiDAR, etc.
        public int FileCount { get; set; }
        public double TotalSize { get; set; } // MB
        public string StorageLocation { get; set; }
        public DateTime CapturedAt { get; set; }
        public ProcessingStatus ProcessingStatus { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>Thermal inspection analysis</summary>
    public class ThermalAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MissionId { get; set; }
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public double AmbientTemperature { get; set; }
        public double Emissivity { get; set; }
        public double ReflectedTemperature { get; set; }
        public double Humidity { get; set; }
        public double Distance { get; set; }
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public double AverageTemperature { get; set; }
        public double DeltaT { get; set; }
        public List<ThermalAnomaly> Anomalies { get; set; } = new();
        public ThermalStatistics Statistics { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>Individual thermal anomaly</summary>
    public class ThermalAnomaly
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ThermalAnomalyType Type { get; set; }
        public string Location { get; set; }
        public GeoPoint GeoLocation { get; set; }
        public double Temperature { get; set; }
        public double DeltaT { get; set; }
        public double Area { get; set; } // sq meters
        public string Severity { get; set; }
        public string Description { get; set; }
        public string ThermalImagePath { get; set; }
        public string RGBImagePath { get; set; }
        public string RecommendedAction { get; set; }
        public double EstimatedRepairCost { get; set; }
        public double EnergyLoss { get; set; } // kWh/year estimated
    }

    /// <summary>Thermal analysis statistics</summary>
    public class ThermalStatistics
    {
        public int TotalImagesAnalyzed { get; set; }
        public int AnomaliesDetected { get; set; }
        public int CriticalAnomalies { get; set; }
        public int ModerateAnomalies { get; set; }
        public int MinorAnomalies { get; set; }
        public double AverageRoofTemperature { get; set; }
        public double AverageWallTemperature { get; set; }
        public double EstimatedTotalEnergyLoss { get; set; }
        public double EstimatedRepairCost { get; set; }
    }

    /// <summary>Construction progress tracking</summary>
    public class ProgressCapture
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string MissionId { get; set; }
        public DateTime CaptureDate { get; set; }
        public int SequenceNumber { get; set; }
        public string OrthomosaicPath { get; set; }
        public string PointCloudPath { get; set; }
        public string DSMPath { get; set; }
        public double GroundSamplingDistance { get; set; }
        public double CoverageArea { get; set; }
        public int PhotoCount { get; set; }
        public ProgressMetrics Metrics { get; set; }
        public List<ProgressComparison> Comparisons { get; set; } = new();
    }

    /// <summary>Progress metrics calculated from capture</summary>
    public class ProgressMetrics
    {
        public double SiteAreaTotal { get; set; }
        public double AreaExcavated { get; set; }
        public double AreaFoundation { get; set; }
        public double AreaSuperstructure { get; set; }
        public double AreaEnclosed { get; set; }
        public double AreaRoofComplete { get; set; }
        public double BuildingHeight { get; set; }
        public int FloorsComplete { get; set; }
        public double MaterialStockpileVolume { get; set; }
        public int EquipmentCount { get; set; }
        public int VehicleCount { get; set; }
        public int WorkerCountEstimate { get; set; }
        public double OverallProgress { get; set; } // percent
    }

    /// <summary>Comparison between progress captures</summary>
    public class ProgressComparison
    {
        public string PreviousCaptureId { get; set; }
        public DateTime PreviousDate { get; set; }
        public double DaysElapsed { get; set; }
        public double VolumeChange { get; set; }
        public double AreaChange { get; set; }
        public double ProgressChange { get; set; }
        public List<string> ObservedChanges { get; set; } = new();
        public List<string> Concerns { get; set; } = new();
    }

    /// <summary>Mapping project deliverable</summary>
    public class MappingDeliverable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string MissionId { get; set; }
        public OutputType Type { get; set; }
        public ProcessingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public string FilePath { get; set; }
        public double FileSize { get; set; } // MB
        public string CoordinateSystem { get; set; }
        public double Resolution { get; set; }
        public double Accuracy { get; set; }
        public BoundingBox Bounds { get; set; }
        public Dictionary<string, object> Specifications { get; set; } = new();
    }

    /// <summary>Geographic bounding box</summary>
    public class BoundingBox
    {
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLon { get; set; }
        public double MinAlt { get; set; }
        public double MaxAlt { get; set; }
    }

    /// <summary>Part 107 compliance record</summary>
    public class ComplianceRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MissionId { get; set; }
        public string PilotId { get; set; }
        public string DroneId { get; set; }
        public DateTime FlightDate { get; set; }
        public bool Part107Compliant { get; set; }
        public List<ComplianceCheck> Checks { get; set; } = new();
        public List<ViolationType> Violations { get; set; } = new();
        public string SignedOffBy { get; set; }
        public DateTime SignedOffAt { get; set; }
    }

    /// <summary>Individual compliance check</summary>
    public class ComplianceCheck
    {
        public string Requirement { get; set; }
        public string CFRReference { get; set; }
        public bool Compliant { get; set; }
        public string Evidence { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Drone operations project</summary>
    public class DroneProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public GeoPoint SiteLocation { get; set; }
        public double SiteArea { get; set; }
        public List<string> Missions { get; set; } = new();
        public List<string> Deliverables { get; set; } = new();
        public List<string> AssignedDrones { get; set; } = new();
        public List<string> AssignedPilots { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; }
        public DroneProjectStatistics Statistics { get; set; }
    }

    /// <summary>Project statistics summary</summary>
    public class DroneProjectStatistics
    {
        public int TotalMissions { get; set; }
        public int CompletedMissions { get; set; }
        public double TotalFlightHours { get; set; }
        public int TotalPhotos { get; set; }
        public double TotalDataCaptured { get; set; } // GB
        public double AreaMapped { get; set; }
        public int ThermalAnomaliesFound { get; set; }
        public int DeliverablesGenerated { get; set; }
    }

    /// <summary>Event arguments for mission alerts</summary>
    public class MissionAlertEventArgs : EventArgs
    {
        public string MissionId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion

    /// <summary>
    /// Comprehensive drone intelligence engine for UAV flight planning, autonomous inspections,
    /// aerial mapping, progress monitoring, thermal analysis, and FAA Part 107 compliance
    /// </summary>
    public sealed class DroneIntelligenceEngine
    {
        private static readonly Lazy<DroneIntelligenceEngine> _instance =
            new Lazy<DroneIntelligenceEngine>(() => new DroneIntelligenceEngine());
        public static DroneIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, DroneProject> _projects = new();
        private readonly Dictionary<string, DroneAsset> _drones = new();
        private readonly Dictionary<string, SensorPayload> _sensors = new();
        private readonly Dictionary<string, RemotePilot> _pilots = new();
        private readonly Dictionary<string, FlightMission> _missions = new();
        private readonly Dictionary<string, ThermalAnalysis> _thermalAnalyses = new();
        private readonly Dictionary<string, ProgressCapture> _progressCaptures = new();
        private readonly Dictionary<string, MappingDeliverable> _deliverables = new();
        private readonly Dictionary<string, ComplianceRecord> _complianceRecords = new();
        private readonly List<PreFlightChecklistTemplate> _checklistTemplates = new();
        private readonly object _lock = new object();

        // Part 107 operational limits
        private readonly Dictionary<string, double> _part107Limits = new()
        {
            ["MaxAltitudeAGL"] = 400 * 0.3048, // 400ft in meters
            ["MaxSpeed"] = 100 * 0.44704, // 100mph in m/s
            ["MinVisibility"] = 3 * 1609.34, // 3 statute miles in meters
            ["MinCloudClearanceVertical"] = 500 * 0.3048, // 500ft in meters
            ["MinCloudClearanceHorizontal"] = 2000 * 0.3048 // 2000ft in meters
        };

        // Weather limits for safe flight
        private readonly Dictionary<string, double> _weatherLimits = new()
        {
            ["MaxWindSpeed"] = 10.0, // m/s
            ["MaxWindGusts"] = 15.0, // m/s
            ["MinVisibility"] = 5000, // meters
            ["MinCloudBase"] = 150, // meters AGL
            ["MaxPrecipProbability"] = 20 // percent
        };

        public event EventHandler<MissionAlertEventArgs> MissionAlert;

        private DroneIntelligenceEngine()
        {
            InitializeChecklistTemplates();
        }

        #region Asset Management

        /// <summary>Register a new drone asset</summary>
        public DroneAsset RegisterDrone(string name, string manufacturer, string model,
            string serialNumber, string faaRegistration, DroneType type)
        {
            var drone = new DroneAsset
            {
                Name = name,
                Manufacturer = manufacturer,
                Model = model,
                SerialNumber = serialNumber,
                FAARegistration = faaRegistration,
                Type = type,
                MaxFlightTime = GetDefaultFlightTime(type),
                MaxAltitude = _part107Limits["MaxAltitudeAGL"],
                LastMaintenance = DateTime.UtcNow,
                NextMaintenanceDue = DateTime.UtcNow.AddDays(30),
                RegistrationExpiry = DateTime.UtcNow.AddYears(3)
            };

            lock (_lock) { _drones[drone.Id] = drone; }
            return drone;
        }

        /// <summary>Register a sensor payload</summary>
        public SensorPayload RegisterSensor(string name, SensorType type, string manufacturer,
            string model, double resolution)
        {
            var sensor = new SensorPayload
            {
                Name = name,
                Type = type,
                Manufacturer = manufacturer,
                Model = model,
                Resolution = resolution,
                IsCalibrated = true,
                LastCalibration = DateTime.UtcNow
            };

            lock (_lock) { _sensors[sensor.Id] = sensor; }
            return sensor;
        }

        /// <summary>Register a remote pilot</summary>
        public RemotePilot RegisterPilot(string name, string email, string certificateNumber,
            PilotCertification certificationType, DateTime certificationExpiry)
        {
            var pilot = new RemotePilot
            {
                Name = name,
                Email = email,
                CertificateNumber = certificateNumber,
                CertificationType = certificationType,
                CertificationDate = DateTime.UtcNow,
                CertificationExpiry = certificationExpiry,
                CurrentMedical = true,
                LastRecurrentTraining = DateTime.UtcNow
            };

            lock (_lock) { _pilots[pilot.Id] = pilot; }
            return pilot;
        }

        /// <summary>Log maintenance for a drone</summary>
        public MaintenanceRecord LogMaintenance(string droneId, string type, string description,
            string performedBy, List<string> partsReplaced = null, double cost = 0)
        {
            lock (_lock)
            {
                if (!_drones.TryGetValue(droneId, out var drone))
                    return null;

                var record = new MaintenanceRecord
                {
                    Date = DateTime.UtcNow,
                    Type = type,
                    Description = description,
                    PerformedBy = performedBy,
                    FlightHoursAtMaintenance = drone.TotalFlightHours,
                    PartsReplaced = partsReplaced ?? new List<string>(),
                    Cost = cost
                };

                drone.MaintenanceHistory.Add(record);
                drone.LastMaintenance = DateTime.UtcNow;
                drone.NextMaintenanceDue = CalculateNextMaintenance(drone);

                return record;
            }
        }

        private double GetDefaultFlightTime(DroneType type)
        {
            return type switch
            {
                DroneType.Multirotor => 30,
                DroneType.FixedWing => 90,
                DroneType.VTOL => 60,
                DroneType.Tethered => 480, // 8 hours powered
                DroneType.Indoor => 15,
                _ => 25
            };
        }

        private DateTime CalculateNextMaintenance(DroneAsset drone)
        {
            // Every 50 flight hours or 30 days, whichever comes first
            var hoursBased = DateTime.UtcNow.AddDays((50 - (drone.TotalFlightHours % 50)) / 2);
            var timeBased = drone.LastMaintenance.AddDays(30);
            return hoursBased < timeBased ? hoursBased : timeBased;
        }

        #endregion

        #region Project Management

        /// <summary>Create a drone operations project</summary>
        public DroneProject CreateProject(string projectId, string projectName, string clientName,
            GeoPoint siteLocation, double siteArea)
        {
            var project = new DroneProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                ClientName = clientName,
                SiteLocation = siteLocation,
                SiteArea = siteArea,
                StartDate = DateTime.UtcNow,
                Status = "Active",
                Statistics = new DroneProjectStatistics()
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        /// <summary>Assign resources to project</summary>
        public void AssignResources(string projectId, List<string> droneIds, List<string> pilotIds)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                project.AssignedDrones.AddRange(droneIds);
                project.AssignedPilots.AddRange(pilotIds);
            }
        }

        #endregion

        #region Flight Planning

        /// <summary>Create a flight mission</summary>
        public FlightMission CreateMission(string projectId, string name, InspectionType inspectionType,
            string droneId, string pilotId)
        {
            var mission = new FlightMission
            {
                ProjectId = projectId,
                Name = name,
                InspectionType = inspectionType,
                DroneId = droneId,
                PilotId = pilotId
            };

            lock (_lock)
            {
                _missions[mission.Id] = mission;
                if (_projects.TryGetValue(projectId, out var project))
                {
                    project.Missions.Add(mission.Id);
                }
            }

            return mission;
        }

        /// <summary>Generate automated flight plan for mapping</summary>
        public FlightPlan GenerateMappingFlightPlan(string missionId, List<GeoPoint> boundary,
            double altitude, double groundSamplingDistance, SensorPayload sensor)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                var plan = new FlightPlan
                {
                    Name = $"{mission.Name} - Mapping Plan",
                    Mode = FlightMode.GridMapping,
                    Altitude = altitude,
                    GimbalPitch = -90
                };

                // Calculate center point as home
                var centerLat = boundary.Average(p => p.Latitude);
                var centerLon = boundary.Average(p => p.Longitude);
                plan.HomeLocation = new GeoPoint { Latitude = centerLat, Longitude = centerLon };
                plan.TakeoffLocation = plan.HomeLocation;
                plan.LandingLocation = plan.HomeLocation;

                // Calculate GSD-based parameters
                double imageWidth = sensor.SensorWidth / 1000; // convert to meters
                double focalLength = sensor.FocalLength / 1000; // convert to meters
                double actualGSD = (altitude * imageWidth) / (focalLength * Math.Sqrt(sensor.Resolution * 1000000));
                plan.GroundSamplingDistance = actualGSD * 100; // convert to cm

                // Calculate photo spacing based on overlap
                double footprintWidth = (altitude * imageWidth) / focalLength;
                double photoSpacingForward = footprintWidth * (1 - plan.FrontOverlap / 100);
                double photoSpacingSide = footprintWidth * (1 - plan.SideOverlap / 100);

                // Generate grid waypoints
                plan.Waypoints = GenerateGridWaypoints(boundary, altitude, photoSpacingForward, photoSpacingSide);

                // Calculate estimates
                plan.EstimatedDistance = CalculateTotalDistance(plan.Waypoints);
                plan.EstimatedFlightTime = plan.EstimatedDistance / plan.Speed / 60 + plan.Waypoints.Count * 0.1; // minutes
                plan.EstimatedPhotos = plan.Waypoints.Sum(w => w.Actions.Count(a => a.Type == "TakePhoto"));

                // Add geofence from boundary
                plan.GeoFences.Add(new GeoFence
                {
                    Name = "Survey Boundary",
                    Type = "Include",
                    Boundary = boundary,
                    MaxAltitude = altitude + 20,
                    MinAltitude = altitude - 20
                });

                mission.FlightPlan = plan;
                return plan;
            }
        }

        /// <summary>Generate flight plan for building inspection</summary>
        public FlightPlan GenerateInspectionFlightPlan(string missionId, GeoPoint buildingCenter,
            double buildingHeight, double buildingRadius, InspectionType inspectionType)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                var plan = new FlightPlan
                {
                    Name = $"{mission.Name} - Inspection Plan",
                    HomeLocation = new GeoPoint
                    {
                        Latitude = buildingCenter.Latitude - 0.0003,
                        Longitude = buildingCenter.Longitude
                    }
                };

                plan.TakeoffLocation = plan.HomeLocation;
                plan.LandingLocation = plan.HomeLocation;

                switch (inspectionType)
                {
                    case InspectionType.RoofInspection:
                        plan.Mode = FlightMode.GridMapping;
                        plan.Altitude = buildingHeight + 30;
                        plan.GimbalPitch = -90;
                        plan.Waypoints = GenerateRoofScanWaypoints(buildingCenter, buildingRadius, plan.Altitude);
                        break;

                    case InspectionType.FacadeInspection:
                        plan.Mode = FlightMode.VerticalScan;
                        plan.GimbalPitch = 0;
                        plan.Waypoints = GenerateFacadeScanWaypoints(buildingCenter, buildingHeight, buildingRadius);
                        break;

                    case InspectionType.StructuralInspection:
                    case InspectionType.ThermalSurvey:
                        plan.Mode = FlightMode.Orbit;
                        plan.Altitude = buildingHeight / 2;
                        plan.GimbalPitch = 0;
                        plan.Waypoints = GenerateOrbitWaypoints(buildingCenter, buildingRadius + 15, plan.Altitude, 12);
                        break;

                    default:
                        plan.Mode = FlightMode.PerimeterFlight;
                        plan.Altitude = buildingHeight + 10;
                        plan.Waypoints = GenerateOrbitWaypoints(buildingCenter, buildingRadius + 20, plan.Altitude, 8);
                        break;
                }

                plan.EstimatedDistance = CalculateTotalDistance(plan.Waypoints);
                plan.EstimatedFlightTime = plan.EstimatedDistance / 5.0 / 60 + plan.Waypoints.Count * 0.2;

                mission.FlightPlan = plan;
                return plan;
            }
        }

        private List<Waypoint> GenerateGridWaypoints(List<GeoPoint> boundary, double altitude,
            double spacingForward, double spacingSide)
        {
            var waypoints = new List<Waypoint>();

            // Get bounding box
            var minLat = boundary.Min(p => p.Latitude);
            var maxLat = boundary.Max(p => p.Latitude);
            var minLon = boundary.Min(p => p.Longitude);
            var maxLon = boundary.Max(p => p.Longitude);

            // Convert spacing to degrees (approximate)
            double latPerMeter = 1.0 / 111320;
            double lonPerMeter = 1.0 / (111320 * Math.Cos(minLat * Math.PI / 180));

            double latSpacing = spacingForward * latPerMeter;
            double lonSpacing = spacingSide * lonPerMeter;

            int sequence = 1;
            bool forward = true;

            for (double lon = minLon; lon <= maxLon; lon += lonSpacing)
            {
                if (forward)
                {
                    for (double lat = minLat; lat <= maxLat; lat += latSpacing)
                    {
                        waypoints.Add(CreatePhotoWaypoint(sequence++, lat, lon, altitude));
                    }
                }
                else
                {
                    for (double lat = maxLat; lat >= minLat; lat -= latSpacing)
                    {
                        waypoints.Add(CreatePhotoWaypoint(sequence++, lat, lon, altitude));
                    }
                }
                forward = !forward;
            }

            return waypoints;
        }

        private List<Waypoint> GenerateOrbitWaypoints(GeoPoint center, double radius, double altitude, int points)
        {
            var waypoints = new List<Waypoint>();
            double latPerMeter = 1.0 / 111320;
            double lonPerMeter = 1.0 / (111320 * Math.Cos(center.Latitude * Math.PI / 180));

            for (int i = 0; i < points; i++)
            {
                double angle = 2 * Math.PI * i / points;
                double lat = center.Latitude + (radius * Math.Cos(angle) * latPerMeter);
                double lon = center.Longitude + (radius * Math.Sin(angle) * lonPerMeter);
                double heading = (angle * 180 / Math.PI + 90) % 360;

                waypoints.Add(new Waypoint
                {
                    Sequence = i + 1,
                    Location = new GeoPoint { Latitude = lat, Longitude = lon, AltitudeAGL = altitude },
                    Heading = heading,
                    GimbalPitch = 0,
                    HoverTime = 3,
                    Actions = new List<WaypointAction>
                    {
                        new WaypointAction { Type = "TakePhoto" }
                    }
                });
            }

            return waypoints;
        }

        private List<Waypoint> GenerateRoofScanWaypoints(GeoPoint center, double radius, double altitude)
        {
            return GenerateGridWaypoints(
                new List<GeoPoint>
                {
                    new GeoPoint { Latitude = center.Latitude - radius/111320, Longitude = center.Longitude - radius/111320 },
                    new GeoPoint { Latitude = center.Latitude + radius/111320, Longitude = center.Longitude - radius/111320 },
                    new GeoPoint { Latitude = center.Latitude + radius/111320, Longitude = center.Longitude + radius/111320 },
                    new GeoPoint { Latitude = center.Latitude - radius/111320, Longitude = center.Longitude + radius/111320 }
                },
                altitude, 8, 8);
        }

        private List<Waypoint> GenerateFacadeScanWaypoints(GeoPoint center, double height, double radius)
        {
            var waypoints = new List<Waypoint>();
            int sequence = 1;
            double distance = radius + 15; // 15m standoff
            double latPerMeter = 1.0 / 111320;

            // Four sides of building
            double[] angles = { 0, 90, 180, 270 };

            foreach (double angle in angles)
            {
                double rad = angle * Math.PI / 180;
                double lat = center.Latitude + (distance * Math.Cos(rad) * latPerMeter);
                double lon = center.Longitude + (distance * Math.Sin(rad) * latPerMeter / Math.Cos(center.Latitude * Math.PI / 180));

                // Vertical scan from bottom to top
                for (double alt = 10; alt <= height + 5; alt += 5)
                {
                    waypoints.Add(new Waypoint
                    {
                        Sequence = sequence++,
                        Location = new GeoPoint { Latitude = lat, Longitude = lon, AltitudeAGL = alt },
                        Heading = (angle + 180) % 360,
                        GimbalPitch = 0,
                        HoverTime = 2,
                        Actions = new List<WaypointAction> { new WaypointAction { Type = "TakePhoto" } }
                    });
                }
            }

            return waypoints;
        }

        private Waypoint CreatePhotoWaypoint(int sequence, double lat, double lon, double altitude)
        {
            return new Waypoint
            {
                Sequence = sequence,
                Location = new GeoPoint { Latitude = lat, Longitude = lon, AltitudeAGL = altitude },
                GimbalPitch = -90,
                HoverTime = 2,
                Actions = new List<WaypointAction> { new WaypointAction { Type = "TakePhoto" } }
            };
        }

        private double CalculateTotalDistance(List<Waypoint> waypoints)
        {
            double total = 0;
            for (int i = 1; i < waypoints.Count; i++)
            {
                total += CalculateDistance(waypoints[i - 1].Location, waypoints[i].Location);
            }
            return total;
        }

        private double CalculateDistance(GeoPoint p1, GeoPoint p2)
        {
            // Haversine formula
            double R = 6371000; // Earth's radius in meters
            double lat1 = p1.Latitude * Math.PI / 180;
            double lat2 = p2.Latitude * Math.PI / 180;
            double dLat = (p2.Latitude - p1.Latitude) * Math.PI / 180;
            double dLon = (p2.Longitude - p1.Longitude) * Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double horizDistance = R * c;
            double vertDistance = Math.Abs(p2.AltitudeAGL - p1.AltitudeAGL);

            return Math.Sqrt(horizDistance * horizDistance + vertDistance * vertDistance);
        }

        #endregion

        #region Airspace & Weather

        /// <summary>Check airspace and request authorization if needed</summary>
        public async Task<AirspaceAuthorization> RequestAirspaceAuthorizationAsync(string missionId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_missions.TryGetValue(missionId, out var mission) || mission.FlightPlan == null)
                        return null;

                    var auth = new AirspaceAuthorization
                    {
                        CenterPoint = mission.FlightPlan.HomeLocation,
                        Radius = 500, // Default 500m radius
                        RequestDate = DateTime.UtcNow
                    };

                    // Simulate airspace check (in real implementation, this would call LAANC API)
                    var airspaceClass = DetermineAirspaceClass(mission.FlightPlan.HomeLocation);
                    auth.AirspaceClass = airspaceClass;

                    if (airspaceClass == AirspaceClass.ClassG)
                    {
                        auth.Status = AuthorizationStatus.NotRequired;
                        auth.ApprovedAltitude = _part107Limits["MaxAltitudeAGL"];
                        auth.ValidFrom = DateTime.UtcNow;
                        auth.ValidUntil = DateTime.UtcNow.AddDays(1);
                    }
                    else if (airspaceClass == AirspaceClass.Prohibited || airspaceClass == AirspaceClass.Restricted)
                    {
                        auth.Status = AuthorizationStatus.Denied;
                        auth.Notes = "Flight not permitted in this airspace";
                    }
                    else
                    {
                        // Simulated LAANC approval
                        auth.Status = AuthorizationStatus.Approved;
                        auth.LANCAuthorizationId = $"LAANC-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
                        auth.ApprovedAltitude = GetApprovedAltitude(airspaceClass);
                        auth.ValidFrom = DateTime.UtcNow;
                        auth.ValidUntil = DateTime.UtcNow.AddHours(4);
                        auth.ControllingFacility = GetControllingFacility(airspaceClass);
                    }

                    mission.Authorization = auth;
                    return auth;
                }
            });
        }

        /// <summary>Get weather briefing for mission</summary>
        public async Task<WeatherBrief> GetWeatherBriefingAsync(string missionId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_missions.TryGetValue(missionId, out var mission) || mission.FlightPlan == null)
                        return null;

                    // Simulated weather data (in real implementation, call weather API)
                    var weather = new WeatherBrief
                    {
                        BriefingTime = DateTime.UtcNow,
                        ValidFrom = DateTime.UtcNow,
                        ValidUntil = DateTime.UtcNow.AddHours(6),
                        Temperature = 22,
                        WindSpeed = 4.5,
                        WindGusts = 7.2,
                        WindDirection = 270,
                        Visibility = 10000,
                        CloudBase = 1500,
                        CloudCoverage = 25,
                        PrecipitationProbability = 10,
                        DensityAltitude = mission.FlightPlan.Altitude + 200
                    };

                    // Determine weather category
                    weather.Category = DetermineWeatherCategory(weather);

                    // Evaluate go/no-go
                    weather.GoNoGo = EvaluateWeatherGoNoGo(weather);
                    weather.Recommendation = weather.GoNoGo ?
                        "Weather conditions suitable for flight" :
                        "Weather conditions not suitable - recommend postponing";

                    // Check for hazards
                    if (weather.WindGusts > _weatherLimits["MaxWindGusts"])
                        weather.Hazards.Add("High wind gusts");
                    if (weather.Visibility < _weatherLimits["MinVisibility"])
                        weather.Hazards.Add("Reduced visibility");
                    if (weather.CloudBase < _weatherLimits["MinCloudBase"])
                        weather.Hazards.Add("Low ceiling");

                    mission.WeatherBriefing = weather;
                    return weather;
                }
            });
        }

        private AirspaceClass DetermineAirspaceClass(GeoPoint location)
        {
            // Simplified airspace determination
            // In real implementation, this would query airspace databases
            return AirspaceClass.ClassG; // Default to uncontrolled
        }

        private double GetApprovedAltitude(AirspaceClass airspaceClass)
        {
            return airspaceClass switch
            {
                AirspaceClass.ClassB => 100 * 0.3048, // 100ft
                AirspaceClass.ClassC => 200 * 0.3048, // 200ft
                AirspaceClass.ClassD => 300 * 0.3048, // 300ft
                AirspaceClass.ClassE => 400 * 0.3048, // 400ft
                _ => _part107Limits["MaxAltitudeAGL"]
            };
        }

        private string GetControllingFacility(AirspaceClass airspaceClass)
        {
            return airspaceClass switch
            {
                AirspaceClass.ClassB => "TRACON",
                AirspaceClass.ClassC => "Approach Control",
                AirspaceClass.ClassD => "Tower",
                _ => "N/A"
            };
        }

        private WeatherCategory DetermineWeatherCategory(WeatherBrief weather)
        {
            if (weather.Visibility >= 5000 && weather.CloudBase >= 1000)
                return WeatherCategory.VFR;
            if (weather.Visibility >= 3000 && weather.CloudBase >= 500)
                return WeatherCategory.MVFR;
            if (weather.Visibility >= 1500 && weather.CloudBase >= 150)
                return WeatherCategory.IFR;
            return WeatherCategory.LIFR;
        }

        private bool EvaluateWeatherGoNoGo(WeatherBrief weather)
        {
            return weather.WindSpeed <= _weatherLimits["MaxWindSpeed"] &&
                   weather.WindGusts <= _weatherLimits["MaxWindGusts"] &&
                   weather.Visibility >= _weatherLimits["MinVisibility"] &&
                   weather.CloudBase >= _weatherLimits["MinCloudBase"] &&
                   weather.PrecipitationProbability <= _weatherLimits["MaxPrecipProbability"];
        }

        #endregion

        #region Pre-Flight & Operations

        /// <summary>Generate pre-flight checklist for mission</summary>
        public PreFlightChecklist GeneratePreFlightChecklist(string missionId)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                var checklist = new PreFlightChecklist
                {
                    Items = new List<ChecklistItem>()
                };

                // Add standard checklist items
                var template = _checklistTemplates.FirstOrDefault(t => t.DroneType == GetDroneType(mission.DroneId));
                if (template != null)
                {
                    foreach (var item in template.Items)
                    {
                        checklist.Items.Add(new ChecklistItem
                        {
                            Category = item.Category,
                            Item = item.Item,
                            Passed = false
                        });
                    }
                }

                // Add mission-specific items
                if (mission.InspectionType == InspectionType.ThermalSurvey)
                {
                    checklist.Items.Add(new ChecklistItem { Category = "Sensor", Item = "Thermal camera calibrated", Passed = false });
                    checklist.Items.Add(new ChecklistItem { Category = "Sensor", Item = "Emissivity settings configured", Passed = false });
                }

                mission.PreFlight = checklist;
                return checklist;
            }
        }

        /// <summary>Complete pre-flight checklist</summary>
        public bool CompletePreFlightChecklist(string missionId, string completedBy, Dictionary<int, bool> itemResults)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission) || mission.PreFlight == null)
                    return false;

                for (int i = 0; i < mission.PreFlight.Items.Count; i++)
                {
                    if (itemResults.TryGetValue(i, out var passed))
                        mission.PreFlight.Items[i].Passed = passed;
                }

                mission.PreFlight.CompletedAt = DateTime.UtcNow;
                mission.PreFlight.CompletedBy = completedBy;
                mission.PreFlight.AllItemsPassed = mission.PreFlight.Items.All(i => i.Passed);

                if (mission.PreFlight.AllItemsPassed)
                    mission.Status = MissionStatus.ReadyForFlight;

                return mission.PreFlight.AllItemsPassed;
            }
        }

        /// <summary>Start mission execution</summary>
        public bool StartMission(string missionId)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return false;

                if (mission.Status != MissionStatus.ReadyForFlight)
                {
                    OnMissionAlert(new MissionAlertEventArgs
                    {
                        MissionId = missionId,
                        AlertType = "PreFlightIncomplete",
                        Message = "Cannot start mission - pre-flight checklist not complete",
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }

                mission.Status = MissionStatus.InProgress;
                mission.ActualStartTime = DateTime.UtcNow;

                // Update drone and pilot statistics
                if (_drones.TryGetValue(mission.DroneId, out var drone))
                    drone.TotalFlights++;
                if (_pilots.TryGetValue(mission.PilotId, out var pilot))
                    pilot.TotalFlights++;

                return true;
            }
        }

        /// <summary>Complete mission with post-flight log</summary>
        public PostFlightLog CompleteMission(string missionId, string completedBy, PostFlightLog log)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                log.CompletedAt = DateTime.UtcNow;
                log.CompletedBy = completedBy;

                mission.PostFlight = log;
                mission.ActualEndTime = DateTime.UtcNow;
                mission.Status = log.Incidents.Count > 0 ? MissionStatus.Completed : MissionStatus.Completed;

                // Update statistics
                if (_drones.TryGetValue(mission.DroneId, out var drone))
                {
                    drone.TotalFlightHours += log.ActualFlightTime / 60;
                }
                if (_pilots.TryGetValue(mission.PilotId, out var pilot))
                {
                    pilot.TotalFlightHours += log.ActualFlightTime / 60;
                }

                // Update project statistics
                if (_projects.TryGetValue(mission.ProjectId, out var project))
                {
                    project.Statistics.CompletedMissions++;
                    project.Statistics.TotalFlightHours += log.ActualFlightTime / 60;
                    project.Statistics.TotalPhotos += log.PhotosCaptured;
                }

                // Check for maintenance flags
                if (log.MaintenanceFlags.Count > 0)
                {
                    OnMissionAlert(new MissionAlertEventArgs
                    {
                        MissionId = missionId,
                        AlertType = "MaintenanceRequired",
                        Message = $"Maintenance flags raised: {string.Join(", ", log.MaintenanceFlags)}",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return log;
            }
        }

        private DroneType GetDroneType(string droneId)
        {
            lock (_lock)
            {
                return _drones.TryGetValue(droneId, out var drone) ? drone.Type : DroneType.Multirotor;
            }
        }

        #endregion

        #region Thermal Analysis

        /// <summary>Perform thermal analysis on captured data</summary>
        public async Task<ThermalAnalysis> AnalyzeThermalDataAsync(string missionId, double ambientTemp,
            double emissivity = 0.95, double humidity = 50)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_missions.TryGetValue(missionId, out var mission))
                        return null;

                    var analysis = new ThermalAnalysis
                    {
                        MissionId = missionId,
                        ProjectId = mission.ProjectId,
                        AnalysisDate = DateTime.UtcNow,
                        AmbientTemperature = ambientTemp,
                        Emissivity = emissivity,
                        Humidity = humidity,
                        ReflectedTemperature = ambientTemp - 5,
                        Distance = 15, // meters
                        Anomalies = new List<ThermalAnomaly>(),
                        Statistics = new ThermalStatistics()
                    };

                    // Simulate thermal analysis results
                    var random = new Random();
                    analysis.MinTemperature = ambientTemp - 5 + random.NextDouble() * 3;
                    analysis.MaxTemperature = ambientTemp + 10 + random.NextDouble() * 15;
                    analysis.AverageTemperature = (analysis.MinTemperature + analysis.MaxTemperature) / 2;
                    analysis.DeltaT = analysis.MaxTemperature - analysis.MinTemperature;

                    // Generate sample anomalies based on inspection type
                    if (mission.InspectionType == InspectionType.ThermalSurvey ||
                        mission.InspectionType == InspectionType.RoofInspection)
                    {
                        analysis.Anomalies = GenerateThermalAnomalies(mission, ambientTemp);
                    }

                    // Calculate statistics
                    analysis.Statistics.TotalImagesAnalyzed = mission.CapturedData
                        .Where(d => d.SensorType == SensorType.Thermal)
                        .Sum(d => d.FileCount);
                    analysis.Statistics.AnomaliesDetected = analysis.Anomalies.Count;
                    analysis.Statistics.CriticalAnomalies = analysis.Anomalies.Count(a => a.Severity == "Critical");
                    analysis.Statistics.ModerateAnomalies = analysis.Anomalies.Count(a => a.Severity == "Moderate");
                    analysis.Statistics.MinorAnomalies = analysis.Anomalies.Count(a => a.Severity == "Minor");
                    analysis.Statistics.EstimatedTotalEnergyLoss = analysis.Anomalies.Sum(a => a.EnergyLoss);
                    analysis.Statistics.EstimatedRepairCost = analysis.Anomalies.Sum(a => a.EstimatedRepairCost);

                    // Generate recommendations
                    if (analysis.Statistics.CriticalAnomalies > 0)
                        analysis.Recommendations.Add("Immediate attention required for critical thermal anomalies");
                    if (analysis.Statistics.EstimatedTotalEnergyLoss > 10000)
                        analysis.Recommendations.Add("Significant energy loss detected - recommend comprehensive envelope review");
                    if (analysis.Anomalies.Any(a => a.Type == ThermalAnomalyType.MoistureIntrusion))
                        analysis.Recommendations.Add("Moisture intrusion detected - investigate source immediately");

                    _thermalAnalyses[analysis.Id] = analysis;
                    return analysis;
                }
            });
        }

        private List<ThermalAnomaly> GenerateThermalAnomalies(FlightMission mission, double ambientTemp)
        {
            var anomalies = new List<ThermalAnomaly>();
            var random = new Random();
            var anomalyTypes = Enum.GetValues(typeof(ThermalAnomalyType)).Cast<ThermalAnomalyType>().ToList();

            int anomalyCount = random.Next(3, 12);

            for (int i = 0; i < anomalyCount; i++)
            {
                var type = anomalyTypes[random.Next(anomalyTypes.Count)];
                var deltaT = 5 + random.NextDouble() * 20;
                var severity = deltaT > 15 ? "Critical" : deltaT > 10 ? "Moderate" : "Minor";

                anomalies.Add(new ThermalAnomaly
                {
                    Type = type,
                    Location = $"Zone {(char)('A' + random.Next(6))}-{random.Next(1, 20)}",
                    Temperature = ambientTemp + deltaT,
                    DeltaT = deltaT,
                    Area = 0.5 + random.NextDouble() * 10,
                    Severity = severity,
                    Description = GetAnomalyDescription(type),
                    RecommendedAction = GetAnomalyRecommendation(type, severity),
                    EstimatedRepairCost = GetRepairCostEstimate(type, severity),
                    EnergyLoss = CalculateEnergyLoss(type, deltaT)
                });
            }

            return anomalies;
        }

        private string GetAnomalyDescription(ThermalAnomalyType type)
        {
            return type switch
            {
                ThermalAnomalyType.ThermalBridging => "Thermal bridge detected at structural connection",
                ThermalAnomalyType.MoistureIntrusion => "Potential moisture infiltration identified",
                ThermalAnomalyType.InsulationDeficiency => "Missing or degraded insulation",
                ThermalAnomalyType.AirLeakage => "Air infiltration at building envelope",
                ThermalAnomalyType.ElectricalHotspot => "Electrical component overheating",
                ThermalAnomalyType.MechanicalOverheat => "Mechanical equipment elevated temperature",
                ThermalAnomalyType.SolarPanelDefect => "Photovoltaic cell hot spot defect",
                ThermalAnomalyType.RoofingDefect => "Roofing membrane anomaly",
                ThermalAnomalyType.HVACImbalance => "HVAC distribution imbalance",
                ThermalAnomalyType.PipeLeakage => "Potential pipe leak identified",
                _ => "Thermal anomaly detected"
            };
        }

        private string GetAnomalyRecommendation(ThermalAnomalyType type, string severity)
        {
            var urgency = severity == "Critical" ? "Immediate" : severity == "Moderate" ? "Schedule" : "Monitor";

            return type switch
            {
                ThermalAnomalyType.MoistureIntrusion => $"{urgency} - Investigate water source and repair envelope",
                ThermalAnomalyType.ElectricalHotspot => $"{urgency} - De-energize and inspect electrical connection",
                ThermalAnomalyType.InsulationDeficiency => $"{urgency} - Add/replace insulation in affected area",
                _ => $"{urgency} - Further investigation recommended"
            };
        }

        private double GetRepairCostEstimate(ThermalAnomalyType type, string severity)
        {
            var baseCost = type switch
            {
                ThermalAnomalyType.MoistureIntrusion => 5000,
                ThermalAnomalyType.ElectricalHotspot => 2500,
                ThermalAnomalyType.RoofingDefect => 3000,
                ThermalAnomalyType.InsulationDeficiency => 1500,
                ThermalAnomalyType.SolarPanelDefect => 4000,
                _ => 1000
            };

            var multiplier = severity == "Critical" ? 2.0 : severity == "Moderate" ? 1.5 : 1.0;
            return baseCost * multiplier;
        }

        private double CalculateEnergyLoss(ThermalAnomalyType type, double deltaT)
        {
            // Simplified energy loss calculation (kWh/year)
            var baseLoss = type switch
            {
                ThermalAnomalyType.ThermalBridging => 500,
                ThermalAnomalyType.InsulationDeficiency => 800,
                ThermalAnomalyType.AirLeakage => 600,
                ThermalAnomalyType.HVACImbalance => 1200,
                _ => 200
            };

            return baseLoss * (deltaT / 10);
        }

        #endregion

        #region Progress Monitoring

        /// <summary>Create progress capture from mission data</summary>
        public async Task<ProgressCapture> CreateProgressCaptureAsync(string missionId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_missions.TryGetValue(missionId, out var mission))
                        return null;

                    var existingCaptures = _progressCaptures.Values
                        .Where(p => p.ProjectId == mission.ProjectId)
                        .OrderBy(p => p.SequenceNumber)
                        .ToList();

                    var capture = new ProgressCapture
                    {
                        ProjectId = mission.ProjectId,
                        MissionId = missionId,
                        CaptureDate = DateTime.UtcNow,
                        SequenceNumber = existingCaptures.Count + 1,
                        PhotoCount = mission.CapturedData.Sum(d => d.FileCount),
                        GroundSamplingDistance = mission.FlightPlan?.GroundSamplingDistance ?? 2.5,
                        Metrics = new ProgressMetrics()
                    };

                    // Simulate progress metrics calculation
                    var random = new Random();
                    capture.Metrics.SiteAreaTotal = _projects.TryGetValue(mission.ProjectId, out var project) ?
                        project.SiteArea : 10000;
                    capture.Metrics.OverallProgress = Math.Min(100, (existingCaptures.Count + 1) * 8 + random.Next(5));
                    capture.Metrics.AreaExcavated = capture.Metrics.SiteAreaTotal * Math.Min(1, capture.Metrics.OverallProgress / 15);
                    capture.Metrics.AreaFoundation = capture.Metrics.SiteAreaTotal * Math.Min(1, Math.Max(0, (capture.Metrics.OverallProgress - 10) / 20));
                    capture.Metrics.AreaSuperstructure = capture.Metrics.SiteAreaTotal * Math.Min(1, Math.Max(0, (capture.Metrics.OverallProgress - 25) / 40));
                    capture.Metrics.FloorsComplete = (int)(capture.Metrics.OverallProgress / 10);
                    capture.Metrics.EquipmentCount = 5 + random.Next(15);
                    capture.Metrics.WorkerCountEstimate = 20 + random.Next(80);

                    // Compare with previous capture if available
                    if (existingCaptures.Count > 0)
                    {
                        var previousCapture = existingCaptures.Last();
                        capture.Comparisons.Add(new ProgressComparison
                        {
                            PreviousCaptureId = previousCapture.Id,
                            PreviousDate = previousCapture.CaptureDate,
                            DaysElapsed = (capture.CaptureDate - previousCapture.CaptureDate).TotalDays,
                            ProgressChange = capture.Metrics.OverallProgress - previousCapture.Metrics.OverallProgress,
                            ObservedChanges = GenerateObservedChanges(previousCapture.Metrics, capture.Metrics)
                        });
                    }

                    _progressCaptures[capture.Id] = capture;
                    return capture;
                }
            });
        }

        private List<string> GenerateObservedChanges(ProgressMetrics previous, ProgressMetrics current)
        {
            var changes = new List<string>();

            if (current.AreaFoundation > previous.AreaFoundation)
                changes.Add($"Foundation work progressed {(current.AreaFoundation - previous.AreaFoundation):F0} sqm");
            if (current.FloorsComplete > previous.FloorsComplete)
                changes.Add($"Completed {current.FloorsComplete - previous.FloorsComplete} additional floor(s)");
            if (current.AreaSuperstructure > previous.AreaSuperstructure)
                changes.Add("Superstructure construction ongoing");
            if (current.EquipmentCount != previous.EquipmentCount)
                changes.Add($"Equipment count changed from {previous.EquipmentCount} to {current.EquipmentCount}");

            return changes;
        }

        #endregion

        #region Mapping Deliverables

        /// <summary>Create mapping deliverable from mission</summary>
        public MappingDeliverable CreateDeliverable(string missionId, OutputType type, string coordinateSystem)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                var deliverable = new MappingDeliverable
                {
                    ProjectId = mission.ProjectId,
                    MissionId = missionId,
                    Type = type,
                    Status = ProcessingStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    CoordinateSystem = coordinateSystem
                };

                // Set specifications based on type
                deliverable.Specifications = type switch
                {
                    OutputType.Orthomosaic => new Dictionary<string, object>
                    {
                        ["Format"] = "GeoTIFF",
                        ["Bands"] = "RGB",
                        ["BitDepth"] = 8
                    },
                    OutputType.PointCloud => new Dictionary<string, object>
                    {
                        ["Format"] = "LAS 1.4",
                        ["Classification"] = true,
                        ["Colorized"] = true
                    },
                    OutputType.DSM or OutputType.DTM => new Dictionary<string, object>
                    {
                        ["Format"] = "GeoTIFF",
                        ["BitDepth"] = 32,
                        ["DataType"] = "Float"
                    },
                    _ => new Dictionary<string, object>()
                };

                _deliverables[deliverable.Id] = deliverable;

                if (_projects.TryGetValue(mission.ProjectId, out var project))
                {
                    project.Deliverables.Add(deliverable.Id);
                }

                return deliverable;
            }
        }

        /// <summary>Update deliverable processing status</summary>
        public void UpdateDeliverableStatus(string deliverableId, ProcessingStatus status,
            string filePath = null, double? fileSize = null)
        {
            lock (_lock)
            {
                if (!_deliverables.TryGetValue(deliverableId, out var deliverable))
                    return;

                deliverable.Status = status;

                if (status == ProcessingStatus.Completed)
                {
                    deliverable.CompletedAt = DateTime.UtcNow;
                    if (filePath != null) deliverable.FilePath = filePath;
                    if (fileSize.HasValue) deliverable.FileSize = fileSize.Value;

                    if (_projects.TryGetValue(deliverable.ProjectId, out var project))
                    {
                        project.Statistics.DeliverablesGenerated++;
                    }
                }
            }
        }

        #endregion

        #region Part 107 Compliance

        /// <summary>Verify Part 107 compliance for mission</summary>
        public ComplianceRecord VerifyCompliance(string missionId)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                var record = new ComplianceRecord
                {
                    MissionId = missionId,
                    PilotId = mission.PilotId,
                    DroneId = mission.DroneId,
                    FlightDate = mission.ScheduledDate,
                    Part107Compliant = true,
                    Checks = new List<ComplianceCheck>(),
                    Violations = new List<ViolationType>()
                };

                // Check pilot certification
                if (_pilots.TryGetValue(mission.PilotId, out var pilot))
                {
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Remote Pilot Certificate",
                        CFRReference = "14 CFR 107.12",
                        Compliant = pilot.CertificationExpiry > DateTime.UtcNow,
                        Evidence = pilot.CertificateNumber
                    });

                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Recurrent Training (24 months)",
                        CFRReference = "14 CFR 107.65",
                        Compliant = pilot.LastRecurrentTraining > DateTime.UtcNow.AddMonths(-24)
                    });
                }

                // Check drone registration
                if (_drones.TryGetValue(mission.DroneId, out var drone))
                {
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Aircraft Registration",
                        CFRReference = "14 CFR 107.13",
                        Compliant = drone.RegistrationExpiry > DateTime.UtcNow,
                        Evidence = drone.FAARegistration
                    });

                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Aircraft Maintenance",
                        CFRReference = "14 CFR 107.15",
                        Compliant = drone.NextMaintenanceDue > DateTime.UtcNow
                    });
                }

                // Check flight plan compliance
                if (mission.FlightPlan != null)
                {
                    bool altitudeCompliant = mission.FlightPlan.Altitude <= _part107Limits["MaxAltitudeAGL"];
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Maximum Altitude (400ft AGL)",
                        CFRReference = "14 CFR 107.51(b)",
                        Compliant = altitudeCompliant
                    });

                    if (!altitudeCompliant)
                        record.Violations.Add(ViolationType.AltitudeExceedance);

                    bool speedCompliant = mission.FlightPlan.Speed <= _part107Limits["MaxSpeed"];
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Maximum Speed (100mph)",
                        CFRReference = "14 CFR 107.51(a)",
                        Compliant = speedCompliant
                    });
                }

                // Check airspace authorization
                if (mission.Authorization != null)
                {
                    bool airspaceCompliant = mission.Authorization.Status == AuthorizationStatus.Approved ||
                                            mission.Authorization.Status == AuthorizationStatus.NotRequired;
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Airspace Authorization",
                        CFRReference = "14 CFR 107.41",
                        Compliant = airspaceCompliant,
                        Evidence = mission.Authorization.LANCAuthorizationId
                    });

                    if (!airspaceCompliant)
                        record.Violations.Add(ViolationType.AirspaceViolation);
                }

                // Check weather conditions
                if (mission.WeatherBriefing != null)
                {
                    bool visibilityCompliant = mission.WeatherBriefing.Visibility >= _part107Limits["MinVisibility"];
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Minimum Visibility (3 statute miles)",
                        CFRReference = "14 CFR 107.51(c)",
                        Compliant = visibilityCompliant
                    });

                    bool cloudCompliant = mission.WeatherBriefing.CloudBase >= _part107Limits["MinCloudClearanceVertical"];
                    record.Checks.Add(new ComplianceCheck
                    {
                        Requirement = "Cloud Clearance (500ft below)",
                        CFRReference = "14 CFR 107.51(d)",
                        Compliant = cloudCompliant
                    });
                }

                // Visual observer requirement
                record.Checks.Add(new ComplianceCheck
                {
                    Requirement = "Visual Line of Sight",
                    CFRReference = "14 CFR 107.31",
                    Compliant = !string.IsNullOrEmpty(mission.VisualObserverId) || mission.FlightPlan?.EstimatedDistance < 500
                });

                // Daylight operation
                bool isDaylight = mission.ScheduledDate.Hour >= 6 && mission.ScheduledDate.Hour <= 18;
                record.Checks.Add(new ComplianceCheck
                {
                    Requirement = "Daylight Operations",
                    CFRReference = "14 CFR 107.29",
                    Compliant = isDaylight || pilot?.Waivers.Contains("Night Operations") == true
                });

                if (!isDaylight && pilot?.Waivers.Contains("Night Operations") != true)
                    record.Violations.Add(ViolationType.NightFlight);

                record.Part107Compliant = record.Checks.All(c => c.Compliant);
                _complianceRecords[record.Id] = record;

                return record;
            }
        }

        /// <summary>Get compliance summary for project</summary>
        public Dictionary<string, object> GetComplianceSummary(string projectId)
        {
            lock (_lock)
            {
                var projectRecords = _complianceRecords.Values
                    .Where(r => _missions.TryGetValue(r.MissionId, out var m) && m.ProjectId == projectId)
                    .ToList();

                return new Dictionary<string, object>
                {
                    ["TotalMissions"] = projectRecords.Count,
                    ["CompliantMissions"] = projectRecords.Count(r => r.Part107Compliant),
                    ["ViolationsCount"] = projectRecords.Sum(r => r.Violations.Count),
                    ["ComplianceRate"] = projectRecords.Count > 0 ?
                        (double)projectRecords.Count(r => r.Part107Compliant) / projectRecords.Count * 100 : 100,
                    ["CommonViolations"] = projectRecords
                        .SelectMany(r => r.Violations)
                        .GroupBy(v => v)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count())
                };
            }
        }

        #endregion

        #region Reporting

        /// <summary>Generate comprehensive mission report</summary>
        public Dictionary<string, object> GenerateMissionReport(string missionId)
        {
            lock (_lock)
            {
                if (!_missions.TryGetValue(missionId, out var mission))
                    return null;

                var report = new Dictionary<string, object>
                {
                    ["MissionId"] = missionId,
                    ["MissionName"] = mission.Name,
                    ["ProjectId"] = mission.ProjectId,
                    ["Status"] = mission.Status.ToString(),
                    ["InspectionType"] = mission.InspectionType.ToString(),
                    ["ScheduledDate"] = mission.ScheduledDate,
                    ["ActualStartTime"] = mission.ActualStartTime,
                    ["ActualEndTime"] = mission.ActualEndTime
                };

                // Pilot info
                if (_pilots.TryGetValue(mission.PilotId, out var pilot))
                {
                    report["Pilot"] = new
                    {
                        pilot.Name,
                        pilot.CertificateNumber,
                        pilot.TotalFlightHours
                    };
                }

                // Drone info
                if (_drones.TryGetValue(mission.DroneId, out var drone))
                {
                    report["Drone"] = new
                    {
                        drone.Name,
                        drone.Model,
                        drone.FAARegistration
                    };
                }

                // Flight plan summary
                if (mission.FlightPlan != null)
                {
                    report["FlightPlan"] = new
                    {
                        mission.FlightPlan.Mode,
                        mission.FlightPlan.Altitude,
                        mission.FlightPlan.EstimatedFlightTime,
                        mission.FlightPlan.EstimatedDistance,
                        WaypointCount = mission.FlightPlan.Waypoints.Count
                    };
                }

                // Weather conditions
                if (mission.WeatherBriefing != null)
                {
                    report["Weather"] = new
                    {
                        mission.WeatherBriefing.Category,
                        mission.WeatherBriefing.Temperature,
                        mission.WeatherBriefing.WindSpeed,
                        mission.WeatherBriefing.Visibility,
                        mission.WeatherBriefing.GoNoGo
                    };
                }

                // Post-flight summary
                if (mission.PostFlight != null)
                {
                    report["PostFlight"] = new
                    {
                        mission.PostFlight.ActualFlightTime,
                        mission.PostFlight.DistanceTraveled,
                        mission.PostFlight.PhotosCaptured,
                        mission.PostFlight.MaxAltitude,
                        BatteryUsed = mission.PostFlight.BatteryStart - mission.PostFlight.BatteryEnd
                    };
                }

                // Captured data summary
                report["DataCaptured"] = mission.CapturedData.Select(d => new
                {
                    d.SensorType,
                    d.FileCount,
                    d.TotalSize,
                    d.ProcessingStatus
                }).ToList();

                // Compliance status
                var compliance = _complianceRecords.Values.FirstOrDefault(r => r.MissionId == missionId);
                if (compliance != null)
                {
                    report["Compliance"] = new
                    {
                        compliance.Part107Compliant,
                        ChecksPassed = compliance.Checks.Count(c => c.Compliant),
                        TotalChecks = compliance.Checks.Count,
                        Violations = compliance.Violations.Select(v => v.ToString()).ToList()
                    };
                }

                return report;
            }
        }

        /// <summary>Get project statistics</summary>
        public DroneProjectStatistics GetProjectStatistics(string projectId)
        {
            lock (_lock)
            {
                if (_projects.TryGetValue(projectId, out var project))
                {
                    // Recalculate statistics
                    var missions = _missions.Values.Where(m => m.ProjectId == projectId).ToList();
                    var analyses = _thermalAnalyses.Values.Where(a => a.ProjectId == projectId).ToList();

                    project.Statistics.TotalMissions = missions.Count;
                    project.Statistics.CompletedMissions = missions.Count(m => m.Status == MissionStatus.Completed);
                    project.Statistics.TotalFlightHours = missions
                        .Where(m => m.PostFlight != null)
                        .Sum(m => m.PostFlight.ActualFlightTime / 60);
                    project.Statistics.TotalPhotos = missions
                        .Where(m => m.PostFlight != null)
                        .Sum(m => m.PostFlight.PhotosCaptured);
                    project.Statistics.ThermalAnomaliesFound = analyses.Sum(a => a.Anomalies.Count);
                    project.Statistics.DeliverablesGenerated = _deliverables.Values
                        .Count(d => d.ProjectId == projectId && d.Status == ProcessingStatus.Completed);

                    return project.Statistics;
                }
                return null;
            }
        }

        #endregion

        #region Initialization

        private void InitializeChecklistTemplates()
        {
            _checklistTemplates.Add(new PreFlightChecklistTemplate
            {
                DroneType = DroneType.Multirotor,
                Items = new List<ChecklistTemplateItem>
                {
                    new() { Category = "Documentation", Item = "Pilot certificate available" },
                    new() { Category = "Documentation", Item = "Aircraft registration verified" },
                    new() { Category = "Documentation", Item = "Airspace authorization confirmed" },
                    new() { Category = "Documentation", Item = "Weather briefing obtained" },
                    new() { Category = "Aircraft", Item = "Visual inspection complete - no damage" },
                    new() { Category = "Aircraft", Item = "Propellers secure and undamaged" },
                    new() { Category = "Aircraft", Item = "Battery fully charged (>95%)" },
                    new() { Category = "Aircraft", Item = "Battery firmly seated" },
                    new() { Category = "Aircraft", Item = "SD card installed with space available" },
                    new() { Category = "Aircraft", Item = "Gimbal moves freely" },
                    new() { Category = "Aircraft", Item = "Lens clean and unobstructed" },
                    new() { Category = "Controller", Item = "Controller battery charged" },
                    new() { Category = "Controller", Item = "Control sticks calibrated" },
                    new() { Category = "Controller", Item = "Mobile device charged and connected" },
                    new() { Category = "Controller", Item = "Flight app updated" },
                    new() { Category = "Environment", Item = "No people in takeoff/landing area" },
                    new() { Category = "Environment", Item = "No overhead obstacles" },
                    new() { Category = "Environment", Item = "Wind conditions acceptable" },
                    new() { Category = "Environment", Item = "Visibility adequate (>3 miles)" },
                    new() { Category = "Systems", Item = "GPS lock acquired (>10 satellites)" },
                    new() { Category = "Systems", Item = "Compass calibrated" },
                    new() { Category = "Systems", Item = "Return-to-home altitude set" },
                    new() { Category = "Systems", Item = "Geofence configured" },
                    new() { Category = "Systems", Item = "Flight mode set correctly" }
                }
            });
        }

        #endregion

        #region Events

        private void OnMissionAlert(MissionAlertEventArgs e) => MissionAlert?.Invoke(this, e);

        #endregion
    }

    #region Supporting Classes

    public class PreFlightChecklistTemplate
    {
        public DroneType DroneType { get; set; }
        public List<ChecklistTemplateItem> Items { get; set; } = new();
    }

    public class ChecklistTemplateItem
    {
        public string Category { get; set; }
        public string Item { get; set; }
    }

    #endregion
}
