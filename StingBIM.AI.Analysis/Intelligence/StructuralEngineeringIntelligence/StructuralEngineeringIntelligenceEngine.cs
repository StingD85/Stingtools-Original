// ===================================================================
// StingBIM Structural Engineering Intelligence Engine
// Seismic analysis, progressive collapse, connection design
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.StructuralEngineeringIntelligence
{
    #region Enums

    public enum StructuralSystem { MomentFrame, BracedFrame, ShearWall, DualSystem, BearingWall }
    public enum SeismicDesignCategory { A, B, C, D, E, F }
    public enum RiskCategory { I, II, III, IV }
    public enum ConnectionType { Bolted, Welded, Hybrid, PinConnection, MomentConnection }
    public enum LoadType { Dead, Live, Snow, Wind, Seismic, Rain }

    #endregion

    #region Data Models

    public class StructuralProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public BuildingParameters Building { get; set; }
        public SeismicParameters Seismic { get; set; }
        public List<StructuralMember> Members { get; set; } = new();
        public List<StructuralConnection> Connections { get; set; } = new();
        public List<LoadCombination> LoadCombinations { get; set; } = new();
        public StructuralAnalysisSummary Summary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BuildingParameters
    {
        public double Height { get; set; }
        public int Stories { get; set; }
        public double FloorArea { get; set; }
        public StructuralSystem LateralSystem { get; set; }
        public RiskCategory RiskCategory { get; set; }
        public string Location { get; set; }
    }

    public class SeismicParameters
    {
        public double Ss { get; set; }
        public double S1 { get; set; }
        public string SiteClass { get; set; } = "D";
        public double Fa { get; set; }
        public double Fv { get; set; }
        public double Sds { get; set; }
        public double Sd1 { get; set; }
        public SeismicDesignCategory SDC { get; set; }
        public double R { get; set; }
        public double Cd { get; set; }
        public double Omega0 { get; set; }
        public double Ie { get; set; }
        public double Cs { get; set; }
        public double T { get; set; }
        public double BaseShear { get; set; }
    }

    public class StructuralMember
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Tag { get; set; }
        public string Type { get; set; }
        public string Section { get; set; }
        public string Material { get; set; }
        public double Length { get; set; }
        public MemberForces Forces { get; set; }
        public double DemandCapacityRatio { get; set; }
        public bool IsAdequate => DemandCapacityRatio <= 1.0;
    }

    public class MemberForces
    {
        public double Axial { get; set; }
        public double ShearMajor { get; set; }
        public double ShearMinor { get; set; }
        public double MomentMajor { get; set; }
        public double MomentMinor { get; set; }
        public double Torsion { get; set; }
    }

    public class StructuralConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Tag { get; set; }
        public ConnectionType Type { get; set; }
        public string BeamSection { get; set; }
        public string ColumnSection { get; set; }
        public ConnectionDesign Design { get; set; }
        public double DemandCapacityRatio { get; set; }
        public bool IsPrequalified { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class ConnectionDesign
    {
        public int BoltCount { get; set; }
        public string BoltSize { get; set; }
        public string BoltGrade { get; set; }
        public double WeldSize { get; set; }
        public string WeldType { get; set; }
        public double PlateThickness { get; set; }
        public double ShearCapacity { get; set; }
        public double MomentCapacity { get; set; }
    }

    public class LoadCombination
    {
        public string Name { get; set; }
        public Dictionary<LoadType, double> Factors { get; set; } = new();
        public string Standard { get; set; }
    }

    public class ProgressiveCollapseAnalysis
    {
        public string MemberRemoved { get; set; }
        public string Location { get; set; }
        public bool SurvivesRemoval { get; set; }
        public double MaxDCR { get; set; }
        public List<string> CriticalMembers { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class DriftAnalysis
    {
        public int Story { get; set; }
        public double StoryHeight { get; set; }
        public double Displacement { get; set; }
        public double StoryDrift { get; set; }
        public double DriftRatio => StoryHeight > 0 ? StoryDrift / StoryHeight : 0;
        public double AllowableDrift { get; set; }
        public bool IsCompliant => DriftRatio <= AllowableDrift;
    }

    public class StructuralAnalysisSummary
    {
        public int TotalMembers { get; set; }
        public int AdequateMembers { get; set; }
        public int OverstressedMembers { get; set; }
        public double MaxDCR { get; set; }
        public double BaseShear { get; set; }
        public double MaxDriftRatio { get; set; }
        public SeismicDesignCategory SDC { get; set; }
        public List<string> CriticalIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    #endregion

    public sealed class StructuralEngineeringIntelligenceEngine
    {
        private static readonly Lazy<StructuralEngineeringIntelligenceEngine> _instance =
            new Lazy<StructuralEngineeringIntelligenceEngine>(() => new StructuralEngineeringIntelligenceEngine());
        public static StructuralEngineeringIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, StructuralProject> _projects = new();
        private readonly object _lock = new object();

        // ASCE 7-22 Table 12.2-1 System parameters
        private readonly Dictionary<StructuralSystem, (double R, double Cd, double Omega0)> _systemParameters = new()
        {
            [StructuralSystem.MomentFrame] = (8, 5.5, 3),
            [StructuralSystem.BracedFrame] = (6, 5, 2),
            [StructuralSystem.ShearWall] = (5, 5, 2.5),
            [StructuralSystem.DualSystem] = (7, 5.5, 2.5),
            [StructuralSystem.BearingWall] = (4, 4, 2.5)
        };

        private StructuralEngineeringIntelligenceEngine()
        {
            InitializeLoadCombinations();
        }

        private List<LoadCombination> _standardCombinations = new();

        private void InitializeLoadCombinations()
        {
            // ASCE 7-22 Load Combinations
            _standardCombinations = new List<LoadCombination>
            {
                new LoadCombination
                {
                    Name = "1.4D",
                    Factors = new Dictionary<LoadType, double> { [LoadType.Dead] = 1.4 },
                    Standard = "ASCE 7-22 2.3.1"
                },
                new LoadCombination
                {
                    Name = "1.2D + 1.6L",
                    Factors = new Dictionary<LoadType, double> { [LoadType.Dead] = 1.2, [LoadType.Live] = 1.6 },
                    Standard = "ASCE 7-22 2.3.1"
                },
                new LoadCombination
                {
                    Name = "1.2D + 1.0E + L",
                    Factors = new Dictionary<LoadType, double> { [LoadType.Dead] = 1.2, [LoadType.Seismic] = 1.0, [LoadType.Live] = 1.0 },
                    Standard = "ASCE 7-22 2.3.6"
                },
                new LoadCombination
                {
                    Name = "0.9D + 1.0E",
                    Factors = new Dictionary<LoadType, double> { [LoadType.Dead] = 0.9, [LoadType.Seismic] = 1.0 },
                    Standard = "ASCE 7-22 2.3.6"
                }
            };
        }

        public StructuralProject CreateStructuralProject(string projectId, string projectName)
        {
            var project = new StructuralProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Building = new BuildingParameters(),
                Seismic = new SeismicParameters(),
                LoadCombinations = _standardCombinations
            };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public SeismicParameters CalculateSeismicParameters(string projectId, double Ss, double S1,
            string siteClass, StructuralSystem system, RiskCategory risk, double height)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var seismic = new SeismicParameters { Ss = Ss, S1 = S1, SiteClass = siteClass };

                // Site coefficients (simplified for Site Class D)
                seismic.Fa = siteClass switch
                {
                    "A" => 0.8,
                    "B" => 0.9,
                    "C" => 1.0,
                    "D" => 1.1,
                    "E" => 1.2,
                    _ => 1.1
                };

                seismic.Fv = siteClass switch
                {
                    "A" => 0.8,
                    "B" => 0.8,
                    "C" => 1.0,
                    "D" => 1.5,
                    "E" => 2.0,
                    _ => 1.5
                };

                // Design spectral accelerations
                seismic.Sds = (2.0 / 3.0) * seismic.Fa * Ss;
                seismic.Sd1 = (2.0 / 3.0) * seismic.Fv * S1;

                // Seismic Design Category
                seismic.SDC = DetermineSDC(seismic.Sds, seismic.Sd1, risk);

                // System parameters
                if (_systemParameters.TryGetValue(system, out var sysParams))
                {
                    seismic.R = sysParams.R;
                    seismic.Cd = sysParams.Cd;
                    seismic.Omega0 = sysParams.Omega0;
                }

                // Importance factor
                seismic.Ie = risk switch
                {
                    RiskCategory.I => 1.0,
                    RiskCategory.II => 1.0,
                    RiskCategory.III => 1.25,
                    RiskCategory.IV => 1.5,
                    _ => 1.0
                };

                // Approximate period
                var Ct = system == StructuralSystem.MomentFrame ? 0.028 : 0.02;
                var x = system == StructuralSystem.MomentFrame ? 0.8 : 0.75;
                seismic.T = Ct * Math.Pow(height, x);

                // Seismic response coefficient
                var CsCalc = seismic.Sds / (seismic.R / seismic.Ie);
                var CsMax = seismic.Sd1 / (seismic.T * (seismic.R / seismic.Ie));
                var CsMin = Math.Max(0.044 * seismic.Sds * seismic.Ie, 0.01);

                seismic.Cs = Math.Max(Math.Min(CsCalc, CsMax), CsMin);

                project.Seismic = seismic;
                project.Building.LateralSystem = system;
                project.Building.RiskCategory = risk;
                project.Building.Height = height;

                return seismic;
            }
        }

        private SeismicDesignCategory DetermineSDC(double Sds, double Sd1, RiskCategory risk)
        {
            // Simplified SDC determination
            if (risk == RiskCategory.IV)
            {
                if (Sds >= 0.50 || Sd1 >= 0.20) return SeismicDesignCategory.D;
            }

            if (Sds >= 0.50 && Sd1 >= 0.20) return SeismicDesignCategory.D;
            if (Sds >= 0.33 || Sd1 >= 0.133) return SeismicDesignCategory.C;
            if (Sds >= 0.167 || Sd1 >= 0.067) return SeismicDesignCategory.B;
            return SeismicDesignCategory.A;
        }

        public double CalculateBaseShear(string projectId, double seismicWeight)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return 0;

                project.Seismic.BaseShear = project.Seismic.Cs * seismicWeight;
                return project.Seismic.BaseShear;
            }
        }

        public StructuralMember AddMember(string projectId, string tag, string type, string section,
            string material, double length, MemberForces forces)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var member = new StructuralMember
                {
                    Tag = tag,
                    Type = type,
                    Section = section,
                    Material = material,
                    Length = length,
                    Forces = forces,
                    DemandCapacityRatio = CalculateDCR(section, material, forces)
                };

                project.Members.Add(member);
                return member;
            }
        }

        private double CalculateDCR(string section, string material, MemberForces forces)
        {
            // Simplified DCR calculation
            var capacity = EstimateMemberCapacity(section, material);
            var demand = Math.Sqrt(
                Math.Pow(forces.Axial / capacity.Axial, 2) +
                Math.Pow(forces.MomentMajor / capacity.Moment, 2));
            return Math.Max(demand, 0.1);
        }

        private (double Axial, double Moment) EstimateMemberCapacity(string section, string material)
        {
            // Simplified capacity estimation
            var fy = material.Contains("50") ? 50 : 36; // ksi
            var area = section.Contains("W14") ? 20 : section.Contains("W12") ? 15 : 10; // in²
            var Zx = section.Contains("W14") ? 150 : section.Contains("W12") ? 100 : 50; // in³

            return (area * fy, Zx * fy);
        }

        public async Task<ProgressiveCollapseAnalysis> AnalyzeProgressiveCollapse(string projectId, string memberTag)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var member = project.Members.FirstOrDefault(m => m.Tag == memberTag);
                    if (member == null) return null;

                    var analysis = new ProgressiveCollapseAnalysis
                    {
                        MemberRemoved = memberTag,
                        Location = member.Type
                    };

                    // Simplified analysis - check adjacent members
                    var adjacentMembers = project.Members
                        .Where(m => m.Tag != memberTag && m.Type == member.Type)
                        .ToList();

                    var maxDCRIncrease = 1.5; // Assume 50% load increase to adjacent members
                    analysis.MaxDCR = adjacentMembers.Any() ?
                        adjacentMembers.Max(m => m.DemandCapacityRatio * maxDCRIncrease) : 0;

                    analysis.SurvivesRemoval = analysis.MaxDCR <= 2.0; // UFC 4-023-03 criterion

                    analysis.CriticalMembers = adjacentMembers
                        .Where(m => m.DemandCapacityRatio * maxDCRIncrease > 1.5)
                        .Select(m => m.Tag)
                        .ToList();

                    if (!analysis.SurvivesRemoval)
                    {
                        analysis.Recommendations.Add("Add redundant load path");
                        analysis.Recommendations.Add("Strengthen adjacent members");
                        analysis.Recommendations.Add("Consider catenary action in floor system");
                    }

                    return analysis;
                }
            });
        }

        public List<DriftAnalysis> CalculateStoryDrifts(string projectId, List<double> storyDisplacements, List<double> storyHeights)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<DriftAnalysis>();

                var drifts = new List<DriftAnalysis>();
                var allowableDrift = 0.02; // 2% for most buildings

                for (int i = 0; i < storyDisplacements.Count; i++)
                {
                    var displacement = storyDisplacements[i];
                    var prevDisplacement = i > 0 ? storyDisplacements[i - 1] : 0;
                    var storyDrift = (displacement - prevDisplacement) * project.Seismic.Cd / project.Seismic.Ie;

                    drifts.Add(new DriftAnalysis
                    {
                        Story = i + 1,
                        StoryHeight = storyHeights[i],
                        Displacement = displacement,
                        StoryDrift = storyDrift,
                        AllowableDrift = allowableDrift
                    });
                }

                return drifts;
            }
        }

        public StructuralConnection DesignConnection(string projectId, string tag, ConnectionType type,
            string beamSection, string columnSection, double shearDemand, double momentDemand)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var connection = new StructuralConnection
                {
                    Tag = tag,
                    Type = type,
                    BeamSection = beamSection,
                    ColumnSection = columnSection
                };

                connection.Design = type switch
                {
                    ConnectionType.Bolted => DesignBoltedConnection(shearDemand, momentDemand),
                    ConnectionType.Welded => DesignWeldedConnection(shearDemand, momentDemand),
                    _ => DesignBoltedConnection(shearDemand, momentDemand)
                };

                connection.DemandCapacityRatio = Math.Max(
                    shearDemand / connection.Design.ShearCapacity,
                    momentDemand / connection.Design.MomentCapacity);

                if (type == ConnectionType.MomentConnection && project.Seismic.SDC >= SeismicDesignCategory.D)
                {
                    connection.Notes.Add("Prequalified moment connection required per AISC 358");
                    connection.IsPrequalified = false;
                }

                project.Connections.Add(connection);
                return connection;
            }
        }

        private ConnectionDesign DesignBoltedConnection(double shearDemand, double momentDemand)
        {
            var boltCapacity = 17.9; // A325-N 3/4" single shear, kips
            var requiredBolts = (int)Math.Ceiling(shearDemand / boltCapacity);

            return new ConnectionDesign
            {
                BoltCount = Math.Max(requiredBolts, 2),
                BoltSize = "3/4\"",
                BoltGrade = "A325",
                PlateThickness = 0.5,
                ShearCapacity = requiredBolts * boltCapacity * 1.1,
                MomentCapacity = momentDemand * 1.25
            };
        }

        private ConnectionDesign DesignWeldedConnection(double shearDemand, double momentDemand)
        {
            var weldStrength = 1.392; // kips/in per 1/16" fillet weld
            var requiredWeldSize = shearDemand / (weldStrength * 12); // 12" effective length

            return new ConnectionDesign
            {
                WeldSize = Math.Max(Math.Ceiling(requiredWeldSize * 16) / 16, 0.25),
                WeldType = "Fillet",
                ShearCapacity = shearDemand * 1.1,
                MomentCapacity = momentDemand * 1.25
            };
        }

        public async Task<StructuralAnalysisSummary> GenerateSummary(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    project.Summary = new StructuralAnalysisSummary
                    {
                        TotalMembers = project.Members.Count,
                        AdequateMembers = project.Members.Count(m => m.IsAdequate),
                        OverstressedMembers = project.Members.Count(m => !m.IsAdequate),
                        MaxDCR = project.Members.Any() ? project.Members.Max(m => m.DemandCapacityRatio) : 0,
                        BaseShear = project.Seismic.BaseShear,
                        SDC = project.Seismic.SDC
                    };

                    foreach (var member in project.Members.Where(m => !m.IsAdequate))
                    {
                        project.Summary.CriticalIssues.Add($"{member.Tag}: DCR = {member.DemandCapacityRatio:F2} > 1.0");
                    }

                    if (project.Summary.MaxDCR > 0.9)
                    {
                        project.Summary.Recommendations.Add("Review members with DCR > 0.9 for optimization");
                    }

                    return project.Summary;
                }
            });
        }
    }
}
