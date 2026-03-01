// ===================================================================
// StingBIM Robotics Intelligence Engine
// Construction robotics, automation, and human-robot collaboration
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RoboticsIntelligence
{
    #region Enums

    /// <summary>
    /// Types of construction robots
    /// </summary>
    public enum RobotType
    {
        Welding,
        Bricklaying,
        ConcretePrinting,
        ConcreteFinishing,
        Rebar,
        Drilling,
        Demolition,
        MaterialHandling,
        SiteInspection,
        SurveyingMapping,
        PaintingCoating,
        SteelErection,
        FacadeInstallation,
        DryWall,
        Excavation,
        Grading,
        Paving,
        Autonomous3DPrinting,
        TileInstallation,
        PlasteringSpraying
    }

    /// <summary>
    /// Robot operational status
    /// </summary>
    public enum RobotStatus
    {
        Offline,
        Initializing,
        Idle,
        Operating,
        Paused,
        EmergencyStop,
        Maintenance,
        Charging,
        Calibrating,
        Error,
        Transporting
    }

    /// <summary>
    /// Task status for robot assignments
    /// </summary>
    public enum RobotTaskStatus
    {
        Queued,
        Assigned,
        InProgress,
        Paused,
        Completed,
        Failed,
        Cancelled,
        RequiresHumanIntervention
    }

    /// <summary>
    /// Task priority levels
    /// </summary>
    public enum TaskPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4,
        Emergency = 5
    }

    /// <summary>
    /// Autonomy levels for robot operations
    /// </summary>
    public enum AutonomyLevel
    {
        Manual,                     // Full human control
        Assisted,                   // Human operates with robot assistance
        SemiAutonomous,             // Robot operates with human supervision
        SupervisedAutonomy,         // Robot operates autonomously, human monitors
        FullAutonomy                // Complete autonomous operation
    }

    /// <summary>
    /// Safety zone classifications
    /// </summary>
    public enum SafetyZoneType
    {
        NoEntry,                    // Robot exclusive zone
        RestrictedEntry,            // Authorized personnel only
        CollaborativeZone,          // Human-robot collaboration area
        SafeOperatingZone,          // Normal operations permitted
        EmergencyZone               // Emergency assembly/access
    }

    /// <summary>
    /// Human-robot collaboration modes
    /// </summary>
    public enum CollaborationMode
    {
        CoExistence,                // Shared space, no direct interaction
        Sequential,                 // Take turns in shared workspace
        Cooperative,                // Work together on same task
        Responsive,                 // Robot assists human dynamically
        Handover                    // Object/task handoff between human and robot
    }

    /// <summary>
    /// Welding types for robotic welding
    /// </summary>
    public enum WeldingType
    {
        MIG,                        // Metal Inert Gas
        TIG,                        // Tungsten Inert Gas
        Stick,                      // Shielded Metal Arc
        FluxCored,                  // Flux-Cored Arc Welding
        Submerged,                  // Submerged Arc Welding
        Spot,                       // Resistance Spot Welding
        Laser,                      // Laser Beam Welding
        Plasma                      // Plasma Arc Welding
    }

    /// <summary>
    /// Concrete printing materials
    /// </summary>
    public enum PrintingMaterial
    {
        StandardConcrete,
        HighPerformanceConcrete,
        FiberReinforcedConcrete,
        GeopolymerConcrete,
        RecycledAggregateConcrete,
        LightweightConcrete,
        SelfHealingConcrete,
        Mortar
    }

    /// <summary>
    /// Brick patterns for robotic bricklaying
    /// </summary>
    public enum BrickPattern
    {
        RunningBond,
        StackBond,
        CommonBond,
        FlemishBond,
        EnglishBond,
        HerringBone,
        BasketWeave,
        Custom
    }

    /// <summary>
    /// Equipment categories for autonomous operations
    /// </summary>
    public enum AutonomousEquipmentType
    {
        Excavator,
        BullDozer,
        Loader,
        DumpTruck,
        Crane,
        Forklift,
        ConveyorSystem,
        MaterialHoist,
        CompactRoller,
        Grader,
        Paver,
        ConcreteMixer,
        PumpTruck,
        DrillRig
    }

    /// <summary>
    /// Maintenance types
    /// </summary>
    public enum MaintenanceType
    {
        Preventive,
        Corrective,
        Predictive,
        ConditionBased,
        Emergency
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical,
        Emergency
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Robot configuration and registration
    /// </summary>
    public class ConstructionRobot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SerialNumber { get; set; }
        public string Name { get; set; }
        public RobotType Type { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public RobotStatus Status { get; set; } = RobotStatus.Offline;
        public AutonomyLevel AutonomyLevel { get; set; } = AutonomyLevel.SemiAutonomous;
        public RobotCapabilities Capabilities { get; set; } = new();
        public RobotPosition CurrentPosition { get; set; }
        public double BatteryLevel { get; set; } = 100;
        public DateTime LastMaintenanceDate { get; set; }
        public DateTime NextMaintenanceDate { get; set; }
        public List<string> CertifiedOperators { get; set; } = new();
        public RobotPerformanceMetrics PerformanceMetrics { get; set; } = new();
        public List<MaintenanceRecord> MaintenanceHistory { get; set; } = new();
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Robot capabilities specification
    /// </summary>
    public class RobotCapabilities
    {
        public double MaxPayload { get; set; }          // kg
        public double MaxReach { get; set; }            // meters
        public double MaxSpeed { get; set; }            // m/s
        public double PositionAccuracy { get; set; }    // mm
        public double RepeatAccuracy { get; set; }      // mm
        public int DegreesOfFreedom { get; set; }
        public List<string> ToolCompatibility { get; set; } = new();
        public List<string> MaterialCompatibility { get; set; } = new();
        public EnvironmentRating EnvironmentRating { get; set; } = new();
        public List<string> Sensors { get; set; } = new();
        public CommunicationCapabilities Communications { get; set; } = new();
    }

    /// <summary>
    /// Environmental operating ratings
    /// </summary>
    public class EnvironmentRating
    {
        public double MinTemperature { get; set; } = -10;   // Celsius
        public double MaxTemperature { get; set; } = 45;
        public double MaxHumidity { get; set; } = 95;       // Percentage
        public string IPRating { get; set; } = "IP65";
        public bool OutdoorCapable { get; set; } = true;
        public double MaxWindSpeed { get; set; } = 15;      // m/s
    }

    /// <summary>
    /// Communication capabilities
    /// </summary>
    public class CommunicationCapabilities
    {
        public bool WiFi { get; set; } = true;
        public bool Bluetooth { get; set; } = true;
        public bool Cellular5G { get; set; }
        public bool LoRaWAN { get; set; }
        public bool Ethernet { get; set; } = true;
        public List<string> Protocols { get; set; } = new() { "OPC-UA", "MQTT", "ROS2" };
    }

    /// <summary>
    /// Robot position in 3D space
    /// </summary>
    public class RobotPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Roll { get; set; }
        public double Pitch { get; set; }
        public double Yaw { get; set; }
        public string Zone { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Robot task assignment
    /// </summary>
    public class RobotTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TaskNumber { get; set; }
        public string ProjectId { get; set; }
        public string RobotId { get; set; }
        public string Description { get; set; }
        public RobotTaskStatus Status { get; set; } = RobotTaskStatus.Queued;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public TaskLocation Location { get; set; }
        public TaskParameters Parameters { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public int ActualDurationMinutes { get; set; }
        public double ProgressPercentage { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public List<string> RequiredMaterials { get; set; } = new();
        public TaskQualityMetrics QualityMetrics { get; set; }
        public string AssignedBy { get; set; }
        public string SupervisorId { get; set; }
        public List<TaskEvent> EventLog { get; set; } = new();
    }

    /// <summary>
    /// Task location specification
    /// </summary>
    public class TaskLocation
    {
        public string BuildingId { get; set; }
        public string FloorId { get; set; }
        public string ZoneId { get; set; }
        public RobotPosition StartPosition { get; set; }
        public RobotPosition EndPosition { get; set; }
        public List<RobotPosition> Waypoints { get; set; } = new();
        public Boundary WorkEnvelope { get; set; }
    }

    /// <summary>
    /// 3D boundary definition
    /// </summary>
    public class Boundary
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
    }

    /// <summary>
    /// Task parameters for specific operations
    /// </summary>
    public class TaskParameters
    {
        public string OperationType { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new();
        public double TargetSpeed { get; set; }
        public double TargetForce { get; set; }
        public double Tolerance { get; set; }
        public List<string> QualityRequirements { get; set; } = new();
    }

    /// <summary>
    /// Task quality metrics
    /// </summary>
    public class TaskQualityMetrics
    {
        public double AccuracyScore { get; set; }
        public double ConsistencyScore { get; set; }
        public double SurfaceFinishScore { get; set; }
        public int DefectsFound { get; set; }
        public int DefectsCorrected { get; set; }
        public bool PassedInspection { get; set; }
        public string InspectorId { get; set; }
        public DateTime InspectionDate { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    /// <summary>
    /// Task event log entry
    /// </summary>
    public class TaskEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; }
        public string Description { get; set; }
        public string TriggeredBy { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Welding operation configuration
    /// </summary>
    public class WeldingOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TaskId { get; set; }
        public WeldingType WeldType { get; set; }
        public WeldingParameters Parameters { get; set; }
        public List<WeldJoint> Joints { get; set; } = new();
        public WeldingQualityResult QualityResult { get; set; }
    }

    /// <summary>
    /// Welding process parameters
    /// </summary>
    public class WeldingParameters
    {
        public double Voltage { get; set; }             // Volts
        public double Amperage { get; set; }            // Amps
        public double WireSpeed { get; set; }           // m/min
        public double TravelSpeed { get; set; }         // mm/s
        public double GasFlowRate { get; set; }         // L/min
        public string GasType { get; set; }
        public string FillerMaterial { get; set; }
        public double FillerDiameter { get; set; }      // mm
        public double StickOut { get; set; }            // mm
        public double WorkAngle { get; set; }           // degrees
        public double TravelAngle { get; set; }         // degrees
        public int NumberOfPasses { get; set; }
        public double PreheatTemperature { get; set; }  // Celsius
    }

    /// <summary>
    /// Weld joint definition
    /// </summary>
    public class WeldJoint
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string JointType { get; set; }           // Butt, Lap, Corner, Edge, Tee
        public RobotPosition StartPoint { get; set; }
        public RobotPosition EndPoint { get; set; }
        public double Length { get; set; }
        public double Gap { get; set; }
        public string Material1 { get; set; }
        public string Material2 { get; set; }
        public double Thickness1 { get; set; }
        public double Thickness2 { get; set; }
        public bool Completed { get; set; }
        public WeldJointQuality Quality { get; set; }
    }

    /// <summary>
    /// Weld joint quality assessment
    /// </summary>
    public class WeldJointQuality
    {
        public double PenetrationDepth { get; set; }
        public double BeadWidth { get; set; }
        public double BeadHeight { get; set; }
        public bool Porosity { get; set; }
        public bool Undercut { get; set; }
        public bool Spatter { get; set; }
        public bool CrackDetected { get; set; }
        public double VisualScore { get; set; }
        public bool PassedNDT { get; set; }
        public string NDTMethod { get; set; }
    }

    /// <summary>
    /// Welding quality result
    /// </summary>
    public class WeldingQualityResult
    {
        public int TotalJoints { get; set; }
        public int PassedJoints { get; set; }
        public double PassRate { get; set; }
        public double AverageQualityScore { get; set; }
        public List<string> IssuesFound { get; set; } = new();
        public string CertificationStandard { get; set; }
        public bool MeetsCertification { get; set; }
    }

    /// <summary>
    /// Bricklaying operation configuration
    /// </summary>
    public class BricklayingOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TaskId { get; set; }
        public BrickPattern Pattern { get; set; }
        public BrickSpecification BrickSpec { get; set; }
        public MortarSpecification MortarSpec { get; set; }
        public WallConfiguration WallConfig { get; set; }
        public BricklayingProgress Progress { get; set; } = new();
        public BricklayingQualityResult QualityResult { get; set; }
    }

    /// <summary>
    /// Brick specification
    /// </summary>
    public class BrickSpecification
    {
        public string Type { get; set; }
        public double Length { get; set; }          // mm
        public double Width { get; set; }           // mm
        public double Height { get; set; }          // mm
        public double Weight { get; set; }          // kg
        public string Material { get; set; }
        public string Color { get; set; }
        public double CompressiveStrength { get; set; }  // MPa
    }

    /// <summary>
    /// Mortar specification
    /// </summary>
    public class MortarSpecification
    {
        public string Type { get; set; }            // Type M, S, N, O
        public double MixRatio { get; set; }
        public double JointThickness { get; set; }  // mm
        public double WorkingTime { get; set; }     // minutes
        public double SettingTime { get; set; }     // hours
    }

    /// <summary>
    /// Wall configuration for bricklaying
    /// </summary>
    public class WallConfiguration
    {
        public double Length { get; set; }          // meters
        public double Height { get; set; }          // meters
        public double Thickness { get; set; }       // mm (number of wythes)
        public List<WallOpening> Openings { get; set; } = new();
        public bool RequiresReinforcement { get; set; }
        public List<ReinforcementSpec> Reinforcement { get; set; } = new();
    }

    /// <summary>
    /// Wall opening definition
    /// </summary>
    public class WallOpening
    {
        public string Type { get; set; }            // Window, Door, Service
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string LintelType { get; set; }
    }

    /// <summary>
    /// Reinforcement specification
    /// </summary>
    public class ReinforcementSpec
    {
        public string Type { get; set; }
        public double Spacing { get; set; }
        public string Location { get; set; }
    }

    /// <summary>
    /// Bricklaying progress tracking
    /// </summary>
    public class BricklayingProgress
    {
        public int TotalBricks { get; set; }
        public int BricksLaid { get; set; }
        public int CurrentCourse { get; set; }
        public int TotalCourses { get; set; }
        public double MortarUsed { get; set; }      // Liters
        public double AreaCompleted { get; set; }   // m2
        public double LayingRate { get; set; }      // Bricks per hour
    }

    /// <summary>
    /// Bricklaying quality result
    /// </summary>
    public class BricklayingQualityResult
    {
        public double LevelScore { get; set; }
        public double PlumbScore { get; set; }
        public double JointConsistencyScore { get; set; }
        public double AlignmentScore { get; set; }
        public double OverallScore { get; set; }
        public int BricksRejected { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    /// <summary>
    /// Concrete printing operation
    /// </summary>
    public class ConcretePrintingOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TaskId { get; set; }
        public PrintingMaterial Material { get; set; }
        public ConcreteMixDesign MixDesign { get; set; }
        public PrintingParameters PrintParams { get; set; }
        public PrintGeometry Geometry { get; set; }
        public ConcretePrintingProgress Progress { get; set; } = new();
        public ConcretePrintingQuality QualityResult { get; set; }
    }

    /// <summary>
    /// Concrete mix design for 3D printing
    /// </summary>
    public class ConcreteMixDesign
    {
        public string Name { get; set; }
        public double CementContent { get; set; }       // kg/m3
        public double WaterCementRatio { get; set; }
        public double FineAggregate { get; set; }       // kg/m3
        public double CoarseAggregate { get; set; }     // kg/m3
        public List<Admixture> Admixtures { get; set; } = new();
        public double Slump { get; set; }               // mm
        public double Buildability { get; set; }        // Layers before deformation
        public double OpenTime { get; set; }            // minutes
        public double CompressiveStrength28Day { get; set; }  // MPa
    }

    /// <summary>
    /// Concrete admixture
    /// </summary>
    public class Admixture
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public double Dosage { get; set; }              // % of cement
        public string Purpose { get; set; }
    }

    /// <summary>
    /// 3D printing parameters
    /// </summary>
    public class PrintingParameters
    {
        public double LayerHeight { get; set; }         // mm
        public double LayerWidth { get; set; }          // mm
        public double PrintSpeed { get; set; }          // mm/s
        public double NozzleDiameter { get; set; }      // mm
        public double ExtrusionRate { get; set; }       // L/min
        public double PumpPressure { get; set; }        // bar
        public double LayerInterval { get; set; }       // seconds between layers
        public double AccelerationFactor { get; set; }
    }

    /// <summary>
    /// Print geometry definition
    /// </summary>
    public class PrintGeometry
    {
        public string GeometryType { get; set; }        // Wall, Column, Freeform
        public double TotalHeight { get; set; }         // meters
        public double Perimeter { get; set; }           // meters per layer
        public int TotalLayers { get; set; }
        public double TotalVolume { get; set; }         // m3
        public List<PathSegment> ToolPath { get; set; } = new();
        public bool IncludesReinforcement { get; set; }
    }

    /// <summary>
    /// Tool path segment for printing
    /// </summary>
    public class PathSegment
    {
        public int LayerNumber { get; set; }
        public List<RobotPosition> Points { get; set; } = new();
        public double ExtrusionMultiplier { get; set; } = 1.0;
        public bool IsRetraction { get; set; }
    }

    /// <summary>
    /// Concrete printing progress
    /// </summary>
    public class ConcretePrintingProgress
    {
        public int LayersPrinted { get; set; }
        public double VolumeExtruded { get; set; }      // m3
        public double HeightAchieved { get; set; }      // meters
        public double PrintingTime { get; set; }        // minutes
        public double MaterialConsumption { get; set; } // kg
        public double AveragePrintSpeed { get; set; }   // mm/s
    }

    /// <summary>
    /// Concrete printing quality assessment
    /// </summary>
    public class ConcretePrintingQuality
    {
        public double LayerAdhesionScore { get; set; }
        public double DimensionalAccuracy { get; set; }
        public double SurfaceFinishScore { get; set; }
        public double StructuralIntegrityScore { get; set; }
        public double VoidContent { get; set; }         // Percentage
        public List<LayerDefect> Defects { get; set; } = new();
        public bool PassedInspection { get; set; }
    }

    /// <summary>
    /// Layer defect record
    /// </summary>
    public class LayerDefect
    {
        public int LayerNumber { get; set; }
        public string DefectType { get; set; }
        public string Location { get; set; }
        public string Severity { get; set; }
        public bool Repaired { get; set; }
    }

    /// <summary>
    /// Autonomous equipment registration
    /// </summary>
    public class AutonomousEquipment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EquipmentNumber { get; set; }
        public string Name { get; set; }
        public AutonomousEquipmentType Type { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public AutonomyLevel AutonomyLevel { get; set; }
        public RobotStatus Status { get; set; } = RobotStatus.Offline;
        public EquipmentCapabilities Capabilities { get; set; } = new();
        public GeofenceConfiguration Geofence { get; set; }
        public RobotPosition CurrentPosition { get; set; }
        public double FuelLevel { get; set; } = 100;
        public EquipmentPerformanceMetrics PerformanceMetrics { get; set; } = new();
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Equipment capabilities
    /// </summary>
    public class EquipmentCapabilities
    {
        public double MaxCapacity { get; set; }         // m3 or kg depending on type
        public double MaxSpeed { get; set; }            // km/h
        public double MaxGradeability { get; set; }     // degrees
        public double OperatingRadius { get; set; }     // meters
        public double LiftHeight { get; set; }          // meters
        public double DigDepth { get; set; }            // meters
        public List<string> Attachments { get; set; } = new();
    }

    /// <summary>
    /// Geofence configuration for autonomous equipment
    /// </summary>
    public class GeofenceConfiguration
    {
        public List<GeofenceZone> AllowedZones { get; set; } = new();
        public List<GeofenceZone> RestrictedZones { get; set; } = new();
        public List<GeofenceZone> SlowDownZones { get; set; } = new();
        public double MaxAllowedSpeed { get; set; }
    }

    /// <summary>
    /// Geofence zone definition
    /// </summary>
    public class GeofenceZone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public List<GeoPoint> Boundary { get; set; } = new();
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
    }

    /// <summary>
    /// Geographic point
    /// </summary>
    public class GeoPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    /// <summary>
    /// Equipment performance metrics
    /// </summary>
    public class EquipmentPerformanceMetrics
    {
        public double TotalOperatingHours { get; set; }
        public double IdleTime { get; set; }
        public double ProductiveTime { get; set; }
        public double FuelConsumption { get; set; }     // L/hour
        public double MaterialMoved { get; set; }       // m3 or tonnes
        public double DistanceTraveled { get; set; }    // km
        public int TasksCompleted { get; set; }
        public double Efficiency { get; set; }          // Percentage
    }

    /// <summary>
    /// Safety zone configuration
    /// </summary>
    public class SafetyZone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string ProjectId { get; set; }
        public SafetyZoneType Type { get; set; }
        public Boundary Boundary { get; set; }
        public List<string> AssociatedRobots { get; set; } = new();
        public List<SafetyRule> Rules { get; set; } = new();
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Safety rule definition
    /// </summary>
    public class SafetyRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string TriggerCondition { get; set; }
        public string Action { get; set; }
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Human-robot collaboration configuration
    /// </summary>
    public class CollaborationConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RobotId { get; set; }
        public CollaborationMode Mode { get; set; }
        public CollaborationSafetySettings Safety { get; set; } = new();
        public List<string> AuthorizedWorkers { get; set; } = new();
        public HandoverProtocol HandoverProtocol { get; set; }
        public bool RequiresAcknowledgement { get; set; } = true;
    }

    /// <summary>
    /// Collaboration safety settings
    /// </summary>
    public class CollaborationSafetySettings
    {
        public double SafetyDistance { get; set; } = 2.0;           // meters
        public double WarningDistance { get; set; } = 3.0;          // meters
        public double SlowDownDistance { get; set; } = 4.0;         // meters
        public double ReducedSpeed { get; set; } = 0.25;            // m/s when human nearby
        public double MaxForce { get; set; } = 150;                 // N (collaborative limit)
        public double MaxPressure { get; set; } = 280;              // N/cm2
        public bool RequiresProximityDetection { get; set; } = true;
        public bool RequiresSafetyLaserScanner { get; set; } = true;
        public List<string> RequiredPPE { get; set; } = new();
    }

    /// <summary>
    /// Handover protocol for human-robot task transfer
    /// </summary>
    public class HandoverProtocol
    {
        public string Type { get; set; }                    // Object, Control, Task
        public bool RequiresConfirmation { get; set; } = true;
        public double HandoverPosition { get; set; }        // Height in meters
        public double MaxWaitTime { get; set; }             // Seconds
        public List<string> SafetyChecks { get; set; } = new();
    }

    /// <summary>
    /// Human worker proximity detection
    /// </summary>
    public class ProximityDetection
    {
        public string RobotId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<DetectedHuman> DetectedHumans { get; set; } = new();
        public bool SafetyActionTriggered { get; set; }
        public string ActionTaken { get; set; }
    }

    /// <summary>
    /// Detected human worker
    /// </summary>
    public class DetectedHuman
    {
        public string WorkerId { get; set; }
        public double Distance { get; set; }
        public double ApproachSpeed { get; set; }
        public string Direction { get; set; }
        public bool WearingRequiredPPE { get; set; }
        public bool Authorized { get; set; }
    }

    /// <summary>
    /// Robot performance metrics
    /// </summary>
    public class RobotPerformanceMetrics
    {
        public double TotalOperatingHours { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksFailed { get; set; }
        public double AverageTaskDuration { get; set; }     // minutes
        public double Uptime { get; set; }                  // Percentage
        public double AverageAccuracy { get; set; }         // Percentage
        public double AverageQualityScore { get; set; }
        public double EnergyConsumption { get; set; }       // kWh
        public double MaterialWaste { get; set; }           // Percentage
        public int SafetyIncidents { get; set; }
        public int EmergencyStops { get; set; }
        public double MTBF { get; set; }                    // Mean Time Between Failures (hours)
        public double MTTR { get; set; }                    // Mean Time To Repair (hours)
    }

    /// <summary>
    /// Maintenance record
    /// </summary>
    public class MaintenanceRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RobotId { get; set; }
        public MaintenanceType Type { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? PerformedDate { get; set; }
        public string TechnicianId { get; set; }
        public List<string> TasksPerformed { get; set; } = new();
        public List<string> PartsReplaced { get; set; } = new();
        public double LaborHours { get; set; }
        public decimal PartsCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Notes { get; set; }
        public DateTime NextMaintenanceDate { get; set; }
    }

    /// <summary>
    /// Safety alert
    /// </summary>
    public class SafetyAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string RobotId { get; set; }
        public AlertSeverity Severity { get; set; }
        public string AlertType { get; set; }
        public string Description { get; set; }
        public RobotPosition Location { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Acknowledged { get; set; }
        public string AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string Resolution { get; set; }
    }

    /// <summary>
    /// Daily production report
    /// </summary>
    public class DailyProductionReport
    {
        public string ProjectId { get; set; }
        public DateTime Date { get; set; }
        public List<RobotDailyMetrics> RobotMetrics { get; set; } = new();
        public double TotalProductionValue { get; set; }
        public int TotalTasksCompleted { get; set; }
        public double TotalOperatingHours { get; set; }
        public int SafetyIncidents { get; set; }
        public double EfficiencyRating { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Achievements { get; set; } = new();
    }

    /// <summary>
    /// Individual robot daily metrics
    /// </summary>
    public class RobotDailyMetrics
    {
        public string RobotId { get; set; }
        public string RobotName { get; set; }
        public double OperatingHours { get; set; }
        public double IdleHours { get; set; }
        public int TasksCompleted { get; set; }
        public double ProductionOutput { get; set; }
        public double QualityScore { get; set; }
        public int Interruptions { get; set; }
        public double Efficiency { get; set; }
    }

    /// <summary>
    /// Project robotics configuration
    /// </summary>
    public class RoboticsProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<string> RobotIds { get; set; } = new();
        public List<string> EquipmentIds { get; set; } = new();
        public List<SafetyZone> SafetyZones { get; set; } = new();
        public List<CollaborationConfiguration> CollaborationConfigs { get; set; } = new();
        public RoboticsProjectMetrics Metrics { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Project-level robotics metrics
    /// </summary>
    public class RoboticsProjectMetrics
    {
        public int ActiveRobots { get; set; }
        public int ActiveEquipment { get; set; }
        public double TotalRobotHours { get; set; }
        public int TotalTasksCompleted { get; set; }
        public double AverageEfficiency { get; set; }
        public double AverageQuality { get; set; }
        public int SafetyIncidents { get; set; }
        public decimal LaborCostSavings { get; set; }
        public int ScheduleDaysReduced { get; set; }
    }

    #endregion

    /// <summary>
    /// Comprehensive robotics intelligence engine for construction automation
    /// Handles robot management, task scheduling, safety, and performance monitoring
    /// </summary>
    public sealed class RoboticsIntelligenceEngine
    {
        private static readonly Lazy<RoboticsIntelligenceEngine> _instance =
            new Lazy<RoboticsIntelligenceEngine>(() => new RoboticsIntelligenceEngine());
        public static RoboticsIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, RoboticsProject> _projects = new();
        private readonly Dictionary<string, ConstructionRobot> _robots = new();
        private readonly Dictionary<string, AutonomousEquipment> _equipment = new();
        private readonly Dictionary<string, RobotTask> _tasks = new();
        private readonly Dictionary<string, SafetyZone> _safetyZones = new();
        private readonly Dictionary<string, WeldingOperation> _weldingOperations = new();
        private readonly Dictionary<string, BricklayingOperation> _bricklayingOperations = new();
        private readonly Dictionary<string, ConcretePrintingOperation> _printingOperations = new();
        private readonly Dictionary<string, CollaborationConfiguration> _collaborationConfigs = new();
        private readonly List<SafetyAlert> _safetyAlerts = new();
        private readonly object _lock = new object();

        public event EventHandler<SafetyAlert> SafetyAlertRaised;
        public event EventHandler<RobotTask> TaskStatusChanged;
        public event EventHandler<ProximityDetection> HumanProximityDetected;

        private RoboticsIntelligenceEngine()
        {
            InitializeDefaultSafetyRules();
        }

        #region Project Management

        /// <summary>
        /// Creates a new robotics-enabled project
        /// </summary>
        public RoboticsProject CreateProject(string projectId, string projectName)
        {
            var project = new RoboticsProject
            {
                ProjectId = projectId,
                ProjectName = projectName
            };

            lock (_lock)
            {
                _projects[project.Id] = project;
            }

            return project;
        }

        /// <summary>
        /// Gets a project by ID
        /// </summary>
        public RoboticsProject GetProject(string projectId)
        {
            lock (_lock)
            {
                return _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
            }
        }

        #endregion

        #region Robot Management

        /// <summary>
        /// Registers a new construction robot
        /// </summary>
        public ConstructionRobot RegisterRobot(string projectId, string serialNumber, string name,
            RobotType type, string manufacturer, string model)
        {
            var robot = new ConstructionRobot
            {
                SerialNumber = serialNumber,
                Name = name,
                Type = type,
                Manufacturer = manufacturer,
                Model = model,
                Capabilities = GetDefaultCapabilities(type),
                NextMaintenanceDate = DateTime.UtcNow.AddDays(30)
            };

            lock (_lock)
            {
                _robots[robot.Id] = robot;

                var project = _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
                project?.RobotIds.Add(robot.Id);
            }

            return robot;
        }

        /// <summary>
        /// Gets default capabilities based on robot type
        /// </summary>
        private RobotCapabilities GetDefaultCapabilities(RobotType type)
        {
            return type switch
            {
                RobotType.Welding => new RobotCapabilities
                {
                    MaxPayload = 20,
                    MaxReach = 2.5,
                    MaxSpeed = 2.0,
                    PositionAccuracy = 0.05,
                    RepeatAccuracy = 0.02,
                    DegreesOfFreedom = 6,
                    ToolCompatibility = new List<string> { "MIG Torch", "TIG Torch", "Spot Welder" },
                    Sensors = new List<string> { "Arc Sensor", "Touch Sensing", "Seam Tracking", "Vision System" }
                },
                RobotType.Bricklaying => new RobotCapabilities
                {
                    MaxPayload = 30,
                    MaxReach = 3.0,
                    MaxSpeed = 1.5,
                    PositionAccuracy = 0.5,
                    RepeatAccuracy = 0.3,
                    DegreesOfFreedom = 6,
                    ToolCompatibility = new List<string> { "Brick Gripper", "Mortar Applicator" },
                    Sensors = new List<string> { "3D Vision", "Force Sensor", "Level Sensor" }
                },
                RobotType.ConcretePrinting => new RobotCapabilities
                {
                    MaxPayload = 50,
                    MaxReach = 15.0,
                    MaxSpeed = 0.5,
                    PositionAccuracy = 1.0,
                    RepeatAccuracy = 0.5,
                    DegreesOfFreedom = 6,
                    ToolCompatibility = new List<string> { "Print Nozzle", "Reinforcement Inserter" },
                    Sensors = new List<string> { "Material Flow Sensor", "Layer Height Sensor", "GPS RTK" }
                },
                RobotType.MaterialHandling => new RobotCapabilities
                {
                    MaxPayload = 500,
                    MaxReach = 5.0,
                    MaxSpeed = 3.0,
                    PositionAccuracy = 5.0,
                    RepeatAccuracy = 2.0,
                    DegreesOfFreedom = 4,
                    ToolCompatibility = new List<string> { "Pallet Fork", "Vacuum Gripper", "Magnetic Gripper" },
                    Sensors = new List<string> { "LIDAR", "Camera Array", "Proximity Sensors" }
                },
                _ => new RobotCapabilities
                {
                    MaxPayload = 25,
                    MaxReach = 2.0,
                    MaxSpeed = 1.0,
                    PositionAccuracy = 1.0,
                    RepeatAccuracy = 0.5,
                    DegreesOfFreedom = 6
                }
            };
        }

        /// <summary>
        /// Updates robot status
        /// </summary>
        public ConstructionRobot UpdateRobotStatus(string robotId, RobotStatus status)
        {
            lock (_lock)
            {
                if (!_robots.TryGetValue(robotId, out var robot))
                    return null;

                var previousStatus = robot.Status;
                robot.Status = status;

                // Log status change
                if (status == RobotStatus.EmergencyStop)
                {
                    RaiseSafetyAlert(null, robotId, AlertSeverity.Critical,
                        "EmergencyStop", "Robot emergency stop activated", robot.CurrentPosition);
                }

                return robot;
            }
        }

        /// <summary>
        /// Updates robot position
        /// </summary>
        public void UpdateRobotPosition(string robotId, double x, double y, double z,
            double roll = 0, double pitch = 0, double yaw = 0, string zone = null)
        {
            lock (_lock)
            {
                if (_robots.TryGetValue(robotId, out var robot))
                {
                    robot.CurrentPosition = new RobotPosition
                    {
                        X = x, Y = y, Z = z,
                        Roll = roll, Pitch = pitch, Yaw = yaw,
                        Zone = zone,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
        }

        /// <summary>
        /// Gets all robots for a project
        /// </summary>
        public List<ConstructionRobot> GetProjectRobots(string projectId)
        {
            lock (_lock)
            {
                var project = _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
                if (project == null) return new List<ConstructionRobot>();

                return project.RobotIds
                    .Where(id => _robots.ContainsKey(id))
                    .Select(id => _robots[id])
                    .ToList();
            }
        }

        #endregion

        #region Autonomous Equipment Management

        /// <summary>
        /// Registers autonomous equipment
        /// </summary>
        public AutonomousEquipment RegisterEquipment(string projectId, string equipmentNumber,
            string name, AutonomousEquipmentType type, string manufacturer, string model)
        {
            var equipment = new AutonomousEquipment
            {
                EquipmentNumber = equipmentNumber,
                Name = name,
                Type = type,
                Manufacturer = manufacturer,
                Model = model,
                Capabilities = GetDefaultEquipmentCapabilities(type)
            };

            lock (_lock)
            {
                _equipment[equipment.Id] = equipment;

                var project = _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
                project?.EquipmentIds.Add(equipment.Id);
            }

            return equipment;
        }

        /// <summary>
        /// Gets default equipment capabilities
        /// </summary>
        private EquipmentCapabilities GetDefaultEquipmentCapabilities(AutonomousEquipmentType type)
        {
            return type switch
            {
                AutonomousEquipmentType.Excavator => new EquipmentCapabilities
                {
                    MaxCapacity = 1.5, // m3 bucket
                    MaxSpeed = 6,
                    MaxGradeability = 35,
                    OperatingRadius = 10,
                    DigDepth = 6.5,
                    LiftHeight = 9.5,
                    Attachments = new List<string> { "Bucket", "Breaker", "Grapple" }
                },
                AutonomousEquipmentType.DumpTruck => new EquipmentCapabilities
                {
                    MaxCapacity = 30, // tonnes
                    MaxSpeed = 45,
                    MaxGradeability = 25,
                    OperatingRadius = 500
                },
                AutonomousEquipmentType.Crane => new EquipmentCapabilities
                {
                    MaxCapacity = 50, // tonnes
                    MaxSpeed = 5,
                    OperatingRadius = 60,
                    LiftHeight = 80
                },
                _ => new EquipmentCapabilities
                {
                    MaxCapacity = 10,
                    MaxSpeed = 20,
                    MaxGradeability = 30
                }
            };
        }

        /// <summary>
        /// Configures geofence for equipment
        /// </summary>
        public void ConfigureGeofence(string equipmentId, GeofenceConfiguration geofence)
        {
            lock (_lock)
            {
                if (_equipment.TryGetValue(equipmentId, out var equipment))
                {
                    equipment.Geofence = geofence;
                }
            }
        }

        #endregion

        #region Task Management

        /// <summary>
        /// Creates and queues a new robot task
        /// </summary>
        public RobotTask CreateTask(string projectId, string robotId, string description,
            TaskPriority priority, TaskLocation location, TaskParameters parameters,
            DateTime scheduledStart, int estimatedMinutes)
        {
            var task = new RobotTask
            {
                TaskNumber = GenerateTaskNumber(projectId),
                ProjectId = projectId,
                RobotId = robotId,
                Description = description,
                Priority = priority,
                Location = location,
                Parameters = parameters,
                ScheduledStart = scheduledStart,
                EstimatedDurationMinutes = estimatedMinutes
            };

            lock (_lock)
            {
                _tasks[task.Id] = task;
            }

            return task;
        }

        /// <summary>
        /// Generates task number
        /// </summary>
        private string GenerateTaskNumber(string projectId)
        {
            lock (_lock)
            {
                var count = _tasks.Values.Count(t => t.ProjectId == projectId) + 1;
                return $"RT-{count:D4}";
            }
        }

        /// <summary>
        /// Assigns a task to a robot
        /// </summary>
        public async Task<RobotTask> AssignTaskAsync(string taskId, string robotId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_tasks.TryGetValue(taskId, out var task))
                        return null;

                    if (!_robots.TryGetValue(robotId, out var robot))
                        return null;

                    // Verify robot is available
                    if (robot.Status != RobotStatus.Idle && robot.Status != RobotStatus.Operating)
                    {
                        task.EventLog.Add(new TaskEvent
                        {
                            EventType = "AssignmentFailed",
                            Description = $"Robot {robot.Name} is not available (Status: {robot.Status})"
                        });
                        return task;
                    }

                    task.RobotId = robotId;
                    task.Status = RobotTaskStatus.Assigned;
                    task.EventLog.Add(new TaskEvent
                    {
                        EventType = "Assigned",
                        Description = $"Task assigned to robot {robot.Name}"
                    });

                    OnTaskStatusChanged(task);
                    return task;
                }
            });
        }

        /// <summary>
        /// Starts a task
        /// </summary>
        public RobotTask StartTask(string taskId)
        {
            lock (_lock)
            {
                if (!_tasks.TryGetValue(taskId, out var task))
                    return null;

                if (task.Status != RobotTaskStatus.Assigned)
                    return task;

                task.Status = RobotTaskStatus.InProgress;
                task.ActualStart = DateTime.UtcNow;
                task.EventLog.Add(new TaskEvent
                {
                    EventType = "Started",
                    Description = "Task execution started"
                });

                // Update robot status
                if (_robots.TryGetValue(task.RobotId, out var robot))
                {
                    robot.Status = RobotStatus.Operating;
                }

                OnTaskStatusChanged(task);
                return task;
            }
        }

        /// <summary>
        /// Updates task progress
        /// </summary>
        public RobotTask UpdateTaskProgress(string taskId, double progressPercentage)
        {
            lock (_lock)
            {
                if (!_tasks.TryGetValue(taskId, out var task))
                    return null;

                task.ProgressPercentage = Math.Min(progressPercentage, 100);
                return task;
            }
        }

        /// <summary>
        /// Completes a task
        /// </summary>
        public RobotTask CompleteTask(string taskId, TaskQualityMetrics qualityMetrics = null)
        {
            lock (_lock)
            {
                if (!_tasks.TryGetValue(taskId, out var task))
                    return null;

                task.Status = RobotTaskStatus.Completed;
                task.CompletedAt = DateTime.UtcNow;
                task.ProgressPercentage = 100;
                task.QualityMetrics = qualityMetrics;

                if (task.ActualStart.HasValue)
                {
                    task.ActualDurationMinutes = (int)(task.CompletedAt.Value - task.ActualStart.Value).TotalMinutes;
                }

                task.EventLog.Add(new TaskEvent
                {
                    EventType = "Completed",
                    Description = "Task completed successfully"
                });

                // Update robot status
                if (_robots.TryGetValue(task.RobotId, out var robot))
                {
                    robot.Status = RobotStatus.Idle;
                    robot.PerformanceMetrics.TasksCompleted++;
                }

                OnTaskStatusChanged(task);
                return task;
            }
        }

        /// <summary>
        /// Gets optimal task schedule for a robot
        /// </summary>
        public async Task<List<RobotTask>> OptimizeTaskScheduleAsync(string robotId, DateTime startDate, DateTime endDate)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    var tasks = _tasks.Values
                        .Where(t => t.RobotId == robotId &&
                                    t.ScheduledStart >= startDate &&
                                    t.ScheduledStart <= endDate &&
                                    t.Status == RobotTaskStatus.Queued)
                        .ToList();

                    // Sort by priority, then by scheduled start
                    var optimized = tasks
                        .OrderByDescending(t => t.Priority)
                        .ThenBy(t => t.ScheduledStart)
                        .ToList();

                    // Resolve scheduling conflicts
                    DateTime currentEnd = startDate;
                    foreach (var task in optimized)
                    {
                        if (task.ScheduledStart < currentEnd)
                        {
                            task.ScheduledStart = currentEnd;
                        }
                        currentEnd = task.ScheduledStart.AddMinutes(task.EstimatedDurationMinutes);
                    }

                    return optimized;
                }
            });
        }

        #endregion

        #region Welding Operations

        /// <summary>
        /// Creates a welding operation
        /// </summary>
        public WeldingOperation CreateWeldingOperation(string taskId, WeldingType weldType,
            WeldingParameters parameters)
        {
            var operation = new WeldingOperation
            {
                TaskId = taskId,
                WeldType = weldType,
                Parameters = parameters
            };

            lock (_lock)
            {
                _weldingOperations[operation.Id] = operation;
            }

            return operation;
        }

        /// <summary>
        /// Adds a weld joint to an operation
        /// </summary>
        public WeldJoint AddWeldJoint(string operationId, string jointType,
            RobotPosition start, RobotPosition end, double length, double gap,
            string material1, string material2, double thickness1, double thickness2)
        {
            lock (_lock)
            {
                if (!_weldingOperations.TryGetValue(operationId, out var operation))
                    return null;

                var joint = new WeldJoint
                {
                    JointType = jointType,
                    StartPoint = start,
                    EndPoint = end,
                    Length = length,
                    Gap = gap,
                    Material1 = material1,
                    Material2 = material2,
                    Thickness1 = thickness1,
                    Thickness2 = thickness2
                };

                operation.Joints.Add(joint);
                return joint;
            }
        }

        /// <summary>
        /// Completes welding and generates quality report
        /// </summary>
        public async Task<WeldingQualityResult> CompleteWeldingAsync(string operationId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_weldingOperations.TryGetValue(operationId, out var operation))
                        return null;

                    var passedJoints = operation.Joints.Count(j => j.Quality?.PassedNDT == true);
                    var result = new WeldingQualityResult
                    {
                        TotalJoints = operation.Joints.Count,
                        PassedJoints = passedJoints,
                        PassRate = operation.Joints.Count > 0
                            ? (passedJoints * 100.0 / operation.Joints.Count)
                            : 100,
                        AverageQualityScore = operation.Joints.Any()
                            ? operation.Joints.Average(j => j.Quality?.VisualScore ?? 0)
                            : 0,
                        CertificationStandard = "AWS D1.1",
                        MeetsCertification = passedJoints == operation.Joints.Count
                    };

                    // Add issues found
                    foreach (var joint in operation.Joints.Where(j => j.Quality != null))
                    {
                        if (joint.Quality.Porosity) result.IssuesFound.Add($"Joint {joint.Id}: Porosity detected");
                        if (joint.Quality.Undercut) result.IssuesFound.Add($"Joint {joint.Id}: Undercut detected");
                        if (joint.Quality.CrackDetected) result.IssuesFound.Add($"Joint {joint.Id}: Crack detected");
                    }

                    operation.QualityResult = result;
                    return result;
                }
            });
        }

        #endregion

        #region Bricklaying Operations

        /// <summary>
        /// Creates a bricklaying operation
        /// </summary>
        public BricklayingOperation CreateBricklayingOperation(string taskId, BrickPattern pattern,
            BrickSpecification brickSpec, MortarSpecification mortarSpec, WallConfiguration wallConfig)
        {
            var operation = new BricklayingOperation
            {
                TaskId = taskId,
                Pattern = pattern,
                BrickSpec = brickSpec,
                MortarSpec = mortarSpec,
                WallConfig = wallConfig
            };

            // Calculate total bricks needed
            var bricksPerRow = (int)(wallConfig.Length * 1000 / (brickSpec.Length + mortarSpec.JointThickness));
            var rows = (int)(wallConfig.Height * 1000 / (brickSpec.Height + mortarSpec.JointThickness));
            operation.Progress.TotalBricks = bricksPerRow * rows;
            operation.Progress.TotalCourses = rows;

            // Adjust for openings
            foreach (var opening in wallConfig.Openings)
            {
                var openingBricks = (int)((opening.Width / brickSpec.Length) * (opening.Height / brickSpec.Height));
                operation.Progress.TotalBricks -= openingBricks;
            }

            lock (_lock)
            {
                _bricklayingOperations[operation.Id] = operation;
            }

            return operation;
        }

        /// <summary>
        /// Updates bricklaying progress
        /// </summary>
        public BricklayingProgress UpdateBricklayingProgress(string operationId, int bricksLaid,
            int currentCourse, double mortarUsed)
        {
            lock (_lock)
            {
                if (!_bricklayingOperations.TryGetValue(operationId, out var operation))
                    return null;

                operation.Progress.BricksLaid = bricksLaid;
                operation.Progress.CurrentCourse = currentCourse;
                operation.Progress.MortarUsed = mortarUsed;
                operation.Progress.AreaCompleted =
                    (bricksLaid * operation.BrickSpec.Length * operation.BrickSpec.Height) / 1000000; // m2

                return operation.Progress;
            }
        }

        /// <summary>
        /// Completes bricklaying and generates quality report
        /// </summary>
        public BricklayingQualityResult CompleteBricklaying(string operationId,
            double levelScore, double plumbScore, double jointScore, double alignmentScore, int rejected)
        {
            lock (_lock)
            {
                if (!_bricklayingOperations.TryGetValue(operationId, out var operation))
                    return null;

                var result = new BricklayingQualityResult
                {
                    LevelScore = levelScore,
                    PlumbScore = plumbScore,
                    JointConsistencyScore = jointScore,
                    AlignmentScore = alignmentScore,
                    OverallScore = (levelScore + plumbScore + jointScore + alignmentScore) / 4,
                    BricksRejected = rejected
                };

                if (result.LevelScore < 80) result.Issues.Add("Level deviation exceeds tolerance");
                if (result.PlumbScore < 80) result.Issues.Add("Plumb deviation exceeds tolerance");
                if (result.JointConsistencyScore < 80) result.Issues.Add("Inconsistent joint thickness");

                operation.QualityResult = result;
                return result;
            }
        }

        #endregion

        #region Concrete Printing Operations

        /// <summary>
        /// Creates a concrete printing operation
        /// </summary>
        public ConcretePrintingOperation CreatePrintingOperation(string taskId, PrintingMaterial material,
            ConcreteMixDesign mixDesign, PrintingParameters printParams, PrintGeometry geometry)
        {
            var operation = new ConcretePrintingOperation
            {
                TaskId = taskId,
                Material = material,
                MixDesign = mixDesign,
                PrintParams = printParams,
                Geometry = geometry
            };

            lock (_lock)
            {
                _printingOperations[operation.Id] = operation;
            }

            return operation;
        }

        /// <summary>
        /// Updates concrete printing progress
        /// </summary>
        public ConcretePrintingProgress UpdatePrintingProgress(string operationId, int layersPrinted,
            double volumeExtruded, double heightAchieved)
        {
            lock (_lock)
            {
                if (!_printingOperations.TryGetValue(operationId, out var operation))
                    return null;

                operation.Progress.LayersPrinted = layersPrinted;
                operation.Progress.VolumeExtruded = volumeExtruded;
                operation.Progress.HeightAchieved = heightAchieved;
                operation.Progress.MaterialConsumption = volumeExtruded * 2400; // Approximate kg/m3

                return operation.Progress;
            }
        }

        /// <summary>
        /// Adds a layer defect to the operation
        /// </summary>
        public void AddLayerDefect(string operationId, int layerNumber, string defectType,
            string location, string severity)
        {
            lock (_lock)
            {
                if (!_printingOperations.TryGetValue(operationId, out var operation))
                    return;

                if (operation.QualityResult == null)
                    operation.QualityResult = new ConcretePrintingQuality();

                operation.QualityResult.Defects.Add(new LayerDefect
                {
                    LayerNumber = layerNumber,
                    DefectType = defectType,
                    Location = location,
                    Severity = severity
                });
            }
        }

        #endregion

        #region Safety Management

        /// <summary>
        /// Creates a safety zone
        /// </summary>
        public SafetyZone CreateSafetyZone(string projectId, string name, SafetyZoneType type, Boundary boundary)
        {
            var zone = new SafetyZone
            {
                Name = name,
                ProjectId = projectId,
                Type = type,
                Boundary = boundary,
                Rules = GetDefaultSafetyRules(type)
            };

            lock (_lock)
            {
                _safetyZones[zone.Id] = zone;

                var project = _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
                project?.SafetyZones.Add(zone);
            }

            return zone;
        }

        /// <summary>
        /// Gets default safety rules for zone type
        /// </summary>
        private List<SafetyRule> GetDefaultSafetyRules(SafetyZoneType type)
        {
            return type switch
            {
                SafetyZoneType.NoEntry => new List<SafetyRule>
                {
                    new SafetyRule { Name = "NoHumanEntry", Description = "Humans not permitted in zone during operation", TriggerCondition = "HumanDetected", Action = "EmergencyStop" },
                    new SafetyRule { Name = "ZoneBreach", Description = "Alert on zone boundary breach", TriggerCondition = "BoundaryBreach", Action = "Alert" }
                },
                SafetyZoneType.CollaborativeZone => new List<SafetyRule>
                {
                    new SafetyRule { Name = "ProximitySlowdown", Description = "Reduce speed when human nearby", TriggerCondition = "HumanWithin3m", Action = "ReduceSpeed" },
                    new SafetyRule { Name = "ContactStop", Description = "Stop on contact detection", TriggerCondition = "ContactDetected", Action = "SafeStop" }
                },
                _ => new List<SafetyRule>()
            };
        }

        /// <summary>
        /// Configures human-robot collaboration
        /// </summary>
        public CollaborationConfiguration ConfigureCollaboration(string robotId, CollaborationMode mode,
            CollaborationSafetySettings safetySettings)
        {
            var config = new CollaborationConfiguration
            {
                RobotId = robotId,
                Mode = mode,
                Safety = safetySettings ?? new CollaborationSafetySettings()
            };

            lock (_lock)
            {
                _collaborationConfigs[config.Id] = config;
            }

            return config;
        }

        /// <summary>
        /// Processes human proximity detection
        /// </summary>
        public async Task<ProximityDetection> ProcessProximityDetectionAsync(string robotId, List<DetectedHuman> detectedHumans)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_robots.TryGetValue(robotId, out var robot))
                        return null;

                    var detection = new ProximityDetection
                    {
                        RobotId = robotId,
                        DetectedHumans = detectedHumans
                    };

                    // Find collaboration config for this robot
                    var config = _collaborationConfigs.Values.FirstOrDefault(c => c.RobotId == robotId);
                    var safetySettings = config?.Safety ?? new CollaborationSafetySettings();

                    // Check for safety violations
                    foreach (var human in detectedHumans)
                    {
                        if (human.Distance < safetySettings.SafetyDistance)
                        {
                            detection.SafetyActionTriggered = true;
                            detection.ActionTaken = "EmergencyStop";
                            robot.Status = RobotStatus.EmergencyStop;

                            RaiseSafetyAlert(null, robotId, AlertSeverity.Critical,
                                "HumanInSafetyZone",
                                $"Human detected within safety distance ({human.Distance:F1}m)",
                                robot.CurrentPosition);
                        }
                        else if (human.Distance < safetySettings.WarningDistance)
                        {
                            detection.ActionTaken = "ReducedSpeed";
                            // Robot would reduce speed in actual implementation
                        }

                        // Check for unauthorized access
                        if (!human.Authorized)
                        {
                            RaiseSafetyAlert(null, robotId, AlertSeverity.Warning,
                                "UnauthorizedAccess",
                                "Unauthorized person detected in work zone",
                                robot.CurrentPosition);
                        }

                        // Check PPE compliance
                        if (!human.WearingRequiredPPE)
                        {
                            RaiseSafetyAlert(null, robotId, AlertSeverity.Warning,
                                "PPEViolation",
                                "Worker detected without required PPE",
                                robot.CurrentPosition);
                        }
                    }

                    OnHumanProximityDetected(detection);
                    return detection;
                }
            });
        }

        /// <summary>
        /// Raises a safety alert
        /// </summary>
        private void RaiseSafetyAlert(string projectId, string robotId, AlertSeverity severity,
            string alertType, string description, RobotPosition location)
        {
            var alert = new SafetyAlert
            {
                ProjectId = projectId,
                RobotId = robotId,
                Severity = severity,
                AlertType = alertType,
                Description = description,
                Location = location
            };

            lock (_lock)
            {
                _safetyAlerts.Add(alert);
            }

            OnSafetyAlertRaised(alert);
        }

        /// <summary>
        /// Gets active safety alerts
        /// </summary>
        public List<SafetyAlert> GetActiveAlerts(string projectId = null)
        {
            lock (_lock)
            {
                var query = _safetyAlerts.Where(a => !a.Acknowledged);
                if (!string.IsNullOrEmpty(projectId))
                    query = query.Where(a => a.ProjectId == projectId);

                return query.OrderByDescending(a => a.Severity).ThenByDescending(a => a.Timestamp).ToList();
            }
        }

        /// <summary>
        /// Acknowledges a safety alert
        /// </summary>
        public void AcknowledgeAlert(string alertId, string acknowledgedBy, string resolution = null)
        {
            lock (_lock)
            {
                var alert = _safetyAlerts.FirstOrDefault(a => a.Id == alertId);
                if (alert != null)
                {
                    alert.Acknowledged = true;
                    alert.AcknowledgedBy = acknowledgedBy;
                    alert.AcknowledgedAt = DateTime.UtcNow;
                    alert.Resolution = resolution;
                }
            }
        }

        /// <summary>
        /// Initializes default safety rules
        /// </summary>
        private void InitializeDefaultSafetyRules()
        {
            // Default rules are applied per zone type
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Gets robot performance metrics
        /// </summary>
        public RobotPerformanceMetrics GetRobotPerformance(string robotId)
        {
            lock (_lock)
            {
                if (!_robots.TryGetValue(robotId, out var robot))
                    return null;

                // Calculate metrics from completed tasks
                var robotTasks = _tasks.Values.Where(t => t.RobotId == robotId).ToList();
                var completedTasks = robotTasks.Where(t => t.Status == RobotTaskStatus.Completed).ToList();
                var failedTasks = robotTasks.Where(t => t.Status == RobotTaskStatus.Failed).ToList();

                robot.PerformanceMetrics.TasksCompleted = completedTasks.Count;
                robot.PerformanceMetrics.TasksFailed = failedTasks.Count;

                if (completedTasks.Any())
                {
                    robot.PerformanceMetrics.AverageTaskDuration = completedTasks.Average(t => t.ActualDurationMinutes);
                    robot.PerformanceMetrics.AverageAccuracy = completedTasks
                        .Where(t => t.QualityMetrics != null)
                        .Average(t => t.QualityMetrics.AccuracyScore);
                    robot.PerformanceMetrics.AverageQualityScore = completedTasks
                        .Where(t => t.QualityMetrics != null)
                        .Average(t => (t.QualityMetrics.AccuracyScore + t.QualityMetrics.ConsistencyScore) / 2);
                }

                // Calculate uptime
                if (robot.PerformanceMetrics.TotalOperatingHours > 0)
                {
                    var totalTime = robot.PerformanceMetrics.TotalOperatingHours;
                    var downtime = robot.MaintenanceHistory.Sum(m => m.LaborHours);
                    robot.PerformanceMetrics.Uptime = ((totalTime - downtime) / totalTime) * 100;
                }

                return robot.PerformanceMetrics;
            }
        }

        /// <summary>
        /// Generates daily production report
        /// </summary>
        public async Task<DailyProductionReport> GenerateDailyReportAsync(string projectId, DateTime date)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    var project = _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
                    if (project == null) return null;

                    var report = new DailyProductionReport
                    {
                        ProjectId = projectId,
                        Date = date
                    };

                    foreach (var robotId in project.RobotIds)
                    {
                        if (!_robots.TryGetValue(robotId, out var robot))
                            continue;

                        var dayTasks = _tasks.Values
                            .Where(t => t.RobotId == robotId &&
                                        t.CompletedAt.HasValue &&
                                        t.CompletedAt.Value.Date == date.Date)
                            .ToList();

                        var metrics = new RobotDailyMetrics
                        {
                            RobotId = robotId,
                            RobotName = robot.Name,
                            TasksCompleted = dayTasks.Count,
                            OperatingHours = dayTasks.Sum(t => t.ActualDurationMinutes) / 60.0,
                            QualityScore = dayTasks.Any(t => t.QualityMetrics != null)
                                ? dayTasks.Where(t => t.QualityMetrics != null)
                                    .Average(t => t.QualityMetrics.AccuracyScore)
                                : 0
                        };

                        metrics.Efficiency = metrics.OperatingHours > 0
                            ? (metrics.TasksCompleted * 100.0 / (metrics.OperatingHours * 2)) // Assuming 2 tasks/hour target
                            : 0;

                        report.RobotMetrics.Add(metrics);
                    }

                    report.TotalTasksCompleted = report.RobotMetrics.Sum(m => m.TasksCompleted);
                    report.TotalOperatingHours = report.RobotMetrics.Sum(m => m.OperatingHours);
                    report.EfficiencyRating = report.RobotMetrics.Any()
                        ? report.RobotMetrics.Average(m => m.Efficiency)
                        : 0;

                    // Count safety incidents for the day
                    report.SafetyIncidents = _safetyAlerts
                        .Count(a => a.ProjectId == projectId && a.Timestamp.Date == date.Date);

                    // Add achievements
                    if (report.SafetyIncidents == 0)
                        report.Achievements.Add("Zero safety incidents");
                    if (report.EfficiencyRating > 90)
                        report.Achievements.Add("Exceeded efficiency target");

                    return report;
                }
            });
        }

        /// <summary>
        /// Updates project metrics
        /// </summary>
        public RoboticsProjectMetrics UpdateProjectMetrics(string projectId)
        {
            lock (_lock)
            {
                var project = _projects.Values.FirstOrDefault(p => p.ProjectId == projectId);
                if (project == null) return null;

                var metrics = new RoboticsProjectMetrics
                {
                    ActiveRobots = project.RobotIds.Count(id =>
                        _robots.TryGetValue(id, out var r) &&
                        r.Status != RobotStatus.Offline && r.Status != RobotStatus.Maintenance),
                    ActiveEquipment = project.EquipmentIds.Count(id =>
                        _equipment.TryGetValue(id, out var e) &&
                        e.Status != RobotStatus.Offline),
                    TotalTasksCompleted = _tasks.Values
                        .Count(t => t.ProjectId == projectId && t.Status == RobotTaskStatus.Completed),
                    SafetyIncidents = _safetyAlerts.Count(a => a.ProjectId == projectId)
                };

                // Calculate robot hours and quality
                var projectRobots = project.RobotIds
                    .Where(id => _robots.ContainsKey(id))
                    .Select(id => _robots[id])
                    .ToList();

                if (projectRobots.Any())
                {
                    metrics.TotalRobotHours = projectRobots.Sum(r => r.PerformanceMetrics.TotalOperatingHours);
                    metrics.AverageEfficiency = projectRobots.Average(r => r.PerformanceMetrics.Uptime);
                    metrics.AverageQuality = projectRobots.Average(r => r.PerformanceMetrics.AverageQualityScore);
                }

                project.Metrics = metrics;
                return metrics;
            }
        }

        #endregion

        #region Maintenance Management

        /// <summary>
        /// Schedules maintenance for a robot
        /// </summary>
        public MaintenanceRecord ScheduleMaintenance(string robotId, MaintenanceType type,
            DateTime scheduledDate, List<string> tasks)
        {
            var record = new MaintenanceRecord
            {
                RobotId = robotId,
                Type = type,
                ScheduledDate = scheduledDate,
                TasksPerformed = tasks
            };

            lock (_lock)
            {
                if (_robots.TryGetValue(robotId, out var robot))
                {
                    robot.MaintenanceHistory.Add(record);
                    robot.NextMaintenanceDate = scheduledDate;
                }
            }

            return record;
        }

        /// <summary>
        /// Completes maintenance
        /// </summary>
        public MaintenanceRecord CompleteMaintenance(string robotId, string recordId,
            string technicianId, List<string> partsReplaced, double laborHours, decimal partsCost)
        {
            lock (_lock)
            {
                if (!_robots.TryGetValue(robotId, out var robot))
                    return null;

                var record = robot.MaintenanceHistory.FirstOrDefault(m => m.Id == recordId);
                if (record == null) return null;

                record.PerformedDate = DateTime.UtcNow;
                record.TechnicianId = technicianId;
                record.PartsReplaced = partsReplaced;
                record.LaborHours = laborHours;
                record.PartsCost = partsCost;
                record.TotalCost = partsCost + (decimal)(laborHours * 75); // Assuming $75/hour labor

                // Calculate next maintenance date based on type
                record.NextMaintenanceDate = record.Type switch
                {
                    MaintenanceType.Preventive => DateTime.UtcNow.AddDays(30),
                    MaintenanceType.Predictive => DateTime.UtcNow.AddDays(45),
                    _ => DateTime.UtcNow.AddDays(30)
                };

                robot.LastMaintenanceDate = record.PerformedDate.Value;
                robot.NextMaintenanceDate = record.NextMaintenanceDate;
                robot.Status = RobotStatus.Idle;

                return record;
            }
        }

        /// <summary>
        /// Gets robots requiring maintenance
        /// </summary>
        public List<ConstructionRobot> GetRobotsRequiringMaintenance(int daysAhead = 7)
        {
            lock (_lock)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
                return _robots.Values
                    .Where(r => r.NextMaintenanceDate <= cutoffDate)
                    .OrderBy(r => r.NextMaintenanceDate)
                    .ToList();
            }
        }

        #endregion

        #region Events

        private void OnSafetyAlertRaised(SafetyAlert alert) =>
            SafetyAlertRaised?.Invoke(this, alert);

        private void OnTaskStatusChanged(RobotTask task) =>
            TaskStatusChanged?.Invoke(this, task);

        private void OnHumanProximityDetected(ProximityDetection detection) =>
            HumanProximityDetected?.Invoke(this, detection);

        #endregion
    }
}
