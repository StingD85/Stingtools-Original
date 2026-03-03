// ===================================================================
// StingBIM Fire Life Safety Intelligence Engine
// Egress analysis, fire engineering, smoke modeling
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.FireLifeSafetyIntelligence
{
    #region Enums

    public enum FireCode { NFPA101, NFPA1, IFC2021, IBC_Chapter10 }
    public enum EvacuationType { Simultaneous, Phased, DefendInPlace, Relocation }
    public enum ExitType { Door, Stair, Ramp, HorizontalExit, ExitPassageway }
    public enum FireRating { None, OneHour, TwoHour, ThreeHour, FourHour }
    public enum SprinklerType { None, NFPA13, NFPA13R, NFPA13D }

    #endregion

    #region Data Models

    public class FireSafetyProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public FireCode ApplicableCode { get; set; } = FireCode.NFPA101;
        public SprinklerType SprinklerSystem { get; set; }
        public List<EgressFloor> Floors { get; set; } = new();
        public List<FireBarrier> Barriers { get; set; } = new();
        public List<ExitComponent> Exits { get; set; } = new();
        public EvacuationAnalysis Evacuation { get; set; }
        public FireSafetySummary Summary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class EgressFloor
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FloorName { get; set; }
        public int FloorLevel { get; set; }
        public double TotalArea { get; set; }
        public List<EgressZone> Zones { get; set; } = new();
        public int TotalOccupancy { get; set; }
        public int RequiredExits { get; set; }
        public int ProvidedExits { get; set; }
        public double RequiredExitWidth { get; set; }
        public double ProvidedExitWidth { get; set; }
    }

    public class EgressZone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string OccupancyType { get; set; }
        public double Area { get; set; }
        public double OccupantLoadFactor { get; set; }
        public int CalculatedOccupancy { get; set; }
        public double MaxTravelDistance { get; set; }
        public double ActualTravelDistance { get; set; }
        public double MaxCommonPath { get; set; }
        public double ActualCommonPath { get; set; }
        public double MaxDeadEnd { get; set; }
        public double ActualDeadEnd { get; set; }
        public bool IsCompliant { get; set; }
    }

    public class ExitComponent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ExitType Type { get; set; }
        public string Location { get; set; }
        public double ClearWidth { get; set; }
        public int Capacity { get; set; }
        public FireRating Rating { get; set; }
        public bool HasPanicHardware { get; set; }
        public bool IsIlluminated { get; set; }
        public bool HasExitSign { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    public class FireBarrier
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; }
        public FireRating RequiredRating { get; set; }
        public FireRating ProvidedRating { get; set; }
        public double Length { get; set; }
        public List<Penetration> Penetrations { get; set; } = new();
        public bool IsCompliant { get; set; }
    }

    public class Penetration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public string Size { get; set; }
        public string FirestopSystem { get; set; }
        public string ULListingNumber { get; set; }
        public bool IsCompliant { get; set; }
    }

    public class EvacuationAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public EvacuationType Strategy { get; set; }
        public int TotalOccupants { get; set; }
        public double EstimatedEvacuationTime { get; set; }
        public double RequiredSafeEgressTime { get; set; }
        public double AvailableSafeEgressTime { get; set; }
        public double SafetyMargin => AvailableSafeEgressTime - RequiredSafeEgressTime;
        public List<EvacuationBottleneck> Bottlenecks { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class EvacuationBottleneck
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public double FlowRate { get; set; }
        public double Delay { get; set; }
        public string Mitigation { get; set; }
    }

    public class FireSafetySummary
    {
        public int TotalFloors { get; set; }
        public int TotalOccupancy { get; set; }
        public int TotalExits { get; set; }
        public int CompliantZones { get; set; }
        public int NonCompliantZones { get; set; }
        public List<string> CriticalIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public double OverallComplianceRate { get; set; }
    }

    public class SmokeControlAnalysis
    {
        public string ZoneName { get; set; }
        public double ZoneVolume { get; set; }
        public double SmokeProductionRate { get; set; }
        public double ExhaustRate { get; set; }
        public double MakeupAirRate { get; set; }
        public double SmokeLayerHeight { get; set; }
        public double TimeToUnsafeConditions { get; set; }
        public bool MeetsTenabilityRequirements { get; set; }
    }

    #endregion

    public sealed class FireLifeSafetyIntelligenceEngine
    {
        private static readonly Lazy<FireLifeSafetyIntelligenceEngine> _instance =
            new Lazy<FireLifeSafetyIntelligenceEngine>(() => new FireLifeSafetyIntelligenceEngine());
        public static FireLifeSafetyIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, FireSafetyProject> _projects = new();
        private readonly object _lock = new object();

        // NFPA 101 Table 7.3.1.2 Occupant Load Factors
        private readonly Dictionary<string, double> _occupantLoadFactors = new()
        {
            ["Assembly - Standing"] = 5,
            ["Assembly - Concentrated"] = 7,
            ["Assembly - Unconcentrated"] = 15,
            ["Assembly - Dining"] = 15,
            ["Business"] = 150,
            ["Educational - Classroom"] = 20,
            ["Industrial"] = 100,
            ["Mercantile - Street Floor"] = 30,
            ["Mercantile - Other Floors"] = 60,
            ["Residential"] = 200,
            ["Storage"] = 500
        };

        private FireLifeSafetyIntelligenceEngine() { }

        public FireSafetyProject CreateFireSafetyProject(string projectId, string projectName, SprinklerType sprinklerType)
        {
            var project = new FireSafetyProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                SprinklerSystem = sprinklerType
            };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public EgressFloor AddFloor(string projectId, string floorName, int floorLevel, double totalArea)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var floor = new EgressFloor
                {
                    FloorName = floorName,
                    FloorLevel = floorLevel,
                    TotalArea = totalArea
                };

                project.Floors.Add(floor);
                return floor;
            }
        }

        public EgressZone AddEgressZone(string projectId, string floorId, string name, string occupancyType, double area)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var floor = project.Floors.FirstOrDefault(f => f.Id == floorId);
                if (floor == null) return null;

                var loadFactor = _occupantLoadFactors.GetValueOrDefault(occupancyType, 100);
                var occupancy = (int)Math.Ceiling(area / loadFactor);

                var zone = new EgressZone
                {
                    Name = name,
                    OccupancyType = occupancyType,
                    Area = area,
                    OccupantLoadFactor = loadFactor,
                    CalculatedOccupancy = occupancy
                };

                // Set travel distance limits based on sprinklered status
                var isSprinklered = project.SprinklerSystem != SprinklerType.None;
                zone.MaxTravelDistance = GetMaxTravelDistance(occupancyType, isSprinklered);
                zone.MaxCommonPath = GetMaxCommonPath(occupancyType, isSprinklered);
                zone.MaxDeadEnd = GetMaxDeadEnd(occupancyType, isSprinklered);

                floor.Zones.Add(zone);
                floor.TotalOccupancy = floor.Zones.Sum(z => z.CalculatedOccupancy);
                CalculateExitRequirements(floor);

                return zone;
            }
        }

        private double GetMaxTravelDistance(string occupancyType, bool sprinklered)
        {
            var baseDistance = occupancyType switch
            {
                var o when o.StartsWith("Assembly") => 200,
                "Business" => 200,
                var o when o.StartsWith("Educational") => 200,
                "Industrial" => 200,
                var o when o.StartsWith("Mercantile") => 200,
                "Residential" => 200,
                _ => 200
            };

            return sprinklered ? baseDistance + 50 : baseDistance;
        }

        private double GetMaxCommonPath(string occupancyType, bool sprinklered)
        {
            return sprinklered ? 100 : 75;
        }

        private double GetMaxDeadEnd(string occupancyType, bool sprinklered)
        {
            return sprinklered ? 50 : 20;
        }

        private void CalculateExitRequirements(EgressFloor floor)
        {
            // Number of exits required
            floor.RequiredExits = floor.TotalOccupancy switch
            {
                <= 500 => floor.TotalOccupancy <= 49 ? 1 : 2,
                <= 1000 => 3,
                _ => 4
            };

            // Exit width required (0.2" per person for stairs, 0.15" for doors)
            floor.RequiredExitWidth = floor.TotalOccupancy * 0.2;
        }

        public ExitComponent AddExit(string projectId, string name, ExitType type, string location,
            double clearWidth, FireRating rating)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var exit = new ExitComponent
                {
                    Name = name,
                    Type = type,
                    Location = location,
                    ClearWidth = clearWidth,
                    Rating = rating,
                    Capacity = (int)(clearWidth / 0.2) // persons
                };

                // Check requirements
                if (clearWidth < 32)
                    exit.Issues.Add("Clear width less than 32\" minimum");
                if (type == ExitType.Stair && rating < FireRating.TwoHour)
                    exit.Issues.Add("Exit stair may require 2-hour rating");

                project.Exits.Add(exit);
                return exit;
            }
        }

        public async Task<EvacuationAnalysis> AnalyzeEvacuation(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var totalOccupants = project.Floors.Sum(f => f.TotalOccupancy);
                    var totalExitCapacity = project.Exits.Sum(e => e.Capacity);

                    var analysis = new EvacuationAnalysis
                    {
                        Strategy = totalOccupants > 500 ? EvacuationType.Phased : EvacuationType.Simultaneous,
                        TotalOccupants = totalOccupants
                    };

                    // Calculate evacuation time (simplified model)
                    var flowRate = 60; // persons per minute per unit width
                    var totalWidth = project.Exits.Sum(e => e.ClearWidth);
                    var unitsOfWidth = totalWidth / 22; // 22" per unit

                    analysis.EstimatedEvacuationTime = totalOccupants / (flowRate * unitsOfWidth);
                    analysis.RequiredSafeEgressTime = analysis.EstimatedEvacuationTime * 1.5; // safety factor
                    analysis.AvailableSafeEgressTime = project.SprinklerSystem != SprinklerType.None ? 15 : 8; // minutes

                    // Identify bottlenecks
                    var exits = project.Exits.OrderBy(e => e.Capacity).ToList();
                    if (exits.Any() && exits.First().Capacity < totalOccupants * 0.2)
                    {
                        analysis.Bottlenecks.Add(new EvacuationBottleneck
                        {
                            Location = exits.First().Location,
                            Type = "Undersized Exit",
                            FlowRate = exits.First().Capacity,
                            Delay = (totalOccupants * 0.2 - exits.First().Capacity) / flowRate,
                            Mitigation = "Increase exit width or add additional exit"
                        });
                    }

                    if (analysis.SafetyMargin < 0)
                    {
                        analysis.Recommendations.Add("Evacuation time exceeds available safe egress time");
                        analysis.Recommendations.Add("Consider additional exits or wider exit components");
                    }

                    project.Evacuation = analysis;
                    return analysis;
                }
            });
        }

        public FireBarrier AddFireBarrier(string projectId, string name, string type, FireRating requiredRating, double length)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var barrier = new FireBarrier
                {
                    Name = name,
                    Type = type,
                    RequiredRating = requiredRating,
                    Length = length,
                    IsCompliant = true
                };

                project.Barriers.Add(barrier);
                return barrier;
            }
        }

        public SmokeControlAnalysis AnalyzeSmokeControl(double zoneVolume, double fireSize, double ceilingHeight)
        {
            // Simplified smoke analysis based on NFPA 92
            var smokeProductionRate = Math.Pow(fireSize, 0.33) * 0.071 * Math.Pow(ceilingHeight, 1.67);

            var analysis = new SmokeControlAnalysis
            {
                ZoneVolume = zoneVolume,
                SmokeProductionRate = smokeProductionRate,
                ExhaustRate = smokeProductionRate * 1.1, // 10% safety factor
                MakeupAirRate = smokeProductionRate * 0.85
            };

            // Calculate smoke layer descent time
            var smokeReservoir = zoneVolume * 0.2; // 20% of volume
            analysis.TimeToUnsafeConditions = smokeReservoir / smokeProductionRate;
            analysis.SmokeLayerHeight = ceilingHeight * 0.7; // maintain at 70% of height

            analysis.MeetsTenabilityRequirements = analysis.TimeToUnsafeConditions > 6; // 6 minutes minimum

            return analysis;
        }

        public async Task<FireSafetySummary> GenerateSummary(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var allZones = project.Floors.SelectMany(f => f.Zones).ToList();

                    project.Summary = new FireSafetySummary
                    {
                        TotalFloors = project.Floors.Count,
                        TotalOccupancy = project.Floors.Sum(f => f.TotalOccupancy),
                        TotalExits = project.Exits.Count,
                        CompliantZones = allZones.Count(z => z.IsCompliant),
                        NonCompliantZones = allZones.Count(z => !z.IsCompliant)
                    };

                    // Check for critical issues
                    foreach (var floor in project.Floors)
                    {
                        if (floor.ProvidedExits < floor.RequiredExits)
                        {
                            project.Summary.CriticalIssues.Add(
                                $"{floor.FloorName}: Requires {floor.RequiredExits} exits, only {floor.ProvidedExits} provided");
                        }
                    }

                    foreach (var exit in project.Exits.Where(e => e.Issues.Any()))
                    {
                        project.Summary.CriticalIssues.AddRange(exit.Issues.Select(i => $"{exit.Name}: {i}"));
                    }

                    project.Summary.OverallComplianceRate = allZones.Any() ?
                        allZones.Count(z => z.IsCompliant) * 100.0 / allZones.Count : 100;

                    return project.Summary;
                }
            });
        }
    }
}
