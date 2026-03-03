// StingBIM.AI.Creation.Spaces.RoomGenerator
// Generates rooms from natural language descriptions
// Master Proposal Reference: Part 4.2 Phase 1 Month 2 Week 3-4 - Room Generation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Elements;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Spaces
{
    /// <summary>
    /// Generates complete rooms with walls, doors, windows, and proper tagging.
    /// Handles room creation from natural language like "Create a 4x5 meter bedroom".
    /// </summary>
    public class RoomGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly WallCreator _wallCreator;
        private readonly FloorCreator _floorCreator;

        // Room type definitions with typical dimensions and requirements
        private static readonly Dictionary<string, RoomTypeDefinition> RoomTypes = new Dictionary<string, RoomTypeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["bedroom"] = new RoomTypeDefinition
            {
                Name = "Bedroom",
                MinArea = 9000000, // 9 m²
                DefaultWidth = 3500,
                DefaultDepth = 4000,
                DefaultHeight = 2700,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 800,
                SuggestedWindowWidth = 1200
            },
            ["master bedroom"] = new RoomTypeDefinition
            {
                Name = "Master Bedroom",
                MinArea = 14000000, // 14 m²
                DefaultWidth = 4000,
                DefaultDepth = 5000,
                DefaultHeight = 2700,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 900,
                SuggestedWindowWidth = 1500
            },
            ["living room"] = new RoomTypeDefinition
            {
                Name = "Living Room",
                MinArea = 18000000, // 18 m²
                DefaultWidth = 5000,
                DefaultDepth = 6000,
                DefaultHeight = 2800,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 900,
                SuggestedWindowWidth = 2000
            },
            ["kitchen"] = new RoomTypeDefinition
            {
                Name = "Kitchen",
                MinArea = 8000000, // 8 m²
                DefaultWidth = 3000,
                DefaultDepth = 4000,
                DefaultHeight = 2700,
                RequiresWindow = true,
                RequiresDoor = true,
                RequiresPlumbing = true,
                SuggestedDoorWidth = 800,
                SuggestedWindowWidth = 1200
            },
            ["bathroom"] = new RoomTypeDefinition
            {
                Name = "Bathroom",
                MinArea = 4000000, // 4 m²
                DefaultWidth = 2000,
                DefaultDepth = 2500,
                DefaultHeight = 2500,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                SuggestedDoorWidth = 700
            },
            ["office"] = new RoomTypeDefinition
            {
                Name = "Home Office",
                MinArea = 9000000, // 9 m²
                DefaultWidth = 3000,
                DefaultDepth = 3500,
                DefaultHeight = 2700,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 800,
                SuggestedWindowWidth = 1400
            },
            ["dining room"] = new RoomTypeDefinition
            {
                Name = "Dining Room",
                MinArea = 12000000, // 12 m²
                DefaultWidth = 4000,
                DefaultDepth = 4500,
                DefaultHeight = 2700,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 900,
                SuggestedWindowWidth = 1500
            },
            ["hallway"] = new RoomTypeDefinition
            {
                Name = "Hallway",
                MinArea = 3000000, // 3 m²
                DefaultWidth = 1200,
                DefaultDepth = 3000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = false
            },
            ["closet"] = new RoomTypeDefinition
            {
                Name = "Closet",
                MinArea = 2000000, // 2 m²
                DefaultWidth = 1500,
                DefaultDepth = 2000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                SuggestedDoorWidth = 700
            },
            // Commercial / specialized room types
            ["reception"] = new RoomTypeDefinition
            {
                Name = "Reception",
                MinArea = 15000000, // 15 m²
                DefaultWidth = 5000,
                DefaultDepth = 6000,
                DefaultHeight = 3000,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 1200,
                SuggestedWindowWidth = 2000,
                OccupancyDensity = 1.4, // m² per person (IBC assembly)
                FireRatingMinutes = 60,
                RequiresAccessibility = true
            },
            ["conference room"] = new RoomTypeDefinition
            {
                Name = "Conference Room",
                MinArea = 14000000, // 14 m²
                DefaultWidth = 4000,
                DefaultDepth = 5000,
                DefaultHeight = 2800,
                RequiresWindow = true,
                RequiresDoor = true,
                SuggestedDoorWidth = 900,
                SuggestedWindowWidth = 1500,
                OccupancyDensity = 1.4,
                RequiresAccessibility = true
            },
            ["server room"] = new RoomTypeDefinition
            {
                Name = "Server Room",
                MinArea = 12000000, // 12 m²
                DefaultWidth = 3000,
                DefaultDepth = 4000,
                DefaultHeight = 3000,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true,
                SuggestedDoorWidth = 900,
                FireRatingMinutes = 120,
                RequiresCooling = true
            },
            ["laundry"] = new RoomTypeDefinition
            {
                Name = "Laundry",
                MinArea = 5000000, // 5 m²
                DefaultWidth = 2500,
                DefaultDepth = 3000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                SuggestedDoorWidth = 800
            },
            ["pantry"] = new RoomTypeDefinition
            {
                Name = "Pantry",
                MinArea = 3000000, // 3 m²
                DefaultWidth = 2000,
                DefaultDepth = 2000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                SuggestedDoorWidth = 700
            },
            ["garage"] = new RoomTypeDefinition
            {
                Name = "Garage",
                MinArea = 18000000, // 18 m² (single car)
                DefaultWidth = 3500,
                DefaultDepth = 6000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true,
                SuggestedDoorWidth = 2400, // garage door
                FireRatingMinutes = 60
            },
            ["stairwell"] = new RoomTypeDefinition
            {
                Name = "Stairwell",
                MinArea = 6000000, // 6 m²
                DefaultWidth = 2500,
                DefaultDepth = 3000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                SuggestedDoorWidth = 900,
                FireRatingMinutes = 120,
                RequiresAccessibility = true,
                IsEgressRoute = true
            },
            ["elevator lobby"] = new RoomTypeDefinition
            {
                Name = "Elevator Lobby",
                MinArea = 8000000, // 8 m²
                DefaultWidth = 3000,
                DefaultDepth = 3000,
                DefaultHeight = 2800,
                RequiresWindow = false,
                RequiresDoor = false,
                RequiresAccessibility = true,
                FireRatingMinutes = 60,
                IsEgressRoute = true
            },
            ["toilet"] = new RoomTypeDefinition
            {
                Name = "Toilet (WC)",
                MinArea = 2500000, // 2.5 m²
                DefaultWidth = 1500,
                DefaultDepth = 2000,
                DefaultHeight = 2500,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                SuggestedDoorWidth = 700
            },
            ["accessible toilet"] = new RoomTypeDefinition
            {
                Name = "Accessible Toilet",
                MinArea = 4500000, // 4.5 m² (ADA minimum)
                DefaultWidth = 2200,
                DefaultDepth = 2200,
                DefaultHeight = 2500,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                RequiresAccessibility = true,
                SuggestedDoorWidth = 900, // ADA min 815mm clear
                SuggestedDoorSwing = "Outward" // ADA requirement
            },
            ["utility room"] = new RoomTypeDefinition
            {
                Name = "Utility Room",
                MinArea = 6000000, // 6 m²
                DefaultWidth = 2500,
                DefaultDepth = 3000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                SuggestedDoorWidth = 800
            },
            ["store"] = new RoomTypeDefinition
            {
                Name = "Store Room",
                MinArea = 4000000, // 4 m²
                DefaultWidth = 2000,
                DefaultDepth = 2500,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                SuggestedDoorWidth = 800
            },
            ["balcony"] = new RoomTypeDefinition
            {
                Name = "Balcony",
                MinArea = 4000000, // 4 m²
                DefaultWidth = 3000,
                DefaultDepth = 1500,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true,
                SuggestedDoorWidth = 1800 // sliding door
            },
            ["corridor"] = new RoomTypeDefinition
            {
                Name = "Corridor",
                MinArea = 3000000, // 3 m²
                DefaultWidth = 1500, // IBC min 1220mm, 1500mm commercial
                DefaultDepth = 5000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = false,
                IsEgressRoute = true,
                RequiresAccessibility = true
            }
        };

        public RoomGenerator(WallCreator wallCreator, FloorCreator floorCreator)
        {
            _wallCreator = wallCreator ?? throw new ArgumentNullException(nameof(wallCreator));
            _floorCreator = floorCreator ?? throw new ArgumentNullException(nameof(floorCreator));
        }

        /// <summary>
        /// Generates a room from natural language parameters.
        /// </summary>
        public async Task<RoomGenerationResult> GenerateRoomAsync(
            RoomGenerationParams parameters,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating room: {parameters.RoomType} ({parameters.Width}x{parameters.Depth}mm)");

            var result = new RoomGenerationResult
            {
                RoomType = parameters.RoomType,
                StartTime = DateTime.Now
            };

            try
            {
                // Get room type definition
                var roomDef = GetRoomDefinition(parameters.RoomType);

                // Apply defaults if dimensions not specified
                var width = parameters.Width > 0 ? parameters.Width : roomDef.DefaultWidth;
                var depth = parameters.Depth > 0 ? parameters.Depth : roomDef.DefaultDepth;
                var height = parameters.Height > 0 ? parameters.Height : roomDef.DefaultHeight;

                // Validate dimensions
                var validation = ValidateRoomDimensions(roomDef, width, depth, height);
                if (!validation.IsValid)
                {
                    result.Success = false;
                    result.Error = validation.Error;
                    result.Warnings.AddRange(validation.Warnings);
                    return result;
                }

                result.Warnings.AddRange(validation.Warnings);

                // Determine origin point
                var origin = parameters.Origin ?? new Point3D(0, 0, 0);

                // Step 1: Create walls
                var wallOptions = new WallCreationOptions
                {
                    Height = height,
                    WallTypeName = parameters.WallType ?? "Generic - 200mm",
                    LevelName = parameters.LevelName ?? "Level 1",
                    IsStructural = false
                };

                var wallResult = await _wallCreator.CreateRectangleAsync(origin, width, depth, wallOptions, cancellationToken);

                if (!wallResult.AllSucceeded)
                {
                    result.Success = false;
                    result.Error = "Failed to create all walls";
                    return result;
                }

                result.CreatedWallIds.AddRange(wallResult.Results.Where(r => r.Success).Select(r => r.CreatedElementId));

                // Step 2: Create floor (optional)
                if (parameters.IncludeFloor)
                {
                    var floorOptions = new FloorCreationOptions
                    {
                        Thickness = 150,
                        FloorTypeName = parameters.FloorType ?? "Generic - 150mm",
                        LevelName = parameters.LevelName ?? "Level 1"
                    };

                    var floorResult = await _floorCreator.CreateRectangularAsync(origin, width, depth, floorOptions, cancellationToken);

                    if (floorResult.Success)
                    {
                        result.CreatedFloorId = floorResult.CreatedElementId;
                    }
                }

                // Step 3: Create room element and tag
                result.RoomElementId = await CreateRoomElementAsync(origin, width, depth, parameters, cancellationToken);

                // Step 4: Add suggested openings info
                result.SuggestedOpenings = GenerateOpeningSuggestions(roomDef, width, depth);

                result.Success = true;
                result.RoomName = parameters.RoomName ?? roomDef.Name;
                result.Dimensions = new RoomDimensions
                {
                    Width = width,
                    Depth = depth,
                    Height = height,
                    Area = width * depth,
                    Volume = width * depth * height
                };

                result.Message = $"Created {result.RoomName} ({width / 1000:F1}m x {depth / 1000:F1}m)";

                Logger.Info($"Room generated successfully: {result.RoomElementId}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Room generation failed");
                result.Success = false;
                result.Error = ex.Message;
            }

            result.EndTime = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Generates a room by specifying room type and letting AI determine dimensions.
        /// </summary>
        public async Task<RoomGenerationResult> GenerateRoomByTypeAsync(
            string roomType,
            Point3D origin = null,
            string levelName = null,
            CancellationToken cancellationToken = default)
        {
            var roomDef = GetRoomDefinition(roomType);

            var parameters = new RoomGenerationParams
            {
                RoomType = roomType,
                RoomName = roomDef.Name,
                Width = roomDef.DefaultWidth,
                Depth = roomDef.DefaultDepth,
                Height = roomDef.DefaultHeight,
                Origin = origin,
                LevelName = levelName,
                IncludeFloor = true
            };

            return await GenerateRoomAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Gets available room types.
        /// </summary>
        public IEnumerable<RoomTypeInfo> GetRoomTypes()
        {
            return RoomTypes.Values.Select(r => new RoomTypeInfo
            {
                Name = r.Name,
                MinArea = r.MinArea / 1000000, // Convert to m²
                DefaultWidth = r.DefaultWidth / 1000, // Convert to m
                DefaultDepth = r.DefaultDepth / 1000,
                DefaultHeight = r.DefaultHeight / 1000,
                RequiresWindow = r.RequiresWindow,
                RequiresPlumbing = r.RequiresPlumbing
            });
        }

        /// <summary>
        /// Validates room parameters against building codes.
        /// </summary>
        public ValidationResult ValidateRoom(string roomType, double width, double depth, double height)
        {
            var roomDef = GetRoomDefinition(roomType);
            return ValidateRoomDimensions(roomDef, width, depth, height);
        }

        #region Private Methods

        private RoomTypeDefinition GetRoomDefinition(string roomType)
        {
            if (RoomTypes.TryGetValue(roomType, out var def))
                return def;

            // Default generic room
            return new RoomTypeDefinition
            {
                Name = roomType ?? "Room",
                MinArea = 4000000,
                DefaultWidth = 3000,
                DefaultDepth = 3000,
                DefaultHeight = 2700,
                RequiresWindow = false,
                RequiresDoor = true
            };
        }

        private ValidationResult ValidateRoomDimensions(RoomTypeDefinition roomDef, double width, double depth, double height)
        {
            var result = new ValidationResult { IsValid = true };

            var area = width * depth;

            // Check minimum area
            if (area < roomDef.MinArea)
            {
                result.Warnings.Add($"Room area ({area / 1000000:F1}m²) is below recommended minimum ({roomDef.MinArea / 1000000:F1}m²) for {roomDef.Name}");
            }

            // Check minimum height (IBC building code - 7'6" = 2286mm, using 2400mm)
            if (height < 2400)
            {
                result.IsValid = false;
                result.Error = $"Room height ({height}mm) is below IBC minimum (2400mm)";
                return result;
            }

            // Check proportions
            var aspectRatio = Math.Max(width, depth) / Math.Min(width, depth);
            if (aspectRatio > 3)
            {
                result.Warnings.Add($"Room proportions are very elongated (aspect ratio {aspectRatio:F1}:1). Consider subdividing.");
            }
            else if (aspectRatio > 2.5)
            {
                result.Warnings.Add($"Aspect ratio {aspectRatio:F1}:1 is high. Golden ratio (1.62:1) recommended for optimal spatial quality.");
            }

            // Check window requirement
            if (roomDef.RequiresWindow && Math.Min(width, depth) < 2000)
            {
                result.Warnings.Add("Room may be too narrow for proper window placement");
            }

            // IBC minimum dimension check (no habitable room dimension < 7' = 2134mm)
            if (Math.Min(width, depth) < 2134 && roomDef.RequiresWindow)
            {
                result.Warnings.Add($"IBC: Habitable room minimum dimension is 2134mm (7ft). Current minimum: {Math.Min(width, depth):F0}mm");
            }

            // Accessibility checks
            if (roomDef.RequiresAccessibility)
            {
                // ADA wheelchair turning radius: 1525mm (60") clear floor space
                if (Math.Min(width, depth) < 1525)
                {
                    result.Warnings.Add($"ADA: Minimum 1525mm (60in) clear dimension required for wheelchair turning. Current: {Math.Min(width, depth):F0}mm");
                }
            }

            // Corridor width check (IBC: 1220mm min residential, 1524mm commercial)
            if (roomDef.IsEgressRoute && width < 1220)
            {
                result.Warnings.Add($"IBC: Egress corridor minimum width is 1220mm. Current: {width:F0}mm");
            }

            // Fire rating notice
            if (roomDef.FireRatingMinutes > 0)
            {
                result.Warnings.Add($"Fire code: {roomDef.Name} requires {roomDef.FireRatingMinutes}-minute fire-rated enclosure walls");
            }

            // Occupancy calculation
            if (roomDef.OccupancyDensity > 0)
            {
                var occupancy = (int)Math.Ceiling((area / 1000000.0) / roomDef.OccupancyDensity);
                result.Warnings.Add($"IBC occupancy load: {occupancy} persons at {roomDef.OccupancyDensity} m²/person");
            }

            return result;
        }

        private async Task<int> CreateRoomElementAsync(
            Point3D origin,
            double width,
            double depth,
            RoomGenerationParams parameters,
            CancellationToken cancellationToken)
        {
            // In real implementation, would create Revit Room element
            // Room.Create(doc, level, UV point, phase)
            // And set room name, number, parameters

            await Task.Delay(10, cancellationToken); // Simulate async work

            return new Random().Next(100000, 999999);
        }

        private List<OpeningSuggestion> GenerateOpeningSuggestions(RoomTypeDefinition roomDef, double width, double depth)
        {
            var suggestions = new List<OpeningSuggestion>();
            var area = width * depth;

            if (roomDef.RequiresDoor)
            {
                var doorWidth = roomDef.SuggestedDoorWidth;
                var doorSwing = roomDef.SuggestedDoorSwing ?? "Inward";

                // ADA: accessible doors need minimum 815mm clear width
                if (roomDef.RequiresAccessibility && doorWidth < 900)
                    doorWidth = 900;

                suggestions.Add(new OpeningSuggestion
                {
                    Type = "Door",
                    Width = doorWidth,
                    WallPosition = "Wall adjacent to circulation path",
                    Reason = roomDef.RequiresAccessibility
                        ? $"ADA accessible door (min 815mm clear). Door swing: {doorSwing}"
                        : "Room requires at least one door for access"
                });

                // Fire-rated rooms need self-closing fire doors
                if (roomDef.FireRatingMinutes > 0)
                {
                    suggestions.Add(new OpeningSuggestion
                    {
                        Type = "Fire Door",
                        Width = doorWidth,
                        WallPosition = "Egress path wall",
                        Reason = $"{roomDef.FireRatingMinutes}-minute fire-rated self-closing door required"
                    });
                }

                // Egress rooms need second exit if large enough
                if (roomDef.IsEgressRoute && area / 1000000 > 30)
                {
                    suggestions.Add(new OpeningSuggestion
                    {
                        Type = "Emergency Exit Door",
                        Width = 900,
                        WallPosition = "Opposite wall from primary door (IBC egress separation)",
                        Reason = "IBC: Rooms >30m² with egress function need secondary exit"
                    });
                }
            }

            if (roomDef.RequiresWindow)
            {
                // Suggest window on longer wall, prefer south for daylighting
                var windowWall = width > depth
                    ? "South wall (longer wall, best daylight)"
                    : "South or East wall (best daylight orientation)";

                // IBC: Window area min 8% of floor area for habitable rooms
                var minGlazingArea = area * 0.08;
                var windowHeight = 1500.0; // typical window height
                var minWindowWidth = minGlazingArea / windowHeight;
                var suggestedWidth = Math.Max(roomDef.SuggestedWindowWidth, minWindowWidth);

                suggestions.Add(new OpeningSuggestion
                {
                    Type = "Window",
                    Width = suggestedWidth,
                    WallPosition = windowWall,
                    Reason = $"IBC: Min 8% floor area glazing ({minGlazingArea / 1000000:F2}m²). " +
                             $"Window sill height: 900mm for safety"
                });

                // Large rooms benefit from multiple windows
                if (area / 1000000 > 15)
                {
                    suggestions.Add(new OpeningSuggestion
                    {
                        Type = "Window",
                        Width = roomDef.SuggestedWindowWidth,
                        WallPosition = "Secondary wall for cross-ventilation",
                        Reason = "Cross-ventilation recommended for rooms >15m². Improves air quality and passive cooling."
                    });
                }
            }

            if (roomDef.RequiresVentilation)
            {
                // ASHRAE 62.1: minimum ventilation rates
                var volumeM3 = (area / 1000000.0) * (roomDef.DefaultHeight / 1000.0);
                var airChangesPerHour = roomDef.RequiresPlumbing ? 8 : 4; // bathrooms need more
                var requiredCFM = volumeM3 * airChangesPerHour / 60 * 35.3147; // convert to CFM

                suggestions.Add(new OpeningSuggestion
                {
                    Type = roomDef.RequiresWindow ? "Vent" : "Mechanical Ventilation",
                    Width = 300,
                    WallPosition = "Ceiling or exterior wall",
                    Reason = $"ASHRAE 62.1: {airChangesPerHour} ACH required. " +
                             $"Min {requiredCFM:F0} CFM for {volumeM3:F1}m³ volume"
                });
            }

            if (roomDef.RequiresCooling)
            {
                suggestions.Add(new OpeningSuggestion
                {
                    Type = "HVAC Supply/Return",
                    Width = 600,
                    WallPosition = "Ceiling-mounted or raised floor",
                    Reason = "Active cooling required. Size per equipment heat load."
                });
            }

            // Electrical suggestions
            if (roomDef.RequiresWindow || roomDef.RequiresDoor)
            {
                // NEC: outlets every 12ft (3.66m) of wall, and within 6ft (1.83m) of any door
                var perimeter = 2 * (width + depth);
                var outletCount = Math.Max(2, (int)Math.Ceiling(perimeter / 3660));

                suggestions.Add(new OpeningSuggestion
                {
                    Type = "Electrical Outlets",
                    Width = 0,
                    WallPosition = $"Distribute {outletCount} outlets around room perimeter",
                    Reason = $"NEC: Min 1 outlet per 3.66m wall. {outletCount} outlets for {perimeter / 1000:F1}m perimeter"
                });

                // Lighting
                var lightingWatts = (area / 1000000) * 10; // 10 W/m² typical
                suggestions.Add(new OpeningSuggestion
                {
                    Type = "Lighting",
                    Width = 0,
                    WallPosition = "Ceiling-mounted",
                    Reason = $"Recommended lighting: {lightingWatts:F0}W ({area / 1000000:F1}m² at 10 W/m²). " +
                             $"Target: 300-500 lux for {roomDef.Name}"
                });
            }

            return suggestions;
        }

        #endregion
    }

    #region Supporting Classes

    public class RoomGenerationParams
    {
        public string RoomType { get; set; }
        public string RoomName { get; set; }
        public double Width { get; set; } // mm
        public double Depth { get; set; } // mm
        public double Height { get; set; } // mm
        public Point3D Origin { get; set; }
        public string LevelName { get; set; }
        public string WallType { get; set; }
        public string FloorType { get; set; }
        public bool IncludeFloor { get; set; } = true;
        public Dictionary<string, object> CustomParameters { get; set; } = new Dictionary<string, object>();
    }

    public class RoomGenerationResult
    {
        public bool Success { get; set; }
        public string RoomType { get; set; }
        public string RoomName { get; set; }
        public int RoomElementId { get; set; }
        public List<int> CreatedWallIds { get; set; } = new List<int>();
        public int? CreatedFloorId { get; set; }
        public RoomDimensions Dimensions { get; set; }
        public List<OpeningSuggestion> SuggestedOpenings { get; set; } = new List<OpeningSuggestion>();
        public string Message { get; set; }
        public string Error { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
    }

    public class RoomDimensions
    {
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Height { get; set; }
        public double Area { get; set; }
        public double Volume { get; set; }
    }

    public class RoomTypeDefinition
    {
        public string Name { get; set; }
        public double MinArea { get; set; }
        public double DefaultWidth { get; set; }
        public double DefaultDepth { get; set; }
        public double DefaultHeight { get; set; }
        public bool RequiresWindow { get; set; }
        public bool RequiresDoor { get; set; }
        public bool RequiresPlumbing { get; set; }
        public bool RequiresVentilation { get; set; }
        public double SuggestedDoorWidth { get; set; }
        public double SuggestedWindowWidth { get; set; }

        // Intelligence properties
        public double OccupancyDensity { get; set; } // m² per person (for IBC occupancy calc)
        public int FireRatingMinutes { get; set; } // Required fire rating for enclosing walls
        public bool RequiresAccessibility { get; set; } // ADA/accessibility compliance required
        public bool RequiresCooling { get; set; } // Active cooling required
        public bool IsEgressRoute { get; set; } // Part of means of egress
        public string SuggestedDoorSwing { get; set; } // Inward/Outward
    }

    public class RoomTypeInfo
    {
        public string Name { get; set; }
        public double MinArea { get; set; }
        public double DefaultWidth { get; set; }
        public double DefaultDepth { get; set; }
        public double DefaultHeight { get; set; }
        public bool RequiresWindow { get; set; }
        public bool RequiresPlumbing { get; set; }
    }

    public class OpeningSuggestion
    {
        public string Type { get; set; }
        public double Width { get; set; }
        public string WallPosition { get; set; }
        public string Reason { get; set; }
    }

    #endregion
}
