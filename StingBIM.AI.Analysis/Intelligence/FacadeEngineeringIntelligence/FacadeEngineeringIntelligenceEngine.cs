// ===================================================================
// StingBIM Facade Engineering Intelligence Engine
// Building envelope analysis, thermal bridging, condensation risk
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.FacadeEngineeringIntelligence
{
    #region Enums

    public enum FacadeType { CurtainWall, WindowWall, Rainscreen, EIFS, Masonry, Precast, MetalPanel }
    public enum GlazingType { SingleClear, DoubleLoE, TripleLoE, Spandrel, Fritted }
    public enum PerformanceClass { AW, CW, LC, R, HC }
    public enum ThermalIssue { None, ThermalBridge, CondensationRisk, MoistureIntrusion }

    #endregion

    #region Data Models

    public class FacadeProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ClimateData Climate { get; set; }
        public List<FacadeAssembly> Assemblies { get; set; } = new();
        public List<ThermalAnalysis> ThermalAnalyses { get; set; } = new();
        public List<CondensationAnalysis> CondensationAnalyses { get; set; } = new();
        public FacadeSummary Summary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ClimateData
    {
        public string Location { get; set; }
        public string ClimateZone { get; set; }
        public double WinterDesignTemp { get; set; }
        public double SummerDesignTemp { get; set; }
        public double WinterDewPoint { get; set; }
        public double DesignWindSpeed { get; set; }
        public double AnnualRainfall { get; set; }
        public int HeatingDegreeDays { get; set; }
        public int CoolingDegreeDays { get; set; }
    }

    public class FacadeAssembly
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public FacadeType Type { get; set; }
        public double Area { get; set; }
        public double UValue { get; set; }
        public double SHGC { get; set; }
        public double VT { get; set; }
        public PerformanceClass PerformanceClass { get; set; }
        public List<FacadeLayer> Layers { get; set; } = new();
        public FacadePerformance Performance { get; set; }
        public List<ThermalIssue> Issues { get; set; } = new();
    }

    public class FacadeLayer
    {
        public int Order { get; set; }
        public string Material { get; set; }
        public double Thickness { get; set; }
        public double Conductivity { get; set; }
        public double RValue => Thickness > 0 && Conductivity > 0 ? Thickness / (Conductivity * 12) : 0;
        public double VaporPermeance { get; set; }
        public bool IsAirBarrier { get; set; }
        public bool IsVaporRetarder { get; set; }
    }

    public class FacadePerformance
    {
        public double AirInfiltration { get; set; }
        public double WaterPenetration { get; set; }
        public double StructuralDeflection { get; set; }
        public double AcousticSTC { get; set; }
        public double BlastResistance { get; set; }
        public bool MeetsASHRAE901 { get; set; }
        public List<string> TestStandards { get; set; } = new();
    }

    public class ThermalAnalysis
    {
        public string AssemblyId { get; set; }
        public double EffectiveUValue { get; set; }
        public double FramingFactor { get; set; }
        public List<ThermalBridge> ThermalBridges { get; set; } = new();
        public double HeatLoss { get; set; }
        public double HeatingLoad { get; set; }
        public double CoolingLoad { get; set; }
        public bool MeetsEnergyCode { get; set; }
    }

    public class ThermalBridge
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public double PsiValue { get; set; }
        public double Length { get; set; }
        public double AdditionalHeatLoss => PsiValue * Length;
        public double SurfaceTemperature { get; set; }
        public bool CondensationRisk { get; set; }
        public string Mitigation { get; set; }
    }

    public class CondensationAnalysis
    {
        public string AssemblyId { get; set; }
        public double InteriorTemp { get; set; }
        public double InteriorRH { get; set; }
        public double ExteriorTemp { get; set; }
        public double DewPoint { get; set; }
        public List<LayerCondition> LayerConditions { get; set; } = new();
        public bool HasCondensationRisk { get; set; }
        public string CriticalLocation { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class LayerCondition
    {
        public string LayerName { get; set; }
        public double Temperature { get; set; }
        public double DewPoint { get; set; }
        public double VaporPressure { get; set; }
        public bool CondensationOccurs => Temperature < DewPoint;
    }

    public class WindLoadAnalysis
    {
        public double BasicWindSpeed { get; set; }
        public double ExposureCategory { get; set; }
        public double Kz { get; set; }
        public double Kzt { get; set; }
        public double Kd { get; set; }
        public double DesignPressure { get; set; }
        public double CornerZonePressure { get; set; }
        public double GlassThicknessRequired { get; set; }
    }

    public class FacadeSummary
    {
        public double TotalArea { get; set; }
        public double AverageUValue { get; set; }
        public double WindowWallRatio { get; set; }
        public int ThermalBridgeCount { get; set; }
        public int CondensationRisks { get; set; }
        public double AnnualHeatLoss { get; set; }
        public List<string> CriticalIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    #endregion

    public sealed class FacadeEngineeringIntelligenceEngine
    {
        private static readonly Lazy<FacadeEngineeringIntelligenceEngine> _instance =
            new Lazy<FacadeEngineeringIntelligenceEngine>(() => new FacadeEngineeringIntelligenceEngine());
        public static FacadeEngineeringIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, FacadeProject> _projects = new();
        private readonly object _lock = new object();

        // ASHRAE 90.1-2019 Table 5.5 U-value requirements
        private readonly Dictionary<string, double> _uValueRequirements = new()
        {
            ["CZ1-Metal"] = 1.20, ["CZ2-Metal"] = 0.70, ["CZ3-Metal"] = 0.70,
            ["CZ4-Metal"] = 0.55, ["CZ5-Metal"] = 0.55, ["CZ6-Metal"] = 0.45,
            ["CZ7-Metal"] = 0.40, ["CZ8-Metal"] = 0.35,
            ["CZ1-NonMetal"] = 0.70, ["CZ2-NonMetal"] = 0.45, ["CZ3-NonMetal"] = 0.45,
            ["CZ4-NonMetal"] = 0.40, ["CZ5-NonMetal"] = 0.40, ["CZ6-NonMetal"] = 0.35,
            ["CZ7-NonMetal"] = 0.32, ["CZ8-NonMetal"] = 0.32
        };

        private FacadeEngineeringIntelligenceEngine() { }

        public FacadeProject CreateFacadeProject(string projectId, string projectName, string location, string climateZone)
        {
            var project = new FacadeProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Climate = new ClimateData { Location = location, ClimateZone = climateZone }
            };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public FacadeAssembly AddFacadeAssembly(string projectId, string name, FacadeType type, double area)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var assembly = new FacadeAssembly
                {
                    Name = name,
                    Type = type,
                    Area = area,
                    Performance = new FacadePerformance()
                };

                project.Assemblies.Add(assembly);
                return assembly;
            }
        }

        public FacadeLayer AddLayer(string projectId, string assemblyId, string material,
            double thickness, double conductivity, double vaporPermeance)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var assembly = project.Assemblies.FirstOrDefault(a => a.Id == assemblyId);
                if (assembly == null) return null;

                var layer = new FacadeLayer
                {
                    Order = assembly.Layers.Count + 1,
                    Material = material,
                    Thickness = thickness,
                    Conductivity = conductivity,
                    VaporPermeance = vaporPermeance,
                    IsAirBarrier = material.Contains("membrane") || material.Contains("barrier"),
                    IsVaporRetarder = vaporPermeance < 1.0
                };

                assembly.Layers.Add(layer);
                CalculateAssemblyUValue(assembly);

                return layer;
            }
        }

        private void CalculateAssemblyUValue(FacadeAssembly assembly)
        {
            var totalRValue = 0.68 + 0.17; // Interior + exterior air film
            totalRValue += assembly.Layers.Sum(l => l.RValue);
            assembly.UValue = 1 / totalRValue;
        }

        public async Task<ThermalAnalysis> AnalyzeThermalPerformance(string projectId, string assemblyId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var assembly = project.Assemblies.FirstOrDefault(a => a.Id == assemblyId);
                    if (assembly == null) return null;

                    var analysis = new ThermalAnalysis
                    {
                        AssemblyId = assemblyId,
                        EffectiveUValue = assembly.UValue,
                        FramingFactor = assembly.Type == FacadeType.CurtainWall ? 0.15 : 0.10
                    };

                    // Identify thermal bridges
                    IdentifyThermalBridges(assembly, analysis, project.Climate);

                    // Adjust effective U-value for thermal bridges
                    var bridgeImpact = analysis.ThermalBridges.Sum(tb => tb.AdditionalHeatLoss) / assembly.Area;
                    analysis.EffectiveUValue += bridgeImpact;

                    // Calculate loads
                    var deltaT = Math.Abs(project.Climate.WinterDesignTemp - 70);
                    analysis.HeatLoss = analysis.EffectiveUValue * assembly.Area * deltaT;
                    analysis.HeatingLoad = analysis.HeatLoss * project.Climate.HeatingDegreeDays * 24 / 1000000; // MMBtu

                    // Check code compliance
                    var climateZone = project.Climate.ClimateZone;
                    var frameType = assembly.Type == FacadeType.CurtainWall ? "Metal" : "NonMetal";
                    var key = $"{climateZone}-{frameType}";
                    if (_uValueRequirements.TryGetValue(key, out var maxU))
                    {
                        analysis.MeetsEnergyCode = analysis.EffectiveUValue <= maxU;
                    }

                    project.ThermalAnalyses.Add(analysis);
                    return analysis;
                }
            });
        }

        private void IdentifyThermalBridges(FacadeAssembly assembly, ThermalAnalysis analysis, ClimateData climate)
        {
            // Common thermal bridges
            var bridges = new List<ThermalBridge>();

            if (assembly.Type == FacadeType.CurtainWall)
            {
                bridges.Add(new ThermalBridge
                {
                    Location = "Mullion",
                    Type = "Linear",
                    PsiValue = 0.5,
                    Length = Math.Sqrt(assembly.Area) * 4,
                    Mitigation = "Use thermally broken mullions"
                });

                bridges.Add(new ThermalBridge
                {
                    Location = "Slab Edge",
                    Type = "Linear",
                    PsiValue = 0.8,
                    Length = Math.Sqrt(assembly.Area) * 2,
                    Mitigation = "Install slab edge insulation"
                });
            }

            // Calculate surface temperatures and condensation risk
            foreach (var bridge in bridges)
            {
                var temperatureRatio = 0.75; // Simplified
                bridge.SurfaceTemperature = 70 - (70 - climate.WinterDesignTemp) * (1 - temperatureRatio);
                bridge.CondensationRisk = bridge.SurfaceTemperature < climate.WinterDewPoint + 10;

                if (bridge.CondensationRisk)
                {
                    assembly.Issues.Add(ThermalIssue.ThermalBridge);
                }
            }

            analysis.ThermalBridges = bridges;
        }

        public async Task<CondensationAnalysis> AnalyzeCondensation(string projectId, string assemblyId,
            double interiorTemp, double interiorRH, double exteriorTemp)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var assembly = project.Assemblies.FirstOrDefault(a => a.Id == assemblyId);
                    if (assembly == null) return null;

                    var analysis = new CondensationAnalysis
                    {
                        AssemblyId = assemblyId,
                        InteriorTemp = interiorTemp,
                        InteriorRH = interiorRH,
                        ExteriorTemp = exteriorTemp,
                        DewPoint = CalculateDewPoint(interiorTemp, interiorRH)
                    };

                    // Calculate temperature profile through assembly
                    var totalR = assembly.Layers.Sum(l => l.RValue) + 0.85; // Include air films
                    var deltaT = interiorTemp - exteriorTemp;
                    var cumulativeR = 0.68; // Interior air film

                    foreach (var layer in assembly.Layers)
                    {
                        cumulativeR += layer.RValue;
                        var layerTemp = interiorTemp - (deltaT * cumulativeR / totalR);

                        analysis.LayerConditions.Add(new LayerCondition
                        {
                            LayerName = layer.Material,
                            Temperature = layerTemp,
                            DewPoint = analysis.DewPoint
                        });

                        if (layerTemp < analysis.DewPoint)
                        {
                            analysis.HasCondensationRisk = true;
                            analysis.CriticalLocation = layer.Material;
                        }
                    }

                    if (analysis.HasCondensationRisk)
                    {
                        assembly.Issues.Add(ThermalIssue.CondensationRisk);
                        analysis.Recommendations.Add("Add vapor retarder on warm side of insulation");
                        analysis.Recommendations.Add("Increase insulation outboard of dew point location");
                        analysis.Recommendations.Add("Consider ventilated rainscreen assembly");
                    }

                    project.CondensationAnalyses.Add(analysis);
                    return analysis;
                }
            });
        }

        private double CalculateDewPoint(double temp, double rh)
        {
            // Magnus formula approximation
            var a = 17.27;
            var b = 237.7;
            var alpha = (a * temp) / (b + temp) + Math.Log(rh / 100);
            return (b * alpha) / (a - alpha);
        }

        public WindLoadAnalysis CalculateWindLoad(double basicWindSpeed, string exposureCategory, double height, bool isCornerZone)
        {
            var analysis = new WindLoadAnalysis { BasicWindSpeed = basicWindSpeed };

            // ASCE 7-22 simplified
            analysis.Kz = Math.Pow(height / 33, 0.28);
            analysis.Kzt = 1.0;
            analysis.Kd = 0.85;

            var qz = 0.00256 * analysis.Kz * analysis.Kzt * analysis.Kd * Math.Pow(basicWindSpeed, 2);
            var GCp = isCornerZone ? -1.4 : -1.1;

            analysis.DesignPressure = qz * Math.Abs(GCp);
            analysis.CornerZonePressure = qz * 1.4;

            // Glass thickness selection (simplified)
            analysis.GlassThicknessRequired = analysis.DesignPressure switch
            {
                < 30 => 0.25,
                < 50 => 0.375,
                < 75 => 0.5,
                _ => 0.625
            };

            return analysis;
        }

        public async Task<FacadeSummary> GenerateSummary(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    project.Summary = new FacadeSummary
                    {
                        TotalArea = project.Assemblies.Sum(a => a.Area),
                        AverageUValue = project.Assemblies.Any() ?
                            project.Assemblies.Average(a => a.UValue) : 0,
                        ThermalBridgeCount = project.ThermalAnalyses.Sum(t => t.ThermalBridges.Count),
                        CondensationRisks = project.CondensationAnalyses.Count(c => c.HasCondensationRisk),
                        AnnualHeatLoss = project.ThermalAnalyses.Sum(t => t.HeatingLoad)
                    };

                    var glazingArea = project.Assemblies
                        .Where(a => a.Type == FacadeType.CurtainWall || a.Type == FacadeType.WindowWall)
                        .Sum(a => a.Area);
                    project.Summary.WindowWallRatio = project.Summary.TotalArea > 0 ?
                        glazingArea / project.Summary.TotalArea * 100 : 0;

                    if (project.Summary.WindowWallRatio > 40)
                    {
                        project.Summary.CriticalIssues.Add("Window-to-wall ratio exceeds 40% - energy code compliance path review required");
                    }

                    if (project.Summary.CondensationRisks > 0)
                    {
                        project.Summary.CriticalIssues.Add($"{project.Summary.CondensationRisks} assemblies have condensation risk");
                        project.Summary.Recommendations.Add("Review vapor barrier locations and thermal bridge details");
                    }

                    return project.Summary;
                }
            });
        }
    }
}
