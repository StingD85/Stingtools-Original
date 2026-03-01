// ============================================================================
// StingBIM AI - Facility Management Knowledge Base
// Domain knowledge for intelligent FM operations
// Equipment lifecycles, failure modes, maintenance best practices
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Knowledge
{
    #region Knowledge Models

    /// <summary>
    /// Equipment knowledge - lifecycle, failure modes, maintenance requirements
    /// </summary>
    public class EquipmentKnowledge
    {
        public string EquipmentType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Subcategory { get; set; } = string.Empty;

        // Lifecycle Information
        public int TypicalLifespanYears { get; set; }
        public int WarrantyPeriodYears { get; set; }
        public double DepreciationRate { get; set; }

        // Maintenance Requirements
        public List<MaintenanceRequirement> MaintenanceRequirements { get; set; } = new();
        public List<FailureMode> CommonFailureModes { get; set; } = new();
        public List<string> RequiredSkills { get; set; } = new();
        public List<string> RequiredCertifications { get; set; } = new();

        // Performance Metrics
        public double ExpectedMTBF { get; set; } // Mean Time Between Failures (hours)
        public double ExpectedMTTR { get; set; } // Mean Time To Repair (hours)
        public double TargetAvailability { get; set; } // Target uptime percentage

        // Cost Information
        public decimal TypicalMaintenanceCostPerYear { get; set; }
        public decimal ReplacementCostMultiplier { get; set; }

        // Environmental Factors
        public List<EnvironmentalSensitivity> EnvironmentalSensitivities { get; set; } = new();

        // Standards & Compliance
        public List<string> ApplicableStandards { get; set; } = new();
        public List<string> InspectionRequirements { get; set; } = new();
    }

    /// <summary>
    /// Maintenance requirement specification
    /// </summary>
    public class MaintenanceRequirement
    {
        public string TaskId { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MaintenanceFrequency Frequency { get; set; }
        public int FrequencyValue { get; set; } // e.g., every 3 months
        public TimeSpan EstimatedDuration { get; set; }
        public MaintenancePriority Priority { get; set; }
        public List<string> RequiredParts { get; set; } = new();
        public List<string> RequiredTools { get; set; } = new();
        public string Procedure { get; set; } = string.Empty;
        public bool RequiresShutdown { get; set; }
        public string SafetyPrecautions { get; set; } = string.Empty;
    }

    public enum MaintenanceFrequency
    {
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        SemiAnnually,
        Annually,
        AsNeeded,
        RunHoursBased,
        ConditionBased
    }

    public enum MaintenancePriority
    {
        Critical,    // Safety-related, must not be missed
        High,        // Important for reliability
        Medium,      // Standard maintenance
        Low,         // Nice to have
        Optional     // Can be deferred if needed
    }

    /// <summary>
    /// Known failure mode for equipment
    /// </summary>
    public class FailureMode
    {
        public string FailureModeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FailureSeverity Severity { get; set; }
        public double OccurrenceProbability { get; set; } // Annual probability
        public double DetectionDifficulty { get; set; } // 1-10 scale

        // Symptoms and Detection
        public List<string> EarlyWarningSymptoms { get; set; } = new();
        public List<string> SensorIndicators { get; set; } = new();
        public List<string> VisualIndicators { get; set; } = new();

        // Root Causes
        public List<string> CommonCauses { get; set; } = new();
        public List<string> PreventiveMeasures { get; set; } = new();

        // Impact
        public string ImpactDescription { get; set; } = string.Empty;
        public bool CausesDowntime { get; set; }
        public TimeSpan TypicalDowntime { get; set; }
        public decimal TypicalRepairCost { get; set; }

        // Resolution
        public string RecommendedAction { get; set; } = string.Empty;
        public List<string> RequiredParts { get; set; } = new();
        public TimeSpan TypicalRepairTime { get; set; }
    }

    public enum FailureSeverity
    {
        Catastrophic,  // Complete failure, safety risk
        Critical,      // Major failure, significant impact
        Major,         // Significant degradation
        Minor,         // Reduced performance
        Negligible     // Cosmetic or minimal impact
    }

    /// <summary>
    /// Environmental sensitivity factors
    /// </summary>
    public class EnvironmentalSensitivity
    {
        public string Factor { get; set; } = string.Empty; // Temperature, Humidity, Dust, etc.
        public double MinAcceptable { get; set; }
        public double MaxAcceptable { get; set; }
        public double OptimalMin { get; set; }
        public double OptimalMax { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string ImpactOfDeviation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Building system knowledge
    /// </summary>
    public class BuildingSystemKnowledge
    {
        public string SystemType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Components { get; set; } = new();
        public List<SystemInteraction> Interactions { get; set; } = new();
        public List<string> CriticalComponents { get; set; } = new();
        public double SystemReliabilityTarget { get; set; }
        public List<string> MonitoringPoints { get; set; } = new();
    }

    /// <summary>
    /// System interaction/dependency
    /// </summary>
    public class SystemInteraction
    {
        public string RelatedSystem { get; set; } = string.Empty;
        public InteractionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ImpactIfFailed { get; set; } = string.Empty;
    }

    public enum InteractionType
    {
        PowerSupply,
        ControlSignal,
        FluidFlow,
        AirFlow,
        StructuralSupport,
        DataExchange,
        SafetyInterlock
    }

    /// <summary>
    /// Maintenance best practice
    /// </summary>
    public class MaintenanceBestPractice
    {
        public string PracticeId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> ApplicableEquipment { get; set; } = new();
        public string Rationale { get; set; } = string.Empty;
        public List<string> Benefits { get; set; } = new();
        public List<string> ImplementationSteps { get; set; } = new();
        public string Source { get; set; } = string.Empty; // Standard reference
    }

    #endregion

    #region FM Knowledge Base

    /// <summary>
    /// Facility Management Knowledge Base
    /// Contains domain expertise for intelligent FM operations
    /// </summary>
    public class FMKnowledgeBase
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Knowledge repositories
        private readonly Dictionary<string, EquipmentKnowledge> _equipmentKnowledge = new();
        private readonly Dictionary<string, BuildingSystemKnowledge> _systemKnowledge = new();
        private readonly Dictionary<string, MaintenanceBestPractice> _bestPractices = new();
        private readonly Dictionary<string, List<FailureMode>> _failureModeLibrary = new();

        // Cross-reference indexes
        private readonly Dictionary<string, List<string>> _equipmentByCategory = new();
        private readonly Dictionary<string, List<string>> _practicesByCategory = new();

        public FMKnowledgeBase()
        {
            InitializeEquipmentKnowledge();
            InitializeSystemKnowledge();
            InitializeBestPractices();
            InitializeFailureModes();
            Logger.Info("FM Knowledge Base initialized with domain expertise");
        }

        #region Knowledge Initialization

        private void InitializeEquipmentKnowledge()
        {
            // HVAC Equipment
            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "AHU",
                Category = "HVAC",
                Subcategory = "Air Handling",
                TypicalLifespanYears = 20,
                WarrantyPeriodYears = 2,
                DepreciationRate = 0.05,
                ExpectedMTBF = 8760, // 1 year in hours
                ExpectedMTTR = 4,
                TargetAvailability = 0.98,
                TypicalMaintenanceCostPerYear = 2500000, // UGX
                ReplacementCostMultiplier = 1.5,
                RequiredSkills = new() { "HVAC Technician", "Electrical" },
                RequiredCertifications = new() { "HVAC Certification", "Refrigerant Handling" },
                ApplicableStandards = new() { "ASHRAE 62.1", "ASHRAE 90.1" },
                MaintenanceRequirements = new()
                {
                    new MaintenanceRequirement
                    {
                        TaskId = "AHU-PM-001",
                        TaskName = "Filter Replacement",
                        Frequency = MaintenanceFrequency.Monthly,
                        FrequencyValue = 1,
                        EstimatedDuration = TimeSpan.FromMinutes(30),
                        Priority = MaintenancePriority.High,
                        RequiredParts = new() { "Air Filter" },
                        RequiresShutdown = false
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "AHU-PM-002",
                        TaskName = "Belt Inspection and Adjustment",
                        Frequency = MaintenanceFrequency.Quarterly,
                        FrequencyValue = 3,
                        EstimatedDuration = TimeSpan.FromMinutes(45),
                        Priority = MaintenancePriority.Medium,
                        RequiresShutdown = true
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "AHU-PM-003",
                        TaskName = "Coil Cleaning",
                        Frequency = MaintenanceFrequency.SemiAnnually,
                        FrequencyValue = 6,
                        EstimatedDuration = TimeSpan.FromHours(2),
                        Priority = MaintenancePriority.High,
                        RequiresShutdown = true
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "AHU-PM-004",
                        TaskName = "Comprehensive Annual Service",
                        Frequency = MaintenanceFrequency.Annually,
                        FrequencyValue = 12,
                        EstimatedDuration = TimeSpan.FromHours(8),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = true
                    }
                },
                EnvironmentalSensitivities = new()
                {
                    new EnvironmentalSensitivity
                    {
                        Factor = "Ambient Temperature",
                        MinAcceptable = 5,
                        MaxAcceptable = 45,
                        OptimalMin = 15,
                        OptimalMax = 35,
                        Unit = "°C",
                        ImpactOfDeviation = "Reduced efficiency, potential compressor damage"
                    }
                }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "Chiller",
                Category = "HVAC",
                Subcategory = "Cooling",
                TypicalLifespanYears = 25,
                WarrantyPeriodYears = 5,
                ExpectedMTBF = 17520, // 2 years
                ExpectedMTTR = 8,
                TargetAvailability = 0.99,
                TypicalMaintenanceCostPerYear = 15000000, // UGX
                RequiredSkills = new() { "Chiller Specialist", "HVAC Technician", "Electrical" },
                RequiredCertifications = new() { "Chiller Certification", "High Voltage" },
                ApplicableStandards = new() { "ASHRAE 90.1", "ASHRAE 15" },
                MaintenanceRequirements = new()
                {
                    new MaintenanceRequirement
                    {
                        TaskId = "CHI-PM-001",
                        TaskName = "Daily Log Reading",
                        Frequency = MaintenanceFrequency.Daily,
                        EstimatedDuration = TimeSpan.FromMinutes(15),
                        Priority = MaintenancePriority.High,
                        RequiresShutdown = false
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "CHI-PM-002",
                        TaskName = "Oil Analysis",
                        Frequency = MaintenanceFrequency.Quarterly,
                        EstimatedDuration = TimeSpan.FromMinutes(30),
                        Priority = MaintenancePriority.High,
                        RequiresShutdown = false
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "CHI-PM-003",
                        TaskName = "Tube Cleaning",
                        Frequency = MaintenanceFrequency.Annually,
                        EstimatedDuration = TimeSpan.FromHours(16),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = true
                    }
                }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "Elevator",
                Category = "Vertical Transport",
                Subcategory = "Passenger Lift",
                TypicalLifespanYears = 25,
                WarrantyPeriodYears = 2,
                ExpectedMTBF = 4380,
                ExpectedMTTR = 2,
                TargetAvailability = 0.995,
                TypicalMaintenanceCostPerYear = 8000000, // UGX
                RequiredSkills = new() { "Elevator Technician" },
                RequiredCertifications = new() { "Elevator Certification", "Safety Certification" },
                ApplicableStandards = new() { "EN 81", "ASME A17.1" },
                InspectionRequirements = new() { "Annual safety inspection", "Load test every 5 years" },
                MaintenanceRequirements = new()
                {
                    new MaintenanceRequirement
                    {
                        TaskId = "ELV-PM-001",
                        TaskName = "Monthly Service",
                        Frequency = MaintenanceFrequency.Monthly,
                        EstimatedDuration = TimeSpan.FromHours(2),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = true
                    }
                }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "Generator",
                Category = "Electrical",
                Subcategory = "Emergency Power",
                TypicalLifespanYears = 30,
                WarrantyPeriodYears = 2,
                ExpectedMTBF = 8760,
                ExpectedMTTR = 4,
                TargetAvailability = 0.99,
                TypicalMaintenanceCostPerYear = 5000000, // UGX
                RequiredSkills = new() { "Generator Technician", "Electrical" },
                RequiredCertifications = new() { "Generator Certification" },
                ApplicableStandards = new() { "NFPA 110", "ISO 8528" },
                MaintenanceRequirements = new()
                {
                    new MaintenanceRequirement
                    {
                        TaskId = "GEN-PM-001",
                        TaskName = "Weekly Test Run",
                        Frequency = MaintenanceFrequency.Weekly,
                        EstimatedDuration = TimeSpan.FromMinutes(30),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = false
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "GEN-PM-002",
                        TaskName = "Oil and Filter Change",
                        Frequency = MaintenanceFrequency.Quarterly,
                        EstimatedDuration = TimeSpan.FromHours(2),
                        Priority = MaintenancePriority.High,
                        RequiredParts = new() { "Oil Filter", "Engine Oil" },
                        RequiresShutdown = true
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "GEN-PM-003",
                        TaskName = "Load Bank Test",
                        Frequency = MaintenanceFrequency.Annually,
                        EstimatedDuration = TimeSpan.FromHours(4),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = false
                    }
                }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "Fire Pump",
                Category = "Fire Protection",
                Subcategory = "Suppression",
                TypicalLifespanYears = 25,
                WarrantyPeriodYears = 2,
                ExpectedMTBF = 17520,
                ExpectedMTTR = 4,
                TargetAvailability = 0.999,
                TypicalMaintenanceCostPerYear = 3000000, // UGX
                RequiredSkills = new() { "Fire Systems Technician", "Mechanical" },
                RequiredCertifications = new() { "Fire Systems Certification" },
                ApplicableStandards = new() { "NFPA 20", "NFPA 25" },
                InspectionRequirements = new() { "Weekly churn test", "Annual flow test" },
                MaintenanceRequirements = new()
                {
                    new MaintenanceRequirement
                    {
                        TaskId = "FPM-PM-001",
                        TaskName = "Weekly Churn Test",
                        Frequency = MaintenanceFrequency.Weekly,
                        EstimatedDuration = TimeSpan.FromMinutes(15),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = false
                    }
                }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "UPS",
                Category = "Electrical",
                Subcategory = "Power Quality",
                TypicalLifespanYears = 15,
                WarrantyPeriodYears = 2,
                ExpectedMTBF = 8760,
                ExpectedMTTR = 2,
                TargetAvailability = 0.9999,
                TypicalMaintenanceCostPerYear = 4000000, // UGX
                RequiredSkills = new() { "UPS Technician", "Electrical" },
                RequiredCertifications = new() { "UPS Certification", "High Voltage" },
                MaintenanceRequirements = new()
                {
                    new MaintenanceRequirement
                    {
                        TaskId = "UPS-PM-001",
                        TaskName = "Visual Inspection",
                        Frequency = MaintenanceFrequency.Monthly,
                        EstimatedDuration = TimeSpan.FromMinutes(30),
                        Priority = MaintenancePriority.High,
                        RequiresShutdown = false
                    },
                    new MaintenanceRequirement
                    {
                        TaskId = "UPS-PM-002",
                        TaskName = "Battery Impedance Test",
                        Frequency = MaintenanceFrequency.Quarterly,
                        EstimatedDuration = TimeSpan.FromHours(2),
                        Priority = MaintenancePriority.Critical,
                        RequiresShutdown = false
                    }
                }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "Boiler",
                Category = "HVAC",
                Subcategory = "Heating",
                TypicalLifespanYears = 25,
                WarrantyPeriodYears = 2,
                ExpectedMTBF = 8760,
                ExpectedMTTR = 6,
                TargetAvailability = 0.98,
                TypicalMaintenanceCostPerYear = 6000000, // UGX
                RequiredSkills = new() { "Boiler Technician", "Mechanical" },
                RequiredCertifications = new() { "Boiler Operation License" },
                ApplicableStandards = new() { "ASME BPVC" },
                InspectionRequirements = new() { "Annual pressure vessel inspection" }
            });

            AddEquipmentKnowledge(new EquipmentKnowledge
            {
                EquipmentType = "Cooling Tower",
                Category = "HVAC",
                Subcategory = "Heat Rejection",
                TypicalLifespanYears = 20,
                WarrantyPeriodYears = 2,
                ExpectedMTBF = 8760,
                ExpectedMTTR = 4,
                TargetAvailability = 0.98,
                TypicalMaintenanceCostPerYear = 4000000, // UGX
                RequiredSkills = new() { "HVAC Technician", "Water Treatment" },
                RequiredCertifications = new() { "Water Treatment Certification" },
                ApplicableStandards = new() { "ASHRAE 188" } // Legionella prevention
            });

            Logger.Info($"Loaded {_equipmentKnowledge.Count} equipment knowledge entries");
        }

        private void InitializeSystemKnowledge()
        {
            _systemKnowledge["HVAC"] = new BuildingSystemKnowledge
            {
                SystemType = "HVAC",
                Description = "Heating, Ventilation, and Air Conditioning system",
                Components = new() { "AHU", "Chiller", "Boiler", "Cooling Tower", "FCU", "VAV", "Ductwork", "Piping" },
                CriticalComponents = new() { "Chiller", "Boiler", "AHU" },
                SystemReliabilityTarget = 0.98,
                MonitoringPoints = new() { "Supply Air Temperature", "Return Air Temperature", "Chilled Water Supply", "Zone Temperatures" },
                Interactions = new()
                {
                    new SystemInteraction { RelatedSystem = "Electrical", Type = InteractionType.PowerSupply, ImpactIfFailed = "Complete HVAC shutdown" },
                    new SystemInteraction { RelatedSystem = "BMS", Type = InteractionType.ControlSignal, ImpactIfFailed = "Loss of automatic control" },
                    new SystemInteraction { RelatedSystem = "Plumbing", Type = InteractionType.FluidFlow, ImpactIfFailed = "Loss of condenser water" }
                }
            };

            _systemKnowledge["Electrical"] = new BuildingSystemKnowledge
            {
                SystemType = "Electrical",
                Description = "Electrical power distribution system",
                Components = new() { "Transformer", "Switchgear", "Generator", "UPS", "Distribution Boards", "Cabling" },
                CriticalComponents = new() { "Transformer", "Generator", "UPS" },
                SystemReliabilityTarget = 0.9999,
                MonitoringPoints = new() { "Voltage", "Current", "Power Factor", "Frequency", "Generator Status" },
                Interactions = new()
                {
                    new SystemInteraction { RelatedSystem = "HVAC", Type = InteractionType.PowerSupply, ImpactIfFailed = "HVAC shutdown" },
                    new SystemInteraction { RelatedSystem = "Fire Protection", Type = InteractionType.PowerSupply, ImpactIfFailed = "Fire system on battery backup" },
                    new SystemInteraction { RelatedSystem = "Vertical Transport", Type = InteractionType.PowerSupply, ImpactIfFailed = "Elevator shutdown" }
                }
            };

            _systemKnowledge["Fire Protection"] = new BuildingSystemKnowledge
            {
                SystemType = "Fire Protection",
                Description = "Fire detection and suppression system",
                Components = new() { "Fire Pump", "Sprinklers", "Fire Alarm Panel", "Smoke Detectors", "Fire Extinguishers", "Hydrants" },
                CriticalComponents = new() { "Fire Pump", "Fire Alarm Panel" },
                SystemReliabilityTarget = 0.9999,
                MonitoringPoints = new() { "Fire Alarm Status", "Pump Pressure", "Tank Level" },
                Interactions = new()
                {
                    new SystemInteraction { RelatedSystem = "Electrical", Type = InteractionType.PowerSupply, ImpactIfFailed = "Battery backup activated" },
                    new SystemInteraction { RelatedSystem = "Plumbing", Type = InteractionType.FluidFlow, ImpactIfFailed = "Limited water supply" },
                    new SystemInteraction { RelatedSystem = "BMS", Type = InteractionType.SafetyInterlock, ImpactIfFailed = "Manual operation required" }
                }
            };

            _systemKnowledge["Plumbing"] = new BuildingSystemKnowledge
            {
                SystemType = "Plumbing",
                Description = "Water supply and drainage system",
                Components = new() { "Water Tanks", "Pumps", "Piping", "Fixtures", "Water Heaters", "Treatment Systems" },
                CriticalComponents = new() { "Water Tanks", "Booster Pumps" },
                SystemReliabilityTarget = 0.99,
                MonitoringPoints = new() { "Tank Levels", "Pump Status", "Water Quality" }
            };

            _systemKnowledge["Vertical Transport"] = new BuildingSystemKnowledge
            {
                SystemType = "Vertical Transport",
                Description = "Elevators and escalators",
                Components = new() { "Elevators", "Escalators", "Controllers", "Motors", "Safety Systems" },
                CriticalComponents = new() { "Elevator Cars", "Controllers" },
                SystemReliabilityTarget = 0.995,
                MonitoringPoints = new() { "Elevator Status", "Trip Count", "Door Cycles" }
            };

            Logger.Info($"Loaded {_systemKnowledge.Count} building system knowledge entries");
        }

        private void InitializeBestPractices()
        {
            _bestPractices["BP-001"] = new MaintenanceBestPractice
            {
                PracticeId = "BP-001",
                Title = "Implement Condition-Based Maintenance for Critical Equipment",
                Category = "Maintenance Strategy",
                Description = "Use sensor data and performance metrics to trigger maintenance based on actual equipment condition rather than fixed schedules.",
                ApplicableEquipment = new() { "Chiller", "AHU", "Generator", "Elevator" },
                Rationale = "Reduces unnecessary maintenance while preventing unexpected failures",
                Benefits = new() { "20-25% reduction in maintenance costs", "Increased equipment uptime", "Reduced spare parts inventory" },
                ImplementationSteps = new()
                {
                    "Identify critical parameters for each equipment type",
                    "Install necessary sensors and monitoring equipment",
                    "Establish baseline performance metrics",
                    "Define threshold values for maintenance triggers",
                    "Integrate with CMMS for automatic work order generation"
                },
                Source = "ISO 55000"
            };

            _bestPractices["BP-002"] = new MaintenanceBestPractice
            {
                PracticeId = "BP-002",
                Title = "Maintain Comprehensive Equipment Documentation",
                Category = "Documentation",
                Description = "Keep detailed records of all maintenance activities, equipment specifications, and operational history.",
                ApplicableEquipment = new() { "All" },
                Rationale = "Enables data-driven decision making and knowledge transfer",
                Benefits = new() { "Faster troubleshooting", "Better maintenance planning", "Regulatory compliance" },
                Source = "ISO 19650"
            };

            _bestPractices["BP-003"] = new MaintenanceBestPractice
            {
                PracticeId = "BP-003",
                Title = "Implement Energy Monitoring for Major Equipment",
                Category = "Energy Management",
                Description = "Monitor energy consumption of major equipment to identify inefficiencies and optimization opportunities.",
                ApplicableEquipment = new() { "Chiller", "AHU", "Boiler", "Cooling Tower" },
                Rationale = "Energy costs represent 30-40% of building operating expenses",
                Benefits = new() { "10-20% energy savings potential", "Early detection of performance degradation", "Carbon footprint reduction" },
                Source = "ASHRAE 90.1"
            };

            _bestPractices["BP-004"] = new MaintenanceBestPractice
            {
                PracticeId = "BP-004",
                Title = "Establish Spare Parts Management Program",
                Category = "Inventory Management",
                Description = "Maintain optimal inventory of critical spare parts based on equipment criticality and lead times.",
                ApplicableEquipment = new() { "All" },
                Rationale = "Reduces downtime from parts unavailability while minimizing inventory costs",
                Benefits = new() { "Reduced downtime", "Lower inventory costs", "Improved response time" }
            };

            _bestPractices["BP-005"] = new MaintenanceBestPractice
            {
                PracticeId = "BP-005",
                Title = "Conduct Regular Water Treatment for Cooling Systems",
                Category = "Water Management",
                Description = "Implement comprehensive water treatment program for cooling towers and closed loop systems.",
                ApplicableEquipment = new() { "Cooling Tower", "Chiller" },
                Rationale = "Prevents Legionella, corrosion, and scale buildup",
                Benefits = new() { "Equipment longevity", "Energy efficiency", "Health safety compliance" },
                Source = "ASHRAE 188"
            };

            Logger.Info($"Loaded {_bestPractices.Count} best practice entries");
        }

        private void InitializeFailureModes()
        {
            // AHU Failure Modes
            _failureModeLibrary["AHU"] = new List<FailureMode>
            {
                new FailureMode
                {
                    FailureModeId = "AHU-FM-001",
                    Name = "Belt Failure",
                    Description = "Fan belt breaks or slips causing reduced airflow",
                    Severity = FailureSeverity.Major,
                    OccurrenceProbability = 0.15,
                    DetectionDifficulty = 3,
                    EarlyWarningSymptoms = new() { "Squealing noise", "Reduced airflow", "Belt dust accumulation" },
                    CommonCauses = new() { "Wear and age", "Misalignment", "Improper tension" },
                    PreventiveMeasures = new() { "Regular belt inspection", "Proper tensioning", "Timely replacement" },
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromHours(2),
                    TypicalRepairCost = 500000, // UGX
                    RequiredParts = new() { "V-Belt" },
                    TypicalRepairTime = TimeSpan.FromHours(1)
                },
                new FailureMode
                {
                    FailureModeId = "AHU-FM-002",
                    Name = "Motor Bearing Failure",
                    Description = "Fan motor bearings wear out causing vibration and eventual failure",
                    Severity = FailureSeverity.Critical,
                    OccurrenceProbability = 0.05,
                    DetectionDifficulty = 4,
                    EarlyWarningSymptoms = new() { "Increased vibration", "High motor temperature", "Grinding noise" },
                    SensorIndicators = new() { "Vibration > 5mm/s", "Temperature > 80°C" },
                    CommonCauses = new() { "Lack of lubrication", "Overloading", "Age" },
                    PreventiveMeasures = new() { "Vibration monitoring", "Regular lubrication", "Load monitoring" },
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromHours(8),
                    TypicalRepairCost = 3000000, // UGX
                    RequiredParts = new() { "Motor Bearings", "Lubricant" },
                    TypicalRepairTime = TimeSpan.FromHours(4)
                },
                new FailureMode
                {
                    FailureModeId = "AHU-FM-003",
                    Name = "Coil Fouling",
                    Description = "Cooling/heating coil becomes fouled reducing heat transfer efficiency",
                    Severity = FailureSeverity.Minor,
                    OccurrenceProbability = 0.3,
                    DetectionDifficulty = 5,
                    EarlyWarningSymptoms = new() { "Reduced cooling capacity", "Higher energy consumption", "Temperature difference across coil decreased" },
                    CommonCauses = new() { "Dirty filters", "Poor air quality", "Lack of cleaning" },
                    PreventiveMeasures = new() { "Regular filter replacement", "Annual coil cleaning", "Air quality monitoring" },
                    CausesDowntime = false,
                    TypicalRepairCost = 1000000, // UGX
                    TypicalRepairTime = TimeSpan.FromHours(3)
                }
            };

            // Chiller Failure Modes
            _failureModeLibrary["Chiller"] = new List<FailureMode>
            {
                new FailureMode
                {
                    FailureModeId = "CHI-FM-001",
                    Name = "Compressor Failure",
                    Description = "Compressor motor or mechanical components fail",
                    Severity = FailureSeverity.Catastrophic,
                    OccurrenceProbability = 0.02,
                    DetectionDifficulty = 6,
                    EarlyWarningSymptoms = new() { "Oil analysis anomalies", "Unusual noise", "High discharge temperature" },
                    SensorIndicators = new() { "Oil contamination", "High vibration", "Low oil pressure" },
                    CommonCauses = new() { "Liquid slugging", "Loss of lubrication", "Electrical failure" },
                    PreventiveMeasures = new() { "Regular oil analysis", "Proper superheat control", "Vibration monitoring" },
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromDays(7),
                    TypicalRepairCost = 100000000, // UGX - major expense
                    RequiredParts = new() { "Compressor" },
                    TypicalRepairTime = TimeSpan.FromDays(3)
                },
                new FailureMode
                {
                    FailureModeId = "CHI-FM-002",
                    Name = "Refrigerant Leak",
                    Description = "Loss of refrigerant charge through system leak",
                    Severity = FailureSeverity.Major,
                    OccurrenceProbability = 0.08,
                    DetectionDifficulty = 5,
                    EarlyWarningSymptoms = new() { "Reduced cooling capacity", "Low suction pressure", "Oil traces at joints" },
                    CommonCauses = new() { "Vibration damage", "Corrosion", "Joint failure" },
                    PreventiveMeasures = new() { "Leak detection system", "Regular inspections", "Vibration isolation" },
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromHours(24),
                    TypicalRepairCost = 5000000, // UGX
                    TypicalRepairTime = TimeSpan.FromHours(8)
                },
                new FailureMode
                {
                    FailureModeId = "CHI-FM-003",
                    Name = "Tube Fouling",
                    Description = "Evaporator or condenser tubes become fouled",
                    Severity = FailureSeverity.Minor,
                    OccurrenceProbability = 0.2,
                    DetectionDifficulty = 4,
                    EarlyWarningSymptoms = new() { "Approach temperature increase", "Higher energy consumption", "Reduced capacity" },
                    CommonCauses = new() { "Poor water treatment", "Scale buildup", "Biological growth" },
                    PreventiveMeasures = new() { "Water treatment program", "Regular tube cleaning" },
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromHours(24),
                    TypicalRepairCost = 2000000, // UGX
                    TypicalRepairTime = TimeSpan.FromHours(16)
                }
            };

            // Generator Failure Modes
            _failureModeLibrary["Generator"] = new List<FailureMode>
            {
                new FailureMode
                {
                    FailureModeId = "GEN-FM-001",
                    Name = "Starting Failure",
                    Description = "Generator fails to start when called upon",
                    Severity = FailureSeverity.Catastrophic,
                    OccurrenceProbability = 0.05,
                    DetectionDifficulty = 2,
                    EarlyWarningSymptoms = new() { "Slow cranking during tests", "Battery issues", "Fuel system problems" },
                    CommonCauses = new() { "Dead batteries", "Fuel problems", "Starter motor failure" },
                    PreventiveMeasures = new() { "Weekly test runs", "Battery maintenance", "Fuel quality monitoring" },
                    ImpactDescription = "Complete loss of emergency power capability",
                    CausesDowntime = false, // Building-level impact
                    TypicalRepairCost = 2000000, // UGX
                    TypicalRepairTime = TimeSpan.FromHours(4)
                }
            };

            // Elevator Failure Modes
            _failureModeLibrary["Elevator"] = new List<FailureMode>
            {
                new FailureMode
                {
                    FailureModeId = "ELV-FM-001",
                    Name = "Door Malfunction",
                    Description = "Elevator doors fail to open, close, or operate properly",
                    Severity = FailureSeverity.Major,
                    OccurrenceProbability = 0.2,
                    DetectionDifficulty = 2,
                    EarlyWarningSymptoms = new() { "Slow door operation", "Unusual door noises", "Frequent door reopening" },
                    CommonCauses = new() { "Door operator wear", "Sensor misalignment", "Track obstruction" },
                    PreventiveMeasures = new() { "Regular door adjustment", "Sensor cleaning", "Lubrication" },
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromHours(2),
                    TypicalRepairCost = 1500000, // UGX
                    TypicalRepairTime = TimeSpan.FromHours(2)
                },
                new FailureMode
                {
                    FailureModeId = "ELV-FM-002",
                    Name = "Entrapment",
                    Description = "Passengers trapped in elevator car",
                    Severity = FailureSeverity.Critical,
                    OccurrenceProbability = 0.01,
                    DetectionDifficulty = 1,
                    CommonCauses = new() { "Power failure", "Controller malfunction", "Safety device activation" },
                    PreventiveMeasures = new() { "Regular maintenance", "Emergency power backup", "Communication system testing" },
                    ImpactDescription = "Safety incident requiring emergency response",
                    CausesDowntime = true,
                    TypicalDowntime = TimeSpan.FromHours(1)
                }
            };

            Logger.Info($"Loaded failure modes for {_failureModeLibrary.Count} equipment types");
        }

        private void AddEquipmentKnowledge(EquipmentKnowledge knowledge)
        {
            _equipmentKnowledge[knowledge.EquipmentType] = knowledge;

            if (!_equipmentByCategory.ContainsKey(knowledge.Category))
                _equipmentByCategory[knowledge.Category] = new List<string>();

            if (!_equipmentByCategory[knowledge.Category].Contains(knowledge.EquipmentType))
                _equipmentByCategory[knowledge.Category].Add(knowledge.EquipmentType);
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get equipment knowledge by type
        /// </summary>
        public EquipmentKnowledge GetEquipmentKnowledge(string equipmentType)
        {
            return _equipmentKnowledge.TryGetValue(equipmentType, out var knowledge)
                ? knowledge
                : null;
        }

        /// <summary>
        /// Get all equipment types in a category
        /// </summary>
        public List<string> GetEquipmentByCategory(string category)
        {
            return _equipmentByCategory.TryGetValue(category, out var equipment)
                ? equipment
                : new List<string>();
        }

        /// <summary>
        /// Get building system knowledge
        /// </summary>
        public BuildingSystemKnowledge GetSystemKnowledge(string systemType)
        {
            return _systemKnowledge.TryGetValue(systemType, out var knowledge)
                ? knowledge
                : null;
        }

        /// <summary>
        /// Get failure modes for equipment type
        /// </summary>
        public List<FailureMode> GetFailureModes(string equipmentType)
        {
            return _failureModeLibrary.TryGetValue(equipmentType, out var modes)
                ? modes
                : new List<FailureMode>();
        }

        /// <summary>
        /// Get maintenance requirements for equipment
        /// </summary>
        public List<MaintenanceRequirement> GetMaintenanceRequirements(string equipmentType)
        {
            var knowledge = GetEquipmentKnowledge(equipmentType);
            return knowledge?.MaintenanceRequirements ?? new List<MaintenanceRequirement>();
        }

        /// <summary>
        /// Get best practices for equipment type
        /// </summary>
        public List<MaintenanceBestPractice> GetBestPractices(string equipmentType)
        {
            return _bestPractices.Values
                .Where(bp => bp.ApplicableEquipment.Contains(equipmentType) || bp.ApplicableEquipment.Contains("All"))
                .ToList();
        }

        /// <summary>
        /// Get all best practices
        /// </summary>
        public IEnumerable<MaintenanceBestPractice> GetAllBestPractices() => _bestPractices.Values;

        /// <summary>
        /// Get expected lifespan for equipment
        /// </summary>
        public int GetExpectedLifespan(string equipmentType)
        {
            return GetEquipmentKnowledge(equipmentType)?.TypicalLifespanYears ?? 15;
        }

        /// <summary>
        /// Get target availability for equipment
        /// </summary>
        public double GetTargetAvailability(string equipmentType)
        {
            return GetEquipmentKnowledge(equipmentType)?.TargetAvailability ?? 0.95;
        }

        /// <summary>
        /// Get system dependencies
        /// </summary>
        public List<SystemInteraction> GetSystemDependencies(string systemType)
        {
            return GetSystemKnowledge(systemType)?.Interactions ?? new List<SystemInteraction>();
        }

        /// <summary>
        /// Identify critical equipment in a system
        /// </summary>
        public List<string> GetCriticalComponents(string systemType)
        {
            return GetSystemKnowledge(systemType)?.CriticalComponents ?? new List<string>();
        }

        /// <summary>
        /// Get required skills for equipment maintenance
        /// </summary>
        public List<string> GetRequiredSkills(string equipmentType)
        {
            return GetEquipmentKnowledge(equipmentType)?.RequiredSkills ?? new List<string>();
        }

        /// <summary>
        /// Get applicable standards for equipment
        /// </summary>
        public List<string> GetApplicableStandards(string equipmentType)
        {
            return GetEquipmentKnowledge(equipmentType)?.ApplicableStandards ?? new List<string>();
        }

        /// <summary>
        /// Calculate risk priority number for failure mode
        /// </summary>
        public double CalculateRPN(FailureMode failureMode)
        {
            // RPN = Severity × Occurrence × Detection
            int severityScore = failureMode.Severity switch
            {
                FailureSeverity.Catastrophic => 10,
                FailureSeverity.Critical => 8,
                FailureSeverity.Major => 6,
                FailureSeverity.Minor => 4,
                FailureSeverity.Negligible => 2,
                _ => 5
            };

            double occurrenceScore = failureMode.OccurrenceProbability * 10;
            double detectionScore = failureMode.DetectionDifficulty;

            return severityScore * occurrenceScore * detectionScore;
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Get knowledge base statistics
        /// </summary>
        public FMKnowledgeStats GetStatistics()
        {
            return new FMKnowledgeStats
            {
                TotalEquipmentTypes = _equipmentKnowledge.Count,
                TotalBuildingSystems = _systemKnowledge.Count,
                TotalFailureModes = _failureModeLibrary.Values.Sum(fm => fm.Count),
                TotalBestPractices = _bestPractices.Count,
                EquipmentByCategory = _equipmentByCategory.ToDictionary(x => x.Key, x => x.Value.Count),
                CoverageBySystem = _systemKnowledge.Keys.ToList()
            };
        }

        #endregion
    }

    /// <summary>
    /// Knowledge base statistics
    /// </summary>
    public class FMKnowledgeStats
    {
        public int TotalEquipmentTypes { get; set; }
        public int TotalBuildingSystems { get; set; }
        public int TotalFailureModes { get; set; }
        public int TotalBestPractices { get; set; }
        public Dictionary<string, int> EquipmentByCategory { get; set; } = new();
        public List<string> CoverageBySystem { get; set; } = new();
    }

    #endregion
}
