// ===================================================================
// StingBIM Code Interpretation Intelligence Engine
// Building code expertise, code path analysis, variance strategies
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.CodeInterpretationIntelligence
{
    #region Enums

    public enum BuildingCode { IBC2021, IBC2018, IBC2015, NFPA101, NFPA5000, LocalAmendment }
    public enum OccupancyGroup { A1, A2, A3, A4, A5, B, E, F1, F2, H1, H2, H3, H4, H5, I1, I2, I3, I4, M, R1, R2, R3, R4, S1, S2, U }
    public enum ConstructionType { IA, IB, IIA, IIB, IIIA, IIIB, IV, VA, VB }
    public enum ComplianceStatus { Compliant, NonCompliant, ConditionallyCompliant, RequiresVariance }
    public enum VarianceType { Appeal, AlternativeMeans, Modification, Exception }

    #endregion

    #region Data Models

    public class CodeProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public BuildingCode ApplicableCode { get; set; } = BuildingCode.IBC2021;
        public List<string> LocalAmendments { get; set; } = new();
        public BuildingClassification Classification { get; set; }
        public List<CodeCheck> CodeChecks { get; set; } = new();
        public List<VarianceRequest> Variances { get; set; } = new();
        public CodeAnalysisSummary Summary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BuildingClassification
    {
        public OccupancyGroup PrimaryOccupancy { get; set; }
        public List<OccupancyGroup> SecondaryOccupancies { get; set; } = new();
        public ConstructionType ConstructionType { get; set; }
        public bool IsSprinklered { get; set; }
        public double BuildingArea { get; set; }
        public double BuildingHeight { get; set; }
        public int NumberOfStories { get; set; }
        public bool IsHighRise { get; set; }
        public bool HasBasement { get; set; }
        public int Occupancy { get; set; }
    }

    public class CodeCheck
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CodeSection { get; set; }
        public string Requirement { get; set; }
        public string Category { get; set; }
        public double RequiredValue { get; set; }
        public double ProvidedValue { get; set; }
        public string Unit { get; set; }
        public ComplianceStatus Status { get; set; }
        public string Notes { get; set; }
        public List<string> AlternativePaths { get; set; } = new();
    }

    public class VarianceRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RequestNumber { get; set; }
        public VarianceType Type { get; set; }
        public string CodeSection { get; set; }
        public string Requirement { get; set; }
        public string ProposedAlternative { get; set; }
        public string Justification { get; set; }
        public List<string> SupportingDocuments { get; set; } = new();
        public string Status { get; set; } = "Draft";
        public DateTime SubmittedDate { get; set; }
        public DateTime? DecisionDate { get; set; }
        public string Decision { get; set; }
        public List<string> Conditions { get; set; } = new();
    }

    public class CodeAnalysisSummary
    {
        public int TotalChecks { get; set; }
        public int CompliantChecks { get; set; }
        public int NonCompliantChecks { get; set; }
        public int VariancesRequired { get; set; }
        public double ComplianceRate => TotalChecks > 0 ? CompliantChecks * 100.0 / TotalChecks : 0;
        public List<string> CriticalIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class AllowableArea
    {
        public OccupancyGroup Occupancy { get; set; }
        public ConstructionType Construction { get; set; }
        public double TabularArea { get; set; }
        public double FrontageIncrease { get; set; }
        public double SprinklerIncrease { get; set; }
        public double TotalAllowable { get; set; }
        public bool UnlimitedArea { get; set; }
    }

    public class AllowableHeight
    {
        public OccupancyGroup Occupancy { get; set; }
        public ConstructionType Construction { get; set; }
        public double MaxHeightFeet { get; set; }
        public int MaxStories { get; set; }
        public double SprinklerIncreaseHeight { get; set; }
        public int SprinklerIncreaseStories { get; set; }
    }

    public class EgressRequirement
    {
        public string SpaceName { get; set; }
        public int Occupancy { get; set; }
        public double OccupantLoadFactor { get; set; }
        public int CalculatedOccupancy { get; set; }
        public int RequiredExits { get; set; }
        public double RequiredExitWidth { get; set; }
        public double MaxTravelDistance { get; set; }
        public double MaxCommonPath { get; set; }
        public double MaxDeadEnd { get; set; }
    }

    public class FireRatingRequirement
    {
        public string Element { get; set; }
        public int RequiredRatingHours { get; set; }
        public string Condition { get; set; }
        public List<string> AcceptableAssemblies { get; set; } = new();
    }

    #endregion

    public sealed class CodeInterpretationIntelligenceEngine
    {
        private static readonly Lazy<CodeInterpretationIntelligenceEngine> _instance =
            new Lazy<CodeInterpretationIntelligenceEngine>(() => new CodeInterpretationIntelligenceEngine());
        public static CodeInterpretationIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, CodeProject> _projects = new();
        private readonly Dictionary<(OccupancyGroup, ConstructionType), (double Area, int Stories, double Height)> _heightAreaTable = new();
        private readonly object _lock = new object();

        private CodeInterpretationIntelligenceEngine()
        {
            InitializeHeightAreaTable();
        }

        private void InitializeHeightAreaTable()
        {
            // IBC Table 504.3/504.4/506.2 simplified
            _heightAreaTable[(OccupancyGroup.A2, ConstructionType.IB)] = (37500, 5, 75);
            _heightAreaTable[(OccupancyGroup.A2, ConstructionType.IIA)] = (15500, 3, 65);
            _heightAreaTable[(OccupancyGroup.A2, ConstructionType.IIIA)] = (14000, 3, 55);
            _heightAreaTable[(OccupancyGroup.B, ConstructionType.IB)] = (60000, 11, 160);
            _heightAreaTable[(OccupancyGroup.B, ConstructionType.IIA)] = (37500, 5, 75);
            _heightAreaTable[(OccupancyGroup.B, ConstructionType.IIIA)] = (28500, 4, 65);
            _heightAreaTable[(OccupancyGroup.E, ConstructionType.IIA)] = (26500, 3, 65);
            _heightAreaTable[(OccupancyGroup.M, ConstructionType.IIA)] = (21500, 4, 65);
            _heightAreaTable[(OccupancyGroup.R2, ConstructionType.IIA)] = (24000, 4, 65);
            _heightAreaTable[(OccupancyGroup.R2, ConstructionType.IIIA)] = (24000, 4, 65);
            _heightAreaTable[(OccupancyGroup.R2, ConstructionType.VA)] = (12000, 3, 50);
        }

        public CodeProject CreateCodeProject(string projectId, string projectName, BuildingCode code)
        {
            var project = new CodeProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                ApplicableCode = code,
                Classification = new BuildingClassification()
            };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public BuildingClassification ClassifyBuilding(string projectId, OccupancyGroup occupancy,
            ConstructionType construction, double area, double height, int stories, bool sprinklered)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                project.Classification = new BuildingClassification
                {
                    PrimaryOccupancy = occupancy,
                    ConstructionType = construction,
                    BuildingArea = area,
                    BuildingHeight = height,
                    NumberOfStories = stories,
                    IsSprinklered = sprinklered,
                    IsHighRise = height > 75
                };

                return project.Classification;
            }
        }

        public AllowableArea CalculateAllowableArea(string projectId, double frontagePercent = 25)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var classification = project.Classification;
                var key = (classification.PrimaryOccupancy, classification.ConstructionType);

                if (!_heightAreaTable.TryGetValue(key, out var limits))
                    return null;

                var allowable = new AllowableArea
                {
                    Occupancy = classification.PrimaryOccupancy,
                    Construction = classification.ConstructionType,
                    TabularArea = limits.Area
                };

                // Frontage increase (IBC 506.3)
                if (frontagePercent > 25)
                {
                    var frontageMultiplier = Math.Min((frontagePercent - 25) / 100 * 0.75, 0.75);
                    allowable.FrontageIncrease = limits.Area * frontageMultiplier;
                }

                // Sprinkler increase (IBC 506.3)
                if (classification.IsSprinklered)
                {
                    allowable.SprinklerIncrease = limits.Area * 2.0; // 200% for multi-story
                }

                allowable.TotalAllowable = allowable.TabularArea + allowable.FrontageIncrease + allowable.SprinklerIncrease;

                // Check for unlimited area (IBC 507)
                if (classification.IsSprinklered && classification.NumberOfStories <= 2 &&
                    classification.ConstructionType == ConstructionType.IB)
                {
                    allowable.UnlimitedArea = true;
                }

                return allowable;
            }
        }

        public AllowableHeight CalculateAllowableHeight(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var classification = project.Classification;
                var key = (classification.PrimaryOccupancy, classification.ConstructionType);

                if (!_heightAreaTable.TryGetValue(key, out var limits))
                    return null;

                var allowable = new AllowableHeight
                {
                    Occupancy = classification.PrimaryOccupancy,
                    Construction = classification.ConstructionType,
                    MaxHeightFeet = limits.Height,
                    MaxStories = limits.Stories
                };

                if (classification.IsSprinklered)
                {
                    allowable.SprinklerIncreaseHeight = 20;
                    allowable.SprinklerIncreaseStories = 1;
                }

                return allowable;
            }
        }

        public EgressRequirement CalculateEgressRequirements(string spaceName, double area, OccupancyGroup occupancy)
        {
            var loadFactors = new Dictionary<OccupancyGroup, double>
            {
                [OccupancyGroup.A1] = 7,
                [OccupancyGroup.A2] = 15,
                [OccupancyGroup.A3] = 15,
                [OccupancyGroup.B] = 150,
                [OccupancyGroup.E] = 20,
                [OccupancyGroup.M] = 60,
                [OccupancyGroup.R2] = 200,
                [OccupancyGroup.S1] = 500
            };

            var loadFactor = loadFactors.GetValueOrDefault(occupancy, 100);
            var calculatedOccupancy = (int)Math.Ceiling(area / loadFactor);

            var egress = new EgressRequirement
            {
                SpaceName = spaceName,
                Occupancy = (int)area,
                OccupantLoadFactor = loadFactor,
                CalculatedOccupancy = calculatedOccupancy
            };

            // Required exits (IBC Table 1006.2.1)
            egress.RequiredExits = calculatedOccupancy switch
            {
                <= 49 => 1,
                <= 500 => 2,
                <= 1000 => 3,
                _ => 4
            };

            // Exit width (IBC 1005.1)
            var widthFactor = occupancy == OccupancyGroup.A1 || occupancy == OccupancyGroup.A2 ? 0.3 : 0.2;
            egress.RequiredExitWidth = calculatedOccupancy * widthFactor;

            // Travel distance (IBC Table 1017.2)
            egress.MaxTravelDistance = occupancy switch
            {
                OccupancyGroup.A1 or OccupancyGroup.A2 => 250,
                OccupancyGroup.B => 300,
                OccupancyGroup.E => 250,
                _ => 250
            };

            egress.MaxCommonPath = 75;
            egress.MaxDeadEnd = 50;

            return egress;
        }

        public List<FireRatingRequirement> GetFireRatingRequirements(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<FireRatingRequirement>();

                var construction = project.Classification.ConstructionType;

                var requirements = new List<FireRatingRequirement>
                {
                    new FireRatingRequirement
                    {
                        Element = "Structural Frame",
                        RequiredRatingHours = construction switch
                        {
                            ConstructionType.IA => 3,
                            ConstructionType.IB => 2,
                            ConstructionType.IIA => 1,
                            _ => 0
                        }
                    },
                    new FireRatingRequirement
                    {
                        Element = "Bearing Walls - Exterior",
                        RequiredRatingHours = construction switch
                        {
                            ConstructionType.IA => 3,
                            ConstructionType.IB => 2,
                            ConstructionType.IIA => 1,
                            _ => 0
                        }
                    },
                    new FireRatingRequirement
                    {
                        Element = "Floor Construction",
                        RequiredRatingHours = construction switch
                        {
                            ConstructionType.IA => 2,
                            ConstructionType.IB => 2,
                            ConstructionType.IIA => 1,
                            _ => 0
                        }
                    },
                    new FireRatingRequirement
                    {
                        Element = "Roof Construction",
                        RequiredRatingHours = construction switch
                        {
                            ConstructionType.IA => 1.5m,
                            ConstructionType.IB => 1,
                            _ => 0
                        } >= 1 ? 1 : 0
                    }
                };

                return requirements;
            }
        }

        public CodeCheck PerformCodeCheck(string projectId, string codeSection, string requirement,
            double requiredValue, double providedValue, string unit)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var check = new CodeCheck
                {
                    CodeSection = codeSection,
                    Requirement = requirement,
                    RequiredValue = requiredValue,
                    ProvidedValue = providedValue,
                    Unit = unit,
                    Status = providedValue >= requiredValue ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant
                };

                if (check.Status == ComplianceStatus.NonCompliant)
                {
                    check.AlternativePaths = GenerateAlternativePaths(codeSection);
                }

                project.CodeChecks.Add(check);
                UpdateSummary(project);
                return check;
            }
        }

        private List<string> GenerateAlternativePaths(string codeSection)
        {
            var alternatives = new List<string>();

            if (codeSection.StartsWith("10")) // Egress
            {
                alternatives.Add("Add automatic sprinkler system to increase travel distance");
                alternatives.Add("Request code modification with equivalent life safety measures");
            }
            else if (codeSection.StartsWith("5")) // Height and Area
            {
                alternatives.Add("Install NFPA 13 sprinkler system for area increase");
                alternatives.Add("Provide additional fire separation to create multiple buildings");
            }

            return alternatives;
        }

        public VarianceRequest RequestVariance(string projectId, VarianceType type, string codeSection,
            string requirement, string proposedAlternative, string justification)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var variance = new VarianceRequest
                {
                    RequestNumber = $"VAR-{project.Variances.Count + 1:D3}",
                    Type = type,
                    CodeSection = codeSection,
                    Requirement = requirement,
                    ProposedAlternative = proposedAlternative,
                    Justification = justification,
                    SubmittedDate = DateTime.UtcNow
                };

                project.Variances.Add(variance);
                return variance;
            }
        }

        private void UpdateSummary(CodeProject project)
        {
            project.Summary = new CodeAnalysisSummary
            {
                TotalChecks = project.CodeChecks.Count,
                CompliantChecks = project.CodeChecks.Count(c => c.Status == ComplianceStatus.Compliant),
                NonCompliantChecks = project.CodeChecks.Count(c => c.Status == ComplianceStatus.NonCompliant),
                VariancesRequired = project.CodeChecks.Count(c => c.Status == ComplianceStatus.RequiresVariance)
            };

            foreach (var check in project.CodeChecks.Where(c => c.Status == ComplianceStatus.NonCompliant))
            {
                project.Summary.CriticalIssues.Add($"{check.CodeSection}: {check.Requirement}");
            }
        }
    }
}
