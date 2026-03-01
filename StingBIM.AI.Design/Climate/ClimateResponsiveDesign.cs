// StingBIM.AI.Design - ClimateResponsiveDesign.cs
// Climate-responsive design analysis and optimization engine
// Phase 4: Enterprise AI Transformation
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Design.Climate
{
    /// <summary>
    /// Comprehensive climate-responsive design engine providing solar analysis,
    /// natural ventilation optimization, thermal comfort prediction, and
    /// climate-specific design recommendations for African and global contexts.
    /// </summary>
    public class ClimateResponsiveDesignEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, ClimateZone> _climateZones;
        private readonly Dictionary<string, SolarGeometry> _solarData;
        private readonly List<PassiveStrategy> _passiveStrategies;
        private readonly ThermalComfortModel _comfortModel;
        private readonly object _lockObject = new object();

        #region Constructor

        public ClimateResponsiveDesignEngine()
        {
            _climateZones = new Dictionary<string, ClimateZone>(StringComparer.OrdinalIgnoreCase);
            _solarData = new Dictionary<string, SolarGeometry>();
            _passiveStrategies = new List<PassiveStrategy>();
            _comfortModel = new ThermalComfortModel();

            InitializeClimateZones();
            InitializePassiveStrategies();
        }

        #endregion

        #region Initialization

        private void InitializeClimateZones()
        {
            // African Climate Zones
            _climateZones["Kampala"] = new ClimateZone
            {
                ZoneId = "UG-KMP",
                Name = "Kampala, Uganda",
                Latitude = 0.3476,
                Longitude = 32.5825,
                KoppenClassification = "Aw", // Tropical savanna
                AnnualMeanTemp = 21.5,
                AnnualTempRange = 3.2,
                AnnualRainfall = 1290,
                HumidityAvg = 74,
                PredominantWindDirection = "SE",
                SolarRadiationAvg = 5.2, // kWh/m²/day
                HeatingDegreeDays = 0,
                CoolingDegreeDays = 1825,
                DesignStrategies = new[] { "NaturalVentilation", "Shading", "ThermalMass", "NightCooling" }
            };

            _climateZones["Nairobi"] = new ClimateZone
            {
                ZoneId = "KE-NBI",
                Name = "Nairobi, Kenya",
                Latitude = -1.2921,
                Longitude = 36.8219,
                KoppenClassification = "Cwb", // Subtropical highland
                AnnualMeanTemp = 17.8,
                AnnualTempRange = 4.5,
                AnnualRainfall = 869,
                HumidityAvg = 65,
                PredominantWindDirection = "E",
                SolarRadiationAvg = 5.8,
                HeatingDegreeDays = 365,
                CoolingDegreeDays = 730,
                DesignStrategies = new[] { "ThermalMass", "SolarGain", "NaturalVentilation", "Daylighting" }
            };

            _climateZones["Lagos"] = new ClimateZone
            {
                ZoneId = "NG-LAG",
                Name = "Lagos, Nigeria",
                Latitude = 6.5244,
                Longitude = 3.3792,
                KoppenClassification = "Am", // Tropical monsoon
                AnnualMeanTemp = 27.0,
                AnnualTempRange = 3.8,
                AnnualRainfall = 1693,
                HumidityAvg = 83,
                PredominantWindDirection = "SW",
                SolarRadiationAvg = 4.8,
                HeatingDegreeDays = 0,
                CoolingDegreeDays = 3285,
                DesignStrategies = new[] { "CrossVentilation", "Shading", "LightweightConstruction", "RainwaterHarvesting" }
            };

            _climateZones["CapeTown"] = new ClimateZone
            {
                ZoneId = "ZA-CPT",
                Name = "Cape Town, South Africa",
                Latitude = -33.9249,
                Longitude = 18.4241,
                KoppenClassification = "Csb", // Mediterranean
                AnnualMeanTemp = 16.7,
                AnnualTempRange = 8.2,
                AnnualRainfall = 515,
                HumidityAvg = 71,
                PredominantWindDirection = "SE",
                SolarRadiationAvg = 5.5,
                HeatingDegreeDays = 912,
                CoolingDegreeDays = 456,
                DesignStrategies = new[] { "ThermalMass", "PassiveSolar", "NaturalVentilation", "WaterConservation" }
            };

            _climateZones["Cairo"] = new ClimateZone
            {
                ZoneId = "EG-CAI",
                Name = "Cairo, Egypt",
                Latitude = 30.0444,
                Longitude = 31.2357,
                KoppenClassification = "BWh", // Hot desert
                AnnualMeanTemp = 21.7,
                AnnualTempRange = 15.2,
                AnnualRainfall = 25,
                HumidityAvg = 52,
                PredominantWindDirection = "N",
                SolarRadiationAvg = 6.2,
                HeatingDegreeDays = 548,
                CoolingDegreeDays = 2190,
                DesignStrategies = new[] { "ThermalMass", "Shading", "EvaporativeCooling", "Courtyard", "NightCooling" }
            };

            // Add global reference zones
            _climateZones["London"] = new ClimateZone
            {
                ZoneId = "GB-LON",
                Name = "London, UK",
                Latitude = 51.5074,
                Longitude = -0.1278,
                KoppenClassification = "Cfb",
                AnnualMeanTemp = 11.3,
                AnnualTempRange = 13.5,
                AnnualRainfall = 602,
                HumidityAvg = 79,
                PredominantWindDirection = "SW",
                SolarRadiationAvg = 2.8,
                HeatingDegreeDays = 2800,
                CoolingDegreeDays = 45,
                DesignStrategies = new[] { "Insulation", "PassiveSolar", "Airtightness", "HeatRecovery" }
            };

            _climateZones["Dubai"] = new ClimateZone
            {
                ZoneId = "AE-DXB",
                Name = "Dubai, UAE",
                Latitude = 25.2048,
                Longitude = 55.2708,
                KoppenClassification = "BWh",
                AnnualMeanTemp = 27.2,
                AnnualTempRange = 16.8,
                AnnualRainfall = 94,
                HumidityAvg = 60,
                PredominantWindDirection = "NW",
                SolarRadiationAvg = 6.0,
                HeatingDegreeDays = 0,
                CoolingDegreeDays = 4380,
                DesignStrategies = new[] { "Shading", "HighPerformanceGlazing", "Insulation", "ThermalMass" }
            };
        }

        private void InitializePassiveStrategies()
        {
            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "NaturalVentilation",
                Name = "Natural Ventilation",
                Description = "Cross-ventilation and stack effect for cooling",
                ApplicableKoppen = new[] { "Aw", "Am", "Cwb", "Csb", "Cfb" },
                MinTempDiff = 5, // Min indoor-outdoor temp difference
                MaxHumidity = 80,
                EffectiveMonths = new[] { 3, 4, 5, 9, 10, 11 }, // Shoulder seasons
                Parameters = new Dictionary<string, double>
                {
                    ["MinOpeningArea"] = 0.05, // 5% of floor area
                    ["OptimalOpeningRatio"] = 0.08,
                    ["InletOutletRatio"] = 1.0,
                    ["MinStackHeight"] = 3.0
                }
            });

            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "Shading",
                Name = "Solar Shading",
                Description = "External shading devices to reduce solar heat gain",
                ApplicableKoppen = new[] { "Aw", "Am", "BWh", "BSh", "Csa", "Csb" },
                Parameters = new Dictionary<string, double>
                {
                    ["ShadingCoefficient"] = 0.3,
                    ["OverhangDepthRatio"] = 0.5, // Ratio to window height
                    ["FinDepthRatio"] = 0.3,
                    ["LouverAngle"] = 45
                }
            });

            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "ThermalMass",
                Name = "Thermal Mass",
                Description = "High mass construction for temperature stabilization",
                ApplicableKoppen = new[] { "BWh", "BSh", "Cwb", "Csb" },
                MinDiurnalRange = 10, // Minimum day-night temp range
                Parameters = new Dictionary<string, double>
                {
                    ["MinMassPerArea"] = 300, // kg/m² floor area
                    ["OptimalThickness"] = 0.2, // meters
                    ["SurfaceExposure"] = 0.7 // Fraction exposed
                }
            });

            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "PassiveSolar",
                Name = "Passive Solar Heating",
                Description = "Solar gain through south-facing glazing",
                ApplicableKoppen = new[] { "Cfb", "Csb", "Cwb", "Dfb" },
                Parameters = new Dictionary<string, double>
                {
                    ["SouthGlazingRatio"] = 0.35,
                    ["ThermalStorageMass"] = 400, // kg/m² glazing
                    ["SolarHeatGainCoeff"] = 0.6,
                    ["NightInsulation"] = 0.5 // R-value
                }
            });

            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "NightCooling",
                Name = "Night Purge Cooling",
                Description = "Night ventilation to cool thermal mass",
                ApplicableKoppen = new[] { "BWh", "BSh", "Csb" },
                MinDiurnalRange = 12,
                Parameters = new Dictionary<string, double>
                {
                    ["MinAirChanges"] = 10, // ACH during night
                    ["MassExposure"] = 0.8,
                    ["StartHour"] = 21,
                    ["EndHour"] = 6
                }
            });

            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "EvaporativeCooling",
                Name = "Evaporative Cooling",
                Description = "Direct/indirect evaporative cooling",
                ApplicableKoppen = new[] { "BWh", "BSh" },
                MaxHumidity = 50,
                Parameters = new Dictionary<string, double>
                {
                    ["WetBulbEfficiency"] = 0.85,
                    ["WaterUsage"] = 3.5, // L/kWh cooling
                    ["MinWetBulbDepression"] = 10 // °C
                }
            });

            _passiveStrategies.Add(new PassiveStrategy
            {
                StrategyId = "Courtyard",
                Name = "Courtyard Design",
                Description = "Internal courtyard for microclimate creation",
                ApplicableKoppen = new[] { "BWh", "BSh", "Csa" },
                Parameters = new Dictionary<string, double>
                {
                    ["AspectRatio"] = 1.5, // Height/width
                    ["MinArea"] = 25, // m²
                    ["VegetationCover"] = 0.3,
                    ["WaterFeatureArea"] = 0.1
                }
            });
        }

        #endregion

        #region Public Methods - Climate Analysis

        /// <summary>
        /// Gets climate zone data for a location
        /// </summary>
        public ClimateZone GetClimateZone(string location)
        {
            return _climateZones.GetValueOrDefault(location);
        }

        /// <summary>
        /// Gets climate zone by coordinates
        /// </summary>
        public ClimateZone GetClimateZoneByCoordinates(double latitude, double longitude)
        {
            // Find nearest climate zone
            return _climateZones.Values
                .OrderBy(z => CalculateDistance(latitude, longitude, z.Latitude, z.Longitude))
                .FirstOrDefault();
        }

        /// <summary>
        /// Calculates solar position for a given location and time
        /// </summary>
        public SolarPosition CalculateSolarPosition(double latitude, double longitude, DateTime dateTime)
        {
            // Julian day calculation
            int n = dateTime.DayOfYear;
            double hour = dateTime.Hour + dateTime.Minute / 60.0;

            // Solar declination (Spencer formula)
            double B = 2 * Math.PI * (n - 1) / 365.0;
            double declination = 0.006918 - 0.399912 * Math.Cos(B) + 0.070257 * Math.Sin(B)
                               - 0.006758 * Math.Cos(2 * B) + 0.000907 * Math.Sin(2 * B)
                               - 0.002697 * Math.Cos(3 * B) + 0.00148 * Math.Sin(3 * B);
            declination = declination * 180 / Math.PI;

            // Equation of time
            double eot = 229.2 * (0.000075 + 0.001868 * Math.Cos(B) - 0.032077 * Math.Sin(B)
                        - 0.014615 * Math.Cos(2 * B) - 0.04089 * Math.Sin(2 * B));

            // Solar time
            double timeOffset = eot + 4 * longitude;
            double solarTime = hour * 60 + timeOffset;

            // Hour angle
            double hourAngle = (solarTime / 4.0 - 180);

            // Solar altitude and azimuth
            double latRad = latitude * Math.PI / 180;
            double decRad = declination * Math.PI / 180;
            double haRad = hourAngle * Math.PI / 180;

            double altitude = Math.Asin(Math.Sin(latRad) * Math.Sin(decRad) +
                                       Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(haRad));
            altitude = altitude * 180 / Math.PI;

            double azimuth = Math.Acos((Math.Sin(decRad) - Math.Sin(latRad) * Math.Sin(altitude * Math.PI / 180)) /
                                       (Math.Cos(latRad) * Math.Cos(altitude * Math.PI / 180)));
            azimuth = azimuth * 180 / Math.PI;

            if (hourAngle > 0) azimuth = 360 - azimuth;

            return new SolarPosition
            {
                DateTime = dateTime,
                Altitude = altitude,
                Azimuth = azimuth,
                Declination = declination,
                HourAngle = hourAngle,
                IsSunUp = altitude > 0
            };
        }

        /// <summary>
        /// Analyzes optimal building orientation for a location
        /// </summary>
        public async Task<OrientationAnalysis> AnalyzeOptimalOrientationAsync(
            string location,
            BuildingParameters building,
            CancellationToken cancellationToken = default)
        {
            var zone = GetClimateZone(location);
            if (zone == null)
            {
                throw new ArgumentException($"Unknown climate zone: {location}");
            }

            return await Task.Run(() =>
            {
                var analysis = new OrientationAnalysis
                {
                    Location = location,
                    ClimateZone = zone,
                    AnalysisDate = DateTime.Now
                };

                // Analyze different orientations (0-360 degrees)
                var orientationResults = new List<OrientationResult>();

                for (int angle = 0; angle < 360; angle += 15)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = EvaluateOrientation(zone, building, angle);
                    orientationResults.Add(result);
                }

                analysis.OrientationResults = orientationResults;
                analysis.OptimalOrientation = orientationResults
                    .OrderByDescending(r => r.OverallScore)
                    .First();

                // Generate recommendations
                analysis.Recommendations = GenerateOrientationRecommendations(zone, analysis.OptimalOrientation, building);

                return analysis;
            }, cancellationToken);
        }

        /// <summary>
        /// Calculates shading requirements for a facade
        /// </summary>
        public ShadingDesign CalculateShadingRequirements(
            ClimateZone zone,
            double facadeAzimuth,
            double windowHeight,
            double windowWidth,
            double sillHeight)
        {
            var design = new ShadingDesign
            {
                FacadeAzimuth = facadeAzimuth,
                WindowHeight = windowHeight,
                WindowWidth = windowWidth
            };

            // Calculate solar angles for critical times
            var summerSolstice = new DateTime(DateTime.Now.Year, zone.Latitude > 0 ? 6 : 12, 21, 12, 0, 0);
            var winterSolstice = new DateTime(DateTime.Now.Year, zone.Latitude > 0 ? 12 : 6, 21, 12, 0, 0);
            var equinox = new DateTime(DateTime.Now.Year, 3, 21, 12, 0, 0);

            var summerSolar = CalculateSolarPosition(zone.Latitude, zone.Longitude, summerSolstice);
            var winterSolar = CalculateSolarPosition(zone.Latitude, zone.Longitude, winterSolstice);

            // Determine if heating or cooling dominated
            bool coolingDominated = zone.CoolingDegreeDays > zone.HeatingDegreeDays;

            if (coolingDominated)
            {
                // Design to block summer sun
                double cutoffAltitude = summerSolar.Altitude;
                double overhangDepth = windowHeight / Math.Tan(cutoffAltitude * Math.PI / 180);

                design.RecommendedOverhang = new ShadingElement
                {
                    Type = "Horizontal Overhang",
                    Depth = overhangDepth,
                    Width = windowWidth + 0.6, // 300mm extension each side
                    Position = "Above window",
                    CutoffAngle = cutoffAltitude
                };

                // Vertical fins for east/west facades
                double facadeAngle = Math.Abs(facadeAzimuth - 90);
                if (facadeAngle < 45 || Math.Abs(facadeAzimuth - 270) < 45)
                {
                    design.RecommendedFins = new ShadingElement
                    {
                        Type = "Vertical Fins",
                        Depth = windowWidth * 0.3,
                        Spacing = windowWidth / 4,
                        Angle = facadeAzimuth > 180 ? -30 : 30
                    };
                }
            }
            else
            {
                // Allow winter sun, block summer
                double winterAltitude = winterSolar.Altitude;
                double overhangDepth = sillHeight / Math.Tan(winterAltitude * Math.PI / 180) * 0.8;

                design.RecommendedOverhang = new ShadingElement
                {
                    Type = "Horizontal Overhang",
                    Depth = overhangDepth,
                    Width = windowWidth + 0.4,
                    Position = "Above window",
                    CutoffAngle = winterAltitude + 10
                };
            }

            // Calculate shading effectiveness
            design.SummerShadingFactor = CalculateShadingFactor(design, summerSolar, facadeAzimuth);
            design.WinterShadingFactor = CalculateShadingFactor(design, winterSolar, facadeAzimuth);

            return design;
        }

        /// <summary>
        /// Evaluates natural ventilation potential
        /// </summary>
        public async Task<VentilationAnalysis> AnalyzeNaturalVentilationAsync(
            ClimateZone zone,
            BuildingParameters building,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var analysis = new VentilationAnalysis
                {
                    ClimateZone = zone.Name,
                    BuildingType = building.BuildingType
                };

                // Analyze each month
                analysis.MonthlyPotential = new Dictionary<int, VentilationPotential>();

                for (int month = 1; month <= 12; month++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var potential = EvaluateVentilationMonth(zone, building, month);
                    analysis.MonthlyPotential[month] = potential;
                }

                // Calculate annual ventilation hours
                analysis.AnnualVentilationHours = analysis.MonthlyPotential.Values
                    .Sum(p => p.ComfortableHours);

                analysis.AnnualVentilationPercentage = analysis.AnnualVentilationHours / 8760.0 * 100;

                // Cross-ventilation potential
                analysis.CrossVentilationEffectiveness = CalculateCrossVentilationEffectiveness(building);

                // Stack ventilation potential
                analysis.StackVentilationEffectiveness = CalculateStackVentilationEffectiveness(building, zone);

                // Recommendations
                analysis.Recommendations = GenerateVentilationRecommendations(zone, building, analysis);

                return analysis;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets recommended passive strategies for a climate zone
        /// </summary>
        public List<PassiveStrategyRecommendation> GetPassiveStrategies(ClimateZone zone)
        {
            var recommendations = new List<PassiveStrategyRecommendation>();

            foreach (var strategy in _passiveStrategies)
            {
                if (strategy.ApplicableKoppen.Contains(zone.KoppenClassification))
                {
                    var effectiveness = CalculateStrategyEffectiveness(strategy, zone);

                    recommendations.Add(new PassiveStrategyRecommendation
                    {
                        Strategy = strategy,
                        Effectiveness = effectiveness,
                        Priority = GetStrategyPriority(strategy, zone),
                        ImplementationGuidance = GenerateImplementationGuidance(strategy, zone)
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.Priority).ThenByDescending(r => r.Effectiveness).ToList();
        }

        /// <summary>
        /// Predicts thermal comfort using adaptive comfort model
        /// </summary>
        public ThermalComfortResult PredictThermalComfort(
            ClimateZone zone,
            double indoorTemp,
            double indoorHumidity,
            double airSpeed,
            int month)
        {
            return _comfortModel.CalculateAdaptiveComfort(zone, indoorTemp, indoorHumidity, airSpeed, month);
        }

        #endregion

        #region Private Methods

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula
            const double R = 6371; // Earth's radius in km
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private OrientationResult EvaluateOrientation(ClimateZone zone, BuildingParameters building, int angle)
        {
            var result = new OrientationResult
            {
                OrientationAngle = angle
            };

            // Calculate solar heat gain for this orientation
            double solarGain = CalculateAnnualSolarGain(zone, building, angle);

            // Normalize to 0-100 score
            bool coolingDominated = zone.CoolingDegreeDays > zone.HeatingDegreeDays;

            if (coolingDominated)
            {
                // Lower solar gain is better
                result.SolarHeatGainScore = 100 - Math.Min(100, solarGain / 10);
            }
            else
            {
                // Higher solar gain is better
                result.SolarHeatGainScore = Math.Min(100, solarGain / 5);
            }

            // Evaluate daylight potential
            result.DaylightScore = CalculateDaylightScore(zone, angle);

            // Evaluate wind exposure for ventilation
            double windAlignment = CalculateWindAlignment(zone.PredominantWindDirection, angle);
            result.VentilationScore = windAlignment * 100;

            // Calculate view potential (assuming views to certain directions are preferred)
            result.ViewScore = CalculateViewScore(angle);

            // Overall weighted score
            result.OverallScore = result.SolarHeatGainScore * 0.35 +
                                 result.DaylightScore * 0.25 +
                                 result.VentilationScore * 0.25 +
                                 result.ViewScore * 0.15;

            return result;
        }

        private double CalculateAnnualSolarGain(ClimateZone zone, BuildingParameters building, int orientationAngle)
        {
            double totalGain = 0;

            // Sample 12 representative days
            for (int month = 1; month <= 12; month++)
            {
                var date = new DateTime(DateTime.Now.Year, month, 15, 12, 0, 0);
                var solar = CalculateSolarPosition(zone.Latitude, zone.Longitude, date);

                // Calculate incident angle on facade
                double incidentAngle = Math.Abs(solar.Azimuth - orientationAngle);
                if (incidentAngle > 180) incidentAngle = 360 - incidentAngle;

                double gain = zone.SolarRadiationAvg * Math.Cos(incidentAngle * Math.PI / 180);
                gain = Math.Max(0, gain);

                totalGain += gain * 30; // Days per month approximation
            }

            return totalGain;
        }

        private double CalculateDaylightScore(ClimateZone zone, int angle)
        {
            // South-facing (northern hemisphere) or north-facing (southern hemisphere) is optimal
            double optimalAngle = zone.Latitude > 0 ? 180 : 0;
            double deviation = Math.Abs(angle - optimalAngle);
            if (deviation > 180) deviation = 360 - deviation;

            return 100 - (deviation / 180.0 * 50);
        }

        private double CalculateWindAlignment(string windDirection, int buildingAngle)
        {
            var windAngles = new Dictionary<string, int>
            {
                ["N"] = 0, ["NE"] = 45, ["E"] = 90, ["SE"] = 135,
                ["S"] = 180, ["SW"] = 225, ["W"] = 270, ["NW"] = 315
            };

            if (!windAngles.TryGetValue(windDirection, out int windAngle))
                return 0.5;

            // Perpendicular to wind is optimal for cross-ventilation
            double perpendicular = (windAngle + 90) % 360;
            double deviation = Math.Abs(buildingAngle - perpendicular);
            if (deviation > 180) deviation = 360 - deviation;

            return 1 - (deviation / 90.0);
        }

        private double CalculateViewScore(int angle)
        {
            // Assume views are preferred in certain directions
            // This would be customized based on site context
            return 70 + Math.Sin(angle * Math.PI / 180) * 20;
        }

        private List<string> GenerateOrientationRecommendations(ClimateZone zone, OrientationResult optimal, BuildingParameters building)
        {
            var recommendations = new List<string>();

            recommendations.Add($"Optimal building orientation: {optimal.OrientationAngle}° from North");

            if (zone.CoolingDegreeDays > zone.HeatingDegreeDays)
            {
                recommendations.Add("Orient long axis East-West to minimize solar heat gain on facades");
                recommendations.Add("Locate service areas and buffer zones on western facade");
            }
            else
            {
                recommendations.Add("Orient main glazing toward equator for passive solar gain");
                recommendations.Add("Minimize north-facing glazing (southern hemisphere: south-facing)");
            }

            if (optimal.VentilationScore > 70)
            {
                recommendations.Add($"Good alignment with prevailing {zone.PredominantWindDirection} winds for cross-ventilation");
            }

            return recommendations;
        }

        private VentilationPotential EvaluateVentilationMonth(ClimateZone zone, BuildingParameters building, int month)
        {
            var potential = new VentilationPotential
            {
                Month = month
            };

            // Estimate monthly temperature
            double monthTemp = zone.AnnualMeanTemp + zone.AnnualTempRange / 2 *
                              Math.Cos(2 * Math.PI * (month - 7) / 12);

            // Estimate humidity
            double monthHumidity = zone.HumidityAvg;

            // Calculate comfortable hours
            int comfortableHours = 0;
            for (int hour = 0; hour < 24; hour++)
            {
                double hourTemp = monthTemp + 5 * Math.Sin(2 * Math.PI * (hour - 14) / 24);
                var comfort = _comfortModel.CalculateAdaptiveComfort(zone, hourTemp, monthHumidity, 0.5, month);

                if (comfort.WithinComfortZone)
                {
                    comfortableHours++;
                }
            }

            potential.ComfortableHours = comfortableHours * 30; // Approximate days per month
            potential.AverageTemperature = monthTemp;
            potential.AverageHumidity = monthHumidity;
            potential.VentilationEffectiveness = comfortableHours / 24.0;

            return potential;
        }

        private double CalculateCrossVentilationEffectiveness(BuildingParameters building)
        {
            // Based on opening sizes and positions
            double inletArea = building.GlazingRatio * building.FloorArea * 0.5; // Assume half openable
            double floorArea = building.FloorArea;

            double openingRatio = inletArea / floorArea;
            return Math.Min(1.0, openingRatio / 0.05); // 5% is ideal
        }

        private double CalculateStackVentilationEffectiveness(BuildingParameters building, ClimateZone zone)
        {
            // Stack effect based on height and temperature difference
            double stackHeight = building.FloorCount * 3.0; // Assume 3m floor-to-floor
            double tempDiff = zone.AnnualTempRange / 2;

            double stackPressure = 0.042 * stackHeight * tempDiff;
            return Math.Min(1.0, stackPressure / 10);
        }

        private List<string> GenerateVentilationRecommendations(ClimateZone zone, BuildingParameters building, VentilationAnalysis analysis)
        {
            var recommendations = new List<string>();

            if (analysis.AnnualVentilationPercentage > 50)
            {
                recommendations.Add($"Natural ventilation viable for {analysis.AnnualVentilationPercentage:F0}% of year");
            }

            if (analysis.CrossVentilationEffectiveness > 0.7)
            {
                recommendations.Add("Cross-ventilation highly effective - ensure opposing openings");
            }
            else
            {
                recommendations.Add($"Increase openable window area to at least 5% of floor area");
            }

            if (building.FloorCount > 2 && analysis.StackVentilationEffectiveness > 0.5)
            {
                recommendations.Add("Stack ventilation viable - consider atrium or stairwell venting");
            }

            recommendations.Add($"Optimize for prevailing {zone.PredominantWindDirection} winds");

            return recommendations;
        }

        private double CalculateStrategyEffectiveness(PassiveStrategy strategy, ClimateZone zone)
        {
            double effectiveness = 0.7; // Base effectiveness

            if (strategy.MinDiurnalRange.HasValue && zone.AnnualTempRange > strategy.MinDiurnalRange)
            {
                effectiveness += 0.15;
            }

            if (strategy.MaxHumidity.HasValue && zone.HumidityAvg < strategy.MaxHumidity)
            {
                effectiveness += 0.1;
            }

            return Math.Min(1.0, effectiveness);
        }

        private int GetStrategyPriority(PassiveStrategy strategy, ClimateZone zone)
        {
            // Higher number = higher priority
            if (zone.CoolingDegreeDays > zone.HeatingDegreeDays * 2)
            {
                // Cooling dominated
                return strategy.StrategyId switch
                {
                    "Shading" => 10,
                    "NaturalVentilation" => 9,
                    "NightCooling" => 8,
                    "EvaporativeCooling" => 7,
                    "ThermalMass" => 6,
                    _ => 5
                };
            }
            else
            {
                // Heating dominated
                return strategy.StrategyId switch
                {
                    "PassiveSolar" => 10,
                    "ThermalMass" => 9,
                    "Insulation" => 8,
                    "Airtightness" => 7,
                    _ => 5
                };
            }
        }

        private string GenerateImplementationGuidance(PassiveStrategy strategy, ClimateZone zone)
        {
            return strategy.StrategyId switch
            {
                "NaturalVentilation" => $"Provide openings of at least {strategy.Parameters["MinOpeningArea"] * 100}% floor area, optimized for {zone.PredominantWindDirection} winds",
                "Shading" => $"Install horizontal overhangs with depth ratio of {strategy.Parameters["OverhangDepthRatio"]:F1} times window height",
                "ThermalMass" => $"Use minimum {strategy.Parameters["MinMassPerArea"]} kg/m² with {strategy.Parameters["SurfaceExposure"] * 100}% exposed surface",
                "PassiveSolar" => $"South glazing ratio of {strategy.Parameters["SouthGlazingRatio"] * 100}% with thermal storage mass",
                "NightCooling" => $"Enable {strategy.Parameters["MinAirChanges"]} ACH between {strategy.Parameters["StartHour"]}:00 and {strategy.Parameters["EndHour"]}:00",
                "EvaporativeCooling" => $"Wet bulb efficiency target: {strategy.Parameters["WetBulbEfficiency"] * 100}%",
                "Courtyard" => $"Aspect ratio of {strategy.Parameters["AspectRatio"]} with {strategy.Parameters["VegetationCover"] * 100}% vegetation",
                _ => "Implement according to local best practices"
            };
        }

        private double CalculateShadingFactor(ShadingDesign design, SolarPosition solar, double facadeAzimuth)
        {
            if (!solar.IsSunUp) return 0;

            double shadeFactor = 0;
            if (design.RecommendedOverhang != null)
            {
                double shadowLength = design.RecommendedOverhang.Depth * Math.Tan(solar.Altitude * Math.PI / 180);
                shadeFactor = Math.Min(1.0, shadowLength / design.WindowHeight);
            }

            return shadeFactor;
        }

        #endregion
    }

    #region Supporting Classes

    public class ClimateZone
    {
        public string ZoneId { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string KoppenClassification { get; set; }
        public double AnnualMeanTemp { get; set; }
        public double AnnualTempRange { get; set; }
        public double AnnualRainfall { get; set; }
        public double HumidityAvg { get; set; }
        public string PredominantWindDirection { get; set; }
        public double SolarRadiationAvg { get; set; }
        public double HeatingDegreeDays { get; set; }
        public double CoolingDegreeDays { get; set; }
        public string[] DesignStrategies { get; set; }
    }

    public class SolarGeometry
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<SolarPosition> DailyPath { get; set; } = new List<SolarPosition>();
    }

    public class SolarPosition
    {
        public DateTime DateTime { get; set; }
        public double Altitude { get; set; }
        public double Azimuth { get; set; }
        public double Declination { get; set; }
        public double HourAngle { get; set; }
        public bool IsSunUp { get; set; }
    }

    public class PassiveStrategy
    {
        public string StrategyId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] ApplicableKoppen { get; set; }
        public double? MinTempDiff { get; set; }
        public double? MaxHumidity { get; set; }
        public double? MinDiurnalRange { get; set; }
        public int[] EffectiveMonths { get; set; }
        public Dictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();
    }

    public class BuildingParameters
    {
        public string BuildingType { get; set; }
        public double FloorArea { get; set; }
        public int FloorCount { get; set; }
        public double GlazingRatio { get; set; }
        public double WallUValue { get; set; }
        public double RoofUValue { get; set; }
        public double ThermalMass { get; set; }
    }

    public class OrientationAnalysis
    {
        public string Location { get; set; }
        public ClimateZone ClimateZone { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<OrientationResult> OrientationResults { get; set; }
        public OrientationResult OptimalOrientation { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class OrientationResult
    {
        public int OrientationAngle { get; set; }
        public double SolarHeatGainScore { get; set; }
        public double DaylightScore { get; set; }
        public double VentilationScore { get; set; }
        public double ViewScore { get; set; }
        public double OverallScore { get; set; }
    }

    public class ShadingDesign
    {
        public double FacadeAzimuth { get; set; }
        public double WindowHeight { get; set; }
        public double WindowWidth { get; set; }
        public ShadingElement RecommendedOverhang { get; set; }
        public ShadingElement RecommendedFins { get; set; }
        public double SummerShadingFactor { get; set; }
        public double WinterShadingFactor { get; set; }
    }

    public class ShadingElement
    {
        public string Type { get; set; }
        public double Depth { get; set; }
        public double Width { get; set; }
        public double? Spacing { get; set; }
        public double? Angle { get; set; }
        public string Position { get; set; }
        public double? CutoffAngle { get; set; }
    }

    public class VentilationAnalysis
    {
        public string ClimateZone { get; set; }
        public string BuildingType { get; set; }
        public Dictionary<int, VentilationPotential> MonthlyPotential { get; set; }
        public double AnnualVentilationHours { get; set; }
        public double AnnualVentilationPercentage { get; set; }
        public double CrossVentilationEffectiveness { get; set; }
        public double StackVentilationEffectiveness { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class VentilationPotential
    {
        public int Month { get; set; }
        public int ComfortableHours { get; set; }
        public double AverageTemperature { get; set; }
        public double AverageHumidity { get; set; }
        public double VentilationEffectiveness { get; set; }
    }

    public class PassiveStrategyRecommendation
    {
        public PassiveStrategy Strategy { get; set; }
        public double Effectiveness { get; set; }
        public int Priority { get; set; }
        public string ImplementationGuidance { get; set; }
    }

    public class ThermalComfortResult
    {
        public double OperativeTemperature { get; set; }
        public double ComfortTemperature { get; set; }
        public double UpperComfortLimit { get; set; }
        public double LowerComfortLimit { get; set; }
        public bool WithinComfortZone { get; set; }
        public double ComfortIndex { get; set; } // -1 (cold) to +1 (hot)
        public string ComfortCategory { get; set; }
    }

    public class ThermalComfortModel
    {
        /// <summary>
        /// Calculates adaptive thermal comfort based on ASHRAE 55 and EN 15251
        /// </summary>
        public ThermalComfortResult CalculateAdaptiveComfort(
            ClimateZone zone,
            double indoorTemp,
            double humidity,
            double airSpeed,
            int month)
        {
            // Calculate running mean outdoor temperature
            double monthlyMeanTemp = zone.AnnualMeanTemp + zone.AnnualTempRange / 2 *
                                    Math.Cos(2 * Math.PI * (month - 7) / 12);

            // ASHRAE 55 adaptive comfort equation
            double comfortTemp = 0.31 * monthlyMeanTemp + 17.8;

            // Acceptability limits (80% acceptability)
            double upperLimit = comfortTemp + 3.5;
            double lowerLimit = comfortTemp - 3.5;

            // Adjust for air speed (elevated air speed effect)
            if (airSpeed > 0.2)
            {
                double coolingEffect = 0.7 * Math.Sqrt(airSpeed - 0.2);
                upperLimit += coolingEffect;
            }

            // Operative temperature (simplified)
            double operativeTemp = indoorTemp; // Assuming MRT ≈ air temp

            // Calculate comfort index
            double comfortIndex = 0;
            if (operativeTemp > comfortTemp)
            {
                comfortIndex = (operativeTemp - comfortTemp) / (upperLimit - comfortTemp);
            }
            else
            {
                comfortIndex = (operativeTemp - comfortTemp) / (comfortTemp - lowerLimit);
            }
            comfortIndex = Math.Max(-1, Math.Min(1, comfortIndex));

            return new ThermalComfortResult
            {
                OperativeTemperature = operativeTemp,
                ComfortTemperature = comfortTemp,
                UpperComfortLimit = upperLimit,
                LowerComfortLimit = lowerLimit,
                WithinComfortZone = operativeTemp >= lowerLimit && operativeTemp <= upperLimit,
                ComfortIndex = comfortIndex,
                ComfortCategory = GetComfortCategory(comfortIndex)
            };
        }

        private string GetComfortCategory(double index)
        {
            return index switch
            {
                < -0.7 => "Cold",
                < -0.3 => "Slightly Cool",
                < 0.3 => "Comfortable",
                < 0.7 => "Slightly Warm",
                _ => "Warm"
            };
        }
    }

    #endregion
}
