// ===================================================================
// StingBIM Retrofit Intelligence Engine
// Existing building renovation, adaptive reuse, seismic retrofit
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RetrofitIntelligence
{
    #region Enums

    public enum BuildingEra { Pre1940, Era1940_1970, Era1970_1990, Era1990_2010, Post2010 }
    public enum RetrofitType { Seismic, Energy, Accessibility, CodeCompliance, AdaptiveReuse, Historic }
    public enum StructuralSystem { UnreinforcedMasonry, NonDuctileConcrete, SteelMoment, WoodFrame, Tiltup }
    public enum SeismicRetrofitStrategy { Shearwalls, BracedFrames, MomentFrames, BaseIsolation, Dampers }
    public enum EnergyMeasure { Insulation, Windows, HVAC, Lighting, Envelope, Solar }
    public enum ConditionRating { Good, Fair, Poor, Critical }

    #endregion

    #region Data Models

    public class RetrofitProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public BuildingEra Era { get; set; }
        public double GrossArea { get; set; }
        public int FloorCount { get; set; }
        public string OriginalUse { get; set; }
        public string ProposedUse { get; set; }
        public BuildingAssessment Assessment { get; set; }
        public List<RetrofitScope> Scopes { get; set; } = new();
        public CostEstimate Cost { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BuildingAssessment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public StructuralAssessment Structural { get; set; }
        public EnvelopeAssessment Envelope { get; set; }
        public MEPAssessment MEP { get; set; }
        public AccessibilityAssessment Accessibility { get; set; }
        public EnvironmentalAssessment Environmental { get; set; }
        public HistoricAssessment Historic { get; set; }
        public double OverallScore { get; set; }
        public List<string> CriticalIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class StructuralAssessment
    {
        public StructuralSystem PrimarySystem { get; set; }
        public ConditionRating Condition { get; set; }
        public double SeismicScore { get; set; }
        public double GravityCapacity { get; set; }
        public double LateralCapacity { get; set; }
        public bool HasSoftStory { get; set; }
        public bool HasPlanIrregularity { get; set; }
        public bool HasVerticalIrregularity { get; set; }
        public List<StructuralDeficiency> Deficiencies { get; set; } = new();
        public double EstimatedRetrofitCost { get; set; }
    }

    public class StructuralDeficiency
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public string RecommendedAction { get; set; }
        public double EstimatedCost { get; set; }
    }

    public class EnvelopeAssessment
    {
        public ConditionRating Condition { get; set; }
        public double WallRValue { get; set; }
        public double RoofRValue { get; set; }
        public double WindowUValue { get; set; }
        public double AirLeakage { get; set; }
        public double MoistureInfiltration { get; set; }
        public List<EnvelopeIssue> Issues { get; set; } = new();
        public double EnergyLoss { get; set; }
        public double EstimatedRetrofitCost { get; set; }
    }

    public class EnvelopeIssue
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public double AffectedArea { get; set; }
        public double EnergyCost { get; set; }
    }

    public class MEPAssessment
    {
        public ConditionRating HVACCondition { get; set; }
        public ConditionRating PlumbingCondition { get; set; }
        public ConditionRating ElectricalCondition { get; set; }
        public int HVACAge { get; set; }
        public double HVACEfficiency { get; set; }
        public double ElectricalCapacity { get; set; }
        public double ElectricalDemand { get; set; }
        public bool HasAsbestos { get; set; }
        public bool HasLeadPaint { get; set; }
        public bool HasPCBs { get; set; }
        public List<MEPDeficiency> Deficiencies { get; set; } = new();
        public double EstimatedRetrofitCost { get; set; }
    }

    public class MEPDeficiency
    {
        public string System { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public double EstimatedCost { get; set; }
    }

    public class AccessibilityAssessment
    {
        public bool HasAccessibleEntrance { get; set; }
        public bool HasAccessibleRoute { get; set; }
        public bool HasAccessibleRestrooms { get; set; }
        public bool HasElevator { get; set; }
        public int ParkingSpaces { get; set; }
        public int AccessibleParkingSpaces { get; set; }
        public List<AccessibilityBarrier> Barriers { get; set; } = new();
        public double CompliancePercentage { get; set; }
        public double EstimatedRetrofitCost { get; set; }
    }

    public class AccessibilityBarrier
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string ADASection { get; set; }
        public double RemovalCost { get; set; }
        public int Priority { get; set; }
    }

    public class EnvironmentalAssessment
    {
        public bool HasAsbestos { get; set; }
        public bool HasLeadPaint { get; set; }
        public bool HasPCBs { get; set; }
        public bool HasMold { get; set; }
        public bool HasRadon { get; set; }
        public bool HasUndergroundTanks { get; set; }
        public double AbatementCost { get; set; }
        public List<HazardLocation> Hazards { get; set; } = new();
    }

    public class HazardLocation
    {
        public string Type { get; set; }
        public string Location { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double AbatementCost { get; set; }
    }

    public class HistoricAssessment
    {
        public bool IsListed { get; set; }
        public string Designation { get; set; }
        public List<string> CharacterDefiningFeatures { get; set; } = new();
        public List<string> ContributingElements { get; set; } = new();
        public List<string> NonContributingElements { get; set; } = new();
        public bool QualifiesForTaxCredits { get; set; }
        public double PotentialTaxCredit { get; set; }
    }

    public class RetrofitScope
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public RetrofitType Type { get; set; }
        public string Description { get; set; }
        public List<RetrofitMeasure> Measures { get; set; } = new();
        public double TotalCost { get; set; }
        public double AnnualSavings { get; set; }
        public double Payback { get; set; }
        public List<string> Permits { get; set; } = new();
    }

    public class RetrofitMeasure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double UnitCost { get; set; }
        public double TotalCost { get; set; }
        public double AnnualSavings { get; set; }
        public double SimplePayback { get; set; }
        public List<string> Benefits { get; set; } = new();
    }

    public class SeismicRetrofit
    {
        public SeismicRetrofitStrategy PrimaryStrategy { get; set; }
        public double TargetPerformanceLevel { get; set; }
        public List<SeismicElement> Elements { get; set; } = new();
        public double TotalCost { get; set; }
        public double StrengthIncrease { get; set; }
        public double DriftReduction { get; set; }
    }

    public class SeismicElement
    {
        public string Type { get; set; }
        public string Location { get; set; }
        public int Quantity { get; set; }
        public double Capacity { get; set; }
        public double Cost { get; set; }
    }

    public class EnergyRetrofit
    {
        public double CurrentEUI { get; set; }
        public double TargetEUI { get; set; }
        public List<EnergyMeasure> Measures { get; set; } = new();
        public double TotalCost { get; set; }
        public double AnnualSavings { get; set; }
        public double SimplePayback { get; set; }
        public double CarbonReduction { get; set; }
    }

    public class CostEstimate
    {
        public double HardCosts { get; set; }
        public double SoftCosts { get; set; }
        public double Contingency { get; set; }
        public double TotalCost { get; set; }
        public double CostPerSF { get; set; }
        public Dictionary<RetrofitType, double> CostByScope { get; set; } = new();
        public List<string> Assumptions { get; set; } = new();
    }

    public class AdaptiveReuseFeasibility
    {
        public string OriginalUse { get; set; }
        public string ProposedUse { get; set; }
        public double FeasibilityScore { get; set; }
        public List<string> Advantages { get; set; } = new();
        public List<string> Challenges { get; set; } = new();
        public double EstimatedCost { get; set; }
        public double EstimatedValue { get; set; }
        public double ROI { get; set; }
    }

    #endregion

    public sealed class RetrofitIntelligenceEngine
    {
        private static readonly Lazy<RetrofitIntelligenceEngine> _instance =
            new Lazy<RetrofitIntelligenceEngine>(() => new RetrofitIntelligenceEngine());
        public static RetrofitIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, RetrofitProject> _projects = new();
        private readonly object _lock = new object();

        // Typical R-values by era
        private readonly Dictionary<BuildingEra, (double wall, double roof)> _typicalRValues = new()
        {
            [BuildingEra.Pre1940] = (4, 8),
            [BuildingEra.Era1940_1970] = (6, 12),
            [BuildingEra.Era1970_1990] = (11, 19),
            [BuildingEra.Era1990_2010] = (15, 30),
            [BuildingEra.Post2010] = (20, 38)
        };

        // Seismic retrofit costs by strategy ($/SF)
        private readonly Dictionary<SeismicRetrofitStrategy, double> _seismicCosts = new()
        {
            [SeismicRetrofitStrategy.Shearwalls] = 25,
            [SeismicRetrofitStrategy.BracedFrames] = 35,
            [SeismicRetrofitStrategy.MomentFrames] = 45,
            [SeismicRetrofitStrategy.BaseIsolation] = 80,
            [SeismicRetrofitStrategy.Dampers] = 60
        };

        // Energy measure costs ($/SF affected)
        private readonly Dictionary<EnergyMeasure, (double cost, double savings)> _energyCosts = new()
        {
            [EnergyMeasure.Insulation] = (8, 0.15),
            [EnergyMeasure.Windows] = (45, 0.20),
            [EnergyMeasure.HVAC] = (25, 0.30),
            [EnergyMeasure.Lighting] = (5, 0.25),
            [EnergyMeasure.Envelope] = (15, 0.12),
            [EnergyMeasure.Solar] = (20, 0.35)
        };

        private RetrofitIntelligenceEngine() { }

        public RetrofitProject CreateRetrofitProject(string projectId, string projectName,
            BuildingEra era, double grossArea, int floorCount, string originalUse, string proposedUse)
        {
            var project = new RetrofitProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Era = era,
                GrossArea = grossArea,
                FloorCount = floorCount,
                OriginalUse = originalUse,
                ProposedUse = proposedUse
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public async Task<BuildingAssessment> ConductAssessment(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var assessment = new BuildingAssessment();

                    // Structural assessment
                    assessment.Structural = AssessStructure(project);

                    // Envelope assessment
                    assessment.Envelope = AssessEnvelope(project);

                    // MEP assessment
                    assessment.MEP = AssessMEP(project);

                    // Accessibility assessment
                    assessment.Accessibility = AssessAccessibility(project);

                    // Environmental assessment
                    assessment.Environmental = AssessEnvironmental(project);

                    // Historic assessment (if applicable)
                    if (project.Era <= BuildingEra.Era1940_1970)
                    {
                        assessment.Historic = AssessHistoric(project);
                    }

                    // Calculate overall score
                    var scores = new List<double>
                    {
                        assessment.Structural.SeismicScore,
                        assessment.Envelope.Condition == ConditionRating.Good ? 80 :
                            assessment.Envelope.Condition == ConditionRating.Fair ? 60 : 40,
                        assessment.MEP.HVACCondition == ConditionRating.Good ? 80 :
                            assessment.MEP.HVACCondition == ConditionRating.Fair ? 60 : 40,
                        assessment.Accessibility.CompliancePercentage
                    };
                    assessment.OverallScore = scores.Average();

                    // Generate recommendations
                    if (assessment.Structural.SeismicScore < 60)
                        assessment.Recommendations.Add("Seismic retrofit strongly recommended");
                    if (assessment.Envelope.WallRValue < 13)
                        assessment.Recommendations.Add("Envelope insulation upgrade recommended");
                    if (assessment.MEP.HVACAge > 20)
                        assessment.Recommendations.Add("HVAC system replacement recommended");
                    if (assessment.Accessibility.CompliancePercentage < 80)
                        assessment.Recommendations.Add("ADA compliance improvements required");

                    project.Assessment = assessment;
                    return assessment;
                }
            });
        }

        private StructuralAssessment AssessStructure(RetrofitProject project)
        {
            var assessment = new StructuralAssessment
            {
                PrimarySystem = project.Era switch
                {
                    BuildingEra.Pre1940 => StructuralSystem.UnreinforcedMasonry,
                    BuildingEra.Era1940_1970 => StructuralSystem.NonDuctileConcrete,
                    BuildingEra.Era1970_1990 => StructuralSystem.SteelMoment,
                    _ => StructuralSystem.SteelMoment
                }
            };

            // Set condition based on era
            assessment.Condition = project.Era <= BuildingEra.Era1940_1970 ?
                ConditionRating.Fair : ConditionRating.Good;

            // Seismic score (higher is better)
            assessment.SeismicScore = project.Era switch
            {
                BuildingEra.Pre1940 => 30,
                BuildingEra.Era1940_1970 => 45,
                BuildingEra.Era1970_1990 => 60,
                BuildingEra.Era1990_2010 => 75,
                BuildingEra.Post2010 => 90,
                _ => 50
            };

            // Check for common deficiencies by era
            if (project.Era <= BuildingEra.Era1940_1970)
            {
                assessment.HasSoftStory = project.FloorCount > 1;
                assessment.Deficiencies.Add(new StructuralDeficiency
                {
                    Type = "Soft Story",
                    Description = "First floor parking or retail creates soft story condition",
                    Severity = "High",
                    RecommendedAction = "Add steel moment frames or braced frames at ground floor",
                    EstimatedCost = project.GrossArea * 15
                });
            }

            if (assessment.PrimarySystem == StructuralSystem.UnreinforcedMasonry)
            {
                assessment.Deficiencies.Add(new StructuralDeficiency
                {
                    Type = "URM Wall-Diaphragm Connection",
                    Description = "Inadequate connection between walls and floor/roof diaphragms",
                    Severity = "Critical",
                    RecommendedAction = "Install steel anchors and clips",
                    EstimatedCost = project.GrossArea * 8
                });
            }

            assessment.EstimatedRetrofitCost = assessment.Deficiencies.Sum(d => d.EstimatedCost);
            return assessment;
        }

        private EnvelopeAssessment AssessEnvelope(RetrofitProject project)
        {
            var typical = _typicalRValues.GetValueOrDefault(project.Era, (11, 19));

            var assessment = new EnvelopeAssessment
            {
                WallRValue = typical.wall,
                RoofRValue = typical.roof,
                WindowUValue = project.Era <= BuildingEra.Era1970_1990 ? 1.0 : 0.5,
                AirLeakage = project.Era <= BuildingEra.Era1970_1990 ? 0.5 : 0.25
            };

            // Determine condition
            assessment.Condition = project.Era switch
            {
                BuildingEra.Pre1940 => ConditionRating.Poor,
                BuildingEra.Era1940_1970 => ConditionRating.Fair,
                _ => ConditionRating.Good
            };

            // Add issues
            if (assessment.WallRValue < 13)
            {
                assessment.Issues.Add(new EnvelopeIssue
                {
                    Type = "Insufficient Insulation",
                    Description = $"Wall R-value of {assessment.WallRValue} below current code minimum",
                    AffectedArea = project.GrossArea * 0.4,
                    EnergyCost = project.GrossArea * 0.5 // $/year
                });
            }

            if (assessment.WindowUValue > 0.35)
            {
                assessment.Issues.Add(new EnvelopeIssue
                {
                    Type = "Inefficient Windows",
                    Description = $"Window U-value of {assessment.WindowUValue} well above current standards",
                    AffectedArea = project.GrossArea * 0.15,
                    EnergyCost = project.GrossArea * 0.3
                });
            }

            assessment.EnergyLoss = assessment.Issues.Sum(i => i.EnergyCost);
            assessment.EstimatedRetrofitCost = project.GrossArea * 25;
            return assessment;
        }

        private MEPAssessment AssessMEP(RetrofitProject project)
        {
            var assessment = new MEPAssessment
            {
                HVACAge = project.Era switch
                {
                    BuildingEra.Pre1940 => 40,
                    BuildingEra.Era1940_1970 => 30,
                    BuildingEra.Era1970_1990 => 25,
                    BuildingEra.Era1990_2010 => 15,
                    BuildingEra.Post2010 => 5,
                    _ => 20
                },
                HVACEfficiency = project.Era <= BuildingEra.Era1990_2010 ? 0.7 : 0.85,
                HasAsbestos = project.Era <= BuildingEra.Era1970_1990,
                HasLeadPaint = project.Era <= BuildingEra.Era1970_1990
            };

            assessment.HVACCondition = assessment.HVACAge > 20 ? ConditionRating.Poor :
                assessment.HVACAge > 15 ? ConditionRating.Fair : ConditionRating.Good;

            assessment.PlumbingCondition = project.Era <= BuildingEra.Era1970_1990 ?
                ConditionRating.Fair : ConditionRating.Good;

            assessment.ElectricalCondition = project.Era <= BuildingEra.Era1970_1990 ?
                ConditionRating.Fair : ConditionRating.Good;

            if (assessment.HVACAge > 20)
            {
                assessment.Deficiencies.Add(new MEPDeficiency
                {
                    System = "HVAC",
                    Description = "System beyond useful life, requiring replacement",
                    Impact = "High energy costs, reliability issues",
                    EstimatedCost = project.GrossArea * 30
                });
            }

            if (assessment.HasAsbestos)
            {
                assessment.Deficiencies.Add(new MEPDeficiency
                {
                    System = "Insulation",
                    Description = "Potential asbestos-containing materials in pipe/duct insulation",
                    Impact = "Must abate before renovation",
                    EstimatedCost = project.GrossArea * 5
                });
            }

            assessment.EstimatedRetrofitCost = assessment.Deficiencies.Sum(d => d.EstimatedCost);
            return assessment;
        }

        private AccessibilityAssessment AssessAccessibility(RetrofitProject project)
        {
            var assessment = new AccessibilityAssessment
            {
                HasElevator = project.FloorCount > 1 && project.Era >= BuildingEra.Era1990_2010,
                HasAccessibleEntrance = project.Era >= BuildingEra.Era1990_2010,
                HasAccessibleRoute = project.Era >= BuildingEra.Era1990_2010,
                HasAccessibleRestrooms = project.Era >= BuildingEra.Era1990_2010
            };

            // Calculate parking
            assessment.ParkingSpaces = (int)(project.GrossArea / 300);
            assessment.AccessibleParkingSpaces = project.Era >= BuildingEra.Era1990_2010 ?
                Math.Max(1, assessment.ParkingSpaces / 25) : 0;

            // Add barriers
            if (!assessment.HasAccessibleEntrance)
            {
                assessment.Barriers.Add(new AccessibilityBarrier
                {
                    Location = "Main Entrance",
                    Type = "Entry",
                    Description = "Steps at main entrance without ramp",
                    ADASection = "206.4",
                    RemovalCost = 15000,
                    Priority = 1
                });
            }

            if (!assessment.HasElevator && project.FloorCount > 1)
            {
                assessment.Barriers.Add(new AccessibilityBarrier
                {
                    Location = "Vertical Circulation",
                    Type = "Elevator",
                    Description = "No elevator access to upper floors",
                    ADASection = "206.2.3",
                    RemovalCost = 150000 * project.FloorCount,
                    Priority = 1
                });
            }

            if (!assessment.HasAccessibleRestrooms)
            {
                assessment.Barriers.Add(new AccessibilityBarrier
                {
                    Location = "Restrooms",
                    Type = "Restroom",
                    Description = "Restrooms do not meet accessibility requirements",
                    ADASection = "213",
                    RemovalCost = 25000,
                    Priority = 2
                });
            }

            assessment.CompliancePercentage = project.Era switch
            {
                BuildingEra.Pre1940 => 20,
                BuildingEra.Era1940_1970 => 30,
                BuildingEra.Era1970_1990 => 50,
                BuildingEra.Era1990_2010 => 80,
                BuildingEra.Post2010 => 95,
                _ => 50
            };

            assessment.EstimatedRetrofitCost = assessment.Barriers.Sum(b => b.RemovalCost);
            return assessment;
        }

        private EnvironmentalAssessment AssessEnvironmental(RetrofitProject project)
        {
            var assessment = new EnvironmentalAssessment
            {
                HasAsbestos = project.Era <= BuildingEra.Era1970_1990,
                HasLeadPaint = project.Era <= BuildingEra.Era1970_1990,
                HasPCBs = project.Era <= BuildingEra.Era1970_1990
            };

            if (assessment.HasAsbestos)
            {
                assessment.Hazards.Add(new HazardLocation
                {
                    Type = "Asbestos",
                    Location = "Pipe insulation, floor tiles, ceiling tiles",
                    Quantity = project.GrossArea * 0.1,
                    Unit = "SF",
                    AbatementCost = project.GrossArea * 0.1 * 25
                });
            }

            if (assessment.HasLeadPaint)
            {
                assessment.Hazards.Add(new HazardLocation
                {
                    Type = "Lead Paint",
                    Location = "Interior and exterior painted surfaces",
                    Quantity = project.GrossArea * 2,
                    Unit = "SF",
                    AbatementCost = project.GrossArea * 2 * 5
                });
            }

            assessment.AbatementCost = assessment.Hazards.Sum(h => h.AbatementCost);
            return assessment;
        }

        private HistoricAssessment AssessHistoric(RetrofitProject project)
        {
            return new HistoricAssessment
            {
                IsListed = project.Era == BuildingEra.Pre1940,
                Designation = project.Era == BuildingEra.Pre1940 ? "Potentially Eligible" : "Not Listed",
                CharacterDefiningFeatures = new List<string>
                {
                    "Original facade",
                    "Window patterns",
                    "Cornice details",
                    "Entry vestibule"
                },
                QualifiesForTaxCredits = project.Era == BuildingEra.Pre1940,
                PotentialTaxCredit = project.Era == BuildingEra.Pre1940 ? project.GrossArea * 50 * 0.20 : 0
            };
        }

        public SeismicRetrofit DesignSeismicRetrofit(string projectId, SeismicRetrofitStrategy strategy,
            double targetPerformance)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var retrofit = new SeismicRetrofit
                {
                    PrimaryStrategy = strategy,
                    TargetPerformanceLevel = targetPerformance
                };

                double baseCost = _seismicCosts.GetValueOrDefault(strategy, 30);

                // Add elements based on strategy
                switch (strategy)
                {
                    case SeismicRetrofitStrategy.Shearwalls:
                        int wallCount = (int)(project.GrossArea / 5000);
                        retrofit.Elements.Add(new SeismicElement
                        {
                            Type = "Concrete Shearwall",
                            Quantity = wallCount,
                            Cost = wallCount * 50000
                        });
                        break;

                    case SeismicRetrofitStrategy.BracedFrames:
                        int frameCount = (int)(project.GrossArea / 3000);
                        retrofit.Elements.Add(new SeismicElement
                        {
                            Type = "Steel Braced Frame",
                            Quantity = frameCount,
                            Cost = frameCount * 35000
                        });
                        break;

                    case SeismicRetrofitStrategy.BaseIsolation:
                        int isolatorCount = (int)(project.GrossArea / 500);
                        retrofit.Elements.Add(new SeismicElement
                        {
                            Type = "Base Isolator",
                            Quantity = isolatorCount,
                            Cost = isolatorCount * 20000
                        });
                        break;
                }

                retrofit.TotalCost = project.GrossArea * baseCost;
                retrofit.StrengthIncrease = strategy == SeismicRetrofitStrategy.BaseIsolation ? 0 : 1.5;
                retrofit.DriftReduction = strategy == SeismicRetrofitStrategy.BaseIsolation ? 0.7 : 0.4;

                return retrofit;
            }
        }

        public EnergyRetrofit DesignEnergyRetrofit(string projectId, double targetEUI)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var retrofit = new EnergyRetrofit
                {
                    CurrentEUI = project.Era switch
                    {
                        BuildingEra.Pre1940 => 150,
                        BuildingEra.Era1940_1970 => 120,
                        BuildingEra.Era1970_1990 => 100,
                        BuildingEra.Era1990_2010 => 80,
                        BuildingEra.Post2010 => 60,
                        _ => 100
                    },
                    TargetEUI = targetEUI
                };

                double euiReduction = retrofit.CurrentEUI - targetEUI;
                double totalSavings = 0;
                double totalCost = 0;

                // Add measures to achieve target
                if (project.Assessment?.Envelope?.WallRValue < 13)
                {
                    var measure = _energyCosts[EnergyMeasure.Insulation];
                    double savingsContribution = retrofit.CurrentEUI * measure.savings;
                    totalCost += project.GrossArea * measure.cost;
                    totalSavings += savingsContribution;
                    retrofit.Measures.Add(EnergyMeasure.Insulation);
                }

                if (project.Assessment?.Envelope?.WindowUValue > 0.35)
                {
                    var measure = _energyCosts[EnergyMeasure.Windows];
                    totalCost += project.GrossArea * 0.15 * measure.cost;
                    totalSavings += retrofit.CurrentEUI * measure.savings;
                    retrofit.Measures.Add(EnergyMeasure.Windows);
                }

                if (project.Assessment?.MEP?.HVACAge > 15)
                {
                    var measure = _energyCosts[EnergyMeasure.HVAC];
                    totalCost += project.GrossArea * measure.cost;
                    totalSavings += retrofit.CurrentEUI * measure.savings;
                    retrofit.Measures.Add(EnergyMeasure.HVAC);
                }

                retrofit.TotalCost = totalCost;
                retrofit.AnnualSavings = totalSavings * project.GrossArea * 0.12; // $/year
                retrofit.SimplePayback = retrofit.TotalCost / retrofit.AnnualSavings;
                retrofit.CarbonReduction = euiReduction * project.GrossArea * 0.0005; // tons CO2/year

                return retrofit;
            }
        }

        public AdaptiveReuseFeasibility AnalyzeAdaptiveReuse(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var feasibility = new AdaptiveReuseFeasibility
                {
                    OriginalUse = project.OriginalUse,
                    ProposedUse = project.ProposedUse
                };

                // Evaluate compatibility
                bool compatible = IsUseCompatible(project.OriginalUse, project.ProposedUse);
                feasibility.FeasibilityScore = compatible ? 75 : 50;

                // Add advantages/challenges based on conversion type
                if (project.OriginalUse == "Industrial" && project.ProposedUse == "Residential")
                {
                    feasibility.Advantages.Add("High ceilings provide loft-style living");
                    feasibility.Advantages.Add("Large floor plates allow flexible layouts");
                    feasibility.Advantages.Add("Industrial character appeals to market");
                    feasibility.Challenges.Add("HVAC and plumbing distribution required");
                    feasibility.Challenges.Add("Window additions may be needed");
                    feasibility.Challenges.Add("Environmental remediation likely required");
                }
                else if (project.OriginalUse == "Office" && project.ProposedUse == "Residential")
                {
                    feasibility.Advantages.Add("Floor plate depth typically suitable");
                    feasibility.Advantages.Add("Existing core can be adapted");
                    feasibility.Challenges.Add("Plumbing distribution significant cost");
                    feasibility.Challenges.Add("Window operation requirements");
                }

                // Estimate costs
                feasibility.EstimatedCost = project.GrossArea * (compatible ? 150 : 200);
                feasibility.EstimatedValue = project.GrossArea * 250;
                feasibility.ROI = (feasibility.EstimatedValue - feasibility.EstimatedCost) / feasibility.EstimatedCost;

                return feasibility;
            }
        }

        private bool IsUseCompatible(string original, string proposed)
        {
            var compatiblePairs = new HashSet<(string, string)>
            {
                ("Office", "Residential"),
                ("Industrial", "Residential"),
                ("Industrial", "Office"),
                ("Retail", "Office"),
                ("Hotel", "Residential"),
                ("School", "Office"),
                ("Church", "Residential")
            };

            return compatiblePairs.Contains((original, proposed));
        }

        public CostEstimate GenerateCostEstimate(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var estimate = new CostEstimate();

                // Sum up scope costs
                foreach (var scope in project.Scopes)
                {
                    estimate.HardCosts += scope.TotalCost;
                    estimate.CostByScope[scope.Type] = scope.TotalCost;
                }

                // Add assessment-based costs if no scopes defined
                if (project.Assessment != null && estimate.HardCosts == 0)
                {
                    estimate.HardCosts = project.Assessment.Structural?.EstimatedRetrofitCost ?? 0;
                    estimate.HardCosts += project.Assessment.Envelope?.EstimatedRetrofitCost ?? 0;
                    estimate.HardCosts += project.Assessment.MEP?.EstimatedRetrofitCost ?? 0;
                    estimate.HardCosts += project.Assessment.Accessibility?.EstimatedRetrofitCost ?? 0;
                    estimate.HardCosts += project.Assessment.Environmental?.AbatementCost ?? 0;
                }

                estimate.SoftCosts = estimate.HardCosts * 0.25;
                estimate.Contingency = estimate.HardCosts * 0.15;
                estimate.TotalCost = estimate.HardCosts + estimate.SoftCosts + estimate.Contingency;
                estimate.CostPerSF = estimate.TotalCost / project.GrossArea;

                estimate.Assumptions.Add("Costs based on Q1 2026 pricing");
                estimate.Assumptions.Add("Assumes standard market conditions");
                estimate.Assumptions.Add("Excludes owner-furnished equipment");

                project.Cost = estimate;
                return estimate;
            }
        }
    }
}
