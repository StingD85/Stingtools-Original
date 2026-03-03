using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;
using Pipeline = StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Advanced wall creation engine that goes beyond simple line-to-wall conversion.
    /// Supports parallel lines, centerlines, polylines, arcs, splines, and hatched regions.
    /// Auto-creates wall types, handles joins, and integrates with family management.
    /// </summary>
    public class AdvancedWallCreator : IElementCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, WallTypeDefinition> _wallTypes;
        private readonly Dictionary<string, WallCreationStrategy> _creationStrategies;
        private readonly List<CreatedWall> _createdWalls;
        private readonly WallCreationSettings _defaultSettings;
        private readonly object _lock = new object();

        public string ElementType => "Wall";

        public event EventHandler<WallCreatedEventArgs> WallCreated;
        public event EventHandler<WallTypeAutoCreatedEventArgs> TypeAutoCreated;

        public AdvancedWallCreator(WallCreationSettings settings = null)
        {
            _defaultSettings = settings ?? new WallCreationSettings();
            _wallTypes = InitializeWallTypes();
            _creationStrategies = InitializeStrategies();
            _createdWalls = new List<CreatedWall>();

            Logger.Info("AdvancedWallCreator initialized with {0} wall types, {1} creation strategies",
                _wallTypes.Count, _creationStrategies.Count);
        }

        #region Wall Types

        private Dictionary<string, WallTypeDefinition> InitializeWallTypes()
        {
            return new Dictionary<string, WallTypeDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                // External Walls
                ["EXT_BRICK_230"] = new WallTypeDefinition
                {
                    TypeId = "WT001",
                    TypeName = "External Brick Wall 230mm",
                    Category = WallCategory.External,
                    Function = WallFunction.Exterior,
                    Thickness = 230,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Face Brick", Thickness = 110, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Air Gap", Thickness = 10, Function = LayerFunction.AirGap },
                        new WallLayer { Material = "Concrete Block", Thickness = 100, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Plaster", Thickness = 10, Function = LayerFunction.Finish }
                    },
                    UValue = 1.8,
                    FireRating = "60",
                    AcousticRating = 45
                },
                ["EXT_BLOCK_200"] = new WallTypeDefinition
                {
                    TypeId = "WT002",
                    TypeName = "External Block Wall 200mm",
                    Category = WallCategory.External,
                    Function = WallFunction.Exterior,
                    Thickness = 200,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Render", Thickness = 15, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Concrete Block", Thickness = 150, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Plaster", Thickness = 15, Function = LayerFunction.Finish }
                    },
                    UValue = 2.1,
                    FireRating = "60",
                    AcousticRating = 42
                },
                ["EXT_CAVITY_280"] = new WallTypeDefinition
                {
                    TypeId = "WT003",
                    TypeName = "External Cavity Wall 280mm",
                    Category = WallCategory.External,
                    Function = WallFunction.Exterior,
                    Thickness = 280,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Face Brick", Thickness = 110, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Cavity + Insulation", Thickness = 50, Function = LayerFunction.Insulation },
                        new WallLayer { Material = "Concrete Block", Thickness = 100, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Plaster", Thickness = 20, Function = LayerFunction.Finish }
                    },
                    UValue = 0.45,
                    FireRating = "90",
                    AcousticRating = 50
                },

                // Internal Walls
                ["INT_BLOCK_150"] = new WallTypeDefinition
                {
                    TypeId = "WT010",
                    TypeName = "Internal Block Wall 150mm",
                    Category = WallCategory.Internal,
                    Function = WallFunction.Interior,
                    Thickness = 150,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Plaster", Thickness = 15, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Concrete Block", Thickness = 100, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Plaster", Thickness = 15, Function = LayerFunction.Finish }
                    },
                    UValue = 2.5,
                    FireRating = "30",
                    AcousticRating = 38
                },
                ["INT_BLOCK_100"] = new WallTypeDefinition
                {
                    TypeId = "WT011",
                    TypeName = "Internal Block Wall 100mm",
                    Category = WallCategory.Internal,
                    Function = WallFunction.Interior,
                    Thickness = 100,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Plaster", Thickness = 10, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Concrete Block", Thickness = 75, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Plaster", Thickness = 10, Function = LayerFunction.Finish }
                    },
                    UValue = 2.8,
                    FireRating = "30",
                    AcousticRating = 35
                },
                ["INT_DRYWALL_100"] = new WallTypeDefinition
                {
                    TypeId = "WT012",
                    TypeName = "Internal Drywall 100mm",
                    Category = WallCategory.Internal,
                    Function = WallFunction.Interior,
                    Thickness = 100,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Gypsum Board", Thickness = 12.5, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Metal Stud", Thickness = 75, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Gypsum Board", Thickness = 12.5, Function = LayerFunction.Finish }
                    },
                    UValue = 3.2,
                    FireRating = "30",
                    AcousticRating = 32
                },
                ["INT_DRYWALL_ACOUSTIC_150"] = new WallTypeDefinition
                {
                    TypeId = "WT013",
                    TypeName = "Internal Acoustic Drywall 150mm",
                    Category = WallCategory.Internal,
                    Function = WallFunction.Interior,
                    Thickness = 150,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Gypsum Board (Double)", Thickness = 25, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Metal Stud + Rockwool", Thickness = 100, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Gypsum Board (Double)", Thickness = 25, Function = LayerFunction.Finish }
                    },
                    UValue = 0.55,
                    FireRating = "60",
                    AcousticRating = 55
                },

                // Partition Walls
                ["PART_GLASS_100"] = new WallTypeDefinition
                {
                    TypeId = "WT020",
                    TypeName = "Glass Partition 100mm",
                    Category = WallCategory.Partition,
                    Function = WallFunction.Interior,
                    Thickness = 100,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Glass (Tempered)", Thickness = 12, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Air Gap", Thickness = 76, Function = LayerFunction.AirGap },
                        new WallLayer { Material = "Glass (Tempered)", Thickness = 12, Function = LayerFunction.Structure }
                    },
                    AcousticRating = 35,
                    IsGlazed = true
                },

                // Retaining/Foundation Walls
                ["RET_CONCRETE_300"] = new WallTypeDefinition
                {
                    TypeId = "WT030",
                    TypeName = "Retaining Wall RC 300mm",
                    Category = WallCategory.Retaining,
                    Function = WallFunction.Retaining,
                    Thickness = 300,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Reinforced Concrete", Thickness = 300, Function = LayerFunction.Structure }
                    },
                    IsStructural = true
                },
                ["FDN_CONCRETE_200"] = new WallTypeDefinition
                {
                    TypeId = "WT031",
                    TypeName = "Foundation Wall 200mm",
                    Category = WallCategory.Foundation,
                    Function = WallFunction.Foundation,
                    Thickness = 200,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Reinforced Concrete", Thickness = 200, Function = LayerFunction.Structure }
                    },
                    IsStructural = true,
                    IsBelowGrade = true
                },

                // Africa-specific Wall Types
                ["AFR_CEB_250"] = new WallTypeDefinition
                {
                    TypeId = "WT040",
                    TypeName = "Compressed Earth Block Wall 250mm",
                    Category = WallCategory.External,
                    Function = WallFunction.Exterior,
                    Thickness = 250,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Lime Wash", Thickness = 5, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Compressed Earth Block", Thickness = 240, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Earth Plaster", Thickness = 5, Function = LayerFunction.Finish }
                    },
                    UValue = 1.5,
                    FireRating = "120",
                    AcousticRating = 48,
                    IsSustainable = true,
                    IsLocalMaterial = true
                },
                ["AFR_STONE_400"] = new WallTypeDefinition
                {
                    TypeId = "WT041",
                    TypeName = "Natural Stone Wall 400mm",
                    Category = WallCategory.External,
                    Function = WallFunction.Exterior,
                    Thickness = 400,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Natural Stone", Thickness = 400, Function = LayerFunction.Structure }
                    },
                    UValue = 2.0,
                    FireRating = "240",
                    AcousticRating = 55,
                    IsLocalMaterial = true
                },
                ["AFR_TIMBER_FRAME_150"] = new WallTypeDefinition
                {
                    TypeId = "WT042",
                    TypeName = "Timber Frame Wall 150mm",
                    Category = WallCategory.External,
                    Function = WallFunction.Exterior,
                    Thickness = 150,
                    Structure = new List<WallLayer>
                    {
                        new WallLayer { Material = "Timber Cladding", Thickness = 20, Function = LayerFunction.Finish },
                        new WallLayer { Material = "Breathable Membrane", Thickness = 2, Function = LayerFunction.Membrane },
                        new WallLayer { Material = "Timber Stud", Thickness = 100, Function = LayerFunction.Structure },
                        new WallLayer { Material = "Plywood", Thickness = 12, Function = LayerFunction.Sheathing },
                        new WallLayer { Material = "Gypsum Board", Thickness = 16, Function = LayerFunction.Finish }
                    },
                    UValue = 0.35,
                    FireRating = "30",
                    AcousticRating = 40,
                    IsSustainable = true
                }
            };
        }

        private Dictionary<string, WallCreationStrategy> InitializeStrategies()
        {
            return new Dictionary<string, WallCreationStrategy>(StringComparer.OrdinalIgnoreCase)
            {
                ["ParallelLines"] = new WallCreationStrategy
                {
                    StrategyId = "STR001",
                    Name = "Parallel Lines",
                    Description = "Creates wall from two parallel lines (wall faces)",
                    RequiredGeometry = new List<string> { "Line", "Line" },
                    AutoDetectThickness = true,
                    ThicknessTolerance = 50
                },
                ["SingleCenterline"] = new WallCreationStrategy
                {
                    StrategyId = "STR002",
                    Name = "Single Centerline",
                    Description = "Creates wall along centerline with specified thickness",
                    RequiredGeometry = new List<string> { "Line" },
                    AutoDetectThickness = false,
                    RequiresThicknessInput = true,
                    DefaultThickness = 200
                },
                ["Polyline"] = new WallCreationStrategy
                {
                    StrategyId = "STR003",
                    Name = "Polyline",
                    Description = "Creates connected walls from polyline vertices",
                    RequiredGeometry = new List<string> { "Polyline" },
                    CreateJoinsAutomatically = true,
                    JoinAtVertices = true
                },
                ["Arc"] = new WallCreationStrategy
                {
                    StrategyId = "STR004",
                    Name = "Arc Wall",
                    Description = "Creates curved wall from arc geometry",
                    RequiredGeometry = new List<string> { "Arc" },
                    SupportsCurves = true
                },
                ["Spline"] = new WallCreationStrategy
                {
                    StrategyId = "STR005",
                    Name = "Spline Wall",
                    Description = "Creates curved wall from spline (approximated with segments)",
                    RequiredGeometry = new List<string> { "Spline" },
                    SupportsCurves = true,
                    SplineSegmentation = 20 // Number of segments for approximation
                },
                ["HatchedRegion"] = new WallCreationStrategy
                {
                    StrategyId = "STR006",
                    Name = "Hatched Region",
                    Description = "Creates wall from filled/hatched region boundary",
                    RequiredGeometry = new List<string> { "FilledRegion" },
                    ExtractsBoundary = true
                },
                ["PointBetweenLines"] = new WallCreationStrategy
                {
                    StrategyId = "STR007",
                    Name = "Point Between Lines",
                    Description = "User picks point between two parallel lines (EaseBit-style)",
                    RequiredGeometry = new List<string> { "Point" },
                    RequiresNearbyLineDetection = true,
                    MaxLineSearchDistance = 1000
                },
                ["RoomBoundary"] = new WallCreationStrategy
                {
                    StrategyId = "STR008",
                    Name = "Room Boundary",
                    Description = "Creates walls around a closed room boundary",
                    RequiredGeometry = new List<string> { "ClosedPolyline" },
                    CreateJoinsAutomatically = true
                }
            };
        }

        #endregion

        #region IElementCreator Implementation

        public bool CanCreate(Pipeline.DesignElement element)
        {
            return element.ElementType.Equals("Wall", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<Pipeline.CreatedElement> CreateElementAsync(
            Pipeline.ElementCreationParams parameters,
            CancellationToken cancellationToken)
        {
            var wall = await CreateWallAsync(
                ConvertToWallInput(parameters.DesignElement),
                parameters.Context?.RevitDocument,
                null,
                cancellationToken);

            return new Pipeline.CreatedElement
            {
                RevitElementId = wall?.WallId,
                DesignElement = parameters.DesignElement,
                CreationStatus = wall != null ? Pipeline.CreationStatus.Success : Pipeline.CreationStatus.Failed
            };
        }

        private WallCreationInput ConvertToWallInput(Pipeline.DesignElement element)
        {
            var input = new WallCreationInput
            {
                InputId = element.ElementId
            };

            // Convert geometry
            if (element.Geometry?.Curves?.Count >= 2)
            {
                input.Geometry = element.Geometry.Curves.Select(c => new WallGeometry
                {
                    GeometryType = "Line",
                    StartPoint = new Point3D { X = c.Start.X, Y = c.Start.Y, Z = c.Start.Z },
                    EndPoint = new Point3D { X = c.End.X, Y = c.End.Y, Z = c.End.Z }
                }).ToList();
                input.Strategy = "ParallelLines";
            }
            else if (element.Geometry?.Curves?.Count == 1)
            {
                var curve = element.Geometry.Curves[0];
                input.Geometry = new List<WallGeometry>
                {
                    new WallGeometry
                    {
                        GeometryType = "Line",
                        StartPoint = new Point3D { X = curve.Start.X, Y = curve.Start.Y, Z = curve.Start.Z },
                        EndPoint = new Point3D { X = curve.End.X, Y = curve.End.Y, Z = curve.End.Z }
                    }
                };
                input.Strategy = "SingleCenterline";
                input.Thickness = element.Geometry.Width > 0 ? element.Geometry.Width : 200;
            }

            // Extract parameters
            if (element.Parameters.TryGetValue("Height", out var height))
                input.Height = Convert.ToDouble(height);

            if (element.Parameters.TryGetValue("WallType", out var wallType))
                input.WallTypeName = wallType?.ToString();

            return input;
        }

        #endregion

        #region Wall Creation

        /// <summary>
        /// Creates walls from various geometry inputs.
        /// </summary>
        public async Task<WallCreationResult> CreateWallsAsync(
            List<WallCreationInput> inputs,
            object revitDocument,
            WallCreationOptions options = null,
            IProgress<WallCreationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Creating {0} walls", inputs.Count);
            options ??= new WallCreationOptions();

            var result = new WallCreationResult
            {
                CreatedWalls = new List<CreatedWall>(),
                FailedInputs = new List<FailedWallInput>(),
                AutoCreatedTypes = new List<string>()
            };

            int processed = 0;
            foreach (var input in inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var wall = await CreateWallAsync(input, revitDocument, options, cancellationToken);
                    if (wall != null)
                    {
                        result.CreatedWalls.Add(wall);

                        if (wall.TypeWasAutoCreated)
                        {
                            result.AutoCreatedTypes.Add(wall.WallTypeName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to create wall from input {0}", input.InputId);
                    result.FailedInputs.Add(new FailedWallInput
                    {
                        Input = input,
                        Error = ex.Message
                    });

                    if (!options.ContinueOnError)
                        throw;
                }

                processed++;
                progress?.Report(new WallCreationProgress
                {
                    PercentComplete = processed * 100 / inputs.Count,
                    CurrentOperation = $"Created {processed}/{inputs.Count} walls"
                });
            }

            // Join walls if requested
            if (options.AutoJoinWalls && result.CreatedWalls.Count > 1)
            {
                await JoinWallsAsync(result.CreatedWalls, options, cancellationToken);
            }

            result.Success = result.FailedInputs.Count == 0;
            result.TotalCreated = result.CreatedWalls.Count;
            result.TotalFailed = result.FailedInputs.Count;

            Logger.Info("Wall creation complete: {0} created, {1} failed",
                result.TotalCreated, result.TotalFailed);

            return result;
        }

        /// <summary>
        /// Creates a single wall from input geometry.
        /// </summary>
        public async Task<CreatedWall> CreateWallAsync(
            WallCreationInput input,
            object revitDocument,
            WallCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new WallCreationOptions();

            // Determine strategy
            string strategyName = input.Strategy ?? DetermineStrategy(input);
            if (!_creationStrategies.TryGetValue(strategyName, out var strategy))
            {
                throw new InvalidOperationException($"Unknown wall creation strategy: {strategyName}");
            }

            CreatedWall wall = null;

            await Task.Run(() =>
            {
                // Determine wall type
                var wallType = DetermineWallType(input, strategy, options);

                // Create based on strategy
                switch (strategyName)
                {
                    case "ParallelLines":
                        wall = CreateFromParallelLines(input, wallType, options);
                        break;

                    case "SingleCenterline":
                        wall = CreateFromCenterline(input, wallType, options);
                        break;

                    case "Polyline":
                        wall = CreateFromPolyline(input, wallType, options);
                        break;

                    case "Arc":
                        wall = CreateFromArc(input, wallType, options);
                        break;

                    case "Spline":
                        wall = CreateFromSpline(input, wallType, strategy, options);
                        break;

                    case "HatchedRegion":
                        wall = CreateFromHatchedRegion(input, wallType, options);
                        break;

                    case "PointBetweenLines":
                        wall = CreateFromPointBetweenLines(input, wallType, strategy, options);
                        break;

                    case "RoomBoundary":
                        wall = CreateFromRoomBoundary(input, wallType, options);
                        break;

                    default:
                        throw new NotSupportedException($"Strategy not implemented: {strategyName}");
                }

                if (wall != null)
                {
                    lock (_lock)
                    {
                        _createdWalls.Add(wall);
                    }

                    OnWallCreated(wall);
                }
            }, cancellationToken);

            return wall;
        }

        #endregion

        #region Creation Strategies

        private CreatedWall CreateFromParallelLines(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationOptions options)
        {
            if (input.Geometry.Count < 2)
                throw new ArgumentException("Parallel lines strategy requires 2 lines");

            var line1 = input.Geometry[0];
            var line2 = input.Geometry[1];

            // Calculate wall centerline and thickness
            var centerline = CalculateCenterline(line1, line2);
            double detectedThickness = CalculateLineDistance(line1, line2);

            // If detected thickness differs significantly from wall type, auto-create type
            bool typeAutoCreated = false;
            if (Math.Abs(detectedThickness - wallType.Thickness) > 20 && options.AutoCreateTypes)
            {
                wallType = AutoCreateWallType(detectedThickness, wallType);
                typeAutoCreated = true;
            }

            // In real implementation, would use Revit API:
            // Wall.Create(doc, centerline, wallType.Id, levelId, height, offset, flip, structural);

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Centerline = centerline,
                Length = CalculateLineLength(centerline),
                Thickness = wallType.Thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                TypeWasAutoCreated = typeAutoCreated,
                CreationStrategy = "ParallelLines",
                SourceGeometry = input.Geometry
            };
        }

        private CreatedWall CreateFromCenterline(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationOptions options)
        {
            if (input.Geometry.Count < 1)
                throw new ArgumentException("Centerline strategy requires 1 line");

            var centerline = input.Geometry[0];

            // Use input thickness or wall type thickness
            double thickness = input.Thickness > 0 ? input.Thickness : wallType.Thickness;

            // Auto-create type if thickness differs
            bool typeAutoCreated = false;
            if (input.Thickness > 0 && Math.Abs(input.Thickness - wallType.Thickness) > 20 && options.AutoCreateTypes)
            {
                wallType = AutoCreateWallType(input.Thickness, wallType);
                typeAutoCreated = true;
            }

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Centerline = centerline,
                Length = CalculateLineLength(centerline),
                Thickness = thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                TypeWasAutoCreated = typeAutoCreated,
                CreationStrategy = "SingleCenterline",
                SourceGeometry = input.Geometry
            };
        }

        private CreatedWall CreateFromPolyline(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationOptions options)
        {
            var polyline = input.Geometry.FirstOrDefault(g => g.GeometryType == "Polyline");
            if (polyline?.Vertices == null || polyline.Vertices.Count < 2)
                throw new ArgumentException("Polyline strategy requires a polyline with vertices");

            // Create walls for each segment
            var segments = new List<WallGeometry>();
            for (int i = 0; i < polyline.Vertices.Count - 1; i++)
            {
                segments.Add(new WallGeometry
                {
                    GeometryType = "Line",
                    StartPoint = polyline.Vertices[i],
                    EndPoint = polyline.Vertices[i + 1]
                });
            }

            // If closed, add closing segment
            if (polyline.IsClosed && polyline.Vertices.Count > 2)
            {
                segments.Add(new WallGeometry
                {
                    GeometryType = "Line",
                    StartPoint = polyline.Vertices[^1],
                    EndPoint = polyline.Vertices[0]
                });
            }

            double totalLength = segments.Sum(s => CalculateLineLength(s));

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Length = totalLength,
                Thickness = wallType.Thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                CreationStrategy = "Polyline",
                SourceGeometry = input.Geometry,
                SegmentCount = segments.Count,
                IsClosedLoop = polyline.IsClosed
            };
        }

        private CreatedWall CreateFromArc(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationOptions options)
        {
            var arc = input.Geometry.FirstOrDefault(g => g.GeometryType == "Arc");
            if (arc == null)
                throw new ArgumentException("Arc strategy requires an arc geometry");

            // Calculate arc length
            double arcLength = CalculateArcLength(arc);

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Length = arcLength,
                Thickness = wallType.Thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                CreationStrategy = "Arc",
                SourceGeometry = input.Geometry,
                IsCurved = true,
                Radius = arc.Radius,
                SweepAngle = arc.SweepAngle
            };
        }

        private CreatedWall CreateFromSpline(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationStrategy strategy,
            WallCreationOptions options)
        {
            var spline = input.Geometry.FirstOrDefault(g => g.GeometryType == "Spline");
            if (spline?.ControlPoints == null)
                throw new ArgumentException("Spline strategy requires spline with control points");

            // Approximate spline with line segments
            int segmentCount = strategy.SplineSegmentation;
            var approximatedPoints = ApproximateSpline(spline.ControlPoints, segmentCount);

            double totalLength = 0;
            for (int i = 0; i < approximatedPoints.Count - 1; i++)
            {
                totalLength += Distance3D(approximatedPoints[i], approximatedPoints[i + 1]);
            }

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Length = totalLength,
                Thickness = wallType.Thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                CreationStrategy = "Spline",
                SourceGeometry = input.Geometry,
                IsCurved = true,
                SegmentCount = segmentCount
            };
        }

        private CreatedWall CreateFromHatchedRegion(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationOptions options)
        {
            var region = input.Geometry.FirstOrDefault(g => g.IsFilled);
            if (region?.Boundary == null)
                throw new ArgumentException("Hatched region strategy requires filled region with boundary");

            // Extract boundary and create wall along it
            double boundaryLength = CalculateBoundaryLength(region.Boundary);

            // Detect thickness from region width
            double detectedThickness = DetectRegionThickness(region);

            bool typeAutoCreated = false;
            if (Math.Abs(detectedThickness - wallType.Thickness) > 20 && options.AutoCreateTypes)
            {
                wallType = AutoCreateWallType(detectedThickness, wallType);
                typeAutoCreated = true;
            }

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Length = boundaryLength,
                Thickness = wallType.Thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                TypeWasAutoCreated = typeAutoCreated,
                CreationStrategy = "HatchedRegion",
                SourceGeometry = input.Geometry
            };
        }

        private CreatedWall CreateFromPointBetweenLines(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationStrategy strategy,
            WallCreationOptions options)
        {
            // This is EaseBit-style creation
            var point = input.SelectionPoint;
            if (point == null)
                throw new ArgumentException("Point between lines strategy requires selection point");

            // Find two nearest parallel lines
            var nearbyLines = FindNearbyParallelLines(point, input.AvailableGeometry, strategy.MaxLineSearchDistance);

            if (nearbyLines.Count < 2)
                throw new InvalidOperationException("Could not find two parallel lines near selection point");

            // Create input for parallel lines strategy
            var parallelInput = new WallCreationInput
            {
                InputId = input.InputId,
                Geometry = nearbyLines.Take(2).ToList(),
                Height = input.Height,
                BaseOffset = input.BaseOffset,
                Strategy = "ParallelLines"
            };

            return CreateFromParallelLines(parallelInput, wallType, options);
        }

        private CreatedWall CreateFromRoomBoundary(
            WallCreationInput input,
            WallTypeDefinition wallType,
            WallCreationOptions options)
        {
            var boundary = input.Geometry.FirstOrDefault(g => g.GeometryType == "ClosedPolyline" || g.IsClosed);
            if (boundary?.Vertices == null || boundary.Vertices.Count < 3)
                throw new ArgumentException("Room boundary strategy requires closed polyline with 3+ vertices");

            // Create walls around boundary
            double totalLength = CalculateBoundaryLength(boundary.Vertices);

            return new CreatedWall
            {
                WallId = Guid.NewGuid().ToString("N"),
                WallTypeName = wallType.TypeName,
                WallTypeId = wallType.TypeId,
                Length = totalLength,
                Thickness = wallType.Thickness,
                Height = input.Height > 0 ? input.Height : options.DefaultHeight,
                BaseOffset = input.BaseOffset,
                CreationStrategy = "RoomBoundary",
                SourceGeometry = input.Geometry,
                IsClosedLoop = true,
                SegmentCount = boundary.Vertices.Count
            };
        }

        #endregion

        #region Type Management

        private WallTypeDefinition DetermineWallType(
            WallCreationInput input,
            WallCreationStrategy strategy,
            WallCreationOptions options)
        {
            // If type specified, use it
            if (!string.IsNullOrEmpty(input.WallTypeName) &&
                _wallTypes.TryGetValue(input.WallTypeName, out var specifiedType))
            {
                return specifiedType;
            }

            // Determine based on thickness and category
            double thickness = input.Thickness;

            if (thickness == 0 && strategy.AutoDetectThickness && input.Geometry.Count >= 2)
            {
                thickness = CalculateLineDistance(input.Geometry[0], input.Geometry[1]);
            }

            if (thickness == 0)
            {
                thickness = strategy.DefaultThickness > 0 ? strategy.DefaultThickness : 200;
            }

            // Find best matching type
            var category = input.WallCategory ?? (options.IsExternalWall ? WallCategory.External : WallCategory.Internal);

            var matchingTypes = _wallTypes.Values
                .Where(t => t.Category == category)
                .OrderBy(t => Math.Abs(t.Thickness - thickness))
                .ToList();

            if (matchingTypes.Count > 0)
            {
                var bestMatch = matchingTypes.First();

                // If close enough, use existing type
                if (Math.Abs(bestMatch.Thickness - thickness) <= options.TypeMatchTolerance)
                {
                    return bestMatch;
                }
            }

            // Return default or create new type
            if (options.AutoCreateTypes)
            {
                return AutoCreateWallType(thickness, matchingTypes.FirstOrDefault() ?? _wallTypes.Values.First());
            }

            return matchingTypes.FirstOrDefault() ?? _wallTypes.Values.First();
        }

        private WallTypeDefinition AutoCreateWallType(double thickness, WallTypeDefinition baseType)
        {
            string newTypeName = $"{baseType.Category}_{(int)thickness}mm_Auto";

            if (_wallTypes.TryGetValue(newTypeName, out var existing))
                return existing;

            var newType = new WallTypeDefinition
            {
                TypeId = Guid.NewGuid().ToString("N"),
                TypeName = newTypeName,
                Category = baseType.Category,
                Function = baseType.Function,
                Thickness = thickness,
                Structure = ScaleLayers(baseType.Structure, thickness / baseType.Thickness),
                IsAutoCreated = true
            };

            lock (_lock)
            {
                _wallTypes[newTypeName] = newType;
            }

            OnTypeAutoCreated(newType);
            Logger.Info("Auto-created wall type: {0} ({1}mm)", newTypeName, thickness);

            return newType;
        }

        private List<WallLayer> ScaleLayers(List<WallLayer> layers, double scaleFactor)
        {
            if (layers == null) return null;

            return layers.Select(l => new WallLayer
            {
                Material = l.Material,
                Thickness = l.Thickness * scaleFactor,
                Function = l.Function
            }).ToList();
        }

        #endregion

        #region Wall Joining

        private async Task JoinWallsAsync(
            List<CreatedWall> walls,
            WallCreationOptions options,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Find walls that share endpoints
                for (int i = 0; i < walls.Count; i++)
                {
                    for (int j = i + 1; j < walls.Count; j++)
                    {
                        if (WallsShareEndpoint(walls[i], walls[j], options.JoinTolerance))
                        {
                            // In real implementation, would use:
                            // JoinGeometryUtils.JoinGeometry(doc, wall1, wall2);

                            walls[i].JoinedWallIds ??= new List<string>();
                            walls[i].JoinedWallIds.Add(walls[j].WallId);

                            walls[j].JoinedWallIds ??= new List<string>();
                            walls[j].JoinedWallIds.Add(walls[i].WallId);
                        }
                    }
                }
            }, cancellationToken);
        }

        private bool WallsShareEndpoint(CreatedWall wall1, CreatedWall wall2, double tolerance)
        {
            if (wall1.Centerline == null || wall2.Centerline == null)
                return false;

            var endpoints1 = new[] { wall1.Centerline.StartPoint, wall1.Centerline.EndPoint };
            var endpoints2 = new[] { wall2.Centerline.StartPoint, wall2.Centerline.EndPoint };

            foreach (var p1 in endpoints1)
            {
                foreach (var p2 in endpoints2)
                {
                    if (p1 != null && p2 != null && Distance3D(p1, p2) <= tolerance)
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Geometry Utilities

        private string DetermineStrategy(WallCreationInput input)
        {
            if (input.Geometry == null || input.Geometry.Count == 0)
            {
                if (input.SelectionPoint != null)
                    return "PointBetweenLines";
                throw new ArgumentException("No geometry provided");
            }

            var firstGeom = input.Geometry[0];

            if (input.Geometry.Count >= 2 &&
                input.Geometry.All(g => g.GeometryType == "Line") &&
                AreParallel(input.Geometry[0], input.Geometry[1]))
            {
                return "ParallelLines";
            }

            return firstGeom.GeometryType switch
            {
                "Line" => "SingleCenterline",
                "Polyline" => "Polyline",
                "Arc" => "Arc",
                "Spline" => "Spline",
                "FilledRegion" or "Hatch" => "HatchedRegion",
                "ClosedPolyline" => "RoomBoundary",
                _ => "SingleCenterline"
            };
        }

        private WallGeometry CalculateCenterline(WallGeometry line1, WallGeometry line2)
        {
            return new WallGeometry
            {
                GeometryType = "Line",
                StartPoint = new Point3D
                {
                    X = (line1.StartPoint.X + line2.StartPoint.X) / 2,
                    Y = (line1.StartPoint.Y + line2.StartPoint.Y) / 2,
                    Z = (line1.StartPoint.Z + line2.StartPoint.Z) / 2
                },
                EndPoint = new Point3D
                {
                    X = (line1.EndPoint.X + line2.EndPoint.X) / 2,
                    Y = (line1.EndPoint.Y + line2.EndPoint.Y) / 2,
                    Z = (line1.EndPoint.Z + line2.EndPoint.Z) / 2
                }
            };
        }

        private double CalculateLineDistance(WallGeometry line1, WallGeometry line2)
        {
            // Calculate perpendicular distance between parallel lines
            var midPoint1 = new Point3D
            {
                X = (line1.StartPoint.X + line1.EndPoint.X) / 2,
                Y = (line1.StartPoint.Y + line1.EndPoint.Y) / 2,
                Z = (line1.StartPoint.Z + line1.EndPoint.Z) / 2
            };

            return PointToLineDistance(midPoint1, line2);
        }

        private double PointToLineDistance(Point3D point, WallGeometry line)
        {
            double x0 = point.X, y0 = point.Y;
            double x1 = line.StartPoint.X, y1 = line.StartPoint.Y;
            double x2 = line.EndPoint.X, y2 = line.EndPoint.Y;

            double numerator = Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1);
            double denominator = Math.Sqrt(Math.Pow(y2 - y1, 2) + Math.Pow(x2 - x1, 2));

            return denominator > 0 ? numerator / denominator : 0;
        }

        private double CalculateLineLength(WallGeometry line)
        {
            if (line.EndPoint == null) return 0;
            return Distance3D(line.StartPoint, line.EndPoint);
        }

        private double Distance3D(Point3D p1, Point3D p2)
        {
            return Math.Sqrt(
                Math.Pow(p2.X - p1.X, 2) +
                Math.Pow(p2.Y - p1.Y, 2) +
                Math.Pow(p2.Z - p1.Z, 2));
        }

        private bool AreParallel(WallGeometry line1, WallGeometry line2, double toleranceDegrees = 2)
        {
            double angle1 = Math.Atan2(
                line1.EndPoint.Y - line1.StartPoint.Y,
                line1.EndPoint.X - line1.StartPoint.X) * 180 / Math.PI;

            double angle2 = Math.Atan2(
                line2.EndPoint.Y - line2.StartPoint.Y,
                line2.EndPoint.X - line2.StartPoint.X) * 180 / Math.PI;

            double diff = Math.Abs(angle1 - angle2);
            if (diff > 90) diff = 180 - diff;

            return diff <= toleranceDegrees;
        }

        private double CalculateArcLength(WallGeometry arc)
        {
            // Arc length = radius * angle (in radians)
            double angleRadians = arc.SweepAngle * Math.PI / 180;
            return arc.Radius * Math.Abs(angleRadians);
        }

        private List<Point3D> ApproximateSpline(List<Point3D> controlPoints, int segments)
        {
            // Simple linear interpolation for approximation
            var result = new List<Point3D>();

            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                // Simplified - would use actual spline calculation
                int index = (int)(t * (controlPoints.Count - 1));
                result.Add(controlPoints[Math.Min(index, controlPoints.Count - 1)]);
            }

            return result;
        }

        private double CalculateBoundaryLength(List<Point3D> vertices)
        {
            double length = 0;
            for (int i = 0; i < vertices.Count - 1; i++)
            {
                length += Distance3D(vertices[i], vertices[i + 1]);
            }
            // Add closing segment
            if (vertices.Count > 2)
            {
                length += Distance3D(vertices[^1], vertices[0]);
            }
            return length;
        }

        private double DetectRegionThickness(WallGeometry region)
        {
            // Analyze region to detect wall thickness
            // This would analyze the region's dimensions
            return region.Width > 0 ? region.Width : 200;
        }

        private List<WallGeometry> FindNearbyParallelLines(
            Point3D point,
            List<WallGeometry> availableGeometry,
            double maxDistance)
        {
            if (availableGeometry == null)
                return new List<WallGeometry>();

            var lines = availableGeometry
                .Where(g => g.GeometryType == "Line")
                .ToList();

            // Find lines within max distance
            var nearbyLines = lines
                .Where(l => PointToLineDistance(point, l) <= maxDistance)
                .OrderBy(l => PointToLineDistance(point, l))
                .ToList();

            // Filter to parallel pairs
            var parallelPairs = new List<WallGeometry>();

            for (int i = 0; i < nearbyLines.Count && parallelPairs.Count < 2; i++)
            {
                if (parallelPairs.Count == 0)
                {
                    parallelPairs.Add(nearbyLines[i]);
                }
                else if (AreParallel(parallelPairs[0], nearbyLines[i]))
                {
                    parallelPairs.Add(nearbyLines[i]);
                }
            }

            return parallelPairs;
        }

        #endregion

        #region Events

        private void OnWallCreated(CreatedWall wall)
        {
            WallCreated?.Invoke(this, new WallCreatedEventArgs(wall));
        }

        private void OnTypeAutoCreated(WallTypeDefinition type)
        {
            TypeAutoCreated?.Invoke(this, new WallTypeAutoCreatedEventArgs(type));
        }

        #endregion

        #region Public API

        public IEnumerable<WallTypeDefinition> GetAvailableWallTypes()
        {
            lock (_lock)
            {
                return _wallTypes.Values.ToList();
            }
        }

        public IEnumerable<WallTypeDefinition> GetWallTypesByCategory(WallCategory category)
        {
            lock (_lock)
            {
                return _wallTypes.Values.Where(t => t.Category == category).ToList();
            }
        }

        public IEnumerable<string> GetAvailableStrategies()
        {
            return _creationStrategies.Keys;
        }

        public IEnumerable<CreatedWall> GetCreatedWalls()
        {
            lock (_lock)
            {
                return _createdWalls.ToList();
            }
        }

        #endregion

        #region IElementCreator Implementation

        /// <summary>
        /// Creates an element from parameters (IElementCreator implementation).
        /// </summary>
        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            var result = new CreationResult
            {
                ElementType = parameters.ElementType
            };

            try
            {
                // Extract wall-specific parameters
                var input = new WallCreationInput();

                if (parameters.Location != null)
                {
                    input.StartPoint = parameters.Location;
                    if (parameters.Parameters.TryGetValue("EndPoint", out var endPointObj) && endPointObj is Point3D endPoint)
                    {
                        input.EndPoint = endPoint;
                    }
                    else
                    {
                        // Create a default 5m wall in X direction
                        input.EndPoint = new Point3D(parameters.Location.X + 5000, parameters.Location.Y, parameters.Location.Z);
                    }
                }

                if (parameters.Parameters.TryGetValue("Thickness", out var thicknessObj) && thicknessObj is double thickness)
                {
                    input.Thickness = thickness;
                }

                if (parameters.Parameters.TryGetValue("Height", out var heightObj) && heightObj is double height)
                {
                    input.Height = height;
                }

                if (parameters.Parameters.TryGetValue("WallTypeName", out var typeNameObj) && typeNameObj is string typeName)
                {
                    input.WallTypeName = typeName;
                }

                var wall = await CreateWallAsync(input, parameters.AdditionalData, null, cancellationToken);

                if (wall != null)
                {
                    result.Success = true;
                    result.CreatedElementId = wall.WallId.GetHashCode();
                    result.UniqueId = wall.WallId;
                    result.Message = $"Wall created successfully: {wall.WallTypeName}";
                }
                else
                {
                    result.Success = false;
                    result.Message = "Failed to create wall";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create wall element");
                result.Success = false;
                result.Message = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Modifies an existing element (IElementCreator implementation).
        /// </summary>
        public async Task<ModificationResult> ModifyAsync(
            int elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            var result = new ModificationResult
            {
                ElementId = elementId
            };

            await Task.Run(() =>
            {
                // Find the wall
                var wall = _createdWalls.FirstOrDefault(w => w.WallId.GetHashCode() == elementId);

                if (wall == null)
                {
                    result.Success = false;
                    result.Message = $"Wall with id {elementId} not found";
                    return;
                }

                // Apply modifications
                foreach (var mod in modifications)
                {
                    switch (mod.Key.ToLowerInvariant())
                    {
                        case "height":
                            if (mod.Value is double height)
                            {
                                wall.Height = height;
                                result.ModifiedProperties.Add("Height");
                            }
                            break;
                        // Add more modification cases as needed
                    }
                }

                result.Success = true;
                result.Message = $"Modified {result.ModifiedProperties.Count} properties";
            }, cancellationToken);

            return result;
        }

        #endregion
    }

    #region Data Models

    public enum WallCategory
    {
        External,
        Internal,
        Partition,
        Retaining,
        Foundation,
        Curtain
    }

    public enum WallFunction
    {
        Exterior,
        Interior,
        Retaining,
        Foundation,
        Soffit,
        CoreShaft
    }

    public enum LayerFunction
    {
        Structure,
        Finish,
        Insulation,
        AirGap,
        Membrane,
        Sheathing
    }

    public class WallTypeDefinition
    {
        public string TypeId { get; set; }
        public string TypeName { get; set; }
        public WallCategory Category { get; set; }
        public WallFunction Function { get; set; }
        public double Thickness { get; set; }
        public List<WallLayer> Structure { get; set; }
        public double? UValue { get; set; }
        public string FireRating { get; set; }
        public double? AcousticRating { get; set; }
        public bool IsStructural { get; set; }
        public bool IsBelowGrade { get; set; }
        public bool IsGlazed { get; set; }
        public bool IsSustainable { get; set; }
        public bool IsLocalMaterial { get; set; }
        public bool IsAutoCreated { get; set; }
    }

    public class WallLayer
    {
        public string Material { get; set; }
        public double Thickness { get; set; }
        public LayerFunction Function { get; set; }
    }

    public class WallCreationStrategy
    {
        public string StrategyId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> RequiredGeometry { get; set; }
        public bool AutoDetectThickness { get; set; }
        public bool RequiresThicknessInput { get; set; }
        public double DefaultThickness { get; set; }
        public double ThicknessTolerance { get; set; }
        public bool CreateJoinsAutomatically { get; set; }
        public bool JoinAtVertices { get; set; }
        public bool SupportsCurves { get; set; }
        public int SplineSegmentation { get; set; }
        public bool ExtractsBoundary { get; set; }
        public bool RequiresNearbyLineDetection { get; set; }
        public double MaxLineSearchDistance { get; set; }
    }

    public class WallCreationInput
    {
        public string InputId { get; set; }
        public List<WallGeometry> Geometry { get; set; } = new List<WallGeometry>();
        public string Strategy { get; set; }
        public string WallTypeName { get; set; }
        public WallCategory? WallCategory { get; set; }
        public double Thickness { get; set; }
        public double Height { get; set; }
        public double BaseOffset { get; set; }
        public string LevelId { get; set; }
        public Point3D SelectionPoint { get; set; }
        public List<WallGeometry> AvailableGeometry { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
    }

    public class WallGeometry
    {
        public string GeometryType { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public Point3D Center { get; set; }
        public double Radius { get; set; }
        public double SweepAngle { get; set; }
        public double Width { get; set; }
        public bool IsFilled { get; set; }
        public bool IsClosed { get; set; }
        public List<Point3D> Vertices { get; set; }
        public List<Point3D> ControlPoints { get; set; }
        public List<Point3D> Boundary { get; set; }
    }

    public class WallCreationOptions
    {
        public double DefaultHeight { get; set; } = 2700;
        public bool AutoJoinWalls { get; set; } = true;
        public double JoinTolerance { get; set; } = 10;
        public bool AutoCreateTypes { get; set; } = true;
        public double TypeMatchTolerance { get; set; } = 20;
        public bool IsExternalWall { get; set; } = false;
        public bool ContinueOnError { get; set; } = true;
    }

    public class WallCreationSettings
    {
        public double DefaultWallHeight { get; set; } = 2700;
        public double DefaultWallThickness { get; set; } = 200;
        public string DefaultWallType { get; set; } = "INT_BLOCK_150";
    }

    public class CreatedWall
    {
        public string WallId { get; set; }
        public string WallTypeName { get; set; }
        public string WallTypeId { get; set; }
        public WallGeometry Centerline { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public double Height { get; set; }
        public double BaseOffset { get; set; }
        public bool TypeWasAutoCreated { get; set; }
        public string CreationStrategy { get; set; }
        public List<WallGeometry> SourceGeometry { get; set; }
        public bool IsCurved { get; set; }
        public double? Radius { get; set; }
        public double? SweepAngle { get; set; }
        public int? SegmentCount { get; set; }
        public bool IsClosedLoop { get; set; }
        public List<string> JoinedWallIds { get; set; }
    }

    public class FailedWallInput
    {
        public WallCreationInput Input { get; set; }
        public string Error { get; set; }
    }

    public class WallCreationResult
    {
        public bool Success { get; set; }
        public List<CreatedWall> CreatedWalls { get; set; }
        public List<FailedWallInput> FailedInputs { get; set; }
        public List<string> AutoCreatedTypes { get; set; }
        public int TotalCreated { get; set; }
        public int TotalFailed { get; set; }
    }

    public class WallCreationProgress
    {
        public int PercentComplete { get; set; }
        public string CurrentOperation { get; set; }
    }

    public class WallCreatedEventArgs : EventArgs
    {
        public CreatedWall Wall { get; }
        public WallCreatedEventArgs(CreatedWall wall) { Wall = wall; }
    }

    public class WallTypeAutoCreatedEventArgs : EventArgs
    {
        public WallTypeDefinition Type { get; }
        public WallTypeAutoCreatedEventArgs(WallTypeDefinition type) { Type = type; }
    }

    #endregion
}
