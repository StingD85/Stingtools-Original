// ============================================================================
// StingBIM.AI.Creation - Structural Drawing Generator
// Auto-generates complete structural drawings from architectural models
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Structural
{
    /// <summary>
    /// Automatically generates complete structural models and drawings
    /// from architectural input including grids, columns, beams, foundations, and slabs.
    /// </summary>
    public class StructuralDrawingGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly StructuralSettings _settings;
        private readonly Dictionary<string, StructuralTemplate> _templates;
        private readonly Dictionary<string, LoadCase> _loadCases;
        private readonly Dictionary<string, MaterialProperties> _materials;

        public StructuralDrawingGenerator(StructuralSettings settings = null)
        {
            _settings = settings ?? new StructuralSettings();
            _templates = InitializeTemplates();
            _loadCases = InitializeLoadCases();
            _materials = InitializeMaterials();
            Logger.Info("StructuralDrawingGenerator initialized");
        }

        #region Main Generation

        public async Task<StructuralGenerationResult> GenerateStructuralModelAsync(
            ArchitecturalModel archModel,
            StructuralGenerationOptions options = null,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating structural model for: {0}", archModel.ProjectName);
            options = options ?? new StructuralGenerationOptions();

            var result = new StructuralGenerationResult
            {
                ProjectName = archModel.ProjectName,
                Grids = new List<StructuralGrid>(),
                Levels = new List<StructuralLevel>(),
                Columns = new List<ColumnElement>(),
                Beams = new List<BeamElement>(),
                Slabs = new List<SlabElement>(),
                Foundations = new List<FoundationElement>(),
                Connections = new List<ConnectionElement>(),
                Drawings = new List<StructuralDrawing>()
            };

            try
            {
                progress?.Report(new GenerationProgress { Stage = "Analyzing architecture", Percentage = 5 });
                var analysis = await AnalyzeArchitecturalModelAsync(archModel);

                progress?.Report(new GenerationProgress { Stage = "Generating grids", Percentage = 15 });
                result.Grids = GenerateStructuralGrids(analysis, options);

                progress?.Report(new GenerationProgress { Stage = "Defining levels", Percentage = 20 });
                result.Levels = DefineStructuralLevels(archModel);

                progress?.Report(new GenerationProgress { Stage = "Placing columns", Percentage = 35 });
                result.Columns = await GenerateColumnsAsync(result.Grids, result.Levels, analysis, options);

                progress?.Report(new GenerationProgress { Stage = "Placing beams", Percentage = 50 });
                result.Beams = await GenerateBeamsAsync(result.Columns, result.Levels, analysis, options);

                progress?.Report(new GenerationProgress { Stage = "Generating slabs", Percentage = 60 });
                result.Slabs = await GenerateSlabsAsync(archModel, result.Levels, options);

                progress?.Report(new GenerationProgress { Stage = "Designing foundations", Percentage = 70 });
                result.Foundations = await GenerateFoundationsAsync(result.Columns, analysis, options);

                progress?.Report(new GenerationProgress { Stage = "Designing connections", Percentage = 80 });
                result.Connections = await GenerateConnectionsAsync(result, options);

                progress?.Report(new GenerationProgress { Stage = "Generating drawings", Percentage = 90 });
                result.Drawings = await GenerateDrawingsAsync(result, options);

                result.IsSuccess = true;
                result.GeneratedAt = DateTime.UtcNow;
                progress?.Report(new GenerationProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("Generated: {0} columns, {1} beams, {2} foundations, {3} drawings",
                    result.Columns.Count, result.Beams.Count, result.Foundations.Count, result.Drawings.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Structural generation failed");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Analysis

        private async Task<ArchitecturalAnalysis> AnalyzeArchitecturalModelAsync(ArchitecturalModel archModel)
        {
            var analysis = new ArchitecturalAnalysis
            {
                Footprint = CalculateFootprint(archModel),
                NumberOfStoreys = archModel.Levels?.Count ?? 1,
                LoadBearingWalls = IdentifyLoadBearingWalls(archModel),
                FloorSpans = CalculateFloorSpans(archModel),
                BuildingType = DetermineBuildingType(archModel),
                DeadLoads = CalculateDeadLoads(archModel),
                LiveLoads = CalculateLiveLoads(archModel)
            };

            analysis.TotalLoads = CombineLoads(analysis.DeadLoads, analysis.LiveLoads);
            return await Task.FromResult(analysis);
        }

        private BuildingFootprint CalculateFootprint(ArchitecturalModel archModel)
        {
            if (archModel.Walls == null || !archModel.Walls.Any())
                return new BuildingFootprint { Length = 20000, Width = 15000 };

            var extWalls = archModel.Walls.Where(w => w.IsExterior).ToList();
            if (!extWalls.Any()) extWalls = archModel.Walls;

            var minX = extWalls.Min(w => Math.Min(w.StartPoint.X, w.EndPoint.X));
            var maxX = extWalls.Max(w => Math.Max(w.StartPoint.X, w.EndPoint.X));
            var minY = extWalls.Min(w => Math.Min(w.StartPoint.Y, w.EndPoint.Y));
            var maxY = extWalls.Max(w => Math.Max(w.StartPoint.Y, w.EndPoint.Y));

            return new BuildingFootprint
            {
                MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY,
                Length = maxX - minX, Width = maxY - minY
            };
        }

        private List<LoadBearingWall> IdentifyLoadBearingWalls(ArchitecturalModel archModel)
        {
            var lbWalls = new List<LoadBearingWall>();
            if (archModel.Walls == null) return lbWalls;

            foreach (var wall in archModel.Walls)
            {
                if (wall.IsExterior || wall.Thickness > 200 || wall.IsLoadBearing)
                {
                    lbWalls.Add(new LoadBearingWall
                    {
                        WallId = wall.Id,
                        StartPoint = wall.StartPoint,
                        EndPoint = wall.EndPoint,
                        Thickness = wall.Thickness
                    });
                }
            }
            return lbWalls;
        }

        private List<FloorSpan> CalculateFloorSpans(ArchitecturalModel archModel)
        {
            var spans = new List<FloorSpan>();
            if (archModel.Rooms == null) return spans;

            foreach (var room in archModel.Rooms)
            {
                var spanX = room.Width > 0 ? room.Width : 6000;
                var spanY = room.Length > 0 ? room.Length : 6000;
                spans.Add(new FloorSpan
                {
                    RoomId = room.Id,
                    SpanX = spanX,
                    SpanY = spanY,
                    MaxSpan = Math.Max(spanX, spanY)
                });
            }
            return spans;
        }

        private BuildingType DetermineBuildingType(ArchitecturalModel archModel)
        {
            var storeys = archModel.Levels?.Count ?? 1;
            if (storeys <= 3) return BuildingType.Residential;
            if (storeys <= 6) return BuildingType.LowRise;
            if (storeys <= 15) return BuildingType.MidRise;
            return BuildingType.HighRise;
        }

        private Dictionary<string, double> CalculateDeadLoads(ArchitecturalModel archModel)
        {
            return new Dictionary<string, double>
            {
                ["SlabSelfWeight"] = 25 * 0.150, // kN/m² for 150mm slab
                ["Finishes"] = 1.5,
                ["Services"] = 0.5,
                ["Partitions"] = 1.0,
                ["Facade"] = 3.0
            };
        }

        private Dictionary<string, double> CalculateLiveLoads(ArchitecturalModel archModel)
        {
            var type = DetermineBuildingType(archModel);
            return type switch
            {
                BuildingType.Residential => new Dictionary<string, double> { ["Floor"] = 2.0, ["Corridor"] = 3.0 },
                BuildingType.LowRise => new Dictionary<string, double> { ["Floor"] = 3.0, ["Corridor"] = 4.0 },
                _ => new Dictionary<string, double> { ["Floor"] = 4.0, ["Corridor"] = 5.0 }
            };
        }

        private Dictionary<string, double> CombineLoads(Dictionary<string, double> dead, Dictionary<string, double> live)
        {
            var combined = new Dictionary<string, double>();
            var totalDead = dead.Values.Sum();
            var totalLive = live.GetValueOrDefault("Floor", 3.0);
            combined["TotalSLS"] = totalDead + totalLive;
            combined["TotalULS"] = 1.35 * totalDead + 1.5 * totalLive;
            return combined;
        }

        #endregion

        #region Grid Generation

        private List<StructuralGrid> GenerateStructuralGrids(ArchitecturalAnalysis analysis, StructuralGenerationOptions options)
        {
            var grids = new List<StructuralGrid>();
            var footprint = analysis.Footprint;

            var spacingX = CalculateGridSpacing(footprint.Length, options.FramingMaterial);
            var spacingY = CalculateGridSpacing(footprint.Width, options.FramingMaterial);

            // X-direction grids (A, B, C...)
            var numX = (int)Math.Ceiling(footprint.Length / spacingX) + 1;
            for (int i = 0; i < numX; i++)
            {
                var x = footprint.MinX + i * spacingX;
                if (x > footprint.MaxX + 100) break;
                grids.Add(new StructuralGrid
                {
                    Name = GetGridLetter(i),
                    Direction = GridDirection.X,
                    Position = x,
                    StartPoint = new Point3D(x, footprint.MinY - 2000, 0),
                    EndPoint = new Point3D(x, footprint.MaxY + 2000, 0)
                });
            }

            // Y-direction grids (1, 2, 3...)
            var numY = (int)Math.Ceiling(footprint.Width / spacingY) + 1;
            for (int i = 0; i < numY; i++)
            {
                var y = footprint.MinY + i * spacingY;
                if (y > footprint.MaxY + 100) break;
                grids.Add(new StructuralGrid
                {
                    Name = (i + 1).ToString(),
                    Direction = GridDirection.Y,
                    Position = y,
                    StartPoint = new Point3D(footprint.MinX - 2000, y, 0),
                    EndPoint = new Point3D(footprint.MaxX + 2000, y, 0)
                });
            }

            return grids;
        }

        private double CalculateGridSpacing(double dimension, FramingMaterial material)
        {
            var target = material switch
            {
                FramingMaterial.ReinforcedConcrete => 6000,
                FramingMaterial.StructuralSteel => 9000,
                FramingMaterial.Timber => 4500,
                _ => 6000
            };
            var bays = Math.Max(1, Math.Round(dimension / target));
            return dimension / bays;
        }

        private string GetGridLetter(int index)
        {
            if (index < 26) return ((char)('A' + index)).ToString();
            return GetGridLetter(index / 26 - 1) + GetGridLetter(index % 26);
        }

        #endregion

        #region Level Generation

        private List<StructuralLevel> DefineStructuralLevels(ArchitecturalModel archModel)
        {
            var levels = new List<StructuralLevel>
            {
                new StructuralLevel { Name = "Foundation", Elevation = -1500, LevelType = LevelType.Foundation }
            };

            if (archModel.Levels != null)
            {
                foreach (var l in archModel.Levels.OrderBy(x => x.Elevation))
                {
                    levels.Add(new StructuralLevel
                    {
                        Id = l.Id,
                        Name = l.Name,
                        Elevation = l.Elevation,
                        LevelType = l.Elevation <= 0 ? LevelType.Ground : LevelType.Floor
                    });
                }
            }
            else
            {
                levels.Add(new StructuralLevel { Name = "Ground Floor", Elevation = 0, LevelType = LevelType.Ground });
                levels.Add(new StructuralLevel { Name = "First Floor", Elevation = 3000, LevelType = LevelType.Floor });
            }

            var top = levels.Where(l => l.LevelType != LevelType.Foundation).Max(l => l.Elevation);
            levels.Add(new StructuralLevel { Name = "Roof", Elevation = top + 3000, LevelType = LevelType.Roof });

            return levels;
        }

        #endregion

        #region Column Generation

        private async Task<List<ColumnElement>> GenerateColumnsAsync(
            List<StructuralGrid> grids,
            List<StructuralLevel> levels,
            ArchitecturalAnalysis analysis,
            StructuralGenerationOptions options)
        {
            var columns = new List<ColumnElement>();
            var xGrids = grids.Where(g => g.Direction == GridDirection.X).ToList();
            var yGrids = grids.Where(g => g.Direction == GridDirection.Y).ToList();
            var floorLevels = levels.Where(l => l.LevelType != LevelType.Roof).OrderBy(l => l.Elevation).ToList();

            int mark = 1;
            foreach (var xGrid in xGrids)
            {
                foreach (var yGrid in yGrids)
                {
                    var pos = new Point3D(xGrid.Position, yGrid.Position, 0);
                    var size = DetermineColumnSize(pos, analysis, options);

                    for (int i = 0; i < floorLevels.Count - 1; i++)
                    {
                        var baseLevel = floorLevels[i];
                        var topLevel = floorLevels[i + 1];

                        columns.Add(new ColumnElement
                        {
                            Id = Guid.NewGuid().ToString(),
                            Mark = $"C{mark}",
                            GridIntersection = $"{xGrid.Name}-{yGrid.Name}",
                            Material = options.FramingMaterial.ToString(),
                            SectionSize = size,
                            CenterPoint = pos,
                            BaseLevel = baseLevel.Name,
                            TopLevel = topLevel.Name,
                            BaseElevation = baseLevel.Elevation,
                            TopElevation = topLevel.Elevation,
                            Reinforcement = CalculateColumnReinforcement(size)
                        });
                    }
                    mark++;
                }
            }

            return await Task.FromResult(columns);
        }

        private string DetermineColumnSize(Point3D pos, ArchitecturalAnalysis analysis, StructuralGenerationOptions options)
        {
            var load = analysis.TotalLoads.GetValueOrDefault("TotalULS", 10) * 36 * analysis.NumberOfStoreys; // kN
            return options.FramingMaterial switch
            {
                FramingMaterial.ReinforcedConcrete => SizeRCColumn(load),
                FramingMaterial.StructuralSteel => SizeSteelColumn(load),
                _ => "300x300"
            };
        }

        private string SizeRCColumn(double load)
        {
            var side = Math.Sqrt(load * 1000 / (0.4 * 0.567 * 30));
            side = Math.Ceiling(side / 50) * 50;
            side = Math.Clamp(side, 300, 800);
            return $"{side}x{side}";
        }

        private string SizeSteelColumn(double load)
        {
            if (load < 500) return "UC 152x152x23";
            if (load < 1000) return "UC 203x203x46";
            if (load < 2000) return "UC 254x254x73";
            return "UC 305x305x97";
        }

        private string CalculateColumnReinforcement(string size)
        {
            var s = double.TryParse(size.Split('x')[0], out var side) ? side : 300;
            var bars = side < 400 ? "4Y16" : side < 500 ? "4Y20" : "8Y20";
            return $"{bars} with Y8@200 links";
        }

        #endregion

        #region Beam Generation

        private async Task<List<BeamElement>> GenerateBeamsAsync(
            List<ColumnElement> columns,
            List<StructuralLevel> levels,
            ArchitecturalAnalysis analysis,
            StructuralGenerationOptions options)
        {
            var beams = new List<BeamElement>();
            var columnsByLevel = columns.GroupBy(c => c.BaseLevel);
            int mark = 1;

            foreach (var levelGroup in columnsByLevel)
            {
                var cols = levelGroup.ToList();
                var level = levels.FirstOrDefault(l => l.Name == levelGroup.Key);
                if (level == null) continue;

                // Group by Y for X-beams
                var byY = cols.GroupBy(c => Math.Round(c.CenterPoint.Y / 100) * 100);
                foreach (var row in byY)
                {
                    var sorted = row.OrderBy(c => c.CenterPoint.X).ToList();
                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        var span = sorted[i + 1].CenterPoint.X - sorted[i].CenterPoint.X;
                        var size = DetermineBeamSize(span, analysis, options);
                        beams.Add(new BeamElement
                        {
                            Id = Guid.NewGuid().ToString(),
                            Mark = $"B{mark++}",
                            Material = options.FramingMaterial.ToString(),
                            SectionSize = size,
                            StartPoint = sorted[i].CenterPoint,
                            EndPoint = sorted[i + 1].CenterPoint,
                            Level = level.Name,
                            Elevation = level.Elevation,
                            Span = span,
                            Reinforcement = CalculateBeamReinforcement(size, span)
                        });
                    }
                }

                // Group by X for Y-beams
                var byX = cols.GroupBy(c => Math.Round(c.CenterPoint.X / 100) * 100);
                foreach (var row in byX)
                {
                    var sorted = row.OrderBy(c => c.CenterPoint.Y).ToList();
                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        var span = sorted[i + 1].CenterPoint.Y - sorted[i].CenterPoint.Y;
                        var size = DetermineBeamSize(span, analysis, options);
                        beams.Add(new BeamElement
                        {
                            Id = Guid.NewGuid().ToString(),
                            Mark = $"B{mark++}",
                            Material = options.FramingMaterial.ToString(),
                            SectionSize = size,
                            StartPoint = sorted[i].CenterPoint,
                            EndPoint = sorted[i + 1].CenterPoint,
                            Level = level.Name,
                            Elevation = level.Elevation,
                            Span = span,
                            Reinforcement = CalculateBeamReinforcement(size, span)
                        });
                    }
                }
            }

            return await Task.FromResult(beams);
        }

        private string DetermineBeamSize(double span, ArchitecturalAnalysis analysis, StructuralGenerationOptions options)
        {
            var ratio = options.FramingMaterial == FramingMaterial.StructuralSteel ? 18.0 : 12.0;
            var depth = Math.Ceiling(span / ratio / 50) * 50;
            depth = Math.Clamp(depth, 300, 900);
            var width = Math.Ceiling(depth * 0.4 / 50) * 50;
            width = Math.Clamp(width, 200, 500);

            if (options.FramingMaterial == FramingMaterial.StructuralSteel)
            {
                if (depth < 400) return "UB 305x165x40";
                if (depth < 500) return "UB 406x178x54";
                if (depth < 600) return "UB 457x191x67";
                return "UB 533x210x92";
            }
            return $"{width}x{depth}";
        }

        private string CalculateBeamReinforcement(string size, double span)
        {
            var parts = size.Split('x');
            if (parts.Length < 2) return "2Y16T, 3Y20B, Y8@200";
            var depth = double.TryParse(parts[1], out var d) ? d : 400;
            var numBars = Math.Max(2, Math.Min(6, (int)(span / 1500)));
            return $"2Y16T, {numBars}Y20B, Y8@200";
        }

        #endregion

        #region Slab Generation

        private async Task<List<SlabElement>> GenerateSlabsAsync(
            ArchitecturalModel archModel,
            List<StructuralLevel> levels,
            StructuralGenerationOptions options)
        {
            var slabs = new List<SlabElement>();
            var floorLevels = levels.Where(l => l.LevelType == LevelType.Floor || l.LevelType == LevelType.Ground).ToList();
            int mark = 1;

            foreach (var level in floorLevels)
            {
                var thickness = DetermineSlabThickness(6000, options);
                slabs.Add(new SlabElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Mark = $"S{mark++}",
                    Level = level.Name,
                    Elevation = level.Elevation,
                    Thickness = thickness,
                    SlabType = options.SlabType?.ToString() ?? "FlatSlab",
                    Reinforcement = CalculateSlabReinforcement(thickness)
                });
            }

            return await Task.FromResult(slabs);
        }

        private double DetermineSlabThickness(double maxSpan, StructuralGenerationOptions options)
        {
            var ratio = options.SlabType switch
            {
                SlabType.FlatSlab => 30.0,
                SlabType.TwoWay => 35.0,
                SlabType.PostTensioned => 40.0,
                _ => 30.0
            };
            var t = Math.Ceiling(maxSpan / ratio / 25) * 25;
            return Math.Clamp(t, 150, 350);
        }

        private string CalculateSlabReinforcement(double thickness)
        {
            var bar = thickness < 200 ? 10 : 12;
            var spacing = thickness < 200 ? 200 : 150;
            return $"Y{bar}@{spacing} B.W.";
        }

        #endregion

        #region Foundation Generation

        private async Task<List<FoundationElement>> GenerateFoundationsAsync(
            List<ColumnElement> columns,
            ArchitecturalAnalysis analysis,
            StructuralGenerationOptions options)
        {
            var foundations = new List<FoundationElement>();
            var groundCols = columns.Where(c => c.BaseLevel == "Foundation" || c.BaseElevation < 0)
                .GroupBy(c => c.GridIntersection).Select(g => g.First()).ToList();

            if (!groundCols.Any())
                groundCols = columns.GroupBy(c => c.GridIntersection).Select(g => g.First()).ToList();

            int mark = 1;
            foreach (var col in groundCols)
            {
                var load = analysis.TotalLoads.GetValueOrDefault("TotalULS", 10) * 36 * analysis.NumberOfStoreys;
                var bearing = GetBearingCapacity(options.SoilCondition);
                var area = load / bearing * 1000000;
                var side = Math.Ceiling(Math.Sqrt(area) / 100) * 100;
                side = Math.Clamp(side, 1000, 3000);
                var depth = Math.Clamp(side * 0.3, 300, 600);

                foundations.Add(new FoundationElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Mark = $"F{mark++}",
                    FoundationType = side < 2000 ? FoundationType.PadFoundation : FoundationType.CombinedFooting,
                    ColumnMark = col.Mark,
                    GridIntersection = col.GridIntersection,
                    CenterPoint = col.CenterPoint,
                    Length = side,
                    Width = side,
                    Depth = depth,
                    DesignLoad = load,
                    BearingCapacity = bearing,
                    Reinforcement = CalculateFoundationReinforcement(side, depth)
                });
            }

            return await Task.FromResult(foundations);
        }

        private double GetBearingCapacity(SoilCondition condition)
        {
            return condition switch
            {
                SoilCondition.Rock => 1000,
                SoilCondition.DenseGravel => 400,
                SoilCondition.DenseSand => 300,
                SoilCondition.MediumSand => 150,
                SoilCondition.StiffClay => 200,
                SoilCondition.FirmClay => 100,
                _ => 150
            };
        }

        private string CalculateFoundationReinforcement(double side, double depth)
        {
            var bar = side < 1500 ? 12 : 16;
            var spacing = side < 1500 ? 200 : 150;
            return $"Y{bar}@{spacing} B.W.";
        }

        #endregion

        #region Connection Generation

        private async Task<List<ConnectionElement>> GenerateConnectionsAsync(
            StructuralGenerationResult model,
            StructuralGenerationOptions options)
        {
            var connections = new List<ConnectionElement>();
            int mark = 1;

            foreach (var beam in model.Beams)
            {
                var startCol = model.Columns.FirstOrDefault(c =>
                    Distance(c.CenterPoint, beam.StartPoint) < 100);
                var endCol = model.Columns.FirstOrDefault(c =>
                    Distance(c.CenterPoint, beam.EndPoint) < 100);

                if (startCol != null)
                {
                    connections.Add(new ConnectionElement
                    {
                        Mark = $"J{mark++}",
                        ConnectionType = DetermineConnectionType(options),
                        BeamMark = beam.Mark,
                        ColumnMark = startCol.Mark,
                        Position = beam.StartPoint,
                        Detail = GetConnectionDetail(options)
                    });
                }
                if (endCol != null)
                {
                    connections.Add(new ConnectionElement
                    {
                        Mark = $"J{mark++}",
                        ConnectionType = DetermineConnectionType(options),
                        BeamMark = beam.Mark,
                        ColumnMark = endCol.Mark,
                        Position = beam.EndPoint,
                        Detail = GetConnectionDetail(options)
                    });
                }
            }

            return await Task.FromResult(connections);
        }

        private ConnectionType DetermineConnectionType(StructuralGenerationOptions options)
        {
            return options.FramingMaterial == FramingMaterial.StructuralSteel
                ? (options.PreferMomentConnections ? ConnectionType.Moment : ConnectionType.Shear)
                : ConnectionType.Monolithic;
        }

        private string GetConnectionDetail(StructuralGenerationOptions options)
        {
            return options.FramingMaterial == FramingMaterial.StructuralSteel
                ? "Extended end plate with M20 Grade 8.8 bolts"
                : "RC monolithic - beam bars anchored into column";
        }

        private double Distance(Point3D p1, Point3D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        #endregion

        #region Drawing Generation

        private async Task<List<StructuralDrawing>> GenerateDrawingsAsync(
            StructuralGenerationResult model,
            StructuralGenerationOptions options)
        {
            var drawings = new List<StructuralDrawing>();

            // Foundation layout
            drawings.Add(new StructuralDrawing
            {
                DrawingNumber = "S-001",
                Title = "Foundation Layout Plan",
                DrawingType = DrawingType.FoundationPlan,
                Scale = "1:100",
                Elements = model.Foundations.Select(f => new DrawingElement
                {
                    Mark = f.Mark,
                    ElementType = "Foundation",
                    Size = $"{f.Length}x{f.Width}x{f.Depth}",
                    Grid = f.GridIntersection
                }).ToList()
            });

            // Framing plans by level
            var levelGroups = model.Beams.GroupBy(b => b.Level).ToList();
            int num = 2;
            foreach (var lg in levelGroups)
            {
                var levelCols = model.Columns.Where(c => c.BaseLevel == lg.Key).ToList();
                drawings.Add(new StructuralDrawing
                {
                    DrawingNumber = $"S-{num:D3}",
                    Title = $"{lg.Key} Framing Plan",
                    DrawingType = DrawingType.FramingPlan,
                    Scale = "1:100",
                    Elements = levelCols.Select(c => new DrawingElement
                    {
                        Mark = c.Mark, ElementType = "Column", Size = c.SectionSize, Grid = c.GridIntersection
                    }).Concat(lg.Select(b => new DrawingElement
                    {
                        Mark = b.Mark, ElementType = "Beam", Size = b.SectionSize, Span = b.Span
                    })).ToList()
                });
                num++;
            }

            // Schedules
            drawings.Add(GenerateColumnSchedule(model));
            drawings.Add(GenerateBeamSchedule(model));
            drawings.Add(GenerateFoundationSchedule(model));

            return await Task.FromResult(drawings);
        }

        private StructuralDrawing GenerateColumnSchedule(StructuralGenerationResult model)
        {
            return new StructuralDrawing
            {
                DrawingNumber = "S-SCH-01",
                Title = "Column Schedule",
                DrawingType = DrawingType.Schedule,
                ScheduleData = new ScheduleData
                {
                    Headers = new[] { "Mark", "Grid", "Size", "Reinforcement", "Base Level", "Top Level" },
                    Rows = model.Columns.GroupBy(c => c.Mark).Select(g => g.First())
                        .Select(c => new[] { c.Mark, c.GridIntersection, c.SectionSize, c.Reinforcement, c.BaseLevel, c.TopLevel })
                        .ToList()
                }
            };
        }

        private StructuralDrawing GenerateBeamSchedule(StructuralGenerationResult model)
        {
            return new StructuralDrawing
            {
                DrawingNumber = "S-SCH-02",
                Title = "Beam Schedule",
                DrawingType = DrawingType.Schedule,
                ScheduleData = new ScheduleData
                {
                    Headers = new[] { "Mark", "Size", "Span (mm)", "Reinforcement", "Level" },
                    Rows = model.Beams.Select(b => new[] { b.Mark, b.SectionSize, b.Span.ToString("F0"), b.Reinforcement, b.Level }).ToList()
                }
            };
        }

        private StructuralDrawing GenerateFoundationSchedule(StructuralGenerationResult model)
        {
            return new StructuralDrawing
            {
                DrawingNumber = "S-SCH-03",
                Title = "Foundation Schedule",
                DrawingType = DrawingType.Schedule,
                ScheduleData = new ScheduleData
                {
                    Headers = new[] { "Mark", "Type", "Size (LxWxD)", "Load (kN)", "Reinforcement", "Grid" },
                    Rows = model.Foundations.Select(f => new[]
                    {
                        f.Mark, f.FoundationType.ToString(), $"{f.Length}x{f.Width}x{f.Depth}",
                        f.DesignLoad.ToString("F0"), f.Reinforcement, f.GridIntersection
                    }).ToList()
                }
            };
        }

        #endregion

        #region Initialization

        private Dictionary<string, StructuralTemplate> InitializeTemplates()
        {
            return new Dictionary<string, StructuralTemplate>
            {
                ["RC_Frame"] = new StructuralTemplate { Name = "RC Frame", Material = FramingMaterial.ReinforcedConcrete },
                ["Steel_Frame"] = new StructuralTemplate { Name = "Steel Frame", Material = FramingMaterial.StructuralSteel },
                ["Timber_Frame"] = new StructuralTemplate { Name = "Timber Frame", Material = FramingMaterial.Timber }
            };
        }

        private Dictionary<string, LoadCase> InitializeLoadCases()
        {
            return new Dictionary<string, LoadCase>
            {
                ["DL"] = new LoadCase { Name = "Dead Load", Factor = 1.35 },
                ["LL"] = new LoadCase { Name = "Live Load", Factor = 1.5 },
                ["WL"] = new LoadCase { Name = "Wind Load", Factor = 1.5 }
            };
        }

        private Dictionary<string, MaterialProperties> InitializeMaterials()
        {
            return new Dictionary<string, MaterialProperties>
            {
                ["C30/37"] = new MaterialProperties { Name = "Concrete C30/37", Fck = 30, Density = 25 },
                ["S355"] = new MaterialProperties { Name = "Steel S355", Fy = 355, Density = 78.5 },
                ["C24"] = new MaterialProperties { Name = "Timber C24", Fm = 24, Density = 4.2 }
            };
        }

        #endregion
    }

    #region Data Models

    public class StructuralSettings
    {
        public string DesignCode { get; set; } = "Eurocode";
        public SoilCondition SoilCondition { get; set; } = SoilCondition.MediumSand;
    }

    public class StructuralGenerationOptions
    {
        public FramingMaterial FramingMaterial { get; set; } = FramingMaterial.ReinforcedConcrete;
        public SlabType? SlabType { get; set; } = Structural.SlabType.FlatSlab;
        public bool PreferMomentConnections { get; set; } = true;
        public SoilCondition SoilCondition { get; set; } = SoilCondition.MediumSand;
    }

    public class StructuralGenerationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string ProjectName { get; set; }
        public List<StructuralGrid> Grids { get; set; }
        public List<StructuralLevel> Levels { get; set; }
        public List<ColumnElement> Columns { get; set; }
        public List<BeamElement> Beams { get; set; }
        public List<SlabElement> Slabs { get; set; }
        public List<FoundationElement> Foundations { get; set; }
        public List<ConnectionElement> Connections { get; set; }
        public List<StructuralDrawing> Drawings { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class GenerationProgress { public string Stage { get; set; } public int Percentage { get; set; } }

    public class ArchitecturalModel
    {
        public string ProjectName { get; set; }
        public List<ArchLevel> Levels { get; set; }
        public List<ArchWall> Walls { get; set; }
        public List<ArchRoom> Rooms { get; set; }
    }
    public class ArchLevel { public string Id { get; set; } public string Name { get; set; } public double Elevation { get; set; } }
    public class ArchWall { public string Id { get; set; } public Point3D StartPoint { get; set; } public Point3D EndPoint { get; set; } public double Thickness { get; set; } public bool IsExterior { get; set; } public bool IsLoadBearing { get; set; } }
    public class ArchRoom { public string Id { get; set; } public string Name { get; set; } public double Width { get; set; } public double Length { get; set; } }

    public class ArchitecturalAnalysis
    {
        public BuildingFootprint Footprint { get; set; }
        public int NumberOfStoreys { get; set; }
        public List<LoadBearingWall> LoadBearingWalls { get; set; }
        public List<FloorSpan> FloorSpans { get; set; }
        public BuildingType BuildingType { get; set; }
        public Dictionary<string, double> DeadLoads { get; set; }
        public Dictionary<string, double> LiveLoads { get; set; }
        public Dictionary<string, double> TotalLoads { get; set; }
    }
    public class BuildingFootprint { public double MinX { get; set; } public double MaxX { get; set; } public double MinY { get; set; } public double MaxY { get; set; } public double Length { get; set; } public double Width { get; set; } }
    public class LoadBearingWall { public string WallId { get; set; } public Point3D StartPoint { get; set; } public Point3D EndPoint { get; set; } public double Thickness { get; set; } }
    public class FloorSpan { public string RoomId { get; set; } public double SpanX { get; set; } public double SpanY { get; set; } public double MaxSpan { get; set; } }

    public class StructuralGrid { public string Name { get; set; } public GridDirection Direction { get; set; } public double Position { get; set; } public Point3D StartPoint { get; set; } public Point3D EndPoint { get; set; } }
    public class StructuralLevel { public string Id { get; set; } public string Name { get; set; } public double Elevation { get; set; } public LevelType LevelType { get; set; } }
    public class ColumnElement { public string Id { get; set; } public string Mark { get; set; } public string GridIntersection { get; set; } public string Material { get; set; } public string SectionSize { get; set; } public Point3D CenterPoint { get; set; } public string BaseLevel { get; set; } public string TopLevel { get; set; } public double BaseElevation { get; set; } public double TopElevation { get; set; } public string Reinforcement { get; set; } }
    public class BeamElement { public string Id { get; set; } public string Mark { get; set; } public string Material { get; set; } public string SectionSize { get; set; } public Point3D StartPoint { get; set; } public Point3D EndPoint { get; set; } public string Level { get; set; } public double Elevation { get; set; } public double Span { get; set; } public string Reinforcement { get; set; } }
    public class SlabElement { public string Id { get; set; } public string Mark { get; set; } public string Level { get; set; } public double Elevation { get; set; } public double Thickness { get; set; } public string SlabType { get; set; } public string Reinforcement { get; set; } }
    public class FoundationElement { public string Id { get; set; } public string Mark { get; set; } public FoundationType FoundationType { get; set; } public string ColumnMark { get; set; } public string GridIntersection { get; set; } public Point3D CenterPoint { get; set; } public double Length { get; set; } public double Width { get; set; } public double Depth { get; set; } public double DesignLoad { get; set; } public double BearingCapacity { get; set; } public string Reinforcement { get; set; } }
    public class ConnectionElement { public string Mark { get; set; } public ConnectionType ConnectionType { get; set; } public string BeamMark { get; set; } public string ColumnMark { get; set; } public Point3D Position { get; set; } public string Detail { get; set; } }

    public class StructuralDrawing { public string DrawingNumber { get; set; } public string Title { get; set; } public DrawingType DrawingType { get; set; } public string Scale { get; set; } public List<DrawingElement> Elements { get; set; } public ScheduleData ScheduleData { get; set; } }
    public class DrawingElement { public string Mark { get; set; } public string ElementType { get; set; } public string Size { get; set; } public string Grid { get; set; } public double Span { get; set; } }
    public class ScheduleData { public string[] Headers { get; set; } public List<string[]> Rows { get; set; } }

    public class StructuralTemplate { public string Name { get; set; } public FramingMaterial Material { get; set; } }
    public class LoadCase { public string Name { get; set; } public double Factor { get; set; } }
    public class MaterialProperties { public string Name { get; set; } public double Fck { get; set; } public double Fy { get; set; } public double Fm { get; set; } public double Density { get; set; } }

    public enum GridDirection { X, Y }
    public enum LevelType { Foundation, Ground, Floor, Roof }
    public enum FramingMaterial { ReinforcedConcrete, StructuralSteel, Timber, Composite }
    public enum SlabType { FlatSlab, TwoWay, OneWay, Ribbed, PostTensioned }
    public enum FoundationType { PadFoundation, StripFoundation, CombinedFooting, RaftFoundation, PileFoundation }
    public enum ConnectionType { Monolithic, Moment, Shear, Pinned }
    public enum BuildingType { Residential, LowRise, MidRise, HighRise }
    public enum SoilCondition { Rock, DenseGravel, DenseSand, MediumSand, StiffClay, FirmClay, SoftClay }
    public enum DrawingType { FoundationPlan, FramingPlan, Section, Detail, Schedule }

    #endregion
}
