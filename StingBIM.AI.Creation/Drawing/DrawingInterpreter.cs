using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Drawing
{
    /// <summary>
    /// Advanced drawing interpreter that reads plans, sections, and elevations
    /// from the same sheet and correlates them to create accurate 3D models.
    /// Goes beyond simple line-to-wall conversion by understanding architectural intent.
    /// </summary>
    public class DrawingInterpreterEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, ViewInterpretationRules> _viewRules;
        private readonly Dictionary<string, ElementPattern> _elementPatterns;
        private readonly Dictionary<string, LayerMapping> _layerMappings;
        private readonly List<InterpretationSession> _sessionHistory;
        private readonly object _lock = new object();

        public event EventHandler<ElementRecognizedEventArgs> ElementRecognized;
        public event EventHandler<CorrelationFoundEventArgs> ViewCorrelationFound;

        public DrawingInterpreterEngine()
        {
            _viewRules = InitializeViewRules();
            _elementPatterns = InitializeElementPatterns();
            _layerMappings = InitializeLayerMappings();
            _sessionHistory = new List<InterpretationSession>();

            Logger.Info("DrawingInterpreterEngine initialized with {0} view rules, {1} element patterns",
                _viewRules.Count, _elementPatterns.Count);
        }

        #region Initialization

        private Dictionary<string, ViewInterpretationRules> InitializeViewRules()
        {
            return new Dictionary<string, ViewInterpretationRules>(StringComparer.OrdinalIgnoreCase)
            {
                ["FloorPlan"] = new ViewInterpretationRules
                {
                    ViewType = ViewType.FloorPlan,
                    ElementsToExtract = new List<string> { "Wall", "Door", "Window", "Column", "Stair", "Room", "Furniture" },
                    ExtractPosition = true,
                    ExtractRotation = true,
                    ExtractWidth = true,
                    ExtractLength = true,
                    HeightSource = HeightSource.FromSection,
                    DefaultHeight = 2700,
                    RecognitionPriority = new List<string> { "Wall", "Column", "Door", "Window", "Stair" }
                },
                ["Section"] = new ViewInterpretationRules
                {
                    ViewType = ViewType.Section,
                    ElementsToExtract = new List<string> { "Wall", "Floor", "Roof", "Beam", "Foundation", "Opening" },
                    ExtractPosition = true,
                    ExtractHeight = true,
                    ExtractLevelElevations = true,
                    ProvideHeightData = true,
                    RecognitionPriority = new List<string> { "Level", "Floor", "Wall", "Beam", "Roof" }
                },
                ["Elevation"] = new ViewInterpretationRules
                {
                    ViewType = ViewType.Elevation,
                    ElementsToExtract = new List<string> { "Window", "Door", "Wall", "Roof", "Facade" },
                    ExtractPosition = true,
                    ExtractHeight = true,
                    ExtractWidth = true,
                    ProvideOpeningPositions = true,
                    RecognitionPriority = new List<string> { "Opening", "Window", "Door", "Wall" }
                },
                ["RoofPlan"] = new ViewInterpretationRules
                {
                    ViewType = ViewType.RoofPlan,
                    ElementsToExtract = new List<string> { "Roof", "RoofOpening", "Chimney", "Skylight" },
                    ExtractPosition = true,
                    ExtractSlope = true,
                    ExtractRidgeDirection = true,
                    RecognitionPriority = new List<string> { "RoofBoundary", "Ridge", "Valley", "Hip" }
                },
                ["ReflectedCeilingPlan"] = new ViewInterpretationRules
                {
                    ViewType = ViewType.ReflectedCeilingPlan,
                    ElementsToExtract = new List<string> { "Ceiling", "Light", "Diffuser", "AccessPanel" },
                    ExtractPosition = true,
                    ExtractCeilingHeight = true,
                    RecognitionPriority = new List<string> { "CeilingBoundary", "Light", "Diffuser" }
                },
                ["FoundationPlan"] = new ViewInterpretationRules
                {
                    ViewType = ViewType.FoundationPlan,
                    ElementsToExtract = new List<string> { "Foundation", "Footing", "PileGroup", "Slab" },
                    ExtractPosition = true,
                    ExtractDepth = true,
                    ExtractWidth = true,
                    RecognitionPriority = new List<string> { "Footing", "Foundation", "Slab" }
                }
            };
        }

        private Dictionary<string, ElementPattern> InitializeElementPatterns()
        {
            return new Dictionary<string, ElementPattern>(StringComparer.OrdinalIgnoreCase)
            {
                // Wall Patterns
                ["Wall_Parallel_Lines"] = new ElementPattern
                {
                    PatternId = "WP001",
                    ElementType = "Wall",
                    Description = "Two parallel lines representing wall faces",
                    GeometryType = GeometryPatternType.ParallelLines,
                    MinLineCount = 2,
                    MaxLineCount = 2,
                    ToleranceParallel = 1.0, // degrees
                    ToleranceSpacing = 50, // mm variance allowed
                    TypicalThickness = new Range { Min = 100, Max = 600 },
                    RecognitionConfidence = 0.95
                },
                ["Wall_Single_Line"] = new ElementPattern
                {
                    PatternId = "WP002",
                    ElementType = "Wall",
                    Description = "Single centerline representing wall",
                    GeometryType = GeometryPatternType.SingleLine,
                    RequiresThicknessAnnotation = true,
                    DefaultThickness = 200,
                    RecognitionConfidence = 0.80
                },
                ["Wall_Hatched_Region"] = new ElementPattern
                {
                    PatternId = "WP003",
                    ElementType = "Wall",
                    Description = "Hatched/filled region representing wall in plan",
                    GeometryType = GeometryPatternType.FilledRegion,
                    HatchPatterns = new List<string> { "Solid", "Concrete", "Masonry", "CrossHatch" },
                    RecognitionConfidence = 0.90
                },

                // Door Patterns
                ["Door_Arc_Symbol"] = new ElementPattern
                {
                    PatternId = "DP001",
                    ElementType = "Door",
                    Description = "Door swing arc with leaf line",
                    GeometryType = GeometryPatternType.ArcWithLine,
                    RequiresWallGap = true,
                    TypicalWidth = new Range { Min = 700, Max = 1200 },
                    RecognitionConfidence = 0.92
                },
                ["Door_Rectangle_Symbol"] = new ElementPattern
                {
                    PatternId = "DP002",
                    ElementType = "Door",
                    Description = "Rectangular door symbol in plan",
                    GeometryType = GeometryPatternType.Rectangle,
                    RequiresWallGap = true,
                    TypicalWidth = new Range { Min = 700, Max = 2400 },
                    RecognitionConfidence = 0.85
                },
                ["Door_Sliding"] = new ElementPattern
                {
                    PatternId = "DP003",
                    ElementType = "Door",
                    Description = "Sliding door symbol with parallel lines",
                    GeometryType = GeometryPatternType.ParallelLinesWithArrow,
                    RequiresWallGap = true,
                    RecognitionConfidence = 0.88
                },

                // Window Patterns
                ["Window_Triple_Line"] = new ElementPattern
                {
                    PatternId = "WI001",
                    ElementType = "Window",
                    Description = "Three parallel lines (glass + frames)",
                    GeometryType = GeometryPatternType.TripleParallelLines,
                    RequiresWallContext = true,
                    TypicalWidth = new Range { Min = 600, Max = 3000 },
                    RecognitionConfidence = 0.90
                },
                ["Window_Rectangle_In_Wall"] = new ElementPattern
                {
                    PatternId = "WI002",
                    ElementType = "Window",
                    Description = "Rectangle interrupting wall pattern",
                    GeometryType = GeometryPatternType.RectangleInWall,
                    RecognitionConfidence = 0.85
                },

                // Column Patterns
                ["Column_Rectangle"] = new ElementPattern
                {
                    PatternId = "CP001",
                    ElementType = "Column",
                    Description = "Rectangular column in plan",
                    GeometryType = GeometryPatternType.FilledRectangle,
                    TypicalSize = new Range { Min = 200, Max = 800 },
                    RequiresGridContext = true,
                    RecognitionConfidence = 0.88
                },
                ["Column_Circle"] = new ElementPattern
                {
                    PatternId = "CP002",
                    ElementType = "Column",
                    Description = "Circular column in plan",
                    GeometryType = GeometryPatternType.FilledCircle,
                    TypicalDiameter = new Range { Min = 200, Max = 1000 },
                    RecognitionConfidence = 0.90
                },

                // Stair Patterns
                ["Stair_Treads"] = new ElementPattern
                {
                    PatternId = "SP001",
                    ElementType = "Stair",
                    Description = "Series of parallel lines (treads) with direction arrow",
                    GeometryType = GeometryPatternType.ParallelLineSeriesWithArrow,
                    MinLineCount = 3,
                    TypicalTreadDepth = new Range { Min = 250, Max = 330 },
                    RecognitionConfidence = 0.92
                },

                // Room Patterns
                ["Room_Closed_Boundary"] = new ElementPattern
                {
                    PatternId = "RP001",
                    ElementType = "Room",
                    Description = "Closed boundary with room tag",
                    GeometryType = GeometryPatternType.ClosedPolyline,
                    RequiresRoomTag = true,
                    RecognitionConfidence = 0.85
                }
            };
        }

        private Dictionary<string, LayerMapping> InitializeLayerMappings()
        {
            // Common CAD layer naming conventions mapped to Revit categories
            return new Dictionary<string, LayerMapping>(StringComparer.OrdinalIgnoreCase)
            {
                // Architectural Layers
                ["A-WALL"] = new LayerMapping { RevitCategory = "Walls", ElementType = "Wall", Priority = 1 },
                ["A-WALL-FULL"] = new LayerMapping { RevitCategory = "Walls", ElementType = "Wall", WallFunction = "Structural", Priority = 1 },
                ["A-WALL-PART"] = new LayerMapping { RevitCategory = "Walls", ElementType = "Wall", WallFunction = "Partition", Priority = 2 },
                ["A-WALL-EXTR"] = new LayerMapping { RevitCategory = "Walls", ElementType = "Wall", WallFunction = "Exterior", Priority = 1 },
                ["A-DOOR"] = new LayerMapping { RevitCategory = "Doors", ElementType = "Door", Priority = 1 },
                ["A-GLAZ"] = new LayerMapping { RevitCategory = "Windows", ElementType = "Window", Priority = 1 },
                ["A-WINDOW"] = new LayerMapping { RevitCategory = "Windows", ElementType = "Window", Priority = 1 },
                ["A-FLOR"] = new LayerMapping { RevitCategory = "Floors", ElementType = "Floor", Priority = 1 },
                ["A-ROOF"] = new LayerMapping { RevitCategory = "Roofs", ElementType = "Roof", Priority = 1 },
                ["A-CLNG"] = new LayerMapping { RevitCategory = "Ceilings", ElementType = "Ceiling", Priority = 1 },
                ["A-COLS"] = new LayerMapping { RevitCategory = "Columns", ElementType = "Column", Priority = 1 },
                ["A-STRS"] = new LayerMapping { RevitCategory = "Stairs", ElementType = "Stair", Priority = 1 },
                ["A-FURN"] = new LayerMapping { RevitCategory = "Furniture", ElementType = "Furniture", Priority = 3 },
                ["A-EQPM"] = new LayerMapping { RevitCategory = "Equipment", ElementType = "Equipment", Priority = 3 },

                // Structural Layers
                ["S-COLS"] = new LayerMapping { RevitCategory = "Structural Columns", ElementType = "StructuralColumn", Priority = 1 },
                ["S-BEAM"] = new LayerMapping { RevitCategory = "Structural Framing", ElementType = "Beam", Priority = 1 },
                ["S-FNDN"] = new LayerMapping { RevitCategory = "Structural Foundations", ElementType = "Foundation", Priority = 1 },
                ["S-SLAB"] = new LayerMapping { RevitCategory = "Floors", ElementType = "StructuralFloor", Priority = 1 },

                // MEP Layers
                ["M-DUCT"] = new LayerMapping { RevitCategory = "Ducts", ElementType = "Duct", Priority = 2 },
                ["M-PIPE"] = new LayerMapping { RevitCategory = "Pipes", ElementType = "Pipe", Priority = 2 },
                ["M-EQPM"] = new LayerMapping { RevitCategory = "Mechanical Equipment", ElementType = "MechanicalEquipment", Priority = 2 },
                ["P-FIXT"] = new LayerMapping { RevitCategory = "Plumbing Fixtures", ElementType = "PlumbingFixture", Priority = 2 },
                ["P-PIPE"] = new LayerMapping { RevitCategory = "Pipes", ElementType = "Pipe", Priority = 2 },
                ["E-LITE"] = new LayerMapping { RevitCategory = "Lighting Fixtures", ElementType = "LightingFixture", Priority = 2 },
                ["E-POWR"] = new LayerMapping { RevitCategory = "Electrical Fixtures", ElementType = "ElectricalFixture", Priority = 2 },

                // Annotation Layers (for dimension/text extraction)
                ["A-ANNO-DIMS"] = new LayerMapping { RevitCategory = "Dimensions", ElementType = "Dimension", IsAnnotation = true },
                ["A-ANNO-TEXT"] = new LayerMapping { RevitCategory = "Text Notes", ElementType = "TextNote", IsAnnotation = true },
                ["A-ANNO-SYMB"] = new LayerMapping { RevitCategory = "Generic Annotations", ElementType = "Symbol", IsAnnotation = true },
                ["A-ANNO-ROOM"] = new LayerMapping { RevitCategory = "Room Tags", ElementType = "RoomTag", IsAnnotation = true },
            };
        }

        #endregion

        #region Drawing Interpretation

        /// <summary>
        /// Interprets a complete drawing sheet containing plans, sections, and elevations.
        /// Correlates all views to create a comprehensive 3D model definition.
        /// </summary>
        public async Task<DrawingInterpretationResult> InterpretDrawingSheetAsync(
            DrawingSheetInput sheet,
            InterpretationOptions options = null,
            IProgress<InterpretationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Interpreting drawing sheet: {0} with {1} views", sheet.SheetName, sheet.Views.Count);
            options ??= new InterpretationOptions();

            var session = new InterpretationSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                SheetName = sheet.SheetName,
                StartTime = DateTime.UtcNow
            };

            var result = new DrawingInterpretationResult
            {
                SessionId = session.SessionId,
                SheetName = sheet.SheetName,
                RecognizedElements = new List<RecognizedElement>(),
                ViewCorrelations = new List<ViewCorrelation>(),
                LevelDefinitions = new List<LevelDefinition>(),
                Warnings = new List<string>()
            };

            try
            {
                // Phase 1: Classify views on the sheet
                ReportProgress(progress, 5, "Classifying views on sheet...");
                var classifiedViews = await ClassifyViewsAsync(sheet.Views, cancellationToken);

                // Phase 2: Extract levels from sections first (critical for height data)
                ReportProgress(progress, 15, "Extracting level information from sections...");
                var sectionViews = classifiedViews.Where(v => v.ClassifiedType == ViewType.Section).ToList();
                result.LevelDefinitions = await ExtractLevelsFromSectionsAsync(sectionViews, cancellationToken);

                // Phase 3: Interpret each view
                int viewIndex = 0;
                foreach (var view in classifiedViews)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int progressPercent = 20 + (viewIndex * 50 / classifiedViews.Count);
                    ReportProgress(progress, progressPercent, $"Interpreting {view.ClassifiedType}: {view.ViewName}...");

                    var viewElements = await InterpretSingleViewAsync(view, options, cancellationToken);
                    result.RecognizedElements.AddRange(viewElements);

                    viewIndex++;
                }

                // Phase 4: Correlate elements across views
                ReportProgress(progress, 75, "Correlating elements across views...");
                result.ViewCorrelations = await CorrelateElementsAcrossViewsAsync(
                    result.RecognizedElements, classifiedViews, cancellationToken);

                // Phase 5: Resolve conflicts and merge data
                ReportProgress(progress, 85, "Resolving conflicts and merging element data...");
                result.MergedElements = await MergeCorrelatedElementsAsync(
                    result.RecognizedElements, result.ViewCorrelations, result.LevelDefinitions, cancellationToken);

                // Phase 6: Validate and generate warnings
                ReportProgress(progress, 95, "Validating interpretation results...");
                result.Warnings.AddRange(ValidateInterpretation(result));

                result.Success = true;
                result.ElementCount = result.MergedElements.Count;

                ReportProgress(progress, 100, "Interpretation complete");

                session.EndTime = DateTime.UtcNow;
                session.ElementsRecognized = result.ElementCount;
                session.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Drawing interpretation failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                session.Success = false;
            }
            finally
            {
                lock (_lock)
                {
                    _sessionHistory.Add(session);
                }
            }

            Logger.Info("Interpretation complete: {0} elements recognized, {1} correlations found",
                result.RecognizedElements.Count, result.ViewCorrelations.Count);

            return result;
        }

        /// <summary>
        /// Interprets a single view (plan, section, or elevation).
        /// </summary>
        public async Task<List<RecognizedElement>> InterpretSingleViewAsync(
            ClassifiedView view,
            InterpretationOptions options,
            CancellationToken cancellationToken)
        {
            var elements = new List<RecognizedElement>();

            if (!_viewRules.TryGetValue(view.ClassifiedType.ToString(), out var rules))
            {
                Logger.Warn("No interpretation rules for view type: {0}", view.ClassifiedType);
                return elements;
            }

            await Task.Run(() =>
            {
                // Group geometry by layer
                var geometryByLayer = view.Geometry.GroupBy(g => g.Layer);

                foreach (var layerGroup in geometryByLayer)
                {
                    // Get layer mapping
                    var mapping = GetLayerMapping(layerGroup.Key);
                    if (mapping == null && !options.InterpretUnmappedLayers)
                        continue;

                    var layerGeometry = layerGroup.ToList();

                    // Apply recognition patterns based on expected element types
                    foreach (var elementType in rules.RecognitionPriority)
                    {
                        var patterns = _elementPatterns.Values
                            .Where(p => p.ElementType == elementType)
                            .OrderByDescending(p => p.RecognitionConfidence);

                        foreach (var pattern in patterns)
                        {
                            var matches = FindPatternMatches(layerGeometry, pattern, view);

                            foreach (var match in matches)
                            {
                                var recognized = new RecognizedElement
                                {
                                    ElementId = Guid.NewGuid().ToString("N").Substring(0, 12),
                                    ElementType = elementType,
                                    SourceViewType = view.ClassifiedType,
                                    SourceViewName = view.ViewName,
                                    PatternUsed = pattern.PatternId,
                                    Confidence = match.Confidence,
                                    Geometry2D = match.MatchedGeometry,
                                    Properties = ExtractPropertiesFromMatch(match, pattern),
                                    Layer = layerGroup.Key,
                                    BoundingBox2D = CalculateBoundingBox(match.MatchedGeometry)
                                };

                                // Extract dimensions from nearby annotations
                                recognized.ExtractedDimensions = ExtractNearbyDimensions(
                                    match.MatchedGeometry, view.Annotations);

                                elements.Add(recognized);
                                OnElementRecognized(recognized);

                                // Remove matched geometry to avoid double-recognition
                                foreach (var geom in match.MatchedGeometry)
                                {
                                    layerGeometry.Remove(geom);
                                }
                            }
                        }
                    }
                }
            }, cancellationToken);

            return elements;
        }

        #endregion

        #region View Classification

        private async Task<List<ClassifiedView>> ClassifyViewsAsync(
            List<DrawingViewInput> views,
            CancellationToken cancellationToken)
        {
            var classified = new List<ClassifiedView>();

            await Task.Run(() =>
            {
                foreach (var view in views)
                {
                    var classification = ClassifyView(view);
                    classified.Add(classification);
                }
            }, cancellationToken);

            return classified;
        }

        private ClassifiedView ClassifyView(DrawingViewInput view)
        {
            var classified = new ClassifiedView
            {
                OriginalView = view,
                ViewName = view.ViewName,
                Geometry = view.Geometry,
                Annotations = view.Annotations
            };

            // Classify based on view name hints
            var nameLower = view.ViewName?.ToLower() ?? "";

            if (nameLower.Contains("plan") || nameLower.Contains("floor") || nameLower.Contains("level"))
            {
                if (nameLower.Contains("roof"))
                    classified.ClassifiedType = ViewType.RoofPlan;
                else if (nameLower.Contains("ceiling") || nameLower.Contains("rcp"))
                    classified.ClassifiedType = ViewType.ReflectedCeilingPlan;
                else if (nameLower.Contains("foundation") || nameLower.Contains("footing"))
                    classified.ClassifiedType = ViewType.FoundationPlan;
                else
                    classified.ClassifiedType = ViewType.FloorPlan;
            }
            else if (nameLower.Contains("section") || nameLower.Contains("sect"))
            {
                classified.ClassifiedType = ViewType.Section;
            }
            else if (nameLower.Contains("elevation") || nameLower.Contains("elev") ||
                     nameLower.Contains("north") || nameLower.Contains("south") ||
                     nameLower.Contains("east") || nameLower.Contains("west"))
            {
                classified.ClassifiedType = ViewType.Elevation;
            }
            else
            {
                // Analyze geometry to classify
                classified.ClassifiedType = ClassifyByGeometryAnalysis(view);
            }

            classified.ClassificationConfidence = CalculateClassificationConfidence(classified);

            return classified;
        }

        private ViewType ClassifyByGeometryAnalysis(DrawingViewInput view)
        {
            // Analyze geometry patterns to determine view type
            var hasStairTreads = view.Geometry.Any(g => LooksLikeStairTreads(g));
            var hasDoorSwings = view.Geometry.Any(g => LooksLikeDoorSwing(g));
            var hasLevelLines = view.Geometry.Any(g => LooksLikeLevelLine(g));
            var aspectRatio = CalculateViewAspectRatio(view);

            if (hasLevelLines && aspectRatio > 0.5)
                return ViewType.Section;

            if (hasDoorSwings || hasStairTreads)
                return ViewType.FloorPlan;

            if (aspectRatio > 1.5) // Wide view likely elevation
                return ViewType.Elevation;

            return ViewType.FloorPlan; // Default assumption
        }

        private bool LooksLikeStairTreads(GeometryObject geom)
        {
            // Pattern: multiple parallel lines of similar length
            return false; // Simplified
        }

        private bool LooksLikeDoorSwing(GeometryObject geom)
        {
            // Pattern: arc geometry
            return geom.GeometryType == "Arc";
        }

        private bool LooksLikeLevelLine(GeometryObject geom)
        {
            // Pattern: long horizontal line with text annotation
            return geom.GeometryType == "Line" && Math.Abs(geom.StartPoint.Y - geom.EndPoint.Y) < 1;
        }

        private double CalculateViewAspectRatio(DrawingViewInput view)
        {
            if (view.Geometry.Count == 0) return 1;

            double minX = view.Geometry.Min(g => Math.Min(g.StartPoint.X, g.EndPoint?.X ?? g.StartPoint.X));
            double maxX = view.Geometry.Max(g => Math.Max(g.StartPoint.X, g.EndPoint?.X ?? g.StartPoint.X));
            double minY = view.Geometry.Min(g => Math.Min(g.StartPoint.Y, g.EndPoint?.Y ?? g.StartPoint.Y));
            double maxY = view.Geometry.Max(g => Math.Max(g.StartPoint.Y, g.EndPoint?.Y ?? g.StartPoint.Y));

            double width = maxX - minX;
            double height = maxY - minY;

            return height > 0 ? width / height : 1;
        }

        private double CalculateClassificationConfidence(ClassifiedView view)
        {
            // Higher confidence if name matches type
            var nameLower = view.ViewName?.ToLower() ?? "";

            return view.ClassifiedType switch
            {
                ViewType.FloorPlan when nameLower.Contains("plan") => 0.95,
                ViewType.Section when nameLower.Contains("section") => 0.95,
                ViewType.Elevation when nameLower.Contains("elevation") => 0.95,
                _ => 0.70
            };
        }

        #endregion

        #region Level Extraction

        private async Task<List<LevelDefinition>> ExtractLevelsFromSectionsAsync(
            List<ClassifiedView> sectionViews,
            CancellationToken cancellationToken)
        {
            var levels = new List<LevelDefinition>();

            await Task.Run(() =>
            {
                foreach (var section in sectionViews)
                {
                    // Look for horizontal lines with level annotations
                    var horizontalLines = section.Geometry
                        .Where(g => g.GeometryType == "Line" && IsHorizontal(g))
                        .OrderBy(g => g.StartPoint.Y)
                        .ToList();

                    foreach (var line in horizontalLines)
                    {
                        // Find nearby text annotation
                        var nearbyText = section.Annotations
                            .Where(a => Math.Abs(a.Position.Y - line.StartPoint.Y) < 500)
                            .FirstOrDefault();

                        if (nearbyText != null)
                        {
                            var levelDef = ParseLevelFromAnnotation(nearbyText, line.StartPoint.Y);
                            if (levelDef != null)
                            {
                                levels.Add(levelDef);
                            }
                        }
                    }
                }

                // If no levels found, create defaults
                if (levels.Count == 0)
                {
                    levels.Add(new LevelDefinition { Name = "Ground Floor", Elevation = 0, Index = 0 });
                    levels.Add(new LevelDefinition { Name = "First Floor", Elevation = 3000, Index = 1 });
                }
            }, cancellationToken);

            return levels.OrderBy(l => l.Elevation).ToList();
        }

        private bool IsHorizontal(GeometryObject geom)
        {
            if (geom.EndPoint == null) return false;
            return Math.Abs(geom.StartPoint.Y - geom.EndPoint.Y) < 10; // 10mm tolerance
        }

        private LevelDefinition ParseLevelFromAnnotation(AnnotationObject annotation, double elevation)
        {
            var text = annotation.Text?.ToLower() ?? "";

            // Common level naming patterns
            if (text.Contains("ground") || text.Contains("gl") || text.Contains("gf"))
            {
                return new LevelDefinition { Name = "Ground Floor", Elevation = elevation, Index = 0 };
            }
            if (text.Contains("first") || text.Contains("1st") || text.Contains("ff"))
            {
                return new LevelDefinition { Name = "First Floor", Elevation = elevation, Index = 1 };
            }
            if (text.Contains("second") || text.Contains("2nd"))
            {
                return new LevelDefinition { Name = "Second Floor", Elevation = elevation, Index = 2 };
            }
            if (text.Contains("roof"))
            {
                return new LevelDefinition { Name = "Roof", Elevation = elevation, Index = 99 };
            }
            if (text.Contains("basement") || text.Contains("bmt"))
            {
                return new LevelDefinition { Name = "Basement", Elevation = elevation, Index = -1 };
            }

            // Try to extract numeric level
            var match = System.Text.RegularExpressions.Regex.Match(text, @"level\s*(\d+)");
            if (match.Success)
            {
                int levelNum = int.Parse(match.Groups[1].Value);
                return new LevelDefinition { Name = $"Level {levelNum}", Elevation = elevation, Index = levelNum };
            }

            return null;
        }

        #endregion

        #region Pattern Matching

        private List<PatternMatch> FindPatternMatches(
            List<GeometryObject> geometry,
            ElementPattern pattern,
            ClassifiedView view)
        {
            var matches = new List<PatternMatch>();

            switch (pattern.GeometryType)
            {
                case GeometryPatternType.ParallelLines:
                    matches.AddRange(FindParallelLineMatches(geometry, pattern));
                    break;

                case GeometryPatternType.SingleLine:
                    matches.AddRange(FindSingleLineMatches(geometry, pattern, view));
                    break;

                case GeometryPatternType.ArcWithLine:
                    matches.AddRange(FindArcWithLineMatches(geometry, pattern));
                    break;

                case GeometryPatternType.TripleParallelLines:
                    matches.AddRange(FindTripleParallelLineMatches(geometry, pattern));
                    break;

                case GeometryPatternType.FilledRectangle:
                case GeometryPatternType.FilledCircle:
                    matches.AddRange(FindFilledShapeMatches(geometry, pattern));
                    break;

                case GeometryPatternType.ParallelLineSeriesWithArrow:
                    matches.AddRange(FindStairPatternMatches(geometry, pattern));
                    break;

                case GeometryPatternType.ClosedPolyline:
                    matches.AddRange(FindClosedPolylineMatches(geometry, pattern));
                    break;
            }

            return matches;
        }

        private List<PatternMatch> FindParallelLineMatches(List<GeometryObject> geometry, ElementPattern pattern)
        {
            var matches = new List<PatternMatch>();
            var lines = geometry.Where(g => g.GeometryType == "Line").ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    var line1 = lines[i];
                    var line2 = lines[j];

                    if (AreParallel(line1, line2, pattern.ToleranceParallel))
                    {
                        double distance = CalculateLineDistance(line1, line2);

                        // Check if distance matches typical wall thickness
                        if (pattern.TypicalThickness != null &&
                            distance >= pattern.TypicalThickness.Min &&
                            distance <= pattern.TypicalThickness.Max)
                        {
                            matches.Add(new PatternMatch
                            {
                                MatchedGeometry = new List<GeometryObject> { line1, line2 },
                                Confidence = pattern.RecognitionConfidence,
                                ExtractedProperties = new Dictionary<string, object>
                                {
                                    ["Thickness"] = distance,
                                    ["Length"] = CalculateLineLength(line1)
                                }
                            });
                        }
                    }
                }
            }

            return matches;
        }

        private List<PatternMatch> FindSingleLineMatches(List<GeometryObject> geometry, ElementPattern pattern, ClassifiedView view)
        {
            var matches = new List<PatternMatch>();
            var lines = geometry.Where(g => g.GeometryType == "Line").ToList();

            foreach (var line in lines)
            {
                double length = CalculateLineLength(line);

                // Only consider lines of reasonable wall length
                if (length >= 500 && length <= 50000)
                {
                    // Look for thickness annotation nearby
                    double thickness = pattern.DefaultThickness;
                    var nearbyDim = FindNearbyDimensionAnnotation(line, view.Annotations);
                    if (nearbyDim != null && nearbyDim.Value >= 100 && nearbyDim.Value <= 600)
                    {
                        thickness = nearbyDim.Value;
                    }

                    matches.Add(new PatternMatch
                    {
                        MatchedGeometry = new List<GeometryObject> { line },
                        Confidence = pattern.RecognitionConfidence * 0.8, // Lower confidence for single line
                        ExtractedProperties = new Dictionary<string, object>
                        {
                            ["Thickness"] = thickness,
                            ["Length"] = length,
                            ["IsCenterline"] = true
                        }
                    });
                }
            }

            return matches;
        }

        private List<PatternMatch> FindArcWithLineMatches(List<GeometryObject> geometry, ElementPattern pattern)
        {
            var matches = new List<PatternMatch>();
            var arcs = geometry.Where(g => g.GeometryType == "Arc").ToList();
            var lines = geometry.Where(g => g.GeometryType == "Line").ToList();

            foreach (var arc in arcs)
            {
                // Find line connected to arc (door leaf)
                var connectedLine = lines.FirstOrDefault(l =>
                    PointsNear(l.StartPoint, arc.StartPoint, 50) ||
                    PointsNear(l.EndPoint, arc.StartPoint, 50));

                if (connectedLine != null)
                {
                    double width = CalculateLineLength(connectedLine);

                    if (pattern.TypicalWidth == null ||
                        (width >= pattern.TypicalWidth.Min && width <= pattern.TypicalWidth.Max))
                    {
                        matches.Add(new PatternMatch
                        {
                            MatchedGeometry = new List<GeometryObject> { arc, connectedLine },
                            Confidence = pattern.RecognitionConfidence,
                            ExtractedProperties = new Dictionary<string, object>
                            {
                                ["Width"] = width,
                                ["SwingAngle"] = arc.SweepAngle,
                                ["SwingDirection"] = DetermineSwingDirection(arc)
                            }
                        });
                    }
                }
            }

            return matches;
        }

        private List<PatternMatch> FindTripleParallelLineMatches(List<GeometryObject> geometry, ElementPattern pattern)
        {
            // Window pattern: three parallel lines (outer frame, glass, outer frame)
            var matches = new List<PatternMatch>();
            // Simplified implementation
            return matches;
        }

        private List<PatternMatch> FindFilledShapeMatches(List<GeometryObject> geometry, ElementPattern pattern)
        {
            var matches = new List<PatternMatch>();
            var shapes = geometry.Where(g => g.IsFilled).ToList();

            foreach (var shape in shapes)
            {
                bool isMatch = false;
                var props = new Dictionary<string, object>();

                if (pattern.GeometryType == GeometryPatternType.FilledRectangle && shape.GeometryType == "Rectangle")
                {
                    double width = shape.Width;
                    double height = shape.Height;
                    double size = Math.Max(width, height);

                    if (pattern.TypicalSize == null || (size >= pattern.TypicalSize.Min && size <= pattern.TypicalSize.Max))
                    {
                        isMatch = true;
                        props["Width"] = width;
                        props["Depth"] = height;
                    }
                }
                else if (pattern.GeometryType == GeometryPatternType.FilledCircle && shape.GeometryType == "Circle")
                {
                    if (pattern.TypicalDiameter == null ||
                        (shape.Radius * 2 >= pattern.TypicalDiameter.Min && shape.Radius * 2 <= pattern.TypicalDiameter.Max))
                    {
                        isMatch = true;
                        props["Diameter"] = shape.Radius * 2;
                    }
                }

                if (isMatch)
                {
                    matches.Add(new PatternMatch
                    {
                        MatchedGeometry = new List<GeometryObject> { shape },
                        Confidence = pattern.RecognitionConfidence,
                        ExtractedProperties = props
                    });
                }
            }

            return matches;
        }

        private List<PatternMatch> FindStairPatternMatches(List<GeometryObject> geometry, ElementPattern pattern)
        {
            var matches = new List<PatternMatch>();
            // Find series of parallel lines that could be stair treads
            return matches;
        }

        private List<PatternMatch> FindClosedPolylineMatches(List<GeometryObject> geometry, ElementPattern pattern)
        {
            var matches = new List<PatternMatch>();
            var polylines = geometry.Where(g => g.GeometryType == "Polyline" && g.IsClosed).ToList();

            foreach (var polyline in polylines)
            {
                matches.Add(new PatternMatch
                {
                    MatchedGeometry = new List<GeometryObject> { polyline },
                    Confidence = pattern.RecognitionConfidence,
                    ExtractedProperties = new Dictionary<string, object>
                    {
                        ["Area"] = CalculatePolylineArea(polyline),
                        ["Perimeter"] = CalculatePolylinePerimeter(polyline)
                    }
                });
            }

            return matches;
        }

        #endregion

        #region Geometry Utilities

        private bool AreParallel(GeometryObject line1, GeometryObject line2, double toleranceDegrees)
        {
            double angle1 = CalculateLineAngle(line1);
            double angle2 = CalculateLineAngle(line2);
            double diff = Math.Abs(angle1 - angle2);

            // Normalize to 0-90 range (parallel lines can differ by 180 degrees)
            if (diff > 90) diff = 180 - diff;

            return diff <= toleranceDegrees;
        }

        private double CalculateLineAngle(GeometryObject line)
        {
            if (line.EndPoint == null) return 0;
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            return Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        private double CalculateLineDistance(GeometryObject line1, GeometryObject line2)
        {
            // Calculate perpendicular distance between parallel lines
            var midPoint1 = new Point2D
            {
                X = (line1.StartPoint.X + (line1.EndPoint?.X ?? line1.StartPoint.X)) / 2,
                Y = (line1.StartPoint.Y + (line1.EndPoint?.Y ?? line1.StartPoint.Y)) / 2
            };

            return PointToLineDistance(midPoint1, line2);
        }

        private double PointToLineDistance(Point2D point, GeometryObject line)
        {
            if (line.EndPoint == null) return double.MaxValue;

            double x0 = point.X, y0 = point.Y;
            double x1 = line.StartPoint.X, y1 = line.StartPoint.Y;
            double x2 = line.EndPoint.X, y2 = line.EndPoint.Y;

            double numerator = Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1);
            double denominator = Math.Sqrt(Math.Pow(y2 - y1, 2) + Math.Pow(x2 - x1, 2));

            return denominator > 0 ? numerator / denominator : 0;
        }

        private double CalculateLineLength(GeometryObject line)
        {
            if (line.EndPoint == null) return 0;
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private bool PointsNear(Point2D p1, Point2D p2, double tolerance)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
        }

        private string DetermineSwingDirection(GeometryObject arc)
        {
            // Determine if door swings left or right based on arc direction
            return arc.SweepAngle > 0 ? "Right" : "Left";
        }

        private BoundingBox2D CalculateBoundingBox(List<GeometryObject> geometry)
        {
            if (geometry.Count == 0) return null;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var geom in geometry)
            {
                minX = Math.Min(minX, geom.StartPoint.X);
                minY = Math.Min(minY, geom.StartPoint.Y);
                maxX = Math.Max(maxX, geom.StartPoint.X);
                maxY = Math.Max(maxY, geom.StartPoint.Y);

                if (geom.EndPoint != null)
                {
                    minX = Math.Min(minX, geom.EndPoint.X);
                    minY = Math.Min(minY, geom.EndPoint.Y);
                    maxX = Math.Max(maxX, geom.EndPoint.X);
                    maxY = Math.Max(maxY, geom.EndPoint.Y);
                }
            }

            return new BoundingBox2D { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }

        private double CalculatePolylineArea(GeometryObject polyline)
        {
            // Shoelace formula
            return 0; // Simplified
        }

        private double CalculatePolylinePerimeter(GeometryObject polyline)
        {
            return 0; // Simplified
        }

        #endregion

        #region Cross-View Correlation

        private async Task<List<ViewCorrelation>> CorrelateElementsAcrossViewsAsync(
            List<RecognizedElement> elements,
            List<ClassifiedView> views,
            CancellationToken cancellationToken)
        {
            var correlations = new List<ViewCorrelation>();

            await Task.Run(() =>
            {
                // Group elements by type
                var elementsByType = elements.GroupBy(e => e.ElementType);

                foreach (var typeGroup in elementsByType)
                {
                    // Separate by view type
                    var planElements = typeGroup.Where(e => e.SourceViewType == ViewType.FloorPlan).ToList();
                    var sectionElements = typeGroup.Where(e => e.SourceViewType == ViewType.Section).ToList();
                    var elevationElements = typeGroup.Where(e => e.SourceViewType == ViewType.Elevation).ToList();

                    // Correlate plan elements with section elements
                    foreach (var planElement in planElements)
                    {
                        // Find section elements at similar X position
                        var matchingSectionElements = sectionElements
                            .Where(s => ElementsAlignInSection(planElement, s))
                            .ToList();

                        foreach (var sectionElement in matchingSectionElements)
                        {
                            var correlation = new ViewCorrelation
                            {
                                CorrelationId = Guid.NewGuid().ToString("N").Substring(0, 12),
                                PlanElementId = planElement.ElementId,
                                SectionElementId = sectionElement.ElementId,
                                CorrelationType = CorrelationType.PlanToSection,
                                Confidence = CalculateCorrelationConfidence(planElement, sectionElement),
                                HeightFromSection = ExtractHeightFromSection(sectionElement),
                                BaseOffsetFromSection = ExtractBaseOffsetFromSection(sectionElement)
                            };

                            correlations.Add(correlation);
                            OnViewCorrelationFound(correlation);
                        }

                        // Find elevation elements at similar position
                        var matchingElevationElements = elevationElements
                            .Where(e => ElementsAlignInElevation(planElement, e))
                            .ToList();

                        foreach (var elevElement in matchingElevationElements)
                        {
                            var correlation = new ViewCorrelation
                            {
                                CorrelationId = Guid.NewGuid().ToString("N").Substring(0, 12),
                                PlanElementId = planElement.ElementId,
                                ElevationElementId = elevElement.ElementId,
                                CorrelationType = CorrelationType.PlanToElevation,
                                Confidence = CalculateCorrelationConfidence(planElement, elevElement),
                                SillHeightFromElevation = ExtractSillHeight(elevElement),
                                HeadHeightFromElevation = ExtractHeadHeight(elevElement)
                            };

                            correlations.Add(correlation);
                        }
                    }
                }
            }, cancellationToken);

            return correlations;
        }

        private bool ElementsAlignInSection(RecognizedElement planElement, RecognizedElement sectionElement)
        {
            // Check if X coordinate of plan element matches section position
            if (planElement.BoundingBox2D == null || sectionElement.BoundingBox2D == null)
                return false;

            double planMidX = (planElement.BoundingBox2D.MinX + planElement.BoundingBox2D.MaxX) / 2;
            double sectionMidX = (sectionElement.BoundingBox2D.MinX + sectionElement.BoundingBox2D.MaxX) / 2;

            return Math.Abs(planMidX - sectionMidX) < 500; // 500mm tolerance
        }

        private bool ElementsAlignInElevation(RecognizedElement planElement, RecognizedElement elevElement)
        {
            // Similar logic for elevation correlation
            return false; // Simplified
        }

        private double CalculateCorrelationConfidence(RecognizedElement elem1, RecognizedElement elem2)
        {
            double confidence = 0.5;

            // Same element type increases confidence
            if (elem1.ElementType == elem2.ElementType)
                confidence += 0.3;

            // Similar dimensions increase confidence
            if (elem1.Properties.TryGetValue("Width", out var w1) &&
                elem2.Properties.TryGetValue("Width", out var w2))
            {
                double width1 = Convert.ToDouble(w1);
                double width2 = Convert.ToDouble(w2);
                if (Math.Abs(width1 - width2) < 50)
                    confidence += 0.2;
            }

            return Math.Min(confidence, 1.0);
        }

        private double ExtractHeightFromSection(RecognizedElement sectionElement)
        {
            if (sectionElement.Properties.TryGetValue("Height", out var height))
                return Convert.ToDouble(height);

            // Calculate from bounding box
            if (sectionElement.BoundingBox2D != null)
                return sectionElement.BoundingBox2D.MaxY - sectionElement.BoundingBox2D.MinY;

            return 2700; // Default wall height
        }

        private double ExtractBaseOffsetFromSection(RecognizedElement sectionElement)
        {
            if (sectionElement.BoundingBox2D != null)
                return sectionElement.BoundingBox2D.MinY;
            return 0;
        }

        private double ExtractSillHeight(RecognizedElement elevElement)
        {
            if (elevElement.Properties.TryGetValue("SillHeight", out var sill))
                return Convert.ToDouble(sill);
            return 900; // Default sill height
        }

        private double ExtractHeadHeight(RecognizedElement elevElement)
        {
            if (elevElement.Properties.TryGetValue("HeadHeight", out var head))
                return Convert.ToDouble(head);
            return 2100; // Default head height
        }

        #endregion

        #region Element Merging

        private async Task<List<MergedElement>> MergeCorrelatedElementsAsync(
            List<RecognizedElement> elements,
            List<ViewCorrelation> correlations,
            List<LevelDefinition> levels,
            CancellationToken cancellationToken)
        {
            var mergedElements = new List<MergedElement>();

            await Task.Run(() =>
            {
                // Create correlation lookup
                var correlationsByPlanId = correlations
                    .Where(c => c.PlanElementId != null)
                    .GroupBy(c => c.PlanElementId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var processedIds = new HashSet<string>();

                foreach (var element in elements.Where(e => e.SourceViewType == ViewType.FloorPlan))
                {
                    if (processedIds.Contains(element.ElementId))
                        continue;

                    var merged = new MergedElement
                    {
                        MergedId = Guid.NewGuid().ToString("N").Substring(0, 12),
                        ElementType = element.ElementType,
                        PlanElement = element,
                        SourceElements = new List<string> { element.ElementId }
                    };

                    // Get correlated elements
                    if (correlationsByPlanId.TryGetValue(element.ElementId, out var relatedCorrelations))
                    {
                        foreach (var corr in relatedCorrelations)
                        {
                            if (corr.SectionElementId != null)
                            {
                                merged.SectionElement = elements.FirstOrDefault(e => e.ElementId == corr.SectionElementId);
                                merged.SourceElements.Add(corr.SectionElementId);
                                merged.Height = corr.HeightFromSection;
                                merged.BaseOffset = corr.BaseOffsetFromSection;
                            }

                            if (corr.ElevationElementId != null)
                            {
                                merged.ElevationElement = elements.FirstOrDefault(e => e.ElementId == corr.ElevationElementId);
                                merged.SourceElements.Add(corr.ElevationElementId);
                                merged.SillHeight = corr.SillHeightFromElevation;
                                merged.HeadHeight = corr.HeadHeightFromElevation;
                            }
                        }
                    }

                    // Determine level
                    merged.Level = DetermineElementLevel(element, levels);

                    // Build 3D geometry from merged data
                    merged.Geometry3D = Build3DGeometry(merged);

                    // Calculate confidence
                    merged.OverallConfidence = CalculateMergedConfidence(merged);

                    mergedElements.Add(merged);
                    processedIds.UnionWith(merged.SourceElements);
                }

                // Handle elements only in sections/elevations (not found in plan)
                foreach (var element in elements.Where(e =>
                    e.SourceViewType != ViewType.FloorPlan && !processedIds.Contains(e.ElementId)))
                {
                    var merged = new MergedElement
                    {
                        MergedId = Guid.NewGuid().ToString("N").Substring(0, 12),
                        ElementType = element.ElementType,
                        SourceElements = new List<string> { element.ElementId },
                        OverallConfidence = element.Confidence * 0.7 // Lower confidence without plan
                    };

                    if (element.SourceViewType == ViewType.Section)
                        merged.SectionElement = element;
                    else if (element.SourceViewType == ViewType.Elevation)
                        merged.ElevationElement = element;

                    mergedElements.Add(merged);
                }
            }, cancellationToken);

            return mergedElements;
        }

        private LevelDefinition DetermineElementLevel(RecognizedElement element, List<LevelDefinition> levels)
        {
            // Default to ground floor if not determined
            return levels.FirstOrDefault(l => l.Index == 0) ?? levels.FirstOrDefault();
        }

        private Geometry3D Build3DGeometry(MergedElement merged)
        {
            var geometry = new Geometry3D();

            if (merged.PlanElement?.BoundingBox2D != null)
            {
                var bb = merged.PlanElement.BoundingBox2D;
                double baseZ = merged.Level?.Elevation ?? 0;
                double height = merged.Height ?? 2700;

                geometry.BoundingBox = new BoundingBox3D
                {
                    MinX = bb.MinX,
                    MinY = bb.MinY,
                    MinZ = baseZ + (merged.BaseOffset ?? 0),
                    MaxX = bb.MaxX,
                    MaxY = bb.MaxY,
                    MaxZ = baseZ + (merged.BaseOffset ?? 0) + height
                };

                // Extract key dimensions
                if (merged.PlanElement.Properties.TryGetValue("Thickness", out var thickness))
                    geometry.Width = Convert.ToDouble(thickness);

                if (merged.PlanElement.Properties.TryGetValue("Length", out var length))
                    geometry.Length = Convert.ToDouble(length);

                geometry.Height = height;
            }

            return geometry;
        }

        private double CalculateMergedConfidence(MergedElement merged)
        {
            double confidence = 0;
            int sources = 0;

            if (merged.PlanElement != null)
            {
                confidence += merged.PlanElement.Confidence;
                sources++;
            }

            if (merged.SectionElement != null)
            {
                confidence += merged.SectionElement.Confidence * 0.3; // Height info bonus
                sources++;
            }

            if (merged.ElevationElement != null)
            {
                confidence += merged.ElevationElement.Confidence * 0.2; // Opening info bonus
                sources++;
            }

            // Bonus for multiple view correlation
            if (sources > 1)
                confidence *= 1.1;

            return Math.Min(confidence, 1.0);
        }

        #endregion

        #region Utilities

        private LayerMapping GetLayerMapping(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return null;

            // Direct match
            if (_layerMappings.TryGetValue(layerName, out var mapping))
                return mapping;

            // Partial match
            var upperLayer = layerName.ToUpper();
            foreach (var kvp in _layerMappings)
            {
                if (upperLayer.Contains(kvp.Key) || kvp.Key.Contains(upperLayer))
                    return kvp.Value;
            }

            return null;
        }

        private Dictionary<string, object> ExtractPropertiesFromMatch(PatternMatch match, ElementPattern pattern)
        {
            var props = new Dictionary<string, object>(match.ExtractedProperties);
            props["PatternId"] = pattern.PatternId;
            props["ElementType"] = pattern.ElementType;
            return props;
        }

        private List<ExtractedDimension> ExtractNearbyDimensions(
            List<GeometryObject> geometry,
            List<AnnotationObject> annotations)
        {
            var dimensions = new List<ExtractedDimension>();

            if (geometry.Count == 0 || annotations == null)
                return dimensions;

            var boundingBox = CalculateBoundingBox(geometry);
            if (boundingBox == null)
                return dimensions;

            // Find dimension annotations near the geometry
            foreach (var annotation in annotations.Where(a => a.AnnotationType == "Dimension"))
            {
                if (IsNearBoundingBox(annotation.Position, boundingBox, 1000))
                {
                    dimensions.Add(new ExtractedDimension
                    {
                        Value = annotation.DimensionValue ?? 0,
                        Unit = annotation.Unit ?? "mm",
                        Direction = DetermineDimensionDirection(annotation)
                    });
                }
            }

            return dimensions;
        }

        private DimensionAnnotation FindNearbyDimensionAnnotation(GeometryObject geom, List<AnnotationObject> annotations)
        {
            if (annotations == null) return null;

            foreach (var ann in annotations.Where(a => a.AnnotationType == "Dimension"))
            {
                if (ann.DimensionValue > 0 && IsNearGeometry(ann.Position, geom, 500))
                {
                    return new DimensionAnnotation { Value = ann.DimensionValue ?? 0, Unit = ann.Unit };
                }
            }

            return null;
        }

        private bool IsNearBoundingBox(Point2D point, BoundingBox2D box, double tolerance)
        {
            return point.X >= box.MinX - tolerance && point.X <= box.MaxX + tolerance &&
                   point.Y >= box.MinY - tolerance && point.Y <= box.MaxY + tolerance;
        }

        private bool IsNearGeometry(Point2D point, GeometryObject geom, double tolerance)
        {
            double dist = Math.Min(
                Distance(point, geom.StartPoint),
                geom.EndPoint != null ? Distance(point, geom.EndPoint) : double.MaxValue);
            return dist <= tolerance;
        }

        private double Distance(Point2D p1, Point2D p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        private string DetermineDimensionDirection(AnnotationObject annotation)
        {
            // Determine if dimension is horizontal, vertical, or diagonal
            return annotation.Rotation < 45 || annotation.Rotation > 135 ? "Horizontal" : "Vertical";
        }

        private List<string> ValidateInterpretation(DrawingInterpretationResult result)
        {
            var warnings = new List<string>();

            // Check for uncorrelated elements
            var uncorrelatedCount = result.RecognizedElements.Count -
                result.ViewCorrelations.SelectMany(c => new[] { c.PlanElementId, c.SectionElementId, c.ElevationElementId })
                    .Where(id => id != null).Distinct().Count();

            if (uncorrelatedCount > result.RecognizedElements.Count * 0.3)
            {
                warnings.Add($"High number of uncorrelated elements ({uncorrelatedCount}). " +
                    "Consider reviewing view alignment.");
            }

            // Check for missing height data
            var elementsWithoutHeight = result.MergedElements?.Count(m => m.Height == null || m.Height == 0) ?? 0;
            if (elementsWithoutHeight > 0)
            {
                warnings.Add($"{elementsWithoutHeight} elements have no height data. " +
                    "Default heights will be used.");
            }

            return warnings;
        }

        private void ReportProgress(IProgress<InterpretationProgress> progress, int percent, string message)
        {
            progress?.Report(new InterpretationProgress { PercentComplete = percent, CurrentOperation = message });
        }

        private void OnElementRecognized(RecognizedElement element)
        {
            ElementRecognized?.Invoke(this, new ElementRecognizedEventArgs(element));
        }

        private void OnViewCorrelationFound(ViewCorrelation correlation)
        {
            ViewCorrelationFound?.Invoke(this, new CorrelationFoundEventArgs(correlation));
        }

        #endregion
    }

    #region Data Models

    public enum ViewType
    {
        FloorPlan,
        Section,
        Elevation,
        RoofPlan,
        ReflectedCeilingPlan,
        FoundationPlan,
        DetailView,
        Unknown
    }

    public enum HeightSource
    {
        FromSection,
        FromElevation,
        FromAnnotation,
        Default
    }

    public enum GeometryPatternType
    {
        ParallelLines,
        SingleLine,
        TripleParallelLines,
        ArcWithLine,
        Rectangle,
        RectangleInWall,
        FilledRectangle,
        FilledCircle,
        FilledRegion,
        ParallelLinesWithArrow,
        ParallelLineSeriesWithArrow,
        ClosedPolyline
    }

    public enum CorrelationType
    {
        PlanToSection,
        PlanToElevation,
        SectionToElevation
    }

    public class ViewInterpretationRules
    {
        public ViewType ViewType { get; set; }
        public List<string> ElementsToExtract { get; set; }
        public bool ExtractPosition { get; set; }
        public bool ExtractRotation { get; set; }
        public bool ExtractWidth { get; set; }
        public bool ExtractLength { get; set; }
        public bool ExtractHeight { get; set; }
        public bool ExtractDepth { get; set; }
        public bool ExtractSlope { get; set; }
        public bool ExtractLevelElevations { get; set; }
        public bool ExtractRidgeDirection { get; set; }
        public bool ExtractCeilingHeight { get; set; }
        public bool ProvideHeightData { get; set; }
        public bool ProvideOpeningPositions { get; set; }
        public HeightSource HeightSource { get; set; }
        public double DefaultHeight { get; set; }
        public List<string> RecognitionPriority { get; set; }
    }

    public class ElementPattern
    {
        public string PatternId { get; set; }
        public string ElementType { get; set; }
        public string Description { get; set; }
        public GeometryPatternType GeometryType { get; set; }
        public int MinLineCount { get; set; }
        public int MaxLineCount { get; set; }
        public double ToleranceParallel { get; set; }
        public double ToleranceSpacing { get; set; }
        public Range TypicalThickness { get; set; }
        public Range TypicalWidth { get; set; }
        public Range TypicalSize { get; set; }
        public Range TypicalDiameter { get; set; }
        public Range TypicalTreadDepth { get; set; }
        public double DefaultThickness { get; set; }
        public bool RequiresThicknessAnnotation { get; set; }
        public bool RequiresWallGap { get; set; }
        public bool RequiresWallContext { get; set; }
        public bool RequiresGridContext { get; set; }
        public bool RequiresRoomTag { get; set; }
        public List<string> HatchPatterns { get; set; }
        public double RecognitionConfidence { get; set; }
    }

    public class Range
    {
        public double Min { get; set; }
        public double Max { get; set; }
    }

    public class LayerMapping
    {
        public string RevitCategory { get; set; }
        public string ElementType { get; set; }
        public string WallFunction { get; set; }
        public int Priority { get; set; }
        public bool IsAnnotation { get; set; }
    }

    public class DrawingSheetInput
    {
        public string SheetName { get; set; }
        public List<DrawingViewInput> Views { get; set; } = new List<DrawingViewInput>();
    }

    public class DrawingViewInput
    {
        public string ViewName { get; set; }
        public List<GeometryObject> Geometry { get; set; } = new List<GeometryObject>();
        public List<AnnotationObject> Annotations { get; set; } = new List<AnnotationObject>();
    }

    public class GeometryObject
    {
        public string GeometryType { get; set; }
        public string Layer { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double SweepAngle { get; set; }
        public bool IsFilled { get; set; }
        public bool IsClosed { get; set; }
        public string HatchPattern { get; set; }
        public List<Point2D> Vertices { get; set; }
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class AnnotationObject
    {
        public string AnnotationType { get; set; }
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public double Rotation { get; set; }
        public double? DimensionValue { get; set; }
        public string Unit { get; set; }
    }

    public class ClassifiedView
    {
        public DrawingViewInput OriginalView { get; set; }
        public string ViewName { get; set; }
        public ViewType ClassifiedType { get; set; }
        public double ClassificationConfidence { get; set; }
        public List<GeometryObject> Geometry { get; set; }
        public List<AnnotationObject> Annotations { get; set; }
    }

    public class RecognizedElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public ViewType SourceViewType { get; set; }
        public string SourceViewName { get; set; }
        public string PatternUsed { get; set; }
        public double Confidence { get; set; }
        public List<GeometryObject> Geometry2D { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public string Layer { get; set; }
        public BoundingBox2D BoundingBox2D { get; set; }
        public List<ExtractedDimension> ExtractedDimensions { get; set; }
    }

    public class BoundingBox2D
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public class ExtractedDimension
    {
        public double Value { get; set; }
        public string Unit { get; set; }
        public string Direction { get; set; }
    }

    public class DimensionAnnotation
    {
        public double Value { get; set; }
        public string Unit { get; set; }
    }

    public class LevelDefinition
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
        public int Index { get; set; }
    }

    public class PatternMatch
    {
        public List<GeometryObject> MatchedGeometry { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> ExtractedProperties { get; set; }
    }

    public class ViewCorrelation
    {
        public string CorrelationId { get; set; }
        public string PlanElementId { get; set; }
        public string SectionElementId { get; set; }
        public string ElevationElementId { get; set; }
        public CorrelationType CorrelationType { get; set; }
        public double Confidence { get; set; }
        public double? HeightFromSection { get; set; }
        public double? BaseOffsetFromSection { get; set; }
        public double? SillHeightFromElevation { get; set; }
        public double? HeadHeightFromElevation { get; set; }
    }

    public class MergedElement
    {
        public string MergedId { get; set; }
        public string ElementType { get; set; }
        public RecognizedElement PlanElement { get; set; }
        public RecognizedElement SectionElement { get; set; }
        public RecognizedElement ElevationElement { get; set; }
        public List<string> SourceElements { get; set; }
        public LevelDefinition Level { get; set; }
        public double? Height { get; set; }
        public double? BaseOffset { get; set; }
        public double? SillHeight { get; set; }
        public double? HeadHeight { get; set; }
        public Geometry3D Geometry3D { get; set; }
        public double OverallConfidence { get; set; }
    }

    public class Geometry3D
    {
        public BoundingBox3D BoundingBox { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
    }

    public class BoundingBox3D
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }

    public class DrawingInterpretationResult
    {
        public string SessionId { get; set; }
        public string SheetName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<RecognizedElement> RecognizedElements { get; set; }
        public List<ViewCorrelation> ViewCorrelations { get; set; }
        public List<LevelDefinition> LevelDefinitions { get; set; }
        public List<MergedElement> MergedElements { get; set; }
        public int ElementCount { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class InterpretationProgress
    {
        public int PercentComplete { get; set; }
        public string CurrentOperation { get; set; }
    }

    public class InterpretationOptions
    {
        public bool InterpretUnmappedLayers { get; set; } = false;
        public double ConfidenceThreshold { get; set; } = 0.7;
        public bool AutoCreateTypes { get; set; } = true;
    }

    public class InterpretationSession
    {
        public string SessionId { get; set; }
        public string SheetName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int ElementsRecognized { get; set; }
        public bool Success { get; set; }
    }

    public class ElementRecognizedEventArgs : EventArgs
    {
        public RecognizedElement Element { get; }
        public ElementRecognizedEventArgs(RecognizedElement element) { Element = element; }
    }

    public class CorrelationFoundEventArgs : EventArgs
    {
        public ViewCorrelation Correlation { get; }
        public CorrelationFoundEventArgs(ViewCorrelation correlation) { Correlation = correlation; }
    }

    #endregion
}
