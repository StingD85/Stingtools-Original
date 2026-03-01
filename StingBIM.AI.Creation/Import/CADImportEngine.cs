// ===================================================================================
// StingBIM CAD Import Engine
// Full DWG/DXF interpretation with layer mapping, geometry extraction, and BIM conversion
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
    /// Comprehensive CAD import engine for converting DWG/DXF files to BIM models.
    /// Supports layer mapping, geometry extraction, block recognition, and intelligent
    /// conversion to Revit elements including walls, doors, windows, and annotations.
    /// </summary>
    public class CADImportEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly LayerMappingConfiguration _layerConfig;
        private readonly GeometryProcessor _geometryProcessor;
        private readonly BlockRecognizer _blockRecognizer;
        private readonly TextExtractor _textExtractor;
        private readonly ElementConverter _elementConverter;
        private readonly ImportSettings _settings;

        // Standard layer name patterns for auto-detection
        private static readonly Dictionary<string, RevitCategory> StandardLayerPatterns = new()
        {
            // Architectural layers
            { @"(?i)(wall|wand|mur|pared)", RevitCategory.Walls },
            { @"(?i)(door|tur|porte|puerta|dr)", RevitCategory.Doors },
            { @"(?i)(window|fenster|fenetre|ventana|wn)", RevitCategory.Windows },
            { @"(?i)(column|col|stutze|poteau|columna)", RevitCategory.Columns },
            { @"(?i)(beam|trager|poutre|viga)", RevitCategory.StructuralFraming },
            { @"(?i)(slab|floor|dalle|platte|losa)", RevitCategory.Floors },
            { @"(?i)(roof|dach|toit|techo)", RevitCategory.Roofs },
            { @"(?i)(stair|treppe|escalier|escalera)", RevitCategory.Stairs },
            { @"(?i)(ceiling|decke|plafond|techo)", RevitCategory.Ceilings },
            { @"(?i)(furniture|mobel|mobilier|mueble|furn)", RevitCategory.Furniture },
            { @"(?i)(equipment|equip|gerat)", RevitCategory.SpecialityEquipment },
            { @"(?i)(plumbing|plumb|sanitair)", RevitCategory.PlumbingFixtures },

            // Structural layers
            { @"(?i)(grid|raster|grille|rejilla)", RevitCategory.Grids },
            { @"(?i)(foundation|fundament|fondation|cimiento)", RevitCategory.StructuralFoundation },
            { @"(?i)(brace|strut|bracing)", RevitCategory.StructuralFraming },

            // MEP layers
            { @"(?i)(duct|kanal|conduit|conducto)", RevitCategory.DuctSystems },
            { @"(?i)(pipe|rohr|tuyau|tubo)", RevitCategory.PipeSystems },
            { @"(?i)(cable|kabel|tray)", RevitCategory.CableTray },
            { @"(?i)(light|lighting|leuchte|luminaire)", RevitCategory.LightingFixtures },
            { @"(?i)(elec|electrical|elektr)", RevitCategory.ElectricalEquipment },
            { @"(?i)(mech|mechanical|hvac)", RevitCategory.MechanicalEquipment },

            // Annotation layers
            { @"(?i)(dim|dimension|mass|cote)", RevitCategory.Dimensions },
            { @"(?i)(text|txt|note|anno)", RevitCategory.TextNotes },
            { @"(?i)(tag|label|etiquette)", RevitCategory.Tags },
            { @"(?i)(symbol|symb)", RevitCategory.GenericAnnotation },

            // Site layers
            { @"(?i)(topo|terrain|contour|site)", RevitCategory.Topography },
            { @"(?i)(parking|park)", RevitCategory.Parking },
            { @"(?i)(road|street|strasse|rue)", RevitCategory.Roads },
            { @"(?i)(landscape|landschaft|plant)", RevitCategory.Planting }
        };

        public CADImportEngine(ImportSettings settings = null)
        {
            _settings = settings ?? new ImportSettings();
            _layerConfig = new LayerMappingConfiguration();
            _geometryProcessor = new GeometryProcessor(_settings);
            _blockRecognizer = new BlockRecognizer();
            _textExtractor = new TextExtractor();
            _elementConverter = new ElementConverter(_settings);

            Logger.Info("CADImportEngine initialized with settings: {0}", JsonConvert.SerializeObject(_settings));
        }

        #region Main Import Methods

        /// <summary>
        /// Import a CAD file and convert to BIM elements
        /// </summary>
        public async Task<CADEngineImportResult> ImportFileAsync(
            string filePath,
            ImportOptions options = null,
            IProgress<CADImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new CADEngineImportResult
            {
                SourceFile = filePath,
                ImportStartTime = DateTime.Now
            };

            try
            {
                Logger.Info("Starting CAD import: {0}", filePath);
                options ??= new ImportOptions();

                // Validate file
                progress?.Report(new CADImportProgress(0, "Validating file..."));
                if (!ValidateFile(filePath, out string fileType, out string validationError))
                {
                    result.Success = false;
                    result.Errors.Add(validationError);
                    return result;
                }
                result.FileType = fileType;

                cancellationToken.ThrowIfCancellationRequested();

                // Parse CAD file
                progress?.Report(new CADImportProgress(10, "Parsing CAD file..."));
                var cadModel = await ParseCADFileAsync(filePath, fileType, cancellationToken);
                result.Statistics.TotalEntities = cadModel.Entities.Count;
                result.Statistics.TotalLayers = cadModel.Layers.Count;
                result.Statistics.TotalBlocks = cadModel.Blocks.Count;

                cancellationToken.ThrowIfCancellationRequested();

                // Map layers
                progress?.Report(new CADImportProgress(25, "Mapping layers to Revit categories..."));
                var layerMappings = MapLayers(cadModel.Layers, options);
                result.LayerMappings = layerMappings;

                cancellationToken.ThrowIfCancellationRequested();

                // Process geometry
                progress?.Report(new CADImportProgress(40, "Processing geometry..."));
                var processedGeometry = await _geometryProcessor.ProcessAsync(
                    cadModel.Entities,
                    layerMappings,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Recognize blocks (doors, windows, etc.)
                progress?.Report(new CADImportProgress(55, "Recognizing blocks..."));
                var recognizedBlocks = await _blockRecognizer.RecognizeAsync(
                    cadModel.Blocks,
                    cadModel.BlockReferences,
                    cancellationToken);
                result.Statistics.RecognizedDoors = recognizedBlocks.Count(b => b.ElementType == BlockElementType.Door);
                result.Statistics.RecognizedWindows = recognizedBlocks.Count(b => b.ElementType == BlockElementType.Window);

                cancellationToken.ThrowIfCancellationRequested();

                // Extract text and annotations
                progress?.Report(new CADImportProgress(65, "Extracting text and annotations..."));
                var extractedText = await _textExtractor.ExtractAsync(cadModel.TextEntities, cancellationToken);
                var extractedDimensions = await _textExtractor.ExtractDimensionsAsync(cadModel.Dimensions, cancellationToken);
                result.Statistics.ExtractedTexts = extractedText.Count;
                result.Statistics.ExtractedDimensions = extractedDimensions.Count;

                cancellationToken.ThrowIfCancellationRequested();

                // Convert to BIM elements
                progress?.Report(new CADImportProgress(75, "Converting to BIM elements..."));
                var conversionResult = await _elementConverter.ConvertAsync(
                    processedGeometry,
                    recognizedBlocks,
                    extractedText,
                    extractedDimensions,
                    options,
                    cancellationToken);

                result.ConvertedElements = conversionResult.Elements;
                result.Statistics.ConvertedWalls = conversionResult.Elements.Count(e => e.Category == RevitCategory.Walls);
                result.Statistics.ConvertedDoors = conversionResult.Elements.Count(e => e.Category == RevitCategory.Doors);
                result.Statistics.ConvertedWindows = conversionResult.Elements.Count(e => e.Category == RevitCategory.Windows);
                result.Statistics.ConvertedColumns = conversionResult.Elements.Count(e => e.Category == RevitCategory.Columns);

                // Post-processing
                progress?.Report(new CADImportProgress(90, "Post-processing..."));
                await PostProcessAsync(result, options, cancellationToken);

                progress?.Report(new CADImportProgress(100, "Import complete"));
                result.Success = true;
                result.ImportEndTime = DateTime.Now;

                Logger.Info("CAD import completed successfully: {0} elements created", result.ConvertedElements.Count);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Errors.Add("Import cancelled by user");
                Logger.Warn("CAD import cancelled: {0}", filePath);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Import failed: {ex.Message}");
                Logger.Error(ex, "CAD import failed: {0}", filePath);
            }

            return result;
        }

        /// <summary>
        /// Import multiple CAD files as a batch
        /// </summary>
        public async Task<List<CADEngineImportResult>> ImportBatchAsync(
            IEnumerable<string> filePaths,
            ImportOptions options = null,
            IProgress<BatchImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<CADEngineImportResult>();
            var files = filePaths.ToList();
            int completed = 0;

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileProgress = new Progress<CADImportProgress>(p =>
                {
                    progress?.Report(new BatchImportProgress
                    {
                        TotalFiles = files.Count,
                        CompletedFiles = completed,
                        CurrentFile = Path.GetFileName(filePath),
                        CurrentFileProgress = p.PercentComplete
                    });
                });

                var result = await ImportFileAsync(filePath, options, fileProgress, cancellationToken);
                results.Add(result);
                completed++;
            }

            return results;
        }

        #endregion

        #region File Validation and Parsing

        private bool ValidateFile(string filePath, out string fileType, out string error)
        {
            fileType = null;
            error = null;

            if (string.IsNullOrEmpty(filePath))
            {
                error = "File path is null or empty";
                return false;
            }

            if (!File.Exists(filePath))
            {
                error = $"File not found: {filePath}";
                return false;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".dwg":
                    fileType = "DWG";
                    break;
                case ".dxf":
                    fileType = "DXF";
                    break;
                default:
                    error = $"Unsupported file type: {extension}. Supported types: .dwg, .dxf";
                    return false;
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > _settings.MaxFileSizeBytes)
            {
                error = $"File size ({fileInfo.Length / 1024 / 1024}MB) exceeds maximum ({_settings.MaxFileSizeBytes / 1024 / 1024}MB)";
                return false;
            }

            return true;
        }

        private async Task<CADModel> ParseCADFileAsync(string filePath, string fileType, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var model = new CADModel();

                if (fileType == "DXF")
                {
                    ParseDXFFile(filePath, model);
                }
                else if (fileType == "DWG")
                {
                    ParseDWGFile(filePath, model);
                }

                return model;
            }, cancellationToken);
        }

        private void ParseDXFFile(string filePath, CADModel model)
        {
            Logger.Debug("Parsing DXF file: {0}", filePath);

            var lines = File.ReadAllLines(filePath);
            var parser = new DXFParser();

            int i = 0;
            while (i < lines.Length)
            {
                var groupCode = int.TryParse(lines[i].Trim(), out int code) ? code : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                switch (value.ToUpper())
                {
                    case "SECTION":
                        i = parser.ParseSection(lines, i, model);
                        break;
                    default:
                        i += 2;
                        break;
                }
            }

            Logger.Debug("DXF parsing complete: {0} layers, {1} entities, {2} blocks",
                model.Layers.Count, model.Entities.Count, model.Blocks.Count);
        }

        private void ParseDWGFile(string filePath, CADModel model)
        {
            Logger.Debug("Parsing DWG file: {0}", filePath);

            // DWG is a binary format - we use file signature detection and structured parsing
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            // Read DWG header
            var header = ReadDWGHeader(reader);
            model.Version = header.Version.ToString();

            // Parse sections based on DWG version
            var dwgParser = new DWGParser(header.Version);
            dwgParser.Parse(reader, model);

            Logger.Debug("DWG parsing complete: {0} layers, {1} entities, {2} blocks",
                model.Layers.Count, model.Entities.Count, model.Blocks.Count);
        }

        private DWGHeader ReadDWGHeader(BinaryReader reader)
        {
            var header = new DWGHeader();

            // Read version string (first 6 bytes)
            var versionBytes = reader.ReadBytes(6);
            var versionString = Encoding.ASCII.GetString(versionBytes);

            header.Version = versionString switch
            {
                "AC1032" => DWGVersion.AutoCAD2018,
                "AC1027" => DWGVersion.AutoCAD2013,
                "AC1024" => DWGVersion.AutoCAD2010,
                "AC1021" => DWGVersion.AutoCAD2007,
                "AC1018" => DWGVersion.AutoCAD2004,
                "AC1015" => DWGVersion.AutoCAD2000,
                "AC1014" => DWGVersion.AutoCAD14,
                _ => DWGVersion.Unknown
            };

            return header;
        }

        #endregion

        #region Layer Mapping

        private Dictionary<string, LayerMapping> MapLayers(
            List<CADFileLayer> layers,
            ImportOptions options)
        {
            var mappings = new Dictionary<string, LayerMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (var layer in layers)
            {
                var mapping = new LayerMapping
                {
                    CADLayerName = layer.Name,
                    CADLayerColor = layer.Color,
                    IsVisible = !layer.IsFrozen && layer.IsOn
                };

                // Check for explicit mapping first
                if (options.ExplicitLayerMappings?.TryGetValue(layer.Name, out var explicitCategory) == true)
                {
                    mapping.RevitCategory = explicitCategory;
                    mapping.MappingSource = MappingSource.Explicit;
                }
                // Then try pattern matching
                else if (TryMatchLayerPattern(layer.Name, out var matchedCategory))
                {
                    mapping.RevitCategory = matchedCategory;
                    mapping.MappingSource = MappingSource.PatternMatch;
                }
                // Use layer configuration
                else if (_layerConfig.TryGetMapping(layer.Name, out var configCategory))
                {
                    mapping.RevitCategory = configCategory;
                    mapping.MappingSource = MappingSource.Configuration;
                }
                // Default to generic model
                else
                {
                    mapping.RevitCategory = RevitCategory.GenericModel;
                    mapping.MappingSource = MappingSource.Default;
                }

                // Determine if layer should be imported based on filters
                mapping.ShouldImport = ShouldImportLayer(layer, mapping, options);

                mappings[layer.Name] = mapping;
                Logger.Debug("Layer mapped: {0} -> {1} ({2})", layer.Name, mapping.RevitCategory, mapping.MappingSource);
            }

            return mappings;
        }

        private bool TryMatchLayerPattern(string layerName, out RevitCategory category)
        {
            foreach (var pattern in StandardLayerPatterns)
            {
                if (Regex.IsMatch(layerName, pattern.Key))
                {
                    category = pattern.Value;
                    return true;
                }
            }

            category = RevitCategory.GenericModel;
            return false;
        }

        private bool ShouldImportLayer(CADFileLayer layer, LayerMapping mapping, ImportOptions options)
        {
            // Skip invisible layers unless explicitly included
            if (!mapping.IsVisible && !options.ImportInvisibleLayers)
                return false;

            // Check category filter
            if (options.CategoryFilter?.Count > 0 && !options.CategoryFilter.Contains(mapping.RevitCategory))
                return false;

            // Check layer name filter
            if (options.LayerNameFilter?.Count > 0)
            {
                bool matches = options.LayerNameFilter.Any(f =>
                    Regex.IsMatch(layer.Name, f, RegexOptions.IgnoreCase));
                if (!matches)
                    return false;
            }

            // Check exclude filter
            if (options.ExcludeLayerPatterns?.Count > 0)
            {
                bool excluded = options.ExcludeLayerPatterns.Any(f =>
                    Regex.IsMatch(layer.Name, f, RegexOptions.IgnoreCase));
                if (excluded)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get suggested layer mappings for a CAD file (preview without importing)
        /// </summary>
        public async Task<LayerMappingPreview> PreviewLayerMappingsAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var preview = new LayerMappingPreview { SourceFile = filePath };

            if (!ValidateFile(filePath, out string fileType, out string error))
            {
                preview.Error = error;
                return preview;
            }

            var cadModel = await ParseCADFileAsync(filePath, fileType, cancellationToken);
            var mappings = MapLayers(cadModel.Layers, new ImportOptions());

            preview.Mappings = mappings.Values.ToList();
            preview.Statistics = new LayerStatistics
            {
                TotalLayers = cadModel.Layers.Count,
                MappedByPattern = mappings.Values.Count(m => m.MappingSource == MappingSource.PatternMatch),
                MappedByConfig = mappings.Values.Count(m => m.MappingSource == MappingSource.Configuration),
                UnmappedLayers = mappings.Values.Count(m => m.MappingSource == MappingSource.Default)
            };

            return preview;
        }

        #endregion

        #region Post-Processing

        private async Task PostProcessAsync(CADEngineImportResult result, ImportOptions options, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Clean up duplicate geometry
                if (options.RemoveDuplicates)
                {
                    RemoveDuplicateElements(result);
                }

                // Join walls
                if (options.JoinWalls)
                {
                    JoinWallElements(result);
                }

                // Insert doors/windows into walls
                if (options.InsertOpeningsIntoWalls)
                {
                    InsertOpeningsIntoWalls(result);
                }

                // Validate geometry
                if (options.ValidateGeometry)
                {
                    ValidateConvertedGeometry(result);
                }

                // Generate warnings for unconverted elements
                GenerateWarnings(result);

            }, cancellationToken);
        }

        private void RemoveDuplicateElements(CADEngineImportResult result)
        {
            var uniqueElements = new List<ConvertedElement>();
            var seen = new HashSet<string>();

            foreach (var element in result.ConvertedElements)
            {
                var hash = element.GetGeometryHash();
                if (!seen.Contains(hash))
                {
                    seen.Add(hash);
                    uniqueElements.Add(element);
                }
                else
                {
                    result.Statistics.DuplicatesRemoved++;
                }
            }

            result.ConvertedElements = uniqueElements;
        }

        private void JoinWallElements(CADEngineImportResult result)
        {
            var walls = result.ConvertedElements.Where(e => e.Category == RevitCategory.Walls).ToList();
            var joinedWalls = new List<ConvertedElement>();
            var processedIndices = new HashSet<int>();

            for (int i = 0; i < walls.Count; i++)
            {
                if (processedIndices.Contains(i)) continue;

                var currentWall = walls[i];
                var connectedWalls = new List<ConvertedElement> { currentWall };
                processedIndices.Add(i);

                // Find walls that can be joined
                for (int j = i + 1; j < walls.Count; j++)
                {
                    if (processedIndices.Contains(j)) continue;

                    var otherWall = walls[j];
                    if (CanJoinWalls(currentWall, otherWall))
                    {
                        connectedWalls.Add(otherWall);
                        processedIndices.Add(j);
                    }
                }

                // Merge collinear walls
                var mergedWall = MergeCollinearWalls(connectedWalls);
                joinedWalls.Add(mergedWall);
            }

            // Replace walls with joined walls
            result.ConvertedElements = result.ConvertedElements
                .Where(e => e.Category != RevitCategory.Walls)
                .Concat(joinedWalls)
                .ToList();

            result.Statistics.WallsJoined = walls.Count - joinedWalls.Count;
        }

        private bool CanJoinWalls(ConvertedElement wall1, ConvertedElement wall2)
        {
            // Check if walls are collinear and endpoints touch
            var line1 = wall1.Geometry as LineGeometry;
            var line2 = wall2.Geometry as LineGeometry;

            if (line1 == null || line2 == null) return false;

            // Check collinearity
            var dir1 = (line1.EndPoint - line1.StartPoint).Normalize();
            var dir2 = (line2.EndPoint - line2.StartPoint).Normalize();

            if (Math.Abs(Vector3D.Dot(dir1, dir2)) < 0.999) return false;

            // Check if endpoints touch
            var tolerance = _settings.JoinTolerance;
            return line1.EndPoint.DistanceTo(line2.StartPoint) < tolerance ||
                   line1.EndPoint.DistanceTo(line2.EndPoint) < tolerance ||
                   line1.StartPoint.DistanceTo(line2.StartPoint) < tolerance ||
                   line1.StartPoint.DistanceTo(line2.EndPoint) < tolerance;
        }

        private ConvertedElement MergeCollinearWalls(List<ConvertedElement> walls)
        {
            if (walls.Count == 1) return walls[0];

            // Find the extreme endpoints
            var allPoints = new List<Point3D>();
            foreach (var wall in walls)
            {
                if (wall.Geometry is LineGeometry line)
                {
                    allPoints.Add(line.StartPoint);
                    allPoints.Add(line.EndPoint);
                }
            }

            // Get direction from first wall
            var firstLine = walls[0].Geometry as LineGeometry;
            var direction = (firstLine.EndPoint - firstLine.StartPoint).Normalize();

            // Project all points onto the line and find extremes
            var projections = allPoints.Select(p => Vector3D.Dot(p.ToVector(), direction)).ToList();
            var minIdx = projections.IndexOf(projections.Min());
            var maxIdx = projections.IndexOf(projections.Max());

            var mergedWall = walls[0].Clone();
            mergedWall.Geometry = new LineGeometry
            {
                StartPoint = allPoints[minIdx],
                EndPoint = allPoints[maxIdx]
            };

            return mergedWall;
        }

        private void InsertOpeningsIntoWalls(CADEngineImportResult result)
        {
            var walls = result.ConvertedElements.Where(e => e.Category == RevitCategory.Walls).ToList();
            var openings = result.ConvertedElements
                .Where(e => e.Category == RevitCategory.Doors || e.Category == RevitCategory.Windows)
                .ToList();

            foreach (var opening in openings)
            {
                var hostWall = FindHostWall(opening, walls);
                if (hostWall != null)
                {
                    opening.HostElementId = hostWall.Id;
                    result.Statistics.OpeningsInserted++;
                }
            }
        }

        private ConvertedElement FindHostWall(ConvertedElement opening, List<ConvertedElement> walls)
        {
            var openingCenter = opening.Geometry.GetCenter();
            var tolerance = _settings.OpeningHostTolerance;

            foreach (var wall in walls)
            {
                if (wall.Geometry is LineGeometry wallLine)
                {
                    var distance = PointToLineDistance(openingCenter, wallLine);
                    if (distance < tolerance)
                    {
                        return wall;
                    }
                }
            }

            return null;
        }

        private double PointToLineDistance(Point3D point, LineGeometry line)
        {
            var lineVector = line.EndPoint - line.StartPoint;
            var pointVector = point - line.StartPoint;
            var lineLength = lineVector.ToVector().Length;

            var t = Math.Max(0, Math.Min(1, Vector3D.Dot(pointVector.ToVector(), lineVector.ToVector()) / (lineLength * lineLength)));
            var projection = line.StartPoint + lineVector * t;

            return point.DistanceTo(projection);
        }

        private void ValidateConvertedGeometry(CADEngineImportResult result)
        {
            foreach (var element in result.ConvertedElements)
            {
                var issues = element.Geometry.Validate();
                if (issues.Any())
                {
                    result.Warnings.AddRange(issues.Select(i =>
                        $"Element {element.Id} ({element.Category}): {i}"));
                }
            }
        }

        private void GenerateWarnings(CADEngineImportResult result)
        {
            // Warn about unmapped layers
            var unmappedLayers = result.LayerMappings.Values
                .Where(m => m.MappingSource == MappingSource.Default && m.ShouldImport)
                .Select(m => m.CADLayerName)
                .ToList();

            if (unmappedLayers.Any())
            {
                result.Warnings.Add($"The following layers were imported as Generic Model: {string.Join(", ", unmappedLayers)}");
            }

            // Warn about small elements
            var smallElements = result.ConvertedElements
                .Where(e => e.Geometry.GetBoundingBox().Volume < _settings.MinElementVolume)
                .Count();

            if (smallElements > 0)
            {
                result.Warnings.Add($"{smallElements} elements may be too small to be useful (< {_settings.MinElementVolume} cubic units)");
            }
        }

        #endregion
    }

    #region Supporting Classes - Parsers

    /// <summary>
    /// DXF file format parser
    /// </summary>
    internal class DXFParser
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public int ParseSection(string[] lines, int startIndex, CADModel model)
        {
            int i = startIndex + 2; // Skip SECTION marker

            // Get section name
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 2)
                {
                    switch (value.ToUpper())
                    {
                        case "HEADER":
                            i = ParseHeaderSection(lines, i + 2, model);
                            break;
                        case "TABLES":
                            i = ParseTablesSection(lines, i + 2, model);
                            break;
                        case "BLOCKS":
                            i = ParseBlocksSection(lines, i + 2, model);
                            break;
                        case "ENTITIES":
                            i = ParseEntitiesSection(lines, i + 2, model);
                            break;
                        default:
                            i = SkipToEndSection(lines, i + 2);
                            break;
                    }
                    break;
                }
                i += 2;
            }

            return i;
        }

        private int ParseHeaderSection(string[] lines, int startIndex, CADModel model)
        {
            int i = startIndex;
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDSEC")
                    return i + 2;

                // Parse header variables
                if (code == 9)
                {
                    var varName = value;
                    i += 2;

                    code = int.TryParse(lines[i].Trim(), out c) ? c : -1;
                    value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                    switch (varName)
                    {
                        case "$ACADVER":
                            model.AcadVersion = value;
                            break;
                        case "$INSUNITS":
                            model.Units = ParseUnits(value);
                            break;
                    }
                }

                i += 2;
            }

            return i;
        }

        private int ParseTablesSection(string[] lines, int startIndex, CADModel model)
        {
            int i = startIndex;
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDSEC")
                    return i + 2;

                if (code == 0 && value == "TABLE")
                {
                    i += 2;
                    code = int.TryParse(lines[i].Trim(), out c) ? c : -1;
                    value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                    if (code == 2 && value == "LAYER")
                    {
                        i = ParseLayerTable(lines, i + 2, model);
                    }
                    else
                    {
                        i = SkipToEndTable(lines, i + 2);
                    }
                }
                else
                {
                    i += 2;
                }
            }

            return i;
        }

        private int ParseLayerTable(string[] lines, int startIndex, CADModel model)
        {
            int i = startIndex;
            CADFileLayer currentLayer = null;

            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDTAB")
                    return i + 2;

                if (code == 0 && value == "LAYER")
                {
                    if (currentLayer != null)
                        model.Layers.Add(currentLayer);
                    currentLayer = new CADFileLayer();
                }
                else if (currentLayer != null)
                {
                    switch (code)
                    {
                        case 2: currentLayer.Name = value; break;
                        case 62: currentLayer.Color = int.Parse(value); break;
                        case 6: currentLayer.LineType = value; break;
                        case 70:
                            var flags = int.Parse(value);
                            currentLayer.IsFrozen = (flags & 1) != 0;
                            currentLayer.IsLocked = (flags & 4) != 0;
                            break;
                    }
                }

                i += 2;
            }

            if (currentLayer != null)
                model.Layers.Add(currentLayer);

            return i;
        }

        private int ParseBlocksSection(string[] lines, int startIndex, CADModel model)
        {
            int i = startIndex;
            CADBlock currentBlock = null;

            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDSEC")
                {
                    if (currentBlock != null)
                        model.Blocks.Add(currentBlock);
                    return i + 2;
                }

                if (code == 0 && value == "BLOCK")
                {
                    if (currentBlock != null)
                        model.Blocks.Add(currentBlock);
                    currentBlock = new CADBlock();
                }
                else if (code == 0 && value == "ENDBLK")
                {
                    if (currentBlock != null)
                    {
                        model.Blocks.Add(currentBlock);
                        currentBlock = null;
                    }
                }
                else if (currentBlock != null)
                {
                    switch (code)
                    {
                        case 2: currentBlock.Name = value; break;
                        case 10: currentBlock.BasePoint = new Point3D(double.Parse(value), 0, 0); break;
                        case 20:
                            if (currentBlock.BasePoint != null)
                                currentBlock.BasePoint = new Point3D(currentBlock.BasePoint.X, double.Parse(value), currentBlock.BasePoint.Z);
                            break;
                        case 30:
                            if (currentBlock.BasePoint != null)
                                currentBlock.BasePoint = new Point3D(currentBlock.BasePoint.X, currentBlock.BasePoint.Y, double.Parse(value));
                            break;
                    }

                    // Parse block entities
                    if (code == 0 && IsEntityType(value))
                    {
                        var entity = ParseEntity(lines, ref i, value);
                        if (entity != null)
                            currentBlock.Entities.Add(entity);
                        continue;
                    }
                }

                i += 2;
            }

            return i;
        }

        private int ParseEntitiesSection(string[] lines, int startIndex, CADModel model)
        {
            int i = startIndex;

            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDSEC")
                    return i + 2;

                if (code == 0 && IsEntityType(value))
                {
                    var entity = ParseEntity(lines, ref i, value);
                    if (entity != null)
                    {
                        model.Entities.Add(entity);

                        if (entity is CADTextEntity textEntity)
                            model.TextEntities.Add(textEntity);
                        else if (entity is CADDimensionEntity dimEntity)
                            model.Dimensions.Add(dimEntity);
                        else if (entity is CADBlockReference blockRef)
                            model.BlockReferences.Add(blockRef);
                    }
                }
                else
                {
                    i += 2;
                }
            }

            return i;
        }

        private bool IsEntityType(string value)
        {
            return value == "LINE" || value == "POLYLINE" || value == "LWPOLYLINE" ||
                   value == "CIRCLE" || value == "ARC" || value == "ELLIPSE" ||
                   value == "TEXT" || value == "MTEXT" || value == "DIMENSION" ||
                   value == "INSERT" || value == "POINT" || value == "SPLINE" ||
                   value == "HATCH" || value == "SOLID" || value == "3DFACE";
        }

        private CADFileEntity ParseEntity(string[] lines, ref int i, string entityType)
        {
            CADFileEntity entity = entityType switch
            {
                "LINE" => new CADLineEntity(),
                "POLYLINE" or "LWPOLYLINE" => new CADPolylineEntity(),
                "CIRCLE" => new CADCircleEntity(),
                "ARC" => new CADArcEntity(),
                "ELLIPSE" => new CADEllipseEntity(),
                "TEXT" or "MTEXT" => new CADTextEntity(),
                "DIMENSION" => new CADDimensionEntity(),
                "INSERT" => new CADBlockReference(),
                "POINT" => new CADPointEntity(),
                "SPLINE" => new CADSplineEntity(),
                "HATCH" => new CADHatchEntity(),
                "SOLID" or "3DFACE" => new CADSolidEntity(),
                _ => null
            };

            if (entity == null)
            {
                i = SkipToNextEntity(lines, i);
                return null;
            }

            i += 2;
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0)
                    break;

                ParseEntityProperty(entity, code, value);
                i += 2;
            }

            return entity;
        }

        private void ParseEntityProperty(CADFileEntity entity, int code, string value)
        {
            // Common properties
            switch (code)
            {
                case 8: entity.Layer = value; break;
                case 62: entity.Color = int.Parse(value); break;
                case 6: entity.LineType = value; break;
            }

            // Entity-specific properties
            switch (entity)
            {
                case CADLineEntity line:
                    ParseLineProperty(line, code, value);
                    break;
                case CADPolylineEntity polyline:
                    ParsePolylineProperty(polyline, code, value);
                    break;
                case CADCircleEntity circle:
                    ParseCircleProperty(circle, code, value);
                    break;
                case CADArcEntity arc:
                    ParseArcProperty(arc, code, value);
                    break;
                case CADTextEntity text:
                    ParseTextProperty(text, code, value);
                    break;
                case CADDimensionEntity dim:
                    ParseDimensionProperty(dim, code, value);
                    break;
                case CADBlockReference blockRef:
                    ParseBlockRefProperty(blockRef, code, value);
                    break;
            }
        }

        private void ParseLineProperty(CADLineEntity line, int code, string value)
        {
            switch (code)
            {
                case 10: line.StartPoint = new Point3D(double.Parse(value), line.StartPoint?.Y ?? 0, line.StartPoint?.Z ?? 0); break;
                case 20: line.StartPoint = new Point3D(line.StartPoint?.X ?? 0, double.Parse(value), line.StartPoint?.Z ?? 0); break;
                case 30: line.StartPoint = new Point3D(line.StartPoint?.X ?? 0, line.StartPoint?.Y ?? 0, double.Parse(value)); break;
                case 11: line.EndPoint = new Point3D(double.Parse(value), line.EndPoint?.Y ?? 0, line.EndPoint?.Z ?? 0); break;
                case 21: line.EndPoint = new Point3D(line.EndPoint?.X ?? 0, double.Parse(value), line.EndPoint?.Z ?? 0); break;
                case 31: line.EndPoint = new Point3D(line.EndPoint?.X ?? 0, line.EndPoint?.Y ?? 0, double.Parse(value)); break;
            }
        }

        private void ParsePolylineProperty(CADPolylineEntity polyline, int code, string value)
        {
            switch (code)
            {
                case 10:
                    polyline.Vertices.Add(new Point3D(double.Parse(value), 0, 0));
                    break;
                case 20:
                    if (polyline.Vertices.Count > 0)
                    {
                        var last = polyline.Vertices[^1];
                        polyline.Vertices[^1] = new Point3D(last.X, double.Parse(value), last.Z);
                    }
                    break;
                case 30:
                    if (polyline.Vertices.Count > 0)
                    {
                        var last = polyline.Vertices[^1];
                        polyline.Vertices[^1] = new Point3D(last.X, last.Y, double.Parse(value));
                    }
                    break;
                case 70:
                    polyline.IsClosed = (int.Parse(value) & 1) != 0;
                    break;
                case 42:
                    if (polyline.Vertices.Count > 0)
                        polyline.Bulges.Add(double.Parse(value));
                    break;
            }
        }

        private void ParseCircleProperty(CADCircleEntity circle, int code, string value)
        {
            switch (code)
            {
                case 10: circle.Center = new Point3D(double.Parse(value), circle.Center?.Y ?? 0, circle.Center?.Z ?? 0); break;
                case 20: circle.Center = new Point3D(circle.Center?.X ?? 0, double.Parse(value), circle.Center?.Z ?? 0); break;
                case 30: circle.Center = new Point3D(circle.Center?.X ?? 0, circle.Center?.Y ?? 0, double.Parse(value)); break;
                case 40: circle.Radius = double.Parse(value); break;
            }
        }

        private void ParseArcProperty(CADArcEntity arc, int code, string value)
        {
            switch (code)
            {
                case 10: arc.Center = new Point3D(double.Parse(value), arc.Center?.Y ?? 0, arc.Center?.Z ?? 0); break;
                case 20: arc.Center = new Point3D(arc.Center?.X ?? 0, double.Parse(value), arc.Center?.Z ?? 0); break;
                case 30: arc.Center = new Point3D(arc.Center?.X ?? 0, arc.Center?.Y ?? 0, double.Parse(value)); break;
                case 40: arc.Radius = double.Parse(value); break;
                case 50: arc.StartAngle = double.Parse(value); break;
                case 51: arc.EndAngle = double.Parse(value); break;
            }
        }

        private void ParseTextProperty(CADTextEntity text, int code, string value)
        {
            switch (code)
            {
                case 1: text.Content = value; break;
                case 10: text.Position = new Point3D(double.Parse(value), text.Position?.Y ?? 0, text.Position?.Z ?? 0); break;
                case 20: text.Position = new Point3D(text.Position?.X ?? 0, double.Parse(value), text.Position?.Z ?? 0); break;
                case 30: text.Position = new Point3D(text.Position?.X ?? 0, text.Position?.Y ?? 0, double.Parse(value)); break;
                case 40: text.Height = double.Parse(value); break;
                case 50: text.Rotation = double.Parse(value); break;
                case 7: text.Style = value; break;
            }
        }

        private void ParseDimensionProperty(CADDimensionEntity dim, int code, string value)
        {
            switch (code)
            {
                case 1: dim.Text = value; break;
                case 10: dim.DefinitionPoint = new Point3D(double.Parse(value), dim.DefinitionPoint?.Y ?? 0, dim.DefinitionPoint?.Z ?? 0); break;
                case 20: dim.DefinitionPoint = new Point3D(dim.DefinitionPoint?.X ?? 0, double.Parse(value), dim.DefinitionPoint?.Z ?? 0); break;
                case 13: dim.ExtLine1Start = new Point3D(double.Parse(value), dim.ExtLine1Start?.Y ?? 0, dim.ExtLine1Start?.Z ?? 0); break;
                case 23: dim.ExtLine1Start = new Point3D(dim.ExtLine1Start?.X ?? 0, double.Parse(value), dim.ExtLine1Start?.Z ?? 0); break;
                case 14: dim.ExtLine2Start = new Point3D(double.Parse(value), dim.ExtLine2Start?.Y ?? 0, dim.ExtLine2Start?.Z ?? 0); break;
                case 24: dim.ExtLine2Start = new Point3D(dim.ExtLine2Start?.X ?? 0, double.Parse(value), dim.ExtLine2Start?.Z ?? 0); break;
                case 42: dim.Measurement = double.Parse(value); break;
                case 70: dim.DimensionType = (DimensionType)int.Parse(value); break;
            }
        }

        private void ParseBlockRefProperty(CADBlockReference blockRef, int code, string value)
        {
            switch (code)
            {
                case 2: blockRef.BlockName = value; break;
                case 10: blockRef.InsertionPoint = new Point3D(double.Parse(value), blockRef.InsertionPoint?.Y ?? 0, blockRef.InsertionPoint?.Z ?? 0); break;
                case 20: blockRef.InsertionPoint = new Point3D(blockRef.InsertionPoint?.X ?? 0, double.Parse(value), blockRef.InsertionPoint?.Z ?? 0); break;
                case 30: blockRef.InsertionPoint = new Point3D(blockRef.InsertionPoint?.X ?? 0, blockRef.InsertionPoint?.Y ?? 0, double.Parse(value)); break;
                case 41: blockRef.ScaleX = double.Parse(value); break;
                case 42: blockRef.ScaleY = double.Parse(value); break;
                case 43: blockRef.ScaleZ = double.Parse(value); break;
                case 50: blockRef.Rotation = double.Parse(value); break;
            }
        }

        private int SkipToEndSection(string[] lines, int startIndex)
        {
            int i = startIndex;
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDSEC")
                    return i + 2;

                i += 2;
            }
            return i;
        }

        private int SkipToEndTable(string[] lines, int startIndex)
        {
            int i = startIndex;
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                if (code == 0 && value == "ENDTAB")
                    return i + 2;

                i += 2;
            }
            return i;
        }

        private int SkipToNextEntity(string[] lines, int startIndex)
        {
            int i = startIndex + 2;
            while (i < lines.Length)
            {
                var code = int.TryParse(lines[i].Trim(), out int c) ? c : -1;
                if (code == 0)
                    return i;
                i += 2;
            }
            return i;
        }

        private CADUnits ParseUnits(string value)
        {
            return int.Parse(value) switch
            {
                1 => CADUnits.Inches,
                2 => CADUnits.Feet,
                4 => CADUnits.Millimeters,
                5 => CADUnits.Centimeters,
                6 => CADUnits.Meters,
                _ => CADUnits.Unitless
            };
        }
    }

    /// <summary>
    /// DWG binary file format parser
    /// </summary>
    internal class DWGParser
    {
        private readonly DWGVersion _version;

        public DWGParser(DWGVersion version)
        {
            _version = version;
        }

        public void Parse(BinaryReader reader, CADModel model)
        {
            // DWG parsing is complex and version-specific
            // This is a simplified implementation that handles basic structure
            // For production use, consider using a dedicated DWG library

            // Skip to object map section based on version
            long objectMapOffset = FindObjectMapOffset(reader);
            if (objectMapOffset > 0)
            {
                reader.BaseStream.Position = objectMapOffset;
                ParseObjectMap(reader, model);
            }
        }

        private long FindObjectMapOffset(BinaryReader reader)
        {
            // Simplified - in reality this involves reading section locator records
            // Position varies by DWG version
            return -1; // Indicates we need to use fallback parsing
        }

        private void ParseObjectMap(BinaryReader reader, CADModel model)
        {
            // Parse object handles and locations
            // This would map handles to object definitions
        }
    }

    #endregion

    #region Supporting Classes - Geometry Processing

    /// <summary>
    /// Processes CAD geometry into Revit-compatible elements
    /// </summary>
    internal class GeometryProcessor
    {
        private readonly ImportSettings _settings;

        public GeometryProcessor(ImportSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<ProcessedGeometry>> ProcessAsync(
            List<CADFileEntity> entities,
            Dictionary<string, LayerMapping> layerMappings,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var results = new List<ProcessedGeometry>();

                foreach (var entity in entities)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!layerMappings.TryGetValue(entity.Layer ?? "0", out var mapping) || !mapping.ShouldImport)
                        continue;

                    var processed = ProcessEntity(entity, mapping);
                    if (processed != null)
                        results.Add(processed);
                }

                return results;
            }, cancellationToken);
        }

        private ProcessedGeometry ProcessEntity(CADFileEntity entity, LayerMapping mapping)
        {
            return entity switch
            {
                CADLineEntity line => ProcessLine(line, mapping),
                CADPolylineEntity polyline => ProcessPolyline(polyline, mapping),
                CADCircleEntity circle => ProcessCircle(circle, mapping),
                CADArcEntity arc => ProcessArc(arc, mapping),
                CADEllipseEntity ellipse => ProcessEllipse(ellipse, mapping),
                CADSolidEntity solid => ProcessSolid(solid, mapping),
                _ => null
            };
        }

        private ProcessedGeometry ProcessLine(CADLineEntity line, LayerMapping mapping)
        {
            if (line.StartPoint == null || line.EndPoint == null)
                return null;

            var length = line.StartPoint.DistanceTo(line.EndPoint);
            if (length < _settings.MinLineLength)
                return null;

            return new ProcessedGeometry
            {
                SourceEntity = line,
                TargetCategory = mapping.RevitCategory,
                Geometry = new LineGeometry
                {
                    StartPoint = ApplyUnitConversion(line.StartPoint),
                    EndPoint = ApplyUnitConversion(line.EndPoint)
                },
                GeometryType = GeometryType.Line
            };
        }

        private ProcessedGeometry ProcessPolyline(CADPolylineEntity polyline, LayerMapping mapping)
        {
            if (polyline.Vertices.Count < 2)
                return null;

            // Convert vertices with unit conversion
            var convertedVertices = polyline.Vertices.Select(ApplyUnitConversion).ToList();

            // Handle bulges (arcs)
            var segments = new List<IGeometrySegment>();
            for (int i = 0; i < convertedVertices.Count - 1; i++)
            {
                var bulge = i < polyline.Bulges.Count ? polyline.Bulges[i] : 0;
                if (Math.Abs(bulge) > 0.0001)
                {
                    // Create arc segment
                    segments.Add(CreateArcFromBulge(convertedVertices[i], convertedVertices[i + 1], bulge));
                }
                else
                {
                    // Create line segment
                    segments.Add(new LineSegment { Start = convertedVertices[i], End = convertedVertices[i + 1] });
                }
            }

            // Close polyline if needed
            if (polyline.IsClosed && convertedVertices.Count > 2)
            {
                var lastBulge = polyline.Bulges.Count >= convertedVertices.Count ? polyline.Bulges[^1] : 0;
                if (Math.Abs(lastBulge) > 0.0001)
                {
                    segments.Add(CreateArcFromBulge(convertedVertices[^1], convertedVertices[0], lastBulge));
                }
                else
                {
                    segments.Add(new LineSegment { Start = convertedVertices[^1], End = convertedVertices[0] });
                }
            }

            return new ProcessedGeometry
            {
                SourceEntity = polyline,
                TargetCategory = mapping.RevitCategory,
                Geometry = new PolylineGeometry
                {
                    Segments = segments,
                    IsClosed = polyline.IsClosed
                },
                GeometryType = polyline.IsClosed ? GeometryType.ClosedPolyline : GeometryType.OpenPolyline
            };
        }

        private ArcSegment CreateArcFromBulge(Point3D start, Point3D end, double bulge)
        {
            // Bulge = tan(arc_angle / 4)
            var angle = 4 * Math.Atan(bulge);
            var chord = start.DistanceTo(end);
            var radius = chord / (2 * Math.Sin(Math.Abs(angle) / 2));

            // Calculate center
            var midpoint = new Point3D((start.X + end.X) / 2, (start.Y + end.Y) / 2, (start.Z + end.Z) / 2);
            var chordDir = (end - start).Normalize();
            var perpDir = new Vector3D(-chordDir.Y, chordDir.X, 0); // 2D perpendicular
            if (bulge < 0) perpDir = new Vector3D(chordDir.Y, -chordDir.X, 0);

            var sagitta = Math.Abs(bulge) * chord / 2;
            var apothem = radius - sagitta;
            var center = midpoint + perpDir * (bulge < 0 ? -apothem : apothem);

            return new ArcSegment
            {
                Center = new Point3D(center.X, center.Y, center.Z),
                Radius = radius,
                StartPoint = start,
                EndPoint = end,
                IsClockwise = bulge < 0
            };
        }

        private ProcessedGeometry ProcessCircle(CADCircleEntity circle, LayerMapping mapping)
        {
            if (circle.Center == null || circle.Radius < _settings.MinRadius)
                return null;

            return new ProcessedGeometry
            {
                SourceEntity = circle,
                TargetCategory = mapping.RevitCategory,
                Geometry = new CircleGeometry
                {
                    Center = ApplyUnitConversion(circle.Center),
                    Radius = ApplyUnitConversion(circle.Radius)
                },
                GeometryType = GeometryType.Circle
            };
        }

        private ProcessedGeometry ProcessArc(CADArcEntity arc, LayerMapping mapping)
        {
            if (arc.Center == null || arc.Radius < _settings.MinRadius)
                return null;

            return new ProcessedGeometry
            {
                SourceEntity = arc,
                TargetCategory = mapping.RevitCategory,
                Geometry = new ArcGeometry
                {
                    Center = ApplyUnitConversion(arc.Center),
                    Radius = ApplyUnitConversion(arc.Radius),
                    StartAngle = arc.StartAngle,
                    EndAngle = arc.EndAngle
                },
                GeometryType = GeometryType.Arc
            };
        }

        private ProcessedGeometry ProcessEllipse(CADEllipseEntity ellipse, LayerMapping mapping)
        {
            if (ellipse.Center == null)
                return null;

            return new ProcessedGeometry
            {
                SourceEntity = ellipse,
                TargetCategory = mapping.RevitCategory,
                Geometry = new EllipseGeometry
                {
                    Center = ApplyUnitConversion(ellipse.Center),
                    MajorAxis = ApplyUnitConversion(ellipse.MajorAxis),
                    MinorAxisRatio = ellipse.MinorAxisRatio,
                    StartAngle = ellipse.StartAngle,
                    EndAngle = ellipse.EndAngle
                },
                GeometryType = GeometryType.Ellipse
            };
        }

        private ProcessedGeometry ProcessSolid(CADSolidEntity solid, LayerMapping mapping)
        {
            if (solid.Vertices.Count < 3)
                return null;

            return new ProcessedGeometry
            {
                SourceEntity = solid,
                TargetCategory = mapping.RevitCategory,
                Geometry = new SolidGeometry
                {
                    Vertices = solid.Vertices.Select(ApplyUnitConversion).ToList()
                },
                GeometryType = GeometryType.Solid
            };
        }

        private Point3D ApplyUnitConversion(Point3D point)
        {
            var factor = _settings.UnitConversionFactor;
            return new Point3D(point.X * factor, point.Y * factor, point.Z * factor);
        }

        private double ApplyUnitConversion(double value)
        {
            return value * _settings.UnitConversionFactor;
        }

        private Vector3D ApplyUnitConversion(Vector3D vector)
        {
            var factor = _settings.UnitConversionFactor;
            return new Vector3D(vector.X * factor, vector.Y * factor, vector.Z * factor);
        }
    }

    #endregion

    #region Supporting Classes - Recognition

    /// <summary>
    /// Recognizes CAD blocks as BIM elements (doors, windows, equipment)
    /// </summary>
    internal class BlockRecognizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Pattern-based recognition for common block names
        private static readonly Dictionary<string, BlockElementType> BlockPatterns = new()
        {
            // Doors
            { @"(?i)(door|dr|tur|porte|puerta)[-_]?(\d+)?", BlockElementType.Door },
            { @"(?i)(swing|hinged|pivot)[-_]?(door|dr)?", BlockElementType.Door },
            { @"(?i)(sliding|pocket|barn)[-_]?(door|dr)?", BlockElementType.SlidingDoor },
            { @"(?i)(folding|bi[-_]?fold)[-_]?(door|dr)?", BlockElementType.FoldingDoor },
            { @"(?i)(double|dbl)[-_]?(door|dr)", BlockElementType.DoubleDoor },
            { @"(?i)(revolving|automatic|auto)[-_]?(door|dr)?", BlockElementType.RevolvingDoor },
            { @"(?i)(overhead|roll[-_]?up|sectional|garage)[-_]?(door|dr)?", BlockElementType.OverheadDoor },

            // Windows
            { @"(?i)(window|wn|win|fenster|fenetre|ventana)", BlockElementType.Window },
            { @"(?i)(casement|awning|hopper)", BlockElementType.CasementWindow },
            { @"(?i)(sliding|gliding)[-_]?(window|wn)?", BlockElementType.SlidingWindow },
            { @"(?i)(fixed|picture)[-_]?(window|wn|glazing)?", BlockElementType.FixedWindow },
            { @"(?i)(double[-_]?hung|single[-_]?hung)", BlockElementType.HungWindow },
            { @"(?i)(curtain[-_]?wall|cw)", BlockElementType.CurtainWall },
            { @"(?i)(skylight|roof[-_]?light)", BlockElementType.Skylight },

            // Furniture
            { @"(?i)(desk|workstation|table|bureau)", BlockElementType.Desk },
            { @"(?i)(chair|seat|stool|fauteuil)", BlockElementType.Chair },
            { @"(?i)(sofa|couch|settee)", BlockElementType.Sofa },
            { @"(?i)(bed|lit|cama)", BlockElementType.Bed },
            { @"(?i)(cabinet|cupboard|armoire|storage)", BlockElementType.Cabinet },
            { @"(?i)(shelf|shelving|etagere)", BlockElementType.Shelving },

            // Plumbing
            { @"(?i)(toilet|wc|water[-_]?closet|toilette)", BlockElementType.Toilet },
            { @"(?i)(sink|lavatory|basin|wash[-_]?basin)", BlockElementType.Sink },
            { @"(?i)(bath|tub|bathtub|baignoire)", BlockElementType.Bathtub },
            { @"(?i)(shower|douche)", BlockElementType.Shower },
            { @"(?i)(urinal|urinoir)", BlockElementType.Urinal },
            { @"(?i)(bidet)", BlockElementType.Bidet },

            // MEP Equipment
            { @"(?i)(hvac|ahu|air[-_]?handler)", BlockElementType.AirHandler },
            { @"(?i)(fcu|fan[-_]?coil)", BlockElementType.FanCoil },
            { @"(?i)(diffuser|grille|register)", BlockElementType.Diffuser },
            { @"(?i)(pump|pompe)", BlockElementType.Pump },
            { @"(?i)(boiler|chaudiere)", BlockElementType.Boiler },
            { @"(?i)(chiller)", BlockElementType.Chiller },
            { @"(?i)(water[-_]?heater|hwc)", BlockElementType.WaterHeater },

            // Electrical
            { @"(?i)(switch|interrupteur)", BlockElementType.Switch },
            { @"(?i)(outlet|receptacle|socket|prise)", BlockElementType.Outlet },
            { @"(?i)(light|luminaire|fixture)", BlockElementType.LightFixture },
            { @"(?i)(panel|distribution|db)", BlockElementType.ElectricalPanel },
            { @"(?i)(transformer|transfo)", BlockElementType.Transformer },

            // Structural
            { @"(?i)(column|col|poteau|pilier)", BlockElementType.Column },
            { @"(?i)(footing|foundation|semelle)", BlockElementType.Footing },

            // Site
            { @"(?i)(tree|arbre|baum)", BlockElementType.Tree },
            { @"(?i)(shrub|bush|buisson)", BlockElementType.Shrub },
            { @"(?i)(car|vehicle|auto)", BlockElementType.Vehicle },
            { @"(?i)(person|people|human|figure)", BlockElementType.EntouragePerson }
        };

        public async Task<List<RecognizedBlock>> RecognizeAsync(
            List<CADBlock> blocks,
            List<CADBlockReference> references,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var results = new List<RecognizedBlock>();
                var blockDict = blocks.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var reference in references)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!blockDict.TryGetValue(reference.BlockName, out var block))
                        continue;

                    var recognized = RecognizeBlock(block, reference);
                    if (recognized != null)
                        results.Add(recognized);
                }

                return results;
            }, cancellationToken);
        }

        private RecognizedBlock RecognizeBlock(CADBlock block, CADBlockReference reference)
        {
            // Try pattern-based recognition
            BlockElementType? elementType = null;
            foreach (var pattern in BlockPatterns)
            {
                if (Regex.IsMatch(block.Name, pattern.Key))
                {
                    elementType = pattern.Value;
                    break;
                }
            }

            // Try geometry-based recognition if pattern didn't match
            if (elementType == null)
            {
                elementType = RecognizeByGeometry(block);
            }

            if (elementType == null)
                return null;

            // Extract dimensions from block geometry
            var dimensions = ExtractDimensions(block, reference);

            return new RecognizedBlock
            {
                BlockName = block.Name,
                ElementType = elementType.Value,
                InsertionPoint = reference.InsertionPoint,
                Rotation = reference.Rotation,
                Scale = new Vector3D(reference.ScaleX, reference.ScaleY, reference.ScaleZ),
                Width = dimensions.Width,
                Height = dimensions.Height,
                Depth = dimensions.Depth,
                Layer = reference.Layer,
                Attributes = ExtractAttributes(reference)
            };
        }

        private BlockElementType? RecognizeByGeometry(CADBlock block)
        {
            var boundingBox = CalculateBoundingBox(block.Entities);
            if (boundingBox == null)
                return null;

            var width = boundingBox.Max.X - boundingBox.Min.X;
            var height = boundingBox.Max.Y - boundingBox.Min.Y;
            var aspectRatio = width / height;

            // Analyze entity composition
            var hasArc = block.Entities.Any(e => e is CADArcEntity);
            var hasCircle = block.Entities.Any(e => e is CADCircleEntity);
            var lineCount = block.Entities.Count(e => e is CADLineEntity);

            // Door recognition heuristics
            if (hasArc && lineCount >= 2 && lineCount <= 10 && aspectRatio > 0.3 && aspectRatio < 3)
            {
                return BlockElementType.Door;
            }

            // Window recognition heuristics (rectangular with divisions)
            if (!hasArc && lineCount >= 4 && aspectRatio > 0.5 && aspectRatio < 2)
            {
                return BlockElementType.Window;
            }

            // Circular fixtures (likely plumbing)
            if (hasCircle && lineCount < 5)
            {
                return BlockElementType.PlumbingFixture;
            }

            return null;
        }

        private (double Width, double Height, double Depth) ExtractDimensions(CADBlock block, CADBlockReference reference)
        {
            var boundingBox = CalculateBoundingBox(block.Entities);
            if (boundingBox == null)
                return (0, 0, 0);

            var width = (boundingBox.Max.X - boundingBox.Min.X) * reference.ScaleX;
            var height = (boundingBox.Max.Y - boundingBox.Min.Y) * reference.ScaleY;
            var depth = (boundingBox.Max.Z - boundingBox.Min.Z) * reference.ScaleZ;

            return (width, height, depth);
        }

        private BoundingBox CalculateBoundingBox(List<CADFileEntity> entities)
        {
            if (entities.Count == 0)
                return null;

            var min = new Point3D(double.MaxValue, double.MaxValue, double.MaxValue);
            var max = new Point3D(double.MinValue, double.MinValue, double.MinValue);

            foreach (var entity in entities)
            {
                var points = GetEntityPoints(entity);
                foreach (var point in points)
                {
                    min = new Point3D(
                        Math.Min(min.X, point.X),
                        Math.Min(min.Y, point.Y),
                        Math.Min(min.Z, point.Z));
                    max = new Point3D(
                        Math.Max(max.X, point.X),
                        Math.Max(max.Y, point.Y),
                        Math.Max(max.Z, point.Z));
                }
            }

            return new BoundingBox { Min = min, Max = max };
        }

        private List<Point3D> GetEntityPoints(CADFileEntity entity)
        {
            return entity switch
            {
                CADLineEntity line => new List<Point3D> { line.StartPoint, line.EndPoint }.Where(p => p != null).ToList(),
                CADPolylineEntity polyline => polyline.Vertices,
                CADCircleEntity circle => circle.Center != null ?
                    new List<Point3D> {
                        new Point3D(circle.Center.X - circle.Radius, circle.Center.Y, circle.Center.Z),
                        new Point3D(circle.Center.X + circle.Radius, circle.Center.Y, circle.Center.Z),
                        new Point3D(circle.Center.X, circle.Center.Y - circle.Radius, circle.Center.Z),
                        new Point3D(circle.Center.X, circle.Center.Y + circle.Radius, circle.Center.Z)
                    } : new List<Point3D>(),
                CADArcEntity arc => arc.Center != null ?
                    new List<Point3D> { arc.Center } : new List<Point3D>(),
                _ => new List<Point3D>()
            };
        }

        private Dictionary<string, string> ExtractAttributes(CADBlockReference reference)
        {
            // In a full implementation, this would parse ATTRIB entities
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Extracts text and dimension information from CAD files
    /// </summary>
    internal class TextExtractor
    {
        public async Task<List<ExtractedText>> ExtractAsync(
            List<CADTextEntity> textEntities,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var results = new List<ExtractedText>();

                foreach (var text in textEntities)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(text.Content))
                        continue;

                    results.Add(new ExtractedText
                    {
                        Content = text.Content,
                        Position = text.Position,
                        Height = text.Height,
                        Rotation = text.Rotation,
                        Style = text.Style,
                        Layer = text.Layer,
                        TextType = ClassifyText(text.Content)
                    });
                }

                return results;
            }, cancellationToken);
        }

        public async Task<List<ExtractedDimension>> ExtractDimensionsAsync(
            List<CADDimensionEntity> dimensions,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var results = new List<ExtractedDimension>();

                foreach (var dim in dimensions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    results.Add(new ExtractedDimension
                    {
                        Value = dim.Measurement,
                        Text = dim.Text,
                        DimensionType = dim.DimensionType,
                        DefinitionPoint = dim.DefinitionPoint,
                        ExtLine1Start = dim.ExtLine1Start,
                        ExtLine2Start = dim.ExtLine2Start,
                        Layer = dim.Layer
                    });
                }

                return results;
            }, cancellationToken);
        }

        private CADTextType ClassifyText(string content)
        {
            // Room/space labels
            if (Regex.IsMatch(content, @"(?i)(room|space|area|zone)\s*\d*|bedroom|bathroom|kitchen|living|office|storage", RegexOptions.IgnoreCase))
                return CADTextType.RoomLabel;

            // Grid labels (single letters or numbers)
            if (Regex.IsMatch(content, @"^[A-Z]$|^\d{1,2}$"))
                return CADTextType.GridLabel;

            // Level labels
            if (Regex.IsMatch(content, @"(?i)(level|floor|storey|ground|basement|roof)", RegexOptions.IgnoreCase))
                return CADTextType.LevelLabel;

            // Dimension text
            if (Regex.IsMatch(content, @"^\d+([.,]\d+)?\s*(mm|m|cm|ft|in|'|"")?$"))
                return CADTextType.DimensionText;

            // Note/annotation
            return CADTextType.Annotation;
        }
    }

    #endregion

    #region Supporting Classes - Element Conversion

    /// <summary>
    /// Converts processed CAD geometry to Revit-compatible BIM elements
    /// </summary>
    internal class ElementConverter
    {
        private readonly ImportSettings _settings;
        private int _nextElementId = 1;

        public ElementConverter(ImportSettings settings)
        {
            _settings = settings;
        }

        public async Task<ElementConversionResult> ConvertAsync(
            List<ProcessedGeometry> geometry,
            List<RecognizedBlock> blocks,
            List<ExtractedText> texts,
            List<ExtractedDimension> dimensions,
            ImportOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var result = new ElementConversionResult();

                // Convert geometry to elements
                foreach (var geom in geometry)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var element = ConvertGeometry(geom, options);
                    if (element != null)
                        result.Elements.Add(element);
                }

                // Convert recognized blocks
                foreach (var block in blocks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var element = ConvertBlock(block, options);
                    if (element != null)
                        result.Elements.Add(element);
                }

                // Convert text (room tags, annotations)
                if (options.ImportText)
                {
                    foreach (var text in texts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var element = ConvertText(text);
                        if (element != null)
                            result.Elements.Add(element);
                    }
                }

                // Convert dimensions
                if (options.ImportDimensions)
                {
                    foreach (var dim in dimensions)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var element = ConvertDimension(dim);
                        if (element != null)
                            result.Elements.Add(element);
                    }
                }

                return result;
            }, cancellationToken);
        }

        private ConvertedElement ConvertGeometry(ProcessedGeometry geom, ImportOptions options)
        {
            var element = new ConvertedElement
            {
                Id = GenerateElementId(),
                Category = geom.TargetCategory,
                SourceLayer = geom.SourceEntity.Layer,
                Geometry = ConvertToElementGeometry(geom)
            };

            // Set element type based on category and geometry
            element.TypeName = DetermineTypeName(geom.TargetCategory, geom);

            // Set additional properties based on category
            switch (geom.TargetCategory)
            {
                case RevitCategory.Walls:
                    element.Parameters["Base Constraint"] = "Level 1";
                    element.Parameters["Top Constraint"] = "Level 2";
                    element.Parameters["Unconnected Height"] = options.DefaultWallHeight.ToString();
                    break;

                case RevitCategory.Columns:
                    element.Parameters["Base Level"] = "Level 1";
                    element.Parameters["Top Level"] = "Level 2";
                    break;

                case RevitCategory.Floors:
                case RevitCategory.Ceilings:
                    element.Parameters["Level"] = "Level 1";
                    break;
            }

            return element;
        }

        private ConvertedElement ConvertBlock(RecognizedBlock block, ImportOptions options)
        {
            var category = MapElementTypeToCategory(block.ElementType);

            var element = new ConvertedElement
            {
                Id = GenerateElementId(),
                Category = category,
                SourceLayer = block.Layer,
                SourceBlockName = block.BlockName,
                Geometry = new PointGeometry { Location = block.InsertionPoint }
            };

            // Set type name based on recognized type
            element.TypeName = DetermineBlockTypeName(block);

            // Set parameters based on element type
            element.Parameters["Width"] = block.Width.ToString();
            element.Parameters["Height"] = block.Height.ToString();
            if (block.Depth > 0)
                element.Parameters["Depth"] = block.Depth.ToString();
            element.Parameters["Rotation"] = block.Rotation.ToString();

            // Copy block attributes
            foreach (var attr in block.Attributes)
            {
                element.Parameters[attr.Key] = attr.Value;
            }

            return element;
        }

        private ConvertedElement ConvertText(ExtractedText text)
        {
            var category = text.TextType switch
            {
                CADTextType.RoomLabel => RevitCategory.Rooms,
                CADTextType.GridLabel => RevitCategory.Grids,
                CADTextType.Annotation => RevitCategory.TextNotes,
                _ => RevitCategory.TextNotes
            };

            return new ConvertedElement
            {
                Id = GenerateElementId(),
                Category = category,
                Geometry = new TextGeometry
                {
                    Position = text.Position,
                    Content = text.Content,
                    Height = text.Height,
                    Rotation = text.Rotation
                },
                Parameters = new Dictionary<string, string>
                {
                    ["Text"] = text.Content,
                    ["Text Size"] = text.Height.ToString()
                }
            };
        }

        private ConvertedElement ConvertDimension(ExtractedDimension dim)
        {
            return new ConvertedElement
            {
                Id = GenerateElementId(),
                Category = RevitCategory.Dimensions,
                Geometry = new DimensionGeometry
                {
                    Origin = dim.DefinitionPoint,
                    Direction = dim.ExtLine2Start - dim.ExtLine1Start,
                    Value = dim.Value
                },
                Parameters = new Dictionary<string, string>
                {
                    ["Value"] = dim.Value.ToString(),
                    ["Override Text"] = dim.Text
                }
            };
        }

        private IElementGeometry ConvertToElementGeometry(ProcessedGeometry geom)
        {
            return geom.Geometry switch
            {
                LineGeometry line => line,
                PolylineGeometry polyline => polyline,
                CircleGeometry circle => circle,
                ArcGeometry arc => arc,
                EllipseGeometry ellipse => ellipse,
                SolidGeometry solid => solid,
                _ => null
            };
        }

        private string DetermineTypeName(RevitCategory category, ProcessedGeometry geom)
        {
            return category switch
            {
                RevitCategory.Walls => "Generic - 200mm",
                RevitCategory.Columns => "Concrete-Square-Column",
                RevitCategory.StructuralFraming => "W Shapes-W10X33",
                RevitCategory.Floors => "Generic Floor - 200mm",
                RevitCategory.Ceilings => "Compound Ceiling - 12mm",
                RevitCategory.Roofs => "Generic - 400mm",
                _ => "Generic Model"
            };
        }

        private string DetermineBlockTypeName(RecognizedBlock block)
        {
            return block.ElementType switch
            {
                BlockElementType.Door => $"Single-Flush - {(int)block.Width}x{(int)block.Height}mm",
                BlockElementType.DoubleDoor => $"Double-Flush - {(int)block.Width}x{(int)block.Height}mm",
                BlockElementType.SlidingDoor => $"Sliding - {(int)block.Width}x{(int)block.Height}mm",
                BlockElementType.Window => $"Fixed - {(int)block.Width}x{(int)block.Height}mm",
                BlockElementType.CasementWindow => $"Casement - {(int)block.Width}x{(int)block.Height}mm",
                BlockElementType.Toilet => "Water Closet - Standard",
                BlockElementType.Sink => "Lavatory - Standard",
                BlockElementType.Desk => "Desk - Standard",
                BlockElementType.Chair => "Chair - Standard",
                _ => block.BlockName
            };
        }

        private RevitCategory MapElementTypeToCategory(BlockElementType elementType)
        {
            return elementType switch
            {
                BlockElementType.Door or
                BlockElementType.DoubleDoor or
                BlockElementType.SlidingDoor or
                BlockElementType.FoldingDoor or
                BlockElementType.RevolvingDoor or
                BlockElementType.OverheadDoor => RevitCategory.Doors,

                BlockElementType.Window or
                BlockElementType.CasementWindow or
                BlockElementType.SlidingWindow or
                BlockElementType.FixedWindow or
                BlockElementType.HungWindow or
                BlockElementType.CurtainWall or
                BlockElementType.Skylight => RevitCategory.Windows,

                BlockElementType.Desk or
                BlockElementType.Chair or
                BlockElementType.Sofa or
                BlockElementType.Bed or
                BlockElementType.Cabinet or
                BlockElementType.Shelving => RevitCategory.Furniture,

                BlockElementType.Toilet or
                BlockElementType.Sink or
                BlockElementType.Bathtub or
                BlockElementType.Shower or
                BlockElementType.Urinal or
                BlockElementType.Bidet or
                BlockElementType.PlumbingFixture => RevitCategory.PlumbingFixtures,

                BlockElementType.AirHandler or
                BlockElementType.FanCoil or
                BlockElementType.Pump or
                BlockElementType.Boiler or
                BlockElementType.Chiller or
                BlockElementType.WaterHeater => RevitCategory.MechanicalEquipment,

                BlockElementType.Diffuser => RevitCategory.AirTerminals,

                BlockElementType.Switch or
                BlockElementType.Outlet => RevitCategory.ElectricalFixtures,

                BlockElementType.LightFixture => RevitCategory.LightingFixtures,

                BlockElementType.ElectricalPanel or
                BlockElementType.Transformer => RevitCategory.ElectricalEquipment,

                BlockElementType.Column => RevitCategory.Columns,
                BlockElementType.Footing => RevitCategory.StructuralFoundation,

                BlockElementType.Tree or
                BlockElementType.Shrub => RevitCategory.Planting,

                BlockElementType.Vehicle or
                BlockElementType.EntouragePerson => RevitCategory.Entourage,

                _ => RevitCategory.GenericModel
            };
        }

        private string GenerateElementId()
        {
            return $"CAD_IMPORT_{_nextElementId++:D6}";
        }
    }

    #endregion

    #region Data Models

    // CAD Model
    public class CADModel
    {
        public string AcadVersion { get; set; }
        public string Version { get; set; }
        public CADUnits Units { get; set; } = CADUnits.Millimeters;
        public List<CADFileLayer> Layers { get; set; } = new();
        public List<CADBlock> Blocks { get; set; } = new();
        public List<CADFileEntity> Entities { get; set; } = new();
        public List<CADTextEntity> TextEntities { get; set; } = new();
        public List<CADDimensionEntity> Dimensions { get; set; } = new();
        public List<CADBlockReference> BlockReferences { get; set; } = new();
    }

    public class CADFileLayer
    {
        public string Name { get; set; }
        public int Color { get; set; }
        public string LineType { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public bool IsOn { get; set; } = true;
    }

    public class CADBlock
    {
        public string Name { get; set; }
        public Point3D BasePoint { get; set; }
        public List<CADFileEntity> Entities { get; set; } = new();
    }

    // CAD Entities
    public abstract class CADFileEntity
    {
        public string Layer { get; set; }
        public int Color { get; set; }
        public string LineType { get; set; }
    }

    public class CADLineEntity : CADFileEntity
    {
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
    }

    public class CADPolylineEntity : CADFileEntity
    {
        public List<Point3D> Vertices { get; set; } = new();
        public List<double> Bulges { get; set; } = new();
        public bool IsClosed { get; set; }
    }

    public class CADCircleEntity : CADFileEntity
    {
        public Point3D Center { get; set; }
        public double Radius { get; set; }
    }

    public class CADArcEntity : CADFileEntity
    {
        public Point3D Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
    }

    public class CADEllipseEntity : CADFileEntity
    {
        public Point3D Center { get; set; }
        public Vector3D MajorAxis { get; set; }
        public double MinorAxisRatio { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
    }

    public class CADTextEntity : CADFileEntity
    {
        public string Content { get; set; }
        public Point3D Position { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public string Style { get; set; }
    }

    public class CADDimensionEntity : CADFileEntity
    {
        public string Text { get; set; }
        public Point3D DefinitionPoint { get; set; }
        public Point3D ExtLine1Start { get; set; }
        public Point3D ExtLine2Start { get; set; }
        public double Measurement { get; set; }
        public DimensionType DimensionType { get; set; }
    }

    public class CADBlockReference : CADFileEntity
    {
        public string BlockName { get; set; }
        public Point3D InsertionPoint { get; set; }
        public double ScaleX { get; set; } = 1;
        public double ScaleY { get; set; } = 1;
        public double ScaleZ { get; set; } = 1;
        public double Rotation { get; set; }
    }

    public class CADPointEntity : CADFileEntity
    {
        public Point3D Location { get; set; }
    }

    public class CADSplineEntity : CADFileEntity
    {
        public List<Point3D> ControlPoints { get; set; } = new();
        public int Degree { get; set; }
    }

    public class CADHatchEntity : CADFileEntity
    {
        public string PatternName { get; set; }
        public List<List<Point3D>> Boundaries { get; set; } = new();
    }

    public class CADSolidEntity : CADFileEntity
    {
        public List<Point3D> Vertices { get; set; } = new();
    }

    // Geometry types
    public interface IElementGeometry
    {
        Point3D GetCenter();
        BoundingBox GetBoundingBox();
        string GetGeometryHash();
        List<string> Validate();
    }

    public interface IGeometrySegment { }

    public class LineSegment : IGeometrySegment
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }
    }

    public class ArcSegment : IGeometrySegment
    {
        public Point3D Center { get; set; }
        public double Radius { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public bool IsClockwise { get; set; }
    }

    public class LineGeometry : IElementGeometry
    {
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }

        public Point3D GetCenter() => new Point3D(
            (StartPoint.X + EndPoint.X) / 2,
            (StartPoint.Y + EndPoint.Y) / 2,
            (StartPoint.Z + EndPoint.Z) / 2);

        public BoundingBox GetBoundingBox() => new BoundingBox
        {
            Min = new Point3D(Math.Min(StartPoint.X, EndPoint.X), Math.Min(StartPoint.Y, EndPoint.Y), Math.Min(StartPoint.Z, EndPoint.Z)),
            Max = new Point3D(Math.Max(StartPoint.X, EndPoint.X), Math.Max(StartPoint.Y, EndPoint.Y), Math.Max(StartPoint.Z, EndPoint.Z))
        };

        public string GetGeometryHash() => $"LINE_{StartPoint.X:F2}_{StartPoint.Y:F2}_{EndPoint.X:F2}_{EndPoint.Y:F2}";

        public List<string> Validate()
        {
            var issues = new List<string>();
            if (StartPoint.DistanceTo(EndPoint) < 0.001)
                issues.Add("Line has zero length");
            return issues;
        }
    }

    public class PolylineGeometry : IElementGeometry
    {
        public List<IGeometrySegment> Segments { get; set; } = new();
        public bool IsClosed { get; set; }

        public Point3D GetCenter()
        {
            var points = GetAllPoints();
            if (points.Count == 0) return new Point3D(0, 0, 0);
            return new Point3D(points.Average(p => p.X), points.Average(p => p.Y), points.Average(p => p.Z));
        }

        public BoundingBox GetBoundingBox()
        {
            var points = GetAllPoints();
            if (points.Count == 0) return new BoundingBox();
            return new BoundingBox
            {
                Min = new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
                Max = new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z))
            };
        }

        public string GetGeometryHash()
        {
            var points = GetAllPoints();
            return $"PLINE_{string.Join("_", points.Select(p => $"{p.X:F2}_{p.Y:F2}"))}";
        }

        public List<string> Validate()
        {
            var issues = new List<string>();
            if (Segments.Count < 1)
                issues.Add("Polyline has no segments");
            return issues;
        }

        private List<Point3D> GetAllPoints()
        {
            var points = new List<Point3D>();
            foreach (var seg in Segments)
            {
                if (seg is LineSegment line)
                {
                    if (points.Count == 0) points.Add(line.Start);
                    points.Add(line.End);
                }
                else if (seg is ArcSegment arc)
                {
                    if (points.Count == 0) points.Add(arc.StartPoint);
                    points.Add(arc.EndPoint);
                }
            }
            return points;
        }
    }

    public class CircleGeometry : IElementGeometry
    {
        public Point3D Center { get; set; }
        public double Radius { get; set; }

        public Point3D GetCenter() => Center;
        public BoundingBox GetBoundingBox() => new BoundingBox
        {
            Min = new Point3D(Center.X - Radius, Center.Y - Radius, Center.Z),
            Max = new Point3D(Center.X + Radius, Center.Y + Radius, Center.Z)
        };
        public string GetGeometryHash() => $"CIRCLE_{Center.X:F2}_{Center.Y:F2}_{Radius:F2}";
        public List<string> Validate()
        {
            var issues = new List<string>();
            if (Radius < 0.001) issues.Add("Circle has zero radius");
            return issues;
        }
    }

    public class ArcGeometry : IElementGeometry
    {
        public Point3D Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }

        public Point3D GetCenter() => Center;
        public BoundingBox GetBoundingBox() => new BoundingBox
        {
            Min = new Point3D(Center.X - Radius, Center.Y - Radius, Center.Z),
            Max = new Point3D(Center.X + Radius, Center.Y + Radius, Center.Z)
        };
        public string GetGeometryHash() => $"ARC_{Center.X:F2}_{Center.Y:F2}_{Radius:F2}_{StartAngle:F2}_{EndAngle:F2}";
        public List<string> Validate()
        {
            var issues = new List<string>();
            if (Radius < 0.001) issues.Add("Arc has zero radius");
            return issues;
        }
    }

    public class EllipseGeometry : IElementGeometry
    {
        public Point3D Center { get; set; }
        public Vector3D MajorAxis { get; set; }
        public double MinorAxisRatio { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }

        public Point3D GetCenter() => Center;
        public BoundingBox GetBoundingBox()
        {
            var majorLength = MajorAxis.Length;
            var minorLength = majorLength * MinorAxisRatio;
            return new BoundingBox
            {
                Min = new Point3D(Center.X - majorLength, Center.Y - minorLength, Center.Z),
                Max = new Point3D(Center.X + majorLength, Center.Y + minorLength, Center.Z)
            };
        }
        public string GetGeometryHash() => $"ELLIPSE_{Center.X:F2}_{Center.Y:F2}_{MajorAxis.Length:F2}_{MinorAxisRatio:F2}";
        public List<string> Validate() => new List<string>();
    }

    public class SolidGeometry : IElementGeometry
    {
        public List<Point3D> Vertices { get; set; } = new();

        public Point3D GetCenter()
        {
            if (Vertices.Count == 0) return new Point3D(0, 0, 0);
            return new Point3D(Vertices.Average(v => v.X), Vertices.Average(v => v.Y), Vertices.Average(v => v.Z));
        }

        public BoundingBox GetBoundingBox()
        {
            if (Vertices.Count == 0) return new BoundingBox();
            return new BoundingBox
            {
                Min = new Point3D(Vertices.Min(v => v.X), Vertices.Min(v => v.Y), Vertices.Min(v => v.Z)),
                Max = new Point3D(Vertices.Max(v => v.X), Vertices.Max(v => v.Y), Vertices.Max(v => v.Z))
            };
        }

        public string GetGeometryHash() => $"SOLID_{string.Join("_", Vertices.Select(v => $"{v.X:F2}_{v.Y:F2}"))}";
        public List<string> Validate()
        {
            var issues = new List<string>();
            if (Vertices.Count < 3) issues.Add("Solid has fewer than 3 vertices");
            return issues;
        }
    }

    public class PointGeometry : IElementGeometry
    {
        public Point3D Location { get; set; }

        public Point3D GetCenter() => Location;
        public BoundingBox GetBoundingBox() => new BoundingBox { Min = Location, Max = Location };
        public string GetGeometryHash() => $"POINT_{Location.X:F2}_{Location.Y:F2}_{Location.Z:F2}";
        public List<string> Validate() => new List<string>();
    }

    public class TextGeometry : IElementGeometry
    {
        public Point3D Position { get; set; }
        public string Content { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }

        public Point3D GetCenter() => Position;
        public BoundingBox GetBoundingBox()
        {
            var width = Content.Length * Height * 0.6;
            return new BoundingBox
            {
                Min = Position,
                Max = new Point3D(Position.X + width, Position.Y + Height, Position.Z)
            };
        }
        public string GetGeometryHash() => $"TEXT_{Position.X:F2}_{Position.Y:F2}_{Content}";
        public List<string> Validate() => new List<string>();
    }

    public class DimensionGeometry : IElementGeometry
    {
        public Point3D Origin { get; set; }
        public Point3D Direction { get; set; }
        public double Value { get; set; }

        public Point3D GetCenter() => Origin;
        public BoundingBox GetBoundingBox() => new BoundingBox { Min = Origin, Max = Direction };
        public string GetGeometryHash() => $"DIM_{Origin.X:F2}_{Origin.Y:F2}_{Value:F2}";
        public List<string> Validate() => new List<string>();
    }

    public class BoundingBox
    {
        public Point3D Min { get; set; } = new Point3D(0, 0, 0);
        public Point3D Max { get; set; } = new Point3D(0, 0, 0);
        public double Volume => (Max.X - Min.X) * (Max.Y - Min.Y) * (Max.Z - Min.Z);
    }

    // Point3D and Vector3D moved to StingBIM.AI.Creation.Common.SharedCreationTypes

    // Results and Options
    public class CADEngineImportResult
    {
        public bool Success { get; set; }
        public string SourceFile { get; set; }
        public string FileType { get; set; }
        public DateTime ImportStartTime { get; set; }
        public DateTime ImportEndTime { get; set; }
        public TimeSpan Duration => ImportEndTime - ImportStartTime;
        public Dictionary<string, LayerMapping> LayerMappings { get; set; } = new();
        public List<ConvertedElement> ConvertedElements { get; set; } = new();
        public ImportStatistics Statistics { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class ImportStatistics
    {
        public int TotalEntities { get; set; }
        public int TotalLayers { get; set; }
        public int TotalBlocks { get; set; }
        public int RecognizedDoors { get; set; }
        public int RecognizedWindows { get; set; }
        public int ExtractedTexts { get; set; }
        public int ExtractedDimensions { get; set; }
        public int ConvertedWalls { get; set; }
        public int ConvertedDoors { get; set; }
        public int ConvertedWindows { get; set; }
        public int ConvertedColumns { get; set; }
        public int DuplicatesRemoved { get; set; }
        public int WallsJoined { get; set; }
        public int OpeningsInserted { get; set; }
    }

    public class ImportOptions
    {
        public double DefaultWallHeight { get; set; } = 3000; // mm
        public bool ImportInvisibleLayers { get; set; } = false;
        public bool ImportText { get; set; } = true;
        public bool ImportDimensions { get; set; } = true;
        public bool RemoveDuplicates { get; set; } = true;
        public bool JoinWalls { get; set; } = true;
        public bool InsertOpeningsIntoWalls { get; set; } = true;
        public bool ValidateGeometry { get; set; } = true;
        public HashSet<RevitCategory> CategoryFilter { get; set; }
        public List<string> LayerNameFilter { get; set; }
        public List<string> ExcludeLayerPatterns { get; set; }
        public Dictionary<string, RevitCategory> ExplicitLayerMappings { get; set; }
    }

    public class ImportSettings
    {
        public double UnitConversionFactor { get; set; } = 1.0; // To mm
        public long MaxFileSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB
        public double MinLineLength { get; set; } = 1.0; // mm
        public double MinRadius { get; set; } = 0.5; // mm
        public double MinElementVolume { get; set; } = 1.0; // cubic mm
        public double JoinTolerance { get; set; } = 10.0; // mm
        public double OpeningHostTolerance { get; set; } = 150.0; // mm
    }

    public class CADImportProgress
    {
        public int PercentComplete { get; set; }
        public string Status { get; set; }

        public CADImportProgress(int percent, string status)
        {
            PercentComplete = percent;
            Status = status;
        }
    }

    public class BatchImportProgress
    {
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public string CurrentFile { get; set; }
        public int CurrentFileProgress { get; set; }
    }

    // Layer Mapping
    public class LayerMapping
    {
        public string CADLayerName { get; set; }
        public int CADLayerColor { get; set; }
        public RevitCategory RevitCategory { get; set; }
        public MappingSource MappingSource { get; set; }
        public bool IsVisible { get; set; }
        public bool ShouldImport { get; set; }
    }

    public class LayerMappingPreview
    {
        public string SourceFile { get; set; }
        public List<LayerMapping> Mappings { get; set; } = new();
        public LayerStatistics Statistics { get; set; }
        public string Error { get; set; }
    }

    public class LayerStatistics
    {
        public int TotalLayers { get; set; }
        public int MappedByPattern { get; set; }
        public int MappedByConfig { get; set; }
        public int UnmappedLayers { get; set; }
    }

    public class LayerMappingConfiguration
    {
        private readonly Dictionary<string, RevitCategory> _mappings = new(StringComparer.OrdinalIgnoreCase);

        public LayerMappingConfiguration()
        {
            // Load default mappings
            LoadDefaultMappings();
        }

        private void LoadDefaultMappings()
        {
            // AutoCAD standard layer naming conventions
            _mappings["A-WALL"] = RevitCategory.Walls;
            _mappings["A-WALL-EXTR"] = RevitCategory.Walls;
            _mappings["A-WALL-INT"] = RevitCategory.Walls;
            _mappings["A-DOOR"] = RevitCategory.Doors;
            _mappings["A-WINDOW"] = RevitCategory.Windows;
            _mappings["A-GLAZ"] = RevitCategory.Windows;
            _mappings["A-CLNG"] = RevitCategory.Ceilings;
            _mappings["A-FLOR"] = RevitCategory.Floors;
            _mappings["A-ROOF"] = RevitCategory.Roofs;
            _mappings["A-COLS"] = RevitCategory.Columns;
            _mappings["A-FURN"] = RevitCategory.Furniture;
            _mappings["A-EQPM"] = RevitCategory.SpecialityEquipment;
            _mappings["A-STRS"] = RevitCategory.Stairs;
            _mappings["S-COLS"] = RevitCategory.StructuralColumns;
            _mappings["S-BEAM"] = RevitCategory.StructuralFraming;
            _mappings["S-FNDN"] = RevitCategory.StructuralFoundation;
            _mappings["S-GRID"] = RevitCategory.Grids;
            _mappings["M-DUCT"] = RevitCategory.DuctSystems;
            _mappings["M-PIPE"] = RevitCategory.PipeSystems;
            _mappings["P-FIXT"] = RevitCategory.PlumbingFixtures;
            _mappings["P-PIPE"] = RevitCategory.PipeSystems;
            _mappings["E-LITE"] = RevitCategory.LightingFixtures;
            _mappings["E-POWR"] = RevitCategory.ElectricalFixtures;
            _mappings["E-PANL"] = RevitCategory.ElectricalEquipment;
        }

        public bool TryGetMapping(string layerName, out RevitCategory category)
        {
            return _mappings.TryGetValue(layerName, out category);
        }

        public void AddMapping(string layerName, RevitCategory category)
        {
            _mappings[layerName] = category;
        }
    }

    // Converted Elements
    public class ConvertedElement
    {
        public string Id { get; set; }
        public RevitCategory Category { get; set; }
        public string TypeName { get; set; }
        public string SourceLayer { get; set; }
        public string SourceBlockName { get; set; }
        public string HostElementId { get; set; }
        public IElementGeometry Geometry { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();

        public string GetGeometryHash() => Geometry?.GetGeometryHash() ?? "";
        public ConvertedElement Clone() => new ConvertedElement
        {
            Id = Id,
            Category = Category,
            TypeName = TypeName,
            SourceLayer = SourceLayer,
            SourceBlockName = SourceBlockName,
            HostElementId = HostElementId,
            Geometry = Geometry,
            Parameters = new Dictionary<string, string>(Parameters)
        };
    }

    public class ElementConversionResult
    {
        public List<ConvertedElement> Elements { get; set; } = new();
    }

    // Processed Geometry
    public class ProcessedGeometry
    {
        public CADFileEntity SourceEntity { get; set; }
        public RevitCategory TargetCategory { get; set; }
        public IElementGeometry Geometry { get; set; }
        public GeometryType GeometryType { get; set; }
    }

    // Recognized Blocks
    public class RecognizedBlock
    {
        public string BlockName { get; set; }
        public BlockElementType ElementType { get; set; }
        public Point3D InsertionPoint { get; set; }
        public double Rotation { get; set; }
        public Vector3D Scale { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
        public string Layer { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    // Extracted Text and Dimensions
    public class ExtractedText
    {
        public string Content { get; set; }
        public Point3D Position { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public string Style { get; set; }
        public string Layer { get; set; }
        public CADTextType TextType { get; set; }
    }

    public class ExtractedDimension
    {
        public double Value { get; set; }
        public string Text { get; set; }
        public DimensionType DimensionType { get; set; }
        public Point3D DefinitionPoint { get; set; }
        public Point3D ExtLine1Start { get; set; }
        public Point3D ExtLine2Start { get; set; }
        public string Layer { get; set; }
    }

    // DWG Header
    internal class DWGHeader
    {
        public DWGVersion Version { get; set; }
    }

    #endregion

    #region Enumerations

    public enum RevitCategory
    {
        Walls,
        Doors,
        Windows,
        Columns,
        StructuralColumns,
        StructuralFraming,
        StructuralFoundation,
        Floors,
        Ceilings,
        Roofs,
        Stairs,
        Railings,
        Furniture,
        SpecialityEquipment,
        PlumbingFixtures,
        MechanicalEquipment,
        ElectricalEquipment,
        ElectricalFixtures,
        LightingFixtures,
        AirTerminals,
        DuctSystems,
        PipeSystems,
        CableTray,
        Grids,
        Levels,
        Rooms,
        Dimensions,
        TextNotes,
        Tags,
        GenericAnnotation,
        Topography,
        Parking,
        Roads,
        Planting,
        Entourage,
        GenericModel
    }

    public enum MappingSource
    {
        Explicit,
        PatternMatch,
        Configuration,
        Default
    }

    public enum GeometryType
    {
        Line,
        OpenPolyline,
        ClosedPolyline,
        Circle,
        Arc,
        Ellipse,
        Solid,
        Point
    }

    public enum BlockElementType
    {
        // Doors
        Door,
        DoubleDoor,
        SlidingDoor,
        FoldingDoor,
        RevolvingDoor,
        OverheadDoor,

        // Windows
        Window,
        CasementWindow,
        SlidingWindow,
        FixedWindow,
        HungWindow,
        CurtainWall,
        Skylight,

        // Furniture
        Desk,
        Chair,
        Sofa,
        Bed,
        Cabinet,
        Shelving,

        // Plumbing
        Toilet,
        Sink,
        Bathtub,
        Shower,
        Urinal,
        Bidet,
        PlumbingFixture,

        // MEP
        AirHandler,
        FanCoil,
        Diffuser,
        Pump,
        Boiler,
        Chiller,
        WaterHeater,

        // Electrical
        Switch,
        Outlet,
        LightFixture,
        ElectricalPanel,
        Transformer,

        // Structural
        Column,
        Footing,

        // Site
        Tree,
        Shrub,
        Vehicle,
        EntouragePerson
    }

    public enum CADTextType
    {
        RoomLabel,
        GridLabel,
        LevelLabel,
        DimensionText,
        Annotation
    }

    public enum DimensionType
    {
        Linear = 0,
        Aligned = 1,
        Angular = 2,
        Diameter = 3,
        Radius = 4,
        Ordinate = 6
    }

    public enum CADUnits
    {
        Unitless = 0,
        Inches = 1,
        Feet = 2,
        Millimeters = 4,
        Centimeters = 5,
        Meters = 6
    }

    public enum DWGVersion
    {
        Unknown,
        AutoCAD14,
        AutoCAD2000,
        AutoCAD2004,
        AutoCAD2007,
        AutoCAD2010,
        AutoCAD2013,
        AutoCAD2018
    }

    #endregion
}
