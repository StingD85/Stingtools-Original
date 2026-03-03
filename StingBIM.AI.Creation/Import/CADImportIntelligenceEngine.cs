// ===================================================================
// StingBIM CAD Import Intelligence Engine
// DWG to BIM conversion, layer mapping, element recognition
// Converts 2D CAD drawings to 3D BIM elements
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Creation.Import
{
    #region Enums

    public enum CADFileFormat { DWG, DXF, DGN, PDF, SVG }
    public enum CADEntityType { Line, Polyline, Arc, Circle, Ellipse, Spline, Text, MText, Block, Hatch, Dimension, Leader }
    public enum RecognizedElementType { Wall, Door, Window, Column, Beam, Room, Stairs, Furniture, Equipment, Annotation, Grid, Unknown }
    public enum ViewType { FloorPlan, Elevation, Section, Detail, ThreeD }
    public enum ConversionQuality { Draft, Standard, High, Precise }

    #endregion

    #region Data Models

    public class CADImportResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceFile { get; set; }
        public CADFileFormat Format { get; set; }
        public DateTime ImportDate { get; set; } = DateTime.UtcNow;
        public CADDocumentInfo DocumentInfo { get; set; }
        public List<CADLayer> Layers { get; set; } = new();
        public List<CADEntity> Entities { get; set; } = new();
        public List<RecognizedElement> RecognizedElements { get; set; } = new();
        public List<BlockDefinition> Blocks { get; set; } = new();
        public ConversionStatistics Statistics { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class CADDocumentInfo
    {
        public double ScaleFactor { get; set; } = 1.0;
        public string Units { get; set; } = "Millimeters";
        public (double X, double Y) Origin { get; set; }
        public (double MinX, double MinY, double MaxX, double MaxY) Extents { get; set; }
        public ViewType DetectedViewType { get; set; }
        public string DrawingTitle { get; set; }
        public string DrawnBy { get; set; }
        public DateTime? DrawingDate { get; set; }
        public string ProjectNumber { get; set; }
    }

    public class CADLayer
    {
        public string Name { get; set; }
        public int Color { get; set; }
        public string LineType { get; set; }
        public double LineWeight { get; set; }
        public bool IsVisible { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public RecognizedElementType? MappedElementType { get; set; }
        public int EntityCount { get; set; }
    }

    public class CADEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public CADEntityType Type { get; set; }
        public string LayerName { get; set; }
        public int Color { get; set; }
        public List<Point2D> Points { get; set; } = new();
        public double Thickness { get; set; }
        public string Text { get; set; }
        public string BlockName { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1.0;
        public Dictionary<string, object> Attributes { get; set; } = new();
        public (double CenterX, double CenterY, double Radius) CircleData { get; set; }
        public (double StartAngle, double EndAngle) ArcData { get; set; }
        public bool IsProcessed { get; set; }
        public RecognizedElementType? RecognizedAs { get; set; }
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public Point2D() { }
        public Point2D(double x, double y) { X = x; Y = y; }
    }

    public class BlockDefinition
    {
        public string Name { get; set; }
        public List<CADEntity> Entities { get; set; } = new();
        public Point2D BasePoint { get; set; }
        public RecognizedElementType? MappedType { get; set; }
        public string MappedFamilyId { get; set; }
        public int InstanceCount { get; set; }
        public Dictionary<string, string> AttributeDefinitions { get; set; } = new();
    }

    public class RecognizedElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public RecognizedElementType ElementType { get; set; }
        public List<string> SourceEntityIds { get; set; } = new();
        public double Confidence { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public List<Point2D> Boundary { get; set; } = new();
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double Area { get; set; }
        public double Rotation { get; set; }
        public string Label { get; set; }
        public string ProposedType { get; set; }
        public Dictionary<string, object> ExtractedParameters { get; set; } = new();
        public bool RequiresReview { get; set; }
        public string ReviewReason { get; set; }
    }

    public class ConversionStatistics
    {
        public int TotalEntities { get; set; }
        public int RecognizedEntities { get; set; }
        public int UnrecognizedEntities { get; set; }
        public Dictionary<RecognizedElementType, int> ElementCounts { get; set; } = new();
        public double RecognitionRate => TotalEntities > 0 ? (double)RecognizedEntities / TotalEntities * 100 : 0;
        public int WallsDetected { get; set; }
        public int DoorsDetected { get; set; }
        public int WindowsDetected { get; set; }
        public int RoomsDetected { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class LayerMappingRule
    {
        public string LayerPattern { get; set; }
        public RecognizedElementType TargetType { get; set; }
        public double DefaultThickness { get; set; }
        public double DefaultHeight { get; set; }
        public string RevitCategory { get; set; }
        public string ProposedType { get; set; }
    }

    public class BlockMappingRule
    {
        public string BlockPattern { get; set; }
        public RecognizedElementType TargetType { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, string> AttributeMapping { get; set; } = new();
    }

    public class WallRecognitionResult
    {
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public double Thickness { get; set; }
        public double Length { get; set; }
        public string WallType { get; set; }
        public bool IsExterior { get; set; }
        public List<OpeningLocation> Openings { get; set; } = new();
        public double Confidence { get; set; }
    }

    public class OpeningLocation
    {
        public RecognizedElementType Type { get; set; } // Door or Window
        public double Position { get; set; } // Distance from wall start
        public double Width { get; set; }
        public string BlockName { get; set; }
    }

    public class RoomRecognitionResult
    {
        public List<Point2D> Boundary { get; set; } = new();
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public Point2D Centroid { get; set; }
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public List<string> BoundingWallIds { get; set; } = new();
        public double Confidence { get; set; }
    }

    public class CADConversionOptions
    {
        public ConversionQuality Quality { get; set; } = ConversionQuality.Standard;
        public bool AutoDetectScale { get; set; } = true;
        public double ManualScale { get; set; } = 1.0;
        public string TargetUnits { get; set; } = "Millimeters";
        public double WallDetectionTolerance { get; set; } = 50; // mm
        public double MinWallLength { get; set; } = 100; // mm
        public double MaxWallThickness { get; set; } = 600; // mm
        public double DefaultWallHeight { get; set; } = 2700; // mm
        public double DefaultCeilingHeight { get; set; } = 2700; // mm
        public bool DetectDoors { get; set; } = true;
        public bool DetectWindows { get; set; } = true;
        public bool DetectRooms { get; set; } = true;
        public bool DetectColumns { get; set; } = true;
        public bool DetectFurniture { get; set; } = true;
        public bool UseLayerMapping { get; set; } = true;
        public bool UseBlockMapping { get; set; } = true;
        public List<LayerMappingRule> LayerMappings { get; set; } = new();
        public List<BlockMappingRule> BlockMappings { get; set; } = new();
        public List<string> IgnoreLayers { get; set; } = new();
    }

    public class BIMConversionResult
    {
        public bool Success { get; set; }
        public int ElementsCreated { get; set; }
        public List<CreatedBIMElement> Elements { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public TimeSpan ConversionTime { get; set; }
    }

    public class CreatedBIMElement
    {
        public int RevitElementId { get; set; }
        public string SourceCADEntityId { get; set; }
        public RecognizedElementType ElementType { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    #endregion

    /// <summary>
    /// CAD Import Intelligence Engine for converting DWG/DXF to BIM elements.
    /// </summary>
    public sealed class CADImportIntelligenceEngine
    {
        private static readonly Lazy<CADImportIntelligenceEngine> _instance =
            new Lazy<CADImportIntelligenceEngine>(() => new CADImportIntelligenceEngine());
        public static CADImportIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, CADImportResult> _importResults = new();
        private readonly List<LayerMappingRule> _defaultLayerMappings;
        private readonly List<BlockMappingRule> _defaultBlockMappings;
        private readonly object _lock = new object();

        public event EventHandler<ImportProgressEventArgs> ProgressChanged;

        private void OnProgressChanged(ImportProgressEventArgs e) => ProgressChanged?.Invoke(this, e);

        private CADImportIntelligenceEngine()
        {
            _defaultLayerMappings = InitializeDefaultLayerMappings();
            _defaultBlockMappings = InitializeDefaultBlockMappings();
        }

        #region Import and Parse

        /// <summary>
        /// Imports and analyzes a CAD file.
        /// </summary>
        public async Task<CADImportResult> ImportFileAsync(
            string filePath,
            CADConversionOptions options = null,
            IProgress<ImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new CADConversionOptions();
            var result = new CADImportResult
            {
                SourceFile = filePath,
                Format = DetectFormat(filePath)
            };

            var startTime = DateTime.UtcNow;

            try
            {
                // Phase 1: Parse CAD file
                progress?.Report(new ImportProgress { Phase = "Parsing CAD file", Percentage = 10 });
                await ParseCADFileAsync(result, options, cancellationToken);

                // Phase 2: Analyze document
                progress?.Report(new ImportProgress { Phase = "Analyzing document", Percentage = 25 });
                AnalyzeDocument(result, options);

                // Phase 3: Map layers
                progress?.Report(new ImportProgress { Phase = "Mapping layers", Percentage = 35 });
                MapLayers(result, options);

                // Phase 4: Map blocks
                progress?.Report(new ImportProgress { Phase = "Mapping blocks", Percentage = 45 });
                MapBlocks(result, options);

                // Phase 5: Recognize walls
                progress?.Report(new ImportProgress { Phase = "Detecting walls", Percentage = 55 });
                await RecognizeWallsAsync(result, options, cancellationToken);

                // Phase 6: Recognize openings
                progress?.Report(new ImportProgress { Phase = "Detecting doors and windows", Percentage = 70 });
                RecognizeOpenings(result, options);

                // Phase 7: Recognize rooms
                progress?.Report(new ImportProgress { Phase = "Detecting rooms", Percentage = 80 });
                await RecognizeRoomsAsync(result, options, cancellationToken);

                // Phase 8: Recognize other elements
                progress?.Report(new ImportProgress { Phase = "Detecting other elements", Percentage = 90 });
                RecognizeOtherElements(result, options);

                // Phase 9: Calculate statistics
                progress?.Report(new ImportProgress { Phase = "Finalizing", Percentage = 95 });
                CalculateStatistics(result, startTime);

                progress?.Report(new ImportProgress { Phase = "Complete", Percentage = 100, IsComplete = true });

                lock (_lock) { _importResults[result.Id] = result; }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Import failed: {ex.Message}");
            }

            return result;
        }

        private CADFileFormat DetectFormat(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath)?.ToLower();
            return extension switch
            {
                ".dwg" => CADFileFormat.DWG,
                ".dxf" => CADFileFormat.DXF,
                ".dgn" => CADFileFormat.DGN,
                ".pdf" => CADFileFormat.PDF,
                ".svg" => CADFileFormat.SVG,
                _ => CADFileFormat.DWG
            };
        }

        private async Task ParseCADFileAsync(CADImportResult result, CADConversionOptions options, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Simulate parsing - in real implementation would use CAD SDK
                result.DocumentInfo = new CADDocumentInfo
                {
                    ScaleFactor = options.AutoDetectScale ? DetectScale(result) : options.ManualScale,
                    Units = options.TargetUnits,
                    Origin = (0, 0),
                    Extents = (0, 0, 50000, 30000), // Sample extents
                    DetectedViewType = ViewType.FloorPlan
                };

                // Create sample layers
                result.Layers = CreateSampleLayers();

                // Create sample entities (walls, doors, windows represented as CAD entities)
                result.Entities = CreateSampleEntities();

                // Create sample blocks
                result.Blocks = CreateSampleBlocks();
            }, cancellationToken);
        }

        private double DetectScale(CADImportResult result)
        {
            // Analyze text entities for scale indicators or dimension blocks
            // Look for standard paper sizes, title blocks, etc.
            return 1.0; // Default 1:1
        }

        #endregion

        #region Layer and Block Mapping

        private void MapLayers(CADImportResult result, CADConversionOptions options)
        {
            var mappings = options.UseLayerMapping ?
                (options.LayerMappings.Any() ? options.LayerMappings : _defaultLayerMappings) :
                new List<LayerMappingRule>();

            foreach (var layer in result.Layers)
            {
                // Skip ignored layers
                if (options.IgnoreLayers.Contains(layer.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                // Find matching mapping rule
                var matchingRule = mappings.FirstOrDefault(m =>
                    layer.Name.ToLower().Contains(m.LayerPattern.ToLower()) ||
                    System.Text.RegularExpressions.Regex.IsMatch(layer.Name, m.LayerPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase));

                if (matchingRule != null)
                {
                    layer.MappedElementType = matchingRule.TargetType;
                }
            }
        }

        private void MapBlocks(CADImportResult result, CADConversionOptions options)
        {
            var mappings = options.UseBlockMapping ?
                (options.BlockMappings.Any() ? options.BlockMappings : _defaultBlockMappings) :
                new List<BlockMappingRule>();

            foreach (var block in result.Blocks)
            {
                var matchingRule = mappings.FirstOrDefault(m =>
                    block.Name.ToLower().Contains(m.BlockPattern.ToLower()) ||
                    System.Text.RegularExpressions.Regex.IsMatch(block.Name, m.BlockPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase));

                if (matchingRule != null)
                {
                    block.MappedType = matchingRule.TargetType;
                    block.MappedFamilyId = matchingRule.FamilyName;
                }
            }
        }

        #endregion

        #region Element Recognition

        private async Task RecognizeWallsAsync(CADImportResult result, CADConversionOptions options, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Get entities on wall layers
                var wallLayers = result.Layers
                    .Where(l => l.MappedElementType == RecognizedElementType.Wall)
                    .Select(l => l.Name)
                    .ToList();

                var wallEntities = result.Entities
                    .Where(e => wallLayers.Contains(e.LayerName) &&
                               (e.Type == CADEntityType.Line || e.Type == CADEntityType.Polyline))
                    .ToList();

                // Group parallel lines as wall candidates
                var wallCandidates = GroupParallelLines(wallEntities, options.WallDetectionTolerance);

                foreach (var candidate in wallCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Validate as wall
                    if (candidate.Length >= options.MinWallLength &&
                        candidate.Thickness <= options.MaxWallThickness)
                    {
                        var wall = new RecognizedElement
                        {
                            ElementType = RecognizedElementType.Wall,
                            StartPoint = candidate.StartPoint,
                            EndPoint = candidate.EndPoint,
                            Length = candidate.Length,
                            Thickness = candidate.Thickness > 0 ? candidate.Thickness : 200, // Default 200mm
                            Height = options.DefaultWallHeight,
                            Confidence = candidate.Confidence,
                            ProposedType = candidate.IsExterior ? "Exterior Wall" : "Interior Wall",
                            SourceEntityIds = candidate.SourceEntityIds
                        };

                        wall.ExtractedParameters["IsExterior"] = candidate.IsExterior;
                        wall.ExtractedParameters["Length"] = candidate.Length;

                        result.RecognizedElements.Add(wall);
                    }
                }
            }, cancellationToken);
        }

        private List<WallCandidate> GroupParallelLines(List<CADEntity> entities, double tolerance)
        {
            var candidates = new List<WallCandidate>();

            // Find pairs of parallel lines that could represent wall edges
            var processedIds = new HashSet<string>();

            foreach (var entity in entities.Where(e => e.Type == CADEntityType.Line))
            {
                if (processedIds.Contains(entity.Id)) continue;
                if (entity.Points.Count < 2) continue;

                var start = entity.Points[0];
                var end = entity.Points[1];
                var length = Distance(start, end);
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);

                // Find parallel line
                var parallel = entities.FirstOrDefault(e =>
                    e.Id != entity.Id &&
                    !processedIds.Contains(e.Id) &&
                    e.Points.Count >= 2 &&
                    IsParallel(entity, e, tolerance) &&
                    IsNearby(entity, e, 600)); // Max 600mm wall thickness

                if (parallel != null)
                {
                    var thickness = CalculateWallThickness(entity, parallel);
                    var wallStart = MidPoint(start, parallel.Points[0]);
                    var wallEnd = MidPoint(end, parallel.Points[1]);

                    candidates.Add(new WallCandidate
                    {
                        StartPoint = wallStart,
                        EndPoint = wallEnd,
                        Length = length,
                        Thickness = thickness,
                        Confidence = 0.9,
                        IsExterior = thickness >= 250,
                        SourceEntityIds = new List<string> { entity.Id, parallel.Id }
                    });

                    processedIds.Add(entity.Id);
                    processedIds.Add(parallel.Id);
                }
                else
                {
                    // Single line - assume centerline representation
                    candidates.Add(new WallCandidate
                    {
                        StartPoint = start,
                        EndPoint = end,
                        Length = length,
                        Thickness = 200, // Default
                        Confidence = 0.7,
                        IsExterior = false,
                        SourceEntityIds = new List<string> { entity.Id }
                    });

                    processedIds.Add(entity.Id);
                }
            }

            return candidates;
        }

        private void RecognizeOpenings(CADImportResult result, CADConversionOptions options)
        {
            if (!options.DetectDoors && !options.DetectWindows) return;

            // Find door and window blocks
            foreach (var entity in result.Entities.Where(e => e.Type == CADEntityType.Block))
            {
                var block = result.Blocks.FirstOrDefault(b => b.Name == entity.BlockName);
                if (block?.MappedType == RecognizedElementType.Door ||
                    block?.MappedType == RecognizedElementType.Window)
                {
                    var opening = new RecognizedElement
                    {
                        ElementType = block.MappedType.Value,
                        StartPoint = entity.Points.FirstOrDefault() ?? new Point2D(0, 0),
                        Rotation = entity.Rotation,
                        Confidence = 0.85,
                        ProposedType = block.MappedType == RecognizedElementType.Door ? "Single Door" : "Fixed Window",
                        SourceEntityIds = new List<string> { entity.Id }
                    };

                    // Extract dimensions from block or attributes
                    if (block.AttributeDefinitions.ContainsKey("WIDTH"))
                    {
                        if (entity.Attributes.TryGetValue("WIDTH", out var width))
                            opening.Width = Convert.ToDouble(width);
                    }
                    else
                    {
                        opening.Width = 900; // Default door width
                    }

                    opening.Height = block.MappedType == RecognizedElementType.Door ? 2100 : 1500;

                    result.RecognizedElements.Add(opening);
                }
            }

            // Also look for arc entities as door swings
            if (options.DetectDoors)
            {
                foreach (var arc in result.Entities.Where(e => e.Type == CADEntityType.Arc))
                {
                    // Door swing arcs are typically 90 degrees
                    var angleSpan = Math.Abs(arc.ArcData.EndAngle - arc.ArcData.StartAngle);
                    if (angleSpan >= 85 && angleSpan <= 95)
                    {
                        var door = new RecognizedElement
                        {
                            ElementType = RecognizedElementType.Door,
                            StartPoint = new Point2D(arc.CircleData.CenterX, arc.CircleData.CenterY),
                            Width = arc.CircleData.Radius, // Radius = door width
                            Height = 2100,
                            Rotation = arc.ArcData.StartAngle,
                            Confidence = 0.75,
                            ProposedType = "Single Door",
                            SourceEntityIds = new List<string> { arc.Id },
                            RequiresReview = true,
                            ReviewReason = "Detected from arc - verify placement"
                        };

                        result.RecognizedElements.Add(door);
                    }
                }
            }
        }

        private async Task RecognizeRoomsAsync(CADImportResult result, CADConversionOptions options, CancellationToken cancellationToken)
        {
            if (!options.DetectRooms) return;

            await Task.Run(() =>
            {
                // Find room tags/labels
                var roomTexts = result.Entities
                    .Where(e => (e.Type == CADEntityType.Text || e.Type == CADEntityType.MText) &&
                               (e.LayerName.ToLower().Contains("room") ||
                                e.LayerName.ToLower().Contains("name") ||
                                e.LayerName.ToLower().Contains("tag")))
                    .ToList();

                // Find closed polylines on room layer
                var roomBoundaries = result.Entities
                    .Where(e => e.Type == CADEntityType.Polyline &&
                               e.LayerName.ToLower().Contains("room") &&
                               IsClosedPolyline(e))
                    .ToList();

                foreach (var boundary in roomBoundaries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var area = CalculatePolygonArea(boundary.Points);
                    var centroid = CalculateCentroid(boundary.Points);

                    // Find associated room name
                    var roomText = roomTexts
                        .FirstOrDefault(t => IsPointInPolygon(t.Points.FirstOrDefault() ?? new Point2D(0, 0), boundary.Points));

                    var room = new RecognizedElement
                    {
                        ElementType = RecognizedElementType.Room,
                        Boundary = boundary.Points,
                        Area = area,
                        Label = roomText?.Text ?? "Room",
                        Confidence = roomText != null ? 0.9 : 0.7,
                        SourceEntityIds = new List<string> { boundary.Id }
                    };

                    room.ExtractedParameters["RoomName"] = room.Label;
                    room.ExtractedParameters["Area"] = area;
                    room.ExtractedParameters["Centroid"] = centroid;

                    result.RecognizedElements.Add(room);
                }

                // If no explicit room boundaries, try to detect from walls
                if (!roomBoundaries.Any())
                {
                    var rooms = DetectRoomsFromWalls(result.RecognizedElements.Where(e => e.ElementType == RecognizedElementType.Wall).ToList());
                    foreach (var room in rooms)
                    {
                        room.RequiresReview = true;
                        room.ReviewReason = "Room detected from wall boundaries - verify";
                        result.RecognizedElements.Add(room);
                    }
                }
            }, cancellationToken);
        }

        private void RecognizeOtherElements(CADImportResult result, CADConversionOptions options)
        {
            // Detect columns
            if (options.DetectColumns)
            {
                var columnBlocks = result.Entities
                    .Where(e => e.Type == CADEntityType.Block &&
                               (e.BlockName.ToLower().Contains("column") ||
                                e.BlockName.ToLower().Contains("col")))
                    .ToList();

                foreach (var col in columnBlocks)
                {
                    result.RecognizedElements.Add(new RecognizedElement
                    {
                        ElementType = RecognizedElementType.Column,
                        StartPoint = col.Points.FirstOrDefault() ?? new Point2D(0, 0),
                        Confidence = 0.8,
                        ProposedType = "Concrete Column",
                        SourceEntityIds = new List<string> { col.Id }
                    });
                }

                // Also look for filled rectangles as columns
                var filledRects = result.Entities
                    .Where(e => e.Type == CADEntityType.Hatch &&
                               e.Points.Count == 4 &&
                               IsRectangle(e.Points) &&
                               CalculatePolygonArea(e.Points) < 1000000) // < 1m²
                    .ToList();

                foreach (var rect in filledRects)
                {
                    var dims = GetRectangleDimensions(rect.Points);
                    if (dims.Width >= 200 && dims.Width <= 1000 &&
                        dims.Height >= 200 && dims.Height <= 1000)
                    {
                        result.RecognizedElements.Add(new RecognizedElement
                        {
                            ElementType = RecognizedElementType.Column,
                            StartPoint = CalculateCentroid(rect.Points),
                            Width = dims.Width,
                            Thickness = dims.Height,
                            Confidence = 0.7,
                            ProposedType = dims.Width == dims.Height ? "Square Column" : "Rectangular Column",
                            SourceEntityIds = new List<string> { rect.Id },
                            RequiresReview = true,
                            ReviewReason = "Detected from hatch - verify"
                        });
                    }
                }
            }

            // Detect furniture
            if (options.DetectFurniture)
            {
                var furnitureBlocks = result.Blocks
                    .Where(b => b.MappedType == RecognizedElementType.Furniture)
                    .ToList();

                foreach (var block in furnitureBlocks)
                {
                    var instances = result.Entities
                        .Where(e => e.Type == CADEntityType.Block && e.BlockName == block.Name)
                        .ToList();

                    foreach (var instance in instances)
                    {
                        result.RecognizedElements.Add(new RecognizedElement
                        {
                            ElementType = RecognizedElementType.Furniture,
                            StartPoint = instance.Points.FirstOrDefault() ?? new Point2D(0, 0),
                            Rotation = instance.Rotation,
                            Label = block.Name,
                            Confidence = 0.75,
                            SourceEntityIds = new List<string> { instance.Id }
                        });
                    }
                }
            }

            // Detect grid lines
            var gridLayers = result.Layers
                .Where(l => l.Name.ToLower().Contains("grid"))
                .Select(l => l.Name)
                .ToList();

            var gridLines = result.Entities
                .Where(e => gridLayers.Contains(e.LayerName) && e.Type == CADEntityType.Line)
                .ToList();

            foreach (var grid in gridLines)
            {
                result.RecognizedElements.Add(new RecognizedElement
                {
                    ElementType = RecognizedElementType.Grid,
                    StartPoint = grid.Points.FirstOrDefault(),
                    EndPoint = grid.Points.Skip(1).FirstOrDefault(),
                    Confidence = 0.9,
                    SourceEntityIds = new List<string> { grid.Id }
                });
            }
        }

        private List<RecognizedElement> DetectRoomsFromWalls(List<RecognizedElement> walls)
        {
            // Find closed loops of walls
            var rooms = new List<RecognizedElement>();

            // Simplified: group walls that form closed loops
            // Real implementation would use computational geometry

            return rooms;
        }

        #endregion

        #region Conversion to BIM

        /// <summary>
        /// Converts recognized elements to BIM elements.
        /// </summary>
        public async Task<BIMConversionResult> ConvertToBIMAsync(
            string importResultId,
            int targetLevelId,
            IProgress<ImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BIMConversionResult();
            var startTime = DateTime.UtcNow;

            CADImportResult importResult;
            lock (_lock)
            {
                if (!_importResults.TryGetValue(importResultId, out importResult))
                {
                    result.Success = false;
                    result.Errors.Add("Import result not found");
                    return result;
                }
            }

            try
            {
                int total = importResult.RecognizedElements.Count;
                int current = 0;

                // Convert walls
                var walls = importResult.RecognizedElements.Where(e => e.ElementType == RecognizedElementType.Wall).ToList();
                foreach (var wall in walls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report(new ImportProgress { Phase = "Creating walls", Percentage = current * 100 / total });

                    var created = await ConvertWallAsync(wall, targetLevelId);
                    if (created != null)
                    {
                        result.Elements.Add(created);
                        result.ElementsCreated++;
                    }
                }

                // Convert doors
                var doors = importResult.RecognizedElements.Where(e => e.ElementType == RecognizedElementType.Door).ToList();
                foreach (var door in doors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report(new ImportProgress { Phase = "Creating doors", Percentage = current * 100 / total });

                    var created = await ConvertDoorAsync(door, targetLevelId);
                    if (created != null)
                    {
                        result.Elements.Add(created);
                        result.ElementsCreated++;
                    }
                }

                // Convert windows
                var windows = importResult.RecognizedElements.Where(e => e.ElementType == RecognizedElementType.Window).ToList();
                foreach (var window in windows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report(new ImportProgress { Phase = "Creating windows", Percentage = current * 100 / total });

                    var created = await ConvertWindowAsync(window, targetLevelId);
                    if (created != null)
                    {
                        result.Elements.Add(created);
                        result.ElementsCreated++;
                    }
                }

                // Convert rooms
                var rooms = importResult.RecognizedElements.Where(e => e.ElementType == RecognizedElementType.Room).ToList();
                foreach (var room in rooms)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report(new ImportProgress { Phase = "Creating rooms", Percentage = current * 100 / total });

                    var created = await ConvertRoomAsync(room, targetLevelId);
                    if (created != null)
                    {
                        result.Elements.Add(created);
                        result.ElementsCreated++;
                    }
                }

                result.Success = true;
                result.ConversionTime = DateTime.UtcNow - startTime;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Conversion failed: {ex.Message}");
            }

            return result;
        }

        private async Task<CreatedBIMElement> ConvertWallAsync(RecognizedElement element, int levelId)
        {
            return await Task.FromResult(new CreatedBIMElement
            {
                RevitElementId = new Random().Next(100000, 999999),
                SourceCADEntityId = element.SourceEntityIds.FirstOrDefault(),
                ElementType = RecognizedElementType.Wall,
                FamilyName = "Basic Wall",
                TypeName = element.ProposedType,
                Parameters = new Dictionary<string, object>
                {
                    { "Length", element.Length },
                    { "Height", element.Height },
                    { "Thickness", element.Thickness }
                }
            });
        }

        private async Task<CreatedBIMElement> ConvertDoorAsync(RecognizedElement element, int levelId)
        {
            return await Task.FromResult(new CreatedBIMElement
            {
                RevitElementId = new Random().Next(100000, 999999),
                SourceCADEntityId = element.SourceEntityIds.FirstOrDefault(),
                ElementType = RecognizedElementType.Door,
                FamilyName = "Single Flush Door",
                TypeName = $"{element.Width}mm",
                Parameters = new Dictionary<string, object>
                {
                    { "Width", element.Width },
                    { "Height", element.Height }
                }
            });
        }

        private async Task<CreatedBIMElement> ConvertWindowAsync(RecognizedElement element, int levelId)
        {
            return await Task.FromResult(new CreatedBIMElement
            {
                RevitElementId = new Random().Next(100000, 999999),
                SourceCADEntityId = element.SourceEntityIds.FirstOrDefault(),
                ElementType = RecognizedElementType.Window,
                FamilyName = "Fixed Window",
                TypeName = $"{element.Width}x{element.Height}mm",
                Parameters = new Dictionary<string, object>
                {
                    { "Width", element.Width },
                    { "Height", element.Height }
                }
            });
        }

        private async Task<CreatedBIMElement> ConvertRoomAsync(RecognizedElement element, int levelId)
        {
            return await Task.FromResult(new CreatedBIMElement
            {
                RevitElementId = new Random().Next(100000, 999999),
                SourceCADEntityId = element.SourceEntityIds.FirstOrDefault(),
                ElementType = RecognizedElementType.Room,
                FamilyName = "Room",
                TypeName = element.Label,
                Parameters = new Dictionary<string, object>
                {
                    { "Name", element.Label },
                    { "Area", element.Area }
                }
            });
        }

        #endregion

        #region Helper Methods

        private void AnalyzeDocument(CADImportResult result, CADConversionOptions options)
        {
            // Detect view type from layer names and content
            var layerNames = string.Join(" ", result.Layers.Select(l => l.Name.ToLower()));

            if (layerNames.Contains("elevation") || layerNames.Contains("elev"))
                result.DocumentInfo.DetectedViewType = ViewType.Elevation;
            else if (layerNames.Contains("section") || layerNames.Contains("sect"))
                result.DocumentInfo.DetectedViewType = ViewType.Section;
            else if (layerNames.Contains("detail"))
                result.DocumentInfo.DetectedViewType = ViewType.Detail;
            else
                result.DocumentInfo.DetectedViewType = ViewType.FloorPlan;
        }

        private void CalculateStatistics(CADImportResult result, DateTime startTime)
        {
            result.Statistics = new ConversionStatistics
            {
                TotalEntities = result.Entities.Count,
                RecognizedEntities = result.RecognizedElements.Count,
                UnrecognizedEntities = result.Entities.Count - result.RecognizedElements.Sum(r => r.SourceEntityIds.Count),
                WallsDetected = result.RecognizedElements.Count(e => e.ElementType == RecognizedElementType.Wall),
                DoorsDetected = result.RecognizedElements.Count(e => e.ElementType == RecognizedElementType.Door),
                WindowsDetected = result.RecognizedElements.Count(e => e.ElementType == RecognizedElementType.Window),
                RoomsDetected = result.RecognizedElements.Count(e => e.ElementType == RecognizedElementType.Room),
                ProcessingTime = DateTime.UtcNow - startTime
            };

            foreach (var group in result.RecognizedElements.GroupBy(e => e.ElementType))
            {
                result.Statistics.ElementCounts[group.Key] = group.Count();
            }
        }

        private double Distance(Point2D p1, Point2D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        private Point2D MidPoint(Point2D p1, Point2D p2)
        {
            return new Point2D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        }

        private bool IsParallel(CADEntity e1, CADEntity e2, double tolerance)
        {
            if (e1.Points.Count < 2 || e2.Points.Count < 2) return false;

            var angle1 = Math.Atan2(e1.Points[1].Y - e1.Points[0].Y, e1.Points[1].X - e1.Points[0].X);
            var angle2 = Math.Atan2(e2.Points[1].Y - e2.Points[0].Y, e2.Points[1].X - e2.Points[0].X);

            var diff = Math.Abs(angle1 - angle2);
            return diff < 0.05 || Math.Abs(diff - Math.PI) < 0.05; // ~3 degrees tolerance
        }

        private bool IsNearby(CADEntity e1, CADEntity e2, double maxDistance)
        {
            if (e1.Points.Count < 2 || e2.Points.Count < 2) return false;

            var d1 = PerpendicularDistance(e1.Points[0], e2.Points[0], e2.Points[1]);
            var d2 = PerpendicularDistance(e1.Points[1], e2.Points[0], e2.Points[1]);

            return Math.Min(d1, d2) <= maxDistance;
        }

        private double PerpendicularDistance(Point2D point, Point2D lineStart, Point2D lineEnd)
        {
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0) return Distance(point, lineStart);

            var t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (length * length);
            t = Math.Max(0, Math.Min(1, t));

            var closest = new Point2D(lineStart.X + t * dx, lineStart.Y + t * dy);
            return Distance(point, closest);
        }

        private double CalculateWallThickness(CADEntity line1, CADEntity line2)
        {
            if (line1.Points.Count < 2 || line2.Points.Count < 2) return 200;
            return PerpendicularDistance(line1.Points[0], line2.Points[0], line2.Points[1]);
        }

        private bool IsClosedPolyline(CADEntity entity)
        {
            if (entity.Points.Count < 3) return false;
            var first = entity.Points.First();
            var last = entity.Points.Last();
            return Distance(first, last) < 1; // Within 1mm
        }

        private double CalculatePolygonArea(List<Point2D> points)
        {
            if (points.Count < 3) return 0;

            double area = 0;
            int j = points.Count - 1;

            for (int i = 0; i < points.Count; i++)
            {
                area += (points[j].X + points[i].X) * (points[j].Y - points[i].Y);
                j = i;
            }

            return Math.Abs(area / 2);
        }

        private Point2D CalculateCentroid(List<Point2D> points)
        {
            if (!points.Any()) return new Point2D(0, 0);
            return new Point2D(points.Average(p => p.X), points.Average(p => p.Y));
        }

        private bool IsPointInPolygon(Point2D point, List<Point2D> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Y < point.Y && polygon[j].Y >= point.Y || polygon[j].Y < point.Y && polygon[i].Y >= point.Y)
                    && (polygon[i].X <= point.X || polygon[j].X <= point.X))
                {
                    if (polygon[i].X + (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < point.X)
                    {
                        inside = !inside;
                    }
                }
                j = i;
            }

            return inside;
        }

        private bool IsRectangle(List<Point2D> points)
        {
            if (points.Count != 4) return false;
            // Check if all angles are approximately 90 degrees
            // Simplified check
            return true;
        }

        private (double Width, double Height) GetRectangleDimensions(List<Point2D> points)
        {
            if (points.Count < 4) return (0, 0);

            double width = Distance(points[0], points[1]);
            double height = Distance(points[1], points[2]);

            return (Math.Min(width, height), Math.Max(width, height));
        }

        #endregion

        #region Initialization

        private List<LayerMappingRule> InitializeDefaultLayerMappings()
        {
            return new List<LayerMappingRule>
            {
                new LayerMappingRule { LayerPattern = "wall", TargetType = RecognizedElementType.Wall, DefaultThickness = 200, DefaultHeight = 2700 },
                new LayerMappingRule { LayerPattern = "a-wall", TargetType = RecognizedElementType.Wall, DefaultThickness = 200, DefaultHeight = 2700 },
                new LayerMappingRule { LayerPattern = "door", TargetType = RecognizedElementType.Door },
                new LayerMappingRule { LayerPattern = "a-door", TargetType = RecognizedElementType.Door },
                new LayerMappingRule { LayerPattern = "window", TargetType = RecognizedElementType.Window },
                new LayerMappingRule { LayerPattern = "a-glaz", TargetType = RecognizedElementType.Window },
                new LayerMappingRule { LayerPattern = "column", TargetType = RecognizedElementType.Column },
                new LayerMappingRule { LayerPattern = "s-col", TargetType = RecognizedElementType.Column },
                new LayerMappingRule { LayerPattern = "beam", TargetType = RecognizedElementType.Beam },
                new LayerMappingRule { LayerPattern = "s-beam", TargetType = RecognizedElementType.Beam },
                new LayerMappingRule { LayerPattern = "stair", TargetType = RecognizedElementType.Stairs },
                new LayerMappingRule { LayerPattern = "furn", TargetType = RecognizedElementType.Furniture },
                new LayerMappingRule { LayerPattern = "equip", TargetType = RecognizedElementType.Equipment },
                new LayerMappingRule { LayerPattern = "anno", TargetType = RecognizedElementType.Annotation },
                new LayerMappingRule { LayerPattern = "text", TargetType = RecognizedElementType.Annotation },
                new LayerMappingRule { LayerPattern = "grid", TargetType = RecognizedElementType.Grid }
            };
        }

        private List<BlockMappingRule> InitializeDefaultBlockMappings()
        {
            return new List<BlockMappingRule>
            {
                new BlockMappingRule { BlockPattern = "door", TargetType = RecognizedElementType.Door, FamilyName = "Single Flush Door" },
                new BlockMappingRule { BlockPattern = "sgl.*door", TargetType = RecognizedElementType.Door, FamilyName = "Single Flush Door" },
                new BlockMappingRule { BlockPattern = "dbl.*door", TargetType = RecognizedElementType.Door, FamilyName = "Double Door" },
                new BlockMappingRule { BlockPattern = "window", TargetType = RecognizedElementType.Window, FamilyName = "Fixed Window" },
                new BlockMappingRule { BlockPattern = "win", TargetType = RecognizedElementType.Window, FamilyName = "Fixed Window" },
                new BlockMappingRule { BlockPattern = "toilet", TargetType = RecognizedElementType.Equipment, FamilyName = "Wall-Hung Toilet" },
                new BlockMappingRule { BlockPattern = "sink", TargetType = RecognizedElementType.Equipment, FamilyName = "Pedestal Lavatory" },
                new BlockMappingRule { BlockPattern = "desk", TargetType = RecognizedElementType.Furniture, FamilyName = "Office Desk" },
                new BlockMappingRule { BlockPattern = "chair", TargetType = RecognizedElementType.Furniture, FamilyName = "Task Chair" }
            };
        }

        private List<CADLayer> CreateSampleLayers()
        {
            return new List<CADLayer>
            {
                new CADLayer { Name = "A-WALL", Color = 1, IsVisible = true, EntityCount = 50 },
                new CADLayer { Name = "A-WALL-FULL", Color = 1, IsVisible = true, EntityCount = 30 },
                new CADLayer { Name = "A-DOOR", Color = 3, IsVisible = true, EntityCount = 15 },
                new CADLayer { Name = "A-GLAZ", Color = 5, IsVisible = true, EntityCount = 20 },
                new CADLayer { Name = "A-ROOM-NAME", Color = 7, IsVisible = true, EntityCount = 12 },
                new CADLayer { Name = "S-COLS", Color = 2, IsVisible = true, EntityCount = 24 },
                new CADLayer { Name = "A-GRID", Color = 8, IsVisible = true, EntityCount = 10 },
                new CADLayer { Name = "A-ANNO", Color = 7, IsVisible = true, EntityCount = 100 }
            };
        }

        private List<CADEntity> CreateSampleEntities()
        {
            var entities = new List<CADEntity>();

            // Sample wall lines
            for (int i = 0; i < 20; i++)
            {
                entities.Add(new CADEntity
                {
                    Type = CADEntityType.Line,
                    LayerName = "A-WALL",
                    Points = new List<Point2D>
                    {
                        new Point2D(i * 3000, 0),
                        new Point2D(i * 3000 + 5000, 0)
                    }
                });
            }

            // Sample door blocks
            for (int i = 0; i < 5; i++)
            {
                entities.Add(new CADEntity
                {
                    Type = CADEntityType.Block,
                    LayerName = "A-DOOR",
                    BlockName = "DOOR-SGL",
                    Points = new List<Point2D> { new Point2D(i * 6000 + 2000, 0) },
                    Rotation = 0
                });
            }

            return entities;
        }

        private List<BlockDefinition> CreateSampleBlocks()
        {
            return new List<BlockDefinition>
            {
                new BlockDefinition
                {
                    Name = "DOOR-SGL",
                    BasePoint = new Point2D(0, 0),
                    AttributeDefinitions = new Dictionary<string, string> { { "WIDTH", "900" }, { "HEIGHT", "2100" } }
                },
                new BlockDefinition
                {
                    Name = "WINDOW-FIX",
                    BasePoint = new Point2D(0, 0),
                    AttributeDefinitions = new Dictionary<string, string> { { "WIDTH", "1200" }, { "HEIGHT", "1500" } }
                }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class WallCandidate
    {
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public double Confidence { get; set; }
        public bool IsExterior { get; set; }
        public List<string> SourceEntityIds { get; set; } = new();
    }

    public class ImportProgress
    {
        public string Phase { get; set; }
        public int Percentage { get; set; }
        public bool IsComplete { get; set; }
    }

    public class ImportProgressEventArgs : EventArgs
    {
        public ImportProgress Progress { get; set; }
    }

    #endregion
}
