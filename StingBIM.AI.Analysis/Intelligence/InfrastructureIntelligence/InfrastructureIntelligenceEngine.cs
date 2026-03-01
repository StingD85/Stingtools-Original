// ===================================================================
// StingBIM Infrastructure Intelligence Engine
// Civil/heavy construction, bridges, tunnels, utilities, earthwork
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.InfrastructureIntelligence
{
    #region Enums

    public enum InfrastructureType { Highway, Bridge, Tunnel, Utility, Airport, Rail, Port, Dam }
    public enum BridgeType { Beam, Girder, Arch, Suspension, CableStayed, Truss, Cantilever }
    public enum TunnelType { Cut_Cover, Bored_TBM, NATM, Immersed, Mined }
    public enum UtilityType { Water, Sewer, Storm, Gas, Electric, Telecom, Combined }
    public enum SoilClassification { ClassI, ClassII, ClassIII, ClassIV, ClassV }
    public enum PavementType { Asphalt, Concrete, Composite, Gravel }
    public enum TrafficClass { Rural, Urban, Highway, Interstate }

    #endregion

    #region Data Models

    public class InfrastructureProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public InfrastructureType PrimaryType { get; set; }
        public double ProjectLength { get; set; }
        public GeotechnicalData Geotechnical { get; set; }
        public List<LinearAlignment> Alignments { get; set; } = new();
        public List<Structure> Structures { get; set; } = new();
        public List<UtilityRun> Utilities { get; set; } = new();
        public EarthworkAnalysis Earthwork { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class GeotechnicalData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<Boring> Borings { get; set; } = new();
        public SoilClassification PredominantSoil { get; set; }
        public double WaterTableDepth { get; set; }
        public double BearingCapacity { get; set; }
        public bool HasBedrock { get; set; }
        public double BedrockDepth { get; set; }
        public List<string> Hazards { get; set; } = new();
    }

    public class Boring
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double Station { get; set; }
        public double Offset { get; set; }
        public double Depth { get; set; }
        public List<SoilLayer> Layers { get; set; } = new();
        public double WaterLevel { get; set; }
        public double SPT_N { get; set; }
    }

    public class SoilLayer
    {
        public double TopDepth { get; set; }
        public double BottomDepth { get; set; }
        public string Description { get; set; }
        public SoilClassification Classification { get; set; }
        public double UnitWeight { get; set; }
        public double Cohesion { get; set; }
        public double FrictionAngle { get; set; }
    }

    public class LinearAlignment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Length => EndStation - StartStation;
        public List<HorizontalElement> HorizontalGeometry { get; set; } = new();
        public List<VerticalElement> VerticalGeometry { get; set; } = new();
        public List<CrossSection> CrossSections { get; set; } = new();
        public double DesignSpeed { get; set; }
        public TrafficClass TrafficClass { get; set; }
    }

    public class HorizontalElement
    {
        public string Type { get; set; }
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Radius { get; set; }
        public double SpiralLength { get; set; }
        public double Superelevation { get; set; }
    }

    public class VerticalElement
    {
        public string Type { get; set; }
        public double Station { get; set; }
        public double Elevation { get; set; }
        public double Grade { get; set; }
        public double CurveLength { get; set; }
        public double K_Value { get; set; }
    }

    public class CrossSection
    {
        public double Station { get; set; }
        public double CenterlineElevation { get; set; }
        public double PavementWidth { get; set; }
        public double ShoulderWidth { get; set; }
        public double SlopeLeft { get; set; }
        public double SlopeRight { get; set; }
        public double CutDepth { get; set; }
        public double FillHeight { get; set; }
        public double CutArea { get; set; }
        public double FillArea { get; set; }
    }

    public class Structure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; }
        public double Station { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public BridgeDesign Bridge { get; set; }
        public TunnelDesign Tunnel { get; set; }
        public CulvertDesign Culvert { get; set; }
    }

    public class BridgeDesign
    {
        public BridgeType Type { get; set; }
        public int SpanCount { get; set; }
        public List<double> SpanLengths { get; set; } = new();
        public double TotalLength { get; set; }
        public double DeckWidth { get; set; }
        public double VerticalClearance { get; set; }
        public double DesignLoad { get; set; }
        public string DeckType { get; set; }
        public string SubstructureType { get; set; }
        public List<string> AbutmentTypes { get; set; } = new();
        public double EstimatedCost { get; set; }
    }

    public class TunnelDesign
    {
        public TunnelType Method { get; set; }
        public double Length { get; set; }
        public double InternalDiameter { get; set; }
        public double ExternalDiameter { get; set; }
        public double CoverDepth { get; set; }
        public string LiningType { get; set; }
        public double LiningThickness { get; set; }
        public bool HasWaterproofing { get; set; }
        public VentilationSystem Ventilation { get; set; }
        public double ExcavationVolume { get; set; }
        public double EstimatedCost { get; set; }
    }

    public class VentilationSystem
    {
        public string Type { get; set; }
        public double Capacity { get; set; }
        public int FanCount { get; set; }
        public List<string> EmergencyFeatures { get; set; } = new();
    }

    public class CulvertDesign
    {
        public string Type { get; set; }
        public double Span { get; set; }
        public double Rise { get; set; }
        public double Length { get; set; }
        public double DesignFlow { get; set; }
        public double HeadwaterDepth { get; set; }
        public double TailwaterDepth { get; set; }
        public string Material { get; set; }
        public double FillHeight { get; set; }
    }

    public class UtilityRun
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public UtilityType Type { get; set; }
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Length => EndStation - StartStation;
        public double Diameter { get; set; }
        public string Material { get; set; }
        public double BurialDepth { get; set; }
        public double Slope { get; set; }
        public List<UtilityStructure> Structures { get; set; } = new();
        public double DesignCapacity { get; set; }
        public double PeakFlow { get; set; }
    }

    public class UtilityStructure
    {
        public string Type { get; set; }
        public double Station { get; set; }
        public double InvertElevation { get; set; }
        public double RimElevation { get; set; }
        public double Diameter { get; set; }
    }

    public class EarthworkAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double TotalCutVolume { get; set; }
        public double TotalFillVolume { get; set; }
        public double NetVolume { get; set; }
        public double ShrinkageFactor { get; set; } = 1.15;
        public double SwellFactor { get; set; } = 1.25;
        public double AdjustedCutVolume { get; set; }
        public double ImportRequired { get; set; }
        public double ExportRequired { get; set; }
        public bool IsBalanced { get; set; }
        public List<MassHaulSegment> MassHaul { get; set; } = new();
        public double EstimatedCost { get; set; }
    }

    public class MassHaulSegment
    {
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Volume { get; set; }
        public double HaulDistance { get; set; }
        public string Direction { get; set; }
    }

    public class PavementDesign
    {
        public PavementType Type { get; set; }
        public double DesignLife { get; set; }
        public double ESAL { get; set; }
        public List<PavementLayer> Layers { get; set; } = new();
        public double TotalThickness { get; set; }
        public double StructuralNumber { get; set; }
        public double EstimatedCost { get; set; }
    }

    public class PavementLayer
    {
        public string Material { get; set; }
        public double Thickness { get; set; }
        public double LayerCoefficient { get; set; }
        public double DrainageCoefficient { get; set; }
    }

    public class DrainageDesign
    {
        public double CatchmentArea { get; set; }
        public double RunoffCoefficient { get; set; }
        public double RainfallIntensity { get; set; }
        public double DesignFlow { get; set; }
        public List<DrainageStructure> Structures { get; set; } = new();
        public double PipeSize { get; set; }
        public double Slope { get; set; }
    }

    public class DrainageStructure
    {
        public string Type { get; set; }
        public double Station { get; set; }
        public double Capacity { get; set; }
    }

    #endregion

    public sealed class InfrastructureIntelligenceEngine
    {
        private static readonly Lazy<InfrastructureIntelligenceEngine> _instance =
            new Lazy<InfrastructureIntelligenceEngine>(() => new InfrastructureIntelligenceEngine());
        public static InfrastructureIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, InfrastructureProject> _projects = new();
        private readonly object _lock = new object();

        // AASHTO Design Speed/Curve Radius requirements (mph / ft)
        private readonly Dictionary<int, int> _minRadiusBySpeed = new()
        {
            [25] = 167,
            [30] = 273,
            [35] = 371,
            [40] = 485,
            [45] = 643,
            [50] = 833,
            [55] = 1060,
            [60] = 1330,
            [65] = 1630,
            [70] = 1970
        };

        // Bridge type selection by span
        private readonly Dictionary<BridgeType, (int minSpan, int maxSpan)> _bridgeSpanRanges = new()
        {
            [BridgeType.Beam] = (20, 100),
            [BridgeType.Girder] = (50, 200),
            [BridgeType.Arch] = (100, 800),
            [BridgeType.Truss] = (150, 600),
            [BridgeType.CableStayed] = (300, 1500),
            [BridgeType.Suspension] = (800, 7000)
        };

        // Unit costs ($/CY or $/LF)
        private readonly Dictionary<string, double> _unitCosts = new()
        {
            ["Cut"] = 8,
            ["Fill"] = 12,
            ["Import"] = 25,
            ["Export"] = 20,
            ["Asphalt"] = 150,
            ["Concrete_Pavement"] = 200,
            ["Pipe_Concrete"] = 85,
            ["Pipe_HDPE"] = 45
        };

        private InfrastructureIntelligenceEngine() { }

        public InfrastructureProject CreateInfrastructureProject(string projectId, string projectName,
            InfrastructureType primaryType, double projectLength)
        {
            var project = new InfrastructureProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                PrimaryType = primaryType,
                ProjectLength = projectLength
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public LinearAlignment CreateAlignment(string projectId, string name, double startStation,
            double endStation, double designSpeed, TrafficClass trafficClass)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var alignment = new LinearAlignment
                {
                    Name = name,
                    StartStation = startStation,
                    EndStation = endStation,
                    DesignSpeed = designSpeed,
                    TrafficClass = trafficClass
                };

                project.Alignments.Add(alignment);
                return alignment;
            }
        }

        public void AddHorizontalCurve(string projectId, string alignmentId, double startStation,
            double endStation, double radius, double spiralLength = 0)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var alignment = project.Alignments.FirstOrDefault(a => a.Id == alignmentId);
                if (alignment == null) return;

                // Calculate superelevation based on radius and speed
                double superelevation = Math.Min(0.08, alignment.DesignSpeed * alignment.DesignSpeed / (15 * radius));

                alignment.HorizontalGeometry.Add(new HorizontalElement
                {
                    Type = spiralLength > 0 ? "Spiral-Curve-Spiral" : "Circular",
                    StartStation = startStation,
                    EndStation = endStation,
                    Radius = radius,
                    SpiralLength = spiralLength,
                    Superelevation = superelevation
                });
            }
        }

        public void AddVerticalCurve(string projectId, string alignmentId, double station,
            double elevation, double gradeIn, double gradeOut, double curveLength)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var alignment = project.Alignments.FirstOrDefault(a => a.Id == alignmentId);
                if (alignment == null) return;

                double A = Math.Abs(gradeOut - gradeIn);
                double K = curveLength / A;

                alignment.VerticalGeometry.Add(new VerticalElement
                {
                    Type = gradeOut > gradeIn ? "Sag" : "Crest",
                    Station = station,
                    Elevation = elevation,
                    Grade = gradeIn,
                    CurveLength = curveLength,
                    K_Value = K
                });
            }
        }

        public BridgeDesign DesignBridge(string projectId, string name, double station,
            double totalLength, double deckWidth, double verticalClearance)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var bridge = new BridgeDesign
                {
                    TotalLength = totalLength,
                    DeckWidth = deckWidth,
                    VerticalClearance = verticalClearance,
                    DesignLoad = 80 // HL-93
                };

                // Select bridge type based on span
                bridge.Type = _bridgeSpanRanges
                    .Where(kv => totalLength >= kv.Value.minSpan && totalLength <= kv.Value.maxSpan)
                    .Select(kv => kv.Key)
                    .FirstOrDefault();

                // Calculate span configuration
                if (totalLength <= 120)
                {
                    bridge.SpanCount = 1;
                    bridge.SpanLengths.Add(totalLength);
                }
                else if (totalLength <= 300)
                {
                    bridge.SpanCount = 3;
                    double endSpan = totalLength * 0.3;
                    double centerSpan = totalLength * 0.4;
                    bridge.SpanLengths.AddRange(new[] { endSpan, centerSpan, endSpan });
                }
                else
                {
                    double optimalSpan = 150;
                    bridge.SpanCount = (int)Math.Ceiling(totalLength / optimalSpan);
                    double actualSpan = totalLength / bridge.SpanCount;
                    for (int i = 0; i < bridge.SpanCount; i++)
                        bridge.SpanLengths.Add(actualSpan);
                }

                // Set deck and substructure types
                bridge.DeckType = totalLength > 200 ? "Precast Segmental" : "Cast-in-Place Concrete";
                bridge.SubstructureType = totalLength > 500 ? "Drilled Shaft" : "Spread Footing";

                // Estimate cost
                double deckArea = totalLength * deckWidth;
                bridge.EstimatedCost = deckArea * (bridge.Type == BridgeType.CableStayed ? 800 : 400);

                var structure = new Structure
                {
                    Name = name,
                    Type = "Bridge",
                    Station = station,
                    Length = totalLength,
                    Width = deckWidth,
                    Bridge = bridge
                };

                project.Structures.Add(structure);
                return bridge;
            }
        }

        public TunnelDesign DesignTunnel(string projectId, string name, double station,
            double length, double internalDiameter, double coverDepth)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                // Select tunneling method
                TunnelType method;
                if (coverDepth < 30)
                    method = TunnelType.Cut_Cover;
                else if (internalDiameter > 20)
                    method = TunnelType.Bored_TBM;
                else
                    method = TunnelType.NATM;

                var tunnel = new TunnelDesign
                {
                    Method = method,
                    Length = length,
                    InternalDiameter = internalDiameter,
                    CoverDepth = coverDepth,
                    HasWaterproofing = true
                };

                // Calculate external diameter and lining
                tunnel.LiningThickness = method == TunnelType.Bored_TBM ? 1.5 : 2.0;
                tunnel.ExternalDiameter = internalDiameter + 2 * tunnel.LiningThickness;
                tunnel.LiningType = method == TunnelType.Bored_TBM ? "Precast Segments" : "Shotcrete with Steel Sets";

                // Calculate excavation volume
                double area = Math.PI * Math.Pow(tunnel.ExternalDiameter / 2, 2);
                tunnel.ExcavationVolume = area * length / 27; // CY

                // Design ventilation
                tunnel.Ventilation = new VentilationSystem
                {
                    Type = length > 1000 ? "Longitudinal Jet Fans" : "Portal Ventilation",
                    Capacity = length * internalDiameter * 10, // CFM estimate
                    FanCount = length > 2000 ? 4 : 2,
                    EmergencyFeatures = new List<string>
                    {
                        "Emergency exits every 1000 ft",
                        "Fire detection system",
                        "Emergency lighting",
                        "Communication system"
                    }
                };

                // Estimate cost
                tunnel.EstimatedCost = tunnel.ExcavationVolume * (method == TunnelType.Bored_TBM ? 2000 : 1500);

                var structure = new Structure
                {
                    Name = name,
                    Type = "Tunnel",
                    Station = station,
                    Length = length,
                    Width = internalDiameter,
                    Tunnel = tunnel
                };

                project.Structures.Add(structure);
                return tunnel;
            }
        }

        public UtilityRun CreateUtilityRun(string projectId, string name, UtilityType type,
            double startStation, double endStation, double diameter, double burialDepth)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var utility = new UtilityRun
                {
                    Name = name,
                    Type = type,
                    StartStation = startStation,
                    EndStation = endStation,
                    Diameter = diameter,
                    BurialDepth = burialDepth
                };

                // Set material based on type
                utility.Material = type switch
                {
                    UtilityType.Water => diameter > 12 ? "Ductile Iron" : "PVC",
                    UtilityType.Sewer => "PVC or RCP",
                    UtilityType.Storm => diameter > 24 ? "RCP" : "HDPE",
                    UtilityType.Gas => "Steel or PE",
                    _ => "PVC"
                };

                // Set minimum slope for gravity systems
                if (type == UtilityType.Sewer || type == UtilityType.Storm)
                {
                    // Manning's equation minimum slope
                    utility.Slope = type == UtilityType.Sewer ?
                        Math.Max(0.005, 0.4 / diameter) : Math.Max(0.003, 0.3 / diameter);
                }

                // Add manholes/structures
                double structureInterval = type == UtilityType.Sewer ? 400 : 500;
                for (double sta = startStation; sta <= endStation; sta += structureInterval)
                {
                    utility.Structures.Add(new UtilityStructure
                    {
                        Type = type == UtilityType.Storm ? "Inlet" : "Manhole",
                        Station = sta,
                        Diameter = diameter > 24 ? 60 : 48
                    });
                }

                project.Utilities.Add(utility);
                return utility;
            }
        }

        public async Task<EarthworkAnalysis> CalculateEarthwork(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var earthwork = new EarthworkAnalysis();

                    // Sum up cross-section areas
                    foreach (var alignment in project.Alignments)
                    {
                        for (int i = 0; i < alignment.CrossSections.Count - 1; i++)
                        {
                            var cs1 = alignment.CrossSections[i];
                            var cs2 = alignment.CrossSections[i + 1];
                            double distance = cs2.Station - cs1.Station;

                            // Average end area method
                            earthwork.TotalCutVolume += (cs1.CutArea + cs2.CutArea) / 2 * distance / 27;
                            earthwork.TotalFillVolume += (cs1.FillArea + cs2.FillArea) / 2 * distance / 27;
                        }
                    }

                    // Apply shrinkage/swell factors
                    earthwork.AdjustedCutVolume = earthwork.TotalCutVolume / earthwork.SwellFactor;
                    double adjustedFill = earthwork.TotalFillVolume * earthwork.ShrinkageFactor;

                    earthwork.NetVolume = earthwork.AdjustedCutVolume - adjustedFill;

                    if (earthwork.NetVolume < 0)
                    {
                        earthwork.ImportRequired = Math.Abs(earthwork.NetVolume);
                        earthwork.ExportRequired = 0;
                        earthwork.IsBalanced = false;
                    }
                    else
                    {
                        earthwork.ExportRequired = earthwork.NetVolume;
                        earthwork.ImportRequired = 0;
                        earthwork.IsBalanced = earthwork.NetVolume < earthwork.TotalFillVolume * 0.1;
                    }

                    // Calculate cost
                    earthwork.EstimatedCost =
                        earthwork.TotalCutVolume * _unitCosts["Cut"] +
                        earthwork.TotalFillVolume * _unitCosts["Fill"] +
                        earthwork.ImportRequired * _unitCosts["Import"] +
                        earthwork.ExportRequired * _unitCosts["Export"];

                    project.Earthwork = earthwork;
                    return earthwork;
                }
            });
        }

        public PavementDesign DesignPavement(double designLife, double trafficESAL, double subgradeModulus)
        {
            var pavement = new PavementDesign
            {
                Type = trafficESAL > 10000000 ? PavementType.Concrete : PavementType.Asphalt,
                DesignLife = designLife,
                ESAL = trafficESAL
            };

            if (pavement.Type == PavementType.Asphalt)
            {
                // AASHTO flexible pavement design
                double SN = CalculateStructuralNumber(trafficESAL, subgradeModulus);

                pavement.Layers.Add(new PavementLayer
                {
                    Material = "HMA Surface",
                    Thickness = 2,
                    LayerCoefficient = 0.44,
                    DrainageCoefficient = 1.0
                });

                pavement.Layers.Add(new PavementLayer
                {
                    Material = "HMA Binder",
                    Thickness = 3,
                    LayerCoefficient = 0.40,
                    DrainageCoefficient = 1.0
                });

                double baseThickness = (SN - 2 * 0.44 - 3 * 0.40) / 0.14;
                pavement.Layers.Add(new PavementLayer
                {
                    Material = "Aggregate Base",
                    Thickness = Math.Max(6, baseThickness),
                    LayerCoefficient = 0.14,
                    DrainageCoefficient = 0.9
                });

                pavement.StructuralNumber = SN;
            }
            else
            {
                // Rigid pavement
                double thickness = 8 + Math.Log10(trafficESAL) * 0.5;
                pavement.Layers.Add(new PavementLayer
                {
                    Material = "PCC Slab",
                    Thickness = Math.Max(8, thickness),
                    LayerCoefficient = 1.0
                });

                pavement.Layers.Add(new PavementLayer
                {
                    Material = "Cement Treated Base",
                    Thickness = 6,
                    LayerCoefficient = 0.5
                });
            }

            pavement.TotalThickness = pavement.Layers.Sum(l => l.Thickness);

            // Calculate cost
            double area = 1000; // per 1000 SF
            pavement.EstimatedCost = pavement.Type == PavementType.Asphalt ?
                area * _unitCosts["Asphalt"] : area * _unitCosts["Concrete_Pavement"];

            return pavement;
        }

        private double CalculateStructuralNumber(double esal, double subgradeModulus)
        {
            // Simplified AASHTO equation
            double W18 = Math.Log10(esal);
            double Mr = Math.Log10(subgradeModulus);
            return 2.0 + 0.5 * (W18 - 5) - 0.3 * (Mr - 4);
        }

        public DrainageDesign DesignDrainage(double catchmentArea, double runoffCoefficient,
            double rainfallIntensity)
        {
            var drainage = new DrainageDesign
            {
                CatchmentArea = catchmentArea,
                RunoffCoefficient = runoffCoefficient,
                RainfallIntensity = rainfallIntensity
            };

            // Rational method: Q = CIA
            drainage.DesignFlow = runoffCoefficient * rainfallIntensity * catchmentArea;

            // Size pipe using Manning's equation (n=0.013, S=0.01)
            double requiredArea = drainage.DesignFlow / (1.49 / 0.013 * Math.Pow(0.01, 0.5));
            drainage.PipeSize = Math.Ceiling(Math.Sqrt(requiredArea * 4 / Math.PI) * 12 / 6) * 6; // Round to 6"
            drainage.Slope = 0.01;

            return drainage;
        }
    }
}
