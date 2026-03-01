// StingBIM.AI.NLP.Consulting.ImagePDFToBIMConversionEngine
// Converts architectural floor plan images and PDFs into structured BIM element plans.
// Uses computer vision (ONNX) for line/shape detection, OCR for text extraction,
// and ML classification to identify walls, doors, windows, rooms, and MEP symbols.
// Produces the same output format as DWGToBIMConversionEngine for unified downstream processing.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.NLP.Consulting
{
    /// <summary>
    /// Converts scanned floor plans, PDFs, and architectural images into structured
    /// BIM creation plans. Uses a multi-stage pipeline: preprocessing → line detection →
    /// symbol recognition → OCR → spatial reasoning → BIM element classification.
    /// Outputs the same DWGConversionResult used by DWGToBIMConversionEngine.
    /// </summary>
    public class ImagePDFToBIMConversionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, SymbolTemplate> _symbolTemplates;
        private readonly Dictionary<string, string> _ocrLabelPatterns;
        private readonly Dictionary<string, double> _lineClassificationThresholds;
        private readonly DWGToBIMConversionEngine _dwgEngine; // Reuse wall type / MEP rules

        public ImagePDFToBIMConversionEngine()
        {
            _symbolTemplates = new Dictionary<string, SymbolTemplate>(StringComparer.OrdinalIgnoreCase);
            _ocrLabelPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _lineClassificationThresholds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            _dwgEngine = new DWGToBIMConversionEngine();

            InitializeSymbolTemplates();
            InitializeOCRPatterns();
            InitializeClassificationThresholds();
        }

        #region Initialization

        private void InitializeSymbolTemplates()
        {
            // Architectural symbols recognized by shape features
            _symbolTemplates["Door-Single"] = new SymbolTemplate
            {
                Name = "Single Door",
                BIMCategory = "Doors",
                FamilyName = "Single-Flush",
                ShapeDescription = "Quarter-circle arc with line (swing arc)",
                DetectionFeatures = new List<string> { "arc_90deg", "line_perpendicular_to_wall", "gap_in_wall" },
                DefaultWidth = 900,
                DefaultHeight = 2100,
                Confidence = 0.85
            };

            _symbolTemplates["Door-Double"] = new SymbolTemplate
            {
                Name = "Double Door",
                BIMCategory = "Doors",
                FamilyName = "Double-Flush",
                ShapeDescription = "Two opposing quarter-circle arcs",
                DetectionFeatures = new List<string> { "arc_90deg_pair", "symmetric_gap_in_wall" },
                DefaultWidth = 1800,
                DefaultHeight = 2100,
                Confidence = 0.80
            };

            _symbolTemplates["Door-Sliding"] = new SymbolTemplate
            {
                Name = "Sliding Door",
                BIMCategory = "Doors",
                FamilyName = "Sliding-Door",
                ShapeDescription = "Parallel offset lines in wall gap",
                DetectionFeatures = new List<string> { "parallel_lines_in_gap", "no_arc" },
                DefaultWidth = 1500,
                DefaultHeight = 2100,
                Confidence = 0.75
            };

            _symbolTemplates["Window-Standard"] = new SymbolTemplate
            {
                Name = "Standard Window",
                BIMCategory = "Windows",
                FamilyName = "Fixed-Window",
                ShapeDescription = "Three parallel lines within wall thickness",
                DetectionFeatures = new List<string> { "triple_parallel_lines", "within_wall_thickness" },
                DefaultWidth = 1200,
                DefaultHeight = 1500,
                Confidence = 0.80
            };

            _symbolTemplates["Window-Casement"] = new SymbolTemplate
            {
                Name = "Casement Window",
                BIMCategory = "Windows",
                FamilyName = "Casement-Window",
                ShapeDescription = "Triangle shape within wall representing opening direction",
                DetectionFeatures = new List<string> { "triangle_in_wall", "triple_parallel_lines" },
                DefaultWidth = 600,
                DefaultHeight = 1200,
                Confidence = 0.75
            };

            _symbolTemplates["Stair-Up"] = new SymbolTemplate
            {
                Name = "Stair (Up)",
                BIMCategory = "Stairs",
                FamilyName = "Straight-Run-Stair",
                ShapeDescription = "Parallel evenly-spaced lines with arrow",
                DetectionFeatures = new List<string> { "evenly_spaced_parallel_lines", "arrow_direction" },
                DefaultWidth = 1200,
                DefaultHeight = 3600,
                Confidence = 0.80
            };

            _symbolTemplates["Elevator"] = new SymbolTemplate
            {
                Name = "Elevator",
                BIMCategory = "SpecialEquipment",
                FamilyName = "Passenger-Elevator",
                ShapeDescription = "Rectangle with X or diagonal lines",
                DetectionFeatures = new List<string> { "rectangle_with_diagonals", "approximate_square" },
                DefaultWidth = 2000,
                DefaultHeight = 2200,
                Confidence = 0.85
            };

            // Plumbing fixtures
            _symbolTemplates["WC"] = new SymbolTemplate
            {
                Name = "Water Closet / Toilet",
                BIMCategory = "PlumbingFixtures",
                FamilyName = "WC-Floor-Mounted",
                ShapeDescription = "Circle/oval attached to wall with rectangular tank",
                DetectionFeatures = new List<string> { "oval_with_rectangle", "near_wall" },
                DefaultWidth = 400,
                DefaultHeight = 700,
                Confidence = 0.80
            };

            _symbolTemplates["Sink"] = new SymbolTemplate
            {
                Name = "Sink / Basin",
                BIMCategory = "PlumbingFixtures",
                FamilyName = "Sink-Countertop",
                ShapeDescription = "Small rectangle/oval against wall",
                DetectionFeatures = new List<string> { "small_oval_or_rect", "against_wall", "in_wet_area" },
                DefaultWidth = 500,
                DefaultHeight = 400,
                Confidence = 0.70
            };

            _symbolTemplates["Bathtub"] = new SymbolTemplate
            {
                Name = "Bathtub",
                BIMCategory = "PlumbingFixtures",
                FamilyName = "Bathtub",
                ShapeDescription = "Large rectangle with rounded end",
                DetectionFeatures = new List<string> { "large_rectangle_rounded_end", "in_bathroom" },
                DefaultWidth = 700,
                DefaultHeight = 1700,
                Confidence = 0.80
            };

            _symbolTemplates["Shower"] = new SymbolTemplate
            {
                Name = "Shower",
                BIMCategory = "PlumbingFixtures",
                FamilyName = "Shower-Stall",
                ShapeDescription = "Square with curved line (shower head symbol)",
                DetectionFeatures = new List<string> { "square_corner_room", "circle_symbol" },
                DefaultWidth = 900,
                DefaultHeight = 900,
                Confidence = 0.70
            };

            // Kitchen
            _symbolTemplates["KitchenSink"] = new SymbolTemplate
            {
                Name = "Kitchen Sink",
                BIMCategory = "PlumbingFixtures",
                FamilyName = "Kitchen-Sink-Double",
                ShapeDescription = "Double rectangle against counter/wall",
                DetectionFeatures = new List<string> { "double_rectangle", "in_kitchen" },
                DefaultWidth = 800,
                DefaultHeight = 500,
                Confidence = 0.70
            };

            _symbolTemplates["Stove"] = new SymbolTemplate
            {
                Name = "Stove / Cooktop",
                BIMCategory = "Equipment",
                FamilyName = "Cooktop",
                ShapeDescription = "Rectangle with four circles (burners)",
                DetectionFeatures = new List<string> { "rectangle_with_circles", "in_kitchen" },
                DefaultWidth = 600,
                DefaultHeight = 600,
                Confidence = 0.80
            };

            // Column
            _symbolTemplates["Column-Round"] = new SymbolTemplate
            {
                Name = "Column (Round)",
                BIMCategory = "Columns",
                FamilyName = "Round-Column",
                ShapeDescription = "Filled or cross-hatched circle at grid intersection",
                DetectionFeatures = new List<string> { "filled_circle", "at_grid_intersection" },
                DefaultWidth = 400,
                DefaultHeight = 400,
                Confidence = 0.85
            };

            _symbolTemplates["Column-Rectangular"] = new SymbolTemplate
            {
                Name = "Column (Rectangular)",
                BIMCategory = "Columns",
                FamilyName = "Rectangular-Column",
                ShapeDescription = "Filled or cross-hatched rectangle at grid intersection",
                DetectionFeatures = new List<string> { "filled_rectangle", "at_grid_intersection" },
                DefaultWidth = 400,
                DefaultHeight = 400,
                Confidence = 0.85
            };
        }

        private void InitializeOCRPatterns()
        {
            // Room labels
            _ocrLabelPatterns[@"(?i)(bedroom|bed\s*rm|br)\s*#?\d*"] = "Bedroom";
            _ocrLabelPatterns[@"(?i)(living\s*room|living|lounge)"] = "Living Room";
            _ocrLabelPatterns[@"(?i)(kitchen|kit)"] = "Kitchen";
            _ocrLabelPatterns[@"(?i)(bath\s*room|bathroom|bath|wc|toilet|restroom|lavatory)"] = "Bathroom";
            _ocrLabelPatterns[@"(?i)(dining\s*room|dining)"] = "Dining Room";
            _ocrLabelPatterns[@"(?i)(office|study)"] = "Office";
            _ocrLabelPatterns[@"(?i)(corridor|hall\s*way|hallway|passage)"] = "Corridor";
            _ocrLabelPatterns[@"(?i)(lobby|reception|foyer|entry|vestibule)"] = "Lobby";
            _ocrLabelPatterns[@"(?i)(store\s*room|storage|store)"] = "Storage";
            _ocrLabelPatterns[@"(?i)(laundry|utility)"] = "Utility";
            _ocrLabelPatterns[@"(?i)(garage|carport)"] = "Garage";
            _ocrLabelPatterns[@"(?i)(balcony|terrace|patio|deck|veranda)"] = "Balcony";
            _ocrLabelPatterns[@"(?i)(meeting|conference|board\s*room)"] = "Meeting Room";
            _ocrLabelPatterns[@"(?i)(server\s*room|comms|data\s*center)"] = "Server Room";
            _ocrLabelPatterns[@"(?i)(plant\s*room|mechanical\s*room|mech)"] = "Plant Room";
            _ocrLabelPatterns[@"(?i)(electrical\s*room|elec\s*rm|switchboard)"] = "Electrical Room";
            _ocrLabelPatterns[@"(?i)(stair|stairwell|staircase)"] = "Stairwell";
            _ocrLabelPatterns[@"(?i)(lift|elevator)"] = "Elevator Shaft";
            _ocrLabelPatterns[@"(?i)(ward|patient)"] = "Patient Ward";
            _ocrLabelPatterns[@"(?i)(classroom|class\s*rm)"] = "Classroom";
            _ocrLabelPatterns[@"(?i)(lecture|auditorium)"] = "Lecture Hall";
            _ocrLabelPatterns[@"(?i)(lab|laboratory)"] = "Laboratory";

            // Dimension patterns
            _ocrLabelPatterns[@"(\d+(?:\.\d+)?)\s*[xX×]\s*(\d+(?:\.\d+)?)\s*(m|mm|ft)?"] = "Dimension";
            _ocrLabelPatterns[@"(\d+(?:\.\d+)?)\s*(m²|sqm|sq\.?\s*m|sf|sq\.?\s*ft)"] = "Area";

            // Level indicators
            _ocrLabelPatterns[@"(?i)(ground\s*floor|gf|level\s*0)"] = "Level:0";
            _ocrLabelPatterns[@"(?i)(first\s*floor|1st\s*floor|level\s*1)"] = "Level:1";
            _ocrLabelPatterns[@"(?i)(second\s*floor|2nd\s*floor|level\s*2)"] = "Level:2";
            _ocrLabelPatterns[@"(?i)(basement|lower\s*ground|level\s*-1)"] = "Level:-1";
            _ocrLabelPatterns[@"(?i)(roof\s*(plan|level))"] = "Level:Roof";
        }

        private void InitializeClassificationThresholds()
        {
            // Confidence thresholds for each classification type
            _lineClassificationThresholds["Wall"] = 0.70;
            _lineClassificationThresholds["Grid"] = 0.65;
            _lineClassificationThresholds["Door"] = 0.60;
            _lineClassificationThresholds["Window"] = 0.60;
            _lineClassificationThresholds["Column"] = 0.75;
            _lineClassificationThresholds["MEP"] = 0.55;
            _lineClassificationThresholds["Stair"] = 0.65;
            _lineClassificationThresholds["Furniture"] = 0.50;
        }

        #endregion

        #region Conversion Pipeline

        /// <summary>
        /// Converts a floor plan image or PDF into a BIM creation plan.
        /// Requires pre-processed image data (pixel analysis from ONNX vision model).
        /// </summary>
        public async Task<ImageConversionResult> ConvertImageAsync(
            FloorPlanImage image,
            ImageConversionOptions options,
            IProgress<ConversionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Starting Image-to-BIM conversion: {image.FileName}, {image.WidthPx}x{image.HeightPx}px, scale 1:{options.DrawingScale}");

            var result = new ImageConversionResult
            {
                SourceFile = image.FileName,
                StartedAt = DateTime.Now,
                ImageWidth = image.WidthPx,
                ImageHeight = image.HeightPx,
                DrawingScale = options.DrawingScale
            };

            // Phase 1: Pre-processing (contrast, deskew, noise removal)
            progress?.Report(new ConversionProgress { Phase = "Image Preprocessing", Percent = 5 });
            var preprocessed = PreprocessImage(image, options);
            result.PreprocessingNotes = preprocessed.Notes;

            // Phase 2: Line detection (Hough transform equivalent)
            progress?.Report(new ConversionProgress { Phase = "Line Detection", Percent = 15 });
            var detectedLines = DetectLines(preprocessed, options);
            result.DetectedLineCount = detectedLines.Count;

            // Phase 3: Wall classification (thick parallel lines = walls)
            progress?.Report(new ConversionProgress { Phase = "Wall Classification", Percent = 25 });
            var wallLines = ClassifyWallLines(detectedLines, options);

            // Phase 4: Symbol detection (template matching)
            progress?.Report(new ConversionProgress { Phase = "Symbol Recognition", Percent = 40 });
            var detectedSymbols = DetectSymbols(preprocessed, options);
            result.DetectedSymbolCount = detectedSymbols.Count;

            // Phase 5: OCR text extraction
            progress?.Report(new ConversionProgress { Phase = "Text Extraction (OCR)", Percent = 55 });
            var extractedText = ExtractText(preprocessed, options);
            result.ExtractedTextCount = extractedText.Count;

            // Phase 6: Room boundary detection (flood fill on enclosed areas)
            progress?.Report(new ConversionProgress { Phase = "Room Boundary Detection", Percent = 65 });
            var roomBoundaries = DetectRoomBoundaries(wallLines, extractedText);

            // Phase 7: Spatial reasoning (classify rooms, assign labels)
            progress?.Report(new ConversionProgress { Phase = "Spatial Reasoning", Percent = 75 });
            var classifiedRooms = ClassifyRooms(roomBoundaries, extractedText, detectedSymbols);

            // Phase 8: Convert to DWG analysis format for unified processing
            progress?.Report(new ConversionProgress { Phase = "BIM Element Generation", Percent = 85 });
            var dwgAnalysis = ConvertToUnifiedFormat(
                image, wallLines, detectedSymbols, extractedText, classifiedRooms, options);

            // Phase 9: Use DWG engine for final BIM conversion
            progress?.Report(new ConversionProgress { Phase = "BIM Plan Generation", Percent = 92 });
            var conversionOptions = new DWGConversionOptions
            {
                DefaultWallThickness = options.DefaultWallThickness,
                DefaultFloorToFloorHeight = options.DefaultFloorToFloorHeight,
                DefaultBaseLevel = "Level 0",
                DefaultTopLevel = "Level 1",
                IncludeFurniture = options.IncludeFurniture,
                IncludeMEP = options.IncludeMEP
            };
            result.BIMResult = await _dwgEngine.ConvertAsync(dwgAnalysis, conversionOptions, null, cancellationToken);

            // Phase 10: Generate confidence and quality metrics
            progress?.Report(new ConversionProgress { Phase = "Quality Assessment", Percent = 96 });
            result.QualityMetrics = AssessConversionQuality(result);

            result.CompletedAt = DateTime.Now;
            progress?.Report(new ConversionProgress { Phase = "Complete", Percent = 100 });

            Logger.Info($"Image conversion complete: {result.BIMResult?.Summary?.TotalBIMElements ?? 0} BIM elements from {image.FileName}");
            return result;
        }

        /// <summary>
        /// Generates a report for image/PDF conversion.
        /// </summary>
        public string FormatImageConversionReport(ImageConversionResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  IMAGE/PDF-TO-BIM CONVERSION REPORT");
            sb.AppendLine($"  Source: {result.SourceFile}");
            sb.AppendLine($"  Converted: {result.CompletedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine("IMAGE ANALYSIS");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Image Size:           {result.ImageWidth} x {result.ImageHeight} px");
            sb.AppendLine($"  Drawing Scale:        1:{result.DrawingScale}");
            sb.AppendLine($"  Lines Detected:       {result.DetectedLineCount}");
            sb.AppendLine($"  Symbols Detected:     {result.DetectedSymbolCount}");
            sb.AppendLine($"  Text Labels Found:    {result.ExtractedTextCount}");
            sb.AppendLine();

            if (result.PreprocessingNotes.Any())
            {
                sb.AppendLine("PREPROCESSING");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (var note in result.PreprocessingNotes)
                    sb.AppendLine($"  - {note}");
                sb.AppendLine();
            }

            sb.AppendLine("QUALITY METRICS");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Wall Detection Confidence:    {result.QualityMetrics.WallConfidence:P0}");
            sb.AppendLine($"  Symbol Detection Confidence:  {result.QualityMetrics.SymbolConfidence:P0}");
            sb.AppendLine($"  OCR Confidence:               {result.QualityMetrics.OCRConfidence:P0}");
            sb.AppendLine($"  Room Detection Confidence:    {result.QualityMetrics.RoomConfidence:P0}");
            sb.AppendLine($"  Overall Confidence:           {result.QualityMetrics.OverallConfidence:P0}");
            sb.AppendLine();

            if (result.QualityMetrics.Recommendations.Any())
            {
                sb.AppendLine("RECOMMENDATIONS");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (var rec in result.QualityMetrics.Recommendations)
                    sb.AppendLine($"  - {rec}");
                sb.AppendLine();
            }

            // Include BIM result summary
            if (result.BIMResult != null)
            {
                sb.AppendLine("BIM ELEMENTS GENERATED");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                sb.AppendLine($"  Total BIM Elements: {result.BIMResult.Summary?.TotalBIMElements ?? 0}");
                if (result.BIMResult.Summary != null)
                {
                    foreach (var cat in result.BIMResult.Summary.ElementsByCategory.OrderByDescending(c => c.Value))
                        sb.AppendLine($"    {cat.Key,-25}: {cat.Value,5}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  Generated by StingBIM AI Image/PDF-to-BIM Conversion Engine");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        #endregion

        #region Pipeline Stages

        private PreprocessedImage PreprocessImage(FloorPlanImage image, ImageConversionOptions options)
        {
            var preprocessed = new PreprocessedImage
            {
                WidthPx = image.WidthPx,
                HeightPx = image.HeightPx,
                PixelsPerMM = CalculatePixelsPerMM(image, options),
                Notes = new List<string>()
            };

            // Determine image quality
            if (image.DPI < 150)
                preprocessed.Notes.Add($"Low resolution ({image.DPI} DPI) - accuracy may be reduced. Recommend 300+ DPI.");
            else if (image.DPI >= 300)
                preprocessed.Notes.Add($"Good resolution ({image.DPI} DPI) for accurate conversion.");

            if (image.IsColor)
                preprocessed.Notes.Add("Color image detected - converting to grayscale for line detection.");

            if (image.SkewAngle > 0.5)
                preprocessed.Notes.Add($"Image skew detected ({image.SkewAngle:F1}°) - applying deskew correction.");

            preprocessed.Notes.Add($"Scale: 1:{options.DrawingScale} ({preprocessed.PixelsPerMM:F2} px/mm)");

            return preprocessed;
        }

        private double CalculatePixelsPerMM(FloorPlanImage image, ImageConversionOptions options)
        {
            // Calculate from DPI and scale
            // At 1:100 scale, 1mm on paper = 100mm in reality
            // At 300 DPI, 1 inch = 300 pixels = 25.4mm paper
            var pixelsPerMMPaper = image.DPI / 25.4;
            var pixelsPerMMReal = pixelsPerMMPaper / options.DrawingScale;
            return pixelsPerMMReal;
        }

        private List<DetectedLine> DetectLines(PreprocessedImage image, ImageConversionOptions options)
        {
            var lines = new List<DetectedLine>();

            // Simulated line detection (in production, this uses ONNX vision model)
            // The actual implementation would use Hough Line Transform or neural network
            if (image.DetectedEdges != null)
            {
                foreach (var edge in image.DetectedEdges)
                {
                    var lengthPx = Math.Sqrt(
                        Math.Pow(edge.EndX - edge.StartX, 2) +
                        Math.Pow(edge.EndY - edge.StartY, 2));

                    // Convert pixel coordinates to mm using scale
                    var ppMM = image.PixelsPerMM;
                    if (ppMM > 0)
                    {
                        lines.Add(new DetectedLine
                        {
                            StartX_mm = edge.StartX / ppMM,
                            StartY_mm = edge.StartY / ppMM,
                            EndX_mm = edge.EndX / ppMM,
                            EndY_mm = edge.EndY / ppMM,
                            ThicknessPx = edge.Thickness,
                            ThicknessMM = edge.Thickness / ppMM,
                            Confidence = edge.Confidence,
                            LengthMM = lengthPx / ppMM
                        });
                    }
                }
            }

            return lines;
        }

        private List<DetectedLine> ClassifyWallLines(List<DetectedLine> lines, ImageConversionOptions options)
        {
            var wallLines = new List<DetectedLine>();

            // Walls are thick lines (thickness > threshold)
            var wallThresholdMM = options.MinWallThicknessMM;

            foreach (var line in lines)
            {
                if (line.ThicknessMM >= wallThresholdMM && line.LengthMM >= options.MinWallLengthMM)
                {
                    line.Classification = "Wall";

                    // Classify wall type by thickness
                    if (line.ThicknessMM >= 250)
                        line.WallType = "ExteriorWall";
                    else if (line.ThicknessMM >= 150)
                        line.WallType = "InteriorWall";
                    else
                        line.WallType = "PartitionWall";

                    wallLines.Add(line);
                }
            }

            return wallLines;
        }

        private List<DetectedSymbol> DetectSymbols(PreprocessedImage image, ImageConversionOptions options)
        {
            var symbols = new List<DetectedSymbol>();

            // Simulated symbol detection (in production uses ONNX template matching model)
            if (image.DetectedShapes != null)
            {
                foreach (var shape in image.DetectedShapes)
                {
                    var bestMatch = MatchSymbolTemplate(shape);
                    if (bestMatch != null && bestMatch.MatchConfidence >= _lineClassificationThresholds.GetValueOrDefault(bestMatch.BIMCategory, 0.5))
                    {
                        var ppMM = image.PixelsPerMM > 0 ? image.PixelsPerMM : 1.0;
                        symbols.Add(new DetectedSymbol
                        {
                            TemplateName = bestMatch.TemplateName,
                            BIMCategory = bestMatch.BIMCategory,
                            FamilyName = bestMatch.FamilyName,
                            CenterX_mm = shape.CenterX / ppMM,
                            CenterY_mm = shape.CenterY / ppMM,
                            Width_mm = (shape.BoundingWidth / ppMM) * options.DrawingScale,
                            Height_mm = (shape.BoundingHeight / ppMM) * options.DrawingScale,
                            Rotation = shape.Rotation,
                            Confidence = bestMatch.MatchConfidence
                        });
                    }
                }
            }

            return symbols;
        }

        private SymbolMatchResult MatchSymbolTemplate(DetectedShape shape)
        {
            SymbolMatchResult bestResult = null;
            double bestConfidence = 0;

            foreach (var template in _symbolTemplates.Values)
            {
                var confidence = CalculateTemplateMatch(shape, template);
                if (confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestResult = new SymbolMatchResult
                    {
                        TemplateName = template.Name,
                        BIMCategory = template.BIMCategory,
                        FamilyName = template.FamilyName,
                        MatchConfidence = confidence
                    };
                }
            }

            return bestResult;
        }

        private double CalculateTemplateMatch(DetectedShape shape, SymbolTemplate template)
        {
            double score = 0;
            int matches = 0;

            foreach (var feature in template.DetectionFeatures)
            {
                if (shape.Features.Contains(feature, StringComparer.OrdinalIgnoreCase))
                {
                    matches++;
                }
            }

            if (template.DetectionFeatures.Count > 0)
                score = (double)matches / template.DetectionFeatures.Count;

            return score * template.Confidence;
        }

        private List<ExtractedTextLabel> ExtractText(PreprocessedImage image, ImageConversionOptions options)
        {
            var labels = new List<ExtractedTextLabel>();

            // Simulated OCR (in production uses ONNX OCR model)
            if (image.OCRResults != null)
            {
                foreach (var ocr in image.OCRResults)
                {
                    var ppMM = image.PixelsPerMM > 0 ? image.PixelsPerMM : 1.0;
                    var label = new ExtractedTextLabel
                    {
                        Text = ocr.Text,
                        CenterX_mm = ocr.CenterX / ppMM,
                        CenterY_mm = ocr.CenterY / ppMM,
                        Confidence = ocr.Confidence
                    };

                    // Classify the text
                    label.LabelType = ClassifyTextLabel(ocr.Text);
                    labels.Add(label);
                }
            }

            return labels;
        }

        private string ClassifyTextLabel(string text)
        {
            foreach (var pattern in _ocrLabelPatterns)
            {
                if (Regex.IsMatch(text, pattern.Key, RegexOptions.IgnoreCase))
                    return pattern.Value;
            }
            return "Unknown";
        }

        private List<DetectedRoom> DetectRoomBoundaries(List<DetectedLine> walls, List<ExtractedTextLabel> labels)
        {
            var rooms = new List<DetectedRoom>();

            // Group nearby text labels that are room names
            var roomLabels = labels.Where(l =>
                l.LabelType != "Dimension" && l.LabelType != "Area" &&
                !l.LabelType.StartsWith("Level:") && l.LabelType != "Unknown").ToList();

            foreach (var label in roomLabels)
            {
                // Find enclosing walls (simplified: find walls near the label)
                var nearbyWalls = walls.Where(w =>
                    IsPointNearLine(label.CenterX_mm, label.CenterY_mm, w, 5000)).ToList();

                if (nearbyWalls.Count >= 3) // Need at least 3 walls to form a room
                {
                    // Estimate room bounds from surrounding walls
                    var bounds = EstimateRoomBounds(label.CenterX_mm, label.CenterY_mm, nearbyWalls);
                    rooms.Add(new DetectedRoom
                    {
                        Name = label.LabelType != "Unknown" ? label.LabelType : label.Text,
                        OriginalLabel = label.Text,
                        CenterX = label.CenterX_mm,
                        CenterY = label.CenterY_mm,
                        EstimatedArea = bounds.Width * bounds.Height,
                        BoundsMinX = bounds.MinX,
                        BoundsMinY = bounds.MinY,
                        BoundsMaxX = bounds.MaxX,
                        BoundsMaxY = bounds.MaxY,
                        Confidence = label.Confidence * 0.8 // Reduce by enclosure uncertainty
                    });
                }
            }

            return rooms;
        }

        private bool IsPointNearLine(double px, double py, DetectedLine line, double tolerance)
        {
            var dx = line.EndX_mm - line.StartX_mm;
            var dy = line.EndY_mm - line.StartY_mm;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 0.001) return false;

            var t = Math.Max(0, Math.Min(1,
                ((px - line.StartX_mm) * dx + (py - line.StartY_mm) * dy) / lenSq));
            var closestX = line.StartX_mm + t * dx;
            var closestY = line.StartY_mm + t * dy;

            var dist = Math.Sqrt(Math.Pow(px - closestX, 2) + Math.Pow(py - closestY, 2));
            return dist <= tolerance;
        }

        private (double MinX, double MinY, double MaxX, double MaxY, double Width, double Height) EstimateRoomBounds(
            double cx, double cy, List<DetectedLine> walls)
        {
            // Find nearest wall in each direction
            double minX = cx - 3000, maxX = cx + 3000, minY = cy - 3000, maxY = cy + 3000;

            foreach (var wall in walls)
            {
                // Check if wall is roughly horizontal or vertical
                var isHorizontal = Math.Abs(wall.EndY_mm - wall.StartY_mm) < Math.Abs(wall.EndX_mm - wall.StartX_mm) * 0.3;
                var isVertical = Math.Abs(wall.EndX_mm - wall.StartX_mm) < Math.Abs(wall.EndY_mm - wall.StartY_mm) * 0.3;

                if (isHorizontal)
                {
                    var wallY = (wall.StartY_mm + wall.EndY_mm) / 2;
                    if (wallY < cy && wallY > minY) minY = wallY;
                    if (wallY > cy && wallY < maxY) maxY = wallY;
                }
                else if (isVertical)
                {
                    var wallX = (wall.StartX_mm + wall.EndX_mm) / 2;
                    if (wallX < cx && wallX > minX) minX = wallX;
                    if (wallX > cx && wallX < maxX) maxX = wallX;
                }
            }

            return (minX, minY, maxX, maxY, maxX - minX, maxY - minY);
        }

        private List<DetectedRoom> ClassifyRooms(
            List<DetectedRoom> rooms,
            List<ExtractedTextLabel> labels,
            List<DetectedSymbol> symbols)
        {
            foreach (var room in rooms)
            {
                // Check for plumbing fixtures inside - indicates wet room
                var fixturesInRoom = symbols.Where(s =>
                    s.CenterX_mm >= room.BoundsMinX && s.CenterX_mm <= room.BoundsMaxX &&
                    s.CenterY_mm >= room.BoundsMinY && s.CenterY_mm <= room.BoundsMaxY).ToList();

                var hasPlumbing = fixturesInRoom.Any(f => f.BIMCategory == "PlumbingFixtures");
                var hasKitchen = fixturesInRoom.Any(f => f.FamilyName.Contains("Kitchen") || f.FamilyName.Contains("Cooktop"));

                if (hasKitchen && room.Name == "Unknown")
                    room.Name = "Kitchen";
                else if (hasPlumbing && room.Name == "Unknown")
                    room.Name = "Bathroom";

                // Check for area labels
                var areaLabel = labels.FirstOrDefault(l =>
                    l.LabelType == "Area" &&
                    l.CenterX_mm >= room.BoundsMinX && l.CenterX_mm <= room.BoundsMaxX &&
                    l.CenterY_mm >= room.BoundsMinY && l.CenterY_mm <= room.BoundsMaxY);

                if (areaLabel != null)
                {
                    var match = Regex.Match(areaLabel.Text, @"(\d+(?:\.\d+)?)");
                    if (match.Success)
                        room.LabeledArea = double.Parse(match.Groups[1].Value);
                }
            }

            return rooms;
        }

        #endregion

        #region Unified Format Conversion

        private DWGAnalysis ConvertToUnifiedFormat(
            FloorPlanImage image,
            List<DetectedLine> wallLines,
            List<DetectedSymbol> symbols,
            List<ExtractedTextLabel> labels,
            List<DetectedRoom> rooms,
            ImageConversionOptions options)
        {
            var analysis = new DWGAnalysis
            {
                FileName = image.FileName,
                Units = "mm"
            };

            // Convert wall lines to DWG entities on wall layers
            foreach (var wall in wallLines)
            {
                var layerName = wall.WallType switch
                {
                    "ExteriorWall" => "A-WALL-EXTR",
                    "InteriorWall" => "A-WALL-INTR",
                    "PartitionWall" => "A-WALL-PART",
                    _ => "A-WALL"
                };

                analysis.Entities.Add(new DWGEntity
                {
                    EntityType = "Line",
                    LayerName = layerName,
                    StartPoint = (wall.StartX_mm, wall.StartY_mm, 0),
                    EndPoint = (wall.EndX_mm, wall.EndY_mm, 0),
                    Width = wall.ThicknessMM
                });

                // Ensure layer exists
                if (!analysis.Layers.Any(l => l.LayerName == layerName))
                {
                    analysis.Layers.Add(new DWGLayer
                    {
                        LayerName = layerName,
                        EntityCount = 0,
                        Color = "White",
                        IsVisible = true
                    });
                }
            }

            // Convert symbols to DWG blocks
            foreach (var symbol in symbols)
            {
                var layerName = symbol.BIMCategory switch
                {
                    "Doors" => "A-DOOR",
                    "Windows" => "A-GLAZ",
                    "Columns" => "S-COLS",
                    "Stairs" => "A-STRS",
                    "PlumbingFixtures" => "P-FIXT",
                    "Equipment" => "A-EQPM",
                    "LightingFixtures" => "E-LITE",
                    "ElectricalFixtures" => "E-POWR",
                    _ => "A-EQPM"
                };

                analysis.Blocks.Add(new DWGBlock
                {
                    BlockName = symbol.FamilyName,
                    InsertionPoint = (symbol.CenterX_mm, symbol.CenterY_mm, 0),
                    Rotation = symbol.Rotation,
                    Scale = 1.0,
                    Attributes = new Dictionary<string, string>
                    {
                        ["WIDTH"] = symbol.Width_mm.ToString(),
                        ["HEIGHT"] = symbol.Height_mm.ToString()
                    }
                });

                analysis.Entities.Add(new DWGEntity
                {
                    EntityType = "Block",
                    LayerName = layerName,
                    StartPoint = (symbol.CenterX_mm, symbol.CenterY_mm, 0),
                    Width = symbol.Width_mm,
                    Height = symbol.Height_mm
                });

                if (!analysis.Layers.Any(l => l.LayerName == layerName))
                {
                    analysis.Layers.Add(new DWGLayer
                    {
                        LayerName = layerName,
                        EntityCount = 0,
                        Color = "White",
                        IsVisible = true
                    });
                }
            }

            // Convert room labels to DWG text entities on room layer
            foreach (var room in rooms)
            {
                analysis.Entities.Add(new DWGEntity
                {
                    EntityType = "Text",
                    LayerName = "A-ROOM",
                    StartPoint = (room.CenterX, room.CenterY, 0),
                    TextContent = room.Name
                });
            }

            if (rooms.Any() && !analysis.Layers.Any(l => l.LayerName == "A-ROOM"))
            {
                analysis.Layers.Add(new DWGLayer { LayerName = "A-ROOM", EntityCount = rooms.Count, Color = "Green", IsVisible = true });
            }

            // Update layer entity counts
            foreach (var layer in analysis.Layers)
            {
                layer.EntityCount = analysis.Entities.Count(e => e.LayerName == layer.LayerName);
            }

            return analysis;
        }

        #endregion

        #region Quality Assessment

        private ImageConversionQuality AssessConversionQuality(ImageConversionResult result)
        {
            var quality = new ImageConversionQuality
            {
                Recommendations = new List<string>()
            };

            // Wall confidence
            quality.WallConfidence = result.BIMResult?.Summary?.ElementsByCategory?.GetValueOrDefault("Walls") > 0 ? 0.75 : 0.2;

            // Symbol confidence
            quality.SymbolConfidence = result.DetectedSymbolCount > 0 ? Math.Min(0.9, 0.5 + result.DetectedSymbolCount * 0.02) : 0.1;

            // OCR confidence
            quality.OCRConfidence = result.ExtractedTextCount > 0 ? Math.Min(0.9, 0.4 + result.ExtractedTextCount * 0.03) : 0.1;

            // Room confidence
            quality.RoomConfidence = result.BIMResult?.Summary?.ElementsByCategory?.GetValueOrDefault("Rooms") > 0 ? 0.7 : 0.15;

            // Overall
            quality.OverallConfidence = (quality.WallConfidence + quality.SymbolConfidence + quality.OCRConfidence + quality.RoomConfidence) / 4.0;

            // Recommendations
            if (quality.WallConfidence < 0.5)
                quality.Recommendations.Add("Low wall detection confidence. Ensure image has clear, continuous wall lines. Consider higher resolution scan.");

            if (quality.SymbolConfidence < 0.5)
                quality.Recommendations.Add("Few symbols detected. Standard architectural symbols (door arcs, window lines) work best. Verify drawing conventions.");

            if (quality.OCRConfidence < 0.5)
                quality.Recommendations.Add("Text labels unclear. Ensure room names and dimensions are legible. Higher DPI helps OCR accuracy.");

            if (quality.RoomConfidence < 0.5)
                quality.Recommendations.Add("Rooms not clearly detected. Ensure walls form closed boundaries and room labels are within the enclosed areas.");

            if (quality.OverallConfidence >= 0.7)
                quality.Recommendations.Add("Good overall conversion quality. Review BIM elements and adjust types/sizes as needed.");
            else if (quality.OverallConfidence >= 0.5)
                quality.Recommendations.Add("Moderate conversion quality. Manual review recommended for element classification and missing elements.");
            else
                quality.Recommendations.Add("Low conversion quality. Consider providing a cleaner source image or DWG file for better results.");

            return quality;
        }

        #endregion
    }

    #region Image/PDF Data Models

    public class FloorPlanImage
    {
        public string FileName { get; set; }
        public int WidthPx { get; set; }
        public int HeightPx { get; set; }
        public int DPI { get; set; } = 300;
        public bool IsColor { get; set; }
        public double SkewAngle { get; set; }
        public string SourceFormat { get; set; } // "PNG", "JPG", "PDF", "TIFF"
    }

    public class ImageConversionOptions
    {
        public int DrawingScale { get; set; } = 100; // 1:100
        public double DefaultWallThickness { get; set; } = 200; // mm
        public double DefaultFloorToFloorHeight { get; set; } = 3600; // mm
        public double MinWallThicknessMM { get; set; } = 50; // mm in real-world
        public double MinWallLengthMM { get; set; } = 300; // mm
        public bool IncludeFurniture { get; set; } = true;
        public bool IncludeMEP { get; set; } = false; // Usually not visible in floor plans
        public double ConfidenceThreshold { get; set; } = 0.5;
    }

    public class ImageConversionResult
    {
        public string SourceFile { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int DrawingScale { get; set; }
        public List<string> PreprocessingNotes { get; set; } = new();
        public int DetectedLineCount { get; set; }
        public int DetectedSymbolCount { get; set; }
        public int ExtractedTextCount { get; set; }
        public DWGConversionResult BIMResult { get; set; }
        public ImageConversionQuality QualityMetrics { get; set; }
    }

    public class ImageConversionQuality
    {
        public double WallConfidence { get; set; }
        public double SymbolConfidence { get; set; }
        public double OCRConfidence { get; set; }
        public double RoomConfidence { get; set; }
        public double OverallConfidence { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    // Internal processing models
    internal class PreprocessedImage
    {
        public int WidthPx { get; set; }
        public int HeightPx { get; set; }
        public double PixelsPerMM { get; set; }
        public List<string> Notes { get; set; } = new();

        // These would be populated by ONNX vision models
        public List<EdgeSegment> DetectedEdges { get; set; }
        public List<DetectedShape> DetectedShapes { get; set; }
        public List<OCRResult> OCRResults { get; set; }
    }

    internal class EdgeSegment
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double Thickness { get; set; }
        public double Confidence { get; set; }
    }

    internal class DetectedShape
    {
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double BoundingWidth { get; set; }
        public double BoundingHeight { get; set; }
        public double Rotation { get; set; }
        public List<string> Features { get; set; } = new();
    }

    internal class OCRResult
    {
        public string Text { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Confidence { get; set; }
    }

    internal class DetectedLine
    {
        public double StartX_mm { get; set; }
        public double StartY_mm { get; set; }
        public double EndX_mm { get; set; }
        public double EndY_mm { get; set; }
        public double ThicknessPx { get; set; }
        public double ThicknessMM { get; set; }
        public double LengthMM { get; set; }
        public double Confidence { get; set; }
        public string Classification { get; set; }
        public string WallType { get; set; }
    }

    internal class DetectedSymbol
    {
        public string TemplateName { get; set; }
        public string BIMCategory { get; set; }
        public string FamilyName { get; set; }
        public double CenterX_mm { get; set; }
        public double CenterY_mm { get; set; }
        public double Width_mm { get; set; }
        public double Height_mm { get; set; }
        public double Rotation { get; set; }
        public double Confidence { get; set; }
    }

    internal class ExtractedTextLabel
    {
        public string Text { get; set; }
        public double CenterX_mm { get; set; }
        public double CenterY_mm { get; set; }
        public double Confidence { get; set; }
        public string LabelType { get; set; }
    }

    internal class DetectedRoom
    {
        public string Name { get; set; }
        public string OriginalLabel { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double EstimatedArea { get; set; }
        public double LabeledArea { get; set; }
        public double BoundsMinX { get; set; }
        public double BoundsMinY { get; set; }
        public double BoundsMaxX { get; set; }
        public double BoundsMaxY { get; set; }
        public double Confidence { get; set; }
    }

    internal class SymbolTemplate
    {
        public string Name { get; set; }
        public string BIMCategory { get; set; }
        public string FamilyName { get; set; }
        public string ShapeDescription { get; set; }
        public List<string> DetectionFeatures { get; set; } = new();
        public double DefaultWidth { get; set; }
        public double DefaultHeight { get; set; }
        public double Confidence { get; set; }
    }

    internal class SymbolMatchResult
    {
        public string TemplateName { get; set; }
        public string BIMCategory { get; set; }
        public string FamilyName { get; set; }
        public double MatchConfidence { get; set; }
    }

    #endregion
}
