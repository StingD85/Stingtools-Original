// ===================================================================================
// StingBIM PDF Plan Extractor
// Vector graphics extraction, annotation parsing, and conversion to BIM
// ===================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Import
{
    /// <summary>
    /// Extracts vector graphics, annotations, and drawing data from PDF construction plans.
    /// Supports multi-page PDFs with plans, sections, elevations, and details.
    /// Converts extracted data to BIM-ready elements.
    /// </summary>
    public class PDFPlanExtractor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly PDFParser _pdfParser;
        private readonly VectorExtractor _vectorExtractor;
        private readonly PDFTextExtractor _textExtractor;
        private readonly AnnotationParser _annotationParser;
        private readonly DrawingAnalyzer _drawingAnalyzer;
        private readonly PDFElementConverter _elementConverter;
        private readonly PDFExtractionSettings _settings;

        public PDFPlanExtractor(PDFExtractionSettings settings = null)
        {
            _settings = settings ?? new PDFExtractionSettings();
            _pdfParser = new PDFParser(_settings);
            _vectorExtractor = new VectorExtractor(_settings);
            _textExtractor = new PDFTextExtractor(_settings);
            _annotationParser = new AnnotationParser(_settings);
            _drawingAnalyzer = new DrawingAnalyzer(_settings);
            _elementConverter = new PDFElementConverter(_settings);

            Logger.Info("PDFPlanExtractor initialized");
        }

        #region Main Extraction Methods

        /// <summary>
        /// Extract all data from a PDF plan document
        /// </summary>
        public async Task<PDFExtractionResult> ExtractAsync(
            string pdfPath,
            ExtractionOptions options = null,
            IProgress<ExtractionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new PDFExtractionResult
            {
                SourceFile = pdfPath,
                ExtractionStartTime = DateTime.Now
            };

            try
            {
                Logger.Info("Starting PDF extraction: {0}", pdfPath);
                options ??= new ExtractionOptions();

                // Validate file
                progress?.Report(new ExtractionProgress(0, "Validating PDF..."));
                if (!ValidatePDF(pdfPath, out string error))
                {
                    result.Success = false;
                    result.Errors.Add(error);
                    return result;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Parse PDF structure
                progress?.Report(new ExtractionProgress(5, "Parsing PDF structure..."));
                var pdfDocument = await _pdfParser.ParseAsync(pdfPath, cancellationToken);
                result.DocumentInfo = pdfDocument.Info;
                result.PageCount = pdfDocument.Pages.Count;

                cancellationToken.ThrowIfCancellationRequested();

                // Process each page
                var pageResults = new List<PageExtractionResult>();
                int pageProgress = 0;
                int progressPerPage = 70 / Math.Max(1, pdfDocument.Pages.Count);

                foreach (var page in pdfDocument.Pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new ExtractionProgress(
                        10 + pageProgress,
                        $"Processing page {page.Number}..."));

                    var pageResult = await ProcessPageAsync(page, options, cancellationToken);
                    pageResults.Add(pageResult);

                    pageProgress += progressPerPage;
                }

                result.Pages = pageResults;

                // Analyze drawings across pages
                progress?.Report(new ExtractionProgress(85, "Analyzing drawings..."));
                var drawingAnalysis = await _drawingAnalyzer.AnalyzeAsync(
                    pageResults,
                    cancellationToken);
                result.DrawingAnalysis = drawingAnalysis;

                // Convert to BIM elements
                progress?.Report(new ExtractionProgress(95, "Converting to BIM elements..."));
                var bimElements = await _elementConverter.ConvertAsync(
                    pageResults,
                    drawingAnalysis,
                    options,
                    cancellationToken);
                result.ConvertedElements = bimElements;

                // Calculate statistics
                result.Statistics = CalculateStatistics(result);

                progress?.Report(new ExtractionProgress(100, "Extraction complete"));
                result.Success = true;
                result.ExtractionEndTime = DateTime.Now;

                Logger.Info("PDF extraction completed: {0} pages, {1} vectors, {2} elements",
                    result.PageCount,
                    result.Pages.Sum(p => p.VectorGraphics.Count),
                    result.ConvertedElements.Count);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Errors.Add("Extraction cancelled by user");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Extraction failed: {ex.Message}");
                Logger.Error(ex, "PDF extraction failed");
            }

            return result;
        }

        /// <summary>
        /// Extract specific pages from a PDF
        /// </summary>
        public async Task<PDFExtractionResult> ExtractPagesAsync(
            string pdfPath,
            IEnumerable<int> pageNumbers,
            ExtractionOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var result = new PDFExtractionResult
            {
                SourceFile = pdfPath,
                ExtractionStartTime = DateTime.Now
            };

            var pdfDocument = await _pdfParser.ParseAsync(pdfPath, cancellationToken);
            var pagesToProcess = pdfDocument.Pages
                .Where(p => pageNumbers.Contains(p.Number))
                .ToList();

            foreach (var page in pagesToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageResult = await ProcessPageAsync(page, options ?? new ExtractionOptions(), cancellationToken);
                result.Pages.Add(pageResult);
            }

            result.PageCount = result.Pages.Count;
            result.Success = true;
            result.ExtractionEndTime = DateTime.Now;

            return result;
        }

        /// <summary>
        /// Preview PDF structure without full extraction
        /// </summary>
        public async Task<PDFPreview> PreviewAsync(
            string pdfPath,
            CancellationToken cancellationToken = default)
        {
            var preview = new PDFPreview { SourceFile = pdfPath };

            if (!ValidatePDF(pdfPath, out string error))
            {
                preview.Error = error;
                return preview;
            }

            var pdfDocument = await _pdfParser.ParseAsync(pdfPath, cancellationToken);
            preview.DocumentInfo = pdfDocument.Info;
            preview.PageCount = pdfDocument.Pages.Count;

            preview.PagePreviews = pdfDocument.Pages.Select(p => new PagePreview
            {
                PageNumber = p.Number,
                Width = p.Width,
                Height = p.Height,
                HasVectorContent = p.ContentStreams.Any(),
                HasTextContent = true,
                EstimatedComplexity = EstimatePageComplexity(p)
            }).ToList();

            return preview;
        }

        #endregion

        #region Page Processing

        private async Task<PageExtractionResult> ProcessPageAsync(
            PDFPage page,
            ExtractionOptions options,
            CancellationToken cancellationToken)
        {
            var result = new PageExtractionResult
            {
                PageNumber = page.Number,
                Width = page.Width,
                Height = page.Height
            };

            // Extract vector graphics
            if (options.ExtractVectors)
            {
                result.VectorGraphics = await _vectorExtractor.ExtractAsync(page, cancellationToken);
            }

            // Extract text content
            if (options.ExtractText)
            {
                result.TextContent = await _textExtractor.ExtractAsync(page, cancellationToken);
            }

            // Parse annotations (dimensions, labels, etc.)
            if (options.ParseAnnotations)
            {
                result.Annotations = await _annotationParser.ParseAsync(
                    result.VectorGraphics,
                    result.TextContent,
                    cancellationToken);
            }

            // Detect drawing views
            result.DetectedViews = DetectDrawingViews(result);

            // Detect scale
            result.DetectedScale = DetectScale(result);

            return result;
        }

        private List<DetectedDrawingView> DetectDrawingViews(PageExtractionResult pageResult)
        {
            var views = new List<DetectedDrawingView>();

            // Find viewport/view boundaries
            var boundaries = FindViewBoundaries(pageResult.VectorGraphics);

            if (boundaries.Count == 0)
            {
                // Single view - entire page
                views.Add(new DetectedDrawingView
                {
                    Id = "VIEW_1",
                    Type = ClassifyViewType(pageResult.VectorGraphics, pageResult.TextContent),
                    Bounds = new PDFRectangle(0, 0, pageResult.Width, pageResult.Height),
                    Title = ExtractViewTitle(pageResult.TextContent, null),
                    Scale = pageResult.DetectedScale
                });
            }
            else
            {
                int viewId = 1;
                foreach (var boundary in boundaries)
                {
                    var viewVectors = FilterVectorsInBounds(pageResult.VectorGraphics, boundary);
                    var viewText = FilterTextInBounds(pageResult.TextContent, boundary);

                    views.Add(new DetectedDrawingView
                    {
                        Id = $"VIEW_{viewId++}",
                        Type = ClassifyViewType(viewVectors, viewText),
                        Bounds = boundary,
                        Title = ExtractViewTitle(viewText, boundary),
                        Scale = DetectScaleInRegion(viewVectors, viewText)
                    });
                }
            }

            return views;
        }

        private List<PDFRectangle> FindViewBoundaries(List<VectorGraphic> vectors)
        {
            var boundaries = new List<PDFRectangle>();

            // Find heavy/thick rectangles that might be view boundaries
            var potentialBoundaries = vectors
                .Where(v => v.Type == VectorType.Rectangle &&
                           v.LineWidth > _settings.ViewBoundaryMinLineWidth)
                .Select(v => v.Bounds)
                .ToList();

            // Filter out small rectangles and nested rectangles
            foreach (var rect in potentialBoundaries.OrderByDescending(r => r.Area))
            {
                if (rect.Area < _settings.MinViewArea)
                    continue;

                // Check if this is not contained within existing boundaries
                if (!boundaries.Any(b => ContainsRectangle(b, rect)))
                {
                    boundaries.Add(rect);
                }
            }

            return boundaries;
        }

        private bool ContainsRectangle(PDFRectangle outer, PDFRectangle inner)
        {
            return inner.X >= outer.X &&
                   inner.Y >= outer.Y &&
                   inner.X + inner.Width <= outer.X + outer.Width &&
                   inner.Y + inner.Height <= outer.Y + outer.Height;
        }

        private DrawingViewType ClassifyViewType(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            // Look for view type indicators in text
            var allText = string.Join(" ", text.Select(t => t.Content)).ToLower();

            if (allText.Contains("plan") || allText.Contains("floor"))
                return DrawingViewType.FloorPlan;
            if (allText.Contains("section") || allText.Contains("sect"))
                return DrawingViewType.Section;
            if (allText.Contains("elevation") || allText.Contains("elev"))
                return DrawingViewType.Elevation;
            if (allText.Contains("detail") || allText.Contains("det"))
                return DrawingViewType.Detail;
            if (allText.Contains("roof"))
                return DrawingViewType.RoofPlan;
            if (allText.Contains("reflected") || allText.Contains("rcp"))
                return DrawingViewType.ReflectedCeilingPlan;
            if (allText.Contains("site"))
                return DrawingViewType.SitePlan;

            // Analyze geometry patterns
            var horizontalLines = vectors.Count(v => v.Type == VectorType.Line && IsHorizontal(v));
            var verticalLines = vectors.Count(v => v.Type == VectorType.Line && IsVertical(v));
            var arcs = vectors.Count(v => v.Type == VectorType.Arc);

            if (arcs > vectors.Count * 0.1)
                return DrawingViewType.FloorPlan; // Door swings suggest plan

            if (horizontalLines > verticalLines * 2)
                return DrawingViewType.Section;

            return DrawingViewType.FloorPlan; // Default
        }

        private bool IsHorizontal(VectorGraphic vector)
        {
            if (vector.Type != VectorType.Line) return false;
            var line = vector as LineVector;
            return line != null && Math.Abs(line.EndY - line.StartY) < 1;
        }

        private bool IsVertical(VectorGraphic vector)
        {
            if (vector.Type != VectorType.Line) return false;
            var line = vector as LineVector;
            return line != null && Math.Abs(line.EndX - line.StartX) < 1;
        }

        private string ExtractViewTitle(List<TextBlock> text, PDFRectangle bounds)
        {
            // Find large text that might be a title
            var titleCandidates = text
                .Where(t => t.FontSize > _settings.TitleMinFontSize)
                .OrderByDescending(t => t.FontSize)
                .ThenBy(t => t.Position.Y)
                .ToList();

            if (titleCandidates.Any())
            {
                return titleCandidates.First().Content;
            }

            return null;
        }

        private ScaleInfo DetectScale(PageExtractionResult pageResult)
        {
            return DetectScaleInRegion(pageResult.VectorGraphics, pageResult.TextContent);
        }

        private ScaleInfo DetectScaleInRegion(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            var scaleInfo = new ScaleInfo
            {
                Confidence = 0.5,
                PixelsPerUnit = _settings.DefaultPixelsPerMM
            };

            // Look for scale notation in text
            var scalePattern = new Regex(@"(?i)(?:scale[:\s]*)?1\s*[:/]\s*(\d+)");
            foreach (var textBlock in text)
            {
                var match = scalePattern.Match(textBlock.Content);
                if (match.Success)
                {
                    var scaleRatio = int.Parse(match.Groups[1].Value);
                    scaleInfo.ScaleText = $"1:{scaleRatio}";
                    scaleInfo.ScaleRatio = 1.0 / scaleRatio;
                    scaleInfo.Confidence = 0.9;
                    break;
                }
            }

            // Look for imperial scale notation
            var imperialPattern = new Regex(@"(?i)(\d+(?:/\d+)?)\s*[""']\s*=\s*1'-?0?[""']?");
            foreach (var textBlock in text)
            {
                var match = imperialPattern.Match(textBlock.Content);
                if (match.Success)
                {
                    scaleInfo.ScaleText = match.Value;
                    scaleInfo.Unit = MeasurementUnit.Feet;
                    scaleInfo.Confidence = 0.9;
                    break;
                }
            }

            // Look for scale bar
            var scaleBar = FindScaleBar(vectors, text);
            if (scaleBar != null && scaleBar.Confidence > scaleInfo.Confidence)
            {
                scaleInfo = scaleBar;
            }

            // Calibrate from dimension strings
            var calibration = CalibrateFromDimensions(vectors, text);
            if (calibration != null && calibration.Confidence > scaleInfo.Confidence)
            {
                scaleInfo.PixelsPerUnit = calibration.PixelsPerUnit;
                scaleInfo.Confidence = calibration.Confidence;
            }

            return scaleInfo;
        }

        private ScaleInfo FindScaleBar(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            // Look for horizontal lines with regular tick marks and numeric labels
            var horizontalLines = vectors
                .OfType<LineVector>()
                .Where(l => IsHorizontal(l) && l.Length > _settings.MinScaleBarLength)
                .ToList();

            foreach (var line in horizontalLines)
            {
                // Look for tick marks (short vertical lines) along this line
                var ticks = vectors.OfType<LineVector>()
                    .Where(v => IsVertical(v) &&
                               Math.Abs(v.StartY - line.StartY) < 5 &&
                               v.StartX >= line.StartX &&
                               v.StartX <= line.EndX)
                    .OrderBy(v => v.StartX)
                    .ToList();

                if (ticks.Count >= 2)
                {
                    // Look for labels near ticks
                    var tickSpacing = (ticks[1].StartX - ticks[0].StartX);
                    var nearbyText = text.Where(t =>
                        Math.Abs(t.Position.Y - line.StartY) < 20 &&
                        t.Position.X >= line.StartX &&
                        t.Position.X <= line.EndX)
                        .ToList();

                    // Try to parse scale from labels
                    var numericLabels = nearbyText
                        .Where(t => double.TryParse(t.Content.Trim(), out _))
                        .Select(t => double.Parse(t.Content.Trim()))
                        .OrderBy(v => v)
                        .ToList();

                    if (numericLabels.Count >= 2)
                    {
                        var labelDiff = numericLabels[1] - numericLabels[0];
                        var pixelsPerUnit = tickSpacing / labelDiff;

                        return new ScaleInfo
                        {
                            PixelsPerUnit = pixelsPerUnit,
                            Confidence = 0.8,
                            ScaleText = "From scale bar"
                        };
                    }
                }
            }

            return null;
        }

        private ScaleInfo CalibrateFromDimensions(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            // Find dimension lines and their values
            var dimensions = FindDimensionLines(vectors, text);

            if (dimensions.Count < 2)
                return null;

            // Calculate pixels per unit for each dimension
            var calibrations = new List<double>();

            foreach (var dim in dimensions)
            {
                if (dim.Value > 0 && dim.PixelLength > 0)
                {
                    calibrations.Add(dim.PixelLength / dim.Value);
                }
            }

            if (calibrations.Count < 2)
                return null;

            // Use median to avoid outliers
            calibrations.Sort();
            var medianCalibration = calibrations[calibrations.Count / 2];

            // Check consistency
            var variance = calibrations.Select(c => Math.Abs(c - medianCalibration) / medianCalibration).Average();

            return new ScaleInfo
            {
                PixelsPerUnit = medianCalibration,
                Confidence = variance < 0.1 ? 0.85 : 0.6
            };
        }

        private List<DimensionMeasurement> FindDimensionLines(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            var dimensions = new List<DimensionMeasurement>();

            // Find parallel line pairs (dimension lines)
            var lines = vectors.OfType<LineVector>().ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (AreDimensionLinePair(lines[i], lines[j]))
                    {
                        var dimText = FindDimensionText(lines[i], lines[j], text);
                        if (dimText != null && TryParseValue(dimText.Content, out double value))
                        {
                            dimensions.Add(new DimensionMeasurement
                            {
                                Line1 = lines[i],
                                Line2 = lines[j],
                                Text = dimText,
                                Value = value,
                                PixelLength = CalculateDistance(lines[i], lines[j])
                            });
                        }
                    }
                }
            }

            return dimensions;
        }

        private bool AreDimensionLinePair(LineVector a, LineVector b)
        {
            // Check if lines are parallel and close together (extension lines)
            var aAngle = Math.Atan2(a.EndY - a.StartY, a.EndX - a.StartX);
            var bAngle = Math.Atan2(b.EndY - b.StartY, b.EndX - b.StartX);

            return Math.Abs(aAngle - bAngle) < 0.1 || Math.Abs(Math.Abs(aAngle - bAngle) - Math.PI) < 0.1;
        }

        private TextBlock FindDimensionText(LineVector line1, LineVector line2, List<TextBlock> text)
        {
            var midX = (line1.StartX + line1.EndX + line2.StartX + line2.EndX) / 4;
            var midY = (line1.StartY + line1.EndY + line2.StartY + line2.EndY) / 4;

            return text
                .Where(t => Math.Abs(t.Position.X - midX) < 50 && Math.Abs(t.Position.Y - midY) < 50)
                .OrderBy(t => Math.Pow(t.Position.X - midX, 2) + Math.Pow(t.Position.Y - midY, 2))
                .FirstOrDefault();
        }

        private bool TryParseValue(string text, out double value)
        {
            value = 0;
            text = text.Trim();

            // Try direct parse
            if (double.TryParse(text, out value))
                return true;

            // Try with units stripped
            var numericPattern = new Regex(@"(\d+(?:\.\d+)?)\s*(?:mm|m|cm|ft|in|'|"")?");
            var match = numericPattern.Match(text);
            if (match.Success)
            {
                return double.TryParse(match.Groups[1].Value, out value);
            }

            return false;
        }

        private double CalculateDistance(LineVector a, LineVector b)
        {
            // Calculate perpendicular distance between parallel lines
            var midA = new Point2D((a.StartX + a.EndX) / 2, (a.StartY + a.EndY) / 2);
            var midB = new Point2D((b.StartX + b.EndX) / 2, (b.StartY + b.EndY) / 2);
            return Math.Sqrt(Math.Pow(midA.X - midB.X, 2) + Math.Pow(midA.Y - midB.Y, 2));
        }

        private List<VectorGraphic> FilterVectorsInBounds(List<VectorGraphic> vectors, PDFRectangle bounds)
        {
            return vectors.Where(v => v.Bounds.Intersects(bounds)).ToList();
        }

        private List<TextBlock> FilterTextInBounds(List<TextBlock> text, PDFRectangle bounds)
        {
            return text.Where(t =>
                t.Position.X >= bounds.X && t.Position.X <= bounds.X + bounds.Width &&
                t.Position.Y >= bounds.Y && t.Position.Y <= bounds.Y + bounds.Height).ToList();
        }

        private double EstimatePageComplexity(PDFPage page)
        {
            // Simple complexity estimate based on content stream length
            var contentLength = page.ContentStreams.Sum(cs => cs.Length);
            if (contentLength > 100000) return 1.0;
            if (contentLength > 50000) return 0.7;
            if (contentLength > 10000) return 0.4;
            return 0.2;
        }

        #endregion

        #region Validation and Statistics

        private bool ValidatePDF(string pdfPath, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(pdfPath))
            {
                error = "PDF path is null or empty";
                return false;
            }

            if (!File.Exists(pdfPath))
            {
                error = $"PDF file not found: {pdfPath}";
                return false;
            }

            if (!pdfPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                error = "File is not a PDF";
                return false;
            }

            var fileInfo = new FileInfo(pdfPath);
            if (fileInfo.Length > _settings.MaxFileSizeBytes)
            {
                error = $"PDF file too large ({fileInfo.Length / 1024 / 1024}MB). Maximum: {_settings.MaxFileSizeBytes / 1024 / 1024}MB";
                return false;
            }

            // Verify PDF signature
            using var stream = File.OpenRead(pdfPath);
            var header = new byte[5];
            stream.Read(header, 0, 5);
            if (Encoding.ASCII.GetString(header) != "%PDF-")
            {
                error = "Invalid PDF file format";
                return false;
            }

            return true;
        }

        private ExtractionStatistics CalculateStatistics(PDFExtractionResult result)
        {
            return new ExtractionStatistics
            {
                TotalPages = result.PageCount,
                TotalVectors = result.Pages.Sum(p => p.VectorGraphics.Count),
                TotalLines = result.Pages.Sum(p => p.VectorGraphics.Count(v => v.Type == VectorType.Line)),
                TotalArcs = result.Pages.Sum(p => p.VectorGraphics.Count(v => v.Type == VectorType.Arc)),
                TotalRectangles = result.Pages.Sum(p => p.VectorGraphics.Count(v => v.Type == VectorType.Rectangle)),
                TotalTextBlocks = result.Pages.Sum(p => p.TextContent.Count),
                TotalAnnotations = result.Pages.Sum(p => p.Annotations.AllAnnotations.Count),
                TotalDetectedViews = result.Pages.Sum(p => p.DetectedViews.Count),
                ConvertedElements = result.ConvertedElements.Count,
                ConvertedWalls = result.ConvertedElements.Count(e => e.ElementType == ConvertedElementType.Wall),
                ConvertedDoors = result.ConvertedElements.Count(e => e.ElementType == ConvertedElementType.Door),
                ConvertedWindows = result.ConvertedElements.Count(e => e.ElementType == ConvertedElementType.Window),
                ConvertedRooms = result.ConvertedElements.Count(e => e.ElementType == ConvertedElementType.Room)
            };
        }

        #endregion
    }

    #region PDF Parser

    /// <summary>
    /// Parses PDF file structure and content streams
    /// </summary>
    internal class PDFParser
    {
        private readonly PDFExtractionSettings _settings;

        public PDFParser(PDFExtractionSettings settings)
        {
            _settings = settings;
        }

        public async Task<PDFDocument> ParseAsync(string path, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var document = new PDFDocument { FilePath = path };

                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);

                // Read PDF header
                document.Version = ReadPDFVersion(reader);

                // Find and parse cross-reference table
                var xrefOffset = FindXRefOffset(stream);
                stream.Position = xrefOffset;
                var xref = ParseXRef(stream);

                // Parse document info
                document.Info = ParseDocumentInfo(stream, xref);

                // Parse pages
                document.Pages = ParsePages(stream, xref);

                return document;
            }, cancellationToken);
        }

        private string ReadPDFVersion(BinaryReader reader)
        {
            var header = new byte[8];
            reader.Read(header, 0, 8);
            var headerStr = Encoding.ASCII.GetString(header);

            if (headerStr.StartsWith("%PDF-"))
            {
                return headerStr.Substring(5, 3);
            }

            return "1.0";
        }

        private long FindXRefOffset(Stream stream)
        {
            // Search backwards from end of file for "startxref"
            stream.Seek(-50, SeekOrigin.End);
            var buffer = new byte[50];
            stream.Read(buffer, 0, 50);
            var content = Encoding.ASCII.GetString(buffer);

            var startxrefIndex = content.IndexOf("startxref");
            if (startxrefIndex >= 0)
            {
                var offsetStr = content.Substring(startxrefIndex + 9).Trim().Split('\n')[0].Trim();
                if (long.TryParse(offsetStr, out long offset))
                {
                    return offset;
                }
            }

            return 0;
        }

        private Dictionary<int, long> ParseXRef(Stream stream)
        {
            var xref = new Dictionary<int, long>();

            using var reader = new StreamReader(stream, Encoding.ASCII, true, 1024, true);

            var line = reader.ReadLine();
            while (line != null && !line.StartsWith("trailer"))
            {
                // Parse xref entries
                if (Regex.IsMatch(line, @"^\d+\s+\d+$"))
                {
                    var parts = line.Split(' ');
                    var startObj = int.Parse(parts[0]);
                    var count = int.Parse(parts[1]);

                    for (int i = 0; i < count; i++)
                    {
                        line = reader.ReadLine();
                        if (line != null && line.Length >= 18)
                        {
                            var offsetStr = line.Substring(0, 10).Trim();
                            if (long.TryParse(offsetStr, out long offset) && offset > 0)
                            {
                                xref[startObj + i] = offset;
                            }
                        }
                    }
                }

                line = reader.ReadLine();
            }

            return xref;
        }

        private PDFDocumentInfo ParseDocumentInfo(Stream stream, Dictionary<int, long> xref)
        {
            var info = new PDFDocumentInfo();

            // Simplified: Would parse Info dictionary from trailer
            var fileInfo = new FileInfo(((FileStream)stream).Name);
            info.FileName = fileInfo.Name;
            info.FileSize = fileInfo.Length;
            info.CreationDate = fileInfo.CreationTime;
            info.ModificationDate = fileInfo.LastWriteTime;

            return info;
        }

        private List<PDFPage> ParsePages(Stream stream, Dictionary<int, long> xref)
        {
            var pages = new List<PDFPage>();

            // Simplified page parsing - would traverse page tree
            // For now, identify pages by content stream patterns
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.ASCII, true, 4096, true);
            var content = reader.ReadToEnd();

            var pagePattern = new Regex(@"/Type\s*/Page[^s].*?/MediaBox\s*\[\s*([\d.\s]+)\]", RegexOptions.Singleline);
            var matches = pagePattern.Matches(content);

            int pageNum = 1;
            foreach (Match match in matches)
            {
                var mediaBox = match.Groups[1].Value.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (mediaBox.Length >= 4)
                {
                    pages.Add(new PDFPage
                    {
                        Number = pageNum++,
                        X = double.Parse(mediaBox[0]),
                        Y = double.Parse(mediaBox[1]),
                        Width = double.Parse(mediaBox[2]),
                        Height = double.Parse(mediaBox[3]),
                        ContentStreams = ExtractContentStreams(content, match.Index)
                    });
                }
            }

            // If no pages found, create a placeholder
            if (pages.Count == 0)
            {
                pages.Add(new PDFPage
                {
                    Number = 1,
                    Width = 841, // A1 landscape
                    Height = 594,
                    ContentStreams = new List<byte[]>()
                });
            }

            return pages;
        }

        private List<byte[]> ExtractContentStreams(string content, int pageStart)
        {
            var streams = new List<byte[]>();

            // Find content stream reference
            var contentsPattern = new Regex(@"/Contents\s+(\d+)\s+\d+\s+R");
            var match = contentsPattern.Match(content, pageStart);

            if (match.Success)
            {
                // Would look up object and extract stream data
                // Simplified: return placeholder
                streams.Add(Encoding.ASCII.GetBytes("placeholder content stream"));
            }

            return streams;
        }
    }

    #endregion

    #region Vector Extractor

    /// <summary>
    /// Extracts vector graphics from PDF content streams
    /// </summary>
    internal class VectorExtractor
    {
        private readonly PDFExtractionSettings _settings;

        public VectorExtractor(PDFExtractionSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<VectorGraphic>> ExtractAsync(PDFPage page, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var vectors = new List<VectorGraphic>();

                foreach (var contentStream in page.ContentStreams)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Parse PDF content stream operators
                    var operators = ParseContentStream(contentStream);
                    vectors.AddRange(ProcessOperators(operators, page));
                }

                return vectors;
            }, cancellationToken);
        }

        private List<PDFOperator> ParseContentStream(byte[] stream)
        {
            var operators = new List<PDFOperator>();

            // Decompress if needed (FlateDecode, etc.)
            var content = DecompressStream(stream);
            var contentStr = Encoding.ASCII.GetString(content);

            // Parse PDF graphics operators
            var lines = contentStr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var currentOperands = new List<string>();

            foreach (var line in lines)
            {
                var tokens = Tokenize(line);

                foreach (var token in tokens)
                {
                    if (IsOperator(token))
                    {
                        operators.Add(new PDFOperator
                        {
                            Name = token,
                            Operands = currentOperands.ToArray()
                        });
                        currentOperands.Clear();
                    }
                    else
                    {
                        currentOperands.Add(token);
                    }
                }
            }

            return operators;
        }

        private byte[] DecompressStream(byte[] data)
        {
            // Check for zlib/deflate compression
            if (data.Length > 2 && data[0] == 0x78)
            {
                try
                {
                    using var input = new MemoryStream(data, 2, data.Length - 2);
                    using var deflate = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
                catch
                {
                    return data;
                }
            }

            return data;
        }

        private List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inString = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '(' && !inString)
                {
                    inString = true;
                    current.Append(c);
                }
                else if (c == ')' && inString)
                {
                    inString = false;
                    current.Append(c);
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                else if (inString)
                {
                    current.Append(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }

        private bool IsOperator(string token)
        {
            // Common PDF graphics operators
            var operators = new HashSet<string>
            {
                "m", "l", "c", "v", "y", "h", // Path construction
                "re", // Rectangle
                "S", "s", "f", "F", "f*", "B", "B*", "b", "b*", "n", // Path painting
                "W", "W*", // Clipping
                "q", "Q", // Graphics state
                "cm", // Transformation matrix
                "w", "J", "j", "M", "d", "ri", "i", "gs", // Graphics state parameters
                "BT", "ET", // Text objects
                "Tc", "Tw", "Tz", "TL", "Tf", "Tr", "Ts", // Text state
                "Td", "TD", "Tm", "T*", // Text positioning
                "Tj", "TJ", "'", "\"", // Text showing
                "d0", "d1", // Type 3 fonts
                "CS", "cs", "SC", "SCN", "sc", "scn", "G", "g", "RG", "rg", "K", "k", // Color
                "sh", // Shading
                "BI", "ID", "EI", // Inline images
                "Do", // XObject
                "MP", "DP", "BMC", "BDC", "EMC" // Marked content
            };

            return operators.Contains(token);
        }

        private List<VectorGraphic> ProcessOperators(List<PDFOperator> operators, PDFPage page)
        {
            var vectors = new List<VectorGraphic>();
            var currentPath = new PathBuilder();
            var graphicsState = new GraphicsState();
            var stateStack = new Stack<GraphicsState>();

            foreach (var op in operators)
            {
                switch (op.Name)
                {
                    case "q": // Save state
                        stateStack.Push(graphicsState.Clone());
                        break;

                    case "Q": // Restore state
                        if (stateStack.Count > 0)
                            graphicsState = stateStack.Pop();
                        break;

                    case "cm": // Transformation matrix
                        if (op.Operands.Length >= 6)
                        {
                            graphicsState.ApplyTransform(
                                ParseDouble(op.Operands[0]),
                                ParseDouble(op.Operands[1]),
                                ParseDouble(op.Operands[2]),
                                ParseDouble(op.Operands[3]),
                                ParseDouble(op.Operands[4]),
                                ParseDouble(op.Operands[5]));
                        }
                        break;

                    case "w": // Line width
                        if (op.Operands.Length >= 1)
                            graphicsState.LineWidth = ParseDouble(op.Operands[0]);
                        break;

                    case "m": // Move to
                        if (op.Operands.Length >= 2)
                        {
                            var pt = graphicsState.Transform(ParseDouble(op.Operands[0]), ParseDouble(op.Operands[1]));
                            currentPath.MoveTo(pt.X, pt.Y);
                        }
                        break;

                    case "l": // Line to
                        if (op.Operands.Length >= 2)
                        {
                            var pt = graphicsState.Transform(ParseDouble(op.Operands[0]), ParseDouble(op.Operands[1]));
                            currentPath.LineTo(pt.X, pt.Y);
                        }
                        break;

                    case "c": // Bezier curve
                        if (op.Operands.Length >= 6)
                        {
                            var p1 = graphicsState.Transform(ParseDouble(op.Operands[0]), ParseDouble(op.Operands[1]));
                            var p2 = graphicsState.Transform(ParseDouble(op.Operands[2]), ParseDouble(op.Operands[3]));
                            var p3 = graphicsState.Transform(ParseDouble(op.Operands[4]), ParseDouble(op.Operands[5]));
                            currentPath.CurveTo(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y);
                        }
                        break;

                    case "h": // Close path
                        currentPath.ClosePath();
                        break;

                    case "re": // Rectangle
                        if (op.Operands.Length >= 4)
                        {
                            var pt = graphicsState.Transform(ParseDouble(op.Operands[0]), ParseDouble(op.Operands[1]));
                            var w = ParseDouble(op.Operands[2]) * graphicsState.ScaleX;
                            var h = ParseDouble(op.Operands[3]) * graphicsState.ScaleY;
                            vectors.Add(new RectangleVector
                            {
                                X = pt.X,
                                Y = pt.Y,
                                Width = w,
                                Height = h,
                                LineWidth = graphicsState.LineWidth,
                                StrokeColor = graphicsState.StrokeColor,
                                FillColor = graphicsState.FillColor
                            });
                            currentPath.Clear();
                        }
                        break;

                    case "S": // Stroke
                    case "s": // Close and stroke
                        vectors.AddRange(currentPath.ToVectors(graphicsState, true, false));
                        currentPath.Clear();
                        break;

                    case "f": // Fill
                    case "F":
                    case "f*":
                        vectors.AddRange(currentPath.ToVectors(graphicsState, false, true));
                        currentPath.Clear();
                        break;

                    case "B": // Fill and stroke
                    case "B*":
                    case "b":
                    case "b*":
                        vectors.AddRange(currentPath.ToVectors(graphicsState, true, true));
                        currentPath.Clear();
                        break;

                    case "n": // End path without filling or stroking
                        currentPath.Clear();
                        break;

                    case "RG": // Stroke color RGB
                    case "rg": // Fill color RGB
                        if (op.Operands.Length >= 3)
                        {
                            var color = new PDFColor
                            {
                                R = ParseDouble(op.Operands[0]),
                                G = ParseDouble(op.Operands[1]),
                                B = ParseDouble(op.Operands[2])
                            };
                            if (op.Name == "RG")
                                graphicsState.StrokeColor = color;
                            else
                                graphicsState.FillColor = color;
                        }
                        break;

                    case "G": // Stroke gray
                    case "g": // Fill gray
                        if (op.Operands.Length >= 1)
                        {
                            var gray = ParseDouble(op.Operands[0]);
                            var color = new PDFColor { R = gray, G = gray, B = gray };
                            if (op.Name == "G")
                                graphicsState.StrokeColor = color;
                            else
                                graphicsState.FillColor = color;
                        }
                        break;
                }
            }

            return vectors;
        }

        private double ParseDouble(string s)
        {
            if (double.TryParse(s, out double result))
                return result;
            return 0;
        }
    }

    /// <summary>
    /// Builds vector paths from PDF operators
    /// </summary>
    internal class PathBuilder
    {
        private readonly List<PathSegment> _segments = new();
        private Point2D _currentPoint = new Point2D(0, 0);
        private Point2D _startPoint = new Point2D(0, 0);

        public void MoveTo(double x, double y)
        {
            _currentPoint = new Point2D(x, y);
            _startPoint = _currentPoint;
        }

        public void LineTo(double x, double y)
        {
            _segments.Add(new PathSegment
            {
                Type = PathSegmentType.Line,
                StartX = _currentPoint.X,
                StartY = _currentPoint.Y,
                EndX = x,
                EndY = y
            });
            _currentPoint = new Point2D(x, y);
        }

        public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            _segments.Add(new PathSegment
            {
                Type = PathSegmentType.Bezier,
                StartX = _currentPoint.X,
                StartY = _currentPoint.Y,
                ControlX1 = x1,
                ControlY1 = y1,
                ControlX2 = x2,
                ControlY2 = y2,
                EndX = x3,
                EndY = y3
            });
            _currentPoint = new Point2D(x3, y3);
        }

        public void ClosePath()
        {
            if (Math.Abs(_currentPoint.X - _startPoint.X) > 0.01 ||
                Math.Abs(_currentPoint.Y - _startPoint.Y) > 0.01)
            {
                LineTo(_startPoint.X, _startPoint.Y);
            }
        }

        public void Clear()
        {
            _segments.Clear();
        }

        public List<VectorGraphic> ToVectors(GraphicsState state, bool stroke, bool fill)
        {
            var vectors = new List<VectorGraphic>();

            foreach (var segment in _segments)
            {
                VectorGraphic vector = segment.Type switch
                {
                    PathSegmentType.Line => new LineVector
                    {
                        StartX = segment.StartX,
                        StartY = segment.StartY,
                        EndX = segment.EndX,
                        EndY = segment.EndY,
                        LineWidth = state.LineWidth,
                        StrokeColor = stroke ? state.StrokeColor : null
                    },
                    PathSegmentType.Bezier => new CurveVector
                    {
                        StartX = segment.StartX,
                        StartY = segment.StartY,
                        ControlX1 = segment.ControlX1,
                        ControlY1 = segment.ControlY1,
                        ControlX2 = segment.ControlX2,
                        ControlY2 = segment.ControlY2,
                        EndX = segment.EndX,
                        EndY = segment.EndY,
                        LineWidth = state.LineWidth,
                        StrokeColor = stroke ? state.StrokeColor : null
                    },
                    _ => null
                };

                if (vector != null)
                {
                    vectors.Add(vector);
                }
            }

            return vectors;
        }
    }

    internal class PathSegment
    {
        public PathSegmentType Type { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double ControlX1 { get; set; }
        public double ControlY1 { get; set; }
        public double ControlX2 { get; set; }
        public double ControlY2 { get; set; }
    }

    internal enum PathSegmentType
    {
        Line,
        Bezier,
        Arc
    }

    internal class GraphicsState
    {
        public double LineWidth { get; set; } = 1;
        public PDFColor StrokeColor { get; set; } = new PDFColor { R = 0, G = 0, B = 0 };
        public PDFColor FillColor { get; set; } = new PDFColor { R = 1, G = 1, B = 1 };

        // Transformation matrix [a b c d e f]
        private double _a = 1, _b = 0, _c = 0, _d = 1, _e = 0, _f = 0;

        public double ScaleX => Math.Sqrt(_a * _a + _b * _b);
        public double ScaleY => Math.Sqrt(_c * _c + _d * _d);

        public void ApplyTransform(double a, double b, double c, double d, double e, double f)
        {
            var na = _a * a + _c * b;
            var nb = _b * a + _d * b;
            var nc = _a * c + _c * d;
            var nd = _b * c + _d * d;
            var ne = _a * e + _c * f + _e;
            var nf = _b * e + _d * f + _f;

            _a = na; _b = nb; _c = nc; _d = nd; _e = ne; _f = nf;
        }

        public Point2D Transform(double x, double y)
        {
            return new Point2D(
                _a * x + _c * y + _e,
                _b * x + _d * y + _f);
        }

        public GraphicsState Clone()
        {
            return new GraphicsState
            {
                LineWidth = LineWidth,
                StrokeColor = StrokeColor,
                FillColor = FillColor,
                _a = _a, _b = _b, _c = _c, _d = _d, _e = _e, _f = _f
            };
        }
    }

    #endregion

    #region Text Extractor

    /// <summary>
    /// Extracts text content from PDF pages
    /// </summary>
    internal class PDFTextExtractor
    {
        private readonly PDFExtractionSettings _settings;

        public PDFTextExtractor(PDFExtractionSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<TextBlock>> ExtractAsync(PDFPage page, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var textBlocks = new List<TextBlock>();

                foreach (var contentStream in page.ContentStreams)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    textBlocks.AddRange(ExtractTextFromStream(contentStream));
                }

                return textBlocks;
            }, cancellationToken);
        }

        private List<TextBlock> ExtractTextFromStream(byte[] stream)
        {
            var blocks = new List<TextBlock>();

            // Decompress if needed
            byte[] content = stream;
            if (stream.Length > 2 && stream[0] == 0x78)
            {
                try
                {
                    using var input = new MemoryStream(stream, 2, stream.Length - 2);
                    using var deflate = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    content = output.ToArray();
                }
                catch { }
            }

            var contentStr = Encoding.ASCII.GetString(content);

            // Find text objects
            var textObjectPattern = new Regex(@"BT\s*(.*?)\s*ET", RegexOptions.Singleline);
            var matches = textObjectPattern.Matches(contentStr);

            foreach (Match match in matches)
            {
                var textContent = match.Groups[1].Value;
                var block = ParseTextObject(textContent);
                if (block != null && !string.IsNullOrWhiteSpace(block.Content))
                {
                    blocks.Add(block);
                }
            }

            return blocks;
        }

        private TextBlock ParseTextObject(string textContent)
        {
            var block = new TextBlock();
            double x = 0, y = 0;
            double fontSize = 12;
            var textBuilder = new StringBuilder();

            var lines = textContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Text positioning
                if (trimmed.EndsWith(" Td") || trimmed.EndsWith(" TD"))
                {
                    var parts = trimmed.Split(' ');
                    if (parts.Length >= 3)
                    {
                        double.TryParse(parts[0], out x);
                        double.TryParse(parts[1], out y);
                    }
                }
                else if (trimmed.EndsWith(" Tm"))
                {
                    var parts = trimmed.Split(' ');
                    if (parts.Length >= 7)
                    {
                        double.TryParse(parts[4], out x);
                        double.TryParse(parts[5], out y);
                    }
                }
                // Font
                else if (trimmed.EndsWith(" Tf"))
                {
                    var parts = trimmed.Split(' ');
                    if (parts.Length >= 3)
                    {
                        double.TryParse(parts[parts.Length - 2], out fontSize);
                    }
                }
                // Text content
                else if (trimmed.EndsWith(" Tj"))
                {
                    var text = ExtractTextString(trimmed);
                    textBuilder.Append(text);
                }
                else if (trimmed.EndsWith(" TJ"))
                {
                    var text = ExtractTextArray(trimmed);
                    textBuilder.Append(text);
                }
            }

            block.Position = new Point2D(x, y);
            block.FontSize = fontSize;
            block.Content = textBuilder.ToString();

            return block;
        }

        private string ExtractTextString(string line)
        {
            var match = Regex.Match(line, @"\(([^)]*)\)\s*Tj");
            if (match.Success)
            {
                return UnescapeString(match.Groups[1].Value);
            }

            // Hex string
            match = Regex.Match(line, @"<([0-9A-Fa-f]*)>\s*Tj");
            if (match.Success)
            {
                return DecodeHexString(match.Groups[1].Value);
            }

            return "";
        }

        private string ExtractTextArray(string line)
        {
            var builder = new StringBuilder();
            var arrayMatch = Regex.Match(line, @"\[(.*)\]\s*TJ");
            if (arrayMatch.Success)
            {
                var arrayContent = arrayMatch.Groups[1].Value;

                // Extract strings from array
                var stringMatches = Regex.Matches(arrayContent, @"\(([^)]*)\)");
                foreach (Match m in stringMatches)
                {
                    builder.Append(UnescapeString(m.Groups[1].Value));
                }

                // Also try hex strings
                var hexMatches = Regex.Matches(arrayContent, @"<([0-9A-Fa-f]*)>");
                foreach (Match m in hexMatches)
                {
                    builder.Append(DecodeHexString(m.Groups[1].Value));
                }
            }

            return builder.ToString();
        }

        private string UnescapeString(string s)
        {
            return s.Replace("\\(", "(")
                   .Replace("\\)", ")")
                   .Replace("\\\\", "\\")
                   .Replace("\\n", "\n")
                   .Replace("\\r", "\r")
                   .Replace("\\t", "\t");
        }

        private string DecodeHexString(string hex)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < hex.Length - 1; i += 2)
            {
                var byteStr = hex.Substring(i, 2);
                if (int.TryParse(byteStr, System.Globalization.NumberStyles.HexNumber, null, out int value))
                {
                    builder.Append((char)value);
                }
            }
            return builder.ToString();
        }
    }

    #endregion

    #region Annotation Parser

    /// <summary>
    /// Parses annotations (dimensions, labels, tags) from extracted data
    /// </summary>
    internal class AnnotationParser
    {
        private readonly PDFExtractionSettings _settings;

        public AnnotationParser(PDFExtractionSettings settings)
        {
            _settings = settings;
        }

        public async Task<ParsedAnnotations> ParseAsync(
            List<VectorGraphic> vectors,
            List<TextBlock> text,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var annotations = new ParsedAnnotations();

                // Parse dimensions
                annotations.Dimensions = ParseDimensions(vectors, text);

                // Parse room labels
                annotations.RoomLabels = ParseRoomLabels(text);

                // Parse grid labels
                annotations.GridLabels = ParseGridLabels(text, vectors);

                // Parse level markers
                annotations.LevelMarkers = ParseLevelMarkers(text);

                // Parse notes
                annotations.Notes = ParseNotes(text);

                // Parse symbols
                annotations.Symbols = ParseSymbols(vectors, text);

                return annotations;
            }, cancellationToken);
        }

        private List<DimensionAnnotation> ParseDimensions(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            var dimensions = new List<DimensionAnnotation>();

            // Find dimension text (numbers with optional units)
            var dimPattern = new Regex(@"^\s*(\d+(?:[.,]\d+)?)\s*(mm|m|cm|ft|in|'|"")?\s*$");

            foreach (var textBlock in text)
            {
                var match = dimPattern.Match(textBlock.Content);
                if (match.Success)
                {
                    // Find associated dimension lines
                    var nearbyLines = vectors.OfType<LineVector>()
                        .Where(l => IsNearText(l, textBlock))
                        .ToList();

                    if (nearbyLines.Count >= 2)
                    {
                        dimensions.Add(new DimensionAnnotation
                        {
                            Text = textBlock.Content,
                            Value = double.Parse(match.Groups[1].Value),
                            Unit = match.Groups[2].Value,
                            Position = textBlock.Position,
                            DimensionLines = nearbyLines
                        });
                    }
                }
            }

            return dimensions;
        }

        private bool IsNearText(LineVector line, TextBlock text)
        {
            var lineCenter = new Point2D((line.StartX + line.EndX) / 2, (line.StartY + line.EndY) / 2);
            var distance = Math.Sqrt(Math.Pow(lineCenter.X - text.Position.X, 2) +
                                    Math.Pow(lineCenter.Y - text.Position.Y, 2));
            return distance < _settings.DimensionProximity;
        }

        private List<RoomLabelAnnotation> ParseRoomLabels(List<TextBlock> text)
        {
            var labels = new List<RoomLabelAnnotation>();

            // Room label patterns
            var roomPatterns = new[]
            {
                new Regex(@"(?i)(bedroom|living|kitchen|bathroom|toilet|office|storage|utility|hall|corridor|lobby|entrance|dining)\s*\d*"),
                new Regex(@"(?i)(rm|room)\s*\d+"),
                new Regex(@"^\d{3}[A-Z]?$") // Room numbers like 101, 102A
            };

            foreach (var textBlock in text)
            {
                foreach (var pattern in roomPatterns)
                {
                    if (pattern.IsMatch(textBlock.Content))
                    {
                        labels.Add(new RoomLabelAnnotation
                        {
                            Text = textBlock.Content,
                            Position = textBlock.Position,
                            FontSize = textBlock.FontSize
                        });
                        break;
                    }
                }
            }

            return labels;
        }

        private List<GridLabelAnnotation> ParseGridLabels(List<TextBlock> text, List<VectorGraphic> vectors)
        {
            var labels = new List<GridLabelAnnotation>();

            // Grid labels are typically single letters (A-Z) or numbers (1-9) in circles
            var gridPattern = new Regex(@"^[A-Z]$|^[1-9]\d?$");

            // Find circles that might contain grid labels
            var circles = vectors.OfType<CircleVector>()
                .Where(c => c.Radius > 5 && c.Radius < 50)
                .ToList();

            foreach (var textBlock in text)
            {
                if (gridPattern.IsMatch(textBlock.Content.Trim()))
                {
                    // Check if text is inside a circle
                    var containingCircle = circles.FirstOrDefault(c =>
                        Math.Sqrt(Math.Pow(c.CenterX - textBlock.Position.X, 2) +
                                 Math.Pow(c.CenterY - textBlock.Position.Y, 2)) < c.Radius);

                    if (containingCircle != null)
                    {
                        labels.Add(new GridLabelAnnotation
                        {
                            Text = textBlock.Content.Trim(),
                            Position = textBlock.Position,
                            CircleRadius = containingCircle.Radius
                        });
                    }
                }
            }

            return labels;
        }

        private List<LevelMarkerAnnotation> ParseLevelMarkers(List<TextBlock> text)
        {
            var markers = new List<LevelMarkerAnnotation>();

            // Level markers typically contain elevation values
            var levelPattern = new Regex(@"(?i)(level|fl|floor)\s*(\d+)|([+-]?\d+[.,]\d+)\s*(m|mm|ft)?");

            foreach (var textBlock in text)
            {
                var match = levelPattern.Match(textBlock.Content);
                if (match.Success)
                {
                    markers.Add(new LevelMarkerAnnotation
                    {
                        Text = textBlock.Content,
                        Position = textBlock.Position,
                        Elevation = TryParseElevation(match)
                    });
                }
            }

            return markers;
        }

        private double? TryParseElevation(Match match)
        {
            if (match.Groups[3].Success)
            {
                if (double.TryParse(match.Groups[3].Value.Replace(",", "."), out double elevation))
                {
                    return elevation;
                }
            }
            return null;
        }

        private List<NoteAnnotation> ParseNotes(List<TextBlock> text)
        {
            var notes = new List<NoteAnnotation>();

            // Notes are typically longer text blocks
            foreach (var textBlock in text.Where(t => t.Content.Length > 20))
            {
                // Skip dimension text
                if (Regex.IsMatch(textBlock.Content, @"^\d+(?:[.,]\d+)?\s*(mm|m|cm|ft|in)?$"))
                    continue;

                notes.Add(new NoteAnnotation
                {
                    Text = textBlock.Content,
                    Position = textBlock.Position,
                    FontSize = textBlock.FontSize
                });
            }

            return notes;
        }

        private List<SymbolAnnotation> ParseSymbols(List<VectorGraphic> vectors, List<TextBlock> text)
        {
            var symbols = new List<SymbolAnnotation>();

            // Find common symbols (north arrow, scale bar, section marks, etc.)

            // North arrow - typically a triangle or arrow pointing up
            var triangles = vectors.Where(v => IsTriangle(v)).ToList();
            var nearbyNorthText = text.FirstOrDefault(t =>
                t.Content.Contains("N") || t.Content.ToLower().Contains("north"));

            if (triangles.Any() && nearbyNorthText != null)
            {
                symbols.Add(new SymbolAnnotation
                {
                    Type = SymbolType.NorthArrow,
                    Position = nearbyNorthText.Position
                });
            }

            return symbols;
        }

        private bool IsTriangle(VectorGraphic vector)
        {
            // Simplified triangle detection
            return vector.Type == VectorType.Polygon && (vector as PolygonVector)?.VertexCount == 3;
        }
    }

    #endregion

    #region Drawing Analyzer

    /// <summary>
    /// Analyzes extracted data to understand drawing structure
    /// </summary>
    internal class DrawingAnalyzer
    {
        private readonly PDFExtractionSettings _settings;

        public DrawingAnalyzer(PDFExtractionSettings settings)
        {
            _settings = settings;
        }

        public async Task<DrawingAnalysis> AnalyzeAsync(
            List<PageExtractionResult> pages,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var analysis = new DrawingAnalysis();

                // Identify drawing set structure
                analysis.DrawingSet = IdentifyDrawingSet(pages);

                // Correlate views across pages
                analysis.CorrelatedViews = CorrelateViews(pages);

                // Identify building structure
                analysis.BuildingStructure = AnalyzeBuildingStructure(pages);

                return analysis;
            }, cancellationToken);
        }

        private DrawingSet IdentifyDrawingSet(List<PageExtractionResult> pages)
        {
            var set = new DrawingSet
            {
                TotalPages = pages.Count,
                DrawingTypes = new Dictionary<DrawingViewType, int>()
            };

            foreach (var page in pages)
            {
                foreach (var view in page.DetectedViews)
                {
                    if (!set.DrawingTypes.ContainsKey(view.Type))
                        set.DrawingTypes[view.Type] = 0;
                    set.DrawingTypes[view.Type]++;
                }
            }

            // Determine primary drawing scale
            var scales = pages
                .Where(p => p.DetectedScale != null)
                .Select(p => p.DetectedScale)
                .OrderByDescending(s => s.Confidence)
                .ToList();

            if (scales.Any())
            {
                set.PrimaryScale = scales.First();
            }

            return set;
        }

        private List<CorrelatedViewSet> CorrelateViews(List<PageExtractionResult> pages)
        {
            var correlatedSets = new List<CorrelatedViewSet>();

            // Find views that represent the same level
            var planViews = pages
                .SelectMany(p => p.DetectedViews)
                .Where(v => v.Type == DrawingViewType.FloorPlan)
                .ToList();

            foreach (var planView in planViews)
            {
                var set = new CorrelatedViewSet
                {
                    PrimaryView = planView,
                    RelatedViews = new List<DetectedDrawingView>()
                };

                // Find related sections and elevations
                var relatedViews = pages
                    .SelectMany(p => p.DetectedViews)
                    .Where(v => v.Type == DrawingViewType.Section ||
                               v.Type == DrawingViewType.Elevation)
                    .ToList();

                set.RelatedViews.AddRange(relatedViews);
                correlatedSets.Add(set);
            }

            return correlatedSets;
        }

        private BuildingStructureAnalysis AnalyzeBuildingStructure(List<PageExtractionResult> pages)
        {
            var analysis = new BuildingStructureAnalysis();

            // Count levels from floor plans
            var floorPlans = pages
                .SelectMany(p => p.DetectedViews)
                .Where(v => v.Type == DrawingViewType.FloorPlan)
                .ToList();

            analysis.EstimatedLevels = Math.Max(1, floorPlans.Count);

            // Analyze grid system from grid labels
            var gridLabels = pages
                .SelectMany(p => p.Annotations.GridLabels)
                .ToList();

            if (gridLabels.Any())
            {
                analysis.HasGridSystem = true;
                analysis.GridLabelsX = gridLabels.Where(g => Regex.IsMatch(g.Text, @"^[A-Z]$")).Select(g => g.Text).Distinct().ToList();
                analysis.GridLabelsY = gridLabels.Where(g => Regex.IsMatch(g.Text, @"^\d+$")).Select(g => g.Text).Distinct().ToList();
            }

            return analysis;
        }
    }

    #endregion

    #region Element Converter

    /// <summary>
    /// Converts extracted PDF data to BIM elements
    /// </summary>
    internal class PDFElementConverter
    {
        private readonly PDFExtractionSettings _settings;

        public PDFElementConverter(PDFExtractionSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<ConvertedPDFElement>> ConvertAsync(
            List<PageExtractionResult> pages,
            DrawingAnalysis analysis,
            ExtractionOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var elements = new List<ConvertedPDFElement>();

                foreach (var page in pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var view in page.DetectedViews)
                    {
                        var viewElements = ConvertViewElements(view, page, options);
                        elements.AddRange(viewElements);
                    }
                }

                return elements;
            }, cancellationToken);
        }

        private List<ConvertedPDFElement> ConvertViewElements(
            DetectedDrawingView view,
            PageExtractionResult page,
            ExtractionOptions options)
        {
            var elements = new List<ConvertedPDFElement>();

            // Filter vectors and annotations for this view
            var viewVectors = FilterVectorsInBounds(page.VectorGraphics, view.Bounds);
            var viewAnnotations = FilterAnnotationsInBounds(page.Annotations, view.Bounds);

            // Convert based on view type
            switch (view.Type)
            {
                case DrawingViewType.FloorPlan:
                    elements.AddRange(ConvertPlanElements(viewVectors, viewAnnotations, view));
                    break;
                case DrawingViewType.Section:
                case DrawingViewType.Elevation:
                    elements.AddRange(ConvertSectionElements(viewVectors, viewAnnotations, view));
                    break;
            }

            return elements;
        }

        private List<ConvertedPDFElement> ConvertPlanElements(
            List<VectorGraphic> vectors,
            ParsedAnnotations annotations,
            DetectedDrawingView view)
        {
            var elements = new List<ConvertedPDFElement>();

            // Convert parallel line pairs to walls
            var walls = ConvertToWalls(vectors, view.Scale);
            elements.AddRange(walls);

            // Convert arcs to doors
            var doors = ConvertToDoors(vectors, annotations, view.Scale);
            elements.AddRange(doors);

            // Convert room labels to rooms
            var rooms = ConvertToRooms(annotations, walls);
            elements.AddRange(rooms);

            return elements;
        }

        private List<ConvertedPDFElement> ConvertToWalls(List<VectorGraphic> vectors, ScaleInfo scale)
        {
            var walls = new List<ConvertedPDFElement>();
            var lines = vectors.OfType<LineVector>().ToList();
            var usedLines = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (usedLines.Contains(i)) continue;

                // Find parallel line that could form wall edges
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (usedLines.Contains(j)) continue;

                    if (AreParallelLines(lines[i], lines[j], out double distance))
                    {
                        var thickness = distance / scale.PixelsPerUnit;

                        if (thickness > _settings.MinWallThickness && thickness < _settings.MaxWallThickness)
                        {
                            var wall = CreateWallElement(lines[i], lines[j], thickness, scale);
                            walls.Add(wall);
                            usedLines.Add(i);
                            usedLines.Add(j);
                            break;
                        }
                    }
                }
            }

            return walls;
        }

        private bool AreParallelLines(LineVector a, LineVector b, out double distance)
        {
            distance = 0;

            var angleA = Math.Atan2(a.EndY - a.StartY, a.EndX - a.StartX);
            var angleB = Math.Atan2(b.EndY - b.StartY, b.EndX - b.StartX);

            var angleDiff = Math.Abs(angleA - angleB);
            if (angleDiff > 0.1 && Math.Abs(angleDiff - Math.PI) > 0.1)
                return false;

            // Calculate perpendicular distance
            var midA = new Point2D((a.StartX + a.EndX) / 2, (a.StartY + a.EndY) / 2);
            var dx = b.EndX - b.StartX;
            var dy = b.EndY - b.StartY;
            var len = Math.Sqrt(dx * dx + dy * dy);

            distance = Math.Abs((b.StartY - midA.Y) * dx - (b.StartX - midA.X) * dy) / len;

            return true;
        }

        private ConvertedPDFElement CreateWallElement(LineVector line1, LineVector line2, double thickness, ScaleInfo scale)
        {
            var centerStartX = (line1.StartX + line2.StartX) / 2;
            var centerStartY = (line1.StartY + line2.StartY) / 2;
            var centerEndX = (line1.EndX + line2.EndX) / 2;
            var centerEndY = (line1.EndY + line2.EndY) / 2;

            return new ConvertedPDFElement
            {
                ElementType = ConvertedElementType.Wall,
                StartPoint = new Point2D(centerStartX / scale.PixelsPerUnit, centerStartY / scale.PixelsPerUnit),
                EndPoint = new Point2D(centerEndX / scale.PixelsPerUnit, centerEndY / scale.PixelsPerUnit),
                Properties = new Dictionary<string, object>
                {
                    ["Thickness"] = thickness,
                    ["Length"] = Math.Sqrt(Math.Pow(centerEndX - centerStartX, 2) +
                                          Math.Pow(centerEndY - centerStartY, 2)) / scale.PixelsPerUnit
                }
            };
        }

        private List<ConvertedPDFElement> ConvertToDoors(
            List<VectorGraphic> vectors,
            ParsedAnnotations annotations,
            ScaleInfo scale)
        {
            var doors = new List<ConvertedPDFElement>();

            // Find arcs that could be door swings
            var arcs = vectors.OfType<CurveVector>().ToList();

            foreach (var arc in arcs)
            {
                // Check if arc looks like a door swing (quarter circle)
                var chord = Math.Sqrt(Math.Pow(arc.EndX - arc.StartX, 2) + Math.Pow(arc.EndY - arc.StartY, 2));
                var width = chord / scale.PixelsPerUnit;

                if (width > 600 && width < 2000) // Typical door widths
                {
                    doors.Add(new ConvertedPDFElement
                    {
                        ElementType = ConvertedElementType.Door,
                        Center = new Point2D(
                            (arc.StartX + arc.EndX) / 2 / scale.PixelsPerUnit,
                            (arc.StartY + arc.EndY) / 2 / scale.PixelsPerUnit),
                        Properties = new Dictionary<string, object>
                        {
                            ["Width"] = width,
                            ["Height"] = _settings.DefaultDoorHeight
                        }
                    });
                }
            }

            return doors;
        }

        private List<ConvertedPDFElement> ConvertToRooms(ParsedAnnotations annotations, List<ConvertedPDFElement> walls)
        {
            var rooms = new List<ConvertedPDFElement>();

            foreach (var label in annotations.RoomLabels)
            {
                rooms.Add(new ConvertedPDFElement
                {
                    ElementType = ConvertedElementType.Room,
                    Name = label.Text,
                    Center = label.Position,
                    Properties = new Dictionary<string, object>
                    {
                        ["Label"] = label.Text
                    }
                });
            }

            return rooms;
        }

        private List<ConvertedPDFElement> ConvertSectionElements(
            List<VectorGraphic> vectors,
            ParsedAnnotations annotations,
            DetectedDrawingView view)
        {
            var elements = new List<ConvertedPDFElement>();

            // Extract level information from section views
            foreach (var marker in annotations.LevelMarkers)
            {
                if (marker.Elevation.HasValue)
                {
                    elements.Add(new ConvertedPDFElement
                    {
                        ElementType = ConvertedElementType.Level,
                        Name = marker.Text,
                        Properties = new Dictionary<string, object>
                        {
                            ["Elevation"] = marker.Elevation.Value
                        }
                    });
                }
            }

            return elements;
        }

        private List<VectorGraphic> FilterVectorsInBounds(List<VectorGraphic> vectors, PDFRectangle bounds)
        {
            return vectors.Where(v => v.Bounds.Intersects(bounds)).ToList();
        }

        private ParsedAnnotations FilterAnnotationsInBounds(ParsedAnnotations annotations, PDFRectangle bounds)
        {
            return new ParsedAnnotations
            {
                Dimensions = annotations.Dimensions.Where(d =>
                    d.Position.X >= bounds.X && d.Position.X <= bounds.X + bounds.Width &&
                    d.Position.Y >= bounds.Y && d.Position.Y <= bounds.Y + bounds.Height).ToList(),
                RoomLabels = annotations.RoomLabels.Where(r =>
                    r.Position.X >= bounds.X && r.Position.X <= bounds.X + bounds.Width &&
                    r.Position.Y >= bounds.Y && r.Position.Y <= bounds.Y + bounds.Height).ToList(),
                GridLabels = annotations.GridLabels,
                LevelMarkers = annotations.LevelMarkers,
                Notes = annotations.Notes,
                Symbols = annotations.Symbols
            };
        }
    }

    #endregion

    #region Data Models

    // Settings
    public class PDFExtractionSettings
    {
        public long MaxFileSizeBytes { get; set; } = 200 * 1024 * 1024; // 200MB
        public double DefaultPixelsPerMM { get; set; } = 2.83; // 72 DPI / 25.4
        public double ViewBoundaryMinLineWidth { get; set; } = 2.0;
        public double MinViewArea { get; set; } = 10000; // pixels²
        public double TitleMinFontSize { get; set; } = 14;
        public double MinScaleBarLength { get; set; } = 50;
        public double DimensionProximity { get; set; } = 100; // pixels
        public double MinWallThickness { get; set; } = 50; // mm
        public double MaxWallThickness { get; set; } = 500; // mm
        public double DefaultDoorHeight { get; set; } = 2100; // mm
    }

    public class ExtractionOptions
    {
        public bool ExtractVectors { get; set; } = true;
        public bool ExtractText { get; set; } = true;
        public bool ParseAnnotations { get; set; } = true;
        public bool ConvertToElements { get; set; } = true;
    }

    // PDF Document
    public class PDFDocument
    {
        public string FilePath { get; set; }
        public string Version { get; set; }
        public PDFDocumentInfo Info { get; set; }
        public List<PDFPage> Pages { get; set; } = new();
    }

    public class PDFDocumentInfo
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Subject { get; set; }
        public DateTime? CreationDate { get; set; }
        public DateTime? ModificationDate { get; set; }
    }

    public class PDFPage
    {
        public int Number { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<byte[]> ContentStreams { get; set; } = new();
    }

    public class PDFOperator
    {
        public string Name { get; set; }
        public string[] Operands { get; set; }
    }

    // Vectors
    public abstract class VectorGraphic
    {
        public VectorType Type { get; set; }
        public double LineWidth { get; set; }
        public PDFColor StrokeColor { get; set; }
        public PDFColor FillColor { get; set; }
        public abstract PDFRectangle Bounds { get; }
    }

    public class LineVector : VectorGraphic
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double Length => Math.Sqrt(Math.Pow(EndX - StartX, 2) + Math.Pow(EndY - StartY, 2));

        public LineVector() { Type = VectorType.Line; }

        public override PDFRectangle Bounds => new PDFRectangle(
            Math.Min(StartX, EndX), Math.Min(StartY, EndY),
            Math.Abs(EndX - StartX), Math.Abs(EndY - StartY));
    }

    public class CurveVector : VectorGraphic
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double ControlX1 { get; set; }
        public double ControlY1 { get; set; }
        public double ControlX2 { get; set; }
        public double ControlY2 { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        public CurveVector() { Type = VectorType.Bezier; }

        public override PDFRectangle Bounds => new PDFRectangle(
            Math.Min(Math.Min(StartX, EndX), Math.Min(ControlX1, ControlX2)),
            Math.Min(Math.Min(StartY, EndY), Math.Min(ControlY1, ControlY2)),
            Math.Max(Math.Max(StartX, EndX), Math.Max(ControlX1, ControlX2)) -
                Math.Min(Math.Min(StartX, EndX), Math.Min(ControlX1, ControlX2)),
            Math.Max(Math.Max(StartY, EndY), Math.Max(ControlY1, ControlY2)) -
                Math.Min(Math.Min(StartY, EndY), Math.Min(ControlY1, ControlY2)));
    }

    public class RectangleVector : VectorGraphic
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public RectangleVector() { Type = VectorType.Rectangle; }

        public override PDFRectangle Bounds => new PDFRectangle(X, Y, Width, Height);
    }

    public class CircleVector : VectorGraphic
    {
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Radius { get; set; }

        public CircleVector() { Type = VectorType.Circle; }

        public override PDFRectangle Bounds => new PDFRectangle(
            CenterX - Radius, CenterY - Radius, Radius * 2, Radius * 2);
    }

    public class PolygonVector : VectorGraphic
    {
        public List<Point2D> Vertices { get; set; } = new();
        public int VertexCount => Vertices.Count;

        public PolygonVector() { Type = VectorType.Polygon; }

        public override PDFRectangle Bounds
        {
            get
            {
                if (Vertices.Count == 0) return new PDFRectangle(0, 0, 0, 0);
                return new PDFRectangle(
                    Vertices.Min(v => v.X), Vertices.Min(v => v.Y),
                    Vertices.Max(v => v.X) - Vertices.Min(v => v.X),
                    Vertices.Max(v => v.Y) - Vertices.Min(v => v.Y));
            }
        }
    }

    public class PDFRectangle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Area => Width * Height;

        public PDFRectangle() { }
        public PDFRectangle(double x, double y, double width, double height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public bool Intersects(PDFRectangle other)
        {
            return !(X > other.X + other.Width || X + Width < other.X ||
                     Y > other.Y + other.Height || Y + Height < other.Y);
        }
    }

    public class PDFColor
    {
        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }
    }

    // Text
    public class TextBlock
    {
        public string Content { get; set; }
        public Point2D Position { get; set; }
        public double FontSize { get; set; }
        public string FontName { get; set; }
    }

    // Annotations
    public class ParsedAnnotations
    {
        public List<DimensionAnnotation> Dimensions { get; set; } = new();
        public List<RoomLabelAnnotation> RoomLabels { get; set; } = new();
        public List<GridLabelAnnotation> GridLabels { get; set; } = new();
        public List<LevelMarkerAnnotation> LevelMarkers { get; set; } = new();
        public List<NoteAnnotation> Notes { get; set; } = new();
        public List<SymbolAnnotation> Symbols { get; set; } = new();

        public List<object> AllAnnotations =>
            Dimensions.Cast<object>()
                .Concat(RoomLabels).Concat(GridLabels).Concat(LevelMarkers)
                .Concat(Notes).Concat(Symbols).ToList();
    }

    public class DimensionAnnotation
    {
        public string Text { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public Point2D Position { get; set; }
        public List<LineVector> DimensionLines { get; set; }
    }

    public class DimensionMeasurement
    {
        public LineVector Line1 { get; set; }
        public LineVector Line2 { get; set; }
        public TextBlock Text { get; set; }
        public double Value { get; set; }
        public double PixelLength { get; set; }
    }

    public class RoomLabelAnnotation
    {
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public double FontSize { get; set; }
    }

    public class GridLabelAnnotation
    {
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public double CircleRadius { get; set; }
    }

    public class LevelMarkerAnnotation
    {
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public double? Elevation { get; set; }
    }

    public class NoteAnnotation
    {
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public double FontSize { get; set; }
    }

    public class SymbolAnnotation
    {
        public SymbolType Type { get; set; }
        public Point2D Position { get; set; }
    }

    // Drawing Views
    public class DetectedDrawingView
    {
        public string Id { get; set; }
        public DrawingViewType Type { get; set; }
        public PDFRectangle Bounds { get; set; }
        public string Title { get; set; }
        public ScaleInfo Scale { get; set; }
    }

    public class ScaleInfo
    {
        public string ScaleText { get; set; }
        public double ScaleRatio { get; set; }
        public double PixelsPerUnit { get; set; }
        public MeasurementUnit Unit { get; set; } = MeasurementUnit.Millimeters;
        public double Confidence { get; set; }
    }

    // Analysis
    public class DrawingAnalysis
    {
        public DrawingSet DrawingSet { get; set; }
        public List<CorrelatedViewSet> CorrelatedViews { get; set; } = new();
        public BuildingStructureAnalysis BuildingStructure { get; set; }
    }

    public class DrawingSet
    {
        public int TotalPages { get; set; }
        public Dictionary<DrawingViewType, int> DrawingTypes { get; set; } = new();
        public ScaleInfo PrimaryScale { get; set; }
    }

    public class CorrelatedViewSet
    {
        public DetectedDrawingView PrimaryView { get; set; }
        public List<DetectedDrawingView> RelatedViews { get; set; } = new();
    }

    public class BuildingStructureAnalysis
    {
        public int EstimatedLevels { get; set; }
        public bool HasGridSystem { get; set; }
        public List<string> GridLabelsX { get; set; } = new();
        public List<string> GridLabelsY { get; set; } = new();
    }

    // Converted Elements
    public class ConvertedPDFElement
    {
        public ConvertedElementType ElementType { get; set; }
        public string Name { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public Point2D Center { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    // Results
    public class PDFExtractionResult
    {
        public bool Success { get; set; }
        public string SourceFile { get; set; }
        public DateTime ExtractionStartTime { get; set; }
        public DateTime ExtractionEndTime { get; set; }
        public TimeSpan Duration => ExtractionEndTime - ExtractionStartTime;
        public PDFDocumentInfo DocumentInfo { get; set; }
        public int PageCount { get; set; }
        public List<PageExtractionResult> Pages { get; set; } = new();
        public DrawingAnalysis DrawingAnalysis { get; set; }
        public List<ConvertedPDFElement> ConvertedElements { get; set; } = new();
        public ExtractionStatistics Statistics { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class PageExtractionResult
    {
        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<VectorGraphic> VectorGraphics { get; set; } = new();
        public List<TextBlock> TextContent { get; set; } = new();
        public ParsedAnnotations Annotations { get; set; } = new();
        public List<DetectedDrawingView> DetectedViews { get; set; } = new();
        public ScaleInfo DetectedScale { get; set; }
    }

    public class PDFPreview
    {
        public string SourceFile { get; set; }
        public PDFDocumentInfo DocumentInfo { get; set; }
        public int PageCount { get; set; }
        public List<PagePreview> PagePreviews { get; set; } = new();
        public string Error { get; set; }
    }

    public class PagePreview
    {
        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool HasVectorContent { get; set; }
        public bool HasTextContent { get; set; }
        public double EstimatedComplexity { get; set; }
    }

    public class ExtractionProgress
    {
        public int PercentComplete { get; set; }
        public string Status { get; set; }

        public ExtractionProgress(int percent, string status)
        {
            PercentComplete = percent;
            Status = status;
        }
    }

    public class ExtractionStatistics
    {
        public int TotalPages { get; set; }
        public int TotalVectors { get; set; }
        public int TotalLines { get; set; }
        public int TotalArcs { get; set; }
        public int TotalRectangles { get; set; }
        public int TotalTextBlocks { get; set; }
        public int TotalAnnotations { get; set; }
        public int TotalDetectedViews { get; set; }
        public int ConvertedElements { get; set; }
        public int ConvertedWalls { get; set; }
        public int ConvertedDoors { get; set; }
        public int ConvertedWindows { get; set; }
        public int ConvertedRooms { get; set; }
    }

    // Point2D moved to StingBIM.AI.Creation.Common.SharedCreationTypes

    #endregion

    #region Enumerations

    public enum VectorType
    {
        Line,
        Arc,
        Bezier,
        Circle,
        Ellipse,
        Rectangle,
        Polygon,
        Path
    }

    // DrawingViewType and MeasurementUnit moved to StingBIM.AI.Creation.Common.SharedCreationTypes

    public enum SymbolType
    {
        NorthArrow,
        ScaleBar,
        SectionMark,
        ElevationMark,
        DetailCallout,
        DoorNumber,
        WindowNumber,
        GridBubble
    }

    public enum ConvertedElementType
    {
        Wall,
        Door,
        Window,
        Room,
        Column,
        Beam,
        Floor,
        Ceiling,
        Roof,
        Level,
        Grid,
        Annotation
    }

    #endregion
}
