// =========================================================================
// StingBIM.AI.Creation - Image to BIM Processor
// AI-powered conversion of images, floor plans, and sketches to BIM elements
// =========================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.ImageProcessing
{
    /// <summary>
    /// Converts images, floor plans, sketches, and scanned drawings to BIM elements.
    /// Uses computer vision and deep learning for element recognition and geometry extraction.
    /// </summary>
    public class ImageToBIMProcessor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ImagePreprocessor _preprocessor;
        private readonly ElementDetector _elementDetector;
        private readonly GeometryExtractor _geometryExtractor;
        private readonly SymbolRecognizer _symbolRecognizer;
        private readonly DimensionReader _dimensionReader;
        private readonly ScaleCalibrator _scaleCalibrator;
        private readonly BIMElementGenerator _bimGenerator;

        private readonly Dictionary<string, ImageProcessingProfile> _processingProfiles;
        private readonly Dictionary<string, ElementTemplate> _elementTemplates;

        public ImageToBIMProcessor()
        {
            _preprocessor = new ImagePreprocessor();
            _elementDetector = new ElementDetector();
            _geometryExtractor = new GeometryExtractor();
            _symbolRecognizer = new SymbolRecognizer();
            _dimensionReader = new DimensionReader();
            _scaleCalibrator = new ScaleCalibrator();
            _bimGenerator = new BIMElementGenerator();

            _processingProfiles = InitializeProcessingProfiles();
            _elementTemplates = InitializeElementTemplates();

            Logger.Info("ImageToBIMProcessor initialized successfully");
        }

        #region Main Processing Methods

        /// <summary>
        /// Processes an image and converts detected elements to BIM format.
        /// </summary>
        public async Task<ImageToBIMResult> ProcessImageAsync(
            string imagePath,
            ImageProcessingOptions options,
            IProgress<ImageProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing image: {imagePath}");

            var result = new ImageToBIMResult
            {
                SourceImagePath = imagePath,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Step 1: Load and preprocess image
                progress?.Report(new ImageProcessingProgress { Stage = "Loading", Percentage = 5 });
                var imageData = await _preprocessor.LoadImageAsync(imagePath, cancellationToken);

                // Step 2: Enhance and clean image
                progress?.Report(new ImageProcessingProgress { Stage = "Preprocessing", Percentage = 15 });
                var enhancedImage = await _preprocessor.EnhanceImageAsync(imageData, options, cancellationToken);

                // Step 3: Calibrate scale
                progress?.Report(new ImageProcessingProgress { Stage = "Calibrating Scale", Percentage = 25 });
                var scaleInfo = await _scaleCalibrator.CalibrateAsync(enhancedImage, options.ScaleReference, cancellationToken);
                result.DetectedScale = scaleInfo;

                // Step 4: Detect elements
                progress?.Report(new ImageProcessingProgress { Stage = "Detecting Elements", Percentage = 40 });
                var detectedElements = await _elementDetector.DetectElementsAsync(enhancedImage, options.DetectionMode, cancellationToken);

                // Step 5: Recognize symbols
                progress?.Report(new ImageProcessingProgress { Stage = "Recognizing Symbols", Percentage = 55 });
                var recognizedSymbols = await _symbolRecognizer.RecognizeSymbolsAsync(enhancedImage, cancellationToken);

                // Step 6: Read dimensions
                progress?.Report(new ImageProcessingProgress { Stage = "Reading Dimensions", Percentage = 65 });
                var dimensions = await _dimensionReader.ReadDimensionsAsync(enhancedImage, scaleInfo, cancellationToken);

                // Step 7: Extract geometry
                progress?.Report(new ImageProcessingProgress { Stage = "Extracting Geometry", Percentage = 75 });
                var geometries = await _geometryExtractor.ExtractGeometriesAsync(
                    detectedElements, recognizedSymbols, dimensions, scaleInfo, cancellationToken);

                // Step 8: Generate BIM elements
                progress?.Report(new ImageProcessingProgress { Stage = "Generating BIM Elements", Percentage = 90 });
                result.GeneratedElements = await _bimGenerator.GenerateElementsAsync(
                    geometries, options.TargetLOD, cancellationToken);

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"Image processing completed. Generated {result.GeneratedElements.Count} elements");
                progress?.Report(new ImageProcessingProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing image: {imagePath}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Processes a floor plan image and generates rooms, walls, doors, and windows.
        /// </summary>
        public async Task<FloorPlanProcessingResult> ProcessFloorPlanAsync(
            string imagePath,
            FloorPlanOptions options,
            IProgress<ImageProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing floor plan: {imagePath}");

            var result = new FloorPlanProcessingResult
            {
                SourceImagePath = imagePath,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Load and preprocess
                progress?.Report(new ImageProcessingProgress { Stage = "Loading Floor Plan", Percentage = 5 });
                var imageData = await _preprocessor.LoadImageAsync(imagePath, cancellationToken);
                var enhancedImage = await _preprocessor.EnhanceFloorPlanAsync(imageData, cancellationToken);

                // Calibrate scale
                progress?.Report(new ImageProcessingProgress { Stage = "Calibrating Scale", Percentage = 15 });
                var scaleInfo = await _scaleCalibrator.CalibrateFloorPlanAsync(
                    enhancedImage, options.KnownDimension, cancellationToken);
                result.Scale = scaleInfo;

                // Detect walls
                progress?.Report(new ImageProcessingProgress { Stage = "Detecting Walls", Percentage = 30 });
                result.DetectedWalls = await DetectWallsFromFloorPlanAsync(enhancedImage, scaleInfo, cancellationToken);

                // Detect openings (doors/windows)
                progress?.Report(new ImageProcessingProgress { Stage = "Detecting Openings", Percentage = 50 });
                var openings = await DetectOpeningsFromFloorPlanAsync(enhancedImage, result.DetectedWalls, cancellationToken);
                result.DetectedDoors = openings.Doors;
                result.DetectedWindows = openings.Windows;

                // Detect rooms
                progress?.Report(new ImageProcessingProgress { Stage = "Detecting Rooms", Percentage = 70 });
                result.DetectedRooms = await DetectRoomsFromFloorPlanAsync(
                    enhancedImage, result.DetectedWalls, cancellationToken);

                // Recognize room labels
                progress?.Report(new ImageProcessingProgress { Stage = "Reading Labels", Percentage = 85 });
                await AssignRoomLabelsAsync(enhancedImage, result.DetectedRooms, cancellationToken);

                // Generate BIM elements
                progress?.Report(new ImageProcessingProgress { Stage = "Generating BIM", Percentage = 95 });
                result.GeneratedLevel = await GenerateLevelFromFloorPlanAsync(result, options, cancellationToken);

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"Floor plan processing completed. Walls: {result.DetectedWalls.Count}, " +
                           $"Doors: {result.DetectedDoors.Count}, Windows: {result.DetectedWindows.Count}, " +
                           $"Rooms: {result.DetectedRooms.Count}");

                progress?.Report(new ImageProcessingProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing floor plan: {imagePath}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Processes a hand-drawn sketch and converts it to BIM elements.
        /// </summary>
        public async Task<SketchProcessingResult> ProcessSketchAsync(
            string imagePath,
            SketchOptions options,
            IProgress<ImageProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing sketch: {imagePath}");

            var result = new SketchProcessingResult
            {
                SourceImagePath = imagePath,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Load sketch
                progress?.Report(new ImageProcessingProgress { Stage = "Loading Sketch", Percentage = 10 });
                var imageData = await _preprocessor.LoadImageAsync(imagePath, cancellationToken);

                // Clean and enhance sketch
                progress?.Report(new ImageProcessingProgress { Stage = "Enhancing Sketch", Percentage = 20 });
                var cleanedSketch = await _preprocessor.CleanSketchAsync(imageData, cancellationToken);

                // Detect sketch strokes
                progress?.Report(new ImageProcessingProgress { Stage = "Analyzing Strokes", Percentage = 35 });
                var strokes = await AnalyzeSketchStrokesAsync(cleanedSketch, cancellationToken);

                // Recognize shapes
                progress?.Report(new ImageProcessingProgress { Stage = "Recognizing Shapes", Percentage = 50 });
                var shapes = await RecognizeShapesFromStrokesAsync(strokes, cancellationToken);

                // Interpret design intent
                progress?.Report(new ImageProcessingProgress { Stage = "Interpreting Intent", Percentage = 65 });
                var interpretedElements = await InterpretSketchIntentAsync(shapes, options.Context, cancellationToken);

                // Regularize geometry
                progress?.Report(new ImageProcessingProgress { Stage = "Regularizing Geometry", Percentage = 80 });
                var regularizedElements = await RegularizeSketchGeometryAsync(interpretedElements, cancellationToken);

                // Generate BIM elements
                progress?.Report(new ImageProcessingProgress { Stage = "Generating BIM", Percentage = 95 });
                result.GeneratedElements = await _bimGenerator.GenerateFromSketchAsync(
                    regularizedElements, options.TargetLOD, cancellationToken);

                result.RecognizedStrokes = strokes.Count;
                result.InterpretedShapes = shapes.Count;
                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"Sketch processing completed. Generated {result.GeneratedElements.Count} elements");
                progress?.Report(new ImageProcessingProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing sketch: {imagePath}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Processes a photo of an existing building and extracts geometry.
        /// </summary>
        public async Task<PhotoProcessingResult> ProcessBuildingPhotoAsync(
            string imagePath,
            PhotoProcessingOptions options,
            IProgress<ImageProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing building photo: {imagePath}");

            var result = new PhotoProcessingResult
            {
                SourceImagePath = imagePath,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Load photo
                progress?.Report(new ImageProcessingProgress { Stage = "Loading Photo", Percentage = 5 });
                var imageData = await _preprocessor.LoadImageAsync(imagePath, cancellationToken);

                // Detect perspective
                progress?.Report(new ImageProcessingProgress { Stage = "Analyzing Perspective", Percentage = 15 });
                result.PerspectiveInfo = await AnalyzePerspectiveAsync(imageData, cancellationToken);

                // Detect facade elements
                progress?.Report(new ImageProcessingProgress { Stage = "Detecting Facade", Percentage = 35 });
                var facadeElements = await DetectFacadeElementsAsync(imageData, cancellationToken);

                // Classify elements (windows, doors, balconies, etc.)
                progress?.Report(new ImageProcessingProgress { Stage = "Classifying Elements", Percentage = 55 });
                result.ClassifiedElements = await ClassifyFacadeElementsAsync(facadeElements, cancellationToken);

                // Extract dimensions
                progress?.Report(new ImageProcessingProgress { Stage = "Extracting Dimensions", Percentage = 70 });
                await ExtractFacadeDimensionsAsync(result.ClassifiedElements, result.PerspectiveInfo, cancellationToken);

                // Generate facade model
                progress?.Report(new ImageProcessingProgress { Stage = "Generating Model", Percentage = 90 });
                result.GeneratedFacade = await GenerateFacadeModelAsync(
                    result.ClassifiedElements, options, cancellationToken);

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"Photo processing completed. Detected {result.ClassifiedElements.Count} facade elements");
                progress?.Report(new ImageProcessingProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing photo: {imagePath}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Processes multiple images to create a 3D model (photogrammetry-lite).
        /// </summary>
        public async Task<MultiImageProcessingResult> ProcessMultipleImagesAsync(
            List<string> imagePaths,
            MultiImageOptions options,
            IProgress<ImageProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing {imagePaths.Count} images for 3D reconstruction");

            var result = new MultiImageProcessingResult
            {
                SourceImagePaths = imagePaths,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Load all images
                progress?.Report(new ImageProcessingProgress { Stage = "Loading Images", Percentage = 10 });
                var images = new List<ProcessedImage>();
                foreach (var path in imagePaths)
                {
                    var imageData = await _preprocessor.LoadImageAsync(path, cancellationToken);
                    images.Add(new ProcessedImage { Path = path, Data = imageData });
                }

                // Extract features
                progress?.Report(new ImageProcessingProgress { Stage = "Extracting Features", Percentage = 25 });
                await ExtractImageFeaturesAsync(images, cancellationToken);

                // Match features between images
                progress?.Report(new ImageProcessingProgress { Stage = "Matching Features", Percentage = 45 });
                var matches = await MatchImageFeaturesAsync(images, cancellationToken);

                // Estimate camera poses
                progress?.Report(new ImageProcessingProgress { Stage = "Estimating Poses", Percentage = 60 });
                result.EstimatedPoses = await EstimateCameraPosesAsync(matches, cancellationToken);

                // Triangulate points
                progress?.Report(new ImageProcessingProgress { Stage = "Triangulating", Percentage = 75 });
                result.PointCloud = await TriangulatePointsAsync(images, matches, result.EstimatedPoses, cancellationToken);

                // Generate mesh
                progress?.Report(new ImageProcessingProgress { Stage = "Generating Mesh", Percentage = 90 });
                result.GeneratedMesh = await GenerateMeshFromPointsAsync(result.PointCloud, options, cancellationToken);

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"Multi-image processing completed. Point cloud: {result.PointCloud.Points.Count} points");
                progress?.Report(new ImageProcessingProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing multiple images");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Floor Plan Processing Helpers

        private async Task<List<DetectedWall>> DetectWallsFromFloorPlanAsync(
            EnhancedImage image, ScaleInfo scale, CancellationToken cancellationToken)
        {
            var walls = new List<DetectedWall>();

            // Use line detection to find wall segments
            var lines = await _geometryExtractor.DetectLinesAsync(image, cancellationToken);

            // Group parallel lines into walls
            var wallSegments = GroupLinesIntoWalls(lines);

            foreach (var segment in wallSegments)
            {
                var wall = new DetectedWall
                {
                    Id = Guid.NewGuid().ToString(),
                    StartPoint = ScalePoint(segment.Start, scale),
                    EndPoint = ScalePoint(segment.End, scale),
                    Thickness = EstimateWallThickness(segment, scale),
                    Confidence = segment.Confidence,
                    WallType = ClassifyWallType(segment)
                };
                walls.Add(wall);
            }

            // Merge connected walls
            walls = MergeConnectedWalls(walls);

            Logger.Debug($"Detected {walls.Count} wall segments");
            return walls;
        }

        private async Task<(List<DetectedDoor> Doors, List<DetectedWindow> Windows)> DetectOpeningsFromFloorPlanAsync(
            EnhancedImage image, List<DetectedWall> walls, CancellationToken cancellationToken)
        {
            var doors = new List<DetectedDoor>();
            var windows = new List<DetectedWindow>();

            // Detect door symbols
            var doorSymbols = await _symbolRecognizer.RecognizeDoorsAsync(image, cancellationToken);
            foreach (var symbol in doorSymbols)
            {
                var door = new DetectedDoor
                {
                    Id = Guid.NewGuid().ToString(),
                    Location = symbol.Center,
                    Width = symbol.Width,
                    SwingDirection = DetermineSwingDirection(symbol),
                    HostWallId = FindHostWall(symbol.Center, walls)?.Id,
                    DoorType = symbol.SubType,
                    Confidence = symbol.Confidence
                };
                doors.Add(door);
            }

            // Detect window symbols
            var windowSymbols = await _symbolRecognizer.RecognizeWindowsAsync(image, cancellationToken);
            foreach (var symbol in windowSymbols)
            {
                var window = new DetectedWindow
                {
                    Id = Guid.NewGuid().ToString(),
                    Location = symbol.Center,
                    Width = symbol.Width,
                    SillHeight = EstimateSillHeight(symbol),
                    HostWallId = FindHostWall(symbol.Center, walls)?.Id,
                    WindowType = symbol.SubType,
                    Confidence = symbol.Confidence
                };
                windows.Add(window);
            }

            Logger.Debug($"Detected {doors.Count} doors and {windows.Count} windows");
            return (doors, windows);
        }

        private async Task<List<DetectedRoom>> DetectRoomsFromFloorPlanAsync(
            EnhancedImage image, List<DetectedWall> walls, CancellationToken cancellationToken)
        {
            var rooms = new List<DetectedRoom>();

            // Find enclosed regions
            var regions = await _geometryExtractor.FindEnclosedRegionsAsync(image, walls, cancellationToken);

            foreach (var region in regions)
            {
                var room = new DetectedRoom
                {
                    Id = Guid.NewGuid().ToString(),
                    Boundary = region.Boundary,
                    Area = region.Area,
                    Centroid = region.Centroid,
                    BoundingWallIds = region.BoundingWalls.Select(w => w.Id).ToList(),
                    Confidence = region.Confidence
                };
                rooms.Add(room);
            }

            Logger.Debug($"Detected {rooms.Count} rooms");
            return rooms;
        }

        private async Task AssignRoomLabelsAsync(
            EnhancedImage image, List<DetectedRoom> rooms, CancellationToken cancellationToken)
        {
            // Use OCR to read room labels
            var textRegions = await _dimensionReader.ReadTextAsync(image, cancellationToken);

            foreach (var room in rooms)
            {
                // Find text within room boundary
                var labelsInRoom = textRegions
                    .Where(t => IsPointInPolygon(t.Center, room.Boundary))
                    .OrderBy(t => DistanceToPoint(t.Center, room.Centroid))
                    .ToList();

                if (labelsInRoom.Any())
                {
                    var primaryLabel = labelsInRoom.First();
                    room.Label = primaryLabel.Text;
                    room.RoomType = InferRoomType(primaryLabel.Text);

                    // Check for area text
                    var areaText = labelsInRoom.FirstOrDefault(t => t.Text.Contains("m²") || t.Text.Contains("sf"));
                    if (areaText != null)
                    {
                        room.LabeledArea = ParseAreaText(areaText.Text);
                    }
                }
            }
        }

        private async Task<GeneratedLevel> GenerateLevelFromFloorPlanAsync(
            FloorPlanProcessingResult planResult, FloorPlanOptions options, CancellationToken cancellationToken)
        {
            var level = new GeneratedLevel
            {
                Id = Guid.NewGuid().ToString(),
                Name = options.LevelName ?? "Level 1",
                Elevation = options.Elevation,
                FloorToFloorHeight = options.FloorToFloorHeight
            };

            // Generate wall elements
            foreach (var wall in planResult.DetectedWalls)
            {
                var wallElement = await _bimGenerator.GenerateWallAsync(wall, options.TargetLOD, cancellationToken);
                level.Walls.Add(wallElement);
            }

            // Generate doors
            foreach (var door in planResult.DetectedDoors)
            {
                var doorElement = await _bimGenerator.GenerateDoorAsync(door, options.TargetLOD, cancellationToken);
                level.Doors.Add(doorElement);
            }

            // Generate windows
            foreach (var window in planResult.DetectedWindows)
            {
                var windowElement = await _bimGenerator.GenerateWindowAsync(window, options.TargetLOD, cancellationToken);
                level.Windows.Add(windowElement);
            }

            // Generate rooms
            foreach (var room in planResult.DetectedRooms)
            {
                var roomElement = await _bimGenerator.GenerateRoomAsync(room, cancellationToken);
                level.Rooms.Add(roomElement);
            }

            return level;
        }

        #endregion

        #region Sketch Processing Helpers

        private async Task<List<SketchStroke>> AnalyzeSketchStrokesAsync(
            CleanedSketch sketch, CancellationToken cancellationToken)
        {
            var strokes = new List<SketchStroke>();

            // Extract connected components (individual strokes)
            var components = await _geometryExtractor.ExtractConnectedComponentsAsync(sketch, cancellationToken);

            foreach (var component in components)
            {
                var stroke = new SketchStroke
                {
                    Id = Guid.NewGuid().ToString(),
                    Points = component.Points,
                    IsClosed = component.IsClosed,
                    Length = component.Length,
                    Curvature = CalculateCurvature(component.Points),
                    StrokeType = ClassifyStrokeType(component)
                };
                strokes.Add(stroke);
            }

            return strokes;
        }

        private async Task<List<RecognizedShape>> RecognizeShapesFromStrokesAsync(
            List<SketchStroke> strokes, CancellationToken cancellationToken)
        {
            var shapes = new List<RecognizedShape>();

            foreach (var stroke in strokes)
            {
                var shape = await RecognizeShapeAsync(stroke, cancellationToken);
                if (shape != null)
                {
                    shapes.Add(shape);
                }
            }

            // Look for composite shapes (rectangles from 4 lines, etc.)
            var compositeShapes = FindCompositeShapes(shapes);
            shapes.AddRange(compositeShapes);

            return shapes;
        }

        private async Task<RecognizedShape> RecognizeShapeAsync(
            SketchStroke stroke, CancellationToken cancellationToken)
        {
            // Attempt to fit various primitives
            var candidates = new List<(ShapeType Type, double Confidence, object Parameters)>();

            // Try line fit
            var lineFit = FitLine(stroke.Points);
            if (lineFit.Confidence > 0.8)
            {
                candidates.Add((ShapeType.Line, lineFit.Confidence, lineFit.Parameters));
            }

            // Try rectangle fit
            if (stroke.IsClosed)
            {
                var rectFit = FitRectangle(stroke.Points);
                if (rectFit.Confidence > 0.7)
                {
                    candidates.Add((ShapeType.Rectangle, rectFit.Confidence, rectFit.Parameters));
                }
            }

            // Try circle/arc fit
            var circleFit = FitCircle(stroke.Points);
            if (circleFit.Confidence > 0.75)
            {
                candidates.Add((stroke.IsClosed ? ShapeType.Circle : ShapeType.Arc,
                               circleFit.Confidence, circleFit.Parameters));
            }

            // Return best match
            if (candidates.Any())
            {
                var best = candidates.OrderByDescending(c => c.Confidence).First();
                return new RecognizedShape
                {
                    SourceStrokeId = stroke.Id,
                    ShapeType = best.Type,
                    Confidence = best.Confidence,
                    Parameters = best.Parameters
                };
            }

            return null;
        }

        private async Task<List<InterpretedElement>> InterpretSketchIntentAsync(
            List<RecognizedShape> shapes, SketchContext context, CancellationToken cancellationToken)
        {
            var elements = new List<InterpretedElement>();

            foreach (var shape in shapes)
            {
                var element = new InterpretedElement
                {
                    SourceShapeId = shape.SourceStrokeId,
                    ShapeType = shape.ShapeType
                };

                // Interpret based on context and shape
                switch (shape.ShapeType)
                {
                    case ShapeType.Line:
                        // Could be wall, beam, duct, etc.
                        element.ElementType = InterpretLineElement(shape, context);
                        break;

                    case ShapeType.Rectangle:
                        // Could be room, door, window, column, etc.
                        element.ElementType = InterpretRectangleElement(shape, context);
                        break;

                    case ShapeType.Circle:
                        // Could be column, pipe, duct, etc.
                        element.ElementType = InterpretCircleElement(shape, context);
                        break;

                    case ShapeType.Arc:
                        // Could be curved wall, arc door, etc.
                        element.ElementType = InterpretArcElement(shape, context);
                        break;
                }

                element.Parameters = shape.Parameters;
                elements.Add(element);
            }

            return elements;
        }

        private async Task<List<RegularizedElement>> RegularizeSketchGeometryAsync(
            List<InterpretedElement> elements, CancellationToken cancellationToken)
        {
            var regularized = new List<RegularizedElement>();

            foreach (var element in elements)
            {
                var reg = new RegularizedElement
                {
                    SourceElementId = element.SourceShapeId,
                    ElementType = element.ElementType
                };

                // Snap to grid
                reg.Geometry = SnapToGrid(element.Parameters, gridSize: 100); // 100mm grid

                // Align to orthogonal (if close to 0°, 90°, etc.)
                reg.Geometry = AlignToOrthogonal(reg.Geometry, tolerance: 5.0); // 5° tolerance

                // Round dimensions to standard sizes
                reg.Geometry = RoundToStandardDimensions(reg.Geometry, element.ElementType);

                regularized.Add(reg);
            }

            // Connect nearby endpoints
            regularized = ConnectNearbyEndpoints(regularized, tolerance: 50); // 50mm tolerance

            return regularized;
        }

        #endregion

        #region Photo Processing Helpers

        private async Task<PerspectiveInfo> AnalyzePerspectiveAsync(
            ImageData image, CancellationToken cancellationToken)
        {
            var info = new PerspectiveInfo();

            // Detect vanishing points
            var lines = await _geometryExtractor.DetectLinesAsync(image, cancellationToken);

            // Group lines by orientation
            var horizontalLines = lines.Where(l => Math.Abs(l.Angle) < 10 || Math.Abs(l.Angle) > 170).ToList();
            var verticalLines = lines.Where(l => Math.Abs(l.Angle - 90) < 10).ToList();

            // Find vanishing points
            if (horizontalLines.Count >= 2)
            {
                info.HorizontalVanishingPoint = FindVanishingPoint(horizontalLines);
            }

            if (verticalLines.Count >= 2)
            {
                info.VerticalVanishingPoint = FindVanishingPoint(verticalLines);
            }

            // Determine perspective type
            info.PerspectiveType = DeterminePerspectiveType(info);

            // Estimate camera parameters
            info.EstimatedFocalLength = EstimateFocalLength(info, image.Width, image.Height);
            info.EstimatedViewAngle = EstimateViewAngle(info);

            return info;
        }

        private async Task<List<FacadeElement>> DetectFacadeElementsAsync(
            ImageData image, CancellationToken cancellationToken)
        {
            var elements = new List<FacadeElement>();

            // Detect rectangular regions (potential windows, doors)
            var rectangles = await _geometryExtractor.DetectRectanglesAsync(image, cancellationToken);

            foreach (var rect in rectangles)
            {
                var element = new FacadeElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Bounds = rect.Bounds,
                    PixelRegion = rect.Region,
                    Confidence = rect.Confidence
                };
                elements.Add(element);
            }

            return elements;
        }

        private async Task<List<ClassifiedFacadeElement>> ClassifyFacadeElementsAsync(
            List<FacadeElement> elements, CancellationToken cancellationToken)
        {
            var classified = new List<ClassifiedFacadeElement>();

            foreach (var element in elements)
            {
                var classifiedElement = new ClassifiedFacadeElement
                {
                    Id = element.Id,
                    Bounds = element.Bounds,
                    PixelRegion = element.PixelRegion
                };

                // Classify based on aspect ratio, position, and visual features
                var aspectRatio = element.Bounds.Width / (double)element.Bounds.Height;
                var relativeY = element.Bounds.Y; // Position from top

                if (aspectRatio > 0.3 && aspectRatio < 0.7 && relativeY > 0.7)
                {
                    classifiedElement.ElementType = FacadeElementType.Door;
                }
                else if (aspectRatio > 0.5 && aspectRatio < 2.0)
                {
                    classifiedElement.ElementType = FacadeElementType.Window;
                }
                else if (aspectRatio > 2.0)
                {
                    classifiedElement.ElementType = FacadeElementType.Balcony;
                }
                else
                {
                    classifiedElement.ElementType = FacadeElementType.Unknown;
                }

                classifiedElement.Confidence = element.Confidence;
                classified.Add(classifiedElement);
            }

            return classified;
        }

        private async Task ExtractFacadeDimensionsAsync(
            List<ClassifiedFacadeElement> elements, PerspectiveInfo perspective,
            CancellationToken cancellationToken)
        {
            foreach (var element in elements)
            {
                // Convert pixel dimensions to real-world estimates
                // This requires perspective correction

                var correctedBounds = CorrectForPerspective(element.Bounds, perspective);

                // Estimate dimensions based on element type standards
                switch (element.ElementType)
                {
                    case FacadeElementType.Window:
                        element.EstimatedWidth = EstimateWindowWidth(correctedBounds);
                        element.EstimatedHeight = EstimateWindowHeight(correctedBounds);
                        break;

                    case FacadeElementType.Door:
                        element.EstimatedWidth = EstimateDoorWidth(correctedBounds);
                        element.EstimatedHeight = EstimateDoorHeight(correctedBounds);
                        break;
                }
            }
        }

        private async Task<GeneratedFacade> GenerateFacadeModelAsync(
            List<ClassifiedFacadeElement> elements, PhotoProcessingOptions options,
            CancellationToken cancellationToken)
        {
            var facade = new GeneratedFacade
            {
                Id = Guid.NewGuid().ToString(),
                Width = options.EstimatedFacadeWidth,
                Height = options.EstimatedFacadeHeight
            };

            foreach (var element in elements.Where(e => e.Confidence > 0.5))
            {
                switch (element.ElementType)
                {
                    case FacadeElementType.Window:
                        facade.Windows.Add(new GeneratedWindow
                        {
                            Id = element.Id,
                            Width = element.EstimatedWidth,
                            Height = element.EstimatedHeight,
                            Position = element.Bounds
                        });
                        break;

                    case FacadeElementType.Door:
                        facade.Doors.Add(new GeneratedDoor
                        {
                            Id = element.Id,
                            Width = element.EstimatedWidth,
                            Height = element.EstimatedHeight,
                            Position = element.Bounds
                        });
                        break;
                }
            }

            return facade;
        }

        #endregion

        #region Multi-Image Processing Helpers

        private async Task ExtractImageFeaturesAsync(
            List<ProcessedImage> images, CancellationToken cancellationToken)
        {
            foreach (var image in images)
            {
                // Extract SIFT/ORB-like features
                image.Features = await _geometryExtractor.ExtractFeaturesAsync(image.Data, cancellationToken);
            }
        }

        private async Task<List<FeatureMatch>> MatchImageFeaturesAsync(
            List<ProcessedImage> images, CancellationToken cancellationToken)
        {
            var matches = new List<FeatureMatch>();

            // Match features between consecutive image pairs
            for (int i = 0; i < images.Count - 1; i++)
            {
                var pairMatches = await _geometryExtractor.MatchFeaturesAsync(
                    images[i].Features, images[i + 1].Features, cancellationToken);

                matches.AddRange(pairMatches.Select(m => new FeatureMatch
                {
                    Image1Index = i,
                    Image2Index = i + 1,
                    Point1 = m.Point1,
                    Point2 = m.Point2,
                    Score = m.Score
                }));
            }

            return matches;
        }

        private async Task<List<CameraPose>> EstimateCameraPosesAsync(
            List<FeatureMatch> matches, CancellationToken cancellationToken)
        {
            var poses = new List<CameraPose>();

            // Estimate relative poses using essential matrix
            // This is a simplified implementation

            var uniqueImages = matches.SelectMany(m => new[] { m.Image1Index, m.Image2Index }).Distinct().OrderBy(i => i);

            foreach (var imageIndex in uniqueImages)
            {
                poses.Add(new CameraPose
                {
                    ImageIndex = imageIndex,
                    Position = new Point3D(0, 0, imageIndex * 1.0), // Simplified
                    Rotation = Matrix3x3.Identity
                });
            }

            return poses;
        }

        private async Task<PointCloud> TriangulatePointsAsync(
            List<ProcessedImage> images, List<FeatureMatch> matches,
            List<CameraPose> poses, CancellationToken cancellationToken)
        {
            var pointCloud = new PointCloud();

            // Triangulate matched points
            foreach (var match in matches)
            {
                var pose1 = poses.First(p => p.ImageIndex == match.Image1Index);
                var pose2 = poses.First(p => p.ImageIndex == match.Image2Index);

                var point3D = TriangulatePoint(match.Point1, match.Point2, pose1, pose2);

                pointCloud.Points.Add(new PointCloudPoint
                {
                    Position = point3D,
                    Color = GetPointColor(images[match.Image1Index], match.Point1),
                    Confidence = match.Score
                });
            }

            return pointCloud;
        }

        private async Task<GeneratedMesh> GenerateMeshFromPointsAsync(
            PointCloud pointCloud, MultiImageOptions options, CancellationToken cancellationToken)
        {
            var mesh = new GeneratedMesh();

            // Simplified mesh generation - in reality would use Delaunay/Poisson reconstruction
            // For now, create a basic surface from the point cloud

            mesh.Vertices = pointCloud.Points.Select(p => p.Position).ToList();
            mesh.VertexColors = pointCloud.Points.Select(p => p.Color).ToList();

            // Generate faces (simplified)
            // In reality, this would use proper surface reconstruction

            return mesh;
        }

        #endregion

        #region Utility Methods

        private Dictionary<string, ImageProcessingProfile> InitializeProcessingProfiles()
        {
            return new Dictionary<string, ImageProcessingProfile>
            {
                ["FloorPlan"] = new ImageProcessingProfile
                {
                    Name = "Floor Plan",
                    ContrastEnhancement = 1.2,
                    EdgeDetectionThreshold = 50,
                    NoiseReduction = NoiseReductionLevel.Medium,
                    Binarization = true
                },
                ["Sketch"] = new ImageProcessingProfile
                {
                    Name = "Hand Sketch",
                    ContrastEnhancement = 1.5,
                    EdgeDetectionThreshold = 30,
                    NoiseReduction = NoiseReductionLevel.High,
                    Binarization = true
                },
                ["Photo"] = new ImageProcessingProfile
                {
                    Name = "Building Photo",
                    ContrastEnhancement = 1.1,
                    EdgeDetectionThreshold = 70,
                    NoiseReduction = NoiseReductionLevel.Low,
                    Binarization = false
                }
            };
        }

        private Dictionary<string, ElementTemplate> InitializeElementTemplates()
        {
            return new Dictionary<string, ElementTemplate>
            {
                ["Wall"] = new ElementTemplate { Type = "Wall", DefaultWidth = 200, DefaultHeight = 2700 },
                ["Door"] = new ElementTemplate { Type = "Door", DefaultWidth = 900, DefaultHeight = 2100 },
                ["Window"] = new ElementTemplate { Type = "Window", DefaultWidth = 1200, DefaultHeight = 1500 },
                ["Column"] = new ElementTemplate { Type = "Column", DefaultWidth = 300, DefaultHeight = 3000 }
            };
        }

        private Point2D ScalePoint(Point2D pixelPoint, ScaleInfo scale)
        {
            return new Point2D
            {
                X = pixelPoint.X * scale.PixelsToMm,
                Y = pixelPoint.Y * scale.PixelsToMm
            };
        }

        private double EstimateWallThickness(LineSegment segment, ScaleInfo scale)
        {
            // Estimate based on line thickness in pixels
            return Math.Max(segment.Thickness * scale.PixelsToMm, 100); // Minimum 100mm
        }

        private string ClassifyWallType(LineSegment segment)
        {
            var thickness = segment.Thickness;
            if (thickness > 30) return "Exterior";
            if (thickness > 15) return "Interior";
            return "Partition";
        }

        private List<LineSegment> GroupLinesIntoWalls(List<DetectedLine> lines)
        {
            var segments = new List<LineSegment>();
            // Group parallel nearby lines into wall segments
            foreach (var line in lines)
            {
                segments.Add(new LineSegment
                {
                    Start = line.Start,
                    End = line.End,
                    Thickness = line.Thickness,
                    Confidence = line.Confidence
                });
            }
            return segments;
        }

        private List<DetectedWall> MergeConnectedWalls(List<DetectedWall> walls)
        {
            // Merge walls that share endpoints within tolerance
            return walls; // Simplified - would implement proper merging
        }

        private SwingDirection DetermineSwingDirection(SymbolMatch symbol)
        {
            // Analyze symbol to determine door swing direction
            return SwingDirection.Inward;
        }

        private DetectedWall FindHostWall(Point2D location, List<DetectedWall> walls)
        {
            return walls.FirstOrDefault(w => IsPointOnWall(location, w));
        }

        private bool IsPointOnWall(Point2D point, DetectedWall wall)
        {
            // Check if point is on or near the wall line
            var distance = DistanceToLineSegment(point, wall.StartPoint, wall.EndPoint);
            return distance < wall.Thickness / 2 + 50; // 50mm tolerance
        }

        private double DistanceToLineSegment(Point2D point, Point2D lineStart, Point2D lineEnd)
        {
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0)
                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));

            var t = Math.Max(0, Math.Min(1, ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared));
            var projX = lineStart.X + t * dx;
            var projY = lineStart.Y + t * dy;

            return Math.Sqrt(Math.Pow(point.X - projX, 2) + Math.Pow(point.Y - projY, 2));
        }

        private double EstimateSillHeight(SymbolMatch symbol)
        {
            return 900; // Default 900mm sill height
        }

        private bool IsPointInPolygon(Point2D point, List<Point2D> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; j = i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                    (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private double DistanceToPoint(Point2D p1, Point2D p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        private string InferRoomType(string label)
        {
            var lowerLabel = label.ToLowerInvariant();
            if (lowerLabel.Contains("bed")) return "Bedroom";
            if (lowerLabel.Contains("bath")) return "Bathroom";
            if (lowerLabel.Contains("kitchen")) return "Kitchen";
            if (lowerLabel.Contains("living")) return "Living Room";
            if (lowerLabel.Contains("dining")) return "Dining Room";
            if (lowerLabel.Contains("office")) return "Office";
            if (lowerLabel.Contains("garage")) return "Garage";
            return "Room";
        }

        private double? ParseAreaText(string text)
        {
            // Parse area from text like "25.5 m²" or "250 sf"
            var numericPart = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (double.TryParse(numericPart, out var value))
            {
                if (text.Contains("sf") || text.Contains("SF"))
                    return value * 0.0929; // Convert sf to m²
                return value;
            }
            return null;
        }

        private double CalculateCurvature(List<Point2D> points)
        {
            if (points.Count < 3) return 0;

            double totalCurvature = 0;
            for (int i = 1; i < points.Count - 1; i++)
            {
                var v1 = new Point2D { X = points[i].X - points[i - 1].X, Y = points[i].Y - points[i - 1].Y };
                var v2 = new Point2D { X = points[i + 1].X - points[i].X, Y = points[i + 1].Y - points[i].Y };

                var cross = v1.X * v2.Y - v1.Y * v2.X;
                var len1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
                var len2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

                if (len1 > 0 && len2 > 0)
                {
                    totalCurvature += Math.Abs(cross / (len1 * len2));
                }
            }

            return totalCurvature / (points.Count - 2);
        }

        private StrokeType ClassifyStrokeType(ConnectedComponent component)
        {
            if (component.IsClosed) return StrokeType.Closed;
            if (component.Length < 50) return StrokeType.Annotation;
            return StrokeType.Open;
        }

        private (double Confidence, object Parameters) FitLine(List<Point2D> points)
        {
            if (points.Count < 2) return (0, null);

            // Simple linear regression
            var n = points.Count;
            var sumX = points.Sum(p => p.X);
            var sumY = points.Sum(p => p.Y);
            var sumXY = points.Sum(p => p.X * p.Y);
            var sumX2 = points.Sum(p => p.X * p.X);

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10)
            {
                // All X values are identical - can't fit a line
                return (0, null);
            }

            var slope = (n * sumXY - sumX * sumY) / denominator;
            var intercept = (sumY - slope * sumX) / n;

            // Calculate fit quality (R²)
            var meanY = sumY / n;
            var ssTot = points.Sum(p => Math.Pow(p.Y - meanY, 2));
            var ssRes = points.Sum(p => Math.Pow(p.Y - (slope * p.X + intercept), 2));
            var r2 = ssTot > 0 ? 1 - ssRes / ssTot : 0;

            return (r2, new LineFitParameters { Slope = slope, Intercept = intercept });
        }

        private (double Confidence, object Parameters) FitRectangle(List<Point2D> points)
        {
            if (points.Count < 4) return (0, null);

            // Find bounding box
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            // Check how well points align with rectangle edges
            double confidence = 0.7; // Simplified

            return (confidence, new RectangleFitParameters
            {
                X = minX, Y = minY,
                Width = maxX - minX,
                Height = maxY - minY
            });
        }

        private (double Confidence, object Parameters) FitCircle(List<Point2D> points)
        {
            if (points.Count < 3) return (0, null);

            // Simplified circle fitting
            var centerX = points.Average(p => p.X);
            var centerY = points.Average(p => p.Y);
            var avgRadius = points.Average(p => Math.Sqrt(Math.Pow(p.X - centerX, 2) + Math.Pow(p.Y - centerY, 2)));

            // Check fit quality
            var variance = points.Average(p =>
                Math.Pow(Math.Sqrt(Math.Pow(p.X - centerX, 2) + Math.Pow(p.Y - centerY, 2)) - avgRadius, 2));
            var confidence = 1 / (1 + variance / avgRadius);

            return (confidence, new CircleFitParameters
            {
                CenterX = centerX,
                CenterY = centerY,
                Radius = avgRadius
            });
        }

        private List<RecognizedShape> FindCompositeShapes(List<RecognizedShape> shapes)
        {
            var composites = new List<RecognizedShape>();

            // Find rectangles from 4 connected lines
            var lines = shapes.Where(s => s.ShapeType == ShapeType.Line).ToList();
            // Simplified - would implement proper composite shape detection

            return composites;
        }

        private string InterpretLineElement(RecognizedShape shape, SketchContext context)
        {
            switch (context.Mode)
            {
                case SketchMode.FloorPlan: return "Wall";
                case SketchMode.Section: return "Structure";
                case SketchMode.MEP: return "Duct";
                default: return "Wall";
            }
        }

        private string InterpretRectangleElement(RecognizedShape shape, SketchContext context)
        {
            var rect = shape.Parameters as RectangleFitParameters;
            if (rect == null) return "Generic";

            var aspectRatio = rect.Width / rect.Height;

            switch (context.Mode)
            {
                case SketchMode.FloorPlan:
                    if (aspectRatio > 0.3 && aspectRatio < 0.7) return "Door";
                    if (aspectRatio > 0.5 && aspectRatio < 2.0) return "Room";
                    return "Generic";

                case SketchMode.Elevation:
                    if (aspectRatio > 0.5 && aspectRatio < 2.0) return "Window";
                    return "Opening";

                default:
                    return "Generic";
            }
        }

        private string InterpretCircleElement(RecognizedShape shape, SketchContext context)
        {
            switch (context.Mode)
            {
                case SketchMode.FloorPlan: return "Column";
                case SketchMode.MEP: return "Duct";
                default: return "Column";
            }
        }

        private string InterpretArcElement(RecognizedShape shape, SketchContext context)
        {
            switch (context.Mode)
            {
                case SketchMode.FloorPlan: return "CurvedWall";
                default: return "Arc";
            }
        }

        private object SnapToGrid(object parameters, double gridSize)
        {
            // Snap geometry to grid
            return parameters; // Simplified
        }

        private object AlignToOrthogonal(object geometry, double tolerance)
        {
            // Align near-orthogonal lines to exact orthogonal
            return geometry; // Simplified
        }

        private object RoundToStandardDimensions(object geometry, string elementType)
        {
            // Round dimensions to standard sizes
            return geometry; // Simplified
        }

        private List<RegularizedElement> ConnectNearbyEndpoints(List<RegularizedElement> elements, double tolerance)
        {
            // Connect endpoints that are within tolerance
            return elements; // Simplified
        }

        private Point2D FindVanishingPoint(List<DetectedLine> lines)
        {
            // Find intersection of lines to determine vanishing point
            return new Point2D { X = 0, Y = 0 }; // Simplified
        }

        private PerspectiveType DeterminePerspectiveType(PerspectiveInfo info)
        {
            if (info.HorizontalVanishingPoint != null && info.VerticalVanishingPoint != null)
                return PerspectiveType.TwoPoint;
            if (info.HorizontalVanishingPoint != null)
                return PerspectiveType.OnePoint;
            return PerspectiveType.Orthographic;
        }

        private double EstimateFocalLength(PerspectiveInfo info, int width, int height)
        {
            return Math.Sqrt(width * width + height * height) / 2; // Simplified estimate
        }

        private double EstimateViewAngle(PerspectiveInfo info)
        {
            return 60; // Default 60° field of view
        }

        private Rectangle CorrectForPerspective(Rectangle bounds, PerspectiveInfo perspective)
        {
            // Apply perspective correction
            return bounds; // Simplified
        }

        private double EstimateWindowWidth(Rectangle correctedBounds)
        {
            return 1200; // Default 1200mm
        }

        private double EstimateWindowHeight(Rectangle correctedBounds)
        {
            return 1500; // Default 1500mm
        }

        private double EstimateDoorWidth(Rectangle correctedBounds)
        {
            return 900; // Default 900mm
        }

        private double EstimateDoorHeight(Rectangle correctedBounds)
        {
            return 2100; // Default 2100mm
        }

        private Point3D TriangulatePoint(Point2D point1, Point2D point2, CameraPose pose1, CameraPose pose2)
        {
            // Simplified triangulation
            return new Point3D
            {
                X = (point1.X + point2.X) / 2,
                Y = (point1.Y + point2.Y) / 2,
                Z = Math.Abs(point1.X - point2.X) / 10 // Simplified depth estimate
            };
        }

        private Color GetPointColor(ProcessedImage image, Point2D point)
        {
            return new Color { R = 128, G = 128, B = 128 }; // Default gray
        }

        #endregion
    }

    #region Supporting Classes

    internal class ImagePreprocessor
    {
        public async Task<ImageData> LoadImageAsync(string path, CancellationToken ct)
            => await Task.FromResult(new ImageData { Path = path, Width = 1920, Height = 1080 });

        public async Task<EnhancedImage> EnhanceImageAsync(ImageData data, ImageProcessingOptions options, CancellationToken ct)
            => await Task.FromResult(new EnhancedImage { SourceData = data });

        public async Task<EnhancedImage> EnhanceFloorPlanAsync(ImageData data, CancellationToken ct)
            => await Task.FromResult(new EnhancedImage { SourceData = data });

        public async Task<CleanedSketch> CleanSketchAsync(ImageData data, CancellationToken ct)
            => await Task.FromResult(new CleanedSketch { SourceData = data });
    }

    internal class ElementDetector
    {
        public async Task<List<DetectedElement>> DetectElementsAsync(EnhancedImage image, DetectionMode mode, CancellationToken ct)
            => await Task.FromResult(new List<DetectedElement>());
    }

    internal class GeometryExtractor
    {
        public async Task<List<DetectedLine>> DetectLinesAsync(object image, CancellationToken ct)
            => await Task.FromResult(new List<DetectedLine>());

        public async Task<List<DetectedRectangle>> DetectRectanglesAsync(ImageData image, CancellationToken ct)
            => await Task.FromResult(new List<DetectedRectangle>());

        public async Task<List<ExtractedGeometry>> ExtractGeometriesAsync(
            List<DetectedElement> elements, List<SymbolMatch> symbols,
            List<DimensionReading> dimensions, ScaleInfo scale, CancellationToken ct)
            => await Task.FromResult(new List<ExtractedGeometry>());

        public async Task<List<EnclosedRegion>> FindEnclosedRegionsAsync(EnhancedImage image, List<DetectedWall> walls, CancellationToken ct)
            => await Task.FromResult(new List<EnclosedRegion>());

        public async Task<List<ConnectedComponent>> ExtractConnectedComponentsAsync(CleanedSketch sketch, CancellationToken ct)
            => await Task.FromResult(new List<ConnectedComponent>());

        public async Task<List<ImageFeature>> ExtractFeaturesAsync(ImageData data, CancellationToken ct)
            => await Task.FromResult(new List<ImageFeature>());

        public async Task<List<FeaturePairMatch>> MatchFeaturesAsync(List<ImageFeature> f1, List<ImageFeature> f2, CancellationToken ct)
            => await Task.FromResult(new List<FeaturePairMatch>());
    }

    internal class SymbolRecognizer
    {
        public async Task<List<SymbolMatch>> RecognizeSymbolsAsync(EnhancedImage image, CancellationToken ct)
            => await Task.FromResult(new List<SymbolMatch>());

        public async Task<List<SymbolMatch>> RecognizeDoorsAsync(EnhancedImage image, CancellationToken ct)
            => await Task.FromResult(new List<SymbolMatch>());

        public async Task<List<SymbolMatch>> RecognizeWindowsAsync(EnhancedImage image, CancellationToken ct)
            => await Task.FromResult(new List<SymbolMatch>());
    }

    internal class DimensionReader
    {
        public async Task<List<DimensionReading>> ReadDimensionsAsync(EnhancedImage image, ScaleInfo scale, CancellationToken ct)
            => await Task.FromResult(new List<DimensionReading>());

        public async Task<List<TextRegion>> ReadTextAsync(EnhancedImage image, CancellationToken ct)
            => await Task.FromResult(new List<TextRegion>());
    }

    internal class ScaleCalibrator
    {
        public async Task<ScaleInfo> CalibrateAsync(EnhancedImage image, ScaleReference reference, CancellationToken ct)
            => await Task.FromResult(new ScaleInfo { PixelsToMm = 1.0 });

        public async Task<ScaleInfo> CalibrateFloorPlanAsync(EnhancedImage image, KnownDimension known, CancellationToken ct)
            => await Task.FromResult(new ScaleInfo { PixelsToMm = 1.0 });
    }

    internal class BIMElementGenerator
    {
        public async Task<List<GeneratedBIMElement>> GenerateElementsAsync(
            List<ExtractedGeometry> geometries, int targetLOD, CancellationToken ct)
            => await Task.FromResult(new List<GeneratedBIMElement>());

        public async Task<List<GeneratedBIMElement>> GenerateFromSketchAsync(
            List<RegularizedElement> elements, int targetLOD, CancellationToken ct)
            => await Task.FromResult(new List<GeneratedBIMElement>());

        public async Task<GeneratedWallElement> GenerateWallAsync(DetectedWall wall, int lod, CancellationToken ct)
            => await Task.FromResult(new GeneratedWallElement { Id = wall.Id });

        public async Task<GeneratedDoorElement> GenerateDoorAsync(DetectedDoor door, int lod, CancellationToken ct)
            => await Task.FromResult(new GeneratedDoorElement { Id = door.Id });

        public async Task<GeneratedWindowElement> GenerateWindowAsync(DetectedWindow window, int lod, CancellationToken ct)
            => await Task.FromResult(new GeneratedWindowElement { Id = window.Id });

        public async Task<GeneratedRoomElement> GenerateRoomAsync(DetectedRoom room, CancellationToken ct)
            => await Task.FromResult(new GeneratedRoomElement { Id = room.Id });
    }

    #endregion

    #region Data Models

    public class ImageToBIMResult
    {
        public string SourceImagePath { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ScaleInfo DetectedScale { get; set; }
        public List<GeneratedBIMElement> GeneratedElements { get; set; } = new List<GeneratedBIMElement>();
    }

    public class FloorPlanProcessingResult
    {
        public string SourceImagePath { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ScaleInfo Scale { get; set; }
        public List<DetectedWall> DetectedWalls { get; set; } = new List<DetectedWall>();
        public List<DetectedDoor> DetectedDoors { get; set; } = new List<DetectedDoor>();
        public List<DetectedWindow> DetectedWindows { get; set; } = new List<DetectedWindow>();
        public List<DetectedRoom> DetectedRooms { get; set; } = new List<DetectedRoom>();
        public GeneratedLevel GeneratedLevel { get; set; }
    }

    public class SketchProcessingResult
    {
        public string SourceImagePath { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int RecognizedStrokes { get; set; }
        public int InterpretedShapes { get; set; }
        public List<GeneratedBIMElement> GeneratedElements { get; set; } = new List<GeneratedBIMElement>();
    }

    public class PhotoProcessingResult
    {
        public string SourceImagePath { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public PerspectiveInfo PerspectiveInfo { get; set; }
        public List<ClassifiedFacadeElement> ClassifiedElements { get; set; } = new List<ClassifiedFacadeElement>();
        public GeneratedFacade GeneratedFacade { get; set; }
    }

    public class MultiImageProcessingResult
    {
        public List<string> SourceImagePaths { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<CameraPose> EstimatedPoses { get; set; }
        public PointCloud PointCloud { get; set; }
        public GeneratedMesh GeneratedMesh { get; set; }
    }

    public class ImageProcessingProgress
    {
        public string Stage { get; set; }
        public int Percentage { get; set; }
    }

    public class ImageProcessingOptions
    {
        public DetectionMode DetectionMode { get; set; } = DetectionMode.Auto;
        public ScaleReference ScaleReference { get; set; }
        public int TargetLOD { get; set; } = 200;
    }

    public class FloorPlanOptions
    {
        public KnownDimension KnownDimension { get; set; }
        public int TargetLOD { get; set; } = 200;
        public string LevelName { get; set; }
        public double Elevation { get; set; } = 0;
        public double FloorToFloorHeight { get; set; } = 3000;
    }

    public class SketchOptions
    {
        public SketchContext Context { get; set; } = new SketchContext();
        public int TargetLOD { get; set; } = 200;
    }

    public class PhotoProcessingOptions
    {
        public double EstimatedFacadeWidth { get; set; }
        public double EstimatedFacadeHeight { get; set; }
    }

    public class MultiImageOptions
    {
        public double PointDensity { get; set; } = 1.0;
        public bool GenerateMesh { get; set; } = true;
    }

    public class SketchContext
    {
        public SketchMode Mode { get; set; } = SketchMode.FloorPlan;
        public string Description { get; set; }
    }

    public enum SketchMode { FloorPlan, Elevation, Section, MEP, Detail }
    public enum DetectionMode { Auto, Manual, FloorPlan, Elevation }
    public enum NoiseReductionLevel { None, Low, Medium, High }
    public enum ShapeType { Line, Rectangle, Circle, Arc, Polygon }
    public enum StrokeType { Open, Closed, Annotation }
    public enum FacadeElementType { Window, Door, Balcony, Column, Unknown }
    public enum PerspectiveType { Orthographic, OnePoint, TwoPoint, ThreePoint }
    public enum SwingDirection { Inward, Outward, Left, Right }

    public class Point2D { public double X { get; set; } public double Y { get; set; } public Point2D() { } public Point2D(double x, double y) { X = x; Y = y; } }
    public class Rectangle { public int X { get; set; } public int Y { get; set; } public int Width { get; set; } public int Height { get; set; } }
    public class Color { public byte R { get; set; } public byte G { get; set; } public byte B { get; set; } }
    public class Matrix3x3 { public static Matrix3x3 Identity => new Matrix3x3(); }

    public class ScaleInfo { public double PixelsToMm { get; set; } }
    public class ScaleReference { public string Type { get; set; } public double Value { get; set; } }
    public class KnownDimension { public double Value { get; set; } public string Unit { get; set; } }
    public class ImageData { public string Path { get; set; } public int Width { get; set; } public int Height { get; set; } }
    public class EnhancedImage { public ImageData SourceData { get; set; } }
    public class CleanedSketch { public ImageData SourceData { get; set; } }
    public class ProcessedImage { public string Path { get; set; } public ImageData Data { get; set; } public List<ImageFeature> Features { get; set; } }

    public class ImageProcessingProfile
    {
        public string Name { get; set; }
        public double ContrastEnhancement { get; set; }
        public int EdgeDetectionThreshold { get; set; }
        public NoiseReductionLevel NoiseReduction { get; set; }
        public bool Binarization { get; set; }
    }

    public class ElementTemplate
    {
        public string Type { get; set; }
        public double DefaultWidth { get; set; }
        public double DefaultHeight { get; set; }
    }

    public class DetectedElement { public string Id { get; set; } public string Type { get; set; } }
    public class DetectedLine { public Point2D Start { get; set; } public Point2D End { get; set; } public double Angle { get; set; } public double Thickness { get; set; } public double Confidence { get; set; } }
    public class DetectedRectangle { public Rectangle Bounds { get; set; } public byte[] Region { get; set; } public double Confidence { get; set; } }

    public class LineSegment { public Point2D Start { get; set; } public Point2D End { get; set; } public double Thickness { get; set; } public double Confidence { get; set; } }

    public class DetectedWall
    {
        public string Id { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public double Thickness { get; set; }
        public double Confidence { get; set; }
        public string WallType { get; set; }
    }

    public class DetectedDoor
    {
        public string Id { get; set; }
        public Point2D Location { get; set; }
        public double Width { get; set; }
        public SwingDirection SwingDirection { get; set; }
        public string HostWallId { get; set; }
        public string DoorType { get; set; }
        public double Confidence { get; set; }
    }

    public class DetectedWindow
    {
        public string Id { get; set; }
        public Point2D Location { get; set; }
        public double Width { get; set; }
        public double SillHeight { get; set; }
        public string HostWallId { get; set; }
        public string WindowType { get; set; }
        public double Confidence { get; set; }
    }

    public class DetectedRoom
    {
        public string Id { get; set; }
        public List<Point2D> Boundary { get; set; }
        public double Area { get; set; }
        public Point2D Centroid { get; set; }
        public List<string> BoundingWallIds { get; set; }
        public double Confidence { get; set; }
        public string Label { get; set; }
        public string RoomType { get; set; }
        public double? LabeledArea { get; set; }
    }

    public class SymbolMatch { public Point2D Center { get; set; } public double Width { get; set; } public string SubType { get; set; } public double Confidence { get; set; } }
    public class DimensionReading { public Point2D Location { get; set; } public double Value { get; set; } public string Unit { get; set; } }
    public class TextRegion { public Point2D Center { get; set; } public string Text { get; set; } }
    public class ExtractedGeometry { public string Type { get; set; } public object Parameters { get; set; } }
    public class EnclosedRegion { public List<Point2D> Boundary { get; set; } public double Area { get; set; } public Point2D Centroid { get; set; } public List<DetectedWall> BoundingWalls { get; set; } public double Confidence { get; set; } }
    public class ConnectedComponent { public List<Point2D> Points { get; set; } public bool IsClosed { get; set; } public double Length { get; set; } }
    public class ImageFeature { public Point2D Location { get; set; } public double[] Descriptor { get; set; } }
    public class FeaturePairMatch { public Point2D Point1 { get; set; } public Point2D Point2 { get; set; } public double Score { get; set; } }
    public class FeatureMatch { public int Image1Index { get; set; } public int Image2Index { get; set; } public Point2D Point1 { get; set; } public Point2D Point2 { get; set; } public double Score { get; set; } }

    public class SketchStroke
    {
        public string Id { get; set; }
        public List<Point2D> Points { get; set; }
        public bool IsClosed { get; set; }
        public double Length { get; set; }
        public double Curvature { get; set; }
        public StrokeType StrokeType { get; set; }
    }

    public class RecognizedShape
    {
        public string SourceStrokeId { get; set; }
        public ShapeType ShapeType { get; set; }
        public double Confidence { get; set; }
        public object Parameters { get; set; }
    }

    public class InterpretedElement
    {
        public string SourceShapeId { get; set; }
        public ShapeType ShapeType { get; set; }
        public string ElementType { get; set; }
        public object Parameters { get; set; }
    }

    public class RegularizedElement
    {
        public string SourceElementId { get; set; }
        public string ElementType { get; set; }
        public object Geometry { get; set; }
    }

    public class LineFitParameters { public double Slope { get; set; } public double Intercept { get; set; } }
    public class RectangleFitParameters { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }
    public class CircleFitParameters { public double CenterX { get; set; } public double CenterY { get; set; } public double Radius { get; set; } }

    public class PerspectiveInfo
    {
        public Point2D HorizontalVanishingPoint { get; set; }
        public Point2D VerticalVanishingPoint { get; set; }
        public PerspectiveType PerspectiveType { get; set; }
        public double EstimatedFocalLength { get; set; }
        public double EstimatedViewAngle { get; set; }
    }

    public class FacadeElement
    {
        public string Id { get; set; }
        public Rectangle Bounds { get; set; }
        public byte[] PixelRegion { get; set; }
        public double Confidence { get; set; }
    }

    public class ClassifiedFacadeElement : FacadeElement
    {
        public FacadeElementType ElementType { get; set; }
        public double EstimatedWidth { get; set; }
        public double EstimatedHeight { get; set; }
    }

    public class CameraPose
    {
        public int ImageIndex { get; set; }
        public Point3D Position { get; set; }
        public Matrix3x3 Rotation { get; set; }
    }

    public class PointCloud
    {
        public List<PointCloudPoint> Points { get; set; } = new List<PointCloudPoint>();
    }

    public class PointCloudPoint
    {
        public Point3D Position { get; set; }
        public Color Color { get; set; }
        public double Confidence { get; set; }
    }

    public class GeneratedLevel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double Elevation { get; set; }
        public double FloorToFloorHeight { get; set; }
        public List<GeneratedWallElement> Walls { get; set; } = new List<GeneratedWallElement>();
        public List<GeneratedDoorElement> Doors { get; set; } = new List<GeneratedDoorElement>();
        public List<GeneratedWindowElement> Windows { get; set; } = new List<GeneratedWindowElement>();
        public List<GeneratedRoomElement> Rooms { get; set; } = new List<GeneratedRoomElement>();
    }

    public class GeneratedFacade
    {
        public string Id { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<GeneratedWindow> Windows { get; set; } = new List<GeneratedWindow>();
        public List<GeneratedDoor> Doors { get; set; } = new List<GeneratedDoor>();
    }

    public class GeneratedMesh
    {
        public List<Point3D> Vertices { get; set; } = new List<Point3D>();
        public List<Color> VertexColors { get; set; } = new List<Color>();
        public List<int[]> Faces { get; set; } = new List<int[]>();
    }

    public class GeneratedBIMElement { public string Id { get; set; } public string ElementType { get; set; } }
    public class GeneratedWallElement { public string Id { get; set; } }
    public class GeneratedDoorElement { public string Id { get; set; } }
    public class GeneratedWindowElement { public string Id { get; set; } }
    public class GeneratedRoomElement { public string Id { get; set; } }
    public class GeneratedWindow { public string Id { get; set; } public double Width { get; set; } public double Height { get; set; } public Rectangle Position { get; set; } }
    public class GeneratedDoor { public string Id { get; set; } public double Width { get; set; } public double Height { get; set; } public Rectangle Position { get; set; } }

    #endregion
}
