// ===================================================================================
// StingBIM Image-to-BIM Pipeline
// Floor plan image processing with AI-based wall, door, and window detection
// ===================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Import
{
    /// <summary>
    /// Comprehensive image-to-BIM pipeline for converting floor plan images into 3D BIM models.
    /// Supports multiple image formats and uses AI-based detection for walls, doors, windows,
    /// rooms, and annotations. Handles plans, sections, and elevations from the same sheet.
    /// </summary>
    public class ImageToBIMPipeline
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ImagePreprocessor _preprocessor;
        private readonly WallDetector _wallDetector;
        private readonly OpeningDetector _openingDetector;
        private readonly RoomDetector _roomDetector;
        private readonly AnnotationExtractor _annotationExtractor;
        private readonly ScaleDetector _scaleDetector;
        private readonly ViewClassifier _viewClassifier;
        private readonly BIMModelBuilder _modelBuilder;
        private readonly ImageToBIMSettings _settings;

        public ImageToBIMPipeline(ImageToBIMSettings settings = null)
        {
            _settings = settings ?? new ImageToBIMSettings();
            _preprocessor = new ImagePreprocessor(_settings);
            _wallDetector = new WallDetector(_settings);
            _openingDetector = new OpeningDetector(_settings);
            _roomDetector = new RoomDetector(_settings);
            _annotationExtractor = new AnnotationExtractor(_settings);
            _scaleDetector = new ScaleDetector(_settings);
            _viewClassifier = new ViewClassifier(_settings);
            _modelBuilder = new BIMModelBuilder(_settings);

            Logger.Info("ImageToBIMPipeline initialized");
        }

        #region Main Pipeline Methods

        /// <summary>
        /// Process a single floor plan image and convert to BIM model
        /// </summary>
        public async Task<ImageToBIMResult> ProcessImageAsync(
            string imagePath,
            ProcessingOptions options = null,
            IProgress<ProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ImageToBIMResult
            {
                SourceImage = imagePath,
                ProcessingStartTime = DateTime.Now
            };

            try
            {
                Logger.Info("Starting image-to-BIM processing: {0}", imagePath);
                options ??= new ProcessingOptions();

                // Validate image
                progress?.Report(new ProcessingProgress(0, "Validating image..."));
                if (!ValidateImage(imagePath, out string validationError))
                {
                    result.Success = false;
                    result.Errors.Add(validationError);
                    return result;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Load and preprocess image
                progress?.Report(new ProcessingProgress(5, "Loading and preprocessing image..."));
                var imageData = await _preprocessor.LoadAndPreprocessAsync(imagePath, cancellationToken);
                result.ImageInfo = imageData.Info;

                cancellationToken.ThrowIfCancellationRequested();

                // Classify views (plan, section, elevation)
                progress?.Report(new ProcessingProgress(10, "Classifying drawing views..."));
                var viewRegions = await _viewClassifier.ClassifyViewsAsync(imageData, cancellationToken);
                result.DetectedViews = viewRegions;

                cancellationToken.ThrowIfCancellationRequested();

                // Detect scale
                progress?.Report(new ProcessingProgress(15, "Detecting drawing scale..."));
                var scaleInfo = await _scaleDetector.DetectScaleAsync(imageData, cancellationToken);
                result.DetectedScale = scaleInfo;
                imageData.Scale = scaleInfo.PixelsPerUnit;

                cancellationToken.ThrowIfCancellationRequested();

                // Process each view region
                var processedViews = new List<ProcessedView>();
                int viewProgress = 0;
                int viewStep = 60 / Math.Max(1, viewRegions.Count);

                foreach (var viewRegion in viewRegions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var viewImage = await _preprocessor.ExtractRegionAsync(imageData, viewRegion.Bounds, cancellationToken);
                    viewImage.Scale = scaleInfo.PixelsPerUnit;

                    progress?.Report(new ProcessingProgress(
                        20 + viewProgress,
                        $"Processing {viewRegion.ViewType} view..."));

                    var processedView = await ProcessViewAsync(
                        viewImage,
                        viewRegion,
                        options,
                        cancellationToken);

                    processedViews.Add(processedView);
                    viewProgress += viewStep;
                }

                result.ProcessedViews = processedViews;

                cancellationToken.ThrowIfCancellationRequested();

                // Build 3D BIM model from processed views
                progress?.Report(new ProcessingProgress(85, "Building 3D BIM model..."));
                var bimModel = await _modelBuilder.BuildModelAsync(
                    processedViews,
                    scaleInfo,
                    options,
                    cancellationToken);

                result.BIMModel = bimModel;
                result.Statistics = CalculateStatistics(result);

                // Post-processing and validation
                progress?.Report(new ProcessingProgress(95, "Validating model..."));
                await ValidateModelAsync(result, options, cancellationToken);

                progress?.Report(new ProcessingProgress(100, "Processing complete"));
                result.Success = true;
                result.ProcessingEndTime = DateTime.Now;

                Logger.Info("Image-to-BIM processing completed: {0} elements created",
                    bimModel.Elements.Count);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Errors.Add("Processing cancelled by user");
                Logger.Warn("Image-to-BIM processing cancelled: {0}", imagePath);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Processing failed: {ex.Message}");
                Logger.Error(ex, "Image-to-BIM processing failed: {0}", imagePath);
            }

            return result;
        }

        /// <summary>
        /// Process multiple related images (plans, sections, elevations) into a single model
        /// </summary>
        public async Task<ImageToBIMResult> ProcessMultipleImagesAsync(
            IEnumerable<ImageInput> images,
            ProcessingOptions options = null,
            IProgress<ProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ImageToBIMResult
            {
                ProcessingStartTime = DateTime.Now
            };

            var imageList = images.ToList();
            var allProcessedViews = new List<ProcessedView>();
            ImageScaleInfo globalScale = null;

            int imageProgress = 0;
            int imageStep = 80 / Math.Max(1, imageList.Count);

            foreach (var imageInput in imageList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ProcessingProgress(
                    imageProgress,
                    $"Processing {Path.GetFileName(imageInput.Path)}..."));

                var singleResult = await ProcessImageAsync(
                    imageInput.Path,
                    options,
                    null,
                    cancellationToken);

                if (singleResult.Success)
                {
                    // Apply level/view type override if specified
                    foreach (var view in singleResult.ProcessedViews)
                    {
                        if (!string.IsNullOrEmpty(imageInput.LevelName))
                            view.LevelName = imageInput.LevelName;
                        if (imageInput.ViewType.HasValue)
                            view.ViewType = imageInput.ViewType.Value;
                        if (imageInput.Elevation.HasValue)
                            view.Elevation = imageInput.Elevation.Value;
                    }

                    allProcessedViews.AddRange(singleResult.ProcessedViews);
                    globalScale ??= singleResult.DetectedScale;
                }
                else
                {
                    result.Warnings.AddRange(singleResult.Errors.Select(e =>
                        $"{Path.GetFileName(imageInput.Path)}: {e}"));
                }

                imageProgress += imageStep;
            }

            // Build combined 3D model
            progress?.Report(new ProcessingProgress(85, "Building combined 3D BIM model..."));
            var bimModel = await _modelBuilder.BuildModelAsync(
                allProcessedViews,
                globalScale ?? new ImageScaleInfo(),
                options ?? new ProcessingOptions(),
                cancellationToken);

            result.BIMModel = bimModel;
            result.ProcessedViews = allProcessedViews;
            result.DetectedScale = globalScale;
            result.Statistics = CalculateStatistics(result);
            result.Success = true;
            result.ProcessingEndTime = DateTime.Now;

            progress?.Report(new ProcessingProgress(100, "Processing complete"));

            return result;
        }

        /// <summary>
        /// Process a single view region within an image
        /// </summary>
        private async Task<ProcessedView> ProcessViewAsync(
            PreprocessedImage viewImage,
            ViewRegion viewRegion,
            ProcessingOptions options,
            CancellationToken cancellationToken)
        {
            var processedView = new ProcessedView
            {
                ViewType = viewRegion.ViewType,
                Bounds = viewRegion.Bounds,
                LevelName = viewRegion.LevelName ?? "Level 1",
                Elevation = viewRegion.Elevation
            };

            // Detect walls
            var walls = await _wallDetector.DetectWallsAsync(viewImage, viewRegion.ViewType, cancellationToken);
            processedView.DetectedWalls = walls;

            // Detect openings (doors, windows)
            var openings = await _openingDetector.DetectOpeningsAsync(viewImage, walls, viewRegion.ViewType, cancellationToken);
            processedView.DetectedOpenings = openings;

            // Detect rooms (for plan views)
            if (viewRegion.ViewType == DrawingViewType.FloorPlan)
            {
                var rooms = await _roomDetector.DetectRoomsAsync(viewImage, walls, cancellationToken);
                processedView.DetectedRooms = rooms;
            }

            // Extract annotations
            var annotations = await _annotationExtractor.ExtractAnnotationsAsync(viewImage, cancellationToken);
            processedView.Annotations = annotations;

            // Match room labels to detected rooms
            if (processedView.DetectedRooms != null && annotations.RoomLabels.Any())
            {
                MatchRoomLabels(processedView.DetectedRooms, annotations.RoomLabels);
            }

            return processedView;
        }

        #endregion

        #region Validation and Statistics

        private bool ValidateImage(string imagePath, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(imagePath))
            {
                error = "Image path is null or empty";
                return false;
            }

            if (!File.Exists(imagePath))
            {
                error = $"Image file not found: {imagePath}";
                return false;
            }

            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp", ".gif" };

            if (!supportedExtensions.Contains(extension))
            {
                error = $"Unsupported image format: {extension}";
                return false;
            }

            var fileInfo = new FileInfo(imagePath);
            if (fileInfo.Length > _settings.MaxFileSizeBytes)
            {
                error = $"Image file too large ({fileInfo.Length / 1024 / 1024}MB). Maximum: {_settings.MaxFileSizeBytes / 1024 / 1024}MB";
                return false;
            }

            return true;
        }

        private async Task ValidateModelAsync(
            ImageToBIMResult result,
            ProcessingOptions options,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Check for disconnected walls
                var disconnectedWalls = FindDisconnectedWalls(result.BIMModel);
                if (disconnectedWalls.Any())
                {
                    result.Warnings.Add($"{disconnectedWalls.Count} walls may not be properly connected");
                }

                // Check for overlapping elements
                var overlappingElements = FindOverlappingElements(result.BIMModel);
                if (overlappingElements.Any())
                {
                    result.Warnings.Add($"{overlappingElements.Count} potential element overlaps detected");
                }

                // Check for rooms without labels
                var unlabeledRooms = result.ProcessedViews
                    .SelectMany(v => v.DetectedRooms ?? new List<ImageDetectedRoom>())
                    .Where(r => string.IsNullOrEmpty(r.Name))
                    .Count();

                if (unlabeledRooms > 0)
                {
                    result.Warnings.Add($"{unlabeledRooms} rooms could not be labeled");
                }

                // Validate scale
                if (result.DetectedScale == null || result.DetectedScale.Confidence < 0.5)
                {
                    result.Warnings.Add("Drawing scale could not be reliably detected. Default scale used.");
                }

            }, cancellationToken);
        }

        private List<BIMElement> FindDisconnectedWalls(BIMModel model)
        {
            var walls = model.Elements.Where(e => e.ElementType == BIMElementType.Wall).ToList();
            var disconnected = new List<BIMElement>();
            var tolerance = _settings.ConnectionTolerance;

            foreach (var wall in walls)
            {
                var wallLine = wall.Geometry as WallGeometry;
                if (wallLine == null) continue;

                bool startConnected = false;
                bool endConnected = false;

                foreach (var other in walls)
                {
                    if (other == wall) continue;
                    var otherLine = other.Geometry as WallGeometry;
                    if (otherLine == null) continue;

                    if (Distance2D(wallLine.Start, otherLine.Start) < tolerance ||
                        Distance2D(wallLine.Start, otherLine.End) < tolerance)
                        startConnected = true;

                    if (Distance2D(wallLine.End, otherLine.Start) < tolerance ||
                        Distance2D(wallLine.End, otherLine.End) < tolerance)
                        endConnected = true;
                }

                if (!startConnected || !endConnected)
                    disconnected.Add(wall);
            }

            return disconnected;
        }

        private List<(BIMElement, BIMElement)> FindOverlappingElements(BIMModel model)
        {
            var overlaps = new List<(BIMElement, BIMElement)>();
            var elements = model.Elements.ToList();

            for (int i = 0; i < elements.Count; i++)
            {
                for (int j = i + 1; j < elements.Count; j++)
                {
                    if (ElementsOverlap(elements[i], elements[j]))
                    {
                        overlaps.Add((elements[i], elements[j]));
                    }
                }
            }

            return overlaps;
        }

        private bool ElementsOverlap(BIMElement a, BIMElement b)
        {
            // Simple bounding box overlap check
            var boxA = a.Geometry?.GetBounds();
            var boxB = b.Geometry?.GetBounds();

            if (boxA == null || boxB == null) return false;

            return !(boxA.Max.X < boxB.Min.X || boxA.Min.X > boxB.Max.X ||
                     boxA.Max.Y < boxB.Min.Y || boxA.Min.Y > boxB.Max.Y);
        }

        private double Distance2D(Point2D a, Point2D b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private ProcessingStatistics CalculateStatistics(ImageToBIMResult result)
        {
            return new ProcessingStatistics
            {
                TotalViewsProcessed = result.ProcessedViews?.Count ?? 0,
                WallsDetected = result.ProcessedViews?.Sum(v => v.DetectedWalls?.Count ?? 0) ?? 0,
                DoorsDetected = result.ProcessedViews?.Sum(v => v.DetectedOpenings?.Count(o => o.OpeningType == OpeningType.Door) ?? 0) ?? 0,
                WindowsDetected = result.ProcessedViews?.Sum(v => v.DetectedOpenings?.Count(o => o.OpeningType == OpeningType.Window) ?? 0) ?? 0,
                RoomsDetected = result.ProcessedViews?.Sum(v => v.DetectedRooms?.Count ?? 0) ?? 0,
                AnnotationsExtracted = result.ProcessedViews?.Sum(v => v.Annotations?.AllAnnotations.Count ?? 0) ?? 0,
                BIMElementsCreated = result.BIMModel?.Elements.Count ?? 0,
                ProcessingDuration = result.ProcessingEndTime - result.ProcessingStartTime
            };
        }

        private void MatchRoomLabels(List<ImageDetectedRoom> rooms, List<RoomLabel> labels)
        {
            foreach (var label in labels)
            {
                var containingRoom = rooms.FirstOrDefault(r =>
                    PointInPolygon(label.Position, r.BoundaryPoints));

                if (containingRoom != null)
                {
                    containingRoom.Name = label.Text;
                    containingRoom.LabelPosition = label.Position;
                }
            }
        }

        private bool PointInPolygon(Point2D point, List<Point2D> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;

            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Y < point.Y && polygon[j].Y >= point.Y ||
                     polygon[j].Y < point.Y && polygon[i].Y >= point.Y) &&
                    (polygon[i].X + (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < point.X))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }

        #endregion
    }

    #region Image Preprocessing

    /// <summary>
    /// Handles image loading, preprocessing, and enhancement
    /// </summary>
    internal class ImagePreprocessor
    {
        private readonly ImageToBIMSettings _settings;

        public ImagePreprocessor(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<PreprocessedImage> LoadAndPreprocessAsync(
            string imagePath,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var image = new PreprocessedImage
                {
                    SourcePath = imagePath,
                    Info = GetImageInfo(imagePath)
                };

                // Load raw pixel data
                image.RawPixels = LoadImagePixels(imagePath, out int width, out int height);
                image.Width = width;
                image.Height = height;

                // Convert to grayscale
                image.GrayscalePixels = ConvertToGrayscale(image.RawPixels, width, height);

                // Apply preprocessing steps
                if (_settings.ApplyNoiseReduction)
                {
                    image.GrayscalePixels = ApplyGaussianBlur(image.GrayscalePixels, width, height, _settings.BlurRadius);
                }

                // Binarize for line detection
                image.BinaryPixels = ApplyAdaptiveThreshold(image.GrayscalePixels, width, height);

                // Edge detection for wall extraction
                image.EdgePixels = ApplySobelEdgeDetection(image.GrayscalePixels, width, height);

                // Hough transform for line detection
                image.DetectedLines = DetectLinesHough(image.BinaryPixels, width, height);

                return image;
            }, cancellationToken);
        }

        public async Task<PreprocessedImage> ExtractRegionAsync(
            PreprocessedImage source,
            Rectangle bounds,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var region = new PreprocessedImage
                {
                    SourcePath = source.SourcePath,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Scale = source.Scale
                };

                region.GrayscalePixels = ExtractRegion(source.GrayscalePixels, source.Width, bounds);
                region.BinaryPixels = ExtractRegion(source.BinaryPixels, source.Width, bounds);
                region.EdgePixels = ExtractRegion(source.EdgePixels, source.Width, bounds);

                // Filter lines to region
                region.DetectedLines = source.DetectedLines
                    .Where(l => LineIntersectsRegion(l, bounds))
                    .Select(l => TranslateLine(l, -bounds.X, -bounds.Y))
                    .ToList();

                return region;
            }, cancellationToken);
        }

        private ImageInfo GetImageInfo(string path)
        {
            var fileInfo = new FileInfo(path);

            // Read image dimensions from file header
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var dimensions = ReadImageDimensions(stream, Path.GetExtension(path).ToLowerInvariant());

            return new ImageInfo
            {
                FileName = fileInfo.Name,
                FilePath = path,
                FileSize = fileInfo.Length,
                Width = dimensions.Width,
                Height = dimensions.Height,
                Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                DPI = dimensions.DPI
            };
        }

        private (int Width, int Height, int DPI) ReadImageDimensions(Stream stream, string extension)
        {
            // Simplified dimension reading - in production, use a proper image library
            int width = 0, height = 0, dpi = 96;

            switch (extension)
            {
                case ".png":
                    // PNG header: signature (8 bytes) + IHDR chunk
                    stream.Seek(16, SeekOrigin.Begin);
                    var widthBytes = new byte[4];
                    var heightBytes = new byte[4];
                    stream.Read(widthBytes, 0, 4);
                    stream.Read(heightBytes, 0, 4);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(widthBytes);
                        Array.Reverse(heightBytes);
                    }
                    width = BitConverter.ToInt32(widthBytes, 0);
                    height = BitConverter.ToInt32(heightBytes, 0);
                    break;

                case ".jpg":
                case ".jpeg":
                    // JPEG SOF0 marker contains dimensions
                    width = 1920; // Placeholder - would parse JPEG structure
                    height = 1080;
                    break;

                default:
                    width = 1920;
                    height = 1080;
                    break;
            }

            return (width, height, dpi);
        }

        private byte[] LoadImagePixels(string path, out int width, out int height)
        {
            // Simplified loading - in production, use System.Drawing or ImageSharp
            var info = GetImageInfo(path);
            width = info.Width;
            height = info.Height;

            // Return placeholder - actual implementation would decode image
            return new byte[width * height * 3];
        }

        private byte[] ConvertToGrayscale(byte[] rgb, int width, int height)
        {
            var gray = new byte[width * height];
            for (int i = 0; i < width * height; i++)
            {
                int r = rgb[i * 3];
                int g = rgb[i * 3 + 1];
                int b = rgb[i * 3 + 2];
                // Luminosity formula
                gray[i] = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
            }
            return gray;
        }

        private byte[] ApplyGaussianBlur(byte[] pixels, int width, int height, int radius)
        {
            var result = new byte[pixels.Length];
            var kernel = CreateGaussianKernel(radius);
            int kernelSize = radius * 2 + 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    float kernelSum = 0;

                    for (int ky = -radius; ky <= radius; ky++)
                    {
                        for (int kx = -radius; kx <= radius; kx++)
                        {
                            int px = Math.Clamp(x + kx, 0, width - 1);
                            int py = Math.Clamp(y + ky, 0, height - 1);
                            float kernelValue = kernel[(ky + radius) * kernelSize + (kx + radius)];
                            sum += pixels[py * width + px] * kernelValue;
                            kernelSum += kernelValue;
                        }
                    }

                    result[y * width + x] = (byte)(sum / kernelSum);
                }
            }

            return result;
        }

        private float[] CreateGaussianKernel(int radius)
        {
            int size = radius * 2 + 1;
            var kernel = new float[size * size];
            float sigma = radius / 3.0f;
            float twoSigmaSquare = 2 * sigma * sigma;
            float sum = 0;

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float value = (float)Math.Exp(-(x * x + y * y) / twoSigmaSquare);
                    kernel[(y + radius) * size + (x + radius)] = value;
                    sum += value;
                }
            }

            // Normalize
            for (int i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;

            return kernel;
        }

        private byte[] ApplyAdaptiveThreshold(byte[] pixels, int width, int height)
        {
            var result = new byte[pixels.Length];
            int blockSize = _settings.AdaptiveBlockSize;
            int offset = blockSize / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Calculate local mean
                    int sum = 0;
                    int count = 0;

                    for (int dy = -offset; dy <= offset; dy++)
                    {
                        for (int dx = -offset; dx <= offset; dx++)
                        {
                            int px = x + dx;
                            int py = y + dy;
                            if (px >= 0 && px < width && py >= 0 && py < height)
                            {
                                sum += pixels[py * width + px];
                                count++;
                            }
                        }
                    }

                    int mean = sum / count;
                    int threshold = mean - _settings.AdaptiveConstant;
                    result[y * width + x] = pixels[y * width + x] < threshold ? (byte)0 : (byte)255;
                }
            }

            return result;
        }

        private byte[] ApplySobelEdgeDetection(byte[] pixels, int width, int height)
        {
            var result = new byte[pixels.Length];

            // Sobel kernels
            int[] gx = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] gy = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int sumX = 0, sumY = 0;
                    int k = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int pixel = pixels[(y + ky) * width + (x + kx)];
                            sumX += pixel * gx[k];
                            sumY += pixel * gy[k];
                            k++;
                        }
                    }

                    int magnitude = (int)Math.Sqrt(sumX * sumX + sumY * sumY);
                    result[y * width + x] = (byte)Math.Min(255, magnitude);
                }
            }

            return result;
        }

        private List<ImageDetectedLine> DetectLinesHough(byte[] binary, int width, int height)
        {
            var lines = new List<ImageDetectedLine>();
            int maxRho = (int)Math.Sqrt(width * width + height * height);
            int numAngles = 180;
            var accumulator = new int[maxRho * 2, numAngles];

            // Vote
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (binary[y * width + x] == 0) // Edge pixel
                    {
                        for (int t = 0; t < numAngles; t++)
                        {
                            double theta = t * Math.PI / numAngles;
                            int rho = (int)(x * Math.Cos(theta) + y * Math.Sin(theta)) + maxRho;
                            if (rho >= 0 && rho < maxRho * 2)
                                accumulator[rho, t]++;
                        }
                    }
                }
            }

            // Find peaks
            int threshold = _settings.HoughThreshold;
            for (int r = 0; r < maxRho * 2; r++)
            {
                for (int t = 0; t < numAngles; t++)
                {
                    if (accumulator[r, t] > threshold)
                    {
                        double theta = t * Math.PI / numAngles;
                        double rho = r - maxRho;
                        lines.Add(CreateLineFromHough(rho, theta, width, height, accumulator[r, t]));
                    }
                }
            }

            // Merge similar lines
            lines = MergeSimilarLines(lines);

            return lines;
        }

        private ImageDetectedLine CreateLineFromHough(double rho, double theta, int width, int height, int votes)
        {
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);

            Point2D start, end;

            if (Math.Abs(sinTheta) < 0.001) // Vertical line
            {
                start = new Point2D(rho, 0);
                end = new Point2D(rho, height);
            }
            else if (Math.Abs(cosTheta) < 0.001) // Horizontal line
            {
                start = new Point2D(0, rho);
                end = new Point2D(width, rho);
            }
            else
            {
                // Calculate intersection with image bounds
                double x0 = 0, y0 = (rho - x0 * cosTheta) / sinTheta;
                double x1 = width, y1 = (rho - x1 * cosTheta) / sinTheta;
                start = new Point2D(x0, y0);
                end = new Point2D(x1, y1);
            }

            return new ImageDetectedLine
            {
                Start = start,
                End = end,
                Angle = theta * 180 / Math.PI,
                Strength = votes
            };
        }

        private List<ImageDetectedLine> MergeSimilarLines(List<ImageDetectedLine> lines)
        {
            var merged = new List<ImageDetectedLine>();
            var used = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                if (used[i]) continue;

                var current = lines[i];
                var similar = new List<ImageDetectedLine> { current };
                used[i] = true;

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (used[j]) continue;

                    if (LinesAreSimilar(current, lines[j]))
                    {
                        similar.Add(lines[j]);
                        used[j] = true;
                    }
                }

                // Average similar lines
                merged.Add(AverageLines(similar));
            }

            return merged;
        }

        private bool LinesAreSimilar(ImageDetectedLine a, ImageDetectedLine b)
        {
            double angleDiff = Math.Abs(a.Angle - b.Angle);
            if (angleDiff > _settings.LineAngleTolerance && angleDiff < 180 - _settings.LineAngleTolerance)
                return false;

            // Check distance between midpoints
            var midA = new Point2D((a.Start.X + a.End.X) / 2, (a.Start.Y + a.End.Y) / 2);
            var midB = new Point2D((b.Start.X + b.End.X) / 2, (b.Start.Y + b.End.Y) / 2);

            return Distance2D(midA, midB) < _settings.LineMergeTolerance;
        }

        private double Distance2D(Point2D a, Point2D b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private ImageDetectedLine AverageLines(List<ImageDetectedLine> lines)
        {
            double avgStartX = lines.Average(l => l.Start.X);
            double avgStartY = lines.Average(l => l.Start.Y);
            double avgEndX = lines.Average(l => l.End.X);
            double avgEndY = lines.Average(l => l.End.Y);
            double avgAngle = lines.Average(l => l.Angle);
            int totalStrength = lines.Sum(l => l.Strength);

            return new ImageDetectedLine
            {
                Start = new Point2D(avgStartX, avgStartY),
                End = new Point2D(avgEndX, avgEndY),
                Angle = avgAngle,
                Strength = totalStrength
            };
        }

        private byte[] ExtractRegion(byte[] pixels, int sourceWidth, Rectangle bounds)
        {
            var result = new byte[bounds.Width * bounds.Height];

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    int srcX = bounds.X + x;
                    int srcY = bounds.Y + y;
                    result[y * bounds.Width + x] = pixels[srcY * sourceWidth + srcX];
                }
            }

            return result;
        }

        private bool LineIntersectsRegion(ImageDetectedLine line, Rectangle region)
        {
            return line.Start.X >= region.X && line.Start.X <= region.X + region.Width &&
                   line.Start.Y >= region.Y && line.Start.Y <= region.Y + region.Height;
        }

        private ImageDetectedLine TranslateLine(ImageDetectedLine line, double dx, double dy)
        {
            return new ImageDetectedLine
            {
                Start = new Point2D(line.Start.X + dx, line.Start.Y + dy),
                End = new Point2D(line.End.X + dx, line.End.Y + dy),
                Angle = line.Angle,
                Strength = line.Strength
            };
        }
    }

    #endregion

    #region Detection Components

    /// <summary>
    /// Classifies different view types within a drawing sheet
    /// </summary>
    internal class ViewClassifier
    {
        private readonly ImageToBIMSettings _settings;

        public ViewClassifier(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<ViewRegion>> ClassifyViewsAsync(
            PreprocessedImage image,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var regions = new List<ViewRegion>();

                // Detect view boundaries using line detection
                var viewBoundaries = DetectViewBoundaries(image);

                if (viewBoundaries.Count == 0)
                {
                    // Single view - use entire image
                    regions.Add(new ViewRegion
                    {
                        ViewType = DrawingViewType.FloorPlan,
                        Bounds = new Rectangle(0, 0, image.Width, image.Height),
                        Confidence = 1.0
                    });
                }
                else
                {
                    foreach (var boundary in viewBoundaries)
                    {
                        var viewType = ClassifyViewType(image, boundary);
                        regions.Add(new ViewRegion
                        {
                            ViewType = viewType,
                            Bounds = boundary,
                            Confidence = 0.8
                        });
                    }
                }

                return regions;
            }, cancellationToken);
        }

        private List<Rectangle> DetectViewBoundaries(PreprocessedImage image)
        {
            var boundaries = new List<Rectangle>();

            // Look for heavy horizontal and vertical lines that might be view borders
            var horizontalLines = image.DetectedLines
                .Where(l => Math.Abs(l.Angle) < 5 || Math.Abs(l.Angle - 180) < 5)
                .OrderBy(l => l.Start.Y)
                .ToList();

            var verticalLines = image.DetectedLines
                .Where(l => Math.Abs(l.Angle - 90) < 5)
                .OrderBy(l => l.Start.X)
                .ToList();

            // Find intersecting lines that form rectangles
            // Simplified: Just look for major divisions

            return boundaries;
        }

        private DrawingViewType ClassifyViewType(PreprocessedImage image, Rectangle region)
        {
            // Analyze the content to determine view type
            // Plans typically have more complex layouts with rooms
            // Sections have horizontal emphasis with level lines
            // Elevations show facade details

            // Count horizontal vs vertical lines
            var regionLines = image.DetectedLines
                .Where(l => LineInRegion(l, region))
                .ToList();

            int horizontal = regionLines.Count(l => Math.Abs(l.Angle) < 10 || Math.Abs(l.Angle - 180) < 10);
            int vertical = regionLines.Count(l => Math.Abs(l.Angle - 90) < 10);

            // Heuristic classification
            if (horizontal > vertical * 1.5)
            {
                return DrawingViewType.Section;
            }
            else if (vertical > horizontal * 1.5)
            {
                return DrawingViewType.Elevation;
            }
            else
            {
                return DrawingViewType.FloorPlan;
            }
        }

        private bool LineInRegion(ImageDetectedLine line, Rectangle region)
        {
            return line.Start.X >= region.X && line.Start.X <= region.X + region.Width &&
                   line.Start.Y >= region.Y && line.Start.Y <= region.Y + region.Height;
        }
    }

    /// <summary>
    /// Detects drawing scale from dimension annotations and scale bars
    /// </summary>
    internal class ScaleDetector
    {
        private readonly ImageToBIMSettings _settings;

        public ScaleDetector(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<ImageScaleInfo> DetectScaleAsync(
            PreprocessedImage image,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var scaleInfo = new ImageScaleInfo
                {
                    DetectedScale = "1:100",
                    PixelsPerUnit = _settings.DefaultPixelsPerMM,
                    Unit = MeasurementUnit.Millimeters,
                    Confidence = 0.5
                };

                // Look for scale notation (e.g., "1:100", "1/4" = 1'-0"")
                var scalePattern = DetectScalePattern(image);
                if (scalePattern != null)
                {
                    scaleInfo.DetectedScale = scalePattern.ScaleText;
                    scaleInfo.PixelsPerUnit = CalculatePixelsPerUnit(scalePattern);
                    scaleInfo.Confidence = scalePattern.Confidence;
                }

                // Look for scale bar
                var scaleBar = DetectScaleBar(image);
                if (scaleBar != null && scaleBar.Confidence > scaleInfo.Confidence)
                {
                    scaleInfo.PixelsPerUnit = scaleBar.PixelsPerUnit;
                    scaleInfo.Confidence = scaleBar.Confidence;
                }

                // Look for dimension strings to calibrate
                var dimensionCalibration = CalibrateFromDimensions(image);
                if (dimensionCalibration != null && dimensionCalibration.Confidence > scaleInfo.Confidence)
                {
                    scaleInfo.PixelsPerUnit = dimensionCalibration.PixelsPerUnit;
                    scaleInfo.Confidence = dimensionCalibration.Confidence;
                }

                return scaleInfo;
            }, cancellationToken);
        }

        private ScalePattern DetectScalePattern(PreprocessedImage image)
        {
            // Would use OCR to find scale notations
            // Common patterns: "1:50", "1:100", "1:200", "1/4\" = 1'-0\""
            return null;
        }

        private ScaleBarInfo DetectScaleBar(PreprocessedImage image)
        {
            // Look for horizontal lines with tick marks and labels
            return null;
        }

        private ScaleCalibration CalibrateFromDimensions(PreprocessedImage image)
        {
            // Find dimension strings and measure their pixel extent
            return null;
        }

        private double CalculatePixelsPerUnit(ScalePattern pattern)
        {
            // Parse scale ratio and calculate pixels per mm
            return _settings.DefaultPixelsPerMM;
        }
    }

    /// <summary>
    /// Detects walls from processed image data
    /// </summary>
    internal class WallDetector
    {
        private readonly ImageToBIMSettings _settings;

        public WallDetector(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<DetectedWall>> DetectWallsAsync(
            PreprocessedImage image,
            DrawingViewType viewType,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var walls = new List<DetectedWall>();

                // For plan views, detect parallel lines that could be wall edges
                if (viewType == DrawingViewType.FloorPlan)
                {
                    walls = DetectWallsFromPlan(image);
                }
                else if (viewType == DrawingViewType.Section || viewType == DrawingViewType.Elevation)
                {
                    walls = DetectWallsFromSection(image);
                }

                return walls;
            }, cancellationToken);
        }

        private List<DetectedWall> DetectWallsFromPlan(PreprocessedImage image)
        {
            var walls = new List<DetectedWall>();
            var usedLines = new HashSet<int>();

            // Group parallel lines that could be wall edges
            for (int i = 0; i < image.DetectedLines.Count; i++)
            {
                if (usedLines.Contains(i)) continue;

                var line1 = image.DetectedLines[i];

                // Find parallel line at consistent distance (wall thickness)
                for (int j = i + 1; j < image.DetectedLines.Count; j++)
                {
                    if (usedLines.Contains(j)) continue;

                    var line2 = image.DetectedLines[j];

                    if (LinesAreParallel(line1, line2))
                    {
                        double distance = DistanceBetweenParallelLines(line1, line2);
                        double thickness = distance / image.Scale;

                        // Check if distance matches common wall thicknesses
                        if (IsValidWallThickness(thickness))
                        {
                            var wall = CreateWallFromLines(line1, line2, thickness, image.Scale);
                            walls.Add(wall);
                            usedLines.Add(i);
                            usedLines.Add(j);
                            break;
                        }
                    }
                }
            }

            // Also detect single thick lines as walls
            foreach (var line in image.DetectedLines)
            {
                if (line.Strength > _settings.ThickLineThreshold)
                {
                    var wall = CreateWallFromThickLine(line, image.Scale);
                    if (!WallOverlapsExisting(wall, walls))
                    {
                        walls.Add(wall);
                    }
                }
            }

            // Clean up and join walls
            walls = JoinCollinearWalls(walls);

            return walls;
        }

        private List<DetectedWall> DetectWallsFromSection(PreprocessedImage image)
        {
            var walls = new List<DetectedWall>();

            // In sections, walls appear as vertical filled regions
            // Detect by looking for vertical lines with fill between them

            return walls;
        }

        private bool LinesAreParallel(ImageDetectedLine a, ImageDetectedLine b)
        {
            double angleDiff = Math.Abs(a.Angle - b.Angle);
            return angleDiff < _settings.ParallelAngleTolerance ||
                   Math.Abs(angleDiff - 180) < _settings.ParallelAngleTolerance;
        }

        private double DistanceBetweenParallelLines(ImageDetectedLine a, ImageDetectedLine b)
        {
            // Calculate perpendicular distance
            var midA = new Point2D((a.Start.X + a.End.X) / 2, (a.Start.Y + a.End.Y) / 2);
            return PointToLineDistance(midA, b);
        }

        private double PointToLineDistance(Point2D point, ImageDetectedLine line)
        {
            double A = line.End.Y - line.Start.Y;
            double B = line.Start.X - line.End.X;
            double C = A * line.Start.X + B * line.Start.Y;
            return Math.Abs(A * point.X + B * point.Y - C) / Math.Sqrt(A * A + B * B);
        }

        private bool IsValidWallThickness(double thickness)
        {
            // Common wall thicknesses in mm
            var validThicknesses = new[] { 100, 150, 200, 250, 300, 350, 400 };
            return validThicknesses.Any(t => Math.Abs(thickness - t) < _settings.WallThicknessTolerance);
        }

        private DetectedWall CreateWallFromLines(ImageDetectedLine line1, ImageDetectedLine line2, double thickness, double scale)
        {
            var centerStart = new Point2D(
                (line1.Start.X + line2.Start.X) / 2 / scale,
                (line1.Start.Y + line2.Start.Y) / 2 / scale);
            var centerEnd = new Point2D(
                (line1.End.X + line2.End.X) / 2 / scale,
                (line1.End.Y + line2.End.Y) / 2 / scale);

            return new DetectedWall
            {
                CenterLine = new Line2D(centerStart, centerEnd),
                Thickness = thickness,
                WallType = ClassifyWallType(thickness),
                Confidence = (line1.Strength + line2.Strength) / 2.0 / 1000.0
            };
        }

        private DetectedWall CreateWallFromThickLine(ImageDetectedLine line, double scale)
        {
            return new DetectedWall
            {
                CenterLine = new Line2D(
                    new Point2D(line.Start.X / scale, line.Start.Y / scale),
                    new Point2D(line.End.X / scale, line.End.Y / scale)),
                Thickness = _settings.DefaultWallThickness,
                WallType = WallType.Interior,
                Confidence = line.Strength / 1000.0
            };
        }

        private WallType ClassifyWallType(double thickness)
        {
            if (thickness >= 250) return WallType.Exterior;
            if (thickness >= 200) return WallType.StructuralInterior;
            return WallType.Interior;
        }

        private bool WallOverlapsExisting(DetectedWall wall, List<DetectedWall> existing)
        {
            foreach (var other in existing)
            {
                if (WallsOverlap(wall, other))
                    return true;
            }
            return false;
        }

        private bool WallsOverlap(DetectedWall a, DetectedWall b)
        {
            // Simple overlap check
            return false;
        }

        private List<DetectedWall> JoinCollinearWalls(List<DetectedWall> walls)
        {
            var joined = new List<DetectedWall>();
            var used = new bool[walls.Count];

            for (int i = 0; i < walls.Count; i++)
            {
                if (used[i]) continue;

                var current = walls[i];
                used[i] = true;

                for (int j = i + 1; j < walls.Count; j++)
                {
                    if (used[j]) continue;

                    if (WallsAreCollinear(current, walls[j]) && WallsTouch(current, walls[j]))
                    {
                        current = MergeWalls(current, walls[j]);
                        used[j] = true;
                    }
                }

                joined.Add(current);
            }

            return joined;
        }

        private bool WallsAreCollinear(DetectedWall a, DetectedWall b)
        {
            var dir1 = new Point2D(
                a.CenterLine.End.X - a.CenterLine.Start.X,
                a.CenterLine.End.Y - a.CenterLine.Start.Y);
            var dir2 = new Point2D(
                b.CenterLine.End.X - b.CenterLine.Start.X,
                b.CenterLine.End.Y - b.CenterLine.Start.Y);

            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            return Math.Abs(cross) < _settings.CollinearTolerance;
        }

        private bool WallsTouch(DetectedWall a, DetectedWall b)
        {
            double tolerance = _settings.ConnectionTolerance;
            return Distance(a.CenterLine.End, b.CenterLine.Start) < tolerance ||
                   Distance(a.CenterLine.End, b.CenterLine.End) < tolerance ||
                   Distance(a.CenterLine.Start, b.CenterLine.Start) < tolerance ||
                   Distance(a.CenterLine.Start, b.CenterLine.End) < tolerance;
        }

        private double Distance(Point2D a, Point2D b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private DetectedWall MergeWalls(DetectedWall a, DetectedWall b)
        {
            // Find the extreme endpoints
            var allPoints = new[] { a.CenterLine.Start, a.CenterLine.End, b.CenterLine.Start, b.CenterLine.End };
            var dir = new Point2D(
                a.CenterLine.End.X - a.CenterLine.Start.X,
                a.CenterLine.End.Y - a.CenterLine.Start.Y);
            double length = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            dir = new Point2D(dir.X / length, dir.Y / length);

            var projections = allPoints.Select(p => dir.X * p.X + dir.Y * p.Y).ToArray();
            int minIdx = Array.IndexOf(projections, projections.Min());
            int maxIdx = Array.IndexOf(projections, projections.Max());

            return new DetectedWall
            {
                CenterLine = new Line2D(allPoints[minIdx], allPoints[maxIdx]),
                Thickness = (a.Thickness + b.Thickness) / 2,
                WallType = a.WallType,
                Confidence = (a.Confidence + b.Confidence) / 2
            };
        }
    }

    /// <summary>
    /// Detects doors and windows from processed image data
    /// </summary>
    internal class OpeningDetector
    {
        private readonly ImageToBIMSettings _settings;

        public OpeningDetector(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<DetectedOpening>> DetectOpeningsAsync(
            PreprocessedImage image,
            List<DetectedWall> walls,
            DrawingViewType viewType,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var openings = new List<DetectedOpening>();

                if (viewType == DrawingViewType.FloorPlan)
                {
                    // Detect door swings (arcs)
                    var doors = DetectDoorSwings(image, walls);
                    openings.AddRange(doors);

                    // Detect window indicators (parallel lines in wall)
                    var windows = DetectWindowIndicators(image, walls);
                    openings.AddRange(windows);

                    // Detect gaps in walls
                    var gaps = DetectWallGaps(walls);
                    foreach (var gap in gaps)
                    {
                        if (!OpeningExistsAt(gap, openings))
                        {
                            openings.Add(ClassifyGap(gap));
                        }
                    }
                }
                else
                {
                    // For sections/elevations, detect rectangular openings
                    openings = DetectRectangularOpenings(image);
                }

                return openings;
            }, cancellationToken);
        }

        private List<DetectedOpening> DetectDoorSwings(PreprocessedImage image, List<DetectedWall> walls)
        {
            var doors = new List<DetectedOpening>();

            // Look for arc patterns in the image
            // Doors typically show a quarter-circle swing pattern

            foreach (var wall in walls)
            {
                // Check along the wall for arc patterns
                var arcs = FindArcsNearWall(image, wall);

                foreach (var arc in arcs)
                {
                    var door = new DetectedOpening
                    {
                        OpeningType = OpeningType.Door,
                        Position = arc.Center,
                        Width = arc.Radius * 2,
                        Height = _settings.DefaultDoorHeight,
                        HostWall = wall,
                        SwingDirection = DetermineSwingDirection(arc),
                        Confidence = arc.Confidence
                    };

                    // Classify door type based on swing pattern
                    door.DoorType = ClassifyDoorType(arc);
                    doors.Add(door);
                }
            }

            return doors;
        }

        private List<ArcPattern> FindArcsNearWall(PreprocessedImage image, DetectedWall wall)
        {
            var arcs = new List<ArcPattern>();

            // Simplified arc detection - would use Hough transform for circles
            // and filter for quarter/semi arcs near wall lines

            return arcs;
        }

        private SwingDirection DetermineSwingDirection(ArcPattern arc)
        {
            // Based on arc quadrant
            return arc.StartAngle < 180 ? SwingDirection.Left : SwingDirection.Right;
        }

        private DoorType ClassifyDoorType(ArcPattern arc)
        {
            double sweepAngle = Math.Abs(arc.EndAngle - arc.StartAngle);

            if (sweepAngle > 170) return DoorType.DoubleDoor;
            if (sweepAngle < 95) return DoorType.SingleDoor;
            return DoorType.SingleDoor;
        }

        private List<DetectedOpening> DetectWindowIndicators(PreprocessedImage image, List<DetectedWall> walls)
        {
            var windows = new List<DetectedOpening>();

            // Windows typically shown as parallel lines within wall thickness
            // or as rectangular patterns

            return windows;
        }

        private List<WallGap> DetectWallGaps(List<DetectedWall> walls)
        {
            var gaps = new List<WallGap>();

            // Find wall endpoints that don't connect to other walls
            foreach (var wall in walls)
            {
                // Check for collinear walls with gaps between them
                var collinearWalls = walls.Where(w =>
                    w != wall && AreCollinear(wall.CenterLine, w.CenterLine)).ToList();

                foreach (var other in collinearWalls)
                {
                    var gap = GetGapBetweenWalls(wall, other);
                    if (gap != null && gap.Width > _settings.MinOpeningWidth && gap.Width < _settings.MaxOpeningWidth)
                    {
                        gap.HostWall = wall;
                        gaps.Add(gap);
                    }
                }
            }

            return gaps;
        }

        private bool AreCollinear(Line2D a, Line2D b)
        {
            // Check if lines are on the same infinite line
            return false; // Simplified
        }

        private WallGap GetGapBetweenWalls(DetectedWall a, DetectedWall b)
        {
            // Calculate gap between wall endpoints
            return null; // Simplified
        }

        private bool OpeningExistsAt(WallGap gap, List<DetectedOpening> openings)
        {
            return openings.Any(o => Distance(o.Position, gap.Center) < _settings.OpeningTolerance);
        }

        private double Distance(Point2D a, Point2D b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private DetectedOpening ClassifyGap(WallGap gap)
        {
            var opening = new DetectedOpening
            {
                Position = gap.Center,
                Width = gap.Width,
                HostWall = gap.HostWall,
                Confidence = 0.6
            };

            // Classify as door or window based on width
            if (gap.Width >= 700 && gap.Width <= 1200)
            {
                opening.OpeningType = OpeningType.Door;
                opening.Height = _settings.DefaultDoorHeight;
                opening.DoorType = DoorType.SingleDoor;
            }
            else if (gap.Width > 1200 && gap.Width <= 2400)
            {
                opening.OpeningType = OpeningType.Door;
                opening.Height = _settings.DefaultDoorHeight;
                opening.DoorType = DoorType.DoubleDoor;
            }
            else
            {
                opening.OpeningType = OpeningType.Window;
                opening.Height = _settings.DefaultWindowHeight;
            }

            return opening;
        }

        private List<DetectedOpening> DetectRectangularOpenings(PreprocessedImage image)
        {
            var openings = new List<DetectedOpening>();

            // For sections and elevations, look for rectangular voids
            // Typically shown with thicker lines or filled differently

            return openings;
        }
    }

    /// <summary>
    /// Detects rooms from wall boundaries
    /// </summary>
    internal class RoomDetector
    {
        private readonly ImageToBIMSettings _settings;

        public RoomDetector(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<ImageDetectedRoom>> DetectRoomsAsync(
            PreprocessedImage image,
            List<DetectedWall> walls,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var rooms = new List<ImageDetectedRoom>();

                // Create wall graph
                var wallGraph = BuildWallGraph(walls);

                // Find closed polygons (room boundaries)
                var polygons = FindClosedPolygons(wallGraph);

                foreach (var polygon in polygons)
                {
                    var room = new ImageDetectedRoom
                    {
                        BoundaryPoints = polygon,
                        Area = CalculatePolygonArea(polygon),
                        Centroid = CalculateCentroid(polygon),
                        Perimeter = CalculatePerimeter(polygon),
                        BoundingWalls = GetBoundingWalls(polygon, walls)
                    };

                    // Classify room type based on area and proportions
                    room.RoomType = ClassifyRoomType(room);
                    rooms.Add(room);
                }

                return rooms;
            }, cancellationToken);
        }

        private WallGraph BuildWallGraph(List<DetectedWall> walls)
        {
            var graph = new WallGraph();

            foreach (var wall in walls)
            {
                var startNode = graph.GetOrCreateNode(wall.CenterLine.Start);
                var endNode = graph.GetOrCreateNode(wall.CenterLine.End);
                graph.AddEdge(startNode, endNode, wall);
            }

            return graph;
        }

        private List<List<Point2D>> FindClosedPolygons(WallGraph graph)
        {
            var polygons = new List<List<Point2D>>();
            var visitedEdges = new HashSet<(int, int)>();

            foreach (var node in graph.Nodes)
            {
                var polygon = TracePolygon(graph, node, visitedEdges);
                if (polygon != null && polygon.Count >= 3)
                {
                    polygons.Add(polygon);
                }
            }

            return polygons;
        }

        private List<Point2D> TracePolygon(WallGraph graph, WallNode startNode, HashSet<(int, int)> visitedEdges)
        {
            var polygon = new List<Point2D> { startNode.Position };
            var current = startNode;
            WallNode previous = null;

            while (true)
            {
                var edges = graph.GetEdges(current)
                    .Where(e => !visitedEdges.Contains((Math.Min(e.Start.Id, e.End.Id), Math.Max(e.Start.Id, e.End.Id))))
                    .ToList();

                if (edges.Count == 0) return null;

                // Select next edge (rightmost turn for consistent direction)
                var nextEdge = SelectNextEdge(edges, current, previous);
                if (nextEdge == null) return null;

                var edgeKey = (Math.Min(nextEdge.Start.Id, nextEdge.End.Id), Math.Max(nextEdge.Start.Id, nextEdge.End.Id));
                visitedEdges.Add(edgeKey);

                previous = current;
                current = nextEdge.Start == current ? nextEdge.End : nextEdge.Start;

                if (current == startNode)
                {
                    return polygon;
                }

                polygon.Add(current.Position);

                if (polygon.Count > 100) return null; // Prevent infinite loops
            }
        }

        private WallEdge SelectNextEdge(List<WallEdge> edges, WallNode current, WallNode previous)
        {
            if (edges.Count == 1) return edges[0];
            if (previous == null) return edges[0];

            // Calculate incoming direction
            double inAngle = Math.Atan2(
                current.Position.Y - previous.Position.Y,
                current.Position.X - previous.Position.X);

            // Select edge with smallest right turn
            WallEdge best = null;
            double bestAngle = double.MaxValue;

            foreach (var edge in edges)
            {
                var next = edge.Start == current ? edge.End : edge.Start;
                double outAngle = Math.Atan2(
                    next.Position.Y - current.Position.Y,
                    next.Position.X - current.Position.X);

                double turn = outAngle - inAngle;
                if (turn < -Math.PI) turn += 2 * Math.PI;
                if (turn > Math.PI) turn -= 2 * Math.PI;

                if (turn < bestAngle)
                {
                    bestAngle = turn;
                    best = edge;
                }
            }

            return best;
        }

        private double CalculatePolygonArea(List<Point2D> polygon)
        {
            double area = 0;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                area += (polygon[j].X + polygon[i].X) * (polygon[j].Y - polygon[i].Y);
                j = i;
            }

            return Math.Abs(area / 2);
        }

        private Point2D CalculateCentroid(List<Point2D> polygon)
        {
            double cx = polygon.Average(p => p.X);
            double cy = polygon.Average(p => p.Y);
            return new Point2D(cx, cy);
        }

        private double CalculatePerimeter(List<Point2D> polygon)
        {
            double perimeter = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                int j = (i + 1) % polygon.Count;
                perimeter += Math.Sqrt(
                    Math.Pow(polygon[j].X - polygon[i].X, 2) +
                    Math.Pow(polygon[j].Y - polygon[i].Y, 2));
            }
            return perimeter;
        }

        private List<DetectedWall> GetBoundingWalls(List<Point2D> polygon, List<DetectedWall> walls)
        {
            // Find walls that form the polygon boundary
            return new List<DetectedWall>();
        }

        private RoomType ClassifyRoomType(ImageDetectedRoom room)
        {
            // Classify based on area
            if (room.Area < 4) return RoomType.Storage;
            if (room.Area < 8) return RoomType.Bathroom;
            if (room.Area < 15) return RoomType.Bedroom;
            if (room.Area < 25) return RoomType.LivingRoom;
            if (room.Area < 40) return RoomType.OpenPlan;
            return RoomType.Hall;
        }
    }

    /// <summary>
    /// Extracts text annotations from drawings
    /// </summary>
    internal class AnnotationExtractor
    {
        private readonly ImageToBIMSettings _settings;

        public AnnotationExtractor(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<ExtractedAnnotations> ExtractAnnotationsAsync(
            PreprocessedImage image,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var annotations = new ExtractedAnnotations();

                // OCR would be used here for actual text extraction
                // This is a placeholder implementation

                // Extract room labels
                annotations.RoomLabels = ExtractRoomLabels(image);

                // Extract dimensions
                annotations.Dimensions = ExtractDimensions(image);

                // Extract level markers
                annotations.LevelMarkers = ExtractLevelMarkers(image);

                // Extract grid labels
                annotations.GridLabels = ExtractGridLabels(image);

                return annotations;
            }, cancellationToken);
        }

        private List<RoomLabel> ExtractRoomLabels(PreprocessedImage image)
        {
            // Would use OCR to find text and classify as room labels
            return new List<RoomLabel>();
        }

        private List<ImageDimensionAnnotation> ExtractDimensions(PreprocessedImage image)
        {
            // Would use OCR to find dimension text
            return new List<ImageDimensionAnnotation>();
        }

        private List<LevelMarker> ExtractLevelMarkers(PreprocessedImage image)
        {
            // Would use OCR to find level annotations
            return new List<LevelMarker>();
        }

        private List<GridLabel> ExtractGridLabels(PreprocessedImage image)
        {
            // Would use OCR to find grid labels (A, B, C... or 1, 2, 3...)
            return new List<GridLabel>();
        }
    }

    #endregion

    #region BIM Model Builder

    /// <summary>
    /// Builds 3D BIM model from processed 2D views
    /// </summary>
    internal class BIMModelBuilder
    {
        private readonly ImageToBIMSettings _settings;

        public BIMModelBuilder(ImageToBIMSettings settings)
        {
            _settings = settings;
        }

        public async Task<BIMModel> BuildModelAsync(
            List<ProcessedView> views,
            ImageScaleInfo scale,
            ProcessingOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var model = new BIMModel();

                // Create levels from view information
                var levels = CreateLevels(views);
                model.Levels.AddRange(levels);

                // Process each view
                foreach (var view in views)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var level = levels.FirstOrDefault(l => l.Name == view.LevelName) ?? levels.FirstOrDefault();

                    switch (view.ViewType)
                    {
                        case DrawingViewType.FloorPlan:
                            ProcessPlanView(model, view, level, options);
                            break;

                        case DrawingViewType.Section:
                            ProcessSectionView(model, view, levels, options);
                            break;

                        case DrawingViewType.Elevation:
                            ProcessElevationView(model, view, levels, options);
                            break;
                    }
                }

                // Correlate elements across views
                if (views.Count > 1)
                {
                    CorrelateElementsAcrossViews(model, views);
                }

                // Post-process model
                PostProcessModel(model, options);

                return model;
            }, cancellationToken);
        }

        private List<BIMLevel> CreateLevels(List<ProcessedView> views)
        {
            var levels = new List<BIMLevel>();
            var levelNames = views
                .Where(v => !string.IsNullOrEmpty(v.LevelName))
                .Select(v => v.LevelName)
                .Distinct()
                .ToList();

            if (levelNames.Count == 0)
            {
                levelNames.Add("Level 1");
            }

            double elevation = 0;
            foreach (var name in levelNames.OrderBy(n => n))
            {
                var view = views.FirstOrDefault(v => v.LevelName == name);
                levels.Add(new BIMLevel
                {
                    Name = name,
                    Elevation = view?.Elevation ?? elevation
                });
                elevation += _settings.DefaultFloorHeight;
            }

            return levels;
        }

        private void ProcessPlanView(BIMModel model, ProcessedView view, BIMLevel level, ProcessingOptions options)
        {
            // Create walls
            foreach (var wall in view.DetectedWalls)
            {
                var bimWall = new BIMElement
                {
                    ElementType = BIMElementType.Wall,
                    Level = level,
                    Geometry = new WallGeometry
                    {
                        Start = new Point2D(wall.CenterLine.Start.X, wall.CenterLine.Start.Y),
                        End = new Point2D(wall.CenterLine.End.X, wall.CenterLine.End.Y),
                        Thickness = wall.Thickness,
                        Height = options.DefaultWallHeight,
                        BaseOffset = 0
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["WallType"] = wall.WallType.ToString(),
                        ["Thickness"] = wall.Thickness
                    }
                };
                model.Elements.Add(bimWall);
            }

            // Create openings
            foreach (var opening in view.DetectedOpenings)
            {
                var bimOpening = new BIMElement
                {
                    ElementType = opening.OpeningType == OpeningType.Door ? BIMElementType.Door : BIMElementType.Window,
                    Level = level,
                    Geometry = new OpeningGeometry
                    {
                        Position = opening.Position,
                        Width = opening.Width,
                        Height = opening.Height,
                        SillHeight = opening.OpeningType == OpeningType.Window ? options.DefaultWindowSillHeight : 0
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["OpeningType"] = opening.OpeningType.ToString(),
                        ["DoorType"] = opening.DoorType?.ToString()
                    }
                };
                model.Elements.Add(bimOpening);
            }

            // Create rooms
            foreach (var room in view.DetectedRooms ?? new List<ImageDetectedRoom>())
            {
                var bimRoom = new BIMElement
                {
                    ElementType = BIMElementType.Room,
                    Level = level,
                    Geometry = new RoomGeometry
                    {
                        Boundary = room.BoundaryPoints,
                        Centroid = room.Centroid
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = room.Name ?? "Room",
                        ["Area"] = room.Area,
                        ["RoomType"] = room.RoomType.ToString()
                    }
                };
                model.Elements.Add(bimRoom);
            }
        }

        private void ProcessSectionView(BIMModel model, ProcessedView view, List<BIMLevel> levels, ProcessingOptions options)
        {
            // Extract floor levels from section
            // Update wall heights based on section
            // Add floor slabs
        }

        private void ProcessElevationView(BIMModel model, ProcessedView view, List<BIMLevel> levels, ProcessingOptions options)
        {
            // Verify window/door positions
            // Extract building height
            // Add roof elements if visible
        }

        private void CorrelateElementsAcrossViews(BIMModel model, List<ProcessedView> views)
        {
            // Match walls between plan and section views
            // Verify opening positions
            // Resolve conflicts
        }

        private void PostProcessModel(BIMModel model, ProcessingOptions options)
        {
            // Join walls at intersections
            if (options.JoinWalls)
            {
                JoinWalls(model);
            }

            // Create floor slabs from room boundaries
            if (options.CreateFloors)
            {
                CreateFloorsFromRooms(model);
            }

            // Validate and fix geometry issues
            ValidateModel(model);
        }

        private void JoinWalls(BIMModel model)
        {
            var walls = model.Elements.Where(e => e.ElementType == BIMElementType.Wall).ToList();

            // Find wall intersections and update geometry
            foreach (var wall in walls)
            {
                var wallGeom = wall.Geometry as WallGeometry;
                if (wallGeom == null) continue;

                // Find walls that intersect at endpoints
                foreach (var other in walls)
                {
                    if (wall == other) continue;
                    var otherGeom = other.Geometry as WallGeometry;
                    if (otherGeom == null) continue;

                    // Check for intersection and extend/trim walls
                }
            }
        }

        private void CreateFloorsFromRooms(BIMModel model)
        {
            var rooms = model.Elements.Where(e => e.ElementType == BIMElementType.Room).ToList();

            foreach (var level in model.Levels)
            {
                var levelRooms = rooms.Where(r => r.Level == level).ToList();
                if (levelRooms.Count == 0) continue;

                // Create floor slab covering all rooms on this level
                var floor = new BIMElement
                {
                    ElementType = BIMElementType.Floor,
                    Level = level,
                    Geometry = new FloorGeometry
                    {
                        Boundary = GetCombinedRoomBoundary(levelRooms),
                        Thickness = _settings.DefaultFloorThickness
                    }
                };
                model.Elements.Add(floor);
            }
        }

        private List<Point2D> GetCombinedRoomBoundary(List<BIMElement> rooms)
        {
            // Combine room boundaries into single floor boundary
            return new List<Point2D>();
        }

        private void ValidateModel(BIMModel model)
        {
            // Check for overlapping walls
            // Verify opening positions within walls
            // Check for disconnected geometry
        }
    }

    #endregion

    #region Data Models

    // Settings
    public class ImageToBIMSettings
    {
        public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
        public double DefaultPixelsPerMM { get; set; } = 10.0;
        public double DefaultWallThickness { get; set; } = 200; // mm
        public double DefaultDoorHeight { get; set; } = 2100; // mm
        public double DefaultWindowHeight { get; set; } = 1200; // mm
        public double DefaultFloorHeight { get; set; } = 3000; // mm
        public double DefaultFloorThickness { get; set; } = 200; // mm
        public double MinOpeningWidth { get; set; } = 500; // mm
        public double MaxOpeningWidth { get; set; } = 3000; // mm
        public double ConnectionTolerance { get; set; } = 50; // mm
        public double OpeningTolerance { get; set; } = 100; // mm
        public double WallThicknessTolerance { get; set; } = 30; // mm
        public double ParallelAngleTolerance { get; set; } = 3; // degrees
        public double CollinearTolerance { get; set; } = 0.01;
        public bool ApplyNoiseReduction { get; set; } = true;
        public int BlurRadius { get; set; } = 2;
        public int AdaptiveBlockSize { get; set; } = 15;
        public int AdaptiveConstant { get; set; } = 5;
        public int HoughThreshold { get; set; } = 100;
        public double LineAngleTolerance { get; set; } = 5;
        public double LineMergeTolerance { get; set; } = 20;
        public int ThickLineThreshold { get; set; } = 500;
    }

    public class ProcessingOptions
    {
        public double DefaultWallHeight { get; set; } = 3000;
        public double DefaultWindowSillHeight { get; set; } = 900;
        public bool JoinWalls { get; set; } = true;
        public bool CreateFloors { get; set; } = true;
        public bool CreateRoofs { get; set; } = false;
    }

    public class ImageInput
    {
        public string Path { get; set; }
        public string LevelName { get; set; }
        public DrawingViewType? ViewType { get; set; }
        public double? Elevation { get; set; }
    }

    // Results
    public class ImageToBIMResult
    {
        public bool Success { get; set; }
        public string SourceImage { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public ImageInfo ImageInfo { get; set; }
        public ImageScaleInfo DetectedScale { get; set; }
        public List<ViewRegion> DetectedViews { get; set; } = new();
        public List<ProcessedView> ProcessedViews { get; set; } = new();
        public BIMModel BIMModel { get; set; }
        public ProcessingStatistics Statistics { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class ProcessingProgress
    {
        public int PercentComplete { get; set; }
        public string Status { get; set; }

        public ProcessingProgress(int percent, string status)
        {
            PercentComplete = percent;
            Status = status;
        }
    }

    public class ProcessingStatistics
    {
        public int TotalViewsProcessed { get; set; }
        public int WallsDetected { get; set; }
        public int DoorsDetected { get; set; }
        public int WindowsDetected { get; set; }
        public int RoomsDetected { get; set; }
        public int AnnotationsExtracted { get; set; }
        public int BIMElementsCreated { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
    }

    // Image Data
    public class ImageInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; }
        public int DPI { get; set; }
    }

    public class PreprocessedImage
    {
        public string SourcePath { get; set; }
        public ImageInfo Info { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Scale { get; set; }
        public byte[] RawPixels { get; set; }
        public byte[] GrayscalePixels { get; set; }
        public byte[] BinaryPixels { get; set; }
        public byte[] EdgePixels { get; set; }
        public List<ImageDetectedLine> DetectedLines { get; set; } = new();
    }

    public class ImageScaleInfo
    {
        public string DetectedScale { get; set; }
        public double PixelsPerUnit { get; set; }
        public MeasurementUnit Unit { get; set; }
        public double Confidence { get; set; }
    }

    public class ScalePattern
    {
        public string ScaleText { get; set; }
        public double Ratio { get; set; }
        public double Confidence { get; set; }
    }

    public class ScaleBarInfo
    {
        public double PixelsPerUnit { get; set; }
        public double Confidence { get; set; }
    }

    public class ScaleCalibration
    {
        public double PixelsPerUnit { get; set; }
        public double Confidence { get; set; }
    }

    // Geometry - Point2D moved to StingBIM.AI.Creation.Common.SharedCreationTypes

    public class Line2D
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }

        public Line2D() { }
        public Line2D(Point2D start, Point2D end) { Start = start; End = end; }
    }

    public class Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle() { }
        public Rectangle(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
    }

    public class ImageDetectedLine
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public double Angle { get; set; }
        public int Strength { get; set; }
    }

    public class ArcPattern
    {
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public double Confidence { get; set; }
    }

    // Detected Elements
    public class ViewRegion
    {
        public DrawingViewType ViewType { get; set; }
        public Rectangle Bounds { get; set; }
        public string LevelName { get; set; }
        public double Elevation { get; set; }
        public double Confidence { get; set; }
    }

    public class ProcessedView
    {
        public DrawingViewType ViewType { get; set; }
        public Rectangle Bounds { get; set; }
        public string LevelName { get; set; }
        public double Elevation { get; set; }
        public List<DetectedWall> DetectedWalls { get; set; } = new();
        public List<DetectedOpening> DetectedOpenings { get; set; } = new();
        public List<ImageDetectedRoom> DetectedRooms { get; set; } = new();
        public ExtractedAnnotations Annotations { get; set; }
    }

    public class DetectedWall
    {
        public Line2D CenterLine { get; set; }
        public double Thickness { get; set; }
        public WallType WallType { get; set; }
        public double Confidence { get; set; }
    }

    public class DetectedOpening
    {
        public OpeningType OpeningType { get; set; }
        public Point2D Position { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public DetectedWall HostWall { get; set; }
        public DoorType? DoorType { get; set; }
        public SwingDirection? SwingDirection { get; set; }
        public double Confidence { get; set; }
    }

    public class WallGap
    {
        public Point2D Center { get; set; }
        public double Width { get; set; }
        public DetectedWall HostWall { get; set; }
    }

    public class ImageDetectedRoom
    {
        public string Name { get; set; }
        public List<Point2D> BoundaryPoints { get; set; } = new();
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public Point2D Centroid { get; set; }
        public Point2D LabelPosition { get; set; }
        public RoomType RoomType { get; set; }
        public List<DetectedWall> BoundingWalls { get; set; } = new();
    }

    // Wall Graph
    internal class WallGraph
    {
        public List<WallNode> Nodes { get; } = new();
        public List<WallEdge> Edges { get; } = new();
        private int _nextId = 0;

        public WallNode GetOrCreateNode(Point2D position)
        {
            var existing = Nodes.FirstOrDefault(n =>
                Math.Abs(n.Position.X - position.X) < 10 &&
                Math.Abs(n.Position.Y - position.Y) < 10);

            if (existing != null) return existing;

            var node = new WallNode { Id = _nextId++, Position = position };
            Nodes.Add(node);
            return node;
        }

        public void AddEdge(WallNode start, WallNode end, DetectedWall wall)
        {
            Edges.Add(new WallEdge { Start = start, End = end, Wall = wall });
        }

        public List<WallEdge> GetEdges(WallNode node)
        {
            return Edges.Where(e => e.Start == node || e.End == node).ToList();
        }
    }

    internal class WallNode
    {
        public int Id { get; set; }
        public Point2D Position { get; set; }
    }

    internal class WallEdge
    {
        public WallNode Start { get; set; }
        public WallNode End { get; set; }
        public DetectedWall Wall { get; set; }
    }

    // Annotations
    public class ExtractedAnnotations
    {
        public List<RoomLabel> RoomLabels { get; set; } = new();
        public List<ImageDimensionAnnotation> Dimensions { get; set; } = new();
        public List<LevelMarker> LevelMarkers { get; set; } = new();
        public List<GridLabel> GridLabels { get; set; } = new();

        public List<object> AllAnnotations =>
            RoomLabels.Cast<object>()
                .Concat(Dimensions)
                .Concat(LevelMarkers)
                .Concat(GridLabels)
                .ToList();
    }

    public class RoomLabel
    {
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public double Confidence { get; set; }
    }

    public class ImageDimensionAnnotation
    {
        public string Text { get; set; }
        public double Value { get; set; }
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
    }

    public class LevelMarker
    {
        public string Text { get; set; }
        public double Elevation { get; set; }
        public Point2D Position { get; set; }
    }

    public class GridLabel
    {
        public string Text { get; set; }
        public Point2D Position { get; set; }
        public bool IsHorizontal { get; set; }
    }

    // BIM Model
    public class BIMModel
    {
        public List<BIMLevel> Levels { get; set; } = new();
        public List<BIMElement> Elements { get; set; } = new();
    }

    public class BIMLevel
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
    }

    public class BIMElement
    {
        public string Id { get; set; }
        public BIMElementType ElementType { get; set; }
        public BIMLevel Level { get; set; }
        public IBIMGeometry Geometry { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public interface IBIMGeometry
    {
        BoundingBox2D GetBounds();
    }

    public class BoundingBox2D
    {
        public Point2D Min { get; set; }
        public Point2D Max { get; set; }
    }

    public class WallGeometry : IBIMGeometry
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public double Thickness { get; set; }
        public double Height { get; set; }
        public double BaseOffset { get; set; }

        public BoundingBox2D GetBounds()
        {
            return new BoundingBox2D
            {
                Min = new Point2D(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y)),
                Max = new Point2D(Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y))
            };
        }
    }

    public class OpeningGeometry : IBIMGeometry
    {
        public Point2D Position { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double SillHeight { get; set; }

        public BoundingBox2D GetBounds()
        {
            return new BoundingBox2D
            {
                Min = new Point2D(Position.X - Width / 2, Position.Y - Width / 2),
                Max = new Point2D(Position.X + Width / 2, Position.Y + Width / 2)
            };
        }
    }

    public class RoomGeometry : IBIMGeometry
    {
        public List<Point2D> Boundary { get; set; }
        public Point2D Centroid { get; set; }

        public BoundingBox2D GetBounds()
        {
            if (Boundary == null || Boundary.Count == 0)
                return new BoundingBox2D { Min = new Point2D(0, 0), Max = new Point2D(0, 0) };

            return new BoundingBox2D
            {
                Min = new Point2D(Boundary.Min(p => p.X), Boundary.Min(p => p.Y)),
                Max = new Point2D(Boundary.Max(p => p.X), Boundary.Max(p => p.Y))
            };
        }
    }

    public class FloorGeometry : IBIMGeometry
    {
        public List<Point2D> Boundary { get; set; }
        public double Thickness { get; set; }

        public BoundingBox2D GetBounds()
        {
            if (Boundary == null || Boundary.Count == 0)
                return new BoundingBox2D { Min = new Point2D(0, 0), Max = new Point2D(0, 0) };

            return new BoundingBox2D
            {
                Min = new Point2D(Boundary.Min(p => p.X), Boundary.Min(p => p.Y)),
                Max = new Point2D(Boundary.Max(p => p.X), Boundary.Max(p => p.Y))
            };
        }
    }

    #endregion

    #region Enumerations

    // DrawingViewType and MeasurementUnit moved to StingBIM.AI.Creation.Common.SharedCreationTypes

    public enum WallType
    {
        Exterior,
        StructuralInterior,
        Interior,
        Partition,
        Curtain
    }

    public enum OpeningType
    {
        Door,
        Window,
        Opening
    }

    public enum DoorType
    {
        SingleDoor,
        DoubleDoor,
        SlidingDoor,
        FoldingDoor,
        RevolvingDoor
    }

    public enum SwingDirection
    {
        Left,
        Right,
        Both
    }

    public enum RoomType
    {
        LivingRoom,
        Bedroom,
        Bathroom,
        Kitchen,
        Dining,
        Office,
        Storage,
        Corridor,
        Hall,
        OpenPlan,
        Unknown
    }

    public enum BIMElementType
    {
        Wall,
        Door,
        Window,
        Floor,
        Ceiling,
        Roof,
        Room,
        Column,
        Beam,
        Stair,
        Railing
    }

    #endregion
}
