// ============================================================================
// StingBIM.AI.Creation - Element Recognition Engine
// AI-based detection of building elements from CAD drawings and images
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Recognition
{
    /// <summary>
    /// AI-powered recognition engine for detecting building elements
    /// from CAD drawings, images, PDFs, and scanned documents.
    /// </summary>
    public class ElementRecognitionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly RecognitionSettings _settings;
        private readonly Dictionary<string, ElementPattern> _elementPatterns;
        private readonly Dictionary<string, SymbolTemplate> _symbolLibrary;
        private readonly Dictionary<string, DimensionPattern> _dimensionPatterns;
        private readonly PatternMatcher _patternMatcher;
        private readonly SymbolRecognizer _symbolRecognizer;
        private readonly TextExtractor _textExtractor;
        private readonly GeometryAnalyzer _geometryAnalyzer;

        public ElementRecognitionEngine(RecognitionSettings settings = null)
        {
            _settings = settings ?? new RecognitionSettings();
            _elementPatterns = InitializeElementPatterns();
            _symbolLibrary = InitializeSymbolLibrary();
            _dimensionPatterns = InitializeDimensionPatterns();
            _patternMatcher = new PatternMatcher(_settings);
            _symbolRecognizer = new SymbolRecognizer(_symbolLibrary);
            _textExtractor = new TextExtractor();
            _geometryAnalyzer = new GeometryAnalyzer();

            Logger.Info("ElementRecognitionEngine initialized with {0} element patterns, {1} symbols",
                _elementPatterns.Count, _symbolLibrary.Count);
        }

        #region Main Recognition Methods

        /// <summary>
        /// Performs full recognition analysis on a drawing.
        /// </summary>
        public async Task<RecognitionResult> RecognizeElementsAsync(
            DrawingInput input,
            RecognitionOptions options = null,
            IProgress<RecognitionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting element recognition for: {0}", input.SourceFile ?? "geometry input");
            options = options ?? new RecognitionOptions();

            var result = new RecognitionResult
            {
                SourceFile = input.SourceFile,
                RecognizedElements = new List<RecognizedElement>(),
                RecognizedSymbols = new List<RecognizedSymbol>(),
                RecognizedDimensions = new List<RecognizedDimension>(),
                RecognizedText = new List<RecognizedText>(),
                Warnings = new List<string>()
            };

            try
            {
                var totalSteps = 6;
                var currentStep = 0;

                // Step 1: Preprocess input
                progress?.Report(new RecognitionProgress
                {
                    Stage = "Preprocessing input",
                    Percentage = (++currentStep * 100) / totalSteps
                });
                var preprocessedData = await PreprocessInputAsync(input, cancellationToken);

                // Step 2: Extract geometry primitives
                progress?.Report(new RecognitionProgress
                {
                    Stage = "Extracting geometry",
                    Percentage = (++currentStep * 100) / totalSteps
                });
                var primitives = await ExtractGeometryPrimitivesAsync(preprocessedData, cancellationToken);

                // Step 3: Recognize walls
                if (options.RecognizeWalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new RecognitionProgress
                    {
                        Stage = "Recognizing walls",
                        Percentage = (++currentStep * 100) / totalSteps
                    });
                    var walls = await RecognizeWallsAsync(primitives, options);
                    result.RecognizedElements.AddRange(walls);
                }

                // Step 4: Recognize openings (doors, windows)
                if (options.RecognizeOpenings)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new RecognitionProgress
                    {
                        Stage = "Recognizing openings",
                        Percentage = (++currentStep * 100) / totalSteps
                    });
                    var openings = await RecognizeOpeningsAsync(primitives, result.RecognizedElements, options);
                    result.RecognizedElements.AddRange(openings);
                }

                // Step 5: Recognize symbols and annotations
                if (options.RecognizeSymbols)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new RecognitionProgress
                    {
                        Stage = "Recognizing symbols",
                        Percentage = (++currentStep * 100) / totalSteps
                    });
                    var symbols = await RecognizeSymbolsAsync(primitives, options);
                    result.RecognizedSymbols.AddRange(symbols);
                }

                // Step 6: Extract dimensions and text
                if (options.ExtractDimensions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new RecognitionProgress
                    {
                        Stage = "Extracting dimensions",
                        Percentage = (++currentStep * 100) / totalSteps
                    });
                    var dimensions = await ExtractDimensionsAsync(preprocessedData, primitives, options);
                    result.RecognizedDimensions.AddRange(dimensions);

                    var text = await ExtractTextAsync(preprocessedData, options);
                    result.RecognizedText.AddRange(text);
                }

                // Apply dimension values to elements
                if (result.RecognizedDimensions.Any())
                {
                    await ApplyDimensionsToElementsAsync(result);
                }

                // Calculate confidence scores
                CalculateOverallConfidence(result);

                result.IsSuccess = true;
                result.ProcessedAt = DateTime.UtcNow;

                Logger.Info("Recognition complete: {0} elements, {1} symbols, {2} dimensions",
                    result.RecognizedElements.Count,
                    result.RecognizedSymbols.Count,
                    result.RecognizedDimensions.Count);

                progress?.Report(new RecognitionProgress
                {
                    Stage = "Complete",
                    Percentage = 100
                });
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Recognition cancelled");
                result.IsSuccess = false;
                result.ErrorMessage = "Operation cancelled";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Recognition failed");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Recognizes elements from an image (photo of drawing, scanned plan).
        /// </summary>
        public async Task<RecognitionResult> RecognizeFromImageAsync(
            string imagePath,
            ImageRecognitionOptions options = null,
            IProgress<RecognitionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting image recognition: {0}", imagePath);
            options = options ?? new ImageRecognitionOptions();

            var result = new RecognitionResult
            {
                SourceFile = imagePath,
                SourceType = SourceType.Image,
                RecognizedElements = new List<RecognizedElement>(),
                Warnings = new List<string>()
            };

            try
            {
                // Step 1: Image preprocessing
                progress?.Report(new RecognitionProgress { Stage = "Preprocessing image", Percentage = 10 });
                var processedImage = await PreprocessImageAsync(imagePath, options, cancellationToken);

                // Step 2: Line detection using Hough transform
                progress?.Report(new RecognitionProgress { Stage = "Detecting lines", Percentage = 25 });
                var detectedLines = await DetectLinesInImageAsync(processedImage, options);

                // Step 3: Contour detection for shapes
                progress?.Report(new RecognitionProgress { Stage = "Detecting shapes", Percentage = 40 });
                var detectedContours = await DetectContoursAsync(processedImage, options);

                // Step 4: OCR for text and dimensions
                progress?.Report(new RecognitionProgress { Stage = "Extracting text (OCR)", Percentage = 55 });
                var extractedText = await PerformOCRAsync(processedImage, options);
                result.RecognizedText = extractedText;

                // Step 5: Pattern matching for elements
                progress?.Report(new RecognitionProgress { Stage = "Matching patterns", Percentage = 70 });
                var elements = await MatchElementPatternsFromImageAsync(detectedLines, detectedContours, extractedText);
                result.RecognizedElements.AddRange(elements);

                // Step 6: Symbol detection
                progress?.Report(new RecognitionProgress { Stage = "Detecting symbols", Percentage = 85 });
                var symbols = await DetectSymbolsInImageAsync(processedImage, detectedContours);
                result.RecognizedSymbols = symbols;

                result.IsSuccess = true;
                result.ProcessedAt = DateTime.UtcNow;

                progress?.Report(new RecognitionProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("Image recognition complete: {0} elements detected", result.RecognizedElements.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Image recognition failed");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Recognizes elements from a PDF drawing.
        /// </summary>
        public async Task<RecognitionResult> RecognizeFromPDFAsync(
            string pdfPath,
            PDFRecognitionOptions options = null,
            IProgress<RecognitionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting PDF recognition: {0}", pdfPath);
            options = options ?? new PDFRecognitionOptions();

            var result = new RecognitionResult
            {
                SourceFile = pdfPath,
                SourceType = SourceType.PDF,
                RecognizedElements = new List<RecognizedElement>(),
                Warnings = new List<string>()
            };

            try
            {
                // Step 1: Extract vector data from PDF
                progress?.Report(new RecognitionProgress { Stage = "Extracting vectors from PDF", Percentage = 15 });
                var vectorData = await ExtractPDFVectorsAsync(pdfPath, options, cancellationToken);

                if (vectorData.HasVectorContent)
                {
                    // Vector PDF - direct geometry extraction
                    progress?.Report(new RecognitionProgress { Stage = "Processing vector geometry", Percentage = 40 });
                    var primitives = ConvertVectorsToPrimitives(vectorData);

                    var recognitionOptions = new RecognitionOptions
                    {
                        RecognizeWalls = true,
                        RecognizeOpenings = true,
                        RecognizeSymbols = true,
                        ExtractDimensions = true
                    };

                    var elements = await RecognizeWallsAsync(primitives, recognitionOptions);
                    result.RecognizedElements.AddRange(elements);

                    var openings = await RecognizeOpeningsAsync(primitives, elements, recognitionOptions);
                    result.RecognizedElements.AddRange(openings);
                }
                else
                {
                    // Raster PDF - use image recognition
                    progress?.Report(new RecognitionProgress { Stage = "Processing raster pages", Percentage = 40 });
                    result.Warnings.Add("PDF contains raster content - using image recognition");

                    foreach (var page in vectorData.Pages)
                    {
                        var pageImage = await RenderPDFPageToImageAsync(pdfPath, page.PageNumber, options.RenderDPI);
                        var imageResult = await RecognizeFromImageAsync(pageImage, new ImageRecognitionOptions());

                        result.RecognizedElements.AddRange(imageResult.RecognizedElements);
                        result.RecognizedSymbols.AddRange(imageResult.RecognizedSymbols);
                    }
                }

                // Extract text layers
                progress?.Report(new RecognitionProgress { Stage = "Extracting text", Percentage = 75 });
                result.RecognizedText = await ExtractPDFTextAsync(pdfPath, options);

                // Extract dimensions from text
                progress?.Report(new RecognitionProgress { Stage = "Parsing dimensions", Percentage = 90 });
                result.RecognizedDimensions = ParseDimensionsFromText(result.RecognizedText);

                result.IsSuccess = true;
                result.ProcessedAt = DateTime.UtcNow;

                progress?.Report(new RecognitionProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("PDF recognition complete: {0} elements detected", result.RecognizedElements.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PDF recognition failed");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Wall Recognition

        private async Task<List<RecognizedElement>> RecognizeWallsAsync(
            GeometryPrimitives primitives,
            RecognitionOptions options)
        {
            var walls = new List<RecognizedElement>();

            // Strategy 1: Parallel line pairs (most common)
            var parallelWalls = await RecognizeParallelLineWallsAsync(primitives.Lines, options);
            walls.AddRange(parallelWalls);

            // Strategy 2: Polyline walls
            var polylineWalls = await RecognizePolylineWallsAsync(primitives.Polylines, options);
            walls.AddRange(polylineWalls);

            // Strategy 3: Hatched/filled regions
            var hatchedWalls = await RecognizeHatchedWallsAsync(primitives.FilledRegions, options);
            walls.AddRange(hatchedWalls);

            // Strategy 4: Arc/curved walls
            var curvedWalls = await RecognizeCurvedWallsAsync(primitives.Arcs, primitives.Splines, options);
            walls.AddRange(curvedWalls);

            // Remove duplicates and merge overlapping walls
            walls = MergeOverlappingWalls(walls);

            // Classify wall types based on thickness and context
            await ClassifyWallTypesAsync(walls, primitives);

            Logger.Debug("Recognized {0} walls using multiple strategies", walls.Count);
            return walls;
        }

        private async Task<List<RecognizedElement>> RecognizeParallelLineWallsAsync(
            List<LinePrimitive> lines,
            RecognitionOptions options)
        {
            var walls = new List<RecognizedElement>();
            var usedLines = new HashSet<int>();
            var tolerance = options.ParallelTolerance;
            var minWallThickness = options.MinWallThickness;
            var maxWallThickness = options.MaxWallThickness;

            for (int i = 0; i < lines.Count; i++)
            {
                if (usedLines.Contains(i)) continue;

                var line1 = lines[i];

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (usedLines.Contains(j)) continue;

                    var line2 = lines[j];

                    // Check if lines are parallel
                    if (!AreParallel(line1, line2, tolerance)) continue;

                    // Calculate perpendicular distance (wall thickness)
                    var distance = PerpendicularDistance(line1, line2);

                    // Check if distance is within wall thickness range
                    if (distance < minWallThickness || distance > maxWallThickness) continue;

                    // Check if lines overlap in length
                    var overlap = CalculateOverlap(line1, line2);
                    if (overlap < options.MinOverlapRatio) continue;

                    // Create wall element
                    var wall = new RecognizedElement
                    {
                        ElementType = RecognizedElementType.Wall,
                        Category = "Walls",
                        Confidence = CalculateWallConfidence(line1, line2, distance),
                        Geometry = CreateWallGeometry(line1, line2),
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness"] = Math.Round(distance, 1),
                            ["Length"] = Math.Round(overlap, 1),
                            ["RecognitionMethod"] = "ParallelLines"
                        }
                    };

                    // Infer wall type from thickness
                    wall.InferredType = InferWallType(distance);

                    walls.Add(wall);
                    usedLines.Add(i);
                    usedLines.Add(j);
                    break;
                }
            }

            return await Task.FromResult(walls);
        }

        private async Task<List<RecognizedElement>> RecognizePolylineWallsAsync(
            List<PolylinePrimitive> polylines,
            RecognitionOptions options)
        {
            var walls = new List<RecognizedElement>();

            foreach (var polyline in polylines)
            {
                // Check if polyline looks like a wall (closed or long)
                if (polyline.Points.Count < 2) continue;

                // For closed polylines, check if it's a room boundary
                if (polyline.IsClosed && IsRoomBoundary(polyline))
                {
                    // Create walls from each segment
                    for (int i = 0; i < polyline.Points.Count; i++)
                    {
                        var start = polyline.Points[i];
                        var end = polyline.Points[(i + 1) % polyline.Points.Count];

                        var wall = new RecognizedElement
                        {
                            ElementType = RecognizedElementType.Wall,
                            Category = "Walls",
                            Confidence = 0.75,
                            Geometry = new ElementGeometry
                            {
                                StartPoint = new Point3D(start.X, start.Y, 0),
                                EndPoint = new Point3D(end.X, end.Y, 0)
                            },
                            Properties = new Dictionary<string, object>
                            {
                                ["Thickness"] = options.DefaultWallThickness,
                                ["RecognitionMethod"] = "PolylineBoundary"
                            }
                        };

                        walls.Add(wall);
                    }
                }
                else if (!polyline.IsClosed)
                {
                    // Open polyline - could be a centerline or single wall face
                    var wall = new RecognizedElement
                    {
                        ElementType = RecognizedElementType.Wall,
                        Category = "Walls",
                        Confidence = 0.6,
                        Geometry = new ElementGeometry
                        {
                            Points = polyline.Points.Select(p => new Point3D(p.X, p.Y, 0)).ToList()
                        },
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness"] = options.DefaultWallThickness,
                            ["RecognitionMethod"] = "PolylineCenterline"
                        }
                    };

                    walls.Add(wall);
                }
            }

            return await Task.FromResult(walls);
        }

        private async Task<List<RecognizedElement>> RecognizeHatchedWallsAsync(
            List<FilledRegionPrimitive> regions,
            RecognitionOptions options)
        {
            var walls = new List<RecognizedElement>();

            foreach (var region in regions)
            {
                // Check if hatch pattern suggests a wall material
                if (!IsWallHatchPattern(region.HatchPattern)) continue;

                // Analyze region shape - walls are typically elongated rectangles
                var bounds = CalculateBounds(region.BoundaryPoints);
                var aspectRatio = bounds.Length / bounds.Width;

                if (aspectRatio < 3.0) continue; // Not elongated enough for a wall

                var wall = new RecognizedElement
                {
                    ElementType = RecognizedElementType.Wall,
                    Category = "Walls",
                    Confidence = 0.85,
                    Geometry = new ElementGeometry
                    {
                        Points = region.BoundaryPoints.Select(p => new Point3D(p.X, p.Y, 0)).ToList()
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["Thickness"] = Math.Round(bounds.Width, 1),
                        ["Length"] = Math.Round(bounds.Length, 1),
                        ["HatchPattern"] = region.HatchPattern,
                        ["RecognitionMethod"] = "HatchedRegion"
                    }
                };

                // Infer material from hatch pattern
                wall.InferredMaterial = InferMaterialFromHatch(region.HatchPattern);
                wall.InferredType = InferWallType(bounds.Width);

                walls.Add(wall);
            }

            return await Task.FromResult(walls);
        }

        private async Task<List<RecognizedElement>> RecognizeCurvedWallsAsync(
            List<ArcPrimitive> arcs,
            List<SplinePrimitive> splines,
            RecognitionOptions options)
        {
            var walls = new List<RecognizedElement>();

            // Process arc pairs (parallel arcs = curved wall)
            for (int i = 0; i < arcs.Count; i++)
            {
                for (int j = i + 1; j < arcs.Count; j++)
                {
                    var arc1 = arcs[i];
                    var arc2 = arcs[j];

                    // Check if arcs are concentric (same center)
                    var centerDistance = Distance(arc1.Center, arc2.Center);
                    if (centerDistance > options.ConcentricTolerance) continue;

                    // Calculate radial difference (wall thickness)
                    var thickness = Math.Abs(arc1.Radius - arc2.Radius);
                    if (thickness < options.MinWallThickness || thickness > options.MaxWallThickness) continue;

                    var wall = new RecognizedElement
                    {
                        ElementType = RecognizedElementType.Wall,
                        Category = "Walls",
                        SubCategory = "CurvedWall",
                        Confidence = 0.8,
                        Geometry = new ElementGeometry
                        {
                            Center = new Point3D(arc1.Center.X, arc1.Center.Y, 0),
                            InnerRadius = Math.Min(arc1.Radius, arc2.Radius),
                            OuterRadius = Math.Max(arc1.Radius, arc2.Radius),
                            StartAngle = arc1.StartAngle,
                            EndAngle = arc1.EndAngle
                        },
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness"] = Math.Round(thickness, 1),
                            ["Radius"] = Math.Round((arc1.Radius + arc2.Radius) / 2, 1),
                            ["RecognitionMethod"] = "ConcentricArcs"
                        }
                    };

                    walls.Add(wall);
                }
            }

            return await Task.FromResult(walls);
        }

        #endregion

        #region Opening Recognition (Doors, Windows)

        private async Task<List<RecognizedElement>> RecognizeOpeningsAsync(
            GeometryPrimitives primitives,
            List<RecognizedElement> walls,
            RecognitionOptions options)
        {
            var openings = new List<RecognizedElement>();

            // Detect door swings (arcs at wall gaps)
            var doors = await RecognizeDoorsAsync(primitives, walls, options);
            openings.AddRange(doors);

            // Detect windows (gaps in walls with specific patterns)
            var windows = await RecognizeWindowsAsync(primitives, walls, options);
            openings.AddRange(windows);

            return openings;
        }

        private async Task<List<RecognizedElement>> RecognizeDoorsAsync(
            GeometryPrimitives primitives,
            List<RecognizedElement> walls,
            RecognitionOptions options)
        {
            var doors = new List<RecognizedElement>();

            // Look for door swing arcs
            foreach (var arc in primitives.Arcs)
            {
                // Door swings are typically 90-degree arcs
                var sweepAngle = Math.Abs(arc.EndAngle - arc.StartAngle);
                if (sweepAngle < 80 || sweepAngle > 100) continue;

                // Radius should be reasonable door width
                if (arc.Radius < 600 || arc.Radius > 1200) continue;

                // Check if arc is near a wall
                var nearestWall = FindNearestWall(arc.Center, walls);
                if (nearestWall == null) continue;

                var door = new RecognizedElement
                {
                    ElementType = RecognizedElementType.Door,
                    Category = "Doors",
                    Confidence = 0.85,
                    Geometry = new ElementGeometry
                    {
                        Center = new Point3D(arc.Center.X, arc.Center.Y, 0),
                        Width = arc.Radius,
                        Height = 2100 // Standard door height
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["Width"] = Math.Round(arc.Radius, 0),
                        ["SwingAngle"] = Math.Round(sweepAngle, 0),
                        ["SwingDirection"] = DetermineSwingDirection(arc),
                        ["RecognitionMethod"] = "DoorSwingArc"
                    },
                    HostElement = nearestWall
                };

                // Infer door type from width
                door.InferredType = InferDoorType(arc.Radius);

                doors.Add(door);
            }

            // Look for door symbols/blocks
            var doorSymbols = primitives.Blocks.Where(b =>
                b.Name.ToLower().Contains("door") ||
                _symbolLibrary.Values.Any(s => s.SymbolType == SymbolType.Door && MatchesSymbol(b, s)));

            foreach (var symbol in doorSymbols)
            {
                var door = new RecognizedElement
                {
                    ElementType = RecognizedElementType.Door,
                    Category = "Doors",
                    Confidence = 0.95,
                    Geometry = new ElementGeometry
                    {
                        Center = new Point3D(symbol.InsertionPoint.X, symbol.InsertionPoint.Y, 0),
                        Rotation = symbol.Rotation
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["BlockName"] = symbol.Name,
                        ["RecognitionMethod"] = "DoorBlock"
                    }
                };

                doors.Add(door);
            }

            return await Task.FromResult(doors);
        }

        private async Task<List<RecognizedElement>> RecognizeWindowsAsync(
            GeometryPrimitives primitives,
            List<RecognizedElement> walls,
            RecognitionOptions options)
        {
            var windows = new List<RecognizedElement>();

            // Look for window patterns (parallel lines perpendicular to wall)
            foreach (var wall in walls)
            {
                // Find gaps in wall
                var gaps = FindWallGaps(wall, primitives);

                foreach (var gap in gaps)
                {
                    // Check for window indicators (parallel lines in gap)
                    var windowLines = FindWindowLinesInGap(gap, primitives.Lines);

                    if (windowLines.Count >= 2)
                    {
                        var window = new RecognizedElement
                        {
                            ElementType = RecognizedElementType.Window,
                            Category = "Windows",
                            Confidence = 0.8,
                            Geometry = new ElementGeometry
                            {
                                StartPoint = gap.StartPoint,
                                EndPoint = gap.EndPoint,
                                Width = gap.Width,
                                Height = 1200 // Default window height
                            },
                            Properties = new Dictionary<string, object>
                            {
                                ["Width"] = Math.Round(gap.Width, 0),
                                ["RecognitionMethod"] = "WallGapWithLines"
                            },
                            HostElement = wall
                        };

                        // Infer window type
                        window.InferredType = InferWindowType(gap.Width);

                        windows.Add(window);
                    }
                }
            }

            // Look for window symbols/blocks
            var windowSymbols = primitives.Blocks.Where(b =>
                b.Name.ToLower().Contains("window") ||
                _symbolLibrary.Values.Any(s => s.SymbolType == SymbolType.Window && MatchesSymbol(b, s)));

            foreach (var symbol in windowSymbols)
            {
                var window = new RecognizedElement
                {
                    ElementType = RecognizedElementType.Window,
                    Category = "Windows",
                    Confidence = 0.95,
                    Geometry = new ElementGeometry
                    {
                        Center = new Point3D(symbol.InsertionPoint.X, symbol.InsertionPoint.Y, 0),
                        Rotation = symbol.Rotation
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["BlockName"] = symbol.Name,
                        ["RecognitionMethod"] = "WindowBlock"
                    }
                };

                windows.Add(window);
            }

            return await Task.FromResult(windows);
        }

        #endregion

        #region Symbol Recognition

        private async Task<List<RecognizedSymbol>> RecognizeSymbolsAsync(
            GeometryPrimitives primitives,
            RecognitionOptions options)
        {
            var symbols = new List<RecognizedSymbol>();

            // Recognize blocks as symbols
            foreach (var block in primitives.Blocks)
            {
                var matchedSymbol = _symbolRecognizer.MatchSymbol(block);
                if (matchedSymbol != null)
                {
                    symbols.Add(new RecognizedSymbol
                    {
                        SymbolType = matchedSymbol.SymbolType,
                        Name = matchedSymbol.Name,
                        Position = new Point3D(block.InsertionPoint.X, block.InsertionPoint.Y, 0),
                        Rotation = block.Rotation,
                        Scale = block.Scale,
                        Confidence = matchedSymbol.Confidence,
                        Properties = ExtractSymbolProperties(block, matchedSymbol)
                    });
                }
            }

            // Recognize common patterns as symbols
            var patternSymbols = await RecognizePatternSymbolsAsync(primitives);
            symbols.AddRange(patternSymbols);

            return symbols;
        }

        private async Task<List<RecognizedSymbol>> RecognizePatternSymbolsAsync(GeometryPrimitives primitives)
        {
            var symbols = new List<RecognizedSymbol>();

            // North arrow detection
            var northArrows = DetectNorthArrows(primitives);
            symbols.AddRange(northArrows);

            // Level markers
            var levelMarkers = DetectLevelMarkers(primitives);
            symbols.AddRange(levelMarkers);

            // Grid bubbles
            var gridBubbles = DetectGridBubbles(primitives);
            symbols.AddRange(gridBubbles);

            // Section marks
            var sectionMarks = DetectSectionMarks(primitives);
            symbols.AddRange(sectionMarks);

            // Detail callouts
            var detailCallouts = DetectDetailCallouts(primitives);
            symbols.AddRange(detailCallouts);

            return await Task.FromResult(symbols);
        }

        private List<RecognizedSymbol> DetectNorthArrows(GeometryPrimitives primitives)
        {
            var northArrows = new List<RecognizedSymbol>();

            // Look for arrow-like shapes with "N" text nearby
            foreach (var block in primitives.Blocks)
            {
                if (block.Name.ToLower().Contains("north") ||
                    block.Name.ToLower().Contains("arrow"))
                {
                    northArrows.Add(new RecognizedSymbol
                    {
                        SymbolType = SymbolType.NorthArrow,
                        Name = "North Arrow",
                        Position = new Point3D(block.InsertionPoint.X, block.InsertionPoint.Y, 0),
                        Rotation = block.Rotation,
                        Confidence = 0.9
                    });
                }
            }

            return northArrows;
        }

        private List<RecognizedSymbol> DetectLevelMarkers(GeometryPrimitives primitives)
        {
            var markers = new List<RecognizedSymbol>();

            // Look for level datum patterns (triangle with horizontal line)
            foreach (var text in primitives.Texts)
            {
                // Check for level notation patterns like "+0.000", "FFL", "SSL"
                if (IsLevelNotation(text.Content))
                {
                    markers.Add(new RecognizedSymbol
                    {
                        SymbolType = SymbolType.LevelMarker,
                        Name = "Level Marker",
                        Position = new Point3D(text.Position.X, text.Position.Y, 0),
                        Confidence = 0.85,
                        Properties = new Dictionary<string, object>
                        {
                            ["LevelValue"] = ParseLevelValue(text.Content),
                            ["LevelText"] = text.Content
                        }
                    });
                }
            }

            return markers;
        }

        private List<RecognizedSymbol> DetectGridBubbles(GeometryPrimitives primitives)
        {
            var bubbles = new List<RecognizedSymbol>();

            // Grid bubbles are circles with text (A, B, C or 1, 2, 3)
            foreach (var circle in primitives.Circles)
            {
                // Check for grid-size circles
                if (circle.Radius < 100 || circle.Radius > 500) continue;

                // Look for text at center
                var centerText = primitives.Texts.FirstOrDefault(t =>
                    Distance(t.Position, circle.Center) < circle.Radius * 0.5);

                if (centerText != null && IsGridLabel(centerText.Content))
                {
                    bubbles.Add(new RecognizedSymbol
                    {
                        SymbolType = SymbolType.GridBubble,
                        Name = $"Grid {centerText.Content}",
                        Position = new Point3D(circle.Center.X, circle.Center.Y, 0),
                        Confidence = 0.9,
                        Properties = new Dictionary<string, object>
                        {
                            ["GridLabel"] = centerText.Content,
                            ["Radius"] = circle.Radius
                        }
                    });
                }
            }

            return bubbles;
        }

        private List<RecognizedSymbol> DetectSectionMarks(GeometryPrimitives primitives)
        {
            var marks = new List<RecognizedSymbol>();

            // Section marks are typically circles with arrows and section numbers
            foreach (var block in primitives.Blocks)
            {
                if (block.Name.ToLower().Contains("section"))
                {
                    marks.Add(new RecognizedSymbol
                    {
                        SymbolType = SymbolType.SectionMark,
                        Name = "Section Mark",
                        Position = new Point3D(block.InsertionPoint.X, block.InsertionPoint.Y, 0),
                        Rotation = block.Rotation,
                        Confidence = 0.85
                    });
                }
            }

            return marks;
        }

        private List<RecognizedSymbol> DetectDetailCallouts(GeometryPrimitives primitives)
        {
            var callouts = new List<RecognizedSymbol>();

            foreach (var block in primitives.Blocks)
            {
                if (block.Name.ToLower().Contains("detail") ||
                    block.Name.ToLower().Contains("callout"))
                {
                    callouts.Add(new RecognizedSymbol
                    {
                        SymbolType = SymbolType.DetailCallout,
                        Name = "Detail Callout",
                        Position = new Point3D(block.InsertionPoint.X, block.InsertionPoint.Y, 0),
                        Confidence = 0.85
                    });
                }
            }

            return callouts;
        }

        #endregion

        #region Dimension Extraction

        private async Task<List<RecognizedDimension>> ExtractDimensionsAsync(
            PreprocessedData data,
            GeometryPrimitives primitives,
            RecognitionOptions options)
        {
            var dimensions = new List<RecognizedDimension>();

            // Extract from CAD dimension entities
            foreach (var dim in primitives.Dimensions)
            {
                dimensions.Add(new RecognizedDimension
                {
                    DimensionType = dim.DimensionType,
                    Value = dim.Value,
                    Units = dim.Units ?? options.DefaultUnits,
                    StartPoint = new Point3D(dim.StartPoint.X, dim.StartPoint.Y, 0),
                    EndPoint = new Point3D(dim.EndPoint.X, dim.EndPoint.Y, 0),
                    TextPosition = new Point3D(dim.TextPosition.X, dim.TextPosition.Y, 0),
                    Confidence = 0.95
                });
            }

            // Extract from text patterns
            var textDimensions = await ExtractDimensionsFromTextAsync(primitives.Texts, primitives.Lines, options);
            dimensions.AddRange(textDimensions);

            return dimensions;
        }

        private async Task<List<RecognizedDimension>> ExtractDimensionsFromTextAsync(
            List<TextPrimitive> texts,
            List<LinePrimitive> lines,
            RecognitionOptions options)
        {
            var dimensions = new List<RecognizedDimension>();

            foreach (var text in texts)
            {
                // Try to parse dimension from text
                var parsedValue = ParseDimensionValue(text.Content, options.DefaultUnits);
                if (!parsedValue.HasValue) continue;

                // Find nearby dimension lines
                var nearbyLines = lines.Where(l =>
                    DistanceToLine(text.Position, l) < 100).ToList();

                if (nearbyLines.Any())
                {
                    var dimLine = nearbyLines.First();

                    dimensions.Add(new RecognizedDimension
                    {
                        DimensionType = DimensionPrimitiveType.Linear,
                        Value = parsedValue.Value,
                        Units = options.DefaultUnits,
                        StartPoint = new Point3D(dimLine.StartPoint.X, dimLine.StartPoint.Y, 0),
                        EndPoint = new Point3D(dimLine.EndPoint.X, dimLine.EndPoint.Y, 0),
                        TextPosition = new Point3D(text.Position.X, text.Position.Y, 0),
                        OriginalText = text.Content,
                        Confidence = 0.8
                    });
                }
            }

            return await Task.FromResult(dimensions);
        }

        private double? ParseDimensionValue(string text, string defaultUnits)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Remove spaces and common formatting
            var cleaned = text.Trim().Replace(" ", "").Replace(",", "");

            // Pattern: number followed by optional units
            var patterns = new[]
            {
                @"^(\d+\.?\d*)\s*(mm|m|cm|ft|in|'|"")?\s*$",  // 1000mm, 1.5m, etc.
                @"^(\d+)'[-\s]*(\d+)?\s*(\d+/\d+)?\s*""?\s*$", // 10'-6", 10' 6 1/2"
                @"^(\d+\.?\d*)\s*[xX×]\s*(\d+\.?\d*)\s*$"      // 1000x2000 (width x height)
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(cleaned, pattern);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, out var value))
                    {
                        // Convert to mm if needed
                        var unit = match.Groups.Count > 2 ? match.Groups[2].Value.ToLower() : defaultUnits;
                        return ConvertToMillimeters(value, unit);
                    }
                }
            }

            return null;
        }

        private double ConvertToMillimeters(double value, string fromUnit)
        {
            return fromUnit?.ToLower() switch
            {
                "m" => value * 1000,
                "cm" => value * 10,
                "mm" => value,
                "ft" or "'" => value * 304.8,
                "in" or "\"" => value * 25.4,
                _ => value // Assume mm if unknown
            };
        }

        private List<RecognizedDimension> ParseDimensionsFromText(List<RecognizedText> texts)
        {
            var dimensions = new List<RecognizedDimension>();

            foreach (var text in texts)
            {
                var value = ParseDimensionValue(text.Content, "mm");
                if (value.HasValue)
                {
                    dimensions.Add(new RecognizedDimension
                    {
                        DimensionType = DimensionPrimitiveType.Linear,
                        Value = value.Value,
                        Units = "mm",
                        TextPosition = text.Position,
                        OriginalText = text.Content,
                        Confidence = 0.7
                    });
                }
            }

            return dimensions;
        }

        #endregion

        #region Text Extraction

        private async Task<List<RecognizedText>> ExtractTextAsync(
            PreprocessedData data,
            RecognitionOptions options)
        {
            var texts = new List<RecognizedText>();

            foreach (var textPrimitive in data.Primitives.Texts)
            {
                texts.Add(new RecognizedText
                {
                    Content = textPrimitive.Content,
                    Position = new Point3D(textPrimitive.Position.X, textPrimitive.Position.Y, 0),
                    Height = textPrimitive.Height,
                    Rotation = textPrimitive.Rotation,
                    Style = textPrimitive.Style,
                    Confidence = 0.95
                });
            }

            return await Task.FromResult(texts);
        }

        private async Task<List<RecognizedText>> PerformOCRAsync(
            ProcessedImage image,
            ImageRecognitionOptions options)
        {
            var texts = new List<RecognizedText>();

            // OCR would be performed here using a library like Tesseract
            // For now, return placeholder
            Logger.Debug("OCR processing placeholder - would use Tesseract or similar");

            return await Task.FromResult(texts);
        }

        private async Task<List<RecognizedText>> ExtractPDFTextAsync(
            string pdfPath,
            PDFRecognitionOptions options)
        {
            var texts = new List<RecognizedText>();

            // PDF text extraction would be performed here
            Logger.Debug("PDF text extraction placeholder");

            return await Task.FromResult(texts);
        }

        #endregion

        #region Helper Methods

        private async Task<PreprocessedData> PreprocessInputAsync(
            DrawingInput input,
            CancellationToken cancellationToken)
        {
            var data = new PreprocessedData
            {
                SourceType = input.SourceType,
                Primitives = new GeometryPrimitives()
            };

            if (input.Geometry != null)
            {
                data.Primitives = input.Geometry;
            }

            return await Task.FromResult(data);
        }

        private async Task<GeometryPrimitives> ExtractGeometryPrimitivesAsync(
            PreprocessedData data,
            CancellationToken cancellationToken)
        {
            return await Task.FromResult(data.Primitives ?? new GeometryPrimitives());
        }

        private async Task<ProcessedImage> PreprocessImageAsync(
            string imagePath,
            ImageRecognitionOptions options,
            CancellationToken cancellationToken)
        {
            // Image preprocessing would happen here
            return await Task.FromResult(new ProcessedImage { Path = imagePath });
        }

        private async Task<List<LinePrimitive>> DetectLinesInImageAsync(
            ProcessedImage image,
            ImageRecognitionOptions options)
        {
            // Hough transform line detection would happen here
            return await Task.FromResult(new List<LinePrimitive>());
        }

        private async Task<List<ContourPrimitive>> DetectContoursAsync(
            ProcessedImage image,
            ImageRecognitionOptions options)
        {
            // Contour detection would happen here
            return await Task.FromResult(new List<ContourPrimitive>());
        }

        private async Task<List<RecognizedElement>> MatchElementPatternsFromImageAsync(
            List<LinePrimitive> lines,
            List<ContourPrimitive> contours,
            List<RecognizedText> texts)
        {
            // Pattern matching from image data
            return await Task.FromResult(new List<RecognizedElement>());
        }

        private async Task<List<RecognizedSymbol>> DetectSymbolsInImageAsync(
            ProcessedImage image,
            List<ContourPrimitive> contours)
        {
            return await Task.FromResult(new List<RecognizedSymbol>());
        }

        private async Task<PDFVectorData> ExtractPDFVectorsAsync(
            string pdfPath,
            PDFRecognitionOptions options,
            CancellationToken cancellationToken)
        {
            // PDF vector extraction would happen here
            return await Task.FromResult(new PDFVectorData());
        }

        private GeometryPrimitives ConvertVectorsToPrimitives(PDFVectorData vectorData)
        {
            return new GeometryPrimitives();
        }

        private async Task<string> RenderPDFPageToImageAsync(string pdfPath, int pageNumber, int dpi)
        {
            // PDF page rendering would happen here
            return await Task.FromResult($"{pdfPath}_page{pageNumber}.png");
        }

        private async Task ApplyDimensionsToElementsAsync(RecognitionResult result)
        {
            foreach (var dimension in result.RecognizedDimensions)
            {
                // Find element closest to dimension
                var nearestElement = result.RecognizedElements
                    .OrderBy(e => DistanceToElement(dimension, e))
                    .FirstOrDefault();

                if (nearestElement != null && DistanceToElement(dimension, nearestElement) < 200)
                {
                    // Apply dimension to element
                    if (dimension.DimensionType == DimensionPrimitiveType.Linear)
                    {
                        // Determine if it's length or thickness
                        if (nearestElement.ElementType == RecognizedElementType.Wall)
                        {
                            var existingThickness = nearestElement.Properties.ContainsKey("Thickness")
                                ? Convert.ToDouble(nearestElement.Properties["Thickness"])
                                : 0;

                            if (Math.Abs(dimension.Value - existingThickness) < 50)
                            {
                                nearestElement.Properties["Thickness"] = dimension.Value;
                                nearestElement.Properties["ThicknessFromDimension"] = true;
                            }
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        private void CalculateOverallConfidence(RecognitionResult result)
        {
            if (!result.RecognizedElements.Any())
            {
                result.OverallConfidence = 0;
                return;
            }

            result.OverallConfidence = result.RecognizedElements.Average(e => e.Confidence);
        }

        private bool AreParallel(LinePrimitive line1, LinePrimitive line2, double tolerance)
        {
            var angle1 = Math.Atan2(line1.EndPoint.Y - line1.StartPoint.Y,
                                    line1.EndPoint.X - line1.StartPoint.X);
            var angle2 = Math.Atan2(line2.EndPoint.Y - line2.StartPoint.Y,
                                    line2.EndPoint.X - line2.StartPoint.X);

            var diff = Math.Abs(angle1 - angle2);
            return diff < tolerance || Math.Abs(diff - Math.PI) < tolerance;
        }

        private double PerpendicularDistance(LinePrimitive line1, LinePrimitive line2)
        {
            // Calculate perpendicular distance between parallel lines
            var midpoint = new Point2D
            {
                X = (line1.StartPoint.X + line1.EndPoint.X) / 2,
                Y = (line1.StartPoint.Y + line1.EndPoint.Y) / 2
            };

            return DistanceToLine(midpoint, line2);
        }

        private double DistanceToLine(Point2D point, LinePrimitive line)
        {
            var A = point.X - line.StartPoint.X;
            var B = point.Y - line.StartPoint.Y;
            var C = line.EndPoint.X - line.StartPoint.X;
            var D = line.EndPoint.Y - line.StartPoint.Y;

            var dot = A * C + B * D;
            var lenSq = C * C + D * D;
            var param = lenSq != 0 ? dot / lenSq : -1;

            double xx, yy;

            if (param < 0)
            {
                xx = line.StartPoint.X;
                yy = line.StartPoint.Y;
            }
            else if (param > 1)
            {
                xx = line.EndPoint.X;
                yy = line.EndPoint.Y;
            }
            else
            {
                xx = line.StartPoint.X + param * C;
                yy = line.StartPoint.Y + param * D;
            }

            return Math.Sqrt((point.X - xx) * (point.X - xx) + (point.Y - yy) * (point.Y - yy));
        }

        private double CalculateOverlap(LinePrimitive line1, LinePrimitive line2)
        {
            // Project both lines onto common axis and calculate overlap
            var dir = new Point2D
            {
                X = line1.EndPoint.X - line1.StartPoint.X,
                Y = line1.EndPoint.Y - line1.StartPoint.Y
            };
            var len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (len == 0) return 0;

            dir.X /= len;
            dir.Y /= len;

            var proj1Start = line1.StartPoint.X * dir.X + line1.StartPoint.Y * dir.Y;
            var proj1End = line1.EndPoint.X * dir.X + line1.EndPoint.Y * dir.Y;
            var proj2Start = line2.StartPoint.X * dir.X + line2.StartPoint.Y * dir.Y;
            var proj2End = line2.EndPoint.X * dir.X + line2.EndPoint.Y * dir.Y;

            var min1 = Math.Min(proj1Start, proj1End);
            var max1 = Math.Max(proj1Start, proj1End);
            var min2 = Math.Min(proj2Start, proj2End);
            var max2 = Math.Max(proj2Start, proj2End);

            var overlapStart = Math.Max(min1, min2);
            var overlapEnd = Math.Min(max1, max2);

            return Math.Max(0, overlapEnd - overlapStart);
        }

        private double CalculateWallConfidence(LinePrimitive line1, LinePrimitive line2, double thickness)
        {
            var confidence = 0.7;

            // Boost confidence for standard wall thicknesses
            var standardThicknesses = new[] { 100, 115, 140, 150, 200, 215, 230, 250, 300 };
            if (standardThicknesses.Any(t => Math.Abs(thickness - t) < 10))
            {
                confidence += 0.15;
            }

            // Boost confidence for longer walls
            var length = CalculateOverlap(line1, line2);
            if (length > 1000) confidence += 0.1;

            return Math.Min(confidence, 0.98);
        }

        private ElementGeometry CreateWallGeometry(LinePrimitive line1, LinePrimitive line2)
        {
            // Create centerline between the two parallel lines
            var centerStart = new Point3D
            {
                X = (line1.StartPoint.X + line2.StartPoint.X) / 2,
                Y = (line1.StartPoint.Y + line2.StartPoint.Y) / 2,
                Z = 0
            };
            var centerEnd = new Point3D
            {
                X = (line1.EndPoint.X + line2.EndPoint.X) / 2,
                Y = (line1.EndPoint.Y + line2.EndPoint.Y) / 2,
                Z = 0
            };

            return new ElementGeometry
            {
                StartPoint = centerStart,
                EndPoint = centerEnd
            };
        }

        private string InferWallType(double thickness)
        {
            return thickness switch
            {
                < 120 => "Partition Wall - 100mm",
                < 160 => "Internal Wall - 140mm",
                < 180 => "Internal Loadbearing - 150mm",
                < 220 => "Cavity Wall - 200mm",
                < 280 => "External Wall - 250mm",
                < 320 => "External Cavity - 300mm",
                _ => "Structural Wall"
            };
        }

        private bool IsRoomBoundary(PolylinePrimitive polyline)
        {
            if (!polyline.IsClosed) return false;

            // Check for reasonable room size
            var area = CalculatePolygonArea(polyline.Points);
            return area > 1000000 && area < 1000000000; // 1m² to 1000m²
        }

        private double CalculatePolygonArea(List<Point2D> points)
        {
            var area = 0.0;
            var n = points.Count;

            for (int i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            return Math.Abs(area / 2.0);
        }

        private bool IsWallHatchPattern(string hatchPattern)
        {
            if (string.IsNullOrEmpty(hatchPattern)) return false;

            var wallPatterns = new[]
            {
                "brick", "block", "concrete", "stone", "masonry",
                "cmu", "solid", "ar-", "ansi31", "ansi32"
            };

            return wallPatterns.Any(p => hatchPattern.ToLower().Contains(p));
        }

        private BoundingBox CalculateBounds(List<Point2D> points)
        {
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            var width = maxX - minX;
            var height = maxY - minY;

            return new BoundingBox
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                Width = Math.Min(width, height),
                Length = Math.Max(width, height)
            };
        }

        private string InferMaterialFromHatch(string hatchPattern)
        {
            if (string.IsNullOrEmpty(hatchPattern)) return "Unknown";

            var lowerPattern = hatchPattern.ToLower();

            if (lowerPattern.Contains("brick")) return "Brick";
            if (lowerPattern.Contains("block") || lowerPattern.Contains("cmu")) return "Concrete Block";
            if (lowerPattern.Contains("concrete")) return "Concrete";
            if (lowerPattern.Contains("stone")) return "Stone";
            if (lowerPattern.Contains("wood") || lowerPattern.Contains("timber")) return "Timber";
            if (lowerPattern.Contains("steel")) return "Steel";

            return "General";
        }

        private List<RecognizedElement> MergeOverlappingWalls(List<RecognizedElement> walls)
        {
            // Simple merge - remove exact duplicates
            var merged = new List<RecognizedElement>();

            foreach (var wall in walls)
            {
                var isDuplicate = merged.Any(w =>
                    w.Geometry?.StartPoint != null &&
                    wall.Geometry?.StartPoint != null &&
                    Distance3D(w.Geometry.StartPoint, wall.Geometry.StartPoint) < 50 &&
                    Distance3D(w.Geometry.EndPoint, wall.Geometry.EndPoint) < 50);

                if (!isDuplicate)
                {
                    merged.Add(wall);
                }
            }

            return merged;
        }

        private async Task ClassifyWallTypesAsync(List<RecognizedElement> walls, GeometryPrimitives primitives)
        {
            foreach (var wall in walls)
            {
                // Check for exterior indicators
                // (walls at boundary of drawing, walls with specific layers)
                wall.Properties["IsExterior"] = false; // Default

                // Could analyze position relative to overall boundary
                // For now, use thickness heuristic
                var thickness = wall.Properties.ContainsKey("Thickness")
                    ? Convert.ToDouble(wall.Properties["Thickness"])
                    : 0;

                if (thickness > 200)
                {
                    wall.Properties["IsExterior"] = true;
                }
            }

            await Task.CompletedTask;
        }

        private double Distance(Point2D p1, Point2D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        private double Distance3D(Point3D p1, Point3D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2) + Math.Pow(p2.Z - p1.Z, 2));
        }

        private RecognizedElement FindNearestWall(Point2D point, List<RecognizedElement> walls)
        {
            return walls
                .Where(w => w.ElementType == RecognizedElementType.Wall)
                .OrderBy(w => DistanceToWall(point, w))
                .FirstOrDefault();
        }

        private double DistanceToWall(Point2D point, RecognizedElement wall)
        {
            if (wall.Geometry?.StartPoint == null || wall.Geometry?.EndPoint == null)
                return double.MaxValue;

            var line = new LinePrimitive
            {
                StartPoint = new Point2D { X = wall.Geometry.StartPoint.X, Y = wall.Geometry.StartPoint.Y },
                EndPoint = new Point2D { X = wall.Geometry.EndPoint.X, Y = wall.Geometry.EndPoint.Y }
            };

            return DistanceToLine(point, line);
        }

        private double DistanceToElement(RecognizedDimension dim, RecognizedElement element)
        {
            if (element.Geometry?.StartPoint == null) return double.MaxValue;

            var dimCenter = dim.TextPosition;
            var elemCenter = element.Geometry.Center ?? element.Geometry.StartPoint;

            return Distance3D(dimCenter, elemCenter);
        }

        private string DetermineSwingDirection(ArcPrimitive arc)
        {
            // Analyze arc direction to determine door swing
            var startAngle = arc.StartAngle;
            var endAngle = arc.EndAngle;

            if (endAngle > startAngle)
                return "Counterclockwise";
            else
                return "Clockwise";
        }

        private string InferDoorType(double width)
        {
            return width switch
            {
                < 700 => "Single Door - Narrow",
                < 850 => "Single Door - 762mm",
                < 950 => "Single Door - 864mm",
                < 1100 => "Single Door - Wide",
                < 1400 => "Double Door - 1200mm",
                _ => "Double Door - Wide"
            };
        }

        private List<WallGap> FindWallGaps(RecognizedElement wall, GeometryPrimitives primitives)
        {
            // Find gaps in walls where openings might be
            var gaps = new List<WallGap>();

            // This would analyze wall geometry for discontinuities
            return gaps;
        }

        private List<LinePrimitive> FindWindowLinesInGap(WallGap gap, List<LinePrimitive> lines)
        {
            // Find lines perpendicular to wall within gap
            return new List<LinePrimitive>();
        }

        private string InferWindowType(double width)
        {
            return width switch
            {
                < 600 => "Small Fixed Window",
                < 900 => "Standard Window - 600mm",
                < 1200 => "Standard Window - 900mm",
                < 1500 => "Large Window - 1200mm",
                < 1800 => "Picture Window - 1500mm",
                _ => "Picture Window - Wide"
            };
        }

        private bool MatchesSymbol(BlockPrimitive block, SymbolTemplate template)
        {
            // Compare block geometry/name with template
            return block.Name.ToLower().Contains(template.Name.ToLower());
        }

        private Dictionary<string, object> ExtractSymbolProperties(BlockPrimitive block, MatchedSymbol matched)
        {
            return new Dictionary<string, object>
            {
                ["BlockName"] = block.Name,
                ["MatchedTemplate"] = matched.Name
            };
        }

        private bool IsLevelNotation(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            var patterns = new[]
            {
                @"^[+-]?\d+\.\d{3}$",  // +0.000, -1.500
                @"^FFL\s*[+-]?\d+",     // FFL +0.000
                @"^SSL\s*[+-]?\d+",     // SSL +3.000
                @"^TOS\s*[+-]?\d+",     // TOS (Top of Slab)
                @"^BOS\s*[+-]?\d+"      // BOS (Bottom of Slab)
            };

            return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(text, p));
        }

        private double ParseLevelValue(string text)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"[+-]?\d+\.\d{3}");
            if (match.Success && double.TryParse(match.Value, out var value))
            {
                return value * 1000; // Convert to mm
            }
            return 0;
        }

        private bool IsGridLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Single letter (A-Z) or number
            return System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), @"^[A-Za-z]$|^\d{1,2}$");
        }

        #endregion

        #region Pattern and Symbol Initialization

        private Dictionary<string, ElementPattern> InitializeElementPatterns()
        {
            return new Dictionary<string, ElementPattern>
            {
                ["Wall_Parallel"] = new ElementPattern
                {
                    PatternType = "ParallelLines",
                    ElementType = RecognizedElementType.Wall,
                    MinThickness = 50,
                    MaxThickness = 600
                },
                ["Wall_Polyline"] = new ElementPattern
                {
                    PatternType = "ClosedPolyline",
                    ElementType = RecognizedElementType.Wall,
                    AspectRatioMin = 3.0
                },
                ["Door_Swing"] = new ElementPattern
                {
                    PatternType = "Arc90Degree",
                    ElementType = RecognizedElementType.Door,
                    RadiusMin = 600,
                    RadiusMax = 1200
                },
                ["Window_Gap"] = new ElementPattern
                {
                    PatternType = "WallGapWithLines",
                    ElementType = RecognizedElementType.Window,
                    MinWidth = 400,
                    MaxWidth = 3000
                }
            };
        }

        private Dictionary<string, SymbolTemplate> InitializeSymbolLibrary()
        {
            return new Dictionary<string, SymbolTemplate>
            {
                // Door symbols
                ["Door_Single"] = new SymbolTemplate
                {
                    Name = "Single Door",
                    SymbolType = SymbolType.Door,
                    Keywords = new[] { "door", "single", "dr" }
                },
                ["Door_Double"] = new SymbolTemplate
                {
                    Name = "Double Door",
                    SymbolType = SymbolType.Door,
                    Keywords = new[] { "double", "pair", "dd" }
                },
                ["Door_Sliding"] = new SymbolTemplate
                {
                    Name = "Sliding Door",
                    SymbolType = SymbolType.Door,
                    Keywords = new[] { "sliding", "slide", "sd" }
                },

                // Window symbols
                ["Window_Single"] = new SymbolTemplate
                {
                    Name = "Single Window",
                    SymbolType = SymbolType.Window,
                    Keywords = new[] { "window", "win", "w" }
                },
                ["Window_Double"] = new SymbolTemplate
                {
                    Name = "Double Window",
                    SymbolType = SymbolType.Window,
                    Keywords = new[] { "double", "dw" }
                },

                // Annotation symbols
                ["North_Arrow"] = new SymbolTemplate
                {
                    Name = "North Arrow",
                    SymbolType = SymbolType.NorthArrow,
                    Keywords = new[] { "north", "arrow", "n" }
                },
                ["Section_Mark"] = new SymbolTemplate
                {
                    Name = "Section Mark",
                    SymbolType = SymbolType.SectionMark,
                    Keywords = new[] { "section", "sect", "s" }
                },
                ["Level_Marker"] = new SymbolTemplate
                {
                    Name = "Level Marker",
                    SymbolType = SymbolType.LevelMarker,
                    Keywords = new[] { "level", "datum", "ffl", "ssl" }
                },

                // MEP symbols
                ["Electrical_Outlet"] = new SymbolTemplate
                {
                    Name = "Electrical Outlet",
                    SymbolType = SymbolType.Electrical,
                    Keywords = new[] { "outlet", "socket", "power" }
                },
                ["Light_Switch"] = new SymbolTemplate
                {
                    Name = "Light Switch",
                    SymbolType = SymbolType.Electrical,
                    Keywords = new[] { "switch", "light", "sw" }
                },
                ["Plumbing_Fixture"] = new SymbolTemplate
                {
                    Name = "Plumbing Fixture",
                    SymbolType = SymbolType.Plumbing,
                    Keywords = new[] { "wc", "basin", "sink", "toilet" }
                }
            };
        }

        private Dictionary<string, DimensionPattern> InitializeDimensionPatterns()
        {
            return new Dictionary<string, DimensionPattern>
            {
                ["Linear"] = new DimensionPattern
                {
                    Pattern = @"(\d+\.?\d*)\s*(mm|m|cm)?",
                    DimensionType = DimensionPrimitiveType.Linear
                },
                ["Imperial"] = new DimensionPattern
                {
                    Pattern = @"(\d+)'[-\s]*(\d+)?[""″]?",
                    DimensionType = DimensionPrimitiveType.Linear
                },
                ["Angular"] = new DimensionPattern
                {
                    Pattern = @"(\d+\.?\d*)°",
                    DimensionType = DimensionPrimitiveType.Angular
                }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class PatternMatcher
    {
        private readonly RecognitionSettings _settings;

        public PatternMatcher(RecognitionSettings settings)
        {
            _settings = settings;
        }
    }

    public class SymbolRecognizer
    {
        private readonly Dictionary<string, SymbolTemplate> _library;

        public SymbolRecognizer(Dictionary<string, SymbolTemplate> library)
        {
            _library = library;
        }

        public MatchedSymbol MatchSymbol(BlockPrimitive block)
        {
            foreach (var template in _library.Values)
            {
                if (template.Keywords.Any(k => block.Name.ToLower().Contains(k)))
                {
                    return new MatchedSymbol
                    {
                        Name = template.Name,
                        SymbolType = template.SymbolType,
                        Confidence = 0.85
                    };
                }
            }
            return null;
        }
    }

    public class TextExtractor { }
    public class GeometryAnalyzer { }

    #endregion

    #region Data Models

    public class RecognitionSettings
    {
        public double ParallelTolerance { get; set; } = 0.1; // radians
        public double MinWallThickness { get; set; } = 50; // mm
        public double MaxWallThickness { get; set; } = 600; // mm
        public double DefaultWallThickness { get; set; } = 200; // mm
        public double MinOverlapRatio { get; set; } = 0.5;
        public double ConcentricTolerance { get; set; } = 10; // mm
        public string DefaultUnits { get; set; } = "mm";
    }

    public class RecognitionOptions
    {
        public bool RecognizeWalls { get; set; } = true;
        public bool RecognizeOpenings { get; set; } = true;
        public bool RecognizeSymbols { get; set; } = true;
        public bool ExtractDimensions { get; set; } = true;
        public double ParallelTolerance { get; set; } = 0.1;
        public double MinWallThickness { get; set; } = 50;
        public double MaxWallThickness { get; set; } = 600;
        public double DefaultWallThickness { get; set; } = 200;
        public double MinOverlapRatio { get; set; } = 0.5;
        public double ConcentricTolerance { get; set; } = 10;
        public string DefaultUnits { get; set; } = "mm";
    }

    public class ImageRecognitionOptions
    {
        public int ResolutionDPI { get; set; } = 300;
        public bool EnhanceContrast { get; set; } = true;
        public bool RemoveNoise { get; set; } = true;
        public double LineDetectionThreshold { get; set; } = 50;
        public double ContourAreaThreshold { get; set; } = 100;
    }

    public class PDFRecognitionOptions
    {
        public int RenderDPI { get; set; } = 300;
        public bool ExtractVectors { get; set; } = true;
        public bool ExtractText { get; set; } = true;
        public List<int> PageNumbers { get; set; }
    }

    public class DrawingInput
    {
        public string SourceFile { get; set; }
        public SourceType SourceType { get; set; }
        public GeometryPrimitives Geometry { get; set; }
    }

    public class RecognitionResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string SourceFile { get; set; }
        public SourceType SourceType { get; set; }
        public List<RecognizedElement> RecognizedElements { get; set; }
        public List<RecognizedSymbol> RecognizedSymbols { get; set; }
        public List<RecognizedDimension> RecognizedDimensions { get; set; }
        public List<RecognizedText> RecognizedText { get; set; }
        public List<string> Warnings { get; set; }
        public double OverallConfidence { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    public class RecognizedElement
    {
        public RecognizedElementType ElementType { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public double Confidence { get; set; }
        public ElementGeometry Geometry { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public string InferredType { get; set; }
        public string InferredMaterial { get; set; }
        public RecognizedElement HostElement { get; set; }
    }

    public class RecognizedSymbol
    {
        public SymbolType SymbolType { get; set; }
        public string Name { get; set; }
        public Point3D Position { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1.0;
        public double Confidence { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class RecognizedDimension
    {
        public DimensionPrimitiveType DimensionType { get; set; }
        public double Value { get; set; }
        public string Units { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public Point3D TextPosition { get; set; }
        public string OriginalText { get; set; }
        public double Confidence { get; set; }
    }

    public class RecognizedText
    {
        public string Content { get; set; }
        public Point3D Position { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public string Style { get; set; }
        public double Confidence { get; set; }
    }

    public class ElementGeometry
    {
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public Point3D Center { get; set; }
        public List<Point3D> Points { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public double InnerRadius { get; set; }
        public double OuterRadius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
    }

    public class GeometryPrimitives
    {
        public List<LinePrimitive> Lines { get; set; } = new List<LinePrimitive>();
        public List<PolylinePrimitive> Polylines { get; set; } = new List<PolylinePrimitive>();
        public List<ArcPrimitive> Arcs { get; set; } = new List<ArcPrimitive>();
        public List<CirclePrimitive> Circles { get; set; } = new List<CirclePrimitive>();
        public List<SplinePrimitive> Splines { get; set; } = new List<SplinePrimitive>();
        public List<FilledRegionPrimitive> FilledRegions { get; set; } = new List<FilledRegionPrimitive>();
        public List<BlockPrimitive> Blocks { get; set; } = new List<BlockPrimitive>();
        public List<TextPrimitive> Texts { get; set; } = new List<TextPrimitive>();
        public List<DimensionPrimitive> Dimensions { get; set; } = new List<DimensionPrimitive>();
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class LinePrimitive
    {
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public string Layer { get; set; }
    }

    public class PolylinePrimitive
    {
        public List<Point2D> Points { get; set; }
        public bool IsClosed { get; set; }
        public string Layer { get; set; }
    }

    public class ArcPrimitive
    {
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public string Layer { get; set; }
    }

    public class CirclePrimitive
    {
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public string Layer { get; set; }
    }

    public class SplinePrimitive
    {
        public List<Point2D> ControlPoints { get; set; }
        public int Degree { get; set; }
        public string Layer { get; set; }
    }

    public class FilledRegionPrimitive
    {
        public List<Point2D> BoundaryPoints { get; set; }
        public string HatchPattern { get; set; }
        public string Layer { get; set; }
    }

    public class BlockPrimitive
    {
        public string Name { get; set; }
        public Point2D InsertionPoint { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1.0;
        public string Layer { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }

    public class TextPrimitive
    {
        public string Content { get; set; }
        public Point2D Position { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public string Style { get; set; }
        public string Layer { get; set; }
    }

    public class DimensionPrimitive
    {
        public DimensionPrimitiveType DimensionType { get; set; }
        public double Value { get; set; }
        public string Units { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public Point2D TextPosition { get; set; }
        public string Layer { get; set; }
    }

    public class PreprocessedData
    {
        public SourceType SourceType { get; set; }
        public GeometryPrimitives Primitives { get; set; }
    }

    public class ProcessedImage
    {
        public string Path { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class ContourPrimitive
    {
        public List<Point2D> Points { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
    }

    public class PDFVectorData
    {
        public bool HasVectorContent { get; set; }
        public List<PDFPage> Pages { get; set; } = new List<PDFPage>();
    }

    public class PDFPage
    {
        public int PageNumber { get; set; }
        public List<LinePrimitive> Lines { get; set; }
        public List<TextPrimitive> Texts { get; set; }
    }

    public class RecognitionProgress
    {
        public string Stage { get; set; }
        public int Percentage { get; set; }
    }

    public class ElementPattern
    {
        public string PatternType { get; set; }
        public RecognizedElementType ElementType { get; set; }
        public double MinThickness { get; set; }
        public double MaxThickness { get; set; }
        public double AspectRatioMin { get; set; }
        public double RadiusMin { get; set; }
        public double RadiusMax { get; set; }
        public double MinWidth { get; set; }
        public double MaxWidth { get; set; }
    }

    public class SymbolTemplate
    {
        public string Name { get; set; }
        public SymbolType SymbolType { get; set; }
        public string[] Keywords { get; set; }
        public List<Point2D> ReferencePoints { get; set; }
    }

    public class MatchedSymbol
    {
        public string Name { get; set; }
        public SymbolType SymbolType { get; set; }
        public double Confidence { get; set; }
    }

    public class DimensionPattern
    {
        public string Pattern { get; set; }
        public DimensionPrimitiveType DimensionType { get; set; }
    }

    public class BoundingBox
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
    }

    public class WallGap
    {
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double Width { get; set; }
    }

    public enum SourceType
    {
        CAD,
        Image,
        PDF,
        Geometry
    }

    public enum RecognizedElementType
    {
        Wall,
        Door,
        Window,
        Column,
        Beam,
        Floor,
        Ceiling,
        Roof,
        Stair,
        Room,
        Furniture,
        Equipment,
        Unknown
    }

    public enum SymbolType
    {
        Door,
        Window,
        NorthArrow,
        SectionMark,
        LevelMarker,
        GridBubble,
        DetailCallout,
        ElevationMark,
        Electrical,
        Plumbing,
        HVAC,
        Fire,
        Furniture,
        Unknown
    }

    public enum DimensionPrimitiveType
    {
        Linear,
        Angular,
        Radial,
        Diameter,
        Ordinate
    }

    #endregion
}
