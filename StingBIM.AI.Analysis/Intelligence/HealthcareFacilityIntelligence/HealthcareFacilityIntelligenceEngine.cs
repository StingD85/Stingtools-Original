// ===================================================================
// StingBIM Healthcare Facility Intelligence Engine
// FGI Guidelines, infection control, clinical workflows, medical gas
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.HealthcareFacilityIntelligence
{
    #region Enums

    public enum FacilityType { Hospital, AmbulatoryCare, Nursing, Psychiatric, Rehabilitation, Outpatient }
    public enum DepartmentType { Emergency, Surgery, ICU, MedSurg, Imaging, Laboratory, Pharmacy, Support }
    public enum RoomType { PatientRoom, OperatingRoom, Procedure, Exam, Treatment, Isolation, Clean, Soiled }
    public enum InfectionControlZone { Protective, Clean, General, Soiled, Isolation }
    public enum VentilationClass { ClassA, ClassB, ClassC, ClassD }
    public enum MedicalGasType { Oxygen, MedicalAir, Vacuum, Nitrogen, NitrousOxide, CO2, WAGD }

    #endregion

    #region Data Models

    public class HealthcareProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public FacilityType FacilityType { get; set; }
        public int LicensedBeds { get; set; }
        public List<Department> Departments { get; set; } = new();
        public List<ClinicalRoom> Rooms { get; set; } = new();
        public List<MedicalGasZone> GasZones { get; set; } = new();
        public InfectionControlPlan InfectionControl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Department
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public DepartmentType Type { get; set; }
        public double GrossArea { get; set; }
        public double NetToGrossRatio { get; set; } = 0.65;
        public int StaffCount { get; set; }
        public List<string> RoomIds { get; set; } = new();
        public List<AdjacencyRequirement> Adjacencies { get; set; } = new();
        public List<string> ComplianceNotes { get; set; } = new();
    }

    public class ClinicalRoom
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RoomNumber { get; set; }
        public string Name { get; set; }
        public RoomType Type { get; set; }
        public double Area { get; set; }
        public double ClearFloorArea { get; set; }
        public double CeilingHeight { get; set; }
        public VentilationRequirements Ventilation { get; set; }
        public List<MedicalGasOutlet> GasOutlets { get; set; } = new();
        public InfectionControlZone ICZone { get; set; }
        public bool HandwashingStation { get; set; }
        public List<string> EquipmentList { get; set; } = new();
        public bool MeetsFGI { get; set; }
        public List<string> Deficiencies { get; set; } = new();
    }

    public class VentilationRequirements
    {
        public VentilationClass Class { get; set; }
        public int MinACH { get; set; }
        public int MinOutsideACH { get; set; }
        public bool NegativePressure { get; set; }
        public bool PositivePressure { get; set; }
        public bool HEPARequired { get; set; }
        public double TemperatureMin { get; set; }
        public double TemperatureMax { get; set; }
        public double HumidityMin { get; set; }
        public double HumidityMax { get; set; }
        public bool RecirculationAllowed { get; set; }
    }

    public class MedicalGasOutlet
    {
        public MedicalGasType GasType { get; set; }
        public int Quantity { get; set; }
        public double HeightAFF { get; set; }
        public string Location { get; set; }
    }

    public class MedicalGasZone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public MedicalGasType GasType { get; set; }
        public List<string> RoomIds { get; set; } = new();
        public double TotalDemand { get; set; }
        public double DiversityFactor { get; set; }
        public double DesignFlow { get; set; }
        public string SourceLocation { get; set; }
        public bool HasAlarmPanel { get; set; }
        public bool HasZoneValve { get; set; }
    }

    public class AdjacencyRequirement
    {
        public string TargetDepartment { get; set; }
        public string Relationship { get; set; }
        public int MaxTravelTime { get; set; }
        public bool DirectAccess { get; set; }
        public string Rationale { get; set; }
    }

    public class InfectionControlPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<AirborneIsolationRoom> AIIRooms { get; set; } = new();
        public List<ProtectiveEnvironment> PERooms { get; set; } = new();
        public List<HandHygieneStation> HandHygiene { get; set; } = new();
        public List<string> TrafficPatterns { get; set; } = new();
        public double AIIRoomRatio { get; set; }
        public bool MeetsCDCGuidelines { get; set; }
    }

    public class AirborneIsolationRoom
    {
        public string RoomId { get; set; }
        public bool HasAnteroom { get; set; }
        public bool HasPrivateBathroom { get; set; }
        public int MinACH { get; set; } = 12;
        public bool NegativePressure { get; set; }
        public bool PressureMonitor { get; set; }
        public bool SelfClosingDoor { get; set; }
    }

    public class ProtectiveEnvironment
    {
        public string RoomId { get; set; }
        public bool HasAnteroom { get; set; }
        public int MinACH { get; set; } = 12;
        public bool PositivePressure { get; set; }
        public bool HEPAFiltered { get; set; }
        public bool SealedRoom { get; set; }
    }

    public class HandHygieneStation
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public bool WithinReach { get; set; }
        public double DistanceFromEntry { get; set; }
    }

    public class FGIComplianceReport
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int TotalRooms { get; set; }
        public int CompliantRooms { get; set; }
        public int NonCompliantRooms { get; set; }
        public double CompliancePercentage => TotalRooms > 0 ? CompliantRooms * 100.0 / TotalRooms : 0;
        public List<ComplianceIssue> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ComplianceIssue
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string FGIReference { get; set; }
        public string RequiredValue { get; set; }
        public string ActualValue { get; set; }
    }

    #endregion

    public sealed class HealthcareFacilityIntelligenceEngine
    {
        private static readonly Lazy<HealthcareFacilityIntelligenceEngine> _instance =
            new Lazy<HealthcareFacilityIntelligenceEngine>(() => new HealthcareFacilityIntelligenceEngine());
        public static HealthcareFacilityIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, HealthcareProject> _projects = new();
        private readonly object _lock = new object();

        // FGI 2022 Minimum Room Areas (sq ft)
        private readonly Dictionary<RoomType, double> _fgiMinAreas = new()
        {
            [RoomType.PatientRoom] = 120,
            [RoomType.OperatingRoom] = 400,
            [RoomType.Procedure] = 250,
            [RoomType.Exam] = 100,
            [RoomType.Treatment] = 120,
            [RoomType.Isolation] = 150,
            [RoomType.Clean] = 50,
            [RoomType.Soiled] = 40
        };

        // FGI Ventilation Requirements
        private readonly Dictionary<RoomType, VentilationRequirements> _fgiVentilation = new()
        {
            [RoomType.OperatingRoom] = new VentilationRequirements
            {
                Class = VentilationClass.ClassA,
                MinACH = 20,
                MinOutsideACH = 4,
                PositivePressure = true,
                HEPARequired = true,
                TemperatureMin = 68,
                TemperatureMax = 75,
                HumidityMin = 30,
                HumidityMax = 60,
                RecirculationAllowed = false
            },
            [RoomType.Isolation] = new VentilationRequirements
            {
                Class = VentilationClass.ClassA,
                MinACH = 12,
                MinOutsideACH = 2,
                NegativePressure = true,
                TemperatureMin = 70,
                TemperatureMax = 75,
                HumidityMin = 30,
                HumidityMax = 60,
                RecirculationAllowed = false
            },
            [RoomType.PatientRoom] = new VentilationRequirements
            {
                Class = VentilationClass.ClassB,
                MinACH = 6,
                MinOutsideACH = 2,
                TemperatureMin = 70,
                TemperatureMax = 75,
                HumidityMin = 30,
                HumidityMax = 60,
                RecirculationAllowed = true
            },
            [RoomType.Exam] = new VentilationRequirements
            {
                Class = VentilationClass.ClassC,
                MinACH = 6,
                MinOutsideACH = 2,
                TemperatureMin = 70,
                TemperatureMax = 75,
                RecirculationAllowed = true
            }
        };

        // Medical Gas Requirements by Room Type
        private readonly Dictionary<RoomType, List<MedicalGasOutlet>> _gasRequirements = new()
        {
            [RoomType.OperatingRoom] = new List<MedicalGasOutlet>
            {
                new() { GasType = MedicalGasType.Oxygen, Quantity = 4 },
                new() { GasType = MedicalGasType.MedicalAir, Quantity = 4 },
                new() { GasType = MedicalGasType.Vacuum, Quantity = 4 },
                new() { GasType = MedicalGasType.Nitrogen, Quantity = 2 },
                new() { GasType = MedicalGasType.NitrousOxide, Quantity = 1 },
                new() { GasType = MedicalGasType.WAGD, Quantity = 2 }
            },
            [RoomType.PatientRoom] = new List<MedicalGasOutlet>
            {
                new() { GasType = MedicalGasType.Oxygen, Quantity = 2 },
                new() { GasType = MedicalGasType.MedicalAir, Quantity = 1 },
                new() { GasType = MedicalGasType.Vacuum, Quantity = 2 }
            },
            [RoomType.Procedure] = new List<MedicalGasOutlet>
            {
                new() { GasType = MedicalGasType.Oxygen, Quantity = 2 },
                new() { GasType = MedicalGasType.MedicalAir, Quantity = 2 },
                new() { GasType = MedicalGasType.Vacuum, Quantity = 2 }
            }
        };

        private HealthcareFacilityIntelligenceEngine() { }

        public HealthcareProject CreateHealthcareProject(string projectId, string projectName,
            FacilityType facilityType, int licensedBeds)
        {
            var project = new HealthcareProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                FacilityType = facilityType,
                LicensedBeds = licensedBeds
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public Department AddDepartment(string projectId, string name, DepartmentType type, double grossArea)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var dept = new Department
                {
                    Name = name,
                    Type = type,
                    GrossArea = grossArea
                };

                // Set standard adjacency requirements
                SetDepartmentAdjacencies(dept);

                project.Departments.Add(dept);
                return dept;
            }
        }

        private void SetDepartmentAdjacencies(Department dept)
        {
            switch (dept.Type)
            {
                case DepartmentType.Emergency:
                    dept.Adjacencies.Add(new AdjacencyRequirement
                    {
                        TargetDepartment = "Imaging",
                        Relationship = "Adjacent",
                        MaxTravelTime = 3,
                        DirectAccess = true,
                        Rationale = "Rapid diagnostic imaging for trauma"
                    });
                    dept.Adjacencies.Add(new AdjacencyRequirement
                    {
                        TargetDepartment = "Surgery",
                        Relationship = "Near",
                        MaxTravelTime = 5,
                        DirectAccess = false,
                        Rationale = "Emergency surgical procedures"
                    });
                    break;

                case DepartmentType.Surgery:
                    dept.Adjacencies.Add(new AdjacencyRequirement
                    {
                        TargetDepartment = "ICU",
                        Relationship = "Adjacent",
                        MaxTravelTime = 2,
                        DirectAccess = true,
                        Rationale = "Post-operative critical care"
                    });
                    break;

                case DepartmentType.ICU:
                    dept.Adjacencies.Add(new AdjacencyRequirement
                    {
                        TargetDepartment = "Pharmacy",
                        Relationship = "Near",
                        MaxTravelTime = 5,
                        DirectAccess = false,
                        Rationale = "Rapid medication delivery"
                    });
                    break;
            }
        }

        public ClinicalRoom AddRoom(string projectId, string departmentId, string roomNumber,
            string name, RoomType type, double area, double ceilingHeight)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var dept = project.Departments.FirstOrDefault(d => d.Id == departmentId);
                if (dept == null) return null;

                var room = new ClinicalRoom
                {
                    RoomNumber = roomNumber,
                    Name = name,
                    Type = type,
                    Area = area,
                    CeilingHeight = ceilingHeight
                };

                // Apply FGI ventilation requirements
                if (_fgiVentilation.TryGetValue(type, out var ventReq))
                {
                    room.Ventilation = ventReq;
                }

                // Apply medical gas requirements
                if (_gasRequirements.TryGetValue(type, out var gasReq))
                {
                    room.GasOutlets = new List<MedicalGasOutlet>(gasReq);
                }

                // Determine infection control zone
                room.ICZone = DetermineICZone(type);

                // Check handwashing requirement
                room.HandwashingStation = RequiresHandwashing(type);

                // Validate against FGI
                ValidateRoom(room);

                dept.RoomIds.Add(room.Id);
                project.Rooms.Add(room);
                return room;
            }
        }

        private InfectionControlZone DetermineICZone(RoomType type)
        {
            return type switch
            {
                RoomType.OperatingRoom => InfectionControlZone.Clean,
                RoomType.Isolation => InfectionControlZone.Isolation,
                RoomType.Clean => InfectionControlZone.Clean,
                RoomType.Soiled => InfectionControlZone.Soiled,
                _ => InfectionControlZone.General
            };
        }

        private bool RequiresHandwashing(RoomType type)
        {
            return type == RoomType.PatientRoom ||
                   type == RoomType.Exam ||
                   type == RoomType.Treatment ||
                   type == RoomType.Procedure ||
                   type == RoomType.OperatingRoom;
        }

        private void ValidateRoom(ClinicalRoom room)
        {
            room.Deficiencies.Clear();

            // Check minimum area
            if (_fgiMinAreas.TryGetValue(room.Type, out var minArea))
            {
                if (room.Area < minArea)
                {
                    room.Deficiencies.Add($"Area {room.Area} SF below FGI minimum {minArea} SF");
                }
            }

            // Check ceiling height
            double minHeight = room.Type == RoomType.OperatingRoom ? 10.0 : 9.0;
            if (room.CeilingHeight < minHeight)
            {
                room.Deficiencies.Add($"Ceiling height {room.CeilingHeight}' below minimum {minHeight}'");
            }

            // Check handwashing
            if (RequiresHandwashing(room.Type) && !room.HandwashingStation)
            {
                room.Deficiencies.Add("Handwashing station required per FGI");
            }

            room.MeetsFGI = room.Deficiencies.Count == 0;
        }

        public async Task<FGIComplianceReport> GenerateComplianceReport(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var report = new FGIComplianceReport
                    {
                        ProjectId = projectId,
                        TotalRooms = project.Rooms.Count,
                        CompliantRooms = project.Rooms.Count(r => r.MeetsFGI),
                        NonCompliantRooms = project.Rooms.Count(r => !r.MeetsFGI)
                    };

                    // Collect all issues
                    foreach (var room in project.Rooms.Where(r => !r.MeetsFGI))
                    {
                        foreach (var deficiency in room.Deficiencies)
                        {
                            report.Issues.Add(new ComplianceIssue
                            {
                                RoomId = room.Id,
                                RoomName = $"{room.RoomNumber} - {room.Name}",
                                Category = "Room Requirements",
                                Description = deficiency,
                                FGIReference = "FGI 2022"
                            });
                        }
                    }

                    // Check AII room ratio for hospitals
                    if (project.FacilityType == FacilityType.Hospital)
                    {
                        var aiirCount = project.Rooms.Count(r => r.Type == RoomType.Isolation);
                        var patientRoomCount = project.Rooms.Count(r => r.Type == RoomType.PatientRoom);
                        var ratio = patientRoomCount > 0 ? (double)aiirCount / patientRoomCount : 0;

                        if (ratio < 0.10)
                        {
                            report.Recommendations.Add(
                                $"Consider increasing AII rooms. Current ratio: {ratio:P1}, Recommended: 10% minimum");
                        }
                    }

                    // Check department adjacencies
                    foreach (var dept in project.Departments)
                    {
                        foreach (var adj in dept.Adjacencies.Where(a => a.DirectAccess))
                        {
                            report.Recommendations.Add(
                                $"Verify direct access between {dept.Name} and {adj.TargetDepartment}");
                        }
                    }

                    return report;
                }
            });
        }

        public MedicalGasZone CreateGasZone(string projectId, string name, MedicalGasType gasType)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var zone = new MedicalGasZone
                {
                    Name = name,
                    GasType = gasType,
                    HasAlarmPanel = true,
                    HasZoneValve = true
                };

                project.GasZones.Add(zone);
                return zone;
            }
        }

        public double CalculateGasDemand(string projectId, string zoneId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return 0;

                var zone = project.GasZones.FirstOrDefault(z => z.Id == zoneId);
                if (zone == null) return 0;

                double totalDemand = 0;

                foreach (var roomId in zone.RoomIds)
                {
                    var room = project.Rooms.FirstOrDefault(r => r.Id == roomId);
                    if (room == null) continue;

                    var outlets = room.GasOutlets.Where(o => o.GasType == zone.GasType);
                    foreach (var outlet in outlets)
                    {
                        // Standard flow rates in SCFM
                        double flowRate = zone.GasType switch
                        {
                            MedicalGasType.Oxygen => 10,
                            MedicalGasType.MedicalAir => 15,
                            MedicalGasType.Vacuum => 2.5,
                            MedicalGasType.Nitrogen => 20,
                            _ => 5
                        };
                        totalDemand += outlet.Quantity * flowRate;
                    }
                }

                // Apply diversity factor
                zone.TotalDemand = totalDemand;
                zone.DiversityFactor = zone.RoomIds.Count > 10 ? 0.6 : 0.8;
                zone.DesignFlow = totalDemand * zone.DiversityFactor;

                return zone.DesignFlow;
            }
        }

        public InfectionControlPlan CreateInfectionControlPlan(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var plan = new InfectionControlPlan();

                // Identify AII rooms
                foreach (var room in project.Rooms.Where(r => r.Type == RoomType.Isolation))
                {
                    plan.AIIRooms.Add(new AirborneIsolationRoom
                    {
                        RoomId = room.Id,
                        HasAnteroom = true,
                        HasPrivateBathroom = true,
                        MinACH = 12,
                        NegativePressure = true,
                        PressureMonitor = true,
                        SelfClosingDoor = true
                    });
                }

                // Calculate ratio
                var totalBeds = project.Rooms.Count(r => r.Type == RoomType.PatientRoom);
                plan.AIIRoomRatio = totalBeds > 0 ? (double)plan.AIIRooms.Count / totalBeds : 0;

                // Define traffic patterns
                plan.TrafficPatterns.Add("Clean supplies enter from clean corridor");
                plan.TrafficPatterns.Add("Soiled materials exit to soiled holding");
                plan.TrafficPatterns.Add("Staff and visitors use separate entries");
                plan.TrafficPatterns.Add("Patients transported via dedicated corridors");

                plan.MeetsCDCGuidelines = plan.AIIRoomRatio >= 0.10;

                project.InfectionControl = plan;
                return plan;
            }
        }

        public List<string> GetEquipmentPlanningList(RoomType roomType)
        {
            return roomType switch
            {
                RoomType.OperatingRoom => new List<string>
                {
                    "Operating table",
                    "Surgical lights",
                    "Anesthesia machine",
                    "Patient monitoring",
                    "Electrosurgical unit",
                    "Surgical boom system",
                    "Imaging (C-arm)",
                    "Instrument tables",
                    "Mayo stand",
                    "Warming cabinet"
                },
                RoomType.PatientRoom => new List<string>
                {
                    "Patient bed",
                    "Bedside cabinet",
                    "Overbed table",
                    "Patient lift",
                    "IV pole",
                    "Patient monitoring",
                    "Nurse call system",
                    "Television",
                    "Guest seating"
                },
                RoomType.Exam => new List<string>
                {
                    "Exam table",
                    "Physician stool",
                    "Guest seating",
                    "Scale",
                    "Diagnostic equipment",
                    "Computer workstation"
                },
                _ => new List<string>()
            };
        }
    }
}
