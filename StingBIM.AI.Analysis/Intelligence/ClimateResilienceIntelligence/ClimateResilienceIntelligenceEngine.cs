// ===================================================================
// StingBIM Climate Resilience Intelligence Engine
// Climate adaptation, extreme weather, future-proofing, vulnerability
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ClimateResilienceIntelligence
{
    #region Enums

    public enum ClimateScenario { RCP26, RCP45, RCP60, RCP85 }
    public enum HazardType { ExtremeHeat, Flooding, Drought, Wildfire, WindStorm, SeaLevelRise, Permafrost }
    public enum TimeHorizon { Near2030, Mid2050, Long2080, EndCentury2100 }
    public enum VulnerabilityLevel { Low, Moderate, High, Critical }
    public enum AdaptationType { Structural, Operational, Behavioral, Systemic }
    public enum ResilienceCategory { Building, Infrastructure, Community, Ecosystem }

    #endregion

    #region Data Models

    public class ClimateAssessment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public Location ProjectLocation { get; set; }
        public ClimateScenario Scenario { get; set; }
        public TimeHorizon Horizon { get; set; }
        public List<ClimateProjection> Projections { get; set; } = new();
        public List<HazardExposure> Exposures { get; set; } = new();
        public VulnerabilityAnalysis Vulnerability { get; set; }
        public List<AdaptationStrategy> Strategies { get; set; } = new();
        public ResilienceScore Score { get; set; }
        public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;
    }

    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public string ClimateZone { get; set; }
        public string CoastalProximity { get; set; }
        public string FloodZone { get; set; }
        public string SeismicZone { get; set; }
    }

    public class ClimateProjection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Parameter { get; set; }
        public TimeHorizon Horizon { get; set; }
        public double BaselineValue { get; set; }
        public double ProjectedValue { get; set; }
        public double ChangePercent { get; set; }
        public double ConfidenceLevel { get; set; }
        public string Unit { get; set; }
        public string Source { get; set; }
    }

    public class HazardExposure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public HazardType Hazard { get; set; }
        public double CurrentProbability { get; set; }
        public double FutureProbability { get; set; }
        public double Intensity { get; set; }
        public double Frequency { get; set; }
        public double Duration { get; set; }
        public List<string> AffectedSystems { get; set; } = new();
        public double EconomicImpact { get; set; }
        public VulnerabilityLevel ExposureLevel { get; set; }
    }

    public class VulnerabilityAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double SensitivityScore { get; set; }
        public double AdaptiveCapacity { get; set; }
        public VulnerabilityLevel OverallLevel { get; set; }
        public List<VulnerableAsset> Assets { get; set; } = new();
        public List<CriticalThreshold> Thresholds { get; set; } = new();
        public List<string> KeyVulnerabilities { get; set; } = new();
    }

    public class VulnerableAsset
    {
        public string AssetId { get; set; }
        public string AssetName { get; set; }
        public string AssetType { get; set; }
        public List<HazardType> ExposedHazards { get; set; } = new();
        public double Sensitivity { get; set; }
        public double Criticality { get; set; }
        public VulnerabilityLevel Level { get; set; }
        public List<string> RequiredAdaptations { get; set; } = new();
    }

    public class CriticalThreshold
    {
        public string Parameter { get; set; }
        public double DesignValue { get; set; }
        public double ThresholdValue { get; set; }
        public double ProjectedValue { get; set; }
        public bool ExceedsThreshold { get; set; }
        public TimeHorizon ExceedanceYear { get; set; }
        public string Impact { get; set; }
    }

    public class AdaptationStrategy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public AdaptationType Type { get; set; }
        public ResilienceCategory Category { get; set; }
        public List<HazardType> AddressedHazards { get; set; } = new();
        public double ImplementationCost { get; set; }
        public double RiskReduction { get; set; }
        public double BenefitCostRatio { get; set; }
        public string Implementation { get; set; }
        public int Priority { get; set; }
        public List<string> CobenefIts { get; set; } = new();
        public bool IsNoRegret { get; set; }
    }

    public class ResilienceScore
    {
        public double OverallScore { get; set; }
        public double RobustnessScore { get; set; }
        public double RedundancyScore { get; set; }
        public double ResourcefulnessScore { get; set; }
        public double RapidityScore { get; set; }
        public string Rating { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public Dictionary<HazardType, double> HazardScores { get; set; } = new();
    }

    public class BuildingEnvelopeAssessment
    {
        public string BuildingId { get; set; }
        public double ThermalMassAdequacy { get; set; }
        public double InsulationEffectiveness { get; set; }
        public double VentilationCapacity { get; set; }
        public double ShadingEfficiency { get; set; }
        public double MoistureResistance { get; set; }
        public double WindResistance { get; set; }
        public List<string> Upgrades { get; set; } = new();
        public double AdaptationCost { get; set; }
    }

    public class FloodRiskAssessment
    {
        public string SiteId { get; set; }
        public double BaseFloodElevation { get; set; }
        public double FinishedFloorElevation { get; set; }
        public double Freeboard { get; set; }
        public double AnnualFloodProbability { get; set; }
        public double FutureFloodProbability { get; set; }
        public double MaxFloodDepth { get; set; }
        public double FloodDuration { get; set; }
        public List<string> MitigationMeasures { get; set; } = new();
        public bool MeetsFutureStandards { get; set; }
    }

    public class HeatStressAnalysis
    {
        public string BuildingId { get; set; }
        public double CurrentCoolingCapacity { get; set; }
        public double RequiredCoolingCapacity { get; set; }
        public int OverheatingHours { get; set; }
        public int FutureOverheatingHours { get; set; }
        public double PassiveCoolingPotential { get; set; }
        public List<string> HeatMitigation { get; set; } = new();
        public double HealthRisk { get; set; }
        public double ProductivityImpact { get; set; }
    }

    #endregion

    public sealed class ClimateResilienceIntelligenceEngine
    {
        private static readonly Lazy<ClimateResilienceIntelligenceEngine> _instance =
            new Lazy<ClimateResilienceIntelligenceEngine>(() => new ClimateResilienceIntelligenceEngine());
        public static ClimateResilienceIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ClimateAssessment> _assessments = new();
        private readonly object _lock = new object();

        // Climate projection data by scenario
        private readonly Dictionary<ClimateScenario, Dictionary<TimeHorizon, double>> _tempIncrease = new()
        {
            [ClimateScenario.RCP26] = new() { [TimeHorizon.Near2030] = 0.8, [TimeHorizon.Mid2050] = 1.2, [TimeHorizon.Long2080] = 1.4, [TimeHorizon.EndCentury2100] = 1.5 },
            [ClimateScenario.RCP45] = new() { [TimeHorizon.Near2030] = 0.9, [TimeHorizon.Mid2050] = 1.6, [TimeHorizon.Long2080] = 2.2, [TimeHorizon.EndCentury2100] = 2.4 },
            [ClimateScenario.RCP60] = new() { [TimeHorizon.Near2030] = 0.9, [TimeHorizon.Mid2050] = 1.7, [TimeHorizon.Long2080] = 2.6, [TimeHorizon.EndCentury2100] = 3.0 },
            [ClimateScenario.RCP85] = new() { [TimeHorizon.Near2030] = 1.0, [TimeHorizon.Mid2050] = 2.2, [TimeHorizon.Long2080] = 3.7, [TimeHorizon.EndCentury2100] = 4.8 }
        };

        private ClimateResilienceIntelligenceEngine() { }

        public ClimateAssessment CreateAssessment(string projectId, string projectName,
            Location location, ClimateScenario scenario, TimeHorizon horizon)
        {
            var assessment = new ClimateAssessment
            {
                ProjectId = projectId,
                ProjectName = projectName,
                ProjectLocation = location,
                Scenario = scenario,
                Horizon = horizon
            };

            // Generate climate projections
            assessment.Projections = GenerateProjections(location, scenario, horizon);

            lock (_lock) { _assessments[assessment.Id] = assessment; }
            return assessment;
        }

        private List<ClimateProjection> GenerateProjections(Location location, ClimateScenario scenario, TimeHorizon horizon)
        {
            var projections = new List<ClimateProjection>();
            double tempIncrease = _tempIncrease[scenario][horizon];

            projections.Add(new ClimateProjection
            {
                Parameter = "Mean Annual Temperature",
                Horizon = horizon,
                BaselineValue = 20.0,
                ProjectedValue = 20.0 + tempIncrease,
                ChangePercent = tempIncrease * 5,
                ConfidenceLevel = 0.9,
                Unit = "°C",
                Source = "IPCC AR6"
            });

            projections.Add(new ClimateProjection
            {
                Parameter = "Extreme Heat Days (>35°C)",
                Horizon = horizon,
                BaselineValue = 15,
                ProjectedValue = 15 + (tempIncrease * 8),
                ChangePercent = tempIncrease * 50,
                ConfidenceLevel = 0.85,
                Unit = "days/year",
                Source = "IPCC AR6"
            });

            projections.Add(new ClimateProjection
            {
                Parameter = "Annual Precipitation",
                Horizon = horizon,
                BaselineValue = 800,
                ProjectedValue = 800 * (1 + (tempIncrease * 0.03)),
                ChangePercent = tempIncrease * 3,
                ConfidenceLevel = 0.7,
                Unit = "mm/year",
                Source = "IPCC AR6"
            });

            projections.Add(new ClimateProjection
            {
                Parameter = "Intense Rainfall Events",
                Horizon = horizon,
                BaselineValue = 5,
                ProjectedValue = 5 + (tempIncrease * 2),
                ChangePercent = tempIncrease * 40,
                ConfidenceLevel = 0.75,
                Unit = "events/year",
                Source = "IPCC AR6"
            });

            projections.Add(new ClimateProjection
            {
                Parameter = "Sea Level Rise",
                Horizon = horizon,
                BaselineValue = 0,
                ProjectedValue = tempIncrease * 0.15,
                ChangePercent = 100,
                ConfidenceLevel = 0.8,
                Unit = "meters",
                Source = "IPCC AR6"
            });

            return projections;
        }

        public async Task<List<HazardExposure>> AnalyzeHazards(string assessmentId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_assessments.TryGetValue(assessmentId, out var assessment))
                        return new List<HazardExposure>();

                    var exposures = new List<HazardExposure>();
                    var location = assessment.ProjectLocation;
                    double tempIncrease = _tempIncrease[assessment.Scenario][assessment.Horizon];

                    // Extreme Heat
                    exposures.Add(new HazardExposure
                    {
                        Hazard = HazardType.ExtremeHeat,
                        CurrentProbability = 0.3,
                        FutureProbability = 0.3 + (tempIncrease * 0.1),
                        Intensity = tempIncrease,
                        Frequency = 15 + (tempIncrease * 8),
                        Duration = 3 + tempIncrease,
                        AffectedSystems = new List<string> { "HVAC", "Envelope", "Occupant Health", "Energy" },
                        EconomicImpact = tempIncrease * 50000,
                        ExposureLevel = tempIncrease > 3 ? VulnerabilityLevel.Critical : VulnerabilityLevel.High
                    });

                    // Flooding
                    bool isFloodZone = location.FloodZone?.Contains("A") == true || location.FloodZone?.Contains("V") == true;
                    exposures.Add(new HazardExposure
                    {
                        Hazard = HazardType.Flooding,
                        CurrentProbability = isFloodZone ? 0.1 : 0.01,
                        FutureProbability = isFloodZone ? 0.15 + (tempIncrease * 0.02) : 0.02 + (tempIncrease * 0.01),
                        Intensity = 1.2,
                        Frequency = 1 + tempIncrease * 0.5,
                        Duration = 2,
                        AffectedSystems = new List<string> { "Foundation", "Electrical", "HVAC", "Finishes" },
                        EconomicImpact = isFloodZone ? 500000 : 100000,
                        ExposureLevel = isFloodZone ? VulnerabilityLevel.High : VulnerabilityLevel.Moderate
                    });

                    // Wind Storm
                    bool isCoastal = location.CoastalProximity == "Coastal";
                    exposures.Add(new HazardExposure
                    {
                        Hazard = HazardType.WindStorm,
                        CurrentProbability = isCoastal ? 0.15 : 0.05,
                        FutureProbability = isCoastal ? 0.2 + (tempIncrease * 0.02) : 0.07,
                        Intensity = 1.1 + (tempIncrease * 0.05),
                        Frequency = 2 + tempIncrease * 0.3,
                        Duration = 0.5,
                        AffectedSystems = new List<string> { "Roof", "Cladding", "Glazing", "Signage" },
                        EconomicImpact = isCoastal ? 300000 : 80000,
                        ExposureLevel = isCoastal ? VulnerabilityLevel.High : VulnerabilityLevel.Moderate
                    });

                    // Sea Level Rise (coastal only)
                    if (isCoastal && location.Elevation < 10)
                    {
                        exposures.Add(new HazardExposure
                        {
                            Hazard = HazardType.SeaLevelRise,
                            CurrentProbability = 0.05,
                            FutureProbability = tempIncrease * 0.08,
                            Intensity = tempIncrease * 0.15,
                            Frequency = 1,
                            Duration = 365,
                            AffectedSystems = new List<string> { "Site", "Foundation", "Utilities", "Access" },
                            EconomicImpact = tempIncrease * 200000,
                            ExposureLevel = VulnerabilityLevel.Critical
                        });
                    }

                    assessment.Exposures = exposures;
                    return exposures;
                }
            });
        }

        public VulnerabilityAnalysis AssessVulnerability(string assessmentId, List<VulnerableAsset> assets)
        {
            lock (_lock)
            {
                if (!_assessments.TryGetValue(assessmentId, out var assessment))
                    return null;

                var vulnerability = new VulnerabilityAnalysis
                {
                    Assets = assets
                };

                // Calculate sensitivity based on assets
                if (assets.Any())
                {
                    vulnerability.SensitivityScore = assets.Average(a => a.Sensitivity);
                }

                // Evaluate adaptive capacity
                vulnerability.AdaptiveCapacity = 0.6; // Base capacity, would be refined

                // Determine overall level
                double vulnScore = vulnerability.SensitivityScore * (1 - vulnerability.AdaptiveCapacity);
                vulnerability.OverallLevel = vulnScore switch
                {
                    < 0.25 => VulnerabilityLevel.Low,
                    < 0.5 => VulnerabilityLevel.Moderate,
                    < 0.75 => VulnerabilityLevel.High,
                    _ => VulnerabilityLevel.Critical
                };

                // Identify thresholds
                double tempIncrease = _tempIncrease[assessment.Scenario][assessment.Horizon];
                vulnerability.Thresholds.Add(new CriticalThreshold
                {
                    Parameter = "Design Temperature",
                    DesignValue = 35,
                    ThresholdValue = 38,
                    ProjectedValue = 35 + tempIncrease,
                    ExceedsThreshold = 35 + tempIncrease > 38,
                    Impact = "HVAC undersizing, thermal comfort failures"
                });

                vulnerability.Thresholds.Add(new CriticalThreshold
                {
                    Parameter = "Rainfall Intensity",
                    DesignValue = 50,
                    ThresholdValue = 75,
                    ProjectedValue = 50 * (1 + tempIncrease * 0.15),
                    ExceedsThreshold = 50 * (1 + tempIncrease * 0.15) > 75,
                    Impact = "Stormwater system overflow, flooding risk"
                });

                // Key vulnerabilities
                vulnerability.KeyVulnerabilities = new List<string>();
                foreach (var threshold in vulnerability.Thresholds.Where(t => t.ExceedsThreshold))
                {
                    vulnerability.KeyVulnerabilities.Add($"{threshold.Parameter}: {threshold.Impact}");
                }

                foreach (var asset in assets.Where(a => a.Level == VulnerabilityLevel.Critical))
                {
                    vulnerability.KeyVulnerabilities.Add($"Critical asset: {asset.AssetName}");
                }

                assessment.Vulnerability = vulnerability;
                return vulnerability;
            }
        }

        public List<AdaptationStrategy> GenerateAdaptationStrategies(string assessmentId)
        {
            lock (_lock)
            {
                if (!_assessments.TryGetValue(assessmentId, out var assessment))
                    return new List<AdaptationStrategy>();

                var strategies = new List<AdaptationStrategy>();

                // Heat adaptation strategies
                if (assessment.Exposures.Any(e => e.Hazard == HazardType.ExtremeHeat))
                {
                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Enhanced Building Envelope",
                        Description = "Improve insulation, cool roofs, shading devices",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.ExtremeHeat },
                        ImplementationCost = 150000,
                        RiskReduction = 0.4,
                        BenefitCostRatio = 2.5,
                        Priority = 1,
                        CobenefIts = new List<string> { "Energy savings", "Comfort improvement", "Lower cooling costs" },
                        IsNoRegret = true
                    });

                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Passive Cooling Design",
                        Description = "Natural ventilation, thermal mass, night cooling",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.ExtremeHeat },
                        ImplementationCost = 80000,
                        RiskReduction = 0.3,
                        BenefitCostRatio = 3.2,
                        Priority = 2,
                        CobenefIts = new List<string> { "Reduced energy dependency", "Resilience during outages" },
                        IsNoRegret = true
                    });

                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "HVAC Capacity Increase",
                        Description = "Upsize cooling systems for future temperatures",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.ExtremeHeat },
                        ImplementationCost = 200000,
                        RiskReduction = 0.5,
                        BenefitCostRatio = 1.8,
                        Priority = 3,
                        CobenefIts = new List<string> { "Future-proofing", "Occupant health" },
                        IsNoRegret = false
                    });
                }

                // Flood adaptation strategies
                if (assessment.Exposures.Any(e => e.Hazard == HazardType.Flooding))
                {
                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Elevated Critical Systems",
                        Description = "Raise electrical, HVAC above flood level",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.Flooding },
                        ImplementationCost = 120000,
                        RiskReduction = 0.6,
                        BenefitCostRatio = 2.8,
                        Priority = 1,
                        CobenefIts = new List<string> { "Faster recovery", "Reduced damage" },
                        IsNoRegret = true
                    });

                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Flood-Resilient Materials",
                        Description = "Water-resistant finishes below flood line",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.Flooding },
                        ImplementationCost = 60000,
                        RiskReduction = 0.35,
                        BenefitCostRatio = 3.5,
                        Priority = 2,
                        CobenefIts = new List<string> { "Durability", "Lower maintenance" },
                        IsNoRegret = true
                    });

                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Enhanced Stormwater Management",
                        Description = "Rain gardens, permeable paving, detention",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Infrastructure,
                        AddressedHazards = new List<HazardType> { HazardType.Flooding },
                        ImplementationCost = 180000,
                        RiskReduction = 0.45,
                        BenefitCostRatio = 2.2,
                        Priority = 2,
                        CobenefIts = new List<string> { "Water quality", "Urban cooling", "Biodiversity" },
                        IsNoRegret = true
                    });
                }

                // Wind adaptation strategies
                if (assessment.Exposures.Any(e => e.Hazard == HazardType.WindStorm))
                {
                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Enhanced Roof Attachment",
                        Description = "Stronger roof-to-wall connections",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.WindStorm },
                        ImplementationCost = 45000,
                        RiskReduction = 0.5,
                        BenefitCostRatio = 4.0,
                        Priority = 1,
                        CobenefIts = new List<string> { "Structural integrity", "Insurance reduction" },
                        IsNoRegret = true
                    });

                    strategies.Add(new AdaptationStrategy
                    {
                        Name = "Impact-Resistant Glazing",
                        Description = "Laminated glass, hurricane shutters",
                        Type = AdaptationType.Structural,
                        Category = ResilienceCategory.Building,
                        AddressedHazards = new List<HazardType> { HazardType.WindStorm },
                        ImplementationCost = 80000,
                        RiskReduction = 0.4,
                        BenefitCostRatio = 2.5,
                        Priority = 2,
                        CobenefIts = new List<string> { "Security", "Noise reduction", "UV protection" },
                        IsNoRegret = false
                    });
                }

                // Calculate priority based on benefit-cost ratio
                int priority = 1;
                foreach (var strategy in strategies.OrderByDescending(s => s.BenefitCostRatio))
                {
                    strategy.Priority = priority++;
                }

                assessment.Strategies = strategies;
                return strategies;
            }
        }

        public ResilienceScore CalculateResilienceScore(string assessmentId)
        {
            lock (_lock)
            {
                if (!_assessments.TryGetValue(assessmentId, out var assessment))
                    return null;

                var score = new ResilienceScore
                {
                    HazardScores = new Dictionary<HazardType, double>()
                };

                // Calculate 4R scores
                var strategies = assessment.Strategies;
                var vulnerability = assessment.Vulnerability;

                // Robustness - structural capacity to withstand
                score.RobustnessScore = strategies.Where(s => s.Type == AdaptationType.Structural)
                    .Sum(s => s.RiskReduction) / Math.Max(1, assessment.Exposures.Count) * 100;

                // Redundancy - backup systems
                score.RedundancyScore = strategies.Count(s => s.IsNoRegret) * 15;

                // Resourcefulness - ability to manage
                score.ResourcefulnessScore = vulnerability?.AdaptiveCapacity * 100 ?? 50;

                // Rapidity - recovery speed
                score.RapidityScore = strategies.Where(s => s.Category == ResilienceCategory.Building)
                    .Sum(s => s.RiskReduction) * 80;

                // Cap scores at 100
                score.RobustnessScore = Math.Min(100, score.RobustnessScore);
                score.RedundancyScore = Math.Min(100, score.RedundancyScore);
                score.ResourcefulnessScore = Math.Min(100, score.ResourcefulnessScore);
                score.RapidityScore = Math.Min(100, score.RapidityScore);

                // Overall score
                score.OverallScore = (score.RobustnessScore + score.RedundancyScore +
                    score.ResourcefulnessScore + score.RapidityScore) / 4;

                // Rating
                score.Rating = score.OverallScore switch
                {
                    >= 80 => "Excellent",
                    >= 60 => "Good",
                    >= 40 => "Fair",
                    >= 20 => "Poor",
                    _ => "Critical"
                };

                // Per-hazard scores
                foreach (var exposure in assessment.Exposures)
                {
                    double hazardMitigation = strategies
                        .Where(s => s.AddressedHazards.Contains(exposure.Hazard))
                        .Sum(s => s.RiskReduction);
                    score.HazardScores[exposure.Hazard] = Math.Min(100, hazardMitigation * 100);
                }

                // Strengths and weaknesses
                if (score.RobustnessScore >= 70) score.Strengths.Add("Strong structural resilience");
                if (score.RedundancyScore >= 70) score.Strengths.Add("Good backup systems");
                if (strategies.Any(s => s.IsNoRegret)) score.Strengths.Add("No-regret strategies identified");

                if (score.RobustnessScore < 50) score.Weaknesses.Add("Limited structural adaptations");
                if (score.RedundancyScore < 50) score.Weaknesses.Add("Insufficient redundancy");
                if (vulnerability?.OverallLevel == VulnerabilityLevel.Critical)
                    score.Weaknesses.Add("Critical vulnerabilities remain");

                assessment.Score = score;
                return score;
            }
        }

        public BuildingEnvelopeAssessment AssessEnvelope(string buildingId, double designTemp,
            double insulation, double thermalMass, double shadingCoef)
        {
            return new BuildingEnvelopeAssessment
            {
                BuildingId = buildingId,
                ThermalMassAdequacy = thermalMass >= 300 ? 1.0 : thermalMass / 300,
                InsulationEffectiveness = insulation >= 3.5 ? 1.0 : insulation / 3.5,
                VentilationCapacity = 0.8,
                ShadingEfficiency = 1 - shadingCoef,
                MoistureResistance = 0.85,
                WindResistance = 0.9,
                Upgrades = new List<string>
                {
                    thermalMass < 300 ? "Increase thermal mass" : null,
                    insulation < 3.5 ? "Improve insulation" : null,
                    shadingCoef > 0.4 ? "Add external shading" : null
                }.Where(u => u != null).ToList(),
                AdaptationCost = (thermalMass < 300 ? 30000 : 0) +
                    (insulation < 3.5 ? 25000 : 0) +
                    (shadingCoef > 0.4 ? 20000 : 0)
            };
        }

        public FloodRiskAssessment AssessFloodRisk(string siteId, double bfe, double ffe,
            double currentProb, double futureClimateMultiplier)
        {
            double freeboard = ffe - bfe;
            bool meetsFuture = freeboard >= 0.6; // 600mm freeboard for future

            return new FloodRiskAssessment
            {
                SiteId = siteId,
                BaseFloodElevation = bfe,
                FinishedFloorElevation = ffe,
                Freeboard = freeboard,
                AnnualFloodProbability = currentProb,
                FutureFloodProbability = currentProb * futureClimateMultiplier,
                MaxFloodDepth = Math.Max(0, bfe - ffe + 0.3),
                FloodDuration = 24,
                MitigationMeasures = new List<string>
                {
                    freeboard < 0.3 ? "Raise floor elevation" : null,
                    freeboard < 0.6 ? "Install flood barriers" : null,
                    "Elevate electrical systems",
                    "Install backflow preventers",
                    "Use flood-resistant materials below BFE+1m"
                }.Where(m => m != null).ToList(),
                MeetsFutureStandards = meetsFuture
            };
        }

        public HeatStressAnalysis AnalyzeHeatStress(string buildingId, double coolingCapacity,
            double floorArea, double designTemp, double futureTemp)
        {
            double currentLoad = floorArea * 100; // W/m2 estimate
            double futureLoad = currentLoad * (1 + (futureTemp - designTemp) * 0.1);
            int baseOverheating = 50;
            int futureOverheating = (int)(baseOverheating * (1 + (futureTemp - designTemp) * 0.3));

            return new HeatStressAnalysis
            {
                BuildingId = buildingId,
                CurrentCoolingCapacity = coolingCapacity,
                RequiredCoolingCapacity = futureLoad,
                OverheatingHours = baseOverheating,
                FutureOverheatingHours = futureOverheating,
                PassiveCoolingPotential = 0.3,
                HeatMitigation = new List<string>
                {
                    "Night purge ventilation",
                    "External shading devices",
                    "Cool roof coating",
                    "Increase thermal mass",
                    coolingCapacity < futureLoad ? "Upgrade HVAC capacity" : null
                }.Where(m => m != null).ToList(),
                HealthRisk = futureOverheating > 200 ? 0.8 : futureOverheating / 250.0,
                ProductivityImpact = futureOverheating * 0.001
            };
        }

        public double EstimateAdaptationCost(string assessmentId)
        {
            lock (_lock)
            {
                if (!_assessments.TryGetValue(assessmentId, out var assessment))
                    return 0;

                return assessment.Strategies.Sum(s => s.ImplementationCost);
            }
        }

        public double CalculateClimateRisk(string assessmentId)
        {
            lock (_lock)
            {
                if (!_assessments.TryGetValue(assessmentId, out var assessment))
                    return 0;

                double totalRisk = 0;
                foreach (var exposure in assessment.Exposures)
                {
                    double hazardRisk = exposure.FutureProbability * exposure.EconomicImpact;
                    totalRisk += hazardRisk;
                }

                // Reduce by mitigation
                double mitigated = assessment.Strategies.Sum(s => s.RiskReduction);
                return totalRisk * (1 - Math.Min(0.8, mitigated));
            }
        }
    }
}
