// ===================================================================
// StingBIM Lighting Design Intelligence Engine
// Illumination engineering, daylight autonomy, circadian design
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.LightingDesignIntelligence
{
    #region Enums

    public enum SpaceCategory { Office, Retail, Healthcare, Educational, Industrial, Residential, Hospitality }
    public enum LightSourceType { LED, Fluorescent, HID, Incandescent, Daylight }
    public enum ControlStrategy { Manual, Occupancy, Daylight, Scheduled, Tunable }
    public enum DaylightMetric { DaylightFactor, sDA, ASE, UDI }

    #endregion

    #region Data Models

    public class LightingProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<LightingZone> Zones { get; set; } = new();
        public List<Luminaire> Luminaires { get; set; } = new();
        public List<DaylightAnalysis> DaylightAnalyses { get; set; } = new();
        public LightingMetrics ProjectMetrics { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LightingZone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public SpaceCategory Category { get; set; }
        public double Area { get; set; }
        public double CeilingHeight { get; set; }
        public double TargetIlluminance { get; set; }
        public double TargetUniformity { get; set; }
        public double CalculatedIlluminance { get; set; }
        public double CalculatedUniformity { get; set; }
        public double LPD { get; set; }
        public double AllowableLPD { get; set; }
        public List<string> LuminaireIds { get; set; } = new();
        public ControlStrategy Controls { get; set; }
        public bool MeetsCode { get; set; }
    }

    public class Luminaire
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Tag { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public LightSourceType SourceType { get; set; }
        public double Wattage { get; set; }
        public double Lumens { get; set; }
        public double Efficacy => Wattage > 0 ? Lumens / Wattage : 0;
        public double CCT { get; set; }
        public double CRI { get; set; }
        public string Distribution { get; set; }
        public double CoefficientOfUtilization { get; set; }
        public double Cost { get; set; }
        public int LifeHours { get; set; }
    }

    public class DaylightAnalysis
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public double GlazingArea { get; set; }
        public double FloorArea { get; set; }
        public double GlazingRatio => FloorArea > 0 ? GlazingArea / FloorArea : 0;
        public double DaylightFactor { get; set; }
        public double sDA { get; set; }
        public double ASE { get; set; }
        public double AnnualDaylightHours { get; set; }
        public bool MeetsLEEDDaylight { get; set; }
        public List<string> GlareIssues { get; set; } = new();
    }

    public class CircadianAnalysis
    {
        public string ZoneId { get; set; }
        public double MorningECS { get; set; }
        public double AfternoonECS { get; set; }
        public double MelanopicRatio { get; set; }
        public bool SupportsCircadianHealth { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class LightingMetrics
    {
        public double TotalWattage { get; set; }
        public double TotalArea { get; set; }
        public double AverageLPD => TotalArea > 0 ? TotalWattage / TotalArea : 0;
        public int TotalLuminaires { get; set; }
        public double AnnualEnergy { get; set; }
        public double EnergyCost { get; set; }
        public int ZonesMeetingCode { get; set; }
        public int ZonesNotMeetingCode { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class LumenMethod
    {
        public double RoomLength { get; set; }
        public double RoomWidth { get; set; }
        public double MountingHeight { get; set; }
        public double RoomCavityRatio { get; set; }
        public double TargetFootcandles { get; set; }
        public double CoefficientOfUtilization { get; set; }
        public double LightLossFactor { get; set; }
        public double TotalLumensRequired { get; set; }
        public int LuminairesRequired { get; set; }
    }

    public class PointByPointCalc
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Illuminance { get; set; }
        public double DirectComponent { get; set; }
        public double ReflectedComponent { get; set; }
    }

    #endregion

    public sealed class LightingDesignIntelligenceEngine
    {
        private static readonly Lazy<LightingDesignIntelligenceEngine> _instance =
            new Lazy<LightingDesignIntelligenceEngine>(() => new LightingDesignIntelligenceEngine());
        public static LightingDesignIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, LightingProject> _projects = new();
        private readonly object _lock = new object();

        // IES recommended illuminance levels (footcandles)
        private readonly Dictionary<SpaceCategory, double> _iesLevels = new()
        {
            [SpaceCategory.Office] = 50,
            [SpaceCategory.Retail] = 50,
            [SpaceCategory.Healthcare] = 50,
            [SpaceCategory.Educational] = 50,
            [SpaceCategory.Industrial] = 50,
            [SpaceCategory.Residential] = 30,
            [SpaceCategory.Hospitality] = 30
        };

        // ASHRAE 90.1-2019 LPD allowances (W/sf)
        private readonly Dictionary<SpaceCategory, double> _lpdAllowances = new()
        {
            [SpaceCategory.Office] = 0.79,
            [SpaceCategory.Retail] = 1.05,
            [SpaceCategory.Healthcare] = 0.89,
            [SpaceCategory.Educational] = 0.87,
            [SpaceCategory.Industrial] = 0.80,
            [SpaceCategory.Residential] = 0.60,
            [SpaceCategory.Hospitality] = 0.75
        };

        private LightingDesignIntelligenceEngine() { }

        public LightingProject CreateLightingProject(string projectId, string projectName)
        {
            var project = new LightingProject { ProjectId = projectId, ProjectName = projectName };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public LightingZone AddZone(string projectId, string name, SpaceCategory category, double area, double ceilingHeight)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var zone = new LightingZone
                {
                    Name = name,
                    Category = category,
                    Area = area,
                    CeilingHeight = ceilingHeight,
                    TargetIlluminance = _iesLevels.GetValueOrDefault(category, 50),
                    TargetUniformity = 0.7,
                    AllowableLPD = _lpdAllowances.GetValueOrDefault(category, 1.0)
                };

                project.Zones.Add(zone);
                return zone;
            }
        }

        public Luminaire AddLuminaire(string projectId, string tag, string manufacturer, string model,
            LightSourceType sourceType, double wattage, double lumens, double cct, double cri)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var luminaire = new Luminaire
                {
                    Tag = tag,
                    Manufacturer = manufacturer,
                    Model = model,
                    SourceType = sourceType,
                    Wattage = wattage,
                    Lumens = lumens,
                    CCT = cct,
                    CRI = cri,
                    CoefficientOfUtilization = sourceType == LightSourceType.LED ? 0.75 : 0.65
                };

                project.Luminaires.Add(luminaire);
                return luminaire;
            }
        }

        public LumenMethod CalculateLumenMethod(string projectId, string zoneId, string luminaireId, int quantity)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var zone = project.Zones.FirstOrDefault(z => z.Id == zoneId);
                var luminaire = project.Luminaires.FirstOrDefault(l => l.Id == luminaireId);
                if (zone == null || luminaire == null) return null;

                var roomLength = Math.Sqrt(zone.Area * 1.5);
                var roomWidth = zone.Area / roomLength;
                var mountingHeight = zone.CeilingHeight - 2.5;

                var calc = new LumenMethod
                {
                    RoomLength = roomLength,
                    RoomWidth = roomWidth,
                    MountingHeight = mountingHeight,
                    TargetFootcandles = zone.TargetIlluminance,
                    LightLossFactor = 0.85
                };

                // Room Cavity Ratio
                calc.RoomCavityRatio = (5 * mountingHeight * (roomLength + roomWidth)) / (roomLength * roomWidth);
                calc.CoefficientOfUtilization = luminaire.CoefficientOfUtilization * (1 - calc.RoomCavityRatio * 0.05);

                // Total lumens required
                calc.TotalLumensRequired = (zone.TargetIlluminance * zone.Area) /
                    (calc.CoefficientOfUtilization * calc.LightLossFactor);

                calc.LuminairesRequired = (int)Math.Ceiling(calc.TotalLumensRequired / luminaire.Lumens);

                // Update zone
                zone.CalculatedIlluminance = (quantity * luminaire.Lumens * calc.CoefficientOfUtilization * calc.LightLossFactor) / zone.Area;
                zone.LPD = (quantity * luminaire.Wattage) / zone.Area;
                zone.MeetsCode = zone.LPD <= zone.AllowableLPD;

                for (int i = 0; i < quantity; i++)
                {
                    zone.LuminaireIds.Add(luminaireId);
                }

                return calc;
            }
        }

        public async Task<DaylightAnalysis> AnalyzeDaylight(string projectId, string zoneId, double glazingArea, double vlt)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var zone = project.Zones.FirstOrDefault(z => z.Id == zoneId);
                    if (zone == null) return null;

                    var analysis = new DaylightAnalysis
                    {
                        ZoneId = zoneId,
                        ZoneName = zone.Name,
                        GlazingArea = glazingArea,
                        FloorArea = zone.Area
                    };

                    // Simplified daylight factor calculation
                    analysis.DaylightFactor = (vlt * glazingArea * 0.5) / zone.Area * 100;

                    // Spatial Daylight Autonomy estimate
                    analysis.sDA = Math.Min(analysis.GlazingRatio * 400, 100);

                    // Annual Sunlight Exposure estimate  
                    analysis.ASE = analysis.GlazingRatio > 0.4 ? 15 : 8;

                    // LEED Daylight credit
                    analysis.MeetsLEEDDaylight = analysis.sDA >= 55 && analysis.ASE <= 10;

                    // Check for glare issues
                    if (analysis.GlazingRatio > 0.5)
                    {
                        analysis.GlareIssues.Add("High glazing ratio may cause glare issues");
                        analysis.GlareIssues.Add("Consider interior shading devices");
                    }

                    analysis.AnnualDaylightHours = analysis.sDA * 25; // Approximate annual hours

                    project.DaylightAnalyses.Add(analysis);
                    return analysis;
                }
            });
        }

        public CircadianAnalysis AnalyzeCircadian(string projectId, string zoneId, double melanopicLux)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var zone = project.Zones.FirstOrDefault(z => z.Id == zoneId);
                if (zone == null) return null;

                var analysis = new CircadianAnalysis
                {
                    ZoneId = zoneId,
                    MorningECS = melanopicLux / zone.CalculatedIlluminance,
                    AfternoonECS = melanopicLux * 0.8 / zone.CalculatedIlluminance
                };

                // WELL Building Standard targets
                analysis.SupportsCircadianHealth = analysis.MorningECS >= 0.9 && melanopicLux >= 200;

                if (!analysis.SupportsCircadianHealth)
                {
                    analysis.Recommendations.Add("Increase morning light exposure (200+ melanopic lux)");
                    analysis.Recommendations.Add("Use higher CCT (5000K+) sources in morning");
                    analysis.Recommendations.Add("Consider tunable white LED system");
                }

                return analysis;
            }
        }

        public void SetControlStrategy(string projectId, string zoneId, ControlStrategy strategy)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var zone = project.Zones.FirstOrDefault(z => z.Id == zoneId);
                if (zone != null)
                {
                    zone.Controls = strategy;
                }
            }
        }

        public async Task<LightingMetrics> CalculateMetrics(string projectId, double operatingHours, double energyRate)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var metrics = new LightingMetrics
                    {
                        TotalArea = project.Zones.Sum(z => z.Area),
                        TotalLuminaires = project.Zones.Sum(z => z.LuminaireIds.Count)
                    };

                    // Calculate total wattage
                    foreach (var zone in project.Zones)
                    {
                        foreach (var lumId in zone.LuminaireIds)
                        {
                            var lum = project.Luminaires.FirstOrDefault(l => l.Id == lumId);
                            if (lum != null)
                                metrics.TotalWattage += lum.Wattage;
                        }
                    }

                    // Control savings factors
                    var controlSavings = new Dictionary<ControlStrategy, double>
                    {
                        [ControlStrategy.Manual] = 0,
                        [ControlStrategy.Occupancy] = 0.20,
                        [ControlStrategy.Daylight] = 0.30,
                        [ControlStrategy.Scheduled] = 0.15,
                        [ControlStrategy.Tunable] = 0.35
                    };

                    var avgControlSavings = project.Zones.Any() ?
                        project.Zones.Average(z => controlSavings.GetValueOrDefault(z.Controls, 0)) : 0;

                    metrics.AnnualEnergy = metrics.TotalWattage * operatingHours * (1 - avgControlSavings) / 1000;
                    metrics.EnergyCost = metrics.AnnualEnergy * energyRate;

                    metrics.ZonesMeetingCode = project.Zones.Count(z => z.MeetsCode);
                    metrics.ZonesNotMeetingCode = project.Zones.Count(z => !z.MeetsCode);

                    if (metrics.AverageLPD > 0.9)
                    {
                        metrics.Recommendations.Add("Consider higher efficacy LED luminaires to reduce LPD");
                    }

                    if (project.Zones.Any(z => z.Controls == ControlStrategy.Manual))
                    {
                        metrics.Recommendations.Add("Add occupancy sensors for energy savings");
                    }

                    project.ProjectMetrics = metrics;
                    return metrics;
                }
            });
        }
    }
}
