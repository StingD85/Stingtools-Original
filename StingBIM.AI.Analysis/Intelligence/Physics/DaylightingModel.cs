// ============================================================================
// StingBIM AI - Daylighting Model
// Provides daylight analysis for building design
// Based on CIE standards, CIBSE LG10, and LEED/BREEAM requirements
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Intelligence.Physics
{
    /// <summary>
    /// Daylighting analysis model for building design
    /// Calculates daylight factors, illuminance levels, and glare risk
    /// </summary>
    public class DaylightingModel
    {
        private readonly Dictionary<string, RoomDaylightTarget> _roomTargets;
        private readonly Dictionary<string, double> _glazingTransmittance;
        private readonly Dictionary<string, double> _surfaceReflectance;

        // CIE Standard Overcast Sky luminance (cd/m²)
        private const double ZenithLuminance = 8500;

        public DaylightingModel()
        {
            _roomTargets = LoadRoomTargets();
            _glazingTransmittance = LoadGlazingData();
            _surfaceReflectance = LoadSurfaceReflectance();
        }

        #region Daylight Factor Calculations

        /// <summary>
        /// Comprehensive daylight analysis for a room
        /// </summary>
        public DaylightAnalysis AnalyzeRoom(RoomDaylightGeometry room, List<WindowOpening> windows)
        {
            var analysis = new DaylightAnalysis
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                RoomType = room.RoomType,
                FloorArea = room.FloorArea,
                AnalysisDate = DateTime.UtcNow
            };

            // Calculate daylight factor at grid points
            analysis.DaylightGrid = CalculateDaylightGrid(room, windows);

            // Summary statistics
            analysis.AverageDaylightFactor = analysis.DaylightGrid.Average(p => p.DaylightFactor);
            analysis.MinimumDaylightFactor = analysis.DaylightGrid.Min(p => p.DaylightFactor);
            analysis.MaximumDaylightFactor = analysis.DaylightGrid.Max(p => p.DaylightFactor);
            analysis.UniformityRatio = analysis.MinimumDaylightFactor / analysis.AverageDaylightFactor;

            // Calculate percentage meeting targets
            if (_roomTargets.TryGetValue(room.RoomType, out var target))
            {
                analysis.Target = target;
                analysis.PercentMeetingTarget = (double)analysis.DaylightGrid
                    .Count(p => p.DaylightFactor >= target.MinimumDF) / analysis.DaylightGrid.Count * 100;

                analysis.MeetsMinimumRequirement = analysis.MinimumDaylightFactor >= target.MinimumDF * 0.3;
                analysis.MeetsAverageRequirement = analysis.AverageDaylightFactor >= target.AverageDF;
            }

            // Window-to-floor ratio analysis
            double totalGlazingArea = windows.Sum(w => w.Width * w.Height);
            analysis.WindowToFloorRatio = totalGlazingArea / room.FloorArea * 100;

            // No-sky-line analysis (points that cannot see sky)
            analysis.NoSkyLineDepth = CalculateNoSkyLineDepth(room, windows);

            // Daylight autonomy estimate
            analysis.EstimatedDaylightAutonomy = EstimateDaylightAutonomy(
                analysis.AverageDaylightFactor, room.Latitude);

            // Glare risk assessment
            analysis.GlareAssessment = AssessGlareRisk(room, windows);

            // Generate recommendations
            analysis.Recommendations = GenerateDaylightRecommendations(analysis, room, windows);

            // LEED/BREEAM compliance
            analysis.CertificationCompliance = CheckCertificationCompliance(analysis);

            return analysis;
        }

        /// <summary>
        /// Calculate daylight factor at a single point using BRS Split-Flux method
        /// </summary>
        public double CalculateDaylightFactor(
            PointLocation point,
            RoomDaylightGeometry room,
            List<WindowOpening> windows)
        {
            double df = 0;

            foreach (var window in windows)
            {
                // Sky Component (SC) - direct light from sky through window
                double sc = CalculateSkyComponent(point, window, room);

                // Externally Reflected Component (ERC) - light reflected from outside surfaces
                double erc = CalculateExternallyReflectedComponent(point, window, room);

                // Internally Reflected Component (IRC) - light reflected inside room
                double irc = CalculateInternallyReflectedComponent(point, window, room);

                // Total from this window
                df += sc + erc + irc;
            }

            return Math.Max(0, df);
        }

        /// <summary>
        /// Calculate sky component using simplified BRS method
        /// </summary>
        private double CalculateSkyComponent(PointLocation point, WindowOpening window, RoomDaylightGeometry room)
        {
            // Distance from point to window center
            double distanceToWindow = CalculateDistance(point, window);

            // Angle subtended by window at point
            double windowArea = window.Width * window.Height;
            double solidAngle = windowArea / (distanceToWindow * distanceToWindow);

            // Vertical angle to window center
            double windowCenterHeight = window.SillHeight + window.Height / 2;
            double verticalAngle = Math.Atan2(windowCenterHeight - point.Height, distanceToWindow);

            // Sky luminance at that angle (CIE overcast sky distribution)
            // L(γ) = Lz * (1 + 2*sin(γ)) / 3
            double gamma = Math.Max(0, verticalAngle);
            double relativeLuminance = (1 + 2 * Math.Sin(gamma)) / 3;

            // Glazing transmittance
            double transmittance = _glazingTransmittance.GetValueOrDefault(window.GlazingType, 0.7);

            // Maintenance factor (dirt on glass)
            double maintenanceFactor = 0.9;

            // Frame factor (percentage of opening that is glass)
            double frameFactor = 0.85;

            // Sky component
            double sc = (solidAngle / (2 * Math.PI)) * 100 * relativeLuminance *
                       transmittance * maintenanceFactor * frameFactor;

            // Obstruction reduction
            if (window.ExternalObstruction > 0)
            {
                double obstructionAngle = Math.Atan(window.ExternalObstruction /
                    (window.DistanceToObstruction > 0 ? window.DistanceToObstruction : 10));
                double obstructionFactor = Math.Max(0, 1 - obstructionAngle / (Math.PI / 2));
                sc *= obstructionFactor;
            }

            return Math.Max(0, sc);
        }

        /// <summary>
        /// Calculate externally reflected component
        /// </summary>
        private double CalculateExternallyReflectedComponent(
            PointLocation point, WindowOpening window, RoomDaylightGeometry room)
        {
            // ERC typically 0.1 * SC for urban areas, less for rural
            // Depends on ground reflectance and building reflectance

            double groundReflectance = 0.15; // Typical ground
            if (window.ExternalObstruction > 0)
            {
                // Additional reflection from facing building
                double buildingReflectance = 0.3;
                double obstructionVisibleFraction = Math.Min(1, window.ExternalObstruction /
                    (window.SillHeight + window.Height));
                return 0.05 * (groundReflectance + buildingReflectance * obstructionVisibleFraction);
            }

            return 0.02; // Minimal ERC for unobstructed windows
        }

        /// <summary>
        /// Calculate internally reflected component using BRS tables
        /// </summary>
        private double CalculateInternallyReflectedComponent(
            PointLocation point, WindowOpening window, RoomDaylightGeometry room)
        {
            // IRC depends on room surface reflectances and geometry
            double avgWallReflectance = _surfaceReflectance.GetValueOrDefault(room.WallFinish, 0.5);
            double ceilingReflectance = _surfaceReflectance.GetValueOrDefault(room.CeilingFinish, 0.7);
            double floorReflectance = _surfaceReflectance.GetValueOrDefault(room.FloorFinish, 0.2);

            // Average reflectance weighted by area
            double totalWallArea = 2 * room.Height * (room.Length + room.Width);
            double totalArea = totalWallArea + 2 * room.FloorArea;
            double avgReflectance = (totalWallArea * avgWallReflectance +
                                    room.FloorArea * ceilingReflectance +
                                    room.FloorArea * floorReflectance) / totalArea;

            // Window to room surface ratio
            double windowArea = window.Width * window.Height;
            double windowRatio = windowArea / totalArea;

            // Transmittance
            double transmittance = _glazingTransmittance.GetValueOrDefault(window.GlazingType, 0.7);

            // IRC formula (simplified BRS)
            // IRC = 0.85 * W * T * ρavg / (A * (1 - ρavg²))
            double irc = 0.85 * windowArea * transmittance * avgReflectance /
                        (totalArea * (1 - avgReflectance * avgReflectance)) * 100;

            return Math.Max(0, Math.Min(irc, 5)); // Practical limits
        }

        private List<DaylightGridPoint> CalculateDaylightGrid(
            RoomDaylightGeometry room, List<WindowOpening> windows)
        {
            var grid = new List<DaylightGridPoint>();

            // Standard working plane height (desk level)
            double workplaneHeight = 0.85;

            // Grid spacing (500mm typical for daylight analysis)
            double gridSpacing = 0.5;

            // Margin from walls (500mm)
            double margin = 0.5;

            int xPoints = (int)Math.Max(3, (room.Width - 2 * margin) / gridSpacing);
            int yPoints = (int)Math.Max(3, (room.Length - 2 * margin) / gridSpacing);

            for (int i = 0; i <= xPoints; i++)
            {
                for (int j = 0; j <= yPoints; j++)
                {
                    double x = margin + i * (room.Width - 2 * margin) / xPoints;
                    double y = margin + j * (room.Length - 2 * margin) / yPoints;

                    var point = new PointLocation { X = x, Y = y, Height = workplaneHeight };
                    double df = CalculateDaylightFactor(point, room, windows);

                    grid.Add(new DaylightGridPoint
                    {
                        X = x,
                        Y = y,
                        DaylightFactor = df,
                        CanSeeSky = df > 0.5 // Simplified sky visibility check
                    });
                }
            }

            return grid;
        }

        #endregion

        #region No-Sky Line and Daylight Autonomy

        /// <summary>
        /// Calculate depth at which sky is no longer visible (no-sky line)
        /// </summary>
        private double CalculateNoSkyLineDepth(RoomDaylightGeometry room, List<WindowOpening> windows)
        {
            if (!windows.Any()) return 0;

            var primaryWindow = windows.OrderByDescending(w => w.Width * w.Height).First();

            // Simple geometric calculation
            // No-sky line depth = (window head height - workplane) / tan(obstruction angle)
            double windowHead = primaryWindow.SillHeight + primaryWindow.Height;
            double workplaneHeight = 0.85;

            if (primaryWindow.ExternalObstruction > 0 && primaryWindow.DistanceToObstruction > 0)
            {
                double obstructionAngle = Math.Atan(primaryWindow.ExternalObstruction /
                    primaryWindow.DistanceToObstruction);
                double noSkyDepth = (windowHead - workplaneHeight) / Math.Tan(obstructionAngle);
                return Math.Min(noSkyDepth, room.Length);
            }

            // Unobstructed - no-sky line is typically 2x window head height from window
            return Math.Min(2 * windowHead, room.Length);
        }

        /// <summary>
        /// Estimate daylight autonomy (percentage of occupied hours meeting target illuminance).
        /// Climate-aware: accounts for tropical (high solar availability), temperate, and
        /// high-latitude locations using external illuminance and cloud cover models.
        /// </summary>
        private double EstimateDaylightAutonomy(double averageDF, double latitude)
        {
            // External illuminance availability varies by latitude and climate zone
            // Tropical regions (0-23.5°): high solar availability, ~60-80k lux clear sky
            // Subtropical (23.5-35°): high availability, ~50-70k lux
            // Temperate (35-55°): moderate availability, ~30-50k lux
            // High latitude (55-90°): lower availability, seasonal extremes

            double absLatitude = Math.Abs(latitude);

            // Annual average external horizontal illuminance factor (relative to 10,000 lux reference)
            // Tropical regions have much higher average illuminance than temperate
            double illuminanceFactor;
            if (absLatitude <= 15)
            {
                // Equatorial: consistently high solar availability year-round
                illuminanceFactor = 4.5;
            }
            else if (absLatitude <= 23.5)
            {
                // Tropical: high availability with slight seasonal variation
                illuminanceFactor = 4.0;
            }
            else if (absLatitude <= 35)
            {
                // Subtropical (much of North/South Africa, Middle East)
                illuminanceFactor = 3.2;
            }
            else if (absLatitude <= 50)
            {
                // Temperate (Europe, northern US)
                illuminanceFactor = 2.2;
            }
            else if (absLatitude <= 60)
            {
                // Northern temperate
                illuminanceFactor = 1.5;
            }
            else
            {
                // High latitude
                illuminanceFactor = 1.0;
            }

            // Cloud cover adjustment: tropical regions have afternoon clouds but strong morning sun
            // CIE overcast sky assumption underestimates tropical daylight by ~40-60%
            double cloudFactor = absLatitude <= 23.5 ? 1.3 : 1.0;

            // DA correlation: target 300 lux (office standard)
            // DA = 100 * (1 - exp(-DF * k)) where k depends on external illuminance
            double baseFactor = 0.35 * illuminanceFactor * cloudFactor;
            double da = 100 * (1 - Math.Exp(-averageDF * baseFactor));

            return Math.Min(100, Math.Max(0, da));
        }

        #endregion

        #region Glare Assessment

        /// <summary>
        /// Assess glare risk from windows
        /// </summary>
        private GlareAssessment AssessGlareRisk(RoomDaylightGeometry room, List<WindowOpening> windows)
        {
            var assessment = new GlareAssessment();

            foreach (var window in windows)
            {
                var windowGlare = new WindowGlareRisk
                {
                    WindowId = window.WindowId,
                    Orientation = window.Orientation
                };

                // Check orientation risk
                if (window.Orientation == "East" || window.Orientation == "West")
                {
                    windowGlare.LowSunRisk = "High";
                    windowGlare.RecommendedControl = "External blind or solar shading essential";
                }
                else if (window.Orientation == "South" || window.Orientation == "North")
                {
                    // South in Northern hemisphere, North in Southern
                    if ((room.Latitude > 0 && window.Orientation == "South") ||
                        (room.Latitude < 0 && window.Orientation == "North"))
                    {
                        windowGlare.LowSunRisk = "Medium";
                        windowGlare.RecommendedControl = "Horizontal overhang or brise soleil";
                    }
                    else
                    {
                        windowGlare.LowSunRisk = "Low";
                        windowGlare.RecommendedControl = "Internal blind for occupant control";
                    }
                }

                // Check window size relative to view
                double windowArea = window.Width * window.Height;
                double fieldOfViewArea = room.Length * room.Height * 0.3; // Approximate
                if (windowArea > fieldOfViewArea * 0.4)
                {
                    windowGlare.SizeRisk = "High";
                    windowGlare.RecommendedControl += "; Consider reducing glazing or adding screening";
                }

                // Daylight glare probability estimate
                // Simplified DGP estimation
                double verticalIlluminance = 500 * (windowArea / room.FloorArea) *
                    _glazingTransmittance.GetValueOrDefault(window.GlazingType, 0.7);
                windowGlare.EstimatedDGP = EstimateDGP(verticalIlluminance);

                assessment.WindowRisks.Add(windowGlare);
            }

            // Overall assessment
            assessment.OverallRisk = assessment.WindowRisks.Any(w => w.LowSunRisk == "High")
                ? "High"
                : assessment.WindowRisks.Any(w => w.LowSunRisk == "Medium")
                    ? "Medium"
                    : "Low";

            return assessment;
        }

        /// <summary>
        /// Estimate Daylight Glare Probability
        /// </summary>
        private double EstimateDGP(double verticalIlluminance)
        {
            // Simplified DGP formula
            // DGP = 5.87e-5 * Ev + 0.0918 * log(1 + sum(Ls²*ωs/(Ev^1.87*P²))) + 0.16
            // Simplified to just Ev component for estimation

            double dgp = 5.87e-5 * verticalIlluminance + 0.16;
            return Math.Min(0.95, Math.Max(0.15, dgp));
        }

        #endregion

        #region Recommendations and Compliance

        private List<DaylightRecommendation> GenerateDaylightRecommendations(
            DaylightAnalysis analysis, RoomDaylightGeometry room, List<WindowOpening> windows)
        {
            var recommendations = new List<DaylightRecommendation>();

            // Check if daylight is insufficient
            if (analysis.AverageDaylightFactor < (analysis.Target?.AverageDF ?? 2.0))
            {
                // Calculate required additional glazing
                double currentGlazingArea = windows.Sum(w => w.Width * w.Height);
                double dfDeficit = (analysis.Target?.AverageDF ?? 2.0) - analysis.AverageDaylightFactor;
                double additionalGlazingPercent = dfDeficit / analysis.AverageDaylightFactor * 100;

                recommendations.Add(new DaylightRecommendation
                {
                    Category = "Glazing Area",
                    Priority = "High",
                    Issue = $"Average DF of {analysis.AverageDaylightFactor:F1}% below target",
                    Recommendation = $"Increase window area by approximately {additionalGlazingPercent:F0}%",
                    ExpectedImprovement = $"DF increase to {analysis.Target?.AverageDF:F1}%"
                });

                // Light shelf suggestion for deep rooms
                if (room.Length > 2 * room.Height)
                {
                    recommendations.Add(new DaylightRecommendation
                    {
                        Category = "Light Distribution",
                        Priority = "Medium",
                        Issue = "Deep room limits daylight penetration",
                        Recommendation = "Install light shelves at 2.1m height to redirect light deeper",
                        ExpectedImprovement = "Improved uniformity and 10-15% increase at back of room"
                    });
                }
            }

            // Surface reflectance recommendations
            double avgReflectance = _surfaceReflectance.GetValueOrDefault(room.WallFinish, 0.5);
            if (avgReflectance < 0.5)
            {
                recommendations.Add(new DaylightRecommendation
                {
                    Category = "Interior Surfaces",
                    Priority = "Medium",
                    Issue = $"Wall finish ({room.WallFinish}) has low reflectance",
                    Recommendation = "Use light-colored paint (LRV 0.7+) on walls and ceiling",
                    ExpectedImprovement = "10-20% improvement in internally reflected component"
                });
            }

            // Glazing type recommendations
            foreach (var window in windows.Where(w => w.GlazingType.Contains("Tinted")))
            {
                recommendations.Add(new DaylightRecommendation
                {
                    Category = "Glazing Type",
                    Priority = "Medium",
                    Issue = $"Window {window.WindowId} has tinted glazing reducing daylight",
                    Recommendation = "Consider clear low-e glazing for better daylight with solar control",
                    ExpectedImprovement = "15-25% increase in visible light transmission"
                });
            }

            // Glare control recommendations
            if (analysis.GlareAssessment?.OverallRisk == "High")
            {
                recommendations.Add(new DaylightRecommendation
                {
                    Category = "Glare Control",
                    Priority = "High",
                    Issue = "High glare risk from east/west facing windows",
                    Recommendation = "Install external solar shading or automated blinds",
                    ExpectedImprovement = "DGP reduction to acceptable levels (<0.40)"
                });
            }

            return recommendations;
        }

        private List<CertificationCompliance> CheckCertificationCompliance(DaylightAnalysis analysis)
        {
            var compliance = new List<CertificationCompliance>();

            // LEED v4.1 Daylight
            compliance.Add(new CertificationCompliance
            {
                Certification = "LEED v4.1",
                Credit = "EQ Credit: Daylight",
                Requirement = "Spatial Daylight Autonomy sDA300/50% ≥ 55%",
                ActualValue = $"Estimated sDA: {analysis.EstimatedDaylightAutonomy:F0}%",
                Points = analysis.EstimatedDaylightAutonomy >= 75 ? "3 points" :
                        analysis.EstimatedDaylightAutonomy >= 55 ? "2 points" :
                        analysis.EstimatedDaylightAutonomy >= 40 ? "1 point" : "0 points",
                Compliant = analysis.EstimatedDaylightAutonomy >= 55
            });

            // BREEAM HEA 01
            double uniformity = analysis.UniformityRatio;
            compliance.Add(new CertificationCompliance
            {
                Certification = "BREEAM",
                Credit = "HEA 01: Visual Comfort",
                Requirement = "Average DF ≥ 2% and uniformity ≥ 0.4",
                ActualValue = $"DF: {analysis.AverageDaylightFactor:F1}%, Uniformity: {uniformity:F2}",
                Points = (analysis.AverageDaylightFactor >= 2 && uniformity >= 0.4) ? "3 credits" : "0 credits",
                Compliant = analysis.AverageDaylightFactor >= 2 && uniformity >= 0.4
            });

            // WELL Building Standard
            compliance.Add(new CertificationCompliance
            {
                Certification = "WELL v2",
                Credit = "L06: Visual Lighting Design",
                Requirement = "sDA300/50% ≥ 55% for 75% of workstations",
                ActualValue = $"Estimated sDA: {analysis.EstimatedDaylightAutonomy:F0}%",
                Points = analysis.EstimatedDaylightAutonomy >= 55 ? "Achieved" : "Not Achieved",
                Compliant = analysis.EstimatedDaylightAutonomy >= 55
            });

            return compliance;
        }

        #endregion

        #region Utility Methods

        private double CalculateDistance(PointLocation point, WindowOpening window)
        {
            // Assume window is on wall at y=0
            double dx = point.X - (window.Position.X + window.Width / 2);
            double dy = point.Y; // Distance from wall
            double dz = point.Height - (window.SillHeight + window.Height / 2);

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        #endregion

        #region Database Loading

        private Dictionary<string, RoomDaylightTarget> LoadRoomTargets()
        {
            return new Dictionary<string, RoomDaylightTarget>(StringComparer.OrdinalIgnoreCase)
            {
                { "Office", new RoomDaylightTarget { MinimumDF = 2.0, AverageDF = 2.0, TargetLux = 300, Notes = "CIBSE LG7" } },
                { "Classroom", new RoomDaylightTarget { MinimumDF = 2.0, AverageDF = 5.0, TargetLux = 300, Notes = "BB93" } },
                { "Laboratory", new RoomDaylightTarget { MinimumDF = 2.0, AverageDF = 5.0, TargetLux = 500, Notes = "Bench work" } },
                { "Hospital Ward", new RoomDaylightTarget { MinimumDF = 1.0, AverageDF = 2.0, TargetLux = 100, Notes = "CIBSE LG2" } },
                { "Operating Theatre", new RoomDaylightTarget { MinimumDF = 0, AverageDF = 0, TargetLux = 0, Notes = "No daylight required" } },
                { "Retail", new RoomDaylightTarget { MinimumDF = 1.0, AverageDF = 2.0, TargetLux = 300, Notes = "Varies by type" } },
                { "Kitchen Domestic", new RoomDaylightTarget { MinimumDF = 2.0, AverageDF = 2.0, TargetLux = 300, Notes = "BS 8206-2" } },
                { "Living Room", new RoomDaylightTarget { MinimumDF = 1.5, AverageDF = 1.5, TargetLux = 100, Notes = "BS 8206-2" } },
                { "Bedroom", new RoomDaylightTarget { MinimumDF = 1.0, AverageDF = 1.0, TargetLux = 100, Notes = "BS 8206-2" } },
                { "Bathroom", new RoomDaylightTarget { MinimumDF = 0.5, AverageDF = 1.0, TargetLux = 150, Notes = "May have no window" } },
                { "Corridor", new RoomDaylightTarget { MinimumDF = 0.5, AverageDF = 1.0, TargetLux = 100, Notes = "Supplementary OK" } },
                { "Library", new RoomDaylightTarget { MinimumDF = 2.0, AverageDF = 3.0, TargetLux = 300, Notes = "Avoid direct sun" } },
                { "Museum", new RoomDaylightTarget { MinimumDF = 1.0, AverageDF = 2.0, TargetLux = 200, Notes = "UV control needed" } },
                { "Sports Hall", new RoomDaylightTarget { MinimumDF = 3.5, AverageDF = 5.0, TargetLux = 300, Notes = "Glare control critical" } },
                { "Assembly Hall", new RoomDaylightTarget { MinimumDF = 1.0, AverageDF = 2.0, TargetLux = 200, Notes = "Blackout capability" } }
            };
        }

        private Dictionary<string, double> LoadGlazingData()
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "Clear Single", 0.88 },
                { "Clear Double", 0.78 },
                { "Clear Triple", 0.68 },
                { "Low-E Double", 0.70 },
                { "Low-E Triple", 0.60 },
                { "Tinted Single", 0.55 },
                { "Tinted Double", 0.45 },
                { "Reflective", 0.20 },
                { "Electrochromic Clear", 0.60 },
                { "Electrochromic Tinted", 0.06 },
                { "Translucent", 0.50 },
                { "Fritted 50%", 0.35 },
                { "Structural Glass", 0.75 }
            };
        }

        private Dictionary<string, double> LoadSurfaceReflectance()
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Wall finishes
                { "White Paint", 0.85 },
                { "Light Paint", 0.70 },
                { "Medium Paint", 0.50 },
                { "Dark Paint", 0.20 },
                { "Exposed Concrete", 0.40 },
                { "Brick", 0.30 },
                { "Timber Panel", 0.35 },
                { "Glass", 0.08 },
                { "Mirror", 0.85 },

                // Floor finishes
                { "Light Carpet", 0.40 },
                { "Medium Carpet", 0.25 },
                { "Dark Carpet", 0.10 },
                { "Light Vinyl", 0.50 },
                { "Dark Vinyl", 0.15 },
                { "Timber Floor", 0.30 },
                { "Concrete Floor", 0.20 },
                { "Light Tile", 0.60 },
                { "Dark Tile", 0.10 },

                // Ceiling finishes
                { "White Ceiling", 0.85 },
                { "Acoustic Tile", 0.75 },
                { "Exposed Structure", 0.30 },
                { "Timber Ceiling", 0.40 }
            };
        }

        #endregion
    }

    #region Data Models

    public class RoomDaylightGeometry
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public double Length { get; set; }  // Depth from window wall
        public double Width { get; set; }
        public double Height { get; set; }
        public double FloorArea => Length * Width;
        public double Latitude { get; set; } = 0; // Degrees
        public string WallFinish { get; set; } = "Light Paint";
        public string CeilingFinish { get; set; } = "White Ceiling";
        public string FloorFinish { get; set; } = "Medium Carpet";
    }

    public class WindowOpening
    {
        public string WindowId { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double SillHeight { get; set; }
        public PointLocation Position { get; set; }
        public string Orientation { get; set; }
        public string GlazingType { get; set; } = "Clear Double";
        public double ExternalObstruction { get; set; } // Height of obstruction
        public double DistanceToObstruction { get; set; }
    }

    public class PointLocation
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Height { get; set; }
    }

    public class RoomDaylightTarget
    {
        public double MinimumDF { get; set; }
        public double AverageDF { get; set; }
        public double TargetLux { get; set; }
        public string Notes { get; set; }
    }

    public class DaylightAnalysis
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public double FloorArea { get; set; }
        public DateTime AnalysisDate { get; set; }

        public List<DaylightGridPoint> DaylightGrid { get; set; }
        public double AverageDaylightFactor { get; set; }
        public double MinimumDaylightFactor { get; set; }
        public double MaximumDaylightFactor { get; set; }
        public double UniformityRatio { get; set; }

        public RoomDaylightTarget Target { get; set; }
        public double PercentMeetingTarget { get; set; }
        public bool MeetsMinimumRequirement { get; set; }
        public bool MeetsAverageRequirement { get; set; }

        public double WindowToFloorRatio { get; set; }
        public double NoSkyLineDepth { get; set; }
        public double EstimatedDaylightAutonomy { get; set; }

        public GlareAssessment GlareAssessment { get; set; }
        public List<DaylightRecommendation> Recommendations { get; set; }
        public List<CertificationCompliance> CertificationCompliance { get; set; }
    }

    public class DaylightGridPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double DaylightFactor { get; set; }
        public bool CanSeeSky { get; set; }
    }

    public class GlareAssessment
    {
        public string OverallRisk { get; set; }
        public List<WindowGlareRisk> WindowRisks { get; set; } = new List<WindowGlareRisk>();
    }

    public class WindowGlareRisk
    {
        public string WindowId { get; set; }
        public string Orientation { get; set; }
        public string LowSunRisk { get; set; }
        public string SizeRisk { get; set; }
        public double EstimatedDGP { get; set; }
        public string RecommendedControl { get; set; }
    }

    public class DaylightRecommendation
    {
        public string Category { get; set; }
        public string Priority { get; set; }
        public string Issue { get; set; }
        public string Recommendation { get; set; }
        public string ExpectedImprovement { get; set; }
    }

    public class CertificationCompliance
    {
        public string Certification { get; set; }
        public string Credit { get; set; }
        public string Requirement { get; set; }
        public string ActualValue { get; set; }
        public string Points { get; set; }
        public bool Compliant { get; set; }
    }

    #endregion
}
