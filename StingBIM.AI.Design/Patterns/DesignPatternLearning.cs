using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Design.Patterns
{
    /// <summary>
    /// Enhanced pattern learning system for architectural and engineering design.
    /// Learns from successful designs, identifies recurring patterns, and provides
    /// intelligent design recommendations based on historical data and context.
    /// </summary>
    public class DesignPatternLearningEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, DesignPattern> _patternLibrary;
        private readonly Dictionary<string, PatternPerformanceMetrics> _patternPerformance;
        private readonly List<LearningExample> _learningExamples;
        private readonly Dictionary<string, PatternCluster> _patternClusters;
        private readonly object _lock = new object();

        private const int MinimumExamplesForLearning = 5;
        private const double PatternConfidenceThreshold = 0.75;
        private const double SimilarityThreshold = 0.80;

        public DesignPatternLearningEngine()
        {
            _patternLibrary = InitializeBasePatterns();
            _patternPerformance = new Dictionary<string, PatternPerformanceMetrics>();
            _learningExamples = new List<LearningExample>();
            _patternClusters = new Dictionary<string, PatternCluster>();

            Logger.Info("DesignPatternLearningEngine initialized with {0} base patterns", _patternLibrary.Count);
        }

        #region Base Pattern Library

        private Dictionary<string, DesignPattern> InitializeBasePatterns()
        {
            return new Dictionary<string, DesignPattern>(StringComparer.OrdinalIgnoreCase)
            {
                // Spatial Organization Patterns
                ["Linear_Circulation"] = new DesignPattern
                {
                    PatternId = "PAT001",
                    Name = "Linear Circulation",
                    Category = PatternCategory.SpatialOrganization,
                    Description = "Sequential arrangement of spaces along a linear path",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "CirculationLength", MinValue = 10, MaxValue = 100, Unit = "m" },
                        new PatternCharacteristic { Name = "CorridorWidth", MinValue = 1.2, MaxValue = 3.0, Unit = "m" },
                        new PatternCharacteristic { Name = "RoomDepth", MinValue = 3.0, MaxValue = 8.0, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Office", "School", "Hospital", "Hotel" },
                    Advantages = new List<string> { "Clear wayfinding", "Efficient distribution", "Flexible expansion" },
                    Disadvantages = new List<string> { "Can be monotonous", "Long travel distances", "Limited daylight depth" }
                },

                ["Radial_Organization"] = new DesignPattern
                {
                    PatternId = "PAT002",
                    Name = "Radial Organization",
                    Category = PatternCategory.SpatialOrganization,
                    Description = "Spaces arranged around a central focal point",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "CentralSpaceArea", MinValue = 50, MaxValue = 500, Unit = "m²" },
                        new PatternCharacteristic { Name = "RadiatingWings", MinValue = 3, MaxValue = 8, Unit = "count" },
                        new PatternCharacteristic { Name = "WingLength", MinValue = 15, MaxValue = 60, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Museum", "Library", "Convention", "Airport" },
                    Advantages = new List<string> { "Dramatic central space", "Equal access from center", "Natural hierarchy" },
                    Disadvantages = new List<string> { "Complex geometry", "Potential dead ends", "Difficult expansion" }
                },

                ["Courtyard_Plan"] = new DesignPattern
                {
                    PatternId = "PAT003",
                    Name = "Courtyard Plan",
                    Category = PatternCategory.SpatialOrganization,
                    Description = "Spaces arranged around an internal open court",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "CourtyardArea", MinValue = 100, MaxValue = 2000, Unit = "m²" },
                        new PatternCharacteristic { Name = "SurroundingDepth", MinValue = 5, MaxValue = 15, Unit = "m" },
                        new PatternCharacteristic { Name = "CourtAspectRatio", MinValue = 0.5, MaxValue = 2.0, Unit = "ratio" }
                    },
                    ApplicableContexts = new List<string> { "Residential", "Office", "School", "Hotel", "Hospital" },
                    Advantages = new List<string> { "Natural ventilation", "Daylight to all spaces", "Private outdoor space", "Climate responsive" },
                    Disadvantages = new List<string> { "Less efficient footprint", "Security considerations", "Weather exposure" }
                },

                ["Clustered_Organization"] = new DesignPattern
                {
                    PatternId = "PAT004",
                    Name = "Clustered Organization",
                    Category = PatternCategory.SpatialOrganization,
                    Description = "Grouped spaces sharing common characteristics or functions",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "ClusterCount", MinValue = 2, MaxValue = 10, Unit = "count" },
                        new PatternCharacteristic { Name = "UnitsPerCluster", MinValue = 3, MaxValue = 20, Unit = "count" },
                        new PatternCharacteristic { Name = "SharedSpaceRatio", MinValue = 0.1, MaxValue = 0.3, Unit = "ratio" }
                    },
                    ApplicableContexts = new List<string> { "Housing", "Dormitory", "Office", "Healthcare" },
                    Advantages = new List<string> { "Community spaces", "Functional grouping", "Flexible growth" },
                    Disadvantages = new List<string> { "Complex circulation", "Potential wayfinding issues" }
                },

                // Structural Patterns
                ["Grid_Structure"] = new DesignPattern
                {
                    PatternId = "PAT010",
                    Name = "Regular Grid Structure",
                    Category = PatternCategory.Structural,
                    Description = "Uniform column grid providing structural support",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "GridSpacingX", MinValue = 6.0, MaxValue = 12.0, Unit = "m" },
                        new PatternCharacteristic { Name = "GridSpacingY", MinValue = 6.0, MaxValue = 12.0, Unit = "m" },
                        new PatternCharacteristic { Name = "FloorToFloor", MinValue = 3.0, MaxValue = 5.0, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Office", "Commercial", "Industrial", "Parking" },
                    Advantages = new List<string> { "Construction efficiency", "Flexible planning", "Cost effective" },
                    Disadvantages = new List<string> { "Column intrusions", "Limited span options" }
                },

                ["Long_Span_Structure"] = new DesignPattern
                {
                    PatternId = "PAT011",
                    Name = "Long Span Structure",
                    Category = PatternCategory.Structural,
                    Description = "Large column-free spaces using trusses, shells, or tension structures",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "ClearSpan", MinValue = 15, MaxValue = 100, Unit = "m" },
                        new PatternCharacteristic { Name = "StructuralDepth", MinValue = 1.0, MaxValue = 5.0, Unit = "m" },
                        new PatternCharacteristic { Name = "SpanToDepthRatio", MinValue = 15, MaxValue = 30, Unit = "ratio" }
                    },
                    ApplicableContexts = new List<string> { "Arena", "Exhibition", "Warehouse", "Airport" },
                    Advantages = new List<string> { "Flexible open space", "Dramatic architecture", "Unobstructed views" },
                    Disadvantages = new List<string> { "Higher cost", "Complex connections", "Acoustic challenges" }
                },

                ["Load_Bearing_Wall"] = new DesignPattern
                {
                    PatternId = "PAT012",
                    Name = "Load Bearing Wall System",
                    Category = PatternCategory.Structural,
                    Description = "Walls provide primary structural support",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "WallThickness", MinValue = 200, MaxValue = 400, Unit = "mm" },
                        new PatternCharacteristic { Name = "MaxOpeningRatio", MinValue = 0.2, MaxValue = 0.4, Unit = "ratio" },
                        new PatternCharacteristic { Name = "MaxStoreys", MinValue = 1, MaxValue = 6, Unit = "count" }
                    },
                    ApplicableContexts = new List<string> { "Residential", "Low-rise", "Affordable Housing" },
                    Advantages = new List<string> { "Simple construction", "Local materials", "Good thermal mass", "Low cost" },
                    Disadvantages = new List<string> { "Limited flexibility", "Height limitations", "Opening constraints" }
                },

                // Facade Patterns
                ["Punched_Window"] = new DesignPattern
                {
                    PatternId = "PAT020",
                    Name = "Punched Window Facade",
                    Category = PatternCategory.Facade,
                    Description = "Individual windows set into solid wall plane",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "WindowToWallRatio", MinValue = 0.2, MaxValue = 0.4, Unit = "ratio" },
                        new PatternCharacteristic { Name = "WindowWidth", MinValue = 0.6, MaxValue = 2.0, Unit = "m" },
                        new PatternCharacteristic { Name = "WindowHeight", MinValue = 1.0, MaxValue = 2.5, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Residential", "School", "Traditional", "Historic" },
                    Advantages = new List<string> { "Thermal efficiency", "Privacy", "Structural simplicity", "Cost effective" },
                    Disadvantages = new List<string> { "Limited views", "Less daylight", "Can appear heavy" }
                },

                ["Curtain_Wall"] = new DesignPattern
                {
                    PatternId = "PAT021",
                    Name = "Curtain Wall Facade",
                    Category = PatternCategory.Facade,
                    Description = "Non-structural glazed facade system",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "GlazingRatio", MinValue = 0.6, MaxValue = 0.95, Unit = "ratio" },
                        new PatternCharacteristic { Name = "MullionSpacing", MinValue = 1.2, MaxValue = 1.8, Unit = "m" },
                        new PatternCharacteristic { Name = "GlassUValue", MinValue = 0.8, MaxValue = 2.0, Unit = "W/m²K" }
                    },
                    ApplicableContexts = new List<string> { "Office", "Commercial", "High-rise", "Modern" },
                    Advantages = new List<string> { "Maximum daylight", "Views", "Light appearance", "Modern aesthetic" },
                    Disadvantages = new List<string> { "Thermal challenges", "Glare", "Privacy", "High energy in hot climates" }
                },

                ["Double_Skin_Facade"] = new DesignPattern
                {
                    PatternId = "PAT022",
                    Name = "Double Skin Facade",
                    Category = PatternCategory.Facade,
                    Description = "Two-layer facade with ventilated cavity",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "CavityDepth", MinValue = 0.2, MaxValue = 1.5, Unit = "m" },
                        new PatternCharacteristic { Name = "OuterSkinType", MinValue = 0, MaxValue = 2, Unit = "type" },
                        new PatternCharacteristic { Name = "VentilationStrategy", MinValue = 0, MaxValue = 3, Unit = "type" }
                    },
                    ApplicableContexts = new List<string> { "Office", "High-rise", "Hot Climate", "Noisy Environment" },
                    Advantages = new List<string> { "Thermal buffer", "Acoustic insulation", "Natural ventilation", "Solar control" },
                    Disadvantages = new List<string> { "Higher cost", "Maintenance", "Space loss", "Fire risk" }
                },

                ["Shaded_Facade"] = new DesignPattern
                {
                    PatternId = "PAT023",
                    Name = "Shaded Facade (Brise-Soleil)",
                    Category = PatternCategory.Facade,
                    Description = "External shading devices integrated with facade",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "ShadingDepth", MinValue = 0.3, MaxValue = 1.5, Unit = "m" },
                        new PatternCharacteristic { Name = "ShadingAngle", MinValue = 0, MaxValue = 90, Unit = "degrees" },
                        new PatternCharacteristic { Name = "ShadingSpacing", MinValue = 0.2, MaxValue = 1.0, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Hot Climate", "Office", "School", "Tropical" },
                    Advantages = new List<string> { "Solar control", "Reduced cooling", "Visual interest", "Glare control" },
                    Disadvantages = new List<string> { "Maintenance", "Cost", "View obstruction", "Structural load" }
                },

                // Climate Responsive Patterns (Africa-specific)
                ["Cross_Ventilation"] = new DesignPattern
                {
                    PatternId = "PAT030",
                    Name = "Cross Ventilation Design",
                    Category = PatternCategory.ClimateResponsive,
                    Description = "Building oriented for through-ventilation",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "BuildingDepth", MinValue = 10, MaxValue = 15, Unit = "m" },
                        new PatternCharacteristic { Name = "OpeningRatio", MinValue = 0.15, MaxValue = 0.30, Unit = "ratio" },
                        new PatternCharacteristic { Name = "InletOutletRatio", MinValue = 0.8, MaxValue = 1.5, Unit = "ratio" }
                    },
                    ApplicableContexts = new List<string> { "Tropical", "Hot Humid", "Residential", "School" },
                    Advantages = new List<string> { "Zero energy cooling", "Comfort in hot climates", "Lower construction cost" },
                    Disadvantages = new List<string> { "Noise intrusion", "Dust/pollution", "Security concerns", "Rain protection needed" }
                },

                ["Stack_Ventilation"] = new DesignPattern
                {
                    PatternId = "PAT031",
                    Name = "Stack Effect Ventilation",
                    Category = PatternCategory.ClimateResponsive,
                    Description = "Vertical air movement using thermal buoyancy",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "StackHeight", MinValue = 3, MaxValue = 20, Unit = "m" },
                        new PatternCharacteristic { Name = "InletArea", MinValue = 1, MaxValue = 10, Unit = "m²" },
                        new PatternCharacteristic { Name = "OutletArea", MinValue = 1, MaxValue = 10, Unit = "m²" }
                    },
                    ApplicableContexts = new List<string> { "Atrium", "Multi-storey", "Public Building", "Hot Climate" },
                    Advantages = new List<string> { "Works without wind", "Dramatic interior spaces", "Natural smoke extraction" },
                    Disadvantages = new List<string> { "Height required", "Complex control", "Heat gain at top" }
                },

                ["Thermal_Mass_Strategy"] = new DesignPattern
                {
                    PatternId = "PAT032",
                    Name = "Thermal Mass Strategy",
                    Category = PatternCategory.ClimateResponsive,
                    Description = "Heavy construction to moderate temperature swings",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "WallMassPerArea", MinValue = 200, MaxValue = 600, Unit = "kg/m²" },
                        new PatternCharacteristic { Name = "FloorMassPerArea", MinValue = 300, MaxValue = 800, Unit = "kg/m²" },
                        new PatternCharacteristic { Name = "ExposedMassRatio", MinValue = 0.3, MaxValue = 0.8, Unit = "ratio" }
                    },
                    ApplicableContexts = new List<string> { "Hot Arid", "Desert", "High Diurnal Range", "Savanna" },
                    Advantages = new List<string> { "Temperature stability", "Night cooling storage", "Reduced peak loads" },
                    Disadvantages = new List<string> { "Slow response", "Higher structural load", "Not for humid climates" }
                },

                ["Verandah_Design"] = new DesignPattern
                {
                    PatternId = "PAT033",
                    Name = "Verandah/Covered Walkway",
                    Category = PatternCategory.ClimateResponsive,
                    Description = "Shaded outdoor circulation and transition spaces",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "VerandahDepth", MinValue = 1.5, MaxValue = 4.0, Unit = "m" },
                        new PatternCharacteristic { Name = "OverhangRatio", MinValue = 0.3, MaxValue = 0.6, Unit = "ratio" },
                        new PatternCharacteristic { Name = "HeightToCeiling", MinValue = 2.7, MaxValue = 4.0, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Tropical", "Equatorial", "Colonial", "Resort" },
                    Advantages = new List<string> { "Rain protection", "Sun shading", "Social space", "Indoor-outdoor living" },
                    Disadvantages = new List<string> { "Additional area", "Maintenance", "Security in some contexts" }
                },

                // Sustainable Design Patterns
                ["Passive_Solar"] = new DesignPattern
                {
                    PatternId = "PAT040",
                    Name = "Passive Solar Design",
                    Category = PatternCategory.Sustainable,
                    Description = "Building design optimized for solar heat gain/rejection",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "SouthGlazingRatio", MinValue = 0.3, MaxValue = 0.6, Unit = "ratio" },
                        new PatternCharacteristic { Name = "ThermalMass", MinValue = 300, MaxValue = 600, Unit = "kg/m²" },
                        new PatternCharacteristic { Name = "OverhangProjection", MinValue = 0.3, MaxValue = 1.0, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Residential", "School", "Office", "Temperate/Cold Climate" },
                    Advantages = new List<string> { "Reduced heating energy", "Free solar heat", "Daylight" },
                    Disadvantages = new List<string> { "Overheating risk", "Orientation dependent", "Glare" }
                },

                ["Green_Roof"] = new DesignPattern
                {
                    PatternId = "PAT041",
                    Name = "Green Roof Design",
                    Category = PatternCategory.Sustainable,
                    Description = "Vegetated roof system for thermal and ecological benefits",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "SubstrateDepth", MinValue = 80, MaxValue = 500, Unit = "mm" },
                        new PatternCharacteristic { Name = "VegetationType", MinValue = 0, MaxValue = 3, Unit = "type" },
                        new PatternCharacteristic { Name = "DrainageCapacity", MinValue = 20, MaxValue = 80, Unit = "L/m²" }
                    },
                    ApplicableContexts = new List<string> { "Urban", "Commercial", "Residential", "Hot Climate" },
                    Advantages = new List<string> { "Thermal insulation", "Stormwater management", "Urban heat island", "Biodiversity" },
                    Disadvantages = new List<string> { "Structural load", "Maintenance", "Waterproofing complexity", "Cost" }
                },

                ["Rainwater_Harvesting"] = new DesignPattern
                {
                    PatternId = "PAT042",
                    Name = "Rainwater Harvesting Integration",
                    Category = PatternCategory.Sustainable,
                    Description = "Building designed for rainwater collection and use",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "CatchmentArea", MinValue = 100, MaxValue = 5000, Unit = "m²" },
                        new PatternCharacteristic { Name = "StorageVolume", MinValue = 5, MaxValue = 500, Unit = "m³" },
                        new PatternCharacteristic { Name = "CollectionEfficiency", MinValue = 0.7, MaxValue = 0.9, Unit = "ratio" }
                    },
                    ApplicableContexts = new List<string> { "Africa", "Water Scarce", "Rural", "Institutional" },
                    Advantages = new List<string> { "Water security", "Reduced utility cost", "Flood mitigation" },
                    Disadvantages = new List<string> { "Space for storage", "Treatment requirements", "First flush disposal" }
                },

                // Accessibility Patterns
                ["Universal_Access"] = new DesignPattern
                {
                    PatternId = "PAT050",
                    Name = "Universal Access Design",
                    Category = PatternCategory.Accessibility,
                    Description = "Design enabling access for all abilities",
                    Characteristics = new List<PatternCharacteristic>
                    {
                        new PatternCharacteristic { Name = "CorridorWidth", MinValue = 1.5, MaxValue = 2.4, Unit = "m" },
                        new PatternCharacteristic { Name = "RampGradient", MinValue = 0.04, MaxValue = 0.083, Unit = "ratio" },
                        new PatternCharacteristic { Name = "DoorWidth", MinValue = 0.9, MaxValue = 1.2, Unit = "m" }
                    },
                    ApplicableContexts = new List<string> { "Public", "Commercial", "Healthcare", "Education" },
                    Advantages = new List<string> { "Inclusive design", "Legal compliance", "Future-proofing" },
                    Disadvantages = new List<string> { "Space requirements", "Cost", "Design constraints" }
                }
            };
        }

        #endregion

        #region Pattern Recognition

        /// <summary>
        /// Analyzes a design and identifies matching patterns.
        /// </summary>
        public async Task<PatternRecognitionResult> RecognizePatternsAsync(DesignAnalysisInput design)
        {
            Logger.Info("Analyzing design for pattern recognition: {0}", design.ProjectName);

            var result = new PatternRecognitionResult
            {
                ProjectName = design.ProjectName,
                AnalysisDate = DateTime.UtcNow,
                IdentifiedPatterns = new List<IdentifiedPattern>(),
                Recommendations = new List<PatternRecommendation>()
            };

            await Task.Run(() =>
            {
                // Analyze spatial organization
                var spatialPatterns = AnalyzeSpatialOrganization(design);
                result.IdentifiedPatterns.AddRange(spatialPatterns);

                // Analyze structural system
                var structuralPatterns = AnalyzeStructuralSystem(design);
                result.IdentifiedPatterns.AddRange(structuralPatterns);

                // Analyze facade design
                var facadePatterns = AnalyzeFacadeDesign(design);
                result.IdentifiedPatterns.AddRange(facadePatterns);

                // Analyze climate response
                var climatePatterns = AnalyzeClimateResponse(design);
                result.IdentifiedPatterns.AddRange(climatePatterns);

                // Generate recommendations based on context
                result.Recommendations = GeneratePatternRecommendations(design, result.IdentifiedPatterns);

                // Calculate overall pattern score
                result.PatternCoherence = CalculatePatternCoherence(result.IdentifiedPatterns);
            });

            Logger.Info("Pattern recognition complete: {0} patterns identified, coherence score: {1:P0}",
                result.IdentifiedPatterns.Count, result.PatternCoherence);

            return result;
        }

        private List<IdentifiedPattern> AnalyzeSpatialOrganization(DesignAnalysisInput design)
        {
            var patterns = new List<IdentifiedPattern>();

            // Check for courtyard pattern
            if (design.HasCentralCourt && design.CourtArea > 100)
            {
                var pattern = _patternLibrary["Courtyard_Plan"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["CourtyardArea"] = design.CourtArea,
                    ["SurroundingDepth"] = design.BuildingDepth,
                    ["CourtAspectRatio"] = design.CourtAspectRatio
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for linear circulation
            if (design.CirculationLength > 20 && design.CorridorWidth > 0)
            {
                var pattern = _patternLibrary["Linear_Circulation"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["CirculationLength"] = design.CirculationLength,
                    ["CorridorWidth"] = design.CorridorWidth,
                    ["RoomDepth"] = design.TypicalRoomDepth
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for clustered organization
            if (design.ClusterCount >= 2)
            {
                var pattern = _patternLibrary["Clustered_Organization"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["ClusterCount"] = design.ClusterCount,
                    ["UnitsPerCluster"] = design.UnitsPerCluster,
                    ["SharedSpaceRatio"] = design.SharedSpaceRatio
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            return patterns;
        }

        private List<IdentifiedPattern> AnalyzeStructuralSystem(DesignAnalysisInput design)
        {
            var patterns = new List<IdentifiedPattern>();

            // Check for regular grid
            if (design.HasRegularGrid && design.GridSpacingX > 0)
            {
                var pattern = _patternLibrary["Grid_Structure"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["GridSpacingX"] = design.GridSpacingX,
                    ["GridSpacingY"] = design.GridSpacingY,
                    ["FloorToFloor"] = design.FloorToFloorHeight
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for long span
            if (design.MaxClearSpan > 15)
            {
                var pattern = _patternLibrary["Long_Span_Structure"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["ClearSpan"] = design.MaxClearSpan,
                    ["StructuralDepth"] = design.StructuralDepth,
                    ["SpanToDepthRatio"] = design.MaxClearSpan / Math.Max(design.StructuralDepth, 1)
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for load bearing
            if (design.HasLoadBearingWalls)
            {
                var pattern = _patternLibrary["Load_Bearing_Wall"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["WallThickness"] = design.LoadBearingWallThickness,
                    ["MaxOpeningRatio"] = design.WallOpeningRatio,
                    ["MaxStoreys"] = design.NumberOfStoreys
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            return patterns;
        }

        private List<IdentifiedPattern> AnalyzeFacadeDesign(DesignAnalysisInput design)
        {
            var patterns = new List<IdentifiedPattern>();

            // Analyze window-to-wall ratio
            if (design.WindowToWallRatio >= 0.5)
            {
                var pattern = _patternLibrary["Curtain_Wall"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["GlazingRatio"] = design.WindowToWallRatio,
                    ["MullionSpacing"] = design.MullionSpacing,
                    ["GlassUValue"] = design.GlassUValue
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }
            else if (design.WindowToWallRatio <= 0.4)
            {
                var pattern = _patternLibrary["Punched_Window"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["WindowToWallRatio"] = design.WindowToWallRatio,
                    ["WindowWidth"] = design.TypicalWindowWidth,
                    ["WindowHeight"] = design.TypicalWindowHeight
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for shading devices
            if (design.HasExternalShading && design.ShadingProjection > 0.3)
            {
                var pattern = _patternLibrary["Shaded_Facade"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["ShadingDepth"] = design.ShadingProjection,
                    ["ShadingAngle"] = design.ShadingAngle,
                    ["ShadingSpacing"] = design.ShadingSpacing
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            return patterns;
        }

        private List<IdentifiedPattern> AnalyzeClimateResponse(DesignAnalysisInput design)
        {
            var patterns = new List<IdentifiedPattern>();

            // Check cross ventilation potential
            if (design.BuildingDepth <= 15 && design.OppositeOpenings)
            {
                var pattern = _patternLibrary["Cross_Ventilation"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["BuildingDepth"] = design.BuildingDepth,
                    ["OpeningRatio"] = design.OpenableWindowRatio,
                    ["InletOutletRatio"] = design.InletOutletRatio
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for thermal mass strategy
            if (design.WallMass > 200 && design.DiurnalTemperatureRange > 10)
            {
                var pattern = _patternLibrary["Thermal_Mass_Strategy"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["WallMassPerArea"] = design.WallMass,
                    ["FloorMassPerArea"] = design.FloorMass,
                    ["ExposedMassRatio"] = design.ExposedMassRatio
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            // Check for verandah
            if (design.HasVerandah && design.VerandahDepth > 1.5)
            {
                var pattern = _patternLibrary["Verandah_Design"];
                double confidence = CalculatePatternMatch(pattern, new Dictionary<string, double>
                {
                    ["VerandahDepth"] = design.VerandahDepth,
                    ["OverhangRatio"] = design.OverhangRatio,
                    ["HeightToCeiling"] = design.VerandahHeight
                });

                if (confidence >= PatternConfidenceThreshold)
                {
                    patterns.Add(new IdentifiedPattern
                    {
                        Pattern = pattern,
                        Confidence = confidence,
                        MatchedCharacteristics = GetMatchedCharacteristics(pattern, design)
                    });
                }
            }

            return patterns;
        }

        private double CalculatePatternMatch(DesignPattern pattern, Dictionary<string, double> values)
        {
            if (pattern.Characteristics == null || pattern.Characteristics.Count == 0)
                return 0;

            double totalMatch = 0;
            int matchedCount = 0;

            foreach (var characteristic in pattern.Characteristics)
            {
                if (values.TryGetValue(characteristic.Name, out double value))
                {
                    // Calculate how well the value fits within the expected range
                    if (value >= characteristic.MinValue && value <= characteristic.MaxValue)
                    {
                        // Perfect match within range
                        totalMatch += 1.0;
                    }
                    else
                    {
                        // Partial match based on distance from range
                        double range = characteristic.MaxValue - characteristic.MinValue;
                        double distance = Math.Min(
                            Math.Abs(value - characteristic.MinValue),
                            Math.Abs(value - characteristic.MaxValue));
                        double matchRatio = Math.Max(0, 1.0 - (distance / range));
                        totalMatch += matchRatio;
                    }
                    matchedCount++;
                }
            }

            return matchedCount > 0 ? totalMatch / matchedCount : 0;
        }

        private Dictionary<string, double> GetMatchedCharacteristics(DesignPattern pattern, DesignAnalysisInput design)
        {
            // Return the relevant characteristics from the design
            return new Dictionary<string, double>(); // Simplified for now
        }

        private double CalculatePatternCoherence(List<IdentifiedPattern> patterns)
        {
            if (patterns.Count == 0) return 0;

            // Calculate coherence based on pattern compatibility and confidence
            double totalConfidence = patterns.Sum(p => p.Confidence);
            double averageConfidence = totalConfidence / patterns.Count;

            // Check for conflicting patterns
            int conflicts = CountPatternConflicts(patterns);
            double conflictPenalty = Math.Max(0, 1.0 - (conflicts * 0.1));

            return averageConfidence * conflictPenalty;
        }

        private int CountPatternConflicts(List<IdentifiedPattern> patterns)
        {
            int conflicts = 0;

            // Check for known conflicts
            var patternNames = patterns.Select(p => p.Pattern.Name).ToHashSet();

            // Curtain wall conflicts with high thermal mass in hot climates
            if (patternNames.Contains("Curtain Wall Facade") && patternNames.Contains("Thermal Mass Strategy"))
                conflicts++;

            // Load bearing wall conflicts with long span
            if (patternNames.Contains("Load Bearing Wall System") && patternNames.Contains("Long Span Structure"))
                conflicts++;

            return conflicts;
        }

        #endregion

        #region Pattern Recommendations

        private List<PatternRecommendation> GeneratePatternRecommendations(
            DesignAnalysisInput design,
            List<IdentifiedPattern> identifiedPatterns)
        {
            var recommendations = new List<PatternRecommendation>();
            var existingPatterns = identifiedPatterns.Select(p => p.Pattern.Name).ToHashSet();

            // Climate-based recommendations
            if (design.ClimateZone?.Contains("Tropical") == true || design.ClimateZone?.Contains("Aw") == true)
            {
                if (!existingPatterns.Contains("Cross Ventilation Design"))
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Cross_Ventilation"],
                        Reason = "Tropical climate benefits significantly from natural cross-ventilation",
                        Priority = RecommendationPriority.High,
                        EstimatedBenefit = "30-50% reduction in cooling energy"
                    });
                }

                if (!existingPatterns.Contains("Verandah/Covered Walkway"))
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Verandah_Design"],
                        Reason = "Verandahs provide essential shading and rain protection in tropical climates",
                        Priority = RecommendationPriority.Medium,
                        EstimatedBenefit = "Enhanced comfort and usable outdoor space"
                    });
                }

                if (!existingPatterns.Contains("Shaded Facade (Brise-Soleil)"))
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Shaded_Facade"],
                        Reason = "External shading is critical for thermal comfort in equatorial regions",
                        Priority = RecommendationPriority.High,
                        EstimatedBenefit = "20-40% reduction in solar heat gain"
                    });
                }
            }

            // Arid climate recommendations
            if (design.ClimateZone?.Contains("BWh") == true || design.DiurnalTemperatureRange > 15)
            {
                if (!existingPatterns.Contains("Thermal Mass Strategy"))
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Thermal_Mass_Strategy"],
                        Reason = "High diurnal temperature range makes thermal mass highly effective",
                        Priority = RecommendationPriority.High,
                        EstimatedBenefit = "Significant temperature moderation and reduced peak cooling"
                    });
                }

                if (!existingPatterns.Contains("Courtyard Plan"))
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Courtyard_Plan"],
                        Reason = "Courtyards are traditionally effective in hot arid climates",
                        Priority = RecommendationPriority.Medium,
                        EstimatedBenefit = "Microclimate creation and natural cooling"
                    });
                }
            }

            // Building type recommendations
            if (design.BuildingType == "Office" && !existingPatterns.Contains("Regular Grid Structure"))
            {
                recommendations.Add(new PatternRecommendation
                {
                    RecommendedPattern = _patternLibrary["Grid_Structure"],
                    Reason = "Regular grid provides flexibility for office space planning",
                    Priority = RecommendationPriority.Medium,
                    EstimatedBenefit = "Improved flexibility and construction efficiency"
                });
            }

            // Sustainability recommendations
            if (design.SustainabilityTarget == "High" || design.SustainabilityTarget == "NetZero")
            {
                if (!existingPatterns.Contains("Green Roof Design"))
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Green_Roof"],
                        Reason = "Green roofs contribute to sustainability goals and urban heat island mitigation",
                        Priority = RecommendationPriority.Medium,
                        EstimatedBenefit = "Thermal insulation, stormwater management, biodiversity"
                    });
                }

                if (!existingPatterns.Contains("Rainwater Harvesting Integration") && design.AnnualRainfall > 800)
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        RecommendedPattern = _patternLibrary["Rainwater_Harvesting"],
                        Reason = "Adequate rainfall makes rainwater harvesting economically viable",
                        Priority = RecommendationPriority.Medium,
                        EstimatedBenefit = "Water security and reduced utility costs"
                    });
                }
            }

            // Accessibility recommendations
            if (design.IsPublicBuilding && !existingPatterns.Contains("Universal Access Design"))
            {
                recommendations.Add(new PatternRecommendation
                {
                    RecommendedPattern = _patternLibrary["Universal_Access"],
                    Reason = "Public buildings require universal accessibility",
                    Priority = RecommendationPriority.Critical,
                    EstimatedBenefit = "Regulatory compliance and inclusive design"
                });
            }

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }

        #endregion

        #region Pattern Learning

        /// <summary>
        /// Learns from a successful design example.
        /// </summary>
        public void LearnFromExample(LearningExample example)
        {
            lock (_lock)
            {
                _learningExamples.Add(example);

                // Update pattern performance metrics
                foreach (var patternUsed in example.PatternsUsed)
                {
                    if (!_patternPerformance.ContainsKey(patternUsed))
                    {
                        _patternPerformance[patternUsed] = new PatternPerformanceMetrics { PatternName = patternUsed };
                    }

                    var metrics = _patternPerformance[patternUsed];
                    metrics.UsageCount++;
                    metrics.TotalSuccessScore += example.SuccessScore;
                    metrics.AverageSuccessScore = metrics.TotalSuccessScore / metrics.UsageCount;

                    // Track by context
                    if (!metrics.ContextSuccessRates.ContainsKey(example.Context))
                    {
                        metrics.ContextSuccessRates[example.Context] = new ContextMetrics();
                    }
                    metrics.ContextSuccessRates[example.Context].Count++;
                    metrics.ContextSuccessRates[example.Context].TotalScore += example.SuccessScore;
                }

                Logger.Info("Learned from example: {0}, patterns: {1}",
                    example.ProjectName, string.Join(", ", example.PatternsUsed));

                // Trigger cluster update if enough examples
                if (_learningExamples.Count % 10 == 0)
                {
                    UpdatePatternClusters();
                }
            }
        }

        /// <summary>
        /// Gets learned pattern recommendations based on context.
        /// </summary>
        public List<LearnedPatternRecommendation> GetLearnedRecommendations(string context, string buildingType)
        {
            var recommendations = new List<LearnedPatternRecommendation>();

            lock (_lock)
            {
                var relevantExamples = _learningExamples
                    .Where(e => e.Context == context || e.BuildingType == buildingType)
                    .ToList();

                if (relevantExamples.Count < MinimumExamplesForLearning)
                {
                    Logger.Debug("Insufficient examples for learned recommendations: {0}", relevantExamples.Count);
                    return recommendations;
                }

                // Find patterns with high success rates in this context
                foreach (var kvp in _patternPerformance)
                {
                    if (kvp.Value.ContextSuccessRates.TryGetValue(context, out var contextMetrics) &&
                        contextMetrics.Count >= 3)
                    {
                        double avgScore = contextMetrics.TotalScore / contextMetrics.Count;
                        if (avgScore >= 0.7)
                        {
                            recommendations.Add(new LearnedPatternRecommendation
                            {
                                PatternName = kvp.Key,
                                AverageSuccessScore = avgScore,
                                ExampleCount = contextMetrics.Count,
                                Context = context,
                                Confidence = Math.Min(1.0, contextMetrics.Count / 10.0)
                            });
                        }
                    }
                }
            }

            return recommendations.OrderByDescending(r => r.AverageSuccessScore * r.Confidence).ToList();
        }

        private void UpdatePatternClusters()
        {
            // Simple clustering based on pattern co-occurrence
            var coOccurrence = new Dictionary<string, Dictionary<string, int>>();

            foreach (var example in _learningExamples)
            {
                var patterns = example.PatternsUsed.ToList();
                for (int i = 0; i < patterns.Count; i++)
                {
                    if (!coOccurrence.ContainsKey(patterns[i]))
                        coOccurrence[patterns[i]] = new Dictionary<string, int>();

                    for (int j = i + 1; j < patterns.Count; j++)
                    {
                        if (!coOccurrence[patterns[i]].ContainsKey(patterns[j]))
                            coOccurrence[patterns[i]][patterns[j]] = 0;
                        coOccurrence[patterns[i]][patterns[j]]++;
                    }
                }
            }

            Logger.Debug("Pattern clusters updated with {0} co-occurrence pairs", coOccurrence.Count);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets a pattern by name.
        /// </summary>
        public DesignPattern GetPattern(string patternName)
        {
            return _patternLibrary.TryGetValue(patternName, out var pattern) ? pattern : null;
        }

        /// <summary>
        /// Gets all patterns in a category.
        /// </summary>
        public IEnumerable<DesignPattern> GetPatternsByCategory(PatternCategory category)
        {
            return _patternLibrary.Values.Where(p => p.Category == category);
        }

        /// <summary>
        /// Gets all available pattern names.
        /// </summary>
        public IEnumerable<string> GetAvailablePatterns()
        {
            return _patternLibrary.Keys;
        }

        /// <summary>
        /// Gets pattern performance metrics.
        /// </summary>
        public PatternPerformanceMetrics GetPatternMetrics(string patternName)
        {
            lock (_lock)
            {
                return _patternPerformance.TryGetValue(patternName, out var metrics) ? metrics : null;
            }
        }

        #endregion
    }

    #region Data Models

    public class DesignPattern
    {
        public string PatternId { get; set; }
        public string Name { get; set; }
        public PatternCategory Category { get; set; }
        public string Description { get; set; }
        public List<PatternCharacteristic> Characteristics { get; set; }
        public List<string> ApplicableContexts { get; set; }
        public List<string> Advantages { get; set; }
        public List<string> Disadvantages { get; set; }
    }

    public class PatternCharacteristic
    {
        public string Name { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string Unit { get; set; }
    }

    public enum PatternCategory
    {
        SpatialOrganization,
        Structural,
        Facade,
        ClimateResponsive,
        Sustainable,
        Accessibility,
        Circulation,
        MaterialExpression
    }

    public class DesignAnalysisInput
    {
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public string SustainabilityTarget { get; set; }
        public bool IsPublicBuilding { get; set; }
        public double AnnualRainfall { get; set; }
        public double DiurnalTemperatureRange { get; set; }

        // Spatial organization
        public bool HasCentralCourt { get; set; }
        public double CourtArea { get; set; }
        public double CourtAspectRatio { get; set; }
        public double BuildingDepth { get; set; }
        public double CirculationLength { get; set; }
        public double CorridorWidth { get; set; }
        public double TypicalRoomDepth { get; set; }
        public int ClusterCount { get; set; }
        public int UnitsPerCluster { get; set; }
        public double SharedSpaceRatio { get; set; }

        // Structural
        public bool HasRegularGrid { get; set; }
        public double GridSpacingX { get; set; }
        public double GridSpacingY { get; set; }
        public double FloorToFloorHeight { get; set; }
        public double MaxClearSpan { get; set; }
        public double StructuralDepth { get; set; }
        public bool HasLoadBearingWalls { get; set; }
        public double LoadBearingWallThickness { get; set; }
        public double WallOpeningRatio { get; set; }
        public int NumberOfStoreys { get; set; }

        // Facade
        public double WindowToWallRatio { get; set; }
        public double MullionSpacing { get; set; }
        public double GlassUValue { get; set; }
        public double TypicalWindowWidth { get; set; }
        public double TypicalWindowHeight { get; set; }
        public bool HasExternalShading { get; set; }
        public double ShadingProjection { get; set; }
        public double ShadingAngle { get; set; }
        public double ShadingSpacing { get; set; }

        // Climate response
        public bool OppositeOpenings { get; set; }
        public double OpenableWindowRatio { get; set; }
        public double InletOutletRatio { get; set; }
        public double WallMass { get; set; }
        public double FloorMass { get; set; }
        public double ExposedMassRatio { get; set; }
        public bool HasVerandah { get; set; }
        public double VerandahDepth { get; set; }
        public double OverhangRatio { get; set; }
        public double VerandahHeight { get; set; }
    }

    public class PatternRecognitionResult
    {
        public string ProjectName { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<IdentifiedPattern> IdentifiedPatterns { get; set; }
        public List<PatternRecommendation> Recommendations { get; set; }
        public double PatternCoherence { get; set; }
    }

    public class IdentifiedPattern
    {
        public DesignPattern Pattern { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, double> MatchedCharacteristics { get; set; }
    }

    public class PatternRecommendation
    {
        public DesignPattern RecommendedPattern { get; set; }
        public string Reason { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string EstimatedBenefit { get; set; }
    }

    public class LearningExample
    {
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public string Context { get; set; }
        public List<string> PatternsUsed { get; set; }
        public double SuccessScore { get; set; }
        public Dictionary<string, double> PerformanceMetrics { get; set; }
        public DateTime RecordedDate { get; set; }
    }

    public class PatternPerformanceMetrics
    {
        public string PatternName { get; set; }
        public int UsageCount { get; set; }
        public double TotalSuccessScore { get; set; }
        public double AverageSuccessScore { get; set; }
        public Dictionary<string, ContextMetrics> ContextSuccessRates { get; set; } = new Dictionary<string, ContextMetrics>();
    }

    public class ContextMetrics
    {
        public int Count { get; set; }
        public double TotalScore { get; set; }
    }

    public class PatternCluster
    {
        public string ClusterId { get; set; }
        public List<string> Patterns { get; set; }
        public double Cohesion { get; set; }
    }

    public class LearnedPatternRecommendation
    {
        public string PatternName { get; set; }
        public double AverageSuccessScore { get; set; }
        public int ExampleCount { get; set; }
        public string Context { get; set; }
        public double Confidence { get; set; }
    }

    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
