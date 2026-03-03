// StingBIM.AI.Creation.Elements.WallCreator
// Creates walls in Revit from AI commands with intelligent type selection
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
    /// Creates and modifies walls in Revit based on natural language commands.
    /// Handles straight walls, curved walls, and wall modifications.
    /// Provides intelligent wall type selection based on context, fire/acoustic ratings,
    /// material properties, and building code compliance.
    /// </summary>
    public class WallCreator : IElementCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Default values (can be overridden by user preferences)
        private const double DefaultWallHeight = 2700; // mm
        private const double DefaultWallThickness = 200; // mm
        private const string DefaultWallType = "Basic Wall";
        private const double MinWallLength = 100; // mm
        private const double MaxWallLength = 100000; // mm (100m)

        // Comprehensive wall type catalogue with intelligence properties
        private static readonly Dictionary<string, BasicWallType> WallTypeCatalogue =
            new Dictionary<string, BasicWallType>(StringComparer.OrdinalIgnoreCase)
        {
            // Interior partition walls
            ["Interior - Partition"] = new BasicWallType
            {
                Name = "Interior - Partition",
                Thickness = 100,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 0,
                STCAcousticRating = 33,
                ThermalResistance = 0.4, // R-value m²·K/W
                PrimaryMaterial = "Gypsum Board on Metal Stud",
                CostPerM2 = 45,
                WeightPerM2 = 25, // kg/m²
                Description = "Light non-load-bearing partition wall"
            },
            ["Interior - Fire Rated 1hr"] = new BasicWallType
            {
                Name = "Interior - Fire Rated 1hr",
                Thickness = 150,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 60,
                STCAcousticRating = 45,
                ThermalResistance = 0.7,
                PrimaryMaterial = "Double Gypsum Board on Metal Stud",
                CostPerM2 = 75,
                WeightPerM2 = 40,
                Description = "1-hour fire-rated partition (NFPA/IBC corridor, exit enclosure)"
            },
            ["Interior - Fire Rated 2hr"] = new BasicWallType
            {
                Name = "Interior - Fire Rated 2hr",
                Thickness = 200,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 120,
                STCAcousticRating = 52,
                ThermalResistance = 0.9,
                PrimaryMaterial = "Triple Gypsum Board on Metal Stud",
                CostPerM2 = 110,
                WeightPerM2 = 55,
                Description = "2-hour fire-rated wall (IBC shaft, stair enclosure)"
            },
            ["Interior - Acoustic"] = new BasicWallType
            {
                Name = "Interior - Acoustic",
                Thickness = 200,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 60,
                STCAcousticRating = 55,
                ThermalResistance = 0.8,
                PrimaryMaterial = "Staggered Stud with Acoustic Insulation",
                CostPerM2 = 95,
                WeightPerM2 = 45,
                Description = "High acoustic performance wall (STC 55+, conference rooms, studios)"
            },
            ["Interior - Wet Area"] = new BasicWallType
            {
                Name = "Interior - Wet Area",
                Thickness = 150,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 0,
                STCAcousticRating = 38,
                ThermalResistance = 0.5,
                PrimaryMaterial = "Moisture-Resistant Gypsum Board on Metal Stud",
                CostPerM2 = 65,
                WeightPerM2 = 35,
                RequiresMoistureBarrier = true,
                Description = "Moisture-resistant partition for bathrooms, kitchens"
            },
            // Structural interior walls
            ["Interior - Load Bearing CMU"] = new BasicWallType
            {
                Name = "Interior - Load Bearing CMU",
                Thickness = 200,
                IsStructural = true,
                Location = WallLocation.Interior,
                FireRatingMinutes = 120,
                STCAcousticRating = 48,
                ThermalResistance = 0.6,
                PrimaryMaterial = "Concrete Masonry Unit",
                CostPerM2 = 130,
                WeightPerM2 = 200,
                MaxLoadBearing = 70, // kN/m
                Description = "Load-bearing CMU wall for structural support"
            },
            ["Interior - Concrete"] = new BasicWallType
            {
                Name = "Interior - Concrete",
                Thickness = 200,
                IsStructural = true,
                Location = WallLocation.Interior,
                FireRatingMinutes = 120,
                STCAcousticRating = 52,
                ThermalResistance = 0.3,
                PrimaryMaterial = "Reinforced Concrete",
                CostPerM2 = 180,
                WeightPerM2 = 480,
                MaxLoadBearing = 150, // kN/m
                Description = "RC shear wall / structural wall"
            },
            // Exterior walls
            ["Exterior - Brick on CMU"] = new BasicWallType
            {
                Name = "Exterior - Brick on CMU",
                Thickness = 350,
                IsStructural = true,
                Location = WallLocation.Exterior,
                FireRatingMinutes = 120,
                STCAcousticRating = 50,
                ThermalResistance = 2.0,
                PrimaryMaterial = "Brick Veneer on CMU with Cavity Insulation",
                CostPerM2 = 220,
                WeightPerM2 = 350,
                MaxLoadBearing = 80,
                Description = "Traditional masonry cavity wall (good thermal mass)"
            },
            ["Exterior - Insulated Metal Panel"] = new BasicWallType
            {
                Name = "Exterior - Insulated Metal Panel",
                Thickness = 150,
                IsStructural = false,
                Location = WallLocation.Exterior,
                FireRatingMinutes = 60,
                STCAcousticRating = 35,
                ThermalResistance = 3.5,
                PrimaryMaterial = "Insulated Metal Panel System",
                CostPerM2 = 160,
                WeightPerM2 = 30,
                Description = "Lightweight insulated cladding (industrial/commercial)"
            },
            ["Exterior - Stud with EIFS"] = new BasicWallType
            {
                Name = "Exterior - Stud with EIFS",
                Thickness = 250,
                IsStructural = false,
                Location = WallLocation.Exterior,
                FireRatingMinutes = 60,
                STCAcousticRating = 42,
                ThermalResistance = 3.0,
                PrimaryMaterial = "Metal Stud with EIFS Cladding and Insulation",
                CostPerM2 = 145,
                WeightPerM2 = 50,
                Description = "EIFS exterior wall (residential/commercial)"
            },
            ["Exterior - Concrete Insulated"] = new BasicWallType
            {
                Name = "Exterior - Concrete Insulated",
                Thickness = 300,
                IsStructural = true,
                Location = WallLocation.Exterior,
                FireRatingMinutes = 120,
                STCAcousticRating = 55,
                ThermalResistance = 2.8,
                PrimaryMaterial = "Insulated Concrete Form (ICF)",
                CostPerM2 = 250,
                WeightPerM2 = 400,
                MaxLoadBearing = 120,
                Description = "ICF structural exterior wall (high performance)"
            },
            ["Exterior - Rammed Earth"] = new BasicWallType
            {
                Name = "Exterior - Rammed Earth",
                Thickness = 400,
                IsStructural = true,
                Location = WallLocation.Exterior,
                FireRatingMinutes = 120,
                STCAcousticRating = 50,
                ThermalResistance = 0.6,
                PrimaryMaterial = "Stabilized Rammed Earth",
                CostPerM2 = 120,
                WeightPerM2 = 800,
                MaxLoadBearing = 60,
                Description = "Sustainable rammed earth wall (excellent thermal mass, Africa-suitable)"
            },
            // Curtain/glazed walls
            ["Curtain Wall"] = new BasicWallType
            {
                Name = "Curtain Wall",
                Thickness = 50,
                IsStructural = false,
                Location = WallLocation.Exterior,
                FireRatingMinutes = 0,
                STCAcousticRating = 30,
                ThermalResistance = 0.5,
                PrimaryMaterial = "Aluminum Frame with Double Glazing",
                CostPerM2 = 350,
                WeightPerM2 = 40,
                Description = "Glass curtain wall system (commercial facades)"
            },
            // Retaining / foundation walls
            ["Retaining - Concrete"] = new BasicWallType
            {
                Name = "Retaining - Concrete",
                Thickness = 300,
                IsStructural = true,
                Location = WallLocation.BelowGrade,
                FireRatingMinutes = 120,
                STCAcousticRating = 55,
                ThermalResistance = 0.3,
                PrimaryMaterial = "Reinforced Concrete with Waterproofing",
                CostPerM2 = 280,
                WeightPerM2 = 720,
                MaxLoadBearing = 200,
                RequiresMoistureBarrier = true,
                Description = "Below-grade retaining wall with waterproofing"
            },
            // Generic fallback
            ["Basic Wall"] = new BasicWallType
            {
                Name = "Basic Wall",
                Thickness = 200,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 0,
                STCAcousticRating = 35,
                ThermalResistance = 0.5,
                PrimaryMaterial = "Gypsum Board on Metal Stud",
                CostPerM2 = 50,
                WeightPerM2 = 30,
                Description = "Generic interior wall"
            },
            ["Generic - 200mm"] = new BasicWallType
            {
                Name = "Generic - 200mm",
                Thickness = 200,
                IsStructural = false,
                Location = WallLocation.Interior,
                FireRatingMinutes = 0,
                STCAcousticRating = 35,
                ThermalResistance = 0.5,
                PrimaryMaterial = "Gypsum Board on Metal Stud",
                CostPerM2 = 50,
                WeightPerM2 = 30,
                Description = "Generic 200mm wall"
            },
            ["Generic - 300mm"] = new BasicWallType
            {
                Name = "Generic - 300mm",
                Thickness = 300,
                IsStructural = true,
                Location = WallLocation.Interior,
                FireRatingMinutes = 60,
                STCAcousticRating = 45,
                ThermalResistance = 0.8,
                PrimaryMaterial = "Concrete Block",
                CostPerM2 = 90,
                WeightPerM2 = 150,
                MaxLoadBearing = 50,
                Description = "Generic 300mm structural wall"
            }
        };

        /// <summary>
        /// Creates a straight wall from parameters.
        /// </summary>
        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Creating wall with params: {parameters}");

                var result = new CreationResult
                {
                    ElementType = "Wall",
                    StartTime = DateTime.Now
                };

                try
                {
                    // Validate parameters
                    var validation = ValidateParameters(parameters);
                    if (!validation.IsValid)
                    {
                        result.Success = false;
                        result.Error = validation.Error;
                        return result;
                    }

                    // Extract wall parameters
                    var wallParams = ExtractWallParameters(parameters);

                    // In real implementation, this would call Revit API:
                    // Wall.Create(doc, curve, wallTypeId, levelId, height, offset, flip, structural)

                    // Simulate creation for now
                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = wallParams;
                    result.Message = $"Created {wallParams.Length:F0}mm {wallParams.WallTypeName} wall";

                    // Add metadata
                    result.Metadata["WallType"] = wallParams.WallTypeName;
                    result.Metadata["Length"] = wallParams.Length;
                    result.Metadata["Height"] = wallParams.Height;
                    result.Metadata["Level"] = wallParams.LevelName;

                    // Add intelligence metadata from type catalogue
                    if (WallTypeCatalogue.TryGetValue(wallParams.WallTypeName, out var wallDef))
                    {
                        result.Metadata["FireRating"] = $"{wallDef.FireRatingMinutes} min";
                        result.Metadata["AcousticRating"] = $"STC {wallDef.STCAcousticRating}";
                        result.Metadata["ThermalResistance"] = $"R-{wallDef.ThermalResistance:F1}";
                        result.Metadata["Material"] = wallDef.PrimaryMaterial;
                        result.Metadata["EstimatedCost"] = $"${wallDef.CostPerM2 * wallParams.Length * wallParams.Height / 1000000:F0}";
                        result.Metadata["EstimatedWeight"] = $"{wallDef.WeightPerM2 * wallParams.Length * wallParams.Height / 1000000:F0} kg";

                        if (wallDef.IsStructural)
                            result.Metadata["StructuralCapacity"] = $"{wallDef.MaxLoadBearing} kN/m";
                    }

                    Logger.Info($"Wall created: {result.CreatedElementId}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Wall creation failed");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a wall by specifying start and end points.
        /// </summary>
        public async Task<CreationResult> CreateByPointsAsync(
            Point3D startPoint,
            Point3D endPoint,
            WallCreatorOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new WallCreatorOptions();

            var length = CalculateDistance(startPoint, endPoint);

            var parameters = new ElementCreationParams
            {
                ElementType = "Wall",
                Parameters = new Dictionary<string, object>
                {
                    { "StartPoint", startPoint },
                    { "EndPoint", endPoint },
                    { "Length", length },
                    { "Height", options.Height ?? DefaultWallHeight },
                    { "WallType", options.WallTypeName ?? DefaultWallType },
                    { "Level", options.LevelName },
                    { "IsStructural", options.IsStructural }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates a rectangular room outline (4 walls).
        /// </summary>
        public async Task<BatchCreationResult> CreateRectangleAsync(
            Point3D origin,
            double width,
            double depth,
            WallCreatorOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Creating rectangular wall outline: {width}x{depth}mm at {origin}");

            options ??= new WallCreatorOptions();
            var results = new BatchCreationResult { StartTime = DateTime.Now };

            // Calculate corner points
            var p1 = origin;
            var p2 = new Point3D(origin.X + width, origin.Y, origin.Z);
            var p3 = new Point3D(origin.X + width, origin.Y + depth, origin.Z);
            var p4 = new Point3D(origin.X, origin.Y + depth, origin.Z);

            // Create 4 walls
            var walls = new[]
            {
                (Start: p1, End: p2, Name: "South"),
                (Start: p2, End: p3, Name: "East"),
                (Start: p3, End: p4, Name: "North"),
                (Start: p4, End: p1, Name: "West")
            };

            foreach (var wall in walls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await CreateByPointsAsync(wall.Start, wall.End, options, cancellationToken);
                results.Results.Add(result);

                if (!result.Success)
                {
                    Logger.Warn($"Failed to create {wall.Name} wall: {result.Error}");
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            return results;
        }

        /// <summary>
        /// Modifies an existing wall's properties.
        /// </summary>
        public async Task<ModificationResult> ModifyAsync(
            int elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Modifying wall {elementId}");

                var result = new ModificationResult
                {
                    ElementId = elementId,
                    StartTime = DateTime.Now
                };

                try
                {
                    // In real implementation, would modify via Revit API
                    foreach (var (key, value) in modifications)
                    {
                        result.ModifiedProperties[key] = value;

                        switch (key.ToUpperInvariant())
                        {
                            case "HEIGHT":
                                Logger.Debug($"Setting wall height to {value}");
                                break;
                            case "WALLTYPE":
                                Logger.Debug($"Changing wall type to {value}");
                                break;
                            case "LOCATION":
                                Logger.Debug($"Moving wall to {value}");
                                break;
                        }
                    }

                    result.Success = true;
                    result.Message = $"Modified {modifications.Count} properties";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to modify wall {elementId}");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Extends or trims a wall to a specified length or boundary.
        /// </summary>
        public async Task<ModificationResult> ExtendAsync(
            int wallElementId,
            double newLength,
            ExtendDirection direction = ExtendDirection.End,
            CancellationToken cancellationToken = default)
        {
            return await ModifyAsync(wallElementId, new Dictionary<string, object>
            {
                { "Length", newLength },
                { "ExtendDirection", direction.ToString() }
            }, cancellationToken);
        }

        /// <summary>
        /// Splits a wall at a specified point.
        /// </summary>
        public async Task<BatchCreationResult> SplitAsync(
            int wallElementId,
            Point3D splitPoint,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Splitting wall {wallElementId} at {splitPoint}");

            // In real implementation, would:
            // 1. Get wall geometry
            // 2. Calculate split point on wall curve
            // 3. Delete original wall
            // 4. Create two new walls

            var result = new BatchCreationResult { StartTime = DateTime.Now };

            // Placeholder for split operation
            result.Results.Add(new CreationResult
            {
                Success = true,
                CreatedElementId = GenerateElementId(),
                Message = "First wall segment"
            });

            result.Results.Add(new CreationResult
            {
                Success = true,
                CreatedElementId = GenerateElementId(),
                Message = "Second wall segment"
            });

            result.TotalCreated = 2;
            result.EndTime = DateTime.Now;

            return result;
        }

        /// <summary>
        /// Gets available wall types in the current document.
        /// </summary>
        public IEnumerable<WallTypeInfo> GetAvailableWallTypes()
        {
            return WallTypeCatalogue.Values.Select(def => new WallTypeInfo
            {
                Id = Math.Abs(def.Name.GetHashCode() % 1000),
                Name = def.Name,
                Thickness = def.Thickness,
                IsStructural = def.IsStructural,
                Material = def.PrimaryMaterial,
                FireRatingMinutes = def.FireRatingMinutes,
                STCAcousticRating = def.STCAcousticRating,
                ThermalResistance = def.ThermalResistance,
                CostPerM2 = def.CostPerM2
            });
        }

        /// <summary>
        /// Recommends the best wall type based on context.
        /// Uses building code requirements, acoustic needs, fire rating, and location.
        /// </summary>
        public WallRecommendation RecommendWallType(WallContext context)
        {
            Logger.Info($"Recommending wall type for context: {context.Location}, structural={context.RequiresStructural}, fire={context.RequiredFireRating}min");

            var recommendation = new WallRecommendation();
            var candidates = WallTypeCatalogue.Values.ToList();

            // Filter by location
            if (context.Location != WallLocation.Any)
            {
                candidates = candidates.Where(w => w.Location == context.Location).ToList();
            }

            // Filter by structural requirement
            if (context.RequiresStructural)
            {
                candidates = candidates.Where(w => w.IsStructural).ToList();
            }

            // Filter by minimum fire rating
            if (context.RequiredFireRating > 0)
            {
                candidates = candidates.Where(w => w.FireRatingMinutes >= context.RequiredFireRating).ToList();
                recommendation.Notes.Add($"NFPA/IBC: {context.RequiredFireRating}-minute fire rating required for this assembly");
            }

            // Filter by minimum acoustic rating
            if (context.MinAcousticRating > 0)
            {
                candidates = candidates.Where(w => w.STCAcousticRating >= context.MinAcousticRating).ToList();
                recommendation.Notes.Add($"Acoustic: STC {context.MinAcousticRating}+ required");
            }

            // Filter by moisture requirement
            if (context.RequiresMoistureResistance)
            {
                candidates = candidates.Where(w => w.RequiresMoistureBarrier || w.Location == WallLocation.Exterior).ToList();
                recommendation.Notes.Add("Moisture-resistant assembly required for wet area");
            }

            // Score remaining candidates
            var scored = candidates.Select(c => new
            {
                WallType = c,
                Score = ScoreWallType(c, context)
            })
            .OrderByDescending(s => s.Score)
            .ToList();

            if (scored.Count == 0)
            {
                recommendation.RecommendedType = "Basic Wall";
                recommendation.Confidence = 0.3;
                recommendation.Notes.Add("Warning: No wall type fully matches requirements. Consider custom assembly.");
                return recommendation;
            }

            var best = scored.First();
            recommendation.RecommendedType = best.WallType.Name;
            recommendation.Confidence = Math.Min(1.0, best.Score);
            recommendation.Thickness = best.WallType.Thickness;
            recommendation.IsStructural = best.WallType.IsStructural;
            recommendation.FireRatingMinutes = best.WallType.FireRatingMinutes;
            recommendation.AcousticRating = best.WallType.STCAcousticRating;
            recommendation.EstimatedCostPerM2 = best.WallType.CostPerM2;
            recommendation.Material = best.WallType.PrimaryMaterial;

            // Add alternatives
            recommendation.Alternatives = scored.Skip(1).Take(2).Select(s => new WallAlternative
            {
                TypeName = s.WallType.Name,
                Reason = CompareWallTypes(best.WallType, s.WallType),
                CostDifference = s.WallType.CostPerM2 - best.WallType.CostPerM2
            }).ToList();

            // Building code notes
            if (context.Location == WallLocation.Exterior)
            {
                var rValue = best.WallType.ThermalResistance;
                if (context.ClimateZone >= 4 && rValue < 2.5)
                    recommendation.Notes.Add($"ASHRAE 90.1: Climate zone {context.ClimateZone} may require R-2.5+ for exterior walls. Current: R-{rValue:F1}");
                else if (context.ClimateZone >= 6 && rValue < 3.5)
                    recommendation.Notes.Add($"ASHRAE 90.1: Climate zone {context.ClimateZone} recommends R-3.5+ for exterior walls. Current: R-{rValue:F1}");
            }

            if (context.AdjacentToExitCorridor)
            {
                if (best.WallType.FireRatingMinutes < 60)
                    recommendation.Notes.Add("IBC 1020.1: Corridor walls serving as exit access must be 1-hour fire-rated");
            }

            if (context.IsPartyWall)
            {
                if (best.WallType.STCAcousticRating < 50)
                    recommendation.Notes.Add("IBC 1207.2: Party walls between dwelling units require STC 50+ (field-tested STC 45+)");
                if (best.WallType.FireRatingMinutes < 60)
                    recommendation.Notes.Add("IBC 706: Party walls (fire walls) require minimum 1-hour fire rating");
            }

            Logger.Info($"Recommended: {recommendation.RecommendedType} (confidence: {recommendation.Confidence:F2})");
            return recommendation;
        }

        /// <summary>
        /// Validates wall placement for potential collisions with existing walls.
        /// </summary>
        public WallCollisionResult CheckCollisions(Point3D startPoint, Point3D endPoint, double thickness, List<WallPlacement> existingWalls)
        {
            var result = new WallCollisionResult();

            if (existingWalls == null || existingWalls.Count == 0)
            {
                result.HasCollisions = false;
                return result;
            }

            var newWallAngle = Math.Atan2(endPoint.Y - startPoint.Y, endPoint.X - startPoint.X);

            foreach (var existing in existingWalls)
            {
                var distance = DistanceToLine(existing.StartPoint, existing.EndPoint, startPoint, endPoint);

                // Check if walls overlap spatially
                var minClearance = (thickness + existing.Thickness) / 2.0;
                if (distance < minClearance)
                {
                    var existingAngle = Math.Atan2(
                        existing.EndPoint.Y - existing.StartPoint.Y,
                        existing.EndPoint.X - existing.StartPoint.X);
                    var angleDiff = Math.Abs(newWallAngle - existingAngle) % Math.PI;

                    if (angleDiff < 0.1 || Math.Abs(angleDiff - Math.PI) < 0.1) // parallel
                    {
                        result.HasCollisions = true;
                        result.Collisions.Add(new WallCollision
                        {
                            ExistingWallId = existing.ElementId,
                            CollisionType = WallCollisionType.Parallel,
                            OverlapDistance = minClearance - distance,
                            Suggestion = $"Parallel wall too close (gap: {distance:F0}mm, need: {minClearance:F0}mm). Move {minClearance - distance:F0}mm apart."
                        });
                    }
                    else if (Math.Abs(angleDiff - Math.PI / 2) < 0.2) // perpendicular
                    {
                        // Perpendicular intersection is normal (T-junction or L-junction)
                        result.Junctions.Add(new WallJunction
                        {
                            ExistingWallId = existing.ElementId,
                            JunctionType = WallJunctionType.TJunction,
                            Suggestion = "Ensure wall join at intersection for proper cleanup"
                        });
                    }
                }
            }

            return result;
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            var result = new ValidationResult { IsValid = true };

            // Check for length
            if (parameters.Parameters.TryGetValue("Length", out var lengthObj))
            {
                var length = Convert.ToDouble(lengthObj);

                if (length < MinWallLength)
                {
                    return ValidationResult.Invalid($"Wall length ({length}mm) is below minimum ({MinWallLength}mm)");
                }

                if (length > MaxWallLength)
                {
                    return ValidationResult.Invalid($"Wall length ({length}mm) exceeds maximum ({MaxWallLength}mm)");
                }

                // Structural check: long unsupported walls need lateral bracing
                if (length > 6000)
                {
                    result.Warnings.Add($"Wall length {length / 1000:F1}m exceeds 6m. Consider lateral bracing or expansion joints per ACI 530.");
                }
            }

            // Check for valid wall type
            if (parameters.Parameters.TryGetValue("WallType", out var wallType))
            {
                var typeName = wallType.ToString();
                if (!WallTypeCatalogue.ContainsKey(typeName))
                {
                    Logger.Warn($"Wall type '{typeName}' not found in catalogue, using default. Available types: {string.Join(", ", WallTypeCatalogue.Keys.Take(5))}...");
                    result.Warnings.Add($"Wall type '{typeName}' not found. Using '{DefaultWallType}'. Use GetAvailableWallTypes() for options.");
                }
            }

            // Check height-to-thickness ratio for slenderness
            if (parameters.Parameters.TryGetValue("Height", out var heightObj) &&
                parameters.Parameters.TryGetValue("Thickness", out var thicknessObj))
            {
                var h = Convert.ToDouble(heightObj);
                var t = Convert.ToDouble(thicknessObj);
                if (t > 0)
                {
                    var slenderness = h / t;
                    if (slenderness > 25)
                    {
                        result.Warnings.Add($"ACI 530: Wall slenderness ratio {slenderness:F0} exceeds 25. Risk of buckling. Increase thickness or add stiffeners.");
                    }
                    else if (slenderness > 20)
                    {
                        result.Warnings.Add($"Wall slenderness ratio {slenderness:F0} is high. Consider structural review per ACI 530.");
                    }
                }
            }

            return result;
        }

        private WallParameters ExtractWallParameters(ElementCreationParams parameters)
        {
            var typeName = GetString(parameters.Parameters, "WallType", DefaultWallType);
            var thickness = DefaultWallThickness;

            // Get thickness from catalogue if available
            if (WallTypeCatalogue.TryGetValue(typeName, out var wallDef))
            {
                thickness = wallDef.Thickness;
            }

            return new WallParameters
            {
                Length = GetDouble(parameters.Parameters, "Length", 3000),
                Height = GetDouble(parameters.Parameters, "Height", DefaultWallHeight),
                Thickness = GetDouble(parameters.Parameters, "Thickness", thickness),
                WallTypeName = typeName,
                LevelName = GetString(parameters.Parameters, "Level", "Level 1"),
                IsStructural = GetBool(parameters.Parameters, "IsStructural", wallDef?.IsStructural ?? false),
                StartPoint = GetPoint(parameters.Parameters, "StartPoint"),
                EndPoint = GetPoint(parameters.Parameters, "EndPoint")
            };
        }

        private double ScoreWallType(BasicWallType wallType, WallContext context)
        {
            double score = 0.5; // base

            // Fire rating match (strict requirement met = bonus, overspec = slight penalty for cost)
            if (context.RequiredFireRating > 0)
            {
                if (wallType.FireRatingMinutes >= context.RequiredFireRating)
                {
                    score += 0.25;
                    // Penalize over-specification
                    if (wallType.FireRatingMinutes > context.RequiredFireRating * 2)
                        score -= 0.05;
                }
            }

            // Acoustic match
            if (context.MinAcousticRating > 0 && wallType.STCAcousticRating >= context.MinAcousticRating)
            {
                score += 0.15;
            }

            // Structural match
            if (context.RequiresStructural == wallType.IsStructural)
                score += 0.1;

            // Cost preference (lower is better within matching types)
            if (context.PreferLowCost)
                score += 0.1 * (1 - wallType.CostPerM2 / 400.0);

            // Thermal performance (for exterior walls)
            if (context.Location == WallLocation.Exterior && wallType.ThermalResistance > 2.0)
                score += 0.1;

            return Math.Max(0, Math.Min(1, score));
        }

        private string CompareWallTypes(BasicWallType recommended, BasicWallType alternative)
        {
            var differences = new List<string>();

            if (alternative.CostPerM2 < recommended.CostPerM2)
                differences.Add($"${recommended.CostPerM2 - alternative.CostPerM2:F0}/m² cheaper");
            else if (alternative.CostPerM2 > recommended.CostPerM2)
                differences.Add($"${alternative.CostPerM2 - recommended.CostPerM2:F0}/m² more expensive");

            if (alternative.STCAcousticRating > recommended.STCAcousticRating)
                differences.Add($"STC {alternative.STCAcousticRating} (better acoustic)");

            if (alternative.ThermalResistance > recommended.ThermalResistance)
                differences.Add($"R-{alternative.ThermalResistance:F1} (better thermal)");

            if (alternative.Thickness < recommended.Thickness)
                differences.Add($"{alternative.Thickness}mm (thinner)");

            return differences.Count > 0 ? string.Join("; ", differences) : "Similar performance";
        }

        private double DistanceToLine(Point3D lineStart, Point3D lineEnd, Point3D segStart, Point3D segEnd)
        {
            // Simplified minimum distance between two line segments
            var midNew = new Point3D(
                (segStart.X + segEnd.X) / 2,
                (segStart.Y + segEnd.Y) / 2,
                (segStart.Z + segEnd.Z) / 2);

            // Point-to-line distance from midpoint of new wall to existing wall line
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) return CalculateDistance(lineStart, midNew);

            var t = Math.Max(0, Math.Min(1,
                ((midNew.X - lineStart.X) * dx + (midNew.Y - lineStart.Y) * dy) / (len * len)));

            var closestX = lineStart.X + t * dx;
            var closestY = lineStart.Y + t * dy;

            return Math.Sqrt(
                (midNew.X - closestX) * (midNew.X - closestX) +
                (midNew.Y - closestY) * (midNew.Y - closestY));
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString()?.Replace("mm", "").Replace("m", "000"), out d)) return d;
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

        private Point3D GetPoint(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value is Point3D point)
                return point;
            return null;
        }

        private double CalculateDistance(Point3D p1, Point3D p2)
        {
            if (p1 == null || p2 == null) return 0;
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private int GenerateElementId()
        {
            return new Random().Next(100000, 999999);
        }

        #endregion
    }

    #region Supporting Classes

    public class WallCreatorOptions
    {
        public double? Height { get; set; }
        public double? Thickness { get; set; }
        public string WallTypeName { get; set; }
        public string LevelName { get; set; }
        public bool IsStructural { get; set; }
        public bool FlipOrientation { get; set; }
        public double BaseOffset { get; set; }
        public double TopOffset { get; set; }
    }

    public class WallParameters
    {
        public double Length { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public string WallTypeName { get; set; }
        public string LevelName { get; set; }
        public bool IsStructural { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
    }

    public class WallTypeInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Thickness { get; set; }
        public bool IsStructural { get; set; }
        public string Material { get; set; }
        public int FireRatingMinutes { get; set; }
        public int STCAcousticRating { get; set; }
        public double ThermalResistance { get; set; }
        public double CostPerM2 { get; set; }
    }

    public class BasicWallType
    {
        public string Name { get; set; }
        public double Thickness { get; set; }
        public bool IsStructural { get; set; }
        public WallLocation Location { get; set; }
        public int FireRatingMinutes { get; set; }
        public int STCAcousticRating { get; set; }
        public double ThermalResistance { get; set; } // R-value m²·K/W
        public string PrimaryMaterial { get; set; }
        public double CostPerM2 { get; set; }
        public double WeightPerM2 { get; set; } // kg/m²
        public double MaxLoadBearing { get; set; } // kN/m
        public bool RequiresMoistureBarrier { get; set; }
        public string Description { get; set; }
    }

    public enum WallLocation
    {
        Any,
        Interior,
        Exterior,
        BelowGrade
    }

    public class WallContext
    {
        public WallLocation Location { get; set; } = WallLocation.Any;
        public bool RequiresStructural { get; set; }
        public int RequiredFireRating { get; set; } // minutes
        public int MinAcousticRating { get; set; } // STC
        public bool RequiresMoistureResistance { get; set; }
        public bool AdjacentToExitCorridor { get; set; }
        public bool IsPartyWall { get; set; }
        public int ClimateZone { get; set; } // ASHRAE climate zone 1-8
        public bool PreferLowCost { get; set; }
        public string AdjacentRoomType { get; set; }
    }

    public class WallRecommendation
    {
        public string RecommendedType { get; set; }
        public double Confidence { get; set; }
        public double Thickness { get; set; }
        public bool IsStructural { get; set; }
        public int FireRatingMinutes { get; set; }
        public int AcousticRating { get; set; }
        public double EstimatedCostPerM2 { get; set; }
        public string Material { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
        public List<WallAlternative> Alternatives { get; set; } = new List<WallAlternative>();
    }

    public class WallAlternative
    {
        public string TypeName { get; set; }
        public string Reason { get; set; }
        public double CostDifference { get; set; }
    }

    public class WallPlacement
    {
        public int ElementId { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double Thickness { get; set; }
    }

    public class WallCollisionResult
    {
        public bool HasCollisions { get; set; }
        public List<WallCollision> Collisions { get; set; } = new List<WallCollision>();
        public List<WallJunction> Junctions { get; set; } = new List<WallJunction>();
    }

    public class WallCollision
    {
        public int ExistingWallId { get; set; }
        public WallCollisionType CollisionType { get; set; }
        public double OverlapDistance { get; set; }
        public string Suggestion { get; set; }
    }

    public class WallJunction
    {
        public int ExistingWallId { get; set; }
        public WallJunctionType JunctionType { get; set; }
        public string Suggestion { get; set; }
    }

    public enum WallCollisionType
    {
        Parallel,
        Overlapping,
        Intersecting
    }

    public enum WallJunctionType
    {
        TJunction,
        LJunction,
        CrossJunction
    }

    public enum ExtendDirection
    {
        Start,
        End,
        Both
    }

    #endregion
}
