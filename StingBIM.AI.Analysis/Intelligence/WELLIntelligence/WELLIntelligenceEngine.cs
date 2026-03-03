// ===================================================================
// StingBIM WELL Intelligence Engine
// WELL Building Standard v2, health-focused design, feature tracking
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.WELLIntelligence
{
    #region Enums

    public enum WELLVersion { v1, v2 }
    public enum WELLConcept { Air, Water, Nourishment, Light, Movement, ThermalComfort, Sound, Materials, Mind, Community }
    public enum WELLLevel { Bronze, Silver, Gold, Platinum }
    public enum FeatureType { Precondition, Optimization }
    public enum FeatureStatus { NotPursued, Pursuing, Achieved, Pending }
    public enum SpaceType { Office, Retail, Restaurant, Multifamily, Educational, Commercial_Kitchen }

    #endregion

    #region Data Models

    public class WELLProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public WELLVersion Version { get; set; } = WELLVersion.v2;
        public SpaceType PrimarySpaceType { get; set; }
        public double GrossArea { get; set; }
        public int Occupancy { get; set; }
        public WELLLevel TargetLevel { get; set; }
        public List<WELLFeature> Features { get; set; } = new();
        public WELLScorecard Scorecard { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WELLFeature
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FeatureId { get; set; }
        public string Name { get; set; }
        public WELLConcept Concept { get; set; }
        public FeatureType Type { get; set; }
        public int MaxPoints { get; set; }
        public int AchievedPoints { get; set; }
        public FeatureStatus Status { get; set; }
        public List<FeaturePart> Parts { get; set; } = new();
        public string Intent { get; set; }
        public List<string> Evidence { get; set; } = new();
        public string ResponsibleParty { get; set; }
    }

    public class FeaturePart
    {
        public string PartId { get; set; }
        public string Name { get; set; }
        public int Points { get; set; }
        public bool IsRequired { get; set; }
        public bool IsAchieved { get; set; }
        public List<string> Requirements { get; set; } = new();
        public string VerificationMethod { get; set; }
    }

    public class WELLScorecard
    {
        public int TotalPossiblePoints { get; set; }
        public int AchievedPoints { get; set; }
        public int PreconditionsTotal { get; set; }
        public int PreconditionsMet { get; set; }
        public bool AllPreconditionsMet => PreconditionsMet >= PreconditionsTotal;
        public WELLLevel CurrentLevel { get; set; }
        public WELLLevel ProjectedLevel { get; set; }
        public Dictionary<WELLConcept, ConceptScore> ConceptScores { get; set; } = new();
    }

    public class ConceptScore
    {
        public WELLConcept Concept { get; set; }
        public int MaxPoints { get; set; }
        public int AchievedPoints { get; set; }
        public int PreconditionsRequired { get; set; }
        public int PreconditionsMet { get; set; }
        public double Percentage => MaxPoints > 0 ? AchievedPoints * 100.0 / MaxPoints : 0;
    }

    public class AirQualityAnalysis
    {
        public double PM2_5 { get; set; }
        public double PM10 { get; set; }
        public double CO2 { get; set; }
        public double TVOC { get; set; }
        public double Formaldehyde { get; set; }
        public double Ozone { get; set; }
        public double Radon { get; set; }
        public bool MeetsPreconditions { get; set; }
        public int AirPoints { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class LightingAnalysis
    {
        public double WorkplaneIlluminance { get; set; }
        public double MelanopicLux { get; set; }
        public double CircadianLightingRatio { get; set; }
        public double DaylightFactor { get; set; }
        public double GlareControl { get; set; }
        public bool HasElectricLightGlareControl { get; set; }
        public bool HasDaylightGlareControl { get; set; }
        public int LightPoints { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class ThermalComfortAnalysis
    {
        public double OperativeTemp { get; set; }
        public double RelativeHumidity { get; set; }
        public double AirSpeed { get; set; }
        public double RadiantAsymmetry { get; set; }
        public double PPD { get; set; }
        public bool MeetsASHRAE55 { get; set; }
        public bool HasThermalZoning { get; set; }
        public bool HasIndividualControl { get; set; }
        public int ThermalPoints { get; set; }
    }

    public class AcousticAnalysis
    {
        public double BackgroundNoise { get; set; }
        public double ReverberationTime { get; set; }
        public double STC { get; set; }
        public double IIC { get; set; }
        public bool MeetsSoundMasking { get; set; }
        public bool HasReverbControl { get; set; }
        public int SoundPoints { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class MovementAnalysis
    {
        public bool HasActiveStairway { get; set; }
        public double StairwayVisibility { get; set; }
        public bool HasFitnessEquipment { get; set; }
        public double FitnessSpaceRatio { get; set; }
        public bool HasActiveWorkstations { get; set; }
        public double ActiveFurnishingPercentage { get; set; }
        public bool HasBikeStorage { get; set; }
        public int MovementPoints { get; set; }
    }

    public class HealthMetrics
    {
        public string ProjectId { get; set; }
        public double AirQualityScore { get; set; }
        public double WaterQualityScore { get; set; }
        public double LightingScore { get; set; }
        public double ThermalComfortScore { get; set; }
        public double AcousticScore { get; set; }
        public double BiophiliaScore { get; set; }
        public double OverallHealthScore { get; set; }
        public List<string> TopRecommendations { get; set; } = new();
    }

    #endregion

    public sealed class WELLIntelligenceEngine
    {
        private static readonly Lazy<WELLIntelligenceEngine> _instance =
            new Lazy<WELLIntelligenceEngine>(() => new WELLIntelligenceEngine());
        public static WELLIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, WELLProject> _projects = new();
        private readonly object _lock = new object();

        // WELL v2 Point Thresholds
        private readonly Dictionary<WELLLevel, int> _levelThresholds = new()
        {
            [WELLLevel.Bronze] = 40,
            [WELLLevel.Silver] = 50,
            [WELLLevel.Gold] = 60,
            [WELLLevel.Platinum] = 80
        };

        // Air quality thresholds (WELL v2)
        private readonly Dictionary<string, double> _airThresholds = new()
        {
            ["PM2.5"] = 15,
            ["PM10"] = 50,
            ["CO2"] = 900,
            ["TVOC"] = 500,
            ["Formaldehyde"] = 27,
            ["Ozone"] = 51,
            ["Radon"] = 4
        };

        // Lighting thresholds
        private readonly Dictionary<string, double> _lightThresholds = new()
        {
            ["WorkplaneMin"] = 300,
            ["MelanopicMin"] = 200,
            ["CircadianRatio"] = 0.9,
            ["DaylightFactor"] = 2
        };

        private WELLIntelligenceEngine() { }

        public WELLProject CreateWELLProject(string projectId, string projectName,
            SpaceType spaceType, double grossArea, int occupancy, WELLLevel targetLevel)
        {
            var project = new WELLProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                PrimarySpaceType = spaceType,
                GrossArea = grossArea,
                Occupancy = occupancy,
                TargetLevel = targetLevel,
                RegistrationDate = DateTime.UtcNow
            };

            InitializeWELLv2Features(project);

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        private void InitializeWELLv2Features(WELLProject project)
        {
            // Air Concept
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "A01", Name = "Air Quality", Concept = WELLConcept.Air, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "A02", Name = "Smoke-Free Environment", Concept = WELLConcept.Air, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "A03", Name = "Ventilation Design", Concept = WELLConcept.Air, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "A04", Name = "Construction Pollution Management", Concept = WELLConcept.Air, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "A05", Name = "Enhanced Air Quality", Concept = WELLConcept.Air, Type = FeatureType.Optimization, MaxPoints = 3 },
                new WELLFeature { FeatureId = "A06", Name = "Enhanced Ventilation", Concept = WELLConcept.Air, Type = FeatureType.Optimization, MaxPoints = 3 },
                new WELLFeature { FeatureId = "A07", Name = "Operable Windows", Concept = WELLConcept.Air, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "A08", Name = "Air Quality Monitoring", Concept = WELLConcept.Air, Type = FeatureType.Optimization, MaxPoints = 3 }
            });

            // Water Concept
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "W01", Name = "Fundamental Water Quality", Concept = WELLConcept.Water, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "W02", Name = "Water Contaminants", Concept = WELLConcept.Water, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "W03", Name = "Legionella Management", Concept = WELLConcept.Water, Type = FeatureType.Optimization, MaxPoints = 1 },
                new WELLFeature { FeatureId = "W04", Name = "Enhanced Water Quality", Concept = WELLConcept.Water, Type = FeatureType.Optimization, MaxPoints = 1 },
                new WELLFeature { FeatureId = "W05", Name = "Drinking Water Access", Concept = WELLConcept.Water, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "W06", Name = "Drinking Water Promotion", Concept = WELLConcept.Water, Type = FeatureType.Optimization, MaxPoints = 1 }
            });

            // Light Concept
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "L01", Name = "Light Exposure", Concept = WELLConcept.Light, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "L02", Name = "Visual Lighting Design", Concept = WELLConcept.Light, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "L03", Name = "Circadian Lighting Design", Concept = WELLConcept.Light, Type = FeatureType.Optimization, MaxPoints = 3 },
                new WELLFeature { FeatureId = "L04", Name = "Glare Control", Concept = WELLConcept.Light, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "L05", Name = "Enhanced Daylight Access", Concept = WELLConcept.Light, Type = FeatureType.Optimization, MaxPoints = 3 },
                new WELLFeature { FeatureId = "L06", Name = "Visual Balance", Concept = WELLConcept.Light, Type = FeatureType.Optimization, MaxPoints = 1 },
                new WELLFeature { FeatureId = "L07", Name = "Electric Light Quality", Concept = WELLConcept.Light, Type = FeatureType.Optimization, MaxPoints = 2 }
            });

            // Thermal Comfort
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "T01", Name = "Thermal Performance", Concept = WELLConcept.ThermalComfort, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "T02", Name = "Enhanced Thermal Performance", Concept = WELLConcept.ThermalComfort, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "T03", Name = "Thermal Zoning", Concept = WELLConcept.ThermalComfort, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "T04", Name = "Individual Thermal Control", Concept = WELLConcept.ThermalComfort, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "T05", Name = "Radiant Thermal Comfort", Concept = WELLConcept.ThermalComfort, Type = FeatureType.Optimization, MaxPoints = 1 },
                new WELLFeature { FeatureId = "T06", Name = "Thermal Comfort Monitoring", Concept = WELLConcept.ThermalComfort, Type = FeatureType.Optimization, MaxPoints = 1 }
            });

            // Sound Concept
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "S01", Name = "Sound Mapping", Concept = WELLConcept.Sound, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "S02", Name = "Maximum Noise Levels", Concept = WELLConcept.Sound, Type = FeatureType.Optimization, MaxPoints = 3 },
                new WELLFeature { FeatureId = "S03", Name = "Sound Barriers", Concept = WELLConcept.Sound, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "S04", Name = "Sound Absorption", Concept = WELLConcept.Sound, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "S05", Name = "Sound Masking", Concept = WELLConcept.Sound, Type = FeatureType.Optimization, MaxPoints = 1 }
            });

            // Mind Concept
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "M01", Name = "Mental Health Promotion", Concept = WELLConcept.Mind, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "M02", Name = "Nature and Place", Concept = WELLConcept.Mind, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "M03", Name = "Mental Health Support", Concept = WELLConcept.Mind, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "M04", Name = "Self-Monitoring", Concept = WELLConcept.Mind, Type = FeatureType.Optimization, MaxPoints = 1 },
                new WELLFeature { FeatureId = "M05", Name = "Stress Management", Concept = WELLConcept.Mind, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "M06", Name = "Restorative Spaces", Concept = WELLConcept.Mind, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "M07", Name = "Restorative Programming", Concept = WELLConcept.Mind, Type = FeatureType.Optimization, MaxPoints = 1 }
            });

            // Movement Concept
            project.Features.AddRange(new[]
            {
                new WELLFeature { FeatureId = "V01", Name = "Active Buildings and Communities", Concept = WELLConcept.Movement, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "V02", Name = "Visual and Physical Ergonomics", Concept = WELLConcept.Movement, Type = FeatureType.Precondition, MaxPoints = 0 },
                new WELLFeature { FeatureId = "V03", Name = "Movement Network", Concept = WELLConcept.Movement, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "V04", Name = "Active Commuter Support", Concept = WELLConcept.Movement, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "V05", Name = "Site Planning and Selection", Concept = WELLConcept.Movement, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "V06", Name = "Physical Activity Opportunities", Concept = WELLConcept.Movement, Type = FeatureType.Optimization, MaxPoints = 3 },
                new WELLFeature { FeatureId = "V07", Name = "Active Furnishings", Concept = WELLConcept.Movement, Type = FeatureType.Optimization, MaxPoints = 2 },
                new WELLFeature { FeatureId = "V08", Name = "Physical Activity Spaces", Concept = WELLConcept.Movement, Type = FeatureType.Optimization, MaxPoints = 2 }
            });
        }

        public async Task<WELLScorecard> CalculateScorecard(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var scorecard = new WELLScorecard();

                    // Calculate by concept
                    foreach (var concept in Enum.GetValues<WELLConcept>())
                    {
                        var conceptFeatures = project.Features.Where(f => f.Concept == concept).ToList();
                        var score = new ConceptScore
                        {
                            Concept = concept,
                            MaxPoints = conceptFeatures.Where(f => f.Type == FeatureType.Optimization).Sum(f => f.MaxPoints),
                            AchievedPoints = conceptFeatures.Sum(f => f.AchievedPoints),
                            PreconditionsRequired = conceptFeatures.Count(f => f.Type == FeatureType.Precondition),
                            PreconditionsMet = conceptFeatures.Count(f => f.Type == FeatureType.Precondition && f.Status == FeatureStatus.Achieved)
                        };
                        scorecard.ConceptScores[concept] = score;
                    }

                    scorecard.TotalPossiblePoints = scorecard.ConceptScores.Values.Sum(s => s.MaxPoints);
                    scorecard.AchievedPoints = scorecard.ConceptScores.Values.Sum(s => s.AchievedPoints);
                    scorecard.PreconditionsTotal = project.Features.Count(f => f.Type == FeatureType.Precondition);
                    scorecard.PreconditionsMet = project.Features.Count(f => f.Type == FeatureType.Precondition && f.Status == FeatureStatus.Achieved);

                    // Determine levels
                    scorecard.CurrentLevel = GetWELLLevel(scorecard.AchievedPoints);
                    scorecard.ProjectedLevel = GetWELLLevel(
                        project.Features.Where(f => f.Status == FeatureStatus.Achieved || f.Status == FeatureStatus.Pursuing)
                            .Sum(f => f.Type == FeatureType.Optimization ? f.MaxPoints : 0));

                    project.Scorecard = scorecard;
                    return scorecard;
                }
            });
        }

        private WELLLevel GetWELLLevel(int points)
        {
            if (points >= 80) return WELLLevel.Platinum;
            if (points >= 60) return WELLLevel.Gold;
            if (points >= 50) return WELLLevel.Silver;
            return WELLLevel.Bronze;
        }

        public AirQualityAnalysis AnalyzeAirQuality(double pm25, double pm10, double co2, double tvoc, double formaldehyde)
        {
            var analysis = new AirQualityAnalysis
            {
                PM2_5 = pm25,
                PM10 = pm10,
                CO2 = co2,
                TVOC = tvoc,
                Formaldehyde = formaldehyde
            };

            analysis.MeetsPreconditions =
                pm25 <= _airThresholds["PM2.5"] &&
                pm10 <= _airThresholds["PM10"] &&
                co2 <= _airThresholds["CO2"] &&
                tvoc <= _airThresholds["TVOC"] &&
                formaldehyde <= _airThresholds["Formaldehyde"];

            // Calculate points
            if (pm25 <= _airThresholds["PM2.5"] * 0.5 && tvoc <= _airThresholds["TVOC"] * 0.5)
                analysis.AirPoints = 3;
            else if (pm25 <= _airThresholds["PM2.5"] * 0.7 && tvoc <= _airThresholds["TVOC"] * 0.7)
                analysis.AirPoints = 2;
            else if (analysis.MeetsPreconditions)
                analysis.AirPoints = 1;

            // Recommendations
            if (pm25 > _airThresholds["PM2.5"] * 0.5)
                analysis.Recommendations.Add("Consider MERV-16 or HEPA filtration for enhanced PM2.5 removal");
            if (co2 > 800)
                analysis.Recommendations.Add("Increase outdoor air ventilation rate");
            if (tvoc > 300)
                analysis.Recommendations.Add("Specify low-VOC finishes and increase ventilation during flush-out");

            return analysis;
        }

        public LightingAnalysis AnalyzeLighting(double workplaneIlluminance, double melanopicLux,
            double daylightFactor, bool hasElectricGlareControl, bool hasDaylightGlareControl)
        {
            var analysis = new LightingAnalysis
            {
                WorkplaneIlluminance = workplaneIlluminance,
                MelanopicLux = melanopicLux,
                DaylightFactor = daylightFactor,
                HasElectricLightGlareControl = hasElectricGlareControl,
                HasDaylightGlareControl = hasDaylightGlareControl
            };

            analysis.CircadianLightingRatio = melanopicLux / workplaneIlluminance;

            // Calculate points
            if (melanopicLux >= 250 && daylightFactor >= 3)
                analysis.LightPoints = 6;
            else if (melanopicLux >= 200 && daylightFactor >= 2)
                analysis.LightPoints = 4;
            else if (melanopicLux >= 150)
                analysis.LightPoints = 2;

            // Recommendations
            if (melanopicLux < 200)
                analysis.Recommendations.Add("Increase CCT or specify melanopically-enriched light sources");
            if (daylightFactor < 2)
                analysis.Recommendations.Add("Increase glazing area or use light shelves to improve daylight penetration");
            if (!hasElectricGlareControl)
                analysis.Recommendations.Add("Add shielding or indirect lighting to control electric light glare");

            return analysis;
        }

        public ThermalComfortAnalysis AnalyzeThermalComfort(double operativeTemp, double humidity,
            double airSpeed, bool hasZoning, bool hasIndividualControl)
        {
            var analysis = new ThermalComfortAnalysis
            {
                OperativeTemp = operativeTemp,
                RelativeHumidity = humidity,
                AirSpeed = airSpeed,
                HasThermalZoning = hasZoning,
                HasIndividualControl = hasIndividualControl
            };

            // Check ASHRAE 55 compliance (simplified)
            analysis.MeetsASHRAE55 = operativeTemp >= 68 && operativeTemp <= 76 &&
                                      humidity >= 30 && humidity <= 60;

            // Calculate PPD (simplified)
            double tempDeviation = Math.Abs(operativeTemp - 72);
            analysis.PPD = 5 + tempDeviation * 2;

            // Calculate points
            int points = 0;
            if (analysis.MeetsASHRAE55) points += 1;
            if (hasZoning) points += 2;
            if (hasIndividualControl) points += 2;
            if (analysis.PPD < 10) points += 1;
            analysis.ThermalPoints = Math.Min(8, points);

            return analysis;
        }

        public AcousticAnalysis AnalyzeAcoustics(double backgroundNoise, double rt60, double stc)
        {
            var analysis = new AcousticAnalysis
            {
                BackgroundNoise = backgroundNoise,
                ReverberationTime = rt60,
                STC = stc
            };

            // Check requirements
            analysis.HasReverbControl = rt60 <= 0.8;
            analysis.MeetsSoundMasking = backgroundNoise >= 40 && backgroundNoise <= 48;

            // Calculate points
            int points = 0;
            if (backgroundNoise <= 40) points += 2;
            if (rt60 <= 0.6) points += 2;
            if (stc >= 50) points += 2;
            if (analysis.MeetsSoundMasking) points += 1;
            analysis.SoundPoints = Math.Min(8, points);

            // Recommendations
            if (backgroundNoise > 45)
                analysis.Recommendations.Add("Add sound masking or improve HVAC noise control");
            if (rt60 > 0.8)
                analysis.Recommendations.Add("Add acoustic ceiling tiles or wall panels to reduce reverberation");
            if (stc < 45)
                analysis.Recommendations.Add("Improve wall/floor assemblies for better sound isolation");

            return analysis;
        }

        public async Task<HealthMetrics> CalculateHealthMetrics(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var metrics = new HealthMetrics { ProjectId = projectId };

                    // Calculate concept scores
                    var scorecard = project.Scorecard;
                    if (scorecard != null)
                    {
                        metrics.AirQualityScore = scorecard.ConceptScores.GetValueOrDefault(WELLConcept.Air)?.Percentage ?? 0;
                        metrics.WaterQualityScore = scorecard.ConceptScores.GetValueOrDefault(WELLConcept.Water)?.Percentage ?? 0;
                        metrics.LightingScore = scorecard.ConceptScores.GetValueOrDefault(WELLConcept.Light)?.Percentage ?? 0;
                        metrics.ThermalComfortScore = scorecard.ConceptScores.GetValueOrDefault(WELLConcept.ThermalComfort)?.Percentage ?? 0;
                        metrics.AcousticScore = scorecard.ConceptScores.GetValueOrDefault(WELLConcept.Sound)?.Percentage ?? 0;
                    }

                    // Overall health score
                    metrics.OverallHealthScore = (metrics.AirQualityScore + metrics.WaterQualityScore +
                        metrics.LightingScore + metrics.ThermalComfortScore + metrics.AcousticScore) / 5;

                    // Generate recommendations
                    if (metrics.AirQualityScore < 50)
                        metrics.TopRecommendations.Add("Prioritize air quality improvements for occupant health");
                    if (metrics.LightingScore < 50)
                        metrics.TopRecommendations.Add("Enhance circadian lighting design");
                    if (metrics.AcousticScore < 50)
                        metrics.TopRecommendations.Add("Address acoustic comfort for productivity");

                    return metrics;
                }
            });
        }
    }
}
