// StingBIM.AI.Creation.Elements.FloorCreator
// Creates floors/slabs in Revit from AI commands with structural and accessibility intelligence
// Master Proposal Reference: Part 4.2 Phase 1 Month 2 - Basic Elements

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Creates and modifies floors/slabs in Revit based on natural language commands.
    /// Handles rectangular floors, irregular shapes, and floor openings.
    /// Provides intelligent floor type selection with structural span validation,
    /// accessibility slope checks, thermal/acoustic properties, and cost estimation.
    /// </summary>
    public class FloorCreator : IElementCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Default values
        private const double DefaultFloorThickness = 150; // mm
        private const string DefaultFloorType = "Generic - 150mm";
        private const double MinFloorArea = 1000000; // mm² (1 m²)
        private const double MaxFloorArea = 10000000000; // mm² (10,000 m²)

        // Comprehensive floor type catalogue with intelligence properties
        private static readonly Dictionary<string, FloorTypeDefinition> FloorTypeCatalogue =
            new Dictionary<string, FloorTypeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            // Concrete slabs
            ["Concrete Slab - 150mm"] = new FloorTypeDefinition
            {
                Name = "Concrete Slab - 150mm",
                Thickness = 150,
                IsStructural = true,
                PrimaryMaterial = "Reinforced Concrete",
                MaxSpan = 5000, // mm
                LiveLoadCapacity = 2.4, // kN/m² (residential per ASCE 7)
                DeadWeight = 3.6, // kN/m² (self-weight)
                FireRatingMinutes = 90,
                AcousticRating = 45, // IIC (Impact Insulation Class)
                ThermalResistance = 0.1, // R-value m²·K/W (concrete alone)
                CostPerM2 = 65,
                Description = "Standard reinforced concrete slab for residential/light commercial"
            },
            ["Concrete Slab - 200mm"] = new FloorTypeDefinition
            {
                Name = "Concrete Slab - 200mm",
                Thickness = 200,
                IsStructural = true,
                PrimaryMaterial = "Reinforced Concrete",
                MaxSpan = 6500,
                LiveLoadCapacity = 4.8, // kN/m² (office per ASCE 7)
                DeadWeight = 4.8,
                FireRatingMinutes = 120,
                AcousticRating = 50,
                ThermalResistance = 0.13,
                CostPerM2 = 85,
                Description = "Heavy-duty reinforced concrete slab for offices/commercial"
            },
            ["Concrete Slab - 250mm"] = new FloorTypeDefinition
            {
                Name = "Concrete Slab - 250mm",
                Thickness = 250,
                IsStructural = true,
                PrimaryMaterial = "Reinforced Concrete",
                MaxSpan = 8000,
                LiveLoadCapacity = 7.2, // kN/m² (assembly/retail)
                DeadWeight = 6.0,
                FireRatingMinutes = 180,
                AcousticRating = 52,
                ThermalResistance = 0.16,
                CostPerM2 = 110,
                Description = "Heavy reinforced concrete slab for assembly/retail/parking"
            },
            ["Post-Tensioned Slab - 200mm"] = new FloorTypeDefinition
            {
                Name = "Post-Tensioned Slab - 200mm",
                Thickness = 200,
                IsStructural = true,
                PrimaryMaterial = "Post-Tensioned Concrete",
                MaxSpan = 10000, // PT allows longer spans
                LiveLoadCapacity = 4.8,
                DeadWeight = 4.8,
                FireRatingMinutes = 120,
                AcousticRating = 50,
                ThermalResistance = 0.13,
                CostPerM2 = 120,
                Description = "Post-tensioned slab for long spans (office, parking)"
            },
            // Composite slabs
            ["Composite Steel Deck - 130mm"] = new FloorTypeDefinition
            {
                Name = "Composite Steel Deck - 130mm",
                Thickness = 130,
                IsStructural = true,
                PrimaryMaterial = "Steel Deck with Concrete Topping",
                MaxSpan = 4000,
                LiveLoadCapacity = 4.8,
                DeadWeight = 2.5,
                FireRatingMinutes = 60,
                AcousticRating = 35,
                ThermalResistance = 0.08,
                CostPerM2 = 75,
                Description = "Composite steel deck for steel-framed buildings"
            },
            ["Steel Deck"] = new FloorTypeDefinition
            {
                Name = "Steel Deck",
                Thickness = 150,
                IsStructural = true,
                PrimaryMaterial = "Steel Deck with Concrete Topping",
                MaxSpan = 4500,
                LiveLoadCapacity = 4.8,
                DeadWeight = 2.8,
                FireRatingMinutes = 60,
                AcousticRating = 38,
                ThermalResistance = 0.1,
                CostPerM2 = 80,
                Description = "Steel deck floor system"
            },
            // Timber floors
            ["Wood Floor - Joist"] = new FloorTypeDefinition
            {
                Name = "Wood Floor - Joist",
                Thickness = 250, // total including joist depth
                IsStructural = true,
                PrimaryMaterial = "Timber Joist with Plywood Sheathing",
                MaxSpan = 4500,
                LiveLoadCapacity = 2.4,
                DeadWeight = 0.6,
                FireRatingMinutes = 30,
                AcousticRating = 30,
                ThermalResistance = 1.5,
                CostPerM2 = 55,
                Description = "Traditional timber joist floor (residential)"
            },
            ["Wood Floor"] = new FloorTypeDefinition
            {
                Name = "Wood Floor",
                Thickness = 100,
                IsStructural = false,
                PrimaryMaterial = "Timber",
                MaxSpan = 3000,
                LiveLoadCapacity = 1.5,
                DeadWeight = 0.4,
                FireRatingMinutes = 0,
                AcousticRating = 25,
                ThermalResistance = 1.2,
                CostPerM2 = 45,
                Description = "Non-structural timber floor finish"
            },
            ["Engineered Timber - CLT"] = new FloorTypeDefinition
            {
                Name = "Engineered Timber - CLT",
                Thickness = 200,
                IsStructural = true,
                PrimaryMaterial = "Cross-Laminated Timber",
                MaxSpan = 7000,
                LiveLoadCapacity = 4.8,
                DeadWeight = 1.0,
                FireRatingMinutes = 90,
                AcousticRating = 40,
                ThermalResistance = 2.0,
                CostPerM2 = 130,
                Description = "CLT structural floor (sustainable, rapid construction)"
            },
            // Raised/access floors
            ["Raised Access Floor"] = new FloorTypeDefinition
            {
                Name = "Raised Access Floor",
                Thickness = 150, // panel + void
                IsStructural = false,
                PrimaryMaterial = "Steel Pedestal with Concrete-Filled Panel",
                MaxSpan = 600, // panel span
                LiveLoadCapacity = 3.0,
                DeadWeight = 0.5,
                FireRatingMinutes = 60,
                AcousticRating = 35,
                ThermalResistance = 0.5,
                CostPerM2 = 95,
                VoidHeight = 300, // mm below-floor void
                Description = "Raised access floor for offices/data centres (underfloor services)"
            },
            // Insulated/heated floors
            ["Insulated Floor - Ground"] = new FloorTypeDefinition
            {
                Name = "Insulated Floor - Ground",
                Thickness = 250,
                IsStructural = true,
                PrimaryMaterial = "Concrete on Insulation on DPM",
                MaxSpan = 0, // ground-bearing
                LiveLoadCapacity = 2.4,
                DeadWeight = 5.0,
                FireRatingMinutes = 120,
                AcousticRating = 50,
                ThermalResistance = 2.5, // with insulation
                CostPerM2 = 90,
                RequiresDampProofing = true,
                Description = "Insulated ground-floor slab with DPM (ASHRAE 90.1 compliant)"
            },
            ["Underfloor Heated"] = new FloorTypeDefinition
            {
                Name = "Underfloor Heated",
                Thickness = 200,
                IsStructural = true,
                PrimaryMaterial = "Concrete with Embedded Heating Coils",
                MaxSpan = 5000,
                LiveLoadCapacity = 2.4,
                DeadWeight = 4.0,
                FireRatingMinutes = 90,
                AcousticRating = 48,
                ThermalResistance = 2.0,
                CostPerM2 = 140,
                HasUnderfloorHeating = true,
                Description = "Concrete slab with embedded hydronic heating"
            },
            // Generic types
            ["Generic - 150mm"] = new FloorTypeDefinition
            {
                Name = "Generic - 150mm",
                Thickness = 150,
                IsStructural = false,
                PrimaryMaterial = "Concrete",
                MaxSpan = 5000,
                LiveLoadCapacity = 2.4,
                DeadWeight = 3.6,
                FireRatingMinutes = 90,
                AcousticRating = 45,
                ThermalResistance = 0.1,
                CostPerM2 = 60,
                Description = "Generic 150mm floor"
            },
            ["Generic - 200mm"] = new FloorTypeDefinition
            {
                Name = "Generic - 200mm",
                Thickness = 200,
                IsStructural = true,
                PrimaryMaterial = "Concrete",
                MaxSpan = 6500,
                LiveLoadCapacity = 4.8,
                DeadWeight = 4.8,
                FireRatingMinutes = 120,
                AcousticRating = 50,
                ThermalResistance = 0.13,
                CostPerM2 = 80,
                Description = "Generic 200mm structural floor"
            }
        };

        /// <summary>
        /// Creates a floor from parameters.
        /// </summary>
        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Creating floor with params: {parameters}");

                var result = new CreationResult
                {
                    ElementType = "Floor",
                    StartTime = DateTime.Now
                };

                try
                {
                    // Validate
                    var validation = ValidateParameters(parameters);
                    if (!validation.IsValid)
                    {
                        result.Success = false;
                        result.Error = validation.Error;
                        return result;
                    }

                    // Extract parameters
                    var floorParams = ExtractFloorParameters(parameters);

                    // In real implementation:
                    // Floor.Create(doc, curveLoop, floorTypeId, levelId, structural, slope, slopeDirection)

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = floorParams;
                    result.Message = $"Created {floorParams.Area / 1000000:F1}m² {floorParams.FloorTypeName} floor";

                    result.Metadata["FloorType"] = floorParams.FloorTypeName;
                    result.Metadata["Area"] = floorParams.Area;
                    result.Metadata["Thickness"] = floorParams.Thickness;
                    result.Metadata["Level"] = floorParams.LevelName;

                    // Add intelligence metadata from type catalogue
                    if (FloorTypeCatalogue.TryGetValue(floorParams.FloorTypeName, out var floorDef))
                    {
                        var areaM2 = floorParams.Area / 1000000.0;
                        result.Metadata["FireRating"] = $"{floorDef.FireRatingMinutes} min";
                        result.Metadata["AcousticRating"] = $"IIC {floorDef.AcousticRating}";
                        result.Metadata["ThermalResistance"] = $"R-{floorDef.ThermalResistance:F1}";
                        result.Metadata["LiveLoadCapacity"] = $"{floorDef.LiveLoadCapacity} kN/m²";
                        result.Metadata["Material"] = floorDef.PrimaryMaterial;
                        result.Metadata["EstimatedCost"] = $"${floorDef.CostPerM2 * areaM2:F0}";
                        result.Metadata["EstimatedWeight"] = $"{floorDef.DeadWeight * areaM2 * 100:F0} kg"; // kN to kg approx

                        if (floorDef.MaxSpan > 0)
                            result.Metadata["MaxSpan"] = $"{floorDef.MaxSpan}mm";
                    }

                    Logger.Info($"Floor created: {result.CreatedElementId}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Floor creation failed");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a rectangular floor.
        /// </summary>
        public async Task<CreationResult> CreateRectangularAsync(
            Point3D origin,
            double width,
            double depth,
            FloorCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new FloorCreationOptions();

            // Create boundary points
            var boundary = new List<Point3D>
            {
                origin,
                new Point3D(origin.X + width, origin.Y, origin.Z),
                new Point3D(origin.X + width, origin.Y + depth, origin.Z),
                new Point3D(origin.X, origin.Y + depth, origin.Z)
            };

            var parameters = new ElementCreationParams
            {
                ElementType = "Floor",
                Parameters = new Dictionary<string, object>
                {
                    { "Boundary", boundary },
                    { "Width", width },
                    { "Depth", depth },
                    { "Area", width * depth },
                    { "Thickness", options.Thickness ?? DefaultFloorThickness },
                    { "FloorType", options.FloorTypeName ?? DefaultFloorType },
                    { "Level", options.LevelName },
                    { "IsStructural", options.IsStructural },
                    { "Offset", options.HeightOffset }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates a floor from a closed boundary curve.
        /// </summary>
        public async Task<CreationResult> CreateFromBoundaryAsync(
            IEnumerable<Point3D> boundaryPoints,
            FloorCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new FloorCreationOptions();
            var points = boundaryPoints.ToList();

            if (points.Count < 3)
            {
                return new CreationResult
                {
                    Success = false,
                    Error = "Floor boundary must have at least 3 points"
                };
            }

            var area = CalculatePolygonArea(points);

            var parameters = new ElementCreationParams
            {
                ElementType = "Floor",
                Parameters = new Dictionary<string, object>
                {
                    { "Boundary", points },
                    { "Area", area },
                    { "Thickness", options.Thickness ?? DefaultFloorThickness },
                    { "FloorType", options.FloorTypeName ?? DefaultFloorType },
                    { "Level", options.LevelName },
                    { "IsStructural", options.IsStructural }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates a floor opening (shaft/stair opening).
        /// </summary>
        public async Task<ModificationResult> CreateOpeningAsync(
            int floorElementId,
            IEnumerable<Point3D> openingBoundary,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Creating opening in floor {floorElementId}");

                var result = new ModificationResult
                {
                    ElementId = floorElementId,
                    StartTime = DateTime.Now
                };

                try
                {
                    var points = openingBoundary.ToList();
                    var openingArea = CalculatePolygonArea(points);

                    // In real implementation:
                    // doc.Create.NewOpening(floor, curveArray, true)

                    result.Success = true;
                    result.Message = $"Created {openingArea / 1000000:F2}m² opening";
                    result.ModifiedProperties["OpeningArea"] = openingArea;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Opening creation failed");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Adds a slope to a floor.
        /// </summary>
        public async Task<ModificationResult> AddSlopeAsync(
            int floorElementId,
            double slopeAngle,
            Point3D slopeDirection,
            CancellationToken cancellationToken = default)
        {
            return await ModifyAsync(floorElementId, new Dictionary<string, object>
            {
                { "SlopeAngle", slopeAngle },
                { "SlopeDirection", slopeDirection },
                { "HasSlope", true }
            }, cancellationToken);
        }

        /// <summary>
        /// Modifies an existing floor.
        /// </summary>
        public async Task<ModificationResult> ModifyAsync(
            int elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Modifying floor {elementId}");

                var result = new ModificationResult
                {
                    ElementId = elementId,
                    StartTime = DateTime.Now
                };

                try
                {
                    foreach (var (key, value) in modifications)
                    {
                        result.ModifiedProperties[key] = value;
                    }

                    result.Success = true;
                    result.Message = $"Modified {modifications.Count} properties";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to modify floor {elementId}");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets available floor types.
        /// </summary>
        public IEnumerable<FloorTypeInfo> GetAvailableFloorTypes()
        {
            return FloorTypeCatalogue.Values.Select(def => new FloorTypeInfo
            {
                Id = Math.Abs(def.Name.GetHashCode() % 1000),
                Name = def.Name,
                Thickness = def.Thickness,
                IsStructural = def.IsStructural,
                Material = def.PrimaryMaterial,
                FireRatingMinutes = def.FireRatingMinutes,
                AcousticRating = def.AcousticRating,
                ThermalResistance = def.ThermalResistance,
                LiveLoadCapacity = def.LiveLoadCapacity,
                MaxSpan = def.MaxSpan,
                CostPerM2 = def.CostPerM2
            });
        }

        /// <summary>
        /// Recommends the best floor type based on context.
        /// Uses structural requirements, span, occupancy, and building code compliance.
        /// </summary>
        public FloorRecommendation RecommendFloorType(FloorContext context)
        {
            Logger.Info($"Recommending floor type: span={context.MaxSpanRequired}mm, occupancy={context.OccupancyType}, level={context.FloorLevel}");

            var recommendation = new FloorRecommendation();
            var candidates = FloorTypeCatalogue.Values.ToList();

            // Filter by structural requirement
            if (context.RequiresStructural)
            {
                candidates = candidates.Where(f => f.IsStructural).ToList();
            }

            // Filter by span capability
            if (context.MaxSpanRequired > 0)
            {
                candidates = candidates.Where(f => f.MaxSpan >= context.MaxSpanRequired || f.MaxSpan == 0).ToList();
                recommendation.Notes.Add($"Structural: {context.MaxSpanRequired / 1000.0:F1}m clear span required");
            }

            // Filter by live load capacity per ASCE 7
            var requiredLiveLoad = GetRequiredLiveLoad(context.OccupancyType);
            if (requiredLiveLoad > 0)
            {
                candidates = candidates.Where(f => f.LiveLoadCapacity >= requiredLiveLoad).ToList();
                recommendation.Notes.Add($"ASCE 7: {context.OccupancyType} occupancy requires {requiredLiveLoad} kN/m² live load capacity");
            }

            // Filter by fire rating
            var requiredFireRating = GetRequiredFireRating(context.OccupancyType, context.BuildingHeight);
            if (requiredFireRating > 0)
            {
                candidates = candidates.Where(f => f.FireRatingMinutes >= requiredFireRating).ToList();
                recommendation.Notes.Add($"IBC: {requiredFireRating}-minute fire rating required for floor assembly");
            }

            // Ground floor needs damp proofing
            if (context.FloorLevel == 0 || context.IsGroundBearing)
            {
                var groundFloors = candidates.Where(f => f.RequiresDampProofing || f.Name.Contains("Ground")).ToList();
                if (groundFloors.Count > 0)
                    candidates = groundFloors;
                recommendation.Notes.Add("Ground floor: Damp-proof membrane (DPM) required per building regulations");
            }

            // Score and rank
            var scored = candidates.Select(c => new
            {
                FloorType = c,
                Score = ScoreFloorType(c, context)
            })
            .OrderByDescending(s => s.Score)
            .ToList();

            if (scored.Count == 0)
            {
                recommendation.RecommendedType = DefaultFloorType;
                recommendation.Confidence = 0.3;
                recommendation.Notes.Add("Warning: No floor type fully matches requirements. Consider custom slab design.");
                return recommendation;
            }

            var best = scored.First();
            recommendation.RecommendedType = best.FloorType.Name;
            recommendation.Confidence = Math.Min(1.0, best.Score);
            recommendation.Thickness = best.FloorType.Thickness;
            recommendation.IsStructural = best.FloorType.IsStructural;
            recommendation.FireRatingMinutes = best.FloorType.FireRatingMinutes;
            recommendation.LiveLoadCapacity = best.FloorType.LiveLoadCapacity;
            recommendation.EstimatedCostPerM2 = best.FloorType.CostPerM2;
            recommendation.Material = best.FloorType.PrimaryMaterial;

            // Add alternatives
            recommendation.Alternatives = scored.Skip(1).Take(2).Select(s => new FloorAlternative
            {
                TypeName = s.FloorType.Name,
                Reason = CompareFloorTypes(best.FloorType, s.FloorType),
                CostDifference = s.FloorType.CostPerM2 - best.FloorType.CostPerM2
            }).ToList();

            // Acoustic requirements between floors
            if (context.FloorLevel > 0 && context.HasResidentialAboveOrBelow)
            {
                if (best.FloorType.AcousticRating < 50)
                    recommendation.Notes.Add($"IBC 1207: Floor/ceiling assembly between dwelling units requires IIC 50+. Current: IIC {best.FloorType.AcousticRating}. Consider adding resilient channel or acoustic mat.");
            }

            Logger.Info($"Recommended: {recommendation.RecommendedType} (confidence: {recommendation.Confidence:F2})");
            return recommendation;
        }

        /// <summary>
        /// Validates slope for accessibility compliance.
        /// </summary>
        public SlopeValidation ValidateSlope(double slopePercent, string purpose)
        {
            var validation = new SlopeValidation
            {
                SlopePercent = slopePercent,
                Purpose = purpose
            };

            switch (purpose?.ToLowerInvariant())
            {
                case "ramp":
                case "accessible ramp":
                    // ADA: max 1:12 slope (8.33%) for ramps
                    if (slopePercent > 8.33)
                    {
                        validation.IsCompliant = false;
                        validation.Code = "ADA 405.2";
                        validation.Message = $"Ramp slope {slopePercent:F1}% exceeds ADA maximum 8.33% (1:12). " +
                                           "Reduce slope or increase ramp length.";
                        validation.MaxAllowedSlope = 8.33;
                    }
                    else if (slopePercent > 5.0)
                    {
                        validation.IsCompliant = true;
                        validation.Message = $"Ramp slope {slopePercent:F1}% compliant but steep. " +
                                           "ADA 405.7: Handrails required on both sides. " +
                                           "Max run 9000mm before landing.";
                    }
                    else
                    {
                        validation.IsCompliant = true;
                        validation.Message = $"Ramp slope {slopePercent:F1}% is compliant and comfortable.";
                    }

                    // ADA: landings required
                    validation.RequiresLandings = slopePercent > 5.0;
                    validation.RequiresHandrails = slopePercent > 5.0;
                    break;

                case "parking":
                case "parking ramp":
                    // Typical parking ramp max: 15-20%
                    if (slopePercent > 20)
                    {
                        validation.IsCompliant = false;
                        validation.Code = "IBC 406.2.5";
                        validation.Message = $"Parking ramp slope {slopePercent:F1}% exceeds recommended maximum 20%.";
                        validation.MaxAllowedSlope = 20;
                    }
                    else
                    {
                        validation.IsCompliant = true;
                        validation.Message = $"Parking ramp slope {slopePercent:F1}% is compliant.";
                    }
                    break;

                case "drainage":
                case "wet area":
                    // Drainage slope: typically 1-2% (1:100 to 1:50)
                    if (slopePercent < 1.0)
                    {
                        validation.IsCompliant = false;
                        validation.Message = $"Drainage slope {slopePercent:F1}% too shallow. " +
                                           "Minimum 1% (1:100) for drainage to prevent ponding.";
                    }
                    else if (slopePercent > 2.0)
                    {
                        validation.IsCompliant = true;
                        validation.Message = $"Drainage slope {slopePercent:F1}% is adequate but may cause slipping. " +
                                           "Recommend slip-resistant finish.";
                    }
                    else
                    {
                        validation.IsCompliant = true;
                        validation.Message = $"Drainage slope {slopePercent:F1}% is optimal for drainage.";
                    }
                    break;

                default:
                    // General floor slope - ADA: max 2% for accessible routes
                    if (slopePercent > 2.0)
                    {
                        validation.IsCompliant = false;
                        validation.Code = "ADA 403.3";
                        validation.Message = $"Floor slope {slopePercent:F1}% exceeds ADA max 2% for accessible routes. " +
                                           "Slopes >5% require ramp design.";
                        validation.MaxAllowedSlope = 2.0;
                    }
                    else
                    {
                        validation.IsCompliant = true;
                        validation.Message = $"Floor slope {slopePercent:F1}% is ADA compliant for accessible routes.";
                    }
                    break;
            }

            return validation;
        }

        /// <summary>
        /// Calculates structural loading for a floor based on occupancy.
        /// </summary>
        public FloorLoadingAnalysis AnalyzeLoading(string floorTypeName, string occupancyType, double areaM2)
        {
            var analysis = new FloorLoadingAnalysis
            {
                FloorType = floorTypeName,
                OccupancyType = occupancyType,
                AreaM2 = areaM2
            };

            // Get floor definition
            if (!FloorTypeCatalogue.TryGetValue(floorTypeName, out var floorDef))
                floorDef = FloorTypeCatalogue[DefaultFloorType];

            // Dead load (self-weight)
            analysis.DeadLoadPerM2 = floorDef.DeadWeight;

            // Live load per ASCE 7
            analysis.LiveLoadPerM2 = GetRequiredLiveLoad(occupancyType);

            // Superimposed dead load (finishes, MEP, partitions)
            analysis.SuperimposedDeadLoadPerM2 = GetSuperimposedDeadLoad(occupancyType);

            // Total loading
            analysis.TotalLoadPerM2 = analysis.DeadLoadPerM2 + analysis.LiveLoadPerM2 + analysis.SuperimposedDeadLoadPerM2;

            // Factored load (ASCE 7 LRFD: 1.2D + 1.6L)
            analysis.FactoredLoadPerM2 = 1.2 * (analysis.DeadLoadPerM2 + analysis.SuperimposedDeadLoadPerM2) +
                                         1.6 * analysis.LiveLoadPerM2;

            // Total load on floor
            analysis.TotalLoadKN = analysis.TotalLoadPerM2 * areaM2;
            analysis.FactoredLoadKN = analysis.FactoredLoadPerM2 * areaM2;

            // Capacity check
            analysis.IsAdequate = floorDef.LiveLoadCapacity >= analysis.LiveLoadPerM2;

            if (!analysis.IsAdequate)
            {
                analysis.Notes.Add($"WARNING: Floor live load capacity ({floorDef.LiveLoadCapacity} kN/m²) " +
                                  $"is less than required ({analysis.LiveLoadPerM2} kN/m²) for {occupancyType} occupancy.");
            }

            analysis.Notes.Add($"ASCE 7 load combination: 1.2D + 1.6L = {analysis.FactoredLoadPerM2:F1} kN/m²");
            analysis.Notes.Add($"Total factored load on floor: {analysis.FactoredLoadKN:F0} kN ({analysis.FactoredLoadKN / 9.81:F0} tonnes)");

            return analysis;
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.Parameters.TryGetValue("Area", out var areaObj))
            {
                var area = Convert.ToDouble(areaObj);
                if (area < MinFloorArea)
                {
                    return ValidationResult.Invalid($"Floor area ({area / 1000000:F2}m²) is too small");
                }
                if (area > MaxFloorArea)
                {
                    return ValidationResult.Invalid($"Floor area ({area / 1000000:F0}m²) exceeds maximum");
                }
            }

            if (parameters.Parameters.TryGetValue("Boundary", out var boundary))
            {
                if (boundary is List<Point3D> points && points.Count < 3)
                {
                    return ValidationResult.Invalid("Floor boundary must have at least 3 points");
                }
            }

            // Structural span check
            if (parameters.Parameters.TryGetValue("Width", out var widthObj) &&
                parameters.Parameters.TryGetValue("FloorType", out var typeObj))
            {
                var span = Convert.ToDouble(widthObj);
                var typeName = typeObj.ToString();

                if (FloorTypeCatalogue.TryGetValue(typeName, out var floorDef) && floorDef.MaxSpan > 0)
                {
                    var maxDim = span;
                    if (parameters.Parameters.TryGetValue("Depth", out var depthObj))
                    {
                        maxDim = Math.Min(Convert.ToDouble(depthObj), span); // shorter span governs
                    }

                    if (maxDim > floorDef.MaxSpan)
                    {
                        result.Warnings.Add($"Structural: Floor span {maxDim / 1000:F1}m exceeds {floorDef.Name} max span " +
                                          $"{floorDef.MaxSpan / 1000:F1}m. Consider intermediate support or post-tensioned slab.");
                    }
                    else if (maxDim > floorDef.MaxSpan * 0.85)
                    {
                        result.Warnings.Add($"Floor span {maxDim / 1000:F1}m is near maximum for {floorDef.Name}. " +
                                          $"Verify with structural engineer.");
                    }
                }
            }

            // Slope accessibility check
            if (parameters.Parameters.TryGetValue("SlopeAngle", out var slopeObj))
            {
                var slopePercent = Convert.ToDouble(slopeObj);
                if (slopePercent > 2.0)
                {
                    result.Warnings.Add($"ADA 403.3: Floor slope {slopePercent:F1}% exceeds 2% maximum for accessible routes. " +
                                      "If this is a ramp, use AddSlopeAsync with proper ramp design.");
                }
            }

            return result;
        }

        private FloorParameters ExtractFloorParameters(ElementCreationParams parameters)
        {
            var typeName = GetString(parameters.Parameters, "FloorType", DefaultFloorType);
            var thickness = DefaultFloorThickness;

            if (FloorTypeCatalogue.TryGetValue(typeName, out var floorDef))
            {
                thickness = floorDef.Thickness;
            }

            var boundary = parameters.Parameters.GetValueOrDefault("Boundary") as List<Point3D>;

            return new FloorParameters
            {
                Boundary = boundary ?? new List<Point3D>(),
                Area = GetDouble(parameters.Parameters, "Area", 0),
                Thickness = GetDouble(parameters.Parameters, "Thickness", thickness),
                FloorTypeName = typeName,
                LevelName = GetString(parameters.Parameters, "Level", "Level 1"),
                IsStructural = GetBool(parameters.Parameters, "IsStructural", floorDef?.IsStructural ?? false),
                HeightOffset = GetDouble(parameters.Parameters, "Offset", 0)
            };
        }

        private double ScoreFloorType(FloorTypeDefinition floorType, FloorContext context)
        {
            double score = 0.5;

            // Span capability match
            if (context.MaxSpanRequired > 0 && floorType.MaxSpan >= context.MaxSpanRequired)
            {
                score += 0.2;
                // Penalize over-designed for span
                if (floorType.MaxSpan > context.MaxSpanRequired * 2)
                    score -= 0.05;
            }

            // Load capacity match
            var requiredLoad = GetRequiredLiveLoad(context.OccupancyType);
            if (floorType.LiveLoadCapacity >= requiredLoad)
                score += 0.15;

            // Cost efficiency
            if (context.PreferLowCost)
                score += 0.1 * (1 - floorType.CostPerM2 / 200.0);

            // Sustainability preference
            if (context.PreferSustainable && floorType.PrimaryMaterial.Contains("Timber"))
                score += 0.1;

            // Thermal performance for ground floor
            if (context.IsGroundBearing && floorType.ThermalResistance > 2.0)
                score += 0.1;

            // Acoustic for multi-storey
            if (context.HasResidentialAboveOrBelow && floorType.AcousticRating >= 50)
                score += 0.1;

            return Math.Max(0, Math.Min(1, score));
        }

        private double GetRequiredLiveLoad(string occupancyType)
        {
            // ASCE 7 Table 4.3-1: Minimum uniformly distributed live loads
            return (occupancyType?.ToLowerInvariant()) switch
            {
                "residential" or "bedroom" or "living room" => 1.92, // 40 psf = 1.92 kN/m²
                "office" => 2.4, // 50 psf
                "retail" or "store" => 4.8, // 100 psf first floor
                "assembly" or "conference" or "reception" => 4.8, // 100 psf
                "classroom" or "educational" => 1.92, // 40 psf
                "corridor" => 4.8, // 100 psf (public)
                "hospital" or "healthcare" => 1.92, // 40 psf
                "library" or "reading room" => 2.87, // 60 psf
                "library stacks" => 7.18, // 150 psf
                "parking" or "garage" => 2.4, // 50 psf
                "manufacturing" or "industrial" => 6.0, // 125 psf
                "warehouse" or "storage" => 6.0, // 125 psf light, 12 kN/m² heavy
                "server room" or "data centre" => 7.2, // equipment
                "rooftop" => 0.96, // 20 psf
                _ => 2.4 // default to office
            };
        }

        private int GetRequiredFireRating(string occupancyType, double buildingHeightM)
        {
            // IBC Table 601: Fire-resistance rating based on construction type and height
            if (buildingHeightM > 23) return 120; // Type I: >75ft
            if (buildingHeightM > 16) return 90; // Type II: >55ft

            return (occupancyType?.ToLowerInvariant()) switch
            {
                "assembly" or "educational" => 60,
                "healthcare" or "hospital" => 120,
                "residential" or "hotel" => 60,
                _ => 60
            };
        }

        private double GetSuperimposedDeadLoad(string occupancyType)
        {
            // Typical superimposed dead loads (finishes, MEP, partitions)
            return (occupancyType?.ToLowerInvariant()) switch
            {
                "office" => 1.5, // partitions + finishes + MEP
                "residential" => 1.0,
                "healthcare" => 2.0, // heavy MEP
                "retail" => 1.2,
                "server room" => 2.5, // heavy cable trays, raised floor
                _ => 1.2
            };
        }

        private string CompareFloorTypes(FloorTypeDefinition recommended, FloorTypeDefinition alternative)
        {
            var differences = new List<string>();

            if (alternative.CostPerM2 < recommended.CostPerM2)
                differences.Add($"${recommended.CostPerM2 - alternative.CostPerM2:F0}/m² cheaper");

            if (alternative.MaxSpan > recommended.MaxSpan)
                differences.Add($"{alternative.MaxSpan / 1000.0:F1}m span (longer)");

            if (alternative.AcousticRating > recommended.AcousticRating)
                differences.Add($"IIC {alternative.AcousticRating} (better acoustic)");

            if (alternative.ThermalResistance > recommended.ThermalResistance)
                differences.Add($"R-{alternative.ThermalResistance:F1} (better thermal)");

            if (alternative.DeadWeight < recommended.DeadWeight)
                differences.Add($"{alternative.DeadWeight:F1} kN/m² (lighter)");

            return differences.Count > 0 ? string.Join("; ", differences) : "Similar performance";
        }

        private double CalculatePolygonArea(List<Point3D> points)
        {
            if (points.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var j = (i + 1) % points.Count;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            return Math.Abs(area / 2);
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString(), out d)) return d;
            }
            return defaultValue;
        }

        private string GetString(Dictionary<string, object> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
            return defaultValue;
        }

        private bool GetBool(Dictionary<string, object> dict, string key, bool defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is bool b) return b;
                if (bool.TryParse(value?.ToString(), out b)) return b;
            }
            return defaultValue;
        }

        private int GenerateElementId() => new Random().Next(100000, 999999);

        #endregion
    }

    #region Supporting Classes

    public class FloorCreationOptions
    {
        public double? Thickness { get; set; }
        public string FloorTypeName { get; set; }
        public string LevelName { get; set; }
        public bool IsStructural { get; set; }
        public double HeightOffset { get; set; }
        public bool HasSlope { get; set; }
        public double SlopeAngle { get; set; }
    }

    public class FloorParameters
    {
        public List<Point3D> Boundary { get; set; }
        public double Area { get; set; }
        public double Thickness { get; set; }
        public string FloorTypeName { get; set; }
        public string LevelName { get; set; }
        public bool IsStructural { get; set; }
        public double HeightOffset { get; set; }
    }

    public class FloorTypeInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Thickness { get; set; }
        public bool IsStructural { get; set; }
        public string Material { get; set; }
        public int FireRatingMinutes { get; set; }
        public int AcousticRating { get; set; }
        public double ThermalResistance { get; set; }
        public double LiveLoadCapacity { get; set; }
        public double MaxSpan { get; set; }
        public double CostPerM2 { get; set; }
    }

    public class FloorTypeDefinition
    {
        public string Name { get; set; }
        public double Thickness { get; set; }
        public bool IsStructural { get; set; }
        public string PrimaryMaterial { get; set; }
        public double MaxSpan { get; set; } // mm (0 = ground-bearing)
        public double LiveLoadCapacity { get; set; } // kN/m²
        public double DeadWeight { get; set; } // kN/m² (self-weight)
        public int FireRatingMinutes { get; set; }
        public int AcousticRating { get; set; } // IIC
        public double ThermalResistance { get; set; } // R-value m²·K/W
        public double CostPerM2 { get; set; }
        public double VoidHeight { get; set; } // for raised floors
        public bool RequiresDampProofing { get; set; }
        public bool HasUnderfloorHeating { get; set; }
        public string Description { get; set; }
    }

    public class FloorContext
    {
        public double MaxSpanRequired { get; set; } // mm
        public string OccupancyType { get; set; }
        public int FloorLevel { get; set; } // 0 = ground
        public double BuildingHeight { get; set; } // m
        public bool IsGroundBearing { get; set; }
        public bool RequiresStructural { get; set; }
        public bool HasResidentialAboveOrBelow { get; set; }
        public bool PreferLowCost { get; set; }
        public bool PreferSustainable { get; set; }
    }

    public class FloorRecommendation
    {
        public string RecommendedType { get; set; }
        public double Confidence { get; set; }
        public double Thickness { get; set; }
        public bool IsStructural { get; set; }
        public int FireRatingMinutes { get; set; }
        public double LiveLoadCapacity { get; set; }
        public double EstimatedCostPerM2 { get; set; }
        public string Material { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
        public List<FloorAlternative> Alternatives { get; set; } = new List<FloorAlternative>();
    }

    public class FloorAlternative
    {
        public string TypeName { get; set; }
        public string Reason { get; set; }
        public double CostDifference { get; set; }
    }

    public class SlopeValidation
    {
        public double SlopePercent { get; set; }
        public string Purpose { get; set; }
        public bool IsCompliant { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public double MaxAllowedSlope { get; set; }
        public bool RequiresLandings { get; set; }
        public bool RequiresHandrails { get; set; }
    }

    public class FloorLoadingAnalysis
    {
        public string FloorType { get; set; }
        public string OccupancyType { get; set; }
        public double AreaM2 { get; set; }
        public double DeadLoadPerM2 { get; set; } // kN/m²
        public double LiveLoadPerM2 { get; set; } // kN/m²
        public double SuperimposedDeadLoadPerM2 { get; set; } // kN/m²
        public double TotalLoadPerM2 { get; set; } // kN/m²
        public double FactoredLoadPerM2 { get; set; } // kN/m² (LRFD)
        public double TotalLoadKN { get; set; }
        public double FactoredLoadKN { get; set; }
        public bool IsAdequate { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
    }

    #endregion
}
