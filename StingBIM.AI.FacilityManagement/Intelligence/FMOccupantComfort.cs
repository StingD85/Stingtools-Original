// ============================================================================
// StingBIM AI - Facility Management Occupant Comfort Intelligence
// Monitors and optimizes indoor environmental quality (IEQ)
// Balances comfort, health, and energy efficiency
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Comfort Models

    /// <summary>
    /// Zone comfort status
    /// </summary>
    public class ZoneComfortStatus
    {
        public string ZoneId { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string ZoneType { get; set; } = string.Empty; // Office, Meeting, Common
        public Guid? RevitRoomId { get; set; }

        // Environmental parameters
        public double Temperature { get; set; } // °C
        public double Humidity { get; set; } // %RH
        public double CO2Level { get; set; } // ppm
        public double AirVelocity { get; set; } // m/s
        public double LightLevel { get; set; } // lux
        public double NoiseLevel { get; set; } // dBA

        // Comfort indices
        public double PMV { get; set; } // Predicted Mean Vote (-3 to +3)
        public double PPD { get; set; } // Predicted Percentage Dissatisfied (%)
        public double OverallComfortScore { get; set; } // 0-100
        public ComfortCategory Category { get; set; }

        // Status
        public DateTime LastUpdated { get; set; }
        public int OccupantCount { get; set; }
        public List<ComfortIssue> ActiveIssues { get; set; } = new();
    }

    public enum ComfortCategory
    {
        Excellent,  // PPD < 6%
        Good,       // PPD 6-10%
        Acceptable, // PPD 10-15%
        Poor,       // PPD 15-25%
        Unacceptable // PPD > 25%
    }

    /// <summary>
    /// Comfort issue/complaint
    /// </summary>
    public class ComfortIssue
    {
        public string IssueId { get; set; } = string.Empty;
        public string ZoneId { get; set; } = string.Empty;
        public ComfortIssueType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public ComfortIssueSeverity Severity { get; set; }

        public DateTime ReportedAt { get; set; }
        public string ReportedBy { get; set; } = string.Empty;
        public bool IsFromSensor { get; set; }
        public bool IsFromOccupant { get; set; }

        // Current readings
        public double? CurrentValue { get; set; }
        public double? TargetMin { get; set; }
        public double? TargetMax { get; set; }
        public string Unit { get; set; } = string.Empty;

        // Response
        public string Status { get; set; } = "Open";
        public string AssignedTo { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public DateTime? ResolvedAt { get; set; }
    }

    public enum ComfortIssueType
    {
        TooCold,
        TooHot,
        TooHumid,
        TooDry,
        Stuffy,
        Draft,
        TooDark,
        TooBright,
        TooNoisy,
        Odor,
        AirQuality
    }

    public enum ComfortIssueSeverity
    {
        Minor,      // Slight discomfort
        Moderate,   // Noticeable impact
        Significant,// Major discomfort
        Severe      // Health concern
    }

    /// <summary>
    /// Occupant feedback entry
    /// </summary>
    public class OccupantFeedback
    {
        public string FeedbackId { get; set; } = string.Empty;
        public string OccupantId { get; set; } = string.Empty;
        public string ZoneId { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }

        // Comfort ratings (1-7 scale: cold to hot, etc.)
        public int ThermalSensation { get; set; } // -3 to +3
        public int ThermalPreference { get; set; } // -1 cooler, 0 no change, +1 warmer
        public int AirQualityRating { get; set; } // 1-5
        public int LightingRating { get; set; } // 1-5
        public int NoiseRating { get; set; } // 1-5
        public int OverallSatisfaction { get; set; } // 1-5

        public string Comments { get; set; } = string.Empty;

        // Environmental conditions at time of feedback
        public double? Temperature { get; set; }
        public double? Humidity { get; set; }
        public double? CO2 { get; set; }
    }

    /// <summary>
    /// Comfort setpoint recommendation
    /// </summary>
    public class ComfortSetpointRecommendation
    {
        public string ZoneId { get; set; } = string.Empty;
        public string Parameter { get; set; } = string.Empty; // Temperature, Humidity, etc.
        public double CurrentSetpoint { get; set; }
        public double RecommendedSetpoint { get; set; }
        public double RecommendedMin { get; set; }
        public double RecommendedMax { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public double EstimatedEnergySavings { get; set; }
        public double EstimatedComfortImpact { get; set; }
    }

    /// <summary>
    /// Indoor air quality assessment
    /// </summary>
    public class AirQualityAssessment
    {
        public string ZoneId { get; set; } = string.Empty;
        public DateTime AssessmentTime { get; set; }

        // Pollutant levels
        public double CO2ppm { get; set; }
        public double PM25 { get; set; } // µg/m³
        public double PM10 { get; set; } // µg/m³
        public double TVOC { get; set; } // µg/m³
        public double Formaldehyde { get; set; } // µg/m³
        public double Ozone { get; set; } // ppb

        // Air quality indices
        public int AQI { get; set; } // 0-500
        public string AQICategory { get; set; } = string.Empty;
        public double VentilationRate { get; set; } // L/s/person
        public double VentilationEffectiveness { get; set; }

        // Standards compliance
        public bool MeetsASHRAE62 { get; set; }
        public bool MeetsWELLStandard { get; set; }
        public List<string> Violations { get; set; } = new();

        // Recommendations
        public List<string> Recommendations { get; set; } = new();
    }

    #endregion

    #region Comfort Intelligence Engine

    /// <summary>
    /// FM Occupant Comfort Intelligence Engine
    /// Monitors and optimizes indoor environmental quality
    /// </summary>
    public class FMOccupantComfort
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Zone data
        private readonly Dictionary<string, ZoneComfortStatus> _zones = new();
        private readonly Dictionary<string, List<OccupantFeedback>> _feedbackHistory = new();
        private readonly List<ComfortIssue> _activeIssues = new();

        // Comfort standards (ASHRAE 55, ISO 7730)
        private readonly ComfortStandards _standards = new();

        public FMOccupantComfort()
        {
            InitializeSampleZones();
            Logger.Info("FM Occupant Comfort Intelligence Engine initialized");
        }

        #region Initialization

        private void InitializeSampleZones()
        {
            var sampleZones = new[]
            {
                new ZoneComfortStatus
                {
                    ZoneId = "ZONE-001",
                    ZoneName = "Open Office Level 1",
                    ZoneType = "Office",
                    Temperature = 23.5,
                    Humidity = 55,
                    CO2Level = 650,
                    LightLevel = 450,
                    NoiseLevel = 42,
                    OccupantCount = 35
                },
                new ZoneComfortStatus
                {
                    ZoneId = "ZONE-002",
                    ZoneName = "Conference Room A",
                    ZoneType = "Meeting",
                    Temperature = 22.0,
                    Humidity = 50,
                    CO2Level = 550,
                    LightLevel = 400,
                    NoiseLevel = 35,
                    OccupantCount = 8
                },
                new ZoneComfortStatus
                {
                    ZoneId = "ZONE-003",
                    ZoneName = "Reception Area",
                    ZoneType = "Common",
                    Temperature = 24.0,
                    Humidity = 60,
                    CO2Level = 480,
                    LightLevel = 350,
                    NoiseLevel = 48,
                    OccupantCount = 12
                },
                new ZoneComfortStatus
                {
                    ZoneId = "ZONE-004",
                    ZoneName = "Executive Suite Level 3",
                    ZoneType = "Office",
                    Temperature = 22.5,
                    Humidity = 52,
                    CO2Level = 520,
                    LightLevel = 500,
                    NoiseLevel = 38,
                    OccupantCount = 6
                }
            };

            foreach (var zone in sampleZones)
            {
                CalculateComfortIndices(zone);
                _zones[zone.ZoneId] = zone;
            }

            Logger.Info($"Initialized {_zones.Count} sample zones");
        }

        #endregion

        #region Comfort Calculation

        /// <summary>
        /// Calculate PMV/PPD comfort indices
        /// </summary>
        private void CalculateComfortIndices(ZoneComfortStatus zone)
        {
            // Simplified PMV calculation based on ISO 7730
            // Full calculation would include metabolic rate, clothing insulation, mean radiant temp

            // Assume typical office conditions
            double metabolicRate = 1.2; // met (seated office work)
            double clothingInsulation = 0.5; // clo (typical summer office)
            double meanRadiantTemp = zone.Temperature; // Assume equal to air temp

            // Simplified PMV approximation
            double neutralTemp = 21 + (clothingInsulation * 2) + (metabolicRate * 0.5);
            double tempDiff = zone.Temperature - neutralTemp;

            zone.PMV = tempDiff * 0.5; // Simplified
            zone.PMV = Math.Max(-3, Math.Min(3, zone.PMV));

            // PPD calculation from PMV
            zone.PPD = 100 - 95 * Math.Exp(-0.03353 * Math.Pow(zone.PMV, 4) - 0.2179 * Math.Pow(zone.PMV, 2));
            zone.PPD = Math.Max(5, Math.Min(100, zone.PPD));

            // Overall comfort score (0-100)
            var thermalScore = 100 - zone.PPD;

            // Air quality score based on CO2
            var aqScore = zone.CO2Level switch
            {
                < 600 => 100,
                < 800 => 90,
                < 1000 => 80,
                < 1200 => 65,
                < 1500 => 50,
                _ => 30
            };

            // Humidity score
            var humidityScore = zone.Humidity switch
            {
                >= 40 and <= 60 => 100,
                >= 30 and <= 70 => 80,
                >= 20 and <= 80 => 60,
                _ => 40
            };

            // Lighting score
            var lightScore = zone.LightLevel switch
            {
                >= 300 and <= 500 => 100,
                >= 200 and <= 750 => 80,
                >= 100 and <= 1000 => 60,
                _ => 40
            };

            // Noise score
            var noiseScore = zone.NoiseLevel switch
            {
                < 40 => 100,
                < 45 => 90,
                < 50 => 75,
                < 55 => 60,
                < 60 => 45,
                _ => 30
            };

            // Weighted overall score
            zone.OverallComfortScore =
                thermalScore * 0.4 +
                aqScore * 0.25 +
                humidityScore * 0.15 +
                lightScore * 0.10 +
                noiseScore * 0.10;

            // Determine category
            zone.Category = zone.PPD switch
            {
                < 6 => ComfortCategory.Excellent,
                < 10 => ComfortCategory.Good,
                < 15 => ComfortCategory.Acceptable,
                < 25 => ComfortCategory.Poor,
                _ => ComfortCategory.Unacceptable
            };

            zone.LastUpdated = DateTime.UtcNow;

            // Check for issues
            CheckForComfortIssues(zone);
        }

        private void CheckForComfortIssues(ZoneComfortStatus zone)
        {
            zone.ActiveIssues.Clear();

            // Temperature issues
            if (zone.Temperature < _standards.TempMinCooling)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-COLD",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.TooCold,
                    Description = $"Temperature {zone.Temperature:F1}°C is below comfort range",
                    Severity = zone.Temperature < 18 ? ComfortIssueSeverity.Significant : ComfortIssueSeverity.Moderate,
                    IsFromSensor = true,
                    CurrentValue = zone.Temperature,
                    TargetMin = _standards.TempMinCooling,
                    TargetMax = _standards.TempMaxCooling,
                    Unit = "°C",
                    ReportedAt = DateTime.UtcNow
                });
            }
            else if (zone.Temperature > _standards.TempMaxCooling)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-HOT",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.TooHot,
                    Description = $"Temperature {zone.Temperature:F1}°C is above comfort range",
                    Severity = zone.Temperature > 28 ? ComfortIssueSeverity.Significant : ComfortIssueSeverity.Moderate,
                    IsFromSensor = true,
                    CurrentValue = zone.Temperature,
                    TargetMin = _standards.TempMinCooling,
                    TargetMax = _standards.TempMaxCooling,
                    Unit = "°C",
                    ReportedAt = DateTime.UtcNow
                });
            }

            // CO2 issues
            if (zone.CO2Level > _standards.CO2Warning)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-CO2",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.Stuffy,
                    Description = $"CO2 level {zone.CO2Level:F0} ppm indicates poor ventilation",
                    Severity = zone.CO2Level > _standards.CO2Critical ? ComfortIssueSeverity.Significant : ComfortIssueSeverity.Moderate,
                    IsFromSensor = true,
                    CurrentValue = zone.CO2Level,
                    TargetMax = _standards.CO2Target,
                    Unit = "ppm",
                    ReportedAt = DateTime.UtcNow
                });
            }

            // Humidity issues
            if (zone.Humidity < _standards.HumidityMin)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-DRY",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.TooDry,
                    Description = $"Humidity {zone.Humidity:F0}% is too low",
                    Severity = zone.Humidity < 20 ? ComfortIssueSeverity.Moderate : ComfortIssueSeverity.Minor,
                    IsFromSensor = true,
                    CurrentValue = zone.Humidity,
                    TargetMin = _standards.HumidityMin,
                    TargetMax = _standards.HumidityMax,
                    Unit = "%RH",
                    ReportedAt = DateTime.UtcNow
                });
            }
            else if (zone.Humidity > _standards.HumidityMax)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-HUMID",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.TooHumid,
                    Description = $"Humidity {zone.Humidity:F0}% is too high",
                    Severity = zone.Humidity > 75 ? ComfortIssueSeverity.Moderate : ComfortIssueSeverity.Minor,
                    IsFromSensor = true,
                    CurrentValue = zone.Humidity,
                    TargetMin = _standards.HumidityMin,
                    TargetMax = _standards.HumidityMax,
                    Unit = "%RH",
                    ReportedAt = DateTime.UtcNow
                });
            }

            // Lighting issues
            if (zone.LightLevel < _standards.LightMinOffice)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-DARK",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.TooDark,
                    Description = $"Light level {zone.LightLevel:F0} lux is insufficient",
                    Severity = zone.LightLevel < 200 ? ComfortIssueSeverity.Moderate : ComfortIssueSeverity.Minor,
                    IsFromSensor = true,
                    CurrentValue = zone.LightLevel,
                    TargetMin = _standards.LightMinOffice,
                    Unit = "lux",
                    ReportedAt = DateTime.UtcNow
                });
            }

            // Noise issues
            if (zone.NoiseLevel > _standards.NoiseMaxOffice)
            {
                zone.ActiveIssues.Add(new ComfortIssue
                {
                    IssueId = $"{zone.ZoneId}-NOISE",
                    ZoneId = zone.ZoneId,
                    Type = ComfortIssueType.TooNoisy,
                    Description = $"Noise level {zone.NoiseLevel:F0} dBA exceeds limit",
                    Severity = zone.NoiseLevel > 55 ? ComfortIssueSeverity.Moderate : ComfortIssueSeverity.Minor,
                    IsFromSensor = true,
                    CurrentValue = zone.NoiseLevel,
                    TargetMax = _standards.NoiseMaxOffice,
                    Unit = "dBA",
                    ReportedAt = DateTime.UtcNow
                });
            }

            // Add to active issues list
            foreach (var issue in zone.ActiveIssues)
            {
                if (!_activeIssues.Any(i => i.IssueId == issue.IssueId))
                    _activeIssues.Add(issue);
            }
        }

        #endregion

        #region Zone Management

        /// <summary>
        /// Update zone sensor readings
        /// </summary>
        public void UpdateZoneReadings(string zoneId, double? temperature = null, double? humidity = null,
            double? co2 = null, double? light = null, double? noise = null, int? occupancy = null)
        {
            if (!_zones.TryGetValue(zoneId, out var zone))
            {
                zone = new ZoneComfortStatus { ZoneId = zoneId, ZoneName = zoneId };
                _zones[zoneId] = zone;
            }

            if (temperature.HasValue) zone.Temperature = temperature.Value;
            if (humidity.HasValue) zone.Humidity = humidity.Value;
            if (co2.HasValue) zone.CO2Level = co2.Value;
            if (light.HasValue) zone.LightLevel = light.Value;
            if (noise.HasValue) zone.NoiseLevel = noise.Value;
            if (occupancy.HasValue) zone.OccupantCount = occupancy.Value;

            CalculateComfortIndices(zone);
        }

        /// <summary>
        /// Get all zone statuses
        /// </summary>
        public List<ZoneComfortStatus> GetAllZoneStatuses()
        {
            return _zones.Values.OrderByDescending(z => z.OccupantCount).ToList();
        }

        /// <summary>
        /// Get zone comfort status
        /// </summary>
        public ZoneComfortStatus GetZoneStatus(string zoneId)
        {
            return _zones.GetValueOrDefault(zoneId);
        }

        /// <summary>
        /// Get zones with issues
        /// </summary>
        public List<ZoneComfortStatus> GetZonesWithIssues()
        {
            return _zones.Values.Where(z => z.ActiveIssues.Any()).ToList();
        }

        #endregion

        #region Feedback Management

        /// <summary>
        /// Submit occupant feedback
        /// </summary>
        public void SubmitFeedback(OccupantFeedback feedback)
        {
            feedback.FeedbackId = Guid.NewGuid().ToString("N")[..8].ToUpper();
            feedback.SubmittedAt = DateTime.UtcNow;

            // Attach current conditions
            if (_zones.TryGetValue(feedback.ZoneId, out var zone))
            {
                feedback.Temperature = zone.Temperature;
                feedback.Humidity = zone.Humidity;
                feedback.CO2 = zone.CO2Level;
            }

            if (!_feedbackHistory.ContainsKey(feedback.ZoneId))
                _feedbackHistory[feedback.ZoneId] = new List<OccupantFeedback>();

            _feedbackHistory[feedback.ZoneId].Add(feedback);

            // Create issue if complaint
            if (feedback.ThermalSensation <= -2 || feedback.ThermalSensation >= 2)
            {
                var issue = new ComfortIssue
                {
                    IssueId = $"FB-{feedback.FeedbackId}",
                    ZoneId = feedback.ZoneId,
                    Type = feedback.ThermalSensation < 0 ? ComfortIssueType.TooCold : ComfortIssueType.TooHot,
                    Description = $"Occupant reported thermal discomfort: {(feedback.ThermalSensation < 0 ? "too cold" : "too hot")}",
                    Severity = Math.Abs(feedback.ThermalSensation) >= 3 ? ComfortIssueSeverity.Significant : ComfortIssueSeverity.Moderate,
                    IsFromOccupant = true,
                    ReportedBy = feedback.OccupantId,
                    ReportedAt = feedback.SubmittedAt,
                    CurrentValue = feedback.Temperature,
                    Unit = "°C"
                };

                if (!string.IsNullOrEmpty(feedback.Comments))
                    issue.Description += $": {feedback.Comments}";

                _activeIssues.Add(issue);
            }

            Logger.Info($"Feedback submitted for zone {feedback.ZoneId}: Thermal={feedback.ThermalSensation}, Overall={feedback.OverallSatisfaction}");
        }

        /// <summary>
        /// Analyze feedback trends
        /// </summary>
        public FeedbackAnalysis AnalyzeFeedback(string zoneId = null, int days = 30)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var feedback = (zoneId != null ?
                _feedbackHistory.GetValueOrDefault(zoneId) ?? new List<OccupantFeedback>() :
                _feedbackHistory.Values.SelectMany(f => f))
                .Where(f => f.SubmittedAt >= cutoff)
                .ToList();

            if (!feedback.Any())
                return new FeedbackAnalysis { ZoneId = zoneId, Period = days };

            return new FeedbackAnalysis
            {
                ZoneId = zoneId ?? "All Zones",
                Period = days,
                TotalResponses = feedback.Count,
                AverageThermalSensation = feedback.Average(f => f.ThermalSensation),
                AverageAirQualityRating = feedback.Average(f => f.AirQualityRating),
                AverageLightingRating = feedback.Average(f => f.LightingRating),
                AverageNoiseRating = feedback.Average(f => f.NoiseRating),
                AverageOverallSatisfaction = feedback.Average(f => f.OverallSatisfaction),
                TooColdPercent = feedback.Count(f => f.ThermalSensation <= -2) * 100.0 / feedback.Count,
                TooHotPercent = feedback.Count(f => f.ThermalSensation >= 2) * 100.0 / feedback.Count,
                ComfortablePercent = feedback.Count(f => Math.Abs(f.ThermalSensation) <= 1) * 100.0 / feedback.Count,
                PreferCooler = feedback.Count(f => f.ThermalPreference < 0),
                PreferNoChange = feedback.Count(f => f.ThermalPreference == 0),
                PreferWarmer = feedback.Count(f => f.ThermalPreference > 0)
            };
        }

        #endregion

        #region Optimization Recommendations

        /// <summary>
        /// Generate setpoint recommendations
        /// </summary>
        public List<ComfortSetpointRecommendation> GenerateSetpointRecommendations(string zoneId = null)
        {
            var recommendations = new List<ComfortSetpointRecommendation>();

            var zonesToAnalyze = zoneId != null ?
                _zones.Where(z => z.Key == zoneId) :
                _zones;

            foreach (var (id, zone) in zonesToAnalyze)
            {
                var feedbackAnalysis = AnalyzeFeedback(id, 30);

                // Temperature recommendation based on feedback
                if (feedbackAnalysis.TotalResponses >= 3)
                {
                    if (feedbackAnalysis.TooColdPercent > 30)
                    {
                        recommendations.Add(new ComfortSetpointRecommendation
                        {
                            ZoneId = id,
                            Parameter = "Cooling Setpoint",
                            CurrentSetpoint = zone.Temperature,
                            RecommendedSetpoint = zone.Temperature + 1,
                            RecommendedMin = 22,
                            RecommendedMax = 26,
                            Unit = "°C",
                            Rationale = $"{feedbackAnalysis.TooColdPercent:F0}% of occupants report feeling cold",
                            ConfidenceScore = 0.75,
                            EstimatedEnergySavings = 5, // 5% per degree increase
                            EstimatedComfortImpact = 15 // % improvement
                        });
                    }
                    else if (feedbackAnalysis.TooHotPercent > 30)
                    {
                        recommendations.Add(new ComfortSetpointRecommendation
                        {
                            ZoneId = id,
                            Parameter = "Cooling Setpoint",
                            CurrentSetpoint = zone.Temperature,
                            RecommendedSetpoint = zone.Temperature - 1,
                            RecommendedMin = 22,
                            RecommendedMax = 26,
                            Unit = "°C",
                            Rationale = $"{feedbackAnalysis.TooHotPercent:F0}% of occupants report feeling hot",
                            ConfidenceScore = 0.75,
                            EstimatedEnergySavings = -5, // Increased energy
                            EstimatedComfortImpact = 15
                        });
                    }
                }

                // CO2/Ventilation recommendation
                if (zone.CO2Level > 800)
                {
                    recommendations.Add(new ComfortSetpointRecommendation
                    {
                        ZoneId = id,
                        Parameter = "Minimum Outside Air",
                        CurrentSetpoint = 10, // L/s/person assumed
                        RecommendedSetpoint = 12,
                        RecommendedMin = 10,
                        Unit = "L/s/person",
                        Rationale = $"CO2 level {zone.CO2Level:F0} ppm exceeds 800 ppm target",
                        ConfidenceScore = 0.85,
                        EstimatedEnergySavings = -8, // More ventilation = more energy
                        EstimatedComfortImpact = 20
                    });
                }

                // Deadband recommendation for energy savings
                if (zone.Category == ComfortCategory.Excellent || zone.Category == ComfortCategory.Good)
                {
                    recommendations.Add(new ComfortSetpointRecommendation
                    {
                        ZoneId = id,
                        Parameter = "Deadband Width",
                        CurrentSetpoint = 2,
                        RecommendedSetpoint = 3,
                        Unit = "°C",
                        Rationale = "Zone comfort is excellent - wider deadband can save energy without impact",
                        ConfidenceScore = 0.70,
                        EstimatedEnergySavings = 8,
                        EstimatedComfortImpact = -5 // Slight reduction in comfort
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.EstimatedEnergySavings).ToList();
        }

        /// <summary>
        /// Assess air quality
        /// </summary>
        public AirQualityAssessment AssessAirQuality(string zoneId)
        {
            if (!_zones.TryGetValue(zoneId, out var zone))
                return null;

            var assessment = new AirQualityAssessment
            {
                ZoneId = zoneId,
                AssessmentTime = DateTime.UtcNow,
                CO2ppm = zone.CO2Level,
                PM25 = 15, // Simulated
                PM10 = 25,
                TVOC = 200,
                Formaldehyde = 30,
                Ozone = 20
            };

            // Calculate AQI (simplified)
            assessment.AQI = (int)Math.Max(
                zone.CO2Level / 10,
                assessment.PM25 * 2
            );

            assessment.AQICategory = assessment.AQI switch
            {
                < 50 => "Good",
                < 100 => "Moderate",
                < 150 => "Unhealthy for Sensitive Groups",
                < 200 => "Unhealthy",
                < 300 => "Very Unhealthy",
                _ => "Hazardous"
            };

            // Ventilation assessment
            assessment.VentilationRate = zone.OccupantCount > 0 ?
                10 * (1000 - zone.CO2Level) / (zone.CO2Level - 400) : 10; // Simplified calc
            assessment.VentilationRate = Math.Max(2.5, Math.Min(20, assessment.VentilationRate));

            assessment.VentilationEffectiveness = assessment.VentilationRate / 10 * 100;

            // Standards compliance
            assessment.MeetsASHRAE62 = zone.CO2Level <= 1000 && assessment.VentilationRate >= 5;
            assessment.MeetsWELLStandard = zone.CO2Level <= 800 && assessment.PM25 <= 15;

            if (zone.CO2Level > 1000)
                assessment.Violations.Add("CO2 exceeds ASHRAE 62.1 limit of 1000 ppm");
            if (assessment.PM25 > 25)
                assessment.Violations.Add("PM2.5 exceeds WHO guideline of 25 µg/m³");
            if (assessment.TVOC > 500)
                assessment.Violations.Add("TVOC exceeds recommended limit of 500 µg/m³");

            // Recommendations
            if (zone.CO2Level > 800)
                assessment.Recommendations.Add("Increase outside air ventilation rate");
            if (assessment.PM25 > 15)
                assessment.Recommendations.Add("Consider enhanced air filtration (MERV 13+)");
            if (assessment.VentilationRate < 7.5)
                assessment.Recommendations.Add("Minimum ventilation rate below recommended 7.5 L/s/person");

            return assessment;
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Get comfort dashboard
        /// </summary>
        public ComfortDashboard GetDashboard()
        {
            var zones = _zones.Values.ToList();

            return new ComfortDashboard
            {
                GeneratedAt = DateTime.UtcNow,
                TotalZones = zones.Count,
                ZonesInComfort = zones.Count(z => z.Category <= ComfortCategory.Acceptable),
                ZonesWithIssues = zones.Count(z => z.ActiveIssues.Any()),
                AverageComfortScore = zones.Average(z => z.OverallComfortScore),
                AverageTemperature = zones.Average(z => z.Temperature),
                AverageCO2 = zones.Average(z => z.CO2Level),
                TotalOccupants = zones.Sum(z => z.OccupantCount),

                ComfortDistribution = new Dictionary<string, int>
                {
                    ["Excellent"] = zones.Count(z => z.Category == ComfortCategory.Excellent),
                    ["Good"] = zones.Count(z => z.Category == ComfortCategory.Good),
                    ["Acceptable"] = zones.Count(z => z.Category == ComfortCategory.Acceptable),
                    ["Poor"] = zones.Count(z => z.Category == ComfortCategory.Poor),
                    ["Unacceptable"] = zones.Count(z => z.Category == ComfortCategory.Unacceptable)
                },

                ActiveIssues = _activeIssues.Where(i => i.Status == "Open").ToList(),
                CriticalIssueCount = _activeIssues.Count(i => i.Severity >= ComfortIssueSeverity.Significant && i.Status == "Open"),

                ZoneStatuses = zones.OrderBy(z => z.OverallComfortScore).ToList(),
                RecentFeedback = _feedbackHistory.Values
                    .SelectMany(f => f)
                    .OrderByDescending(f => f.SubmittedAt)
                    .Take(10)
                    .ToList()
            };
        }

        #endregion

        #endregion // Comfort Intelligence Engine
    }

    #region Supporting Classes

    public class ComfortStandards
    {
        // Temperature (ASHRAE 55 for cooling season)
        public double TempMinCooling { get; set; } = 23.0;
        public double TempMaxCooling { get; set; } = 26.0;
        public double TempMinHeating { get; set; } = 20.0;
        public double TempMaxHeating { get; set; } = 23.5;

        // Humidity
        public double HumidityMin { get; set; } = 30;
        public double HumidityMax { get; set; } = 65;

        // CO2
        public double CO2Target { get; set; } = 800;
        public double CO2Warning { get; set; } = 1000;
        public double CO2Critical { get; set; } = 1500;

        // Lighting
        public double LightMinOffice { get; set; } = 300;
        public double LightTargetOffice { get; set; } = 500;

        // Noise
        public double NoiseMaxOffice { get; set; } = 45;
        public double NoiseMaxMeeting { get; set; } = 40;
    }

    public class FeedbackAnalysis
    {
        public string ZoneId { get; set; } = string.Empty;
        public int Period { get; set; }
        public int TotalResponses { get; set; }
        public double AverageThermalSensation { get; set; }
        public double AverageAirQualityRating { get; set; }
        public double AverageLightingRating { get; set; }
        public double AverageNoiseRating { get; set; }
        public double AverageOverallSatisfaction { get; set; }
        public double TooColdPercent { get; set; }
        public double TooHotPercent { get; set; }
        public double ComfortablePercent { get; set; }
        public int PreferCooler { get; set; }
        public int PreferNoChange { get; set; }
        public int PreferWarmer { get; set; }
    }

    public class ComfortDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalZones { get; set; }
        public int ZonesInComfort { get; set; }
        public int ZonesWithIssues { get; set; }
        public double AverageComfortScore { get; set; }
        public double AverageTemperature { get; set; }
        public double AverageCO2 { get; set; }
        public int TotalOccupants { get; set; }
        public Dictionary<string, int> ComfortDistribution { get; set; } = new();
        public List<ComfortIssue> ActiveIssues { get; set; } = new();
        public int CriticalIssueCount { get; set; }
        public List<ZoneComfortStatus> ZoneStatuses { get; set; } = new();
        public List<OccupantFeedback> RecentFeedback { get; set; } = new();
    }

    #endregion
}
