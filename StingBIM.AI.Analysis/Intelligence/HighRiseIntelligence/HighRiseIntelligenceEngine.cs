// ===================================================================
// StingBIM High-Rise Intelligence Engine
// Tall building systems, vertical transportation, stack effect, façade
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.HighRiseIntelligence
{
    #region Enums

    public enum BuildingClass { ClassA, ClassB, ClassC, Supertall, Megatall }
    public enum StructuralSystem { RigidFrame, BracedTube, BundledTube, Outrigger, CoreWall, Diagrid }
    public enum ElevatorType { Passenger, Service, Freight, Firefighter, Shuttle, Sky }
    public enum PumpingStrategy { Direct, ZonedTanks, VariableSpeed, Hydropneumatic }
    public enum StackEffectMitigation { Revolving, Vestibule, Pressurization, AirCurtain }
    public enum FireStrategy { Phased, AreaOfRefuge, ElevatorEvacuation, SkyLobby }

    #endregion

    #region Data Models

    public class HighRiseProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public BuildingClass Classification { get; set; }
        public double Height { get; set; }
        public int FloorCount { get; set; }
        public double FloorToFloor { get; set; }
        public double GrossArea { get; set; }
        public StructuralDesign Structure { get; set; }
        public VerticalTransportation Elevators { get; set; }
        public MechanicalZoning Mechanical { get; set; }
        public PlumbingZoning Plumbing { get; set; }
        public FireLifeSafety Fire { get; set; }
        public StackEffectAnalysis StackEffect { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class StructuralDesign
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public StructuralSystem PrimarySystem { get; set; }
        public StructuralSystem? SecondarySystem { get; set; }
        public double SlendernessRatio { get; set; }
        public double AspectRatio { get; set; }
        public double DriftLimit { get; set; }
        public double AccelerationLimit { get; set; }
        public double BaseMoment { get; set; }
        public double BaseShear { get; set; }
        public List<OutriggerLevel> Outriggers { get; set; } = new();
        public DampingSystem Damping { get; set; }
        public List<string> DesignConsiderations { get; set; } = new();
    }

    public class OutriggerLevel
    {
        public int FloorLevel { get; set; }
        public string Type { get; set; }
        public double MomentCapacity { get; set; }
        public bool HasBeltTruss { get; set; }
    }

    public class DampingSystem
    {
        public string Type { get; set; }
        public double MassRatio { get; set; }
        public double EffectiveDamping { get; set; }
        public string Location { get; set; }
        public double Cost { get; set; }
    }

    public class VerticalTransportation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<ElevatorBank> Banks { get; set; } = new();
        public List<SkyLobby> SkyLobbies { get; set; } = new();
        public int TotalElevators { get; set; }
        public double AverageWaitTime { get; set; }
        public double HandlingCapacity { get; set; }
        public double ShaftAreaRatio { get; set; }
        public List<string> DesignNotes { get; set; } = new();
    }

    public class ElevatorBank
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ElevatorType Type { get; set; }
        public int CarCount { get; set; }
        public int LowFloor { get; set; }
        public int HighFloor { get; set; }
        public int FloorsServed => HighFloor - LowFloor + 1;
        public double Speed { get; set; }
        public double Capacity { get; set; }
        public double RoundTripTime { get; set; }
        public double Interval { get; set; }
        public double HandlingCapacity { get; set; }
        public bool HasDestinationDispatch { get; set; }
        public bool HasRegenerative { get; set; }
    }

    public class SkyLobby
    {
        public int FloorLevel { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public List<string> ConnectingBanks { get; set; } = new();
        public bool HasAmenities { get; set; }
        public bool IsRefugeFloor { get; set; }
    }

    public class MechanicalZoning
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<MechanicalZone> Zones { get; set; } = new();
        public List<MechanicalFloor> MechFloors { get; set; } = new();
        public double TotalCoolingLoad { get; set; }
        public double TotalHeatingLoad { get; set; }
        public string PrimarySystem { get; set; }
        public double FanPressure { get; set; }
    }

    public class MechanicalZone
    {
        public string Name { get; set; }
        public int LowFloor { get; set; }
        public int HighFloor { get; set; }
        public double CoolingLoad { get; set; }
        public double HeatingLoad { get; set; }
        public string AHULocation { get; set; }
        public double SupplyCFM { get; set; }
        public double StaticPressure { get; set; }
    }

    public class MechanicalFloor
    {
        public int FloorLevel { get; set; }
        public double Area { get; set; }
        public double FloorToFloor { get; set; }
        public List<string> Equipment { get; set; } = new();
        public bool HasCoolingTowers { get; set; }
        public bool HasBoilers { get; set; }
    }

    public class PlumbingZoning
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public PumpingStrategy Strategy { get; set; }
        public List<PressureZone> Zones { get; set; } = new();
        public double MaxStaticPressure { get; set; }
        public List<string> TankLocations { get; set; } = new();
        public double TotalStorage { get; set; }
        public double DomesticDemand { get; set; }
    }

    public class PressureZone
    {
        public string Name { get; set; }
        public int LowFloor { get; set; }
        public int HighFloor { get; set; }
        public double StaticHead { get; set; }
        public double MaxPressure { get; set; }
        public string PumpLocation { get; set; }
        public bool HasPRVs { get; set; }
    }

    public class FireLifeSafety
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public FireStrategy EvacuationStrategy { get; set; }
        public List<RefugeFloor> RefugeFloors { get; set; } = new();
        public List<StairCore> Stairs { get; set; } = new();
        public SmokeControlSystem SmokeControl { get; set; }
        public int TotalEvacuationTime { get; set; }
        public double OccupantLoad { get; set; }
        public bool HasElevatorEvacuation { get; set; }
    }

    public class RefugeFloor
    {
        public int FloorLevel { get; set; }
        public double Area { get; set; }
        public int Capacity { get; set; }
        public bool HasDirectFirefighterAccess { get; set; }
        public bool HasCommunicationSystem { get; set; }
        public int HoldingTime { get; set; }
    }

    public class StairCore
    {
        public string Name { get; set; }
        public double Width { get; set; }
        public bool Scissor { get; set; }
        public bool Pressurized { get; set; }
        public double FlowCapacity { get; set; }
        public string DischargeLevel { get; set; }
    }

    public class SmokeControlSystem
    {
        public string Type { get; set; }
        public double StairPressure { get; set; }
        public double ElevatorPressure { get; set; }
        public double FloorExhaustCFM { get; set; }
        public bool HasZonedControl { get; set; }
        public int SmokeZoneCount { get; set; }
    }

    public class StackEffectAnalysis
    {
        public double NeutralPressurePlane { get; set; }
        public double MaxPressureDifferential { get; set; }
        public double WinterDeltaT { get; set; }
        public double SummerDeltaT { get; set; }
        public double InfiltrationRate { get; set; }
        public List<StackEffectMitigation> Mitigations { get; set; } = new();
        public double EnergyImpact { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class WindAnalysis
    {
        public double BasicWindSpeed { get; set; }
        public double DesignPressure { get; set; }
        public double PeakAcceleration { get; set; }
        public double ReturnPeriod { get; set; }
        public bool RequiresWindTunnelTest { get; set; }
        public List<string> Concerns { get; set; } = new();
    }

    #endregion

    public sealed class HighRiseIntelligenceEngine
    {
        private static readonly Lazy<HighRiseIntelligenceEngine> _instance =
            new Lazy<HighRiseIntelligenceEngine>(() => new HighRiseIntelligenceEngine());
        public static HighRiseIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, HighRiseProject> _projects = new();
        private readonly object _lock = new object();

        // Building classification by height
        private readonly Dictionary<BuildingClass, (double minHeight, double maxHeight)> _heightClasses = new()
        {
            [BuildingClass.ClassA] = (0, 150),
            [BuildingClass.ClassB] = (150, 300),
            [BuildingClass.ClassC] = (300, 400),
            [BuildingClass.Supertall] = (400, 600),
            [BuildingClass.Megatall] = (600, double.MaxValue)
        };

        // Elevator speed by height (fpm)
        private readonly Dictionary<BuildingClass, (int low, int shuttle, int express)> _elevatorSpeeds = new()
        {
            [BuildingClass.ClassA] = (500, 700, 1000),
            [BuildingClass.ClassB] = (700, 1000, 1400),
            [BuildingClass.ClassC] = (1000, 1400, 1800),
            [BuildingClass.Supertall] = (1200, 1800, 2500),
            [BuildingClass.Megatall] = (1400, 2000, 3000)
        };

        // Pressure zone limits (psi)
        private const double MaxPressure = 80;
        private const double MinPressure = 15;

        private HighRiseIntelligenceEngine() { }

        public HighRiseProject CreateHighRiseProject(string projectId, string projectName,
            double height, int floorCount, double grossArea)
        {
            var classification = _heightClasses
                .Where(kv => height >= kv.Value.minHeight && height < kv.Value.maxHeight)
                .Select(kv => kv.Key)
                .FirstOrDefault();

            var project = new HighRiseProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Height = height,
                FloorCount = floorCount,
                GrossArea = grossArea,
                FloorToFloor = height / floorCount,
                Classification = classification
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public StructuralDesign DesignStructuralSystem(string projectId, StructuralSystem primarySystem,
            double footprintWidth, double footprintLength)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var structure = new StructuralDesign
                {
                    PrimarySystem = primarySystem,
                    SlendernessRatio = project.Height / Math.Min(footprintWidth, footprintLength),
                    AspectRatio = project.Height / Math.Sqrt(footprintWidth * footprintLength),
                    DriftLimit = project.Height / 500,
                    AccelerationLimit = project.Classification >= BuildingClass.Supertall ? 15 : 20 // milli-g
                };

                // Determine if outriggers are needed
                if (structure.SlendernessRatio > 7)
                {
                    int outriggerCount = structure.SlendernessRatio > 10 ? 2 : 1;
                    for (int i = 0; i < outriggerCount; i++)
                    {
                        int level = (int)(project.FloorCount * (0.6 + i * 0.2));
                        structure.Outriggers.Add(new OutriggerLevel
                        {
                            FloorLevel = level,
                            Type = "Steel Belt Truss",
                            HasBeltTruss = true
                        });
                    }
                    structure.DesignConsiderations.Add("Outrigger system required for drift control");
                }

                // Check if damping is needed
                if (structure.SlendernessRatio > 8 || project.Classification >= BuildingClass.Supertall)
                {
                    structure.Damping = new DampingSystem
                    {
                        Type = "Tuned Mass Damper",
                        MassRatio = 0.01,
                        EffectiveDamping = 0.02,
                        Location = "Upper mechanical floor",
                        Cost = project.GrossArea * 2
                    };
                    structure.DesignConsiderations.Add("Supplemental damping recommended for occupant comfort");
                }

                // Add design considerations
                if (project.Classification >= BuildingClass.Supertall)
                {
                    structure.DesignConsiderations.Add("Wind tunnel testing required");
                    structure.DesignConsiderations.Add("Creep and shrinkage analysis required");
                    structure.DesignConsiderations.Add("Differential settlement monitoring recommended");
                }

                project.Structure = structure;
                return structure;
            }
        }

        public async Task<VerticalTransportation> DesignElevatorSystem(string projectId, double population)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var vt = new VerticalTransportation();
                    var speeds = _elevatorSpeeds.GetValueOrDefault(project.Classification, (low: 500, shuttle: 700, express: 1000));

                    // Determine zoning strategy
                    bool useSkyLobby = project.FloorCount > 40;
                    int zonesRequired = (int)Math.Ceiling(project.FloorCount / 25.0);

                    if (useSkyLobby && zonesRequired >= 2)
                    {
                        // Create sky lobby
                        int skyLobbyFloor = project.FloorCount / 2;
                        vt.SkyLobbies.Add(new SkyLobby
                        {
                            FloorLevel = skyLobbyFloor,
                            Name = "Sky Lobby",
                            Area = 5000,
                            HasAmenities = true,
                            IsRefugeFloor = true
                        });

                        // Shuttle elevators to sky lobby
                        vt.Banks.Add(new ElevatorBank
                        {
                            Name = "Shuttle",
                            Type = ElevatorType.Shuttle,
                            CarCount = CalculateCarCount(population, 300),
                            LowFloor = 1,
                            HighFloor = skyLobbyFloor,
                            Speed = speeds.shuttle,
                            Capacity = 4000,
                            HasDestinationDispatch = true,
                            HasRegenerative = true
                        });

                        // Low-rise zone
                        vt.Banks.Add(new ElevatorBank
                        {
                            Name = "Low Rise",
                            Type = ElevatorType.Passenger,
                            CarCount = CalculateCarCount(population * 0.5, 200),
                            LowFloor = 1,
                            HighFloor = skyLobbyFloor - 1,
                            Speed = speeds.low,
                            Capacity = 3500,
                            HasDestinationDispatch = true
                        });

                        // High-rise zone
                        vt.Banks.Add(new ElevatorBank
                        {
                            Name = "High Rise",
                            Type = ElevatorType.Passenger,
                            CarCount = CalculateCarCount(population * 0.5, 200),
                            LowFloor = skyLobbyFloor,
                            HighFloor = project.FloorCount,
                            Speed = speeds.express,
                            Capacity = 3500,
                            HasDestinationDispatch = true
                        });
                    }
                    else
                    {
                        // Single zone
                        vt.Banks.Add(new ElevatorBank
                        {
                            Name = "Main",
                            Type = ElevatorType.Passenger,
                            CarCount = CalculateCarCount(population, 200),
                            LowFloor = 1,
                            HighFloor = project.FloorCount,
                            Speed = speeds.low,
                            Capacity = 3500,
                            HasDestinationDispatch = true
                        });
                    }

                    // Add service elevator
                    vt.Banks.Add(new ElevatorBank
                    {
                        Name = "Service",
                        Type = ElevatorType.Service,
                        CarCount = Math.Max(2, project.FloorCount / 20),
                        LowFloor = 0, // Basement
                        HighFloor = project.FloorCount,
                        Speed = speeds.low,
                        Capacity = 5000
                    });

                    // Add firefighter elevator
                    vt.Banks.Add(new ElevatorBank
                    {
                        Name = "Firefighter",
                        Type = ElevatorType.Firefighter,
                        CarCount = project.FloorCount > 75 ? 2 : 1,
                        LowFloor = 0,
                        HighFloor = project.FloorCount,
                        Speed = speeds.express
                    });

                    // Calculate totals
                    vt.TotalElevators = vt.Banks.Sum(b => b.CarCount);

                    // Calculate shaft area ratio
                    double shaftArea = vt.TotalElevators * 80; // sq ft per elevator
                    vt.ShaftAreaRatio = shaftArea / (project.GrossArea / project.FloorCount);

                    // Calculate performance metrics
                    foreach (var bank in vt.Banks.Where(b => b.Type == ElevatorType.Passenger))
                    {
                        bank.RoundTripTime = CalculateRoundTripTime(bank, project.FloorToFloor);
                        bank.Interval = bank.RoundTripTime / bank.CarCount;
                        bank.HandlingCapacity = (bank.Capacity * 0.8 * 300 / bank.RoundTripTime);
                    }

                    vt.AverageWaitTime = vt.Banks
                        .Where(b => b.Type == ElevatorType.Passenger)
                        .Average(b => b.Interval / 2);

                    if (vt.AverageWaitTime > 30)
                    {
                        vt.DesignNotes.Add("Consider additional elevators to improve wait time");
                    }

                    if (vt.ShaftAreaRatio > 0.25)
                    {
                        vt.DesignNotes.Add("High shaft area ratio - consider double-deck elevators");
                    }

                    project.Elevators = vt;
                    return vt;
                }
            });
        }

        private int CalculateCarCount(double population, double handlingCapacity)
        {
            // 5-minute handling capacity
            double peakPopulation = population * 0.15; // 15% peak
            return Math.Max(2, (int)Math.Ceiling(peakPopulation / handlingCapacity));
        }

        private double CalculateRoundTripTime(ElevatorBank bank, double floorToFloor)
        {
            double travelTime = (bank.FloorsServed * floorToFloor / bank.Speed) * 60;
            double doorTime = bank.FloorsServed * 0.5 * 8; // 8 seconds per stop
            double loadTime = 10;
            return travelTime + doorTime + loadTime;
        }

        public PlumbingZoning DesignPlumbingZones(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var plumbing = new PlumbingZoning
                {
                    Strategy = project.FloorCount > 30 ? PumpingStrategy.ZonedTanks : PumpingStrategy.Direct,
                    MaxStaticPressure = MaxPressure
                };

                // Calculate zone heights based on pressure limits
                double maxZoneHeight = (MaxPressure - MinPressure) * 2.31; // feet
                int zoneCount = (int)Math.Ceiling(project.Height / maxZoneHeight);

                int floorsPerZone = (int)Math.Ceiling((double)project.FloorCount / zoneCount);

                for (int i = 0; i < zoneCount; i++)
                {
                    int lowFloor = i * floorsPerZone + 1;
                    int highFloor = Math.Min((i + 1) * floorsPerZone, project.FloorCount);

                    plumbing.Zones.Add(new PressureZone
                    {
                        Name = $"Zone {i + 1}",
                        LowFloor = lowFloor,
                        HighFloor = highFloor,
                        StaticHead = (highFloor - lowFloor) * project.FloorToFloor * 0.433,
                        MaxPressure = MaxPressure,
                        PumpLocation = i == 0 ? "Basement" : $"Mechanical Floor {lowFloor - 1}",
                        HasPRVs = true
                    });

                    if (plumbing.Strategy == PumpingStrategy.ZonedTanks)
                    {
                        plumbing.TankLocations.Add($"Floor {highFloor + 1}");
                    }
                }

                // Calculate storage requirements
                plumbing.DomesticDemand = project.GrossArea * 0.1; // GPD
                plumbing.TotalStorage = plumbing.DomesticDemand * 0.5; // 12 hours storage

                project.Plumbing = plumbing;
                return plumbing;
            }
        }

        public StackEffectAnalysis AnalyzeStackEffect(string projectId, double outsideWinterTemp, double insideTemp)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var stack = new StackEffectAnalysis
                {
                    WinterDeltaT = insideTemp - outsideWinterTemp,
                    SummerDeltaT = 15 // Typical summer delta
                };

                // Calculate neutral pressure plane (typically at 40-60% of height)
                stack.NeutralPressurePlane = project.Height * 0.5;

                // Calculate max pressure differential (Pa)
                // ΔP = 0.04 × h × (1/To - 1/Ti) × 3460
                double toKelvin = outsideWinterTemp + 273.15;
                double tiKelvin = insideTemp + 273.15;
                stack.MaxPressureDifferential = 0.04 * project.Height * Math.Abs(1 / toKelvin - 1 / tiKelvin) * 3460;

                // Determine mitigation strategies
                if (stack.MaxPressureDifferential > 25)
                {
                    stack.Mitigations.Add(StackEffectMitigation.Revolving);
                    stack.Mitigations.Add(StackEffectMitigation.Vestibule);
                }

                if (project.Height > 300)
                {
                    stack.Mitigations.Add(StackEffectMitigation.Pressurization);
                    stack.Recommendations.Add("Consider lobby compartmentalization");
                }

                if (stack.MaxPressureDifferential > 50)
                {
                    stack.Mitigations.Add(StackEffectMitigation.AirCurtain);
                    stack.Recommendations.Add("Seal elevator shaft at lobby level");
                    stack.Recommendations.Add("Consider sky lobby to interrupt stack effect");
                }

                // Estimate energy impact
                stack.InfiltrationRate = stack.MaxPressureDifferential * 0.1; // CFM per unit area
                stack.EnergyImpact = stack.InfiltrationRate * project.GrossArea * 0.001 * 8760 * 0.03; // $ per year

                stack.Recommendations.Add("Install pressure monitors at ground floor entries");
                stack.Recommendations.Add("Weatherstrip elevator doors at lobby");

                project.StackEffect = stack;
                return stack;
            }
        }

        public FireLifeSafety DesignFireSafety(string projectId, double occupantLoad)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var fire = new FireLifeSafety
                {
                    OccupantLoad = occupantLoad,
                    HasElevatorEvacuation = project.FloorCount > 75
                };

                // Determine evacuation strategy
                fire.EvacuationStrategy = project.FloorCount switch
                {
                    > 75 => FireStrategy.ElevatorEvacuation,
                    > 50 => FireStrategy.SkyLobby,
                    > 30 => FireStrategy.AreaOfRefuge,
                    _ => FireStrategy.Phased
                };

                // Add refuge floors (every 15-20 floors for tall buildings)
                if (project.FloorCount > 30)
                {
                    int interval = project.FloorCount > 50 ? 15 : 20;
                    for (int floor = interval; floor < project.FloorCount; floor += interval)
                    {
                        fire.RefugeFloors.Add(new RefugeFloor
                        {
                            FloorLevel = floor,
                            Area = 500 + (occupantLoad / project.FloorCount * interval * 3),
                            Capacity = (int)(occupantLoad / project.FloorCount * interval),
                            HasDirectFirefighterAccess = true,
                            HasCommunicationSystem = true,
                            HoldingTime = 30
                        });
                    }
                }

                // Design stairs
                int stairCount = project.GrossArea / project.FloorCount > 20000 ? 3 : 2;
                double stairWidth = 44 + (project.FloorCount > 50 ? 12 : 0); // inches

                for (int i = 0; i < stairCount; i++)
                {
                    fire.Stairs.Add(new StairCore
                    {
                        Name = $"Stair {(char)('A' + i)}",
                        Width = stairWidth,
                        Scissor = stairCount == 2 && project.FloorCount > 40,
                        Pressurized = true,
                        FlowCapacity = stairWidth * 0.3 * 60, // persons per minute
                        DischargeLevel = "Grade Level"
                    });
                }

                // Smoke control system
                fire.SmokeControl = new SmokeControlSystem
                {
                    Type = project.FloorCount > 50 ? "Zoned Smoke Control" : "Stair Pressurization",
                    StairPressure = 0.10, // inches WC
                    ElevatorPressure = 0.25,
                    FloorExhaustCFM = project.GrossArea / project.FloorCount * 4,
                    HasZonedControl = project.FloorCount > 50,
                    SmokeZoneCount = (int)Math.Ceiling(project.FloorCount / 10.0)
                };

                // Calculate evacuation time
                double totalEgressCapacity = fire.Stairs.Sum(s => s.FlowCapacity);
                fire.TotalEvacuationTime = (int)(occupantLoad / totalEgressCapacity);

                project.Fire = fire;
                return fire;
            }
        }
    }
}
